## Context
- This is a Vintage Story mod
- Vintage Story API and decompiled source code from the entire game live in `VS_1.21_Decompiled/`
- Vintage Story assets (json for blocktypes, recipes, shapes etc) live in `VS_1.21_assets`
- All the code in VS_1.21_assets and VS_1.21_Decompiled are from the latest compatible game version so you can trust it completely.
- Our focus is on NET8, but NET7 is included - do not be concerned with warnings about NET7 compatibility.

## Instructions
- When changing or using functions, methods, classes, variables or other things from the Vintage Story API or source, always check the corresponding file in VS_1.21_Decompiled/
- Always check the full code in LocatorBox for context, even when doing small changes.

## Build & test
- Do not build or test Cake / ZZCakeBuild / Program.cs
- Ignore all warnings about NET7 compatiblity or legacy net 7 warnings
- Build: `dotnet build -nologo -clp:Summary -warnaserror`
- Test: `dotnet test --nologo --verbosity=minimal`
- Lint (optional): `dotnet format --verify-no-changes`
- Game dependency DLL files live in `VSFOLDER` also available as env variable VINTAGE_STORY

## Project name to search for
- Solution: `ImprovedModMenu.sln`
