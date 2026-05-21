using System.Collections.Generic;
using System.Xml.Serialization;
using Rocket.API;

namespace VirtualGarage
{
    /// <summary>Per-vehicle store channel time: <c>&lt;Vehicle Id="4125" Seconds="60" /&gt;</c></summary>
    public sealed class VehicleStoreTime
    {
        [XmlAttribute] public ushort Id;
        [XmlAttribute] public float Seconds;

        public VehicleStoreTime() { }
        public VehicleStoreTime(ushort id, float seconds) { Id = id; Seconds = seconds; }
    }

    /// <summary>
    /// Configuration for the Virtual Garage plugin.
    /// Stored at <c>Plugins/VirtualGarage/VirtualGarage.configuration.xml</c>.
    /// </summary>
    public sealed class VirtualGarageConfiguration : IRocketPluginConfiguration
    {
        // --- MySQL / MariaDB connection ---
        public string DatabaseHost;
        public ushort DatabasePort;
        public string DatabaseName;
        public string DatabaseUsername;
        public string DatabasePassword;
        public string TableName;

        // --- Behaviour ---
        /// <summary>Maximum vehicles a single player may keep in their garage.</summary>
        public int MaxVehiclesPerPlayer;

        /// <summary>How far (metres) the /gadd raycast looks for a vehicle.</summary>
        public float InteractDistance;

        /// <summary>Also save/restore items inside the vehicle trunk.</summary>
        public bool SaveTrunkContents;

        /// <summary>Also save/restore barricades mounted on the vehicle (lockers, safes, signs and their contents).</summary>
        public bool SaveVehicleDecorations;

        /// <summary>Allow storing a vehicle that still has players inside (they are ejected/destroyed with it).</summary>
        public bool AllowStoreWhileOccupied;

        // --- Store channel (stand-and-wait before /gadd completes) ---
        /// <summary>Seconds to stand and wait before /gadd stores a vehicle that isn't listed below. 0 = instant.</summary>
        public float StoreChannelDefaultSeconds;

        /// <summary>Per-vehicle-id channel times. Overrides the default for matching vehicle ids.</summary>
        public List<VehicleStoreTime> StoreChannelTimes;

        /// <summary>Sound effect played once/second while channeling a store (nearby players hear it). 0 = off.</summary>
        public ushort StoreChannelSoundEffectID;

        /// <summary>If the player moves further than this (metres) from where they started, the store cancels.</summary>
        public float StoreChannelMoveCancelDistance;

        // --- Colours (names like white/green/red/yellow or #RRGGBB) ---
        public string ColorSuccess;
        public string ColorError;
        public string ColorInfo;

        // --- Messages ({0}, {1} are substituted) ---
        public string MsgNoVehicle;
        public string MsgNotOwner;
        public string MsgOccupied;
        public string MsgNameRequired;
        public string MsgNameExists;
        public string MsgLimitReached;
        public string MsgStored;
        public string MsgNotFound;
        public string MsgRetrieved;
        public string MsgDeleted;
        public string MsgListHeader;
        public string MsgListItem;
        public string MsgListEmpty;
        public string MsgDbError;
        public string MsgPlayerNotFound;
        public string MsgInvalidVehicleId;
        public string MsgAdminStored;
        public string MsgAdminGiven;
        public string MsgAdminRetrieved;
        public string MsgStoreStarting;
        public string MsgStoreChanneling;
        public string MsgStoreCancelledMoved;
        public string MsgAlreadyStoring;

        public void LoadDefaults()
        {
            DatabaseHost = "127.0.0.1";
            DatabasePort = 3306;
            DatabaseName = "unturned";
            DatabaseUsername = "root";
            DatabasePassword = "";
            TableName = "virtual_garage";

            MaxVehiclesPerPlayer = 5;
            InteractDistance = 12f;
            SaveTrunkContents = true;
            SaveVehicleDecorations = true;
            AllowStoreWhileOccupied = false;

            StoreChannelDefaultSeconds = 0f;
            StoreChannelTimes = new List<VehicleStoreTime>
            {
                new VehicleStoreTime(4125, 60f)   // example: vehicle id 4125 takes 60s to store
            };
            StoreChannelSoundEffectID = 56;       // vanilla "Beep"
            StoreChannelMoveCancelDistance = 2f;

            ColorSuccess = "green";
            ColorError = "red";
            ColorInfo = "white";

            MsgNoVehicle = "No vehicle found - look at a vehicle (or sit in one) | ไม่พบรถ ให้มองที่รถหรือนั่งในรถ";
            MsgNotOwner = "You are not the owner of this vehicle | คุณไม่ใช่เจ้าของรถคันนี้";
            MsgOccupied = "Vehicle still has players inside | ยังมีผู้เล่นอยู่ในรถ";
            MsgNameRequired = "Usage: name required | ต้องระบุชื่อรถ";
            MsgNameExists = "You already have a vehicle named '{0}' | คุณมีรถชื่อ '{0}' อยู่แล้ว";
            MsgLimitReached = "Garage full ({0} max) | อู่เต็มแล้ว (สูงสุด {0} คัน)";
            MsgStored = "Stored vehicle as '{0}' | เก็บรถ '{0}' เข้าอู่แล้ว";
            MsgNotFound = "No vehicle named '{0}' in the garage | ไม่มีรถชื่อ '{0}' ในอู่";
            MsgRetrieved = "Retrieved '{0}' | นำรถ '{0}' ออกจากอู่แล้ว";
            MsgDeleted = "Deleted '{0}' from the garage | ลบรถ '{0}' จากอู่แล้ว";
            MsgListHeader = "Garage ({0}/{1}):";
            MsgListItem = "- {0} ({1})";
            MsgListEmpty = "Your garage is empty | อู่ของคุณว่างเปล่า";
            MsgDbError = "Database error, try again later | ฐานข้อมูลผิดพลาด ลองใหม่ภายหลัง";
            MsgPlayerNotFound = "Player not found: {0} | ไม่พบผู้เล่น: {0}";
            MsgInvalidVehicleId = "Invalid vehicle id | รหัสรถไม่ถูกต้อง";
            MsgAdminStored = "Stored a vehicle as '{0}' into {1}'s garage | เก็บรถ '{0}' เข้าอู่ของ {1}";
            MsgAdminGiven = "Added vehicle to {1}'s garage as '{0}' | เพิ่มรถ '{0}' เข้าอู่ของ {1}";
            MsgAdminRetrieved = "Retrieved '{0}' from {1}'s garage | นำรถ '{0}' จากอู่ของ {1} ออกมา";
            MsgStoreStarting = "Securing vehicle... stand still for {0}s | กำลังเก็บรถ... ยืนนิ่งๆ {0} วิ";
            MsgStoreChanneling = "Storing... {0}s left | กำลังเก็บ... เหลือ {0} วิ";
            MsgStoreCancelledMoved = "Store cancelled - you moved | ยกเลิกการเก็บ เพราะคุณขยับ";
            MsgAlreadyStoring = "You are already storing a vehicle | คุณกำลังเก็บรถอยู่แล้ว";
        }
    }
}
