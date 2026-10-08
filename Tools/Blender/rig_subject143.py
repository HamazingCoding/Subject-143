"""
Auto-rig Subject_143_True (static Meshy mesh) into a Unity-humanoid skeleton.

Run headless:  blender -b --python Tools/Blender/rig_subject143.py -- <src.fbx> <out.fbx> <height_m> [--preview <png_dir>]

Steps: import -> apply transforms -> scale to height, feet at origin -> estimate joints from mesh cross-sections
-> build armature (Mixamo-style names Unity auto-maps) -> automatic weights -> re-pose arms to a T-pose and
apply it as the rest pose -> export FBX for Unity. The source FBX is never modified.
"""
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC, OUT, HEIGHT = argv[0], argv[1], float(argv[2])
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None
COAT = argv[argv.index("--coat") + 1] if "--coat" in argv else None
# Bind pose: the model's own A-pose by default (matches the A-pose coat); --tpose straightens the arms instead.
TPOSE = "--tpose" in argv
COAT_VERTS = int(argv[argv.index("--coat-verts") + 1]) if "--coat-verts" in argv else 30000
COAT_CHAINS = int(argv[argv.index("--coat-chains") + 1]) if "--coat-chains" in argv else 14
COAT_SEGMENTS = int(argv[argv.index("--coat-segments") + 1]) if "--coat-segments" in argv else 5
# Extra vertical offset of the coat (metres, negative = lower). Default sits the collar around the shoulders.
NECK_DROP = float(argv[argv.index("--coat-drop") + 1]) if "--coat-drop" in argv else -0.035
# Hair locks given bone chains (RE "Strand"-style chain hair). 0 disables.
HAIR_STRANDS = int(argv[argv.index("--hair-strands") + 1]) if "--hair-strands" in argv else 8
HAIR_SEGMENTS = 3

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
mesh.name = "Subject143_Body"
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# ---------------------------------------------------------------- normalise: height, feet on the ground, centred
def verts():
    return [v.co.copy() for v in mesh.data.vertices]

V = verts()
zmin = min(v.z for v in V); zmax = max(v.z for v in V)
s = HEIGHT / (zmax - zmin)
for v in mesh.data.vertices:
    v.co = v.co * s
V = verts()
zmin = min(v.z for v in V)
# centre on the legs/torso (exclude the long claw arm by using the lower body only)
low = [v for v in V if v.z < zmin + 0.4 * HEIGHT and abs(v.x) < 0.2 * HEIGHT]
cx = sum(v.x for v in low) / len(low); cy = sum(v.y for v in low) / len(low)
for v in mesh.data.vertices:
    v.co -= Vector((cx, cy, zmin))
mesh.data.update()
V = verts()
H = HEIGHT

def slab(u0, u1, xmin=-9, xmax=9):
    return [v for v in V if u0 * H <= v.z < u1 * H and xmin <= v.x <= xmax]

def centroid(pts):
    n = len(pts)
    return Vector((sum(p.x for p in pts) / n, sum(p.y for p in pts) / n, sum(p.z for p in pts) / n)) if n else None

def section_centre(u, xmin, xmax, du=0.012):
    """Centre of the mesh cross-section at height u (fraction of H) within a lateral window."""
    pts = slab(u - du, u + du, xmin * H, xmax * H)
    if not pts:
        return None
    c = centroid(pts)
    # bounding-box centre is more robust than the mean for clothing flaps
    xs = sorted(p.x for p in pts); ys = sorted(p.y for p in pts)
    lo = int(len(xs) * 0.05); hi = max(lo, int(len(xs) * 0.95) - 1)
    return Vector(((xs[lo] + xs[hi]) / 2, (ys[lo] + ys[hi]) / 2, u * H))

def legs_at(u):
    """Two leg centres at height u: split the lower-body section at its largest x gap."""
    pts = slab(u - 0.015, u + 0.015, -0.16 * H, 0.16 * H)
    xs = sorted(p.x for p in pts)
    best, split = 0, 0.0
    for a, b in zip(xs, xs[1:]):
        if b - a > best and -0.08 * H < (a + b) / 2 < 0.08 * H:
            best, split = b - a, (a + b) / 2
    right = [p for p in pts if p.x < split]; left = [p for p in pts if p.x >= split]
    def ctr(ps):
        xs2 = sorted(p.x for p in ps); ys2 = sorted(p.y for p in ps)
        return Vector(((xs2[0] + xs2[-1]) / 2, (ys2[0] + ys2[-1]) / 2, u * H))
    return ctr(right), ctr(left), best

# ---------------------------------------------------------------- landmarks
# Joint positions measured on a gridded orthographic render of Subject_143_True scaled to 1.25 m (x: character's
# left is +x, z up). They are expressed as fractions of the height so the rig follows if the target height changes.
# Depth (y) is snapped to the centre of the mesh cross-section at each joint.
M = {
    "Hips": (-0.045, 0.58), "Spine": (-0.045, 0.65), "Chest": (-0.045, 0.76), "UpperChest": (-0.045, 0.87),
    "Neck": (-0.04, 0.975), "Head": (-0.04, 1.03), "HeadTop": (-0.04, 1.22),
    "RightClavicle": (-0.07, 0.935), "RightShoulder": (-0.15, 0.95), "RightElbow": (-0.212, 0.73),
    "RightWrist": (-0.27, 0.575), "RightHandTip": (-0.278, 0.50),
    "LeftClavicle": (-0.02, 0.935), "LeftShoulder": (0.055, 0.95), "LeftElbow": (0.118, 0.725),
    "LeftWrist": (0.172, 0.58), "LeftHandTip": (0.2, 0.47),
    "RightHip": (-0.10, 0.545), "LeftHip": (0.005, 0.545),
    "RightKnee": (-0.10, 0.31), "LeftKnee": (0.02, 0.31),
    "RightAnkle": (-0.095, 0.07), "LeftAnkle": (0.015, 0.07),
}
SCALE = H / 1.25

def snap_y(x, z, rx=0.035, rz=0.02):
    pts = [v for v in V if abs(v.x - x) < rx * SCALE and abs(v.z - z) < rz * SCALE]
    if len(pts) < 5:
        return 0.0
    ys = sorted(p.y for p in pts)
    lo = int(len(ys) * 0.05); hi = max(lo, int(len(ys) * 0.95) - 1)
    return (ys[lo] + ys[hi]) / 2

J = {}
for name, (x, z) in M.items():
    x *= SCALE; z *= SCALE
    J[name] = Vector((x, snap_y(x, z), z))
# feet: toes point toward -y (the model faces -Y)
for side in ("Right", "Left"):
    ank = J[side + "Ankle"]
    foot = [v for v in V if v.z < 0.035 * H and abs(v.x - ank.x) < 0.05 * H]
    toe_y = min(v.y for v in foot)
    J[side + "Ball"] = Vector((ank.x, ank.y + (toe_y - ank.y) * 0.68, 0.015 * H))
    J[side + "ToeTip"] = Vector((ank.x, toe_y, 0.015 * H))
R, L = [], []

for k, v in J.items():
    print("JOINT %-14s %7.3f %7.3f %7.3f   (u %.2f)" % (k, v.x, v.y, v.z, v.z / H))
print("ARM SAMPLES R %d L %d" % (len(R), len(L)))

# ---------------------------------------------------------------- armature
arm_data = bpy.data.armatures.new("Subject143_Armature")
rig = bpy.data.objects.new("Subject143_Rig", arm_data)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones

def bone(name, head, tail, parent=None, connect=False):
    b = eb.new(name)
    b.head = head; b.tail = tail
    if (b.tail - b.head).length < 1e-3:
        b.tail = b.head + Vector((0, 0, 0.02))
    if parent:
        b.parent = eb[parent]; b.use_connect = connect
    return b

bone("Hips", J["Hips"], J["Spine"])
bone("Spine", J["Spine"], J["Chest"], "Hips", True)
bone("Spine1", J["Chest"], J["UpperChest"], "Spine", True)
bone("Spine2", J["UpperChest"], J["Neck"], "Spine1", True)
bone("Neck", J["Neck"], J["Head"], "Spine2", True)
bone("Head", J["Head"], J["HeadTop"], "Neck", True)
for side in ("Left", "Right"):
    bone(side + "Shoulder", J[side + "Clavicle"], J[side + "Shoulder"], "Spine2")
    bone(side + "Arm", J[side + "Shoulder"], J[side + "Elbow"], side + "Shoulder", True)
    bone(side + "ForeArm", J[side + "Elbow"], J[side + "Wrist"], side + "Arm", True)
    bone(side + "Hand", J[side + "Wrist"], J[side + "HandTip"], side + "ForeArm", True)
    bone(side + "UpLeg", J[side + "Hip"], J[side + "Knee"], "Hips")
    bone(side + "Leg", J[side + "Knee"], J[side + "Ankle"], side + "UpLeg", True)
    bone(side + "Foot", J[side + "Ankle"], J[side + "Ball"], side + "Leg", True)
    bone(side + "ToeBase", J[side + "Ball"], J[side + "ToeTip"], side + "Foot", True)
# consistent bone rolls (Z axis pointing forward = -Y in this model)
for b in eb:
    b.align_roll(Vector((0, -1, 0)) if abs((b.tail - b.head).normalized().y) < 0.9 else Vector((0, 0, 1)))
bpy.ops.object.mode_set(mode="OBJECT")

def render_preview(tag):
    if not PREVIEW:
        return
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 700; scene.render.resolution_y = 900
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_xray = True
    scene.display.shading.xray_alpha = 0.35
    arm_data.display_type = "STICK"
    rig.show_in_front = True
    if "cam" not in bpy.data.objects:
        cd = bpy.data.cameras.new("cam"); cd.type = "ORTHO"; cd.ortho_scale = H * 1.25
        cam = bpy.data.objects.new("cam", cd); scene.collection.objects.link(cam); scene.camera = cam
    cam = bpy.data.objects["cam"]
    for view, loc, rot in [("front", (0, -10, H / 2), (math.radians(90), 0, 0)), ("side", (10, 0, H / 2), (math.radians(90), 0, math.radians(90)))]:
        cam.location = loc; cam.rotation_euler = rot
        scene.render.filepath = f"{PREVIEW}\\rig_{tag}_{view}.png"
        bpy.ops.render.render(write_still=True)

render_preview("joints")
if "--joints-only" in argv:
    sys.exit(0)

# ---------------------------------------------------------------- skin
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True); rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
unweighted = sum(1 for v in mesh.data.vertices if len(v.groups) == 0)
print("UNWEIGHTED VERTS", unweighted, "of", len(mesh.data.vertices))

# ---------------------------------------------------------------- T-pose (optional): straighten arms horizontally, apply as rest
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE" if TPOSE else "OBJECT")
if not TPOSE:
    print("BIND POSE: A-pose (model's own)")
if TPOSE:
    for side, sign in (("Left", 1), ("Right", -1)):
        for bname in (side + "Arm", side + "ForeArm", side + "Hand"):
            pb = rig.pose.bones[bname]
            bpy.context.view_layer.update()
            cur = (pb.tail - pb.head).normalized()                     # armature space
            target = Vector((sign, 0, 0))
            rot = cur.rotation_difference(target)
            # convert the armature-space delta into the bone's local rotation
            mat = pb.matrix.copy()
            loc = mat.to_translation()
            new = (rot.to_matrix().to_4x4() @ mat.to_3x3().to_4x4())
            new.translation = loc
            pb.matrix = new
            bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    # bake the pose into the mesh, then make it the rest pose
    bpy.context.view_layer.objects.active = mesh
    mod = [m for m in mesh.modifiers if m.type == "ARMATURE"][0]
    bpy.ops.object.modifier_copy(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for m in mesh.modifiers:
        if m.type == "ARMATURE":
            m.object = rig
render_preview("tpose")

# ---------------------------------------------------------------- hair locks: bone chains on the curls that stand proud
# The hair is part of the body mesh. Locks are found as clumps that stick out of the hair mass (per-direction
# median radius around the skull centre), excluding anything pointing up (horns and thorns stay rigid). Each
# seed is grown back toward its root, then the longest, well-separated locks get a chain parented to Head.
# Vertices near the root stay on Head and blend onto the chain toward the tip.
def rig_hair():
    import math
    from collections import deque
    bones = arm_data.bones
    W = lambda n: rig.matrix_world @ bones[n].head_local
    headb = W("Head")
    Vh = [v.co.copy() for v in mesh.data.vertices]
    region = [i for i, p in enumerate(Vh) if p.z > headb.z - 0.01 * SCALE and abs(p.x - headb.x) < 0.2 * SCALE and abs(p.y - headb.y) < 0.2 * SCALE]
    zcut = 1.2 * SCALE
    core = [Vh[i] for i in region if Vh[i].z < zcut]
    xs = sorted(p.x for p in core); ys = sorted(p.y for p in core); zs = sorted(p.z for p in core)
    C = Vector(((xs[2] + xs[-3]) / 2, (ys[2] + ys[-3]) / 2, (zs[0] + zcut) / 2 + 0.01 * SCALE))

    def key(d):
        th = math.atan2(d.y, d.x); ph = math.acos(max(-1.0, min(1.0, d.z)))
        return (int((th + math.pi) / (2 * math.pi) * 24) % 24, int(ph / math.pi * 12))
    bins = {}
    for i in region:
        d = Vh[i] - C
        bins.setdefault(key(d.normalized()), []).append(d.length)
    med = {k: sorted(v)[len(v) // 2] for k, v in bins.items()}
    prot = {}
    for i in region:
        d = Vh[i] - C
        if d.normalized().z > 0.45:
            continue
        prot[i] = d.length - med[key(d.normalized())]
    adj = [[] for _ in Vh]
    for e in mesh.data.edges:
        a, b = e.vertices
        adj[a].append(b); adj[b].append(a)
    cand = set(i for i, pr in prot.items() if pr > 0.012 * SCALE)
    seen, locks = set(), []
    for s0 in cand:
        if s0 in seen:
            continue
        comp, dq = [], deque([s0]); seen.add(s0)
        while dq:
            k = dq.popleft(); comp.append(k)
            for m in adj[k]:
                if m in cand and m not in seen:
                    seen.add(m); dq.append(m)
        if len(comp) < 12:
            continue
        tip = max(comp, key=lambda i: prot[i])
        cs = set(comp); dq = deque(comp)
        while dq and len(cs) < 500:
            k = dq.popleft()
            for m in adj[k]:
                if m not in cs and m in prot and prot[m] > -0.004 * SCALE and (Vh[m] - Vh[tip]).length < 0.075 * SCALE:
                    cs.add(m); dq.append(m)
        base = min(cs, key=lambda i: (Vh[i] - C).length)
        locks.append(((Vh[tip] - Vh[base]).length, tip, base, cs))
    locks.sort(key=lambda t: -t[0])
    chosen = []
    for L, tip, base, cs in locks:
        if L < 0.02 * SCALE or len(cs) > 1500:
            continue
        if all((Vh[tip] - Vh[o[1]]).length > 0.04 * SCALE and not (cs & o[3]) for o in chosen):
            chosen.append((L, tip, base, cs))
        if len(chosen) >= HAIR_STRANDS:
            break

    # Chains: joints along the lock's centre line, root at its base.
    chains = []
    for n, (L, tip, base, cs) in enumerate(chosen):
        a, b = Vh[base], Vh[tip]
        axis = b - a
        ln = max(1e-5, axis.length)
        u = axis / ln
        joints = []
        for j in range(HAIR_SEGMENTS + 1):
            t = j / HAIR_SEGMENTS
            near = [Vh[i] for i in cs if abs((Vh[i] - a).dot(u) / ln - t) < 0.18]
            joints.append(sum(near, Vector()) / len(near) if near and 0 < j < HAIR_SEGMENTS else a + axis * t)
        chains.append(("HairStrand_%02d" % n, joints, cs, a, u, ln))
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    ebs = arm_data.edit_bones
    for name, joints, *_ in chains:
        prev = None
        for j in range(len(joints) - 1):
            bn = ebs.new("%s_%d" % (name, j))
            bn.head = joints[j]; bn.tail = joints[j + 1]
            if (bn.tail - bn.head).length < 1e-3:
                bn.tail = bn.head + Vector((0, 0, 0.005))
            bn.parent = ebs["Head"] if prev is None else prev
            bn.use_connect = prev is not None
            prev = bn
    bpy.ops.object.mode_set(mode="OBJECT")

    # Weights: root stays with the head, blending onto the chain toward the tip (max 4 influences).
    groups = {g.name: g for g in mesh.vertex_groups}
    def group(name):
        if name not in groups:
            groups[name] = mesh.vertex_groups.new(name=name)
        return groups[name]
    idx_name = {g.index: g.name for g in mesh.vertex_groups}
    for name, joints, cs, a, u, ln in chains:
        nseg = len(joints) - 1
        for i in cs:
            v = mesh.data.vertices[i]
            t = max(0.0, min(1.0, (Vh[i] - a).dot(u) / ln))
            blend = max(0.0, min(1.0, (t - 0.25) / 0.45))
            if prot.get(i, 0.0) < 0.0:
                blend = 0.0                      # hair lying in the mass stays on the head: only the proud curl swings
            blend = blend * blend * (3 - 2 * blend)
            old = {idx_name[g.group]: g.weight for g in v.groups if g.weight > 0}
            w = {k: val * (1 - blend) for k, val in old.items()}
            sp = min(nseg - 1e-3, t * nseg)
            chain_w = {}
            for bi in range(nseg):
                cw = max(0.0, 1.0 - abs(sp - (bi + 0.5)))
                if cw > 0:
                    chain_w["%s_%d" % (name, bi)] = cw
            tot = sum(chain_w.values()) or 1.0
            for k, cw in chain_w.items():
                w[k] = w.get(k, 0.0) + cw / tot * blend
            top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
            norm = sum(x for _, x in top) or 1.0
            keep = dict(top)
            for k in old:
                if k not in keep:
                    group(k).remove([i])
            for k, x in top:
                group(k).add([i], x / norm, "REPLACE")
    print("HAIR strands:", len(chains), [round(c[5], 3) for c in chains])

if HAIR_STRANDS > 0:
    rig_hair()

# ---------------------------------------------------------------- coat (optional): fit, decimate, skin to the torso
if COAT:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=COAT)
    coat = [o for o in bpy.context.scene.objects if o not in before and o.type == "MESH"][0]
    for o in list(bpy.context.scene.objects):
        if o not in before and o is not coat and o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    coat.name = "Coat"
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = coat
    coat.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Render resolution: the coat is driven by bone chains, so it can stay detailed (only the chains are simulated).
    dec = coat.modifiers.new("decimate", "DECIMATE")
    dec.ratio = min(1.0, COAT_VERTS / max(1, len(coat.data.vertices)))
    bpy.ops.object.modifier_apply(modifier=dec.name)

    cv = [v.co.copy() for v in coat.data.vertices]
    cz0 = min(v.z for v in cv); cz1 = max(v.z for v in cv); ch = cz1 - cz0
    shoulder_rel = 0.82                      # Meshy humanoid garment: shoulder seam at ~82% of its height (width reference)
    # Neck opening: lowest height where the garment closes to a narrow hole around its axis.
    def hole_radius(u):
        sl = [v for v in cv if abs(v.z - (cz0 + u * ch)) < 0.005 * ch]
        return min(((v.x - collar_x0) ** 2 + (v.y - collar_y0) ** 2) ** 0.5 for v in sl) if sl else 9.0
    top = [v for v in cv if v.z > cz0 + 0.9 * ch]
    collar_x = sum(v.x for v in top) / len(top); collar_y = sum(v.y for v in top) / len(top)
    collar_x0, collar_y0 = collar_x, collar_y
    band = [v for v in cv if abs(v.z - (cz0 + shoulder_rel * ch)) < 0.02 * ch]
    coat_shoulder_span = max(v.x for v in band) - min(v.x for v in band)

    # Body landmarks from the (T-pose) rest skeleton.
    bones = arm_data.bones
    neck = rig.matrix_world @ bones["Neck"].head_local
    l_sh = rig.matrix_world @ bones["LeftArm"].head_local
    r_sh = rig.matrix_world @ bones["RightArm"].head_local
    body_shoulder_z = (l_sh.z + r_sh.z) / 2
    body_span = abs(l_sh.x - r_sh.x)
    hem_clearance = 0.1 * SCALE
    neck_rel = next((u / 100 for u in range(80, 100) if hole_radius(u / 100) < 0.09 * ch / 1.9), 0.91)
    neck_z = neck.z + 0.01 * SCALE + NECK_DROP                                     # neck hole at the base of his neck
    sv = (neck_z - hem_clearance) / (neck_rel * ch)                               # length: hem just above the ground
    sh = max(body_span * 1.85 / coat_shoulder_span, 0.5)                          # width: clears both shoulders + claw arm
    sd = sh * 1.05                                                               # depth
    print("COAT fit: neck hole at %.2f of coat height -> z %.3f; vertical %.3f horizontal %.3f depth %.3f (coat %.2f m tall, %d verts)" % (neck_rel, neck_z, sv, sh, sd, ch * sv, len(cv)))
    for v in coat.data.vertices:
        c = v.co
        v.co = Vector(((c.x - collar_x) * sh + neck.x, (c.y - collar_y) * sd + neck.y + 0.01 * SCALE, (c.z - (cz0 + neck_rel * ch)) * sv + neck_z))
    coat.data.update()

    # ------------------------------------------------------------ bone chains (chain cloth, as RE-style coats)
    import math
    for poly in coat.data.polygons:
        poly.use_smooth = True
    S = SCALE
    torso = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "LeftShoulder", "RightShoulder", "LeftArm", "RightArm"]
    W = lambda name: rig.matrix_world @ bones[name].head_local
    segs = {b: (W(b), rig.matrix_world @ bones[b].tail_local) for b in torso}
    ax, ay = neck.x, neck.y                              # body axis
    z_root = W("Spine1").z                               # chains hang from the chest line down
    arms = {side: (W(side + "Arm"), W(side + "Hand")) for side in ("Left", "Right")}

    def seg_dist(p, a, b):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-8, ab.dot(ab))))
        return (p - (a + ab * t)).length, t

    def sleeve_of(p):
        """Which sleeve a vertex belongs to (or None): close to an arm line and outside the torso."""
        if abs(p.x - ax) < 0.13 * S:
            return None
        best = None
        for side, (a, b) in arms.items():
            d, t = seg_dist(p, a, b)
            c = a + (b - a) * t
            inward = math.hypot(c.x - ax, c.y - ay) - math.hypot(p.x - ax, p.y - ay)   # toward the body from the arm
            if d < 0.11 * S and t > 0.15 and inward < 0.05 * S and (best is None or d < best[1]):
                best = (side, d, t)
        return best

    verts = [v.co.copy() for v in coat.data.vertices]
    sleeve_info = [sleeve_of(p) for p in verts]
    # Each sleeve is one connected piece: drop stray bits of the side panel that happen to lie near the arm.
    adj = [[] for _ in verts]
    for e in coat.data.edges:
        i, j = e.vertices
        adj[i].append(j); adj[j].append(i)
    for side in ("Left", "Right"):
        seen, best = set(), []
        for s0 in range(len(verts)):
            if s0 in seen or not sleeve_info[s0] or sleeve_info[s0][0] != side:
                continue
            comp, stack = [], [s0]
            seen.add(s0)
            while stack:
                k = stack.pop(); comp.append(k)
                for m in adj[k]:
                    if m not in seen and sleeve_info[m] and sleeve_info[m][0] == side:
                        seen.add(m); stack.append(m)
            if len(comp) > len(best):
                best = comp
        keepset = set(best)
        for k in seen - keepset:
            sleeve_info[k] = None
    # Meshy fuses the sleeve's underside to the coat's side; when the arm moves away that strip becomes a
    # membrane from armpit to hand. Cut the faces joining sleeve and body below the armpit.
    import bmesh
    from collections import deque
    bm = bmesh.new()
    bm.from_mesh(coat.data)
    bm.verts.ensure_lookup_table()
    cut = []
    for f in bm.faces:
        info = [sleeve_info[v.index] for v in f.verts]
        on = [x for x in info if x is not None]
        if on and len(on) < len(info) and max(x[2] for x in on) > 0.32:
            cut.append(f)
    bmesh.ops.delete(bm, geom=cut, context="FACES_ONLY")
    loose = [v for v in bm.verts if not v.link_faces]
    keep = [v.index for v in bm.verts if v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(coat.data)
    bm.free()
    coat.data.update()
    verts = [verts[k] for k in keep]
    sleeve_info = [sleeve_info[k] for k in keep]
    print("COAT underarm cut: %d faces" % len(cut))
    # Small pieces the cut detached (bits of sleeve) would stay behind on the torso chains and float beside him when
    # the arm moves: skin them to the nearest arm instead (or drop them if they're nowhere near an arm).
    adj2 = [[] for _ in verts]
    for poly in coat.data.polygons:                 # face connectivity (the cut leaves bare edges behind)
        pv = list(poly.vertices)
        for q in range(len(pv)):
            adj2[pv[q]].append(pv[q - 1]); adj2[pv[q - 1]].append(pv[q])
    seen2, islands = set(), []
    for s0 in range(len(verts)):
        if s0 in seen2:
            continue
        comp, dq = [], deque([s0]); seen2.add(s0)
        while dq:
            k = dq.popleft(); comp.append(k)
            for m in adj2[k]:
                if m not in seen2:
                    seen2.add(m); dq.append(m)
        islands.append(comp)
    islands.sort(key=len, reverse=True)
    print("COAT islands after cut:", [len(c) for c in islands[:6]])
    drop = []
    for comp in islands[1:]:
        if len(comp) > 0.05 * len(verts):
            continue
        cen = sum((verts[i] for i in comp), Vector()) / len(comp)
        best = None
        for side, (a0, b0) in arms.items():
            d, t = seg_dist(cen, a0, b0)
            if best is None or d < best[1]:
                best = (side, d, max(0.16, t))
        if best is not None and best[1] < 0.18 * S:
            for i in comp:
                sleeve_info[i] = best
            print("COAT island of %d verts reattached to the %s arm" % (len(comp), best[0]))
        else:
            drop.extend(comp)
    if drop:
        bm = bmesh.new(); bm.from_mesh(coat.data); bm.verts.ensure_lookup_table()
        dset = set(drop)
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i in drop], context="VERTS")
        bm.to_mesh(coat.data); bm.free(); coat.data.update()
        keep2 = [i for i in range(len(verts)) if i not in dset]
        verts = [verts[i] for i in keep2]
        sleeve_info = [sleeve_info[i] for i in keep2]
        print("COAT dropped %d stray verts" % len(drop))
    print("COAT sleeves: %d left, %d right verts" % (sum(1 for x in sleeve_info if x and x[0] == "Left"), sum(1 for x in sleeve_info if x and x[0] == "Right")))

    def ang(p):
        return math.atan2(p.x - ax, p.y - ay)            # 0 = back (+Y), pi/2 = character's left

    def angdiff(a, b):
        d = abs(a - b) % (2 * math.pi)
        return min(d, 2 * math.pi - d)

    chain_list = []                                       # (name, parent bone, joints, kind, data)
    for k in range(COAT_CHAINS):
        th = 2 * math.pi * k / COAT_CHAINS
        sector = [p for p, sl in zip(verts, sleeve_info) if sl is None and p.z < z_root + 0.02 * S and angdiff(ang(p), th) < math.pi / COAT_CHAINS]
        if len(sector) < 60:
            continue
        zmin = min(p.z for p in sector) + 0.01 * S
        joints = []
        for j in range(COAT_SEGMENTS + 1):
            z = z_root + (zmin - z_root) * j / COAT_SEGMENTS
            ring = [p for p in sector if abs(p.z - z) < 0.03 * S]
            if len(ring) < 4:
                break
            radii = sorted(math.hypot(p.x - ax, p.y - ay) for p in ring)
            r = radii[len(radii) // 2]
            joints.append(Vector((ax + r * math.sin(th), ay + r * math.cos(th), z)))
        if len(joints) >= 3:
            chain_list.append(("CoatChain_%02d" % k, "Spine1", joints, "torso", th))
    # Sleeves are worn on the arms (skinned to upper arm / forearm / hand below), not simulated as chains.
    print("COAT chains:", len(chain_list), [c[0] for c in chain_list])

    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    for name, parent, joints, kind, _ in chain_list:
        prev = None
        for j in range(len(joints) - 1):
            b = eb.new("%s_%d" % (name, j))
            b.head = joints[j]
            b.tail = joints[j + 1]
            b.parent = eb[parent] if prev is None else prev
            b.use_connect = prev is not None
            prev = b
    bpy.ops.object.mode_set(mode="OBJECT")

    # ------------------------------------------------------------ weights: torso above the chest line, chains below
    groups = {}
    def group(name):
        if name not in groups:
            groups[name] = coat.vertex_groups.new(name=name)
        return groups[name]

    torso_chains = [c for c in chain_list if c[3] == "torso"]

    def along_weights(name, joints, s_pos):
        """Weights over a chain's bones for a position measured in bone units from its root."""
        n = len(joints) - 1
        s_pos = max(0.0, min(n - 1e-3, s_pos))
        out = {}
        for b in range(n):
            w = max(0.0, 1.0 - abs(s_pos - (b + 0.5)))
            if w > 0:
                out["%s_%d" % (name, b)] = w
        tot = sum(out.values()) or 1.0
        return {k: v / tot for k, v in out.items()}

    for v, p, sl in zip(coat.data.vertices, verts, sleeve_info):
        # torso part (two nearest torso bones)
        ds = sorted(((seg_dist(p, *segs[b])[0], b) for b in torso))[:2]
        w0 = 1.0 / max(1e-4, ds[0][0]) ** 2; w1 = 1.0 / max(1e-4, ds[1][0]) ** 2
        torso_w = {ds[0][1]: w0 / (w0 + w1), ds[1][1]: w1 / (w0 + w1)}
        weights = torso_w
        if sl is not None:
            side = sl[0]
            limb = [side + "Arm", side + "ForeArm", side + "Hand", side + "Shoulder"]
            ls = sorted(((seg_dist(p, rig.matrix_world @ bones[b].head_local, rig.matrix_world @ bones[b].tail_local)[0], b) for b in limb))[:2]
            a0 = 1.0 / max(1e-4, ls[0][0]) ** 2; a1 = 1.0 / max(1e-4, ls[1][0]) ** 2
            weights = {ls[0][1]: a0 / (a0 + a1), ls[1][1]: a1 / (a0 + a1)}
        elif torso_chains and p.z < z_root + 0.04 * S:
            th = ang(p)
            near = sorted(torso_chains, key=lambda c: angdiff(th, c[4]))[:2]
            d0 = angdiff(th, near[0][4]); d1 = angdiff(th, near[1][4]) if len(near) > 1 else 9.0
            mix = {}
            for c, wa in ((near[0], d1 / (d0 + d1)), (near[1], d0 / (d0 + d1))) if len(near) > 1 and d1 < 2 * math.pi / COAT_CHAINS * 1.5 else ((near[0], 1.0),):
                zr, ze = c[2][0].z, c[2][-1].z
                s_pos = (zr - p.z) / max(1e-4, zr - ze) * (len(c[2]) - 1)
                for k, w in along_weights(c[0], c[2], s_pos).items():
                    mix[k] = mix.get(k, 0.0) + w * wa
            blend = max(0.0, min(1.0, (z_root + 0.04 * S - p.z) / (0.12 * S)))   # soft seam at the chest line
            weights = {k: w * (1 - blend) for k, w in torso_w.items()}
            for k, w in mix.items():
                weights[k] = weights.get(k, 0.0) + w * blend
        top = sorted(weights.items(), key=lambda kv: -kv[1])[:4]      # Unity skins with 4 influences
        tot = sum(w for _, w in top) or 1.0
        for name, w in top:
            if w > 1e-4:
                group(name).add([v.index], w / tot, "REPLACE")
    coat.parent = rig
    mod = coat.modifiers.new("Armature", "ARMATURE")
    mod.object = rig

    # Coat material carrying its texture (Unity remaps materials, but keep the link for previews).
    tex_path = COAT.replace("Coat.fbx", "CoatTexture.png")
    mat = bpy.data.materials.new("Coat")
    mat.use_nodes = True
    img = mat.node_tree.nodes.new("ShaderNodeTexImage")
    try:
        img.image = bpy.data.images.load(tex_path)
        mat.node_tree.links.new(img.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    except Exception as e:
        print("COAT texture not linked:", e)
    coat.data.materials.clear()
    coat.data.materials.append(mat)
    print("COAT verts", len(coat.data.vertices), "polys", len(coat.data.polygons))
    render_preview("coat")

# ---------------------------------------------------------------- export for Unity
bpy.ops.object.select_all(action="SELECT")
for o in bpy.context.scene.objects:
    if o.type == "CAMERA":
        o.select_set(False)
bpy.ops.export_scene.fbx(
    filepath=OUT, use_selection=True, object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", bake_anim=False,
    mesh_smooth_type="FACE", use_armature_deform_only=True, path_mode="COPY", embed_textures=False)
print("EXPORTED", OUT)
