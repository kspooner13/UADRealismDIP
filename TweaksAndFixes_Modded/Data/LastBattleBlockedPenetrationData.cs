using System;
using System.Reflection;
using Il2Cpp;

namespace TweaksAndFixes
{
    /// <summary>
    /// Holds armor penetration values that were blocked (shown on battle screen)
    /// so they can be displayed in the constructor UI.
    /// </summary>
    public static class LastBattleBlockedPenetrationData
    {
        /// <summary>Number of hits that were blocked by armor.</summary>
        public static int BlockedCount { get; private set; }

        /// <summary>Total armor penetration value that was blocked (e.g. 583).</summary>
        public static float BlockedPenetrationValue { get; private set; }

        /// <summary>Ship name when data was captured (for display context).</summary>
        public static string ShipName { get; private set; } = "";

        /// <summary>Whether we have any captured data to show.</summary>
        public static bool HasData => BlockedCount > 0 || BlockedPenetrationValue > 0f;

        /// <summary>Set values (e.g. from battle UI or tests).</summary>
        public static void Set(int blockedCount, float blockedPenetrationValue, string shipName = null)
        {
            BlockedCount = blockedCount;
            BlockedPenetrationValue = blockedPenetrationValue;
            ShipName = shipName ?? "";
        }

        /// <summary>Clear stored data.</summary>
        public static void Clear()
        {
            BlockedCount = 0;
            BlockedPenetrationValue = 0f;
            ShipName = "";
        }

        private static int ToInt(object v)
        {
            if (v == null) return 0;
            try { return Convert.ToInt32(v); }
            catch { return 0; }
        }

        private static float ToFloat(object v)
        {
            if (v == null) return 0f;
            try { return Convert.ToSingle(v); }
            catch { return 0f; }
        }

        /// <summary>
        /// Try to read blocked penetration from a Ship (e.g. when entering constructor after battle).
        /// Uses reflection to find combat stat fields on the game's Ship type.
        /// </summary>
        public static bool TryCaptureFromShip(Ship ship)
        {
            if (ship == null) return false;

            Clear();
            string name = "";
            try { name = ship.Name(false, false); } catch { }

            var t = ship.GetType();
            int count = 0;
            float value = 0f;

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string fn = f.Name.ToLowerInvariant();
                if (fn.Contains("blocked"))
                {
                    try
                    {
                        var v = f.GetValue(ship);
                        if (v == null) continue;
                        int i = ToInt(v);
                        float fl = ToFloat(v);
                        if (fn.Contains("count") || fn.Contains("hit") || fn == "blocked")
                            count = Math.Max(count, i);
                        else
                            count = Math.Max(count, i);
                        value = Math.Max(value, fl);
                    }
                    catch { /* ignore */ }
                }
                if (fn.Contains("penetration") && fn.Contains("block"))
                {
                    try
                    {
                        var v = f.GetValue(ship);
                        value = Math.Max(value, ToFloat(v));
                    }
                    catch { }
                }
            }

            if (count > 0 || value > 0f)
            {
                Set(count, value, name);
                return true;
            }

            ShipName = name;
            return false;
        }
    }
}
