# Working Vehicles

A [MelonLoader](https://melonwiki.xyz) mod for **The Long Dark** that turns the abandoned trucks into drivable vehicles, with a working engine, battery, fuel, dashboard and repairable parts.

> Formerly "Camioneta Conducible". Bugs and suggestions: use the Issues tab.

## Features
- Drive trucks and cars with W/A/S/D and Space (handbrake).
- Engine with starter sequence and sound, fuel consumption and refuelling at gas station pumps.
- Dashboard with working RPM, fuel and speed gauges, light and A/C buttons.
- **Radio**: click the radio on the dashboard for a small menu (on/off, volume, previous/next song). It plays your own music from the `Mods\WorkingVehicles` folder (`.wav` files only). Works with the engine off too (it drains the battery like the lights). Uses the Unity audio enabled by the AudioCore plugin.
- Heater (A/C) that warms the cabin up to about 10 °C.
- Battery that only drains when starting the engine, or when the lights / A/C are on with the engine off, and recharges while the engine runs.
- Engine parts (alternator, spark plugs, ignition wires, valve cover, cylinder head, head gasket) that wear with distance and can be installed, removed and repaired at the workbench.
- Wear difficulty option, options page translated (follows the game language; edit `localization.json` to add yours), volume options.
- Debug mode with extra options for testing.

- **Garage**: store a vehicle from the engine menu (H, with confirmation) and take it out with F3 in any region, keeping its parts, battery, fuel and snow.

## Requirements
- [AudioCore](https://github.com/DigitalzombieTLD/AudioCore) plugin (Unity audio for sounds and the radio)
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

## Localization
The texts of the Mod Options page are translated through `localization.json` (embedded in the mod) and follow the game's language. To override or add translations without rebuilding, put a copy of the file in `UserData/WorkingVehicles/localization.json`.

You can localize any other text in the mod too:
1. Add a line like `"WV.blablabla": "blablabla"` to each language block of `localization.json` (language names are the game's: `English`, `Spanish`, `Russian`, `Brazilian`...). Keys must start with `WV.`; a missing key falls back to English.
2. In the code, replace the hard-coded text with `LocalizationManager.Get("WV.blablabla")`.

The mod's own menus and messages still use `L.T("spanish", "english")` (Spanish if the game is in Spanish, English otherwise); they can be moved to the JSON the same way.

## Author
Made by **stykko**. YouTube: [@loxo3679](https://www.youtube.com/@loxo3679)

## Contributing and credits
This project is open source: you are free to fork it, edit it and build on it. If you reuse or redistribute it, please keep the credit to the original author (the MIT license requires keeping the copyright notice) and mention the original mod, **Working Vehicles by stykko**.

## License
MIT, see [LICENSE](LICENSE).
