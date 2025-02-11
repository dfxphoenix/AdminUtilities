## Overview
**Admin Utilities** is a plugin who have some functionalities like noclip. under terrain and more.

This plugin is the only plugin that offers a premium and identical experience to using the command directly from the console.

## Permissions
* ``adminutilities.nocliptoggle`` - Allows group/player to have access to use noclip.
* ``adminutilities.godmodetoggle`` - Allows group/player to have access to use godmode.
* ``adminutilities.disconnectteleport`` - Allow group/player to teleport under terrain or custom coordonates on disconnect.
* ``adminutilities.saveinventory`` - Allow group/player to save inventory on disconnect.
* ``adminutilities.maxhht`` - Allow group/player to give max metabolism on connect.

## Chat Commands
* ``/noclip`` - Enable/Disable the noclip mode (require adminutilities.nocliptoggle).
* ``/god`` - Enable/Disable the god mode (require adminutilities.godmodetoggle).
* ``/disconnectteleport`` - Show personal disconnect teleport's options (require adminutilities.disconnectteleport).
* ``/saveinventory`` - Enable/Disable personal save inventory on disconnect (require adminutilities.saveinventory).

## Configuration
```json
{
  "Settings": {
    "Default Teleport To Position On Disconnect": "(0, 0, 0)",
    "Enable Persistent GodMode": false,
    "Enable Persistent NoClip": false,
    "Wipe Players Settings On Map Wipe": false,
    "Wipe Saved Inventories On Map Wipe": false
  }
}
```

## Languages
**Admin Utilities** have two languages by default (**English** and **Romanian**), but you can add more in Oxide lang folder

## API Hooks
```csharp
void API_ToggleNoClip(BasePlayer player)
void API_ToggleGodMode(BasePlayer player)
object API_GetDisconnectTeleportPos(string userid)
bool API_GetSaveInventoryStatus(string userid)
bool API_GetUnderTerrainStatus(string userid)
bool API_GetNoClipStatus(string userid)
bool API_GetGodModeStatus(string userid)
```