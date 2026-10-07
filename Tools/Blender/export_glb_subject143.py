"""Export Subject_143_True as a textured, upright, 1.25 m GLB (input for Meshy auto-rigging).
blender -b --python Tools/Blender/export_glb_subject143.py -- <src.fbx> <texture.png> <out.glb> <height_m>"""
import bpy, sys
from mathutils import Vector

src, tex, out, height = sys.argv[sys.argv.index("--") + 1:][:4]
height = float(height)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
zs = [v.co.z for v in mesh.data.vertices]
s = height / (max(zs) - min(zs))
for v in mesh.data.vertices:
    v.co *= s
low = [v.co for v in mesh.data.vertices if v.co.z < min(zs) * s + 0.4 * height]
cx = sum(p.x for p in low) / len(low); cy = sum(p.y for p in low) / len(low); z0 = min(v.co.z for v in mesh.data.vertices)
for v in mesh.data.vertices:
    v.co -= Vector((cx, cy, z0))
mesh.data.update()

# Material with the Meshy colour texture.
mat = bpy.data.materials.new("Subject143")
mat.use_nodes = True
nodes = mat.node_tree.nodes
bsdf = nodes.get("Principled BSDF")
img = nodes.new("ShaderNodeTexImage")
img.image = bpy.data.images.load(tex)
mat.node_tree.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
mesh.data.materials.clear()
mesh.data.materials.append(mat)
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True, export_yup=True, export_apply=True)
print("EXPORTED", out, "verts", len(mesh.data.vertices))
