using System;

namespace Spy
{
    /// <summary>
    /// Mission types and difficulty-based success % for turn-based spy mission resolution.
    /// Missions affect the game.  
    /// Mission outcomes apply different # so, Capture lowers prestige and increases tension and unrest
    /// Failures lower prestige
    /// capture but evade increases tension and unrest
    /// Intel is important to know specs on an enemy ship
    /// Plant agents make next missions easier
    /// GetShipInformation is getting a specific hull build design
    /// StealTechnology is stealing a random technology, could add for elite ones specific techs
    /// </summary>
    public static class SpyMission
    {
        public const string TypeGatherIntel = "Gather Intel";
        public const string TypePlantAgents = "Plant Agents";
        public const string TypeGetShipInformation = "Get Ship Information";

        public const string TypeStealTechnology = "Steal Technology";

        /// <summary>Difficulty 1 (easiest) to 5 (hardest).</summary>
        public const int DifficultyMin = 1;
        public const int DifficultyMax = 5;

        /// <summary>Base success chance (0–100) by difficulty. Index 0 = difficulty 1, index 4 = difficulty 5.</summary>
        private static readonly int[] BaseSuccessPercentByDifficulty = { 85, 70, 55, 40, 25 };

        /// <summary>Base capture chance on failure (0–100). Higher difficulty = more likely captured.</summary>
        private static readonly int[] BaseCapturePercentOnFail = { 10, 20, 35, 50, 65 };

        /// <summary>Get base success chance (0–100) for a given difficulty.</summary>


        /// <summary>All mission type display names for UI.</summary>
        public static string[] AllMissionTypes { get; } =
        {
            TypeGatherIntel,
            TypePlantAgents,
            TypeGetShipInformation,
            TypeStealTechnology
        };
    }
}
