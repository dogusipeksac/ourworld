"""
Blender 5.3 headless script: Sculpts living Istanbul neighborhood props for GRIDROAM v24:
  1. Istanbul_StreetCat.fbx — Cute stylized Turkish street cat (body, arched tail, pointed ears,
     muzzle, eyes, paws) ready for runtime fur tinting (Sarman ginger, Tekir tabby, Pamuk white, Kömür black).
  2. Sultanbeyli_StreetLamp.fbx — Authentic curved Istanbul/AYEDAŞ galvanized streetlamp pole
     with luminaire housing and glowing LED lens for warm night-time URP Bloom pools.
"""
import math
import os
import bpy
from mathutils import Vector

OUT_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/Assets/GRIDROAM/Art/Models/Blender"
BLEND_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/scripts/blender/blend_sources"
PREVIEW_IMG = "/Users/dogusipeksac/.gemini/antigravity/brain/2dd72233-235e-4c47-8c3c-78844f1a7f7f/blender_v24_cats_lamps.png"
os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(BLEND_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def get_mat(name, rgba, rough=0.65, metal=0.0, emission=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name=name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = metal
        if emission > 0.0 and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = rgba
            bsdf.inputs["Emission Strength"].default_value = emission
    return m


def add_box(name, loc, size, mat, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0.0:
        mod = o.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier="Bevel")
    o.data.materials.append(mat)
    return o


def add_sphere(name, loc, scale, mat, segs=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, radius=1.0, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    for p in o.data.polygons:
        p.use_smooth = True
    o.data.materials.append(mat)
    return o


def add_cyl(name, loc, rad, depth, mat, rot=(0, 0, 0), verts=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=rad, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    for p in o.data.polygons:
        p.use_smooth = True
    o.data.materials.append(mat)
    return o


def add_cone(name, loc, r1, r2, depth, mat, rot=(0, 0, 0), verts=8):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(mat)
    return o


def export_fbx(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    out_path = os.path.join(OUT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        path_mode="AUTO",
    )
    tris = sum(len(o.data.polygons) * 2 for o in objs if o.type == "MESH")
    print(f"Exported {filename} ({len(objs)} meshes, ~{tris} tris)")


def build_istanbul_street_cat():
    """Facing +Y in Blender -> +Z in Unity after export! Standing cat ~0.34m tall, 0.56m long."""
    fur = get_mat("CatFur_Warm", (0.86, 0.56, 0.28, 1.0), rough=0.82)
    belly = get_mat("CreamPaint", (0.95, 0.91, 0.84, 1.0), rough=0.85)
    dark = get_mat("TrimDark", (0.14, 0.14, 0.15, 1.0), rough=0.45)
    nose = get_mat("Terracotta", (0.86, 0.48, 0.46, 1.0), rough=0.55)
    collar = get_mat("AccentTeal", (0.14, 0.62, 0.58, 1.0), rough=0.5)
    bell = get_mat("GoldBrass", (0.90, 0.72, 0.24, 1.0), rough=0.25, metal=0.85)

    parts = []
    # Plump torso
    parts.append(add_sphere("Cat_Body", (0.0, 0.0, 0.21), (0.115, 0.21, 0.105), fur))
    parts.append(add_sphere("Cat_Chest", (0.0, 0.13, 0.22), (0.095, 0.10, 0.095), belly))

    # Head & cute muzzle
    parts.append(add_sphere("Cat_Head", (0.0, 0.24, 0.31), (0.092, 0.092, 0.085), fur))
    parts.append(add_sphere("Cat_Muzzle", (0.0, 0.315, 0.285), (0.048, 0.036, 0.032), belly))
    parts.append(add_sphere("Cat_Nose", (0.0, 0.345, 0.295), (0.016, 0.012, 0.012), nose, segs=8, rings=6))

    # Expressive dark eyes
    parts.append(add_sphere("Cat_EyeL", (-0.040, 0.312, 0.325), (0.015, 0.012, 0.017), dark, segs=8, rings=6))
    parts.append(add_sphere("Cat_EyeR", (0.040, 0.312, 0.325), (0.015, 0.012, 0.017), dark, segs=8, rings=6))

    # Pointed ears
    parts.append(add_cone("Cat_EarL", (-0.056, 0.235, 0.395), 0.036, 0.004, 0.072, fur, rot=(0.08, -0.22, 0.0)))
    parts.append(add_cone("Cat_EarR", (0.056, 0.235, 0.395), 0.036, 0.004, 0.072, fur, rot=(0.08, 0.22, 0.0)))

    # Little neighborhood collar + gold bell
    parts.append(add_cyl("Cat_Collar", (0.0, 0.185, 0.265), 0.072, 0.022, collar, rot=(math.radians(48), 0, 0), verts=14))
    parts.append(add_sphere("Cat_Bell", (0.0, 0.245, 0.232), (0.016, 0.016, 0.016), bell, segs=8, rings=6))

    # 4 legs + white sock paws
    leg_coords = [
        ("FL", -0.058, 0.125),
        ("FR", 0.058, 0.125),
        ("BL", -0.062, -0.125),
        ("BR", 0.062, -0.125),
    ]
    for tag, lx, ly in leg_coords:
        parts.append(add_cyl(f"Cat_Leg_{tag}", (lx, ly, 0.095), 0.028, 0.17, fur, verts=10))
        parts.append(add_sphere(f"Cat_Paw_{tag}", (lx, ly + 0.012, 0.020), (0.032, 0.038, 0.022), belly, segs=8, rings=6))

    # Happy curved upright tail (classic friendly Istanbul cat greeting!)
    tail_pts = [
        (0.0, -0.19, 0.23, 0.024, math.radians(-55)),
        (0.0, -0.25, 0.30, 0.022, math.radians(-28)),
        (0.0, -0.27, 0.39, 0.020, math.radians(-5)),
        (0.0, -0.24, 0.46, 0.018, math.radians(28)),
    ]
    for idx, (tx, ty, tz, r, pitch) in enumerate(tail_pts):
        parts.append(add_cyl(f"Cat_Tail_{idx}", (tx, ty, tz), r, 0.105, fur, rot=(pitch, 0, 0), verts=8))
    parts.append(add_sphere("Cat_TailTip", (0.0, -0.21, 0.495), (0.019, 0.019, 0.019), belly, segs=8, rings=6))

    return parts


def build_sultanbeyli_streetlamp():
    """6.2m tall authentic Istanbul galvanized curved streetlamp pole with LED luminaire."""
    pole_metal = get_mat("Chrome", (0.58, 0.61, 0.64, 1.0), rough=0.42, metal=0.72)
    base_dark = get_mat("TrimDark", (0.20, 0.22, 0.24, 1.0), rough=0.55, metal=0.35)
    head_casing = get_mat("WhitePVC", (0.76, 0.78, 0.80, 1.0), rough=0.38, metal=0.25)
    glow = get_mat("LitScreen", (1.0, 0.90, 0.68, 1.0), rough=0.15, emission=3.5)

    parts = []
    # Concrete/metal anchor flange & lower tapered column
    parts.append(add_cyl("Lamp_Base", (0.0, 0.0, 0.22), 0.17, 0.44, base_dark, verts=12))
    parts.append(add_cone("Lamp_Shaft", (0.0, 0.0, 2.95), 0.095, 0.058, 5.45, pole_metal, verts=12))

    # Curved AYEDAŞ overhang arm extending toward +Y (roadside in Unity +Z)
    parts.append(add_cyl("Lamp_Arm1", (0.0, 0.36, 5.82), 0.044, 0.82, pole_metal, rot=(math.radians(64), 0, 0), verts=10))
    parts.append(add_cyl("Lamp_Arm2", (0.0, 1.05, 6.08), 0.038, 0.78, pole_metal, rot=(math.radians(82), 0, 0), verts=10))

    # Modern Istanbul cobra-head LED housing + glowing warm lens underneath
    parts.append(add_box("Lamp_HeadHousing", (0.0, 1.48, 6.14), (0.34, 0.66, 0.13), head_casing, bevel=0.03))
    parts.append(add_box("Lamp_HeadLens", (0.0, 1.50, 6.065), (0.26, 0.46, 0.04), glow, bevel=0.008))

    return parts


def render_preview(cat_objs, lamp_objs):
    # Offset cat next to lamp base for a clean studio preview
    for o in cat_objs:
        o.location.x -= 1.05
    for o in lamp_objs:
        o.location.x += 0.95

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.filepath = PREVIEW_IMG

    world = bpy.data.worlds.new("Studio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.17, 0.22, 1.0)
        bg.inputs[1].default_value = 0.9

    bpy.ops.object.light_add(type="SUN", location=(4, -4, 8), rotation=(math.radians(52), 0, math.radians(38)))
    bpy.context.active_object.data.energy = 3.2

    bpy.ops.object.camera_add(location=(0.2, -5.6, 2.2), rotation=(math.radians(78), 0, 0))
    scene.camera = bpy.context.active_object

    bpy.ops.render.render(write_still=True)
    print("Preview saved:", PREVIEW_IMG)


def main():
    clear_scene()
    cat_objs = build_istanbul_street_cat()
    export_fbx(cat_objs, "Istanbul_StreetCat.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "Istanbul_StreetCat.blend"))

    clear_scene()
    lamp_objs = build_sultanbeyli_streetlamp()
    export_fbx(lamp_objs, "Sultanbeyli_StreetLamp.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "Sultanbeyli_StreetLamp.blend"))

    # Combined preview
    clear_scene()
    c = build_istanbul_street_cat()
    l = build_sultanbeyli_streetlamp()
    render_preview(c, l)


if __name__ == "__main__":
    main()
