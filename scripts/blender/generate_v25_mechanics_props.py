"""
Blender 5.3 headless script: Sculpts interactive gameplay props for GRIDROAM v25:
  1. Istanbul_FootballGoal.fbx — Mahalle maçı white metal goal frame (3.2m wide x 2.0m tall) with rear net bars.
  2. Istanbul_ATM.fbx — Illuminated Turkish neighborhood bank ATM kiosk (blue/teal canopy, lit screen, keypad, card slot).
  3. Istanbul_WaterJug.fbx — Classic Turkish 19L blue polycarbonate Damacana water bottle (for Grab & Throw physics!).
"""
import math
import os
import bpy

OUT_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/Assets/GRIDROAM/Art/Models/Blender"
BLEND_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/scripts/blender/blend_sources"
os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(BLEND_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def get_mat(name, rgba, rough=0.55, metal=0.0, emission=0.0):
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


def add_cyl(name, loc, rad, depth, mat, rot=(0, 0, 0), verts=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=rad, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    for p in o.data.polygons:
        p.use_smooth = True
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
    print(f"Exported {filename} ({len(objs)} meshes)")


def build_football_goal():
    """Opening faces +Y in Blender -> +Z in Unity. Width 3.4m, height 2.1m, depth 1.25m."""
    white = get_mat("WhitePVC", (0.95, 0.96, 0.98, 1.0), rough=0.35, metal=0.2)
    net_mat = get_mat("Chrome", (0.82, 0.85, 0.88, 1.0), rough=0.6)
    red_stripe = get_mat("SignRed", (0.82, 0.14, 0.14, 1.0), rough=0.4)

    parts = []
    # Front posts & crossbar
    parts.append(add_cyl("Goal_PostL", (-1.70, 0.0, 1.05), 0.055, 2.10, white))
    parts.append(add_cyl("Goal_PostR", (1.70, 0.0, 1.05), 0.055, 2.10, white))
    parts.append(add_cyl("Goal_Crossbar", (0.0, 0.0, 2.10), 0.055, 3.50, white, rot=(0, math.radians(90), 0)))
    # Turkish mahalle red stripes on posts
    for sx in (-1.70, 1.70):
        parts.append(add_cyl(f"Goal_Stripe_{sx}", (sx, 0.0, 1.55), 0.058, 0.22, red_stripe))

    # Rear support stanchions & ground frame
    parts.append(add_cyl("Goal_BackBase", (0.0, -1.25, 0.04), 0.04, 3.44, white, rot=(0, math.radians(90), 0)))
    for sx in (-1.70, 1.70):
        parts.append(add_cyl(f"Goal_SideBase_{sx}", (sx, -0.625, 0.04), 0.04, 1.28, white, rot=(math.radians(90), 0, 0)))
        parts.append(add_cyl(f"Goal_TopSide_{sx}", (sx, -0.45, 2.02), 0.036, 0.92, white, rot=(math.radians(82), 0, 0)))
        parts.append(add_cyl(f"Goal_BackDiag_{sx}", (sx, -1.06, 1.02), 0.036, 2.04, white, rot=(math.radians(-11), 0, 0)))

    # Crisp net mesh cords (vertical & horizontal thin ropes)
    for i in range(-7, 8):
        x = i * 0.22
        parts.append(add_cyl(f"Net_V_{i}", (x, -1.06, 1.04), 0.008, 2.02, net_mat, rot=(math.radians(-11), 0, 0), verts=6))
    for j in range(1, 7):
        z = j * 0.30
        y = -1.24 + (z / 2.1) * 0.36
        parts.append(add_cyl(f"Net_H_{j}", (0.0, y, z), 0.008, 3.38, net_mat, rot=(0, math.radians(90), 0), verts=6))
    # Side net cords
    for sx in (-1.70, 1.70):
        for j in range(1, 7):
            z = j * 0.30
            parts.append(add_cyl(f"Net_Side_{sx}_{j}", (sx, -0.55, z), 0.008, 1.12, net_mat, rot=(math.radians(90), 0, 0), verts=6))

    return parts


def build_atm_kiosk():
    """Faces +Y in Blender -> +Z in Unity. 1.05m wide, 2.18m tall, 0.78m deep."""
    body_blue = get_mat("CobaltBlue", (0.10, 0.28, 0.56, 1.0), rough=0.35, metal=0.25)
    dark = get_mat("TrimDark", (0.14, 0.16, 0.20, 1.0), rough=0.45, metal=0.35)
    silver = get_mat("Chrome", (0.72, 0.75, 0.78, 1.0), rough=0.32, metal=0.7)
    screen = get_mat("LitScreen", (0.34, 0.84, 1.0, 1.0), rough=0.12, emission=2.8)
    accent = get_mat("AccentTeal", (0.10, 0.68, 0.62, 1.0), rough=0.35)

    parts = []
    parts.append(add_box("ATM_Body", (0.0, 0.0, 1.04), (1.02, 0.74, 2.08), body_blue, bevel=0.03))
    parts.append(add_box("ATM_BasePlinth", (0.0, 0.02, 0.08), (1.08, 0.80, 0.16), dark))
    parts.append(add_box("ATM_TopCanopy", (0.0, 0.14, 2.12), (1.12, 1.00, 0.22), accent, bevel=0.02))
    parts.append(add_box("ATM_SignStrip", (0.0, 0.645, 2.12), (0.86, 0.03, 0.13), screen))

    # Recessed metallic fascia, glowing screen, stainless keypad shelf & cash dispenser slot
    parts.append(add_box("ATM_Fascia", (0.0, 0.36, 1.24), (0.76, 0.06, 0.94), silver))
    parts.append(add_box("ATM_Screen", (0.0, 0.395, 1.44), (0.52, 0.02, 0.38), screen))
    parts.append(add_box("ATM_KeypadShelf", (0.0, 0.44, 1.06), (0.68, 0.20, 0.06), dark))
    parts.append(add_box("ATM_Keypad", (0.0, 0.46, 1.095), (0.34, 0.14, 0.02), silver))
    parts.append(add_box("ATM_CardSlot", (0.24, 0.395, 1.22), (0.12, 0.04, 0.05), accent))
    parts.append(add_box("ATM_CashSlot", (0.0, 0.395, 0.88), (0.34, 0.04, 0.09), dark))

    return parts


def build_damacana():
    """Classic Turkish 19L blue polycarbonate Damacana water jug (~0.50m tall, 0.27m diameter)."""
    jug_blue = get_mat("Water", (0.28, 0.68, 0.94, 1.0), rough=0.18)
    cap_blue = get_mat("CobaltBlue", (0.08, 0.26, 0.64, 1.0), rough=0.4)

    parts = []
    parts.append(add_cyl("Damacana_Body", (0.0, 0.0, 0.19), 0.135, 0.34, jug_blue, verts=16))
    parts.append(add_cyl("Damacana_Ring1", (0.0, 0.0, 0.11), 0.140, 0.025, jug_blue, verts=16))
    parts.append(add_cyl("Damacana_Ring2", (0.0, 0.0, 0.25), 0.140, 0.025, jug_blue, verts=16))
    parts.append(add_cyl("Damacana_Shoulder", (0.0, 0.0, 0.40), 0.095, 0.10, jug_blue, verts=16))
    parts.append(add_cyl("Damacana_Neck", (0.0, 0.0, 0.475), 0.036, 0.07, jug_blue, verts=12))
    parts.append(add_cyl("Damacana_Cap", (0.0, 0.0, 0.52), 0.040, 0.032, cap_blue, verts=12))
    return parts


def main():
    clear_scene()
    g = build_football_goal()
    export_fbx(g, "Istanbul_FootballGoal.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "Istanbul_FootballGoal.blend"))

    clear_scene()
    a = build_atm_kiosk()
    export_fbx(a, "Istanbul_ATM.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "Istanbul_ATM.blend"))

    clear_scene()
    d = build_damacana()
    export_fbx(d, "Istanbul_WaterJug.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "Istanbul_WaterJug.blend"))


if __name__ == "__main__":
    main()
