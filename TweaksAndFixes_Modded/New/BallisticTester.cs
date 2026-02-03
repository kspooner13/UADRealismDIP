using UnityEngine;
using Il2Cpp;
using MelonLoader;
using TweaksAndFixes.Harmony;

namespace TweaksAndFixes
{
    public static class BallisticTester
    {
        // Pre-defined gun part to use for testing (by PartData name/ID)
        // Set to null or empty string to use manual values below
        // Example: "gun_12_x2", "gun_9_x1", etc.
        public static string TestGunPartName = "gun_12_x2";

        // Manual test values (used if TestGunPartName is empty or gun not found)
        // NOTE: TestCaliber is in INCHES (e.g. 12.0 = 12").
        // We convert to mm internally for penetration/armor math.
        public static float TestCaliber = 12.0f;
        public static float TestVelocity = 800f; // m/s
        public static string TestShellType = "ap";
        // Gun grade used for GunData lookups (penetration/velocity tables).
        // Set to -1 to try to infer from the target ship/part (best-effort), else use the exact grade.
        public static int TestGunGrade = 0;

        //HitChance Calc Test Variables
        public static float TestMulti= 0.0f;
        public static float TestDistance = 0.0f;
        public static float TestDeckHitPercent = 0.0f;

        //Pull Deck Hit Percent Min and Max from Config
        public static float TestMin = Config.Param("taf_shell_deck_hit_percent_min", 0f);
        public static float TestMax = Config.Param("taf_shell_deck_hit_percent_max", 1.2f);

        // Cached gun info from the pre-defined gun part
        private static GunInfo? _cachedGunInfo = null;

        private struct GunInfo
        {
            public float caliberInch;
            public float caliberMm;
            public float velocity;
            public int grade;
            public string shellType; // "ap" or "he"
            public string source; // "PartData:name" or "manual"
        }

        public static void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))
            {
                //ExecuteTestShot();
                
            }
        }

        private static void ExecuteTestShot()
        {
            if (G.cam == null || G.cam.cameraComp == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[BallisticTester] Camera is not available");
                return;
            }

            Ray ray = G.cam.cameraComp.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
            {
                if (hit.collider == null)
                {
                    Melon<TweaksAndFixes>.Logger.Warning("[BallisticTester] Raycast hit has no collider");
                    return;
                }

                // Load/cache gun info from pre-defined gun part if specified
                GunInfo? gunInfo = GetGunInfoFromPredefinedPart();

                Ship targetShip = hit.collider.GetComponentInParent<Ship>();
                if (targetShip != null)
                {
                    FireProjectile(targetShip, hit.point, ray.direction, gunInfo);
                }
                else
                {
                    Melon<TweaksAndFixes>.Logger.Warning("[BallisticTester] No Ship component found on hit object");
                }
            }
        }

        private static GunInfo? GetGunInfoFromPredefinedPart()
        {
            // If no gun part name specified, return null to use manual values
            if (string.IsNullOrEmpty(TestGunPartName))
                return null;

            // Return cached value if available
            if (_cachedGunInfo.HasValue)
                return _cachedGunInfo;

            // Try to find the PartData by name
            PartData? partData = null;
            if (G.GameData.parts != null && G.GameData.parts.TryGetValue(TestGunPartName, out var pd))
            {
                partData = pd;
            }

            if (partData == null || !partData.isGun)
            {
                Melon<TweaksAndFixes>.Logger.Warning($"[BallisticTester] Gun part '{TestGunPartName}' not found or is not a gun. Using manual values.");
                return null;
            }

            try
            {
                float caliberMm = partData.caliber; // Already in mm
                float caliberInch = caliberMm / 25.4f;

                // Get GunData for velocity/penetration
                GunData? gunData = null;
                try
                {
                    gunData = G.GameData.GunData(partData);
                }
                catch { }

                // Get velocity from GunData (use grade 0 as default, or TestGunGrade if set)
                float velocity = TestVelocity; // Default to manual value
                int grade = TestGunGrade >= 0 ? TestGunGrade : 0;
                
                if (gunData != null && gunData.shellVelocities != null && gunData.shellVelocities.ContainsKey(grade))
                {
                    velocity = gunData.shellVelocities[grade];
                }

                // Use manual shell type (could be enhanced to detect from part data)
                string shellType = TestShellType.ToLower();

                var gunInfo = new GunInfo
                {
                    caliberInch = caliberInch,
                    caliberMm = caliberMm,
                    velocity = velocity,
                    grade = grade,
                    shellType = shellType,
                    source = $"PartData:{TestGunPartName}"
                };

                // Cache it
                _cachedGunInfo = gunInfo;
                
                Melon<TweaksAndFixes>.Logger.Msg($"[BallisticTester] Using pre-defined gun: {TestGunPartName} | {caliberInch:0.#}\" ({caliberMm:0.#}mm), Grade {grade}, V: {velocity:0.#} m/s, Type: {shellType.ToUpper()}");
                
                return gunInfo;
            }
            catch (System.Exception ex)
            {
                Melon<TweaksAndFixes>.Logger.Warning($"[BallisticTester] Error extracting gun info from '{TestGunPartName}': {ex.Message}. Using manual values.");
                return null;
            }
        }

        private static void FireProjectile(Ship target, Vector3 impactPoint, Vector3 direction, GunInfo? gunInfo)
        {
            // Shells require a 'from' part and a shooter ship.
            if (target == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[BallisticTester] Target ship is null");
                return;
            }

            // Calculate armor effectiveness without visual effects
            // This avoids unsafe delegate marshaling and provides armor testing
            ApplyDirectHit(target, impactPoint, direction, gunInfo);
        }

        private static void ApplyDirectHit(Ship target, Vector3 impactPoint, Vector3 direction, GunInfo? gunInfo)
        {
            // Find the closest part to the impact point
            Part? hitPart = null;
            float closestDistance = float.MaxValue;
            const float maxPartDistance = 50f; // Max distance to consider a part hit

            foreach (var part in target.parts)
            {
                if (part == null || part.gameObject == null)
                    continue;

                // Use transform position as approximation
                float distance = Vector3.Distance(impactPoint, part.transform.position);
                
                if (distance < closestDistance && distance < maxPartDistance)
                {
                    closestDistance = distance;
                    hitPart = part;
                }
            }

            bool isHullHit = (hitPart == null || hitPart == target.hull);
            Ship.A armorZone = Ship.A.Belt;
            string hitLocation = "Hull";

            // If hitting hull directly, determine if it's belt or deck based on position
            if (isHullHit)
            {
                // Get ship's waterline/center position
                Vector3 shipCenter = target.transform.position;
                float impactY = impactPoint.y;
                float shipY = shipCenter.y;
                
                // Check if impact is above or below the ship's center (rough estimate)
                // Above center = likely deck hit, below = likely belt hit
                if (target.hullSize.size.y > 0)
                {
                    float hullTop = shipY + target.hullSize.size.y * 0.5f;
                    float hullBottom = shipY - target.hullSize.size.y * 0.5f;
                    float hullMid = (hullTop + hullBottom) * 0.5f;
                    
                    if (impactY > hullMid)
                    {
                        armorZone = Ship.A.Deck;
                        hitLocation = "Deck";
                    }
                    else
                    {
                        armorZone = Ship.A.Belt;
                        hitLocation = "Belt";
                    }
                }
                else
                {
                    // Fallback: use direction to guess
                    if (direction.y < -0.3f) // Coming from above
                    {
                        armorZone = Ship.A.Deck;
                        hitLocation = "Deck";
                    }
                    else
                    {
                        armorZone = Ship.A.Belt;
                        hitLocation = "Belt";
                    }
                }
                
                // Use hull as the "hit part" for calculations
                hitPart = target.hull;
            }

            if (hitPart == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[BallisticTester] No part or hull found");
                return;
            }

            // Use detected gun info if available, otherwise fall back to manual test values
            float caliberInch;
            float caliberMm;
            float velocity;
            int grade;
            bool isAP;
            string shellType;
            string gunSource;

            if (gunInfo.HasValue)
            {
                var detectedGun = gunInfo.Value;
                caliberInch = detectedGun.caliberInch;
                caliberMm = detectedGun.caliberMm;
                velocity = detectedGun.velocity;
                grade = detectedGun.grade;
                shellType = detectedGun.shellType;
                isAP = shellType.ToLower() == "ap";
                gunSource = detectedGun.source;
            }
            else
            {
                // Fall back to manual test values
                caliberInch = TestCaliber;
                caliberMm = caliberInch * 25.4f;
                velocity = TestVelocity;
                grade = TestGunGrade >= 0 ? TestGunGrade : 0;
                shellType = TestShellType;
                isAP = shellType.ToLower() == "ap";
                gunSource = "manual";
            }

            // Use improved armor-zone selection for non-hull parts.
            if (!isHullHit)
            {
                armorZone = GuessArmorZoneForPart(hitPart, direction);
                hitLocation = armorZone.ToString();
            }
            else
            {
                // Refine bow/stern zones for hull hits using local Z.
                armorZone = RefineHullZone(target, armorZone, impactPoint);
            }

            var result = CalculateDamageAndArmor(
                caliberInch,
                caliberMm,
                velocity,
                grade,
                isAP,
                hitPart,
                target,
                armorZone
            );

            // Log armor effectiveness analysis
            string partName = isHullHit ? hitLocation : (hitPart.data != null ? hitPart.data.name : "Unknown");
            string protectionStatus = result.penetrated ? "PENETRATED" : "BLOCKED";
            string armorStatus = result.armor > 0 ? $"Armor: {result.armor:F1}mm ({result.armorZoneUsed}) [{result.armorSource}]" : "No armor";
            
            string gunInfoStr = $"Gun: {caliberInch:0.#}\" ({caliberMm:0.#}mm) | V: {result.velocityUsed:0.#} m/s [{result.velocitySource}] | Grade: {result.gradeUsed} [{gunSource}]";
            string penInfo = $"Pen: {result.penetration:F1}mm [{result.penetrationSource}]";
            
            Melon<TweaksAndFixes>.Logger.Msg($"[BallisticTester] {shellType.ToUpper()} vs {partName}: {protectionStatus} | {gunInfoStr} | {armorStatus} | {penInfo} | Dmg: {result.damage:F1}");
            
            if (!result.penetrated && result.armor > 0)
            {
                Melon<TweaksAndFixes>.Logger.Msg($"[BallisticTester] ✓ Armor sufficient - {result.armor:F1}mm blocks {result.penetration:F1}mm penetration");
            }
            else if (result.penetrated)
            {
                Melon<TweaksAndFixes>.Logger.Msg($"[BallisticTester] ✗ Armor insufficient - {result.armor:F1}mm cannot block {result.penetration:F1}mm penetration");
            }
        }

        private struct ArmorTestResult
        {
            public float damage;
            public float armor;
            public float penetration;
            public bool penetrated;

            public int gradeUsed;
            public float velocityUsed;
            public Ship.A armorZoneUsed;

            public string penetrationSource;
            public string velocitySource;
            public string armorSource;
        }

        private static ArmorTestResult CalculateDamageAndArmor(
            float caliberInch,
            float caliberMm,
            float velocityInput,
            int testGunGrade,
            bool isAP,
            Part hitPart,
            Ship target,
            Ship.A armorZone = Ship.A.Belt)
        {
            if (target == null)
            {
                return new ArmorTestResult();
            }
            
            var result = new ArmorTestResult();
            
            // Try to get penetration from GunData if available
            float penetration = 0f;
            bool useGamePenetration = false;
            bool useGameVelocity = false;
            float velocity = velocityInput;
            
            // Try to find matching GunData for the test caliber
            int calInchKey = Mathf.RoundToInt(caliberInch);
            string calKey = calInchKey.ToString();
            
            if (G.GameData.guns != null && G.GameData.guns.TryGetValue(calKey, out var gunData))
            {
                // Get gun grade from ship (default to grade 0 if not available)
                int grade = testGunGrade >= 0 ? testGunGrade : 0;

                if (testGunGrade < 0 && hitPart != null && hitPart.data != null && target != null)
                {
                    try
                    {
                        grade = target.TechGunGrade(hitPart.data);
                    }
                    catch { }
                }

                result.gradeUsed = grade;
                
                // Try to get penetration from GunData.penetrations dictionary
                if (gunData.penetrations != null && gunData.penetrations.ContainsKey(grade))
                {
                    penetration = gunData.penetrations[grade];
                    useGamePenetration = true;
                }

                // Try to get velocity from GunData.shellVelocities dictionary (if present)
                if (gunData.shellVelocities != null && gunData.shellVelocities.ContainsKey(grade))
                {
                    velocity = gunData.shellVelocities[grade];
                    useGameVelocity = true;
                }
            }
            else
            {
                result.gradeUsed = testGunGrade >= 0 ? testGunGrade : 0;
            }
            
            // Fallback to calculated penetration if game data not available
            // Penetration formula: roughly caliber (in mm) * velocity factor
            // For AP: ~caliber * (velocity/1000) gives reasonable values
            // 12 inch (304.8mm) at 800 m/s should penetrate ~240-300mm
            if (!useGamePenetration)
            {
                if (isAP)
                {
                    // AP penetration: roughly caliber * velocity factor
                    // More realistic formula for AP shells
                    penetration = caliberMm * (velocity / 1000f) * 0.8f;
                }
                else
                {
                    // HE penetration is much lower
                    penetration = caliberMm * (velocity / 1000f) * 0.1f;
                }
            }
            
            result.penetration = penetration;
            result.penetrationSource = useGamePenetration ? "GunData.penetrations" : "fallback";
            result.velocityUsed = velocity;
            result.velocitySource = useGameVelocity ? "GunData.shellVelocities" : "input";
            
            // Get armor from Ship.armor dictionary for the specified zone
            float armor = 0f;
            Ship.A finalArmorZone = armorZone;
            
            // Try to get armor thickness from ship's armor dictionary for the specific zone
#pragma warning disable CS8602 // Nullable reference - we check for null
            var shipArmor = target.armor;
#pragma warning restore CS8602
            if (shipArmor != null)
            {
                // First try the specified zone
                if (shipArmor.TryGetValue(finalArmorZone, out float armorThickness) && armorThickness > 0)
                {
                    armor = armorThickness; // Already in mm
                    result.armorSource = "Ship.armor";
                }
                else
                {
                    // Fallback: check other zones
                    Ship.A[] zonesToCheck = { Ship.A.Belt, Ship.A.BeltBow, Ship.A.BeltStern, Ship.A.Deck, Ship.A.DeckBow, Ship.A.DeckStern, Ship.A.TurretSide, Ship.A.TurretTop, Ship.A.Barbette, Ship.A.ConningTower, Ship.A.Superstructure };
                    foreach (var zone in zonesToCheck)
                    {
                        if (shipArmor.TryGetValue(zone, out float armorThickness2) && armorThickness2 > 0)
                        {
                            armor = armorThickness2;
                            finalArmorZone = zone; // Update zone to match
                            result.armorSource = "Ship.armor(fallback)";
                            break;
                        }
                    }
                }
            }
            
            // Fallback: try to get from part data using reflection if no armor found
            // Also prefer per-part armor when available for non-hull parts, since Ship.armor is zone-level.
            if (hitPart != null)
            {
                var partData = hitPart.data;
                if (partData != null)
                {
                    var armorField = partData.GetType().GetField("armor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (armorField != null)
                    {
                        var armorValue = armorField.GetValue(partData);
                        if (armorValue != null && float.TryParse(armorValue.ToString(), out float a))
                        {
                            // Use part-specific armor if it exists and is non-zero; otherwise keep zone armor.
                            if (a > 0f)
                            {
                                armor = a;
                                result.armorSource = "PartData.armor";
                            }
                        }
                    }
                }
            }
            
            // Use game-based penetration vs armor comparison
            // Note: The game's actual penetration/armor calculations may include angle effects internally,
            // but those are not exposed as separate functions. We use the flat penetration value from GunData.
            result.armor = armor;
            result.penetrated = penetration > armor;
            result.armorZoneUsed = finalArmorZone;
            
            // Calculate damage (simplified - actual game damage calculation is more complex)
            float baseDamage = caliberMm * velocity * 0.01f;
            
            if (result.penetrated)
            {
                result.damage = baseDamage;
            }
            else
            {
                // Blocked - minimal damage
                result.damage = baseDamage * 0.1f * (penetration / Mathf.Max(armor, 1f));
            }

            return result;
        }

        private static Ship.A GuessArmorZoneForPart(Part part, Vector3 incomingDir)
        {
            if (part == null || part.data == null)
                return Ship.A.Belt;

            var d = part.data;

            // Hull should be handled elsewhere.
            if (d.isHull)
                return Ship.A.Belt;

            // Towers: main tower is effectively conning tower; secondary towers/funnels are superstructure.
            if (d.isTowerMain)
                return Ship.A.ConningTower;
            if (d.isTowerAny || d.isFunnel)
                return Ship.A.Superstructure;

            // Weapons:
            if (d.isGun)
            {
                bool isCasemate = false;
                try { isCasemate = Ship.IsCasemateGun(d); } catch { }
                if (isCasemate)
                    return Ship.A.Belt;

                // Turrets: top vs side based on impact direction (plunging vs side).
                return incomingDir.y < -0.35f ? Ship.A.TurretTop : Ship.A.TurretSide;
            }

            if (d.isTorpedo)
                return Ship.A.Superstructure;

            // Generic parts (funnels, aux, etc.) tend to be superstructure-level.
            return Ship.A.Superstructure;
        }

        private static Ship.A RefineHullZone(Ship ship, Ship.A baseZone, Vector3 impactPoint)
        {
            if (ship == null)
                return baseZone;

            // Determine bow/stern based on local Z position on the ship.
            Vector3 local = ship.transform.InverseTransformPoint(impactPoint);
            float z = local.z;

            // Use hullSize extents if available to scale thresholds.
            float extZ = Mathf.Max(1f, ship.hullSize.extents.z);
            float bowSternThreshold = extZ * 0.35f;

            if (z > bowSternThreshold)
                return baseZone == Ship.A.Deck ? Ship.A.DeckBow : Ship.A.BeltBow;
            if (z < -bowSternThreshold)
                return baseZone == Ship.A.Deck ? Ship.A.DeckStern : Ship.A.BeltStern;

            return baseZone;
        }

    }
}