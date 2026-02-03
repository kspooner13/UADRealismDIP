using System;
using System.IO;
using System.Text;
using Il2Cpp;
using MelonLoader;
using TweaksAndFixes;

namespace Spy
{
    /// <summary>
    /// Injects Spy Tech into the game's technology system (G.GameData.technologies) so they appear
    /// in the standard Research window and use the same research/save flow.
    /// </summary>
    public static class SpyTechInjection
    {
        /// <summary>Technology type: aim_control so spy techs appear in the Aim (Fire Control) group in Research.</summary>
        public const string SpyTechType = "aim_control";

        private static bool _injected;

        /// <summary>
        /// Call once with the GameData instance (from PostProcessAll). Merges spy technologies into its technologies dict.
        /// </summary>
        public static void InjectIntoGameData(GameData gameData)
        {
            if (_injected) return;
            if (gameData?.technologies == null)
            {
                MelonLogger.Warning("[Spy] GameData.technologies not available.");
                return;
            }
            try
            {
                string csv = BuildSpyTechnologiesCsv();
                Serializer.CSV.ProcessCSV<TechnologyData>(csv, false, gameData.technologies);

                _injected = true;
                MelonLogger.Msg($"[Spy] Injected {SpyTechnology.All.Count} spy technologies into aim group (Research).");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Spy] Failed to inject spy techs: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static string BuildSpyTechnologiesCsv()
        {
            // Match game's technologies.csv header and @ line so the parser maps fields correctly
            var sb = new StringBuilder();
            sb.AppendLine("name,technology type,\"image for ui, if any\",name for ui,historical year,\"max year randomization, plus-minus\",difficulty of reseach modifier,effect: please see table TechEffects,,effect to add,effect description,old effect description,,comment");
            sb.AppendLine("@name,type,#image,nameUi,year,yearRand,difficulty,effect,component,#todo,desc,#descOld,#desc3?,#comment");
            sb.AppendLine("default,,,,-1,0,25,,,,,,,");

            int year = 1890;
            foreach (SpyTechnology tech in SpyTechnology.All)
            {
                string desc = EscapeCsv(tech.Description + " +" + tech.SuccessRateBonus + "% mission success" + (tech.CaptureReducePercent > 0 ? ", -" + tech.CaptureReducePercent + "% capture chance on failure" : ""));
                sb.AppendLine($"{tech.Id},{SpyTechType},,{EscapeCsv(tech.Title)},{year},,34,,,{desc},,,,");
                year += 2;
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

    }
}
