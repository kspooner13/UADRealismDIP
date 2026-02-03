using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader;

namespace TweaksAndFixes.Data
{
    /// <summary>
    /// Data structure and persistence for Fleet Composition templates.
    /// </summary>
    public static class FleetCompositionData
    {
        private static string GetCompositionsPath()
        {
            string basePath = Config._BasePath;
            string compositionsDir = Path.Combine(basePath, "TAFData", "FleetCompositions");
            if (!Directory.Exists(compositionsDir))
            {
                Directory.CreateDirectory(compositionsDir);
            }
            return compositionsDir;
        }

        private static string GetCompositionFilePath(string name)
        {
            string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(GetCompositionsPath(), $"{safeName}.json");
        }

        /// <summary>
        /// Fleet composition template data.
        /// </summary>
        public class Composition
        {
            public string Name { get; set; } = string.Empty;
            public int Battleships { get; set; } = 0;
            public int Cruisers { get; set; } = 0;
            public int Destroyers { get; set; } = 0;
            public string? Description { get; set; }
        }

        /// <summary>
        /// Save a fleet composition to disk.
        /// </summary>
        public static void Save(Composition comp)
        {
            try
            {
                string filePath = GetCompositionFilePath(comp.Name);
                string json = JsonSerializer.Serialize(comp, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                Melon<TweaksAndFixes>.Logger.Msg($"[FleetCompositionData] Saved composition: {comp.Name}");
            }
            catch (Exception ex)
            {
                Melon<TweaksAndFixes>.Logger.Error($"[FleetCompositionData] Failed to save composition '{comp.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Load a fleet composition by name.
        /// </summary>
        public static Composition? Load(string name)
        {
            try
            {
                string filePath = GetCompositionFilePath(name);
                if (!File.Exists(filePath))
                    return null;

                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Composition>(json);
            }
            catch (Exception ex)
            {
                Melon<TweaksAndFixes>.Logger.Error($"[FleetCompositionData] Failed to load composition '{name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load all saved fleet compositions.
        /// </summary>
        public static List<Composition> LoadAll()
        {
            var compositions = new List<Composition>();
            
            try
            {
                string compositionsDir = GetCompositionsPath();
                if (!Directory.Exists(compositionsDir))
                    return compositions;

                foreach (string filePath in Directory.GetFiles(compositionsDir, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        var comp = JsonSerializer.Deserialize<Composition>(json);
                        if (comp != null)
                        {
                            compositions.Add(comp);
                        }
                    }
                    catch (Exception ex)
                    {
                        Melon<TweaksAndFixes>.Logger.Warning($"[FleetCompositionData] Failed to load composition from '{filePath}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Melon<TweaksAndFixes>.Logger.Error($"[FleetCompositionData] Failed to load compositions: {ex.Message}");
            }

            return compositions;
        }

        /// <summary>
        /// Delete a fleet composition by name.
        /// </summary>
        public static bool Delete(string name)
        {
            try
            {
                string filePath = GetCompositionFilePath(name);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Melon<TweaksAndFixes>.Logger.Msg($"[FleetCompositionData] Deleted composition: {name}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Melon<TweaksAndFixes>.Logger.Error($"[FleetCompositionData] Failed to delete composition '{name}': {ex.Message}");
                return false;
            }
        }
    }
}
