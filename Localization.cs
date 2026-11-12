#nullable disable
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using UnityEngine;

// Option text translation system, contributed by a community member (thank you!). Strings live in localization.json (embedded; a copy in
// UserData/WorkingVehicles/localization.json overrides it). Keys start with "WV.".
// To localize any other text: add "WV.key": "text" to each language block of localization.json and replace the text in the code
// with LocalizationManager.Get("WV.key"). Language names are the game's ("English", "Spanish", "Russian"...); missing keys fall back to English.
namespace WorkingVehicles
{
    internal static class LocalizationManager
    {
        // Название мода (используется в имени ресурса, пути UserData и логах).
        // The name of the mod (used in the resource name, UserData path, and logs).
        private const string namespace_name = "WorkingVehicles";

        // Сокращённое название мода (префикс ключей локализации, например "RGM.").
        // The abbreviated name of the mod (prefix of localization keys, for example, “CCM.”

        //"WV = Working Vehicles"
        private const string locale_name = "WV.";

        private static Dictionary<string, Dictionary<string, string>> _data;

        internal static Dictionary<string, Dictionary<string, string>> Data
        {
            get { return _data ?? (_data = Load()); }
        }

        internal static void Reload() => _data = null;

        // Имя встроенного ресурса: {RootNamespace}.{filename}
        // Name embedded resource: {RootNamespace}.{filename}
        private const string EmbeddedResourceName = namespace_name + ".localization.json";

        private static Dictionary<string, Dictionary<string, string>> Load()
        {

            string dllDir  = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

            string userPath = Path.Combine( Path.GetDirectoryName(dllDir) ?? dllDir,   // выходим из Mods/ в корень игры
                "UserData", namespace_name, "localization.json");

            if (File.Exists(userPath))
            {
                var fromFile = TryLoadJson(File.ReadAllText(userPath, Encoding.UTF8));
                if (fromFile != null)
                {
                    MelonLogger.Msg($"[{namespace_name}] Localization override loaded from: {userPath}");
                    return fromFile;
                }
            }


            var asm    = Assembly.GetExecutingAssembly();
            var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
            if (stream != null)
            {
                using var reader  = new StreamReader(stream, Encoding.UTF8);
                var fromEmbedded  = TryLoadJson(reader.ReadToEnd());
                if (fromEmbedded != null)
                {
                    MelonLogger.Msg($"[{namespace_name}] Localization loaded from embedded resource.");
                    return fromEmbedded;
                }
            }
            else
            {
                MelonLogger.Warning($"[{namespace_name}] Embedded resource '{EmbeddedResourceName}' not found — using built-in English strings.");
            }

            return FallbackEnglish;
        }

        private static Dictionary<string, Dictionary<string, string>> TryLoadJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{namespace_name}] Failed to parse localization JSON: {ex.Message}");
                return null;
            }
        }

        // Without the JSON the keys are shown as they are (the mod keeps working; only the option texts look raw).
        private static readonly Dictionary<string, Dictionary<string, string>> FallbackEnglish = new()
        {
            ["English"] = new()
        };

        // The game reports its language as "English", "Spanish", "Brazilian"... (not "Spanish (Spain)"). Variants fall back to the base entry.
        private static string Resolve(Dictionary<string, Dictionary<string, string>> data, string lang)
        {
            if (data.ContainsKey(lang)) return lang;
            if (lang.StartsWith("Spanish", StringComparison.OrdinalIgnoreCase) && data.ContainsKey("Spanish")) return "Spanish";
            if (lang.StartsWith("Portuguese", StringComparison.OrdinalIgnoreCase) && data.ContainsKey("Brazilian")) return "Brazilian";
            foreach (var k in data.Keys)
                if (lang.StartsWith(k, StringComparison.OrdinalIgnoreCase)) return k;
            return "English";
        }

        internal static string Get(string key)
        {
            string lang = "English";
            try { lang = Localization.Language ?? "English"; } catch (Exception) { }
            var data = Data;
            if (data.TryGetValue(Resolve(data, lang), out var dict) && dict.TryGetValue(key, out string val))
                return val;
            if (data.TryGetValue("English", out var en) && en.TryGetValue(key, out string enVal))
                return enVal;
            return key;
        }

        // Is the game in Spanish? Drives the language of the mod's own texts (menus, messages). Cached for a second or so because menus ask every frame.
        private static int _langFrame = -10000;
        private static bool _spanish;
        internal static bool IsSpanish()
        {
            int f = Time.frameCount;
            if (f < _langFrame || f - _langFrame > 60)
            {
                _langFrame = f;
                try { _spanish = (Localization.Language ?? "").StartsWith("Spanish", StringComparison.OrdinalIgnoreCase); }
                catch (Exception) { _spanish = false; }
            }
            return _spanish;
        }

        internal static bool IsLocaleKey(string key) => key != null && key.StartsWith(locale_name);
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.Get))]
    internal static class LocalizationPatch
    {
        static void Postfix(string __0, ref string __result)
        {
            if (!LocalizationManager.IsLocaleKey(__0))
                return;
            __result = LocalizationManager.Get(__0);
        }
    }

    /// <summary>
    /// Патч на DescriptionHolder.get_Text (ModSettings) —
    /// перехватывает чтение описания в момент показа, когда язык уже установлен.
    /// </summary>
    [HarmonyPatch]
    internal static class DescriptionTextTranslatePatch
    {
        static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.PropertyGetter(
                AccessTools.TypeByName("ModSettings.DescriptionHolder"), "Text");

        static void Postfix(ref string __result)
        {
            if (!LocalizationManager.IsLocaleKey(__result))
                return;
            __result = LocalizationManager.Get(__result);
        }
    }
}