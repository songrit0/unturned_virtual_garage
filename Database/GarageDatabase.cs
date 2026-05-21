using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Rocket.Core.Logging;
using VirtualGarage.Models;

namespace VirtualGarage.Database
{
    /// <summary>
    /// MySQL / MariaDB data access for the garage. One short-lived connection per call
    /// (simple and robust under Mono). All calls run on the main thread from command handlers;
    /// queries are indexed and tiny, so the blocking time is negligible.
    /// </summary>
    public sealed class GarageDatabase
    {
        private readonly VirtualGarageConfiguration _cfg;

        public GarageDatabase(VirtualGarageConfiguration cfg)
        {
            _cfg = cfg;
        }

        private string ConnectionString =>
            $"Server={_cfg.DatabaseHost};Port={_cfg.DatabasePort};Database={_cfg.DatabaseName};" +
            $"Uid={_cfg.DatabaseUsername};Pwd={_cfg.DatabasePassword};CharSet=utf8mb4;" +
            "Pooling=true;Connection Timeout=10;";

        private string Table => "`" + _cfg.TableName.Replace("`", "") + "`";

        /// <summary>Opens a connection and ensures the table exists. Returns false on failure.</summary>
        public bool Initialize()
        {
            try
            {
                using (MySqlConnection c = new MySqlConnection(ConnectionString))
                {
                    c.Open();
                    using (MySqlCommand cmd = c.CreateCommand())
                    {
                        cmd.CommandText =
                            "CREATE TABLE IF NOT EXISTS " + Table + " (" +
                            "`id` INT NOT NULL AUTO_INCREMENT," +
                            "`steam_id` BIGINT UNSIGNED NOT NULL," +
                            "`name` VARCHAR(64) NOT NULL," +
                            "`vehicle_guid` CHAR(32) NOT NULL DEFAULT ''," +
                            "`legacy_id` INT UNSIGNED NOT NULL DEFAULT 0," +
                            "`skin_id` INT UNSIGNED NOT NULL DEFAULT 0," +
                            "`paint_color` INT UNSIGNED NOT NULL DEFAULT 0," +
                            "`fuel` INT UNSIGNED NOT NULL DEFAULT 0," +
                            "`health` INT UNSIGNED NOT NULL DEFAULT 0," +
                            "`battery` INT UNSIGNED NOT NULL DEFAULT 0," +
                            "`tire_mask` TINYINT UNSIGNED NOT NULL DEFAULT 255," +
                            "`locked` TINYINT(1) NOT NULL DEFAULT 0," +
                            "`locked_owner` BIGINT UNSIGNED NOT NULL DEFAULT 0," +
                            "`locked_group` BIGINT UNSIGNED NOT NULL DEFAULT 0," +
                            "`trunk_blob` LONGTEXT NULL," +
                            "`barricade_blob` LONGTEXT NULL," +
                            "`created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP," +
                            "PRIMARY KEY (`id`)," +
                            "UNIQUE KEY `uq_player_name` (`steam_id`,`name`)" +
                            ") DEFAULT CHARSET=utf8mb4;";
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("[VirtualGarage] Database init failed: " + ex.Message);
                return false;
            }
        }

        public int Count(ulong steamId)
        {
            using (MySqlConnection c = new MySqlConnection(ConnectionString))
            {
                c.Open();
                using (MySqlCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM " + Table + " WHERE steam_id=@s;";
                    cmd.Parameters.AddWithValue("@s", steamId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public bool Exists(ulong steamId, string name)
        {
            using (MySqlConnection c = new MySqlConnection(ConnectionString))
            {
                c.Open();
                using (MySqlCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT 1 FROM " + Table + " WHERE steam_id=@s AND name=@n LIMIT 1;";
                    cmd.Parameters.AddWithValue("@s", steamId);
                    cmd.Parameters.AddWithValue("@n", name);
                    return cmd.ExecuteScalar() != null;
                }
            }
        }

        public void Add(StoredVehicle v)
        {
            using (MySqlConnection c = new MySqlConnection(ConnectionString))
            {
                c.Open();
                using (MySqlCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO " + Table + " (steam_id,name,vehicle_guid,legacy_id,skin_id,paint_color," +
                        "fuel,health,battery,tire_mask,locked,locked_owner,locked_group,trunk_blob,barricade_blob) " +
                        "VALUES (@s,@n,@g,@lid,@skin,@paint,@fuel,@hp,@bat,@tire,@lock,@lo,@lg,@trunk,@barr);";
                    cmd.Parameters.AddWithValue("@s", v.SteamId);
                    cmd.Parameters.AddWithValue("@n", v.Name);
                    cmd.Parameters.AddWithValue("@g", v.VehicleGuid ?? "");
                    cmd.Parameters.AddWithValue("@lid", v.LegacyId);
                    cmd.Parameters.AddWithValue("@skin", v.SkinId);
                    cmd.Parameters.AddWithValue("@paint", v.PaintColor);
                    cmd.Parameters.AddWithValue("@fuel", v.Fuel);
                    cmd.Parameters.AddWithValue("@hp", v.Health);
                    cmd.Parameters.AddWithValue("@bat", v.Battery);
                    cmd.Parameters.AddWithValue("@tire", v.TireMask);
                    cmd.Parameters.AddWithValue("@lock", v.Locked ? 1 : 0);
                    cmd.Parameters.AddWithValue("@lo", v.LockedOwner);
                    cmd.Parameters.AddWithValue("@lg", v.LockedGroup);
                    cmd.Parameters.AddWithValue("@trunk", (object)v.TrunkBlob ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@barr", (object)v.BarricadeBlob ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<StoredVehicle> List(ulong steamId)
        {
            List<StoredVehicle> result = new List<StoredVehicle>();
            using (MySqlConnection c = new MySqlConnection(ConnectionString))
            {
                c.Open();
                using (MySqlCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT name,vehicle_guid,legacy_id FROM " + Table +
                                      " WHERE steam_id=@s ORDER BY name;";
                    cmd.Parameters.AddWithValue("@s", steamId);
                    using (MySqlDataReader rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            result.Add(new StoredVehicle
                            {
                                SteamId = steamId,
                                Name = rd.GetString(0),
                                VehicleGuid = rd.GetString(1),
                                LegacyId = (ushort)rd.GetUInt32(2)
                            });
                        }
                    }
                }
            }
            return result;
        }

        public StoredVehicle Get(ulong steamId, string name)
        {
            using (MySqlConnection c = new MySqlConnection(ConnectionString))
            {
                c.Open();
                using (MySqlCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT id,name,vehicle_guid,legacy_id,skin_id,paint_color,fuel,health,battery," +
                        "tire_mask,locked,locked_owner,locked_group,trunk_blob,barricade_blob " +
                        "FROM " + Table + " WHERE steam_id=@s AND name=@n LIMIT 1;";
                    cmd.Parameters.AddWithValue("@s", steamId);
                    cmd.Parameters.AddWithValue("@n", name);
                    using (MySqlDataReader rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                            return null;

                        return new StoredVehicle
                        {
                            Id = rd.GetInt64(0),
                            SteamId = steamId,
                            Name = rd.GetString(1),
                            VehicleGuid = rd.GetString(2),
                            LegacyId = (ushort)rd.GetUInt32(3),
                            SkinId = (ushort)rd.GetUInt32(4),
                            PaintColor = rd.GetUInt32(5),
                            Fuel = (ushort)rd.GetUInt32(6),
                            Health = (ushort)rd.GetUInt32(7),
                            Battery = (ushort)rd.GetUInt32(8),
                            TireMask = (byte)rd.GetUInt32(9),
                            Locked = rd.GetInt32(10) != 0,
                            LockedOwner = rd.GetUInt64(11),
                            LockedGroup = rd.GetUInt64(12),
                            TrunkBlob = rd.IsDBNull(13) ? null : rd.GetString(13),
                            BarricadeBlob = rd.IsDBNull(14) ? null : rd.GetString(14)
                        };
                    }
                }
            }
        }

        public bool Delete(ulong steamId, string name)
        {
            using (MySqlConnection c = new MySqlConnection(ConnectionString))
            {
                c.Open();
                using (MySqlCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM " + Table + " WHERE steam_id=@s AND name=@n;";
                    cmd.Parameters.AddWithValue("@s", steamId);
                    cmd.Parameters.AddWithValue("@n", name);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}
