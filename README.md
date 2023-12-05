# LogNeuter
Allows you to remove certain logging messages from a Unity game.  
Supports constant strings and formatted strings.  
Download via Thunderstore [here](https://thunderstore.io/c/lethal-company/p/BlueAmulet/LogNeuter/)

Libraries included in this project are stripped of code and used as reference only.

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

## Known generator issues
Debug.Log with context not supported.  
Debug.Log with string concat not supported.  
Debug.LogFormat not supported.  
These only affect very few possible logging messages.

## Examples
To get rid of a few spammy messages for Lethal Company, try the following config:  
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
