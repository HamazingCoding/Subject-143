# Environmental Hand IK — Naturalism Pass (tuning guide)

This pass layers Uncharted-style secondary motion onto your existing state machine
(Reset → Search → Approach → Rise → Touch). Nothing in the state flow changed; the
difference is *how* the hand moves toward its goal. All feel is inspector-tunable via
the **Feel → Hand IK Settings** block on the `EnvironmentInteractionStateMachine`.

## What was added

- **Anticipation (look-ahead):** the reach point is computed from where the shoulder
  *will* be (`velocity × Look Ahead Time`), so the hand starts reaching before you
  pass a surface instead of after you overlap it.
- **Spring / secondary motion:** the hand target and elbow hint use `SmoothDamp`
  (critically damped) instead of stiff lerps — natural ease-in, drag, and a touch of
  overshoot/settle. The wrist uses exponential rotation smoothing so it trails and
  follows through.
- **Elbow pose:** the existing IK hint poles are now driven so the elbow bends
  down-and-out and follows the hand with slight lag (no chicken-wing).
- **Speed & angle response:** reach strength scales weight — a fast glancing pass is a
  light brush; a close/slow pass is a full plant. Standing still = no reach.
- **Brush-along:** because the target tracks the anticipated closest point every frame,
  the hand naturally slides along a wall while you walk parallel to it.
- **Micro-motion:** subtle Perlin drift (scaled by how planted the hand is) keeps the
  contact from looking frozen.
- **Optional finger curl:** `HandFingerCurl` component (see below).

## Parameters — start here

| Parameter | What it does | Try |
|---|---|---|
| `Look Ahead Time` | How far ahead the hand anticipates. Too high = reaches at thin air. | 0.25–0.45 |
| `Reach Offset` | How far in front of the surface the palm plants. | 0.35–0.5 |
| `Anticipation Range` | Distance at which the hand notices a surface. | 1.4–1.8 |
| `Position Smooth Time` | Hand drag/float. Lower = snappier, higher = floatier. | 0.10–0.16 |
| `Hint Smooth Time` | Forearm follow. Keep slightly above Position Smooth Time. | 0.14–0.20 |
| `Rotation Smooth Speed` | Wrist follow-through. Lower = more trailing lag. | 9–13 |
| `Weight Smooth Speed` | Blend in/out speed. | 6–9 |
| `Min Move Speed` | Below this you don't reach. Set 0 to allow reaching while stationary. | 0.25 |
| `Full Reach Speed` | Speed at which reach is full strength. | ~ your run speed |
| `Approach Angle Influence` | 1 = only reach for stuff roughly ahead; 0 = any angle. | 0.5–0.7 |
| `Elbow Drop` / `Elbow Out` | Elbow lowered / bowed outward (metres). | 0.30 / 0.15 |
| `Idle Noise Amount` | Micro-motion. 0 to disable. | 0.008–0.015 |
| `Max Ik Weight` / `Max Rotation Weight` | Peak plant strength. | 1.0 / 0.85 |

Tuning order that works well: (1) Look Ahead Time + Reach Offset so the plant lands in
the right spot; (2) Position/Hint Smooth Time for the drag; (3) Rotation Smooth Speed
for the wrist; (4) Elbow Drop/Out for the pose; (5) Speed/angle response last.

## Optional: procedural finger curl (`HandFingerCurl`)

This is the part that needs a few minutes in the editor, per hand:

1. Add a `HandFingerCurl` component to the character.
2. Assign the matching `TwoBoneIKConstraint` (left or right) to **Ik Constraint** —
   the fingers curl in proportion to that hand's IK weight.
3. For each finger, add an entry and drag its joint bones **proximal → distal**
   (e.g. `Index_01`, `Index_02`, `Index_03`).
4. Set **Curl Axis** to the bones' local bend axis. Start with `(0,0,1)`; if fingers
   bend backward, flip the sign. Thumb often wants a smaller angle / different axis.
5. Tune **Curl Angle** and **Max Curl** in Play Mode.

It runs after the animation/rig each frame and rebuilds from a captured rest pose, so
it never accumulates or fights the solver.

## In-editor test protocol

1. Enter Play Mode. Console must stay clean — no NaN/Infinity transform errors, no
   `Collider.ClosestPoint` warnings (mesh surfaces are routed through their bounds).
2. Walk **past a wall on your left**, then **on your right** — both hands reach,
   plant, slide along, and retract smoothly. Confirms both-hands support.
3. Walk **toward** a surface head-on vs. **glance past** it fast — the head-on plant
   should be fuller; the fast pass should be a light brush.
4. **Stop** mid-reach — the hand should ease back to the idle animation, not stick.
5. Watch the **elbow**: it should bend down/out and follow, not snap or flip.
6. Stress it: brush along a **non-convex mesh** surface and a **curved** collider,
   run alongside a wall, reverse direction quickly — no jitter, no errors.

## Notes / honesty

The C# math (spring, look-ahead, reach strength, finger smoothing) was verified
numerically to stay finite under adversarial inputs (infinity, NaN, degenerate
directions). But *feel* can only be judged in Play Mode — the default values are a
starting point, not a final tune. The finger curl needs your rig's bones wired as
above before it does anything.
