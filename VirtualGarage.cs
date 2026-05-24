using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using VirtualGarage.Database;
using VirtualGarage.Models;
using Logger = Rocket.Core.Logging.Logger;

namespace VirtualGarage
{
    public sealed class VirtualGarage : RocketPlugin<VirtualGarageConfiguration>
    {
        public static VirtualGarage Instance { get; private set; }
        public IGarageStore Store { get; private set; }
        public VirtualGarageConfiguration Conf => Configuration.Instance;

        public enum StoreOutcome { Ok, NameExists, LimitReached, AssetMissing, DbError, NotOwner, HasMountedStorage }
        public enum RetrieveOutcome { Ok, NotFound, AssetMissing, DbError }

        /// <summary>Active "stand and wait" store channels, keyed by player.</summary>
        private readonly Dictionary<CSteamID, StoreChannel> _channels = new Dictionary<CSteamID, StoreChannel>();
        private readonly List<StoreChannel> _channelScratch = new List<StoreChannel>();

        private sealed class StoreChannel
        {
            public UnturnedPlayer Player;
            public ulong OwnerId;
            public string Name;
            public InteractableVehicle Vehicle;
            public float Remaining;
            public float TickAccumulator;
            public Vector3 StartPosition;
            public bool BypassLock;     // admin started the store -> skip the locked-by-other guard
        }

        protected override void Load()
        {
            Instance = this;

            string mode = (Conf.StorageMode ?? "AUTO").Trim().ToUpperInvariant();

            if (mode == "FILE")
            {
                Store = new GarageFileStore(Directory);
                Store.Initialize();
                Logger.Log("VirtualGarage loaded. Storage = FILE (" + Conf.TableName + ").");
                return;
            }

            GarageDatabase db = new GarageDatabase(Conf);
            if (db.Initialize())
            {
                Store = db;
                Logger.Log("VirtualGarage loaded. Storage = MySQL OK (table '" + Conf.TableName + "').");
            }
            else if (mode == "AUTO")
            {
                Store = new GarageFileStore(Directory);
                Store.Initialize();
                Logger.LogWarning("VirtualGarage: MySQL unavailable - falling back to FILE storage (VirtualGarage.data.xml).");
            }
            else
            {
                // Explicit MYSQL mode but it failed; keep the DB store so commands report a DB error.
                Store = db;
                Logger.LogError("VirtualGarage loaded BUT MySQL init failed - check the database settings (StorageMode=MYSQL).");
            }
        }

        protected override void Unload()
        {
            _channels.Clear();
            _channelScratch.Clear();
            Instance = null;
            Store = null;
            Logger.Log("VirtualGarage unloaded.");
        }

        // ----------------------------------------------------------------
        //  Vehicle targeting
        // ----------------------------------------------------------------

        /// <summary>The vehicle the player is sitting in, or the one they are looking at.</summary>
        public InteractableVehicle GetTargetVehicle(UnturnedPlayer player)
        {
            InteractableVehicle inside = player.Player.movement.getVehicle();
            if (inside != null)
                return inside;

            Transform aim = player.Player.look.aim;
            if (Physics.Raycast(aim.position, aim.forward, out RaycastHit hit, Conf.InteractDistance, RayMasks.VEHICLE))
                return hit.transform.GetComponentInParent<InteractableVehicle>();

            return null;
        }

        public bool IsOccupied(InteractableVehicle vehicle)
        {
            if (vehicle?.passengers == null)
                return false;
            foreach (Passenger seat in vehicle.passengers)
                if (seat != null && seat.player != null)
                    return true;
            return false;
        }

        public Vector3 GetSpawnPoint(UnturnedPlayer player, out Quaternion rotation)
        {
            Transform t = player.Player.transform;
            rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            return t.position + t.forward * 5f + Vector3.up * 1.5f;
        }

        // ----------------------------------------------------------------
        //  Store / retrieve / list / delete
        // ----------------------------------------------------------------

        /// <summary>How many seconds the player must stand and wait to store this vehicle (0 = instant).</summary>
        public float GetStoreSeconds(ushort vehicleId)
        {
            if (Conf.StoreChannelTimes != null)
                foreach (VehicleStoreTime t in Conf.StoreChannelTimes)
                    if (t != null && t.Id == vehicleId)
                        return t.Seconds;
            return Conf.StoreChannelDefaultSeconds;
        }

        /// <summary>
        /// Player-facing /gadd entry point. Starts a stand-and-wait channel if this vehicle has a
        /// store time, otherwise stores immediately. Handles all messaging.
        /// </summary>
        public void BeginStore(IRocketPlayer caller, UnturnedPlayer player, string name, InteractableVehicle vehicle, bool bypassLock)
        {
            ulong ownerId = player.CSteamID.m_SteamID;

            // Fail fast on the cheap checks before making the player wait.
            try
            {
                if (Store.Count(ownerId) >= Conf.MaxVehiclesPerPlayer)
                {
                    Err(caller, string.Format(Conf.MsgLimitReached, Conf.MaxVehiclesPerPlayer));
                    return;
                }
                if (Store.Exists(ownerId, name))
                {
                    Err(caller, string.Format(Conf.MsgNameExists, name));
                    return;
                }
            }
            catch
            {
                Err(caller, Conf.MsgDbError);
                return;
            }

            float seconds = GetStoreSeconds(vehicle.id);
            if (seconds <= 0f)
            {
                FinishStore(caller, ownerId, name, vehicle, bypassLock);
                return;
            }

            if (_channels.ContainsKey(player.CSteamID))
            {
                Err(caller, Conf.MsgAlreadyStoring);
                return;
            }

            _channels[player.CSteamID] = new StoreChannel
            {
                Player = player,
                OwnerId = ownerId,
                Name = name,
                Vehicle = vehicle,
                Remaining = seconds,
                TickAccumulator = 0f,
                StartPosition = player.Position,
                BypassLock = bypassLock
            };
            Info(caller, string.Format(Conf.MsgStoreStarting, Mathf.CeilToInt(seconds)));
        }

        private void FinishStore(IRocketPlayer caller, ulong ownerId, string name, InteractableVehicle vehicle, bool bypassLock)
        {
            switch (StoreVehicle(ownerId, name, vehicle, bypassLock))
            {
                case StoreOutcome.Ok: Ok(caller, string.Format(Conf.MsgStored, name)); break;
                case StoreOutcome.NameExists: Err(caller, string.Format(Conf.MsgNameExists, name)); break;
                case StoreOutcome.LimitReached: Err(caller, string.Format(Conf.MsgLimitReached, Conf.MaxVehiclesPerPlayer)); break;
                case StoreOutcome.NotOwner: Err(caller, Conf.MsgNotOwner); break;
                case StoreOutcome.HasMountedStorage: Err(caller, MountedStorageMessage()); break;
                default: Err(caller, Conf.MsgDbError); break;
            }
        }

        public string MountedStorageMessage()
        {
            return string.IsNullOrEmpty(Conf.MsgHasMountedStorage)
                ? "This vehicle has a mounted safe/locker and cannot be stored | รถคันนี้มีตู้เซฟ/ตู้เก็บของติดอยู่ เก็บไม่ได้"
                : Conf.MsgHasMountedStorage;
        }

        private void FixedUpdate()
        {
            if (_channels.Count == 0)
                return;

            float dt = Time.fixedDeltaTime;
            _channelScratch.Clear();
            _channelScratch.AddRange(_channels.Values);

            foreach (StoreChannel ch in _channelScratch)
            {
                Player p = ch.Player?.Player;

                // Player gone, dead, or vehicle gone -> cancel silently.
                if (p == null || p.life == null || p.life.isDead || ch.Vehicle == null || ch.Vehicle.isDead)
                {
                    _channels.Remove(ch.Player != null ? ch.Player.CSteamID : default(CSteamID));
                    continue;
                }

                // Moved too far -> cancel.
                if ((p.transform.position - ch.StartPosition).sqrMagnitude >
                    Conf.StoreChannelMoveCancelDistance * Conf.StoreChannelMoveCancelDistance)
                {
                    _channels.Remove(ch.Player.CSteamID);
                    Err(ch.Player, Conf.MsgStoreCancelledMoved);
                    continue;
                }

                ch.Remaining -= dt;
                ch.TickAccumulator += dt;
                if (ch.TickAccumulator >= 1f)
                {
                    ch.TickAccumulator -= 1f;
                    Info(ch.Player, string.Format(Conf.MsgStoreChanneling, Mathf.Max(0, Mathf.CeilToInt(ch.Remaining))));
                    if (Conf.StoreChannelSoundEffectID != 0)
                        TriggerSound(Conf.StoreChannelSoundEffectID, ch.Vehicle.transform.position);
                }

                if (ch.Remaining <= 0f)
                {
                    _channels.Remove(ch.Player.CSteamID);
                    FinishStore(ch.Player, ch.OwnerId, ch.Name, ch.Vehicle, ch.BypassLock);
                }
            }
        }

        private static void TriggerSound(ushort effectId, Vector3 position)
        {
            EffectAsset asset = Assets.find(EAssetType.EFFECT, effectId) as EffectAsset;
            if (asset == null)
                return;
            TriggerEffectParameters parameters = new TriggerEffectParameters(asset)
            {
                position = position,
                relevantDistance = 48f,
                reliable = true
            };
            EffectManager.triggerEffect(parameters);
        }

        public StoreOutcome StoreVehicle(ulong ownerId, string name, InteractableVehicle vehicle, bool bypassLock)
        {
            try
            {
                // Locked by someone else -> refuse (re-checked here so a vehicle locked DURING a
                // stand-and-wait channel can't slip through). Admins bypass via bypassLock.
                if (!bypassLock && vehicle.isLocked &&
                    vehicle.lockedOwner.m_SteamID != 0 && vehicle.lockedOwner.m_SteamID != ownerId)
                    return StoreOutcome.NotOwner;

                // Optionally refuse vehicles carrying a mounted safe / locker / storage barricade.
                if (Conf.BlockStoreWithMountedStorage && VehicleSerializer.HasMountedStorage(vehicle))
                    return StoreOutcome.HasMountedStorage;

                if (Store.Count(ownerId) >= Conf.MaxVehiclesPerPlayer)
                    return StoreOutcome.LimitReached;
                if (Store.Exists(ownerId, name))
                    return StoreOutcome.NameExists;

                StoredVehicle sv = Capture(ownerId, name, vehicle);
                Store.Add(sv);

                // Empty the trunk + mounted safes/lockers so their items don't spill on the ground when
                // the vehicle is destroyed (everything is already saved and returns on retrieve).
                if (Conf.SaveTrunkContents && vehicle.trunkItems != null)
                    vehicle.trunkItems.clear();
                if (Conf.SaveVehicleDecorations)
                    VehicleSerializer.ClearMountedStorages(vehicle);

                VehicleManager.askVehicleDestroy(vehicle);
                return StoreOutcome.Ok;
            }
            catch (Exception ex)
            {
                Logger.LogError("[VirtualGarage] StoreVehicle failed: " + ex);
                return StoreOutcome.DbError;
            }
        }

        /// <summary>Adds a vehicle to a garage directly from an asset id, with default full state.</summary>
        public StoreOutcome GiveVehicle(ulong ownerId, string name, ushort vehicleId)
        {
            try
            {
                VehicleAsset asset = Assets.find(EAssetType.VEHICLE, vehicleId) as VehicleAsset;
                if (asset == null)
                    return StoreOutcome.AssetMissing;
                if (Store.Count(ownerId) >= Conf.MaxVehiclesPerPlayer)
                    return StoreOutcome.LimitReached;
                if (Store.Exists(ownerId, name))
                    return StoreOutcome.NameExists;

                StoredVehicle sv = new StoredVehicle
                {
                    SteamId = ownerId,
                    Name = name,
                    VehicleGuid = asset.GUID != Guid.Empty ? asset.GUID.ToString("N") : "",
                    LegacyId = vehicleId,
                    SkinId = 0,
                    PaintColor = 0,
                    Fuel = asset.fuel,
                    Health = asset.health,
                    Battery = 10000,
                    TireMask = 255,
                    Locked = false
                };
                Store.Add(sv);
                return StoreOutcome.Ok;
            }
            catch (Exception ex)
            {
                Logger.LogError("[VirtualGarage] GiveVehicle failed: " + ex);
                return StoreOutcome.DbError;
            }
        }

        public RetrieveOutcome RetrieveVehicle(ulong ownerId, string name, Vector3 point, Quaternion rotation)
        {
            try
            {
                StoredVehicle sv = Store.Get(ownerId, name);
                if (sv == null)
                    return RetrieveOutcome.NotFound;

                InteractableVehicle vehicle = Spawn(sv, point, rotation);
                if (vehicle == null)
                    return RetrieveOutcome.AssetMissing;

                Store.Delete(ownerId, name);
                return RetrieveOutcome.Ok;
            }
            catch (Exception ex)
            {
                Logger.LogError("[VirtualGarage] RetrieveVehicle failed: " + ex);
                return RetrieveOutcome.DbError;
            }
        }

        public List<StoredVehicle> ListVehicles(ulong ownerId)
        {
            List<StoredVehicle> rows = Store.List(ownerId);
            foreach (StoredVehicle sv in rows)
                sv.DisplayName = ResolveDisplayName(sv);
            return rows;
        }

        public bool DeleteVehicle(ulong ownerId, string name)
        {
            return Store.Delete(ownerId, name);
        }

        // ----------------------------------------------------------------
        //  Capture / spawn helpers
        // ----------------------------------------------------------------

        private StoredVehicle Capture(ulong ownerId, string name, InteractableVehicle v)
        {
            StoredVehicle sv = new StoredVehicle
            {
                SteamId = ownerId,
                Name = name,
                VehicleGuid = v.asset != null && v.asset.GUID != Guid.Empty ? v.asset.GUID.ToString("N") : "",
                LegacyId = v.id,
                SkinId = v.skinID,
                PaintColor = PackColor(v.PaintColor),
                Fuel = v.fuel,
                Health = v.health,
                Battery = v.batteryCharge,
                TireMask = v.tireAliveMask,
                Locked = v.isLocked,
                LockedOwner = v.lockedOwner.m_SteamID,
                LockedGroup = v.lockedGroup.m_SteamID
            };

            if (Conf.SaveTrunkContents)
                sv.TrunkBlob = VehicleSerializer.SerializeTrunk(v.trunkItems);
            if (Conf.SaveVehicleDecorations)
                sv.BarricadeBlob = VehicleSerializer.SerializeBarricades(v);

            return sv;
        }

        private InteractableVehicle Spawn(StoredVehicle sv, Vector3 point, Quaternion rotation)
        {
            VehicleAsset asset = ResolveVehicleAsset(sv);
            if (asset == null)
                return null;

            Color32 paint = UnpackColor(sv.PaintColor);
            Color32? paintArg = (sv.PaintColor == 0u) ? (Color32?)null : paint;

            InteractableVehicle v = VehicleManager.spawnVehicleV2(asset, point, rotation, paintArg);
            if (v == null)
                return null;

            // Trunk items can be restored immediately - the container exists as soon as the
            // vehicle spawns and is not reset by vehicle initialization.
            if (Conf.SaveTrunkContents)
                VehicleSerializer.RestoreTrunk(v, sv.TrunkBlob);

            // Fuel / health / battery / tire / skin / lock + mounted barricades must be applied a
            // moment AFTER the vehicle exists. A freshly-spawned vehicle re-initializes itself to
            // FULL fuel/health/battery on its first frame(s), so setting these synchronously here
            // gets overwritten and the vehicle returns at 100%. Defer until it is ready.
            StartCoroutine(RestoreStateDelayed(v, sv));

            return v;
        }

        private System.Collections.IEnumerator RestoreStateDelayed(InteractableVehicle vehicle, StoredVehicle sv)
        {
            yield return new WaitForSeconds(0.5f);
            if (vehicle == null || vehicle.isDead)
                yield break;

            // Apply persisted condition now that the vehicle has finished initializing.
            // BOTH calls per stat are required, they do complementary halves:
            //   vehicle.tell*            -> sets the authoritative server-side field (so fuel burn,
            //                               and the state re-sync that happens when a player enters/
            //                               exits, use the correct value) but does NOT broadcast.
            //   VehicleManager.sendVehicle* -> broadcasts to clients so the dashboard gauges refresh
            //                               immediately, but does NOT set the server field.
            // Using only tell* leaves the gauge stuck at 100% until you drive; using only sendVehicle*
            // shows the right value until you drive/exit, then it snaps back to 100%. Do both.
            vehicle.tellFuel(sv.Fuel);
            VehicleManager.sendVehicleFuel(vehicle, sv.Fuel);
            vehicle.tellHealth(sv.Health);
            VehicleManager.sendVehicleHealth(vehicle, sv.Health);
            if (vehicle.usesBattery)
            {
                vehicle.tellBatteryCharge(sv.Battery);
                VehicleManager.sendVehicleBatteryCharge(vehicle, sv.Battery);
            }
            VehicleManager.sendVehicleTireAliveMask(vehicle, sv.TireMask);
            if (sv.SkinId != 0)
                vehicle.tellSkin(sv.SkinId, 0);
            if (sv.Locked)
            {
                CSteamID owner = new CSteamID(sv.LockedOwner);
                CSteamID group = new CSteamID(sv.LockedGroup);
                // Prefer the owner's CURRENT group so their current party can enter the retrieved
                // vehicle (the saved group may be stale, e.g. they joined a new party).
                try
                {
                    UnturnedPlayer op = UnturnedPlayer.FromCSteamID(owner);
                    if (op != null && op.Player != null && op.Player.quests != null)
                    {
                        CSteamID g = op.Player.quests.groupID;
                        if (g != CSteamID.Nil) group = g;
                    }
                }
                catch { }
                // ServerSetVehicleLock (NOT tellLocked) sets the field AND replicates, so the lock
                // and its group are known to every client - otherwise party members can't enter.
                VehicleManager.ServerSetVehicleLock(vehicle, owner, group, true);
            }

            if (Conf.SaveVehicleDecorations && !string.IsNullOrEmpty(sv.BarricadeBlob))
            {
                int restored = VehicleSerializer.RestoreBarricades(vehicle, sv.BarricadeBlob);
                Logger.Log("[VirtualGarage] Restored " + restored + " decoration(s) onto vehicle.");
            }
        }

        private static VehicleAsset ResolveVehicleAsset(StoredVehicle sv)
        {
            if (!string.IsNullOrEmpty(sv.VehicleGuid) && Guid.TryParse(sv.VehicleGuid, out Guid g))
            {
                VehicleAsset byGuid = Assets.find(g) as VehicleAsset;
                if (byGuid != null)
                    return byGuid;
            }
            return Assets.find(EAssetType.VEHICLE, sv.LegacyId) as VehicleAsset;
        }

        private static string ResolveDisplayName(StoredVehicle sv)
        {
            VehicleAsset a = ResolveVehicleAsset(sv);
            return a != null ? a.vehicleName : ("#" + sv.LegacyId);
        }

        private static uint PackColor(Color32 c)
        {
            return ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
        }

        private static Color32 UnpackColor(uint v)
        {
            return new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        // ----------------------------------------------------------------
        //  Target resolution + messaging
        // ----------------------------------------------------------------

        /// <summary>Resolves a SteamID from a 17-digit id or an online player's name.</summary>
        public bool ResolveTarget(string input, out ulong steamId, out string display)
        {
            steamId = 0;
            display = input;

            if (input.Length >= 17 && ulong.TryParse(input, out ulong sid))
            {
                steamId = sid;
                UnturnedPlayer online = UnturnedPlayer.FromCSteamID(new CSteamID(sid));
                display = online != null ? online.DisplayName : input;
                return true;
            }

            UnturnedPlayer byName = UnturnedPlayer.FromName(input);
            if (byName != null)
            {
                steamId = byName.CSteamID.m_SteamID;
                display = byName.DisplayName;
                return true;
            }
            return false;
        }

        public void Ok(IRocketPlayer to, string message) => Say(to, message, Conf.ColorSuccess);
        public void Err(IRocketPlayer to, string message) => Say(to, message, Conf.ColorError);
        public void Info(IRocketPlayer to, string message) => Say(to, message, Conf.ColorInfo);

        public void Say(IRocketPlayer to, string message, string colorName)
        {
            if (to == null || string.IsNullOrEmpty(message))
                return;
            Color color = UnturnedChat.GetColorFromName(colorName, Color.white);
            UnturnedChat.Say(to, message, color);
        }
    }
}
