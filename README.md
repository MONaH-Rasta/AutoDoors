# AutoDoors

Oxide plugin for Rust. Automatically closes doors behind players after X seconds

Automatically closes doors behind players after the default amount of seconds or as set by them. 

## Permissions

- `autodoors.use` -- Allows player to toggle and have automatic doors
 
## Chat Commands
**NOTE**:   `/ad <type | t>` and `/ad <single | s>` commands must looking at a door when used

* `/ad` - Enable/Disable automatic door closing
* `/ad <time (seconds)>` - Set automatic closing delay for doors  (Doors set by 'single' and 'type' are not included). 
* `/ad <all | a> <time (seconds)>` - Set automatic closing delay for all doors. 
* `/ad <single | s>` - Enable/Disable automatic closing of the door you are looking at
* `/ad <single | s> <time (seconds)>` - Set automatic closing delay for the door you are looking at
* **"type" is just a word, not the type of door**
* `/ad <type | t>` - Enable/disable automatic door closing for the type of door you are looking at
* `/ad <type | t> <time (seconds)>` - Set automatic closing delay for the type of door you are looking at
* `/ad <help | h>` - View help

## Configuration

```json
{
  "Use permissions": false,
  "Clear data on map wipe": true,
  "Global settings": {
    "Allows automatic closing of unowned doors": false,
    "Exclude door controller": true,
    "Cancel on player dead": false,
    "Default enabled": true,
    "Default delay": 5.0,
    "Maximum delay": 30.0,
    "Minimum delay": 5.0
  },
  "Chat settings": {
    "Chat command": [
      "ad",
      "autodoor"
    ],
    "Chat prefix": "<color=#00FFFF>[AutoDoors]</color>: ",
    "Chat steamID icon": 0
  },
  "Door Settings": {
    "door.double.hinged.metal": {
      "enabled": true,
      "displayName": "Sheet Metal Double Door"
    },
    "door.double.hinged.toptier": {
      "enabled": true,
      "displayName": "Armored Double Door"
    },
    "door.double.hinged.wood": {
      "enabled": true,
      "displayName": "Wood Double Door"
    },
    "door.hinged.metal": {
      "enabled": true,
      "displayName": "Sheet Metal Door"
    },
    "door.hinged.toptier": {
      "enabled": true,
      "displayName": "Armored Door"
    },
    "door.hinged.wood": {
      "enabled": true,
      "displayName": "Wooden Door"
    },
    "floor.ladder.hatch": {
      "enabled": true,
      "displayName": "Ladder Hatch"
    },
    "floor.triangle.ladder.hatch": {
      "enabled": true,
      "displayName": "Triangle Ladder Hatch"
    },
    "gates.external.high.stone": {
      "enabled": true,
      "displayName": "High External Stone Gate"
    },
    "gates.external.high.wood": {
      "enabled": true,
      "displayName": "High External Wooden Gate"
    },
    "wall.frame.cell.gate": {
      "enabled": true,
      "displayName": "Prison Cell Gate"
    },
    "wall.frame.fence.gate": {
      "enabled": true,
      "displayName": "Chainlink Fence Gate"
    },
    "wall.frame.garagedoor": {
      "enabled": true,
      "displayName": "Garage Door"
    },
    "wall.frame.shopfront": {
      "enabled": true,
      "displayName": "Shop Front"
    },
    "shutter.wood.a": {
      "enabled": true,
      "displayName": "Wood Shutters"
    }
  },
  "Version": {
    "Major": 3,
    "Minor": 2,
    "Patch": 8
  }
}
```

## Localization

```json
{
  "NotAllowed": "You do not have permission to use this command",
  "Enabled": "<color=#8ee700>Enabled</color>",
  "Disabled": "<color=#ce422b>Disabled</color>",
  "AutoDoor": "Automatic door closing is now {0}",
  "AutoDoorDelay": "Automatic door closing delay set to {0}s. (Doors set by 'single' and 'type' are not included)",
  "AutoDoorDelayAll": "Automatic closing delay of all doors set to {0}s",
  "DoorNotFound": "You need to look at a door",
  "DoorNotSupported": "This type of door is not supported",
  "AutoDoorDelayLimit": "Automatic door closing delay allowed is between {0}s and {1}s",
  "AutoDoorSingle": "Automatic closing of this {0} is {1}",
  "AutoDoorSingleDelay": "Automatic closing delay of this {0} is {1}s",
  "AutoDoorType": "Automatic closing of {0} door is {1}",
  "AutoDoorTypeDelay": "Automatic closing delay of {0} door is {1}s",
  "SyntaxError": "Syntax error, type '<color=#ce422b>/{0} <help | h></color>' to view help",
  "AutoDoorSyntax": "<color=#ce422b>/{0} </color> - Enable/Disable automatic door closing",
  "AutoDoorSyntax1": "<color=#ce422b>/{0} <time (seconds)></color> - Set automatic closing delay for doors, the allowed time is between {1}s and {2}s. (Doors set by 'single' and 'type' are not included)",
  "AutoDoorSyntax2": "<color=#ce422b>/{0} <single | s></color> - Enable/Disable automatic closing of the door you are looking at",
  "AutoDoorSyntax3": "<color=#ce422b>/{0} <single | s> <time (seconds)></color> - Set automatic closing delay for the door you are looking at, the allowed time is between {1}s and {2}s",
  "AutoDoorSyntax4": "<color=#ce422b>/{0} <type | t></color> - Enable/disable automatic door closing for the type of door you are looking at. ('type' is just a word, not the type of door)",
  "AutoDoorSyntax5": "<color=#ce422b>/{0} <type | t> <time (seconds)></color> - Set automatic closing delay for the type of door you are looking at, the allowed time is between {1}s and {2}s. ('type' is just a word, not the type of door)",
  "AutoDoorSyntax6": "<color=#ce422b>/{0} <all | a> <time (seconds)></color> - Set automatic closing delay for all doors, the allowed time is between {1}s and {2}s."
}
```
## Hooks

```csharp
private object OnDoorAutoClose(BasePlayer player, Door door)
```
## Credits

- **Bombardir**, for the original version of this plugin
- **Wulf**, for the previous re-write of this plugin
- **Arainrr**: Previous maintainer
- **James**: Helping test plugin update before force wipe