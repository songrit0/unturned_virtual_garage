using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Player;
using VirtualGarage.Models;

namespace VirtualGarage.Commands
{
    public sealed class CommandGarage : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "garage";
        public string Help => "List all vehicles in your garage.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "garage" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            VirtualGarage vg = VirtualGarage.Instance;
            UnturnedPlayer player = (UnturnedPlayer)caller;

            List<StoredVehicle> vehicles;
            try
            {
                vehicles = vg.ListVehicles(player.CSteamID.m_SteamID);
            }
            catch
            {
                vg.Err(caller, vg.Conf.MsgDbError);
                return;
            }

            if (vehicles.Count == 0)
            {
                vg.Info(caller, vg.Conf.MsgListEmpty);
                return;
            }

            vg.Info(caller, string.Format(vg.Conf.MsgListHeader, vehicles.Count, vg.Conf.MaxVehiclesPerPlayer));
            foreach (StoredVehicle v in vehicles)
                vg.Info(caller, string.Format(vg.Conf.MsgListItem, v.Name, v.DisplayName));
        }
    }
}
