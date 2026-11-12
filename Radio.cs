using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// The truck radio: plays the player's own songs, taken from the folder Mods\WorkingVehicles (next to the mod).
    /// It uses the standard Unity audio (enabled in the game by the AudioCore plugin, which the mod already needs for its sounds).
    /// Only .wav files are supported (the game has no .mp3 / .ogg decoder that mods can use); they are read by the mod itself.
    /// The radio plays while you are in the vehicle with the engine running, or with it off while the battery lasts (it drains the battery, like the lights).
    /// </summary>
    internal static class Radio
    {
        public static bool menu;                 // the little menu is open
        public static bool on;                   // the radio is switched on
        public static bool userPaused;           // the song was paused from the menu
        public static float volume = 0.5f;
        public static int index;
        static int sel;
        static string message = "";
        static float messageT;

        static readonly List<string> files = new List<string>();
        static int skipped;                      // .mp3 / .ogg files that can't be played in this build

        static AudioClip clip;
        static AudioClip clipLow;                // muffled copy (mono, low sample rate) used from outside the vehicle
        static AudioSource src;
        static Transform srcParent;
        static bool started;                     // the current clip has already begun to play
        static bool paused;
        static bool wasPowered;

        // loading
        class Wav { public float[] data, low; public int channels, rate, lowRate; public string error; }
        static Task<Wav> wavTask;
        static string loadingFile;
        public static bool WebOk { get { return false; } }     // only .wav (the game has no decoder for .mp3 / .ogg)

        public static string Folder() { return Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "Mods"), "WorkingVehicles"); }

        // ------------------------------------------------------------------ files

        public static void Scan()
        {
            files.Clear();
            skipped = 0;
            try
            {
                string dir = Folder();
                Directory.CreateDirectory(dir);
                foreach (string f in Directory.GetFiles(dir))
                {
                    string e = Path.GetExtension(f).ToLowerInvariant();
                    if (e == ".wav") files.Add(f);
                    else if (e == ".mp3" || e == ".ogg") skipped++;
                }
                files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) { MelonLoader.MelonLogger.Warning("[Radio] could not read the music folder: " + ex.Message); }
            if (index >= files.Count || index < 0) index = 0;
        }

        static string NameOf(int i)
        {
            return i >= 0 && i < files.Count ? Path.GetFileNameWithoutExtension(files[i]) : "";
        }

        // ------------------------------------------------------------------ control

        public static void Open()
        {
            Scan();
            menu = true;
            sel = 0;
            LoadState();
        }

        static void Say(string t) { message = t; messageT = 3f; }

        public static void Toggle()
        {
            if (on) { on = false; userPaused = false; return; }
            if (files.Count == 0) { Say(L.T("No hay canciones: ponelas en Mods\\WorkingVehicles", "No songs: put them in Mods\\WorkingVehicles")); return; }
            if (!wasPowered) { Say(L.T("Sin batería: la radio no enciende", "No battery: the radio won't turn on")); return; }
            on = true;
        }

        public static void Next() { Go(index + 1); }
        public static void Prev() { Go(index - 1); }

        static void Go(int i)
        {
            if (files.Count == 0) { Say(L.T("No hay canciones: ponelas en Mods\\WorkingVehicles", "No songs: put them in Mods\\WorkingVehicles")); return; }
            index = ((i % files.Count) + files.Count) % files.Count;
            userPaused = false;
            Unload();
            if (!on && wasPowered) on = true;
        }

        public static void Volume(float delta)
        {
            volume = Mathf.Clamp01(Mathf.Round((volume + delta) * 20f) / 20f);
            if (src != null) src.volume = volume;
        }

        /// <summary>Keys of the menu (arrows, Enter, Backspace).</summary>
        public static void HandleMenu()
        {
            if (!menu) return;
            if (messageT > 0f) messageT -= Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) sel = (sel + 1) % 6;
            if (Input.GetKeyDown(KeyCode.UpArrow)) sel = (sel + 5) % 6;
            if (Input.GetKeyDown(KeyCode.RightArrow)) Volume(0.05f);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Volume(-0.05f);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (sel == 0) Toggle();
                else if (sel == 1) { if (on) userPaused = !userPaused; else Say(L.T("La radio está apagada", "The radio is off")); }
                else if (sel == 2) Volume(volume >= 0.99f ? -1f : 0.1f);      // Enter: +10 %, and from the top it goes back to 0
                else if (sel == 3) Prev();
                else if (sel == 4) Next();
                else Close();
            }
        }

        public static void Close() { menu = false; SaveState(); }

        // ------------------------------------------------------------------ every frame

        /// <param name="driving">the player is at the wheel</param>
        /// <param name="powered">the engine is running</param>
        /// <param name="parent">the vehicle (the sound travels with it)</param>
        public static void Tick(bool inside, bool powered, Transform parent)
        {
            try
            {
                wasPowered = powered && inside;
                if (!inside) menu = false;
                if (on && !powered) { on = false; userPaused = false; }                       // no engine and no battery: the radio goes off
                Poll();

                bool allowed = on && !userPaused && powered && parent != null;
                if (!allowed)
                {
                    if (src != null && src.isPlaying) { src.Pause(); paused = true; }
                    return;
                }
                if (files.Count == 0) Scan();
                if (files.Count == 0) { on = false; return; }

                if (clip == null)
                {
                    if (!Loading()) Load(index);
                    return;
                }

                Source(parent);
                Mode(inside);
                src.volume = volume * (inside ? 1f : OutsideVolume);
                AudioClip want = inside || clipLow == null ? clip : clipLow;     // from outside: the muffled copy
                if (paused)
                {
                    if (started && src.clip != want) { Swap(want); }
                    else src.UnPause();
                    paused = false;
                }
                else if (!src.isPlaying)
                {
                    if (!started) { src.clip = want; src.Play(); started = true; }
                    else Go(index + 1);                               // the song ended: next one
                }
                else if (src.clip != want) Swap(want);
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Radio] " + e.Message); on = false; }
        }

        const float OutsideVolume = 0.6f;       // from outside the closed vehicle it sounds quieter...
        const float OutsideCutoff = 750f;        // ...and muffled, as if inside a box (only the low tones get through; made while the song is read)
        static int mode = -1;

        /// <summary>Inside: plain 2D sound. Outside: it comes from the vehicle, quieter and muffled.</summary>
        static void Mode(bool inside)
        {
            int m = inside ? 1 : 0;
            if (m == mode) return;
            mode = m;
            src.spatialBlend = inside ? 0f : 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2.5f;
            src.maxDistance = 28f;
        }

        // Changes between the normal and the muffled copy keeping the place in the song.
        static void Swap(AudioClip to)
        {
            float t = src.time;
            src.clip = to;
            src.Play();
            src.time = Mathf.Min(t, Mathf.Max(0f, to.length - 0.05f));
        }

        public static void TurnOff() { on = false; userPaused = false; }

        static void Source(Transform parent)
        {
            if (src == null)
            {
                var go = new GameObject("WV_Radio");
                src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0f;                                // heard from inside, without positioning
                src.loop = false;
                src.playOnAwake = false;
                src.dopplerLevel = 0f;
            }
            if (srcParent != parent) { src.transform.SetParent(parent, false); srcParent = parent; mode = -1; }
        }

        // ------------------------------------------------------------------ loading songs

        static bool Loading()
        {
            return wavTask != null;
        }

        static void Unload()
        {
            try { if (src != null) { src.Stop(); src.clip = null; } } catch (Exception) { }
            try { if (clip != null) UnityEngine.Object.Destroy(clip); } catch (Exception) { }
            try { if (clipLow != null) UnityEngine.Object.Destroy(clipLow); } catch (Exception) { }
            clip = null; clipLow = null;
            started = false;
            paused = false;
            wavTask = null;
            loadingFile = null;
        }

        static void Load(int i)
        {
            if (i < 0 || i >= files.Count) return;
            string f = files[i];
            loadingFile = f;
            string e = Path.GetExtension(f).ToLowerInvariant();
            if (e == ".wav")
            {
                wavTask = Task.Run(() => ReadWav(f));         // the file is decoded away from the game thread (no hiccups)
                return;
            }
            Fail("only .wav files are supported");
        }

        static void Poll()
        {
            if (wavTask != null && wavTask.IsCompleted)
            {
                Wav w = null;
                try { w = wavTask.Result; } catch (Exception ex) { w = new Wav { error = ex.Message }; }
                wavTask = null;
                if (w == null || w.data == null)
                {
                    Fail(w != null ? w.error : "unknown");
                    return;
                }
                try
                {
                    int frames = w.data.Length / w.channels;
                    var c = AudioClip.Create(Path.GetFileNameWithoutExtension(loadingFile), frames, w.channels, w.rate, false);
                    c.SetData(w.data, 0);
                    if (w.low != null && w.low.Length > 0)
                    {
                        var cl = AudioClip.Create(Path.GetFileNameWithoutExtension(loadingFile) + "_low", w.low.Length, 1, w.lowRate, false);
                        cl.SetData(w.low, 0);
                        cl.hideFlags = HideFlags.DontUnloadUnusedAsset;
                        clipLow = cl;
                    }
                    c.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    clip = c;
                    started = false;
                    failedInRow = 0;
                    MelonLoader.MelonLogger.Msg("[Radio] playing '" + Path.GetFileName(loadingFile) + "' (" + (frames / (float)w.rate).ToString("0") + " s)");
                }
                catch (Exception ex) { Fail(ex.Message); }
            }
        }

        static int failedInRow;

        static void Fail(string why)
        {
            MelonLoader.MelonLogger.Warning("[Radio] could not play '" + (loadingFile != null ? Path.GetFileName(loadingFile) : "?") + "': " + why);
            Say(L.T("No pude reproducir: ", "Could not play: ") + (loadingFile != null ? Path.GetFileName(loadingFile) : ""));
            loadingFile = null;
            // the next song is tried (but not forever if none works)
            if (++failedInRow >= Mathf.Max(1, files.Count)) { failedInRow = 0; on = false; return; }
            index = (index + 1) % Mathf.Max(1, files.Count);
        }

        // ------------------------------------------------------------------ .wav reader (PCM 8/16/24/32 bits and 32-bit float)

        static Wav ReadWav(string path)
        {
            try
            {
                byte[] b = File.ReadAllBytes(path);
                if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return new Wav { error = "not a WAV file" };
                int pos = 12, fmtTag = 0, ch = 0, rate = 0, bits = 0, dataPos = -1, dataLen = 0;
                while (pos + 8 <= b.Length)
                {
                    string id = "" + (char)b[pos] + (char)b[pos + 1] + (char)b[pos + 2] + (char)b[pos + 3];
                    int len = BitConverter.ToInt32(b, pos + 4);
                    int body = pos + 8;
                    if (id == "fmt ")
                    {
                        fmtTag = BitConverter.ToUInt16(b, body);
                        ch = BitConverter.ToUInt16(b, body + 2);
                        rate = BitConverter.ToInt32(b, body + 4);
                        bits = BitConverter.ToUInt16(b, body + 14);
                        if (fmtTag == 0xFFFE && len >= 26) fmtTag = BitConverter.ToUInt16(b, body + 24);    // extensible: real format
                    }
                    else if (id == "data")
                    {
                        dataPos = body;
                        dataLen = len < 0 || body + len > b.Length ? b.Length - body : len;
                        break;
                    }
                    if (len < 0) break;
                    pos = body + len + (len & 1);
                }
                if (dataPos < 0 || ch < 1 || ch > 2 || rate < 8000) return new Wav { error = "unsupported WAV (needs 1 or 2 channels)" };
                int bytes = bits / 8;
                if (!((fmtTag == 1 && (bits == 8 || bits == 16 || bits == 24 || bits == 32)) || (fmtTag == 3 && bits == 32)))
                    return new Wav { error = "unsupported WAV format (use 16-bit PCM)" };
                int n = dataLen / bytes;
                n -= n % ch;
                var d = new float[n];
                for (int i = 0, p = dataPos; i < n; i++, p += bytes)
                {
                    float v;
                    if (fmtTag == 3) v = BitConverter.ToSingle(b, p);
                    else if (bits == 16) v = BitConverter.ToInt16(b, p) / 32768f;
                    else if (bits == 8) v = (b[p] - 128) / 128f;
                    else if (bits == 24) v = (((b[p + 2] << 24) | (b[p + 1] << 16) | (b[p] << 8)) >> 8) / 8388608f;
                    else v = BitConverter.ToInt32(b, p) / 2147483648f;
                    d[i] = v;
                }
                // muffled copy: mono, low-passed (two one-pole filters) and reduced to about 4 kHz (it is tiny and needs no more)
                int f = Mathf.Max(1, Mathf.RoundToInt(rate / 4000f));
                int frames = n / ch;
                var low = new float[frames / f];
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * OutsideCutoff / rate);
                float y1 = 0f, y2 = 0f;
                for (int i = 0; i < frames; i++)
                {
                    float m = ch == 2 ? (d[i * 2] + d[i * 2 + 1]) * 0.5f : d[i];
                    y1 += a * (m - y1);
                    y2 += a * (y1 - y2);
                    int o = i / f;
                    if (o < low.Length && i % f == f - 1) low[o] = y2 * 1.6f;     // a bit louder: the filter takes energy away
                }
                return new Wav { data = d, channels = ch, rate = rate, low = low, lowRate = rate / f };
            }
            catch (Exception e) { return new Wav { error = e.Message }; }
        }

        // ------------------------------------------------------------------ remembered volume and song

        static bool stateLoaded;

        static string StatePath() { return Cfg.DataFile("WorkingVehicles_radio.txt", "WorkingVehicles_radio.txt"); }

        static void LoadState()
        {
            if (stateLoaded) return;
            stateLoaded = true;
            try
            {
                if (!File.Exists(StatePath())) return;
                string[] l = File.ReadAllText(StatePath()).Trim().Split('|');
                if (l.Length >= 1) volume = Mathf.Clamp01(float.Parse(l[0], CultureInfo.InvariantCulture));
                if (l.Length >= 2 && files.Count > 0) index = Mathf.Clamp(int.Parse(l[1], CultureInfo.InvariantCulture), 0, files.Count - 1);
            }
            catch (Exception) { }
        }

        static void SaveState()
        {
            try { File.WriteAllText(StatePath(), volume.ToString("R", CultureInfo.InvariantCulture) + "|" + index); }
            catch (Exception) { }
        }

        public static void Reset()
        {
            menu = false;
            on = false;
            Unload();
            try { if (src != null) UnityEngine.Object.Destroy(src.gameObject); } catch (Exception) { }
            src = null; srcParent = null; mode = -1;
        }

        // ------------------------------------------------------------------ drawing

        public static void Draw(float W, float H)
        {
            float e = Ui.Sc;
            float rowH = 58f * e;
            string track = files.Count > 0 ? (index + 1) + "/" + files.Count + "  " + NameOf(index) : L.T("Sin canciones", "No songs");
            float extra = files.Count == 0 || skipped > 0 || messageT > 0f ? 30f * e : 0f;
            float hPanel = 82f * e + 6 * rowH + 56f * e + 14f * e + extra;
            Rect box = new Rect(W * 0.5f - 270f * e, H * 0.5f - hPanel * 0.5f, 540f * e, hPanel);
            Rect u = Ui.Panel(box, L.T("Radio", "Radio"), track, on ? Ui.Green : Ui.Accent);
            float y = u.y;
            Ui.Row(new Rect(u.x, y, u.width, rowH), on ? L.T("Apagar la radio", "Turn the radio off") : L.T("Encender la radio", "Turn the radio on"), on ? L.T("Encendida", "On") : L.T("Apagada", "Off"), on ? Ui.Green : Ui.Gray, sel == 0, -1f, Ui.Green);
            y += rowH;
            Ui.Row(new Rect(u.x, y, u.width, rowH), userPaused ? L.T("Reanudar la canción", "Resume the song") : L.T("Pausar la canción", "Pause the song"), on && userPaused ? L.T("En pausa", "Paused") : "", Ui.Yellow, sel == 1, -1f, Ui.Green);
            y += rowH;
            Ui.Row(new Rect(u.x, y, u.width, rowH), L.T("Volumen", "Volume"), Mathf.RoundToInt(volume * 100f) + " %", Ui.White, sel == 2, volume, Ui.Sage);
            y += rowH;
            Ui.Row(new Rect(u.x, y, u.width, rowH), L.T("Canción anterior", "Previous song"), "", Ui.Gray, sel == 3, -1f, Ui.Green);
            y += rowH;
            Ui.Row(new Rect(u.x, y, u.width, rowH), L.T("Canción siguiente", "Next song"), "", Ui.Gray, sel == 4, -1f, Ui.Green);
            y += rowH;
            Ui.Row(new Rect(u.x, y, u.width, rowH), L.T("Cerrar", "Close"), "", Ui.Gray, sel == 5, -1f, Ui.Green);
            y += rowH;
            if (extra > 0f)
            {
                string t = messageT > 0f ? message
                    : files.Count == 0 ? L.T("Poné tus canciones (.wav" + (WebOk ? ", .mp3, .ogg" : "") + ") en la carpeta Mods\\WorkingVehicles", "Put your songs (.wav" + (WebOk ? ", .mp3, .ogg" : "") + ") in the Mods\\WorkingVehicles folder")
                    : L.T(skipped + " archivo(s) .mp3/.ogg ignorados: la radio solo lee .wav", skipped + " .mp3/.ogg file(s) ignored: the radio only reads .wav");
                Ui.Text(new Rect(u.x + 22f * e, y + 2f * e, u.width - 44f * e, 24f * e), t, 14f, Ui.Yellow, TextAnchor.MiddleLeft, false);
            }
            Ui.Footer(box, L.T("Flechas", "Arrows"), L.T("Mover / volumen", "Move / volume"), "Enter", L.T("Elegir", "Select"), "Backspace", L.T("Cerrar", "Close"));
        }
    }


}
