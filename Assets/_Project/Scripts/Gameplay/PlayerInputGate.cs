namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Cand un meniu e deschis (cumparare), jucatorul nu se uita in jur si nu trage; cursorul e liber.
    /// Mai multe meniuri pot cere blocarea: e blocat cat timp cel putin unul o cere.
    /// </summary>
    public static class PlayerInputGate
    {
        private static int requests;

        public static bool Blocked => requests > 0;

        public static void Request(bool block)
        {
            requests = System.Math.Max(0, requests + (block ? 1 : -1));
        }

        /// <summary>La iesirea din Play mode / reincarcarea scenei.</summary>
        public static void Reset()
        {
            requests = 0;
        }
    }
}
