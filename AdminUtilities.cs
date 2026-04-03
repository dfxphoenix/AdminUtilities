using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CompanionServer.Handlers;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Network;

namespace Oxide.Plugins
{
    [Info("Admin Utilities", "dFxPhoeniX", "2.4.6")]
    [Description("Toggle NoClip, teleport Under Terrain and more")]
    public class AdminUtilities : RustPlugin
    {
        private const string permTerrain = "adminutilities.disconnectteleport";
        private const string permInventory = "adminutilities.saveinventory";
        private const string permHHT = "adminutilities.maxhht";
        private const string permNoClip = "adminutilities.nocliptoggle";
        private const string permGodMode = "adminutilities.godmodetoggle";
        private const string permKick = "adminutilities.kick";
        private const string permBan = "adminutilities.ban";
        private const string permBanIp = "adminutilities.banip";
        private const string permUnban = "adminutilities.unban";
        private const string permBanList = "adminutilities.banlist";
        private const string permPreventKick = "adminutilities.preventkick";
        private const string permPreventBan = "adminutilities.preventban";
        private const string permPreventBanIp = "adminutilities.preventbanip";

        private DataFileSystem dataFile;
        private DataFileSystem dataFileItems;

        private ModerationData moderationData = new ModerationData();
        private readonly HashSet<ulong> pendingForceNoClip = new HashSet<ulong>();

        private bool newSave;
        private bool suppressNativeModerationCommandHook;

        ////////////////////////////////////////////////////////////
        // Files
        ////////////////////////////////////////////////////////////

        private class PlayerInfo
        {
            public string Teleport { get; set; } = Vector3.zero.ToString();
            public bool SaveInventory { get; set; } = true;
            public bool UnderTerrain { get; set; } = false;
            public bool NoClip { get; set; } = false;
            public bool GodMode { get; set; } = false;
        }

        private Dictionary<string, PlayerInfo> playerInfoCache = new Dictionary<string, PlayerInfo>();

        private class PlayerInfoItems
        {
            public List<AdminUtilitiesItem> Items { get; set; } = new List<AdminUtilitiesItem>();
            public bool SnapshotOnly { get; set; } = false;
        }

        private Dictionary<string, PlayerInfoItems> playerInfoItemsCache = new Dictionary<string, PlayerInfoItems>();

        private class AdminUtilitiesItem
        {
            public List<AdminUtilitiesItem> contents { get; set; }
            public string container { get; set; } = "main";
            public int ammo { get; set; }
            public int amount { get; set; }
            public int flags { get; set; }
            public int containerSlots { get; set; }
            public bool hasInstanceData { get; set; }
            public string ammoType { get; set; }
            public float condition { get; set; }
            public float fuel { get; set; }
            public int frequency { get; set; }
            public int itemid { get; set; }
            public float maxCondition { get; set; }
            public string name { get; set; }
            public int position { get; set; } = -1;
            public ulong skin { get; set; }
            public string text { get; set; }
            public int blueprintAmount { get; set; }
            public int blueprintTarget { get; set; }
            public int dataInt { get; set; }
            public ulong subEntity { get; set; }
            public bool shouldPool { get; set; }

            public AdminUtilitiesItem() { }

            public AdminUtilitiesItem(string container, Item item)
            {
                this.container = container;
                itemid = item.info.itemid;
                name = item.name;
                text = item.text;
                amount = item.amount;
                condition = item.condition;
                maxCondition = item.maxCondition;
                fuel = item.fuel;
                position = item.position;
                skin = item.skin;
                flags = (int)item.flags;

                hasInstanceData = item.instanceData != null;
                if (item.instanceData != null)
                {
                    dataInt = item.instanceData.dataInt;
                    blueprintAmount = item.instanceData.blueprintAmount;
                    blueprintTarget = item.instanceData.blueprintTarget;
                    subEntity = item.instanceData.subEntity.Value;
                    shouldPool = item.instanceData.ShouldPool;
                }

                if (item.contents != null)
                {
                    containerSlots = item.contents.capacity;

                    if (item.contents.itemList != null && item.contents.itemList.Count > 0)
                    {
                        contents = new();
                        foreach (var mod in item.contents.itemList)
                            contents.Add(new("default", mod));
                    }
                }

                if (item.GetHeldEntity() is HeldEntity e)
                {
                    if (e is BaseProjectile baseProjectile)
                    {
                        ammo = baseProjectile.primaryMagazine.contents;
                        ammoType = baseProjectile.primaryMagazine.ammoType?.shortname;
                    }
                    else if (e is FlameThrower flameThrower)
                    {
                        ammo = flameThrower.ammo;
                    }
                }

                if (ItemModAssociatedEntity<PagerEntity>.GetAssociatedEntity(item) is PagerEntity pagerEntity)
                {
                    frequency = pagerEntity.GetFrequency();
                }
            }

            public static Item Create(AdminUtilitiesItem aui, bool restoreContents = true)
            {
                if (aui.itemid == 0 || string.IsNullOrEmpty(aui.container))
                    return null;

                Item item;
                if (aui.blueprintTarget != 0)
                {
                    item = ItemManager.Create(Workbench.GetBlueprintTemplate());
                    item.blueprintTarget = aui.blueprintTarget;
                    item.amount = aui.blueprintAmount;
                }
                else item = ItemManager.CreateByItemID(aui.itemid, aui.amount, aui.skin);

                if (item == null) return null;

                item.flags = (Item.Flag)aui.flags;

                if (aui.hasInstanceData)
                {
                    item.instanceData = aui.shouldPool ? Pool.Get<ProtoBuf.Item.InstanceData>() : new ProtoBuf.Item.InstanceData();
                    item.instanceData.ShouldPool = aui.shouldPool;
                    item.instanceData.blueprintAmount = aui.blueprintAmount;
                    item.instanceData.blueprintTarget = aui.blueprintTarget;
                    item.instanceData.dataInt = aui.dataInt;
                    item.instanceData.subEntity = new(aui.subEntity);
                }

                if (!string.IsNullOrEmpty(aui.name)) item.name = aui.name;
                if (!string.IsNullOrEmpty(aui.text)) item.text = aui.text;

                if (item.GetHeldEntity() is HeldEntity e)
                {
                    if (item.skin != 0) e.skinID = item.skin;

                    if (e is BaseProjectile bp)
                    {
                        bp.primaryMagazine.contents = aui.ammo;
                        if (!string.IsNullOrEmpty(aui.ammoType))
                            bp.primaryMagazine.ammoType = ItemManager.FindItemDefinition(aui.ammoType);
                        bp.DelayedModsChanged();
                    }
                    else if (e is FlameThrower ft) ft.ammo = aui.ammo;
                    else if (e is Chainsaw cs) cs.ammo = aui.ammo;

                    e.SendNetworkUpdate();
                }

                if (aui.frequency > 0 && item.info.GetComponentInChildren<ItemModRFListener>() != null)
                {
                    if (item.instanceData != null && item.instanceData.subEntity.IsValid &&
                        BaseNetworkable.serverEntities.Find(item.instanceData.subEntity) is PagerEntity pagerEntity)
                    {
                        pagerEntity.ChangeFrequency(aui.frequency);
                    }
                }

                if (restoreContents)
                {
                    var slots = System.Math.Max(aui.containerSlots, aui.contents?.Count ?? 0);
                    EnsureContainer(item, slots);
                    RestoreContents(null, item, aui.contents);
                }

                if (item.hasCondition)
                {
                    item._maxCondition = aui.maxCondition;
                    item._condition = aui.condition;
                }

                item.fuel = aui.fuel;
                item.MarkDirty();
                return item;
            }

            private static T FindItemMod<T>(Item item) where T : ItemMod
            {
                var mods = item?.info?.itemMods;
                if (mods == null) return null;

                foreach (var m in mods)
                    if (m is T t) return t;

                return null;
            }

            private static void EnsureContainer(Item item, int slots)
            {
                if (item == null || slots <= 0) return;

                // Armor inserts container
                var armorSlot = FindItemMod<ItemModContainerArmorSlot>(item);
                if (armorSlot != null)
                {
                    armorSlot.CreateAtCapacity(slots, item);
                    return;
                }

                // fallback generic container
                if (item.contents == null)
                {
                    item.contents = Pool.Get<ItemContainer>();
                    item.contents.ServerInitialize(item, slots);
                    if (!item.contents.uid.IsValid)
                        item.contents.GiveUID();
                }
            }

            private static void RestoreContents(BasePlayer player, Item parent, List<AdminUtilitiesItem> saved)
            {
                if (saved == null || saved.Count == 0) return;
                if (parent?.contents == null) return;

                foreach (var aum in saved)
                {
                    var mod = Create(aum, true);
                    if (mod == null) continue;

                    if (mod.MoveToContainer(parent.contents, aum.position, true) || mod.MoveToContainer(parent.contents))
                        continue;

                    if (player != null) player.GiveItem(mod);
                    else mod.Remove();
                }

                parent.contents.MarkDirty();
            }

            public static void Restore(BasePlayer player, AdminUtilitiesItem aui)
            {
                Item item = Create(aui, restoreContents: false);
                if (item == null) return;

                ItemContainer target = aui.container switch
                {
                    "belt" => player.inventory.containerBelt,
                    "wear" => player.inventory.containerWear,
                    "main" or _ => player.inventory.containerMain,
                };

                bool moved = item.MoveToContainer(target, aui.position, true);
                if (!moved) player.GiveItem(item);

                var slots = System.Math.Max(aui.containerSlots, aui.contents?.Count ?? 0);
                EnsureContainer(item, slots);
                RestoreContents(player, item, aui.contents);

                if (item.GetHeldEntity() is BaseProjectile bp)
                {
                    bp.DelayedModsChanged();
                    bp.SendNetworkUpdate();
                }

                item.MarkDirty();
            }
        }

        private class ModerationData
        {
            public Dictionary<string, BanRecord> PlayerBans { get; set; } = new Dictionary<string, BanRecord>();
            public Dictionary<string, BanRecord> IpBans { get; set; } = new Dictionary<string, BanRecord>();
            public Dictionary<string, string> LastKnownIps { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> LastKnownNames { get; set; } = new Dictionary<string, string>();
        }

        private class BanRecord
        {
            public string Target { get; set; }
            public string DisplayName { get; set; }
            public string Reason { get; set; }
            public string Source { get; set; }
            public long CreatedAt { get; set; }
            public long ExpiresAt { get; set; } // 0 = permanent
        }

        private PlayerInfo LoadPlayerInfo(BasePlayer player)
        {
            if (dataFile == null || player == null) return null;

            if (playerInfoCache.TryGetValue(player.UserIDString, out var cached))
                return cached;

            var user = dataFile.ReadObject<PlayerInfo>(player.UserIDString) ?? new PlayerInfo();
            playerInfoCache[player.UserIDString] = user;
            return user;
        }

        private void SavePlayerInfo(BasePlayer player, PlayerInfo playerInfo)
        {
            if (dataFile == null) return;

            dataFile.WriteObject($"{player.UserIDString}", playerInfo);
            playerInfoCache[player.UserIDString] = playerInfo;
        }

        private PlayerInfoItems LoadPlayerInfoItems(BasePlayer player)
        {
            if (dataFileItems == null || player == null) return null;

            if (playerInfoItemsCache.TryGetValue(player.UserIDString, out var cached))
                return cached;

            var userItems = dataFileItems.ReadObject<PlayerInfoItems>(player.UserIDString) ?? new PlayerInfoItems();
            playerInfoItemsCache[player.UserIDString] = userItems;
            return userItems;
        }

        private void SavePlayerInfoItems(BasePlayer player, PlayerInfoItems playerInfoItems)
        {
            if (dataFileItems == null) return;

            dataFileItems.WriteObject($"{player.UserIDString}", playerInfoItems);
            playerInfoItemsCache[player.UserIDString] = playerInfoItems;
        }

        private void LoadModerationData()
        {
            if (dataFile == null)
                return;

            moderationData = dataFile.ReadObject<ModerationData>("global") ?? new ModerationData();
        }

        private void SaveModerationData()
        {
            if (dataFile == null)
                return;

            dataFile.WriteObject("global", moderationData);
        }

        ////////////////////////////////////////////////////////////
        // Oxide Hooks
        ////////////////////////////////////////////////////////////

        private void OnNewSave()
        {
            newSave = true;
        }

        private void Init()
        {
            InitConfig();

            dataFile = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\AdminUtilities\\Settings");
            dataFileItems = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\AdminUtilities\\Items");
            LoadModerationData();
        }

        private void Loaded()
        {
            permission.RegisterPermission(permTerrain, this);
            permission.RegisterPermission(permInventory, this);
            permission.RegisterPermission(permHHT, this);
            permission.RegisterPermission(permNoClip, this);
            permission.RegisterPermission(permGodMode, this);
            permission.RegisterPermission(permKick, this);
            permission.RegisterPermission(permBan, this);
            permission.RegisterPermission(permBanIp, this);
            permission.RegisterPermission(permUnban, this);
            permission.RegisterPermission(permBanList, this);
            permission.RegisterPermission(permPreventKick, this);
            permission.RegisterPermission(permPreventBan, this);
            permission.RegisterPermission(permPreventBanIp, this);
        }

        private void OnServerInformationUpdated()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;

                bool relevant = player.IsFlying || player.IsGod() || player.IsDeveloper;
                if (!relevant) continue;

                var user = LoadPlayerInfo(player);
                if (user == null) continue;

                if (!player.IsFlying && user.NoClip)
                {
                    user.NoClip = false;
                    SavePlayerInfo(player, user);
                }

                if (!player.IsGod() && user.GodMode)
                {
                    user.GodMode = false;
                    SavePlayerInfo(player, user);
                }

                if (player.IsDeveloper && !player.IsFlying && !player.IsGod())
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
            }
        }

        private object OnClientCommand(Connection connection, string command)
        {
            if (command.Contains("/"))
                return null;

            BasePlayer player = BasePlayer.FindByID(connection.userid);

            if (player == null)
                return null;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return null;

            string lowerCommand = command.ToLower();

            if (lowerCommand.Contains("setinfo \"global.god\" \"True\""))
            {
                if (!HasPermission(player, permGodMode) && !user.GodMode && !player.IsGod())
                {
                    return false;
                }
            }

            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg?.cmd == null || arg.Connection != null)
                return null;

            string cmd = arg.cmd.FullName.ToLowerInvariant();

            if (suppressNativeModerationCommandHook && (cmd == "ban" || cmd == "banid" || cmd == "unban"))
                return null;

            string[] args = arg.Args ?? Array.Empty<string>();

            switch (cmd)
            {
                case "kick":
                    HandleConsoleKick(arg, args);
                    return true;

                case "ban":
                case "banid":
                    HandleConsoleBan(arg, args, false);
                    return true;

                case "banip":
                    HandleConsoleBan(arg, args, true);
                    return true;

                case "unban":
                    HandleConsoleUnban(arg, args);
                    return true;
            }

            return null;
        }

        private object OnRconCommand(IPAddress ipAddress, string command, string[] args)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;

            switch (command.ToLowerInvariant())
            {
                case "kick":
                    HandleConsoleKick(null, args ?? Array.Empty<string>());
                    return true;

                case "ban":
                case "banid":
                    HandleConsoleBan(null, args ?? Array.Empty<string>(), false);
                    return true;

                case "banip":
                    HandleConsoleBan(null, args ?? Array.Empty<string>(), true);
                    return true;

                case "unban":
                    HandleConsoleUnban(null, args ?? Array.Empty<string>());
                    return true;
            }

            return null;
        }

        private void OnPlayerTick(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            if (player.IsNpc || player is NPCPlayer) return;

            if (!player.IsFlying) return;

            if (HasPermission(player, permNoClip)) return;

            if (pendingForceNoClip.Contains(player.userID))
                return;

            var user = LoadPlayerInfo(player);
            if (user == null) return;

            if (user.NoClip) return;

            pendingForceNoClip.Add(player.userID);

            timer.Once(0.05f, () =>
            {
                if (player == null || !player.IsConnected)
                {
                    pendingForceNoClip.Remove(player.userID);
                    return;
                }

                pendingForceNoClip.Remove(player.userID);

                if (!HasPermission(player, permNoClip) && player.IsFlying)
                    player.SendConsoleCommand("noclip");
            });
        }

        private void OnServerInitialized()
        {
            if (GetSave())
            {
                if (wipeSettings)
                {
                    string folderPath = $"{Interface.Oxide.DataDirectory}/AdminUtilities/Settings";

                    if (Directory.Exists(folderPath))
                    {
                        foreach (string file in Directory.GetFiles(folderPath))
                            File.Delete(file);
                    }
                }

                if (wipeItems)
                {
                    string folderItemsPath = $"{Interface.Oxide.DataDirectory}/AdminUtilities/Items";

                    if (Directory.Exists(folderItemsPath))
                    {
                        foreach (string file in Directory.GetFiles(folderItemsPath))
                            File.Delete(file);
                    }
                }

                newSave = false;
            }

            CleanupExpiredBans();
        }

        private void OnUserBanned(string name, string id, string ipAddress, string reason, long expiry)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            long now = UnixNow();
            long newExpiry = expiry > 0 ? expiry : 0;
            bool changed = false;

            if (!moderationData.PlayerBans.TryGetValue(id, out var existing) || existing == null)
            {
                moderationData.PlayerBans[id] = new BanRecord
                {
                    Target = id,
                    DisplayName = string.IsNullOrWhiteSpace(name) ? id : name,
                    Reason = string.IsNullOrWhiteSpace(reason) ? defaultBanReason : reason,
                    Source = "Native",
                    CreatedAt = now,
                    ExpiresAt = newExpiry
                };

                changed = true;
            }
            else
            {
                string newName = string.IsNullOrWhiteSpace(name) ? existing.DisplayName : name;
                string newReason = string.IsNullOrWhiteSpace(reason) ? existing.Reason : reason;

                if (existing.DisplayName != newName)
                {
                    existing.DisplayName = newName;
                    changed = true;
                }

                if (existing.Reason != newReason)
                {
                    existing.Reason = newReason;
                    changed = true;
                }

                if (existing.Source != "Native")
                {
                    existing.Source = "Native";
                    changed = true;
                }

                // Native event should be authoritative for the current player-ban state
                if (existing.ExpiresAt != newExpiry)
                {
                    existing.ExpiresAt = newExpiry;
                    changed = true;
                }

                // Optional: refresh CreatedAt when the native ban is re-applied/changed
                if (existing.CreatedAt <= 0)
                {
                    existing.CreatedAt = now;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress != "0")
            {
                if (IsValidStoredIp(ipAddress))
                {
                    if (!moderationData.LastKnownIps.TryGetValue(id, out var oldIp) || oldIp != ipAddress)
                    {
                        moderationData.LastKnownIps[id] = ipAddress;
                        changed = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!moderationData.LastKnownNames.TryGetValue(id, out var oldName) || oldName != name)
                {
                    moderationData.LastKnownNames[id] = name;
                    changed = true;
                }
            }

            if (changed)
                SaveModerationData();
        }

        private void OnUserUnbanned(string name, string id, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            bool changed = false;

            if (moderationData.PlayerBans.Remove(id))
                changed = true;

            if (changed)
                SaveModerationData();
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null)
                return;

            if (player.IsNpc || (player is NPCPlayer))
                return;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            if (user.UnderTerrain)
            {
                info.damageTypes = new Rust.DamageTypeList();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            pendingForceNoClip.Remove(player.userID);

            var user = LoadPlayerInfo(player);
            if (user == null)
            {
                playerInfoCache.Remove(player.UserIDString);
                playerInfoItemsCache.Remove(player.UserIDString);
                return;
            }

            if (!HasPermission(player, permNoClip) && user.NoClip)
            {
                user.NoClip = false;
                SavePlayerInfo(player, user);
            }

            if (!HasPermission(player, permGodMode) && user.GodMode)
            {
                user.GodMode = false;
                SavePlayerInfo(player, user);
            }

            if (!persistentNoClip && user.NoClip)
            {
                user.NoClip = false;
                SavePlayerInfo(player, user);
            }

            if (!persistentGodMode && user.GodMode)
            {
                user.GodMode = false;
                SavePlayerInfo(player, user);
            }

            if (!player.IsDead())
            {
                if (HasPermission(player, permTerrain))
                    DisconnectTeleport(player);

                if (HasPermission(player, permInventory) && user.SaveInventory)
                    SaveInventory(player, player.IsSleeping());
            }

            playerInfoCache.Remove(player.UserIDString);
            playerInfoItemsCache.Remove(player.UserIDString);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            UpdateLastKnownPlayerIdentity(player);

            PlayerInfo user = LoadPlayerInfo(player);
            if (user == null)
                return;

            if (!HasPermission(player, permTerrain) && user.UnderTerrain)
            {
                NoUnderTerrain(player);
            }

            if (persistentNoClip && HasPermission(player, permNoClip) && user.NoClip)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);

                timer.Once(0.2f, () =>
                {
                    if (player == null || !player.IsConnected) return;
                    player.SendConsoleCommand("noclip");
                });
            }

            if (persistentGodMode && HasPermission(player, permGodMode) && user.GodMode)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);

                timer.Once(0.2f, () =>
                {
                    if (player == null || !player.IsConnected) return;
                    player.SendConsoleCommand("setinfo \"global.god\" \"True\"");
                });
            }

            timer.Once(0.5f, () =>
            {
                if (player == null || !player.IsConnected) return;
                RestoreSavedInventory(player);
            });
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (HasPermission(player, permHHT))
            {
                player.health = 100f;
                player.metabolism.hydration.value = player.metabolism.hydration.max;
                player.metabolism.calories.value = player.metabolism.calories.max;
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!player || !player.IsConnected)
                return;

            PlayerInfo user = LoadPlayerInfo(player);
            if (user == null)
                return;

            if (HasPermission(player, permTerrain) && user.UnderTerrain)
            {
                NoUnderTerrain(player);
            }

            RestoreSavedInventory(player);
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type)
        {
            if (player.IsSleeping() && (type == AntiHackType.InsideTerrain) && HasPermission(player, permTerrain))
                return true;

            if (player.IsFlying && (type == AntiHackType.FlyHack || type == AntiHackType.InsideTerrain || type == AntiHackType.NoClip) && HasPermission(player, permNoClip))
                return true;

            return null;
        }

        private object CanUserLogin(string name, string id, string ip)
        {
            string message = BuildLoginBanMessage(id, ip, id);

            if (!string.IsNullOrWhiteSpace(message))
                return message;

            return null;
        }

        ////////////////////////////////////////////////////////////
        // Commands
        ////////////////////////////////////////////////////////////

        [ChatCommand("disconnectteleport")]
        private void cmdDisconnectTeleport(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permTerrain))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "set":
                        {
                            if (args.Length == 4 && float.TryParse(args[1], out float x) && float.TryParse(args[2], out float y) && float.TryParse(args[3], out float z))
                            {
                                var customPos = new Vector3(x, y, z);

                                if (Vector3.Distance(customPos, Vector3.zero) <= TerrainMeta.Size.x / 1.5f && customPos.y > -100f && customPos.y < 4400f)
                                {
                                    user.Teleport = customPos.ToString();
                                    Player.Message(player, msg("PositionAdded", player.UserIDString, FormatPosition(customPos)));
                                }
                                else
                                {
                                    Player.Message(player, msg("OutOfBounds", player.UserIDString));
                                }
                            }
                            else
                            {
                                Player.Message(player, msg("DisconnectTeleportSet", player.UserIDString, FormatPosition(user.Teleport.ToVector3())));
                            }

                            SavePlayerInfo(player, user);
                            return;
                        }
                    case "reset":
                        {
                            user.Teleport = defaultPos.ToString();
                            string message = defaultPos != Vector3.zero ? msg("PositionRemoved2", player.UserIDString, defaultPos) : msg("PositionRemoved1", player.UserIDString);
                            Player.Message(player, message);
                            SavePlayerInfo(player, user);
                            return;
                        }
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

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

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

            ToggleNoClip(player);
        }

        [ChatCommand("god")]
        private void cmdGodMode(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permGodMode))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            ToggleGodMode(player);
        }

        [ChatCommand("kick")]
        private void cmdKick(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permKick))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                Player.Message(player, msg("KickUsage", player.UserIDString));
                return;
            }

            var target = FindOnlinePlayer(args[0]);
            if (target == null)
            {
                Player.Message(player, msg("PlayerNotFound", player.UserIDString));
                return;
            }

            if (permission.UserHasPermission(target.UserIDString, permPreventKick))
            {
                Player.Message(player, msg("TargetProtectedFromKick", player.UserIDString, target.displayName));
                return;
            }

            string reason = string.Join(" ", args.Skip(1));
            if (string.IsNullOrWhiteSpace(reason))
                reason = defaultKickReason;

            target.IPlayer.Kick(reason);
            NotifyKickEvent(target.displayName, target.UserIDString, reason, true);
            Player.Message(player, msg("KickSuccess", player.UserIDString, target.displayName));
        }

        [ChatCommand("ban")]
        private void cmdBan(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permBan))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            if (!TryParseModerationArgs(args, out var targetInput, out var duration, out var reason))
            {
                Player.Message(player, msg("BanUsage", player.UserIDString));
                return;
            }

            if (IsIpAddress(targetInput))
            {
                AddIpBan(targetInput, targetInput, reason, duration, player.UserIDString);
                KickAllPlayersByIp(targetInput);
                NotifyBanEvent(targetInput, null, targetInput, duration, reason, false, false, 0, true);

                if (duration.HasValue)
                    Player.Message(player, msg("TempIpOnlyBanSuccess", player.UserIDString, targetInput, FormatDuration(duration.Value)));
                else
                    Player.Message(player, msg("IpOnlyBanSuccess", player.UserIDString, targetInput));

                return;
            }

            if (!TryResolveSteamId(targetInput, out var steamId, out var displayName, out var ip, out var onlinePlayer))
            {
                Player.Message(player, msg("PlayerNotFound", player.UserIDString));
                return;
            }

            if (permission.UserHasPermission(steamId, permPreventBan))
            {
                Player.Message(player, msg("TargetProtectedFromBan", player.UserIDString, displayName ?? steamId));
                return;
            }

            AddOrUpdatePlayerBan(steamId, displayName, reason, duration, player.UserIDString);
            SetNativeBan(steamId, displayName ?? steamId, reason, duration);

            onlinePlayer?.IPlayer?.Kick(BuildLoginBanMessage(steamId, ip, onlinePlayer?.UserIDString));
            NotifyBanEvent(displayName ?? steamId, steamId, ip, duration, reason, false, false, 0, true);

            if (duration.HasValue)
                Player.Message(player, msg("TempBanSuccess", player.UserIDString, displayName ?? steamId, FormatDuration(duration.Value)));
            else
                Player.Message(player, msg("BanSuccess", player.UserIDString, displayName ?? steamId));
        }

        [ChatCommand("banip")]
        private void cmdBanIp(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permBanIp))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            if (!TryParseModerationArgs(args, out var targetInput, out var duration, out var reason))
            {
                Player.Message(player, msg("BanIpUsage", player.UserIDString));
                return;
            }

            if (IsIpAddress(targetInput))
            {
                AddIpBan(targetInput, targetInput, reason, duration, player.UserIDString);
                KickAllPlayersByIp(targetInput);

                int bannedAccounts = BanKnownAccountsOnIp(targetInput, duration, reason, player.UserIDString);
                NotifyBanEvent(targetInput, null, targetInput, duration, reason, true, true, bannedAccounts, true);

                if (duration.HasValue)
                    Player.Message(player, msg("TempIpAndAccountsBanSuccess", player.UserIDString, targetInput, FormatDuration(duration.Value), bannedAccounts));
                else
                    Player.Message(player, msg("IpAndAccountsBanSuccess", player.UserIDString, targetInput, bannedAccounts));

                return;
            }

            if (!TryResolveSteamId(targetInput, out var steamId, out var displayName, out var ip, out var onlinePlayer))
            {
                Player.Message(player, msg("PlayerNotFound", player.UserIDString));
                return;
            }

            if (permission.UserHasPermission(steamId, permPreventBanIp))
            {
                Player.Message(player, msg("TargetProtectedFromBanIp", player.UserIDString, displayName ?? steamId));
                return;
            }

            AddOrUpdatePlayerBan(steamId, displayName, reason, duration, player.UserIDString);
            SetNativeBan(steamId, displayName ?? steamId, reason, duration);

            if (IsValidStoredIp(ip))
            {
                AddIpBan(ip, displayName ?? ip, reason, duration, player.UserIDString);
                KickAllPlayersByIp(ip);
            }
            else if (onlinePlayer != null)
            {
                onlinePlayer.IPlayer.Kick(BuildLoginBanMessage(steamId, null, onlinePlayer.UserIDString));
            }

            NotifyBanEvent(displayName ?? steamId, steamId, ip, duration, reason, true, false, 0, true);

            string shownIp = IsValidStoredIp(ip) ? ip : "unknown";

            if (duration.HasValue)
                Player.Message(player, msg("TempBanIpSuccess", player.UserIDString, displayName ?? steamId, shownIp, FormatDuration(duration.Value)));
            else
                Player.Message(player, msg("BanIpSuccess", player.UserIDString, displayName ?? steamId, shownIp));
        }

        [ChatCommand("unban")]
        private void cmdUnban(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permUnban))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                Player.Message(player, msg("UnbanUsage", player.UserIDString));
                return;
            }

            if (!TryResolveUnbanTarget(args[0], out var steamId, out var ip))
            {
                Player.Message(player, msg("NotBanned", player.UserIDString));
                return;
            }

            if (!string.IsNullOrWhiteSpace(ip))
            {
                bool removedIpBan = moderationData.IpBans.Remove(ip);
                if (!removedIpBan)
                {
                    Player.Message(player, msg("NotBanned", player.UserIDString));
                    return;
                }

                SaveModerationData();
                NotifyUnbanEvent(ip, true, true);
                Player.Message(player, msg("UnbanIpSuccess", player.UserIDString, ip));
                return;
            }

            if (!string.IsNullOrWhiteSpace(steamId))
            {
                moderationData.PlayerBans.TryGetValue(steamId, out var banRecordBeforeRemove);

                string displayName =
                    banRecordBeforeRemove != null &&
                    !string.IsNullOrWhiteSpace(banRecordBeforeRemove.DisplayName) &&
                    !string.Equals(banRecordBeforeRemove.DisplayName, steamId, StringComparison.OrdinalIgnoreCase)
                        ? banRecordBeforeRemove.DisplayName
                        : GetKnownDisplayName(steamId);

                bool removedPlayerBan = moderationData.PlayerBans.Remove(steamId);
                bool removedIpBan = false;
                string linkedIp = null;

                if (moderationData.LastKnownIps.TryGetValue(steamId, out linkedIp) && IsValidStoredIp(linkedIp))
                    removedIpBan = moderationData.IpBans.Remove(linkedIp);
                else
                    linkedIp = null;

                if (!removedPlayerBan && !removedIpBan)
                {
                    Player.Message(player, msg("NotBanned", player.UserIDString));
                    return;
                }

                if (removedPlayerBan)
                    RemoveNativeBan(steamId);

                SaveModerationData();

                if (removedPlayerBan && removedIpBan)
                    NotifyUnbanPlayerAndIpEvent(displayName, steamId, linkedIp, true);
                else if (removedPlayerBan)
                    NotifyUnbanEvent(displayName, false, true, steamId);
                else if (removedIpBan)
                    NotifyUnbanEvent(linkedIp, true, true);

                if (removedPlayerBan && removedIpBan)
                    Player.Message(player, msg("UnbanPlayerAndIpSuccess", player.UserIDString, displayName, linkedIp));
                else if (removedPlayerBan)
                    Player.Message(player, msg("UnbanPlayerSuccess", player.UserIDString, displayName));
                else if (removedIpBan)
                    Player.Message(player, msg("UnbanIpSuccess", player.UserIDString, linkedIp));
            }
        }

        [ChatCommand("banlist")]
        private void cmdBanList(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permBanList))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            string mode = "all";
            int page = 1;

            if (args.Length >= 1)
                mode = args[0].ToLower();

            if (args.Length >= 2 && !int.TryParse(args[1], out page))
            {
                Player.Message(player, msg("BanListUsage", player.UserIDString));
                return;
            }

            if (page < 1)
                page = 1;

            var entries = GetBanListEntries(mode).ToList();
            if (entries.Count == 0)
            {
                Player.Message(player, msg("BanListEmpty", player.UserIDString));
                return;
            }

            int totalPages = (int)Math.Ceiling(entries.Count / (double)banListPageSize);
            page = Math.Min(page, totalPages);

            Player.Message(player, msg("BanListHeader", player.UserIDString, mode, page, totalPages));

            int start = (page - 1) * banListPageSize;
            var pageEntries = entries.Skip(start).Take(banListPageSize).ToList();

            for (int i = 0; i < pageEntries.Count; i++)
            {
                var entry = pageEntries[i];
                var ban = entry.Value;
                int index = start + i + 1;

                if (ban.ExpiresAt <= 0)
                {
                    Player.Message(player, msg("BanListEntryPermanent", player.UserIDString,
                        index,
                        ban.DisplayName,
                        ban.Target,
                        ban.Reason));
                }
                else
                {
                    var remaining = TimeSpan.FromSeconds(Math.Max(0, ban.ExpiresAt - UnixNow()));
                    Player.Message(player, msg("BanListEntryTemporary", player.UserIDString,
                        index,
                        ban.DisplayName,
                        ban.Target,
                        FormatDuration(remaining),
                        ban.Reason));
                }
            }
        }

        ////////////////////////////////////////////////////////////
        // General Methods
        ////////////////////////////////////////////////////////////

        private bool GetSave()
        {
            if (newSave || BuildingManager.server.buildingDictionary.Count == 0)
            {
                return true;
            }

            return false;
        }

        private void ToggleNoClip(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
            if (!player.IsFlying)
            {
                timer.Once(0.2f, () => {
                    if (player == null || !player.IsConnected) return;
                    player.SendConsoleCommand("noclip");
                    Player.Message(player, msg("FlyEnabled", player.UserIDString));
                });
            }
            else
            {
                player.SendConsoleCommand("noclip");
                Player.Message(player, msg("FlyDisabled", player.UserIDString));
            }

            user.NoClip = !player.IsFlying;
            SavePlayerInfo(player, user);
        }

        private void ToggleGodMode(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
            if (!player.IsGod())
            {
                timer.Once(0.2f, () => {
                    if (player == null || !player.IsConnected) return;
                    player.SendConsoleCommand("setinfo \"global.god\" \"True\"");
                    Player.Message(player, msg("GodEnabled", player.UserIDString));
                });
            }
            else
            {
                player.SendConsoleCommand("setinfo \"global.god\" \"False\"");
                Player.Message(player, msg("GodDisabled", player.UserIDString));
            }

            user.GodMode = !player.IsGod();
            SavePlayerInfo(player, user);
        }

        private void DisconnectTeleport(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            var userTeleport = user.Teleport.ToVector3();
            var position = userTeleport == Vector3.zero ? defaultPos : userTeleport;

            if (position == Vector3.zero)
            {
                position = new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) - 5f, player.transform.position.z);
            }

            player.Teleport(position);

            float terrainHeight = TerrainMeta.HeightMap.GetHeight(player.transform.position);
            bool isUnderTerrain = player.transform.position.y < terrainHeight || player.IsHeadUnderwater();

            if (isUnderTerrain)
            {
                player.metabolism.temperature.min = 20;
                player.metabolism.temperature.max = 20;
                player.metabolism.radiation_poison.max = 0;
                player.metabolism.oxygen.min = 1;
                player.metabolism.wetness.max = 0;
                player.metabolism.calories.min = player.metabolism.calories.value;
                player.metabolism.isDirty = true;
                player.metabolism.SendChangesToClient();
                user.UnderTerrain = true;
                SavePlayerInfo(player, user);
            }
        }

        private void NoUnderTerrain(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            float terrainHeight = TerrainMeta.HeightMap.GetHeight(player.transform.position);
            bool isUnderTerrain = player.transform.position.y < terrainHeight || player.IsHeadUnderwater();

            if (isUnderTerrain)
            {
                float newY = terrainHeight + 2f;
                player.Teleport(new Vector3(player.transform.position.x, newY, player.transform.position.z));
                player.SendNetworkUpdateImmediate();
                player.metabolism.temperature.min = -100;
                player.metabolism.temperature.max = 100;
                player.metabolism.radiation_poison.max = 500;
                player.metabolism.oxygen.min = 0;
                player.metabolism.calories.min = 0;
                player.metabolism.wetness.max = 1;
                player.metabolism.SendChangesToClient();
                user.UnderTerrain = false;
                SavePlayerInfo(player, user);
            }
        }

        private string FormatPosition(Vector3 position)
        {
            string x = position.x.ToString("N2");
            string y = position.y.ToString("N2");
            string z = position.z.ToString("N2");

            return $"{x} {y} {z}";
        }

        private void SaveInventory(BasePlayer player, bool snapshotOnly = false)
        {
            PlayerInfoItems userItems = LoadPlayerInfoItems(player);

            if (userItems == null)
                return;

            List<Item> itemList = Pool.Get<List<Item>>();
            int num = player.inventory.GetAllItems(itemList);
            Pool.FreeUnmanaged(ref itemList);

            if (num == 0)
            {
                return;
            }

            var items = new List<AdminUtilitiesItem>();

            AddItemsFromContainer(player.inventory.containerWear, "wear", items, snapshotOnly);
            AddItemsFromContainer(player.inventory.containerMain, "main", items, snapshotOnly);
            AddItemsFromContainer(player.inventory.containerBelt, "belt", items, snapshotOnly);

            if (items.Count == 0)
            {
                return;
            }

            if (!snapshotOnly)
                ItemManager.DoRemoves();

            userItems.Items.Clear();
            userItems.Items.AddRange(items);
            userItems.SnapshotOnly = snapshotOnly;
            SavePlayerInfoItems(player, userItems);
        }

        private void RestoreSavedInventory(BasePlayer player)
        {
            if (player == null || !player.IsConnected)
                return;

            PlayerInfo user = LoadPlayerInfo(player);
            if (user == null)
                return;

            PlayerInfoItems userItems = LoadPlayerInfoItems(player);
            if (userItems == null)
                return;

            if (!HasPermission(player, permInventory) || !user.SaveInventory || userItems.Items.Count == 0)
                return;

            List<Item> currentItems = Pool.Get<List<Item>>();
            int currentCount = player.inventory.GetAllItems(currentItems);
            Pool.FreeUnmanaged(ref currentItems);

            bool hasOnlyDefaultItems = HasOnlyDefaultSpawnItems(player);

            if (userItems.SnapshotOnly)
            {
                if (currentCount > 0 && !hasOnlyDefaultItems)
                {
                    userItems.Items.Clear();
                    userItems.SnapshotOnly = false;
                    SavePlayerInfoItems(player, userItems);
                    return;
                }

                if (hasOnlyDefaultItems)
                    player.inventory.Strip();
            }
            else
            {
                if (currentCount > 0 && !hasOnlyDefaultItems)
                    return;

                if (hasOnlyDefaultItems)
                    player.inventory.Strip();
            }

            foreach (var aui in userItems.Items)
            {
                if (aui.amount > 0)
                    AdminUtilitiesItem.Restore(player, aui);
            }

            userItems.Items.Clear();
            userItems.SnapshotOnly = false;
            SavePlayerInfoItems(player, userItems);
        }

        private bool HasOnlyDefaultSpawnItems(BasePlayer player)
        {
            if (player == null)
                return false;

            var rockDef = ItemManager.FindItemDefinition("rock");
            var torchDef = ItemManager.FindItemDefinition("torch");

            if (rockDef == null || torchDef == null)
                return false;

            List<Item> items = Pool.Get<List<Item>>();
            int count = player.inventory.GetAllItems(items);
            Pool.FreeUnmanaged(ref items);

            return count == 2
                && player.inventory.GetAmount(rockDef.itemid) == 1
                && player.inventory.GetAmount(torchDef.itemid) == 1;
        }

        private void AddItemsFromContainer(ItemContainer container, string containerName, List<AdminUtilitiesItem> items, bool snapshotOnly = false)
        {
            foreach (var item in container.itemList.ToList())
            {
                items.Add(new AdminUtilitiesItem(containerName, item));

                if (!snapshotOnly)
                    item.Remove();
            }
        }

        private void UpdateLastKnownPlayerIdentity(BasePlayer player)
        {
            if (player == null)
                return;

            bool changed = false;

            if (!string.IsNullOrWhiteSpace(player.displayName))
            {
                if (!moderationData.LastKnownNames.TryGetValue(player.UserIDString, out var oldName) || oldName != player.displayName)
                {
                    moderationData.LastKnownNames[player.UserIDString] = player.displayName;
                    changed = true;
                }
            }

            if (player.IPlayer != null && IsValidStoredIp(player.IPlayer.Address))
            {
                if (!moderationData.LastKnownIps.TryGetValue(player.UserIDString, out var oldIp) || oldIp != player.IPlayer.Address)
                {
                    moderationData.LastKnownIps[player.UserIDString] = player.IPlayer.Address;
                    changed = true;
                }
            }

            if (changed)
                SaveModerationData();
        }

        private long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private bool IsExpired(BanRecord ban)
        {
            return ban != null && ban.ExpiresAt > 0 && UnixNow() >= ban.ExpiresAt;
        }

        private void CleanupExpiredBans()
        {
            bool changed = false;

            foreach (var key in moderationData.PlayerBans.Where(x => IsExpired(x.Value)).Select(x => x.Key).ToList())
            {
                moderationData.PlayerBans.Remove(key);
                changed = true;
            }

            foreach (var key in moderationData.IpBans.Where(x => IsExpired(x.Value)).Select(x => x.Key).ToList())
            {
                moderationData.IpBans.Remove(key);
                changed = true;
            }

            if (changed)
                SaveModerationData();
        }

        private string MaskIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return "unknown";

            var parts = ip.Split('.');
            if (parts.Length == 4)
                return $"{parts[0]}.{parts[1]}.xxx.xxx";

            return ip;
        }

        private bool IsValidStoredIp(string ip)
        {
            return !string.IsNullOrWhiteSpace(ip)
                && ip != "0"
                && ip != "0.0.0.0"
                && IsIpAddress(ip);
        }

        private bool TryParseDuration(string input, out TimeSpan duration)
        {
            duration = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var match = Regex.Match(input.Trim().ToLower(), @"^(?<value>\d+)(?<unit>[smhdw])$");
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups["value"].Value, out int value) || value <= 0)
                return false;

            switch (match.Groups["unit"].Value)
            {
                case "s": duration = TimeSpan.FromSeconds(value); return true;
                case "m": duration = TimeSpan.FromMinutes(value); return true;
                case "h": duration = TimeSpan.FromHours(value); return true;
                case "d": duration = TimeSpan.FromDays(value); return true;
                case "w": duration = TimeSpan.FromDays(value * 7); return true;
            }

            return false;
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return $"{Math.Floor(duration.TotalDays)}d";
            if (duration.TotalHours >= 1)
                return $"{Math.Floor(duration.TotalHours)}h";
            if (duration.TotalMinutes >= 1)
                return $"{Math.Floor(duration.TotalMinutes)}m";

            return $"{Math.Max(1, Math.Floor(duration.TotalSeconds))}s";
        }

        private BasePlayer FindOnlinePlayer(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (ulong.TryParse(input, out ulong userId))
                return BasePlayer.activePlayerList.FirstOrDefault(p => p.userID == userId);

            return BasePlayer.activePlayerList.FirstOrDefault(p =>
                p.UserIDString == input ||
                p.displayName.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                p.displayName.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string GetKnownDisplayName(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return steamId;

            if (moderationData.PlayerBans.TryGetValue(steamId, out var banRecord) &&
                !string.IsNullOrWhiteSpace(banRecord?.DisplayName) &&
                !string.Equals(banRecord.DisplayName, steamId, StringComparison.OrdinalIgnoreCase))
            {
                return banRecord.DisplayName;
            }

            if (moderationData.LastKnownNames.TryGetValue(steamId, out var knownName) &&
                !string.IsNullOrWhiteSpace(knownName) &&
                !string.Equals(knownName, steamId, StringComparison.OrdinalIgnoreCase))
            {
                return knownName;
            }

            var onlinePlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.UserIDString == steamId);
            if (onlinePlayer != null && !string.IsNullOrWhiteSpace(onlinePlayer.displayName))
                return onlinePlayer.displayName;

            var sleepingPlayer = BasePlayer.sleepingPlayerList.FirstOrDefault(p => p.UserIDString == steamId);
            if (sleepingPlayer != null && !string.IsNullOrWhiteSpace(sleepingPlayer.displayName))
                return sleepingPlayer.displayName;

            if (ulong.TryParse(steamId, out var userId))
            {
                var nativeUser = ServerUsers.Get(userId);
                if (nativeUser != null &&
                    !string.IsNullOrWhiteSpace(nativeUser.username) &&
                    !string.Equals(nativeUser.username, steamId, StringComparison.OrdinalIgnoreCase))
                {
                    return nativeUser.username;
                }
            }

            return steamId;
        }

        private void CacheKnownDisplayName(string steamId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(displayName))
                return;

            if (string.Equals(displayName, steamId, StringComparison.OrdinalIgnoreCase))
                return;

            if (!moderationData.LastKnownNames.TryGetValue(steamId, out var oldName) ||
                !string.Equals(oldName, displayName, StringComparison.Ordinal))
            {
                moderationData.LastKnownNames[steamId] = displayName;
                SaveModerationData();
            }
        }

        private bool IsIpAddress(string input)
        {
            return !string.IsNullOrWhiteSpace(input) && Regex.IsMatch(input, @"^\d{1,3}(\.\d{1,3}){3}$");
        }

        private bool TryResolveSteamId(string input, out string steamId, out string displayName, out string ip, out BasePlayer onlinePlayer)
        {
            steamId = null;
            displayName = null;
            ip = null;
            onlinePlayer = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (ulong.TryParse(input, out ulong userId))
            {
                steamId = userId.ToString();

                onlinePlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.userID == userId);
                BasePlayer sleepingPlayer = BasePlayer.sleepingPlayerList.FirstOrDefault(p => p.userID == userId);

                if (moderationData.LastKnownIps.TryGetValue(steamId, out ip) && !IsValidStoredIp(ip))
                    ip = null;

                if (onlinePlayer != null)
                {
                    displayName = onlinePlayer.displayName;
                    ip = onlinePlayer.IPlayer?.Address;
                    CacheKnownDisplayName(steamId, displayName);
                    return true;
                }

                if (sleepingPlayer != null && !string.IsNullOrWhiteSpace(sleepingPlayer.displayName))
                {
                    displayName = sleepingPlayer.displayName;
                    CacheKnownDisplayName(steamId, displayName);
                    return true;
                }

                if (moderationData.LastKnownNames.TryGetValue(steamId, out var knownName) &&
                    !string.IsNullOrWhiteSpace(knownName))
                {
                    displayName = knownName;
                    return true;
                }

                if (moderationData.PlayerBans.TryGetValue(steamId, out var existingBan) &&
                    !string.IsNullOrWhiteSpace(existingBan.DisplayName))
                {
                    displayName = existingBan.DisplayName;
                    return true;
                }

                if (ulong.TryParse(steamId, out var nativeUserId))
                {
                    var nativeUser = ServerUsers.Get(nativeUserId);
                    if (nativeUser != null && !string.IsNullOrWhiteSpace(nativeUser.username))
                    {
                        displayName = nativeUser.username;
                        CacheKnownDisplayName(steamId, displayName);
                        return true;
                    }
                }

                displayName = steamId;
                return true;
            }

            onlinePlayer = FindOnlinePlayer(input);
            if (onlinePlayer == null)
                return false;

            steamId = onlinePlayer.UserIDString;
            displayName = onlinePlayer.displayName;
            ip = onlinePlayer.IPlayer?.Address;
            CacheKnownDisplayName(steamId, displayName);
            return true;
        }

        private void AddIpBan(string ip, string displayName, string reason, TimeSpan? duration, string source)
        {
            moderationData.IpBans[ip] = new BanRecord
            {
                Target = ip,
                DisplayName = displayName ?? ip,
                Reason = string.IsNullOrWhiteSpace(reason) ? defaultBanReason : reason,
                Source = source,
                CreatedAt = UnixNow(),
                ExpiresAt = duration.HasValue ? UnixNow() + (long)duration.Value.TotalSeconds : 0
            };

            SaveModerationData();
        }

        private bool TryGetActiveBan(string steamId, string ip, out BanRecord ban)
        {
            CleanupExpiredBans();

            ban = null;

            if (!string.IsNullOrWhiteSpace(steamId) &&
                moderationData.PlayerBans.TryGetValue(steamId, out ban) &&
                !IsExpired(ban))
                return true;

            if (!string.IsNullOrWhiteSpace(ip) &&
                moderationData.IpBans.TryGetValue(ip, out ban) &&
                !IsExpired(ban))
                return true;

            ban = null;
            return false;
        }

        private string BuildBanReasonLine(BanRecord ban, string langId = null)
        {
            if (ban == null || string.IsNullOrWhiteSpace(ban.Reason))
                return msg("BanReasonNoneLine", langId);

            return msg("BanReasonLine", langId, ban.Reason);
        }

        private void KickAllPlayersByIp(string ip)
        {
            foreach (var target in BasePlayer.activePlayerList.ToList())
            {
                if (target?.IPlayer == null)
                    continue;

                if (string.Equals(target.IPlayer.Address, ip, StringComparison.OrdinalIgnoreCase))
                    target.IPlayer.Kick(BuildLoginBanMessage(target.UserIDString, ip, target.UserIDString));
            }
        }

        private IEnumerable<KeyValuePair<string, BanRecord>> GetBanListEntries(string mode)
        {
            CleanupExpiredBans();

            mode = (mode ?? "all").ToLower();

            if (mode == "players")
                return moderationData.PlayerBans.OrderBy(x => x.Value.DisplayName);

            if (mode == "ips")
                return moderationData.IpBans.OrderBy(x => x.Value.DisplayName);

            return moderationData.PlayerBans
                .Concat(moderationData.IpBans)
                .OrderBy(x => x.Value.DisplayName);
        }

        private bool TryParseModerationArgs(string[] args, out string targetInput, out TimeSpan? duration, out string reason)
        {
            targetInput = null;
            duration = null;
            reason = null;

            if (args == null || args.Length < 1)
                return false;

            targetInput = args[0];

            if (args.Length >= 2 && TryParseDuration(args[1], out var parsedDuration))
            {
                duration = parsedDuration;
                reason = string.Join(" ", args.Skip(2));
            }
            else
            {
                reason = string.Join(" ", args.Skip(1));
            }

            if (string.IsNullOrWhiteSpace(reason))
                reason = defaultBanReason;

            return !string.IsNullOrWhiteSpace(targetInput);
        }

        private bool TryResolveUnbanTarget(string input, out string steamId, out string ip)
        {
            steamId = null;
            ip = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (IsIpAddress(input))
            {
                ip = input;
                return true;
            }

            if (TryResolveSteamId(input, out var resolvedSteamId, out _, out _, out _))
            {
                steamId = resolvedSteamId;
                return true;
            }

            var existing = moderationData.PlayerBans
                .FirstOrDefault(x =>
                    x.Key.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(x.Value.DisplayName) &&
                     x.Value.DisplayName.Equals(input, StringComparison.OrdinalIgnoreCase)));

            if (!string.IsNullOrWhiteSpace(existing.Key))
            {
                steamId = existing.Key;
                return true;
            }

            return false;
        }

        private IEnumerable<string> GetKnownSteamIdsByIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                yield break;

            foreach (var entry in moderationData.LastKnownIps)
            {
                if (string.Equals(entry.Value, ip, StringComparison.OrdinalIgnoreCase))
                    yield return entry.Key;
            }
        }

        private string BuildLoginBanMessage(string steamId, string ip, string langId = null)
        {
            CleanupExpiredBans();

            moderationData.PlayerBans.TryGetValue(steamId ?? string.Empty, out var playerBan);
            moderationData.IpBans.TryGetValue(ip ?? string.Empty, out var ipBan);

            if (playerBan != null && IsExpired(playerBan)) playerBan = null;
            if (ipBan != null && IsExpired(ipBan)) ipBan = null;

            if (playerBan == null && ipBan == null)
                return null;

            List<string> lines = new List<string>();

            if (playerBan != null && ipBan != null)
                lines.Add(msg("LoginBanAccountAndIp", langId));
            else if (playerBan != null)
                lines.Add(msg("LoginBanAccountOnly", langId));
            else
                lines.Add(msg("LoginBanIpOnly", langId));

            if (playerBan != null)
            {
                if (playerBan.ExpiresAt <= 0)
                    lines.Add(msg("AccountBanLinePermanent", langId));
                else
                    lines.Add(msg("AccountBanLineTemporary", langId, FormatDuration(TimeSpan.FromSeconds(Math.Max(0, playerBan.ExpiresAt - UnixNow())))));

                lines.Add(BuildBanReasonLine(playerBan, langId));
            }

            if (ipBan != null)
            {
                if (ipBan.ExpiresAt <= 0)
                    lines.Add(msg("IpBanLinePermanent", langId));
                else
                    lines.Add(msg("IpBanLineTemporary", langId, FormatDuration(TimeSpan.FromSeconds(Math.Max(0, ipBan.ExpiresAt - UnixNow())))));

                lines.Add(BuildBanReasonLine(ipBan, langId));
            }

            return string.Join("\n", lines);
        }

        private void AddOrUpdatePlayerBan(string steamId, string displayName, string reason, TimeSpan? duration, string source)
        {
            string finalDisplayName = displayName;

            if (string.IsNullOrWhiteSpace(finalDisplayName) ||
                string.Equals(finalDisplayName, steamId, StringComparison.OrdinalIgnoreCase))
            {
                finalDisplayName = GetKnownDisplayName(steamId);
            }

            if (string.IsNullOrWhiteSpace(finalDisplayName))
                finalDisplayName = steamId;

            moderationData.PlayerBans[steamId] = new BanRecord
            {
                Target = steamId,
                DisplayName = finalDisplayName,
                Reason = string.IsNullOrWhiteSpace(reason) ? defaultBanReason : reason,
                Source = source,
                CreatedAt = UnixNow(),
                ExpiresAt = duration.HasValue ? UnixNow() + (long)duration.Value.TotalSeconds : 0
            };

            CacheKnownDisplayName(steamId, finalDisplayName);
            SaveModerationData();
        }

        private int BanKnownAccountsOnIp(string ip, TimeSpan? duration, string reason, string source, string excludeSteamId = null)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return 0;

            int count = 0;

            foreach (var kvp in moderationData.LastKnownIps.ToList())
            {
                string steamId = kvp.Key;
                string knownIp = kvp.Value;

                if (knownIp != ip)
                    continue;

                if (!string.IsNullOrWhiteSpace(excludeSteamId) && steamId == excludeSteamId)
                    continue;

                string knownName = moderationData.LastKnownNames.TryGetValue(steamId, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName)
                    ? cachedName
                    : steamId;

                AddOrUpdatePlayerBan(steamId, knownName, reason, duration, source);
                SetNativeBan(steamId, knownName, reason, duration);
                count++;
            }

            return count;
        }

        private void SetNativeBan(string steamId, string displayName, string reason, TimeSpan? duration = null)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            if (!ulong.TryParse(steamId, out var userId))
                return;

            string safeName = string.IsNullOrWhiteSpace(displayName) ? steamId : displayName;
            string safeReason = string.IsNullOrWhiteSpace(reason) ? defaultBanReason : reason;

            // Scoate banul vechi, ca să poți trece corect permanent <-> temporary
            ServerUsers.Remove(userId);

            if (duration.HasValue)
            {
                long expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)duration.Value.TotalSeconds;
                ServerUsers.Set(userId, ServerUsers.UserGroup.Banned, safeName, safeReason, expiry);
            }
            else
            {
                ServerUsers.Set(userId, ServerUsers.UserGroup.Banned, safeName, safeReason);
            }

            ServerUsers.Save();
        }

        private void RemoveNativeBan(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            if (!ulong.TryParse(steamId, out var userId))
                return;

            var user = ServerUsers.Get(userId);
            if (user != null && user.@group == ServerUsers.UserGroup.Banned)
            {
                ServerUsers.Remove(userId);
                ServerUsers.Save();
            }
        }

        private void HandleConsoleKick(ConsoleSystem.Arg arg, string[] args)
        {
            if (args == null || args.Length < 1)
            {
                ReplyConsole(arg, "Usage: kick <name/steamid> [reason]");
                return;
            }

            var target = FindOnlinePlayer(args[0]);
            if (target == null)
            {
                ReplyConsole(arg, msg("PlayerNotFound", null));
                return;
            }

            string reason = string.Join(" ", args.Skip(1));
            if (string.IsNullOrWhiteSpace(reason))
                reason = defaultKickReason;

            target.IPlayer.Kick(reason);
            NotifyKickEvent(target.displayName, target.UserIDString, reason, false);
            ReplyConsole(arg, msg("KickSuccess", null, target.displayName));
        }

        private void HandleConsoleBan(ConsoleSystem.Arg arg, string[] args, bool includeIp)
        {
            if (!TryParseModerationArgs(args, out var targetInput, out var duration, out var reason))
            {
                ReplyConsole(arg, includeIp
                    ? "Usage: banip <name/steamid/ip> [duration] [reason]"
                    : "Usage: ban <name/steamid/ip> [duration] [reason]");
                return;
            }

            if (includeIp)
            {
                if (IsIpAddress(targetInput))
                {
                    AddIpBan(targetInput, targetInput, reason, duration, "Console");
                    KickAllPlayersByIp(targetInput);
                    int bannedAccounts = BanKnownAccountsOnIp(targetInput, duration, reason, "Console");

                    NotifyBanEvent(targetInput, null, targetInput, duration, reason, true, true, bannedAccounts, false);
                    if (duration.HasValue)
                        ReplyConsole(arg, msg("TempIpAndAccountsBanSuccess", null, targetInput, FormatDuration(duration.Value), bannedAccounts));
                    else
                        ReplyConsole(arg, msg("IpAndAccountsBanSuccess", null, targetInput, bannedAccounts));
                    return;
                }

                if (!TryResolveSteamId(targetInput, out var steamId, out var displayName, out var ip, out var onlinePlayer))
                {
                    ReplyConsole(arg, msg("PlayerNotFound", null));
                    return;
                }

                AddOrUpdatePlayerBan(steamId, displayName, reason, duration, "Console");
                SetNativeBan(steamId, displayName ?? steamId, reason, duration);

                if (!string.IsNullOrWhiteSpace(ip))
                {
                    AddIpBan(ip, displayName ?? ip, reason, duration, "Console");
                    KickAllPlayersByIp(ip);
                }
                else
                {
                    onlinePlayer?.IPlayer?.Kick(BuildLoginBanMessage(steamId, null, onlinePlayer?.UserIDString));
                }

                NotifyBanEvent(displayName ?? steamId, steamId, ip, duration, reason, true, false, 0, false);
                string shownIp = IsValidStoredIp(ip) ? ip : "unknown";

                if (duration.HasValue)
                    ReplyConsole(arg, msg("TempBanIpSuccess", null, displayName ?? steamId, shownIp, FormatDuration(duration.Value)));
                else
                    ReplyConsole(arg, msg("BanIpSuccess", null, displayName ?? steamId, shownIp));
                return;
            }

            if (IsIpAddress(targetInput))
            {
                AddIpBan(targetInput, targetInput, reason, duration, "Console");
                KickAllPlayersByIp(targetInput);

                NotifyBanEvent(targetInput, null, targetInput, duration, reason, false, true, 0, false);

                if (duration.HasValue)
                    ReplyConsole(arg, msg("TempIpOnlyBanSuccess", null, targetInput, FormatDuration(duration.Value)));
                else
                    ReplyConsole(arg, msg("IpOnlyBanSuccess", null, targetInput));

                return;
            }

            if (!TryResolveSteamId(targetInput, out var steamId2, out var displayName2, out var ip2, out var onlinePlayer2))
            {
                ReplyConsole(arg, "Player not found.");
                return;
            }

            AddOrUpdatePlayerBan(steamId2, displayName2, reason, duration, "Console");
            SetNativeBan(steamId2, displayName2 ?? steamId2, reason, duration);
            onlinePlayer2?.IPlayer?.Kick(BuildLoginBanMessage(steamId2, ip2, onlinePlayer2?.UserIDString));

            NotifyBanEvent(displayName2 ?? steamId2, steamId2, ip2, duration, reason, false, false, 0, false);
            if (duration.HasValue)
                ReplyConsole(arg, msg("TempBanSuccess", null, displayName2 ?? steamId2, FormatDuration(duration.Value)));
            else
                ReplyConsole(arg, msg("BanSuccess", null, displayName2 ?? steamId2));
        }

        private void HandleConsoleUnban(ConsoleSystem.Arg arg, string[] args)
        {
            if (args == null || args.Length != 1)
            {
                ReplyConsole(arg, "Usage: unban <name/steamid/ip>");
                return;
            }

            if (!TryResolveUnbanTarget(args[0], out var steamId, out var ip))
            {
                ReplyConsole(arg, msg("NotBanned", null));
                return;
            }

            if (!string.IsNullOrWhiteSpace(ip))
            {
                bool removedIpBan = moderationData.IpBans.Remove(ip);

                if (!removedIpBan)
                {
                    ReplyConsole(arg, msg("NotBanned", null));
                    return;
                }

                SaveModerationData();
                NotifyUnbanEvent(ip, true, false);
                ReplyConsole(arg, msg("UnbanIpSuccess", null, ip));
                return;
            }

            if (!string.IsNullOrWhiteSpace(steamId))
            {
                moderationData.PlayerBans.TryGetValue(steamId, out var banRecordBeforeRemove);

                string displayName =
                    banRecordBeforeRemove != null &&
                    !string.IsNullOrWhiteSpace(banRecordBeforeRemove.DisplayName) &&
                    !string.Equals(banRecordBeforeRemove.DisplayName, steamId, StringComparison.OrdinalIgnoreCase)
                        ? banRecordBeforeRemove.DisplayName
                        : GetKnownDisplayName(steamId);

                bool removedPlayerBan = moderationData.PlayerBans.Remove(steamId);
                bool removedIpBan = false;
                string linkedIp = null;

                if (moderationData.LastKnownIps.TryGetValue(steamId, out linkedIp) && IsValidStoredIp(linkedIp))
                    removedIpBan = moderationData.IpBans.Remove(linkedIp);
                else
                    linkedIp = null;

                if (!removedPlayerBan && !removedIpBan)
                {
                    ReplyConsole(arg, msg("NotBanned", null));
                    return;
                }

                if (removedPlayerBan)
                    RemoveNativeBan(steamId);

                SaveModerationData();

                if (removedPlayerBan && removedIpBan)
                    NotifyUnbanPlayerAndIpEvent(displayName, steamId, linkedIp, false);
                else if (removedPlayerBan)
                    NotifyUnbanEvent(displayName, false, false, steamId);
                else if (removedIpBan)
                    NotifyUnbanEvent(linkedIp, true, false);

                if (removedPlayerBan && removedIpBan)
                    ReplyConsole(arg, msg("UnbanPlayerAndIpSuccess", null, displayName, linkedIp));
                else if (removedPlayerBan)
                    ReplyConsole(arg, msg("UnbanPlayerSuccess", null, displayName));
                else if (removedIpBan)
                    ReplyConsole(arg, msg("UnbanIpSuccess", null, linkedIp));
            }
        }

        private string FormatDisplayWithSteamId(string displayName, string steamId)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return steamId ?? string.Empty;

            if (string.IsNullOrWhiteSpace(steamId) ||
                string.Equals(displayName, steamId, StringComparison.OrdinalIgnoreCase))
                return displayName;

            return $"{displayName} ({steamId})";
        }

        private void NotifyUnbanPlayerAndIpEvent(string displayName, string steamId, string ip, bool fromChatCommand = true)
        {
            string shownTargetForChat = displayName;
            string shownTargetForLog = FormatDisplayWithSteamId(displayName, steamId);
            string shownIpForLog = FormatIpForLog(ip);

            if (broadcastBanToChat)
                BroadcastLocalized("GlobalUnbanPlayerAndIpAnnouncement", shownTargetForChat, MaskIp(ip));

            if (logBanToConsole && fromChatCommand)
                Puts($"[Unban] {shownTargetForLog} + IP {shownIpForLog}");
        }

        private bool HasPermission(BasePlayer player, string permName)
        {
            return player != null && player.IPlayer.HasPermission(permName);
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

        private void ReplyConsole(ConsoleSystem.Arg arg, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (arg != null && arg.Connection != null)
            {
                arg.ReplyWith(message);
                return;
            }

            Puts(message);
        }

        private void BroadcastLocalized(string key, params object[] args)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected)
                    continue;

                Player.Message(player, msg(key, player.UserIDString, args));
            }
        }

        private string FormatIpForLog(string ip)
        {
            return IsValidStoredIp(ip) ? ip : "unknown";
        }

        private void NotifyKickEvent(string displayName, string steamId, string reason, bool fromChatCommand = true)
        {
            if (broadcastKickToChat)
                BroadcastLocalized("GlobalKickAnnouncement", displayName, reason);

            if (logKickToConsole && fromChatCommand)
                Puts($"[Kick] {displayName} ({steamId}) | Reason: {reason}");
        }

        private void NotifyBanEvent(string displayName, string steamId, string ip, TimeSpan? duration, string reason, bool includesIp, bool ipOnly, int linkedAccounts = 0, bool fromChatCommand = true)
        {
            if (broadcastBanToChat)
            {
                string maskedIp = MaskIp(ip);

                if (ipOnly)
                {
                    if (duration.HasValue)
                        BroadcastLocalized("GlobalTempIpOnlyBanAnnouncement", maskedIp, FormatDuration(duration.Value), reason);
                    else
                        BroadcastLocalized("GlobalIpOnlyBanAnnouncement", maskedIp, reason);
                }
                else if (includesIp)
                {
                    if (duration.HasValue)
                        BroadcastLocalized("GlobalTempBanIpAnnouncement", displayName, maskedIp, FormatDuration(duration.Value), reason);
                    else
                        BroadcastLocalized("GlobalBanIpAnnouncement", displayName, maskedIp, reason);
                }
                else
                {
                    if (duration.HasValue)
                        BroadcastLocalized("GlobalTempBanAnnouncement", displayName, FormatDuration(duration.Value), reason);
                    else
                        BroadcastLocalized("GlobalBanAnnouncement", displayName, reason);
                }
            }

            if (logBanToConsole && fromChatCommand)
            {
                string shownIp = FormatIpForLog(ip);
                string shownDuration = duration.HasValue ? FormatDuration(duration.Value) : "permanent";
                string shownReason = string.IsNullOrWhiteSpace(reason) ? defaultBanReason : reason;

                if (ipOnly)
                {
                    if (linkedAccounts > 0)
                        Puts($"[BanIP] IP: {shownIp} | Duration: {shownDuration} | Reason: {shownReason} | Native banned linked accounts: {linkedAccounts}");
                    else
                        Puts($"[BanIP] IP: {shownIp} | Duration: {shownDuration} | Reason: {shownReason}");
                }
                else if (includesIp)
                {
                    Puts($"[BanIP] {displayName} ({steamId}) | IP: {shownIp} | Duration: {shownDuration} | Reason: {shownReason}");
                }
                else
                {
                    Puts($"[Ban] {displayName} ({steamId}) | Duration: {shownDuration} | Reason: {shownReason}");
                }
            }
        }

        private void NotifyUnbanEvent(string target, bool isIp, bool fromChatCommand = true, string steamId = null)
        {
            string shownTargetForChat = isIp ? MaskIp(target) : target;
            string shownTargetForLog = isIp ? FormatIpForLog(target) : FormatDisplayWithSteamId(target, steamId);

            if (broadcastBanToChat)
            {
                if (isIp)
                    BroadcastLocalized("GlobalUnbanIpAnnouncement", shownTargetForChat);
                else
                    BroadcastLocalized("GlobalUnbanAnnouncement", shownTargetForChat);
            }

            if (logBanToConsole && fromChatCommand)
            {
                if (isIp)
                    Puts($"[UnbanIP] {shownTargetForLog}");
                else
                    Puts($"[Unban] {shownTargetForLog}");
            }
        }

        ////////////////////////////////////////////////////////////
        // Configs
        ////////////////////////////////////////////////////////////

        private bool ConfigChanged;
        private Vector3 defaultPos;
        private bool wipeItems;
        private bool wipeSettings;
        private bool persistentNoClip;
        private bool persistentGodMode;
        private string defaultKickReason;
        private string defaultBanReason;
        private int banListPageSize;
        private bool broadcastKickToChat;
        private bool broadcastBanToChat;
        private bool logKickToConsole;
        private bool logBanToConsole;

        protected override void LoadDefaultConfig() => PrintWarning("Generating default configuration file...");

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
                ["GodDisabled"] = "You switched GodMode off!",
                ["GodEnabled"] = "You switched GodMode on!",
                ["NoPermission"] = "You do not have permission to use this command.",
                ["PlayerNotFound"] = "Player not found.",
                ["IpUnavailable"] = "No IP could be resolved for that target.",
                ["InvalidDuration"] = "Invalid duration. Use formats like 30m, 2h, 7d, 1w.",
                ["KickUsage"] = "/kick <name/steamid> [reason]",
                ["BanUsage"] = "/ban <name/steamid/ip> [duration] [reason]",
                ["BanIpUsage"] = "/banip <name/steamid/ip> [duration] [reason]",
                ["UnbanUsage"] = "/unban <name/steamid/ip>",
                ["KickSuccess"] = "You kicked <color=orange>{0}</color>.",
                ["BanSuccess"] = "You permanently banned <color=orange>{0}</color>.",
                ["TempBanSuccess"] = "You banned <color=orange>{0}</color> for <color=orange>{1}</color>.",
                ["BanIpSuccess"] = "You banned <color=orange>{0}</color> and their IP <color=orange>{1}</color>.",
                ["TempBanIpSuccess"] = "You banned <color=orange>{0}</color> and their IP <color=orange>{1}</color> for <color=orange>{2}</color>.",
                ["IpOnlyBanSuccess"] = "You banned IP <color=orange>{0}</color>.",
                ["TempIpOnlyBanSuccess"] = "You banned IP <color=orange>{0}</color> for <color=orange>{1}</color>.",
                ["IpAndAccountsBanSuccess"] = "You banned IP <color=orange>{0}</color> and also native banned <color=orange>{1}</color> known account(s) on that IP.",
                ["TempIpAndAccountsBanSuccess"] = "You banned IP <color=orange>{0}</color> for <color=orange>{1}</color> and also native banned <color=orange>{2}</color> known account(s) on that IP.",
                ["UnbanPlayerSuccess"] = "You unbanned player/SteamID <color=orange>{0}</color>.",
                ["UnbanIpSuccess"] = "You unbanned IP <color=orange>{0}</color>.",
                ["UnbanPlayerAndIpSuccess"] = "You unbanned player/SteamID <color=orange>{0}</color> and their linked IP <color=orange>{1}</color>.",
                ["NotBanned"] = "That target is not banned.",
                ["AccountBanLinePermanent"] = "Account ban: permanent.",
                ["AccountBanLineTemporary"] = "Account ban: temporary, {0} remaining.",
                ["IpBanLinePermanent"] = "IP ban: permanent.",
                ["IpBanLineTemporary"] = "IP ban: temporary, {0} remaining.",
                ["BanReasonLine"] = "Reason: {0}",
                ["BanReasonNoneLine"] = "Reason: not specified.",
                ["LoginBanAccountOnly"] = "Your account is banned from this server.",
                ["LoginBanIpOnly"] = "The IP address used by this connection is banned from this server.",
                ["LoginBanAccountAndIp"] = "Both your account and the IP address used by this connection are banned from this server.",
                ["GlobalKickAnnouncement"] = "<color=orange>{0}</color> a primit kick de pe server. Motiv: <color=orange>{1}</color>",
                ["GlobalBanAnnouncement"] = "<color=orange>{0}</color> a primit ban permanent pe server. Motiv: <color=orange>{1}</color>",
                ["GlobalTempBanAnnouncement"] = "<color=orange>{0}</color> a primit ban pentru <color=orange>{1}</color>. Motiv: <color=orange>{2}</color>",
                ["GlobalIpOnlyBanAnnouncement"] = "IP-ul <color=orange>{0}</color> a fost banat permanent pe server. Motiv: <color=orange>{1}</color>",
                ["GlobalTempIpOnlyBanAnnouncement"] = "IP-ul <color=orange>{0}</color> a fost banat pentru <color=orange>{1}</color>. Motiv: <color=orange>{2}</color>",
                ["GlobalBanIpAnnouncement"] = "<color=orange>{0}</color> și IP-ul său <color=orange>{1}</color> au fost banate permanent. Motiv: <color=orange>{2}</color>",
                ["GlobalTempBanIpAnnouncement"] = "<color=orange>{0}</color> și IP-ul său <color=orange>{1}</color> au fost banate pentru <color=orange>{2}</color>. Motiv: <color=orange>{3}</color>",
                ["GlobalUnbanAnnouncement"] = "<color=orange>{0}</color> a primit unban pe server.",
                ["GlobalUnbanIpAnnouncement"] = "IP-ul <color=orange>{0}</color> a primit unban pe server.",
                ["GlobalUnbanPlayerAndIpAnnouncement"] = "<color=orange>{0}</color> și IP-ul său <color=orange>{1}</color> au primit unban pe server.",
                ["BanListUsage"] = "/banlist [all/players/ips] [page]",
                ["BanListEmpty"] = "There are no banned entries to display.",
                ["BanListHeader"] = "Ban list (<color=orange>{0}</color>) - page <color=orange>{1}</color>/<color=orange>{2}</color>:",
                ["BanListEntryPermanent"] = "<color=orange>{0}.</color> [{1}] <color=orange>{2}</color> - permanent - Reason: <color=orange>{3}</color>",
                ["BanListEntryTemporary"] = "<color=orange>{0}.</color> [{1}] <color=orange>{2}</color> - <color=orange>{3}</color> remaining - Reason: <color=orange>{4}</color>",
                ["TargetProtectedFromKick"] = "<color=orange>{0}</color> is protected from kick.",
                ["TargetProtectedFromBan"] = "<color=orange>{0}</color> is protected from ban.",
                ["TargetProtectedFromBanIp"] = "<color=orange>{0}</color> is protected from banip."
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
                ["FlyEnabled"] = "Ai activat NoClip-ul!",
                ["GodDisabled"] = "Ai dezactivat GodMode-ul!",
                ["GodEnabled"] = "Ai activat GodMode-ul!",
                ["NoPermission"] = "Nu ai permisiunea de a folosi această comandă.",
                ["PlayerNotFound"] = "Jucătorul nu a fost găsit.",
                ["IpUnavailable"] = "Nu s-a putut rezolva niciun IP pentru acea țintă.",
                ["InvalidDuration"] = "Durată invalidă. Folosește formate precum 30m, 2h, 7d, 1w.",
                ["KickUsage"] = "/kick <nume/steamid> [motiv]",
                ["BanUsage"] = "/ban <nume/steamid/ip> [durată] [motiv]",
                ["BanIpUsage"] = "/banip <nume/steamid/ip> [durată] [motiv]",
                ["UnbanUsage"] = "/unban <nume/steamid/ip>",
                ["KickSuccess"] = "I-ai dat kick lui <color=orange>{0}</color>.",
                ["BanSuccess"] = "I-ai dat ban permanent lui <color=orange>{0}</color>.",
                ["TempBanSuccess"] = "I-ai dat ban lui <color=orange>{0}</color> pentru <color=orange>{1}</color>.",
                ["BanIpSuccess"] = "Ai banat jucătorul <color=orange>{0}</color> și IP-ul lui <color=orange>{1}</color>.",
                ["TempBanIpSuccess"] = "Ai banat jucătorul <color=orange>{0}</color> și IP-ul lui <color=orange>{1}</color> pentru <color=orange>{2}</color>.",
                ["IpOnlyBanSuccess"] = "Ai banat IP-ul <color=orange>{0}</color>.",
                ["TempIpOnlyBanSuccess"] = "Ai banat IP-ul <color=orange>{0}</color> pentru <color=orange>{1}</color>.",
                ["IpAndAccountsBanSuccess"] = "Ai banat IP-ul <color=orange>{0}</color> și ai dat native ban la <color=orange>{1}</color> cont(uri) cunoscute de pe acel IP.",
                ["TempIpAndAccountsBanSuccess"] = "Ai banat IP-ul <color=orange>{0}</color> pentru <color=orange>{1}</color> și ai dat native ban la <color=orange>{2}</color> cont(uri) cunoscute de pe acel IP.",
                ["UnbanPlayerSuccess"] = "I-ai scos banul lui <color=orange>{0}</color>.",
                ["UnbanIpSuccess"] = "Ai scos banul IP-ului <color=orange>{0}</color>.",
                ["UnbanPlayerAndIpSuccess"] = "I-ai scos banul lui <color=orange>{0}</color> și IP-ului asociat <color=orange>{1}</color>.",
                ["NotBanned"] = "Ținta respectivă nu este banată.",
                ["AccountBanLinePermanent"] = "Ban pe cont: permanent.",
                ["AccountBanLineTemporary"] = "Ban pe cont: temporar, au rămas {0}.",
                ["IpBanLinePermanent"] = "Ban pe IP: permanent.",
                ["IpBanLineTemporary"] = "Ban pe IP: temporar, au rămas {0}.",
                ["BanReasonLine"] = "Motiv: {0}",
                ["BanReasonNoneLine"] = "Motiv: nespecificat.",
                ["LoginBanAccountOnly"] = "Contul tău este banat pe acest server.",
                ["LoginBanIpOnly"] = "IP-ul folosit de această conexiune este banat pe acest server.",
                ["LoginBanAccountAndIp"] = "Atât contul tău, cât și IP-ul folosit de această conexiune sunt banate pe acest server.",
                ["GlobalKickAnnouncement"] = "<color=orange>{0}</color> a primit kick de pe server. Motiv: <color=orange>{1}</color>",
                ["GlobalBanAnnouncement"] = "<color=orange>{0}</color> a primit ban permanent pe server. Motiv: <color=orange>{1}</color>",
                ["GlobalTempBanAnnouncement"] = "<color=orange>{0}</color> a primit ban pentru <color=orange>{1}</color>. Motiv: <color=orange>{2}</color>",
                ["GlobalIpOnlyBanAnnouncement"] = "IP-ul <color=orange>{0}</color> a fost banat permanent pe server. Motiv: <color=orange>{1}</color>",
                ["GlobalTempIpOnlyBanAnnouncement"] = "IP-ul <color=orange>{0}</color> a fost banat pentru <color=orange>{1}</color>. Motiv: <color=orange>{2}</color>",
                ["GlobalBanIpAnnouncement"] = "<color=orange>{0}</color> și IP-ul său <color=orange>{1}</color> au fost banate permanent. Motiv: <color=orange>{2}</color>",
                ["GlobalTempBanIpAnnouncement"] = "<color=orange>{0}</color> și IP-ul său <color=orange>{1}</color> au fost banate pentru <color=orange>{2}</color>. Motiv: <color=orange>{3}</color>",
                ["GlobalUnbanAnnouncement"] = "<color=orange>{0}</color> a primit unban pe server.",
                ["GlobalUnbanIpAnnouncement"] = "IP-ul <color=orange>{0}</color> a primit unban pe server.",
                ["GlobalUnbanPlayerAndIpAnnouncement"] = "<color=orange>{0}</color> și IP-ul său <color=orange>{1}</color> au primit unban pe server.",
                ["BanListUsage"] = "/banlist [all/players/ips] [page]",
                ["BanListEmpty"] = "Nu există niciun ban activ de afișat.",
                ["BanListHeader"] = "Lista de banuri (<color=orange>{0}</color>) - pagina <color=orange>{1}</color>/<color=orange>{2}</color>:",
                ["BanListEntryPermanent"] = "<color=orange>{0}.</color> [{1}] <color=orange>{2}</color> - permanent - Motiv: <color=orange>{3}</color>",
                ["BanListEntryTemporary"] = "<color=orange>{0}.</color> [{1}] <color=orange>{2}</color> - <color=orange>{3}</color> rămas - Motiv: <color=orange>{4}</color>",
                ["TargetProtectedFromKick"] = "<color=orange>{0}</color> este protejat de kick.",
                ["TargetProtectedFromBan"] = "<color=orange>{0}</color> este protejat de ban.",
                ["TargetProtectedFromBanIp"] = "<color=orange>{0}</color> este protejat de banip."
            }, this, "ro");
        }

        private void InitConfig()
        {
            defaultPos = GetConfig("(0, 0, 0)", "Settings", "Default Teleport To Position On Disconnect").ToVector3();
            wipeItems = GetConfig(false, "Settings", "Wipe Saved Inventories On Map Wipe");
            wipeSettings = GetConfig(false, "Settings", "Wipe Players Settings On Map Wipe");
            persistentNoClip = GetConfig(false, "Settings", "Enable Persistent NoClip");
            persistentGodMode = GetConfig(false, "Settings", "Enable Persistent GodMode");
            defaultKickReason = GetConfig("Unknown reason.", "Settings", "Default Kick Reason");
            defaultBanReason = GetConfig("Unknown reason.", "Settings", "Default Ban Reason");
            broadcastKickToChat = GetConfig(true, "Settings", "Broadcast Kick To Global Chat");
            broadcastBanToChat = GetConfig(true, "Settings", "Broadcast Ban To Global Chat");
            logKickToConsole = GetConfig(true, "Settings", "Log Kick Events To Console");
            logBanToConsole = GetConfig(true, "Settings", "Log Ban Events To Console");
            banListPageSize = GetConfig(10, "Settings", "BanList Page Size");

            if (ConfigChanged)
            {
                PrintWarning("Updated configuration file with new/changed values.");
                SaveConfig();
            }
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            var data = Config.Get(path);
            if (data != null)
            {
                return Config.ConvertValue<T>(data);
            }

            Config.Set(path.Concat(new object[] { defaultVal }).ToArray());
            ConfigChanged = true;
            return defaultVal;
        }

        ////////////////////////////////////////////////////////////
        // Plugin Hooks
        ////////////////////////////////////////////////////////////

        [HookMethod(nameof(API_ToggleNoClip))]
        public void API_ToggleNoClip(BasePlayer player)
        {
            ToggleNoClip(player);
        }

        [HookMethod(nameof(API_ToggleGodMode))]
        public void API_ToggleGodMode(BasePlayer player)
        {
            ToggleGodMode(player);
        }

        [HookMethod(nameof(API_GetDisconnectTeleportPos))]
        public object API_GetDisconnectTeleportPos(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return null;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return null;

            return user.Teleport;
        }

        [HookMethod(nameof(API_GetSaveInventoryStatus))]
        public bool API_GetSaveInventoryStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.SaveInventory;
        }

        [HookMethod(nameof(API_GetUnderTerrainStatus))]
        public bool API_GetUnderTerrainStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.UnderTerrain;
        }

        [HookMethod(nameof(API_GetNoClipStatus))]
        public bool API_GetNoClipStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.NoClip;
        }

        [HookMethod(nameof(API_GetGodModeStatus))]
        public bool API_GetGodModeStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.GodMode;
        }

        [HookMethod(nameof(API_GetBanStatus))]
        public bool API_GetBanStatus(string steamId, string ip = null)
        {
            return TryGetActiveBan(steamId, ip, out _);
        }

        [HookMethod(nameof(API_GetBanInfo))]
        public object API_GetBanInfo(string steamId, string ip = null)
        {
            if (!TryGetActiveBan(steamId, ip, out var ban))
                return null;

            return new
            {
                Target = ban.Target,
                DisplayName = ban.DisplayName,
                Reason = ban.Reason,
                Source = ban.Source,
                CreatedAt = ban.CreatedAt,
                ExpiresAt = ban.ExpiresAt
            };
        }

        [HookMethod(nameof(API_Kick))]
        public bool API_Kick(BasePlayer actor, string targetInput, string reason = null)
        {
            if (string.IsNullOrWhiteSpace(targetInput))
                return false;

            var target = FindOnlinePlayer(targetInput);
            if (target == null)
            {
                actor?.ChatMessage(msg("PlayerNotFound", actor?.UserIDString));
                return false;
            }

            if (permission.UserHasPermission(target.UserIDString, permPreventKick))
            {
                actor?.ChatMessage(msg("TargetProtectedFromKick", actor?.UserIDString, target.displayName));
                return false;
            }

            if (string.IsNullOrWhiteSpace(reason))
                reason = defaultKickReason;

            target.IPlayer.Kick(reason);
            NotifyKickEvent(target.displayName, target.UserIDString, reason, true);

            if (actor != null)
                actor.ChatMessage(msg("KickSuccess", actor.UserIDString, target.displayName));

            return true;
        }

        [HookMethod(nameof(API_Ban))]
        public bool API_Ban(BasePlayer actor, string targetInput, string durationText = null, string reason = null, string source = "API")
        {
            if (string.IsNullOrWhiteSpace(targetInput))
                return false;

            if (string.IsNullOrWhiteSpace(reason))
                reason = defaultBanReason;

            TimeSpan? duration = null;

            if (!string.IsNullOrWhiteSpace(durationText))
            {
                if (!TryParseDuration(durationText, out var parsedDuration))
                {
                    actor?.ChatMessage(msg("InvalidDuration", actor?.UserIDString));
                    return false;
                }

                duration = parsedDuration;
            }

            if (IsIpAddress(targetInput))
            {
                AddIpBan(targetInput, targetInput, reason, duration, source);
                KickAllPlayersByIp(targetInput);
                NotifyBanEvent(targetInput, null, targetInput, duration, reason, false, true, 0, true);

                if (actor != null)
                {
                    if (duration.HasValue)
                        actor.ChatMessage(msg("TempIpOnlyBanSuccess", actor.UserIDString, targetInput, FormatDuration(duration.Value)));
                    else
                        actor.ChatMessage(msg("IpOnlyBanSuccess", actor.UserIDString, targetInput));
                }

                return true;
            }

            if (!TryResolveSteamId(targetInput, out var steamId, out var displayName, out var ip, out var onlinePlayer))
            {
                actor?.ChatMessage(msg("PlayerNotFound", actor?.UserIDString));
                return false;
            }

            if (permission.UserHasPermission(steamId, permPreventBan))
            {
                actor?.ChatMessage(msg("TargetProtectedFromBan", actor?.UserIDString, displayName ?? steamId));
                return false;
            }

            AddOrUpdatePlayerBan(steamId, displayName, reason, duration, source);
            SetNativeBan(steamId, displayName ?? steamId, reason, duration);

            onlinePlayer?.IPlayer?.Kick(BuildLoginBanMessage(steamId, ip, onlinePlayer?.UserIDString));
            NotifyBanEvent(displayName ?? steamId, steamId, ip, duration, reason, false, false, 0, true);

            if (actor != null)
            {
                if (duration.HasValue)
                    actor.ChatMessage(msg("TempBanSuccess", actor.UserIDString, displayName ?? steamId, FormatDuration(duration.Value)));
                else
                    actor.ChatMessage(msg("BanSuccess", actor.UserIDString, displayName ?? steamId));
            }

            return true;
        }

        [HookMethod(nameof(API_Unban))]
        public bool API_Unban(BasePlayer actor, string targetInput)
        {
            if (string.IsNullOrWhiteSpace(targetInput))
                return false;

            if (!TryResolveUnbanTarget(targetInput, out var steamId, out var ip))
            {
                actor?.ChatMessage(msg("NotBanned", actor?.UserIDString));
                return false;
            }

            if (!string.IsNullOrWhiteSpace(ip))
            {
                bool removedIpBan = moderationData.IpBans.Remove(ip);
                if (!removedIpBan)
                {
                    actor?.ChatMessage(msg("NotBanned", actor?.UserIDString));
                    return false;
                }

                SaveModerationData();
                NotifyUnbanEvent(ip, true, true);

                if (actor != null)
                    actor.ChatMessage(msg("UnbanIpSuccess", actor.UserIDString, ip));

                return true;
            }

            if (!string.IsNullOrWhiteSpace(steamId))
            {
                moderationData.PlayerBans.TryGetValue(steamId, out var banRecordBeforeRemove);

                string displayName =
                    banRecordBeforeRemove != null &&
                    !string.IsNullOrWhiteSpace(banRecordBeforeRemove.DisplayName) &&
                    !string.Equals(banRecordBeforeRemove.DisplayName, steamId, StringComparison.OrdinalIgnoreCase)
                        ? banRecordBeforeRemove.DisplayName
                        : GetKnownDisplayName(steamId);

                bool removedPlayerBan = moderationData.PlayerBans.Remove(steamId);
                bool removedIpBan = false;
                string linkedIp = null;

                if (moderationData.LastKnownIps.TryGetValue(steamId, out linkedIp) && IsValidStoredIp(linkedIp))
                    removedIpBan = moderationData.IpBans.Remove(linkedIp);
                else
                    linkedIp = null;

                if (!removedPlayerBan && !removedIpBan)
                {
                    actor?.ChatMessage(msg("NotBanned", actor?.UserIDString));
                    return false;
                }

                if (removedPlayerBan)
                    RemoveNativeBan(steamId);

                SaveModerationData();

                if (removedPlayerBan && removedIpBan)
                    NotifyUnbanPlayerAndIpEvent(displayName, steamId, linkedIp, true);
                else if (removedPlayerBan)
                    NotifyUnbanEvent(displayName, false, true, steamId);
                else if (removedIpBan)
                    NotifyUnbanEvent(linkedIp, true, true);

                if (actor != null)
                {
                    if (removedPlayerBan && removedIpBan)
                        actor.ChatMessage(msg("UnbanPlayerAndIpSuccess", actor.UserIDString, displayName, linkedIp));
                    else if (removedPlayerBan)
                        actor.ChatMessage(msg("UnbanPlayerSuccess", actor.UserIDString, displayName));
                    else if (removedIpBan)
                        actor.ChatMessage(msg("UnbanIpSuccess", actor.UserIDString, linkedIp));
                }

                return true;
            }

            actor?.ChatMessage(msg("NotBanned", actor?.UserIDString));
            return false;
        }

        [HookMethod(nameof(API_BanIp))]
        public bool API_BanIp(BasePlayer actor, string targetInput, string durationText = null, string reason = null, string source = "API")
        {
            if (string.IsNullOrWhiteSpace(targetInput))
                return false;

            if (string.IsNullOrWhiteSpace(reason))
                reason = defaultBanReason;

            TimeSpan? duration = null;

            if (!string.IsNullOrWhiteSpace(durationText))
            {
                if (!TryParseDuration(durationText, out var parsedDuration))
                {
                    actor?.ChatMessage(msg("InvalidDuration", actor?.UserIDString));
                    return false;
                }

                duration = parsedDuration;
            }

            if (IsIpAddress(targetInput))
            {
                AddIpBan(targetInput, targetInput, reason, duration, source);
                KickAllPlayersByIp(targetInput);

                int bannedAccounts = BanKnownAccountsOnIp(targetInput, duration, reason, source);
                NotifyBanEvent(targetInput, null, targetInput, duration, reason, true, true, bannedAccounts, true);

                if (actor != null)
                {
                    if (duration.HasValue)
                        actor.ChatMessage(msg("TempIpAndAccountsBanSuccess", actor.UserIDString, targetInput, FormatDuration(duration.Value), bannedAccounts));
                    else
                        actor.ChatMessage(msg("IpAndAccountsBanSuccess", actor.UserIDString, targetInput, bannedAccounts));
                }

                return true;
            }

            if (!TryResolveSteamId(targetInput, out var steamId, out var displayName, out var ip, out var onlinePlayer))
            {
                actor?.ChatMessage(msg("PlayerNotFound", actor?.UserIDString));
                return false;
            }

            if (permission.UserHasPermission(steamId, permPreventBanIp))
            {
                actor?.ChatMessage(msg("TargetProtectedFromBanIp", actor?.UserIDString, displayName ?? steamId));
                return false;
            }

            AddOrUpdatePlayerBan(steamId, displayName, reason, duration, source);
            SetNativeBan(steamId, displayName ?? steamId, reason, duration);

            if (IsValidStoredIp(ip))
            {
                AddIpBan(ip, displayName ?? ip, reason, duration, source);
                KickAllPlayersByIp(ip);
            }
            else if (onlinePlayer != null)
            {
                onlinePlayer.IPlayer.Kick(BuildLoginBanMessage(steamId, null, onlinePlayer.UserIDString));
            }

            NotifyBanEvent(displayName ?? steamId, steamId, ip, duration, reason, true, false, 0, true);

            string shownIp = IsValidStoredIp(ip) ? ip : "unknown";

            if (actor != null)
            {
                if (duration.HasValue)
                    actor.ChatMessage(msg("TempBanIpSuccess", actor.UserIDString, displayName ?? steamId, shownIp, FormatDuration(duration.Value)));
                else
                    actor.ChatMessage(msg("BanIpSuccess", actor.UserIDString, displayName ?? steamId, shownIp));
            }

            return true;
        }
    }
}