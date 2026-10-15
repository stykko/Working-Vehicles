using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace WorkingVehicles
{
    /// <summary>Gradually removes and restores the snow the game puts on the hood, roof and trunk. The wheels are not touched.</summary>
    internal static class Snow
    {
        static readonly Dictionary<int, List<GameObject>> parts = new Dictionary<int, List<GameObject>>();

        static List<GameObject> Discover(Transform root)
        {
            int id = root.GetInstanceID();
            List<GameObject> list;
            if (parts.TryGetValue(id, out list)) return list;
            list = new List<GameObject>();
            int blended = 0;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                GameObject go = r.gameObject;
                string n = go.name.ToLowerInvariant();
                if (n.Contains("tire") || n.Contains("wheel") || n.Contains("rueda")) continue;

                bool any = false, every = true;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;
                foreach (Material m in mats)
                {
                    bool s = m != null && m.name.ToLowerInvariant().Contains("snow");
                    any |= s;
                    every &= s;
                }

                bool perName = n.Contains("snow") && !n.Contains("door") && !n.Contains("hood");
                if (!(every || perName))
                {
                    if (any) blended++;
                    continue;
                }
                // Never touch a part that is also a door, a box or the vehicle's bed.
                if (go.GetComponent<VehicleDoor>() != null || go.GetComponent<Container>() != null || go.GetComponent<Bed>() != null) continue;
                if (!go.activeSelf) continue;
                list.Add(go);
            }

            // Fixed "random" order so the snow comes back evenly and not in patches.
            var rnd = new System.Random(root.name.GetHashCode());
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                GameObject t = list[i]; list[i] = list[j]; list[j] = t;
            }
            parts[id] = list;
            if (Cfg.DebugLog)
                MelonLoader.MelonLogger.Msg("[Snow] '" + root.name + "': " + list.Count + " snow parts" + (blended > 0 ? " (" + blended + " meshes mix snow with something else and were left alone)" : "") + ".");
            return list;
        }

        /// <param name="nivel">0 = no snow, 1 = all the snow</param>
        public static void Apply(Transform root, float level)
        {
            if (root == null) return;
            if (level >= 0.999f && !parts.ContainsKey(root.GetInstanceID())) { Glass.Apply(root, 1f); return; }
            List<GameObject> l = Discover(root);
            int n = l.Count;
            for (int i = 0; i < n; i++)
            {
                if (l[i] == null) continue;
                bool ver = level >= (i + 1f) / n - 0.0001f;
                if (l[i].activeSelf != ver) l[i].SetActive(ver);
            }
            Glass.Apply(root, level);
        }
    }

    /// <summary>
    /// Snow on the windows. We don't know in advance which properties the glass material has, so the
    /// material is copied (only for that vehicle) and whatever looks like snow / frost / dirt and the color opacity are turned down.
    /// 35 % always remains so it doesn't look fully transparent. What was found is saved in WorkingVehicles_glass.txt.
    /// </summary>
    internal static class Glass
    {
        class Prop { public string names; public bool color; public float f0; public Color c0; }
        class Pane { public Material mat; public List<Prop> props = new List<Prop>(); }

        static readonly Dictionary<int, List<Pane>> perVehicle = new Dictionary<int, List<Pane>>();
        static readonly string[] keys = { "snow", "frost", "ice", "dirt", "grime", "cover", "condens" };
        const float Rest = 0.35f;

        static bool Has(string n)
        {
            n = n.ToLowerInvariant();
            foreach (string c in keys) if (n.Contains(c)) return true;
            return false;
        }

        public static void Apply(Transform root, float level)
        {
            try
            {
                int id = root.GetInstanceID();
                List<Pane> list;
                if (!perVehicle.TryGetValue(id, out list))
                {
                    if (level >= 0.999f) return;          // nothing is touched until needed
                    list = Prepare(root);
                    perVehicle[id] = list;
                }
                float f = Mathf.Lerp(Rest, 1f, Mathf.Clamp01(level));
                foreach (Pane c in list)
                {
                    if (c.mat == null) continue;
                    foreach (Prop p in c.props)
                    {
                        if (p.color) { Color k = p.c0; k.a = p.c0.a * f; c.mat.SetColor(p.names, k); }
                        else c.mat.SetFloat(p.names, p.f0 * f);
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Glass] " + e.Message); }
        }

        static List<Pane> Prepare(Transform root)
        {
            var list = new List<Pane>();
            var sb = new StringBuilder();
            sb.AppendLine("=== " + root.name + " (" + DateTime.Now + ") ===");
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var shared = r.sharedMaterials;
                if (shared == null) continue;
                bool exists = false;
                foreach (Material m in shared)
                    if (m != null) { string n = m.name.ToLowerInvariant(); if ((n.Contains("glass") || n.Contains("window") || n.Contains("windshield")) && !n.Contains("snow")) exists = true; }
                if (!exists) continue;

                var inst = r.materials;                       // own copies for this renderer
                for (int i = 0; i < inst.Length; i++)
                {
                    Material m = inst[i];
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    if (!(n.Contains("glass") || n.Contains("window") || n.Contains("windshield")) || n.Contains("snow")) continue;

                    var c = new Pane { mat = m };
                    sb.AppendLine("Renderer '" + r.gameObject.name + "'  material '" + m.name + "'  shader '" + (m.shader != null ? m.shader.name : "?") + "'");
                    try
                    {
                        Shader sh = m.shader;
                        int count = sh.GetPropertyCount();
                        for (int k = 0; k < count; k++)
                        {
                            string pn = sh.GetPropertyName(k);
                            string type = sh.GetPropertyType(k).ToString();
                            bool touched = false;
                            if ((type == "Float" || type == "Range") && Has(pn)) { c.props.Add(new Prop { names = pn, f0 = m.GetFloat(pn) }); touched = true; }
                            else if (type == "Color" && (Has(pn) || pn == "_Color" || pn == "_BaseColor" || pn == "_TintColor"))
                            { c.props.Add(new Prop { names = pn, color = true, c0 = m.GetColor(pn) }); touched = true; }
                            sb.AppendLine("    " + pn + " (" + type + ")" + (touched ? "   <- se ajusta" : ""));
                        }
                    }
                    catch (Exception e) { sb.AppendLine("    could not list properties: " + e.Message); }
                    list.Add(c);
                }
            }
            if (list.Count == 0) sb.AppendLine("No glass materials found.");
            try
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "WorkingVehicles_glass.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                if (Cfg.Report) File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
            return list;
        }
    }

    /// <summary>In-game time and weather, to know how much snow builds up again.</summary>
    internal static class Climate
    {
        public static float Hours()
        {
            try
            {
                TimeOfDay t = GameManager.GetTimeOfDayComponent();
                return t != null ? t.GetHoursPlayedNotPaused() : -1f;
            }
            catch (Exception) { return -1f; }
        }

        /// <summary>Fraction of "full snow" that builds up per in-game hour.</summary>
        public static float Pace()
        {
            float r = 1f / 24f;          // on its own, it all comes back in a day
            try
            {
                Weather w = GameManager.GetWeatherComponent();
                if (w != null && !w.IsIndoorScene())
                {
                    if (w.IsBlizzard()) r += 0.5f;          // blizzard: in 2 hours
                    else if (w.IsHeavySnow()) r += 0.25f;   // heavy snowfall: in 4 hours
                    else if (w.IsSnowing() || w.IsLightSnow()) r += 0.1f;
                }
            }
            catch (Exception) { }
            return r;
        }
    }

    /// <summary>
    /// Removes motion blur if the option is on; if it is turned off, restores it.
    /// The game has the effect in the camera's post-process layer (PostProcessLayer.motionBlurSettings) and also
    /// an "old" ImageEffects MotionBlur on the "ImageEffectCamera" camera. Both are turned off, every frame,
    /// because the game can turn them back on. What it found is saved in UserData\WorkingVehicles_camera.txt.
    /// </summary>
    internal static class Blur
    {
        class Layer { public PostProcessLayer layer; public bool here; }
        static readonly List<Layer> layers = new List<Layer>();
        static readonly List<Behaviour> oldOnes = new List<Behaviour>();
        static float scanT;
        static int lastCont = -1;
        static bool dump;
        static bool qualityForced;
        static float qualityT;
        static QualityMotionBlur qualityOrig;

        // The game decides the blur according to graphics quality (QualityLevelSettings.m_MotionBlurQuality).
        static void Quality(bool turnOff)
        {
            try
            {
                QualitySettingsManager qm = GameManager.GetQualitySettingsManager();
                if (qm == null) return;
                QualityLevelSettings q = qm.GetCurrentQualitySettings();
                if (q == null) return;
                if (turnOff)
                {
                    if (q.m_MotionBlurQuality == QualityMotionBlur.Off || Time.unscaledTime < qualityT) return;
                    qualityT = Time.unscaledTime + 5f;
                    if (!qualityForced) { qualityOrig = q.m_MotionBlurQuality; qualityForced = true; }
                    MelonLoader.MelonLogger.Msg("[MotionBlur] game blur quality: " + q.m_MotionBlurQuality + " -> Off");
                    q.m_MotionBlurQuality = QualityMotionBlur.Off;
                    qm.ApplyCurrentQualitySettings();
                }
                else if (qualityForced)
                {
                    qualityForced = false;
                    q.m_MotionBlurQuality = qualityOrig;
                    qm.ApplyCurrentQualitySettings();
                    MelonLoader.MelonLogger.Msg("[MotionBlur] blur quality restored: " + qualityOrig);
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[MotionBlur] quality: " + e.Message); }
        }

        public static void Update(float dt)
        {
            try
            {
                bool remove = Settings.RemoveBlur();
                Quality(remove);
                if (!remove)
                {
                    if (layers.Count > 0 || oldOnes.Count > 0)
                    {
                        foreach (Layer c in layers)
                            if (c.layer != null) { MotionBlur mb = c.layer.motionBlurSettings; if (mb != null) mb.enabled.value = c.here; }
                        foreach (Behaviour b in oldOnes) if (b != null) b.enabled = true;
                        layers.Clear();
                        oldOnes.Clear();
                        lastCont = -1;
                    }
                    return;
                }

                scanT -= dt;
                if (scanT <= 0f)
                {
                    scanT = 3f;
                    var diag = dump ? null : new StringBuilder();
                    foreach (PostProcessLayer l in UnityEngine.Object.FindObjectsOfType<PostProcessLayer>())
                    {
                        if (l == null) continue;
                        MotionBlur mb = l.motionBlurSettings;
                        if (diag != null) diag.AppendLine("PostProcessLayer on '" + l.gameObject.name + "': motionBlurSettings " + (mb != null ? "enabled=" + mb.enabled.value : "none"));
                        if (mb == null) continue;
                        bool ya = false;
                        foreach (Layer c in layers) if (c.layer != null && c.layer.Pointer == l.Pointer) ya = true;
                        if (!ya) layers.Add(new Layer { layer = l, here = mb.enabled.value });
                    }
                    foreach (Camera cam in UnityEngine.Object.FindObjectsOfType<Camera>())
                    {
                        if (cam == null) continue;
                        if (diag != null) diag.AppendLine("Camera '" + cam.gameObject.name + "':");
                        foreach (Behaviour b in cam.GetComponents<Behaviour>())
                        {
                            if (b == null) continue;
                            string n = b.ToString() + " " + b.GetType().Name;
                            if (diag != null) diag.AppendLine("    " + n + (b.enabled ? "" : "  (apagado)"));
                            if (!n.Contains("MotionBlur")) continue;
                            bool ya = false;
                            foreach (Behaviour o in oldOnes) if (o != null && o.Pointer == b.Pointer) ya = true;
                            if (!ya) oldOnes.Add(b);
                        }
                    }
                    if (diag != null)
                    {
                        dump = true;
                        try
                        {
                            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "WorkingVehicles_camera.txt");
                            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                            if (Cfg.Report) File.WriteAllText(filePath, diag.ToString(), Encoding.UTF8);
                        }
                        catch (Exception) { }
                    }
                    int cont = layers.Count * 1000 + oldOnes.Count;
                    if (cont != lastCont)
                    {
                        lastCont = cont;
                        MelonLoader.MelonLogger.Msg("[MotionBlur] post-process layers turned off: " + layers.Count + ", old effects turned off: " + oldOnes.Count);
                    }
                }

                foreach (Layer c in layers)
                    if (c.layer != null) { MotionBlur mb = c.layer.motionBlurSettings; if (mb != null && mb.enabled.value) mb.enabled.value = false; }
                foreach (Behaviour b in oldOnes) if (b != null && b.enabled) b.enabled = false;
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Warning("[MotionBlur] " + e.Message);
                layers.Clear();
                oldOnes.Clear();
            }
        }
    }

    /// <summary>Engine and tire-on-snow sound. Generated by code, no files.</summary>
    internal class Engine
    {
        static AudioClip clipEngine, clipRoll;
        AudioSource srcEngine, srcRoll;
        GameObject go;
        float rpm;
        float starter;

        const int SR = 32000;

        static void Build()
        {
            if (clipEngine != null) return;

            // Engine: a deep, soft "explosion" repeated 24 times per second (idle), with harmonics and small irregularities.
            int n = SR;               // exactly 1 second => the loop closes without cuts
            float[] m = new float[n];
            var rnd = new System.Random(11);
            int explosions = 24;
            float[] jit = new float[explosions];
            for (int i = 0; i < explosions; i++) jit[i] = 0.85f + 0.3f * (float)rnd.NextDouble();
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float phase = t * explosions;
                int k = (int)phase % explosions;
                float local = (phase - Mathf.Floor(phase)) / explosions;   // seconds within the explosion
                float env = Mathf.Exp(-local * 95f);
                float f0 = 48f;
                float v = Mathf.Sin(2f * Mathf.PI * f0 * local) * 0.8f
                        + Mathf.Sin(2f * Mathf.PI * f0 * 2f * local) * 0.45f
                        + Mathf.Sin(2f * Mathf.PI * f0 * 3f * local) * 0.25f
                        + ((float)rnd.NextDouble() * 2f - 1f) * 0.08f;
                float s = v * env * jit[k];
                // A bit of continuous engine (24 Hz, 48 Hz and 96 Hz whole cycles per second: they close the loop)
                s += 0.22f * Mathf.Sin(2f * Mathf.PI * 24f * t) + 0.16f * Mathf.Sin(2f * Mathf.PI * 48f * t + 0.7f) + 0.07f * Mathf.Sin(2f * Mathf.PI * 96f * t + 1.9f);
                lp += (s - lp) * 0.35f;                                    // low-pass filter: sound from inside the cabin
                m[i] = lp;
            }
            Normalize(m, 0.8f);
            clipEngine = AudioClip.Create("WV_Engine", n, 1, SR, false);
            clipEngine.SetData(m, 0);
            clipEngine.hideFlags = HideFlags.DontUnloadUnusedAsset;

            // Rolling on snow: filtered noise that is louder the faster it goes.
            int n2 = SR * 2;
            float[] r = new float[n2];
            float a = 0f, b = 0f;
            for (int i = 0; i < n2; i++)
            {
                float x = (float)rnd.NextDouble() * 2f - 1f;
                a += (x - a) * 0.12f;
                b += (a - b) * 0.25f;
                r[i] = b;
            }
            // Smooth splice so the loop doesn't "click".
            int f = SR / 20;
            for (int i = 0; i < f; i++)
            {
                float w = i / (float)f;
                r[i] = r[i] * w + r[n2 - f + i] * (1f - w);
            }
            Normalize(r, 0.7f);
            clipRoll = AudioClip.Create("WV_Roll", n2 - f, 1, SR, false);
            var cut = new float[n2 - f];
            Array.Copy(r, cut, n2 - f);
            clipRoll.SetData(cut, 0);
            clipRoll.hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        static void Normalize(float[] d, float peak)
        {
            float mx = 0.0001f;
            for (int i = 0; i < d.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(d[i]));
            float g = peak / mx;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
        }

        public void TurnOn(Transform root)
        {
            if (!Settings.Sound()) return;
            Build();
            TurnOff();
            go = new GameObject("WV_Sound");
            go.transform.SetParent(root, false);
            srcEngine = Create(clipEngine);
            srcRoll = Create(clipRoll);
            rpm = 0.25f;
            starter = 0f;
            srcEngine.pitch = 0.5f;
            srcEngine.volume = 0f;
            srcRoll.volume = 0f;
            srcEngine.Play();
            srcRoll.Play();
        }

        AudioSource Create(AudioClip c)
        {
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.spatialBlend = 0.35f;     // heard "inside" the cabin, but somewhat positioned
            s.dopplerLevel = 0f;
            s.minDistance = 3f;
            s.maxDistance = 45f;
            s.rolloffMode = AudioRolloffMode.Linear;
            return s;
        }

        /// <param name="rapidez">0..1 relative to top speed</param>
        /// <param name="acelera">0..1 how hard the accelerator is pressed</param>
        public void Update(float speed, float throttle, float dt)
        {
            if (srcEngine == null) return;
            starter += dt;
            float target = 0.18f + speed * 0.62f + throttle * 0.12f;
            rpm = Mathf.Lerp(rpm, target, 1f - Mathf.Exp(-4f * dt));
            float vol = Settings.VolumeEngine() * Mathf.Clamp01(starter / 1.2f);   // the engine "starts" with a small fade-in
            srcEngine.pitch = Mathf.Lerp(0.55f, 2.4f, rpm);
            srcEngine.volume = vol * Mathf.Lerp(0.55f, 1f, rpm);
            srcRoll.pitch = Mathf.Lerp(0.8f, 1.4f, speed);
            srcRoll.volume = vol * 0.5f * Mathf.Clamp01(speed * 2.2f);
        }

        // ---- starter: the starter motor turns and, if it starts, the engine "catches"; if it fails, it only turns and the attempt dies out
        static AudioClip clipStarterOk, clipStarterFail;
        GameObject goStarter;

        static float Az(System.Random r, float a, float b) { return a + (b - a) * (float)r.NextDouble(); }

        /// <summary>
        /// A mechanical/combustion hit: broadband noise with a fast decay + a low thump + a metallic "tick" of a different pitch each time.
        /// Every hit comes out slightly different (so it doesn't sound like a machine).
        /// </summary>
        static void Hit(float[] a, int sr, float t0, float amp, float f0, float fall, float gloss, System.Random rnd)
        {
            int i0 = (int)(t0 * sr);
            int len = (int)(0.16f * sr);
            float lo = 0f, hi = 0f;
            float kh = 0.38f * gloss + 0.05f, kl = 0.04f;
            float rf = Az(rnd, 900f, 1700f), rph = Az(rnd, 0f, 6.28f), ra = Az(rnd, 0.15f, 0.35f);
            for (int j = 0; j < len; j++)
            {
                int i = i0 + j;
                if (i < 0 || i >= a.Length) continue;
                float loc = j / (float)sr;
                float nz = Az(rnd, -1f, 1f);
                hi += (nz - hi) * kh;
                lo += (nz - lo) * kl;
                float v = (hi - lo) * Mathf.Exp(-loc * fall) * 1.1f;
                v += Mathf.Sin(2f * Mathf.PI * f0 * loc) * Mathf.Exp(-loc * 45f) * 0.8f;
                v += Mathf.Sin(2f * Mathf.PI * rf * loc + rph) * Mathf.Exp(-loc * 160f) * ra;
                a[i] += v * amp;
            }
        }

        static AudioClip MakeStarter(bool ok)
        {
            // Own sound, generated by code (it uses no recorded audio). Structure of a typical start:
            // the starter motor turns and speeds up, the engine "catches" with a strong explosion, a small rev and the idle.
            // If it fails: it turns, coughs and gives up. Broadband noise + variation on every hit so it sounds mechanical and not synthetic.
            float dur = ok ? 1.9f : 2.7f;
            int n = (int)(SR * dur);
            var a = new float[n];
            var rnd = new System.Random(ok ? 31 : 32);

            // pinion "clack" when engaging
            for (int i = 0; i < (int)(0.045f * SR); i++)
            {
                float t = i / (float)SR;
                a[i] += Az(rnd, -1f, 1f) * Mathf.Exp(-t * 100f) * 0.6f;
            }

            float endTurn = ok ? 1.15f : 2.45f;
            float tc = 0.06f;
            while (tc < endTurn)
            {
                float rate;
                if (tc < 1.0f) rate = 9f + 8f * tc;
                else if (!ok && tc > 2.0f) rate = 15f - 10f * Mathf.Min(1f, (tc - 2.0f) / 0.45f);
                else rate = ok ? 17f : 15f + 2f * Mathf.Sin(tc * 5f);
                float amp = 0.5f * (0.55f + 0.45f * Mathf.Min(1f, tc / 0.5f)) * Az(rnd, 0.7f, 1.25f);
                if (ok) amp *= Mathf.Min(1f, (endTurn - tc) / 0.25f) * 0.7f + 0.3f;
                else if (tc > 2.1f) amp *= Mathf.Clamp01((2.5f - tc) / 0.4f);
                Hit(a, SR, tc, amp, Az(rnd, 48f, 64f), Az(rnd, 50f, 85f), 1f, rnd);
                tc += 1f / rate * Az(rnd, 0.88f, 1.12f);
            }

            if (ok)
            {
                Hit(a, SR, 1.02f, 1.5f, 58f, 60f, 1f, rnd);
                float tf = 1.08f;
                while (tf < dur - 0.02f)
                {
                    float k = Mathf.Min(1f, (tf - 1.08f) / 0.45f);
                    float rate = 16f + 8f * k;
                    float flare = 1f + 0.35f * Mathf.Exp(-(tf - 1.08f) * 4f);
                    Hit(a, SR, tf, 0.8f * flare * Az(rnd, 0.75f, 1.2f), Az(rnd, 46f, 56f), Az(rnd, 55f, 90f), 0.55f, rnd);
                    tf += 1f / rate * Az(rnd, 0.9f, 1.1f);
                }
            }
            else
            {
                float[] tt = { 1.05f, 1.48f, 1.75f, 2.12f, 2.34f };
                float[] aa = { 0.8f, 1.0f, 0.6f, 0.8f, 0.45f };
                for (int q = 0; q < tt.Length; q++) Hit(a, SR, tt[q], aa[q], 54f, 65f, 1f, rnd);
            }

            // continuous cushion of mechanical noise (the "roar" between hits)
            float b1 = 0f, b2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float x = Az(rnd, -1f, 1f);
                b1 += (x - b1) * (t < 1.0f ? 0.30f : 0.18f);
                b2 += (x - b2) * 0.03f;
                float g = 0f;
                if (t < endTurn) g = 0.11f * Mathf.Min(1f, t / 0.1f) * (0.6f + 0.4f * Mathf.Min(1f, t));
                if (ok && t > 1.0f) g = Mathf.Max(g, 0.12f * Mathf.Min(1f, (t - 1.0f) / 0.2f));
                a[i] += (b1 - b2) * g;
            }

            // slight smoothing and output
            float f1 = 0f;
            for (int i = 0; i < n; i++)
            {
                f1 += (a[i] - f1) * 0.75f;
                float t = i / (float)SR;
                a[i] = f1 * Mathf.Clamp01((dur - t) / 0.12f);
            }
            Normalize(a, 0.8f);
            var c = AudioClip.Create(ok ? "WV_Starter" : "WV_StarterFail", n, 1, SR, false);
            c.SetData(a, 0);
            c.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return c;
        }

        public void PlayStarter(Transform root, bool ok)
        {
            try
            {
                CutStarter();
                if (!Settings.Sound()) return;
                if (clipStarterOk == null) { clipStarterOk = MakeStarter(true); clipStarterFail = MakeStarter(false); }
                goStarter = new GameObject("WV_Starter");
                goStarter.transform.SetParent(root, false);
                AudioSource s = goStarter.AddComponent<AudioSource>();
                s.clip = ok ? clipStarterOk : clipStarterFail;
                s.loop = false;
                s.spatialBlend = 0.35f;
                s.dopplerLevel = 0f;
                s.minDistance = 3f;
                s.maxDistance = 45f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.volume = Mathf.Clamp01(Settings.VolumeEngine() * 1.1f);
                s.Play();
                UnityEngine.Object.Destroy(goStarter, s.clip.length + 0.3f);
            }
            catch (Exception) { }
        }

        public void CutStarter()
        {
            try { if (goStarter != null) UnityEngine.Object.Destroy(goStarter); } catch (Exception) { }
            goStarter = null;
        }

        public void TurnOff()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            go = null; srcEngine = null; srcRoll = null;
        }
    }

    /// <summary>Writes the whole vehicle hierarchy to a file, to find out which parts exist (steering wheel, snow, etc.).</summary>
    internal static class Hierarchy
    {
        static readonly HashSet<string> made = new HashSet<string>();

        public static void Dump(Transform root)
        {
            if (!made.Add(root.name)) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== " + root.name + " (" + DateTime.Now + ") ===");
                Walk(sb, root, root, 0);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "WorkingVehicles_hierarchy.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                if (Cfg.Report) File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
        }

        static void Walk(StringBuilder sb, Transform t, Transform root, int level)
        {
            sb.Append(new string(' ', level * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (INACTIVO)");
            Renderer r = t.GetComponent<Renderer>();
            if (r != null)
            {
                sb.Append("  [mats: ");
                foreach (Material m in r.sharedMaterials) sb.Append(m != null ? m.name : "null").Append(", ");
                sb.Append("]");
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) sb.Append(" [malla " + mf.sharedMesh.name + " tam " + mf.sharedMesh.bounds.size + "]");
            }
            sb.AppendLine();
            for (int i = 0; i < t.childCount; i++) Walk(sb, t.GetChild(i), root, level + 1);
        }
    }
}
