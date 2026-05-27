using System;
using System.Collections.Generic;
using System.IO;
using SDG.Unturned;
using UnityEngine;

namespace VirtualGarage
{
    /// <summary>
    /// Serializes a vehicle's trunk contents and mounted barricades (lockers, safes, signs,
    /// including their stored items - which live inside the barricade <c>state</c> bytes) into
    /// compact base64 strings, and restores them onto a freshly spawned vehicle.
    /// </summary>
    public static class VehicleSerializer
    {
        private const byte TrunkVersion = 1;
        private const byte BarricadeVersion = 2;

        // ---------------- Trunk ----------------

        public static string SerializeTrunk(Items trunk)
        {
            if (trunk == null || trunk.getItemCount() == 0)
                return string.Empty;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(TrunkVersion);
                w.Write(trunk.width);
                w.Write(trunk.height);

                byte count = trunk.getItemCount();
                w.Write(count);

                for (byte i = 0; i < count; i++)
                {
                    ItemJar jar = trunk.getItem(i);
                    if (jar == null || jar.item == null)
                    {
                        // Placeholder so the count stays consistent.
                        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
                        w.Write((ushort)0); w.Write((byte)0); w.Write((byte)0);
                        w.Write((ushort)0);
                        continue;
                    }

                    w.Write(jar.x);
                    w.Write(jar.y);
                    w.Write(jar.rot);
                    w.Write(jar.item.id);
                    w.Write(jar.item.amount);
                    w.Write(jar.item.quality);
                    WriteBytes(w, jar.item.state);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static void RestoreTrunk(InteractableVehicle vehicle, string base64)
        {
            if (vehicle == null || vehicle.trunkItems == null || string.IsNullOrEmpty(base64))
                return;

            byte[] data = Convert.FromBase64String(base64);
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader r = new BinaryReader(ms))
            {
                byte version = r.ReadByte();
                if (version != TrunkVersion)
                    return;

                r.ReadByte(); // width  (vehicle defines its own trunk size)
                r.ReadByte(); // height
                byte count = r.ReadByte();

                vehicle.trunkItems.clear();

                for (byte i = 0; i < count; i++)
                {
                    byte x = r.ReadByte();
                    byte y = r.ReadByte();
                    byte rot = r.ReadByte();
                    ushort id = r.ReadUInt16();
                    byte amount = r.ReadByte();
                    byte quality = r.ReadByte();
                    byte[] state = ReadBytes(r);

                    if (id == 0 || amount == 0)
                        continue;

                    Item item = new Item(id, amount, quality, state ?? new byte[0]);
                    vehicle.trunkItems.addItem(x, y, rot, item);
                }
            }
        }

        // ---------------- Mounted barricades (decorations) ----------------

        public static string SerializeBarricades(InteractableVehicle vehicle)
        {
            if (vehicle == null)
                return string.Empty;

            List<BarricadeDrop> drops = CollectVehicleBarricades(vehicle);
            Rocket.Core.Logging.Logger.Log("[VirtualGarage] gadd: found " + drops.Count + " mounted barricade(s) to save.");
            if (drops.Count == 0)
                return string.Empty;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(BarricadeVersion);
                w.Write((ushort)drops.Count);

                foreach (BarricadeDrop drop in drops)
                {
                    BarricadeData bd = drop.GetServersideData();
                    Barricade b = bd.barricade;

                    Guid guid = drop.asset != null ? drop.asset.GUID : Guid.Empty;

                    // bd.point / bd.rotation are already LOCAL to the vehicle for planted barricades.
                    w.Write(guid.ToByteArray());           // 16 bytes
                    w.Write(drop.asset != null ? drop.asset.id : (ushort)0);
                    w.Write(b.health);
                    w.Write(bd.point.x); w.Write(bd.point.y); w.Write(bd.point.z);
                    w.Write(bd.rotation.x); w.Write(bd.rotation.y); w.Write(bd.rotation.z); w.Write(bd.rotation.w);
                    w.Write(bd.owner);
                    w.Write(bd.group);
                    WriteBytes(w, b.state);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static int RestoreBarricades(InteractableVehicle vehicle, string base64)
        {
            if (vehicle == null || string.IsNullOrEmpty(base64))
                return 0;

            int restored = 0;
            byte[] data = Convert.FromBase64String(base64);
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader r = new BinaryReader(ms))
            {
                byte version = r.ReadByte();
                if (version != BarricadeVersion)
                    return 0;

                ushort count = r.ReadUInt16();
                for (ushort i = 0; i < count; i++)
                {
                    Guid guid = new Guid(r.ReadBytes(16));
                    ushort id = r.ReadUInt16();
                    ushort health = r.ReadUInt16();
                    Vector3 localPoint = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    Quaternion localRotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    ulong owner = r.ReadUInt64();
                    ulong group = r.ReadUInt64();
                    byte[] state = ReadBytes(r);

                    ItemBarricadeAsset asset = ResolveBarricadeAsset(guid, id);
                    if (asset == null)
                        continue;

                    Barricade barricade = new Barricade(asset, health, state ?? new byte[0]);

                    // dropPlantedBarricade parents to the vehicle and uses LOCAL point/rotation.
                    Transform placed = BarricadeManager.dropPlantedBarricade(
                        vehicle.transform, barricade, localPoint, localRotation, owner, group);
                    if (placed != null)
                        restored++;
                }
            }
            return restored;
        }

        /// <summary>
        /// Empties mounted storage barricades (safes/lockers) so their items don't spill onto the
        /// ground when the vehicle is destroyed during /gadd. Their contents are already saved in the
        /// barricade state, so they come back on retrieve. Call this AFTER serializing, BEFORE destroying.
        /// </summary>
        public static void ClearMountedStorages(InteractableVehicle vehicle)
        {
            if (vehicle == null)
                return;

            foreach (BarricadeDrop drop in CollectVehicleBarricades(vehicle))
            {
                InteractableStorage storage = drop.interactable as InteractableStorage;
                if (storage != null && storage.items != null)
                    storage.items.clear();
            }
        }

        /// <summary>True if the vehicle has at least one mounted storage barricade (safe / locker).</summary>
        public static bool HasMountedStorage(InteractableVehicle vehicle)
        {
            if (vehicle == null)
                return false;

            foreach (BarricadeDrop drop in CollectVehicleBarricades(vehicle))
                if (drop.interactable is InteractableStorage)
                    return true;
            return false;
        }

        /// <summary>
        /// True if any mounted storage barricade on the vehicle currently contains at least one item.
        /// An empty safe/locker returns false (so it's allowed to be stored).
        /// </summary>
        public static bool HasItemsInMountedStorage(InteractableVehicle vehicle)
        {
            if (vehicle == null)
                return false;

            foreach (BarricadeDrop drop in CollectVehicleBarricades(vehicle))
            {
                InteractableStorage storage = drop.interactable as InteractableStorage;
                if (storage == null || storage.items == null)
                    continue;
                if (storage.items.getItemCount() > 0)
                    return true;
            }
            return false;
        }

        /// <summary>Gathers every barricade mounted on the vehicle, across all of its regions.</summary>
        private static List<BarricadeDrop> CollectVehicleBarricades(InteractableVehicle vehicle)
        {
            List<BarricadeDrop> drops = new List<BarricadeDrop>();

            System.Collections.Generic.IReadOnlyList<VehicleBarricadeRegion> regions = BarricadeManager.vehicleRegions;
            if (regions != null)
            {
                foreach (VehicleBarricadeRegion vr in regions)
                    if (vr != null && vr.vehicle == vehicle && vr.drops != null && vr.drops.Count > 0)
                        drops.AddRange(vr.drops);
            }

            if (drops.Count == 0)
            {
                VehicleBarricadeRegion r = BarricadeManager.findRegionFromVehicle(vehicle, 0);
                if (r != null && r.drops != null)
                    drops.AddRange(r.drops);
            }

            return drops;
        }

        // ---------------- Helpers ----------------

        private static ItemBarricadeAsset ResolveBarricadeAsset(Guid guid, ushort id)
        {
            if (guid != Guid.Empty)
            {
                ItemBarricadeAsset byGuid = Assets.find(guid) as ItemBarricadeAsset;
                if (byGuid != null)
                    return byGuid;
            }
            return Assets.find(EAssetType.ITEM, id) as ItemBarricadeAsset;
        }

        private static void WriteBytes(BinaryWriter w, byte[] bytes)
        {
            if (bytes == null)
            {
                w.Write((ushort)0);
                return;
            }
            w.Write((ushort)bytes.Length);
            w.Write(bytes);
        }

        private static byte[] ReadBytes(BinaryReader r)
        {
            ushort len = r.ReadUInt16();
            return len == 0 ? new byte[0] : r.ReadBytes(len);
        }
    }
}
