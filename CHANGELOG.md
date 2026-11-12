# Changelog

## 1.0.13
- Vehicle detection no longer depends only on `FindObjectsOfType`: if another mod filters that search (e.g. SeamlessInteriors), a second search is used. This should fix vehicles not being detected (hood not opening, no vehicle to spawn from the garage).
- The log now says how many vehicle doors each scan found (`[Vehicles] scan in scene ...`).

## 1.0.12
- Fixed the alternator's repair panel (game 2.58/2.59) showing half drawn with extra options: the game's materials table fails with 3 materials, so parts now ask for 2 at most (the alternator needs fuses and wires; scrap metal is no longer requested).

## 1.0.11
- Fixed (game 2.58/2.59): repairing engine parts at the workbench always said "not enough materials". The repair materials are now registered with the game's real item name.
- The repair materials found are always written to the log.

## 1.0.10
- Fixed for The Long Dark 2.58/2.59: engine parts were duplicated endlessly in the inventory and could not be installed (the game removed `ObjectGuid.Get()` and `GearItem.LoadGearItemPrefab`). Identifiers and prefabs are now looked up in a version-tolerant way.
- If a part cannot be registered, the created copy is removed instead of being left in the inventory.

## 1.0.9
- Performance: the engine model of an open hood is only shown within 30 m of the camera (many open hoods around the map made the game lag).
- Working radio: click the radio on the dashboard to open a small menu (turn on / off, volume, previous / next song). It plays your own songs from the folder `Mods\WorkingVehicles` (`.wav` files only). It works with the engine off too, draining the battery like the lights do. The menu also has a pause / resume option. If you leave the radio on and get out, it keeps playing from the vehicle (quieter and muffled, like through closed doors) and keeps draining the battery, until it dies or you walk far away.
- Performance: the scan of every game item that gives names and models to the engine parts now runs every 3 s instead of 1.5 s, and a per-frame allocation was removed. New option "Low-resource mode" (Mod Options): the background scans (parts, vehicles, garage, pumps) run about 5 times less often, for weak computers.
- A third physical button on the dashboard, right below the lights button: in cars it unlocks the trunk so it can be searched without a prybar; in the pickup truck it opens and closes the tailgate (EXPERIMENTAL: the tailgate is part of the body model, so the mod cuts its triangles out into their own piece; the log shows `[Trunk]` lines with what it found). The lever turns blue when open / unlocked. The option "Trunk button in cars" (Mod Options) removes the button from cars if the free unlock is considered too easy. The tailgate has its own collision that moves with it, and the body's collision loses the closed tailgate.
- Jerry cans as fuel cans: with a jerry can in the inventory, look at a pump and hold click to fill it; look at the truck's open fuel cap and hold click to pour the can into the tank. The can always keeps 1 % so the game does not destroy it when it empties. Any jerry can with kerosene works (also the ones found in the world). The fuel from the pumps goes into the can as kerosene, so it also fuels lamps; the option "Fill jerry cans at the pumps" (Mod Options) turns pump filling off.
- Translations of the Mod Options page (contributed by a community member): English, Spanish, Russian and the rest of the game languages (the other languages still show English for now). They follow the game's language and are stored in `localization.json` (embedded; a copy in `UserData/WorkingVehicles/localization.json` overrides it). The "Language" option was removed: the mod's own menus and messages are in Spanish when the game is in Spanish and in English otherwise.
- More detail in the engine bay: A/C compressor with pulley, clutch and lines to a receiver-drier and the firewall, catalytic converter and oxygen sensor on the exhaust pipe, brake lines from the master cylinder, and strut towers with plates and nuts.
- The engine parts now LOOK their condition, both mounted on the engine and as items in the inventory / on the ground / in the hand: new ones are clean, used ones get a few oil stains, worn ones add rust patches and oil drips, ruined ones are covered. Metal darkens and rusts, rubber and plastic dry out and turn dusty grey. The marks are glued to the part's own surfaces. The look follows the item's condition (4 levels: 85 %, 60 %, 35 %).
- The valve cover was redone: no more bolts on top; the fixing bolts are around the flange and the oil filler cap is one solid piece resting on the cover (it was floating).
- Menus no longer allocate a string per letter per frame (less garbage collection while a menu is open).
- The engine screen (H with the hood open) now replicates the game's own look: painted panel frame with a rough light rim, engine icon in the header, one row per part with its icon, name, painted condition bar and value ("86%", amber "36% WORN", red "MISSING / USELESS"), a hairline, centered painted buttons (store the vehicle / EXIT), the engine-parts count underneath and the key hints below the panel. Icons, frame and bar texture are bundled inside the mod (no extra files).
- The other menus (vehicle, garage) use the same frame, header, separators and painted bars.

## 1.0.8
- New: **Garage**. Look at the engine with the hood open, press H and choose "Store the vehicle in the garage" (it asks for confirmation). The vehicle disappears from the world. Press F3 (changeable in Mod Options) anywhere, in any region, to open the garage and take a vehicle out in front of you. It comes back with everything it had: engine parts and their wear, battery and its charge, fuel, snow and steering wheel. A vehicle taken out and left in a region stays there (also after saving and reloading).
- The garage is saved with the game (`WorkingVehicles_garage.txt`, only when the game saves). To build a vehicle in a region where it does not exist, the mod keeps a copy of each model it has seen during the session; after restarting the game, get near a vehicle of the same type once before taking it out.
- The garage remembers the exact vehicle (color included), not just its model.
- The glovebox and trunk contents travel with the vehicle (and stay inspected). If something is not restored, the log shows `[Garage] container ...` lines and the container API of the game.
- F3 no longer turns the mod off (the "toggle mod" key was removed).

## 1.0.7
- New look for every menu and message, using colors sampled from The Long Dark's own screens (inventory, item details, pause menu): flat near-black panels and tiles with no borders or rounded corners, the petrol-blue gradient of the selected tile, white / grey tracked upper-case text, amber for worn and red for critical, dark flat buttons like SOLTAR / ATRÁS, and on-screen hints over a soft dark band. The system font is chosen to resemble the game's (Bahnschrift / Segoe UI), with the default font as a fallback.
- New option "Vehicles only during an aurora" (Mod Options): trucks and cars only start and run while an aurora is active; when it ends the engine shuts off.
- Cars have their OWN settings, so tuning a car no longer changes the truck: driver camera (left/right, up/down, forward/back), engine height and size, dashboard size, steering wheel speed and center adjustment, km per liter (Mod Options, debug mode), top speed / acceleration / braking / handbrake / drag / turn rate / collision box (`WorkingVehicles.cfg`, `Car_...` entries), and the dashboard adjustment (saved in `WorkingVehicles_dashboard_car.txt`).
- The mod ships with the tuned car engine (height/size) and car dashboard positions as built-in defaults. Every time you sit in a vehicle the log prints the settings in use (`[Options] CAR/TRUCK settings in use ...`).
- Cars can now be driven with the same system as the truck: engine, starter, battery, fuel and refuelling, dashboard, hood with removable parts and wear. Vehicles are detected as cars by name (sedan, coupe, wagon, SUV, "Car...") or by having a hood hinge and a door; tractors, trailers and snowmobiles are excluded.
- The log lists which vehicles were detected as truck/car. If a car is still not detected, report the name printed in the log.

## 1.0.6
- Fixed: the front wheels of the Breyerhouse truck did not turn. A flat piece at the front of the body (0.54 x 0.33 x 0.10 m) was mistaken for the front wheels. Wheel detection now requires a round shape (disc).

## 1.0.5
- Fixed: the truck at Breyerhouse (Mystery Lake) was not detected. A vehicle is now treated as a truck if its name contains "Truck" or "Pickup", or if it has a hood hinge and a fuel-cap hinge (whatever its name). The root of the vehicle is also found when it is not under an "ENCAMPMENT_..." object.
- The log now says which vehicles were detected as trucks and which were not.

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
