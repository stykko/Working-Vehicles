using System;
using MelonLoader;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>Options. Saved in UserData\WorkingVehicles.cfg (editable with Notepad, with the game closed).</summary>
    internal static class Cfg
    {
        public static MelonPreferences_Entry<bool> Enabled, InvertTurnView, CorrectLimitView, DebugEntry, ExitOnLoad, Reports;
        public static MelonPreferences_Entry<float> VelMaxKmh, VelBackKmh, Acceleration, Braking, BrakeHand, Drag, TurnMax, Length, Width, Height, NormalGround, MarginObjects, SteeringDegrees, WheelsDegrees, SteeringForward, SteeringDown;
        public static MelonPreferences_Entry<string> TAccelerate, TBrake, TLeft, TRight, TBrakeHand;

        static readonly System.Collections.Generic.HashSet<string> merged = new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// Path of a data file in UserData. Data from the old (Spanish) file name is never lost: if the new file is missing or empty it is copied,
        /// and if it exists but lacks some saved games (slotField = position of the saved-game name in each line, -1 = none), those lines are added.
        /// The old file is never modified or deleted.
        /// </summary>
        public static string DataFile(string newName, string oldName, int slotField = -1)
        {
            string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "UserData");
            string np = System.IO.Path.Combine(dir, newName), op = System.IO.Path.Combine(dir, oldName);
            if (!merged.Add(newName)) return np;
            try
            {
                if (!System.IO.File.Exists(op)) return np;
                if (!System.IO.File.Exists(np) || new System.IO.FileInfo(np).Length == 0) { System.IO.File.Copy(op, np, true); return np; }
                if (slotField < 0) return np;
                var have = new System.Collections.Generic.HashSet<string>();
                var lines = new System.Collections.Generic.List<string>(System.IO.File.ReadAllLines(np));
                foreach (string l in lines) { string[] q = l.Split('|'); if (q.Length > slotField) have.Add(q[slotField]); }
                bool added = false;
                foreach (string l in System.IO.File.ReadAllLines(op))
                {
                    string[] q = l.Split('|');
                    if (q.Length > slotField && !have.Contains(q[slotField])) { lines.Add(l); added = true; }
                }
                if (added) { System.IO.File.WriteAllLines(np, lines.ToArray()); MelonLoader.MelonLogger.Msg("[Data] saved games recovered from " + oldName); }
            }
            catch (Exception) { }
            return np;
        }

        static readonly string[,] OldKeys =
        {
            { "Activado", "Enabled" },
            { "VelocidadMaximaKmh", "MaxSpeedKmh" },
            { "VelocidadMarchaAtrasKmh", "MaxReverseSpeedKmh" },
            { "Aceleracion", "Acceleration" },
            { "Frenado", "Braking" },
            { "FrenoDeMano", "Handbrake" },
            { "Inercia", "Drag" },
            { "GiroMaximoGradosPorSeg", "MaxTurnDegPerSec" },
            { "LargoColision", "CollisionLength" },
            { "AnchoColision", "CollisionWidth" },
            { "AltoColision", "CollisionHeight" },
            { "PendienteMaxima", "MaxSlope" },
            { "MargenObjetos", "ObjectMargin" },
            { "VolanteGrados", "SteeringWheelDegrees" },
            { "RuedasGrados", "WheelDegrees" },
            { "VolanteDistanciaAdelante", "SteeringSearchForward" },
            { "VolanteDistanciaAbajo", "SteeringSearchDown" },
            { "CorregirLimiteDeVista", "FixViewLimit" },
            { "InvertirGiroVista", "InvertViewTurn" },
            { "TeclaAcelerar", "KeyAccelerate" },
            { "TeclaFrenar", "KeyBrake" },
            { "TeclaIzquierda", "KeyLeft" },
            { "TeclaDerecha", "KeyRight" },
            { "TeclaFrenoMano", "KeyHandbrake" },
            { "SalirAlCargar", "ExitOnLoad" },
            { "EscribirInformes", "WriteReports" },
            { "Depurar", "DebugMessages" }
        };

        /// <summary>If only the old .cfg (Spanish names) exists, it is converted to the new file so nobody loses their settings.</summary>
        static void MigrateCfg()
        {
            try
            {
                string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "UserData");
                string np = System.IO.Path.Combine(dir, "WorkingVehicles.cfg"), op = System.IO.Path.Combine(dir, "CamionetaConducible.cfg");
                if (System.IO.File.Exists(np) || !System.IO.File.Exists(op)) return;
                string t = System.IO.File.ReadAllText(op).Replace("[CamionetaConducible]", "[WorkingVehicles]");
                for (int i = 0; i < OldKeys.GetLength(0); i++)
                    t = System.Text.RegularExpressions.Regex.Replace(t, "(?m)^" + OldKeys[i, 0] + "(\\s*=)", OldKeys[i, 1] + "$1");
                System.IO.File.WriteAllText(np, t);
            }
            catch (Exception) { }
        }

        public static void Init()
        {
            MigrateCfg();
            var cat = MelonPreferences.CreateCategory("WorkingVehicles", "Working Vehicles");
            cat0 = cat;
            cat.SetFilePath("UserData/WorkingVehicles.cfg");

            Enabled = cat.CreateEntry("Enabled", true, "Mod enabled", "Main switch (keys are changed in Mod Options).");
            VelMaxKmh = cat.CreateEntry("MaxSpeedKmh", 70f, "Top speed (km/h)", "");
            VelBackKmh = cat.CreateEntry("MaxReverseSpeedKmh", 20f, "Top reverse speed (km/h)", "");
            Acceleration = cat.CreateEntry("Acceleration", 4f, "Acceleration (m/s²)", "How long it takes to get going. Higher = more agile.");
            Braking = cat.CreateEntry("Braking", 10f, "Braking (m/s²)", "Force when pressing S while going forward.");
            BrakeHand = cat.CreateEntry("Handbrake", 14f, "Handbrake (m/s²)", "");
            Drag = cat.CreateEntry("Drag", 1.6f, "Deceleration without accelerating (m/s²)", "Lower = the vehicle keeps rolling longer.");
            TurnMax = cat.CreateEntry("MaxTurnDegPerSec", 55f, "Maximum turn rate (°/s)", "");
            Length = cat.CreateEntry("CollisionLength", 4.6f, "Collision box length (m)", "");
            Width = cat.CreateEntry("CollisionWidth", 1.9f, "Collision box width (m)", "");
            Height = cat.CreateEntry("CollisionHeight", 1.3f, "Collision box height (m)", "");
            NormalGround = cat.CreateEntry("MaxSlope", 0.7f, "Maximum slope (0.7 = about 45°)", "Surfaces steeper than this count as a wall.");
            MarginObjects = cat.CreateEntry("ObjectMargin", 1.2f, "Margin for carrying objects (m)", "Loose objects (dropped things, boxes) within this distance of the truck travel with it.");
            SteeringDegrees = cat.CreateEntry("SteeringWheelDegrees", 100f, "How far the steering wheel turns to the stop (degrees)", "");
            WheelsDegrees = cat.CreateEntry("WheelDegrees", 30f, "How far the front wheels turn with the steering wheel at the stop (degrees)", "");
            SteeringForward = cat.CreateEntry("SteeringSearchForward", 0.55f, "Where to look for the steering wheel: meters forward from your eyes", "Only if the steering wheel is not detected properly.");
            SteeringDown = cat.CreateEntry("SteeringSearchDown", 0.30f, "Where to look for the steering wheel: meters down from your eyes", "Only if the steering wheel is not detected properly.");
            CorrectLimitView = cat.CreateEntry("FixViewLimit", true, "Move the head turn limit along with the truck", "If the view gets stuck or jumps when turning, try false.");
            InvertTurnView = cat.CreateEntry("InvertViewTurn", false, "Invert the direction the view turns", "If the view turns the wrong way when the truck turns, set it to true.");
            TAccelerate = cat.CreateEntry("KeyAccelerate", "W", "Accelerate key", "Unity names: W, UpArrow, Space, LeftShift...");
            TBrake = cat.CreateEntry("KeyBrake", "S", "Brake / reverse key", "");
            TLeft = cat.CreateEntry("KeyLeft", "A", "Left key", "");
            TRight = cat.CreateEntry("KeyRight", "D", "Right key", "");
            TBrakeHand = cat.CreateEntry("KeyHandbrake", "Space", "Handbrake key", "");
            ExitOnLoad = cat.CreateEntry("ExitOnLoad", true, "When loading a saved game while inside a vehicle, take the player out and leave them next to the door", "Prevents the game from seating you in another vehicle or another place on load.");
            Reports = cat.CreateEntry("WriteReports", false, "Write report files to UserData", "Diagnostics only (hierarchy, meshes, glass, camera). Currently on (development); off by default in the public version.");
            DebugEntry = cat.CreateEntry("DebugMessages", false, "Debug messages", "Writes to the MelonLoader console what the mod is doing (useful while testing).");

            cat.SaveToFile(false);
        }

        /// <summary>Reports and debug messages: only with debug mode on (in Mod Options) and its option in the .cfg.</summary>
        public static bool Report { get { return Settings.Debug() && Reports != null && Reports.Value; } }
        public static bool DebugLog { get { return Settings.Debug() && DebugEntry != null && DebugEntry.Value; } }

        static MelonPreferences_Category cat0;
        public static void Save() { if (cat0 != null) cat0.SaveToFile(false); }

        public static KeyCode Key(MelonPreferences_Entry<string> e, KeyCode perDefault)
        {
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), e.Value, true); }
            catch (Exception) { return perDefault; }
        }
    }
}
