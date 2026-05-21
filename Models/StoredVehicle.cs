using System;
using System.Xml.Serialization;

namespace VirtualGarage.Models
{
    /// <summary>A vehicle row stored in a player's garage.</summary>
    public sealed class StoredVehicle
    {
        public long Id;
        public ulong SteamId;
        public string Name;

        // Asset identity (GUID preferred for redirect-safety, legacy id as fallback).
        public string VehicleGuid;
        public ushort LegacyId;

        // Full state.
        public ushort SkinId;
        public uint PaintColor;     // packed RGBA (Color32)
        public ushort Fuel;
        public ushort Health;
        public ushort Battery;
        public byte TireMask;
        public bool Locked;
        public ulong LockedOwner;
        public ulong LockedGroup;

        // Base64 blobs (may be null/empty).
        public string TrunkBlob;
        public string BarricadeBlob;

        /// <summary>Human-readable vehicle name for listing (resolved from the asset, not persisted).</summary>
        [XmlIgnore]
        public string DisplayName;
    }
}
