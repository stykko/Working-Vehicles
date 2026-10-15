using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>The engine parts (besides the battery, which is a game item).</summary>
    internal static class Parts
    {
        public const int Count = 6;
        public const int Alternator = 0, SparkPlugs = 1, Cables = 2, Cap = 3, Head = 4, Gasket = 5;

        public static readonly string[] Names =
        {
            "Alternador", "Bujías", "Cables de encendido", "Tapa de balancín", "Culata", "Junta de culata"
        };

        public static readonly string[] NameIn =
        {
            "Alternator", "Spark plugs", "Ignition wires", "Valve cover", "Cylinder head", "Head gasket"
        };

        public static string NameOf(int i)
        {
            return L.En ? NameIn[i] : Names[i];
        }

        // To remove a part, the ones mounted on top of it must be removed first; to install it, the one below must be there.
        // -1 = depends on nothing.
        public static readonly int[] GoesOn = { -1, Head, SparkPlugs, Head, Gasket, -1 };

        public const float Min = 0.20f;       // below this the part is useless
        public const float Worn = 0.50f;      // below this the engine starts worse

        /// <summary>Initial state of a truck that hasn't been saved yet: everything installed, with different wear (always the same for the same truck).</summary>
        public static float[] Initial(Vector3 orig)
        {
            var rnd = new System.Random(unchecked((int)(orig.x * 100f) * 73856093 ^ (int)(orig.z * 100f) * 19349663));
            var c = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                double u = rnd.NextDouble();
                c[i] = u < 0.12 ? (float)(0.05 + rnd.NextDouble() * 0.14) : (float)(0.35 + rnd.NextDouble() * 0.65);
            }
            return c;
        }

        public static string Pct(float c) { return Mathf.RoundToInt(Mathf.Clamp01(c) * 100f) + "%"; }
    }

    internal class Loose
    {
        public int type;
        public float condition;
        public string scene;
        public Vector3 pos;
        public GameObject go;
    }

    internal class InPack
    {
        public int type;
        public float condition;
    }

    /// <summary>
    /// The parts the player carries ("pack") and the ones left on the ground, per saved game.
    /// Saved in UserData\WorkingVehicles_parts.txt (old name: CamionetaConducible_piezas.txt, migrated automatically).
    /// </summary>
    internal static class Storage
    {
        public static readonly List<InPack> pack = new List<InPack>();
        public static readonly List<Loose> ground = new List<Loose>();
        static string slotLoaded;

        static string FilePath { get { return Cfg.DataFile("WorkingVehicles_parts.txt", "CamionetaConducible_piezas.txt", 1); } }
        static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
        static string S(float f) { return f.ToString("R", CultureInfo.InvariantCulture); }

        /// <summary>Loads the data of the current saved game (if the game changed, reads again).</summary>
        public static void Ensure(string slot)
        {
            if (slotLoaded == slot) return;
            MelonLoader.MelonLogger.Msg("[Parts] saved game '" + slot + "' (before: '" + (slotLoaded ?? "none") + "')");
            slotLoaded = slot;
            foreach (Loose s in ground) if (s.go != null) UnityEngine.Object.Destroy(s.go);
            pack.Clear();
            ground.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] p = line.Split('|');
                    if (p.Length < 4 || p[1] != slot) continue;
                    int type = int.Parse(p[2], CultureInfo.InvariantCulture);
                    if (type < 0 || type >= Parts.Count) continue;
                    float condition = Mathf.Clamp01(F(p[3]));
                    if (p[0] == "M") pack.Add(new InPack { type = type, condition = condition });
                    else if (p[0] == "S" && p.Length == 6)
                    {
                        string[] q = p[5].Split(';');
                        ground.Add(new Loose { type = type, condition = condition, scene = p[4], pos = new Vector3(F(q[0]), F(q[1]), F(q[2])) });
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("Could not read the saved parts: " + e.Message); }
            MelonLoader.MelonLogger.Msg("[Parts] loaded: " + pack.Count + " in the pack, " + ground.Count + " on the ground");
        }

        public static void Save(string slot)
        {
            try
            {
                var otherSet = new List<string>();
                if (File.Exists(FilePath))
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 2 && p[1] != slot) otherSet.Add(line);
                    }
                foreach (InPack m in pack) otherSet.Add("M|" + slot + "|" + m.type + "|" + S(m.condition));
                foreach (Loose s in ground)
                    otherSet.Add("S|" + slot + "|" + s.type + "|" + S(s.condition) + "|" + s.scene + "|" + S(s.pos.x) + ";" + S(s.pos.y) + ";" + S(s.pos.z));
                if (otherSet.Count == 0 && !File.Exists(FilePath)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, otherSet.ToArray());
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("Could not save the parts: " + e.Message); }
        }

        /// <summary>Takes the part of that type with the best condition out of the pack. Returns -1 if there is none.</summary>
        public static float TakeBest(int type)
        {
            int best = -1;
            for (int i = 0; i < pack.Count; i++)
                if (pack[i].type == type && (best < 0 || pack[i].condition > pack[best].condition)) best = i;
            if (best < 0) return -1f;
            float c = pack[best].condition;
            pack.RemoveAt(best);
            return c;
        }

        public static int HowMany(int type)
        {
            int n = 0;
            foreach (InPack m in pack) if (m.type == type) n++;
            return n;
        }

        public static string Summary()
        {
            if (pack.Count == 0) return L.T("vacía", "empty");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pack.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Parts.NameOf(pack[i].type)).Append(' ').Append(Parts.Pct(pack[i].condition));
            }
            return sb.ToString();
        }
    }
}
