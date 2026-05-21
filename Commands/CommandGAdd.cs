using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace VirtualGarage.Commands
{
    public sealed class CommandGAdd : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "gadd";
        public string Help => "Store the vehicle you are looking at into your garage.";
        public string Syntax => "<name>";
        public List<string> Aliases => new List<string> { "ga" };
        public List<string> Permissions => new List<string> { "gadd" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            VirtualGarage vg = VirtualGarage.Instance;
            UnturnedPlayer player = (UnturnedPlayer)caller;

            string name = string.Join(" ", command).Trim();
            if (string.IsNullOrEmpty(name))
            {
                vg.Err(caller, vg.Conf.MsgNameRequired);
                return;
            }

            InteractableVehicle vehicle = vg.GetTargetVehicle(player);
            if (vehicle == null)
            {
                vg.Err(caller, vg.Conf.MsgNoVehicle);
                return;
            }

            if (vehicle.isLocked && vehicle.lockedOwner != player.CSteamID && !player.IsAdmin)
            {
                vg.Err(caller, vg.Conf.MsgNotOwner);
                return;
            }

            if (!vg.Conf.AllowStoreWhileOccupied && vg.IsOccupied(vehicle))
            {
                vg.Err(caller, vg.Conf.MsgOccupied);
                return;
            }

            // Starts a stand-and-wait channel if the vehicle has a store time, else stores instantly.
            vg.BeginStore(caller, player, name, vehicle);
        }
    }
}
