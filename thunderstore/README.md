# LogNeuter
Allows you to remove certain logging messages from the game.  
Supports constant strings and formatted strings.

Also has a few generic fixes to reduce log messages:  
Fixes audio spatialization warnings.  
Masks "Look rotation viewing vector is zero" warnings.

## Installation (manual)
If you are installing this manually, do the following

1. Extract the archive into a folder. **Do not extract into the game folder.**
2. Move the contents of `plugins` folder into `<GameDirectory>\BepInEx\plugins`.
3. Run the game.

## Instructions
Add an entry to the configuration in the following format:  
```
[Namespace.TypeName|MethodName]
Static Message To Filter
^Dynamic Message To Filter: .*$
```

Some log messages are dynamic or formatted, and regex syntax may be used in order to filter these.  
LogNeuter checks for regex syntax by looking for the ^ and $ characters at the beginning and end.

To generate an example config file containing (nearly) all possible messages to filter, enable GenBlockAll and run the game once.  
A file called `BlueAmulet.LogNeuter.Generated.cfg` will appear inside the config folder.  
The presence of this file does not filter any logs, and filters must be set up inside the main configuration file.

## Examples
To get rid of a few spammy messages, try the following config:  
```
[RedLocustBees|Update]
Setting zap mode to 0 aaa

[InteractTrigger|HoldInteractNotFilled]
Holding interact

[GameNetcodeStuff.PlayerControllerB|OpenMenu_performed]
PLAYER OPENED MENU

[PlayAudioAnimationEvent|PlayAudio2RandomClip]
Playing random clip 2

[Unity.Netcode.RpcMessageHelpers, Unity.Netcode.Runtime|Handle]
^[0-9]+ | __rpc_handler_[0-9]+$
```

## Known generator issues
Debug.Log with context not supported.  
Debug.Log with string concat not supported.  
Debug.LogFormat not supported.  
These only affect very few possible logging messages.

## Changelog
<details>
<summary>Click to expand</summary>

### 1.0.2
Fixed patching logs from classes other than Assembly-CSharp.  
Fixed patching not applying if only regex patches were used.  
Blocked trying to patch LogException.

### 1.0.1
Added versioning to the config, a warning message will appear if the config version is different than expected.  
Added patch for Quaternion.LookRotation, will mask the "Look rotation viewing vector is zero" warnings.  
Plugin no longer depends on Lethal Company and should work for any game.  
Config generation now done in separate harmony namespace and also unpatches itself.
</details>
