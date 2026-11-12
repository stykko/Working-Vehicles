using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>A vehicle kept in the garage, or placed in the world after leaving it. Everything it had travels with it.</summary>
    internal class GarageEntry
    {
        public string slot;
        public string id;            // unique, stays the same for the whole life of the vehicle
        public string family;        // model ("CarSedan", "CarTruck"...), used to find the model to build it from
        public string label;         // what the menu shows
        public string scene;         // region where it is standing (when it is not stored)
        public bool stored = true;   // true = in the garage (not in the world)
        public string cont = "";    // contents of the glovebox / trunk (game containers), serialized by the game
        public Data d = new Data();  // position / rotation (when placed) and all the stats: steering, snow, battery, parts, fuel
    }

    /// <summary>
    /// The garage: lets you store a vehicle (it disappears from the world) and bring it back anywhere, in any region,
    /// with the same parts, battery, fuel, snow and steering wheel it had.
    /// Written to UserData\WorkingVehicles_garage.txt when the game saves (like the rest of the vehicle data).
    /// </summary>
    internal static class Garage
    {
        static readonly List<GarageEntry> list = new List<GarageEntry>();
        static bool loaded;
        static string FilePath { get { return Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "UserData"), "WorkingVehicles_garage.txt"); } }

        public static void Reload() { list.Clear(); loaded = false; Load(); }

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
                    if (p.Length != 15 && p.Length != 16) continue;
                    string[] o = p[6].Split(';'), q = p[7].Split(';');
                    if (o.Length != 3 || q.Length != 4) continue;
                    var e = new GarageEntry { slot = p[0], id = p[1], family = p[2], stored = p[3] == "1", scene = p[4], label = p[5] };
                    e.d.pos = new Vector3(Persistence.F(o[0]), Persistence.F(o[1]), Persistence.F(o[2]));
                    e.d.rot = new Quaternion(Persistence.F(q[0]), Persistence.F(q[1]), Persistence.F(q[2]), Persistence.F(q[3]));
                    e.d.steering = Persistence.F(p[8]);
                    e.d.level = Mathf.Clamp01(Persistence.F(p[9]));
                    e.d.hour = Persistence.F(p[10]);
                    e.d.battery = p[11] == "1";
                    e.d.batteryCondition = Mathf.Clamp01(Persistence.F(p[12]));
                    e.d.parts = Persistence.ReadParts(p[13]);
                    e.d.fuel = Mathf.Clamp01(Persistence.F(p[14]));
                    e.cont = p.Length == 16 ? p[15] : "";
                    list.Add(e);
                }
            }
            catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Garage] could not read the garage: " + ex.Message); }
        }

        /// <summary>Writes the garage to the file (only when the game saves).</summary>
        public static void Confirm()
        {
            Load();
            try
            {
                var l = new List<string>();
                foreach (GarageEntry e in list)
                {
                    l.Add(e.slot + "|" + e.id + "|" + e.family + "|" + (e.stored ? "1" : "0") + "|" + (e.scene ?? "") + "|" + (e.label ?? "").Replace('|', '/')
                        + "|" + Persistence.S(e.d.pos.x) + ";" + Persistence.S(e.d.pos.y) + ";" + Persistence.S(e.d.pos.z)
                        + "|" + Persistence.S(e.d.rot.x) + ";" + Persistence.S(e.d.rot.y) + ";" + Persistence.S(e.d.rot.z) + ";" + Persistence.S(e.d.rot.w)
                        + "|" + Persistence.S(e.d.steering) + "|" + Persistence.S(e.d.level) + "|" + Persistence.S(e.d.hour)
                        + "|" + (e.d.battery ? "1" : "0") + "|" + Persistence.S(e.d.batteryCondition) + "|" + Persistence.WriteParts(e.d.parts) + "|" + Persistence.S(e.d.fuel) + "|" + (e.cont ?? ""));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, l.ToArray());
            }
            catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Garage] could not save the garage: " + ex.Message); }
        }

        public static List<GarageEntry> All(string slot)
        {
            Load();
            var r = new List<GarageEntry>();
            foreach (GarageEntry e in list) if (e.slot == slot) r.Add(e);
            return r;
        }

        public static GarageEntry Find(string slot, string id)
        {
            Load();
            foreach (GarageEntry e in list) if (e.slot == slot && e.id == id) return e;
            return null;
        }

        public static void Add(GarageEntry e) { Load(); list.Add(e); }

        // ------------------------------------------------------------------ models (to build the vehicle again in another region)

        /// <summary>"ENCAMPMENT_CarSedan_town4(Clone)" -> "CarSedan_town4".</summary>
        public static string Family(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string n = name.Replace("(Clone)", "").Trim();
            if (n.StartsWith("ENCAMPMENT_")) n = n.Substring("ENCAMPMENT_".Length);
            return n;     // the exact variant ("CarSedan_town4"): each one has its own color
        }

        public static string Display(string family)
        {
            string f = family.ToLowerInvariant();
            if (f.Contains("sedan")) return L.T("Sedán", "Sedan");
            if (f.Contains("truck")) return L.T("Camioneta", "Truck");
            return family;
        }


        // ------------------------------------------------------------------ glovebox / trunk (game containers)
        // The copy of a vehicle comes with new, uninspected containers (the loot would be rolled again and what the player left
        // inside lost). So what the game itself serializes for each container is saved and given back. Done by reflection
        // so a different game version cannot break the mod; the log says what worked.

        const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static bool dumped;

        static object GetM(object o, params string[] names)
        {
            Type t = o.GetType();
            foreach (string n in names)
            {
                try
                {
                    FieldInfo f = t.GetField(n, BF);
                    if (f != null) return f.GetValue(o);
                    PropertyInfo pr = t.GetProperty(n, BF);
                    if (pr != null && pr.CanRead) return pr.GetValue(o, null);
                }
                catch (Exception) { }
            }
            return null;
        }

        static bool SetM(object o, bool v, params string[] names)
        {
            Type t = o.GetType();
            foreach (string n in names)
            {
                try
                {
                    FieldInfo f = t.GetField(n, BF);
                    if (f != null && f.FieldType == typeof(bool)) { f.SetValue(o, v); return true; }
                    PropertyInfo pr = t.GetProperty(n, BF);
                    if (pr != null && pr.CanWrite && pr.PropertyType == typeof(bool)) { pr.SetValue(o, v, null); return true; }
                }
                catch (Exception) { }
            }
            return false;
        }

        static int CountOf(string text, string what)
        {
            int n = 0, i = 0;
            while ((i = text.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; }
            return n;
        }

        static void CallNoArgs(object o, string name)
        {
            MethodInfo m = o.GetType().GetMethod(name, BF, null, Type.EmptyTypes, null);
            if (m != null) m.Invoke(o, null);
        }

        static void DumpOnce(Container c)
        {
            if (dumped) return;
            dumped = true;
            try
            {
                var sb = new StringBuilder("[Garage] Container API:");
                foreach (MethodInfo m in c.GetType().GetMethods(BF | BindingFlags.DeclaredOnly))
                    if (!m.IsSpecialName) sb.Append(' ').Append(m.Name).Append('/').Append(m.GetParameters().Length);
                foreach (FieldInfo f in c.GetType().GetFields(BF | BindingFlags.DeclaredOnly)) sb.Append(" [").Append(f.Name).Append(']');
                MelonLoader.MelonLogger.Msg(sb.ToString());
            }
            catch (Exception) { }
        }

        /// <summary>Everything the game keeps in the containers of this vehicle (glovebox, trunk), ready to save in the file.</summary>
        public static string CaptureContainers(Transform root, bool log = true)
        {
            var parts = new List<string>();
            try
            {
                int i = 0;
                foreach (Container c in root.GetComponentsInChildren<Container>(true))
                {
                    int idx = i++;
                    if (c == null) continue;
                    if (log) DumpOnce(c);
                    object insp = GetM(c, "m_Inspected", "Inspected", "m_IsInspected");
                    string ser = null;
                    try
                    {
                        MethodInfo m = c.GetType().GetMethod("Serialize", BF, null, Type.EmptyTypes, null);
                        if (m != null && m.ReturnType == typeof(string)) ser = (string)m.Invoke(c, null);
                    }
                    catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Garage] Container.Serialize failed: " + ex.Message); }
                    int cpBefore = 0, mk = 0;
                    if (ser != null)
                    {
                        // the engine parts of the mod are renamed items (GEAR_..._CP3): the game cannot rebuild them by that name,
                        // so the mod's mark is removed (same as the game's own saves) and the part comes back from the mark inside the data
                        cpBefore = CountOf(ser, PartItems.Suf);
                        mk = CountOf(ser, PartItems.Marker);
                        ser = PartItems.BaseOf(ser);
                    }
                    bool inspected = insp is bool && (bool)insp;
                    parts.Add(idx + "," + (inspected ? "1" : "0") + "," + (ser == null ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes(ser))));
                    if (log) MelonLoader.MelonLogger.Msg("[Garage] container #" + idx + " '" + c.name + "': inspected=" + inspected + ", " + (ser == null ? "no data (the game has no Serialize())" : ser.Length + " chars saved") + (log && (cpBefore > 0 || mk > 0) ? "; engine parts inside: names " + cpBefore + ", marks " + mk : ""));
                }
            }
            catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Garage] could not read the containers: " + ex.Message); }
            return string.Join(";", parts.ToArray());
        }

        /// <summary>Puts back the contents of the containers (and marks them inspected, so the loot is not rolled again).</summary>
        public static void RestoreContainers(Transform root, string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var cs = new List<Container>();
                foreach (Container c in root.GetComponentsInChildren<Container>(true)) cs.Add(c);
                foreach (string part in data.Split(';'))
                {
                    string[] q = part.Split(',');
                    if (q.Length != 3) continue;
                    int idx;
                    if (!int.TryParse(q[0], out idx) || idx < 0 || idx >= cs.Count || cs[idx] == null) continue;
                    Container c = cs[idx];
                    bool ok = false;
                    if (q[2].Length > 0)
                    {
                        try
                        {
                            string ser = PartItems.BaseOf(Encoding.UTF8.GetString(Convert.FromBase64String(q[2])));
                            MethodInfo m = null;
                            foreach (MethodInfo mm in c.GetType().GetMethods(BF))
                                if (mm.Name == "Deserialize" && mm.GetParameters().Length == 2 && mm.GetParameters()[0].ParameterType == typeof(string)) { m = mm; break; }
                            if (m != null)
                            {
                                ParameterInfo[] pi = m.GetParameters();
                                MelonLoader.MelonLogger.Msg("[Garage] Deserialize(" + pi[0].ParameterType.Name + " " + pi[0].Name + ", " + pi[1].ParameterType.Name + " " + pi[1].Name + ")");
                                try { CallNoArgs(c, "DestroyAllGear"); } catch (Exception) { }
                                // second parameter: unknown to us (a flag): tried as false, then true
                                foreach (bool flag in new[] { false, true })
                                {
                                    try
                                    {
                                        object second = pi[1].ParameterType == typeof(bool) ? (object)flag : (pi[1].ParameterType.IsValueType ? Activator.CreateInstance(pi[1].ParameterType) : null);
                                        m.Invoke(c, new object[] { ser, second });
                                        ok = true;
                                        break;
                                    }
                                    catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Garage] Deserialize(flag " + flag + ") failed: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message)); }
                                    if (pi[1].ParameterType != typeof(bool)) break;
                                }
                            }
                            else MelonLoader.MelonLogger.Warning("[Garage] this game has no Container.Deserialize(string, x)");
                        }
                        catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Garage] Container.Deserialize failed: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message)); }
                    }
                    if (q[1] == "1" || ok) { SetM(c, true, "m_Inspected", "Inspected", "m_IsInspected"); try { CallNoArgs(c, "MarkAsInspected"); } catch (Exception) { } }
                    MelonLoader.MelonLogger.Msg("[Garage] container #" + idx + " '" + c.name + "': " + (ok ? "contents restored" : q[1] == "1" ? "marked inspected (no data to restore)" : "left as is"));
                }
            }
            catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Garage] could not restore the containers: " + ex.Message); }
        }

        static GameObject container;
        static readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();

        static void EnsureContainer()
        {
            if (container != null) return;
            container = new GameObject("WV_Garage");
            container.SetActive(false);                       // everything inside stays asleep (no Awake / OnEnable)
            UnityEngine.Object.DontDestroyOnLoad(container);  // and survives the changes of region
        }

        static void StripMod(Transform root)
        {
            var kill = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == root) continue;
                if (t.name.StartsWith("WV_") || t.name.StartsWith("Mod_Pieza_")) kill.Add(t.gameObject);
            }
            foreach (GameObject g in kill) if (g != null) UnityEngine.Object.Destroy(g);
        }

        /// <summary>Keeps a sleeping copy of this vehicle (model) so it can be built again in any region.</summary>
        public static void CacheTemplate(Transform r)
        {
            try
            {
                if (r == null) return;
                string fam = Family(r.name);
                GameObject ex;
                if (templates.TryGetValue(fam, out ex) && ex != null) return;
                EnsureContainer();
                GameObject c = UnityEngine.Object.Instantiate(r.gameObject, container.transform);
                c.name = r.name.Replace("(Clone)", "");
                StripMod(c.transform);
                templates[fam] = c;
                MelonLoader.MelonLogger.Msg("[Garage] model of '" + fam + "' kept to build it in other regions.");
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Garage] could not keep the model of '" + (r != null ? r.name : "?") + "': " + e.Message); }
        }

        public static bool HasTemplate(string family)
        {
            GameObject g;
            return templates.TryGetValue(family, out g) && g != null;
        }

        /// <summary>The model for this family: the one kept, or (if none) one found among the vehicles/models the game has loaded.</summary>
        public static GameObject TemplateFor(string family, Func<VehicleDoor, Transform> rootOf)
        {
            GameObject g;
            if (templates.TryGetValue(family, out g) && g != null) return g;
            foreach (KeyValuePair<string, GameObject> kv in templates)      // saved by an older version (no variant): any of that model
                if (kv.Value != null && family.IndexOf('_') < 0 && kv.Key.StartsWith(family + "_") && kv.Key.IndexOf("Damaged", StringComparison.Ordinal) < 0) return kv.Value;
            try
            {
                foreach (VehicleDoor d in Resources.FindObjectsOfTypeAll<VehicleDoor>())
                {
                    if (d == null) continue;
                    Transform r = rootOf(d);
                    if (r == null || Family(r.name) != family) continue;
                    CacheTemplate(r);
                    if (templates.TryGetValue(family, out g) && g != null) return g;
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Garage] looking for a model of '" + family + "': " + e.Message); }
            return null;
        }
    }

    public partial class Mod
    {
        // garage menu (F3)
        bool garageMenu;
        int selGarage;
        // confirmation to store a vehicle (from the engine menu)
        bool confirmStore;
        int selConfirm = 1;
        float garageT, garageWorldT;
        readonly Dictionary<string, Transform> live = new Dictionary<string, Transform>();

        static readonly string[] optionsConfirm = { "Sí, guardar el vehículo", "No, cancelar" };

        // ------------------------------------------------------------------ every frame (on foot)

        void GarageTick(float dt)
        {
            garageT += dt;
            garageWorldT -= dt;
            if (garageT > 5f * Settings.ScanScale()) { garageT = 0f; SyncLive(); }
            if (garageWorldT <= 0f) { garageWorldT = 2f * Settings.ScanScale(); PlaceStoredInWorld(); }

            if (confirmStore) return;                                   // the confirmation has priority (handled in HandleHood)
            if (Input.GetKeyDown(Settings.KeyGarage()) && !hoodMenu) { garageMenu = !garageMenu; selGarage = 0; return; }
            if (!garageMenu) return;

            List<GarageEntry> items = Stored();
            if (Input.GetKeyDown(KeyCode.Backspace)) { garageMenu = false; return; }
            int n = items.Count + 1;                                   // + "close"
            if (Input.GetKeyDown(KeyCode.DownArrow)) selGarage = (selGarage + 1) % n;
            if (Input.GetKeyDown(KeyCode.UpArrow)) selGarage = (selGarage + n - 1) % n;
            if (selGarage >= n) selGarage = 0;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (selGarage >= items.Count) { garageMenu = false; return; }
                if (SpawnFromGarage(items[selGarage])) garageMenu = false;
            }
        }

        List<GarageEntry> Stored()
        {
            var r = new List<GarageEntry>();
            foreach (GarageEntry e in Garage.All(Persistence.Slot())) if (e.stored) r.Add(e);
            return r;
        }

        // ------------------------------------------------------------------ store

        /// <summary>Called from the engine menu once confirmed.</summary>
        void StoreVehicle(Veh v)
        {
            try
            {
                if (v == null || v.root == null) return;
                string slot = Persistence.Slot();
                Transform r = v.root;
                string fam = Garage.Family(r.name);
                Garage.CacheTemplate(r);     // (it is kept already if the vehicle was seen before; otherwise now)

                GarageEntry e = v.gid != null ? Garage.Find(slot, v.gid) : null;
                if (e == null)
                {
                    e = new GarageEntry { slot = slot, id = Guid.NewGuid().ToString("N").Substring(0, 12), family = fam };
                    Garage.Add(e);
                }
                e.stored = true;
                e.scene = Scene();
                e.label = Garage.Display(fam) + " (" + Scene() + ")";
                FillEntry(e, v);
                e.cont = Garage.CaptureContainers(r);
                e.d.isRunning = false;

                // original vehicle of the game: stays hidden in its region, for good
                if (v.gid == null)
                    Persistence.Save(slot, new Data { orig = v.orig, pos = r.position, rot = r.rotation, steering = v.steering, isRunning = false, level = v.level, hour = v.hour, battery = v.battery, batteryCondition = v.batteryCondition, parts = v.parts, fuel = v.fuel, stored = true });

                if (v.dashboard != null) v.dashboard.Destroy();
                vehs.Remove(r.GetInstanceID());
                live.Remove(e.id);
                hoodMenu = false; hoodVeh = null; confirmStore = false;
                if (v.gid == null) r.gameObject.SetActive(false);
                else UnityEngine.Object.Destroy(r.gameObject);

                Notice(L.T("Vehículo guardado en el garaje (", "Vehicle stored in the garage (") + Settings.KeyGarage() + ")");
                LoggerInstance.Msg("[Garage] stored '" + e.label + "' (" + e.id + "): fuel " + Mathf.RoundToInt(e.d.fuel * 100f) + " %, battery " + (e.d.battery ? Parts.Pct(e.d.batteryCondition) : "none"));
            }
            catch (Exception ex) { LoggerInstance.Warning("[Garage] could not store the vehicle: " + ex); confirmStore = false; }
        }

        static void FillEntry(GarageEntry e, Veh v)
        {
            e.d.pos = v.root != null ? v.root.position : e.d.pos;
            e.d.rot = v.root != null ? v.root.rotation : e.d.rot;
            e.d.steering = v.steering;
            e.d.level = v.level;
            e.d.hour = v.hour;
            e.d.battery = v.battery;
            e.d.batteryCondition = v.batteryCondition;
            e.d.parts = v.parts != null ? (float[])v.parts.Clone() : null;
            e.d.fuel = v.fuel;
            e.d.isRunning = v.isRunning;
        }

        /// <summary>Updates the garage data of a vehicle that was brought out of the garage (called when the vehicle is saved).</summary>
        void SaveGarageVehicle(Veh v)
        {
            GarageEntry e = Garage.Find(Persistence.Slot(), v.gid);
            if (e == null) return;
            FillEntry(e, v);
            e.cont = Garage.CaptureContainers(v.root, false);
            e.stored = false;
            e.scene = Scene();
        }

        void SyncLive()
        {
            foreach (Veh v in vehs.Values)
                if (v.gid != null && v.root != null) SaveGarageVehicle(v);
        }

        // ------------------------------------------------------------------ bring out

        bool SpawnFromGarage(GarageEntry e)
        {
            Camera cam = GameManager.GetMainCamera();
            if (cam == null) return false;
            Vector3 f = cam.transform.forward; f.y = 0f;
            f = f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized;
            Vector3 p = cam.transform.position + f * 6.5f;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, cam.transform.position.y + 3f, p.z), Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.05f;
            else p.y = cam.transform.position.y - 1.6f;

            Vector3 right = cam.transform.right; right.y = 0f;
            right = right.sqrMagnitude < 0.01f ? Vector3.right : right.normalized;
            Transform r = Build(e, p, right);
            if (r == null) return false;
            Notice(L.T("Vehículo sacado del garaje", "Vehicle taken out of the garage"));
            return true;
        }

        /// <summary>Builds the vehicle from its model and gives it back everything it had. 'frontDir' = where its front should point.</summary>
        Transform Build(GarageEntry e, Vector3 pos, Vector3 frontDir)
        {
            try
            {
                GameObject model = Garage.TemplateFor(e.family, RootOf);
                if (model == null)
                {
                    Notice(L.T("Falta el modelo: acercate a un vehículo del mismo tipo (", "Model not available: get near a vehicle of the same type (") + Garage.Display(e.family) + ")");
                    return null;
                }
                GameObject go = UnityEngine.Object.Instantiate(model, pos, Quaternion.identity);
                go.name = model.name;                         // keeps the "ENCAMPMENT_..." name the mod relies on
                go.transform.SetParent(null);
                go.SetActive(true);
                Transform r = go.transform;

                // turn it so its front points where we want
                VehicleDoor d = r.GetComponentInChildren<VehicleDoor>(true);
                Vector3 eye;
                Vector3 front = Front(r, d, out eye); front.y = 0f;
                if (front.sqrMagnitude > 0.01f && frontDir.sqrMagnitude > 0.01f)
                {
                    float ang = Vector3.SignedAngle(front.normalized, frontDir.normalized, Vector3.up);
                    r.rotation = Quaternion.AngleAxis(ang, Vector3.up) * r.rotation;
                }
                r.position = pos;

                Veh v = GetVeh(r);
                v.gid = e.id;
                v.orig = r.position;
                v.isRunning = false;
                v.steering = e.d.steering;
                v.level = e.d.level; v.levelSaved = v.level; v.hour = e.d.hour;
                v.battery = e.d.battery;
                v.batteryCondition = e.d.batteryCondition;
                v.parts = e.d.parts != null ? (float[])e.d.parts.Clone() : Parts.Initial(v.orig);
                v.fuel = e.d.fuel;
                seen.Add(r.GetInstanceID());
                live[e.id] = r;

                if (v.level < 0.999f) Snow.Apply(r, v.level);
                if (Mathf.Abs(v.steering) > 0.5f) PrepareBody(v, d);
                Garage.RestoreContainers(r, e.cont);

                e.stored = false;
                e.scene = Scene();
                FillEntry(e, v);
                LoggerInstance.Msg("[Garage] '" + e.label + "' (" + e.id + ") brought out at " + pos + " in '" + Scene() + "': fuel " + Mathf.RoundToInt(e.d.fuel * 100f) + " %");
                return r;
            }
            catch (Exception ex) { LoggerInstance.Warning("[Garage] could not bring the vehicle out: " + ex); return null; }
        }

        /// <summary>Vehicles that were left standing in this region reappear (the game does not know them) when the region loads.</summary>
        void PlaceStoredInWorld()
        {
            string sc = Scene();
            foreach (GarageEntry e in Garage.All(Persistence.Slot()))
            {
                if (e.stored || e.scene != sc) continue;
                Transform alive;
                if (live.TryGetValue(e.id, out alive) && alive != null) continue;
                if (!Garage.HasTemplate(e.family) && Garage.TemplateFor(e.family, RootOf) == null) continue;   // it will be tried again
                Vector3 p0 = e.d.pos; Quaternion q0 = e.d.rot;
                // same position and rotation as it was left: restored exactly after building it
                Transform r = Build(e, p0, q0 * Vector3.forward);
                if (r != null) { r.SetPositionAndRotation(p0, q0); e.d.pos = p0; e.d.rot = q0; }
            }
        }

        // ------------------------------------------------------------------ drawing

        void DrawGarage(float W, float H)
        {
            float e = Ui.Sc;
            if (confirmStore && hoodVeh != null)
            {
                string name = Garage.Display(Garage.Family(hoodVeh.root != null ? hoodVeh.root.name : ""));
                float[] hh = { 50f * e, 50f * e };
                float hp = 82f * e + hh[0] + hh[1] + 56f * e + 14f * e + 30f * e;
                Rect b = new Rect(W * 0.5f - 270f * e, H * 0.5f - hp * 0.5f, 540f * e, hp);
                Rect u = Ui.Panel(b, L.T("Guardar vehículo", "Store vehicle"), name + L.T(": desaparece del mundo", ": leaves the world"), Ui.Yellow);
                float y = u.y;
                Ui.Text(new Rect(u.x + 22f * e, y, u.width - 44f * e, 28f * e), L.T("Se guardan piezas, batería y combustible. Lo sacás con ", "Parts, battery and fuel are kept. Take it out with ") + Settings.KeyGarage(), 15f, Ui.Gray, TextAnchor.MiddleLeft, false);
                y += 30f * e;
                for (int i = 0; i < 2; i++)
                {
                    Ui.Row(new Rect(u.x, y, u.width, hh[i]), i == 0 ? L.T("Sí, guardar el vehículo", "Yes, store the vehicle") : L.T("No, cancelar", "No, cancel"), "", Ui.Gray, i == selConfirm, -1f, Ui.Green);
                    y += hh[i];
                }
                Ui.Footer(b, L.T("Flechas", "Arrows"), L.T("Mover", "Move"), "Enter", L.T("Elegir", "Select"), "Backspace", L.T("Cancelar", "Cancel"));
                return;
            }

            if (!garageMenu) return;
            List<GarageEntry> items = Stored();
            int shown = Mathf.Min(items.Count, 8);
            int first = Mathf.Clamp(selGarage - 7, 0, Mathf.Max(0, items.Count - shown));
            float rowH = 58f * e;
            float hPanel = 82f * e + (shown + 1) * rowH + 56f * e + 14f * e + (items.Count == 0 ? 26f * e : 0f);
            Rect box = new Rect(W * 0.5f - 290f * e, H * 0.5f - hPanel * 0.5f, 580f * e, hPanel);
            string sub = items.Count == 0 ? L.T("No hay vehículos guardados", "No stored vehicles") : items.Count + (items.Count == 1 ? L.T(" vehículo guardado", " stored vehicle") : L.T(" vehículos guardados", " stored vehicles"));
            Rect uu = Ui.Panel(box, L.T("Garaje", "Garage"), sub, Ui.Accent);
            float yy = uu.y;
            for (int i = first; i < first + shown; i++)
            {
                GarageEntry it = items[i];
                float f = it.d.fuel;
                Color cf = f > 0.25f ? Ui.Green : f > 0.1f ? Ui.Yellow : Ui.Red;
                Ui.Row(new Rect(uu.x, yy, uu.width, rowH), it.label, Mathf.RoundToInt(f * DataTank.Capacity) + " L", cf, i == selGarage, f, cf);
                yy += rowH;
            }
            Ui.Row(new Rect(uu.x, yy, uu.width, rowH), L.T("Cerrar", "Close"), "", Ui.Gray, selGarage >= items.Count, -1f, Ui.Green);
            yy += rowH;
            if (items.Count > 0 && selGarage < items.Count && !Garage.HasTemplate(items[selGarage].family))
                Ui.Text(new Rect(uu.x + 22f * e, yy + 2f * e, uu.width - 44f * e, 24f * e), L.T("Modelo no cargado todavía: si no sale, acercate a un vehículo igual", "Model not loaded yet: if it does not appear, get near a vehicle of the same type"), 14f, Ui.Yellow, TextAnchor.MiddleLeft, false);
            Ui.Footer(box, L.T("Flechas", "Arrows"), L.T("Mover", "Move"), "Enter", L.T("Sacar", "Take out"), Settings.KeyGarage().ToString(), L.T("Cerrar", "Close"));
        }

        /// <summary>Handles the confirmation keys. Returns true while the confirmation is open.</summary>
        bool HandleConfirmStore()
        {
            if (!confirmStore) return false;
            if (hoodVeh == null || hoodVeh.root == null) { confirmStore = false; return false; }
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(Settings.KeyHood())) { confirmStore = false; return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow)) selConfirm = 1 - selConfirm;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (selConfirm == 0) StoreVehicle(hoodVeh);
                else confirmStore = false;
            }
            return true;
        }
    }
}
