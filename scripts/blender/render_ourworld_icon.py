"""
OurWorld — Handcrafted 3D Studio App Icon Renderer (Blender 5.3 + Cycles)
Renders a bold, tactile, Apple-Design-Award-style isometric floating mini-world diorama
with crisp beveled geometry, zero Z-fighting, vibrant studio colors, and real ray-traced shadows.
Zero AI artifacts — 100% authentic 3D game studio icon!
"""

import math
import os
import bpy
from mathutils import Vector

PROJECT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_RAW = os.path.join(PROJECT_DIR, "StoreAssets", "_ourworld_icon_render.png")


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.38, metallic=0.0, emission=None, emission_strength=0.0, coat=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
    if emission is not None and emission_strength > 0.0:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
            bsdf.inputs["Emission Strength"].default_value = emission_strength

    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def add_bevel(obj, width=0.04, segments=3):
    mod = obj.modifiers.new(name="Bevel", type="BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(30)


def add_box(name, loc, scale, mat, rot=(0, 0, 0), bevel=0.04, bevel_seg=3):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        add_bevel(obj, bevel, bevel_seg)
    obj.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return obj


def add_cyl(name, loc, radius, depth, mat, verts=36, rot=(0, 0, 0), bevel=0.02):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    if bevel > 0:
        add_bevel(obj, bevel, 2)
    obj.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return obj


def add_sphere(name, loc, radius, mat, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=36, ring_count=22, radius=radius, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return obj


def build_diorama():
    # Punchy, saturated, tactile Nintendo/Supercell/Monument-Valley palette (calibrated for AgX!)
    m_grass      = make_mat("Grass",      (0.06, 0.56, 0.20), roughness=0.52)
    m_grass_hi   = make_mat("GrassHi",    (0.10, 0.68, 0.22), roughness=0.45, coat=0.15)
    m_grass_dk   = make_mat("GrassDk",    (0.03, 0.42, 0.16), roughness=0.55)
    m_soil       = make_mat("Soil",       (0.32, 0.14, 0.06), roughness=0.70)
    m_stone_base = make_mat("StoneBase",  (0.06, 0.14, 0.26), roughness=0.35, coat=0.35)
    m_asphalt    = make_mat("Asphalt",    (0.09, 0.11, 0.15), roughness=0.62)
    m_curb       = make_mat("Curb",       (0.86, 0.88, 0.90), roughness=0.45)
    m_dash       = make_mat("Dash",       (1.00, 0.76, 0.08), roughness=0.30)

    m_cream      = make_mat("Cream",      (0.95, 0.87, 0.72), roughness=0.42)
    m_coral      = make_mat("Coral",      (0.88, 0.22, 0.12), roughness=0.40)
    m_roof_tile  = make_mat("RoofTile",   (0.68, 0.10, 0.06), roughness=0.38, coat=0.20)
    m_roof_teal  = make_mat("RoofTeal",   (0.02, 0.50, 0.56), roughness=0.26, coat=0.55)
    m_stone_lt   = make_mat("StoneLt",    (0.84, 0.76, 0.62), roughness=0.45)
    m_gold       = make_mat("Gold",       (1.00, 0.68, 0.06), roughness=0.20, metallic=0.82)
    m_win_glow   = make_mat("WinGlow",    (1.00, 0.78, 0.24), roughness=0.15, emission=(1.0, 0.72, 0.18), emission_strength=3.6)
    m_glass      = make_mat("Glass",      (0.05, 0.16, 0.28), roughness=0.08, metallic=0.20, coat=0.95)
    m_wood       = make_mat("Wood",       (0.36, 0.16, 0.06), roughness=0.60)

    m_car_teal   = make_mat("CarTeal",    (0.02, 0.74, 0.66), roughness=0.16, metallic=0.22, coat=1.0)
    m_car_wheel  = make_mat("CarWheel",   (0.05, 0.06, 0.08), roughness=0.55)
    m_hubcap     = make_mat("Hubcap",     (0.86, 0.88, 0.92), roughness=0.22, metallic=0.75)
    m_headlight  = make_mat("Headlight",  (1.00, 0.94, 0.72), roughness=0.12, emission=(1.0, 0.92, 0.58), emission_strength=6.0)
    m_taillight  = make_mat("Taillight",  (0.98, 0.10, 0.08), roughness=0.15, emission=(1.0, 0.10, 0.08), emission_strength=4.5)
    m_white      = make_mat("White",      (0.98, 0.98, 0.99), roughness=0.26, coat=0.35)

    # 1. Floating Rounded Isometric World Tile Base (crisp layered island!)
    add_box("Base_StonePedestal", (0, 0, -0.50), (4.62, 4.62, 0.36), m_stone_base, bevel=0.22, bevel_seg=4)
    add_box("Base_SoilLayer",     (0, 0, -0.22), (4.84, 4.84, 0.40), m_soil,       bevel=0.18, bevel_seg=4)
    add_box("Base_GrassTop",      (0, 0,  0.04), (5.00, 5.00, 0.26), m_grass,      bevel=0.14, bevel_seg=4)

    # 2. ZERO-OVERLAP L-Shaped Avenue (Front X-road + Right Y-road cleanly segmented!)
    # Front road full width along X at Y = -1.22 (top surface at Z = 0.20)
    add_box("Road_Front", (0.0, -1.22, 0.175), (4.94, 1.08, 0.05), m_asphalt, bevel=0.015, bevel_seg=2)
    # Right road split cleanly NORTH of front road (Y = -0.68 .. +2.44 -> center +0.88, len 3.12) and SOUTH stub (-2.44..-1.76 -> center -2.10, len 0.68)
    add_box("Road_RightNorth", (1.35,  0.88, 0.175), (1.08, 3.12, 0.05), m_asphalt, bevel=0.015, bevel_seg=2)
    add_box("Road_RightSouth", (1.35, -2.10, 0.175), (1.08, 0.68, 0.05), m_asphalt, bevel=0.015, bevel_seg=2)

    # Clean non-overlapping sidewalks/curbs
    add_box("Curb_FrontN_W", (-0.82, -0.64, 0.195), (3.18, 0.12, 0.08), m_curb, bevel=0.02)
    add_box("Curb_FrontN_E", ( 2.20, -0.64, 0.195), (0.56, 0.12, 0.08), m_curb, bevel=0.02)
    add_box("Curb_FrontS_W", (-0.82, -1.80, 0.195), (3.18, 0.12, 0.08), m_curb, bevel=0.02)
    add_box("Curb_FrontS_E", ( 2.20, -1.80, 0.195), (0.56, 0.12, 0.08), m_curb, bevel=0.02)
    add_box("Curb_RightW",   ( 0.77,  0.90, 0.195), (0.12, 3.06, 0.08), m_curb, bevel=0.02)
    add_box("Curb_RightE",   ( 1.93,  0.90, 0.195), (0.12, 3.06, 0.08), m_curb, bevel=0.02)

    # Crisp golden road center dashes + white pedestrian crosswalk stripes at the intersection!
    for dx in (-1.90, -1.10, -0.30, 0.42):
        add_box(f"DashX_{dx}", (dx, -1.22, 0.204), (0.42, 0.09, 0.012), m_dash, bevel=0.004)
    for dy in (-0.15, 0.62, 1.38, 2.08):
        add_box(f"DashY_{dy}", (1.35, dy, 0.204), (0.09, 0.42, 0.012), m_dash, bevel=0.004)
    for ci in (-0.34, -0.11, 0.12, 0.35):
        add_box(f"ZebraN_{ci}", (1.35 + ci, -0.54, 0.204), (0.11, 0.30, 0.012), m_white, bevel=0.004)
        add_box(f"ZebraW_{ci}", (0.68, -1.22 + ci, 0.204), (0.30, 0.11, 0.012), m_white, bevel=0.004)

    # 3. HERO LANDMARK 1: Iconic Clock Tower (tall golden-domed landmark silhouette!)
    tx, ty = -1.15, 1.05
    add_box("Tower_Plinth", (tx, ty, 0.30), (1.38, 1.38, 0.28), m_stone_lt, bevel=0.05)
    add_box("Tower_Shaft",  (tx, ty, 1.50), (1.00, 1.00, 2.22), m_cream,    bevel=0.06)
    add_box("Tower_Band",   (tx, ty, 1.48), (1.08, 1.08, 0.12), m_stone_lt, bevel=0.03)
    add_box("Tower_Cornice",(tx, ty, 2.64), (1.22, 1.22, 0.20), m_stone_lt, bevel=0.05)
    # Clock faces on front (-Y) and right (+X)
    add_cyl("Clock_FaceY", (tx, ty - 0.51, 2.06), 0.35, 0.08, m_gold, rot=(math.radians(90), 0, 0), bevel=0.015)
    add_cyl("Clock_DialY", (tx, ty - 0.54, 2.06), 0.27, 0.05, m_white, rot=(math.radians(90), 0, 0), bevel=0.01)
    add_box("Clock_HandY1",(tx, ty - 0.57, 2.12), (0.048, 0.03, 0.18), m_stone_base, bevel=0.005)
    add_box("Clock_HandY2",(tx + 0.075, ty - 0.57, 2.06), (0.15, 0.03, 0.048), m_stone_base, bevel=0.005)

    add_cyl("Clock_FaceX", (tx + 0.51, ty, 2.06), 0.35, 0.08, m_gold, rot=(0, math.radians(90), 0), bevel=0.015)
    add_cyl("Clock_DialX", (tx + 0.54, ty, 2.06), 0.27, 0.05, m_white, rot=(0, math.radians(90), 0), bevel=0.01)
    add_box("Clock_HandX1",(tx + 0.57, ty, 2.12), (0.03, 0.048, 0.18), m_stone_base, bevel=0.005)
    add_box("Clock_HandX2",(tx + 0.57, ty + 0.075, 2.06), (0.03, 0.15, 0.048), m_stone_base, bevel=0.005)

    add_box("Tower_Door", (tx, ty - 0.50, 0.72), (0.38, 0.08, 0.64), m_win_glow, bevel=0.04)
    add_sphere("Tower_Dome", (tx, ty, 2.82), 0.54, m_roof_teal, scale=(1.0, 1.0, 0.94))
    add_cyl("Tower_Spire",   (tx, ty, 3.46), 0.06, 0.52, m_gold, bevel=0.01)
    add_sphere("Tower_Orb",  (tx, ty, 3.72), 0.13, m_gold)

    # 4. HERO BUILDING 2: Warm Terracotta-Coral Home (center-right)
    hx, hy = 0.06, 0.56
    add_box("House_Body",     (hx, hy, 0.90), (1.30, 1.24, 1.48), m_coral, bevel=0.06)
    add_box("House_RoofBase", (hx, hy, 1.70), (1.48, 1.42, 0.20), m_roof_tile, bevel=0.05)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1.02, radius2=0.14, depth=0.64,
                                    location=(hx, hy, 2.08), rotation=(0, 0, math.radians(45)))
    roof = bpy.context.active_object
    roof.name = "House_RoofTop"
    add_bevel(roof, 0.04, 2)
    roof.data.materials.append(m_roof_tile)
    bpy.ops.object.shade_smooth()

    for wx in (-0.32, 0.32):
        add_box(f"House_WinF_{wx}",  (hx + wx, hy - 0.62, 1.20), (0.34, 0.08, 0.36), m_white, bevel=0.02)
        add_box(f"House_GlowF_{wx}", (hx + wx, hy - 0.65, 1.20), (0.24, 0.04, 0.26), m_win_glow, bevel=0.01)
    add_box("House_Door",   (hx, hy - 0.62, 0.52), (0.34, 0.08, 0.58), m_wood, bevel=0.02)
    add_box("House_Awning", (hx, hy - 0.73, 0.87), (0.60, 0.28, 0.10), m_roof_teal, rot=(math.radians(18), 0, 0), bevel=0.02)
    for wy in (-0.28, 0.28):
        add_box(f"House_WinR_{wy}",  (hx + 0.65, hy + wy, 1.20), (0.08, 0.32, 0.34), m_white, bevel=0.02)
        add_box(f"House_GlowR_{wy}", (hx + 0.68, hy + wy, 1.20), (0.04, 0.22, 0.24), m_win_glow, bevel=0.01)

    # 5. HERO BUILDING 3: Cozy Cream Corner Shop (front-left)
    sx, sy = -1.62, -0.02
    add_box("Shop_Body",    (sx, sy, 0.66), (1.10, 1.06, 0.98), m_cream, bevel=0.05)
    add_box("Shop_Parapet", (sx, sy, 1.18), (1.18, 1.14, 0.14), m_roof_teal, bevel=0.04)
    add_box("Shop_Window",  (sx, sy - 0.53, 0.56), (0.70, 0.08, 0.46), m_win_glow, bevel=0.02)
    add_box("Shop_Awning",  (sx, sy - 0.66, 0.89), (0.96, 0.34, 0.10), m_coral, rot=(math.radians(20), 0, 0), bevel=0.02)

    # 6. HERO VEHICLE: Glossy Mint-Teal Coupe cruising the front avenue!
    cx, cy, cz = -0.42, -1.20, 0.35
    add_box("Car_Lower", (cx, cy, cz + 0.14), (1.36, 0.68, 0.34), m_car_teal, bevel=0.09, bevel_seg=3)
    add_box("Car_Cabin", (cx - 0.08, cy, cz + 0.42), (0.76, 0.58, 0.30), m_car_teal, bevel=0.09, bevel_seg=3)
    add_box("Car_WindF", (cx + 0.26, cy, cz + 0.42), (0.14, 0.52, 0.24), m_glass, rot=(0, math.radians(-22), 0), bevel=0.02)
    add_box("Car_SideG", (cx - 0.08, cy, cz + 0.43), (0.56, 0.60, 0.19), m_glass, bevel=0.02)
    for wx in (-0.40, 0.40):
        for wy in (-0.32, 0.32):
            add_cyl(f"Wheel_{wx}_{wy}", (cx + wx, cy + wy, cz), 0.17, 0.12, m_car_wheel, rot=(math.radians(90), 0, 0), bevel=0.03)
            add_cyl(f"Hub_{wx}_{wy}",   (cx + wx, cy + wy + (0.05 if wy > 0 else -0.05), cz), 0.08, 0.04, m_hubcap, rot=(math.radians(90), 0, 0), bevel=0.01)
    for ly in (-0.21, 0.21):
        add_box(f"Headlight_{ly}", (cx + 0.68, cy + ly, cz + 0.16), (0.06, 0.15, 0.11), m_headlight, bevel=0.02)
        add_box(f"Taillight_{ly}", (cx - 0.68, cy + ly, cz + 0.16), (0.05, 0.15, 0.09), m_taillight, bevel=0.02)

    # 7. Crisp Emerald Studio Trees (bold, clean silhouettes!)
    trees = [
        ( 2.15,  1.65, 0.44),
        ( 2.12, -0.18, 0.38),
        (-0.22,  1.95, 0.44),
        (-2.10,  1.75, 0.38),
    ]
    for i, (tx2, ty2, rad) in enumerate(trees):
        add_cyl(f"Trunk_{i}", (tx2, ty2, 0.42), 0.09, 0.50, m_wood, bevel=0.01)
        add_sphere(f"CrownA_{i}", (tx2, ty2, 0.80 + rad * 0.42), rad, m_grass_hi, scale=(1.0, 1.0, 1.08))
        add_sphere(f"CrownB_{i}", (tx2 + rad * 0.40, ty2 - rad * 0.22, 0.72 + rad * 0.18), rad * 0.64, m_grass_dk)


def setup_studio_camera_and_lights():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 112
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    try:
        scene.view_settings.look = "AgX - High Contrast"
    except Exception:
        pass
    scene.view_settings.exposure = 0.26

    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 6.95
    cam_obj = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    cam_pos = Vector((7.4, -7.8, 6.3))
    target = Vector((0.06, -0.05, 0.96))
    cam_obj.location = cam_pos
    cam_obj.rotation_euler = (target - cam_pos).normalized().to_track_quat("-Z", "Y").to_euler()

    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.18, 0.36, 0.58, 1.0)
        bg.inputs["Strength"].default_value = 0.65

    def add_area(name, loc, target_pt, energy, size, color):
        ld = bpy.data.lights.new(name=name, type="AREA")
        ld.energy = energy
        ld.size = size
        ld.color = color
        lo = bpy.data.objects.new(name=name, object_data=ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = (Vector(target_pt) - Vector(loc)).normalized().to_track_quat("-Z", "Y").to_euler()

    # Crisp high-contrast studio key + cool sky fill + bright turquoise rim
    add_area("KeyLight",    ( 6.2, -5.2, 9.2), (0, 0, 0.8), 1450, 4.2, (1.00, 0.94, 0.80))
    add_area("FillLight",   (-7.2, -4.0, 5.2), (0, 0, 0.8),  320, 6.0, (0.55, 0.82, 1.00))
    add_area("RimLight",    (-4.5,  7.0, 7.6), (0, 0, 1.2),  920, 4.0, (0.30, 0.96, 0.92))
    add_area("BounceLight", ( 2.0, -3.0,-4.5), (0, 0, 0.0),  150, 6.0, (0.20, 0.60, 0.72))


def main():
    os.makedirs(os.path.dirname(OUT_RAW), exist_ok=True)
    reset_scene()
    build_diorama()
    setup_studio_camera_and_lights()
    bpy.context.scene.render.filepath = OUT_RAW
    bpy.ops.render.render(write_still=True)
    print(f"[OurWorld Icon] Raw Cycles render complete: {OUT_RAW}")


if __name__ == "__main__":
    main()
