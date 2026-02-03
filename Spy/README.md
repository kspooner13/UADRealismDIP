# Spy

A MelonLoader mod that depends on **TweaksAndFixes (TAF)** and references its DLL from this folder.

## Setup

1. **Build TweaksAndFixes first**  
   Build the `TweaksAndFixes` project so `TweaksAndFixes\TAF_Nightly\TweaksAndFixes.dll` exists.

2. **TAF DLL in Spy**  
   The Spy project expects `TweaksAndFixes.dll` in this folder (`Spy\`).  
   On build, if the DLL is missing here, it is copied from `..\TweaksAndFixes\TAF_Nightly\TweaksAndFixes.dll` when that path exists.

3. **Game paths in Spy.csproj**  
   The `.csproj` references MelonLoader and game assemblies under  
   `F:\Games\Ultimate.Admiral.Dreadnoughts.1.7.0.0\game\`.  
   Adjust those `HintPath` values if your game is installed elsewhere.

## Build order

From the solution root (e.g. `UADRealism.sln`):

1. Build **TweaksAndFixes** (produces `TAF_Nightly\TweaksAndFixes.dll`).
2. Build **Spy** (uses the TAF DLL in `Spy\` or copies it from TAF_Nightly).

## Usage

`SpyMod` is a MelonMod with `MelonOptionalDependencies("TweaksAndFixes")`, so MelonLoader loads it after TAF. You can use TAF types (e.g. `TweaksAndFixes.Config`) from this project.
