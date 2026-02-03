# Which discovery items are usable to tap into

## Directly usable (no decompiling)

| What | How to use |
|------|------------|
| **FoldSectionsInfo** GameObject | Find under Right/Scroll View/Viewport/Cont. Entry point for the whole Sections fold. |
| **SectionsSide** RawImage | Path: `FoldSectionsInfo → SectionsInfoCont → SectionsInfo → SectionsPlan → SectionsPlanActual → SectionsSide`. Get `RawImage.texture` (RenderTexture **SectionsCameraSide** 1024×512). **Usable when non-null** (usually when Sections fold is expanded). |
| **SectionsTop** RawImage | Same path, sibling **SectionsTop**. `RawImage.texture` = **SectionsCameraTop** 1024×512. Same condition. |

We already use these in `GetShipImageTexture()`: we resolve SectionsSide/SectionsTop and use their texture. If you still see a grey box, the texture is likely null when our fold is visible (e.g. Sections fold is collapsed or cameras only render when that fold is open).

---

## Usable with Harmony / Il2Cpp (game types from Assembly-CSharp)

| Type | Use |
|------|-----|
| **Il2Cpp.Fold** | Component on **Fold** (child of FoldSectionsInfo). Drives expand/collapse and likely when content (and camera texture) is built. **Tap into:** Get component from `FoldSectionsInfo/Fold`, call something like `SetExpanded(true)` or set an “expanded” field so Sections content is active and texture is set; then read SectionsSide.texture. Or **Harmony-patch** a method on `Fold` that runs when the fold is expanded to copy the texture to our overlay. |
| **Il2Cpp.SectionInfo** | Likely on SectionsInfoCont or SectionsInfo. May hold references to camera, RawImages, or update logic. **Tap into:** Reflect or patch to get the RenderTexture reference or the camera that fills it. |

Search for **Il2Cpp.Fold** and **Il2Cpp.SectionInfo** in the game’s decompiled Assembly-CSharp (dnSpy/ilSpy) to find methods/properties (e.g. expand, content root, RawImage/camera references) and patch or call them.

---

## Not useful for Sections UI

- **Il2Cpp.Ship+Section**, **Il2Cpp.Ship+Section+Store** – ship section data, not UI.
- **Il2Cpp.Fold+__c__DisplayClass11_0/1** – compiler-generated closures inside Fold (e.g. callbacks). Not useful to call directly.
- **Il2CppSuimono.Core.SuimonoTrailSection** – water/trail.
- **Il2CppMessagePack.Formatters.Ship_Section_StoreFormatter** – serialization.
- **Il2CppSystem.***, **Il2CppSystem.Configuration.***, etc. – .NET / system libs, not game UI.

---

## Cameras

The discovery logged **no cameras** under “Cameras that feed Sections texture” – so either `Camera.allCamerasCount` was 0 or no camera had a `targetTexture`. The Sections cameras are probably **disabled** (only used when Sections is open) and not in `Camera.allCameras`. So we can’t reliably get the Camera instance at runtime; using **SectionsSide/SectionsTop RawImage.texture** (or forcing **Il2Cpp.Fold** to expand first) is the way to tap into the same image.
