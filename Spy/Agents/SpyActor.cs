using System;
using System.Collections.Generic;

namespace Spy
{
    /// <summary>
    /// Tracks an individual spy (like a ship) with identity and stats.
    /// </summary>
    public class SpyActor
    {
        public const string StatusAvailable = "Available";
        public const string StatusOnMission = "On Mission";
        public const string StatusCaptured = "Captured";
        public const string StatusTransiting = "Transiting";

        public string Name { get; set; } = "";
        public int YrsActive { get; set; }
        public int SuccessMissions { get; set; }
        public int FailedMissions { get; set; }
        public int ExperienceLevel { get; set; }
        public int Sneakiness { get; set; }
        public int Efficiency { get; set; }
        public int Planning { get; set; }
        /// <summary>Current activity: Available, On Mission, Captured, Transiting.</summary>
        public string Status { get; set; } = StatusAvailable;

        // --- Current mission (when Status == StatusOnMission) ---
        /// <summary>Mission type: e.g. Gather Intel, Plant Agents, Get Ship Information.</summary>
        public string CurrentMissionType { get; set; } = "";
        /// <summary>Difficulty 1 (easiest) to 5 (hardest).</summary>
        public int MissionDifficulty { get; set; } = 1;
        /// <summary>Turns remaining until mission resolves. When 0, we roll success/fail.</summary>
        public int TurnsRemaining { get; set; } = 0;
        /// <summary>Target country name (optional, for intel/display).</summary>
        public string TargetCountry { get; set; } = "";

        public SpyActor() { }

        public SpyActor(string name, int yrsActive = 0, int successMissions = 0, int failedMissions = 0,
            int experienceLevel = 1, int sneakiness = 50, int efficiency = 50, int planning = 50,
            string? status = null)
        {
            Name = name;
            YrsActive = yrsActive;
            SuccessMissions = successMissions;
            FailedMissions = failedMissions;
            ExperienceLevel = Math.Clamp(experienceLevel, 1, 10);
            Sneakiness = Math.Clamp(sneakiness, 0, 100);
            Efficiency = Math.Clamp(efficiency, 0, 100);
            Planning = Math.Clamp(planning, 0, 100);
            Status = status ?? StatusAvailable;
        }



        /// <summary>
        /// All active spy actors (in-memory registry; can be replaced with save/load later).
        /// </summary>
        public static readonly List<SpyActor> All = new List<SpyActor>();

        static SpyActor()
        {
            // Seed with a few example spies for UI testing
            All.Add(new SpyActor("Agent Alpha", 3, 12, 1, 4, 72, 68, 55, StatusAvailable));
            All.Add(new SpyActor("Agent Beta", 1, 4, 2, 2, 45, 60, 70, StatusOnMission));
            All.Add(new SpyActor("Agent Gamma", 0, 0, 0, 1, 50, 50, 50, StatusTransiting));
        }
    }
}
