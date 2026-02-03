using HarmonyLib;
using MelonLoader;

[assembly: MelonGame("Game Labs", "Ultimate Admiral Dreadnoughts")]
[assembly: MelonInfo(typeof(Spy.SpyMod), "Spy", "0.1.0", "Spoonz")]
[assembly: MelonOptionalDependencies("TweaksAndFixes")]

namespace Spy
{
    /// <summary>
    /// Spy mod: loads after TweaksAndFixes and can use TAF APIs.
    /// Adds an "Intel" button to the Campaign UI that opens a window listing Spies.
    /// </summary>
    public class SpyMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            HarmonyInstance.PatchAll(MelonAssembly.Assembly);
            base.OnInitializeMelon();
        }

        public override void OnDeinitializeMelon()
        {
            base.OnDeinitializeMelon();
        }
    }
}
