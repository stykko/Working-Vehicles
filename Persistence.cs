using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2Cpp;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>What is remembered about each vehicle (per saved game).</summary>
    internal class Data
    {
        public string slot;
        public Vector3 orig, pos;
        public Quaternion rot = Quaternion.identity;
        public bool snowRemoved;
        public float steering;        // degrees; positive = to the right
        public bool isRunning;
        public float level = 1f;     // accumulated snow: 0 = clean, 1 = as the game brings it
        public float hour = -1f;     // in-game hours at the last snow update
        public bool battery;         // battery installed (trucks)
        public float batteryCondition = 1f;   // battery condition (0..1)
        public float[] parts;           // condition of each engine part (-1 = not there); null = no data
        public float fuel = 0.4f; // tank: 0..1
    }

    /// <summary>Which vehicle the player was sitting in (for when the game is saved and quit while inside).</summary>
    internal class Marker
    {
        public string slot;
        public Vector3 orig;
        public string door;
    }

    /// <summary>
    /// The game doesn't know we moved the vehicles, so on load it always puts them in their original place.
    /// Here the state of each vehicle (identified by its original position) is saved, per saved game, in
    /// UserData\WorkingVehicles_positions.txt (old name migrated automatically): position, rotation, snow, steering wheel and engine.
    /// </summary>
    internal static class Persistence
    {
        /// <summary>When a game is loaded, anything unsaved is discarded and the file is read again.</summary>
        public static void Revert()
        {
            list.Clear();
            loaded = false;
            Load();
        }

        static readonly List<Data> list = new List<Data>();
        static bool loaded;
        static string FilePath { get { return Cfg.DataFile("WorkingVehicles_positions.txt", "CamionetaConducible_posiciones.txt", 0); } }

        public static string Slot()
        {
            try
            {
                string s = SaveGameSystem.GetCurrentSaveName();
                return string.IsNullOrEmpty(s) ? "default" : s.Replace('|', '_');
            }
            catch (Exception) { return "default"; }
        }

        static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
        static string S(float f) { return f.ToString("R", CultureInfo.InvariantCulture); }

        static float[] ReadParts(string t)
        {
            string[] q = t.Split(';');
            if (q.Length != Parts.Count) return null;
            var r = new float[Parts.Count];
            for (int i = 0; i < r.Length; i++) r[i] = F(q[i]);
            return r;
        }

        static string WriteParts(float[] c)
        {
            if (c == null || c.Length != Parts.Count) return "";
            var q = new string[c.Length];
            for (int i = 0; i < c.Length; i++) q[i] = S(c[i]);
            return string.Join(";", q);
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] p = line.Split('|');
                    if (p.Length != 4 && p.Length != 5 && p.Length != 7 && p.Length != 9 && p.Length != 11 && p.Length != 12 && p.Length != 13) continue;
                    string[] o = p[1].Split(';'), q = p[2].Split(';'), r = p[3].Split(';');
                    if (o.Length != 3 || q.Length != 3 || r.Length != 4) continue;
                    list.Add(new Data
                    {
                        slot = p[0],
                        orig = new Vector3(F(o[0]), F(o[1]), F(o[2])),
                        pos = new Vector3(F(q[0]), F(q[1]), F(q[2])),
                        rot = new Quaternion(F(r[0]), F(r[1]), F(r[2]), F(r[3])),
                        snowRemoved = p.Length >= 5 && p[4] == "1",
                        steering = p.Length >= 7 ? F(p[5]) : 0f,
                        isRunning = p.Length >= 7 ? p[6] == "1" : Settings.StartOnSit(),
                        level = p.Length >= 9 ? Mathf.Clamp01(F(p[7])) : (p.Length >= 5 && p[4] == "1" ? 0f : 1f),
                        hour = p.Length >= 9 ? F(p[8]) : -1f,
                        battery = p.Length >= 11 && p[9] == "1",
                        batteryCondition = p.Length >= 11 ? Mathf.Clamp01(F(p[10])) : 1f,
                        parts = p.Length >= 12 ? ReadParts(p[11]) : null,
                        fuel = p.Length == 13 ? Mathf.Clamp01(F(p[12])) : 0.4f
                    });
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("Could not read the saved positions: " + e.Message); }
        }

        public static Data Find(string slot, Vector3 orig)
        {
            Load();
            foreach (Data e in list)
                if (e.slot == slot && (e.orig - orig).sqrMagnitude < 1f) return e;
            return null;
        }

        public static void Save(string slot, Data d)
        {
            Load();
            Data ex = Find(slot, d.orig);
            if (ex == null) { ex = new Data { slot = slot, orig = d.orig }; list.Add(ex); }
            ex.pos = d.pos; ex.rot = d.rot; ex.snowRemoved = d.snowRemoved; ex.steering = d.steering; ex.isRunning = d.isRunning; ex.level = d.level; ex.hour = d.hour; ex.battery = d.battery; ex.batteryCondition = d.batteryCondition; ex.parts = d.parts; ex.fuel = d.fuel;
        }

        /// <summary>Writes what is in memory to the file. It is only called when the game saves:
        /// so, if you quit without saving, the truck goes back to where it was at the last save.</summary>
        public static void Confirm()
        {
            Load();
            try
            {
                var l = new List<string>();
                foreach (Data e in list)
                    l.Add(e.slot + "|" + S(e.orig.x) + ";" + S(e.orig.y) + ";" + S(e.orig.z)
                          + "|" + S(e.pos.x) + ";" + S(e.pos.y) + ";" + S(e.pos.z)
                          + "|" + S(e.rot.x) + ";" + S(e.rot.y) + ";" + S(e.rot.z) + ";" + S(e.rot.w)
                          + "|" + (e.level < 0.5f ? "1" : "0") + "|" + S(e.steering) + "|" + (e.isRunning ? "1" : "0") + "|" + S(e.level) + "|" + S(e.hour) + "|" + (e.battery ? "1" : "0") + "|" + S(e.batteryCondition) + "|" + WriteParts(e.parts) + "|" + S(e.fuel));
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, l.ToArray());
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("Could not save the vehicle state: " + e.Message); }
        }

    }
}
