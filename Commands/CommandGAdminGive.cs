using System;
using System.Collections.Generic;
using Rocket.API;

namespace VirtualGarage.Commands
{
    /// <summary>/gadmingive [player/ID] [VehicleID] [name] - add a vehicle to a player's garage straight from an asset id.</summary>
    public sealed class CommandGAdminGive : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "gadmingive";
        public string Help => "Add a vehicle directly (by vehicle id) to a player's garage.";
        public string Syntax => "<player/ID> <VehicleID> <name>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "gadmingive" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            VirtualGarage vg = VirtualGarage.Instance;

            if (command.Length < 3)
            {
                vg.Err(caller, "Usage: /gadmingive <player/ID> <VehicleID> <name>");
                return;
            }

            if (!vg.ResolveTarget(command[0], out ulong steamId, out string display))
            {
                vg.Err(caller, string.Format(vg.Conf.MsgPlayerNotFound, command[0]));
                return;
            }

            if (!ushort.TryParse(command[1], out ushort vehicleId) || vehicleId == 0)
            {
                vg.Err(caller, vg.Conf.MsgInvalidVehicleId);
                return;
            }

            string name = string.Join(" ", new ArraySegment<string>(command, 2, command.Length - 2)).Trim();

            switch (vg.GiveVehicle(steamId, name, vehicleId))
            {
                case VirtualGarage.StoreOutcome.Ok:
                    vg.Ok(caller, string.Format(vg.Conf.MsgAdminGiven, name, display));
                    break;
                case VirtualGarage.StoreOutcome.AssetMissing:
                    vg.Err(caller, vg.Conf.MsgInvalidVehicleId);
                    break;
                case VirtualGarage.StoreOutcome.NameExists:
                    vg.Err(caller, string.Format(vg.Conf.MsgNameExists, name));
                    break;
                case VirtualGarage.StoreOutcome.LimitReached:
                    vg.Err(caller, string.Format(vg.Conf.MsgLimitReached, vg.Conf.MaxVehiclesPerPlayer));
                    break;
                default:
                    vg.Err(caller, vg.Conf.MsgDbError);
                    break;
            }
        }
    }
}
