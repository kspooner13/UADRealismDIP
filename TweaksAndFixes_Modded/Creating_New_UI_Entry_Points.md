# Creating Custom / New UI Entry Points (UAD TAF)

This guide describes how to add new UI elements and screens to the game using the **TweaksAndFixes** framework. It is based on the patterns used in `/TweaksAndFixes/` (Modified/, Harmony/, Data/, Utils/).

---

## Table of Contents

1. [When UI Modifications Run](#1-when-ui-modifications-run)
2. [Entry Points: Where to Hook Your UI](#2-entry-points-where-to-hook-your-ui)
3. [Campaign UI: Buttons and Windows](#3-campaign-ui-buttons-and-windows)
4. [Constructor (Dockyard) UI: Panels and Folds](#4-constructor-dockyard-ui-panels-and-folds)
5. [Finding Game Objects: GetChildAtPath](#5-finding-game-objects-getchildatpath)
6. [ModifyUi and Per-Frame Updates](#6-modifyui-and-per-frame-updates)
7. [TAFUI Building Blocks](#7-tafui-building-blocks)
8. [Cloning vs. Creating From Scratch](#8-cloning-vs-creating-from-scratch)
9. [Building UI From Scratch](#9-building-ui-from-scratch)
10. [Tables and list rows](#10-tables-and-list-rows)
11. [Localization and Tooltips](#11-localization-and-tooltips)
12. [Adding UI From an External Mod](#12-adding-ui-from-an-external-mod)
13. [Discovering Game UI (Hierarchy and Types)](#13-discovering-game-ui-hierarchy-and-types)
14. [Checklist for a New UI Feature](#14-checklist-for-a-new-ui-feature)

---

## 1. When UI Modifications Run

- **`UiM.ApplyUiModifications()`** is called once from **`GameData.PostProcessAll`** (see `Harmony/GameData.cs`). That is the single place where all TAF UI setup is triggered after game data is loaded.
- **`ApplyUiModifications()`** (in `Modified/UiM.cs`) does, in order:
  - `CreateBailoutPopup()`, `CreateHidePopupsButton()`, `ApplySettingsMenuModifications()`
  - **`ApplyCampaignWindowModifications()`** — campaign map UI (top panel buttons, fleet window, etc.)
  - **`ApplyDockyardModifications()`** — constructor/dockyard UI (left/right panels, folds, etc.)
- **Per-frame application** of stored modifications happens in **`Ui.Update`**: TAF's Harmony postfix calls **`UiM.UpdateModifications()`**, which applies all registered `UiModification` instances (including any `SetOnUpdate` callbacks).

So: **one-time setup** = inside `ApplyCampaignWindowModifications` or `ApplyDockyardModifications` (or from a Harmony postfix that runs after them). **Per-frame logic** = register via `ModifyUi(...).SetOnUpdate(...)` and run it from that callback.

---

## 2. Entry Points: Where to Hook Your UI

| Context | Method to extend | What to do |
|--------|-------------------|------------|
| **Campaign map** (world view, top tabs, fleet/politics windows) | `UiM.ApplyCampaignWindowModifications()` | Add your button and/or window initialization here (e.g. your `Initialize()`). For external mods, use a Harmony **Postfix** on this method. |
| **Constructor / dockyard** (left/right scroll areas, folds) | `UiM.ApplyDockyardModifications()` | Create panels, folds, or inject into existing scroll content. Register `SetOnUpdate` for the Constructor root and call your UI's update/ensure logic there (e.g. your `Create()` then your `OnUpdate(ui)` and `EnsurePanel(ui)`). |

- **Campaign:** add a **tab button** and a **window** (often by cloning `G.ui.FleetWindow.Root`).
- **Dockyard:** add **panels** or **folds** under the left or right scroll content, and optionally use **ModifyUi(Constructor).SetOnUpdate** to create/update your UI only when in constructor.

---

## 3. Campaign UI: Buttons and Windows

### 3.1 Adding a Top-Panel Button (Campaign)

- **Path to the button container:** `Global/Ui/UiMain/WorldEx/TopPanel/Tabs/Buttons`
- **Two approaches:**
  - **Clone a template** — Instantiate an existing button (e.g. **"Fleet"**), reparent, rename, set text, replace click listener. Simple and preserves all visuals and components.
  - **Create from scratch and copy reference aspects** — Build a new button (GameObject + RectTransform + Image + Button + TextMeshProUGUI child) and copy only the visual properties (color, font, fontSize, layout) from a reference button. No template clone; you control structure and avoid carrying over unwanted components (e.g. LocalizeText).

**Example (clone template):**

```csharp
GameObject buttonsParent = ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/TopPanel/Tabs/Buttons");
GameObject templateButton = buttonsParent.GetChild("Fleet");

GameObject buttonObj = GameObject.Instantiate(templateButton);
buttonObj.transform.SetParent(buttonsParent.transform, false);
buttonObj.name = "YourButtonName";
buttonObj.transform.localScale = Vector3.one;
buttonObj.transform.localPosition = Vector3.zero;

// Set label (remove LocalizeText if you set text manually)
GameObject textObj = buttonObj.GetChild("Text (TMP)", true);
if (textObj != null)
{
    textObj.TryDestroyComponent<LocalizeText>();
    textObj.GetComponent<TMP_Text>().text = "Your Label";
}

Button button = buttonObj.GetComponent<Button>();
button.onClick.RemoveAllListeners();
button.onClick.AddListener(() => ShowYourWindow());
```

**Example (create new and copy reference aspects):**

```csharp
GameObject buttonsParent = ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/TopPanel/Tabs/Buttons");
GameObject referenceButton = buttonsParent.GetChild("Fleet");

// Create new button (no clone)
GameObject buttonObj = new GameObject("YourButtonName");
buttonObj.transform.SetParent(buttonsParent.transform, false);
RectTransform rt = buttonObj.AddComponent<RectTransform>();
rt.localScale = Vector3.one;
rt.localPosition = Vector3.zero;
// Copy size/layout from reference
RectTransform refRt = referenceButton.GetComponent<RectTransform>();
rt.sizeDelta = refRt.sizeDelta;
rt.anchorMin = refRt.anchorMin;
rt.anchorMax = refRt.anchorMax;

Image bg = buttonObj.AddComponent<Image>();
bg.color = referenceButton.GetComponent<Image>().color;  // or set your own
Button button = buttonObj.AddComponent<Button>();

// Text child
GameObject textObj = new GameObject("Text (TMP)");
textObj.transform.SetParent(buttonObj.transform, false);
RectTransform textRt = textObj.AddComponent<RectTransform>();
textRt.anchorMin = Vector2.zero;
textRt.anchorMax = Vector2.one;
textRt.offsetMin = Vector2.zero;
textRt.offsetMax = Vector2.zero;
TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
tmp.text = "Your Label";
// Copy font aspects from reference
TMP_Text refTmp = referenceButton.GetChild("Text (TMP)", true).GetComponent<TMP_Text>();
tmp.font = refTmp.font;
tmp.fontSize = refTmp.fontSize;
tmp.alignment = refTmp.alignment;
tmp.color = refTmp.color;

button.onClick.AddListener(() => ShowYourWindow());
```

- Use **`LocalizeManager.Localize("$Your_Key")`** if you use TAF's localization (see [§11](#11-localization-and-tooltips)).

### 3.2 Adding a Campaign Window (Clone Fleet Window)

- **Template:** `G.ui.FleetWindow.Root`
- **Steps:**
  1. `GameObject.Instantiate(G.ui.FleetWindow.Root)`.
  2. Set name, parent to `G.ui.gameObject.transform`, scale/position, and `SetActive(false)`.
  3. Remove the game's **CampaignFleetWindow** component from the clone so it doesn't drive fleet logic.
  4. Get the inner **"Root"** child; resize with **RectTransform** (e.g. `offsetMin` / `offsetMax`).
  5. Hide or remove original children of that Root, then add your own panels (see [§9](#9-building-ui-from-scratch)).

Example (conceptually):

```csharp
_windowRoot = GameObject.Instantiate(G.ui.FleetWindow.Root);
_windowRoot.name = "YourWindow";
_windowRoot.transform.SetParent(G.ui.gameObject.transform, false);
_windowRoot.transform.localScale = Vector3.one;
_windowRoot.transform.localPosition = Vector3.zero;
_windowRoot.SetActive(false);

_windowRoot.TryDestroyComponent<CampaignFleetWindow>();

GameObject root = _windowRoot.GetChild("Root");
RectTransform rootRect = root.GetComponent<RectTransform>();
rootRect.offsetMax = new Vector2(800f, 400f);
rootRect.offsetMin = new Vector2(-800f, -400f);

// Hide all original content, then add your panels
for (int i = 0; i < root.transform.childCount; i++)
    root.transform.GetChild(i).gameObject.SetActive(false);

CreateYourPanel(root);
```

- **Show/hide:** keep a reference to `_windowRoot` and call `_windowRoot.SetActive(true)` / `SetActive(false)` from your button's click handler.

---

## 4. Constructor (Dockyard) UI: Panels and Folds

### 4.1 Left Panel (Scroll Content)

- **Path:** `Global/Ui/UiMain/Constructor/Left/Scroll View/Viewport/Cont`
- **Pattern:** In `ApplyDockyardModifications()`, call your static `Create()` which uses `ModUtils.GetChildAtPath(LeftPanelContentPath)` and adds a new panel as a child. Use **TAFUI** (e.g. `TAF_Text`, `TAF_Button`) or build with **RectTransform** + **LayoutGroup**.

Example (conceptually):

```csharp
// In ApplyDockyardModifications():
YourPanel.Create();

// And in the Constructor SetOnUpdate callback:
ModifyUi(ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor")).SetOnUpdate((GameObject ui) => {
    // ... other logic ...
    YourPanel.OnUpdate(ui);
    YourFold.EnsurePanel(ui);
});
```

### 4.2 Right Panel (Folds / Sections)

- **Path:** `Global/Ui/UiMain/Constructor/Right/Scroll View/Viewport/Cont`
- **Pattern:** Find an existing fold (e.g. **"FoldSectionsInfo"**) in that container, clone it with `GameObject.Instantiate`, rename, reparent to the same container, then replace the fold's inner content with your own.
- For content that depends on the game's Sections view (e.g. RawImages fed by Sections cameras), locate **SectionsSide** / **SectionsTop** RawImage textures and game types (e.g. **Il2Cpp.Fold**, **Il2Cpp.SectionInfo**) via hierarchy logging and decompilation (e.g. dnSpy).

### 4.3 When Constructor UI Is Active

- **UiM** registers **ModifyUi(Constructor).SetOnUpdate(...)**. That callback runs every frame only when the Constructor root is active; inside it call your **OnUpdate(ui)** and **EnsurePanel(ui)** so your UI is created once and then updated (or conditionally created) each frame. Use **`GameManager.IsConstructor`** if you need to skip work when not in dockyard.

---

## 5. Finding Game Objects: GetChildAtPath

- **`ModUtils.GetChildAtPath(string path, GameObject root = null)`** (in `Utils/ModUtils.cs`).
- **Default root:** if `root` is null, it uses **`G.ui.gameObject`**. For that default, paths can start with **`Global/Ui/UiMain/`** or **omit** it (the method strips `Global/Ui/UiMain/` when root is the UI root).
- **Path format:** slash-separated child names, e.g. `"Global/Ui/UiMain/Constructor/Left/Scroll View/Viewport/Cont"` or `"Constructor/Left/Scroll View/Viewport/Cont"`.
- **Relative to a custom root:** pass the parent GameObject as the second argument, e.g. `ModUtils.GetChildAtPath("Root/Header", politicsWindow)`.
- **Inactive children:** the underlying lookup uses the game's `GetChild(entry, true)` (include inactive). If a path fails, the method logs an error and returns a new empty GameObject, so check for null or invalid names in debug.

**Common paths:**

| Path (under `Global/Ui/UiMain/` or relative to `G.ui.gameObject`) | Use |
|-------------------------------------------------------------------|-----|
| `WorldEx/TopPanel/Tabs/Buttons` | Campaign top tab buttons (Fleet, Design, etc.) |
| `WorldEx/Windows/Politics Window` | Politics window root |
| `Constructor/Left/Scroll View/Viewport/Cont` | Left scroll content (add panels here) |
| `Constructor/Right/Scroll View/Viewport/Cont` | Right scroll content (folds) |
| `Constructor/Right/.../FoldSectionsInfo` | Sections fold template |
| `Popup/PopupMenu/Window/ButtonBase` | Button template for TAF_Button |
| `Constructor/.../ShipName/EditName/Static/Text` | Text template for TAF_Text |

---

## 6. ModifyUi and Per-Frame Updates

- **`UiM.ModifyUi(GameObject ui)`** (and overload **`ModifyUi(ui, childPath)`**) registers or retrieves a **UiModification** for that GameObject. Modifications are applied each frame in **UiM.UpdateModifications()**, which is called from the **Ui.Update** Harmony postfix.
- **UiModification** supports:
  - **ReplaceOffsets / ReplaceOffsetMin/Max**, **ReplaceAnchors**, **ReplaceLayoutDimensions**, **SetChildOrder**
  - **SetActive / SetVisible / SetEnabled**
  - **SetOnUpdate(Action<GameObject>)** — invoked every frame when the modification is applied; use this to create/update your custom UI (e.g. refresh text, ensure panel exists).

Example:

```csharp
ModifyUi(ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor")).SetOnUpdate((GameObject ui) => {
    YourPanel.OnUpdate(ui);
    YourFold.EnsurePanel(ui);
});
```

- So: **one-time creation** can happen inside `ApplyDockyardModifications` (e.g. your `Create()`); **per-frame creation/update** can happen inside a **SetOnUpdate** callback registered for the appropriate root (e.g. Constructor).

---

## 7. TAFUI Building Blocks

**Namespace:** `TweaksAndFixes.Data` (**TAFUI** class in `Data/TAFUI.cs`).

- **TAF_InputField** — from existing GameObject or new from template (Constructor ShipName style). Use for editable text (e.g. with character limits, submit/change callbacks).
- **TAF_Text** — from existing or new; template: Constructor `.../EditName/Static/Text`. Use for labels/read-only text.
- **TAF_Button** — from existing or new; template: `Global/Ui/UiMain/Popup/PopupMenu/Window/ButtonBase`. Use for buttons; call **SetOnClick(Action)** to wire click.

**Alternative to templates:** You do not have to clone a template. You can create a new GameObject, add **RectTransform**, **Image**, **Button**, and a child with **TextMeshProUGUI**, then copy only the visual aspects (color, font, fontSize, alignment, sizeDelta, anchors) from a reference button. That gives you a matching look without carrying over the reference's components or hierarchy (see [§3.1](#31-adding-a-top-panel-button-campaign) example "create new and copy reference aspects").

Creating a **TAF_Button** (conceptually):

```csharp
var btn = new TAFUI.TAF_Button(parent, "ButtonName", "Label", offsetMax, offsetMin);
btn.SetOnClick(() => { /* ... */ });
```

Creating **TAF_Text**:

```csharp
var tafText = new TAFUI.TAF_Text(parent, "Name", "Your text", offsetMax, offsetMin);
// then use tafText.textComp to update text later
```

- These clones use **game templates**, so they match the game's look and work with the game's Canvas/EventSystem.

---

## 8. Cloning vs. Creating From Scratch

- **Templates (clone):** **Instantiate** an existing GameObject to reuse its full hierarchy, components, and look. Use when you want minimal code and are fine removing or overriding a few components (e.g. LocalizeText, click listener).
- **Reference aspects (no clone):** Create new GameObjects (e.g. **Image** + **Button** + **TextMeshProUGUI** child), then copy only the visual properties you need from a reference (color, font, fontSize, sizeDelta, anchors). Use when you want full control over structure and no inherited components.
- **Campaign window:** Clone **`G.ui.FleetWindow.Root`** for a full-window layout; remove **CampaignFleetWindow**, resize **Root** RectTransform, hide original children, add your content (see [§3.2](#32-adding-a-campaign-window-clone-fleet-window)).
- **Constructor fold:** Clone a fold from **Right/Scroll View/Viewport/Cont** (e.g. **FoldSectionsInfo**) for a collapsible section; replace the inner content with your own GameObjects and components.
- **Buttons:** Either clone **Fleet** (or **Popup/PopupMenu/Window/ButtonBase**) or create a new button and copy color, font, and size from a reference (see [§3.1](#31-adding-a-top-panel-button-campaign)).

---

## 9. Building UI From Scratch

When you don't clone a template, build with standard Unity UI:

- **GameObject** + **RectTransform** (or **AddComponent<RectTransform>** for UI).
- **Image** for backgrounds (e.g. `panel.AddComponent<Image>(); ... color = new Color(...)`).
- **ScrollRect**: create a parent with **ScrollRect**, child **Viewport** (with **Mask**), child **Content** with **VerticalLayoutGroup** + **ContentSizeFitter** (vertical = PreferredSize). Set **scroll.content** and **scroll.viewport** to the Content and Viewport **RectTransform**s.
- **TextMeshProUGUI** for labels; **Button** for clickable elements.
- **LayoutGroup** (e.g. **VerticalLayoutGroup**, **HorizontalLayoutGroup**) and **LayoutElement** (preferred height/width) to arrange and size elements.

Example (conceptually, for a simple panel with title and scroll):

```csharp
GameObject panel = new GameObject("Panel");
panel.transform.SetParent(root.transform, false);
RectTransform panelRect = panel.AddComponent<RectTransform>();
panelRect.anchorMin = new Vector2(0f, 0f);
panelRect.anchorMax = new Vector2(1f, 1f);
panelRect.offsetMin = new Vector2(-800f, -700f);
panelRect.offsetMax = new Vector2(-450f, -70f);
panel.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 0.95f);

GameObject title = new GameObject("Title");
title.transform.SetParent(panel.transform, false);
// ... RectTransform, TextMeshProUGUI ...
```

Use the patterns in §3 and §9 for panels, scroll views, headers, and buttons.

---

## 10. Tables and list rows

Tables and list views are typically: a **container panel** → optional **header row** → **ScrollRect** → **Viewport** (with **Mask**) → **Content** (with **VerticalLayoutGroup** + **ContentSizeFitter**). Each **row** is a child of Content. Rows can be **cloned from a template** or **built from scratch** (with optional copy of reference aspects).

### 10.1 Table structure (from scratch)

1. **Panel** — RectTransform + Image (background).
2. **ScrollRect** — parent with **ScrollRect** (vertical = true, horizontal = false).
3. **Viewport** — child of ScrollRect, RectTransform (stretch), **Mask** (showMaskGraphic = false), optional Image for clipping.
4. **Content** — child of Viewport, RectTransform with **anchorMin** (0, 1), **anchorMax** (1, 1), **pivot** (0.5, 1); add **VerticalLayoutGroup** (spacing, childControlHeight/Width) and **ContentSizeFitter** (verticalFit = PreferredSize). Assign **scroll.content** = Content RectTransform, **scroll.viewport** = Viewport RectTransform.
5. **Header row (optional)** — same layout as a data row (e.g. HorizontalLayoutGroup + columns), added as sibling above the scroll or as first child of Content with fixed labels.
6. **Data rows** — each row is a child of Content (see below).

### 10.2 Row items: clone template vs create from scratch

- **Clone a template row:** Get a reference row from the game (e.g. under FleetWindow's fleet list Content, or a **Template** child). **Instantiate** it, set parent to your Content, set name, then fill or replace text/buttons. Use **UiM.InstanciateUI(template, parent, name, localPos, scale)** if you want the same helper. Removes the need to define layout yourself but carries over all components (LocalizeText, game logic, etc.); you may need to clear listeners or destroy components.
- **Create a row from scratch:** Build a new GameObject for the row, add **RectTransform**, **HorizontalLayoutGroup** (spacing, childForceExpandHeight), and **LayoutElement** (preferredHeight). Add one child per column: each child has **RectTransform**, **LayoutElement** (preferredWidth or flexibleWidth), and **TextMeshProUGUI**. No template; you control structure. Optionally copy **font**, **fontSize**, **color**, and **preferredHeight** / column widths from a reference row so it matches the game's look.

### 10.3 Building a table row from scratch

Create one GameObject per row, add layout and columns, then set text (and optionally copy visual aspects from a reference).

**Example (one row with three columns, no template):**

```csharp
GameObject CreateTableRow(GameObject contentParent, float rowHeight, TMP_Text referenceText = null)
{
    GameObject row = new GameObject("Row");
    row.transform.SetParent(contentParent.transform, false);
    RectTransform rowRt = row.AddComponent<RectTransform>();
    rowRt.anchorMin = new Vector2(0f, 1f);
    rowRt.anchorMax = new Vector2(1f, 1f);
    rowRt.pivot = new Vector2(0.5f, 1f);
    rowRt.offsetMin = Vector2.zero;
    rowRt.offsetMax = Vector2.zero;

    LayoutElement rowLe = row.AddComponent<LayoutElement>();
    rowLe.preferredHeight = rowHeight;
    HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
    hlg.spacing = 6f;
    hlg.childForceExpandHeight = true;
    hlg.childControlWidth = true;

    // Column 1
    GameObject col1 = new GameObject("Col1");
    col1.transform.SetParent(row.transform, false);
    col1.AddComponent<RectTransform>();
    col1.AddComponent<LayoutElement>().preferredWidth = 120f;
    TMP_Text t1 = col1.AddComponent<TextMeshProUGUI>();
    t1.text = "";
    if (referenceText != null) { t1.font = referenceText.font; t1.fontSize = referenceText.fontSize; t1.color = referenceText.color; }

    // Column 2
    GameObject col2 = new GameObject("Col2");
    col2.transform.SetParent(row.transform, false);
    col2.AddComponent<RectTransform>();
    col2.AddComponent<LayoutElement>().preferredWidth = 80f;
    TMP_Text t2 = col2.AddComponent<TextMeshProUGUI>();
    t2.text = "";
    if (referenceText != null) { t2.font = referenceText.font; t2.fontSize = referenceText.fontSize; t2.color = referenceText.color; }

    // Column 3 (flexible)
    GameObject col3 = new GameObject("Col3");
    col3.transform.SetParent(row.transform, false);
    col3.AddComponent<RectTransform>();
    col3.AddComponent<LayoutElement>().flexibleWidth = 1f;
    TMP_Text t3 = col3.AddComponent<TextMeshProUGUI>();
    t3.text = "";
    if (referenceText != null) { t3.font = referenceText.font; t3.fontSize = referenceText.fontSize; t3.color = referenceText.color; }

    return row;
}
```

To use: create your **Content** (with VerticalLayoutGroup + ContentSizeFitter) as in §9, then call `CreateTableRow(content, 28f, referenceRow?.GetChild("Col1")?.GetComponent<TMP_Text>())` for each data row; set each column's **TextMeshProUGUI.text** from your data. For a **header row**, create one row with bold or different styling and add it as the first child of Content.

### 10.4 Making rows clickable (optional)

Add a **Button** (or **EventTrigger**) to the row GameObject so the whole row is clickable. Set **Button.targetGraphic** to a transparent **Image** on the row (or the row's Image if it has one), and **onClick** to your handler. Alternatively, add a small button only in one column.

### 10.5 Summary

| Approach | When to use |
|----------|-------------|
| **Clone template row** | Fast; you accept the game's row structure and remove/override a few components. |
| **Create row from scratch** | Full control; no inherited components. Copy font/size/color from a reference row to match the game's look. |
| **Full table from scratch** | Panel + ScrollRect + Viewport + Content (§9 + above), then add header row (optional) and data rows (clone or create from scratch). |

---

## 11. Localization and Tooltips

- **Localization:** Use **`LocalizeManager.Localize("$Key")`** for user-facing strings. Add keys and values in TAF's `.lng` file (e.g. under `Assets/TAFData/locText.lng` or the path configured in **Config._LocFile**). For buttons that use the game's **LocalizeText** component, you can instead set **LocalizedElements[0].Tag** to `$Key` (see **UiM.SetLocalizedTextTag**).
- **Tooltips:** **UiM.AddTooltip(GameObject ui, string content)** adds an **OnEnter** component that shows a tooltip (content can be a localization key). Example: `AddTooltip(buttonObj, "$TAF_tooltip_set_role");`

---

## 12. Adding UI From an External Mod

If your mod lives **outside** TweaksAndFixes:

1. **Reference** the TweaksAndFixes assembly (and any Il2Cpp/Unity/MelonLoader refs the game uses).
2. **Hook after** TAF's campaign UI is applied so the campaign top panel and windows exist. The standard way is to **Harmony Postfix** **`UiM.ApplyCampaignWindowModifications`** (or **ApplyDockyardModifications** for dockyard UI).
3. In the Postfix, call your **static Initialize()** which creates the button and window (same patterns as above: **GetChildAtPath** for Buttons, clone **G.ui.FleetWindow.Root** for the window, etc.).

Example:

```csharp
[HarmonyPatch(typeof(TweaksAndFixes.UiM), nameof(TweaksAndFixes.UiM.ApplyCampaignWindowModifications))]
internal static class Patch_UiM_YourMod
{
    [HarmonyPostfix]
    internal static void Postfix()
    {
        YourWindow.Initialize();
    }
}
```

- Your **Initialize()** should:
  - Use **TweaksAndFixes.ModUtils.GetChildAtPath** for paths (and optionally **TweaksAndFixes.Data.TAFUI** if you want TAF buttons/text).
  - Clone **G.ui.FleetWindow.Root**, remove **CampaignFleetWindow**, build your content on the inner Root.
  - Add a button under **Global/Ui/UiMain/WorldEx/TopPanel/Tabs/Buttons** that shows/hides your window.

---

## 13. Discovering Game UI (Hierarchy and Types)

- **Hierarchy:** At runtime, use **ModUtils.GetChildAtPath** and the game's **GetChild(name, true)** / **GetChildren()** to walk the tree. Log names and child counts to learn paths (e.g. under **Constructor**, **WorldEx**, **Popup**).
- **Sections / Constructor right panel:** At runtime you can log **FoldSectionsInfo**, **SectionsSide** / **SectionsTop** RawImages, and game types containing "Section"/"Fold". Use decompilation (e.g. dnSpy) to find the methods that build or update the Sections UI so you can hook or reuse them (e.g. **Il2Cpp.Fold**, **Il2Cpp.SectionInfo**).

---

## 14. Checklist for a New UI Feature

- [ ] **Decide context:** Campaign (top panel + window) or Constructor (left/right panel or fold).
- [ ] **Choose entry point:** `ApplyCampaignWindowModifications()` or `ApplyDockyardModifications()` (or Harmony Postfix on one of them for external mods).
- [ ] **One-time creation:** In your `Create()` / `Initialize()`: get parent via **GetChildAtPath**, clone or create GameObjects, set parent/name/transform, wire Button listeners, store references for show/hide or update.
- [ ] **Per-frame (if needed):** Register **ModifyUi(appropriateRoot).SetOnUpdate(...)** and in the callback call your **OnUpdate** or **EnsurePanel** logic.
- [ ] **Paths:** Use **ModUtils.GetChildAtPath** with paths from [§5](#5-finding-game-objects-getchildatpath); for new panels, use the Left/Right **Cont** paths.
- [ ] **Building blocks:** Prefer **TAFUI.TAF_Button**, **TAF_Text**, **TAF_InputField** when they fit; otherwise build with RectTransform, Image, TextMeshProUGUI, ScrollRect, LayoutGroups.
- [ ] **Tables/lists:** For row-based data, use ScrollRect + Content (VerticalLayoutGroup + ContentSizeFitter); add rows by cloning a template or creating from scratch and copying reference aspects (see [§10](#10-tables-and-list-rows)).
- [ ] **Windows:** Clone **G.ui.FleetWindow.Root** for campaign; remove **CampaignFleetWindow**; resize Root and replace content.
- [ ] **Localization:** Use **LocalizeManager.Localize("$Key")** and **AddTooltip** with keys; add entries to the TAF loc file.
- [ ] **External mod:** Add a Harmony Postfix on **UiM.ApplyCampaignWindowModifications** (or **ApplyDockyardModifications**) and call your **Initialize()** there; use TAF's **ModUtils** and optionally **TAFUI**.

---

## Conventions Used in the Codebase

- **`GetChild(name, true)`** — game/Il2Cpp extension to get a child by name; second parameter `true` includes inactive children. Use when the target might be disabled.
- **`TryDestroyComponent<T>()`** — safely destroy a component (e.g. **LocalizeText**, **CampaignFleetWindow**) so cloned UI does not run the original game logic.
- **`UiVisible(bool)`** — used in some patches to show/hide UI without destroying it; prefer **SetActive** for your own objects unless the game expects **UiVisible**.

---

## Reference: Key Files

| File | Purpose |
|-----|--------|
| `Modified/UiM.cs` | **ApplyUiModifications**, **ApplyCampaignWindowModifications**, **ApplyDockyardModifications**, **ModifyUi**, **UpdateModifications**, **InstanciateUI**, **SetLocalizedTextTag**, **AddTooltip** |
| `Data/TAFUI.cs` | **TAF_InputField**, **TAF_Text**, **TAF_Button** |
| `Utils/ModUtils.cs` | **GetChildAtPath** |
| `Harmony/Ui.cs` | **Ui.Update** postfix → **UiM.UpdateModifications()** |
| `Harmony/GameData.cs` | **GameData.PostProcessAll** postfix → **UiM.ApplyUiModifications()** |

---

*This guide reflects the structure and patterns in the TweaksAndFixes codebase as of the current revision. For game-specific types (e.g. `CampaignFleetWindow`, `Ui`, `G.ui`), refer to the game's assemblies or Il2Cpp dumps.*
