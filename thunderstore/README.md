# LogNeuter
Allows you to remove certain logging messages from the game.  
Support constant strings and formatted strings.  
Also fixes audio spatialization warnings.

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

## Installation (manual)
If you are installing this manually, do the following

1. Extract the archive into a folder. **Do not extract into the game folder.**
2. Move the contents of `plugins` folder into `<GameDirectory>\BepInEx\plugins`.
3. Run the game.
