# Nightfarer (Subject 143)

## Subject 143 (current player)

- **Model:** `Assets/Characters/Subject143/Subject143_Rigged.fbx` is your `Subject_143_True` mesh rigged
  by `Tools/Blender/rig_subject143.py`:
  - a humanoid skeleton at 1.25 m child height, bound with automatic weights and re-posed to a T-pose bind;
  - the original FBX is untouched;
  - to re-rig after editing the mesh, run
    `blender -b --python Tools/Blender/rig_subject143.py -- <src.fbx> <out.fbx> 1.25`, then rebuild.
- **Material:** `M_Subject143` (the Meshy colour texture plus its normal map).
- **Child scale:** capsule 1.15 m x 0.22 m, camera pivot 1.0 m and distance 3.2 m, hit-volume height 0.62 m.
  All of these are set from `Editor/Subject143Builder.cs`.
- **Claws, no weapon** (`Data/Weapon_Subject143_Claws.asset`):
  - the long thorned left arm is the main claw and the right hand is the off claw;
  - hit volumes run along each hand's fingers;
  - each attack's `Swing` (main) and `Swing Off` (off hand) paths drive the arms through `WeaponIK`'s
    claw mode, and `Hand` picks which claw deals damage;
  - light chain: Claw Rend, Off-hand Rake, Rising Rake, Double Rend;
  - heavies: Pouncing Rake and Thorn Whirl, both chargeable;
  - also a Leaping Rake (sprint), Diving Rend (jump) and Flash Rend (attack out of a step).
- **Flash step** replaces the roll (`NightfarerConfig → Evade style = FlashStep`):
  - a 3.8 m displacement in about 0.14 s, in any direction from your input;
  - with no input it steps back, away from the lock-on target or your facing;
  - invulnerable through the travel; the body vanishes, with afterimages and mist marking the path (`FlashStepVFX`);
  - locked on, steps are true sidesteps and backsteps that keep facing the target;
  - steps chain, and a light attack out of a step becomes Flash Rend.
- **Animation set** `AnimSet_Subject143_Feral`: hunched locomotion derived from the project clips, plus
  procedural step, claw, reaction and landing clips (a crouch landing, not a roll).
  `AnimSet_Subject143_PlainLocomotion` keeps the upright locomotion for comparison (F2).
- **Scene:** in SampleScene your static `Subject_143_True` is disabled, and the rig spawns where it stood.
  Backups: `SampleScene_BeforeSubject143.unity` and `SampleScene_BeforeNightfarer.unity`.

- **Coat:** your Meshy coat (`Characters/Subject143/143_Coat`) is set up as follows.
  - The rig script (`--coat`) fits it on his A-pose bind with the neck hole at the base of his neck. It keeps
    about 30k vertices (smooth shading) and ships in the same FBX as his body, sharing the skeleton.
  - The collar, shoulders and upper back are skinned to the torso. The sleeves are skinned to the arms. Below
    the chest line the coat is skinned to hidden bone chains (`CoatChain_##_i`, about 13 around the body).
    Meshy had fused the sleeve undersides to the coat's sides; the script cuts that strip so the claw arm can
    move freely.
  - In Unity, `ChainCloth` simulates only those chains (RE Engine-style): Verlet with substeps, gravity, wind
    gusts, turbulence, air drag and inertia; bend limits; links between neighbouring chains; body capsules
    including the thick claw arm; and the ground. Segments are inextensible, so the coat can no longer break
    into triangles. Warps snap it to the body, and flash steps carry it along.
  - Presets on `ChainCloth`: **Balanced** (default), **Flowing** (Journey-like: light, airy, wind-driven) and
    **Heavy** (RE-like: weighty, tight). **Custom** exposes every value. The loose coat in SampleScene is
    disabled, not deleted.
- **Claw Shot (E):**
  - the claw arm snaps out straight at the target like a web-shooter throw, while the off arm pulls back;
  - the claw line shoots out from the claw hand at 90 m/s;
  - the arm stays locked on through the pull;
  - range is 30 m (the `range`, `lineSpeed` and `pullSpeed` settings on `Ability_Subject143_ClawShot`);
  - it also works in the air, where he hangs briefly while the line flies;
  - the pull's speed carries out of it, like Wylder's hook: he slides on with it, and Jump during or just after
    the pull turns it into a faster, longer leap (the `Momentum` settings on the same asset).
- **Default animation set:** upright locomotion. The feral set is on F2. Climbing, vaulting, the flask, the super
  jump crouch, the hero landing and the side jumps have upright clips (`S143U_*`) in the default set and hunched
  ones (`S143_*`) in the feral set.
- **Movement feel (design pillars: momentum, commitment, explosive power):** set every build in
  `Subject143Builder.ConfigureTraversal`; the values live on `Config_Subject143`.
  - Acceleration about 0.35 s to run. Braking 20 at run, 9 at sprint, 6 at surge (m/s²).
  - Letting go at sprint speed or above **skids** (feet plant, lean back, dust). Reversing more than 120° at speed
    is a **pivot-skid** (brake, plant, relaunch). Flash step, attacks and jumps cancel skids.
  - The turn rate falls with speed, from 1080°/s at walk to 150°/s at surge. Above run speed the velocity follows
    the body with less grip, so he **drifts** and loses speed sliding sideways. Running keeps full grip.
  - **Zero air control:** the takeoff vector is final; the claw line is the only mid-air redirect. Spirit springs
    keep their steering.
  - Every jump starts with a 0.07 s **crouch**.
  - **Sprint launches** (hold Jump while sprinting or surging) multiply the speed he has built, up to 28 m/s, and
    fly flatter.
  - **Landings keep momentum:** a fast hero landing slides on with the claw dragging and sparking. Landings
    compress the pelvis in proportion to impact (FootIK spring).
  - **Combat momentum:** lights keep 30% of approach speed, sprint-attack lunges scale with speed, knockback grows
    with speed, and a flash step out of a sprint keeps the sprint speed.
  - **Body language:** `MomentumPose` banks the body into turns (more at higher speed) and pitches it with
    acceleration and braking. At surge the claw arm drags and scrapes sparks.
  - **Camera:** FOV 55/58/63/70 by speed, pulls back up to 0.6 m at surge, rolls up to 2° in surge turns, and dips
    on landings.
  - `NightfarerPlaytest` measures all of this. Logs go to `Logs/nightfarer_playtest.txt`; captures go to
    `body_language.png`.
- **Arms at rest:** idle and walking, both arms hang at his sides. The claws come up into the ready pose only
  when locked on, or for 2.5 s after attacking or being hit.
- **Claw moveset (v2, beast-style):**
  - Lights: Beast Swipe → Counter Swipe → Twin Rake → Frenzy (three separate hits) → Mauling Pounce.
  - Heavies (hold to charge): Reaver Sweep (the mutated arm hauls back while charging, then whips through a
    wide arc) and Thorn Whirl. Charging keeps winding up slowly, Elden Ring style (`chargeHoldEnd`).
  - Also Prowling Lunge (sprint), Falling Maul (jump), and Ambush X / Recoil X (out of a flash step).
  - Attacks can have several hit windows (`extraHitWindows`). The weapon asset rebuilds when
    `ClawMoveset.Version` changes.
  - These are original animations, not Elden Ring's; its assets can't be used.
- **Surge sprint:** speed streaks off the legs. Holding Jump while surging charges
  the super jump without a crouch or slowdown; release for the big jump at full speed.
- **Hair strands:** the rig script (`--hair-strands 8`) finds curly locks that stick out of the hair mass
  (horns and thorns are excluded) and gives each a 3-bone chain on Head. A second `ChainCloth` with the
  **Hair** preset simulates them: springy and shape-keeping, with wind, inertia and head collision, in the RE
  Strand chain-hair style.
- **Climbing:** hands grip the lip while the feet step up the wall, then he rolls over the top. Low walls are a
  one-handed claw vault. Hands are placed by WeaponIK and feet by FootIK.
- **Super jump (hold Jump):** a short press is a normal jump. Holding past 0.16 s crouches and charges for up to
  0.75 s; release to spring up to 7.5 m, plus forward speed if moving. Settings are under
  `Config_Subject143 → Super jump`.
- **Hero landing:** drops of 1.9 m or more end in a three-point landing: right knee down, claw planted, off arm
  back. The camera shakes, dust rings out, the floor cracks and debris flies, all scaled by the height. Shorter
  falls kick up a little dust. To restore the old landing roll, untick `heroLanding`.
- **Side jump:** Jump with sideways input while locked on (or out of an attack or flash step) hops about 3.4 m
  sideways. He keeps facing the target, with brief i-frames, and can throw the claw line or jump-attack from it.
- **Foot IK** (`FootIK` on the player): feet land on slopes, steps and ledges, and the pelvis drops for the
  lower foot. It also plants both feet in the super jump crouch and the hero landing.
- **Combat feel:**
  - `CombatFX` on the player draws action lines (streaks off the claw tips) during strikes, the claw throw and
    the ultimate.
  - It cracks the air on heavy hits, with black fractures and a violet glow.
  - Per-attack `screenBlur`, `speedLines` and `spaceCrack` settings fire a radial blur and anime speed lines
    when the hit lands (heavies, finisher, sprint, jump and step attacks).
  - `ScreenFX` on the camera does the screen work: blur, speed lines, and the Thornburst ultimate's impact frames
    (inverted black-and-white cuts with a freeze).

The sections below describe the shared system. Sword content from earlier rounds is still in `Data/` but is no
longer used by the rig.

A Nightreign-inspired third-person movement and combat system with a HUD, pause menu and title screen,
played by the Meshy "Nocturnal Warden" character.

## Play it

- **Your game scene:** `Assets/Scenes/SampleScene.unity` now contains the `NightfarerRig` prefab
  (player, camera, HUD). Your original Player and cameras are **disabled, not deleted**.
- **From the title screen:** `Assets/Nightfarer/Scenes/MainMenu.unity` is first in Build Settings.
  *Start* loads SampleScene; *Combat Arena* loads the test arena.
- **Combat arena:** `Assets/Nightfarer/Scenes/NightfarerTestScene.unity` has dummies, a drop tower,
  climbing walls, a spirit spring and a sprint lane.

To go back to your old setup:
- open `Assets/Scenes/SampleScene_BeforeNightfarer.unity` (an untouched copy made before integration); or
- in SampleScene, disable `NightfarerRig` and re-enable `Player` and `Main Camera`.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move / camera | WASD / mouse | LS / RS |
| Dodge (roll; backstep with no input) | tap Space | tap B |
| Sprint | hold Space | hold B |
| Surge sprint (toggle while moving) | Left Alt | L3 |
| Jump / climb a ledge (jump into a wall) / launch from a spirit spring | F | A |
| Light attack (chains; sprint/roll/jump/backstep attacks) | LMB | RB |
| Heavy attack (hold to charge) | RMB | RT |
| Lock on / switch target (flick the camera) | Q or MMB | R3 |
| Character skill: Claw Shot, then Light for the follow-up | E | LT |
| Ultimate Art: Onslaught Stake (gauge fills by dealing damage) | G | Y |
| Crimson Tears flask (you can walk while drinking) | R | X |
| Switch weapon | X | D-pad right |
| Walk toggle | Left Ctrl | D-pad up |
| Pause menu (settings, controls) | Esc | Start |
| Debug overlay / anim set / feel profile / slow-mo / hitbox lines / reset | F1 / F2 / F3 / F4 / F5 / Backspace | |

## What it does

**Movement:**
- tap-roll and hold-sprint on one button, released into a roll (Elden Ring style);
- surge sprint toggle with an entry stamina cost;
- fast camera-relative turning; sprinting carries momentum through turns;
- no fall damage: high falls end in a landing roll;
- ledge mantling up to about 2.4 m when jumping into a wall;
- spirit springs that launch you about 14 m up with air steering;
- walking slowly while drinking a flask.

**Lock-on:**
- picks the target nearest the view centre and needs line of sight;
- flick the mouse or right stick to switch targets; the lock breaks at range;
- while locked you strafe around the target in 8 directions;
- rolls stay directional while you keep facing the target;
- sprinting breaks facing so you can reposition;
- the camera frames both of you;
- the HUD shows a reticle and the target's health bar.

**Combat:**
- buffered light chains with combo windows, plus dodge-cancel and move-cancel points;
- chargeable heavies with hyper armour;
- sprint, roll, jump and backstep attacks;
- hit-stop and camera shake;
- poise breaks on dummies;
- Claw Shot skill with a follow-up attack;
- an ultimate art charged by dealt damage;
- weapon switching.

**UI:**
- **HUD** (uGUI):
  - health with a lagging damage trail, and stamina;
  - skill cooldown dial and ultimate gauge, which glows when ready;
  - flask count and weapon slot;
  - lock-on reticle and boss-style target bar;
  - notifications, and a damage / low-health vignette.
- **Pause menu:** Resume, Settings, Controls, Restart, Main Menu, Quit.
  - Settings: mouse and pad sensitivity, FOV, invert Y, animation set, movement feel, debug overlay.
  - Settings are saved to PlayerPrefs.
- **Title screen** with the character on a lit stage.
- To restyle, edit `Prefabs/NightfarerRig.prefab → NightfarerUI`. The code only needs the references
  on the `NightfarerUI` component.

## Animation: how it's built, and how to replace it

There are two layers:

1. **Body clips** in an `AnimationSet`, applied through an `AnimatorOverrideController` on the generated
   base controller.
   - Locomotion uses your existing Kevin Iglesias clips. The Nightreign-style set derives versions with a
     weapon-carry posture and forward lean, including a surge sprint pose.
   - Attacks, rolls, mantle, drink and ultimate use procedural placeholder clips.
   - Every action clip is time-stretched to the gameplay duration in its data, so replacing a clip never
     changes hit windows, cancels or i-frames.
2. **Procedural weapon layer** (`WeaponIK`), applied after the Animator:
   - Two-bone IK drives both arms so the weapon follows each attack's authored `Swing` path (grip
     position and blade direction over time, in `WeaponData → Moveset → Swing`).
   - The off hand holds the handle for two-handed weapons.
   - The weapon sits in a guard pose when standing or locked on, and a low trailing carry when running.
   - This is what makes the swings read as real weapon arcs, and it puts the blade through the target
     for hit detection.

To use your own animations:
- Drop clips into an AnimationSet slot.
- If a clip already animates the arms and weapon properly, clear that attack's `Swing` keys so the clip
  drives the arms.
- Set `Arc Reach` to 0 once the blade path in your clip is reliable.

To use another character: any Humanoid model works. Swap the model under `NightfarerPlayer/Visual` in the
prefab. Weapon grips are derived automatically from the rig: from finger bones if the rig has them, else
from the T-pose forearm axis, as with the Meshy rig.

## Nightreign assets

- The Nightreign install was only read, never modified.
- An attempt to extract its animations stopped when downloading the community archive decryption keys
  was blocked by this session's security permissions. Its archives are encrypted, so without those keys
  nothing can be read.
- No Nightreign files are in this project. Everything here reproduces publicly documented behaviour with
  hand-tuned values.
- If you later convert reference clips yourself for private evaluation, put them in
  `Experimental/ReferenceAssets/`. That folder is git-ignored and must never ship.

## Regenerating

- **Subject 143 → Nightfarer → Build Test Setup** rebuilds generated assets, the rig prefab, the arena,
  the title screen and the SampleScene integration.
  - It keeps your tuned data assets.
  - It makes the SampleScene backup only once.
- **Rebuild (overwrite tuning data)** resets configs, weapons, profiles and sets to their defaults.

## Tests

There are 51 PlayMode tests. `NightfarerPlaytest` records movement-feel numbers to
`Logs/nightfarer_playtest.txt` (acceleration, stopping, turning, air control, landing, camera); rerun it after
tuning to compare. The rest are in `Tests/`, covering:
- movement, defence, combat, lock-on, skill and ultimate;
- climbing (hands and feet), super jump, hero landing, side jump, air claw line and momentum, foot IK;
- spirit springs, flasks, the coat and combat screen effects;
- weapon IK, the HUD and pause menu;
- keyboard bindings;
- SampleScene and title-screen flows.

Measured values go to `Logs/nightfarer_playmode_metrics.txt`. Screenshots go to `Logs/NightfarerCaptures/`.
