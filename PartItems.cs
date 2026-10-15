using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// The engine parts as REAL items of the game's inventory.
    /// The game has no "alternator" or "cylinder head", so each part is a tool item from the game (the first one that exists in
    /// the list) to which the mod gives the part's name and remembers which one it is by its unique identifier (GUID), which the game
    /// saves with the saved game. The part's condition is the item's condition. They can be dropped and picked up like any item.
    /// If any of this fails (there is no base item or no GUID), the mod uses the old "pack" system.
    /// </summary>
    internal static class PartItems
    {
        static readonly string[] pool = { "GEAR_Hammer", "GEAR_Hacksaw", "GEAR_Prybar", "GEAR_Hatchet", "GEAR_SimpleTools", "GEAR_Knife" };
        static int failures;
        /// <summary>If it already failed several times, old parts are not converted automatically (avoids creating and deleting items endlessly).</summary>
        public static bool Blocked { get { return failures >= 2; } }
        static string slotLoaded;
        static readonly Dictionary<string, int> registry = new Dictionary<string, int>();   // GUID -> part type
        static readonly Mesh[] meshes = new Mesh[Parts.Count];
        static readonly Il2CppReferenceArray<Material>[] mats = new Il2CppReferenceArray<Material>[Parts.Count];
        static float sweepT;

        static string FilePath { get { return Cfg.DataFile("WorkingVehicles_partitems.txt", "CamionetaConducible_piezasitem.txt", 0); } }

        /// <summary>Are inventory items used? (option on and no failures creating them)</summary>
        public static bool Active { get { return Settings.PartsInventory(); } }

        /// <summary>Asks the game to give the item a unique identifier (trying several ways) and returns it.</summary>
        static string CreateGuid(GearItem g)
        {
            string id = Guid(g);
            if (!string.IsNullOrEmpty(id)) return id;
            GameObject go = g.gameObject;
            try { g.ForceGUIDSetup(); } catch (Exception e) { MelonLoader.MelonLogger.Msg("[Piezas] ForceGUIDSetup: " + e.Message); }
            id = Guid(g);
            if (!string.IsNullOrEmpty(id)) return id;
            try
            {
                ObjectGuid og = ObjectGuid.GetObjectGuidFromGO(go);
                if (og == null) { ObjectGuid.MaybeAttachObjectGuidAndRegister(go, go.name); og = ObjectGuid.GetObjectGuidFromGO(go); }
                if (og != null)
                {
                    try { og.MaybeRuntimeRegister(); } catch (Exception e) { MelonLoader.MelonLogger.Msg("[Piezas] MaybeRuntimeRegister: " + e.Message); }
                    try { id = og.Get(); } catch (Exception) { }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Msg("[Piezas] ObjectGuid: " + e.Message); }
            if (string.IsNullOrEmpty(id)) id = Guid(g);
            return id ?? "";
        }

        static string Guid(GearItem g)
        {
            try { return g != null ? ObjectGuid.GetGuidFromGameObject(g.gameObject) : ""; }
            catch (Exception) { return ""; }
        }

        // ------------------------------------------------------------------ per-game registry

        public static void Ensure(string slot)
        {
            if (slotLoaded == slot) return;
            slotLoaded = slot;
            registry.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] p = line.Split('|');
                    if (p.Length != 3 || p[0] != slot) continue;
                    int type;
                    if (int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out type) && type >= 0 && type < Parts.Count)
                        registry[p[1]] = type;
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] could not read the registry: " + e.Message); }
            MelonLoader.MelonLogger.Msg("[Parts] parts registered in this saved game: " + registry.Count);
        }

        static void SaveRegistry()
        {
            try
            {
                var lines = new List<string>();
                if (File.Exists(FilePath))
                    foreach (string l in File.ReadAllLines(FilePath))
                    {
                        string[] p = l.Split('|');
                        if (p.Length == 3 && (p[0] != slotLoaded || registry.Count == 0)) lines.Add(l);   // an empty registry never wipes what was saved
                    }
                foreach (KeyValuePair<string, int> kv in registry) lines.Add(slotLoaded + "|" + kv.Key + "|" + kv.Value);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] could not save the registry: " + e.Message); }
        }

        // ------------------------------------------------------------------ inventory

        // The name, description and icon are changed with patches (see Patches), there is no need to touch the item.
        static void NameItem(GearItem g, int type) { }

        static readonly Dictionary<int, int> perInstance = new Dictionary<int, int>();   // live item -> type
        public const string Marker = "WV_PART:";
        /// <summary>Old marker (Spanish), still read so parts already stored in saved games are recovered.</summary>
        public const string OldMarker = "CAMIONETA_PIEZA:";

        public const string Suf = "_CP";

        /// <summary>Name of the base item without the part mark (so the game recognizes it when saving / creating).</summary>
        public static string BaseOf(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            for (int i = 0; i < Parts.Count; i++) n = n.Replace(Suf + i.ToString(CultureInfo.InvariantCulture), "");
            return n;
        }

        /// <summary>Gives the item the part's own name (e.g. GEAR_Knife_CP5) so the game doesn't mix it up with real knives, hammers, etc.</summary>
        static void Rename(GearItem g, int type)
        {
            try
            {
                string n = g.gameObject.name;
                string fresh = BaseOf(n) + Suf + type.ToString(CultureInfo.InvariantCulture);
                if (n != fresh) g.gameObject.name = fresh;
            }
            catch (Exception) { }
        }

        /// <summary>The inventory remembers each item's name in its own list: it is updated so it matches.</summary>
        static void FixList()
        {
            try
            {
                Inventory inv = GameManager.GetInventoryComponent();
                if (inv == null) return;
                var items = inv.m_Items;
                if (items == null) return;
                for (int i = 0; i < items.Count; i++)
                {
                    GearItemObject w = items[i];
                    if (w == null) continue;
                    GearItem gi = w.m_GearItem;
                    int t;
                    if (gi == null || !IsPart(gi, out t)) continue;
                    string n = gi.gameObject.name;
                    if (w.m_GearItemName != n) w.m_GearItemName = n;
                }
            }
            catch (Exception) { }
        }

        static bool IsBase(string n)
        {
            foreach (string p in pool) if (n.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Is this item an engine part? Returns its type.</summary>
        public static bool IsPart(GearItem g, out int type)
        {
            type = -1;
            try
            {
                if (g == null) return false;
                GameObject go = g.gameObject;
                if (go == null) return false;
                int id = go.GetInstanceID();
                if (perInstance.TryGetValue(id, out type)) return true;
                string shortName = go.name;
                int ip = shortName.IndexOf(Suf, StringComparison.Ordinal);
                if (ip >= 0 && ip + Suf.Length < shortName.Length)
                {
                    int tn;
                    if (int.TryParse(shortName.Substring(ip + Suf.Length, 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out tn) && tn >= 0 && tn < Parts.Count)
                    {
                        type = tn; perInstance[id] = tn; return true;
                    }
                }
                type = -1;
                if (registry.Count == 0 || !IsBase(go.name)) return false;
                if (registry.TryGetValue(Guid(g), out type)) { perInstance[id] = type; return true; }
                type = -1;
                return false;
            }
            catch (Exception) { type = -1; return false; }
        }

        /// <summary>Called when saving: leaves the part's mark inside the data the game saves for the item.</summary>
        public static void OnSave(GearItem g, GearItemSaveDataProxy proxy)
        {
            int t;
            if (proxy == null || !IsPart(g, out t)) return;
            proxy.m_StackableItemSerialized = Marker + t.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Called when loading an item from the saved game: if it carries the mark, it becomes the part again.</summary>
        public static void OnLoad(GearItem g, GearItemSaveDataProxy proxy)
        {
            try
            {
                if (g == null || proxy == null) return;
                string m = proxy.m_StackableItemSerialized;
                if (string.IsNullOrEmpty(m)) return;
                string mk = m.StartsWith(Marker, StringComparison.Ordinal) ? Marker : (m.StartsWith(OldMarker, StringComparison.Ordinal) ? OldMarker : null);
                if (mk == null) return;
                int t;
                if (!int.TryParse(m.Substring(mk.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out t) || t < 0 || t >= Parts.Count) return;
                perInstance[g.gameObject.GetInstanceID()] = t;
                Rename(g, t);
                Model(g, t);
                MelonLoader.MelonLogger.Msg("[Parts] part recovered on load: " + Parts.Names[t]);
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] on load: " + e.Message); }
        }

        static readonly Texture2D[] icons = new Texture2D[Parts.Count];
        static readonly bool[] iconTried = new bool[Parts.Count];

        /// <summary>The part's inventory icon: a picture of its 3D model, taken once.</summary>
        public static Texture2D Icon(int type)
        {
            if (icons[type] != null) return icons[type];
            if (iconTried[type]) return null;
            iconTried[type] = true;
            GameObject model = null, camGO = null, l1 = null, l2 = null;
            RenderTexture rt = null;
            try
            {
                model = Hood.CreateObject(type);
                model.transform.position = new Vector3(type * 200f, -3000f - type * 200f, 0f);
                model.transform.rotation = Quaternion.Euler(24f, -38f, 0f);
                Bounds b = new Bounds(model.transform.position, Vector3.zero);
                bool first = true;
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                camGO = new GameObject("WV_IconCam");
                Camera cam = camGO.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(0.05f, b.extents.magnitude * 0.95f);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 20f;
                cam.transform.position = b.center + new Vector3(0f, 0f, -5f);
                cam.transform.rotation = Quaternion.identity;
                l1 = new GameObject("IconoLuz1");
                Light a = l1.AddComponent<Light>();
                a.type = LightType.Directional; a.intensity = 1.1f; a.color = Color.white;
                l1.transform.position = b.center;
                l1.transform.rotation = Quaternion.Euler(40f, -25f, 0f);
                l2 = new GameObject("IconoLuz2");
                Light c = l2.AddComponent<Light>();
                c.type = LightType.Directional; c.intensity = 0.55f; c.color = new Color(0.85f, 0.9f, 1f);
                l2.transform.position = b.center;
                l2.transform.rotation = Quaternion.Euler(-20f, 150f, 0f);

                rt = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(256, 256, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                cam.targetTexture = null;
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                icons[type] = tex;
                MelonLoader.MelonLogger.Msg("[Parts] icon of '" + Parts.Names[type] + "' generated");
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] could not generate the icon: " + e.Message); }
            finally
            {
                try { if (rt != null) RenderTexture.ReleaseTemporary(rt); } catch (Exception) { }
                // DestroyImmediate: with Destroy the model stayed alive until the end of the frame and showed up in the next part's picture (the icons looked overlapped)
                if (camGO != null) UnityEngine.Object.DestroyImmediate(camGO);
                if (l1 != null) UnityEngine.Object.DestroyImmediate(l1);
                if (l2 != null) UnityEngine.Object.DestroyImmediate(l2);
                if (model != null) UnityEngine.Object.DestroyImmediate(model);
            }
            return icons[type];
        }

        /// <summary>Name of the game object that acts as the part's body (to show it in the menu).</summary>
        public static string ObjectBase(int type) { return pool[type % pool.Length].Replace("GEAR_", ""); }

        /// <summary>Engine parts in the inventory, of that type (-1 = all).</summary>
        static List<GearItem> FromInventory(int type)
        {
            var res = new List<GearItem>();
            Inventory inv = GameManager.GetInventoryComponent();
            if (inv == null) return res;
            var items = inv.m_Items;
            if (items == null) return res;
            for (int i = 0; i < items.Count; i++)
            {
                GearItemObject w = items[i];
                if (w == null) continue;
                GearItem g = w.m_GearItem;
                int t;
                if (g != null && IsPart(g, out t) && (type < 0 || t == type)) res.Add(g);
            }
            return res;
        }

        public static int HowMany(int type)
        {
            try { return Active ? FromInventory(type).Count : 0; }
            catch (Exception) { return 0; }
        }

        public static int Total()
        {
            try { return Active ? FromInventory(-1).Count : 0; }
            catch (Exception) { return 0; }
        }

        /// <summary>Takes the part of that type with the best condition out of the inventory and returns its condition (-1 if there is none).</summary>
        public static float Take(int type)
        {
            try
            {
                Ensure(Persistence.Slot());
                GearItem best = null;
                foreach (GearItem g in FromInventory(type))
                    if (best == null || g.GetNormalizedCondition() > best.GetNormalizedCondition()) best = g;
                if (best == null) return -1f;
                float c = Mathf.Clamp01(best.GetNormalizedCondition());
                string guid = Guid(best);
                Inventory inv = GameManager.GetInventoryComponent();
                inv.DestroyGear(best);
                registry.Remove(guid);
                perInstance.Remove(best.gameObject.GetInstanceID());
                SaveRegistry();
                return c;
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] could not take the part out of the inventory: " + e.Message); return -1f; }
        }

        /// <summary>Creates the part as an inventory item. false if it couldn't be done (then the old pack is used).</summary>
        public static bool Give(int type, float condition)
        {
            try
            {
                if (!Active) return false;
                Ensure(Persistence.Slot());
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm == null) return false;
                GearItem g = null;
                string used = null;
                for (int k = 0; k < pool.Length && g == null; k++)
                {
                    string n = pool[(type + k) % pool.Length];
                    try { g = GearItem.InstantiateGearItem(n); }
                    catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] could not create '" + n + "': " + e.Message); }
                    if (g != null) used = n;
                }
                if (g == null)
                {
                    MelonLoader.MelonLogger.Warning("[Parts] no base item could be created.");
                    return false;
                }
                Rename(g, type);
                try { g.SetNormalizedHP(Mathf.Clamp01(condition), false); } catch (Exception) { }
                GearItem installed = pm.AddItemToPlayerInventory(g, false, true);
                if (installed == null) installed = g;
                string guid = CreateGuid(installed);
                if (string.IsNullOrEmpty(guid))
                {
                    MelonLoader.MelonLogger.Warning("[Parts] the game gave no identifier to '" + used + "' (item " + installed.gameObject.name + ").");
                    try { GameManager.GetInventoryComponent().DestroyGear(installed); } catch (Exception) { }
                    failures++;
                    return false;
                }
                failures = 0;
                registry[guid] = type;
                perInstance[installed.gameObject.GetInstanceID()] = type;
                Rename(installed, type);
                FixList();
                SaveRegistry();
                NameItem(installed, type);
                Model(installed, type);
                MelonLoader.MelonLogger.Msg("[Parts] " + Parts.Names[type] + " created on '" + used + "' (id " + guid + ")");
                return true;
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] could not create the part: " + e.Message); return false; }
        }

        static readonly Dictionary<string, Mesh> locals = new Dictionary<string, Mesh>();
        static readonly int[] notices = new int[Parts.Count];

        /// <summary>The part's mesh expressed in the space of each of the item's MeshFilters (so it doesn't end up small/rotated if the base item has its own scale or rotation).</summary>
        static Mesh MeshFor(int type, MeshFilter mf, GearItem g)
        {
            // the base item can have its own scale (hammer 0.27, knife 1.2...): it is compensated so the part comes out at its real size
            float rs = g.transform.lossyScale.x;
            if (rs < 0.01f) return null;
            rs = Mathf.Max(0.05f, Mathf.Round(rs * 20f) / 20f);   // in steps: while the game scales the item up/down, meshes are reused
            float insp = 1f;
            float f = Settings.SizeParts() / rs * insp;
            Matrix4x4 rel = mf.transform.worldToLocalMatrix * g.transform.localToWorldMatrix;
            if (rel.GetColumn(0).magnitude < 1e-6f || rel.GetColumn(1).magnitude < 1e-6f || rel.GetColumn(2).magnitude < 1e-6f) return null;
            string cacheKey = type + "|" + Mathf.Round(f * 1000f).ToString(CultureInfo.InvariantCulture) + "|";
            for (int i = 0; i < 16; i++) cacheKey += Mathf.Round(rel[i] * 1000f).ToString(CultureInfo.InvariantCulture) + ",";
            Mesh m;
            if (locals.TryGetValue(cacheKey, out m) && m != null) return m;
            if (locals.Count > 300) locals.Clear();
            Mesh src = meshes[type];
            m = new Mesh();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            Vector3[] v = src.vertices;
            Vector3[] nr = src.normals;
            Matrix4x4 nm = rel.inverse.transpose;
            for (int i = 0; i < v.Length; i++) v[i] = rel.MultiplyPoint3x4(v[i] * f);
            for (int i = 0; i < nr.Length; i++) nr[i] = nm.MultiplyVector(nr[i]).normalized;
            m.vertices = v;
            m.normals = nr;
            m.uv = src.uv;
            m.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) m.SetTriangles(src.GetTriangles(s), s);
            m.RecalculateBounds();
            m.name = "WV_Part" + type;
            locals[cacheKey] = m;
            return m;
        }

        static string V3(Vector3 v) { return "(" + v.x.ToString("F3", CultureInfo.InvariantCulture) + ", " + v.y.ToString("F3", CultureInfo.InvariantCulture) + ", " + v.z.ToString("F3", CultureInfo.InvariantCulture) + ")"; }
        static int noticesGen;
        static void Notice(string t) { if (noticesGen++ < 12) MelonLoader.MelonLogger.Msg("[Parts] " + t); }

        /// <summary>Does this model hang from another game object (another GearItem) that is inside this one?</summary>
        static bool OfOtherObject(Transform t, Transform root)
        {
            try
            {
                while (t != null && t.Pointer != root.Pointer)
                {
                    if (t.GetComponent<GearItem>() != null) return true;
                    t = t.parent;
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Changes the item's 3D model to the part's (the game item stays the same).</summary>
        public static void Model(GearItem g, int type)
        {
            Repair.Prepare(g, type);
            try
            {
                if (meshes[type] != null && Near(g)) return;      // the model is not touched while inspecting
                if (meshes[type] == null)
                {
                    Mesh m; Material[] ms;
                    Hood.BuildMesh(type, out m, out ms);
                    var arr = new Il2CppReferenceArray<Material>(ms.Length);
                    for (int i = 0; i < ms.Length; i++) arr[i] = ms[i];
                    meshes[type] = m;
                    mats[type] = arr;
                }
                var filters = g.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0) Notice("'" + g.gameObject.name + "' has no MeshFilter (another kind of model?): renderers = " + g.GetComponentsInChildren<Renderer>(true).Length);
                foreach (MeshFilter mf in filters)
                {
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr == null) { Notice("'" + mf.name + "' has no MeshRenderer"); continue; }
                    // only the models of THIS item (not those of another item hanging from it)
                    if (OfOtherObject(mf.transform, g.transform)) { Notice("model of '" + mf.name + "' ignored: it hangs from another object"); continue; }
                    Mesh fresh = MeshFor(type, mf, g);
                    if (fresh == null) { Notice("could not compute the mesh for " + g.gameObject.name + "' (object scale " + V3(g.transform.lossyScale) + ")"); continue; }
                    live[g.gameObject.GetInstanceID()] = g;
                    types[g.gameObject.GetInstanceID()] = type;
                    if (mf.sharedMesh != null && mf.sharedMesh.Pointer == fresh.Pointer) continue;
                    mf.sharedMesh = fresh;
                    mr.sharedMaterials = mats[type];
                    if (notices[type] < 3)
                    {
                        notices[type]++;
                        Vector3 e = mr.bounds.size;
                        MelonLoader.MelonLogger.Msg("[Parts] model of '" + Parts.Names[type] + "' placed on " + g.gameObject.name + ": object scale " + V3(g.transform.lossyScale) + ", mesh scale " + V3(mf.transform.lossyScale) + ", visible size " + V3(e));
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] could not change the model: " + e.Message); }
        }

        /// <summary>A complete set of new parts (70-100 %).</summary>
        public static int Generate()
        {
            int n = 0;
            for (int i = 0; i < Parts.Count; i++)
                if (Give(i, 0.7f + UnityEngine.Random.value * 0.3f)) n++;
            return n;
        }

        /// <summary>Is the part right up against the camera (the game brings it close like that to inspect it or hold it in hand)?</summary>
        static bool Near(GearItem g)
        {
            try
            {
                if (g == null) return false;
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm == null) return false;
                GearItem insp = pm.GearItemBeingInspected();
                return insp != null && insp.Pointer == g.Pointer;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Called by the game when it places / updates the item on the inspection screen: shrinks the part so it fits comfortably on screen.</summary>
        public static void FitInspection(PlayerManager pm)
        {
            try
            {
                if (!Active || pm == null) return;
                FitGear(pm.GearItemBeingInspected());
            }
            catch (Exception) { }
        }

        static Panel_Inventory_Examine examine;
        static bool examineSearched;
        public static void ResetExamine() { examine = null; examineSearched = false; }

        /// <summary>The inventory's examine / repair screen also shows the part in inspection space, but it doesn't go through PlayerManager: the panel is checked directly.</summary>
        static void FitExamine()
        {
            try
            {
                if (!Active) return;
                if (examine == null)
                {
                    // FindObjectOfType doesn't find inactive objects and returned null every frame (very heavy): it is searched only once, including inactive ones
                    if (examineSearched) return;
                    examineSearched = true;
                    var all = Resources.FindObjectsOfTypeAll<Panel_Inventory_Examine>();
                    if (all != null && all.Length > 0) examine = all[0];
                    if (examine == null) return;
                }
                GearItem g = examine.m_GearItem;
                if (g == null || !examine.gameObject.activeInHierarchy) return;
                Camera cam = Camera.main;
                if (cam != null && (cam.transform.position - g.transform.position).magnitude > 3f) return;     // the part is not on the examine screen
                FitGear(g);
            }
            catch (Exception) { examine = null; examineSearched = false; }
        }

        static void FitGear(GearItem g)
        {
            try
            {
                if (g == null) return;
                int type;
                if (!IsPart(g, out type)) return;
                float ext = 0f;
                bool first = true;
                Bounds b = new Bounds();
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null || !r.enabled) continue;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                if (first) return;
                ext = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (ext < 0.001f) return;
                float target = Settings.SizeInspection() * 0.75f;      // maximum size (in meters) seen when inspecting it
                if (ext <= target * 1.02f) return;
                float k = target / ext;
                g.transform.localScale = g.transform.localScale * k;
                if (inspNotices++ < 6) MelonLoader.MelonLogger.Msg("[Parts] inspection of '" + Parts.Names[type] + "': size " + ext.ToString("F2", CultureInfo.InvariantCulture) + " m -> " + target.ToString("F2", CultureInfo.InvariantCulture) + " m");
            }
            catch (Exception) { }
        }
        static int inspNotices;

        static readonly Dictionary<int, bool> nearLast = new Dictionary<int, bool>();
        static readonly Dictionary<int, GearItem> live = new Dictionary<int, GearItem>();
        static readonly Dictionary<int, int> types = new Dictionary<int, int>();
        static readonly Dictionary<int, float> scLast = new Dictionary<int, float>();

        /// <summary>Every frame: if the game changed a part's scale (when grabbing / inspecting / dropping it), the model is adjusted right away.</summary>
        static void Fast()
        {
            FitExamine();
            if (live.Count == 0) return;
            var list = new List<KeyValuePair<int, GearItem>>(live);
            foreach (KeyValuePair<int, GearItem> kv in list)
            {
                try
                {
                    GearItem g = kv.Value;
                    if (g == null || g.gameObject == null) { live.Remove(kv.Key); types.Remove(kv.Key); scLast.Remove(kv.Key); nearLast.Remove(kv.Key); continue; }
                    float rs = Mathf.Round(g.transform.lossyScale.x * 20f) / 20f;
                    float before; bool near = Near(g), cBefore;
                    bool sameSc = scLast.TryGetValue(kv.Key, out before) && Mathf.Abs(before - rs) < 0.001f;
                    bool sameNear = nearLast.TryGetValue(kv.Key, out cBefore) && cBefore == near;
                    if (sameSc && sameNear) continue;
                    scLast[kv.Key] = rs;
                    nearLast[kv.Key] = near;
                    int t;
                    if (types.TryGetValue(kv.Key, out t)) Model(g, t);
                }
                catch (Exception) { live.Remove(kv.Key); }
            }
        }

        static int lastCount = -1;

        /// <summary>Every so often it looks for the parts (in the inventory and in the world) and puts the model on them, because the game doesn't save those changes.</summary>
        public static void Sweep(float dt)
        {
            Fast();
            sweepT -= dt;
            if (sweepT > 0f) return;
            sweepT = 1.5f;
            try
            {
                if (!Active) return;
                int n = 0;
                foreach (GearItem g in Resources.FindObjectsOfTypeAll<GearItem>())
                {
                    if (g == null) continue;
                    GameObject go = g.gameObject;
                    if (go == null || !go.scene.IsValid()) continue;
                    if (!IsBase(go.name)) continue;
                    int t;
                    if (!IsPart(g, out t)) continue;
                    n++;
                    Rename(g, t);
                    Model(g, t);
                }
                if (n > 0) FixList();
                if (n != lastCount) { lastCount = n; MelonLoader.MelonLogger.Msg("[Parts] engine parts in the game: " + n + " (registered: " + registry.Count + ")"); }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Parts] " + e.Message); }
        }
    }
}
