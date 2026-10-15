using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>Data of the trucks' fuel tank (diesel).</summary>
    internal static class DataTank
    {
        public const float Capacity = 60f;        // liters
        public const float Airflow = 4f;            // liters per second when filling at the pump
    }

    /// <summary>
    /// A truck's fuel cap: it opens like the hood (the game itself rotates the "...FuelCoverHinge" hinge).
    /// </summary>
    internal class Tank
    {
        Transform root, hinge;
        Quaternion rotClosed;
        Renderer[] parts;
        public bool open;
        const float AngleOpenState = 12f;

        public static Tank Create(Transform root)
        {
            try
            {
                Transform h = null;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.ToLowerInvariant().Contains("fuelcoverhinge")) { h = t; break; }
                if (h == null)
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        string n = t.name.ToLowerInvariant();
                        if (n.Contains("fuelcover") && !n.Contains("snow")) { h = t; break; }
                    }
                if (h == null) { MelonLoader.MelonLogger.Msg("[Fuel] '" + root.name + "': fuel cap not found"); return null; }
                var tq = new Tank { root = root, hinge = h };
                tq.parts = h.GetComponentsInChildren<Renderer>(true);
                tq.rotClosed = Quaternion.Inverse(root.rotation) * h.rotation;
                MelonLoader.MelonLogger.Msg("[Fuel] '" + root.name + "': fuel cap '" + h.name + "'");
                return tq;
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Fuel] cap: " + e.Message); return null; }
        }

        public void Update()
        {
            if (hinge == null || root == null) return;
            Quaternion rel = Quaternion.Inverse(root.rotation) * hinge.rotation;
            open = Quaternion.Angle(rel, rotClosed) > AngleOpenState;
        }

        public Vector3 Point()
        {
            try
            {
                if (parts != null && parts.Length > 0)
                {
                    Bounds b = parts[0].bounds;
                    for (int i = 1; i < parts.Length; i++) if (parts[i] != null) b.Encapsulate(parts[i].bounds);
                    return b.center;
                }
            }
            catch (Exception) { }
            return hinge != null ? hinge.position : root.position;
        }
    }

    /// <summary>Fuel pumps in the world (found by name around the player).</summary>
    internal static class Pumps
    {
        public class Pump { public Transform root; public Vector3 center; }
        public static readonly List<Pump> near = new List<Pump>();
        static readonly HashSet<string> logged = new HashSet<string>();

        static bool IsPump(string names)
        {
            string n = names.ToLowerInvariant();
            if (n.Contains("surtidor")) return true;
            return n.Contains("pump") && !n.Contains("water") && !n.Contains("pumpkin") && !n.Contains("hand");
        }

        /// <param name="diagnostico">if no pumps are found, logs the names of what is around</param>
        public static void Scan(Vector3 o, float radius, bool diagnostic)
        {
            near.Clear();
            try
            {
                var boxes = new Dictionary<int, Bounds>();
                var roots = new Dictionary<int, Transform>();
                var others = new List<string>();
                foreach (Collider c in Physics.OverlapSphere(o, radius, ~0, QueryTriggerInteraction.Collide))
                {
                    if (c == null) continue;
                    Transform m = null;
                    int rises = 0;
                    for (Transform a = c.transform; a != null && rises < 6; a = a.parent, rises++)
                        if (IsPump(a.name)) m = a;
                    if (m != null)
                    {
                        int id = m.GetInstanceID();
                        if (boxes.ContainsKey(id)) { Bounds b = boxes[id]; b.Encapsulate(c.bounds); boxes[id] = b; }
                        else { boxes[id] = c.bounds; roots[id] = m; }
                    }
                    else if (diagnostic)
                    {
                        Transform t = c.transform;
                        string shortName = t.name + (t.parent != null ? "  <  " + t.parent.name : "");
                        if (!others.Contains(shortName)) others.Add(shortName);
                    }
                }
                foreach (var kv in roots) near.Add(new Pump { root = kv.Value, center = boxes[kv.Key].center });
                if (near.Count > 0)
                {
                    foreach (Pump b in near)
                        if (logged.Add("B" + b.root.name)) MelonLoader.MelonLogger.Msg("[Fuel] pump found: '" + b.root.name + "'");
                }
                else if (diagnostic && others.Count > 0)
                {
                    string cacheKey = string.Join(",", others.ToArray());
                    if (logged.Add(cacheKey))
                    {
                        var l = new List<string>();
                        for (int i = 0; i < others.Count && i < 40; i++) l.Add(others[i]);
                        MelonLoader.MelonLogger.Msg("[Fuel] no pumps by name; objects with colliders around: " + string.Join(" | ", l.ToArray()));
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Fuel] pump search: " + e.Message); }
        }
    }

    /// <summary>Liquid sound when filling (generated, no files).</summary>
    internal class SoundLoad
    {
        static AudioClip clip;
        AudioSource src;
        GameObject go;

        static void Build()
        {
            if (clip != null) return;
            const int SR = 24000;
            int n = SR * 2;
            var rnd = new System.Random(11);
            float[] a = new float[n];
            float l1 = 0f, l2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)rnd.NextDouble() * 2f - 1f;
                l1 += (x - l1) * 0.16f;
                l2 += (l1 - l2) * 0.12f;
                float t = i / (float)SR;
                float burb = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * t * 7f) * Mathf.Sin(2f * Mathf.PI * t * 3f + 1f);
                a[i] = l2 * 3.2f * burb;
            }
            int f = SR / 10;
            for (int i = 0; i < f; i++) { float w = i / (float)f; a[i] = a[i] * w + a[n - f + i] * (1f - w); }
            var c = new float[n - f];
            Array.Copy(a, c, n - f);
            float mx = 0.0001f;
            for (int i = 0; i < c.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(c[i]));
            for (int i = 0; i < c.Length; i++) c[i] *= 0.6f / mx;
            clip = AudioClip.Create("WV_Refuel", c.Length, 1, SR, false);
            clip.SetData(c, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        public void Play(bool si, Vector3 pos)
        {
            try
            {
                if (!si)
                {
                    if (src != null && src.isPlaying) src.Stop();
                    return;
                }
                if (!Settings.Sound()) return;
                Build();
                if (go == null)
                {
                    go = new GameObject("WV_Refuel");
                    src = go.AddComponent<AudioSource>();
                    src.clip = clip; src.loop = true; src.spatialBlend = 0.8f; src.dopplerLevel = 0f;
                    src.minDistance = 1f; src.maxDistance = 9f; src.rolloffMode = AudioRolloffMode.Linear;
                    src.volume = 0.18f;
                }
                go.transform.position = pos;
                if (!src.isPlaying) src.Play();
            }
            catch (Exception) { }
        }
    }
}
