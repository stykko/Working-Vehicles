using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Il2Cpp;
using Il2CppTLD.ModularElectrolizer;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// Working truck dashboard: three gauges (left large RPM, middle small fuel, right large speedometer)
    /// and two buttons (lights, air conditioning). The game's dashboard is part of the interior mesh, with no loose parts:
    /// that's why custom faces and needles are placed on top, in the exact spot (calculated automatically and adjustable with the dashboard adjustment key).
    /// </summary>
    internal class Dashboard
    {
        public const int Rpm = 0, Fuel = 1, Vel = 2, Lights = 3, Ac = 4;
        static readonly string[] nameList =
        {
            "indicador IZQUIERDO (RPM, el grande)", "indicador del MEDIO (combustible, el chico)", "indicador DERECHO (velocímetro, el grande)",
            "botón de las LUCES", "botón del AIRE ACONDICIONADO"
        };
        static readonly string[] nameListEn =
        {
            "LEFT gauge (RPM, the big one)", "MIDDLE gauge (fuel, the small one)", "RIGHT gauge (speedometer, the big one)",
            "LIGHTS button", "A/C button"
        };

        struct Frame { public Vector3 p, n; public bool ok; }

        Transform root;
        Vector3 fwdL;
        GameObject group;
        readonly Frame[] frames = new Frame[5];
        readonly Transform[] needles = new Transform[3];
        readonly Renderer[] levers = new Renderer[2];
        readonly Transform[] leverT = new Transform[2];
        readonly Renderer[] leds = new Renderer[2];
        readonly List<Light> headlights = new List<Light>();
        readonly List<Light> rear = new List<Light>();
        // the game's original lights (the same ones that turn on with the aurora)
        readonly List<AuroraLightingSimple> elecSimple = new List<AuroraLightingSimple>();
        readonly List<AuroraModularElectrolizer> elecModular = new List<AuroraModularElectrolizer>();
        readonly List<Transform> elecObjects = new List<Transform>();
        bool elecOk;
        float checkLights = -1f;
        bool childrenManual;

        public bool lights, ac;
        public bool inAdjust;
        int step;
        readonly Frame[] fresh = new Frame[5];

        float rpmM, velM, fuelM = 0.7f, rpmNoise;
        public float fuel = 0.7f;          // 0..1 (used later, when there is fuel)
        int hover = -1;
        AudioSource srcAc, srcClick;
        float volAc;

        static AudioClip clipAc, clipClick;
        static Texture2D[] faces = new Texture2D[3];
        static Shader shWithoutLight, shColor;
        static bool shSearched;
        static readonly Dictionary<int, Material> matsColor = new Dictionary<int, Material>();
        static readonly Material[] matsFace = new Material[3];
        static readonly Material[] matsFaceLight = new Material[3];
        readonly Renderer[] facesR = new Renderer[3];
        readonly Renderer[] armsR = new Renderer[3];
        readonly Color[] colNeedle = new Color[3];
        bool lit;

        // ------------------------------------------------------------------ creation

        public static Dashboard Create(Transform root, Vector3 fwdLocal, Vector3 eyeWorld)
        {
            try
            {
                var t = new Dashboard { root = root, fwdL = fwdLocal.sqrMagnitude > 0.001f ? fwdLocal.normalized : Vector3.forward };
                t.Prepare(root.InverseTransformPoint(eyeWorld));
                return t;
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Warning("[Dashboard] could not build the dashboard: " + e);
                return null;
            }
        }

        void Prepare(Vector3 eyeL)
        {
            if (!Load() && !LockedList())
            {
                Values(eyeL);
                MelonLoader.MelonLogger.Msg("[Dashboard] positions calculated automatically (if they don't match the gauges, use the dashboard adjustment key)");
            }
            else MelonLoader.MelonLogger.Msg("[Dashboard] positions loaded from the adjustment file");
            Build();
            CreateHeadlights();
        }

        // ---- interior geometry (to find the dashboard with rays)
        class Geo3
        {
            public Vector3[] v;
            public int[] t;
        }
        static readonly Dictionary<int, Geo3> cacheGeo = new Dictionary<int, Geo3>();

        static Geo3 Geometry(Transform root)
        {
            Geo3 g;
            int id = root.GetInstanceID();
            if (cacheGeo.TryGetValue(id, out g)) return g;
            var vs = new List<Vector3>();
            var ts = new List<int>();
            var seen = new HashSet<string>();
            try
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (!n.Contains("interior") || n.Contains("door") || n.Contains("lod1") || n.Contains("lod2")) continue;
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                    if (!seen.Add(mf.sharedMesh.name)) continue;
                    Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    Vector3[] v = mf.sharedMesh.vertices;
                    int baseIx = vs.Count;
                    for (int i = 0; i < v.Length; i++) vs.Add(m.MultiplyPoint3x4(v[i]));
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    {
                        int[] tr = mf.sharedMesh.GetTriangles(s);
                        for (int i = 0; i < tr.Length; i++) ts.Add(tr[i] + baseIx);
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Dashboard] interior geometry: " + e.Message); }
            g = new Geo3 { v = vs.ToArray(), t = ts.ToArray() };
            cacheGeo[id] = g;
            MelonLoader.MelonLogger.Msg("[Dashboard] interior geometry: " + g.v.Length + " vertices, " + (g.t.Length / 3) + " triangles");
            return g;
        }

        static bool Ray(Geo3 g, Vector3 o, Vector3 d, float max, out Vector3 point, out Vector3 normal)
        {
            point = o; normal = -d;
            float best = max;
            bool exists = false;
            Vector3[] v = g.v; int[] t = g.t;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 p = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, p);
                if (det > -1e-8f && det < 1e-8f) continue;
                float inv = 1f / det;
                Vector3 s = o - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(d, q) * inv;
                if (w < 0f || u + w > 1f) continue;
                float tt = Vector3.Dot(e2, q) * inv;
                if (tt < 0.05f || tt >= best) continue;
                best = tt; exists = true;
                Vector3 nn = Vector3.Cross(e1, e2).normalized;
                if (Vector3.Dot(nn, d) > 0f) nn = -nn;
                normal = nn;
            }
            if (exists) point = o + d * best;
            return exists;
        }

        // Initial positions: rays are cast from the driver's eyes toward where each gauge / button is in the normal view,
        // and the ray finds the dashboard's real surface (distance and tilt).
        void Values(Vector3 eyeL)
        {
            Geo3 g = Geometry(root);
            Vector3 rgt = Vector3.Cross(Vector3.up, fwdL).normalized;
            // (horizontal offset in degrees, vertical offset in degrees) of each element relative to looking straight ahead
            float[,] ang = { { -11.3f, 3.1f }, { -5.1f, 3.1f }, { 1.5f, 3.1f }, { 21.6f, -1.7f }, { 25.2f, -1.7f } };
            Vector3 nGroup = Vector3.zero;
            for (int i = 0; i < 5; i++)
            {
                Vector3 d = Quaternion.AngleAxis(ang[i, 0], Vector3.up) * Quaternion.AngleAxis(-ang[i, 1], rgt) * fwdL;
                Vector3 p, n;
                if (!Ray(g, eyeL, d.normalized, 2.2f, out p, out n)) { p = eyeL + d.normalized * 0.8f; n = -fwdL; }
                frames[i] = new Frame { p = p, n = n, ok = true };
                if (i < 3) nGroup += n;
            }
            // the three gauges share the same tilt
            if (nGroup.sqrMagnitude > 0.01f)
            {
                nGroup.Normalize();
                for (int i = 0; i < 3; i++) frames[i].n = nGroup;
            }
        }

        // ---- already adjusted positions (they ship with the mod: nobody has to calibrate anything)
        static readonly string[] lockedSet =
        {
            "0|-0.58223164;1.459719;1.0756372|-9.933013E-07;-0.0023766332;-0.9999972",
            "1|-0.47383898;1.4523416;1.069798|-9.933013E-07;-0.0023766332;-0.9999972",
            "2|-0.3691669;1.4555166;1.0746597|-9.933013E-07;-0.0023766332;-0.9999972",
            "3|-0.035687923;1.4166596;0.986107|0.02397408;-0.025496311;-0.9993874",
            "4|0.03255564;1.4168577;0.99598587|0.023978107;-0.025502836;-0.9993871"
        };

        bool LockedList()
        {
            try
            {
                foreach (string l in lockedSet)
                {
                    string[] p = l.Split('|');
                    int k = int.Parse(p[0], CultureInfo.InvariantCulture);
                    frames[k] = new Frame { p = ReadV(p[1]), n = ReadV(p[2]).normalized, ok = true };
                }
                MelonLoader.MelonLogger.Msg("[Dashboard] fixed positions of the mod (already adjusted)");
                return true;
            }
            catch (Exception) { return false; }
        }

        // ---- adjustment file
        static string FilePath() { return Cfg.DataFile("WorkingVehicles_dashboard.txt", "CamionetaConducible_tablero.txt"); }

        bool Load()
        {
            try
            {
                if (!File.Exists(FilePath())) return false;
                int n = 0;
                foreach (string l in File.ReadAllLines(FilePath()))
                {
                    string[] p = l.Split('|');
                    int k;
                    if (p.Length < 3 || !int.TryParse(p[0], out k) || k < 0 || k > 4) continue;
                    Vector3 pt = ReadV(p[1]), nm = ReadV(p[2]);
                    if (nm.sqrMagnitude < 0.5f) continue;
                    frames[k] = new Frame { p = pt, n = nm.normalized, ok = true };
                    n++;
                }
                return n == 5;
            }
            catch (Exception) { return false; }
        }

        static Vector3 ReadV(string s)
        {
            string[] c = s.Split(';');
            if (c.Length != 3) return Vector3.zero;
            return new Vector3(float.Parse(c[0], CultureInfo.InvariantCulture), float.Parse(c[1], CultureInfo.InvariantCulture), float.Parse(c[2], CultureInfo.InvariantCulture));
        }

        static string V(Vector3 v) { return v.x.ToString("R", CultureInfo.InvariantCulture) + ";" + v.y.ToString("R", CultureInfo.InvariantCulture) + ";" + v.z.ToString("R", CultureInfo.InvariantCulture); }

        void Save()
        {
            try
            {
                var sb = new StringBuilder();
                for (int i = 0; i < 5; i++) sb.AppendLine(i + "|" + V(frames[i].p) + "|" + V(frames[i].n));
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath()));
                File.WriteAllText(FilePath(), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ materials and meshes

        static void FindShaders()
        {
            if (shSearched) return;
            shSearched = true;
            foreach (string n in new[] { "Unlit/Texture", "Sprites/Default", "UI/Default" })
            {
                Shader sh = Shader.Find(n);
                if (sh != null) { shWithoutLight = sh; break; }
            }
            foreach (string n in new[] { "Unlit/Color", "Sprites/Default", "UI/Default" })
            {
                Shader sh = Shader.Find(n);
                if (sh != null) { shColor = sh; break; }
            }
            MelonLoader.MelonLogger.Msg("[Dashboard] unlit shaders: faces = " + (shWithoutLight != null ? shWithoutLight.name : "none (the engine one is used)") + ", colors = " + (shColor != null ? shColor.name : "none (the engine one is used)"));
        }

        static Shader WithoutLight() { FindShaders(); return shWithoutLight; }

        static Texture2D Face(int i)
        {
            if (faces[i] != null) return faces[i];
            string[] shortName = { "dashboard_rpm.png", "dashboard_fuel.png", "dashboard_speed.png" };
            try
            {
                using (Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WorkingVehicles." + shortName[i]))
                {
                    if (st != null)
                    {
                        var ms = new MemoryStream();
                        st.CopyTo(ms);
                        var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        if (ImageConversion.LoadImage(t, ms.ToArray()))
                        {
                            t.wrapMode = TextureWrapMode.Clamp;
                            t.filterMode = FilterMode.Bilinear;
                            t.anisoLevel = 4;
                            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
                            faces[i] = t;
                        }
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Dashboard] face " + shortName[i] + ": " + e.Message); }
            return faces[i];
        }

        static Material MatFace(int i)
        {
            if (matsFace[i] != null) return matsFace[i];
            Shader sh = WithoutLight();
            Material m = sh != null ? new Material(sh) : new Material(Hood.Mat(Color.white));
            try { m.mainTexture = Face(i); } catch (Exception) { }
            try { m.color = Color.white; } catch (Exception) { }
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            matsFace[i] = m;
            return m;
        }

        /// <summary>Face that receives the scene's light (dark at night, like the rest of the dashboard).</summary>
        static Material MatFaceLight(int i)
        {
            if (matsFaceLight[i] != null) return matsFaceLight[i];
            Material m = new Material(Hood.Mat(Color.white));
            try { m.mainTexture = Face(i); } catch (Exception) { }
            try { m.color = new Color(0.85f, 0.85f, 0.85f); } catch (Exception) { }
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            matsFaceLight[i] = m;
            return m;
        }

        /// <summary>With the engine running the gauges are lit by their own light; off, they are only visible if light reaches them.</summary>
        void Illuminate(bool on, bool force)
        {
            if (!force && on == lit) return;
            lit = on;
            for (int i = 0; i < 3; i++)
            {
                if (facesR[i] != null) facesR[i].sharedMaterial = on ? MatFace(i) : MatFaceLight(i);
                if (armsR[i] != null) armsR[i].sharedMaterial = on ? MatGloss(colNeedle[i]) : Hood.Mat(colNeedle[i] * 0.8f);
            }
        }

        /// <summary>Flat color that doesn't depend on light (needles, warning lights).</summary>
        static Material MatGloss(Color c)
        {
            int k = ((int)(c.r * 255f) << 16) | ((int)(c.g * 255f) << 8) | (int)(c.b * 255f);
            Material m;
            if (matsColor.TryGetValue(k, out m) && m != null) return m;
            FindShaders();
            Shader sh = shColor;
            if (sh != null)
            {
                m = new Material(sh);
                try { m.mainTexture = Texture2D.whiteTexture; } catch (Exception) { }
                m.color = c;
            }
            else m = new Material(Hood.Mat(c));
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            matsColor[k] = m;
            return m;
        }

        /// <summary>Rectangle with cut corners (headlight glass), visible from both sides.</summary>
        static Mesh Rect(float w, float h)
        {
            float a = w * 0.5f, b = h * 0.5f, c = Mathf.Min(a, b) * 0.35f;
            Vector2[] p = { new Vector2(-a + c, -b), new Vector2(a - c, -b), new Vector2(a, -b + c), new Vector2(a, b - c), new Vector2(a - c, b), new Vector2(-a + c, b), new Vector2(-a, b - c), new Vector2(-a, -b + c) };
            int n = p.Length;
            var v = new Vector3[n * 2 + 2]; var nr = new Vector3[n * 2 + 2]; var uv = new Vector2[n * 2 + 2];
            for (int i = 0; i < n; i++) { v[i] = new Vector3(p[i].x, p[i].y, 0f); nr[i] = -Vector3.forward; uv[i] = new Vector2(0.5f, 0.5f); v[n + i] = v[i]; nr[n + i] = Vector3.forward; uv[n + i] = uv[i]; }
            v[2 * n] = Vector3.zero; nr[2 * n] = -Vector3.forward; uv[2 * n] = new Vector2(0.5f, 0.5f);
            v[2 * n + 1] = Vector3.zero; nr[2 * n + 1] = Vector3.forward; uv[2 * n + 1] = new Vector2(0.5f, 0.5f);
            var t = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                t.Add(2 * n); t.Add(i); t.Add(j);
                t.Add(2 * n + 1); t.Add(n + j); t.Add(n + i);
            }
            var m = new Mesh();
            m.vertices = v; m.normals = nr; m.uv = uv; m.SetTriangles(t.ToArray(), 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh Disc(float r, int n)
        {
            var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var nr = new Vector3[n + 1];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f); nr[0] = -Vector3.forward;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                v[i + 1] = new Vector3(cx * r, cy * r, 0f);
                uv[i + 1] = new Vector2(0.5f + cx * 0.5f, 0.5f + cy * 0.5f);
                nr[i + 1] = -Vector3.forward;
            }
            var t = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int a = i + 1, b = (i + 1) % n + 1;
                t.Add(0); t.Add(a); t.Add(b);       // both faces, so it doesn't depend on the winding direction
                t.Add(0); t.Add(b); t.Add(a);
            }
            var m = new Mesh { vertices = v, uv = uv, normals = nr };
            m.SetTriangles(t.ToArray(), 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh NeedleMesh(float length, float width)
        {
            var v = new[] { new Vector3(0f, length, 0f), new Vector3(-width, 0f, 0f), new Vector3(width, 0f, 0f), new Vector3(0f, -length * 0.2f, 0f) };
            var nr = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            var uv = new[] { new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0f) };
            var t = new[] { 0, 1, 2, 0, 2, 1, 3, 2, 1, 3, 1, 2 };
            var m = new Mesh { vertices = v, normals = nr, uv = uv };
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static GameObject Part(Transform parent, string names, Mesh m, Material mat, Vector3 pos)
        {
            var o = new GameObject(names);
            o.transform.SetParent(parent, false);
            o.transform.localPosition = pos;
            o.AddComponent<MeshFilter>().sharedMesh = m;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return o;
        }

        static GameObject Box(Transform parent, string names, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = names;
            Collider col = o.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            o.transform.SetParent(parent, false);
            o.transform.localPosition = pos;
            o.transform.localScale = size;
            o.GetComponent<Renderer>().sharedMaterial = mat;
            return o;
        }

        static Vector3 UpOf(Vector3 n, Vector3 fwd)
        {
            Vector3 u = Vector3.up - n * Vector3.Dot(Vector3.up, n);
            if (u.sqrMagnitude < 1e-4f) u = fwd;
            return u.normalized;
        }

        // ------------------------------------------------------------------ assembly

        void Build()
        {
            if (group != null) UnityEngine.Object.Destroy(group);
            group = new GameObject("WV_Dashboard");
            group.transform.SetParent(root, false);
            float k = Settings.SizeDashboard();

            // gauges: 0 = RPM (large), 1 = fuel (small), 2 = speedometer (large)
            float[] diam = { 0.088f, 0.050f, 0.088f };
            for (int i = 0; i < 3; i++)
            {
                Frame m = frames[i];
                float d = diam[i] * k;
                var frameT = new GameObject("Indicador" + i).transform;
                frameT.SetParent(group.transform, false);
                frameT.localPosition = m.p + m.n * 0.004f;
                frameT.localRotation = Quaternion.LookRotation(-m.n, UpOf(m.n, fwdL));
                facesR[i] = Part(frameT, "Cara", Disc(d * 0.5f, 48), MatFace(i), Vector3.zero).GetComponent<Renderer>();
                var piv = new GameObject("Aguja").transform;
                piv.SetParent(frameT, false);
                piv.localPosition = new Vector3(0f, 0f, -0.0013f);
                Color ca = i == 1 ? new Color(0.95f, 0.18f, 0.12f) : new Color(1f, 0.55f, 0.12f);
                colNeedle[i] = ca;
                armsR[i] = Part(piv, "Brazo", NeedleMesh(d * 0.40f, d * 0.035f), MatGloss(ca), Vector3.zero).GetComponent<Renderer>();
                Part(frameT, "Eje", Disc(d * 0.07f, 16), Hood.Mat(new Color(0.12f, 0.12f, 0.13f)), new Vector3(0f, 0f, -0.0022f));
                needles[i] = piv;
            }

            // buttons: 3 = lights, 4 = air conditioning
            for (int j = 0; j < 2; j++)
            {
                Frame m = frames[3 + j];
                var frameT = new GameObject("Boton" + j).transform;
                frameT.SetParent(group.transform, false);
                frameT.localPosition = m.p + m.n * 0.003f;
                frameT.localRotation = Quaternion.LookRotation(-m.n, UpOf(m.n, fwdL));
                Material dark = Hood.Mat(new Color(0.10f, 0.10f, 0.11f));
                Box(frameT, "Base", new Vector3(0f, 0f, -0.003f), new Vector3(0.036f, 0.030f, 0.008f) * k, dark);
                var pal = new GameObject("Palanca").transform;
                pal.SetParent(frameT, false);
                pal.localPosition = new Vector3(0f, 0f, -0.008f * k);
                GameObject p = Box(pal, "Tecla", new Vector3(0f, 0f, -0.003f * k), new Vector3(0.026f, 0.016f, 0.008f) * k, Hood.Mat(new Color(0.20f, 0.20f, 0.21f)));
                leverT[j] = pal;
                levers[j] = p.GetComponent<Renderer>();
                GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                led.name = "Luz";
                Collider col = led.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
                led.transform.SetParent(frameT, false);
                led.transform.localPosition = new Vector3(0f, 0.0105f * k, -0.0105f * k);
                led.transform.localScale = new Vector3(0.0055f, 0.0055f, 0.0055f) * k;
                leds[j] = led.GetComponent<Renderer>();
            }
            Illuminate(false, true);
            RefreshButtons();
        }

        readonly List<GameObject> lenses = new List<GameObject>();

        void CreateHeadlights()
        {
            foreach (Light l in headlights) if (l != null) UnityEngine.Object.Destroy(l.gameObject);
            foreach (GameObject g in lenses) if (g != null) UnityEngine.Object.Destroy(g);
            headlights.Clear(); lenses.Clear();
            Vector3 rgt = Vector3.Cross(Vector3.up, fwdL).normalized;
            // headlight positions: the model's if valid; otherwise, calculated from the body size
            var pos = new List<Vector3>();
            var posT = new List<Vector3>();
            float back = -2.3f, halfT = 0.9f, lowT = 0f;
            try
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    bool isF = n.Contains("auroraelectrolizer_headlight"), isT = n.Contains("auroraelectrolizer_taillight");
                    if (!isF && !isT) continue;
                    Vector3 p = root.InverseTransformPoint(t.position);
                    MelonLoader.MelonLogger.Msg("[Dashboard] headlight of model '" + t.name + "' at (" + p.x.ToString("F2") + ", " + p.y.ToString("F2") + ", " + p.z.ToString("F2") + ")");
                    if (isF && p.magnitude > 0.5f && Vector3.Dot(p, fwdL) > 1f) pos.Add(p);
                    if (isT && p.magnitude > 0.5f && Vector3.Dot(p, fwdL) < -1f) posT.Add(p);
                }
            }
            catch (Exception) { }
            if (pos.Count != 2)
            {
                pos.Clear();
                float front = 2.3f, half = 0.9f, low = 0f;
                try
                {
                    bool exists = false; float fmax = 0f, fmin = 0f, wmax = 0f, ymin = 0f;
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
                    {
                        if (r == null || r.GetComponent<MeshFilter>() == null) continue;
                        if (group != null && r.transform.IsChildOf(group.transform)) continue;
                        string nr = r.gameObject.name.ToLowerInvariant();
                        if (nr.Contains("wheel") || nr.Contains("tire") || nr.Contains("snow") || nr.StartsWith("wv_")) continue;
                        if (nr.Contains("lod1") || nr.Contains("lod2") || nr.Contains("glass")) continue;
                        MeshFilter mfb = r.GetComponent<MeshFilter>();
                        if (mfb.sharedMesh == null) continue;
                        Bounds lb = mfb.sharedMesh.bounds;
                        float zf = -99f, zb = 99f, wmx = 0f, ymn = 99f;
                        for (int c = 0; c < 8; c++)
                        {
                            Vector3 corner = lb.center + new Vector3((c & 1) == 0 ? -lb.extents.x : lb.extents.x, (c & 2) == 0 ? -lb.extents.y : lb.extents.y, (c & 4) == 0 ? -lb.extents.z : lb.extents.z);
                            Vector3 q = root.InverseTransformPoint(mfb.transform.TransformPoint(corner));
                            float zz = Vector3.Dot(q, fwdL);
                            zf = Mathf.Max(zf, zz); zb = Mathf.Min(zb, zz);
                            wmx = Mathf.Max(wmx, Mathf.Abs(Vector3.Dot(q, rgt)));
                            ymn = Mathf.Min(ymn, q.y);
                        }
                        if (zf > 3.5f || zb < -3.5f) continue;
                        if (!exists || zf > fmax) fmax = zf;
                        if (!exists || zb < fmin) fmin = zb;
                        if (wmx < 1.4f && wmx > wmax) wmax = wmx;
                        if (!exists || ymn < ymin) ymin = ymn;
                        exists = true;
                    }
                    if (exists) { front = fmax; half = wmax; low = ymin; back = fmin; halfT = wmax; lowT = ymin; }
                }
                catch (Exception) { }
                MelonLoader.MelonLogger.Msg("[Dashboard] headlights calculated: front " + front.ToString("F2") + ", rear " + back.ToString("F2") + ", half width " + half.ToString("F2") + ", base " + low.ToString("F2"));
                float y = low + 1.05f;
                posT.Clear();
                posT.Add(fwdL * (back + 0.02f) + rgt * (-halfT * 0.52f) + Vector3.up * (lowT + 0.775f));
                posT.Add(fwdL * (back + 0.02f) + rgt * (halfT * 0.52f) + Vector3.up * (lowT + 0.775f));
                pos.Add(fwdL * (front - 0.285f) + rgt * (-half * 0.74f) + Vector3.up * y);
                pos.Add(fwdL * (front - 0.285f) + rgt * (half * 0.74f) + Vector3.up * y);
            }
            // tuning: headlights lower and further back; taillights higher and a bit forward
            for (int i = 0; i < pos.Count; i++) pos[i] += -fwdL * 0.07f + Vector3.down * 0.13f;
            for (int i = 0; i < posT.Count; i++) posT[i] += fwdL * 0.03f + Vector3.up * 0.11f;
            foreach (Vector3 p in pos)
            {
                headlights.Add(NewLight(p, true));
                var lens = new GameObject("WV_Lens");
                lens.transform.SetParent(root, false);
                lens.transform.localPosition = p + fwdL * 0.04f;
                float sideSign = Vector3.Dot(p, rgt) >= 0f ? 1f : -1f;   // each headlight faces its own side (mirror)
                lens.transform.localRotation = Quaternion.AngleAxis(12f * sideSign, Vector3.up) * Quaternion.LookRotation(-fwdL, Vector3.up);
                var mf = lens.AddComponent<MeshFilter>(); mf.sharedMesh = Rect(0.31f, 0.27f);
                var mr = lens.AddComponent<MeshRenderer>(); mr.sharedMaterial = MatGloss(new Color(1f, 0.96f, 0.8f));
                lenses.Add(lens);
            }
            foreach (Vector3 p in posT)
            {
                var lens = new GameObject("WV_TailLens");
                lens.transform.SetParent(root, false);
                lens.transform.localPosition = p + fwdL * 0.02f;
                lens.transform.localRotation = Quaternion.LookRotation(fwdL, Vector3.up);
                var mf = lens.AddComponent<MeshFilter>(); mf.sharedMesh = Rect(0.26f, 0.095f);
                var mr = lens.AddComponent<MeshRenderer>(); mr.sharedMaterial = MatGloss(new Color(1f, 0.10f, 0.05f));
                lenses.Add(lens);
                // a soft red glow, just behind the glass
                var lr = new GameObject("WV_TailLight");
                lr.transform.SetParent(root, false);
                lr.transform.localPosition = p - fwdL * 0.22f;
                Light lz = lr.AddComponent<Light>();
                lz.type = LightType.Point; lz.range = 2.4f; lz.intensity = 0.55f; lz.color = new Color(1f, 0.1f, 0.05f);
                lz.shadows = LightShadows.None; lz.renderMode = LightRenderMode.ForcePixel;
                headlights.Add(lz);
            }
            ApplyLights();
            MelonLoader.MelonLogger.Msg("[Dashboard] headlights: " + headlights.Count + ", taillights: " + posT.Count);
        }

        void FindElectrolyzers()
        {
            elecSimple.Clear(); elecModular.Clear(); elecObjects.Clear(); elecOk = false;
            try
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (!n.Contains("auroraelectrolizer_headlight") && !n.Contains("auroraelectrolizer_taillight")) continue;
                    var sb = new StringBuilder();
                    foreach (Component c in t.GetComponents<Component>()) if (c != null) sb.Append(c.GetType().Name).Append(' ');
                    AuroraLightingSimple a = t.GetComponent<AuroraLightingSimple>();
                    AuroraModularElectrolizer m = t.GetComponent<AuroraModularElectrolizer>();
                    if (a == null && m == null) { a = t.GetComponentInParent<AuroraLightingSimple>(); m = t.GetComponentInParent<AuroraModularElectrolizer>(); }
                    MelonLoader.MelonLogger.Msg("[Dashboard] game light '" + t.name + "': components = " + sb + ", simple=" + (a != null) + ", modular=" + (m != null));
                    elecObjects.Add(t);
                    if (a != null && !elecSimple.Contains(a)) elecSimple.Add(a);
                    if (m != null && !elecModular.Contains(m)) elecModular.Add(m);
                }
                elecOk = elecSimple.Count + elecModular.Count > 0;
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Dashboard] game lights: " + e.Message); elecOk = false; }
            MelonLoader.MelonLogger.Msg("[Dashboard] lights: " + (elecOk ? "the game's originals are used (like with the aurora)" : "could not find the originals, using own lights"));
        }

        void PutChildrenManual(bool on)
        {
            foreach (Transform t in elecObjects)
            {
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++)
                {
                    Transform h = t.GetChild(i);
                    string nh = h.name.ToLowerInvariant();
                    if (nh == "light" || nh == "lightshaft") h.gameObject.SetActive(on);
                }
            }
            childrenManual = on;
        }

        bool ChildrenActive()
        {
            foreach (Transform t in elecObjects)
            {
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++)
                    if (t.GetChild(i).name.ToLowerInvariant() == "light" && t.GetChild(i).gameObject.activeSelf) return true;
            }
            return false;
        }

        Light NewLight(Vector3 pLocal, bool fore)
        {
            var go = new GameObject(fore ? "WV_Headlight" : "WV_TailLight");
            go.transform.SetParent(root, false);
            go.transform.localPosition = pLocal;
            Vector3 dir = fore ? fwdL : -fwdL;
            Vector3 rgt = Vector3.Cross(Vector3.up, dir).normalized;
            go.transform.localRotation = Quaternion.AngleAxis(fore ? 3f : 0f, rgt) * Quaternion.LookRotation(dir, Vector3.up);
            Light l = go.AddComponent<Light>();
            if (fore)
            {
                l.type = LightType.Spot; l.spotAngle = 42f; l.range = 45f; l.intensity = 3.5f; l.renderMode = LightRenderMode.ForcePixel;
                l.color = new Color(1f, 0.93f, 0.78f);
            }
            else
            {
                l.type = LightType.Point; l.range = 1.0f; l.intensity = 0.35f;
                l.color = new Color(1f, 0.12f, 0.06f);
            }
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ state

        void ApplyLights()
        {
            foreach (Light l in headlights) if (l != null) l.enabled = lights;
            foreach (GameObject g in lenses) if (g != null) g.SetActive(lights);
            if (!elecOk) return;
            try
            {
                foreach (AuroraLightingSimple a in elecSimple) if (a != null) a.SetExternalPowerState(lights);
                foreach (AuroraModularElectrolizer m in elecModular) if (m != null) m.ExternalPowerActive = lights;
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Dashboard] SetExternalPowerState: " + e.Message); }
            if (!lights && childrenManual) PutChildrenManual(false);
            checkLights = lights ? 0.8f : -1f;
        }

        void RefreshButtons()
        {
            bool[] on = { lights, ac };
            Color[] cOn = { new Color(0.25f, 1f, 0.3f), new Color(1f, 0.55f, 0.1f) };
            for (int j = 0; j < 2; j++)
            {
                if (leds[j] != null) leds[j].sharedMaterial = on[j] ? MatGloss(cOn[j]) : Hood.Mat(new Color(0.08f, 0.08f, 0.08f));
                if (leverT[j] != null) leverT[j].localRotation = Quaternion.Euler(on[j] ? 11f : -11f, 0f, 0f);
                if (levers[j] != null) levers[j].sharedMaterial = Hood.Mat(hover == 3 + j ? new Color(0.42f, 0.42f, 0.44f) : new Color(0.20f, 0.20f, 0.21f));
            }
        }

        public string ToggleLights(bool broken)
        {
            if (!lights && broken) { Click(); return L.T("Sin batería: las luces no encienden", "No battery: the lights won't turn on"); }
            lights = !lights;
            ApplyLights(); RefreshButtons(); Click();
            return lights ? L.T("Luces encendidas", "Lights on") : L.T("Luces apagadas", "Lights off");
        }

        public string ToggleAc(bool broken)
        {
            if (!ac && broken) { Click(); return L.T("Sin batería: el aire no funciona", "No battery: the AC won't work"); }
            ac = !ac;
            RefreshButtons(); Click();
            if (ac) StartAc();
            return ac ? L.T("Aire acondicionado (caliente) encendido", "Air conditioning (heat) on") : L.T("Aire acondicionado apagado", "Air conditioning off");
        }

        // ------------------------------------------------------------------ sound

        static void BuildClips()
        {
            if (clipAc != null) return;
            const int SR = 32000;
            var rnd = new System.Random(5);
            int n = SR * 3;
            float[] a = new float[n];
            float l1 = 0f, l2 = 0f, l3 = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)rnd.NextDouble() * 2f - 1f;
                l1 += (x - l1) * 0.30f;
                l2 += (l1 - l2) * 0.22f;
                l3 += (l2 - l3) * 0.08f;
                float t = i / (float)SR;
                float mod = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 3f) + 0.05f * Mathf.Sin(2f * Mathf.PI * t * 2f / 3f + 1f);
                a[i] = ((l2 - l3) * 1.4f + l3 * 0.4f) * mod;
            }
            int f = SR / 8;      // smooth loop splice
            for (int i = 0; i < f; i++)
            {
                float w = i / (float)f;
                a[i] = a[i] * w + a[n - f + i] * (1f - w);
            }
            var cut = new float[n - f];
            Array.Copy(a, cut, n - f);
            float mx = 0.0001f;
            for (int i = 0; i < cut.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(cut[i]));
            for (int i = 0; i < cut.Length; i++) cut[i] *= 0.7f / mx;
            clipAc = AudioClip.Create("WV_Ac", cut.Length, 1, SR, false);
            clipAc.SetData(cut, 0);
            clipAc.hideFlags = HideFlags.DontUnloadUnusedAsset;

            int nc = SR / 14;
            float[] c = new float[nc];
            float lp = 0f;
            for (int i = 0; i < nc; i++)
            {
                float t = i / (float)SR;
                float x = (float)rnd.NextDouble() * 2f - 1f;
                lp += (x - lp) * 0.45f;
                c[i] = (lp * 0.7f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.3f) * Mathf.Exp(-t * 130f);
            }
            clipClick = AudioClip.Create("WV_Click", nc, 1, SR, false);
            clipClick.SetData(c, 0);
            clipClick.hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        void Click()
        {
            try
            {
                if (!Settings.Sound()) return;
                BuildClips();
                if (srcClick == null)
                {
                    var go = new GameObject("WV_Click");
                    go.transform.SetParent(group != null ? group.transform : root, false);
                    srcClick = go.AddComponent<AudioSource>();
                    srcClick.spatialBlend = 0.2f; srcClick.minDistance = 2f; srcClick.maxDistance = 12f;
                }
                srcClick.PlayOneShot(clipClick, 0.5f);
            }
            catch (Exception) { }
        }

        void StartAc()
        {
            try
            {
                if (!Settings.Sound()) return;
                BuildClips();
                if (srcAc == null)
                {
                    var go = new GameObject("WV_Ac");
                    go.transform.SetParent(group != null ? group.transform : root, false);
                    srcAc = go.AddComponent<AudioSource>();
                    srcAc.clip = clipAc;
                    srcAc.loop = true;
                    srcAc.spatialBlend = 0.55f;
                    srcAc.dopplerLevel = 0f;
                    srcAc.minDistance = 0.8f;
                    srcAc.maxDistance = 7f;
                    srcAc.rolloffMode = AudioRolloffMode.Linear;
                    srcAc.volume = 0f;
                }
                if (!srcAc.isPlaying) srcAc.Play();
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ every frame

        /// <param name="acel">0..1 how hard the accelerator is pressed</param>
        public void Update(bool engineOn, bool broken, float velMs, float accel, float vmaxKmh, float dt)
        {
            if (group == null) return;
            if (broken && lights) { lights = false; ApplyLights(); RefreshButtons(); }
            Illuminate((engineOn && !broken) || lights, false);
            if (checkLights > 0f)
            {
                checkLights -= dt;
                if (checkLights <= 0f && lights && elecOk && !ChildrenActive())
                {
                    PutChildrenManual(true);
                    MelonLoader.MelonLogger.Msg("[Dashboard] the game did not turn on just the lights: they were turned on by hand");
                }
            }
            if (broken && ac) { ac = false; RefreshButtons(); }

            float kmh = Mathf.Abs(velMs) * 3.6f;
            // revolutions: idle with the engine running, rising with speed and with the accelerator
            float rpmObj = 0f;
            if (engineOn)
            {
                rpmNoise += dt;
                rpmObj = 850f + Mathf.Clamp01(kmh / Mathf.Max(vmaxKmh, 1f)) * 3900f + accel * 650f + Mathf.Sin(rpmNoise * 9f) * 18f;
            }
            rpmM = Mathf.Lerp(rpmM, rpmObj, 1f - Mathf.Exp(-(rpmObj > rpmM ? 5f : 2.5f) * dt));
            velM = Mathf.Lerp(velM, kmh, 1f - Mathf.Exp(-8f * dt));
            float fuelObj = engineOn || lights ? Mathf.Clamp01(fuel) : 0f;
            fuelM = Mathf.Lerp(fuelM, fuelObj, 1f - Mathf.Exp(-2f * dt));

            // angles (degrees, clockwise from above)
            if (needles[Rpm] != null) needles[Rpm].localRotation = Quaternion.Euler(0f, 0f, -(-135f + 270f * Mathf.Clamp01(rpmM / 6000f)));
            if (needles[Vel] != null) needles[Vel].localRotation = Quaternion.Euler(0f, 0f, -(-135f + 270f * Mathf.Clamp01(velM / 100f)));
            if (needles[Fuel] != null) needles[Fuel].localRotation = Quaternion.Euler(0f, 0f, -(-50f + 100f * Mathf.Clamp01(fuelM)));

            // air: fade in and out
            if (srcAc != null)
            {
                float obj = ac ? Settings.VolumeAc() : 0f;
                volAc = Mathf.MoveTowards(volAc, obj, dt * 0.35f);
                srcAc.volume = volAc;
                if (!ac && volAc <= 0.0005f && srcAc.isPlaying) srcAc.Stop();
            }
        }

        /// <summary>When getting out: the air goes quiet (it is noted whether it was on).</summary>
        public void Pause()
        {
            hover = -1;
            inAdjust = false;
            RefreshButtons();
            if (srcAc != null && srcAc.isPlaying) { srcAc.Stop(); volAc = 0f; }
        }

        public void Resume() { if (ac) StartAc(); }

        // ------------------------------------------------------------------ aiming and click

        /// <summary>Text to show over the crosshair if a button is being aimed at (or null).</summary>
        public string Aim(Camera cam, bool broken, out int which)
        {
            which = -1;
            if (group == null || cam == null) { SetHover(-1); return null; }
            Vector3 o = cam.transform.position, f = cam.transform.forward;
            float best = 999f;
            int hit = -1;
            float k = Settings.SizeDashboard();
            for (int j = 0; j < 2; j++)
            {
                Vector3 p = root.TransformPoint(frames[3 + j].p);
                Vector3 d = p - o;
                float dist = d.magnitude;
                if (dist < 0.05f || dist > 1.9f) continue;
                float ang = Vector3.Angle(f, d / dist);
                float lim = Mathf.Atan2(0.024f * k, dist) * Mathf.Rad2Deg + 1.3f;
                if (ang < lim && ang < best) { best = ang; hit = j; }
            }
            SetHover(hit < 0 ? -1 : 3 + hit);
            if (hit < 0) return null;
            which = 3 + hit;
            return hit == 0 ? "[Click] " + (lights ? L.T("Apagar las luces", "Turn lights off") : L.T("Encender las luces", "Turn lights on")) : "[Click] " + (ac ? L.T("Apagar el aire acondicionado", "Turn AC off") : L.T("Encender el aire acondicionado", "Turn AC on"));
        }

        void SetHover(int h)
        {
            if (h == hover) return;
            hover = h;
            RefreshButtons();
        }

        // ------------------------------------------------------------------ dashboard adjustment

        public void StartAdjust() { inAdjust = true; step = 0; }

        public string TextAdjust()
        {
            int k = Mathf.Min(step, 4);
            return L.T("AJUSTE DEL TABLERO (" + (step + 1) + "/5): poné la mira en el CENTRO del " + nameList[k] + " y apretá Enter\n" +
                   "Backspace = volver atrás   " + Settings.KeyDashboard() + " = cancelar   Supr = volver a las posiciones automáticas",
                   "DASHBOARD ADJUSTMENT (" + (step + 1) + "/5): aim at the CENTER of the " + nameListEn[k] + " and press Enter\n" +
                   "Backspace = go back   " + Settings.KeyDashboard() + " = cancel   Del = back to automatic positions");
        }

        /// <summary>Handles the adjustment mode keys. Returns a notice (or null).</summary>
        public string ProcessAdjust(Camera cam)
        {
            if (!inAdjust) return null;
            if (Input.GetKeyDown(Settings.KeyDashboard())) { inAdjust = false; return "Ajuste del tablero cancelado"; }
            if (Input.GetKeyDown(KeyCode.Backspace)) { step = Mathf.Max(0, step - 1); return null; }
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                try { if (File.Exists(FilePath())) File.Delete(FilePath()); } catch (Exception) { }
                inAdjust = false;
                Vector3 eye = cam != null ? root.InverseTransformPoint(cam.transform.position) : Vector3.zero;
                if (!LockedList()) Values(eye);
                Build(); CreateHeadlights();
                return L.T("Tablero: posiciones originales del mod", "Dashboard: the mod's original positions");
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (cam == null) return null;
                Vector3 o = root.InverseTransformPoint(cam.transform.position);
                Vector3 d = root.InverseTransformDirection(cam.transform.forward).normalized;
                Vector3 p, n;
                if (!Ray(Geometry(root), o, d, 2.5f, out p, out n)) return L.T("No encontré el tablero ahí: mirá más cerca del indicador", "Dashboard not found there: look closer at the gauge");
                fresh[step] = new Frame { p = p, n = n, ok = true };
                step++;
                if (step >= 5)
                {
                    // the three gauges share the same tilt
                    Vector3 nm = (fresh[0].n + fresh[1].n + fresh[2].n).normalized;
                    for (int i = 0; i < 3; i++) fresh[i].n = nm;
                    for (int i = 0; i < 5; i++) frames[i] = fresh[i];
                    Save();
                    inAdjust = false;
                    Build();
                    MelonLoader.MelonLogger.Msg("[Dashboard] positions saved in UserData\\WorkingVehicles_dashboard.txt");
                    return "Tablero ajustado y guardado";
                }
            }
            return null;
        }

        public void Destroy()
        {
            if (group != null) UnityEngine.Object.Destroy(group);
            try
            {
                foreach (AuroraLightingSimple a in elecSimple) if (a != null) a.SetExternalPowerState(false);
                foreach (AuroraModularElectrolizer m in elecModular) if (m != null) m.ExternalPowerActive = false;
                if (childrenManual) PutChildrenManual(false);
            }
            catch (Exception) { }
            foreach (Light l in headlights) if (l != null) UnityEngine.Object.Destroy(l.gameObject);
            foreach (GameObject g in lenses) if (g != null) UnityEngine.Object.Destroy(g);
        }
    }
}
