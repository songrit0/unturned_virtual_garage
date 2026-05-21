using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Player;
using UnityEngine;

namespace VirtualGarage.Commands
{
    /// <summary>/gadminretrieve [player/ID] [name] - spawn a vehicle from a player's garage to the admin.</summary>
    public sealed class CommandGAdminRetrieve : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "gadminretrieve";
        public string Help => "Spawn a vehicle from another player's garage to yourself.";
        public string Syntax => "<player/ID> <name>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "gadminretrieve" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            VirtualGarage vg = VirtualGarage.Instance;
            UnturnedPlayer admin = (UnturnedPlayer)caller;

            if (command.Length < 2)
            {
                vg.Err(caller, "Usage: /gadminretrieve <player/ID> <name>");
                return;
            }

            if (!vg.ResolveTarget(command[0], out ulong steamId, out string display))
            {
                vg.Err(caller, string.Format(vg.Conf.MsgPlayerNotFound, command[0]));
                return;
            }

            string name = string.Join(" ", new ArraySegment<string>(command, 1, command.Length - 1)).Trim();

            Vector3 point = vg.GetSpawnPoint(admin, out Quaternion rotation);

            switch (vg.RetrieveVehicle(steamId, name, point, rotation))
            {
                case VirtualGarage.RetrieveOutcome.Ok:
                    vg.Ok(caller, string.Format(vg.Conf.MsgAdminRetrieved, name, display));
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
