# Sections UI Discovery – How to Find the Function That Builds FoldSectionsInfo

The mod runs a **full discovery** once when you open the constructor (when the armor overlay is created). It logs everything about the FoldSectionsInfo / Sections UI so you can see how it works and which game code drives it.

## 1. Get the log output

1. Start the game with the mod.
2. Open the **Constructor** (dockyard).
3. In the MelonLoader log (`Latest.log` in the game’s MelonLoader folder), search for:
   - `[SectionsUIDiscovery] ========== FULL FOLD SECTIONS INFO DISCOVERY ==========`
4. Read from there down to `========== END DISCOVERY ==========`.

## 2. What the discovery logs

- **Hierarchy + component types**  
  For every GameObject under **FoldSectionsInfo** it logs:
  - Name, active state.
  - Every **Component** on that GameObject with its **full type name** (e.g. `UnityEngine.UI.RawImage`, or the game’s Il2Cpp type like `Ui.ConstructorSectionsView`).
  - For Cameras: `targetTexture` name and size.
  - For RawImage/Image: texture/sprite size.

- **Cameras that render to a texture**  
  For each Camera with a non-null `targetTexture` it logs:
  - Camera name, enabled, depth.
  - `targetTexture` name and size (e.g. `SectionsCameraSide 1024x512`).
  - **GameObject path** (full hierarchy path).
  - All **Components** on that Camera’s GameObject (with type names).

- **Game types containing "Section" / "Fold"**  
  It scans loaded assemblies for type names containing `Section` or `Fold` and logs them.  
  Use these type names in the decompiled game code to find the class that manages the Sections UI.

## 3. How to find the exact function

1. **From the discovery log**
   - Note the **Component type names** on FoldSectionsInfo and its children (especially any **MonoBehaviour** that isn’t Unity built-in).
   - Note the **Camera GameObject path** and the **Component types** on that Camera’s GameObject.
   - Note any **game types** listed at the end (e.g. `SomeNamespace.ConstructorSectionsView`).

2. **Decompile the game**
   - Open the game’s managed DLL(s) in **dnSpy** or **ilSpy** (e.g. `Assembly-CSharp.dll`, or the Il2Cpp dump if the game is Il2CPP).
   - **Search** (Ctrl+Shift+K in dnSpy) for:
     - `SectionsCameraSide`
     - `SectionsCameraTop`
     - `FoldSectionsInfo`
     - `SectionsPlanActual`
     - `SectionsSide` / `SectionsTop`
     - Any of the **type names** you saw in the discovery log (e.g. the MonoBehaviour on FoldSectionsInfo or on the Camera’s GameObject).

3. **In the decompiled code**
   - Where those strings or types are used, you’ll see the **function** that:
     - Creates or parents the Sections UI.
     - Creates the camera and RenderTexture.
     - Assigns `camera.targetTexture` and `rawImage.texture`.
     - Updates when the ship changes.
   - That function (and the class it belongs to) is what “builds” the Sections UI and its image.

## 4. If the game is Il2CPP

- Managed type names might not show up in the discovery (native code).
- You can still use:
  - **Hierarchy and component types** (Unity components will show).
  - **Camera path and targetTexture name** to find the same objects in the game’s Il2Cpp dump or in a tool that shows native → managed mapping.
- Search the **game’s project or Il2Cpp metadata** for `SectionsCameraSide`, `FoldSectionsInfo`, `SectionsPlanActual` to find the corresponding native/script references.

Once you have the exact class and method name from the decompiled code, you can add a **Harmony patch** (Prefix/Postfix) to that method to hook into how the Sections image is built or to reuse the same camera/texture for the custom fold.
