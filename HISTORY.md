# Development history (versions before 1.0)

English translation of the original Spanish history (`LEEME.md`). Names of files, options and classes mentioned here are the ones used at the time; since 1.0.3 they are in English (see `CHANGELOG.md`).

**Known risks (still true):**
- If the game's meshes are not readable (`Mesh.isReadable`), cutting out the steering wheel / wheels is impossible: there is no animation and the log says so.
- Which parts are chosen as steering wheel / wheels is a heuristic. If it picks wrong, check the meshes report in `UserData`.
- The Mod Options page uses the ModSettings API. If ModSettings is missing from the game, the mod still works with its default keys.

## 0.9.19
- Vehicle menu (F2): new "Lights" entry (turns the lights on/off).
- The "Engine off" notice disappears after 5 seconds.
- Starter sound redone with the timbre of the driving engine: low thumps of the cranking, then the explosions that rev up to idle. If the start fails, it coughs a few times and the cranking stops.

## 0.9.18
- Fixed a freeze when drawing the new panels: rounded boxes now use simple textures per size and text width is estimated without measuring it with Unity.

## 0.9.17 — real consumption, new UI, starter sound
- Consumption: 11 km per liter (option "Kilometers per liter", 3 to 30). A 60 L tank is about 660 km. Idle uses very little.
- New interface: rounded panels, rows with colored bars (battery, parts, snow, fuel), status chips (ON / OFF / BROKEN) and key hints at the bottom. Notices use the same style.
- Starting: the starter motor sounds first (~1.5 s) and only then the engine starts. If the engine is failing (several worn parts) and the failure roll hits, only the cranking attempt plays (with a couple of explosions that do not catch); try again.

## 0.9.16 — fuel (diesel)
- Trucks have a 60 L tank. It is used while the engine runs (little at idle, more at speed). At 0 the engine stops and will not start. The middle gauge shows the level. "Low fuel" notice at 10 %.
- Refuelling: park next to a gas station pump, open the fuel cap (like the hood) and look at the pump: "[Hold click] Refuel" appears. Holding the click fills the tank (4 L per second, with a liquid sound).
- The vehicle menu (F2) shows the fuel. New options: "Trucks use fuel" and "Kilometers per liter".
- Each truck's level is saved with the game. New trucks start at 40 %.
- Pumps are found by name (containing "pump"). The log shows what was found.

## 0.9.15
- Bigger RPM and speedometer gauges (they fill the dashboard circle).
- Lights: the truck's original lights are used (the same ones that turn on with the aurora: headlights with beam and taillights), switched on with the dashboard button. If the game does not turn them on by itself, the mod does it by hand. Own, softer lights are used only if the originals are not found.
- Dashboard positions come pre-adjusted inside the mod: nobody has to calibrate. The adjustment key still exists (Del goes back to the originals).
- On start the log prints the option values so they can be used as defaults.
- Diagnostic reports (hierarchy, meshes, glass, camera): option `WriteReports` in the .cfg; ON during development, OFF by default in the public version.
- Lights: headlights only (as in the base game), with a bright lens and own beam; indicators light up with the engine on or the lights on.
- Default values adjusted: camera, engine height and size, inspection size.

## 0.9.14
- **Working dashboard (trucks)**: three gauges with moving needles — left RPM (big), middle fuel (small), right speedometer (big, up to 100 km/h). The game's dashboard is part of the interior mesh, so the original gauges are covered with own faces and needles; the place is calculated automatically with rays against the interior.
- **Lights button**: click on the left of the two buttons to turn the headlights and taillights on/off. They do not work if the truck is broken (no battery).
- **A/C (heat) button**: click it; for now only a soft air sound plays (volume in Mod Options). Also available from the vehicle menu (F2).
- **Dashboard adjustment** (key in Mod Options, F6 by default, seated): if a gauge or button does not match the model, the mod asks you to look at the center of each (5 steps) and press Enter; it is saved to a file in `UserData`. Del in that mode goes back to automatic.

## 0.9.13
- Inspection: the mod patches the moment the game places the object on the inspection screen and shrinks it to a maximum size. Cylinders: the bores with ring and piston are visible again with the gasket removed.

## 0.9.12
- Removed the text on the cover and every brand/model reference. Firewall behind the engine (the cabin is no longer visible). More details: tensioner, sensors, flange bolts, reservoirs, relay box, gearbox.

## 0.9.11
- **Inspection size**: the mod asks the game which object is being inspected (real inspection mode) and applies the inspection size there, instead of guessing by distance. Object scales shrunk by the game (0.5) when dropped are no longer rescued.
- **New engine models** (inline 4-cylinder, petrol with injection): ribbed iron block with freeze plugs, aluminum head, aluminum intake manifold with four runners, plenum, throttle body, fuel rail and injectors, vacuum hoses; rusty cast-iron exhaust manifold with heat shield; red cast valve cover with five studs and oil cap; distributor with cap and boots; crank pulley, water pump, fan clutch with 7-blade fan, alternator low on the left. No brands or letters.
- **Firewall** behind the engine (with insulation, heater box, grommets, relay box and gearbox).
- **Spark plugs** are now ordinary (petrol engine): thread, hex nut, ribbed insulator and terminal. Already saved parts keep working (same part number, only the name changed).

## 0.9.10
- Red (painted) valve cover, with tall central studs with acorn nuts.
- Inspection: the part is also shrunk when it is stuck to the camera (any time the game brings it close to look at it), not only when its scale changes.

## 0.9.9
- New paint texture, made with the `TLD_Brush` brush shared by a member of The Long Dark community (strokes with varied rotation and tone and 2 % noise), to get closer to the game's painted style. It is bundled inside the mod; no game textures are used.
- A PNG placed in the custom texture folder takes priority.

## 0.9.8
- New option "Size of parts when inspecting them": shrinks the part when the game enlarges it to look at it.
- Custom texture: a PNG (seamlessly repeating) in the texture folder replaces the generated texture.

## 0.9.7
- Clicking a part on the ground no longer enlarges it for a few seconds: the size is adjusted every frame according to the scale the game gives it.
- The cylinder head now has the combustion chamber recesses (with ring and valves) on both big faces.
- Finish of all parts: duller, matte colors and a procedural worn-paint texture (stains, grain, scratches and dirt) so they do not look like smooth plastic. Also applied to the hood engine.

## 0.9.6
- Part proportions as objects: head, gasket and cover at engine scale; alternator, spark plugs and cables enlarged. The four spark plugs are shown together instead of spread out.
- The base object's own scale is compensated (hammer 0.27, saw 0.23...), which made the alternator and plugs small. "Part size" now defaults to 1.

## 0.9.5
- Fixed: switching to the 3D model always failed ("invalid program") because of Unity calls the game does not support in a mod; they are no longer used. That is why the tools kept appearing on the floor.

## 0.9.4
- Any object with a part's own name (`_CP0`..`_CP5`) is recognized as a part even if the game copied it when dropping it.
- More diagnostic lines in the log when the model cannot be changed.

## 0.9.3
- Overlapping icons fixed: the part "photos" were all taken at the same place and frame, and each included the previous ones.
- The part's 3D model is found without depending on the object being active.

## 0.9.2
- Each part now has its own internal name (e.g. `GEAR_Knife_CP5`), so the game no longer mixes it with real knives / hammers: dropping a part yields that part. The own name is never written to the saved game (it is saved as the base object).
- The big icon (when clicking the part) and the inventory grid use the part's icon.
- New option in Mod Options: size of the engine parts (objects), to enlarge or shrink them.
- A part's model is only applied to the models of THAT part.

## 0.9.1
- Parts now carry a mark inside the item's saved data: after loading the game they are still parts (they no longer turn back into tools).
- Name, description and icon: more places in the game are patched, including the inventory grid.
- The part model adapts to the base object's scale/rotation, and parts are bigger (alternator, plugs).

## 0.9.0
- **Own name, description and icon** for each part in the inventory (patches to the game functions that build those texts; the icon is a photo of the 3D model taken once by the mod).
- **Part size in hand/world**: alternator ~26 cm, plugs ~30 cm, cables ~34 cm, cover/head/gasket ~46 cm (before they were real size and looked tiny).
- **New models** with an own shape generator (`Geo.cs`): rounded edges, hex bolts with washer, solids of revolution, curved tubes. Valve cover with rim, rubber gasket, ribs, 16 bolts, ribbed oil cap and breather with hose; alternator with housings, vent slots, ears, grooved pulley and regulator; plugs with nut, body, ceramic insulator and terminal; head gasket with rings and holes; cables with bar, tabs and curved wire.

## 0.8.2
- Parts no longer fall into the mod's "backpack": they are only inventory items. The 0.8.1 log showed the game did not give new objects an identifier; now it is requested in several ways (`ForceGUIDSetup`, `ObjectGuid`) and the log says what happened. Parts left lying under the engine move to the inventory by themselves.

## 0.8.1
- **Parts with their own 3D model**: each part is still a game object (a tool), but the mod swaps its model for the part's, in the inventory, in hand and in the world (it can be dropped and moved like any object).
- **Engine**: narrower (like a real inline 4), with cylinder sleeves and ribs; removed frame, suspension towers, inner fenders, radiator frame and firewall.
- **Battery** at radiator height, with a bracket.
- **Camera**: the adjustment is fixed (it was not being applied).
- Parts did **not wear** yet (condition only changed if you changed it).

## 0.8.0
- **Parts as inventory items**: alternator, spark plugs, cables, valve cover, head and gasket are real inventory items: dropped and picked up like any object, and the condition is the item's. The game has no such objects, so each part uses a game tool (hammer, saw, crowbar...) as its "body" with the part's name; the mod recognizes them by their unique identifier. If something fails, the old backpack is used. Whatever was in the old backpack moves to the inventory by itself. Can be turned off in Mod Options.
- **Getting parts**: key **F7** (changeable) or the "Get new parts" row of the engine menu (H): gives a set of 6 new parts (70-100 %).
- **Engine without turbo** (single-outlet manifold and pipe) and much more detail: frame, suspension towers, inner fenders, radiator frame and latch, reservoirs, hoses with clamps, wiring harness with fuses and relays, power steering, firewall, hardware.
- **Camera in the truck**: three adjustments in Mod Options (left/right, up/down, forward/back) that move the driver's view.

## 0.7.1
- **Parts on the ground**: "Drop the backpack on the ground" puts them in front of you, on the ground (found with a downward ray). They no longer have physics, so they do not sink or get lost; they are saved instantly and reappear on load.
- **Blur**: besides the post-process layer and the old effects, the game's blur quality is now set to "Off" (the same option as in Graphics). Restored when the Mod Options option is turned off.

## 0.7.0
- **More detailed engine** (rounded block, head with manifolds, ribbed valve cover, injectors, plugs, cables, alternator with belt, fan, radiator, brake booster, etc.).
- **Engine parts**: Alternator, Spark plugs, Ignition cables, Valve cover, Cylinder head and Head gasket, plus the battery. In the engine menu (H) each one is **installed or removed with Enter**, and only the installed one is visible on the engine (if missing, the gap shows: cylinders, studs or rockers).
  - Order: to remove the head you must first remove the valve cover and the plugs (and the cables before that); the head needs the gasket underneath.
  - **All must be above 20 % condition** for the truck to start and run. With **2 or more parts below 50 %** (the battery counts too) starting fails at random: 30 % with 2, 50 % with 3, 70 % with 4, up to 80 %.
  - Each new truck comes with parts of different wear (one may come broken). Parts can be moved from one truck to another.
- **Backpack and parts on the ground**: what you remove goes to a mod "backpack" (not the game inventory). "Drop the backpack on the ground" leaves them as physical objects; looking at them up close, **H** picks them up. Saved per game.
- **Blur**: the camera's post-process layer is now switched off (plus the old effect), every frame.

## 0.6.0
- **Engine and battery (trucks only)**: the hood opens as usual (hold click); when open, a generic 80s 4-cylinder engine (simple shapes, no textures) appears instead of the original engine. Looking at it up close, press **H** (changeable in Mod Options): menu with the **battery slot**. Enter installs or removes a *Car battery* (`GEAR_CarBattery`) from your inventory. Engine height and size are set in Mod Options.
- **Without a battery the truck neither starts nor moves** (the F2 menu shows "NO BATTERY"). Trucks already saved come without a battery: one must be installed. Saved per vehicle (battery condition too). Can be disabled in Mod Options ("The truck needs a battery").
- **Blur**: it was a camera effect of the game itself (MotionBlur from ImageEffects, camera "ImageEffectCamera"); the option now turns it off.

## 0.5.1
- **Off-center camera in the truck**: the view now rotates exactly as much as the seat does (before the "theoretical" rotation was used and it drifted, especially on slopes).
- **Blur**: 0.5.0 looked for the wrong effect (see 0.6.0).

## 0.5.0
- **Speed blur**: Mod Options option "Remove speed blur". It comes OFF, so blur stays as in the base game until you turn it on. Turns off the post-process Motion Blur effect.
- **Snow from the menu (F2)**: "Remove snow" removes the snow from the hood/roof/bed and lowers it on the windows, which do not become fully transparent (~35 % remains).
- **Snow comes back**: by itself in 1 game day; faster with snowfall (~10 h light, ~4 h heavy, ~2 h blizzard). It does not build up while the vehicle is moving or indoors. Saved per vehicle.

## 0.4.3
- **Loading while seated**: it is now detected when the game seats you by itself on load (without you entering) and you are taken out through that vehicle's door, left next to it. It no longer depends on any saved mark.
- **Straight steering wheel**: the model has tilted spokes. The mod measures that tilt and turns the wheel so that with straight wheels it looks straight. Fine adjustment remains in Mod Options.

## 0.4.2
- **Removed the 45-second correction** (it took the player to another vehicle by mistake).
- **Save and quit while seated**: the mod notes which vehicle you were in. On load, if the game seats you somewhere else or in another vehicle, you are taken out through the right vehicle's door and left next to it. Can be turned off with `ExitOnLoad` in the `.cfg`. Uses the game's own exit function.
- **Steering wheel**: with the engine on the wheel always returns to center (also when stopped). With the engine off it stays as you left it. If the wheel looks turned with straight wheels, there is a "Steering wheel center adjustment" in Mod Options.

## 0.4.1
- **Truck steering wheel**: now the rim and the center (horn) turn.
- **Sedan**: looks for the steering wheel in all interior meshes and the wheels inside the body. Untested.
- **Steering wheel speed**: option in Mod Options.

## 0.4.0
- **Engine**: with the engine off it does not accelerate (only inertia and brakes) and makes no sound. Each vehicle remembers whether you left it on or off. A new vehicle starts OFF (there is an option for it to start on). If it does not move, open the menu with F2 and turn it on.
- **Steering wheel**: the game's wheel is not a separate piece (it is part of the interior mesh; the front wheels are part of the body). The mod cuts those parts out in memory and rotates them. With the engine on, the wheel returns to center when moving; off, it stays where you left it.
- **Front wheels**: they stay turned according to the steering wheel, also when exiting and on loading the game.
- Saved per vehicle and per game: position, rotation, snow, steering wheel and engine.

## 0.1 – 0.3
First versions: the game's ORIGINAL vehicles can be driven while seated (the game still handles entering, exiting and the camera). W A S D / Space: accelerate, brake/reverse, steer, handbrake. F3 enables/disables the mod, F2 opens the vehicle menu.
