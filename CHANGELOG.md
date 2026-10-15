# Changelog

## 1.0.4
- Report files and debug messages in the `.cfg` are off by default.
- Fixed: the battery now only drains when starting the engine and when the lights or A/C are on with the engine off. With the engine running it recharges (before, the drain cancelled the recharge).

## 1.0.3
- Option names, `.cfg` keys and the part marker stored in saved games renamed to English. Old settings, `.cfg` and saved parts are migrated automatically (old marker is still read).
- Data files renamed to English (WorkingVehicles_positions.txt etc.). Saved progress from the old file names is copied and merged automatically (old files are never touched), and an empty part registry can no longer wipe saved data.
- Headlights moved down and back, taillights up and forward.
- Dashboard calibration text and diagnostic logs now bilingual/English; internal object names renamed.
- Embedded images and the optional custom texture folder renamed to English (the old texture folder is still read).
- Spanish history translated to English (HISTORY.md).
- Debug mode is now off by default.
- Source code: classes, methods, fields, variables and source files renamed from Spanish to English. No gameplay changes; saved games and settings are preserved.

## 1.0.2
- Log messages, debug option labels and the `.cfg` descriptions are now in English. No gameplay changes.

## 1.0.1
- Only the trucks can be driven now. Other vehicles (sedan, tractors) have no engine, battery or fuel and handled badly; there is an experimental option in Mod Options to enable them again.
- Gravity: driving off a cliff or ledge now makes the truck fall and land instead of floating.
- If the truck falls through the map (no ground below for a few seconds), it is put back at the last place where it stood on the ground.

## 1.0.0 — Working Vehicles
First public release (formerly "Camioneta Conducible").

- Drivable trucks: acceleration, braking, reverse, handbrake, steering, collisions and terrain following.
- Engine with starter sequence and generated sounds; fuel consumption (about 11 km/L) and refuelling at gas station pumps.
- Working dashboard: RPM, fuel and speed gauges, lights button and A/C button.
- Cabin heater (A/C) that raises the interior temperature gradually, up to about 10 °C.
- Battery that drains with the engine, lights and A/C and recharges while driving.
- Hood with a detailed engine. Removable parts: battery, alternator, spark plugs, ignition wires, valve cover, cylinder head, head gasket. Parts are real inventory items.
- Parts wear with distance (option: Brutal 4 % / Hard 2 % / Normal 1 % / Easy 0.5 % / Very easy 0.25 % / No wear, per 10 km).
- Workbench repair of every part, using game materials (scrap metal, wire, fuses, nuts and bolts) and basic or high-quality tools.
- Snow on the vehicle that can be removed and slowly builds up again.
- Vehicle state (position, fuel, battery, parts) is stored per saved game and only written when the game saves.
- English / Spanish language option; volume options; optional removal of motion blur.
- Debug mode (Mod Options) shows the test and tuning options; it is on by default in pre-release builds.
