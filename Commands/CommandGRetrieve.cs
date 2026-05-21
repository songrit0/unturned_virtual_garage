using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Player;
using UnityEngine;

namespace VirtualGarage.Commands
{
    public sealed class CommandGRetrieve : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "gretrieve";
        public string Help => "Take a vehicle out of your garage.";
        public string Syntax => "<name>";
        public List<string> Aliases => new List<string> { "gr" };
        public List<string> Permissions => new List<string> { "gretrieve" };

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

            Vector3 point = vg.GetSpawnPoint(player, out Quaternion rotation);

            switch (vg.RetrieveVehicle(player.CSteamID.m_SteamID, name, point, rotation))
            {
                case VirtualGarage.RetrieveOutcome.Ok:
                    vg.Ok(caller, string.Format(vg.Conf.MsgRetrieved, name));
                    break;
                case VirtualGarage.RetrieveOutcome.NotFound:
                    vg.Err(caller, string.Format(vg.Conf.MsgNotFound, name));
                    break;
                default:
                    vg.Err(caller, vg.Conf.MsgDbError);
                    break;
            }
        }
    }
}
