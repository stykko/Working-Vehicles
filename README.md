# Working Vehicles

A [MelonLoader](https://melonwiki.xyz) mod for **The Long Dark** that turns the abandoned trucks into drivable vehicles, with a working engine, battery, fuel, dashboard and repairable parts.

> Formerly "Camioneta Conducible". Bugs and suggestions: use the Issues tab.

## Features
- Drive trucks with W/A/S/D and Space (handbrake).
- Engine with starter sequence and sound, fuel consumption and refuelling at gas station pumps.
- Dashboard with working RPM, fuel and speed gauges, light and A/C buttons.
- Heater (A/C) that warms the cabin up to about 10 °C.
- Battery that only drains when starting the engine, or when the lights / A/C are on with the engine off, and recharges while the engine runs.
- Engine parts (alternator, spark plugs, ignition wires, valve cover, cylinder head, head gasket) that wear with distance and can be installed, removed and repaired at the workbench.
- Wear difficulty option, English / Spanish language option, volume options.
- Debug mode with extra options for testing.

## Requirements
- The Long Dark with [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.2 (Il2Cpp)
- [ModSettings](https://github.com/ds5678/ModSettings)

## Install
1. Download `WorkingVehicles.dll` (or build it, see below).
2. Copy it to `TheLongDark\Mods`.
3. If you have an old `CamionetaConducible.dll` in that folder, delete it.

## Build
```
dotnet build -c Release
```
If the game is not in the default Steam path:
```
dotnet build -c Release -p:GameDir="D:\YOUR\PATH\TheLongDark"
```
The project references the game's assemblies from `GameDir`. The DLL is copied to `Mods` automatically.

## What it touches
- It only writes its own files in `UserData` (`WorkingVehicles.cfg`, `WorkingVehicles_*.txt` with vehicle positions, parts and dashboard calibration, and debug reports when Debug mode is on).
- It does not use the network.

## Notes
- `HISTORY.md` is the development history before 1.0 (translated from the original Spanish `LEEME.md`); see `CHANGELOG.md` for the changes since 1.0.
- Code comments, documentation and identifiers are in English. Data files, settings keys and the part marker are in English; data from older versions (including `CamionetaConducible_*` files) is migrated automatically, so saved games, settings and parts are kept when updating.
- The code was developed with the help of AI (Claude by Anthropic) and tested in-game by the author.
- Debug mode is a setting in Mod Options. When off, test options and logs are hidden.

## Author
Made by **stykko**. YouTube: [@loxo3679](https://www.youtube.com/@loxo3679)

## Contributing and credits
This project is open source: you are free to fork it, edit it and build on it. If you reuse or redistribute it, please keep the credit to the original author (the MIT license requires keeping the copyright notice) and mention the original mod, **Working Vehicles by stykko**.

## License
MIT, see [LICENSE](LICENSE).
