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

        [Name("WV.wear.Name", Localize = true)]
        [Description("WV.wear.Description", Localize = true)]
        [Choice("WV.wear.Brutal", "WV.wear.Hard", "WV.wear.Normal", "WV.wear.Easy", "WV.wear.VeryEasy", "WV.wear.NoWear", Localize = true)]
        public int wear = 2;

        [Name("WV.menuKey.Name", Localize = true)]
        [Description("WV.menuKey.Description", Localize = true)]
        public KeyCode menuKey = KeyCode.F2;

        [Name("WV.hoodKey.Name", Localize = true)]
        [Description("WV.hoodKey.Description", Localize = true)]
        public KeyCode hoodKey = KeyCode.H;

        [Name("WV.garageKey.Name", Localize = true)]
        [Description("WV.garageKey.Description", Localize = true)]
        public KeyCode garageKey = KeyCode.F3;

        [Name("WV.sound.Name", Localize = true)]
        [Description("WV.sound.Description", Localize = true)]
        public bool sound = true;

        [Name("WV.engineVolume.Name", Localize = true)]
        [Description("WV.engineVolume.Description", Localize = true)]
        [Slider(0f, 1f)]
        public float engineVolume = 0.7f;

        [Name("WV.acVolume.Name", Localize = true)]
        [Description("WV.acVolume.Description", Localize = true)]
        [Slider(0f, 0.6f)]
        public float acVolume = 0.12f;

        [Name("WV.driveOthers.Name", Localize = true)]
        [Description("WV.driveOthers.Description", Localize = true)]
        public bool driveOthers = false;

        [Name("WV.auroraOnly.Name", Localize = true)]
        [Description("WV.auroraOnly.Description", Localize = true)]
        public bool auroraOnly = false;

        [Name("WV.jerryAtPumps.Name", Localize = true)]
        [Description("WV.jerryAtPumps.Description", Localize = true)]
        public bool jerryAtPumps = true;

        [Name("WV.carTrunkButton.Name", Localize = true)]
        [Description("WV.carTrunkButton.Description", Localize = true)]
        public bool carTrunkButton = true;

        [Name("WV.lowResources.Name", Localize = true)]
        [Description("WV.lowResources.Description", Localize = true)]
        public bool lowResources = false;

        [Name("WV.removeBlur.Name", Localize = true)]
        [Description("WV.removeBlur.Description", Localize = true)]
        public bool removeBlur = false;

        [Name("WV.startOnSit.Name", Localize = true)]
        [Description("WV.startOnSit.Description", Localize = true)]
        public bool startOnSit = false;

        [Name("WV.debugMode.Name", Localize = true)]
        [Description("WV.debugMode.Description", Localize = true)]
        public bool debugMode = false;

        // ------------------------------------------------------------------ development options (only with debug mode on)

        [Section("WV.debug.Section", Localize = true)]
        [Name("WV.engineHeight.Name", Localize = true)]
        [Description("WV.engineHeight.Description", Localize = true)]
        [Slider(-0.6f, 0.4f)]
        public float engineHeight = -0.26515353f;

        [Name("WV.engineSize.Name", Localize = true)]
        [Description("WV.engineSize.Description", Localize = true)]
        [Slider(0.5f, 1.2f)]
        public float engineSize = 0.8729342f;

        [Name("WV.dashboardKey.Name", Localize = true)]
        [Description("WV.dashboardKey.Description", Localize = true)]
        public KeyCode dashboardKey = KeyCode.F6;

        [Name("WV.dashboardSize.Name", Localize = true)]
        [Description("WV.dashboardSize.Description", Localize = true)]
        [Slider(0.5f, 2f)]
        public float dashboardSize = 1f;

        [Name("WV.useFuel.Name", Localize = true)]
        [Description("WV.useFuel.Description", Localize = true)]
        public bool useFuel = true;

        [Name("WV.kmPerLiter.Name", Localize = true)]
        [Description("WV.kmPerLiter.Description", Localize = true)]
        [Slider(3f, 30f)]
        public float kmPerLiter = 11f;

        [Name("WV.batteryDrain.Name", Localize = true)]
        [Description("WV.batteryDrain.Description", Localize = true)]
        [Slider(0f, 10f)]
        public float batteryDrain = 2f;

        [Name("WV.batteryRecharge.Name", Localize = true)]
        [Description("WV.batteryRecharge.Description", Localize = true)]
        [Slider(0f, 10f)]
        public float batteryRecharge = 2f;

        [Name("WV.acTemp.Name", Localize = true)]
        [Description("WV.acTemp.Description", Localize = true)]
        [Slider(0f, 25f)]
        public float acTemp = 10f;

        [Name("WV.generateKey.Name", Localize = true)]
        [Description("WV.generateKey.Description", Localize = true)]
        public KeyCode generateKey = KeyCode.F7;

        [Name("WV.partsInventory.Name", Localize = true)]
        [Description("WV.partsInventory.Description", Localize = true)]
        public bool partsInventory = true;

        [Name("WV.partsSize.Name", Localize = true)]
        [Description("WV.partsSize.Description", Localize = true)]
        [Slider(0.5f, 3f)]
        public float partsSize = 1f;

        [Name("WV.inspectionSize.Name", Localize = true)]
        [Description("WV.inspectionSize.Description", Localize = true)]
        [Slider(0.1f, 1.5f)]
        public float inspectionSize = 0.991702f;

        [Name("WV.cameraX.Name", Localize = true)]
        [Description("WV.cameraX.Description", Localize = true)]
        [Slider(-0.8f, 0.8f)]
        public float cameraX = -0.13091224f;

        [Name("WV.cameraY.Name", Localize = true)]
        [Description("WV.cameraY.Description", Localize = true)]
        [Slider(-0.4f, 0.4f)]
        public float cameraY = 0.0012105405f;

        [Name("WV.cameraZ.Name", Localize = true)]
        [Description("WV.cameraZ.Description", Localize = true)]
        [Slider(-0.6f, 0.6f)]
        public float cameraZ = 0f;

        [Name("WV.requireBattery.Name", Localize = true)]
        [Description("WV.requireBattery.Description", Localize = true)]
        public bool requireBattery = true;

        [Name("WV.steeringSpeed.Name", Localize = true)]
        [Description("WV.steeringSpeed.Description", Localize = true)]
        [Slider(0.2f, 3f)]
        public float steeringSpeed = 1f;

        [Name("WV.steeringAdjust.Name", Localize = true)]
        [Description("WV.steeringAdjust.Description", Localize = true)]
        [Slider(-180f, 180f)]
        public float steeringAdjust = 0f;


        // ------------------------------------------------------------------ car settings (same as the truck's, but separate)

        [Section("WV.debugCars.Section", Localize = true)]
        [Name("WV.carCameraX.Name", Localize = true)]
        [Description("WV.carCameraX.Description", Localize = true)]
        [Slider(-0.8f, 0.8f)]
        public float carCameraX = -0.13091224f;

        [Name("WV.carCameraY.Name", Localize = true)]
        [Description("WV.carCameraY.Description", Localize = true)]
        [Slider(-0.4f, 0.4f)]
        public float carCameraY = 0.0012105405f;

        [Name("WV.carCameraZ.Name", Localize = true)]
        [Description("WV.carCameraZ.Description", Localize = true)]
        [Slider(-0.6f, 0.6f)]
        public float carCameraZ = 0f;

        [Name("WV.carEngineHeight.Name", Localize = true)]
        [Description("WV.carEngineHeight.Description", Localize = true)]
        [Slider(-0.6f, 0.4f)]
        public float carEngineHeight = -0.26515353f;

        [Name("WV.carEngineSize.Name", Localize = true)]
        [Description("WV.carEngineSize.Description", Localize = true)]
        [Slider(0.5f, 1.2f)]
        public float carEngineSize = 0.8729342f;

        [Name("WV.carDashboardSize.Name", Localize = true)]
        [Description("WV.carDashboardSize.Description", Localize = true)]
        [Slider(0.5f, 2f)]
        public float carDashboardSize = 1f;

        [Name("WV.carSteeringSpeed.Name", Localize = true)]
        [Description("WV.carSteeringSpeed.Description", Localize = true)]
        [Slider(0.2f, 3f)]
        public float carSteeringSpeed = 1f;

        [Name("WV.carSteeringAdjust.Name", Localize = true)]
        [Description("WV.carSteeringAdjust.Description", Localize = true)]
        [Slider(-180f, 180f)]
        public float carSteeringAdjust = 0f;

        [Name("WV.carKmPerLiter.Name", Localize = true)]
        [Description("WV.carKmPerLiter.Description", Localize = true)]
        [Slider(3f, 30f)]
        public float carKmPerLiter = 11f;

        /// <summary>Fields that are only visible with debug mode on.</summary>
        public static readonly string[] FieldsDebug =
        {
            "engineHeight", "engineSize", "dashboardKey", "dashboardSize", "useFuel", "kmPerLiter", "batteryDrain", "batteryRecharge",
            "acTemp", "generateKey", "partsInventory", "partsSize", "inspectionSize", "cameraX", "cameraY", "cameraZ", "requireBattery", "steeringSpeed", "steeringAdjust",
            "carCameraX", "carCameraY", "carCameraZ", "carEngineHeight", "carEngineSize", "carDashboardSize", "carSteeringSpeed", "carSteeringAdjust", "carKmPerLiter"
        };

        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            try { if (field != null && field.Name == "debugMode") Settings.ApplyVisibility((bool)newValue); }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// Built-in values for CARS when debug mode is off. They start equal to the truck's; once the car has been tuned in game,
    /// the values printed by the log ([Options] ... CAR ...) are pasted here so the mod always ships with them.
    /// </summary>
    internal static class CarDefault
    {
        public static readonly Vector3 Camera = new Vector3(-0.13091224f, 0.0012105405f, 0f);
        public const float EngineHeight = -0.213f;      // tuned in game (log: engine height -0.213)
        public const float EngineSize = 0.977f;          // tuned in game (log: size 0.977)
        public const float DashboardSize = 1f;
        public const float SteeringSpeed = 1f;
        public const float SteeringAdjust = 0f;
        public const float KmPerLiter = 11f;
    }

    /// <summary>Is there an aurora right now? (cached per frame; false if the game call fails)</summary>
    internal static class AuroraGate
    {
        static int frame = -1;
        static bool value;
        public static bool Active()
        {
            if (frame == Time.frameCount) return value;
            frame = Time.frameCount;
            try
            {
                var am = Il2Cpp.GameManager.GetAuroraManager();
                value = am != null && am.AuroraIsActive();
            }
            catch (Exception) { value = false; }
            return value;
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
                MelonLoader.MelonLogger.Warning("Could not create the options page (is the ModSettings mod missing?). Default keys are used: F3 garage, F2 menu. " + e.Message);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DumpValues()
        {
            var o = Options.I;
            MelonLoader.MelonLogger.Msg("[Options] camera=(" + o.cameraX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " + o.cameraY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " + o.cameraZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ") engineHeight=" + o.engineHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " engineSize=" + o.engineSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " dashboardSize=" + o.dashboardSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " steeringAdjust=" + o.steeringAdjust.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " | CAR camera=(" + o.carCameraX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " + o.carCameraY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " + o.carCameraZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ") carEngineHeight=" + o.carEngineHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " carEngineSize=" + o.carEngineSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " carDashboardSize=" + o.carDashboardSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " carSteeringAdjust=" + o.carSteeringAdjust.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
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
        /// <summary>Text language of the mod: Spanish if the game is in Spanish, English otherwise (follows the game's language).</summary>
        public static bool English() { return !LocalizationManager.IsSpanish(); }

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

        [MethodImpl(MethodImplOptions.NoInlining)] static KeyCode LGarage() { return Options.I.garageKey; }
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
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LJerry() { return Options.I.jerryAtPumps; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LCarTrunk() { return Options.I.carTrunkButton; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LLowRes() { return Options.I.lowResources; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LAurora() { return Options.I.auroraOnly; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCCX() { return Options.I.carCameraX; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCCY() { return Options.I.carCameraY; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCCZ() { return Options.I.carCameraZ; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCAlt() { return Options.I.carEngineHeight; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCSize() { return Options.I.carEngineSize; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCTT() { return Options.I.carDashboardSize; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCVel() { return Options.I.carSteeringSpeed; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCFit() { return Options.I.carSteeringAdjust; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LCKm() { return Options.I.carKmPerLiter; }

        public static KeyCode KeyGarage() { return exists ? LGarage() : KeyCode.F3; }
        public static KeyCode KeyMenu() { return exists ? LMenu() : KeyCode.F2; }
        public static bool Sound() { return exists ? LSound() : true; }
        public static float VolumeEngine() { return exists ? LVol() : 0.7f; }
        public static float VelSteering(bool car = false) { if (car) return Debug() ? Mathf.Clamp(LCVel(), 0.2f, 3f) : CarDefault.SteeringSpeed; return Debug() ? Mathf.Clamp(LVelVol(), 0.2f, 3f) : 1f; }
        public static float AdjustSteering(bool car = false) { if (car) return Debug() ? LCFit() : CarDefault.SteeringAdjust; return Debug() ? LFitVol() : 0f; }
        public static KeyCode KeyHood() { return exists ? LHood() : KeyCode.H; }
        public static float HeightEngine(bool car = false) { if (car) return Debug() ? Mathf.Clamp(LCAlt(), -0.6f, 0.4f) : CarDefault.EngineHeight; return Debug() ? Mathf.Clamp(LAlt(), -0.6f, 0.4f) : -0.26515353f; }
        public static float SizeEngine(bool car = false) { if (car) return Debug() ? Mathf.Clamp(LCSize(), 0.5f, 1.2f) : CarDefault.EngineSize; return Debug() ? Mathf.Clamp(LSize(), 0.5f, 1.2f) : 0.8729342f; }
        public static bool RequireBattery() { return Debug() ? LExi() : true; }
        public static bool RemoveBlur() { return exists ? LDes() : false; }
        public static KeyCode KeyGenerate() { return Debug() ? LGen() : KeyCode.None; }
        public static bool PartsInventory() { return Debug() ? LPInv() : true; }
        public static float SizeParts() { return Debug() ? Mathf.Clamp(LTP(), 0.5f, 3f) : 1f; }
        public static float SizeInspection() { return Debug() ? Mathf.Clamp(LTI(), 0.1f, 1.5f) : 0.991702f; }
        public static Vector3 Cam(bool car = false)
        {
            if (car) return Debug() ? new Vector3(Mathf.Clamp(LCCX(), -0.8f, 0.8f), Mathf.Clamp(LCCY(), -0.4f, 0.4f), Mathf.Clamp(LCCZ(), -0.6f, 0.6f)) : CarDefault.Camera;
            return CamTruck();
        }
        static Vector3 CamTruck() { return Debug() ? new Vector3(Mathf.Clamp(LCX(), -0.8f, 0.8f), Mathf.Clamp(LCY(), -0.4f, 0.4f), Mathf.Clamp(LCZ(), -0.6f, 0.6f)) : new Vector3(-0.13091224f, 0.0012105405f, 0f); }
        public static KeyCode KeyDashboard() { return Debug() ? LTab() : KeyCode.None; }
        public static float SizeDashboard(bool car = false) { if (car) return Debug() ? Mathf.Clamp(LCTT(), 0.5f, 2f) : CarDefault.DashboardSize; return Debug() ? Mathf.Clamp(LTT(), 0.5f, 2f) : 1f; }
        public static float VolumeAc() { return exists ? Mathf.Clamp(LVA(), 0f, 0.6f) : 0.12f; }
        public static bool UseFuel() { return Debug() ? LUC() : true; }
        public static float BatteryDrain() { return Debug() ? Mathf.Clamp(LBG(), 0f, 10f) : 2f; }
        public static float BatteryRecharge() { return Debug() ? Mathf.Clamp(LBR(), 0f, 10f) : 2f; }
        public static float TempAc() { return Debug() ? Mathf.Clamp(LTA(), 0f, 25f) : 10f; }
        public static float KmPerLiter(bool car = false) { if (car) return Debug() ? Mathf.Clamp(LCKm(), 3f, 30f) : CarDefault.KmPerLiter; return Debug() ? Mathf.Clamp(LCC(), 3f, 30f) : 11f; }
        /// <summary>Vehicles only work during an aurora (option).</summary>
        public static bool JerryAtPumps() { return exists ? LJerry() : true; }
        public static bool CarTrunkButton() { return exists ? LCarTrunk() : true; }
        /// <summary>Low-resource mode: the background scans run much less often (for weak computers).</summary>
        public static bool LowResources() { return exists ? LLowRes() : false; }
        /// <summary>Multiplier for the interval of the background scans.</summary>
        public static float ScanScale() { return LowResources() ? 5f : 1f; }
        public static bool AuroraOnly() { return exists && LAurora(); }

        /// <summary>Text with the values really used for a vehicle kind (for the log).</summary>
        public static string Describe(bool car)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            Vector3 c = Cam(car);
            return (car ? "CAR" : "TRUCK") + " settings in use (debug " + (Debug() ? "ON" : "off, built-in values") + "): camera=(" + c.x.ToString("R", ci) + ", " + c.y.ToString("R", ci) + ", " + c.z.ToString("R", ci)
                + ") engineHeight=" + HeightEngine(car).ToString("R", ci) + " engineSize=" + SizeEngine(car).ToString("R", ci)
                + " dashboardSize=" + SizeDashboard(car).ToString("R", ci) + " steeringSpeed=" + VelSteering(car).ToString("R", ci)
                + " steeringAdjust=" + AdjustSteering(car).ToString("R", ci) + " kmPerLiter=" + KmPerLiter(car).ToString("R", ci);
        }
        public static bool StartOnSit() { return exists ? LStarter() : false; }
    }
}
