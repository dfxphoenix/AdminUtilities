using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CompanionServer.Handlers;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Utilities", "dFxPhoeniX", "1.9.8")]
    [Description("Toggle NoClip, teleport Under Terrain and more")]
    class AdminUtilities : RustPlugin
    {
        private const string permTerrain = "adminutilities.disconnectteleport";
        private const string permInventory = "adminutilities.saveinventory";
        private const string permHHT = "adminutilities.maxhht";
        private const string permNoClip = "adminutilities.nocliptoggle";
        private DataFileSystem dataFile;
        private DataFileSystem dataFileItems;
        private bool newSave;
        private readonly List<BasePlayer> underterrain = new List<BasePlayer>();
        private bool flying = false;

        public class PlayerInfo
        {
            public string Teleport { get; set; } = Vector3.zero.ToString();
            public bool SaveInventory { get; set; } = true;
        }

        public class PlayerInfoItems
        {
            public List<AdminUtilitiesItem> Items { get; set; } = new List<AdminUtilitiesItem>();
        }

        public class AdminUtilitiesItem
        {
            public string container { get; set; } = "main";
            public string shortname { get; set; }
            public int itemid { get; set; }
            public ulong skinID { get; set; }
            public int amount { get; set; }
            public float condition { get; set; }
            public float maxCondition { get; set; }
            public int position { get; set; } = -1;
            public float fuel { get; set; }
            public int keyCode { get; set; }
            public int ammo { get; set; }
            public string ammoTypeShortname { get; set; }
            public string fogImages { get; set; }
            public string paintImages { get; set; }
            public List<AdminUtilitiesMod> contents { get; set; }

            public AdminUtilitiesItem() { }

            public AdminUtilitiesItem(string container, Item item)
            {
                if (item == null)
                    return;

                this.container = container;
                shortname = ItemManager.FindItemDefinition(item.info.shortname).shortname;
                itemid = item.info.itemid;
                skinID = item.skin;
                amount = item.amount;
                condition = item.condition;
                maxCondition = item.maxCondition;
                position = item.position;
                fuel = item.fuel;
                keyCode = item.instanceData?.dataInt ?? 0;
                ammo = item?.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.contents ?? 0;
                ammoTypeShortname = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType?.shortname ?? null;

                if (item.contents?.itemList?.Count > 0)
                {
                    contents = new List<AdminUtilitiesMod>();

                    foreach (var mod in item.contents.itemList)
                    {
                        contents.Add(new AdminUtilitiesMod
                        {
                            shortname = mod.info.shortname,
                            amount = mod.amount,
                            condition = mod.condition,
                            maxCondition = mod.maxCondition,
                            itemid = mod.info.itemid
                        });
                    }
                }
            }
        }

        public class AdminUtilitiesMod
        {
            public string shortname { get; set; }
            public int amount { get; set; }
            public float condition { get; set; }
            public float maxCondition { get; set; }
            public int itemid { get; set; }
        }

        private void OnNewSave()
        {
            newSave = true;
        }

        private void Init()
        {
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerSleepEnded));

            dataFile = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\AdminUtilities\\Config");
            dataFileItems = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\AdminUtilities\\Items");
        }

        private void Loaded()
        {
            permission.RegisterPermission(permTerrain, this);
            permission.RegisterPermission(permInventory, this);
            permission.RegisterPermission(permHHT, this);
            permission.RegisterPermission(permNoClip, this);

            LoadVariables();
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPlayerSleepEnded));
        }

        private PlayerInfo LoadPlayerInfo(BasePlayer player)
        {
            if (!player || !player.IsConnected || !HasPermission(player, permTerrain) || !HasPermission(player, permInventory))
                return null;

            return dataFile.ReadObject<PlayerInfo>($"{player.UserIDString}");
        }

        private void SavePlayerInfo(BasePlayer player, PlayerInfo playerInfo)
        {
            if (dataFile != null)
            {
                dataFile.WriteObject<PlayerInfo>($"{player.UserIDString}", playerInfo);
            }
        }

        private PlayerInfoItems LoadPlayerInfoItems(BasePlayer player)
        {
            if (!player || !player.IsConnected || !HasPermission(player, permTerrain) || !HasPermission(player, permInventory))
                return null;

            return dataFileItems.ReadObject<PlayerInfoItems>($"{player.UserIDString}");
        }

        private void SavePlayerInfoItems(BasePlayer player, PlayerInfoItems playerInfo)
        {
            if (dataFileItems != null)
            {
                dataFileItems.WriteObject<PlayerInfoItems>($"{player.UserIDString}", playerInfo);
            }
        }

        private bool GetSave()
        {
            if (newSave || BuildingManager.server.buildingDictionary.Count == 0)
            {
                return true;
            }

            return false;
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
        {
            if (hitInfo != null && underterrain.Contains(player))
            {
                hitInfo.damageTypes = new Rust.DamageTypeList();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.IsDead())
            {
                return;
            }

            var user = LoadPlayerInfo(player);
            var userItems = LoadPlayerInfoItems(player);

            if (user == null)
            {
                return;
            }

            if (userItems == null)
            {
                return;
            }

            var userTeleport = user.Teleport.ToVector3();
            var position = userTeleport == Vector3.zero ? defaultPos : userTeleport;

            if (position == Vector3.zero)
            {
                position = new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) - 5f, player.transform.position.z);
            }

            player.Teleport(position);
            SaveInventory(player, user, userItems);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var user = LoadPlayerInfo(player);
            var userItems = LoadPlayerInfoItems(player);

            if (user == null)
            {
                return;
            }

            if (userItems == null)
            {
                return;
            }

            if (wipeSaves && GetSave())
            {
                userItems.Items.Clear();

                newSave = false;
                SavePlayerInfoItems(player, userItems);
            }

            LoadPlayerInfo(player);

            if (player.IsDead())
            {
                return;
            }

            if (player.IsSleeping() && HasPermission(player, permTerrain))
            {
                UnderTerrain(player);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!player || !player.IsConnected)
            {
                return;
            }

            NoUnderTerrain(player);

            var user = LoadPlayerInfo(player);
            var userItems = LoadPlayerInfoItems(player);

            if (user == null)
            {
                return;
            }

            if (userItems == null)
            {
                return;
            }

            if (HasPermission(player, permHHT))
            {
                player.health = 100f;
                player.metabolism.hydration.value = player.metabolism.hydration.max;
                player.metabolism.calories.value = player.metabolism.calories.max;
            }

            if (HasPermission(player, permInventory) && user.SaveInventory && userItems.Items.Count > 0)
            {
                if (player.inventory.AllItems().Length == 2)
                {
                    if (player.inventory.GetAmount(ItemManager.FindItemDefinition("rock").itemid) == 1)
                    {
                        if (player.inventory.GetAmount(ItemManager.FindItemDefinition("torch").itemid) == 1)
                        {
                            player.inventory.Strip();
                        }
                    }
                }

                if (userItems.Items.Any(item => item.amount > 0))
                {
                    var list = new List<AdminUtilitiesItem>(userItems.Items);

                    foreach (var aui in list)
                    {
                        RestoreItem(player, aui);
                    }

                    list.Clear();
                }

                userItems.Items.Clear();
                SavePlayerInfoItems(player, userItems);
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!HasPermission(player, permNoClip))
            {
                return;
            }
            if ((input.IsDown(BUTTON.FORWARD) || input.IsDown(BUTTON.BACKWARD) || input.IsDown(BUTTON.LEFT) || input.IsDown(BUTTON.RIGHT) || input.IsDown(BUTTON.JUMP)) && player.IsDeveloper && !flying)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
            }
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type)
        {
            if (player.IsSleeping() && type == AntiHackType.InsideTerrain && HasPermission(player, permTerrain)) return false;
            if (player.IsFlying && (type == AntiHackType.FlyHack || type == AntiHackType.InsideTerrain || type == AntiHackType.NoClip) && HasPermission(player, permNoClip)) return false;
            return null;
        }

        [ChatCommand("disconnectteleport")]
        private void cmdDisconnectTeleport(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permTerrain))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            var user = LoadPlayerInfo(player);

            if (user == null)
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "set":
                        {
                            var position = player.transform.position;

                            if (args.Length == 4)
                            {
                                if (args[1].All(char.IsDigit) && args[2].All(char.IsDigit) && args[3].All(char.IsDigit))
                                {
                                    var customPos = new Vector3(float.Parse(args[1]), 0f, float.Parse(args[3]));

                                    if (Vector3.Distance(customPos, Vector3.zero) <= TerrainMeta.Size.x / 1.5f)
                                    {
                                        customPos.y = float.Parse(args[2]);

                                        if (customPos.y > -100f && customPos.y < 4400f)
                                            position = customPos;
                                        else
                                            Player.Message(player, msg("OutOfBounds", player.UserIDString));
                                    }
                                    else
                                        Player.Message(player, msg("OutOfBounds", player.UserIDString));
                                }
                                else
                                    Player.Message(player, msg("DisconnectTeleportSet", player.UserIDString, FormatPosition(user.Teleport.ToVector3())));
                            }

                            user.Teleport = position.ToString();
                            Player.Message(player, msg("PositionAdded", player.UserIDString, FormatPosition(position)));
                            SavePlayerInfo(player, user);
                        }
                        return;
                    case "reset":
                        {
                            user.Teleport = Vector3.zero.ToString();

                            if (defaultPos != Vector3.zero)
                            {
                                user.Teleport = defaultPos.ToString();
                                Player.Message(player, msg("PositionRemoved2", player.UserIDString, user.Teleport));
                            }
                            else
                                Player.Message(player, msg("PositionRemoved1", player.UserIDString));

                            SavePlayerInfo(player, user);
                        }
                        return;
                }
            }

            string teleportPos = FormatPosition(user.Teleport.ToVector3() == Vector3.zero ? defaultPos : user.Teleport.ToVector3());

            Player.Message(player, msg("DisconnectTeleportSet", player.UserIDString, teleportPos));
            Player.Message(player, msg("DisconnectTeleportReset", player.UserIDString));
        }

        [ChatCommand("saveinventory")]
        private void cmdSaveInventory(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permInventory))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            var user = LoadPlayerInfo(player);

            if (user == null)
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            user.SaveInventory = !user.SaveInventory;
            Player.Message(player, msg(user.SaveInventory ? "SavingInventory" : "NotSavingInventory", player.UserIDString));
            SavePlayerInfo(player, user);
        }

        [ChatCommand("noclip")]
        private void cmdNoClip(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permNoClip))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
            if (!player.IsFlying)
            {
                timer.Once(0.1f, () => {
                    player.SendConsoleCommand("noclip");
                    Player.Message(player, msg("FlyEnabled", player.UserIDString));
                    flying = true;
                });
            }
            else
            {
                player.SendConsoleCommand("noclip");
                Player.Message(player, msg("FlyDisabled", player.UserIDString));
                flying = false;
            }
        }

        public void UnderTerrain(BasePlayer player)
        {
            if (!player.IsConnected || underterrain.Contains(player))
            {
                return;
            }

            if (player.transform.position.y < TerrainMeta.HeightMap.GetHeight(player.transform.position) || player.IsHeadUnderwater())
            {
                player.metabolism.temperature.min = 20;
                player.metabolism.temperature.max = 20;
                player.metabolism.radiation_poison.max = 0;
                player.metabolism.oxygen.min = 1;
                player.metabolism.wetness.max = 0;
                player.metabolism.calories.min = player.metabolism.calories.value;
                player.metabolism.isDirty = true;
                player.metabolism.SendChangesToClient();
                underterrain.Add(player);
            }
        }

        public void NoUnderTerrain(BasePlayer player)
        {
            if (!player.IsConnected || !underterrain.Contains(player))
            {
                return;
            }

            if (player.transform.position.y < TerrainMeta.HeightMap.GetHeight(player.transform.position) || player.IsHeadUnderwater())
            {
                float y = TerrainMeta.HeightMap.GetHeight(player.transform.position);
                player.Teleport(new Vector3(player.transform.position.x, y + 2f, player.transform.position.z));
                player.SendNetworkUpdateImmediate();
                player.metabolism.temperature.min = -100;
                player.metabolism.temperature.max = 100;
                player.metabolism.radiation_poison.max = 500;
                player.metabolism.oxygen.min = 0;
                player.metabolism.calories.min = 0;
                player.metabolism.wetness.max = 1;
                underterrain.Remove(player);
            }
        }

        public string FormatPosition(Vector3 position)
        {
            string x = position.x.ToString("N2");
            string y = position.y.ToString("N2");
            string z = position.z.ToString("N2");

            return $"{x} {y} {z}";
        }

        private void SaveInventory(BasePlayer player, PlayerInfo user, PlayerInfoItems userItems)
        {
            if (!HasPermission(player, permInventory) || !user.SaveInventory)
                return;

            if (player.inventory.AllItems().Length == 0)
            {
                userItems.Items.Clear();
                SavePlayerInfoItems(player, userItems);
                return;
            }

            var items = new List<AdminUtilitiesItem>();
            var list = new List<Item>(player.inventory.containerWear.itemList);

            foreach (Item item in list)
            {
                items.Add(new AdminUtilitiesItem("wear", item));
                item.Remove();
            }

            list = new List<Item>(player.inventory.containerMain.itemList);

            foreach (Item item in list)
            {
                items.Add(new AdminUtilitiesItem("main", item));
                item.Remove();
            }

            list = new List<Item>(player.inventory.containerBelt.itemList);

            foreach (Item item in list)
            {
                items.Add(new AdminUtilitiesItem("belt", item));
                item.Remove();
            }

            list.Clear();

            if (items.Count == 0)
            {
                return;
            }

            ItemManager.DoRemoves();
            userItems.Items.Clear();
            userItems.Items.AddRange(items);
            SavePlayerInfoItems(player, userItems);
        }

        private void RestoreItem(BasePlayer player, AdminUtilitiesItem aui)
        {
            if (aui.itemid == 0 || aui.amount < 1 || string.IsNullOrEmpty(aui.container))
                return;

            Item item = ItemManager.CreateByItemID(aui.itemid, aui.amount, aui.skinID);

            if (item == null)
                return;

            if (item.hasCondition)
            {
                item.maxCondition = aui.maxCondition;
                item.condition = aui.condition;
            }

            item.fuel = aui.fuel;

            var heldEntity = item.GetHeldEntity();

            if (heldEntity != null)
            {
                if (item.skin != 0)
                    heldEntity.skinID = item.skin;

                var weapon = heldEntity as BaseProjectile;

                if (weapon != null)
                {
                    if (!string.IsNullOrEmpty(aui.ammoTypeShortname))
                    {
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(aui.ammoTypeShortname);
                    }

                    weapon.primaryMagazine.contents = 0;
                    weapon.SendNetworkUpdateImmediate(false);
                    weapon.primaryMagazine.contents = aui.ammo;
                    weapon.SendNetworkUpdateImmediate(false);
                }
            }

            if (aui.contents != null)
            {
                foreach (var aum in aui.contents)
                {
                    Item mod = ItemManager.CreateByItemID(aum.itemid, 1);

                    if (mod == null)
                        continue;

                    if (mod.hasCondition)
                    {
                        mod.maxCondition = aum.maxCondition;
                        mod.condition = aum.condition;
                    }

                    item.contents.AddItem(mod.info, Math.Max(aum.amount, 1));
                }
            }

            if (aui.keyCode != 0)
            {
                item.instanceData = Pool.Get<ProtoBuf.Item.InstanceData>();
                item.instanceData.ShouldPool = false;
                item.instanceData.dataInt = aui.keyCode;
            }

            item.MarkDirty();

            var container = aui.container == "belt" ? player.inventory.containerBelt : aui.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain;

            if (!item.MoveToContainer(container, aui.position, true))
            {
                if (!item.MoveToContainer(player.inventory.containerMain, -1, true))
                {
                    item.Remove();
                }
            }
        }

        private bool HasPermission(BasePlayer player, string permName)
        {
            return player != null && player.IPlayer.HasPermission(permName);
        }

        #region Config

        private bool Changed;
        private Vector3 defaultPos;
        private bool wipeSaves;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PositionAdded"] = "You will now teleport to <color=orange>{0}</color> on disconnect.",
                ["PositionRemoved1"] = "You will now teleport under ground on disconnect.",
                ["PositionRemoved2"] = "You will now teleport to <color=orange>{0}</color> on disconnect.",
                ["SavingInventory"] = "Your inventory will be saved on disconnect and restored when you wake up.",
                ["NotSavingInventory"] = "Your inventory will no longer be saved.",
                ["DisconnectTeleportSet"] = "/disconnectteleport set <x y z> - sets your log out position. can specify coordinates <color=orange>{0}</color>",
                ["DisconnectTeleportReset"] = "/disconnectteleport reset - resets your log out position to be underground unless a position is configured in the config file",
                ["OutOfBounds"] = "The specified coordinates are not within the allowed boundaries of the map.",
                ["FlyDisabled"] = "You switched NoClip off!",
                ["FlyEnabled"] = "You switched NoClip on!",
                ["NoPermission"] = "You do not have permission to use this command."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PositionAdded"] = "Te vei teleporta la <color=orange>{0}</color> odată cu deconectarea.",
                ["PositionRemoved1"] = "Te vei teleporta sub pământ odată cu deconectarea.",
                ["PositionRemoved2"] = "Te vei teleporta la <color=orange>{0}</color> odată cu deconectarea.",
                ["SavingInventory"] = "Inventarul tău va fi salvat la deconectare și recuperat la reconectare.",
                ["NotSavingInventory"] = "Inventarul tău nu va mai fi salvat.",
                ["DisconnectTeleportSet"] = "/disconnectteleport set <x y z> - setează poziția ta de deconectare. poți specifica coordonatele <color=orange>{0}</color>",
                ["DisconnectTeleportReset"] = "/disconnectteleport reset - resetează poziția ta de deconectare pentru a fi sub pământ, doar dacă o poziție nu este configurată într-o filă de configurare",
                ["OutOfBounds"] = "Coordonatele specificate nu se încadrează în limitele hărții.",
                ["FlyDisabled"] = "Ai dezactivat NoClip-ul!!",
                ["FlyEnabled"] = "Ai Activat NoClip-ul!",
                ["NoPermission"] = "Nu ai permisiunea de a folosi această comandă."
            }, this, "ro");
        }

        private void LoadVariables()
        {
            defaultPos = GetConfig("Settings", "Default Teleport To Position On Disconnect", "(0, 0, 0)").ToString().ToVector3();
            wipeSaves = Convert.ToBoolean(GetConfig("Settings", "Wipe Saved Inventories On Map Wipe", false));
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }

            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }

            return value;
        }

        private string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        #endregion
    }
}