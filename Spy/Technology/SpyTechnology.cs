using System.Collections.Generic;

namespace Spy
{
    /// <summary>
    /// One spy technology: improves mission success rate and/or reduces capture chance on failure.
    /// </summary>
    public class SpyTechnology
    {
        /// <summary>Unique id for save/UI (e.g. "cipher_training").</summary>
        public string Id { get; }
        /// <summary>Display title (e.g. "Cipher Training").</summary>
        public string Title { get; }
        /// <summary>Short description for UI.</summary>
        public string Description { get; }
        /// <summary>Flat bonus to mission success chance (0–100).</summary>
        public int SuccessRateBonus { get; }
        /// <summary>Reduction to capture chance on failure (positive = less capture).</summary>
        public int CaptureReducePercent { get; }

        public SpyTechnology(string id, string title, string description, int successRateBonus, int captureReducePercent = 0)
        {
            Id = id;
            Title = title;
            Description = description;
            SuccessRateBonus = successRateBonus;
            CaptureReducePercent = captureReducePercent;
        }

        /// <summary>All spy technologies available in the game (10 to start).</summary>
        public static IReadOnlyList<SpyTechnology> All { get; } = new List<SpyTechnology>
        {
            new SpyTechnology(
                "cipher_training",
                "Cipher Training",
                "Agents are trained in secure communications; fewer leaks and better coordination.",
                2),
            new SpyTechnology(
                "dead_drops",
                "Dead Drops",
                "Standardized dead-drop procedures reduce exposure during handoffs.",
                2),
            new SpyTechnology(
                "safe_houses",
                "Safe Houses",
                "Network of safe houses gives agents a place to hide and regroup.",
                2, 3),
            new SpyTechnology(
                "forged_papers",
                "Forged Papers",
                "High-quality forgeries improve identity cover and border crossings.",
                3),
            new SpyTechnology(
                "counter_surveillance",
                "Counter-Surveillance",
                "Training to spot and shake tails reduces operational exposure.",
                2, 2),
            new SpyTechnology(
                "local_informants",
                "Local Informants",
                "Cultivating local sources improves situational awareness.",
                3),
            new SpyTechnology(
                "covert_communications",
                "Covert Communications",
                "One-time pads and secure channels reduce intercept risk.",
                2),
            new SpyTechnology(
                "evasion_tactics",
                "Evasion Tactics",
                "Escape and evasion training improves chances of avoiding capture if compromised.",
                2, 4),
            new SpyTechnology(
                "psychological_profiling",
                "Psychological Profiling",
                "Profiling of targets improves approach and recruitment success.",
                3),
            new SpyTechnology(
                "deep_cover_protocols",
                "Deep Cover Protocols",
                "Long-term cover identities and protocols for sustained operations.",
                3, 3)
        };
    }
}
