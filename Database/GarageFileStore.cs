using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Rocket.Core.Logging;
using VirtualGarage.Models;

namespace VirtualGarage.Database
{
    /// <summary>
    /// File-based garage storage (XML in the plugin folder). Used when no SQL is available
    /// (StorageMode = FILE, or AUTO fallback when MySQL can't connect).
    /// All access is on the main thread, so no locking is needed.
    /// </summary>
    public sealed class GarageFileStore : IGarageStore
    {
        [XmlRoot("Garage")]
        public sealed class GarageData
        {
            public List<StoredVehicle> Vehicles = new List<StoredVehicle>();
        }

        private readonly string _path;
        private GarageData _data = new GarageData();
        private long _nextId = 1;

        public GarageFileStore(string directory)
        {
            _path = Path.Combine(directory, "VirtualGarage.data.xml");
        }

        public bool Initialize()
        {
            try
            {
                if (File.Exists(_path))
                {
                    XmlSerializer ser = new XmlSerializer(typeof(GarageData));
                    using (FileStream fs = File.OpenRead(_path))
                        _data = (GarageData)ser.Deserialize(fs);

                    if (_data == null)
                        _data = new GarageData();
                    if (_data.Vehicles == null)
                        _data.Vehicles = new List<StoredVehicle>();

                    foreach (StoredVehicle v in _data.Vehicles)
                        if (v.Id >= _nextId)
                            _nextId = v.Id + 1;
                }
                else
                {
                    _data = new GarageData();
                    Save();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("[VirtualGarage] File store init failed: " + ex.Message);
                return false;
            }
        }

        private void Save()
        {
            XmlSerializer ser = new XmlSerializer(typeof(GarageData));
            using (FileStream fs = File.Create(_path))
                ser.Serialize(fs, _data);
        }

        public int Count(ulong steamId)
        {
            return _data.Vehicles.Count(v => v.SteamId == steamId);
        }

        public bool Exists(ulong steamId, string name)
        {
            return _data.Vehicles.Any(v => v.SteamId == steamId &&
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public void Add(StoredVehicle vehicle)
        {
            vehicle.Id = _nextId++;
            _data.Vehicles.Add(vehicle);
            Save();
        }

        public List<StoredVehicle> List(ulong steamId)
        {
            return _data.Vehicles
                .Where(v => v.SteamId == steamId)
                .OrderBy(v => v.Name)
                .ToList();
        }

        public StoredVehicle Get(ulong steamId, string name)
        {
            return _data.Vehicles.FirstOrDefault(v => v.SteamId == steamId &&
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public bool Delete(ulong steamId, string name)
        {
            int removed = _data.Vehicles.RemoveAll(v => v.SteamId == steamId &&
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                Save();
            return removed > 0;
        }
    }
}
