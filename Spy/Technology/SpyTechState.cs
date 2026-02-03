using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using MelonLoader;
using Il2Cpp;

namespace Spy
{
    /// <summary>
    /// Tracks which spy technologies the player has researched. Persisted per campaign.
    /// </summary>
    public static class SpyTechState
    {
        /// <summary>Ids of technologies the player has unlocked.</summary>
        private static readonly HashSet<string> ResearchedIds = new HashSet<string>();

        private static string GetSpyModDir()
        {
            string dir = Path.Combine(Application.persistentDataPath, "SpyMod");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Stable id for current campaign (so save file is per campaign, not per turn).</summary>
        private static string GetCampaignId()
        {
            try
            {
                if (CampaignController.Instance?.CampaignData != null)
                {
                    // Use a hash of campaign state so same campaign reuses the same file
                    var cd = CampaignController.Instance.CampaignData;
                    int hash = (cd.Players?.Count ?? 0) ^ (cd.Vessels?.Count ?? 0);
                    return "campaign_" + Math.Abs(hash).ToString();
                }
            }
            catch { }
            return "default";
        }

        private static string GetSaveFilePath()
        {
            string safeId = string.Join("_", GetCampaignId().Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(GetSpyModDir(), $"ResearchedTechs_{safeId}.json");
        }

        /// <summary>Load researched tech ids from disk for the current campaign.</summary>
        public static void LoadFromDisk()
        {
            try
            {
                string path = GetSaveFilePath();
                if (!File.Exists(path))
                    return;
                string json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<SaveData>(json);
                if (data?.ResearchedIds != null)
                {
                    ResearchedIds.Clear();
                    foreach (string id in data.ResearchedIds)
                        ResearchedIds.Add(id);
                    MelonLogger.Msg($"[Spy] Loaded {ResearchedIds.Count} researched spy techs.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Spy] Failed to load spy techs: {ex.Message}");
            }
        }

        /// <summary>Save researched tech ids to disk for the current campaign.</summary>
        public static void SaveToDisk()
        {
            try
            {
                string path = GetSaveFilePath();
                var data = new SaveData { ResearchedIds = ResearchedIds.ToList() };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Spy] Failed to save spy techs: {ex.Message}");
            }
        }

        private class SaveData
        {
            public List<string> ResearchedIds { get; set; } = new List<string>();
        }

        /// <summary>When in campaign, use player.technologies (game save) as source of truth for researched spy techs.</summary>
        private static bool IsSpyTechResearchedInGame(string techId)
        {
            try
            {
                if (CampaignController.Instance?.CampaignData?.PlayersMajor == null) return false;
                Player? main = null;
                foreach (Player p in CampaignController.Instance.CampaignData.PlayersMajor)
                {
                    if (p != null && !p.isAi)
                    {
                        main = p;
                        break;
                    }
                }
                if (main?.technologies == null) return false;
                for (int i = 0; i < main.technologies.Count; i++)
                {
                    var tech = main.technologies[i];
                    if (tech?.data == null) continue;
                    if (tech.data.name != techId) continue;
                    if (tech.progress >= 100f || (tech.IsEndTechResearched && tech.Index > 0))
                        return true;
                    return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Whether this spy tech is researched (game research system or legacy ResearchedIds).</summary>
        public static bool IsUnlocked(string techId)
        {
            if (string.IsNullOrEmpty(techId)) return false;
            if (IsSpyTechResearchedInGame(techId)) return true;
            return ResearchedIds.Contains(techId);
        }

        /// <summary>Total success-rate bonus (%) from all researched spy techs.</summary>
        public static int GetTotalSuccessBonus()
        {
            int total = 0;
            foreach (var tech in SpyTechnology.All)
            {
                if (IsUnlocked(tech.Id))
                    total += tech.SuccessRateBonus;
            }
            return total;
        }

        /// <summary>Total reduction to capture chance on failure (%) from all researched spy techs.</summary>
        public static int GetTotalCaptureReduce()
        {
            int total = 0;
            foreach (var tech in SpyTechnology.All)
            {
                if (IsUnlocked(tech.Id))
                    total += tech.CaptureReducePercent;
            }
            return total;
        }

        /// <summary>Unlock a technology by id (e.g. when "researched" in UI). Persists to disk.</summary>
        public static void Unlock(string techId)
        {
            if (string.IsNullOrEmpty(techId)) return;
            ResearchedIds.Add(techId);
            SaveToDisk();
        }

        /// <summary>Lock a technology (e.g. for testing). Persists to disk.</summary>
        public static void Lock(string techId)
        {
            if (string.IsNullOrEmpty(techId)) return;
            ResearchedIds.Remove(techId);
            SaveToDisk();
        }

        /// <summary>Unlock all spy technologies (e.g. for testing or cheat).</summary>
        public static void UnlockAll()
        {
            foreach (var tech in SpyTechnology.All)
                ResearchedIds.Add(tech.Id);
        }

        /// <summary>Clear all researched techs.</summary>
        public static void ClearAll()
        {
            ResearchedIds.Clear();
        }
    }
}
