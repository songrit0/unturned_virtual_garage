using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Player;

namespace VirtualGarage.Commands
{
    public sealed class CommandGarageDelete : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "garagedelete";
        public string Help => "Delete a vehicle from your garage without spawning it.";
        public string Syntax => "<name>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "garagedelete" };

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

            try
            {
                if (vg.DeleteVehicle(player.CSteamID.m_SteamID, name))
                    vg.Ok(caller, string.Format(vg.Conf.MsgDeleted, name));
                else
                    vg.Err(caller, string.Format(vg.Conf.MsgNotFound, name));
            }
            catch
            {
                vg.Err(caller, vg.Conf.MsgDbError);
            }
        }
    }
}
