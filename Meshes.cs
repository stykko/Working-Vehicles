using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// In the game's model the steering wheel is part of the interior mesh and the front wheels are part of the body mesh
    /// and of the tires: there are no separate parts. This class cuts them out IN MEMORY (the game's files are not
    /// touched): it detects "islands" of triangles that don't touch each other, picks the steering wheel's and the two
    /// front wheels' islands, turns them into its own objects with a pivot, and rotates them.
    /// It requires the meshes to be readable (Mesh.isReadable). If they are not, the mod warns and cuts nothing.
    /// </summary>
    internal class Body
    {
        public Transform pivotSteering;
        public Vector3 axisSteering = Vector3.forward;
        public float zero;      // rotation (degrees) at which the steering wheel looks straight; the model's spokes come tilted
        public readonly Transform[] pivotWheel = new Transform[2];
        public bool Exists { get { return pivotSteering != null || pivotWheel[0] != null || pivotWheel[1] != null; } }

        float lastVol = float.NaN;

        public void Apply(float degreesSteering)
        {
            float fit = Settings.AdjustSteering();
            float cacheKey = degreesSteering + (fit + zero) * 1000f;
            if (Mathf.Abs(cacheKey - lastVol) < 0.01f) return;
            lastVol = cacheKey;
            if (pivotSteering != null) pivotSteering.localRotation = Quaternion.AngleAxis(degreesSteering + fit + zero, axisSteering);
            float wheel = Cfg.SteeringDegrees.Value > 0.1f ? degreesSteering / Cfg.SteeringDegrees.Value * Cfg.WheelsDegrees.Value : 0f;
            for (int i = 0; i < 2; i++)
                if (pivotWheel[i] != null) pivotWheel[i].localRotation = Quaternion.AngleAxis(wheel, Vector3.up);
        }

        // ====================================================================================== islands

        class Island
        {
            public int sub;
            public readonly List<int> tris = new List<int>();   // start position of each triangle within GetTriangles(sub)
            public Bounds b;
            public bool start = true;
            public Vector3 center { get { return b.center; } }
            public float ext { get { Vector3 s = b.size; return Mathf.Max(s.x, Mathf.Max(s.y, s.z)); } }
            public int nVerts;
            public HashSet<int> verts = new HashSet<int>();
            public void Add(Vector3 p)
            {
                if (start) { b = new Bounds(p, Vector3.zero); start = false; }
                else b.Encapsulate(p);
            }
        }

        class Data3D
        {
            public Mesh mesh;
            public MeshFilter mf;
            public Renderer rend;
            public Vector3[] v;
            public int[][] tri;               // per submesh
            public List<Island> islands = new List<Island>();
            public Vector3[] pRoot;           // vertices in the local space of the vehicle's root
        }

        static List<Renderer> all;

        static string Stem(string n)
        {
            int i = n.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
            return i > 0 ? n.Substring(0, i) : n;
        }

        static bool Tire(Island i)
        {
            Vector3 sz = i.b.size;
            float[] a = { sz.x, sz.y, sz.z };
            Array.Sort(a);
            return i.nVerts >= 20 && a[2] > 0.5f && a[2] < 1.3f && a[1] >= 0.6f * a[2] && a[0] <= 0.7f * a[1] && a[0] > 0.08f;
        }

        static int Find(int[] parent, int a)
        {
            while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; }
            return a;
        }

        static Data3D Analyze(Renderer rend, Transform root)
        {
            MeshFilter mf = rend.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;
            Mesh m = mf.sharedMesh;
            if (!m.isReadable)
            {
                MelonLoader.MelonLogger.Warning("[Meshes] The mesh '" + m.name + "' is NOT readable (isReadable=false): it cannot be cut out.");
                return null;
            }

            var d = new Data3D { mesh = m, mf = mf, rend = rend };
            Vector3[] v = m.vertices;
            d.v = v;
            d.pRoot = new Vector3[v.Length];
            Transform mt = mf.transform;
            for (int i = 0; i < v.Length; i++) d.pRoot[i] = root.InverseTransformPoint(mt.TransformPoint(v[i]));

            // Weld vertices that are in the same place (models duplicate vertices on hard edges).
            var group = new int[v.Length];
            var map = new Dictionary<long, int>();
            int ng = 0;
            for (int i = 0; i < v.Length; i++)
            {
                long kx = (long)Mathf.Round(v[i].x * 400f) & 0x1FFFFF;
                long ky = (long)Mathf.Round(v[i].y * 400f) & 0x1FFFFF;
                long kz = (long)Mathf.Round(v[i].z * 400f) & 0x1FFFFF;
                long k = (kx << 42) | (ky << 21) | kz;
                int g;
                if (!map.TryGetValue(k, out g)) { g = ng++; map[k] = g; }
                group[i] = g;
            }
            var parent = new int[ng];
            for (int i = 0; i < ng; i++) parent[i] = i;

            int subs = m.subMeshCount;
            d.tri = new int[subs][];
            for (int s = 0; s < subs; s++)
            {
                int[] t = m.GetTriangles(s);
                d.tri[s] = t;
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    int a = Find(parent, group[t[i]]);
                    int b = Find(parent, group[t[i + 1]]);
                    if (a != b) parent[b] = a;
                    int c = Find(parent, group[t[i + 2]]);
                    a = Find(parent, a);
                    if (a != c) parent[c] = a;
                }
            }

            var perRoot = new Dictionary<long, Island>();
            for (int s = 0; s < subs; s++)
            {
                int[] t = d.tri[s];
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    long cacheKey = ((long)s << 32) | (uint)Find(parent, group[t[i]]);
                    Island island;
                    if (!perRoot.TryGetValue(cacheKey, out island)) { island = new Island { sub = s }; perRoot[cacheKey] = island; d.islands.Add(island); }
                    island.tris.Add(i);
                    for (int k = 0; k < 3; k++)
                    {
                        island.Add(d.pRoot[t[i + k]]);
                        island.verts.Add(t[i + k]);
                    }
                }
            }
            foreach (Island island in d.islands) island.nVerts = island.verts.Count;
            return d;
        }

        // Removes from the mesh (its own copy) the triangles of the given islands.
        static void Remove(Data3D d, List<Island> remove)
        {
            if (remove.Count == 0) return;
            Mesh copy = UnityEngine.Object.Instantiate(d.mesh);
            copy.name = d.mesh.name + "_mod";
            for (int s = 0; s < d.tri.Length; s++)
            {
                var take = new HashSet<int>();
                foreach (Island island in remove)
                    if (island.sub == s) foreach (int t in island.tris) take.Add(t);
                if (take.Count == 0) continue;
                var fresh = new List<int>();
                int[] orig = d.tri[s];
                for (int i = 0; i + 2 < orig.Length; i += 3)
                {
                    if (take.Contains(i)) continue;
                    fresh.Add(orig[i]); fresh.Add(orig[i + 1]); fresh.Add(orig[i + 2]);
                }
                copy.SetTriangles(fresh.ToArray(), s);
            }
            copy.hideFlags = HideFlags.DontUnloadUnusedAsset;
            d.mf.sharedMesh = copy;
            if (all != null)
                foreach (Renderer r2 in all)
                {
                    if (r2 == null) continue;
                    MeshFilter f2 = r2.GetComponent<MeshFilter>();
                    if (f2 != null && f2 != d.mf && f2.sharedMesh == d.mesh) f2.sharedMesh = copy;   // the same model repeated on another object
                }
        }

        // Creates an object with only the triangles of an island, as a child of the pivot, in the same place as the original mesh.
        static Renderer CreatePart(Data3D d, Island island, Transform pivot, Material mat)
        {
            Mesh m = d.mesh;
            Vector3[] v = d.v;
            Vector3[] n = m.normals;
            Vector4[] tg = m.tangents;
            Vector2[] uv = m.uv;
            Color[] col = m.colors;

            var map = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nu = new List<Vector2>(); var nc = new List<Color>();
            var tris = new List<int>();
            int[] t = d.tri[island.sub];
            foreach (int ini in island.tris)
            {
                for (int k = 0; k < 3; k++)
                {
                    int old = t[ini + k];
                    int fresh;
                    if (!map.TryGetValue(old, out fresh))
                    {
                        fresh = nv.Count; map[old] = fresh;
                        nv.Add(v[old]);
                        if (n.Length == v.Length) nn.Add(n[old]);
                        if (tg.Length == v.Length) nt.Add(tg[old]);
                        if (uv.Length == v.Length) nu.Add(uv[old]);
                        if (col.Length == v.Length) nc.Add(col[old]);
                    }
                    tris.Add(fresh);
                }
            }

            var pm = new Mesh();
            pm.name = m.name + "_pieza";
            pm.vertices = nv.ToArray();
            if (nn.Count == nv.Count) pm.normals = nn.ToArray();
            if (nt.Count == nv.Count) pm.tangents = nt.ToArray();
            if (nu.Count == nv.Count) pm.uv = nu.ToArray();
            if (nc.Count == nv.Count) pm.colors = nc.ToArray();
            pm.SetTriangles(tris.ToArray(), 0);
            pm.RecalculateBounds();
            if (nn.Count != nv.Count) pm.RecalculateNormals();
            pm.hideFlags = HideFlags.DontUnloadUnusedAsset;

            var go = new GameObject("Mod_Pieza_" + island.sub);
            go.layer = d.rend.gameObject.layer;
            Transform mt = d.mf.transform;
            go.transform.SetParent(pivot, false);
            go.transform.position = mt.position;
            go.transform.rotation = mt.rotation;
            go.transform.localScale = mt.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = pm;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = d.rend.shadowCastingMode;
            mr.receiveShadows = d.rend.receiveShadows;
            return mr;
        }

        // So that the cutout hides with the rest of the vehicle at a distance (LOD), it is registered in each level of the LODGroup.
        static void Register(Renderer r, LODGroup lg)
        {
            if (lg == null || r == null) return;
            LOD[] lods = lg.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                LOD l = lods[i];
                Renderer[] oldOnes = l.renderers;
                var fresh = new Renderer[(oldOnes == null ? 0 : oldOnes.Length) + 1];
                for (int j = 0; j < fresh.Length - 1; j++) fresh[j] = oldOnes[j];
                fresh[fresh.Length - 1] = r;
                l.renderers = fresh;
                lods[i] = l;
            }
            lg.SetLODs(lods);
        }

        static Transform NewPivot(Transform root, Vector3 centerLocal, string names)
        {
            var go = new GameObject(names);
            go.transform.SetParent(root, false);
            go.transform.localPosition = centerLocal;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static Vector3 AxisMoreThin(Vector3[] p, HashSet<int> verts)
        {
            // Direction in which the object is thinnest (for a flat ring, its axis), using power iteration.
            Vector3 c = Vector3.zero;
            foreach (int i in verts) c += p[i];
            c /= Mathf.Max(1, verts.Count);
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (int i in verts)
            {
                Vector3 d = p[i] - c;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            float tr = xx + yy + zz;
            // M = tr*I - C ; its dominant eigenvector is the least "wide" one of C
            float[,] m = { { tr - xx, -xy, -xz }, { -xy, tr - yy, -yz }, { -xz, -yz, tr - zz } };
            Vector3 v = new Vector3(0.577f, 0.5f, 0.64f);
            for (int it = 0; it < 60; it++)
            {
                Vector3 nv = new Vector3(m[0, 0] * v.x + m[0, 1] * v.y + m[0, 2] * v.z, m[1, 0] * v.x + m[1, 1] * v.y + m[1, 2] * v.z, m[2, 0] * v.x + m[2, 1] * v.y + m[2, 2] * v.z);
                if (nv.sqrMagnitude < 1e-12f) break;
                v = nv.normalized;
            }
            return v.normalized;
        }

        // Wheels that are part of the body mesh (no separate tire mesh, like the sedan).
        static void WheelsInBody(Body body, List<Renderer> renders, Vector3 fwdL, Transform root, StringBuilder log)
        {
            var ids = new HashSet<int>();
            foreach (Renderer r in renders)
            {
                string n = r.gameObject.name.ToLowerInvariant();
                if (!n.EndsWith("lod0") || r.GetComponent<MeshFilter>() == null) continue;
                if (n.Contains("door") || n.Contains("glass") || n.Contains("snow") || n.Contains("interior") || n.Contains("visor") || n.Contains("hood") || n.Contains("trunk") || n.Contains("engine")) continue;
                Mesh m = r.GetComponent<MeshFilter>().sharedMesh;
                if (m == null || !ids.Add(m.GetInstanceID())) continue;

                Data3D dt = Analyze(r, root);
                if (dt == null) continue;
                var cand = new List<Island>();
                foreach (Island island in dt.islands)
                    if (Tire(island) && island.center.y < 1.0f) cand.Add(island);
                log.AppendLine("  mesh '" + dt.mesh.name + "' (" + r.gameObject.name + "): " + dt.islands.Count + " islands, " + cand.Count + " wheel-shaped.");
                foreach (Island island in cand)
                    log.AppendLine("    wheel: center " + island.center + " size " + island.b.size + " (forward " + Vector3.Dot(island.center, fwdL).ToString("0.00") + ")");
                if (cand.Count < 4) continue;

                cand.Sort((a, b) => Vector3.Dot(b.center, fwdL).CompareTo(Vector3.Dot(a.center, fwdL)));
                var foreWheels = new List<Island> { cand[0], cand[1] };
                Vector3 rgt = Vector3.Cross(Vector3.up, fwdL);
                if (Vector3.Dot(foreWheels[0].center, rgt) * Vector3.Dot(foreWheels[1].center, rgt) >= 0f)
                {
                    log.AppendLine("  the two front wheels are not on opposite sides: left untouched.");
                    continue;
                }

                LODGroup lg = r.GetComponentInParent<LODGroup>();
                for (int w = 0; w < 2; w++)
                {
                    body.pivotWheel[w] = NewPivot(root, foreWheels[w].center, "Mod_Rueda" + w);
                    Register(CreatePart(dt, foreWheels[w], body.pivotWheel[w], r.sharedMaterials[foreWheels[w].sub]), lg);
                }
                Remove(dt, foreWheels);
                log.AppendLine("  ruedas delanteras separadas de '" + dt.mesh.name + "'.");

                string stem = Stem(r.gameObject.name);
                foreach (Renderer r2 in renders)
                {
                    if (r2 == r || r2.GetComponent<MeshFilter>() == null || Stem(r2.gameObject.name) != stem) continue;
                    if (r2.GetComponent<MeshFilter>().sharedMesh == m) continue;
                    Data3D dl = Analyze(r2, root);
                    if (dl == null) continue;
                    var take = new List<Island>();
                    foreach (Island island in dl.islands)
                        for (int w = 0; w < 2; w++)
                            if (Vector3.Distance(island.center, foreWheels[w].center) < 0.2f && island.ext > foreWheels[w].ext * 0.4f && island.ext < foreWheels[w].ext * 1.6f) { take.Add(island); break; }
                    Remove(dl, take);
                    log.AppendLine("  " + r2.gameObject.name + ": " + take.Count + " pieza(s) de rueda quitadas.");
                }
                return;
            }
        }

        // ====================================================================================== creation

        /// <param name="ojos">positions of the front seats (world); each one is tried</param>
        /// <param name="fwd">front of the vehicle (world, horizontal)</param>
        public static Body Create(Transform root, Vector3[] eyes, Vector3 fwd)
        {
            var body = new Body();
            var log = new StringBuilder();
            log.AppendLine("=== Mesh cutting in '" + root.name + "' (" + DateTime.Now + ") ===");
            try
            {
                var eyesL = new List<Vector3>();
                foreach (Vector3 o in eyes) eyesL.Add(root.InverseTransformPoint(o));
                Vector3 fwdL = root.InverseTransformDirection(fwd);
                fwdL.y = 0f; fwdL.Normalize();
                Vector3 upL = Vector3.up;

                var renders = new List<Renderer>();
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) if (r != null) renders.Add(r);
                all = renders;

                // ---------------- steering wheel (interior mesh)
                // All the different interior meshes are checked (the sedan has two) and the island that fits best is kept.
                var interior = new List<Renderer>();
                foreach (Renderer r in renders)
                {
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (n.Contains("interior") && !n.Contains("door") && r.GetComponent<MeshFilter>() != null) interior.Add(r);
                }
                interior.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, StringComparison.Ordinal));

                var reps = new List<Renderer>();
                var idsMesh = new HashSet<int>();
                foreach (Renderer r in interior)
                {
                    if (!r.gameObject.name.EndsWith("LOD0", StringComparison.OrdinalIgnoreCase)) continue;
                    Mesh mm0 = r.GetComponent<MeshFilter>().sharedMesh;
                    if (mm0 == null || !idsMesh.Add(mm0.GetInstanceID())) continue;
                    reps.Add(r);
                }
                if (reps.Count == 0 && interior.Count > 0) reps.Add(interior[0]);

                Data3D dBest = null; Renderer rBest = null; Island best = null; float bestD = 0.8f; Vector3 eyeSel = eyesL.Count > 0 ? eyesL[0] : Vector3.zero;
                foreach (Renderer rep in reps)
                {
                    Data3D dd = Analyze(rep, root);
                    if (dd == null) continue;
                    log.AppendLine("Steering wheel: mesh '" + dd.mesh.name + "' (" + rep.gameObject.name + "), " + dd.islands.Count + " islands.");
                    int idx = 0;
                    foreach (Island island in dd.islands)
                    {
                        idx++;
                        if (island.nVerts < 12) continue;
                        foreach (Vector3 eyeL in eyesL)
                        {
                            Vector3 estimated = eyeL + fwdL * Cfg.SteeringForward.Value - upL * Cfg.SteeringDown.Value;
                            float dist = Vector3.Distance(island.center, estimated);
                            if (dist < 1.2f)
                                log.AppendLine("  island #" + idx + " verts " + island.nVerts + " size " + island.b.size + " distance to estimated center " + dist.ToString("0.00") + " (seat " + eyeL + ")");
                            if (island.ext < 0.25f || island.ext > 0.7f) continue;
                            if (dist < bestD) { bestD = dist; best = island; dBest = dd; rBest = rep; eyeSel = eyeL; }
                        }
                    }
                }

                if (best == null)
                {
                    log.AppendLine("  NO steering-wheel-shaped island found (size 0.25-0.7 m near the estimated center).");
                }
                else
                {
                    Vector3 aPca = AxisMoreThin(dBest.pRoot, best.verts);
                    Vector3 aEye = (eyeSel - best.center).normalized;
                    Vector3 axis = Mathf.Abs(Vector3.Dot(aPca, aEye)) > 0.75f ? aPca : aEye;
                    if (Vector3.Dot(axis, aEye) < 0f) axis = -axis;
                    axis.Normalize();
                    body.axisSteering = axis;
                    body.pivotSteering = NewPivot(root, best.center, "Mod_Volante");

                    // The ring and also the center (horn, cover): islands concentric with the ring.
                    var segments = new List<Island> { best };
                    foreach (Island island in dBest.islands)
                    {
                        if (island == best || island.nVerts < 6 || island.ext > best.ext * 1.05f) continue;
                        Vector3 v = island.center - best.center;
                        float ax = Vector3.Dot(v, axis);
                        float rad = (v - axis * ax).magnitude;
                        if (rad <= 0.09f && Mathf.Abs(ax) <= 0.12f) segments.Add(island);
                    }

                    // The model has tilted spokes. Their tilt is measured so that "straight" looks straight.
                    Island rays = null;
                    foreach (Island pt in segments) if (pt != best && (rays == null || pt.nVerts > rays.nVerts)) rays = pt;
                    if (rays != null)
                    {
                        Vector3 dR = Vector3.Cross(Vector3.up, fwdL);
                        Vector3 hh = dR - axis * Vector3.Dot(dR, axis); hh.Normalize();
                        Vector3 vv = Vector3.up - axis * Vector3.Dot(Vector3.up, axis); vv.Normalize();
                        Vector3 cm = Vector3.zero;
                        foreach (int vi in rays.verts) cm += dBest.pRoot[vi];
                        cm /= Mathf.Max(1, rays.verts.Count);
                        float sxx = 0f, sxy = 0f, syy = 0f;
                        foreach (int vi in rays.verts)
                        {
                            Vector3 dd2 = dBest.pRoot[vi] - cm;
                            float x = Vector3.Dot(dd2, hh), y = Vector3.Dot(dd2, vv);
                            sxx += x * x; sxy += x * y; syy += y * y;
                        }
                        float mid = (sxx + syy) * 0.5f;
                        float diff = Mathf.Sqrt(Mathf.Max(0f, (sxx - syy) * (sxx - syy) * 0.25f + sxy * sxy));
                        float l1 = mid + diff, l2 = Mathf.Max(mid - diff, 1e-8f);
                        float fi = 0.5f * Mathf.Atan2(2f * sxy, sxx - syy) * Mathf.Rad2Deg;      // angle of the spokes, counterclockwise as seen by the driver
                        log.AppendLine("  spokes: " + rays.nVerts + " verts, elongation " + (l1 / l2).ToString("0.00") + ", angle " + fi.ToString("0.0") + "°");
                        if (l1 / l2 > 1.6f)
                        {
                            float c0 = Mathf.DeltaAngle(0f, fi);
                            while (c0 > 90f) c0 -= 180f;
                            while (c0 <= -90f) c0 += 180f;
                            body.zero = c0;
                            log.AppendLine("  steering wheel center corrected: " + c0.ToString("0.0") + "°");
                        }
                        else log.AppendLine("  the spokes have no clear direction: center not corrected (can be adjusted in Mod Options).");
                    }

                    LODGroup lg = rBest.GetComponentInParent<LODGroup>();
                    foreach (Island pt in segments)
                    {
                        Material mat = rBest.sharedMaterials[pt.sub];
                        Register(CreatePart(dBest, pt, body.pivotSteering, mat), lg);
                        log.AppendLine("  steering wheel piece: verts " + pt.nVerts + " size " + pt.b.size);
                    }
                    Remove(dBest, segments);
                    log.AppendLine("  STEERING WHEEL chosen (" + segments.Count + " pieces): rim " + best.nVerts + " verts, size " + best.b.size + ", axis " + axis);

                    // The other levels of detail: remove the steering wheel in the same way.
                    string stem = Stem(rBest.gameObject.name);
                    foreach (Renderer r in interior)
                    {
                        if (r == rBest || Stem(r.gameObject.name) != stem) continue;
                        if (r.GetComponent<MeshFilter>().sharedMesh == rBest.GetComponent<MeshFilter>().sharedMesh) continue;
                        Data3D dl = Analyze(r, root);
                        if (dl == null) continue;
                        var take = new List<Island>();
                        foreach (Island island in dl.islands)
                            if (Vector3.Distance(island.center, best.center) < 0.14f && island.ext >= 0.06f && island.ext <= best.ext * 1.6f) take.Add(island);
                        Remove(dl, take);
                        log.AppendLine("  " + r.gameObject.name + ": " + take.Count + " isla(s) quitadas.");
                    }
                }
                if (interior.Count == 0) log.AppendLine("Steering wheel: no interior mesh found.");

                // ---------------- front wheels (tires + rims)
                Renderer tireR = null;
                var rimsR = new List<Renderer>();
                foreach (Renderer r in renders)
                {
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (r.GetComponent<MeshFilter>() == null) continue;
                    if (n.Contains("tire")) { if (tireR == null || n.EndsWith("lod0")) tireR = r; }
                    else if (!n.Contains("snow"))
                    {
                        foreach (Material mm in r.sharedMaterials)
                            if (mm != null && mm.name.ToLowerInvariant().Contains("wheel")) { rimsR.Add(r); break; }
                    }
                }

                if (tireR != null)
                {
                    Data3D dt = Analyze(tireR, root);
                    if (dt != null)
                    {
                        var cand = new List<Island>();
                        foreach (Island island in dt.islands)
                            if (island.nVerts >= 20 && island.ext > 0.55f && island.ext < 1.3f) cand.Add(island);
                        log.AppendLine("Wheels: mesh '" + dt.mesh.name + "', " + dt.islands.Count + " islands, " + cand.Count + " tire-shaped.");
                        foreach (Island island in cand)
                            log.AppendLine("  tire: center " + island.center + " size " + island.b.size + " (forward " + Vector3.Dot(island.center, fwdL).ToString("0.00") + ")");

                        cand.Sort((a, b) => Vector3.Dot(b.center, fwdL).CompareTo(Vector3.Dot(a.center, fwdL)));
                        var foreWheels = new List<Island>();
                        foreach (Island island in cand) { if (foreWheels.Count < 2) foreWheels.Add(island); }

                        if (cand.Count >= 4 && foreWheels.Count == 2)
                        {
                            LODGroup lgT = tireR.GetComponentInParent<LODGroup>();
                            Data3D[] rims = new Data3D[rimsR.Count];
                            for (int i = 0; i < rimsR.Count; i++) rims[i] = Analyze(rimsR[i], root);

                            for (int w = 0; w < 2; w++)
                            {
                                Island ti = foreWheels[w];
                                body.pivotWheel[w] = NewPivot(root, ti.center, "Mod_Rueda" + w);
                                Register(CreatePart(dt, ti, body.pivotWheel[w], tireR.sharedMaterials[ti.sub]), lgT);
                            }
                            Remove(dt, foreWheels);

                            // The rims (part of the body with the wheels' material) that are attached to each front tire.
                            for (int li = 0; li < rimsR.Count; li++)
                            {
                                Data3D dl = rims[li];
                                if (dl == null) continue;
                                bool isLod0 = rimsR[li].gameObject.name.EndsWith("LOD0");
                                LODGroup lgL = rimsR[li].GetComponentInParent<LODGroup>();
                                var take = new List<Island>();
                                for (int w = 0; w < 2; w++)
                                {
                                    foreach (Island island in dl.islands)
                                    {
                                        if (island.nVerts < 8 || island.ext > 1.0f) continue;
                                        Material mm = rimsR[li].sharedMaterials[island.sub];
                                        if (mm == null || !mm.name.ToLowerInvariant().Contains("wheel")) continue;
                                        if (Vector3.Distance(island.center, foreWheels[w].center) > 0.45f) continue;
                                        take.Add(island);
                                        if (isLod0) Register(CreatePart(dl, island, body.pivotWheel[w], mm), lgL);
                                    }
                                }
                                Remove(dl, take);
                                log.AppendLine("  " + rimsR[li].gameObject.name + ": " + take.Count + " pieza(s) de llanta pasadas a las ruedas delanteras.");
                            }
                        }
                        else
                        {
                            log.AppendLine("  Not exactly 4 recognizable tires: wheels are not turned.");
                        }
                    }
                }
                else
                {
                    log.AppendLine("Wheels: no separate tire mesh; looking for wheels inside the body meshes.");
                    WheelsInBody(body, renders, fwdL, root, log);
                }

                if (body.pivotSteering == null || body.pivotWheel[0] == null)
                {
                    log.AppendLine("--- Vehicle parts (to tune the search) ---");
                    foreach (Renderer r in renders)
                    {
                        MeshFilter f = r.GetComponent<MeshFilter>();
                        if (f == null || f.sharedMesh == null) continue;
                        string mats = "";
                        foreach (Material mm in r.sharedMaterials) mats += (mm != null ? mm.name : "?") + ", ";
                        log.AppendLine("  " + r.gameObject.name + " | malla " + f.sharedMesh.name + " | legible " + f.sharedMesh.isReadable + " | tam " + f.sharedMesh.bounds.size + " | mats " + mats);
                    }
                }
            }
            catch (Exception e)
            {
                log.AppendLine("ERROR: " + e);
                MelonLoader.MelonLogger.Warning("[Meshes] Error cutting out the steering wheel/wheels: " + e.Message);
            }

            try
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "WorkingVehicles_meshes.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                if (Cfg.Report) File.AppendAllText(filePath, log.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }

            MelonLoader.MelonLogger.Msg("[Meshes] '" + root.name + "': steering wheel " + (body.pivotSteering != null ? "OK" : "NO") + ", front wheels " + (body.pivotWheel[0] != null ? "OK" : "NO") + ". Details in UserData\\WorkingVehicles_meshes.txt");
            return body;
        }
    }
}
