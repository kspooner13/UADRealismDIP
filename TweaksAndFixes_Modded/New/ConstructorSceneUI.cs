using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Il2Cpp;
using MelonLoader;
using TweaksAndFixes.Data;

namespace TweaksAndFixes
{
    /// <summary>
    /// New UI for the constructor (dockyard) scene.
    /// Builds a custom panel in the left scroll area using TAFUI components and game templates.
    /// Hook from UiM.ApplyDockyardModifications() so it appears when the player is in the dockyard.
    /// </summary>
    public static class ConstructorSceneUI
    {
        private static bool _created;
        private static GameObject _panelRoot;
        private static TAFUI.TAF_Text _headerText;
        private static TAFUI.TAF_Text _defenseByAreaText;
        private static TAFUI.TAF_Text _blockedPenetrationText;
        private static TAFUI.TAF_Button _actionButton;

        /// <summary>
        /// Path to the left panel scroll content. New sections are added as children here.
        /// </summary>
        public const string LeftPanelContentPath =
            "Global/Ui/UiMain/Constructor/Left/Scroll View/Viewport/Cont";

        /// <summary>
        /// Create and attach the new constructor UI panel. Safe to call multiple times; only runs once.
        /// </summary>
        public static void Create()
        {
            if (_created)
                return;

            GameObject cont = ModUtils.GetChildAtPath(LeftPanelContentPath);
            if (cont == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[ConstructorSceneUI] Left panel content not found. Skipping.");
                return;
            }

            _panelRoot = CreatePanelRoot(cont);
            _headerText = CreateHeader(_panelRoot);
            _defenseByAreaText = CreateDefenseByAreaRow(_panelRoot);
            _blockedPenetrationText = CreateBlockedPenetrationRow(_panelRoot);
            _actionButton = CreateActionButton(_panelRoot);

            _created = true;
            Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSceneUI] Panel created.");
        }

        /// <summary>
        /// Destroy the panel and allow Create() to run again (e.g. for hot reload).
        /// </summary>
        public static void Destroy()
        {
            if (_panelRoot != null)
            {
                UnityEngine.Object.Destroy(_panelRoot);
                _panelRoot = null;
            }

            _headerText = null;
            _defenseByAreaText = null;
            _blockedPenetrationText = null;
            _actionButton = null;
            _created = false;
        }

        private static GameObject CreatePanelRoot(GameObject parent)
        {
            var go = new GameObject("TAF_ConstructorPanel");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -230f);
            rt.offsetMax = new Vector2(0f, 0f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 220f;
            le.flexibleWidth = 1f;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            var pad = vlg.padding;
            pad.left = 8;
            pad.right = 8;
            pad.top = 8;
            pad.bottom = 8;
            vlg.padding = pad;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            return go;
        }

        private static TAFUI.TAF_Text CreateHeader(GameObject parent)
        {
            var tafText = new TAFUI.TAF_Text(
                parent,
                "TAF_Header",
                "TAF — Constructor",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            );
            if (tafText.root.TryGetComponent<LayoutElement>(out var le))
                le.preferredHeight = 24f;
            else
                tafText.root.AddComponent<LayoutElement>().preferredHeight = 24f;
            return tafText;
        }

        private static TAFUI.TAF_Text CreateDefenseByAreaRow(GameObject parent)
        {
            var tafText = new TAFUI.TAF_Text(
                parent,
                "TAF_DefenseByArea",
                "Defense (armor mm): —",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            );
            if (tafText.root.TryGetComponent<LayoutElement>(out var le))
                le.preferredHeight = 60f;
            else
                tafText.root.AddComponent<LayoutElement>().preferredHeight = 60f;
            return tafText;
        }

        private static TAFUI.TAF_Text CreateBlockedPenetrationRow(GameObject parent)
        {
            var tafText = new TAFUI.TAF_Text(
                parent,
                "TAF_BlockedPenetration",
                "Blocked (last battle): —",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            );
            if (tafText.root.TryGetComponent<LayoutElement>(out var le))
                le.preferredHeight = 22f;
            else
                tafText.root.AddComponent<LayoutElement>().preferredHeight = 22f;
            return tafText;
        }

        private static void UpdateDefenseByAreaDisplay()
        {
            if (_defenseByAreaText?.textComp == null) return;
            Ship ship = ShipM.GetActiveShip();
            if (ship == null || ship.armor == null)
            {
                _defenseByAreaText.textComp.text = "Defense (armor mm): —";
                return;
            }
            var sb = new StringBuilder("Defense (armor mm):\n");
            float b = ModUtils.ArmorValue(ship.armor, Ship.A.Belt);
            float bBow = ModUtils.ArmorValue(ship.armor, Ship.A.BeltBow);
            float bStern = ModUtils.ArmorValue(ship.armor, Ship.A.BeltStern);
            float d = ModUtils.ArmorValue(ship.armor, Ship.A.Deck);
            float dBow = ModUtils.ArmorValue(ship.armor, Ship.A.DeckBow);
            float dStern = ModUtils.ArmorValue(ship.armor, Ship.A.DeckStern);
            float trSide = ModUtils.ArmorValue(ship.armor, Ship.A.TurretSide);
            float trTop = ModUtils.ArmorValue(ship.armor, Ship.A.TurretTop);
            float barb = ModUtils.ArmorValue(ship.armor, Ship.A.Barbette);
            float ct = ModUtils.ArmorValue(ship.armor, Ship.A.ConningTower);
            float super = ModUtils.ArmorValue(ship.armor, Ship.A.Superstructure);
            sb.Append($"Belt {b:F0} / Bow {bBow:F0} / Stern {bStern:F0}\n");
            sb.Append($"Deck {d:F0} / Bow {dBow:F0} / Stern {dStern:F0}\n");
            sb.Append($"Turret {trSide:F0}/{trTop:F0} | Barb {barb:F0} | CT {ct:F0} | Super {super:F0}");
            _defenseByAreaText.textComp.text = sb.ToString();
        }

        private static void UpdateBlockedPenetrationDisplay()
        {
            if (_blockedPenetrationText?.textComp == null) return;
            if (LastBattleBlockedPenetrationData.HasData)
            {
                int c = LastBattleBlockedPenetrationData.BlockedCount;
                float v = LastBattleBlockedPenetrationData.BlockedPenetrationValue;
                _blockedPenetrationText.textComp.text = $"Blocked (last battle): {c} hits, {v:F0} penetration";
            }
            else
                _blockedPenetrationText.textComp.text = "Blocked (last battle): —";
        }

        private static TAFUI.TAF_Button CreateActionButton(GameObject parent)
        {
            var btn = new TAFUI.TAF_Button(
                parent,
                "TAF_Action",
                "Do action",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            );
            if (!btn.root.TryGetComponent<LayoutElement>(out _))
                btn.root.AddComponent<LayoutElement>().preferredHeight = 36f;
            btn.SetOnClick(OnActionClick);
            return btn;
        }

        private static void OnActionClick()
        {
            Melon<TweaksAndFixes>.Logger.Msg("[ConstructorSceneUI] Action clicked.");
            // Example: open a popup, run logic, or refresh ship info
            if (_headerText != null)
                _headerText.textComp.text = "TAF — Clicked!";
        }

        /// <summary>
        /// Update panel visibility or content each frame while in constructor. Call from ModifyUi(Constructor).SetOnUpdate if needed.
        /// </summary>
        public static void OnUpdate(GameObject constructorRoot)
        {
            if (_panelRoot == null) return;
            UpdateDefenseByAreaDisplay();
            UpdateBlockedPenetrationDisplay();
        }
    }
}
