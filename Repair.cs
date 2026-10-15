using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// Repair of the engine parts at the game's workbench (the same menu used to repair tools).
    /// Each part gets a game "Repairable" component with its materials and tools:
    ///   Spark plugs and ignition wires: wire + scrap metal
    ///   Alternator: fuses + wire + scrap metal
    ///   Valve cover and cylinder head: scrap metal + bolts + nuts
    ///   Head gasket: scrap metal
    /// and always basic or high-quality tools. The items are looked up in the game by name; anything not found is reported in the log
    /// ([Reparar] line) and is not requested as a material.
    /// </summary>
    internal static class Repair
    {
        const int Metal = 0, Cable = 1, Fuse = 2, Bolt = 3, Nut = 4;
        static readonly string[] label = { "trozo de metal", "cable", "fusible", "tornillo", "tuerca" };

        // possible names of each material in the game (tried in order) and loose words to search for them if none match
        static readonly string[][] candidates =
        {
            new[] { "GEAR_ScrapMetal" },
            new[] { "GEAR_WireBundle", "GEAR_Wire", "GEAR_Wires", "GEAR_Cable", "GEAR_ElectricalWire", "GEAR_CopperWire", "GEAR_ElectricalCable" },
            new[] { "GEAR_Fuse", "GEAR_Fuses", "GEAR_SpareFuse", "GEAR_SpareFuses", "GEAR_FuseBox" },
            new[] { "GEAR_NutsNBolts", "GEAR_Screws", "GEAR_Screw" },      // in the game "nuts and bolts" is a single item
            new[] { "GEAR_NutsNBolts" },
        };
        static readonly string[][] words =
        {
            new[] { "scrap" },
            new[] { "wirebundle", "wire", "wires", "cable", "cables" },
            new[] { "fuse", "fuses" },
            new[] { "screw", "screws" },
            new[] { "nutsnbolts" },
        };
        static readonly string[] toolNames = { "GEAR_SimpleTools", "GEAR_HighQualityTools" };

        // what each part needs (material, quantity)
        static readonly int[][][] recipes =
        {
            /* Alternator */           new[] { new[] { Fuse, 2 }, new[] { Cable, 2 }, new[] { Metal, 1 } },
            /* Bujías */               new[] { new[] { Cable, 1 }, new[] { Metal, 1 } },
            /* Cables de encendido */  new[] { new[] { Cable, 2 }, new[] { Metal, 1 } },
            /* Cap de balancín */     new[] { new[] { Metal, 2 }, new[] { Bolt, 2 }, new[] { Nut, 2 } },
            /* Head */               new[] { new[] { Metal, 3 }, new[] { Bolt, 3 }, new[] { Nut, 3 } },
            /* Gasket de culata */      new[] { new[] { Metal, 1 } },
        };
        static readonly int[] minutes = { 120, 60, 60, 90, 150, 45 };

        static readonly GearItem[] mat = new GearItem[5];
        static readonly string[] matName = new string[5];
        static readonly List<ToolsItem> tools = new List<ToolsItem>();
        static readonly HashSet<int> made = new HashSet<int>();
        static bool resolved;
        static int attempts;
        static float next;

        static string Clean(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            int i = n.IndexOf("(Clone)", StringComparison.Ordinal);
            if (i >= 0) n = n.Substring(0, i);
            return n.Trim();
        }

        static List<string> Connectors(string names)
        {
            var r = new List<string>();
            var sb = new StringBuilder();
            string n = names.StartsWith("GEAR_", StringComparison.OrdinalIgnoreCase) ? names.Substring(5) : names;
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                bool cut = c == '_' || c == ' ' || (i > 0 && char.IsUpper(c) && !char.IsUpper(n[i - 1]));
                if (cut && sb.Length > 0) { r.Add(sb.ToString().ToLowerInvariant()); sb.Length = 0; }
                if (c != '_' && c != ' ') sb.Append(c);
            }
            if (sb.Length > 0) r.Add(sb.ToString().ToLowerInvariant());
            return r;
        }

        static GearItem Prefab(string n)
        {
            try { return GearItem.LoadGearItemPrefab(n); }
            catch (Exception) { return null; }
        }

        static IEnumerable<object> Iterate(object collection)
        {
            var res = new List<object>();
            if (collection == null) return res;
            try
            {
                MethodInfo ge = collection.GetType().GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (ge == null) return res;
                object here = ge.Invoke(collection, null);
                Type te = here.GetType();
                MethodInfo mn = te.GetMethod("MoveNext", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                PropertyInfo cur = te.GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
                int save = 0;
                while ((bool)mn.Invoke(here, null) && save++ < 200000) res.Add(cur.GetValue(here, null));
            }
            catch (Exception) { }
            return res;
        }

        /// <summary>All the GEAR_ items the game can create (Addressables keys). Done by reflection: if something changes, it simply contributes no names.</summary>
        static void CatalogAddressables(SortedDictionary<string, bool> cat)
        {
            try
            {
                Type t = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = a.GetType("UnityEngine.AddressableAssets.Addressables", false);
                    if (t != null) break;
                }
                if (t == null) { MelonLoader.MelonLogger.Msg("[Repair] Addressables: type not found"); return; }
                PropertyInfo pl = t.GetProperty("ResourceLocators", BindingFlags.Public | BindingFlags.Static);
                if (pl == null) { MelonLoader.MelonLogger.Msg("[Repair] Addressables: no ResourceLocators"); return; }
                int locs = 0, keys = 0;
                var rx = new Regex("GEAR_[A-Za-z0-9_]+");
                foreach (object loc in Iterate(pl.GetValue(null, null)))
                {
                    locs++;
                    PropertyInfo pk = loc.GetType().GetProperty("Keys", BindingFlags.Public | BindingFlags.Instance);
                    if (pk == null) continue;
                    foreach (object k in Iterate(pk.GetValue(loc, null)))
                    {
                        keys++;
                        if (k == null) continue;
                        string ks = k.ToString();
                        if (string.IsNullOrEmpty(ks) || ks.IndexOf("GEAR_", StringComparison.Ordinal) < 0) continue;
                        Match m = rx.Match(ks);
                        if (m.Success) cat[m.Value] = true;
                    }
                }
                if (Cfg.Report) MelonLoader.MelonLogger.Msg("[Repair] Addressables: " + locs + " catalogs, " + keys + " keys, GEAR_ items seen: " + cat.Count);
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Repair] Addressables: " + e.Message); }
        }

        /// <summary>Collects all the item names that can be found in the game (all the game's, loaded ones, workshop recipes).</summary>
        static SortedDictionary<string, bool> Catalog()
        {
            var cat = new SortedDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (GearItem g in Resources.FindObjectsOfTypeAll<GearItem>())
                {
                    if (g == null || g.gameObject == null) continue;
                    string n = Clean(g.gameObject.name);
                    if (n.StartsWith("GEAR_", StringComparison.OrdinalIgnoreCase)) cat[n] = true;
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Repair] catalog (loaded objects): " + e.Message); }
            try
            {
                BlueprintManager bm = UnityEngine.Object.FindObjectOfType<BlueprintManager>();
                if (bm != null)
                {
                    var list = bm.GetAllBlueprints();
                    for (int i = 0; list != null && i < list.Count; i++)
                    {
                        BlueprintData bp = list[i];
                        if (bp == null) continue;
                        try
                        {
                            var req = bp.m_RequiredGear;
                            for (int k = 0; req != null && k < req.Length; k++)
                                if (req[k] != null && req[k].m_Item != null) cat[Clean(req[k].m_Item.gameObject.name)] = true;
                            if (bp.m_CraftedResultGear != null) cat[Clean(bp.m_CraftedResultGear.gameObject.name)] = true;
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Repair] catalog (recipes): " + e.Message); }
            return cat;
        }

        static void Resolve()
        {
            resolved = true;
            attempts++;
            next = Time.unscaledTime + 45f;
            SortedDictionary<string, bool> cat = Catalog();
            tools.Clear();
            int missing = 0;
            for (int m = 0; m < 5; m++)
            {
                mat[m] = null; matName[m] = null;
                foreach (string c in candidates[m])
                {
                    GearItem p = Prefab(c);
                    if (p != null) { mat[m] = p; matName[m] = c; break; }
                }
                if (mat[m] == null)
                {
                    var similar = new List<string>();
                    foreach (string n in cat.Keys)
                    {
                        List<string> f = Connectors(n);
                        bool ok = false;
                        foreach (string w in words[m]) if (f.Contains(w)) ok = true;
                        foreach (string x in f) if (x.StartsWith("cutter", StringComparison.Ordinal) || x == "driver" || x == "box" || x == "crate") ok = false;   // bolt cutters, screwdrivers, boxes...
                        if (ok) similar.Add(n);
                    }
                    similar.Sort((x, y) => x.Length.CompareTo(y.Length));          // the shortest name is usually the loose item
                    if (Cfg.Report) MelonLoader.MelonLogger.Msg("[Repair] " + label[m] + ": similar names in the game: " + (similar.Count == 0 ? "(none)" : string.Join(", ", similar.ToArray())));
                    foreach (string n in similar)
                    {
                        GearItem p = Prefab(n);
                        if (p != null) { mat[m] = p; matName[m] = n; break; }
                    }
                }
                if (mat[m] == null) missing++;
                if (Cfg.Report) MelonLoader.MelonLogger.Msg("[Repair] " + label[m] + ": " + (matName[m] ?? "NOT FOUND"));
            }
            foreach (string t in toolNames)
            {
                GearItem p = Prefab(t);
                ToolsItem ti = p != null ? p.m_ToolsItem : null;
                if (ti != null) { tools.Add(ti); MelonLoader.MelonLogger.Msg("[Repair] tool: " + t); }
                else { MelonLoader.MelonLogger.Msg("[Repair] tool NOT FOUND: " + t); missing++; }
            }
            if (Cfg.Report)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Game objects the mod could see (to choose repair materials)");
                    foreach (string n in cat.Keys) sb.AppendLine(n);
                    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "WorkingVehicles_objects.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
                catch (Exception) { }
            }
            if (missing > 0 && attempts < 6) { made.Clear(); }          // retried later (the game may not have loaded them yet)
            else if (missing > 0) MelonLoader.MelonLogger.Warning("[Repair] some materials were not found: they are not requested for the repair.");
        }

        /// <summary>Leaves the part ready to be repaired at the workbench.</summary>
        public static void Prepare(GearItem g, int type)
        {
            try
            {
                if (g == null || g.gameObject == null || type < 0 || type >= Parts.Count) return;
                if (!resolved || (attempts < 6 && Time.unscaledTime > next && NoComplete())) Resolve();
                int id = g.gameObject.GetInstanceID();
                if (made.Contains(id)) return;
                made.Add(id);

                Repairable rep = g.m_Repairable;
                bool freshOne = rep == null;
                float incOrig = -1f, capOrig = -1f;
                if (freshOne)
                {
                    rep = g.gameObject.AddComponent<Repairable>();
                    g.m_Repairable = rep;
                    rep.m_GearItem = g;
                }
                else
                {
                    incOrig = rep.m_ConditionIncrease; capOrig = rep.m_RepairConditionCap;
                }
                if (!freshOne)
                {
                    int nf = -1;
                    try { var fl = rep.GetFilteredRepairToolChoices(); nf = fl != null ? fl.Count : 0; } catch (Exception) { }
                    if (Cfg.Report) MelonLoader.MelonLogger.Msg("[Repair] (diagnostic) '" + Parts.Names[type] + "' came with: requires tool=" + rep.m_RequiresToolToRepair + ", possible tools=" + nf);
                }

                // materials (if two different parts turn out to be the same game item, the quantities are added up)
                var objs = new List<GearItem>();
                var count = new List<int>();
                var nameList = new List<string>();
                foreach (int[] r in recipes[type])
                {
                    GearItem p = mat[r[0]];
                    if (p == null) continue;
                    string n = matName[r[0]];
                    int ix = nameList.IndexOf(n);
                    if (ix >= 0) { count[ix] = Mathf.Max(count[ix], r[1]); continue; }
                    objs.Add(p); count.Add(r[1]); nameList.Add(n);
                }
                var arr = new Il2CppReferenceArray<GearItem>(objs.Count);
                var un = new int[count.Count];
                for (int i = 0; i < objs.Count; i++) { arr[i] = objs[i]; un[i] = count[i]; }
                rep.m_RequiredGear = arr;
                rep.m_RequiredGearUnits = un;

                var tarr = new Il2CppReferenceArray<ToolsItem>(tools.Count);
                for (int i = 0; i < tools.Count; i++) tarr[i] = tools[i];
                rep.m_RepairToolChoices = tarr;
                rep.m_RequiresToolToRepair = true;

                // in the game repairs are measured in percent: each one adds 25 and the cap is 100
                rep.m_ConditionIncrease = 25f;
                rep.m_RepairConditionCap = 100f;
                rep.m_DurationMinutes = minutes[type];
                rep.m_NeverFail = true;
                try { if (string.IsNullOrEmpty(rep.m_RepairAudio)) rep.m_RepairAudio = "Play_RepairingMetal"; } catch (Exception) { }
                try { rep.InitializeRepairToolChoices(); } catch (Exception) { }
                // the list of tools the bench offers is forced directly (basic or high-quality)
                try
                {
                    var filtered = new Il2CppSystem.Collections.Generic.List<ToolsItem>();
                    foreach (ToolsItem ti in tools) filtered.Add(ti);
                    rep.m_FilteredRepairToolChoices = filtered;
                    rep.m_RequiresToolToRepair = true;
                }
                catch (Exception e) { MelonLoader.MelonLogger.Warning("[Repair] tools: " + e.Message); }
                try { if (Cfg.Report) MelonLoader.MelonLogger.Msg("[Repair] '" + Parts.Names[type] + "': requires tool=" + rep.RequiresToolToRepair() + ", options=" + rep.GetNumRepairToolChoices() + ", offered=" + rep.GetFilteredRepairToolChoices().Count); } catch (Exception) { }

                // the parts come from tools (hammer, hatchet, knife...): whatever doesn't belong on an engine part is removed from them
                try { Sharpenable sh = g.m_Sharpenable; if (sh != null) { g.m_Sharpenable = null; UnityEngine.Object.Destroy(sh.TryCast<Component>()); } } catch (Exception) { }
                try { HarvestBase hv = g.m_Harvest; if (hv != null) { g.m_Harvest = null; UnityEngine.Object.Destroy(hv.TryCast<Component>()); } } catch (Exception) { }

                if (Cfg.Report) MelonLoader.MelonLogger.Msg("[Repair] '" + Parts.Names[type] + "' ready for the workbench (" + (freshOne ? "new component" : "already had a repair: increase " + incOrig + ", cap " + capOrig) + "; materials: " + string.Join(", ", nameList.ToArray()) + "; increase " + rep.m_ConditionIncrease.ToString("F1") + ", cap " + rep.m_RepairConditionCap.ToString("F1") + ")");
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Repair] " + e.Message); }
        }

        static bool NoComplete()
        {
            for (int i = 0; i < 5; i++) if (mat[i] == null) return true;
            return tools.Count < toolNames.Length;
        }
    }
}
