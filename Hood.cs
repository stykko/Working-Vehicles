using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// Truck hood: opens by rotating the hinge and shows a 4-cylinder engine built from simple shapes
    /// (inline 4, gasoline with fuel injection). The removable parts
    /// (alternator, spark plugs, wires, valve cover, cylinder head, gasket) and the battery are only visible when installed.
    /// </summary>
    internal class Hood
    {
        public Transform root;
        Transform hinge;
        Renderer[] partsHood;
        readonly List<GameObject> engineOriginal = new List<GameObject>();
        GameObject group, batteryGO, boresGO, studsGO, rockersGO;
        readonly Transform[] pz = new Transform[Parts.Count];
        Vector3 posLocal;                  // position of the engine group (in truck space)
        Quaternion rotClosedRel;          // closed rotation of the hinge relative to the truck
        public bool opened;               // the hood is open (the game itself opens it when you hold the click)
        const float AngleOpen = 15f;   // from how many degrees it counts as open

        static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        static Shader shader;
        static bool shaderSearched;
        static Material template;


        // ------------------------------------------------------------------ creation

        public static Hood Create(Transform root, Vector3 frontWorld)
        {
            try
            {
                var c = new Hood { root = root };
                return c.Assemble(frontWorld) ? c : null;
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Warning("[Hood] could not prepare the hood of '" + root.name + "': " + e.Message);
                return null;
            }
        }

        bool Assemble(Vector3 front)
        {
            // Hinge: the one called "...HoodHinge". If missing, the hood piece itself.
            foreach (Transform h in root.GetComponentsInChildren<Transform>(true))
            {
                string n = h.name.ToLowerInvariant();
                if (n.Contains("hoodhinge")) { hinge = h; break; }
            }
            if (hinge == null)
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (n.Contains("hood") && !n.Contains("snow")) { hinge = r.transform.parent != null ? r.transform.parent : r.transform; break; }
                }
            }
            if (hinge == null)
            {
                MelonLoader.MelonLogger.Warning("[Hood] '" + root.name + "': hood not found, there will be no engine.");
                return false;
            }
            partsHood = hinge.GetComponentsInChildren<Renderer>(true);

            // The game's original engine (hidden while ours is shown) and its cavity.
            Bounds? cavity = null;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name.ToLowerInvariant();
                if (!n.Contains("engine")) continue;
                if (r.transform.IsChildOf(hinge)) continue;
                if (cavity == null) cavity = r.bounds; else { Bounds b = cavity.Value; b.Encapsulate(r.bounds); cavity = b; }
            }
            foreach (Transform h in root.GetComponentsInChildren<Transform>(true))
            {
                string n = h.name.ToLowerInvariant();
                if (n.Contains("engine") && n.Contains("prefab") && !n.Contains("sound") && !h.IsChildOf(hinge))
                { engineOriginal.Add(h.gameObject); break; }
            }

            Bounds hb = Limits(partsHood);
            Vector3 centerEngine;
            if (cavity != null) centerEngine = new Vector3(cavity.Value.center.x, cavity.Value.min.y - 0.12f, cavity.Value.center.z);
            else centerEngine = new Vector3(hb.center.x, hb.min.y - 0.35f, hb.center.z);
            posLocal = root.InverseTransformPoint(centerEngine);

            Vector3 f = front; f.y = 0f; f = f.sqrMagnitude < 0.001f ? root.forward : f.normalized;
            rotClosedRel = Quaternion.Inverse(root.rotation) * hinge.rotation;

            Build(root.InverseTransformDirection(f));
            group.SetActive(false);

            MelonLoader.MelonLogger.Msg("[Hood] '" + root.name + "': hinge '" + hinge.name + "', engine in " + posLocal + ", game engine " + (engineOriginal.Count > 0 ? engineOriginal[0].name : "not found"));
            return true;
        }


        static Bounds Limits(Renderer[] rs)
        {
            Bounds b = new Bounds();
            bool first = true;
            foreach (Renderer r in rs)
            {
                if (r == null) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            if (first) b = new Bounds(Vector3.zero, Vector3.one);
            return b;
        }

        // ------------------------------------------------------------------ state

        /// <summary>Every frame: checks whether the game opened the hood and, if so, shows the engine (and hides the original).</summary>
        public void Update()
        {
            if (hinge == null || root == null || group == null) return;
            Quaternion rel = Quaternion.Inverse(root.rotation) * hinge.rotation;
            opened = Quaternion.Angle(rel, rotClosedRel) > AngleOpen;

            if (group.activeSelf != opened)
            {
                group.SetActive(opened);
                foreach (GameObject g in engineOriginal) if (g != null) g.SetActive(!opened);
            }
            if (opened)
            {
                // Fine adjustments from Mod Options (engine height and size)
                group.transform.localPosition = posLocal + new Vector3(0f, Settings.HeightEngine(), 0f);
                float k = Settings.SizeEngine();
                group.transform.localScale = new Vector3(k, k, k);
            }
        }

        public Vector3 PointHood()
        {
            return partsHood != null && partsHood.Length > 0 ? Limits(partsHood).center : hinge.position;
        }

        public Vector3 PointEngine()
        {
            return root.TransformPoint(posLocal + new Vector3(0f, 0.25f + Settings.HeightEngine(), 0f));
        }

        /// <summary>Shows only what is installed. 'cond' &lt; 0 = the part is not there.</summary>
        public void Apply(float[] condition, bool battery)
        {
            if (group == null) return;
            for (int i = 0; i < Parts.Count; i++)
                if (pz[i] != null) pz[i].gameObject.SetActive(condition[i] >= 0f);
            if (batteryGO != null) batteryGO.SetActive(battery);
            if (boresGO != null) boresGO.SetActive(condition[Parts.Gasket] < 0f);
            if (studsGO != null) studsGO.SetActive(condition[Parts.Head] < 0f);
            if (rockersGO != null) rockersGO.SetActive(condition[Parts.Head] >= 0f && condition[Parts.Cap] < 0f);
        }

        // ------------------------------------------------------------------ colors

        static readonly Color black = new Color(0.07f, 0.07f, 0.08f);
        static readonly Color block = new Color(0.17f, 0.18f, 0.19f);
        static readonly Color headC = new Color(0.24f, 0.25f, 0.26f);
        static readonly Color silver = new Color(0.55f, 0.56f, 0.58f);
        static readonly Color aluminum = new Color(0.40f, 0.41f, 0.43f);
        static readonly Color rust = new Color(0.34f, 0.20f, 0.12f);
        static readonly Color blue = new Color(0.10f, 0.22f, 0.52f);
        static readonly Color rubber = new Color(0.05f, 0.05f, 0.05f);
        static readonly Color copper = new Color(0.60f, 0.35f, 0.15f);
        static readonly Color red = new Color(0.55f, 0.06f, 0.06f);
        static readonly Color alum = new Color(0.56f, 0.57f, 0.59f);         // cast aluminum (cylinder head)
        static readonly Color iron = new Color(0.14f, 0.14f, 0.15f);       // dark cast iron
        static readonly Color rust2 = new Color(0.27f, 0.17f, 0.12f);       // rustier casting
        static readonly Color plastLight = new Color(0.80f, 0.79f, 0.72f);   // light plastic of the fan

        // ------------------------------------------------------------------ the engine

        void Build(Vector3 frontLocal)
        {
            group = new GameObject("WV_Engine");
            group.transform.SetParent(root, false);
            group.transform.localPosition = posLocal;
            group.transform.localRotation = Quaternion.LookRotation(frontLocal.sqrMagnitude > 0.001f ? frontLocal : Vector3.forward, Vector3.up);
            Transform g = group.transform;

            KX = WidthCore;
            try
            {
            Locked(g);
            Details(g);
            MoreDetails(g);
            Firewall(g);
            for (int i = 0; i < Parts.Count; i++)
            {
                var t = new GameObject(Parts.Names[i]).transform;
                t.SetParent(g, false);
                pz[i] = t;
            }
            Gasket(pz[Parts.Gasket]);
            Head(pz[Parts.Head], false);
            Cap(pz[Parts.Cap]);
            SparkPlugs(pz[Parts.SparkPlugs]);
            Cables(pz[Parts.Cables], false);
            Alternator(pz[Parts.Alternator], false);
            Internal(g);
            }
            finally { KX = 1f; }
            Battery(g);
        }

        static float Zc(int i) { return -0.21f + i * 0.14f; }       // position of each cylinder

        // ---- what cannot be removed (inline-4 engine: narrow, long iron block, exhaust on the right, intake on the left)
        static void Locked(Transform g)
        {
            Cube(g, "Piso", new Vector3(0f, 0.01f, -0.06f), new Vector3(1.6f, 0.02f, 1.42f), new Color(0.06f, 0.06f, 0.07f));

            // stamped sheet-metal oil pan with flange and plug
            RCube(g, "Carter", new Vector3(0f, 0.10f, -0.02f), new Vector3(0.34f, 0.10f, 0.52f), black);
            Cyl(g, "CarterBajo", new Vector3(0f, 0.07f, -0.02f), 0.30f, 0.50f, 'z', black);
            RCube(g, "BridaCarter", new Vector3(0f, 0.155f, 0f), new Vector3(0.44f, 0.02f, 0.60f), iron);
            Cyl(g, "TaponCarter", new Vector3(0f, 0.012f, -0.10f), 0.04f, 0.03f, 'y', silver);
            for (int i = 0; i < 8; i++)
            {
                Stud(g, "PernoCarter", new Vector3(0.21f, 0.168f, -0.27f + i * 0.077f), 0.02f, 0.016f, 'y', silver);
                Stud(g, "PernoCarter", new Vector3(-0.21f, 0.168f, -0.27f + i * 0.077f), 0.02f, 0.016f, 'y', silver);
            }

            // iron block: flat side faces with vertical ribs, water plugs and bosses
            RCube(g, "Bloque", new Vector3(0f, 0.28f, 0f), new Vector3(0.39f, 0.24f, 0.60f), block);
            RCube(g, "FaldaBloque", new Vector3(0f, 0.185f, 0f), new Vector3(0.43f, 0.05f, 0.62f), block);
            RCube(g, "CubiertaSuperior", new Vector3(0f, 0.3965f, 0f), new Vector3(0.42f, 0.007f, 0.62f), iron);
            foreach (float sx in new[] { -1f, 1f })
            {
                for (int i = 0; i < 5; i++)
                    RCube(g, "NervioBloque", new Vector3(sx * 0.203f, 0.285f, -0.28f + i * 0.14f), new Vector3(0.016f, 0.21f, 0.022f), iron);
                for (int i = 0; i < 4; i++)
                    Cyl(g, "TaponAgua", new Vector3(sx * 0.208f, 0.33f, Zc(i)), 0.045f, 0.014f, 'x', rust2);
                RCube(g, "RefuerzoInferior", new Vector3(sx * 0.206f, 0.215f, 0f), new Vector3(0.014f, 0.035f, 0.56f), iron);
            }
            Cyl(g, "SensorPresion", new Vector3(0.215f, 0.25f, -0.05f), 0.025f, 0.04f, 'x', silver);

            // bell housing (round block flange with the starter notch)
            Rev(g, "BridaTrasera", Profile(0f, 0f, 0.205f, 0f, 0.205f, 0.030f, 0.190f, 0.036f, 0.190f, 0.090f, 0f, 0.090f), 40, new Vector3(0f, 0.30f, -0.345f), 'z', iron);
            Cyl(g, "Campana", new Vector3(0f, 0.30f, -0.45f), 0.38f, 0.12f, 'z', headC);
            Cyl(g, "BridaCampana", new Vector3(0f, 0.30f, -0.51f), 0.42f, 0.02f, 'z', aluminum);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Stud(g, "PernoCampana", new Vector3(Mathf.Cos(a) * 0.195f, 0.30f + Mathf.Sin(a) * 0.195f, -0.335f), 0.022f, 0.02f, 'z', silver);
            }
            RCube(g, "MuescaArranque", new Vector3(0.20f, 0.18f, -0.40f), new Vector3(0.09f, 0.10f, 0.12f), iron);

            // stamped timing cover, crankshaft pulley with damper, water pump and clutch fan
            RCube(g, "TapaDistribucion", new Vector3(0f, 0.27f, 0.315f), new Vector3(0.38f, 0.34f, 0.03f), aluminum);
            Cyl(g, "AbombadoTapa", new Vector3(0f, 0.20f, 0.335f), 0.17f, 0.012f, 'z', aluminum);
            Rev(g, "CrankPulley", Profile(0f, 0f, 0.088f, 0f, 0.088f, 0.012f, 0.078f, 0.014f, 0.088f, 0.016f, 0.088f, 0.024f, 0.078f, 0.026f, 0.088f, 0.028f, 0.088f, 0.040f, 0f, 0.040f), 40, new Vector3(0f, 0.20f, 0.345f), 'z', black);
            Rev(g, "CrankDamper", Profile(0f, 0f, 0.068f, 0f, 0.068f, 0.014f, 0f, 0.014f), 32, new Vector3(0f, 0.20f, 0.382f), 'z', silver);
            Stud(g, "TornilloPolea", new Vector3(0f, 0.20f, 0.405f), 0.034f, 0.02f, 'z', silver);
            RCube(g, "MarcaDistribucion", new Vector3(0f, 0.288f, 0.37f), new Vector3(0.008f, 0.014f, 0.006f), new Color(0.9f, 0.9f, 0.8f));
            Cyl(g, "BombaAgua", new Vector3(0f, 0.43f, 0.335f), 0.13f, 0.05f, 'z', aluminum);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                Stud(g, "PernoBombaAgua", new Vector3(Mathf.Cos(a) * 0.058f, 0.43f + Mathf.Sin(a) * 0.058f, 0.355f), 0.014f, 0.012f, 'z', silver);
            }
            Rev(g, "PoleaAgua", Profile(0f, 0f, 0.060f, 0f, 0.060f, 0.006f, 0.052f, 0.008f, 0.060f, 0.010f, 0.060f, 0.022f, 0f, 0.022f), 28, new Vector3(0f, 0.43f, 0.365f), 'z', black);
            Rev(g, "Embrague", Profile(0f, 0f, 0.052f, 0f, 0.052f, 0.040f, 0.040f, 0.050f, 0f, 0.050f), 28, new Vector3(0f, 0.43f, 0.385f), 'z', new Color(0.12f, 0.12f, 0.13f));
            ObjMesh(g, "EspiralEmbrague", Geo.Torus(0.026f, 0.004f, 24, 6), new Vector3(0f, 0.43f, 0.438f), RotAxis('z'), silver);
            ObjMesh(g, "EspiralEmbrague2", Geo.Torus(0.014f, 0.003f, 20, 6), new Vector3(0f, 0.43f, 0.438f), RotAxis('z'), silver);

            KX = 1f;   // fan and radiator: full width
            // 7-blade light plastic fan, with the clutch hub
            Rev(g, "CuboVentilador", Profile(0f, 0f, 0.050f, 0f, 0.050f, 0.012f, 0.030f, 0.020f, 0f, 0.020f), 24, new Vector3(0f, 0.43f, 0.436f), 'z', plastLight);
            for (int i = 0; i < 7; i++)
            {
                var pivot = new GameObject("PivoteAspa").transform;
                pivot.SetParent(g, false);
                pivot.localPosition = new Vector3(0f, 0.43f, 0.448f);
                pivot.localRotation = Quaternion.Euler(0f, 0f, i * (360f / 7f));
                GameObject blade = Cube(pivot, "Aspa", new Vector3(0f, 0.125f, 0f), new Vector3(0.075f, 0.20f, 0.006f), plastLight);
                blade.transform.localRotation = Quaternion.Euler(0f, 22f, 0f);
                GameObject tip = Cube(pivot, "PuntaAspa", new Vector3(0f, 0.215f, 0f), new Vector3(0.060f, 0.040f, 0.006f), plastLight);
                tip.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            }
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2f / 18f;
                GameObject rim = Cube(g, "CubiertaVentilador", new Vector3(Mathf.Cos(a) * 0.255f, 0.43f + Mathf.Sin(a) * 0.255f, 0.50f), new Vector3(0.10f, 0.018f, 0.06f), new Color(0.05f, 0.05f, 0.06f));
                rim.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            }

            // radiator: core, fins, tanks, cap
            Cube(g, "Radiador", new Vector3(0f, 0.36f, 0.60f), new Vector3(0.74f, 0.50f, 0.07f), new Color(0.09f, 0.09f, 0.10f));
            for (int i = 0; i < 18; i++)
                Cube(g, "AletaRadiador", new Vector3(-0.34f + i * 0.04f, 0.36f, 0.638f), new Vector3(0.008f, 0.48f, 0.006f), new Color(0.26f, 0.26f, 0.27f));
            Cube(g, "DepositoIzq", new Vector3(-0.39f, 0.36f, 0.60f), new Vector3(0.06f, 0.54f, 0.10f), black);
            Cube(g, "DepositoDer", new Vector3(0.39f, 0.36f, 0.60f), new Vector3(0.06f, 0.54f, 0.10f), black);
            Cyl(g, "TaponRadiador", new Vector3(0.30f, 0.64f, 0.60f), 0.055f, 0.04f, 'y', silver);
            Cyl(g, "CuelloRadiador", new Vector3(0.30f, 0.62f, 0.60f), 0.04f, 0.04f, 'y', black);
            TubeO(g, "MangueraSuperior", new[] { new Vector3(-0.39f, 0.58f, 0.58f), new Vector3(-0.22f, 0.60f, 0.46f), new Vector3(-0.08f, 0.58f, 0.38f), new Vector3(-0.04f, 0.55f, 0.355f) }, 0.030f, rubber);
            TubeO(g, "MangueraInferior", new[] { new Vector3(0.39f, 0.16f, 0.58f), new Vector3(0.20f, 0.20f, 0.48f), new Vector3(0.06f, 0.34f, 0.40f), new Vector3(0.02f, 0.40f, 0.36f) }, 0.030f, rubber);
            for (int i = 0; i < 2; i++)
            {
                Cyl(g, "AbrazaderaManguera", new Vector3(-0.38f + i * 0.30f, 0.58f + i * 0.016f, 0.57f - i * 0.11f), 0.045f, 0.012f, 'x', silver);
            }
            KX = WidthCore;

            // filters, starter, brake booster, engine mounts
            Cyl(g, "FiltroAceite", new Vector3(0.26f, 0.15f, 0.12f), 0.085f, 0.13f, 'y', blue);
            Cyl(g, "BaseFiltroAceite", new Vector3(0.26f, 0.08f, 0.12f), 0.10f, 0.02f, 'y', aluminum);
            Cyl(g, "FiltroCombustible", new Vector3(-0.46f, 0.28f, -0.10f), 0.07f, 0.16f, 'y', new Color(0.55f, 0.55f, 0.50f));
            RCube(g, "SoporteFiltroComb", new Vector3(-0.46f, 0.20f, -0.10f), new Vector3(0.09f, 0.03f, 0.09f), black);
            Cyl(g, "Arranque", new Vector3(0.26f, 0.14f, -0.22f), 0.09f, 0.20f, 'z', new Color(0.15f, 0.15f, 0.16f));
            Cyl(g, "Solenoide", new Vector3(0.26f, 0.22f, -0.19f), 0.05f, 0.12f, 'z', new Color(0.12f, 0.12f, 0.13f));
            Cyl(g, "ServoFreno", new Vector3(-0.30f, 0.52f, -0.62f), 0.22f, 0.12f, 'z', black);
            Cyl(g, "CilindroMaestro", new Vector3(-0.30f, 0.52f, -0.53f), 0.05f, 0.12f, 'z', silver);
            Cube(g, "DepositoFreno", new Vector3(-0.30f, 0.575f, -0.51f), new Vector3(0.06f, 0.05f, 0.09f), new Color(0.70f, 0.68f, 0.55f));
            foreach (float sx in new[] { -1f, 1f })
            {
                Cube(g, "ApoyoMotor", new Vector3(sx * 0.28f, 0.17f, 0f), new Vector3(0.10f, 0.06f, 0.14f), rubber);
                Cube(g, "SoporteApoyo", new Vector3(sx * 0.37f, 0.13f, 0f), new Vector3(0.12f, 0.03f, 0.14f), block);
            }
            // oil dipstick
            Cable(g, new Vector3(0.22f, 0.24f, 0.18f), new Vector3(0.22f, 0.50f, 0.18f), 0.004f, new Color(0.7f, 0.6f, 0.2f));
            Cyl(g, "AroVarilla", new Vector3(0.22f, 0.51f, 0.18f), 0.03f, 0.006f, 'x', new Color(0.9f, 0.75f, 0.2f));
            Cyl(g, "TuboVarilla", new Vector3(0.22f, 0.36f, 0.18f), 0.014f, 0.30f, 'y', silver);

            // air filter box (on the left) with airflow meter and air intake
            RCube(g, "CajaFiltroAire", new Vector3(-0.54f, 0.58f, -0.14f), new Vector3(0.22f, 0.16f, 0.36f), new Color(0.07f, 0.07f, 0.08f));
            RCube(g, "TapaFiltroAire", new Vector3(-0.54f, 0.668f, -0.14f), new Vector3(0.235f, 0.022f, 0.375f), iron);
            foreach (float sx in new[] { -1f, 1f })
                for (int i = 0; i < 3; i++)
                    Cyl(g, "BrocheFiltroAire", new Vector3(-0.54f + sx * 0.108f, 0.655f, -0.26f + i * 0.12f), 0.018f, 0.02f, 'y', silver);
            RCube(g, "MedidorCaudal", new Vector3(-0.54f, 0.58f, -0.355f), new Vector3(0.14f, 0.13f, 0.08f), iron);
            RCube(g, "ConectorMedidor", new Vector3(-0.45f, 0.61f, -0.355f), new Vector3(0.03f, 0.05f, 0.05f), black);
            TubeO(g, "TomaAire", new[] { new Vector3(-0.54f, 0.58f, 0.04f), new Vector3(-0.56f, 0.60f, 0.22f), new Vector3(-0.50f, 0.60f, 0.42f) }, 0.045f, rubber);

            // distributor on the rear of the cylinder head, and coil
            Cyl(g, "CuerpoDistribuidor", new Vector3(0.12f, 0.505f, -0.355f), 0.065f, 0.10f, 'y', aluminum);
            Cyl(g, "BaseDistribuidor", new Vector3(0.12f, 0.45f, -0.345f), 0.085f, 0.03f, 'z', aluminum);
            Cyl(g, "CapsulaVacio", new Vector3(0.19f, 0.50f, -0.37f), 0.035f, 0.07f, 'z', silver);
            Cyl(g, "Bobina", new Vector3(0.03f, 0.58f, -0.40f), 0.05f, 0.10f, 'y', new Color(0.05f, 0.05f, 0.06f));
            Cyl(g, "TorreBobina", new Vector3(0.03f, 0.64f, -0.40f), 0.022f, 0.025f, 'y', new Color(0.10f, 0.10f, 0.11f));
            RCube(g, "SoporteBobina", new Vector3(0.03f, 0.53f, -0.40f), new Vector3(0.07f, 0.014f, 0.07f), iron);
        }

        // ---- firewall (the back of the engine bay, which would otherwise show the cabin), gearbox and accessories
        static void Firewall(Transform g)
        {
            float kx0 = KX;
            KX = 1f;
            Color sheet = new Color(0.10f, 0.11f, 0.12f);
            Color insulator = new Color(0.045f, 0.045f, 0.05f);
            Color pl = new Color(0.14f, 0.14f, 0.15f);
            // main sheet covering the full width and height of the opening, with the upper step (covers the window frame)
            Cube(g, "ParedFuego", new Vector3(0f, 0.52f, -0.725f), new Vector3(1.64f, 1.06f, 0.03f), sheet);
            // insulating pad, stamping ribs and sealant bead
            RCube(g, "AislanteCentral", new Vector3(-0.05f, 0.50f, -0.705f), new Vector3(1.10f, 0.70f, 0.014f), insulator);
            for (int i = 0; i < 3; i++)
                RCube(g, "NervioPared", new Vector3(0f, 0.20f + i * 0.34f, -0.708f), new Vector3(1.55f, 0.022f, 0.012f), pl);
            for (int i = 0; i < 4; i++)
                RCube(g, "NervioParedV", new Vector3(-0.60f + i * 0.40f, 0.50f, -0.708f), new Vector3(0.022f, 0.90f, 0.012f), pl);
            // heater box with its two pipes and rubber grommet
            RCube(g, "CajonCalefaccion", new Vector3(0.38f, 0.56f, -0.665f), new Vector3(0.34f, 0.30f, 0.07f), black);
            Cyl(g, "CanoCalefA", new Vector3(-0.20f, 0.66f, -0.69f), 0.040f, 0.060f, 'z', silver);
            Cyl(g, "CanoCalefB", new Vector3(0.02f, 0.66f, -0.69f), 0.040f, 0.060f, 'z', silver);
            Cyl(g, "PasacablesA", new Vector3(-0.22f, 0.58f, -0.700f), 0.07f, 0.020f, 'z', rubber);
            Cyl(g, "PasacablesB", new Vector3(-0.38f, 0.50f, -0.700f), 0.05f, 0.020f, 'z', rubber);
            Cyl(g, "PasacablesC", new Vector3(-0.42f, 0.52f, -0.700f), 0.04f, 0.020f, 'z', rubber);
            // windshield wiper: motor and shaft
            Cyl(g, "MotorLimpia", new Vector3(0.62f, 0.84f, -0.66f), 0.09f, 0.11f, 'x', new Color(0.12f, 0.12f, 0.13f));
            Cyl(g, "EjeLimpia", new Vector3(0.40f, 0.86f, -0.66f), 0.012f, 0.18f, 'y', silver);
            // fuse and relay box on the left fender
            RCube(g, "CajaRele", new Vector3(-0.70f, 0.56f, -0.55f), new Vector3(0.16f, 0.12f, 0.24f), black);
            RCube(g, "TapaCajaRele", new Vector3(-0.70f, 0.625f, -0.55f), new Vector3(0.17f, 0.012f, 0.25f), pl);
            for (int i = 0; i < 6; i++)
                RCube(g, "Rele", new Vector3(-0.70f + (i % 2 - 0.5f) * 0.06f, 0.65f, -0.62f + (i / 2) * 0.065f), new Vector3(0.04f, 0.035f, 0.05f), new Color(0.16f, 0.16f, 0.18f));
            // gearbox and mount (behind the bell housing)
            Cyl(g, "CajaCambios", new Vector3(0f, 0.30f, -0.615f), 0.30f, 0.20f, 'z', headC);
            Cyl(g, "AroCajaCambios", new Vector3(0f, 0.30f, -0.52f), 0.34f, 0.02f, 'z', aluminum);
            RCube(g, "TravesanoCaja", new Vector3(0f, 0.12f, -0.60f), new Vector3(0.50f, 0.04f, 0.10f), pl);
            // coolant expansion tank and washer fluid reservoir
            RCube(g, "DepositoExpansion", new Vector3(0.48f, 0.60f, 0.47f), new Vector3(0.10f, 0.18f, 0.07f), new Color(0.78f, 0.78f, 0.70f));
            Cyl(g, "TaponExpansion", new Vector3(0.48f, 0.70f, 0.47f), 0.05f, 0.02f, 'y', new Color(0.15f, 0.15f, 0.16f));
            Cable(g, new Vector3(0.48f, 0.52f, 0.47f), new Vector3(0.38f, 0.60f, 0.58f), 0.010f, rubber);
            RCube(g, "DepositoLavaparabrisas", new Vector3(-0.62f, 0.48f, 0.45f), new Vector3(0.14f, 0.22f, 0.10f), new Color(0.70f, 0.72f, 0.74f));
            Cyl(g, "TaponLavaparabrisas", new Vector3(-0.62f, 0.60f, 0.45f), 0.05f, 0.02f, 'y', new Color(0.12f, 0.20f, 0.45f));
            KX = kx0;
        }

        // ---- more detail on the engine: hardware, sensors, tensioner, clamps
        static void MoreDetails(Transform g)
        {
            Color connector = new Color(0.07f, 0.07f, 0.08f);
            // bolts of the timing cover and of the steering pump
            for (int i = 0; i < 6; i++)
                Stud(g, "PernoDistribucion", new Vector3(-0.15f + (i % 3) * 0.15f, 0.12f + (i / 3) * 0.30f, 0.335f), 0.02f, 0.016f, 'z', silver);
            // belt tensioner with its idler pulley
            Cyl(g, "BrazoTensor", new Vector3(-0.16f, 0.34f, 0.36f), 0.018f, 0.16f, 'y', black);
            Cyl(g, "PoleaTensor", new Vector3(-0.16f, 0.27f, 0.372f), 0.060f, 0.020f, 'z', black);
            Stud(g, "TornilloTensor", new Vector3(-0.16f, 0.27f, 0.392f), 0.020f, 0.014f, 'z', silver);
            // sensors with their connectors: water temperature, oil pressure, choke
            Cyl(g, "SensorTemperatura", new Vector3(0.04f, 0.565f, 0.30f), 0.018f, 0.040f, 'y', copper);
            RCube(g, "FichaTemperatura", new Vector3(0.04f, 0.600f, 0.30f), new Vector3(0.026f, 0.020f, 0.030f), connector);
            Cyl(g, "SensorAceite", new Vector3(0.213f, 0.30f, 0.20f), 0.026f, 0.034f, 'x', silver);
            RCube(g, "FichaAceite", new Vector3(0.236f, 0.30f, 0.20f), new Vector3(0.020f, 0.022f, 0.030f), connector);
            // oil filter ridges and mount
            for (int i = 0; i < 4; i++)
                Cyl(g, "EstriaFiltro", new Vector3(0.26f, 0.105f + i * 0.030f, 0.12f), 0.088f, 0.006f, 'y', new Color(0.07f, 0.16f, 0.40f));
            // engine ground strap to the body
            TubeO(g, "TrenzaMasa", new[] { new Vector3(-0.21f, 0.34f, 0.12f), new Vector3(-0.32f, 0.26f, 0.20f), new Vector3(-0.50f, 0.30f, 0.12f) }, 0.007f, new Color(0.25f, 0.25f, 0.26f));
            // clamps on the radiator and heater hoses
            for (int i = 0; i < 4; i++)
            {
                Cyl(g, "AbrazaderaRadiador", new Vector3(-0.30f + i * 0.02f, 0.59f - i * 0.01f, 0.50f - i * 0.05f), 0.050f, 0.010f, 'x', silver);
            }
            // hood rods and supports, oil pan bolts and hydraulic clutch housing
            Cyl(g, "CilindroEmbrague", new Vector3(-0.22f, 0.20f, -0.46f), 0.035f, 0.10f, 'z', silver);
            TubeO(g, "TuboEmbrague", new[] { new Vector3(-0.22f, 0.20f, -0.51f), new Vector3(-0.36f, 0.22f, -0.60f), new Vector3(-0.40f, 0.30f, -0.69f) }, 0.006f, silver);
            // steering tube and pump mount bolts
            for (int i = 0; i < 3; i++)
                Stud(g, "PernoBombaDir", new Vector3(0.14f + i * 0.04f, 0.37f, 0.36f), 0.014f, 0.012f, 'z', silver);
        }

        // ---- details attached to the engine: vacuum hoses, wiring, steering, hardware
        static void Details(Transform g)
        {
            Color plastic = new Color(0.14f, 0.14f, 0.15f);
            Color cableYellow = new Color(0.65f, 0.55f, 0.10f), cableNeg = new Color(0.04f, 0.04f, 0.04f), cableBlue = new Color(0.12f, 0.20f, 0.45f);

            // power steering pump with its pulley and reservoir
            Cyl(g, "BombaDireccion", new Vector3(0.18f, 0.31f, 0.40f), 0.09f, 0.10f, 'z', aluminum);
            Cyl(g, "PoleaDireccion", new Vector3(0.18f, 0.31f, 0.455f), 0.07f, 0.02f, 'z', black);
            Cyl(g, "DepositoDireccion", new Vector3(0.18f, 0.40f, 0.40f), 0.06f, 0.08f, 'y', plastic);
            Cable(g, new Vector3(0.18f, 0.29f, 0.36f), new Vector3(0.36f, 0.14f, 0.22f), 0.012f, rubber);
            Cable(g, new Vector3(0.20f, 0.33f, 0.36f), new Vector3(0.38f, 0.18f, 0.16f), 0.012f, rubber);

            // heater hoses with clamps
            TubeO(g, "MangueraCalefaccion", new[] { new Vector3(-0.05f, 0.53f, 0.36f), new Vector3(-0.12f, 0.66f, 0.20f), new Vector3(-0.14f, 0.70f, -0.30f), new Vector3(-0.20f, 0.66f, -0.67f) }, 0.016f, rubber);
            TubeO(g, "MangueraCalefaccion2", new[] { new Vector3(0.10f, 0.40f, 0.20f), new Vector3(0.12f, 0.50f, -0.10f), new Vector3(0.10f, 0.60f, -0.40f), new Vector3(0.02f, 0.66f, -0.67f) }, 0.016f, rubber);
            for (int i = 0; i < 2; i++)
                Cyl(g, "AbrazaderaManguera", new Vector3(-0.07f, 0.53f, 0.35f - i * 0.02f), 0.035f, 0.012f, 'z', silver);

            // wiring harness along the left side, with clips
            Cable(g, new Vector3(-0.26f, 0.32f, 0.35f), new Vector3(-0.26f, 0.34f, -0.45f), 0.014f, cableNeg);
            Cable(g, new Vector3(-0.26f, 0.34f, -0.45f), new Vector3(-0.22f, 0.58f, -0.69f), 0.014f, cableNeg);
            Cable(g, new Vector3(-0.25f, 0.36f, 0.30f), new Vector3(-0.25f, 0.37f, -0.40f), 0.006f, cableYellow);
            Cable(g, new Vector3(-0.25f, 0.37f, 0.10f), new Vector3(-0.22f, 0.25f, 0.18f), 0.006f, cableBlue);
            for (int i = 0; i < 6; i++)
                Cube(g, "GrapaCable", new Vector3(-0.26f, 0.33f, 0.30f - i * 0.14f), new Vector3(0.025f, 0.03f, 0.02f), plastic);

            // ground and starter cable
            Cable(g, new Vector3(0.45f, 0.30f, 0.20f), new Vector3(0.20f, 0.20f, 0.00f), 0.012f, cableNeg);
            Cable(g, new Vector3(0.30f, 0.14f, -0.20f), new Vector3(0.45f, 0.38f, 0.28f), 0.016f, red);

            // vacuum hoses: from the intake manifold to the distributor, the brake booster and the valves
            TubeO(g, "VacioDistribuidor", new[] { new Vector3(-0.34f, 0.69f, 0.10f), new Vector3(-0.28f, 0.74f, -0.05f), new Vector3(-0.05f, 0.76f, -0.25f), new Vector3(0.10f, 0.60f, -0.34f), new Vector3(0.19f, 0.52f, -0.36f) }, 0.005f, cableNeg);
            TubeO(g, "VacioServo", new[] { new Vector3(-0.38f, 0.64f, -0.10f), new Vector3(-0.44f, 0.72f, -0.30f), new Vector3(-0.38f, 0.70f, -0.50f), new Vector3(-0.30f, 0.58f, -0.56f) }, 0.009f, rubber);
            TubeO(g, "VacioValvula", new[] { new Vector3(-0.34f, 0.69f, 0.18f), new Vector3(-0.44f, 0.70f, 0.24f), new Vector3(-0.48f, 0.62f, 0.30f) }, 0.005f, cableNeg);
            TubeO(g, "VacioRegulador", new[] { new Vector3(-0.26f, 0.60f, 0.27f), new Vector3(-0.30f, 0.70f, 0.20f), new Vector3(-0.34f, 0.69f, 0.14f) }, 0.005f, cableBlue);
            TubeO(g, "VacioCanister", new[] { new Vector3(-0.34f, 0.69f, -0.02f), new Vector3(-0.46f, 0.72f, 0.0f), new Vector3(-0.56f, 0.70f, 0.14f) }, 0.006f, cableNeg);
            RCube(g, "ArbolVacio", new Vector3(-0.30f, 0.735f, -0.06f), new Vector3(0.03f, 0.02f, 0.05f), plastic);

            // fuel line from the rail to the firewall
            TubeO(g, "CombustibleEntrada", new[] { new Vector3(-0.26f, 0.585f, -0.25f), new Vector3(-0.30f, 0.62f, -0.40f), new Vector3(-0.36f, 0.52f, -0.69f) }, 0.007f, new Color(0.10f, 0.10f, 0.10f));
            TubeO(g, "CombustibleRetorno", new[] { new Vector3(-0.26f, 0.585f, 0.25f), new Vector3(-0.28f, 0.66f, 0.10f), new Vector3(-0.40f, 0.64f, -0.30f), new Vector3(-0.42f, 0.50f, -0.69f) }, 0.007f, new Color(0.10f, 0.10f, 0.10f));

            // throttle linkage (cable to the pedal)
            TubeO(g, "CableAcelerador", new[] { new Vector3(-0.40f, 0.675f, -0.31f), new Vector3(-0.44f, 0.72f, -0.40f), new Vector3(-0.38f, 0.76f, -0.69f) }, 0.004f, aluminum);

            // data plate, lifting hooks
            RCube(g, "PlacaMotor", new Vector3(0f, 0.31f, 0.335f), new Vector3(0.10f, 0.04f, 0.004f), new Color(0.75f, 0.75f, 0.70f));
            Cyl(g, "GanchoLevante", new Vector3(-0.17f, 0.57f, -0.28f), 0.05f, 0.02f, 'z', silver);
            Cyl(g, "GanchoLevante", new Vector3(0.17f, 0.57f, 0.30f), 0.05f, 0.02f, 'z', silver);
        }

        // ---- head gasket: sheet with reinforcement rings, cylinder, water, oil and bolt holes
        static void Gasket(Transform t)
        {
            Color sheet = new Color(0.52f, 0.47f, 0.41f);
            Color edge = new Color(0.20f, 0.17f, 0.14f);
            RCube(t, "JuntaCulata", new Vector3(0f, 0.404f, 0f), new Vector3(0.40f, 0.008f, 0.60f), sheet);
            RCube(t, "BordeJunta", new Vector3(0f, 0.4025f, 0f), new Vector3(0.41f, 0.004f, 0.61f), edge);
            for (int i = 0; i < 4; i++)
            {
                ObjMesh(t, "AroCilindro", Geo.Torus(0.054f, 0.0035f, 28, 8), new Vector3(0f, 0.4085f, Zc(i)), Quaternion.identity, new Color(0.62f, 0.60f, 0.56f));
                Cyl(t, "AgujeroJunta", new Vector3(0f, 0.4082f, Zc(i)), 0.098f, 0.002f, 'y', new Color(0.03f, 0.03f, 0.03f));
            }
            for (int i = 0; i < 5; i++)
            {
                Cyl(t, "AgujeroPerno", new Vector3(0.15f, 0.4085f, -0.28f + i * 0.14f), 0.026f, 0.002f, 'y', new Color(0.03f, 0.03f, 0.03f));
                Cyl(t, "AgujeroPerno", new Vector3(-0.15f, 0.4085f, -0.28f + i * 0.14f), 0.026f, 0.002f, 'y', new Color(0.03f, 0.03f, 0.03f));
            }
            for (int i = 0; i < 3; i++)
            {
                float z = -0.14f + i * 0.14f;
                Cyl(t, "AgujeroAgua", new Vector3(0.10f, 0.4085f, z), 0.016f, 0.002f, 'y', new Color(0.03f, 0.03f, 0.03f));
                Cyl(t, "AgujeroAgua", new Vector3(-0.10f, 0.4085f, z), 0.016f, 0.002f, 'y', new Color(0.03f, 0.03f, 0.03f));
            }
            Cyl(t, "AgujeroAceite", new Vector3(0f, 0.4085f, 0.27f), 0.012f, 0.002f, 'y', new Color(0.03f, 0.03f, 0.03f));
        }

        // ---- aluminum cylinder head with the intake manifold (left: runners, plenum, throttle body and injectors)
        //      and the cast iron exhaust manifold (right: ports joined in pairs, heat shield and pipe)
        static void Head(Transform t, bool prop)
        {
            RCube(t, "Culata", new Vector3(0f, 0.475f, 0f), new Vector3(0.40f, 0.13f, 0.60f), alum);
            // combustion chambers: round recesses with a ring and the valves, on the two large faces of the cylinder head
            Color cavity = new Color(0.05f, 0.05f, 0.05f), rim = new Color(0.55f, 0.55f, 0.52f);
            foreach (float yc in new[] { 0.4095f, 0.5405f })
                for (int i = 0; i < 4; i++)
                {
                    float sg = yc > 0.5f ? 1f : -1f;
                    ObjMesh(t, "AroCamara", Geo.Torus(0.05f, 0.004f, 28, 8), new Vector3(0f, yc + sg * 0.001f, Zc(i)), Quaternion.identity, rim);
                    Cyl(t, "CamaraCombustion", new Vector3(0f, yc, Zc(i)), 0.098f, 0.003f, 'y', cavity);
                    Cyl(t, "ValvulaAdmision", new Vector3(-0.022f, yc + sg * 0.002f, Zc(i)), 0.026f, 0.003f, 'y', silver);
                    Cyl(t, "ValvulaEscape", new Vector3(0.022f, yc + sg * 0.002f, Zc(i)), 0.022f, 0.003f, 'y', rust);
                }
            foreach (float x in new[] { -0.20f, 0.20f })
                foreach (float y in new[] { 0.41f, 0.54f })
                    Cyl(t, "AristaCulata", new Vector3(x, y, 0f), 0.05f, 0.60f, 'z', alum);
            // cast ribs on the sides and the spark plug well
            for (int i = 0; i < 5; i++)
            {
                RCube(t, "NervioCulata", new Vector3(-0.208f, 0.475f, -0.28f + i * 0.14f), new Vector3(0.012f, 0.11f, 0.016f), alum);
                RCube(t, "NervioCulata", new Vector3(0.208f, 0.475f, -0.28f + i * 0.14f), new Vector3(0.012f, 0.11f, 0.016f), alum);
            }
            for (int i = 0; i < 4; i++)
                Cyl(t, "PozoBujia", new Vector3(0.208f, 0.545f, Zc(i)), 0.050f, 0.020f, 'y', alum);
            Cyl(t, "CuerpoTermostato", new Vector3(0f, 0.53f, 0.33f), 0.09f, 0.06f, 'y', alum);
            Cyl(t, "SalidaAgua", new Vector3(-0.04f, 0.56f, 0.355f), 0.05f, 0.06f, 'x', alum);
            Stud(t, "TuercaSalidaAgua", new Vector3(0f, 0.565f, 0.33f), 0.022f, 0.012f, 'y', silver);
            Cyl(t, "OjalLevante", new Vector3(0.10f, 0.56f, -0.32f), 0.04f, 0.015f, 'z', silver);

            // -------- intake (left)
            Color alum2 = new Color(0.50f, 0.51f, 0.53f);
            for (int i = 0; i < 4; i++)
            {
                float z = Zc(i);
                RCube(t, "BridaAdmision", new Vector3(-0.225f, 0.48f, z), new Vector3(0.03f, 0.075f, 0.10f), alum2);
                TubeO(t, "RamalAdmision", new[] { new Vector3(-0.23f, 0.48f, z), new Vector3(-0.285f, 0.495f, z), new Vector3(-0.325f, 0.56f, z * 0.9f), new Vector3(-0.34f, 0.625f, z * 0.72f) }, 0.030f, alum2);
                // injector with its connector, resting on the runner
                Cyl(t, "Inyector", new Vector3(-0.262f, 0.545f, z), 0.022f, 0.060f, 'y', black);
                RCube(t, "ConectorInyector", new Vector3(-0.262f, 0.583f, z), new Vector3(0.020f, 0.016f, 0.028f), silver);
            }
            // fuel rail and pressure regulator
            Cyl(t, "RielCombustible", new Vector3(-0.262f, 0.596f, 0f), 0.017f, 0.52f, 'z', silver);
            Cyl(t, "ReguladorPresion", new Vector3(-0.262f, 0.596f, 0.285f), 0.048f, 0.050f, 'z', silver);
            Esf(t, "TapaRegulador", new Vector3(-0.262f, 0.596f, 0.312f), 0.040f, silver);
            // long plenum with ribs and the throttle body at the end
            Rev(t, "CamaraAdmision", Profile(0f, -0.250f, 0.030f, -0.250f, 0.045f, -0.232f, 0.045f, 0.232f, 0.030f, 0.250f, 0f, 0.250f), 28, new Vector3(-0.34f, 0.64f, 0f), 'z', alum2);
            for (int i = 0; i < 6; i++)
                Cyl(t, "NervioCamara", new Vector3(-0.34f, 0.64f, -0.20f + i * 0.08f), 0.098f, 0.008f, 'z', alum2);
            RCube(t, "SoporteAdmision", new Vector3(-0.34f, 0.57f, 0.18f), new Vector3(0.050f, 0.045f, 0.040f), alum2);
            Cyl(t, "BridaCuerpo", new Vector3(-0.34f, 0.64f, -0.258f), 0.096f, 0.014f, 'z', alum2);
            Rev(t, "CuerpoAceleracion", Profile(0f, -0.046f, 0.036f, -0.046f, 0.036f, 0.046f, 0f, 0.046f), 28, new Vector3(-0.34f, 0.64f, -0.31f), 'z', alum2);
            Cyl(t, "EjeMariposa", new Vector3(-0.34f, 0.64f, -0.31f), 0.009f, 0.100f, 'x', silver);
            RCube(t, "PalancaAcelerador", new Vector3(-0.395f, 0.655f, -0.31f), new Vector3(0.006f, 0.060f, 0.014f), silver);
            Cyl(t, "PoleaAcelerador", new Vector3(-0.402f, 0.675f, -0.31f), 0.034f, 0.010f, 'x', silver);
            RCube(t, "SensorMariposa", new Vector3(-0.285f, 0.64f, -0.31f), new Vector3(0.028f, 0.030f, 0.036f), black);
            Cyl(t, "ValvulaAireRalenti", new Vector3(-0.34f, 0.695f, -0.30f), 0.030f, 0.040f, 'y', black);
            // air hose to the filter box
            if (prop) TubeO(t, "MangueraAire", new[] { new Vector3(-0.34f, 0.64f, -0.350f), new Vector3(-0.36f, 0.64f, -0.425f), new Vector3(-0.40f, 0.62f, -0.44f) }, 0.036f, rubber);
            else TubeO(t, "MangueraAire", new[] { new Vector3(-0.34f, 0.64f, -0.350f), new Vector3(-0.36f, 0.64f, -0.425f), new Vector3(-0.47f, 0.60f, -0.455f), new Vector3(-0.54f, 0.58f, -0.40f) }, 0.036f, rubber);
            for (int i = 0; i < 2; i++)
                Cyl(t, "AbrazaderaAire", new Vector3(-0.34f, 0.64f, -0.345f - i * 0.012f), 0.088f, 0.008f, 'z', silver);

            // intake flange bolts (two per cylinder)
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 2; k++)
                    Stud(t, "PernoBridaAdm", new Vector3(-0.245f, 0.455f + k * 0.050f, Zc(i) + (k == 0 ? -0.038f : 0.038f)), 0.014f, 0.016f, 'x', silver);

            // -------- exhaust (right): rusty cast iron
            Color cast = rust, cast2 = rust2;
            for (int i = 0; i < 4; i++)
            {
                float z = Zc(i);
                RCube(t, "BridaEscape", new Vector3(0.225f, 0.47f, z), new Vector3(0.03f, 0.075f, 0.09f), cast2);
                Stud(t, "EspaldaEscape", new Vector3(0.245f, 0.50f, z + 0.045f), 0.014f, 0.016f, 'x', silver);
                TubeO(t, "PuertoEscape", new[] { new Vector3(0.23f, 0.47f, z), new Vector3(0.285f, 0.45f, z * 0.85f), new Vector3(0.31f, 0.43f, z * 0.60f) }, 0.030f, cast);
                Esf(t, "AbultamientoEscape", new Vector3(0.28f, 0.45f, z * 0.88f), 0.062f, cast2);
            }
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 2; k++)
                    Stud(t, "TuercaBridaEsc", new Vector3(0.245f, 0.440f + k * 0.060f, Zc(i) + (k == 0 ? 0.040f : -0.040f)), 0.016f, 0.016f, 'x', silver);
            // two manifolds (cylinders 1-2 and 3-4) joined into a single central outlet
            Rev(t, "ColectorA", Profile(0f, -0.13f, 0.034f, -0.13f, 0.046f, -0.11f, 0.046f, 0.11f, 0.034f, 0.13f, 0f, 0.13f), 22, new Vector3(0.31f, 0.43f, -0.14f), 'z', cast);
            Rev(t, "ColectorB", Profile(0f, -0.13f, 0.034f, -0.13f, 0.046f, -0.11f, 0.046f, 0.11f, 0.034f, 0.13f, 0f, 0.13f), 22, new Vector3(0.31f, 0.43f, 0.14f), 'z', cast);
            Rev(t, "ColectorCentral", Profile(0f, -0.07f, 0.040f, -0.07f, 0.050f, -0.05f, 0.050f, 0.05f, 0.040f, 0.07f, 0f, 0.07f), 22, new Vector3(0.325f, 0.41f, 0f), 'z', cast2);
            ObjMesh(t, "CosturaEscapeA", Geo.Torus(0.047f, 0.006f, 22, 6), new Vector3(0.31f, 0.43f, -0.02f), RotAxis('z'), cast2);
            ObjMesh(t, "CosturaEscapeB", Geo.Torus(0.047f, 0.006f, 22, 6), new Vector3(0.31f, 0.43f, 0.02f), RotAxis('z'), cast2);
            // downward outlet flange and exhaust pipe
            Cyl(t, "BridaSalidaEscape", new Vector3(0.325f, 0.345f, 0f), 0.090f, 0.020f, 'y', cast2);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Stud(t, "ExhaustStud", new Vector3(0.325f + Mathf.Cos(a) * 0.036f, 0.335f, Mathf.Sin(a) * 0.036f), 0.014f, 0.022f, 'y', silver);
            }
            if (prop) TubeO(t, "ExhaustPipe", new[] { new Vector3(0.325f, 0.36f, 0f), new Vector3(0.335f, 0.28f, -0.05f), new Vector3(0.345f, 0.20f, -0.14f) }, 0.036f, cast);
            else TubeO(t, "ExhaustPipe", new[] { new Vector3(0.325f, 0.36f, 0f), new Vector3(0.335f, 0.28f, -0.05f), new Vector3(0.35f, 0.14f, -0.30f), new Vector3(0.35f, 0.06f, -0.62f) }, 0.036f, cast);
            // stamped heat shield with ribs and spacers
            GameObject sup = RCube(t, "ProtectorCalor", new Vector3(0.315f, 0.508f, 0f), new Vector3(0.150f, 0.008f, 0.50f), silver);
            sup.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            for (int i = 0; i < 4; i++)
            {
                GameObject nv = RCube(t, "NervaduraProtector", new Vector3(0.275f + i * 0.036f, 0.514f - i * 0.006f, 0f), new Vector3(0.008f, 0.005f, 0.46f), new Color(0.46f, 0.47f, 0.49f));
                nv.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            }
            for (int i = 0; i < 3; i++)
            {
                float zz = -0.20f + i * 0.20f;
                Cyl(t, "SeparadorProtector", new Vector3(0.265f, 0.490f, zz), 0.016f, 0.030f, 'y', silver);
                Stud(t, "PernoProtector", new Vector3(0.265f, 0.512f, zz), 0.018f, 0.010f, 'y', silver);
            }
            RCube(t, "FaldonProtector", new Vector3(0.392f, 0.455f, 0f), new Vector3(0.006f, 0.100f, 0.30f), silver);
        }

        // ---- valve cover: aluminum casting painted red, rim with a lip,
        //      five studs with cap nuts on the axis and the oil filler cap
        static void Cap(Transform t)
        {
            Color c = new Color(0.90f, 0.12f, 0.09f);
            Color c2 = new Color(0.74f, 0.09f, 0.07f);
            Color c3 = new Color(0.96f, 0.30f, 0.22f);       // lighter relief (letters and edges)
            // rubber gasket, base with a lip and body with slightly sloped sides
            RCube(t, "JuntaTapa", new Vector3(0f, 0.543f, 0f), new Vector3(0.330f, 0.008f, 0.580f), rubber);
            RCube(t, "BridaTapa", new Vector3(0f, 0.556f, 0f), new Vector3(0.336f, 0.018f, 0.588f), c2);
            RCube(t, "CuerpoTapa", new Vector3(0f, 0.590f, 0f), new Vector3(0.300f, 0.056f, 0.560f), c);
            RCube(t, "EscalonTapa", new Vector3(0f, 0.624f, 0f), new Vector3(0.262f, 0.020f, 0.520f), c);
            RCube(t, "MesetaTapa", new Vector3(0f, 0.637f, 0f), new Vector3(0.228f, 0.012f, 0.480f), c2);
            // raised lip around the plateau
            RCube(t, "RebordeLargoI", new Vector3(-0.108f, 0.6445f, 0f), new Vector3(0.010f, 0.007f, 0.470f), c3);
            RCube(t, "RebordeLargoD", new Vector3(0.108f, 0.6445f, 0f), new Vector3(0.010f, 0.007f, 0.470f), c3);
            RCube(t, "RebordeCortoA", new Vector3(0f, 0.6445f, 0.232f), new Vector3(0.218f, 0.007f, 0.010f), c3);
            RCube(t, "RebordeCortoB", new Vector3(0f, 0.6445f, -0.232f), new Vector3(0.218f, 0.007f, 0.010f), c3);
            // cast ribs on the sides
            for (int i = 0; i < 7; i++)
            {
                float z = -0.24f + i * 0.08f;
                RCube(t, "NervioLateral", new Vector3(0.148f, 0.590f, z), new Vector3(0.008f, 0.046f, 0.014f), c2);
                RCube(t, "NervioLateral", new Vector3(-0.148f, 0.590f, z), new Vector3(0.008f, 0.046f, 0.014f), c2);
            }
            // five studs on the axis, with sealing washer, nut and cap nut
            foreach (float zz in new[] { -0.220f, -0.110f, 0f, 0.110f, 0.220f })
            {
                Cyl(t, "Esparrago", new Vector3(0f, 0.660f, zz), 0.012f, 0.050f, 'y', silver);
                Stud(t, "ArandelaEsparrago", new Vector3(0f, 0.6445f, zz), 0.036f, 0.004f, 'y', silver);
                Stud(t, "TuercaEsparrago", new Vector3(0f, 0.652f, zz), 0.026f, 0.014f, 'y', silver);
                Rev(t, "TuercaCiega", Profile(0f, 0f, 0.013f, 0f, 0.013f, 0.010f, 0.010f, 0.018f, 0.004f, 0.023f, 0f, 0.024f), 18, new Vector3(0f, 0.660f, zz), 'y', silver);
            }
            // oil filler cap with ribbed grip, to one side of the studs
            Vector3 po = new Vector3(0.070f, 0.0f, 0.165f);
            Rev(t, "CuelloAceite", Profile(0f, 0f, 0.046f, 0f, 0.046f, 0.006f, 0.038f, 0.010f, 0.038f, 0.018f, 0f, 0.018f), 28, new Vector3(po.x, 0.6445f, po.z), 'y', black);
            Rev(t, "TaponAceite", Profile(0f, 0f, 0.036f, 0f, 0.036f, 0.004f, 0.033f, 0.006f, 0.033f, 0.016f, 0.027f, 0.020f, 0f, 0.020f), 28, new Vector3(po.x, 0.660f, po.z), 'y', new Color(0.07f, 0.07f, 0.08f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                GameObject e = RCube(t, "EstriaTapon", new Vector3(po.x + Mathf.Cos(a) * 0.033f, 0.670f, po.z + Mathf.Sin(a) * 0.033f), new Vector3(0.006f, 0.014f, 0.006f), new Color(0.12f, 0.12f, 0.13f));
                e.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            RCube(t, "ManijaTapon", new Vector3(po.x, 0.682f, po.z), new Vector3(0.048f, 0.008f, 0.012f), new Color(0.12f, 0.12f, 0.13f));
            // breather (PCV) with a hose to the intake
            Rev(t, "BocaRespiradero", Profile(0f, 0f, 0.020f, 0f, 0.020f, 0.012f, 0.014f, 0.016f, 0.014f, 0.030f, 0f, 0.030f), 20, new Vector3(0f, 0.6445f, -0.160f), 'y', c2);
            TubeO(t, "MangueraRespiradero", new[] { new Vector3(0f, 0.676f, -0.160f), new Vector3(-0.05f, 0.700f, -0.190f), new Vector3(-0.14f, 0.690f, -0.200f), new Vector3(-0.23f, 0.675f, -0.200f) }, 0.012f, rubber);
            // flat raised plate and two long ribs on the plateau (no marks or letters)
            RCube(t, "PlacaLisa", new Vector3(-0.060f, 0.6445f, -0.040f), new Vector3(0.050f, 0.004f, 0.300f), c3);
            RCube(t, "NervioMeseta", new Vector3(-0.088f, 0.6445f, -0.040f), new Vector3(0.008f, 0.006f, 0.380f), c2);
        }

        // ---- spark plugs (gasoline engine): thread, hex nut, body, ribbed ceramic insulator and terminal
        static void SparkPlugs(Transform t, bool prop = false)
        {
            Color ceramic = new Color(0.86f, 0.85f, 0.80f);
            Color body = new Color(0.58f, 0.59f, 0.61f);
            for (int i = 0; i < 4; i++)
            {
                float z = prop ? (i - 1.5f) * 0.075f : Zc(i);   // as an object: the four together
                float x = 0.205f;
                // threaded part with sealing washer and electrode
                Rev(t, "RoscaBujia", Profile(0f, 0f, 0.0065f, 0f, 0.0065f, 0.020f, 0f, 0.020f), 14, new Vector3(x, 0.534f, z), 'y', body);
                for (int k = 0; k < 5; k++)
                    Cyl(t, "FiletePerfil", new Vector3(x, 0.538f + k * 0.0035f, z), 0.0145f, 0.0012f, 'y', silver);
                Cyl(t, "ElectrodoBujia", new Vector3(x, 0.530f, z), 0.003f, 0.010f, 'y', black);
                Stud(t, "TuercaBujia", new Vector3(x, 0.559f, z), 0.030f, 0.016f, 'y', silver);
                Cyl(t, "CuerpoBujia", new Vector3(x, 0.572f, z), 0.021f, 0.012f, 'y', body);
                // insulator with ribs
                Rev(t, "AisladorBujia", Profile(0f, 0f, 0.0105f, 0f, 0.0105f, 0.010f, 0.0125f, 0.013f, 0.0125f, 0.016f, 0.0105f, 0.019f, 0.0125f, 0.022f, 0.0105f, 0.025f, 0.0125f, 0.028f, 0.0105f, 0.031f, 0.0095f, 0.036f, 0f, 0.036f), 20, new Vector3(x, 0.578f, z), 'y', ceramic);
                Cyl(t, "BorneBujia", new Vector3(x, 0.620f, z), 0.0055f, 0.014f, 'y', copper);
                Rev(t, "TuercaBorne", Profile(0f, 0f, 0.0085f, 0f, 0.0085f, 0.007f, 0f, 0.007f), 12, new Vector3(x, 0.620f, z), 'y', copper);
            }
        }

        // ---- spark plug wires: distributor cap with four towers and the coil one, wires with rubber boots
        static void Cables(Transform t, bool prop)
        {
            Color bakelite = new Color(0.10f, 0.09f, 0.08f);
            Color cable = new Color(0.04f, 0.04f, 0.045f);
            Vector3 cap = new Vector3(0.12f, 0.56f, -0.355f);
            // distributor cap with clips
            Rev(t, "TapaDistribuidor", Profile(0f, 0f, 0.046f, 0f, 0.046f, 0.030f, 0.040f, 0.036f, 0.038f, 0.044f, 0f, 0.044f), 32, cap, 'y', bakelite);
            foreach (float s in new[] { -1f, 1f })
            {
                Cyl(t, "BrocheTapa", new Vector3(cap.x + s * 0.044f, cap.y + 0.012f, cap.z), 0.010f, 0.034f, 'y', silver);
                ObjMesh(t, "ArgollaBroche", Geo.Torus(0.007f, 0.0018f, 12, 6), new Vector3(cap.x + s * 0.046f, cap.y + 0.003f, cap.z), RotAxis('z'), silver);
            }
            // towers: 4 for spark plugs + 1 central for the coil
            Vector3[] tower = new Vector3[4];
            float[] ang = { 135f, 105f, 75f, 45f };
            for (int i = 0; i < 4; i++)
            {
                float a = ang[i] * Mathf.Deg2Rad;
                tower[i] = new Vector3(cap.x + Mathf.Cos(a) * 0.030f, cap.y + 0.062f, cap.z + Mathf.Sin(a) * 0.030f);
                Rev(t, "TorreTapa", Profile(0f, 0f, 0.010f, 0f, 0.010f, 0.020f, 0f, 0.020f), 12, new Vector3(tower[i].x, cap.y + 0.044f, tower[i].z), 'y', bakelite);
            }
            Vector3 towerC = new Vector3(cap.x, cap.y + 0.070f, cap.z);
            Rev(t, "TorreCentral", Profile(0f, 0f, 0.011f, 0f, 0.011f, 0.026f, 0f, 0.026f), 12, new Vector3(cap.x, cap.y + 0.044f, cap.z), 'y', bakelite);
            for (int i = 0; i < 4; i++)
            {
                float zp = prop ? (i - 1.5f) * 0.075f : Zc(i);
                Vector3 end = prop ? new Vector3(cap.x - 0.30f + (i - 1.5f) * 0.02f, cap.y + 0.02f, cap.z + 0.10f + i * 0.085f) : new Vector3(0.205f, 0.640f, zp);
                Vector3 m1 = tower[i] + new Vector3(0.015f, 0.045f, 0.040f);
                Vector3 m2 = prop ? Vector3.Lerp(m1, end, 0.55f) + new Vector3(0f, 0.06f, 0f) : new Vector3(0.19f, 0.69f + 0.004f * i, Mathf.Lerp(tower[i].z, zp, 0.7f));
                TubeO(t, "CableBujia" + i, new[] { tower[i] + new Vector3(0f, 0.010f, 0f), m1, m2, end }, 0.0062f, cable);
                // rubber boot on the distributor side and on the spark plug side
                Rev(t, "BotaTorre", Profile(0f, 0f, 0.0125f, 0f, 0.0115f, 0.018f, 0.008f, 0.026f, 0f, 0.026f), 14, tower[i] + new Vector3(0f, -0.004f, 0f), 'y', cable);
                Rev(t, "BotaBujia", Profile(0f, 0f, 0.0155f, 0f, 0.0165f, 0.012f, 0.0125f, 0.030f, 0.008f, 0.040f, 0f, 0.040f), 16, end + new Vector3(0f, -0.038f, 0f), 'y', cable);
                Rev(t, "TerminalBota", Profile(0f, 0f, 0.0125f, 0f, 0.0125f, 0.004f, 0f, 0.004f), 14, end + new Vector3(0f, -0.039f, 0f), 'y', silver);
            }
            // coil wire (high voltage) to the central tower
            Vector3 coilEnd = prop ? new Vector3(cap.x - 0.04f, cap.y + 0.04f, cap.z - 0.14f) : new Vector3(0.03f, 0.66f, -0.40f);
            TubeO(t, "CableBobina", new[] { towerC + new Vector3(0f, 0.012f, 0f), towerC + new Vector3(-0.02f, 0.045f, -0.02f), Vector3.Lerp(towerC, coilEnd, 0.6f) + new Vector3(0f, 0.04f, 0f), coilEnd }, 0.0068f, cable);
            Rev(t, "BotaCentral", Profile(0f, 0f, 0.0135f, 0f, 0.0125f, 0.020f, 0.0085f, 0.030f, 0f, 0.030f), 14, towerC + new Vector3(0f, -0.006f, 0f), 'y', cable);
            // plastic separator comb (installed) that groups the wires
            if (!prop)
            {
                RCube(t, "PeineCables", new Vector3(0.195f, 0.683f, -0.120f), new Vector3(0.020f, 0.012f, 0.150f), new Color(0.12f, 0.12f, 0.13f));
                RCube(t, "PeineCables", new Vector3(0.165f, 0.700f, -0.290f), new Vector3(0.020f, 0.012f, 0.060f), new Color(0.12f, 0.12f, 0.13f));
            }
        }

        // ---- alternator with its belt: housings with ventilation slots, mounting ears, grooved pulley and regulator
        static void Alternator(Transform t, bool prop)
        {
            Vector3 c = new Vector3(-0.30f, 0.24f, 0.27f);
            var gr = new GameObject("Alternador").transform;
            gr.SetParent(t, false);
            gr.localPosition = new Vector3(c.x * KX, c.y, c.z);
            float kx0 = KX;
            KX = 1f;                                 // the alternator is round: it is not compressed
            Color housing = new Color(0.17f, 0.17f, 0.18f);
            Color stator = new Color(0.46f, 0.47f, 0.49f);
            Color front = new Color(0.40f, 0.41f, 0.43f);
            // rear housing, stator and front housing (the alternator axis is Z)
            Rev(gr, "CarcasaTrasera", Profile(0f, -0.082f, 0.038f, -0.082f, 0.052f, -0.075f, 0.058f, -0.062f, 0.060f, -0.048f, 0.060f, -0.015f, 0f, -0.015f), 36, Vector3.zero, 'z', housing);
            Rev(gr, "Estator", Profile(0f, -0.015f, 0.0585f, -0.015f, 0.0585f, 0.026f, 0f, 0.026f), 36, Vector3.zero, 'z', stator);
            Rev(gr, "AroEstator", Profile(0f, 0.002f, 0.0605f, 0.002f, 0.0605f, 0.006f, 0f, 0.006f), 36, Vector3.zero, 'z', new Color(0.30f, 0.31f, 0.33f));
            Rev(gr, "CarcasaDelantera", Profile(0f, 0.026f, 0.060f, 0.026f, 0.060f, 0.040f, 0.052f, 0.056f, 0.034f, 0.068f, 0.030f, 0.070f, 0.030f, 0.080f, 0f, 0.080f), 36, Vector3.zero, 'z', front);
            // ventilation slots around the rear and front housings
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                GameObject r1 = RCube(gr, "RanuraVentilacion", new Vector3(Mathf.Cos(a) * 0.060f, Mathf.Sin(a) * 0.060f, -0.040f), new Vector3(0.005f, 0.020f, 0.036f), new Color(0.03f, 0.03f, 0.03f));
                r1.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                float b2 = a + Mathf.PI / 14f;
                GameObject r2 = RCube(gr, "RanuraDelantera", new Vector3(Mathf.Cos(b2) * 0.058f, Mathf.Sin(b2) * 0.058f, 0.044f), new Vector3(0.005f, 0.016f, 0.012f), new Color(0.03f, 0.03f, 0.03f));
                r2.transform.localRotation = Quaternion.Euler(0f, 0f, b2 * Mathf.Rad2Deg);
            }
            // mounting ears with holes and bolts
            RCube(gr, "OrejaSuperior", new Vector3(0f, 0.070f, 0.046f), new Vector3(0.040f, 0.034f, 0.014f), front);
            Cyl(gr, "AgujeroOreja", new Vector3(0f, 0.076f, 0.046f), 0.016f, 0.016f, 'z', new Color(0.03f, 0.03f, 0.03f));
            RCube(gr, "OrejaInferior", new Vector3(0f, -0.070f, 0.040f), new Vector3(0.040f, 0.030f, 0.014f), front);
            Stud(gr, "PernoOreja", new Vector3(0f, -0.072f, 0.054f), 0.018f, 0.014f, 'z', silver);
            RCube(gr, "OrejaTrasera", new Vector3(0f, -0.068f, -0.060f), new Vector3(0.036f, 0.028f, 0.012f), housing);
            // long through bolts (four)
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
                Stud(gr, "TornilloPasante", new Vector3(Mathf.Cos(a) * 0.048f, Mathf.Sin(a) * 0.048f, -0.088f), 0.014f, 0.012f, 'z', silver);
            }
            // grooved pulley, shaft and nut
            Rev(gr, "EjePolea", Profile(0f, 0.078f, 0.011f, 0.078f, 0.011f, 0.116f, 0f, 0.116f), 16, Vector3.zero, 'z', silver);
            Rev(gr, "PoleaAlternador", Profile(0f, 0.084f, 0.040f, 0.084f, 0.040f, 0.0865f, 0.0355f, 0.0885f, 0.040f, 0.0905f, 0.040f, 0.0935f, 0.0355f, 0.0955f, 0.040f, 0.0975f, 0.040f, 0.1005f, 0.0355f, 0.1025f, 0.040f, 0.1045f, 0.040f, 0.1085f, 0.034f, 0.1085f, 0f, 0.1085f), 36, Vector3.zero, 'z', silver);
            Stud(gr, "TuercaPolea", new Vector3(0f, 0f, 0.1175f), 0.024f, 0.014f, 'z', black);
            // regulator and connectors at the rear
            RCube(gr, "Regulador", new Vector3(0f, 0.012f, -0.092f), new Vector3(0.070f, 0.046f, 0.018f), black);
            RCube(gr, "Conector", new Vector3(0.030f, 0.030f, -0.108f), new Vector3(0.030f, 0.024f, 0.022f), new Color(0.04f, 0.04f, 0.05f));
            Stud(gr, "BorneBateria", new Vector3(-0.030f, 0.030f, -0.098f), 0.014f, 0.014f, 'z', copper);
            Rev(gr, "BotaBorne", Profile(0f, -0.098f, 0.018f, -0.098f, 0.015f, -0.112f, 0.009f, -0.118f, 0f, -0.118f), 16, new Vector3(-0.030f, 0.030f, 0f), 'z', red);
            // bracket on the block
            RCube(gr, "SoporteAlternador", new Vector3(0f, -0.088f, 0.020f), new Vector3(0.060f, 0.026f, 0.130f), block);
            KX = kx0;
            if (prop) return;

            Cable(t, new Vector3(-0.30f, 0.30f, 0.25f), new Vector3(-0.20f, 0.45f, 0.20f), 0.01f, block);            // tensioner arm
            TubeO(t, "CableBateria", new[] { new Vector3(-0.30f, 0.30f, 0.20f), new Vector3(-0.40f, 0.34f, 0.05f), new Vector3(-0.42f, 0.32f, -0.20f) }, 0.008f, red);   // cable to the battery

            // belt over the three pulleys (crankshaft, alternator, water pump)
            Vector2[] p = { new Vector2(0f, 0.20f), new Vector2(-0.30f, 0.24f), new Vector2(0f, 0.43f) };
            float[] r = { 0.088f, 0.040f, 0.060f };
            Vector2 cen = (p[0] + p[1] + p[2]) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                Vector2 d = (p[j] - p[i]).normalized;
                Vector2 n = new Vector2(-d.y, d.x);
                if (Vector2.Dot(n, (p[i] + p[j]) * 0.5f - cen) < 0f) n = -n;
                Vector2 a2 = p[i] + n * r[i], b2 = p[j] + n * r[j];
                Cable(t, new Vector3(a2.x, a2.y, 0.36f), new Vector3(b2.x, b2.y, 0.36f), 0.011f, rubber);
            }
        }

        // ---- what you see when a part is missing: cylinders, studs and rocker arms
        void Internal(Transform g)
        {
            boresGO = new GameObject("Cilindros");
            boresGO.transform.SetParent(g, false);
            for (int i = 0; i < 4; i++)
            {
                // cylinder: dark opening with the liner ring and the aluminum piston with its rings, a little lower down
                Cyl(boresGO.transform, "Camisa", new Vector3(0f, 0.4015f, Zc(i)), 0.104f, 0.004f, 'y', new Color(0.03f, 0.03f, 0.03f));
                ObjMesh(boresGO.transform, "AroCamisa", Geo.Torus(0.052f, 0.004f, 28, 8), new Vector3(0f, 0.4035f, Zc(i)), Quaternion.identity, new Color(0.50f, 0.50f, 0.48f));
                Cyl(boresGO.transform, "AroPiston", new Vector3(0f, 0.4040f, Zc(i)), 0.088f, 0.002f, 'y', new Color(0.12f, 0.12f, 0.12f));
                Cyl(boresGO.transform, "Piston", new Vector3(0f, 0.4050f, Zc(i)), 0.078f, 0.003f, 'y', aluminum);
                Cyl(boresGO.transform, "BulonPiston", new Vector3(0f, 0.4068f, Zc(i)), 0.018f, 0.002f, 'y', silver);
            }
            studsGO = new GameObject("PernosCulata");
            studsGO.transform.SetParent(g, false);
            for (int i = 0; i < 5; i++)
            {
                Cyl(studsGO.transform, "HeadStud", new Vector3(0.15f, 0.41f, -0.28f + i * 0.14f), 0.022f, 0.02f, 'y', silver);
                Cyl(studsGO.transform, "HeadStud", new Vector3(-0.15f, 0.41f, -0.28f + i * 0.14f), 0.022f, 0.02f, 'y', silver);
            }
            rockersGO = new GameObject("Balancines");
            rockersGO.transform.SetParent(g, false);
            Transform b = rockersGO.transform;
            Cube(b, "SoporteArbol", new Vector3(0f, 0.552f, 0f), new Vector3(0.30f, 0.02f, 0.56f), headC);
            Cyl(b, "ArbolLevas", new Vector3(0f, 0.585f, 0f), 0.028f, 0.56f, 'z', silver);
            for (int i = 0; i < 4; i++)
            {
                Cyl(b, "Leva", new Vector3(0.015f, 0.585f, Zc(i) - 0.03f), 0.055f, 0.02f, 'z', aluminum);
                Cyl(b, "Leva", new Vector3(-0.015f, 0.585f, Zc(i) + 0.03f), 0.055f, 0.02f, 'z', aluminum);
                Cube(b, "Balancin", new Vector3(0.075f, 0.572f, Zc(i) - 0.03f), new Vector3(0.05f, 0.025f, 0.03f), aluminum);
                Cube(b, "Balancin", new Vector3(-0.075f, 0.572f, Zc(i) + 0.03f), new Vector3(0.05f, 0.025f, 0.03f), aluminum);
                Cyl(b, "ResorteValvula", new Vector3(0.11f, 0.565f, Zc(i) - 0.03f), 0.03f, 0.04f, 'y', rust);
                Cyl(b, "ResorteValvula", new Vector3(-0.11f, 0.565f, Zc(i) + 0.03f), 0.03f, 0.04f, 'y', rust);
            }
        }

        // ---- battery, tray and cables (the battery is a game item)
        void Battery(Transform g)
        {
            Vector3 bpos = new Vector3(0.58f, 0.42f, 0.30f);   // at radiator height
            Cube(g, "BandejaBateria", bpos + new Vector3(0f, -0.07f, 0f), new Vector3(0.40f, 0.03f, 0.28f), new Color(0.10f, 0.10f, 0.11f));
            Cube(g, "SoporteBateria", new Vector3(bpos.x, (bpos.y - 0.07f) * 0.5f, bpos.z), new Vector3(0.05f, bpos.y - 0.08f, 0.05f), new Color(0.12f, 0.12f, 0.13f));
            Cube(g, "PieSoporte", new Vector3(bpos.x, 0.03f, bpos.z), new Vector3(0.22f, 0.02f, 0.22f), new Color(0.12f, 0.12f, 0.13f));
            Cube(g, "ReborBandeja", bpos + new Vector3(0f, -0.045f, 0.14f), new Vector3(0.40f, 0.03f, 0.02f), new Color(0.10f, 0.10f, 0.11f));
            Cable(g, bpos + new Vector3(-0.16f, -0.05f, -0.10f), bpos + new Vector3(-0.16f, 0.12f, -0.10f), 0.006f, aluminum);   // hold-down clamp
            Cable(g, bpos + new Vector3(0.16f, -0.05f, -0.10f), bpos + new Vector3(0.16f, 0.12f, -0.10f), 0.006f, aluminum);
            Vector3 terminalMore = bpos + new Vector3(-0.10f, 0.12f, 0.08f);
            Vector3 terminalMinus = bpos + new Vector3(0.10f, 0.12f, 0.08f);
            Cable(g, terminalMore, new Vector3(0.26f, 0.20f, -0.17f), 0.014f, red);
            Cable(g, terminalMinus, new Vector3(0.20f, 0.30f, -0.10f), 0.014f, rubber);

            batteryGO = new GameObject("Bateria");
            batteryGO.transform.SetParent(g, false);
            batteryGO.transform.localPosition = bpos;
            Transform b = batteryGO.transform;
            Cube(b, "Caja", Vector3.zero, new Vector3(0.34f, 0.20f, 0.22f), new Color(0.08f, 0.10f, 0.16f));
            Cube(b, "Tapa", new Vector3(0f, 0.105f, 0f), new Vector3(0.35f, 0.02f, 0.23f), new Color(0.16f, 0.18f, 0.24f));
            Cyl(b, "BorneMas", new Vector3(-0.10f, 0.14f, 0.08f), 0.04f, 0.05f, 'y', new Color(0.60f, 0.08f, 0.08f));
            Cyl(b, "BorneMenos", new Vector3(0.10f, 0.14f, 0.08f), 0.04f, 0.05f, 'y', new Color(0.12f, 0.12f, 0.12f));
            Cube(b, "Etiqueta", new Vector3(0f, 0.02f, -0.116f), new Vector3(0.22f, 0.10f, 0.005f), new Color(0.75f, 0.70f, 0.20f));
            for (int i = 0; i < 3; i++)
                Cyl(b, "TaponCelda", new Vector3(-0.09f + i * 0.09f, 0.12f, -0.05f), 0.035f, 0.01f, 'y', new Color(0.25f, 0.27f, 0.32f));
            batteryGO.SetActive(false);
        }

        // ------------------------------------------------------------------ loose part (object on the ground)

        /// <summary>A physical object shaped like the part, which can be left on the ground and picked up.</summary>
        public static GameObject CreateObject(int type)
        {
            var rootObj = new GameObject("WV_Part_" + Parts.Names[type]);
            var inner = new GameObject("Modelo").transform;
            inner.SetParent(rootObj.transform, false);

            KX = WidthCore;
            try
            {
                switch (type)
                {
                    case Parts.Alternator: Alternator(inner, true); break;
                    case Parts.SparkPlugs: SparkPlugs(inner, true); break;
                    case Parts.Cables: Cables(inner, true); break;
                    case Parts.Cap: Cap(inner); break;
                    case Parts.Head: Head(inner, true); break;
                    default: Gasket(inner); break;
                }
            }
            finally { KX = 1f; }

            // center the model and fit the size to what can be carried in the hand
            Renderer[] rs = inner.GetComponentsInChildren<Renderer>(true);
            Bounds b = Limits(rs);
            // scale of each part as an object: the big ones (head, gasket, cover) at engine scale; the small ones (alternator, spark plugs, wires) enlarged so they are easy to see
            float[] scales = { 1.6f, 2.2f, 1.0f, 0.85f, 0.85f, 0.85f };
            float k = scales[type];
            inner.localScale = new Vector3(k, k, k);
            inner.localPosition = -b.center * k;

            // no physics: it stays exactly where you leave it (with physics it sank into the ground and got lost)
            return rootObj;
        }

        /// <summary>Single mesh (one submesh per color) with the part's model, to put on a game object.</summary>
        public static void BuildMesh(int type, out Mesh meshObj, out Material[] materialsOut)
        {
            GameObject o = CreateObject(type);
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            Matrix4x4 aLocal = o.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in o.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                Material m = mr.sharedMaterial;
                if (!groups.ContainsKey(m)) { groups[m] = new List<CombineInstance>(); order.Add(m); }
                var ci = new CombineInstance();
                ci.mesh = mf.sharedMesh;
                ci.subMeshIndex = 0;
                ci.transform = aLocal * mf.transform.localToWorldMatrix;
                groups[m].Add(ci);
            }
            var segments = new List<CombineInstance>();
            foreach (Material m in order)
            {
                var sub = new Mesh();
                sub.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                sub.CombineMeshes(groups[m].ToArray(), true, true);
                var ci = new CombineInstance();
                ci.mesh = sub;
                ci.subMeshIndex = 0;
                ci.transform = Matrix4x4.identity;
                segments.Add(ci);
            }
            meshObj = new Mesh();
            meshObj.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshObj.CombineMeshes(segments.ToArray(), false, false);
            meshObj.hideFlags = HideFlags.DontUnloadUnusedAsset;
            materialsOut = order.ToArray();
            UnityEngine.Object.Destroy(o);
        }

        /// <summary>Half the height of the object (to rest it on the ground).</summary>
        public static float SemiHeight(GameObject go)
        {
            Bounds b = Limits(go.GetComponentsInChildren<Renderer>(true));
            return Mathf.Max(0.03f, b.extents.y);
        }

        // ------------------------------------------------------------------ shapes

        internal static Material Mat(Color c)
        {
            int k = ((int)(c.r * 255f) << 16) | ((int)(c.g * 255f) << 8) | (int)(c.b * 255f);
            Material m;
            if (materials.TryGetValue(k, out m) && m != null) return m;

            if (!shaderSearched)
            {
                shaderSearched = true;
                string[] candidates = { "Legacy Shaders/Diffuse", "Standard", "Sprites/Default", "UI/Default", "Unlit/Color" };
                foreach (string n in candidates)
                {
                    Shader s = Shader.Find(n);
                    if (s != null) { shader = s; break; }
                }
                MelonLoader.MelonLogger.Msg("[Hood] engine shader: " + (shader != null ? shader.name : "none (a game material is copied)"));
            }

            if (shader != null) m = new Material(shader);
            else
            {
                if (template == null)
                {
                    foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.name.Contains("CarTruck")) { template = r.sharedMaterial; break; }
                }
                m = template != null ? new Material(template) : new Material(Shader.Find("Hidden/Internal-Colored"));
                try { m.mainTexture = null; } catch (Exception) { }
            }
            m.color = Paint.TurnOff(c);
            try { Texture2D pt = Paint.Textura(); if (pt != null) m.mainTexture = pt; } catch (Exception) { }
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            materials[k] = m;
            return m;
        }

        static GameObject Shape(Transform parent, PrimitiveType type, string names, Vector3 pos, Vector3 sc, Color c)
        {
            GameObject o = GameObject.CreatePrimitive(type);
            o.name = names;
            Collider col = o.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            o.transform.SetParent(parent, false);
            o.transform.localPosition = pos;
            o.transform.localScale = sc;
            Renderer r = o.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Mat(c);
            return o;
        }

        // ------------------------------------------------------------------ higher-quality shapes (see Geo.cs)

        static Quaternion RotAxis(char axis)
        {
            if (axis == 'x') return Quaternion.Euler(0f, 0f, -90f);
            if (axis == 'z') return Quaternion.Euler(90f, 0f, 0f);
            return Quaternion.identity;
        }

        static GameObject ObjMesh(Transform parent, string names, Mesh m, Vector3 pos, Quaternion rot, Color c)
        {
            var o = new GameObject(names);
            o.transform.SetParent(parent, false);
            o.transform.localPosition = new Vector3(pos.x * KX, pos.y, pos.z);
            o.transform.localRotation = rot;
            o.AddComponent<MeshFilter>().sharedMesh = m;
            o.AddComponent<MeshRenderer>().sharedMaterial = Mat(c);
            return o;
        }

        /// <summary>Box with rounded edges.</summary>
        static GameObject RCube(Transform parent, string names, Vector3 pos, Vector3 size, Color c)
        {
            size.x *= KX;
            float r = Mathf.Min(0.008f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.35f);
            return ObjMesh(parent, names, Geo.Box(size, r, 3), pos, Quaternion.identity, c);
        }

        /// <summary>Hex bolt / nut with washer. 'largo' is the total height; points toward 'eje'.</summary>
        static GameObject Stud(Transform parent, string names, Vector3 pos, float diam, float length, char axis, Color c)
        {
            Quaternion rot = RotAxis(axis);
            Vector3 dir = rot * Vector3.up;
            GameObject cab = ObjMesh(parent, names, Geo.Prism(6, diam * 0.5f, length * 0.7f, diam * 0.05f), pos + dir * (length * 0.15f), rot, c);
            ObjMesh(parent, names + "Arandela", Geo.Prism(20, diam * 0.78f, length * 0.3f, 0.0006f), pos - dir * (length * 0.35f), rot, new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f));
            return cab;
        }

        /// <summary>Surface of revolution: the profile (radius, height) rotates around the given axis.</summary>
        static GameObject Rev(Transform parent, string names, Vector2[] profile, int sides, Vector3 pos, char axis, Color c)
        {
            return ObjMesh(parent, names, Geo.Revolution(profile, sides, true, false, null), pos, RotAxis(axis), c);
        }

        /// <summary>Tube / hose / cable that curves smoothly through the points.</summary>
        static GameObject TubeO(Transform parent, string names, Vector3[] pts, float radius, Color c)
        {
            var q = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++) q[i] = new Vector3(pts[i].x * KX, pts[i].y, pts[i].z);
            return ObjMesh(parent, names, Geo.Tube(q, radius, 10, 6), Vector3.zero, Quaternion.identity, c);
        }

        static Vector2[] Profile(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }

        // Lateral compression of the engine (an inline-4 engine is narrow): applied to the positions and measurements on X.
        const float WidthCore = 0.78f;
        static float KX = 1f;

        static GameObject Cube(Transform parent, string names, Vector3 pos, Vector3 size, Color c)
        {
            pos.x *= KX; size.x *= KX;
            return Shape(parent, PrimitiveType.Cube, names, pos, size, c);
        }

        static GameObject Esf(Transform parent, string names, Vector3 pos, float diam, Color c)
        {
            pos.x *= KX;
            return Shape(parent, PrimitiveType.Sphere, names, pos, new Vector3(diam, diam, diam), c);
        }

        /// <summary>Cylinder of diameter 'diam' and length 'largo', pointing along the axis 'eje' (x, y or z).</summary>
        static GameObject Cyl(Transform parent, string names, Vector3 pos, float diam, float length, char axis, Color c)
        {
            pos.x *= KX;
            if (axis == 'x') length *= KX;
            GameObject o = Shape(parent, PrimitiveType.Cylinder, names, pos, new Vector3(diam, length * 0.5f, diam), c);
            if (axis == 'x') o.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            else if (axis == 'z') o.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return o;
        }

        /// <summary>Pipe / cable: a thin cylinder between two points.</summary>
        static GameObject Cable(Transform parent, Vector3 a, Vector3 b, float radius, Color c)
        {
            a.x *= KX; b.x *= KX;
            Vector3 d = b - a;
            float length = d.magnitude;
            GameObject o = Shape(parent, PrimitiveType.Cylinder, "Cable", (a + b) * 0.5f, new Vector3(radius * 2f, length * 0.5f, radius * 2f), c);
            if (length > 0.0001f) o.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            return o;
        }
    }
}
