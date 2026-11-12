namespace WorkingVehicles
{
    /// <summary>Mod texts in Spanish / English. Follows the game's language (Spanish game = Spanish, anything else = English).</summary>
    internal static class L
    {
        public static bool En { get { return Settings.English(); } }

        /// <summary>Returns the text in the chosen language.</summary>
        public static string T(string es, string en) { return Settings.English() ? en : es; }
    }
}
