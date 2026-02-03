using UnityEngine;

namespace UIOverall.Utils
{
    /// <summary>
    /// UI helpers for UIOverall. No dependency on TweaksAndFixes; logic kept in sync with TAF's ModUtils where useful.
    /// </summary>
    public static class UIUtils
    {
        /// <summary>Find a descendant by name (depth-first). Returns null if not found.</summary>
        public static GameObject? FindDeepChild(GameObject? obj, string? name, bool allowInactive = true)
        {
            if (obj == null || string.IsNullOrEmpty(name))
                return null;
            if (obj.name == name)
                return obj;

            for (int i = 0; i < obj.transform.childCount; ++i)
            {
                GameObject go = obj.transform.GetChild(i).gameObject;
                if (!allowInactive && !go.active)
                    continue;

                GameObject? test = FindDeepChild(go, name, allowInactive);
                if (test != null)
                    return test;
            }

            return null;
        }
    }
}
