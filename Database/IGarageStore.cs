using System.Collections.Generic;
using VirtualGarage.Models;

namespace VirtualGarage.Database
{
    /// <summary>Storage backend for the garage (implemented by MySQL and by a local XML file).</summary>
    public interface IGarageStore
    {
        bool Initialize();
        int Count(ulong steamId);
        bool Exists(ulong steamId, string name);
        void Add(StoredVehicle vehicle);
        List<StoredVehicle> List(ulong steamId);
        StoredVehicle Get(ulong steamId, string name);
        bool Delete(ulong steamId, string name);
    }
}
