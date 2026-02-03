# Gun Firing, Hit Chance, and Hit Location References

## Overview
This document lists the key game methods and classes related to gun firing calculations, hit chance, and hit location determination.

---

## 1. Hit Chance Calculation

### `Ship.HitChanceCalc` Class
**Location**: `Il2Cpp.Ship.HitChanceCalc`  
**Usage**: Used to calculate the probability of a shell hitting a target.

**Key Method**:
- `Add(float mult, string reason, string value)` - Accumulates hit chance multipliers
  - `mult`: Multiplier value (e.g., 0.8 for 80% accuracy)
  - `reason`: Description of the modifier (e.g., "Distance", "Stability", "Weather")
  - `value`: Specific value or condition

**Example Usage** (from `TweaksAndFixes\Harmony\Ship.cs`):
```csharp
[HarmonyPatch(typeof(Ship.HitChanceCalc))]
internal class Patch_Ship_HitChanceCalc
{
    internal static void Prefix(Ship.HitChanceCalc __instance, ref float mult, ref string reason, ref string value)
    {
        // Modify hit chance multipliers here
    }
}
```

**Related Methods**:
- `Ship.HitChanceTorpedoEst(Ship ally, Ship enemy, float rangeToEnemy, float torpedoRange)` - Estimates torpedo hit chance

---

## 2. Hit Location Calculation

### `Ship.GetSectionFromPositions(Vector3 position)`
**Location**: `Il2Cpp.Ship.GetSectionFromPositions`  
**Purpose**: Determines which section of the ship a hit occurs at (belt, deck, turret, etc.)

**Parameters**:
- `position`: The 3D world position where the hit occurs

**Returns**: Ship section identifier

**Deck Hit Calculation** (from `TweaksAndFixes\Harmony\Ship.cs:474-521`):
The game calculates deck hit probability based on:
- **Distance to Range Ratio**: `distance / range`
- **Gun Grade**: `ship.TechGunGrade(partData)`
- **Deck Percent Formula**:
  ```csharp
  float percentDeckModifier = distance / range;
  float deckPercent = ((max - min) * (percentDeckModifier * (mark / MaxGunGrade))) + min;
  ```
- **Y Position Adjustment**: `hullSize.min.y * deckPercent` (adjusts hit Y position for deck hits)

**Example Usage**:
```csharp
[HarmonyPatch(nameof(Ship.GetSectionFromPositions))]
internal static void Fix(Ship __instance, ref Vector3 tempPos)
{
    // Modify hit location calculation here
    // Access shell data via Patch_Shell.updating
}
```

---

## 3. Shell Creation and Firing

### `Shell.Create(...)`
**Location**: `Il2Cpp.Shell.Create`  
**Purpose**: Creates a shell projectile that will travel to the target

**Key Parameters** (from previous attempts):
- `Part from`: The gun part that fires the shell
- `Ship target`: The target ship
- `Ship.ShellType shellType`: AP or HE shell type
- `Ship.CalcHitPointMethod calcHitPoint`: Delegate to calculate where the shell will hit
- `Il2CppSystem.Func<Il2CppSystem.Nullable<Vector3>> targetPosition`: Function returning target position
- `Vector3 velocity`: Initial velocity vector

**Note**: This method requires Il2Cpp-compatible delegates, which is why we avoided using it in `BallisticTester`.

### `Shell` Class Properties
**Location**: `Il2Cpp.Shell`

**Key Properties**:
- `Shell.from` - The `Part` (gun) that fired this shell
- `Shell.willHitTarget` - Boolean indicating if shell will hit target
- `Shell.timer` - Shell lifetime/duration
- `Shell.transform.position` - Current shell position in world space

**Example Usage** (from `TweaksAndFixes\Harmony\Shell.cs`):
```csharp
[HarmonyPatch(typeof(Shell))]
internal class Patch_Shell
{
    public static Dictionary<Shell, Vector3> shellTargetData = new();
    public static Shell updating;
    
    [HarmonyPatch(nameof(Shell.Update))]
    internal static void Prefix_Update(Shell __instance)
    {
        // Track shell position
        updating = __instance;
    }
}
```

---

## 4. Ship Armor Zones

### `Ship.A` Enum (Armor Zones)
**Location**: `Il2Cpp.Ship.A`

**Values**:
- `Ship.A.Belt` - Side armor (belt armor)
- `Ship.A.Deck` - Top armor (deck armor)
- `Ship.A.TurretSide` - Turret side armor
- (Other armor zones may exist)

**Usage**:
```csharp
// Get armor thickness for a specific zone
if (ship.armor != null && ship.armor.TryGetValue(Ship.A.Belt, out float beltArmor))
{
    // beltArmor is in mm
}
```

---

## 5. Gun Data and Penetration

### `GunData.penetrations` Dictionary
**Location**: `Il2Cpp.GunData.penetrations`  
**Type**: `Dictionary<int, float>` (grade -> penetration in mm)

**Usage**:
```csharp
// Get penetration for a specific gun caliber and grade
if (G.GameData.guns.TryGetValue(caliberKey, out GunData gunData))
{
    int grade = ship.TechGunGrade(partData);
    if (gunData.penetrations != null && gunData.penetrations.ContainsKey(grade))
    {
        float penetration = gunData.penetrations[grade]; // in mm
    }
}
```

### `Ship.TechGunGrade(PartData partData)`
**Purpose**: Gets the technology grade of a gun part (0 to MaxGunGrade)

---

## 6. Weapon Range Caches

### `Ship.weaponRangesCache`
**Type**: `Dictionary<PartData, float>`  
**Purpose**: Caches weapon range values for different gun types

**Related**:
- `Ship.weaponRangesAPCache` - AP shell ranges
- `Ship.weaponRangesHECache` - HE shell ranges

**Usage**:
```csharp
float range = ship.weaponRangesCache.GetValueOrDefault(partData);
```

---

## 7. Practical Usage in BallisticTester

### Current Implementation
The `BallisticTester` currently:
1. **Hit Location**: Uses `Ship.GetSectionFromPositions` logic (simplified) to determine belt vs deck hits
2. **Penetration**: Uses `GunData.penetrations[grade]` when available, falls back to calculated formula
3. **Armor**: Uses `Ship.armor[Ship.A.Belt/Deck/TurretSide]` dictionary

### Potential Improvements
To make the test more accurate, you could:
1. **Use `Ship.HitChanceCalc`** to get actual hit probability for the test conditions
2. **Use `Ship.GetSectionFromPositions`** directly to get the exact hit section
3. **Access `Shell.willHitTarget`** if you create shells (requires proper Il2Cpp delegate setup)

---

## 8. File References

- **Hit Chance**: `TweaksAndFixes\Harmony\Ship.cs` (lines 18-45, 686-690)
- **Hit Location**: `TweaksAndFixes\Harmony\Ship.cs` (lines 474-521)
- **Shell Tracking**: `TweaksAndFixes\Harmony\Shell.cs`
- **Current Implementation**: `TweaksAndFixes\New\BallisticTester.cs`

---

## Notes

- All `Ship` and `Shell` methods are Il2Cpp types, requiring special handling for delegates
- The game uses a multiplier-based system for hit chance (accumulate various modifiers)
- Deck hits become more likely at longer ranges (plunging fire)
- Hit location is determined by shell trajectory and ship section geometry
- Armor values are stored in mm in the `Ship.armor` dictionary
