using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace VirtualGarage.Commands
{
    /// <summary>/gadminadd [player/ID] [name] - store the vehicle the admin is looking at into a player's garage.</summary>
    public sealed class CommandGAdminAdd : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "gadminadd";
        public string Help => "Store the vehicle you are looking at into another player's garage.";
        public string Syntax => "<player/ID> <name>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "gadminadd" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            VirtualGarage vg = VirtualGarage.Instance;
            UnturnedPlayer admin = (UnturnedPlayer)caller;

            if (command.Length < 2)
            {
                vg.Err(caller, "Usage: /gadminadd <player/ID> <name>");
                return;
            }

            if (!vg.ResolveTarget(command[0], out ulong steamId, out string display))
            {
                vg.Err(caller, string.Format(vg.Conf.MsgPlayerNotFound, command[0]));
                return;
            }

            string name = string.Join(" ", new ArraySegment<string>(command, 1, command.Length - 1)).Trim();

            InteractableVehicle vehicle = vg.GetTargetVehicle(admin);
            if (vehicle == null)
            {
                vg.Err(caller, vg.Conf.MsgNoVehicle);
                return;
            }

            switch (vg.StoreVehicle(steamId, name, vehicle))
            {
                case VirtualGarage.StoreOutcome.Ok:
                    vg.Ok(caller, string.Format(vg.Conf.MsgAdminStored, name, display));
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
