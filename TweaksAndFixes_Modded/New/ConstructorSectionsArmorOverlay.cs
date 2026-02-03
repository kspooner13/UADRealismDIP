using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Il2Cpp;
using MelonLoader;
using TweaksAndFixes.Data;
using Il2CppSystem;

namespace TweaksAndFixes
{
    /// <summary>
    /// Armor protection overlay that shows armor values alongside the ship sections view.
    /// Uses the game's existing Sections camera render and adds an armor info panel.
    /// </summary>
    public static class ConstructorSectionsArmorOverlay
    {
        private const float ReferencePenetrationMm = 250f;
        private const int PreviewWidth = 1024;
        private const int PreviewHeight = 512;
        private static GameObject _foldRoot;
        private static GameObject _overlayRoot;
        private static RawImage _shipImageRawImage;
        private static Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Guid, Texture2D> _previewCache;
        private static GameObject _previewCameraGo;
        private static Camera _previewCamera;
        private static RenderTexture _previewRenderTexture;
        private static bool? _lastTextureWasNull;
        private static bool _loggedRightContNull;
        private static bool _loggedFoldSectionsNotFound;
        private static bool _loggedSectionsInfoContNotFound;
        private static bool _loggedSectionsInfoContFound;
        private static float _lastGetShipImageNullLogTime = -999f;
        private static bool _loggedSectionsSideNullOrMissing;
        private static bool _loggedSectionsTopNullOrMissing;
        private static readonly Dictionary<Ship.A, Image> _zoneBoxes = new Dictionary<Ship.A, Image>();
        private static readonly Dictionary<Ship.A, Text> _zoneLabels = new Dictionary<Ship.A, Text>();

        // Zone definitions: (zone, label, anchorX, anchorY, width, height)
        // Positions are relative to overlay area: X=0 is left (stern), X=1 is right (bow), Y=0 is bottom, Y=1 is top
        // Ship typically renders in lower ~40% of frame in Sections camera view
        // These are tuned for the SectionsCameraSide render which shows ship from starboard
        private static readonly (Ship.A zone, string label, float anchorX, float anchorY, float width, float height)[] _zones = new (Ship.A, string, float, float, float, float)[]
        {
            // Belt armor - main side armor along waterline (ship's lower hull, near bottom of frame)
            (Ship.A.Belt,       "Belt",   0.50f, 0.18f, 0.32f, 0.10f),
            (Ship.A.BeltBow,    "Bow",    0.80f, 0.20f, 0.12f, 0.08f),
            (Ship.A.BeltStern,  "Stern",  0.20f, 0.20f, 0.12f, 0.08f),
            // Deck armor - top of hull (just above belt, still low in frame)
            (Ship.A.Deck,       "Deck",   0.50f, 0.28f, 0.28f, 0.05f),
            (Ship.A.DeckBow,    "D.Bow",  0.78f, 0.27f, 0.10f, 0.04f),
            (Ship.A.DeckStern,  "D.Stern",0.22f, 0.27f, 0.10f, 0.04f),
            // Superstructure elements - mid-ship (still in lower half of frame)
            (Ship.A.Barbette,   "Barb",   0.50f, 0.33f, 0.06f, 0.06f),
            (Ship.A.TurretSide, "Turret", 0.50f, 0.40f, 0.08f, 0.06f),
            (Ship.A.TurretTop,  "T.Top",  0.50f, 0.46f, 0.06f, 0.04f),
            (Ship.A.ConningTower,"CT",    0.58f, 0.38f, 0.05f, 0.08f),
            (Ship.A.Superstructure,"Super",0.50f,0.35f, 0.14f, 0.05f),
        };

        // Semi-transparent colors for zone overlays (more subtle than solid boxes)
        private static readonly Color Green = new Color(0.2f, 0.8f, 0.2f, 0.45f);
        private static readonly Color Yellow = new Color(0.95f, 0.8f, 0.1f, 0.45f);
        private static readonly Color Red = new Color(0.9f, 0.25f, 0.2f, 0.45f);
        private static readonly Color BoxOutline = new Color(0.1f, 0.1f, 0.1f, 0.6f);

        // Zone offset adjustment - can be tweaked to align with ship position in render
        private static float _zoneOffsetX = 0f;
        private static float _zoneOffsetY = 0f;
        private static float _zoneScaleX = 1f;
        private static float _zoneScaleY = 1f;

        /// <summary>
        /// Create a fold (clone of Right FoldSectionsInfo) on Right/Cont and attach overlay inside it. Call from constructor OnUpdate until created.
        /// Only runs when actually in the constructor (GameManager.IsConstructor) so Right Cont and FoldSectionsInfo are built.
        /// </summary>
        public static void EnsureOverlay(GameObject constructorRoot)
        {
            if (constructorRoot == null) return;
            if (!GameManager.IsConstructor) { Clear(); return; }
            if (_overlayRoot != null) { UpdateShipImageTexture(); UpdateValues(); return; }

            GameObject rightCont = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Right/Scroll View/Viewport/Cont");
            if (rightCont == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[ConstructorSectionsArmorOverlay] Right Cont not found.");
                return;
            }

            LogRightContHierarchy(rightCont);

            GameObject foldTemplate = null;
            for (int i = 0; i < rightCont.transform.childCount; i++)
            {
                var c = rightCont.transform.GetChild(i).gameObject;
                if (c.name == "FoldSectionsInfo") { foldTemplate = c; break; }
            }
            // Create preview camera first so we can calculate zone offset
            _previewCache = new Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Guid, Texture2D>();
            CreatePreviewCameraIfNeeded();
            CalculateZoneOffset(); // Calculate where ship is in the render BEFORE creating overlay

            if (foldTemplate == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[ConstructorSectionsArmorOverlay] FoldSectionsInfo template not found, adding overlay without fold.");
                _overlayRoot = CreateOverlay(rightCont);
            }
            else
            {
                _foldRoot = GameObject.Instantiate(foldTemplate);
                _foldRoot.name = "TAF_FoldArmorZones";
                _foldRoot.transform.SetParent(rightCont.transform, false);
                _foldRoot.transform.SetAsLastSibling();
                _foldRoot.SetActive(true);

                GameObject content = FindChildRecursive(_foldRoot, "SectionsInfoCont");
                if (content == null) content = _foldRoot;
                else
                {
                    for (int i = content.transform.childCount - 1; i >= 0; i--)
                        UnityEngine.Object.Destroy(content.transform.GetChild(i).gameObject);
                }

                _overlayRoot = CreateOverlay(content);
            }

            if (_overlayRoot != null)
            {
                UpdateShipImageTexture();
                Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] Sections-fold clone (armor zones) attached to right Cont.");
                UpdateValues();
            }
        }
        public static void DebugSections()
        {
           GameObject t = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Right/Scroll View/Viewport/Cont/FoldSectionsInfo/SectionsInfoCont");
            if (t != null)
                Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] DebugSections: texture (not null): {t}");
            else
                Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] DebugSections: texture is null.");

            
        }
        /// <summary>
        /// Calculate where the ship appears in the render and adjust zone offsets accordingly.
        /// Uses the game's Sections camera if available, otherwise our preview camera setup.
        /// </summary>
        private static void CalculateZoneOffset()
        {
            Ship ship = ShipM.GetActiveShip();
            if (ship == null)
            {
                Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] CalculateZoneOffset: No ship, using defaults");
                return;
            }

            // Get ship bounds first
            GameObject root = ship.gameObject;
            Bounds bounds = GetShipBounds(root);
            if (bounds.size.sqrMagnitude < 0.01f && ship.hull != null)
                bounds = GetShipBounds(ship.hull.gameObject);
            if (bounds.size.sqrMagnitude < 0.01f)
            {
                Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] CalculateZoneOffset: No ship bounds, using defaults");
                return;
            }

            // Try to use game's Sections camera for accurate positioning
            Camera cam = FindSectionsCamera();
            if (cam == null) cam = _previewCamera;

            if (cam == null)
            {
                // No camera yet - use reasonable defaults based on typical Sections camera setup
                // The ship usually appears centered horizontally, in lower 40% vertically
                _zoneOffsetY = -0.15f; // Ship is lower than center
                Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] CalculateZoneOffset: No camera, using default offset Y=-0.15");
                return;
            }

            // Calculate where ship center appears in viewport (0-1)
            Vector3 shipCenter = bounds.center;
            Vector3 viewportCenter = cam.WorldToViewportPoint(shipCenter);

            // Check if the point is actually in front of camera (z > 0)
            if (viewportCenter.z <= 0)
            {
                _zoneOffsetY = -0.15f;
                Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] CalculateZoneOffset: Ship behind camera, using default offset");
                return;
            }

            // Calculate where ship bounds edges appear
            Vector3 shipMin = bounds.min;
            Vector3 shipMax = bounds.max;

            Vector3 viewportMin = cam.WorldToViewportPoint(new Vector3(shipCenter.x, shipMin.y, shipMin.z));
            Vector3 viewportMax = cam.WorldToViewportPoint(new Vector3(shipCenter.x, shipMax.y, shipMax.z));

            // Calculate offset from assumed center (0.5, 0.5)
            _zoneOffsetX = viewportCenter.x - 0.5f;
            _zoneOffsetY = viewportCenter.y - 0.5f;

            // Calculate scale based on how much of viewport the ship occupies
            float shipViewportWidth = Mathf.Abs(viewportMax.x - viewportMin.x);
            float shipViewportHeight = Mathf.Abs(viewportMax.y - viewportMin.y);

            // Our zones assume ship fills about 70% of the frame
            if (shipViewportWidth > 0.1f)
                _zoneScaleX = shipViewportWidth / 0.7f;
            if (shipViewportHeight > 0.1f)
                _zoneScaleY = shipViewportHeight / 0.7f;

            // Clamp to reasonable values
            _zoneOffsetX = Mathf.Clamp(_zoneOffsetX, -0.3f, 0.3f);
            _zoneOffsetY = Mathf.Clamp(_zoneOffsetY, -0.4f, 0.2f);
            _zoneScaleX = Mathf.Clamp(_zoneScaleX, 0.5f, 1.5f);
            _zoneScaleY = Mathf.Clamp(_zoneScaleY, 0.5f, 1.5f);

            Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] Zone offset: X={_zoneOffsetX:F2}, Y={_zoneOffsetY:F2}, scaleX={_zoneScaleX:F2}, scaleY={_zoneScaleY:F2}");
            Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] Ship viewport center: ({viewportCenter.x:F2}, {viewportCenter.y:F2})");
        }

        /// <summary>
        /// Create our own camera + RenderTexture to render the ship (like SectionsCameraSide). Used when copying the game's texture fails.
        /// </summary>
        private static void CreatePreviewCameraIfNeeded()
        {
            if (_previewCamera != null) return;
            _previewRenderTexture = new RenderTexture(PreviewWidth, PreviewHeight, 16, RenderTextureFormat.ARGB32);
            _previewCameraGo = new GameObject("TAF_SectionsPreviewCamera");
            _previewCamera = _previewCameraGo.AddComponent<Camera>();
            _previewCamera.enabled = false;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.2f, 0.25f, 0.35f, 1f);
            _previewCamera.orthographic = true;
            _previewCamera.orthographicSize = 1f;
            _previewCamera.aspect = (float)PreviewWidth / PreviewHeight;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 500f;
            _previewCamera.targetTexture = _previewRenderTexture;
            _previewCamera.cullingMask = -1;
        }

        /// <summary>
        /// Render the active constructor ship from the side into our RenderTexture (same idea as SectionsCameraSide).
        /// Returns our RenderTexture if successful, else null.
        /// </summary>
        private static RenderTexture RenderShipToTexture()
        {
            if (_previewCamera == null || _previewRenderTexture == null) return null;
            Ship ship = ShipM.GetActiveShip();
            if (ship == null) return null;

            // Try multiple sources for the ship model
            GameObject root = ship.gameObject;
            Bounds bounds = default;
            bool hasBounds = false;

            // Try ship.gameObject
            if (root != null && root.activeInHierarchy)
            {
                bounds = GetShipBounds(root);
                hasBounds = bounds.size.sqrMagnitude > 0.01f;
            }

            // Try ship.hull
            if (!hasBounds && ship.hull != null && ship.hull.gameObject != null)
            {
                bounds = GetShipBounds(ship.hull.gameObject);
                hasBounds = bounds.size.sqrMagnitude > 0.01f;
            }

            // Try root transform
            if (!hasBounds && root != null && root.transform.root != null)
            {
                bounds = GetShipBounds(root.transform.root.gameObject);
                hasBounds = bounds.size.sqrMagnitude > 0.01f;
            }

            if (!hasBounds) return null;

            // Position camera to view ship from the side (starboard side, looking at port)
            // Ship typically has Z as length, X as beam, Y as height
            float shipLength = bounds.size.z;
            float shipHeight = bounds.size.y;
            float shipBeam = bounds.size.x;

            // Camera looks from +X toward -X (starboard to port), showing the ship's length (Z) and height (Y)
            float viewHeight = Mathf.Max(shipHeight, shipLength * 0.5f) * 1.1f; // Add 10% margin
            float cameraDistance = shipBeam + 10f; // Far enough to see whole ship

            _previewCamera.transform.position = new Vector3(
                bounds.center.x + cameraDistance,
                bounds.center.y,
                bounds.center.z
            );
            _previewCamera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            _previewCamera.orthographicSize = viewHeight;
            _previewCamera.nearClipPlane = 1f;
            _previewCamera.farClipPlane = cameraDistance * 2f + shipBeam;
            _previewCamera.targetTexture = _previewRenderTexture;
            _previewCamera.Render();
            return _previewRenderTexture;
        }

        private static Bounds GetShipBounds(GameObject root)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            if (root == null) return bounds;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>
        /// Log the children of Right/Scroll View/Viewport/Cont so we can find "Sections" and see how the ship image is set up.
        /// </summary>
        private static void LogRightContHierarchy(GameObject rightCont)
        {
            if (rightCont == null) return;
            Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] === Right/Scroll View/Viewport/Cont children ===");
            int n = rightCont.transform.childCount;
            Melon<TweaksAndFixes>.Logger.Msg($"  Direct children count: {n}");
            for (int i = 0; i < n; i++)
            {
                var child = rightCont.transform.GetChild(i).gameObject;
                bool active = child.activeSelf;
                var raw = child.GetComponent<RawImage>();
                var img = child.GetComponent<Image>();
                string comps = "";
                if (raw != null) comps += " RawImage";
                if (img != null) comps += " Image";
                if (raw != null && raw.texture != null) comps += $" tex={raw.texture.name ?? "?"} {raw.texture.width}x{raw.texture.height}";
                if (img != null && img.sprite != null && img.sprite.texture != null) comps += $" sprite tex={img.sprite.texture.width}x{img.sprite.texture.height}";
                Melon<TweaksAndFixes>.Logger.Msg($"  [{i}] '{child.name}' active={active}{comps}");
                int sub = child.transform.childCount;
                for (int j = 0; j < sub; j++)
                {
                    var subChild = child.transform.GetChild(j).gameObject;
                    var subRaw = subChild.GetComponent<RawImage>();
                    var subImg = subChild.GetComponent<Image>();
                    string subComps = "";
                    if (subRaw != null) subComps += " RawImage";
                    if (subImg != null) subComps += " Image";
                    if (subRaw != null && subRaw.texture != null) subComps += $" tex={subRaw.texture.name ?? "?"} {subRaw.texture.width}x{subRaw.texture.height}";
                    if (subImg != null && subImg.sprite != null && subImg.sprite.texture != null) subComps += $" sprite tex={subImg.sprite.texture.width}x{subImg.sprite.texture.height}";
                    Melon<TweaksAndFixes>.Logger.Msg($"      -> '{subChild.name}' active={subChild.activeSelf}{subComps}");
                    int sub2 = subChild.transform.childCount;
                    for (int k = 0; k < sub2 && k < 8; k++)
                    {
                        var sub2Child = subChild.transform.GetChild(k).gameObject;
                        Melon<TweaksAndFixes>.Logger.Msg($"          -> '{sub2Child.name}'");
                    }
                    if (sub2 > 8) Melon<TweaksAndFixes>.Logger.Msg($"          ... +{sub2 - 8} more");
                }
            }
            Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] === end Right Cont hierarchy ===");

            // Deep dump of FoldSectionsInfo -> SectionsInfoCont -> SectionsInfo to see how the ship image is built
            GameObject foldSections = null;
            for (int i = 0; i < rightCont.transform.childCount; i++)
            {
                var c = rightCont.transform.GetChild(i).gameObject;
                if (c.name == "FoldSectionsInfo") { foldSections = c; break; }
            }
            if (foldSections != null)
                LogSectionsInfoImageStructure(foldSections);
        }

        private static GameObject FindChildRecursive(GameObject root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var found = FindChildRecursive(root.transform.GetChild(i).gameObject, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Recursively log FoldSectionsInfo subtree with RawImage/Image/texture at each level to see how the ship image is built.
        /// </summary>
        private static void LogSectionsInfoImageStructure(GameObject root, int depth = 0)
        {
            if (root == null || depth > 6) return;
            string indent = new string(' ', depth * 2);
            var raw = root.GetComponent<RawImage>();
            var img = root.GetComponent<Image>();
            string comps = "";
            if (raw != null) comps += " RawImage";
            if (img != null) comps += " Image";
            if (raw != null && raw.texture != null) comps += $" tex={raw.texture.name ?? "?"} {raw.texture.width}x{raw.texture.height}";
            if (img != null && img.sprite != null && img.sprite.texture != null) comps += $" sprite {img.sprite.texture.width}x{img.sprite.texture.height}";
            Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] SectionsTree {indent}'{root.name}' active={root.activeSelf}{comps}");
            for (int i = 0; i < root.transform.childCount; i++)
                LogSectionsInfoImageStructure(root.transform.GetChild(i).gameObject, depth + 1);
        }

        /// <summary>
        /// Get the ship image: first from FoldSectionsInfo (same as Sections UI), then any sibling RawImage/Image, else GetShipPreviewTexGeneric.
        /// </summary>
        private static Texture GetShipImageTexture(GameObject rightCont)
        {
            if (rightCont == null)
            {
                if (!_loggedRightContNull) { _loggedRightContNull = true; Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] GetShipImageTexture: rightCont is null."); }
                return null;
            }

            // 1) Reference RawImage specifically from SectionsInfoCont (FoldSectionsInfo -> SectionsInfoCont -> ... -> SectionsSide/SectionsTop)
            GameObject foldSections = null;
            for (int i = 0; i < rightCont.transform.childCount; i++)
            {
                var c = rightCont.transform.GetChild(i).gameObject;
                if (c.name == "FoldSectionsInfo") { foldSections = c; break; }
            }
            if (foldSections == null)
            {
                if (!_loggedFoldSectionsNotFound) { _loggedFoldSectionsNotFound = true; Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] GetShipImageTexture: FoldSectionsInfo not found."); }
            }
            else
            {
                GameObject sectionsInfoCont = FindChildRecursive(foldSections, "SectionsInfoCont");
                if (sectionsInfoCont == null)
                {
                    if (!_loggedSectionsInfoContNotFound) { _loggedSectionsInfoContNotFound = true; Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsInfoCont not found under FoldSectionsInfo."); }
                }
                else
                {
                    if (!_loggedSectionsInfoContFound) { _loggedSectionsInfoContFound = true; Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsInfoCont found."); }
                    RawImage sectionsSide = FindChildRecursive(sectionsInfoCont, "SectionsSide")?.GetComponent<RawImage>();
                    if (sectionsSide != null)
                    {
                        if (sectionsSide.texture != null && sectionsSide.texture.width >= 512)
                        {
                            Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsSide RawImage texture OK (not null) ({sectionsSide.texture.name} {sectionsSide.texture.width}x{sectionsSide.texture.height}).");
                            return sectionsSide.texture;
                        }
                        if (!_loggedSectionsSideNullOrMissing) { _loggedSectionsSideNullOrMissing = true; Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsSide RawImage texture null or too small ({(sectionsSide.texture != null ? sectionsSide.texture.width + "x" + sectionsSide.texture.height : "null")})."); }
                    }
                    else if (!_loggedSectionsSideNullOrMissing) { _loggedSectionsSideNullOrMissing = true; Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsSide not found."); }
                    RawImage sectionsTop = FindChildRecursive(sectionsInfoCont, "SectionsTop")?.GetComponent<RawImage>();
                    if (sectionsTop != null)
                    {
                        if (sectionsTop.texture != null && sectionsTop.texture.width >= 512)
                        {
                            Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsTop RawImage texture OK (not null) ({sectionsTop.texture.name} {sectionsTop.texture.width}x{sectionsTop.texture.height}).");
                            return sectionsTop.texture;
                        }
                        if (!_loggedSectionsTopNullOrMissing) { _loggedSectionsTopNullOrMissing = true; Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsTop RawImage texture null or too small ({(sectionsTop.texture != null ? sectionsTop.texture.width + "x" + sectionsTop.texture.height : "null")})."); }
                    }
                    else if (!_loggedSectionsTopNullOrMissing) { _loggedSectionsTopNullOrMissing = true; Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] GetShipImageTexture: SectionsTop not found."); }
                    foreach (var raw in sectionsInfoCont.GetComponentsInChildren<RawImage>(true))
                    {
                        if (raw.texture != null && raw.texture.width >= 512 && raw.texture.height >= 256)
                        {
                            Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] GetShipImageTexture: RawImage under SectionsInfoCont OK ({raw.texture.name} {raw.texture.width}x{raw.texture.height}).");
                            return raw.texture;
                        }
                    }
                }
            }

            // 2) Fallback: any other sibling (excluding our fold) with a RawImage/Image
            Transform foldTransform = _foldRoot != null ? _foldRoot.transform : (_overlayRoot != null ? _overlayRoot.transform : null);
            for (int i = 0; i < rightCont.transform.childCount; i++)
            {
                Transform child = rightCont.transform.GetChild(i);
                if (child == foldTransform) continue;
                RawImage raw = child.GetComponentInChildren<RawImage>(true);
                if (raw != null && raw.texture != null && raw.texture.width > 0 && raw.texture.height > 0)
                    return raw.texture;
                foreach (var img in child.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite != null && img.sprite.texture != null && img.sprite.texture.width > 0 && img.sprite.texture.height > 0)
                        return img.sprite.texture;
                }
            }

            // 2) Use game's ship preview API (same as design list / Sections-style preview)
            Ship ship = ShipM.GetActiveShip();
            if (ship == null || G.ui == null) return null;
            try
            {
                if (_previewCache == null) _previewCache = new Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Guid, Texture2D>();
                Texture2D tex = G.ui.GetShipPreviewTexGeneric(ship, _previewCache, null, null, false);
                if (tex != null && tex.width > 0 && tex.height > 0) return tex;
            }
            catch (System.Exception e)
            {
                Melon<TweaksAndFixes>.Logger.Warning($"[ConstructorSectionsArmorOverlay] GetShipPreviewTexGeneric: {e.Message}");
            }
            if (UnityEngine.Time.unscaledTime - _lastGetShipImageNullLogTime >= 5f)
            {
                _lastGetShipImageNullLogTime = UnityEngine.Time.unscaledTime;
                Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] GetShipImageTexture: no texture found (fallbacks exhausted), returning null.");
            }
            return null;
        }

        private static void UpdateShipImageTexture()
        {
            if (_shipImageRawImage == null) return;
            Texture t = null;

            // Strategy 1: Get texture from game's SectionsSide/SectionsTop RawImages (best quality, matches Sections UI)
            t = GetGameSectionsTexture();

            // Strategy 2: If game's RawImages don't have texture, try forcing Sections camera to render
            if (t == null)
            {
                Camera sectionsCam = FindSectionsCamera();
                if (sectionsCam != null && sectionsCam.targetTexture != null)
                {
                    // Force the camera to render even if disabled
                    if (!sectionsCam.enabled)
                    {
                        sectionsCam.Render();
                    }
                    t = sectionsCam.targetTexture;
                }
            }

            // Strategy 3: Try to expand the Sections fold so its content initializes
            if (t == null)
            {
                GameObject rightCont = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Right/Scroll View/Viewport/Cont");
                TryExpandSectionsFoldSoTextureIsSet(rightCont);
                t = GetGameSectionsTexture(); // Try again after expanding
            }

            // Strategy 4: Use our own camera to render the ship
            if (t == null)
            {
                RenderTexture ourTex = RenderShipToTexture();
                if (ourTex != null)
                    t = ourTex;
            }

            // Strategy 5: Use game's generic ship preview API
            if (t == null)
            {
                Ship ship = ShipM.GetActiveShip();
                if (ship != null && G.ui != null)
                {
                    try
                    {
                        if (_previewCache == null)
                            _previewCache = new Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Guid, Texture2D>();
                        Texture2D tex = G.ui.GetShipPreviewTexGeneric(ship, _previewCache, null, null, false);
                        if (tex != null && tex.width > 0)
                            t = tex;
                    }
                    catch (System.Exception) { }
                }
            }

            _shipImageRawImage.texture = t;
            _shipImageRawImage.enabled = t != null;

            bool isNull = t == null;
            if (_lastTextureWasNull == null || _lastTextureWasNull != isNull)
            {
                _lastTextureWasNull = isNull;
                if (t != null)
                    Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] Texture acquired: {t.name ?? "unnamed"} {t.width}x{t.height}");
                else
                    Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSectionsArmorOverlay] No texture available");
            }
        }

        /// <summary>
        /// Tap into Il2Cpp.Fold: expand the Sections fold so the game builds the content and sets SectionsSide/SectionsTop texture.
        /// </summary>
        private static void TryExpandSectionsFoldSoTextureIsSet(GameObject rightCont)
        {
            if (rightCont == null) return;
            GameObject foldSections = null;
            for (int i = 0; i < rightCont.transform.childCount; i++)
            {
                var c = rightCont.transform.GetChild(i).gameObject;
                if (c.name == "FoldSectionsInfo") { foldSections = c; break; }
            }
            if (foldSections == null) return;
            GameObject foldGo = FindChildRecursive(foldSections, "Fold");
            if (foldGo == null) return;
            Component[] comps = foldGo.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component comp = comps[i];
                if (comp == null) continue;
                string fullName = comp.GetType().FullName ?? "";
                if (fullName.IndexOf("Fold", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    System.Type t = comp.GetType();
                    MethodInfo setExpanded = t.GetMethod("SetExpanded", BindingFlags.Public | BindingFlags.Instance, null, new System.Type[] { typeof(bool) }, null);
                    if (setExpanded != null) { setExpanded.Invoke(comp, new object[] { true }); return; }
                    PropertyInfo expanded = t.GetProperty("expanded", BindingFlags.Public | BindingFlags.Instance);
                    if (expanded != null && expanded.CanWrite) { expanded.SetValue(comp, true); return; }
                    FieldInfo expandedField = t.GetField("expanded", BindingFlags.Public | BindingFlags.Instance);
                    if (expandedField != null) { expandedField.SetValue(comp, true); return; }
                }
                catch (System.Exception) { }
            }
        }

        /// <summary>
        /// Cached reference to the game's SectionsSide RawImage (from the original Sections fold).
        /// We monitor this to copy its texture when available.
        /// </summary>
        private static RawImage _gameSectionsSideRawImage;
        private static RawImage _gameSectionsTopRawImage;
        private static bool _loggedCameraSearch;

        /// <summary>
        /// Find and cache reference to game's SectionsSide/SectionsTop RawImages.
        /// These have the actual rendered section textures when the Sections fold is expanded.
        /// </summary>
        private static void CacheGameSectionsRawImages()
        {
            if (_gameSectionsSideRawImage != null && _gameSectionsTopRawImage != null) return;

            GameObject foldSections = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Right/Scroll View/Viewport/Cont/FoldSectionsInfo");
            if (foldSections == null) return;

            GameObject sectionsSide = FindChildRecursive(foldSections, "SectionsSide");
            if (sectionsSide != null)
                _gameSectionsSideRawImage = sectionsSide.GetComponent<RawImage>();

            GameObject sectionsTop = FindChildRecursive(foldSections, "SectionsTop");
            if (sectionsTop != null)
                _gameSectionsTopRawImage = sectionsTop.GetComponent<RawImage>();
        }

        /// <summary>
        /// Get texture from game's Sections RawImages if available.
        /// </summary>
        private static Texture GetGameSectionsTexture()
        {
            CacheGameSectionsRawImages();

            if (_gameSectionsSideRawImage != null && _gameSectionsSideRawImage.texture != null)
                return _gameSectionsSideRawImage.texture;
            if (_gameSectionsTopRawImage != null && _gameSectionsTopRawImage.texture != null)
                return _gameSectionsTopRawImage.texture;

            return null;
        }

        private static Camera FindSectionsCamera()
        {
            // First try direct find (works if cameras exist in scene)
            GameObject go = GameObject.Find("SectionsCameraSide");
            if (go != null)
            {
                Camera c = go.GetComponent<Camera>();
                if (c != null && c.targetTexture != null && c.targetTexture.width >= 512)
                {
                    if (!_loggedCameraSearch)
                    {
                        _loggedCameraSearch = true;
                        Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] Found SectionsCameraSide: {c.targetTexture.width}x{c.targetTexture.height}, enabled={c.enabled}");
                    }
                    return c;
                }
            }

            go = GameObject.Find("SectionsCameraTop");
            if (go != null)
            {
                Camera c = go.GetComponent<Camera>();
                if (c != null && c.targetTexture != null && c.targetTexture.width >= 512)
                {
                    if (!_loggedCameraSearch)
                    {
                        _loggedCameraSearch = true;
                        Melon<TweaksAndFixes>.Logger.Msg($"[ConstructorSectionsArmorOverlay] Found SectionsCameraTop: {c.targetTexture.width}x{c.targetTexture.height}, enabled={c.enabled}");
                    }
                    return c;
                }
            }

            return null;
        }

        private static Color ColorForArmor(float armorMm)
        {
            if (armorMm <= 0) return Red;
            if (armorMm >= ReferencePenetrationMm) return Green;
            if (armorMm >= ReferencePenetrationMm * 0.6f) return Yellow;
            return Red;
        }

        private static Font GetGameFont()
        {
            var textGo = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Left/Scroll View/Viewport/Cont/FoldShipSettings/ShipSettings/ShipName/EditName/Static/Text");
            if (textGo != null)
            {
                var t = textGo.GetComponent<Text>();
                if (t != null && t.font != null) return t.font;
            }
            return null;
        }

        private static GameObject CreateOverlay(GameObject parent)
        {
            Font gameFont = GetGameFont();
            DebugSections();
            var overlay = new GameObject("TAF_ProtectionOverlay");
            overlay.transform.SetParent(parent.transform, false);
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();

            var rt = overlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var le = overlay.AddComponent<LayoutElement>();
            le.preferredHeight = 380f;
            le.flexibleWidth = 1f;

            var cg = overlay.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 1f;

            // Ship image background (same as Sections UI: existing RawImage texture or GetShipPreviewTexGeneric)
            var shipImageGo = new GameObject("TAF_ShipImage");
            shipImageGo.transform.SetParent(overlay.transform, false);
            shipImageGo.transform.SetAsFirstSibling();
            shipImageGo.SetActive(true);
            var shipImageRt = shipImageGo.AddComponent<RectTransform>();
            shipImageRt.anchorMin = Vector2.zero;
            shipImageRt.anchorMax = Vector2.one;
            shipImageRt.offsetMin = Vector2.zero;
            shipImageRt.offsetMax = Vector2.zero;
            _shipImageRawImage = shipImageGo.AddComponent<RawImage>();
            _shipImageRawImage.color = new Color(0.25f, 0.3f, 0.4f, 1f);
            _shipImageRawImage.raycastTarget = false;

            foreach (var (zone, label, ax, ay, w, h) in _zones)
            {
                // Apply offset and scale to zone position
                float adjX = (ax - 0.5f) * _zoneScaleX + 0.5f + _zoneOffsetX;
                float adjY = (ay - 0.5f) * _zoneScaleY + 0.5f + _zoneOffsetY;
                float adjW = w * _zoneScaleX;
                float adjH = h * _zoneScaleY;

                // Create zone box as a semi-transparent overlay region
                var boxGo = new GameObject($"Box_{zone}");
                boxGo.transform.SetParent(overlay.transform, false);
                boxGo.SetActive(true);

                var boxRt = boxGo.AddComponent<RectTransform>();
                // Use anchors to define the zone as a region on the ship image
                float halfW = adjW * 0.5f;
                float halfH = adjH * 0.5f;
                boxRt.anchorMin = new Vector2(adjX - halfW, adjY - halfH);
                boxRt.anchorMax = new Vector2(adjX + halfW, adjY + halfH);
                boxRt.offsetMin = Vector2.zero;
                boxRt.offsetMax = Vector2.zero;

                var img = boxGo.AddComponent<Image>();
                img.color = Red;
                img.raycastTarget = false;
                _zoneBoxes[zone] = img;

                // Add outline effect by creating a slightly larger background
                var outlineGo = new GameObject($"Outline_{zone}");
                outlineGo.transform.SetParent(overlay.transform, false);
                outlineGo.transform.SetSiblingIndex(boxGo.transform.GetSiblingIndex()); // Put outline behind box
                outlineGo.SetActive(true);
                var outlineRt = outlineGo.AddComponent<RectTransform>();
                outlineRt.anchorMin = new Vector2(adjX - halfW - 0.005f, adjY - halfH - 0.01f);
                outlineRt.anchorMax = new Vector2(adjX + halfW + 0.005f, adjY + halfH + 0.01f);
                outlineRt.offsetMin = Vector2.zero;
                outlineRt.offsetMax = Vector2.zero;
                var outlineImg = outlineGo.AddComponent<Image>();
                outlineImg.color = BoxOutline;
                outlineImg.raycastTarget = false;

                // Label showing armor value - positioned at center of zone
                var labelGo = new GameObject($"Label_{zone}");
                labelGo.transform.SetParent(overlay.transform, false);
                labelGo.SetActive(true);
                var labelRt = labelGo.AddComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(adjX, adjY);
                labelRt.anchorMax = new Vector2(adjX, adjY);
                labelRt.pivot = new Vector2(0.5f, 0.5f);
                labelRt.anchoredPosition = Vector2.zero;
                labelRt.sizeDelta = new Vector2(60f, 20f);

                var text = labelGo.AddComponent<Text>();
                if (gameFont != null) text.font = gameFont;
                text.fontSize = 11;
                text.alignment = TextAnchor.MiddleCenter;
                text.text = "—";
                text.color = Color.white;
                text.fontStyle = FontStyle.Bold;
                // Add shadow for readability
                var shadow = labelGo.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
                shadow.effectDistance = new Vector2(1f, -1f);
                _zoneLabels[zone] = text;
            }

            return overlay;
        }

        public static void UpdateValues()
        {
            if (_zoneBoxes.Count == 0) return;
            UpdateShipImageTexture();

            Ship ship = ShipM.GetActiveShip();
            if (ship == null || ship.armor == null)
            {
                foreach (var (zone, label, _, _, _, _) in _zones)
                {
                    if (_zoneBoxes.TryGetValue(zone, out var img)) img.color = Red;
                    if (_zoneLabels.TryGetValue(zone, out var t)) t.text = "—";
                }
                return;
            }

            foreach (var (zone, label, _, _, _, _) in _zones)
            {
                float mm = ModUtils.ArmorValue(ship.armor, zone);
                if (_zoneBoxes.TryGetValue(zone, out var img))
                    img.color = ColorForArmor(mm);
                if (_zoneLabels.TryGetValue(zone, out var text))
                    text.text = mm > 0f ? $"{mm:F0}" : "—";
            }
        }

        public static void Clear()
        {
            if (_foldRoot != null)
            {
                UnityEngine.Object.Destroy(_foldRoot);
                _foldRoot = null;
            }
            else if (_overlayRoot != null)
            {
                UnityEngine.Object.Destroy(_overlayRoot);
            }
            _overlayRoot = null;
            _shipImageRawImage = null;
            _previewCache = null;
            if (_previewCameraGo != null)
            {
                UnityEngine.Object.Destroy(_previewCameraGo);
                _previewCameraGo = null;
            }
            _previewCamera = null;
            if (_previewRenderTexture != null)
            {
                _previewRenderTexture.Release();
                _previewRenderTexture = null;
            }
            _lastTextureWasNull = null;
            _loggedRightContNull = false;
            _loggedFoldSectionsNotFound = false;
            _loggedSectionsInfoContNotFound = false;
            _loggedSectionsInfoContFound = false;
            _loggedSectionsSideNullOrMissing = false;
            _loggedSectionsTopNullOrMissing = false;
            _gameSectionsSideRawImage = null;
            _gameSectionsTopRawImage = null;
            _loggedCameraSearch = false;
            _zoneOffsetX = 0f;
            _zoneOffsetY = 0f;
            _zoneScaleX = 1f;
            _zoneScaleY = 1f;
            _zoneBoxes.Clear();
            _zoneLabels.Clear();
        }
    }
}
