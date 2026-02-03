using System;
using System.Collections.Generic;
using MelonLoader;

namespace Spy
{
    /// <summary>
    /// Progresses spy missions each turn: decrements turns remaining, then rolls success/fail when duration ends.
    /// </summary>
    public static class SpyMissionProgress
    {
        private static readonly Random Rng = new Random();

        /// <summary>
        /// Call once per turn (e.g. from OnNewTurn postfix). For each spy "On Mission",
        /// decrements TurnsRemaining; when 0, rolls success % (based on difficulty + spy stats),
        /// then completes mission (success/fail/captured) and updates spy.
        /// </summary>
        public static void ProgressMissionsThisTurn()
        {
            if (SpyActor.All == null) return;

            for (int i = SpyActor.All.Count - 1; i >= 0; i--)
            {
                SpyActor spy = SpyActor.All[i];
                if (spy.Status != SpyActor.StatusOnMission) continue;

                spy.TurnsRemaining--;
                if (spy.TurnsRemaining > 0) continue;

                string missionType = spy.CurrentMissionType;
                // Mission duration complete: roll success/fail
                int successPercent = SpyMission.ComputeSuccessPercent(spy.MissionDifficulty, spy);
                bool success = SpyMission.RollSuccess(successPercent, Rng);

                if (success)
                {
                    spy.SuccessMissions++;
                    spy.ExperienceLevel = Math.Clamp(spy.ExperienceLevel + 1, 1, 10);
                    spy.ClearMission(keepCaptured: false);
                    MelonLogger.Msg($"[Spy] {spy.Name} completed mission '{missionType}' (success).");
                }
                else
                {
                    spy.FailedMissions++;
                    int capturePercent = SpyMission.GetBaseCaptureOnFailPercent(spy.MissionDifficulty)
                        - SpyTechState.GetTotalCaptureReduce();
                    capturePercent = Math.Max(0, capturePercent);
                    bool captured = SpyMission.RollCaptureOnFail(capturePercent, Rng);
                    if (captured)
                    {
                        spy.Status = SpyActor.StatusCaptured;
                        spy.ClearMission(keepCaptured: true); // leaves Status = Captured
                        MelonLogger.Msg($"[Spy] {spy.Name} failed mission '{missionType}' and was captured.");
                    }
                    else
                    {
                        spy.ClearMission(keepCaptured: false);
                        MelonLogger.Msg($"[Spy] {spy.Name} failed mission '{missionType}' (escaped).");
                    }
                }
            }
        }
    }
}
