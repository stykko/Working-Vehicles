namespace WorkingVehicles
{
    /// <summary>Mod texts in Spanish / English ("Idioma / Language" option in Mod Options).</summary>
    internal static class L
    {
        public static bool En { get { return Settings.English(); } }

        /// <summary>Returns the text in the chosen language.</summary>
        public static string T(string es, string en) { return Settings.English() ? en : es; }
    }
}
