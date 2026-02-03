# UIOverall

UI overhaul baseline DLL for **Ultimate Admiral: Dreadnoughts**. Loads via MelonLoader and uses Harmony to hook the game's UI so you can replace or restyle screens in one place.

**Goal: replace IMGUI with UI Toolkit.** Where the game (or other mods) use IMGUI (`OnGUI`, `GUILayout`, etc.), UIOverall is intended to substitute **Unity UI Toolkit (UIElements)** — VisualElement-based UI, UXML/USS, and runtime panels — for a modern, scalable UI stack instead of IMGUI.

## Structure

- **UIOverallMod.cs** — MelonMod entry; applies `HarmonyInstance.PatchAll`. Hooks **OnSceneWasLoaded** for the main menu: when scene name is **MainMenu** or **LevelMainMenu**, shows the UI Toolkit replacement (the game uses a scene for the main menu, not a GameObject named MainMenu).
- **Core/UIOverhaulBase.cs** — Central hook class. Harmony patches call `OnUIAvailable`, `OnConstructorUIShown`, etc. Extend these methods to implement your UI changes. Use `UiInstance`, `UiRoot`, and `FindInUI(name)` for access to the game UI.
- **Harmony/Patch_Ui_Baseline.cs** — Baseline patches on the game's `Ui` class:
  - `Ui.Start` (postfix) → `UIOverhaulBase.OnUIAvailable(ui)`
  - `Ui.ConstructorUI` (postfix) → `UIOverhaulBase.OnConstructorUIShown()` when in constructor
- **Utils/UIUtils.cs** — Local helpers (e.g. `FindDeepChild`). No dependency on TweaksAndFixes; logic kept in sync with TAF's ModUtils where useful.
- **UIToolkit/MainMenuReplacement.cs** — UI Toolkit replacement for the main menu (when `USE_UI_TOOLKIT`). Shown when the **MainMenu** or **LevelMainMenu** scene loads; displays a custom panel (title + New Game, Continue, Settings, Quit). Uses `UIOverhaulBase.TrySetUiFromScene()` or the first Canvas in the scene as parent. Wire button callbacks to game flows (e.g. via dnSpy) for full behavior.

## Building

1. **ref folder:** UI Toolkit DLLs are taken from **UIOverall/ref/** — `UnityEngine.UIModule.dll`, `UnityEngine.UIElementsModule.dll`, and the source generator DLLs. The **game does not ship native UI Toolkit**. To use UI Toolkit at runtime **without breaking the game or other mods**, the ref DLLs must be **Il2Cpp-built** from the **same Unity version** as the game — see [Integrating UI Toolkit DLLs without breaking the game](#integrating-ui-toolkit-dlls-without-breaking-the-game) below. Do not put Editor/managed copies in UserLibs.
2. Ensure game assembly paths in **UIOverall.csproj** match your install (e.g. `F:\Games\Ultimate.Admiral.Dreadnoughts.1.7.0.0\game\MelonLoader\...`). Only MelonLoader/Il2Cpp/Assembly-CSharp paths need the game; UIModule and UIElementsModule use `ref/`.
3. Build UIOverall from the solution or:  
   `dotnet build UIOverall\UIOverall.csproj`
4. Post-build copies **UIOverall.dll**, **UnityEngine.UIModule.dll**, and **UnityEngine.UIElementsModule.dll** to the game Mods folder. Deploy all three so the game can load UI Toolkit at runtime. Change the `PostBuild` target in the csproj if your Mods folder is different.

**TweaksAndFixes is not required.** UIOverall has no project or runtime dependency on TAF; you can use TAF's code as a reference for patterns (e.g. ModUtils, Harmony patches) but the mod runs standalone.

## Extending the baseline

- **Add more hooks**  
  In `UIOverhaulBase`, add methods like `OnMainMenuShown()` or `OnCampaignUIShown()`. In `Patch_Ui_Baseline.cs` (or a new Harmony patch class), add `[HarmonyPostfix]` (or prefix) on the corresponding `Ui` method and call your new hook. Use dnSpy/ILSpy on `Assembly-CSharp.dll` to find the right `Ui` methods (e.g. main menu show/hide, campaign open, battle UI).

- **Replace or restyle UI**  
  Inside the hook methods, use `UIOverhaulBase.UiInstance` (same as `G.ui`), `UIOverhaulBase.UiRoot`, and `UIOverhaulBase.FindInUI("ChildName")` to get transforms/GameObjects. Clone existing elements with `GameObject.Instantiate`, change components (e.g. `Text`, `Image`, `RectTransform`), or add new panels; then parent under the appropriate root and show/hide as needed.

## Dependencies

- **Game assemblies only** — MelonLoader, Harmony, Assembly-CSharp, Il2Cpp*, UnityEngine*, Unity.TextMeshPro, etc. (via references in the csproj). **TweaksAndFixes is not required**; UIOverall uses its own `Utils.UIUtils` (e.g. `FindDeepChild`). You can use TAF as a reference for UI/Harmony patterns.

## Unity UI DLLs referenced (beneficial for UI overhaul)

UIOverall references the same UI-related Unity modules used in the game (and in TweaksAndFixes as a reference) so you can build and modify UI without missing types:

| Reference | Purpose |
|-----------|--------|
| **UnityEngine.UI** | Canvas, Button, Image, Text, Slider, ScrollRect, LayoutGroup, etc. |
| **UnityEngine.UIModule** | RectTransform, CanvasRenderer — core layout/rendering when creating or reparenting UI. |
| **UnityEngine.IMGUIModule** | IMGUI / OnGUI — **legacy; being replaced by UI Toolkit.** Kept for detecting/suppressing existing OnGUI code paths. |
| **UnityEngine.InputModule** | EventSystem, StandaloneInputModule, PointerEventData — pointer/click handling for custom panels and buttons. |
| **UnityEngine.InputLegacyModule** | `Input` class — keyboard/gamepad (e.g. hotkeys, escape to close). |
| **UnityEngine.TextRenderingModule** | `Font`, text rendering — when using legacy `Text` or styling fonts (see e.g. ConstructorArmorProtectionPanel in TAF). |
| **UnityEngine.ImageConversionModule** | Texture2D loading, sprite/icon creation — custom assets, icons, backgrounds. |
| **Unity.TextMeshPro** | TMP_Text, TextMeshProUGUI — game uses TMP for most text; use for new labels/inputs that match the game. |
| **UnityEngine.UIElementsModule** | **UI Toolkit (UIElements)** — VisualElement, UIDocument, PanelSettings, UXML/USS. Optional; see below. |

No additional third-party UI DLLs are required; the game ships with these in `MelonLoader\Il2CppAssemblies\`. If the game is updated and adds packages (e.g. Unity.InputSystem), you can add those references the same way if needed.

### Using Unity UI Toolkit (UIElements) — replacing IMGUI

UIOverall’s goal is to **replace IMGUI with UI Toolkit**. The project references **UnityEngine.UIElementsModule** so you can build UI with `UnityEngine.UIElements`: `VisualElement`, `UIDocument`, `PanelSettings`, `Button`, `Label`, `TextField`, etc., plus UXML/USS where the game supports it.

**Requirement:** The game must be built with UI Toolkit included. Check for  
`MelonLoader\Il2CppAssemblies\UnityEngine.UIElementsModule.dll` in your game install.  
If it’s missing, the game doesn’t ship UI Toolkit; set `<UseUIToolkit>false</UseUIToolkit>` in **UIOverall.csproj** (in the first `<PropertyGroup>`) so the reference is not used and the project still builds.

**At runtime (in your hooks, e.g. `UIOverhaulBase.OnUIAvailable` or `OnConstructorUIShown`):**

1. **Panel and root** — Create a `PanelSettings` (asset or from code) and a root `VisualElement`. For overlay UI you typically create a `UIDocument` (or attach a root to the game’s Canvas via `PanelSettings` that uses the game’s camera/render mode).
2. **Build UI in C#** — Use `new VisualElement()`, `new Button()`, `new Label()`, etc., set `style.*`, and add to the root. No UXML required.
3. **Or load UXML/USS** — If you ship `.uxml` / `.uss` in your mod, load them with `AssetDatabase` (editor) or at runtime via `Resources`, `Addressables`, or reading from disk and parsing; then instantiate the tree and attach styles. The game must support loading these at runtime (Il2Cpp may require assets to be in the game bundle or loaded in a supported way).
4. **Attach to the game** — Parent your UI under the right Canvas or use a `UIDocument` that renders on top. You can instantiate a `GameObject`, add `UIDocument`, assign your root and `PanelSettings`, and parent under `UIOverhaulBase.UiRoot` or another transform so it appears in the correct place.

**Conditional compile:** When `<UseUIToolkit>` is `true`, the project defines `USE_UI_TOOLKIT`. Wrap UI Toolkit–only code in `#if USE_UI_TOOLKIT` so the project still builds when you set `UseUIToolkit` to `false` (game without UI Toolkit).

**Replacing IMGUI in practice:** (1) Use dnSpy/ILSpy to find where the game (or mods) call `OnGUI`, `GUILayout.*`, `GUI.*`, etc. (2) Add Harmony patches: prefix to skip the original IMGUI block (return without running it) and/or postfix to run after. (3) In your hook, create and show a UI Toolkit panel (e.g. `UIDocument` + root `VisualElement`) that provides the same or better functionality. Parent it under `UIOverhaulBase.UiRoot` or the right Canvas so it appears where the old IMGUI did. Over time, replace each IMGUI screen with a UI Toolkit equivalent and suppress the original OnGUI for that path.

**Docs:** [Unity – UI Toolkit](https://docs.unity3d.com/Manual/UIElements.html), [Runtime UI](https://docs.unity3d.com/Manual/UIE-get-started-with-runtime-ui.html). Namespace: `UnityEngine.UIElements`.

### Integrating UI Toolkit DLLs without breaking the game

The game is built with **Il2CPP**. If you put **managed** (Editor or standalone) Unity DLLs in MelonLoader’s **UserLibs** (or load them with the mod), they conflict with the game’s **Il2Cpp** Unity assemblies. You get:

- `MissingMethodException: Method not found: 'Void UnityEngine.RequireComponent..ctor(System.Type)'`
- `TypeLoadException: Could not load type 'UnityEngine.PropertyName' from assembly 'UnityEngine.CoreModule'`
- Other mods (e.g. TweaksAndFixes) can break when they call `GetComponent<T>()` because the runtime resolves types from the wrong assembly.

**Cause:** Managed `UnityEngine.UIElementsModule.dll` / `UnityEngine.UIModule.dll` (from Unity Editor or a NuGet/ref build) reference `UnityEngine` types (e.g. `RequireComponent`, `PropertyName`) that have **different assembly identity** than the game’s Il2Cpp `UnityEngine.CoreModule`. Loading both in the same app domain makes the CLR resolve those types from the wrong module and breaks Il2CppInterop and the game.

**Fix: use Il2Cpp-built UI Toolkit DLLs from the same Unity version as the game.**

1. **Find the game’s Unity version**  
   - In the game folder: check `[Game]_Data/globalgamemanagers` or version files, or run the game and log `Application.unityVersion` (e.g. `2020.3.xx` or `2021.3.xx`).

2. **Get Il2Cpp-built UIElementsModule and UIModule**  
   - Install that **exact** Unity version (e.g. via Unity Hub).  
   - Create a **new Unity project** with that version.  
   - Ensure the project uses UI Toolkit (e.g. add a UIDocument to a scene, or add the UI Toolkit package).  
   - **Build for Windows, Il2CPP** (File → Build Settings → Platform Windows, Scripting Backend Il2CPP, Build).  
   - In the **build output**, open the folder that contains the game’s managed/Il2Cpp assemblies (often `[BuildName]_Data/Managed` or the equivalent Il2Cpp-generated C# DLLs; on some setups they are under the build root or in a Backup folder).  
   - Copy **UnityEngine.UIElementsModule.dll** and **UnityEngine.UIModule.dll** from that build. Those are the **Il2Cpp** wrappers that match the game’s Unity version and CoreModule.

3. **Use those DLLs everywhere**  
   - Put the **same** Il2Cpp-built DLLs in **UIOverall/ref/** so the project compiles against them.  
   - At runtime, put them in **MelonLoader’s UserLibs** (or wherever you currently load them) so MelonLoader loads them before mods.  
   - Do **not** put Editor/managed copies of UIElementsModule or UIModule in UserLibs; only the Il2Cpp build output from step 2.

4. **If the Il2CPP build doesn’t include UIElementsModule**  
   - Some Unity versions or templates don’t include UI Toolkit in the Il2CPP build. In that case, add a scene or script that references UI Toolkit (e.g. `UnityEngine.UIElements.UIDocument`) so Unity includes the module in the build, then rebuild and copy the DLLs again.

**Summary:** Use **Il2Cpp-built** `UnityEngine.UIElementsModule.dll` and `UnityEngine.UIModule.dll` from a **same-version** Unity Il2CPP build. Use them in **ref/** for compile and in **UserLibs** (or your load path) for runtime. Do not use Editor or other managed Unity DLLs for UI Toolkit at runtime or you will break the game and other mods.

---

## External UI modules from the internet (not included)

These are **not** referenced by UIOverall by default. They are optional NuGet/community packages that can help with in-game UI if you choose to add them. Compatibility with this game’s MelonLoader + Il2CppInterop version should be verified before relying on them.

| Module | Source | Purpose | Notes |
|--------|--------|---------|--------|
| **UniverseLib.IL2CPP.Unhollower** | [NuGet](https://www.nuget.org/packages/UniverseLib.IL2CPP.Unhollower/) (v1.5.1) | Shared UI/plugin framework for IL2CPP Unity games; used by tools like UnityExplorer. | Built for the older Unhollower runtime. MelonLoader now uses Il2CppInterop — may need the Interop variant below. |
| **UniverseLib.IL2CPP.Interop.ML** | [NuGet](https://www.nuget.org/packages/UniverseLib.IL2CPP.Interop.ML/) | UniverseLib variant for Il2CppInterop / MelonLoader. | “ML” = MelonLoader; better fit if you want UniverseLib with current MelonLoader. |
| **rainbowblood.UniverseLib.IL2CPP** | [NuGet](https://www.nuget.org/packages/rainbowblood.UniverseLib.IL2CPP) | Maintained fork of UniverseLib for IL2CPP. | See [UniverseLib wiki](https://github.com/sinai-dev/UniverseLib/wiki) (archived) for usage; check which Il2Cpp stack this fork targets. |
| **UniverseLib.Analyzers** | [NuGet](https://www.nuget.org/packages/UniverseLib.Analyzers) | Roslyn analyzers for UniverseLib to avoid common mistakes. | Optional; add only if you use UniverseLib. |
| **MelonLoader-GUI-Menu-Base** | [GitHub](https://github.com/UrFingPoor/MelonLoader-GUI-Menu-Base) | Base for IMGUI-style menus in MelonLoader mods (MIT). | Good for a simple mod menu overlay; IMGUI-based, not uGUI/Canvas. |

### Adding a NuGet UI module

1. **Add the package** (example for UniverseLib Unhollower variant):
   ```bash
   dotnet add UIOverall/UIOverall.csproj package UniverseLib.IL2CPP.Unhollower
   ```
2. **Resolve conflicts**: These packages may bring their own Il2Cpp/Unhollower references. Prefer the game’s MelonLoader/Il2CppInterop assemblies (from `HintPath` in the csproj); exclude or alias duplicate references if the build complains.
3. **Use the API**: Follow the module’s docs (e.g. UniverseLib wiki, or the MelonLoader-GUI-Menu-Base README) to create panels, menus, or explorers that run inside the game process.

### Recommendation

- For **overhauling the game’s existing UI** (menus, constructor, campaign): the built-in Unity modules above are enough; patch and clone the game’s `Ui` / Canvas as in this baseline.
- For **new overlay UI** (e.g. a separate mod menu or debug panel): consider **UniverseLib.IL2CPP.Interop.ML** (if it works with your MelonLoader version) or **MelonLoader-GUI-Menu-Base** for a quick IMGUI menu.
