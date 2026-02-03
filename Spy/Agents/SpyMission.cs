using System;

namespace Spy
{
    /// <summary>
    /// Mission types and difficulty-based success % for turn-based spy mission resolution.
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
        public static int GetBaseSuccessPercent(int difficulty)
        {
            int d = Math.Clamp(difficulty, DifficultyMin, DifficultyMax);
            return BaseSuccessPercentByDifficulty[d - 1];
        }

        /// <summary>Get base capture-on-failure chance (0–100) for a given difficulty.</summary>
        public static int GetBaseCaptureOnFailPercent(int difficulty)
        {
            int d = Math.Clamp(difficulty, DifficultyMin, DifficultyMax);
            return BaseCapturePercentOnFail[d - 1];
        }

        /// <summary>
        /// Compute success chance (0–100) for a spy on a mission: base from difficulty,
        /// modified by spy's Sneakiness, Efficiency, Planning, Experience, and researched spy technologies.
        /// </summary>
        public static int ComputeSuccessPercent(int difficulty, SpyActor spy)
        {
            int basePct = GetBaseSuccessPercent(difficulty);
            // Average of three stats (0–100) gives up to +25 bonus; experience gives up to +10
            int statBonus = (spy.Sneakiness + spy.Efficiency + spy.Planning) / 12; // 0–25
            int expBonus = Math.Clamp(spy.ExperienceLevel * 2, 0, 10);
            int techBonus = SpyTechState.GetTotalSuccessBonus();
            return Math.Clamp(basePct + statBonus + expBonus + techBonus, 0, 100);
        }

        /// <summary>Roll for mission success. Returns true if mission succeeds.</summary>
        public static bool RollSuccess(int successPercent, Random? rng = null)
        {
            rng ??= new Random();
            return rng.Next(0, 100) < Math.Clamp(successPercent, 0, 100);
        }

        /// <summary>Roll for capture on failure. Returns true if spy is captured.</summary>
        public static bool RollCaptureOnFail(int capturePercent, Random? rng = null)
        {
            rng ??= new Random();
            return rng.Next(0, 100) < Math.Clamp(capturePercent, 0, 100);
        }

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
