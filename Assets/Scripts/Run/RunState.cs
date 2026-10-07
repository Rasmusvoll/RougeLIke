using RougeLike.Units;

namespace RougeLike
{
    /// <summary>
    /// The current run's player state, shared between scenes (builder, battle, rewards).
    /// Nothing persists across runs: a new run resets the collection to the starter set.
    /// </summary>
    public static class RunState
    {
        public static PlayerCollection Collection { get; private set; }

        /// <summary>Battles won this run. Picks the next enemy wave.</summary>
        public static int BattlesWon;

        public static bool HasRun => Collection != null;

        public static void StartNewRun(StarterSet starter)
        {
            Collection = PlayerCollection.FromStarter(starter);
            BattlesWon = 0;
        }

        public static void EndRun()
        {
            Collection = null;
            BattlesWon = 0;
        }
    }
}
