## Overview
**Admin Utilities** is a plugin who have some functionalities like noclip. under terrain and more.

It is designed to offer a clean and native-like administrative experience, closely matching Rust console commands while extending them with additional features.

It includes a built-in moderation system with support for kick, ban, banip, unban and banlist, along with player/IP bans, temporary bans, login blocking, API support and staff protection permissions.

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

## Chat Commands
* `/noclip` - Enable/Disable the noclip mode (require adminutilities.nocliptoggle).
* `/god` - Enable/Disable the god mode (require adminutilities.godmodetoggle).
* `/disconnectteleport` - Show personal disconnect teleport's options (require adminutilities.disconnectteleport).
* `/saveinventory` - Enable/Disable personal save inventory on disconnect (require adminutilities.saveinventory).
* `/kick <name> [reason]` - Kick a player from the server (require adminutilities.kick).
* `/ban <name/steamid/ip> [duration] [reason]` - Ban a player or IP (permanent or temporary) (require adminutilities.ban).
* `/banip <name/steamid/ip> [duration] [reason]` - Ban a player and their IP or ban an IP with all known accounts (require adminutilities.banip).
* `/unban <name/steamid/ip>` - Remove a ban from a player or IP (require adminutilities.unban).
* `/banlist [page]` - Show the list of active bans with pagination (require adminutilities.banlist).

## Server Commands
* `kick <name> [reason]` - Kick a player from the server.
* `ban <name/steamid/ip> [duration] [reason]` - Ban a player or IP (permanent or temporary).
* `banip <name/steamid/ip> [duration] [reason]` - Ban a player and their IP or ban an IP with all known accounts.
* `unban <name/steamid/ip>` - Remove a ban from a player or IP.

## Configuration

```json
{
  "Settings": {
    "BanList Page Size": 10,
    "Broadcast Ban To Global Chat": true,
    "Broadcast Kick To Global Chat": true,
    "Default Ban Reason": "Unknown reason.",
    "Default Kick Reason": "Unknown reason.",
    "Default Teleport To Position On Disconnect": "(0, 0, 0)",
    "Enable Persistent GodMode": false,
    "Enable Persistent NoClip": false,
    "Log Ban Events To Console": true,
    "Log Kick Events To Console": true,
    "Wipe Players Settings On Map Wipe": false,
    "Wipe Saved Inventories On Map Wipe": false
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
bool API_KickPlayer(BasePlayer actor, string target, string reason = null)
bool API_Ban(BasePlayer actor, string target, string duration = null, string reason = null)
bool API_BanIp(BasePlayer actor, string target, string duration = null, string reason = null)
bool API_Unban(BasePlayer actor, string target)
```