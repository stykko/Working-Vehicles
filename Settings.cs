using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModSettings;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>The mod's page in "Mod Options" (uses the ModSettings mod, which you already have installed).</summary>
    internal class Options : JsonModSettings
    {
        public static Options I;

        // ------------------------------------------------------------------ player options

        [Name("Idioma / Language")]
        [Description("Idioma de los textos del mod y de las piezas del motor. / Language of the mod's texts and engine parts.")]
        [Choice("Español", "English")]
        public int language = 1;

        [Name("Desgaste de las piezas / Part wear")]
        [Description("Cuánto se gastan las piezas del motor al conducir, de más difícil a más fácil. Por defecto: 1 % cada 10 km. / How fast engine parts wear while driving, from hardest to easiest. Default: 1% per 10 km.")]
        [Choice("Brutal / Brutal: 4 % cada 10 km / per 10 km", "Difícil / Hard: 2 % cada 10 km / per 10 km", "Normal / Normal: 1 % cada 10 km / per 10 km", "Fácil / Easy: 0,5 % cada 10 km / per 10 km", "Muy fácil / Very easy: 0,25 % cada 10 km / per 10 km", "Sin wear / No wear")]
        public int wear = 2;

        [Name("Tecla: menú del vehículo / Vehicle menu key")]
        [Description("Abre el menú del vehículo (motor, nieve, luces, aire, combustible). Solo sentada. Flechas arriba/abajo y Enter. / Opens the vehicle menu while seated. Up/Down arrows and Enter.")]
        public KeyCode menuKey = KeyCode.F2;

        [Name("Tecla: menú del motor / Engine menu key")]
        [Description("Con el capó abierto, mirando el motor: abre el menú del motor (batería y piezas). / With the hood open, looking at the engine: opens the engine menu (battery and parts).")]
        public KeyCode hoodKey = KeyCode.H;

        [Name("Sonido del motor / Engine sound")]
        [Description("Motor y ruedas sobre la nieve. / Engine and tires on snow.")]
        public bool sound = true;

        [Name("Volumen del motor / Engine volume")]
        [Description("0 = mudo, 1 = máximo. / 0 = mute, 1 = max.")]
        [Slider(0f, 1f)]
        public float engineVolume = 0.7f;

        [Name("Volumen del aire acondicionado / A/C volume")]
        [Description("Sonido del aire al apretar el botón. 0 = mudo. / A/C sound when pressing the button. 0 = mute.")]
        [Slider(0f, 0.6f)]
        public float acVolume = 0.12f;

        [Name("Conducir otros vehículos (experimental) / Drive other vehicles (experimental)")]
        [Description("Apagado = solo se manejan las camionetas. Encendido = también otros vehículos (sedán, tractores...), pero no tienen motor, batería ni combustible y se manejan mal. / Off = only the trucks can be driven. On = other vehicles too (sedan, tractors...), but they have no engine, battery or fuel and handle badly.")]
        public bool driveOthers = false;

        [Name("Quitar el desenfoque de velocidad / Remove speed blur")]
        [Description("Apagado = el desenfoque de movimiento queda como en el juego base. Encendido = se elimina. / Off = motion blur stays as in the base game. On = removed.")]
        public bool removeBlur = false;

        [Name("Motor encendido al sentarse / Engine on when sitting")]
        [Description("Si está apagado, hay que encender el motor desde el menú del vehículo. / If off, you start the engine from the vehicle menu.")]
        public bool startOnSit = false;

        [Name("Modo debug (trucos y herramientas) / Debug mode (cheats and tools)")]
        [Description("Muestra las opciones de desarrollo de abajo (combustible infinito, ajustes del motor y del tablero, piezas gratis, informes). Apagado = el mod usa los valores ya ajustados. / Shows the development options below. Off = the mod uses the tuned defaults.")]
        public bool debugMode = false;

        // ------------------------------------------------------------------ development options (only with debug mode on)

        [Section("Debug")]
        [Name("Key: toggle the mod")]
        [Description("Turns the whole truck mod on or off.")]
        public KeyCode modKey = KeyCode.F3;

        [Name("Engine height (meters)")]
        [Description("Raises or lowers the engine inside the hood. Negative = lower.")]
        [Slider(-0.6f, 0.4f)]
        public float engineHeight = -0.26515353f;

        [Name("Engine size")]
        [Description("Engine scale. 1 = full size, less = smaller.")]
        [Slider(0.5f, 1.2f)]
        public float engineSize = 0.8729342f;

        [Name("Key: adjust the dashboard")]
        [Description("While seated in the truck: enters the adjustment mode for the dashboard gauges and buttons. Look at the center of each one and press Enter.")]
        public KeyCode dashboardKey = KeyCode.F6;

        [Name("Size of the dashboard gauges and buttons")]
        [Description("1 = normal size. Change it if the new faces do not cover the model's gauges well.")]
        [Slider(0.5f, 2f)]
        public float dashboardSize = 1f;

        [Name("Trucks use fuel")]
        [Description("On: the tank (60 L) is used up while driving and must be filled at the pumps. Off: infinite fuel.")]
        public bool useFuel = true;

        [Name("Kilometers per liter")]
        [Description("Fuel efficiency (diesel). 11 = about 660 km on a full 60 L tank.")]
        [Slider(3f, 30f)]
        public float kmPerLiter = 11f;

        [Name("Battery drain (% every 5 s)")]
        [Description("How much battery is lost every 5 seconds while the lights or A/C are on with the engine OFF (starting the engine costs 3 times this rate during the starter). 0 = no drain.")]
        [Slider(0f, 10f)]
        public float batteryDrain = 2f;

        [Name("Battery recharge (% every 10 s)")]
        [Description("How much battery is recovered every 10 seconds with the engine running. 0 = no recharge.")]
        [Slider(0f, 10f)]
        public float batteryRecharge = 2f;

        [Name("A/C temperature (°C)")]
        [Description("Interior temperature the cabin reaches with the A/C on (and the engine running).")]
        [Slider(0f, 25f)]
        public float acTemp = 10f;

        [Name("Key: get new parts")]
        [Description("Gives you a full set of new engine parts in the inventory. Meant for testing.")]
        public KeyCode generateKey = KeyCode.F7;

        [Name("Engine parts as game items")]
        [Description("On: the parts are inventory items. Off: the mod's old 'pack' system.")]
        public bool partsInventory = true;

        [Name("Size of the engine parts (items)")]
        [Description("How big the parts look when they are items. 1 = base size.")]
        [Slider(0.5f, 3f)]
        public float partsSize = 1f;

        [Name("Size of the parts when inspecting them")]
        [Description("When a part is inspected the game brings it close to the camera. 1 = as the game does it, less = smaller.")]
        [Slider(0.1f, 1.5f)]
        public float inspectionSize = 0.991702f;

        [Name("Camera in the truck: left / right (m)")]
        [Description("Moves the driver's view sideways. Negative = left, positive = right.")]
        [Slider(-0.8f, 0.8f)]
        public float cameraX = -0.13091224f;

        [Name("Camera in the truck: up / down (m)")]
        [Description("Raises or lowers the driver's view inside the truck.")]
        [Slider(-0.4f, 0.4f)]
        public float cameraY = 0.0012105405f;

        [Name("Camera in the truck: forward / back (m)")]
        [Description("Moves the driver's view closer to or farther from the steering wheel.")]
        [Slider(-0.6f, 0.6f)]
        public float cameraZ = 0f;

        [Name("The truck needs a battery and parts")]
        [Description("On: it will not start or move without a battery and all the engine parts in good condition. Off: it runs as before.")]
        public bool requireBattery = true;

        [Name("Steering wheel speed")]
        [Description("How fast the steering wheel turns when pressing A / D. 1 = normal.")]
        [Slider(0.2f, 3f)]
        public float steeringSpeed = 1f;

        [Name("Steering wheel center adjustment (degrees)")]
        [Description("Only if the steering wheel looks turned when the wheels are straight. 0 = as it comes in the game.")]
        [Slider(-180f, 180f)]
        public float steeringAdjust = 0f;

        /// <summary>Fields that are only visible with debug mode on.</summary>
        public static readonly string[] FieldsDebug =
        {
            "modKey", "engineHeight", "engineSize", "dashboardKey", "dashboardSize", "useFuel", "kmPerLiter", "batteryDrain", "batteryRecharge",
            "acTemp", "generateKey", "partsInventory", "partsSize", "inspectionSize", "cameraX", "cameraY", "cameraZ", "requireBattery", "steeringSpeed", "steeringAdjust"
        };

        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            try { if (field != null && field.Name == "debugMode") Settings.ApplyVisibility((bool)newValue); }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// Safe access to the options: if ModSettings is not installed, default values are used and the mod keeps working.
    /// (Each read is in a separate method so that, without ModSettings, the failure stays contained.)
    /// </summary>
    internal static class Settings
    {
        static bool exists;

        public static void Start()
        {
            try
            {
                Register();
                exists = true;
                try { if (Options.I.debugMode) DumpValues(); } catch (Exception) { }
            }
            catch (Exception e)
            {
                exists = false;
                MelonLoader.MelonLogger.Warning("Could not create the options page (is the ModSettings mod missing?). Default keys are used: F3 mod, F2 menu. " + e.Message);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DumpValues()
        {
            var o = Options.I;
            MelonLoader.MelonLogger.Msg("[Options] camera=(" + o.cameraX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " + o.cameraY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " + o.cameraZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ") engineHeight=" + o.engineHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " engineSize=" + o.engineSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " dashboardSize=" + o.dashboardSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " steeringAdjust=" + o.steeringAdjust.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " partsSize=" + o.partsSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " inspectionSize=" + o.inspectionSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ApplyVisibility(bool debug)
        {
            if (Options.I == null) return;
            foreach (string c in Options.FieldsDebug)
            {
                try { Options.I.SetFieldVisible(c, debug); } catch (Exception) { }
            }
            try { Options.I.RefreshGUI(); } catch (Exception) { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)] static bool LDbg() { return Options.I.debugMode; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int LWear() { return Options.I.wear; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LCOther() { return Options.I.driveOthers; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int LIdi() { return Options.I.language; }

        /// <summary>Is debug mode on? (without ModSettings: off)</summary>
        public static bool Debug() { return exists && LDbg(); }
        /// <summary>% of condition each part loses every 10 km (default 1).</summary>
        public static float WearPer10Km()
        {
            int d = exists ? LWear() : 2;
            switch (d) { case 0: return 4f; case 1: return 2f; case 2: return 1f; case 3: return 0.5f; case 4: return 0.25f; default: return 0f; }
        }
        /// <summary>Can vehicles other than the trucks be driven? (experimental, off by default)</summary>
        public static bool DriveOther() { return exists && LCOther(); }
        public static bool English() { return exists && LIdi() == 1; }

        /// <summary>Old (Spanish) option names -> new names. The saved settings file is updated so nobody loses their options.</summary>
        static readonly string[,] OldKeys =
        {
            { "idioma", "language" },
            { "desgaste", "wear" },
            { "teclaMenu", "menuKey" },
            { "teclaCapo", "hoodKey" },
            { "sonido", "sound" },
            { "volumenMotor", "engineVolume" },
            { "volumenAire", "acVolume" },
            { "conducirOtros", "driveOthers" },
            { "quitarDesenfoque", "removeBlur" },
            { "arrancarAlSentarse", "startOnSit" },
            { "modoDebug", "debugMode" },
            { "teclaMod", "modKey" },
            { "alturaMotor", "engineHeight" },
            { "tamanoMotor", "engineSize" },
            { "teclaTablero", "dashboardKey" },
            { "tamanoTablero", "dashboardSize" },
            { "usarCombustible", "useFuel" },
            { "kmPorLitro", "kmPerLiter" },
            { "bateriaGasto", "batteryDrain" },
            { "bateriaRecarga", "batteryRecharge" },
            { "tempAire", "acTemp" },
            { "teclaGenerar", "generateKey" },
            { "piezasInventario", "partsInventory" },
            { "tamanoPiezas", "partsSize" },
            { "tamanoInspeccion", "inspectionSize" },
            { "camaraX", "cameraX" },
            { "camaraY", "cameraY" },
            { "camaraZ", "cameraZ" },
            { "exigirBateria", "requireBattery" },
            { "velVolante", "steeringSpeed" },
            { "ajusteVolante", "steeringAdjust" }
        };

        static void MigrateOptionsFile()
        {
            try
            {
                string root = System.IO.Directory.GetCurrentDirectory();
                foreach (string dirName in new[] { "Mods", "UserData" })
                {
                    string dir = System.IO.Path.Combine(root, dirName);
                    if (!System.IO.Directory.Exists(dir)) continue;
                    foreach (string f in System.IO.Directory.GetFiles(dir, "*.json", System.IO.SearchOption.AllDirectories))
                    {
                        string t = System.IO.File.ReadAllText(f);
                        if (t.IndexOf("\"teclaMenu\"", StringComparison.Ordinal) < 0 || t.IndexOf("\"teclaMod\"", StringComparison.Ordinal) < 0) continue;
                        for (int i = 0; i < OldKeys.GetLength(0); i++) t = t.Replace("\"" + OldKeys[i, 0] + "\"", "\"" + OldKeys[i, 1] + "\"");
                        System.IO.File.WriteAllText(f, t);
                        MelonLoader.MelonLogger.Msg("[Options] settings migrated to the new names: " + f);
                    }
                }
            }
            catch (Exception) { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Register()
        {
            MigrateOptionsFile();
            Options.I = new Options();
            Options.I.AddToModSettings("Working Vehicles");
            ApplyVisibility(Options.I.debugMode);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] static KeyCode LMod() { return Options.I.modKey; }
        [MethodImpl(MethodImplOptions.NoInlining)] static KeyCode LMenu() { return Options.I.menuKey; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LSound() { return Options.I.sound; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LVol() { return Options.I.engineVolume; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LVelVol() { return Options.I.steeringSpeed; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LFitVol() { return Options.I.steeringAdjust; }
        [MethodImpl(MethodImplOptions.NoInlining)] static KeyCode LHood() { return Options.I.hoodKey; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LAlt() { return Options.I.engineHeight; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LSize() { return Options.I.engineSize; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LExi() { return Options.I.requireBattery; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LDes() { return Options.I.removeBlur; }
        [MethodImpl(MethodImplOptions.NoInlining)] static KeyCode LGen() { return Options.I.generateKey; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LPInv() { return Options.I.partsInventory; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LTP() { return Options.I.partsSize; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LTI() { return Options.I.inspectionSize; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCX() { return Options.I.cameraX; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCY() { return Options.I.cameraY; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCZ() { return Options.I.cameraZ; }
        [MethodImpl(MethodImplOptions.NoInlining)] static KeyCode LTab() { return Options.I.dashboardKey; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LTT() { return Options.I.dashboardSize; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LVA() { return Options.I.acVolume; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LUC() { return Options.I.useFuel; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCC() { return Options.I.kmPerLiter; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LBG() { return Options.I.batteryDrain; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LBR() { return Options.I.batteryRecharge; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LTA() { return Options.I.acTemp; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LStarter() { return Options.I.startOnSit; }

        public static KeyCode KeyMod() { return Debug() ? LMod() : KeyCode.None; }
        public static KeyCode KeyMenu() { return exists ? LMenu() : KeyCode.F2; }
        public static bool Sound() { return exists ? LSound() : true; }
        public static float VolumeEngine() { return exists ? LVol() : 0.7f; }
        public static float VelSteering() { return Debug() ? Mathf.Clamp(LVelVol(), 0.2f, 3f) : 1f; }
        public static float AdjustSteering() { return Debug() ? LFitVol() : 0f; }
        public static KeyCode KeyHood() { return exists ? LHood() : KeyCode.H; }
        public static float HeightEngine() { return Debug() ? Mathf.Clamp(LAlt(), -0.6f, 0.4f) : -0.26515353f; }
        public static float SizeEngine() { return Debug() ? Mathf.Clamp(LSize(), 0.5f, 1.2f) : 0.8729342f; }
        public static bool RequireBattery() { return Debug() ? LExi() : true; }
        public static bool RemoveBlur() { return exists ? LDes() : false; }
        public static KeyCode KeyGenerate() { return Debug() ? LGen() : KeyCode.None; }
        public static bool PartsInventory() { return Debug() ? LPInv() : true; }
        public static float SizeParts() { return Debug() ? Mathf.Clamp(LTP(), 0.5f, 3f) : 1f; }
        public static float SizeInspection() { return Debug() ? Mathf.Clamp(LTI(), 0.1f, 1.5f) : 0.991702f; }
        public static Vector3 Cam() { return Debug() ? new Vector3(Mathf.Clamp(LCX(), -0.8f, 0.8f), Mathf.Clamp(LCY(), -0.4f, 0.4f), Mathf.Clamp(LCZ(), -0.6f, 0.6f)) : new Vector3(-0.13091224f, 0.0012105405f, 0f); }
        public static KeyCode KeyDashboard() { return Debug() ? LTab() : KeyCode.None; }
        public static float SizeDashboard() { return Debug() ? Mathf.Clamp(LTT(), 0.5f, 2f) : 1f; }
        public static float VolumeAc() { return exists ? Mathf.Clamp(LVA(), 0f, 0.6f) : 0.12f; }
        public static bool UseFuel() { return Debug() ? LUC() : true; }
        public static float BatteryDrain() { return Debug() ? Mathf.Clamp(LBG(), 0f, 10f) : 2f; }
        public static float BatteryRecharge() { return Debug() ? Mathf.Clamp(LBR(), 0f, 10f) : 2f; }
        public static float TempAc() { return Debug() ? Mathf.Clamp(LTA(), 0f, 25f) : 10f; }
        public static float KmPerLiter() { return Debug() ? Mathf.Clamp(LCC(), 3f, 30f) : 11f; }
        public static bool StartOnSit() { return exists ? LStarter() : false; }
    }
}
