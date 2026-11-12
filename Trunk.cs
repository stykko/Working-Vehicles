using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// The trunk button on the dashboard. Cars: unlocks the trunk (a game Container with a Lock) so it can be searched without a prybar.
    /// Pickup truck: its tailgate is part of the body mesh, so the triangles of the tailgate are cut out of the body into their own object,
    /// which then swings open and closed (experimental; only works if the game's mesh is readable).
    /// </summary>
    internal static class Trunk
    {
        /// <summary>Every Lock that guards a trunk of this vehicle (the container's own, the ones on its interactions, and any lock named "trunk").</summary>
        static List<Lock> LocksOf(Transform root)
        {
            var list = new List<Lock>();
            Action<Lock> add = l => { if (l != null && !list.Contains(l)) list.Add(l); };
            foreach (Container c in root.GetComponentsInChildren<Container>(true))
            {
                if (c == null || c.name.IndexOf("trunk", StringComparison.OrdinalIgnoreCase) < 0) continue;
                try { add(c.m_Lock); } catch (Exception) { }
                foreach (Lock l in c.GetComponentsInChildren<Lock>(true)) add(l);
                add(c.GetComponentInParent<Lock>());
                foreach (LockedInteraction li in c.GetComponentsInChildren<LockedInteraction>(true)) { try { add(li.Lock); } catch (Exception) { } }
                foreach (LockedInteraction li in c.GetComponentsInParent<LockedInteraction>(true)) { try { add(li.Lock); } catch (Exception) { } }
            }
            foreach (Lock l in root.GetComponentsInChildren<Lock>(true))
                if (l != null && l.name.IndexOf("trunk", StringComparison.OrdinalIgnoreCase) >= 0) add(l);
            return list;
        }

        static bool IsUnlockedCar(Transform root)
        {
            try
            {
                var locks = LocksOf(root);
                if (locks.Count == 0) return false;
                foreach (Lock l in locks) if (l.IsLocked()) return false;
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Unlocks the trunk. Returns the message for the player.</summary>
        static string UnlockCar(Transform root)
        {
            try
            {
                if (root == null) return null;
                var locks = LocksOf(root);
                if (locks.Count == 0) return L.T("Este vehículo no tiene maletero con llave", "This vehicle has no locked trunk");
                int unlocked = 0;
                foreach (Lock l in locks)
                {
                    bool was = l.IsLocked();
                    if (was)
                    {
                        l.SetLockState(LockState.Unlocked);
                        try { l.m_LockStateRolled = true; l.m_WasEverLocked = true; } catch (Exception) { }
                        try { l.PlayUnlockAudio(); } catch (Exception) { }
                        unlocked++;
                    }
                    MelonLoader.MelonLogger.Msg("[Trunk] '" + root.name + "' lock '" + l.name + "' (in '" + (l.transform.parent != null ? l.transform.parent.name : "-") + "'): " + (was ? "was locked" : "already unlocked") + ", now locked = " + l.IsLocked());
                }
                if (unlocked > 0) return L.T("Maletero desbloqueado: ya podés revisarlo desde afuera", "Trunk unlocked: you can search it from outside now");
                return L.T("El maletero ya está sin llave", "The trunk is already unlocked");
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Warning("[Trunk] " + e.Message);
                return L.T("No se pudo desbloquear el maletero", "Could not unlock the trunk");
            }
        }

        // ------------------------------------------------------------------ pickup: tailgate

        class Gate
        {
            public Transform root;
            public readonly List<Transform> pieces = new List<Transform>();
            public Vector3 pivotLocal, axisLocal = Vector3.right;
            public float sign = 1f, cur;
            public bool open, failed, dbl;
            public readonly List<Swap> swaps = new List<Swap>();
        }

        /// <summary>The two versions of a cut mesh: plain (normal use) and double-sided (only while the gate is down, so the open edges are not see-through).</summary>
        class Swap
        {
            public MeshFilter gateMf, bodyMf;
            public Mesh gateClosed, gateOpen, bodyClosed, bodyOpen;
        }

        static readonly Dictionary<int, Gate> gates = new Dictionary<int, Gate>();
        const float OpenAngle = 88f, Speed = 150f;      // degrees and degrees per second

        public static void Reset() { gates.Clear(); }

        public static bool IsOpen(Transform root, bool car)
        {
            if (root == null) return false;
            if (car) return IsUnlockedCar(root);
            Gate g;
            return gates.TryGetValue(root.GetInstanceID(), out g) && g.open;
        }

        public static string Toggle(Transform root, bool car)
        {
            try
            {
                if (root == null) return null;
                if (car) return UnlockCar(root);
                Gate g;
                int id = root.GetInstanceID();
                if (!gates.TryGetValue(id, out g))
                {
                    g = new Gate { root = root };
                    gates[id] = g;
                    if (!Cut(g)) g.failed = true;
                }
                if (g.failed) return L.T("No pude separar la compuerta de este modelo (mirá el log, líneas [Trunk])", "Could not separate the tailgate from this model (see the log, [Trunk] lines)");
                g.open = !g.open;
                return g.open ? L.T("Compuerta trasera abierta", "Tailgate open") : L.T("Compuerta trasera cerrada", "Tailgate closed");
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Warning("[Trunk] " + e);
                return L.T("No se pudo abrir la compuerta", "Could not open the tailgate");
            }
        }

        class Part
        {
            public MeshFilter mf; public MeshRenderer mr; public Mesh mesh;
            public Vector3[] pl;              // vertices in the vehicle's space
            public int[][] tris;
        }

        static bool Mine(Transform t, Transform root)
        {
            for (Transform a = t; a != null && a != root; a = a.parent)
                if (a.name.StartsWith("WV", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Finds the tailgate triangles in the body meshes and moves them to their own object. Returns false if it cannot.</summary>
        static bool Cut(Gate g)
        {
            Transform root = g.root;
            var parts = new List<Part>();
            var report = new StringBuilder("[Trunk] '" + root.name + "' meshes:");
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null || Mine(mf.transform, root)) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                Mesh m = mf.sharedMesh;
                string n = mf.name.ToLowerInvariant();
                if (n.Contains("snow") || n.Contains("wheel") || n.Contains("tire")) continue;
                if (!m.isReadable) { report.Append(" | ").Append(mf.name).Append(" (not readable)"); continue; }
                var p = new Part { mf = mf, mr = mr, mesh = m };
                Vector3[] v = m.vertices;
                p.pl = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) p.pl[i] = root.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                p.tris = new int[m.subMeshCount][];
                for (int s = 0; s < m.subMeshCount; s++) p.tris[s] = m.GetTriangles(s);
                Bounds b = new Bounds(p.pl[0], Vector3.zero);
                foreach (Vector3 q in p.pl) b.Encapsulate(q);
                report.Append(" | ").Append(mf.name).Append(" v").Append(v.Length).Append(" size ").Append(b.size.x.ToString("0.0")).Append("x").Append(b.size.y.ToString("0.0")).Append("x").Append(b.size.z.ToString("0.0")).Append(" zmin ").Append(b.min.z.ToString("0.00"));
                if (b.size.x < 1.0f || b.size.y < 0.3f) continue;       // too small to be the body (lights, mirrors...)
                parts.Add(p);
            }
            MelonLoader.MelonLogger.Msg(report.ToString());
            if (parts.Count == 0) return false;

            // rear end of the body (the front of the vehicle is +Z)
            float rear = float.MaxValue;
            foreach (Part p in parts) foreach (Vector3 q in p.pl) if (Mathf.Abs(q.x) < 0.9f && q.z < rear) rear = q.z;
            const float Depth = 0.30f;
            float HalfWidth = 0.98f;

            // the rear slab: triangles close to the back, between the corners
            float lowY = float.MaxValue, hiY = float.MinValue;
            foreach (Part p in parts)
                foreach (int[] tr in p.tris)
                    for (int i = 0; i + 2 < tr.Length; i += 3)
                    {
                        if (!InSlab(p.pl, tr, i, rear, Depth, HalfWidth)) continue;
                        float cy = (p.pl[tr[i]].y + p.pl[tr[i + 1]].y + p.pl[tr[i + 2]].y) / 3f;
                        if (cy < lowY) lowY = cy;
                        if (cy > hiY) hiY = cy;
                    }
            if (hiY <= lowY) { MelonLoader.MelonLogger.Msg("[Trunk] no rear surface found (rear z " + rear.ToString("0.00") + ")"); return false; }
            float cutY = lowY + 0.42f * (hiY - lowY);
            MelonLoader.MelonLogger.Msg("[Trunk] rear z " + rear.ToString("0.00") + ", slab y " + lowY.ToString("0.00") + " .. " + hiY.ToString("0.00") + ", gate from y " + cutY.ToString("0.00"));

            // split each mesh
            int total = 0;
            Bounds gb = new Bounds();
            bool gbSet = false;
            var work = new List<KeyValuePair<Part, List<int>[]>>();
            foreach (Part p in parts)
            {
                var gateTris = new List<int>[p.tris.Length];
                int count = 0;
                for (int s = 0; s < p.tris.Length; s++)
                {
                    gateTris[s] = new List<int>();
                    int[] tr = p.tris[s];
                    for (int i = 0; i + 2 < tr.Length; i += 3)
                    {
                        if (!InSlab(p.pl, tr, i, rear, Depth, HalfWidth)) continue;
                        float cy = (p.pl[tr[i]].y + p.pl[tr[i + 1]].y + p.pl[tr[i + 2]].y) / 3f;
                        if (cy < cutY) continue;
                        gateTris[s].Add(tr[i]); gateTris[s].Add(tr[i + 1]); gateTris[s].Add(tr[i + 2]);
                        count++;
                        for (int k = 0; k < 3; k++)
                        {
                            if (!gbSet) { gb = new Bounds(p.pl[tr[i + k]], Vector3.zero); gbSet = true; }
                            else gb.Encapsulate(p.pl[tr[i + k]]);
                        }
                    }
                }
                MelonLoader.MelonLogger.Msg("[Trunk] '" + p.mf.name + "': " + count + " tailgate triangles");
                if (count > 0) work.Add(new KeyValuePair<Part, List<int>[]>(p, gateTris));
                total += count;
            }
            if (total < 12 || !gbSet) { MelonLoader.MelonLogger.Msg("[Trunk] not enough tailgate triangles (" + total + ")"); return false; }
            MelonLoader.MelonLogger.Msg("[Trunk] tailgate region in the vehicle's space: center " + gb.center.ToString("F2") + " size " + gb.size.ToString("F2"));

            var bodyOf = new Dictionary<int, Mesh>();
            foreach (var kv in work)
            {
                Part p = kv.Key;
                var gateTris = kv.Value;
                Mesh gate = UnityEngine.Object.Instantiate(p.mesh);
                Mesh body = UnityEngine.Object.Instantiate(p.mesh);
                Mesh gateD = UnityEngine.Object.Instantiate(p.mesh);
                Mesh bodyD = UnityEngine.Object.Instantiate(p.mesh);
                gate.name = p.mesh.name + "_WVgate";
                body.name = p.mesh.name + "_WVbody";
                gateD.name = gate.name + "_2s";
                bodyD.name = body.name + "_2s";
                for (int s = 0; s < p.tris.Length; s++)
                {
                    var gt = gateTris[s];
                    int[] tr = p.tris[s];
                    var set = new HashSet<long>();
                    for (int i = 0; i + 2 < gt.Count; i += 3) set.Add(Key(gt[i], gt[i + 1], gt[i + 2]));
                    var rest = new List<int>();
                    for (int i = 0; i + 2 < tr.Length; i += 3)
                        if (!set.Contains(Key(tr[i], tr[i + 1], tr[i + 2]))) { rest.Add(tr[i]); rest.Add(tr[i + 1]); rest.Add(tr[i + 2]); }
                    // double-sided near the cut, so the open edges are not see-through
                    var gt2 = new List<int>(gt);
                    for (int i = 0; i + 2 < gt.Count; i += 3) { gt2.Add(gt[i]); gt2.Add(gt[i + 2]); gt2.Add(gt[i + 1]); }
                    var body2 = new List<int>(rest);
                    for (int i = 0; i + 2 < rest.Count; i += 3)
                    {
                        Vector3 q0 = p.pl[rest[i]], q1 = p.pl[rest[i + 1]], q2 = p.pl[rest[i + 2]];
                        float cy = (q0.y + q1.y + q2.y) / 3f;
                        if (cy < cutY - 0.25f || q0.z > rear + 0.6f || q1.z > rear + 0.6f || q2.z > rear + 0.6f) continue;
                        if (Mathf.Abs(q0.x) > 1.25f || Mathf.Abs(q1.x) > 1.25f || Mathf.Abs(q2.x) > 1.25f) continue;
                        body2.Add(rest[i]); body2.Add(rest[i + 2]); body2.Add(rest[i + 1]);
                    }
                    gate.SetTriangles(gt.ToArray(), s);
                    body.SetTriangles(rest.ToArray(), s);
                    gateD.SetTriangles(gt2.ToArray(), s);
                    bodyD.SetTriangles(body2.ToArray(), s);
                }
                gate.RecalculateBounds();
                body.RecalculateBounds();
                gateD.RecalculateBounds();
                bodyD.RecalculateBounds();
                p.mf.sharedMesh = body;
                bodyOf[p.mesh.GetInstanceID()] = body;
                var go = new GameObject("WV_Tailgate");
                go.layer = p.mf.gameObject.layer;
                go.transform.SetParent(p.mf.transform, false);
                MeshFilter gmf = go.AddComponent<MeshFilter>();
                gmf.sharedMesh = gate;
                g.swaps.Add(new Swap { gateMf = gmf, bodyMf = p.mf, gateClosed = gate, gateOpen = gateD, bodyClosed = body, bodyOpen = bodyD });
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = p.mr.sharedMaterials;
                r.shadowCastingMode = p.mr.shadowCastingMode;
                // the gate's own collision (it travels with the gate)
                // size taken from the vertices the gate really uses (the copied mesh still holds all of the body's vertices)
                Bounds lb = new Bounds();
                bool lbSet = false;
                foreach (List<int> gtl in gateTris)
                    foreach (int vi in gtl)
                    {
                        Vector3 lp = go.transform.InverseTransformPoint(root.TransformPoint(p.pl[vi]));
                        if (!lbSet) { lb = new Bounds(lp, Vector3.zero); lbSet = true; } else lb.Encapsulate(lp);
                    }
                if (lbSet)
                {
                    gate.bounds = lb;
                    gateD.bounds = lb;
                    var box = go.AddComponent<BoxCollider>();
                    box.center = lb.center;
                    box.size = Vector3.Max(lb.size, new Vector3(0.05f, 0.05f, 0.05f));
                    MelonLoader.MelonLogger.Msg("[Trunk] gate collider (" + p.mf.name + "): size " + box.size.ToString("F2"));
                }
                g.pieces.Add(go.transform);
            }

            // the body's own collision: take the closed tailgate out of it
            int colFixed = 0;
            var colLog = new StringBuilder("[Trunk] colliders near the rear:");
            foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc == null || mc.isTrigger || mc.sharedMesh == null || Mine(mc.transform, root)) continue;
                Mesh orig = mc.sharedMesh;
                Mesh made;
                if (bodyOf.TryGetValue(orig.GetInstanceID(), out made)) { mc.sharedMesh = made; colFixed++; colLog.Append(" | MeshCollider ").Append(mc.name).Append(" (visual mesh)"); continue; }
                if (!orig.isReadable) { colLog.Append(" | MeshCollider ").Append(mc.name).Append(" (not readable)"); continue; }
                Vector3[] v = orig.vertices;
                var pl = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) pl[i] = root.InverseTransformPoint(mc.transform.TransformPoint(v[i]));
                var tris = new int[orig.subMeshCount][];
                for (int s = 0; s < tris.Length; s++) tris[s] = orig.GetTriangles(s);
                int removed = 0;
                var kept = new List<int>[tris.Length];
                for (int s = 0; s < tris.Length; s++)
                {
                    kept[s] = new List<int>();
                    for (int i = 0; i + 2 < tris[s].Length; i += 3)
                    {
                        bool inGate = InSlab(pl, tris[s], i, rear, Depth, HalfWidth) && (pl[tris[s][i]].y + pl[tris[s][i + 1]].y + pl[tris[s][i + 2]].y) / 3f >= cutY;
                        if (inGate) { removed++; continue; }
                        kept[s].Add(tris[s][i]); kept[s].Add(tris[s][i + 1]); kept[s].Add(tris[s][i + 2]);
                    }
                }
                colLog.Append(" | MeshCollider ").Append(mc.name).Append(" (-").Append(removed).Append(" triangles)");
                if (removed == 0) continue;
                Mesh cm = UnityEngine.Object.Instantiate(orig);
                cm.name = orig.name + "_WVcol";
                for (int s = 0; s < tris.Length; s++) cm.SetTriangles(kept[s].ToArray(), s);
                mc.sharedMesh = cm;
                colFixed++;
            }
            foreach (BoxCollider bc in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (bc == null || bc.isTrigger || !bc.enabled || Mine(bc.transform, root) || bc.name == "WV_Tailgate") continue;
                Bounds w = bc.bounds;
                Vector3 bcen = root.InverseTransformPoint(w.center);
                Vector3 sz = w.size;
                if (bcen.z > rear + 0.8f || bcen.y < 0.2f) continue;
                colLog.Append(" | Box ").Append(bc.name).Append(" c ").Append(bcen.ToString("F2")).Append(" s ").Append(sz.ToString("F2"));
                // a thin box over the tailgate area is the closed gate: the gate piece has its own
                if (bcen.y >= cutY - 0.15f && Mathf.Abs(bcen.x) < 1.0f && sz.x < 2.3f && Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)) < 0.4f && bcen.z < rear + 0.45f)
                {
                    bc.enabled = false; colFixed++;
                    colLog.Append(" (disabled)");
                }
            }
            MelonLoader.MelonLogger.Msg(colLog.ToString());
            MelonLoader.MelonLogger.Msg("[Trunk] colliders adjusted: " + colFixed);

            g.pivotLocal = new Vector3(gb.center.x, gb.min.y, gb.min.z);      // bottom rear edge
            // open towards the back: the sign that sends the top of the gate to smaller Z (the rear), not into the bed
            Vector3 c = gb.center - g.pivotLocal;
            Vector3 a1 = Quaternion.AngleAxis(OpenAngle, g.axisLocal) * c;
            Vector3 a2 = Quaternion.AngleAxis(-OpenAngle, g.axisLocal) * c;
            g.sign = a1.z < a2.z ? 1f : -1f;
            MelonLoader.MelonLogger.Msg("[Trunk] '" + root.name + "': tailgate separated (" + g.pieces.Count + " pieces)");
            return true;
        }

        static long Key(int a, int b, int c) { return ((long)a << 42) ^ ((long)b << 21) ^ (long)c; }

        static bool InSlab(Vector3[] pl, int[] tr, int i, float rear, float depth, float half)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector3 q = pl[tr[i + k]];
                if (q.z > rear + depth || Mathf.Abs(q.x) > half) return false;
            }
            return true;
        }

        /// <summary>Moves the tailgates (called every frame after the game's own updates).</summary>
        public static void Tick(float dt)
        {
            if (gates.Count == 0) return;
            foreach (Gate g in gates.Values)
            {
                if (g.failed || g.root == null) continue;
                float target = g.open ? OpenAngle : 0f;
                g.cur = Mathf.MoveTowards(g.cur, target, Speed * dt);
                bool down = g.open || g.cur > 0.001f;
                if (down != g.dbl)
                {
                    g.dbl = down;
                    foreach (Swap sw in g.swaps)
                    {
                        if (sw.gateMf != null) sw.gateMf.sharedMesh = down ? sw.gateOpen : sw.gateClosed;
                        if (sw.bodyMf != null) sw.bodyMf.sharedMesh = down ? sw.bodyOpen : sw.bodyClosed;
                    }
                }
                Vector3 pivot = g.root.TransformPoint(g.pivotLocal);
                Quaternion q = Quaternion.AngleAxis(g.sign * g.cur, g.root.TransformDirection(g.axisLocal));
                foreach (Transform t in g.pieces)
                {
                    if (t == null || t.parent == null) continue;
                    Vector3 wp = t.parent.position;
                    Quaternion wr = t.parent.rotation;
                    t.SetPositionAndRotation(pivot + q * (wp - pivot), q * wr);
                }
            }
        }
    }
}
