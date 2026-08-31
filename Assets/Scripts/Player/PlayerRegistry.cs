using System.Collections.Generic;

namespace Project.Player
{
    // Lista compartida de jugadores activos, para no obligar a cada sistema (culling de partículas,
    // IA de enemigos, etc.) a hacer su propio FindObjectsByType. Mismo patrón que SplitScreenManager.
    public static class PlayerRegistry
    {
        private static readonly List<GobblinController> players = new();

        public static IReadOnlyList<GobblinController> ActivePlayers => players;

        public static void Register(GobblinController player)
        {
            if (!players.Contains(player)) players.Add(player);
        }

        public static void Unregister(GobblinController player)
        {
            players.Remove(player);
        }
    }
}
