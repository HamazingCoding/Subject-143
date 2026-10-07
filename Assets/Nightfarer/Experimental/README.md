# Experimental / reference assets

Put any **temporary reference material** for local experiments in `Experimental/ReferenceAssets/`.
That folder is git-ignored, so nothing placed there is committed, and nothing in the permanent
Subject 143 assets may reference it.

To try a reference or work-in-progress clip:

1. Import the humanoid clip into `ReferenceAssets/` (Rig → Animation Type: Humanoid).
2. Duplicate `Data/AnimSet_Nightreign_Experimental.asset` into this folder.
3. Drag the clip into the slot you want (e.g. `Light1`). Empty slots fall back to the current set.
4. Add the new set to `NightfarerCharacter → Animation Sets` in the test scene, press Play, cycle with **F2**.

Gameplay timing comes from the WeaponData/NightfarerConfig assets, not from the clip, so the clip is
stretched to fit. If the clip's swing lines up with the blade, set that attack's `Arc Reach` to 0 so only
the blade sweep deals damage.

No Nightreign files are included in this project. See `../README.md` for why.
