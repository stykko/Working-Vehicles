using System;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>Procedural "painted and worn" texture (soft stains, grain, scratches and dirt) so the parts don't look like smooth plastic.</summary>
    internal static class Paint
    {
        static Texture2D tex;
        const int N = 256;

        static float Noise(float[,] g, int cells, float x, float y)
        {
            float fx = x * cells, fy = y * cells;
            int x0 = (int)Mathf.Floor(fx), y0 = (int)Mathf.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            int xa = ((x0 % cells) + cells) % cells, xb = (xa + 1) % cells;
            int ya = ((y0 % cells) + cells) % cells, yb = (ya + 1) % cells;
            float a = Mathf.Lerp(g[xa, ya], g[xb, ya], tx);
            float b = Mathf.Lerp(g[xa, yb], g[xb, yb], tx);
            return Mathf.Lerp(a, b, ty);
        }

        public static Texture2D Textura()
        {
            if (tex != null) return tex;
            try
            {
                // texture included in the mod (painted with the TLD_Brush brush)
                try
                {
                    using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WorkingVehicles.paint.png"))
                    {
                        if (st != null)
                        {
                            var ms = new System.IO.MemoryStream();
                            st.CopyTo(ms);
                            var emb = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                            if (ImageConversion.LoadImage(emb, ms.ToArray()))
                            {
                                emb.wrapMode = TextureWrapMode.Repeat;
                                emb.filterMode = FilterMode.Bilinear;
                                emb.anisoLevel = 4;
                                emb.hideFlags = HideFlags.DontUnloadUnusedAsset;
                                tex = emb;
                                MelonLoader.MelonLogger.Msg("[Paint] bundled texture loaded");
                            }
                        }
                    }
                }
                catch (Exception e) { MelonLoader.MelonLogger.Warning("[Paint] bundled texture: " + e.Message); }
                // if there is a custom texture (for example taken from the game) in UserData\WorkingVehicles_textures\paint.png (or the old UserData\CamionetaConducible_texturas\pintura.png), that one is used
                string ud = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "UserData");
                string filePath = System.IO.Path.Combine(ud, "WorkingVehicles_textures", "paint.png");
                if (!System.IO.File.Exists(filePath)) filePath = System.IO.Path.Combine(ud, "CamionetaConducible_texturas", "pintura.png");
                if (System.IO.File.Exists(filePath))
                {
                    var own = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    if (ImageConversion.LoadImage(own, System.IO.File.ReadAllBytes(filePath)))
                    {
                        own.wrapMode = TextureWrapMode.Repeat;
                        own.filterMode = FilterMode.Bilinear;
                        own.hideFlags = HideFlags.DontUnloadUnusedAsset;
                        tex = own;
                        MelonLoader.MelonLogger.Msg("[Paint] using the custom texture: " + filePath);
                        return tex;
                    }
                }
                if (tex != null) return tex;
                var rnd = new System.Random(1234);
                int[] cells = { 4, 8, 16, 32 };
                float[] weight = { 0.45f, 0.30f, 0.17f, 0.08f };
                var grids = new float[cells.Length][,];
                for (int o = 0; o < cells.Length; o++)
                {
                    grids[o] = new float[cells[o], cells[o]];
                    for (int i = 0; i < cells[o]; i++)
                        for (int j = 0; j < cells[o]; j++) grids[o][i, j] = (float)rnd.NextDouble();
                }
                var v = new float[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float u = x / (float)N, w = y / (float)N, s = 0f;
                        for (int o = 0; o < cells.Length; o++) s += Noise(grids[o], cells[o], u, w) * weight[o];
                        float grain = ((float)rnd.NextDouble() - 0.5f) * 0.07f;
                        v[y * N + x] = 0.80f + (s - 0.5f) * 0.42f + grain;
                    }
                // fine light scratches
                for (int k = 0; k < 40; k++)
                {
                    float x = (float)rnd.NextDouble() * N, y = (float)rnd.NextDouble() * N;
                    float ang = (float)rnd.NextDouble() * Mathf.PI, len = 10f + (float)rnd.NextDouble() * 40f;
                    float dx = Mathf.Cos(ang), dy = Mathf.Sin(ang), light = 0.05f + (float)rnd.NextDouble() * 0.08f;
                    for (int i = 0; i < (int)len; i++)
                    {
                        int px = ((int)(x + dx * i) % N + N) % N, py = ((int)(y + dy * i) % N + N) % N;
                        v[py * N + px] += light;
                    }
                }
                // dirt: dark stains
                for (int k = 0; k < 18; k++)
                {
                    float cx = (float)rnd.NextDouble() * N, cy = (float)rnd.NextDouble() * N, r = 6f + (float)rnd.NextDouble() * 18f, osc = 0.08f + (float)rnd.NextDouble() * 0.12f;
                    for (int y = -(int)r; y <= (int)r; y++)
                        for (int x = -(int)r; x <= (int)r; x++)
                        {
                            float d = Mathf.Sqrt(x * x + y * y) / r;
                            if (d >= 1f) continue;
                            int px = (((int)cx + x) % N + N) % N, py = (((int)cy + y) % N + N) % N;
                            v[py * N + px] -= osc * (1f - d) * (1f - d);
                        }
                }
                var px32 = new Color32[N * N];
                for (int i = 0; i < px32.Length; i++)
                {
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v[i] * 255f), 90, 255);
                    px32[i] = new Color32(b, b, (byte)Mathf.Clamp(b - 4, 0, 255), 255);
                }
                var t = new Texture2D(N, N, TextureFormat.RGBA32, true);
                t.SetPixels32(px32);
                t.Apply(true);
                t.wrapMode = TextureWrapMode.Repeat;
                t.filterMode = FilterMode.Bilinear;
                t.hideFlags = HideFlags.DontUnloadUnusedAsset;
                tex = t;
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Paint] could not create the texture: " + e.Message); }
            return tex;
        }

        /// <summary>Matte, muted color, like the paint of the game's truck.</summary>
        public static Color TurnOff(Color c)
        {
            float g = (c.r + c.g + c.b) / 3f;
            float r = Mathf.Lerp(c.r, g, 0.28f) * 0.82f, gg = Mathf.Lerp(c.g, g, 0.28f) * 0.82f, b = Mathf.Lerp(c.b, g, 0.28f) * 0.82f;
            return new Color(Mathf.Min(r, 0.66f), Mathf.Min(gg, 0.66f), Mathf.Min(b, 0.66f), 1f);
        }
    }
}
