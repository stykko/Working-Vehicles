using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>Shape generator with higher quality than Unity's cubes and cylinders: boxes with rounded edges, surfaces of revolution, prisms (bolts), curved tubes.</summary>
    internal static class Geo
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        static string K(float f) { return f.ToString("F4", System.Globalization.CultureInfo.InvariantCulture); }

        static Mesh Make(List<Vector3> v, List<Vector3> n, List<int> t, string names)
        {
            var m = new Mesh();
            m.name = names;
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = v.ToArray();
            m.normals = n.ToArray();
            m.triangles = t.ToArray();
            // texture coordinates: box projection (depending on where each face looks), so the parts can be painted with a texture
            var uv = new Vector2[v.Count];
            for (int i = 0; i < uv.Length; i++)
            {
                Vector3 p = v[i], q = n[i];
                float ax = Mathf.Abs(q.x), ay = Mathf.Abs(q.y), az = Mathf.Abs(q.z);
                if (ay >= ax && ay >= az) uv[i] = new Vector2(p.x, p.z) * 3f;
                else if (ax >= az) uv[i] = new Vector2(p.z, p.y) * 3f;
                else uv[i] = new Vector2(p.x, p.y) * 3f;
            }
            m.uv = uv;
            m.RecalculateBounds();
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return m;
        }

        // ------------------------------------------------------------------ box with rounded edges

        public static Mesh Box(Vector3 size, float radius, int segs)
        {
            float rmax = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f * 0.98f;
            float r = Mathf.Clamp(radius, 0.0005f, rmax);
            string cacheKey = "caja" + K(size.x) + K(size.y) + K(size.z) + K(r) + segs;
            Mesh m;
            if (cache.TryGetValue(cacheKey, out m) && m != null) return m;

            float[] h = { size.x * 0.5f, size.y * 0.5f, size.z * 0.5f };
            var raw = new List<float>();
            var sideSign = new List<int>();
            for (int k = segs; k >= 0; k--) { raw.Add(-Mathf.Tan(k * Mathf.PI * 0.25f / segs)); sideSign.Add(-1); }
            for (int k = 0; k <= segs; k++) { raw.Add(Mathf.Tan(k * Mathf.PI * 0.25f / segs)); sideSign.Add(1); }
            int T = raw.Count;

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tr = new List<int>();
            for (int a = 0; a < 3; a++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int b = (a + 1) % 3, c = (a + 2) % 3;
                    int baseI = v.Count;
                    for (int i = 0; i < T; i++)
                        for (int j = 0; j < T; j++)
                        {
                            var nn = new float[3];
                            nn[a] = s; nn[b] = raw[i]; nn[c] = raw[j];
                            Vector3 nv = new Vector3(nn[0], nn[1], nn[2]).normalized;
                            var pos = new float[3];
                            pos[a] = s * (h[a] - r);
                            pos[b] = sideSign[i] * (h[b] - r);
                            pos[c] = sideSign[j] * (h[c] - r);
                            v.Add(new Vector3(pos[0], pos[1], pos[2]) + nv * r);
                            n.Add(nv);
                        }
                    for (int i = 0; i < T - 1; i++)
                        for (int j = 0; j < T - 1; j++)
                        {
                            int i0 = baseI + i * T + j, i1 = i0 + 1, i2 = i0 + T, i3 = i2 + 1;
                            if (s > 0) { tr.Add(i0); tr.Add(i2); tr.Add(i1); tr.Add(i1); tr.Add(i2); tr.Add(i3); }
                            else { tr.Add(i0); tr.Add(i1); tr.Add(i2); tr.Add(i1); tr.Add(i3); tr.Add(i2); }
                        }
                }
            m = Make(v, n, tr, cacheKey);
            cache[cacheKey] = m;
            return m;
        }

        // ------------------------------------------------------------------ surface of revolution (Y axis)

        /// <summary>
        /// Rotates a profile (radius, height) around the Y axis. The profile must be traversed so that its normal (dy, -dx) points outward:
        /// for example from the axis at the bottom, outward, up along the side and back to the axis at the top.
        /// 'suave' averages the normals between segments; 'planos' makes flat faces (prisms: hex bolts).
        /// </summary>
        public static Mesh Revolution(Vector2[] profile, int sides, bool smooth, bool flat, string cacheKey)
        {
            Mesh m;
            if (cacheKey != null && cache.TryGetValue(cacheKey, out m) && m != null) return m;
            int P = profile.Length;
            var nseg = new Vector2[P - 1];
            for (int s = 0; s < P - 1; s++)
            {
                Vector2 t = profile[s + 1] - profile[s];
                Vector2 nn = new Vector2(t.y, -t.x);
                nseg[s] = nn.sqrMagnitude > 1e-12f ? nn.normalized : Vector2.right;
            }
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tr = new List<int>();
            for (int s = 0; s < P - 1; s++)
            {
                Vector2 n0 = nseg[s], n1 = nseg[s];
                if (smooth)
                {
                    if (s > 0) { Vector2 q = nseg[s - 1] + nseg[s]; if (q.sqrMagnitude > 1e-6f) n0 = q.normalized; }
                    if (s < P - 2) { Vector2 q = nseg[s] + nseg[s + 1]; if (q.sqrMagnitude > 1e-6f) n1 = q.normalized; }
                }
                Vector2 p0 = profile[s], p1 = profile[s + 1];
                for (int k = 0; k < sides; k++)
                {
                    float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                    float am = (a0 + a1) * 0.5f;
                    int b = v.Count;
                    v.Add(new Vector3(p0.x * Mathf.Cos(a0), p0.y, p0.x * Mathf.Sin(a0)));
                    v.Add(new Vector3(p1.x * Mathf.Cos(a0), p1.y, p1.x * Mathf.Sin(a0)));
                    v.Add(new Vector3(p0.x * Mathf.Cos(a1), p0.y, p0.x * Mathf.Sin(a1)));
                    v.Add(new Vector3(p1.x * Mathf.Cos(a1), p1.y, p1.x * Mathf.Sin(a1)));
                    if (flat)
                    {
                        Vector3 nm = new Vector3(nseg[s].x * Mathf.Cos(am), nseg[s].y, nseg[s].x * Mathf.Sin(am)).normalized;
                        n.Add(nm); n.Add(nm); n.Add(nm); n.Add(nm);
                    }
                    else
                    {
                        n.Add(new Vector3(n0.x * Mathf.Cos(a0), n0.y, n0.x * Mathf.Sin(a0)));
                        n.Add(new Vector3(n1.x * Mathf.Cos(a0), n1.y, n1.x * Mathf.Sin(a0)));
                        n.Add(new Vector3(n0.x * Mathf.Cos(a1), n0.y, n0.x * Mathf.Sin(a1)));
                        n.Add(new Vector3(n1.x * Mathf.Cos(a1), n1.y, n1.x * Mathf.Sin(a1)));
                    }
                    // (A_k, B_k, A_k+1) and (A_k+1, B_k, B_k+1)
                    tr.Add(b); tr.Add(b + 1); tr.Add(b + 2);
                    tr.Add(b + 2); tr.Add(b + 1); tr.Add(b + 3);
                }
            }
            m = Make(v, n, tr, cacheKey ?? "rev");
            if (cacheKey != null) cache[cacheKey] = m;
            return m;
        }

        /// <summary>Cylinder (or prism with 'lados' sides) with a chamfer, Y axis.</summary>
        public static Mesh Prism(int sides, float radius, float height, float chamfer)
        {
            string cacheKey = "prisma" + sides + K(radius) + K(height) + K(chamfer);
            float c = Mathf.Min(chamfer, Mathf.Min(radius, height * 0.5f) * 0.9f);
            float h = height * 0.5f;
            Vector2[] p =
            {
                new Vector2(0f, -h), new Vector2(radius - c, -h), new Vector2(radius, -h + c),
                new Vector2(radius, h - c), new Vector2(radius - c, h), new Vector2(0f, h)
            };
            return Revolution(p, sides, false, sides <= 8, cacheKey);
        }

        /// <summary>Torus (ring) in the XZ plane.</summary>
        public static Mesh Torus(float R, float r, int sides, int section)
        {
            string cacheKey = "toro" + K(R) + K(r) + sides + section;
            var p = new Vector2[section + 1];
            for (int i = 0; i <= section; i++)
            {
                float th = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / section;
                p[i] = new Vector2(R + r * Mathf.Cos(th), r * Mathf.Sin(th));
            }
            return Revolution(p, sides, true, false, cacheKey);
        }

        // ------------------------------------------------------------------ curved tube

        /// <summary>Tube that passes smoothly through the points (Catmull-Rom spline). With flat caps at the ends.</summary>
        public static Mesh Tube(Vector3[] pts, float radius, int sides, int sub)
        {
            var route = new List<Vector3>();
            int N = pts.Length;
            for (int i = 0; i < N - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, N - 1)];
                for (int s = 0; s < sub; s++)
                {
                    float t = s / (float)sub, t2 = t * t, t3 = t2 * t;
                    route.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            route.Add(pts[N - 1]);
            int M = route.Count;

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tr = new List<int>();
            Vector3 tang = (route[1] - route[0]).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(tang, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 side = Vector3.Cross(up, tang).normalized;
            Vector3 nor = Vector3.Cross(tang, side).normalized;
            for (int i = 0; i < M; i++)
            {
                Vector3 tg = i == 0 ? route[1] - route[0] : (i == M - 1 ? route[M - 1] - route[M - 2] : route[i + 1] - route[i - 1]);
                if (tg.sqrMagnitude < 1e-10f) tg = tang;
                tg.Normalize();
                // parallel transport of the frame
                side = (side - tg * Vector3.Dot(side, tg));
                if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(Vector3.up, tg);
                side.Normalize();
                nor = Vector3.Cross(tg, side).normalized;
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    Vector3 d = side * Mathf.Cos(a) + nor * Mathf.Sin(a);
                    v.Add(route[i] + d * radius);
                    n.Add(d);
                }
            }
            int R = sides + 1;
            for (int i = 0; i < M - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a0 = i * R + k, a1 = a0 + 1, b0 = a0 + R, b1 = b0 + 1;
                    tr.Add(a0); tr.Add(b0); tr.Add(a1);
                    tr.Add(a1); tr.Add(b0); tr.Add(b1);
                }
            // the turning direction depends on the frame: it is corrected by looking at the first frame
            if (Vector3.Dot(Vector3.Cross(v[tr[1]] - v[tr[0]], v[tr[2]] - v[tr[0]]), n[tr[0]]) < 0f)
                for (int i = 0; i < tr.Count; i += 3) { int tmp = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = tmp; }
            // caps
            for (int e = 0; e < 2; e++)
            {
                int ring = e == 0 ? 0 : (M - 1) * R;
                Vector3 center = route[e == 0 ? 0 : M - 1];
                Vector3 tg = e == 0 ? -(route[1] - route[0]).normalized : (route[M - 1] - route[M - 2]).normalized;
                int ci = v.Count;
                v.Add(center); n.Add(tg);
                int b0 = v.Count;
                for (int k = 0; k < sides; k++) { v.Add(v[ring + k]); n.Add(tg); }
                for (int k = 0; k < sides; k++)
                {
                    int p = b0 + k, q = b0 + (k + 1) % sides;
                    tr.Add(ci); tr.Add(p); tr.Add(q);
                    if (Vector3.Dot(Vector3.Cross(v[p] - v[ci], v[q] - v[ci]), tg) < 0f) { tr[tr.Count - 2] = q; tr[tr.Count - 1] = p; }
                }
            }
            return Make(v, n, tr, "tubo");
        }
    }
}
