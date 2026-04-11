## Overview
**Admin Utilities** is a plugin who have some functionalities like noclip, under terrain and more.

It is designed to offer a clean and native-like administrative experience, closely matching Rust console commands while extending them with additional features.

It includes a built-in moderation system with support for kick, ban, banip, unban and banlist, along with player/IP bans, temporary bans, login blocking, API support and staff protection permissions.

It also includes give/spawn utilities, blacklist support for items and spawn aliases, global server chat icon support, disabled command lists for chat/player console with bypass permissions, and native-compatible console aliases for inventory give commands and `entity.spawn`.

## Permissions
* `adminutilities.nocliptoggle` - Allows group/player to have access to use noclip command.
* `adminutilities.godmodetoggle` - Allows group/player to have access to use godmode command.
* `adminutilities.disconnectteleport` - Allows group/player to teleport under terrain or custom coordonates on disconnect.
* `adminutilities.saveinventory` - Allows group/player to save inventory on disconnect.
* `adminutilities.maxhht` - Allows group/player to give max metabolism on connect.
* `adminutilities.kick` - Allows group/player to have access to use kick command.
* `adminutilities.ban` - Allows group/player to have access to use ban command.
* `adminutilities.banip` - Allows group/player to have access to use banip command.
* `adminutilities.unban` - Allows group/player to have access to use unban command.
* `adminutilities.banlist` - Allows group/player to have access to use banlist command.
* `adminutilities.preventkick` - Player/group cannot receive kick.
* `adminutilities.preventban` - Player/group cannot receive ban.
* `adminutilities.preventbanip` - Player/group cannot receive banip.
* `adminutilities.give` - Allows group/player to give items to self.
* `adminutilities.giveto` - Allows group/player to give items to another player.
* `adminutilities.giveall` - Allows group/player to give items to all connected players.
* `adminutilities.spawn` - Allows group/player to spawn entities for self.
* `adminutilities.spawnto` - Allows group/player to spawn entities for another player.
* `adminutilities.spawnall` - Allows group/player to spawn entities for all connected players.
* `adminutilities.despawn` - Allows group/player to despawn the looked-at entity.
* `adminutilities.bypassblacklist` - Bypasses give item blacklist and spawn alias blacklist checks.
* `adminutilities.bypassplayerconsolecommands` - Allows group/player to bypass disabled player console commands.
* `adminutilities.bypasschatcommands` - Allows group/player to bypass disabled chat commands.

## Chat Commands
* `/noclip` - Enable/Disable the noclip mode (require `adminutilities.nocliptoggle`).
* `/nocliptoggle` - Enable/Disable the noclip mode (require `adminutilities.nocliptoggle`).
* `/god` - Enable/Disable the god mode (require `adminutilities.godmodetoggle`).
* `/godmode` - Enable/Disable the god mode (require `adminutilities.godmodetoggle`).
* `/disconnectteleport` - Show personal disconnect teleport's options (require `adminutilities.disconnectteleport`).
* `/saveinventory` - Enable/Disable personal save inventory on disconnect (require `adminutilities.saveinventory`).
* `/kick <name> [reason]` - Kick a player from the server (require `adminutilities.kick`).
* `/ban <name/steamid/ip> [duration] [reason]` - Ban a player or IP (permanent or temporary) (require `adminutilities.ban`).
* `/banip <name/steamid/ip> [duration] [reason]` - Ban a player and their IP or ban an IP with all known accounts (require `adminutilities.banip`).
* `/unban <name/steamid/ip>` - Remove a ban from a player or IP (require `adminutilities.unban`).
* `/banlist [all/players/ips] [page]` - Show the list of active bans with pagination (require `adminutilities.banlist`).
* `/spawn <entity>` - Spawn an entity for yourself (require `adminutilities.spawn`).
* `/spawnto <name/steamid> <entity>` - Spawn an entity for another player (require `adminutilities.spawnto`).
* `/spawnall <entity>` - Spawn an entity for all connected players (require `adminutilities.spawnall`).
* `/despawn` - Despawn the entity you are looking at (require `adminutilities.despawn`).
* `/give <item> [amount]` - Give yourself an item (require `adminutilities.give`).
* `/give <name/steamid> <item> [amount]` - Give an item to another player (require `adminutilities.giveto`).
* `/giveto <name/steamid> <item> [amount]` - Give an item to another player (require `adminutilities.giveto`).
* `/giveall <item> [amount]` - Give an item to all connected players (require `adminutilities.giveall`).

## Console Commands
* `nocliptoggle` - Enable/Disable the noclip mode (require `adminutilities.nocliptoggle`).
* `godmode` - Enable/Disable the god mode (require `adminutilities.godmodetoggle`).
* `disconnectteleport [set <x> <y> <z> / reset]` - Manage personal disconnect teleport settings (require `adminutilities.disconnectteleport`).
* `saveinventory` - Enable/Disable personal save inventory on disconnect (require `adminutilities.saveinventory`).
* `kick <name> [reason]` - Kick a player from the server (require `adminutilities.kick`).
* `ban <name/steamid/ip> [duration] [reason]` - Ban a player or IP (permanent or temporary) (require `adminutilities.ban`).
* `banip <name/steamid/ip> [duration] [reason]` - Ban a player and their IP or ban an IP with all known accounts (require `adminutilities.banip`).
* `unban <name/steamid/ip>` - Remove a ban from a player or IP (require `adminutilities.unban`).
* `banlist [all/players/ips] [page]` - Show the list of active bans with pagination (require `adminutilities.banlist`).
* `spawn <entity>` - Spawn an entity for yourself (require `adminutilities.spawn`).
* `spawnto <name/steamid> <entity>` - Spawn an entity for another player (require `adminutilities.spawnto`).
* `spawnall <entity>` - Spawn an entity for all connected players (require `adminutilities.spawnall`).
* `despawn` - Despawn the entity you are looking at (require `adminutilities.despawn`).
* `give <item> [amount]` - Give yourself an item (require `adminutilities.give`).
* `give <name/steamid> <item> [amount]` - Give an item to another player (require `adminutilities.giveto`).
* `giveto <name/steamid> <item> [amount]` - Give an item to another player (require `adminutilities.giveto`).
* `giveall <item> [amount]` - Give an item to all connected players (require `adminutilities.giveall`).
* `inventory.give <item> [amount]` - Native-compatible give alias (require `adminutilities.give`).
* `inventory.giveid <itemid> [amount]` - Native-compatible give-by-itemid alias (require `adminutilities.give`).
* `inventory.giveto <name/steamid> <item> [amount]` - Native-compatible giveto alias (require `adminutilities.giveto`).
* `inventory.giveall <item> [amount]` - Native-compatible giveall alias (require `adminutilities.giveall`).
* `entity.spawn <entity>` - Native-compatible player console alias for spawning an entity for yourself (require `adminutilities.spawn`).

## Server Commands
* `kick <name> [reason]` - Kick a player from the server.
* `ban <name/steamid/ip> [duration] [reason]` - Ban a player or IP (permanent or temporary).
* `banip <name/steamid/ip> [duration] [reason]` - Ban a player and their IP or ban an IP with all known accounts.
* `unban <name/steamid/ip>` - Remove a ban from a player or IP.
* `banlist [all/players/ips] [page]` - Show the list of active bans with pagination.
* `spawnto <name/steamid> <entity>` - Spawn an entity for another player.
* `spawnall <entity>` - Spawn an entity for all connected players.
* `give <name/steamid> <item> [amount]` - Native-like combined syntax for giving an item to a target from server console.
* `giveto <name/steamid> <item> [amount]` - Give an item to another player.
* `giveall <item> [amount]` - Give an item to all connected players.
* `inventory.giveto <name/steamid> <item> [amount]` - Native-compatible giveto alias.
* `inventory.giveall <item> [amount]` - Native-compatible giveall alias.

## Configuration

```json
{
  "General": {
    "Default Teleport To Position On Disconnect": "(0, 0, 0)",
    "Disabled Player Console Commands": [],
    "Disabled Chat Commands": [],
    "Wipe Saved Inventories On Map Wipe": false,
    "Wipe Players Settings On Map Wipe": false,
    "Global Server Messages Icon Steam ID Or Group ID": ""
  },
  "NoClip & God": {
    "Enable Persistent NoClip": false,
    "Enable Persistent GodMode": false
  },
  "Moderation": {
    "Default Kick Reason": "Unknown reason.",
    "Default Ban Reason": "Unknown reason.",
    "Broadcast Kick To Global Chat": true,
    "Broadcast Ban To Global Chat": true,
    "Log Kick Events To Console": true,
    "Log Ban Events To Console": true,
    "BanList Page Size": 10
  },
  "Give & Spawn": {
    "Spawn Distance In Front Of Player": 4.0,
    "Use Crosshair Raycast For Spawn Position": false,
    "Maximum Crosshair Spawn Distance": 25.0,
    "Raise Spawn Position On Y Axis": 0.25,
    "Allow Direct Prefab Paths": true,
    "Allow Partial Prefab Search": true,
    "Maximum Despawn Distance": 25.0,
    "Log Give Commands To Console": true,
    "Log Spawn Commands To Console": true,
    "Spawn Alias Blacklist": [],
    "Give Item Blacklist": [],
    "Default Aliases": {
      "mini": "assets/content/vehicles/minicopter/minicopter.entity.prefab",
      "minicopter": "assets/content/vehicles/minicopter/minicopter.entity.prefab",
      "scrap": "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
      "scrapheli": "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
      "2mod_01": "car_2mod_01",
      "2mod_02": "car_2mod_02",
      "2mod_03": "car_2mod_03",
      "2mod_04": "car_2mod_04",
      "2mod_05": "car_2mod_05",
      "2mod_06": "car_2mod_06",
      "2mod_07": "car_2mod_07",
      "2mod_08": "car_2mod_08",
      "3mod_01": "car_3mod_01",
      "3mod_02": "car_3mod_02",
      "3mod_03": "car_3mod_03",
      "3mod_04": "car_3mod_04",
      "3mod_05": "car_3mod_05",
      "3mod_06": "car_3mod_06",
      "3mod_07": "car_3mod_07",
      "3mod_08": "car_3mod_08",
      "3mod_09": "car_3mod_09",
      "3mod_10": "car_3mod_10",
      "3mod_11": "car_3mod_11",
      "3mod_12": "car_3mod_12",
      "4mod_01": "car_4mod_01",
      "4mod_02": "car_4mod_02",
      "4mod_03": "car_4mod_03",
      "4mod_04": "car_4mod_04",
      "4mod_05": "car_4mod_05",
      "4mod_06": "car_4mod_06",
      "4mod_07": "car_4mod_07",
      "4mod_08": "car_4mod_08",
      "4mod_09": "car_4mod_09",
      "4mod_10": "car_4mod_10",
      "4mod_11": "car_4mod_11"
    }
  }
}
```

## Languages
**Admin Utilities** have two languages by default (**English** and **Romanian**), but you can add more in Oxide lang folder.

## API Hooks
```csharp
void API_ToggleNoClip(BasePlayer player)
void API_ToggleGodMode(BasePlayer player)
object API_GetDisconnectTeleportPos(string userid)
bool API_GetSaveInventoryStatus(string userid)
bool API_GetUnderTerrainStatus(string userid)
bool API_GetNoClipStatus(string userid)
bool API_GetGodModeStatus(string userid)
bool API_GetBanStatus(string steamId, string ip = null)
object API_GetBanInfo(string steamId, string ip = null)
bool API_Kick(BasePlayer actor, string targetInput, string reason = null)
bool API_Ban(BasePlayer actor, string targetInput, string durationText = null, string reason = null, string source = "API")
bool API_Unban(BasePlayer actor, string targetInput)
bool API_BanIp(BasePlayer actor, string targetInput, string durationText = null, string reason = null, string source = "API")
```