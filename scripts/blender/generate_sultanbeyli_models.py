#!/usr/bin/env python3
"""
GRIDROAM Sultanbeyli / 48 Selman Sokağı 3D Asset Generator (Blender 5.3+ Python)
Sculpts authentic Istanbul/Sultanbeyli neighborhood architecture directly from
48 Selman Sokağı (Hamidiye Mah., Sultanbeyli, İstanbul) Street View reference:
  1. Sultanbeyli_Selman48_House.fbx     (Red/Sage-green two-tone detached house, timber gable porch,
                                         barred PVC window, grapevine pergola, concrete garden wall,
                                         light-blue wrought-iron fence & double 'P' sliding driveway gate)
  2. Sultanbeyli_BrickConstruction.fbx  (3-story unfinished exposed hollow red-brick construction building
                                         with concrete floor slabs, open window bays & raw roof trusses)
  3. Sultanbeyli_YellowApartman.fbx     (3-story pale-yellow Sultanbeyli apartment with terracotta vertical
                                         window bands, satellite dish & hipped roof)
  4. Sultanbeyli_ModernApartman.fbx     (4-story white & olive-green modern apartment with glass balconies)
  5. Sultanbeyli_MahalleApartman.fbx    (3-story classic Istanbul neighborhood apartment + ground shop)
  6. Sultanbeyli_PowerPole_Steel.fbx    (Rusted steel lattice utility pole + ceramic insulators)
  7. Sultanbeyli_PowerPole_Wood.fbx     (Timber utility pole + crossarm & streetlamp)
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_FBX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")
BLEND_OUT_DIR = os.path.join(SCRIPT_DIR, "blend_sources")
PREVIEW_PATH = os.environ.get(
    "GRIDROAM_SULTANBEYLI_PREVIEW",
    os.path.join(SCRIPT_DIR, "blender_sultanbeyli_preview.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.58, metallic=0.0, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if emission is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def get_sultanbeyli_materials():
    return {
        "RedPlaster": make_mat("RedPlaster", (0.66, 0.21, 0.16), roughness=0.76),
        "SagePlaster": make_mat("SagePlaster", (0.64, 0.67, 0.56), roughness=0.76),
        "YellowPlaster": make_mat("YellowPlaster", (0.86, 0.78, 0.54), roughness=0.74),
        "OrangeStripe": make_mat("OrangeStripe", (0.74, 0.42, 0.20), roughness=0.72),
        "OliveFacade": make_mat("OliveFacade", (0.34, 0.43, 0.30), roughness=0.68),
        "CreamPaint": make_mat("CreamPaint", (0.90, 0.89, 0.85), roughness=0.70),
        "RawBrick": make_mat("RawBrick", (0.68, 0.31, 0.19), roughness=0.86),
        "Concrete": make_mat("Concrete", (0.58, 0.57, 0.54), roughness=0.84),
        "Wood": make_mat("Wood", (0.62, 0.41, 0.22), roughness=0.64),
        "DarkWood": make_mat("DarkWood", (0.28, 0.19, 0.12), roughness=0.72),
        "FenceBlue": make_mat("FenceBlue", (0.40, 0.56, 0.72), roughness=0.42, metallic=0.58),
        "WhitePVC": make_mat("WhitePVC", (0.94, 0.94, 0.93), roughness=0.35),
        "Glass": make_mat("Glass", (0.10, 0.15, 0.20), roughness=0.10, metallic=0.25),
        "DarkInterior": make_mat("DarkInterior", (0.06, 0.06, 0.07), roughness=0.92),
        "RoofTile": make_mat("RoofTile", (0.54, 0.25, 0.17), roughness=0.74),
        "RustSteel": make_mat("RustSteel", (0.36, 0.24, 0.18), roughness=0.74, metallic=0.55),
        "TrimDark": make_mat("TrimDark", (0.14, 0.15, 0.16), roughness=0.60, metallic=0.25),
        "VineBrown": make_mat("VineBrown", (0.30, 0.22, 0.16), roughness=0.90),
        "ShrubGreen": make_mat("ShrubGreen", (0.22, 0.36, 0.18), roughness=0.85),
        "DoorTeal": make_mat("DoorTeal", (0.12, 0.28, 0.36), roughness=0.45, metallic=0.20),
    }


def apply_smooth(obj, angle_deg=35.0):
    mesh = obj.data
    if not isinstance(mesh, bpy.types.Mesh):
        return
    for poly in mesh.polygons:
        poly.use_smooth = True
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.edges.ensure_lookup_table()
    thresh = math.radians(angle_deg)
    for edge in bm.edges:
        if len(edge.link_faces) == 2:
            edge.smooth = edge.calc_face_angle(0.0) < thresh
        else:
            edge.smooth = False
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def box_uv(obj, scale=0.45):
    mesh = obj.data
    if not isinstance(mesh, bpy.types.Mesh):
        return
    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_layer = bm.loops.layers.uv.verify()
    for face in bm.faces:
        n = face.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in face.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:
                u, v = co.x * scale, co.y * scale
            elif ax >= ay and ax >= az:
                u, v = co.y * scale, co.z * scale
            else:
                u, v = co.x * scale, co.z * scale
            loop[uv_layer].uv = (u, v)
    bm.to_mesh(mesh)
    bm.free()


def make_obj(name, bm, mat, smooth_angle=35.0, uv_scale=0.45):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if mat is not None:
        obj.data.materials.append(mat)
    box_uv(obj, scale=uv_scale)
    apply_smooth(obj, angle_deg=smooth_angle)
    return obj


def add_box(name, center, size, mat, bevel=0.025, segments=1, rot_euler=(0.0, 0.0, 0.0), uv_scale=0.45):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.2:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True)
    m = Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return make_obj(name, bm, mat, smooth_angle=35.0, uv_scale=uv_scale)


def add_cyl(name, center, r_bot, r_top, depth, segs, mat, rot_euler=(0.0, 0.0, 0.0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r_bot, radius2=r_top, depth=depth)
    m = Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return make_obj(name, bm, mat, smooth_angle=52.0)


def add_gable_prism(name, x_min, x_max, y_front, y_back, z_eave, z_peak, mat):
    """Triangular gable end extruded from y_back to y_front."""
    bm = bmesh.new()
    xm = (x_min + x_max) * 0.5
    v0 = bm.verts.new(Vector((x_min, y_front, z_eave)))
    v1 = bm.verts.new(Vector((x_max, y_front, z_eave)))
    v2 = bm.verts.new(Vector((xm, y_front, z_peak)))
    v3 = bm.verts.new(Vector((x_min, y_back, z_eave)))
    v4 = bm.verts.new(Vector((x_max, y_back, z_eave)))
    v5 = bm.verts.new(Vector((xm, y_back, z_peak)))
    bm.faces.new((v0, v1, v2))
    bm.faces.new((v4, v3, v5))
    bm.faces.new((v0, v3, v4, v1))
    bm.faces.new((v1, v4, v5, v2))
    bm.faces.new((v2, v5, v3, v0))
    return make_obj(name, bm, mat, smooth_angle=25.0)


def join_all(parts, final_name):
    bpy.ops.object.select_all(action="DESELECT")
    valid = [p for p in parts if p is not None]
    for p in valid:
        p.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = final_name
    obj.location = Vector((0.0, 0.0, 0.0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


# ---------------------------------------------------------------------------
# 1. 48 SELMAN SOKAĞI HERO HOUSE (Exact match to Street View reference!)
#    Front faces -Y (or +Y -> we orient front toward +Y so Unity +Z is street!)
# ---------------------------------------------------------------------------
def build_selman48_house(mats):
    parts = []
    # Front faces +Y (Unity +Z). When viewing front from +Y looking toward -Y:
    #   +X is viewer's LEFT, -X is viewer's RIGHT (exact match in both Blender and Unity!).
    # Exact 48 Selman Sokağı layout left-to-right from street:
    #   LEFT (+X = +0.6..+4.6): Terracotta Red plaster half + barred white PVC window
    #   CENTER (X = -2.2..+0.6): Sage Green plaster half
    #   RIGHT (X = -4.2..-2.2): Recessed porch bay + teal door + vertical timber gable cladding
    #   FAR RIGHT (X = -8.8..-4.4): Double light-blue wrought-iron driveway gate with two 'P' plaques!

    # Concrete plinth
    parts.append(add_box("House_Plinth", (0.2, -2.8, 0.18), (9.0, 7.6, 0.36), mats["Concrete"], bevel=0.02))
    # Left Red Plastered Block (X = +0.6..+4.6)
    parts.append(add_box("House_RedHalf", (2.6, -2.8, 1.85), (4.0, 7.2, 3.05), mats["RedPlaster"], bevel=0.03))
    # Center Sage Green Block (X = -2.2..+0.6)
    parts.append(add_box("House_SageHalf", (-0.8, -2.8, 1.85), (2.8, 7.2, 3.05), mats["SagePlaster"], bevel=0.03))
    # Right recessed porch back wall + teal front door + timber porch column
    parts.append(add_box("House_PorchBackWall", (-3.2, -4.0, 1.85), (2.0, 4.8, 3.05), mats["SagePlaster"], bevel=0.03))
    parts.append(add_box("House_PorchDoor", (-2.6, -1.56, 1.38), (0.98, 0.12, 2.15), mats["DoorTeal"], bevel=0.02))
    parts.append(add_box("House_PorchPost", (-4.05, 0.65, 1.78), (0.18, 0.18, 2.95), mats["Wood"], bevel=0.015))

    # Gable triangles (sage right half + exact triangular red left half so nothing pokes past roof!)
    parts.append(add_gable_prism("House_GableMain", -4.2, 4.6, 0.80, -6.40, 3.35, 5.05, mats["SagePlaster"]))
    # Front-left triangular red half-gable overlay (X = +0.6..+4.55, following exact roof slope!)
    bm_rg = bmesh.new()
    # Roof ridge is at X = +0.2, Z = 5.05; eave at X = +4.6, Z = 3.35
    z_at_split = 5.05 - (0.6 - 0.2) * (5.05 - 3.35) / 4.4
    v0 = bm_rg.verts.new(Vector((0.6, 0.82, 3.35)))
    v1 = bm_rg.verts.new(Vector((4.55, 0.82, 3.35)))
    v2 = bm_rg.verts.new(Vector((0.6, 0.82, z_at_split - 0.04)))
    v3 = bm_rg.verts.new(Vector((0.6, 0.77, 3.35)))
    v4 = bm_rg.verts.new(Vector((4.55, 0.77, 3.35)))
    v5 = bm_rg.verts.new(Vector((0.6, 0.77, z_at_split - 0.04)))
    bm_rg.faces.new((v0, v1, v2))
    bm_rg.faces.new((v4, v3, v5))
    bm_rg.faces.new((v0, v3, v4, v1))
    bm_rg.faces.new((v1, v4, v5, v2))
    bm_rg.faces.new((v2, v5, v3, v0))
    parts.append(make_obj("House_FrontRedSplit", bm_rg, mats["RedPlaster"], smooth_angle=25.0))

    # Warm vertical timber cladding triangle on upper-right front gable (X = -4.2..-2.1, tucked cleanly under roof!)
    bm_tg = bmesh.new()
    z_at_timber = 5.05 - (0.2 - (-2.1)) * (5.05 - 3.35) / 4.4
    t0 = bm_tg.verts.new(Vector((-4.18, 0.83, 2.75)))
    t1 = bm_tg.verts.new(Vector((-2.10, 0.83, 2.75)))
    t2 = bm_tg.verts.new(Vector((-2.10, 0.83, z_at_timber - 0.04)))
    t3 = bm_tg.verts.new(Vector((-4.18, 0.83, 3.35)))
    t4 = bm_tg.verts.new(Vector((-4.18, 0.65, 2.75)))
    t5 = bm_tg.verts.new(Vector((-2.10, 0.65, 2.75)))
    t6 = bm_tg.verts.new(Vector((-2.10, 0.65, z_at_timber - 0.04)))
    t7 = bm_tg.verts.new(Vector((-4.18, 0.65, 3.35)))
    bm_tg.faces.new((t0, t1, t2, t3))
    bm_tg.faces.new((t5, t4, t7, t6))
    bm_tg.faces.new((t0, t4, t5, t1))
    bm_tg.faces.new((t1, t5, t6, t2))
    bm_tg.faces.new((t2, t6, t7, t3))
    bm_tg.faces.new((t3, t7, t4, t0))
    parts.append(make_obj("House_TimberCladding", bm_tg, mats["Wood"], smooth_angle=25.0))

    # Pitched terracotta roof slopes overhanging nicely over front porch
    slope_angle = math.atan2(5.05 - 3.35, 4.4)
    slope_len = math.sqrt(4.4 * 4.4 + 1.70 * 1.70) + 0.55
    parts.append(add_box("Roof_LeftSlope", (2.55, -2.70, 4.18), (slope_len, 8.20, 0.18), mats["RoofTile"], bevel=0.03, rot_euler=(0.0, slope_angle, 0.0)))
    parts.append(add_box("Roof_RightSlope", (-2.15, -2.70, 4.18), (slope_len, 8.20, 0.18), mats["RoofTile"], bevel=0.03, rot_euler=(0.0, -slope_angle, 0.0)))
    parts.append(add_box("Roof_RidgeCap", (0.20, -2.70, 5.12), (0.32, 8.28, 0.18), mats["RoofTile"], bevel=0.03))

    # Front White PVC Window with crisp dark glass pane + white wrought-iron security bars (demir parmaklık!)
    parts.append(add_box("Win_Frame", (1.55, 0.82, 1.92), (1.76, 0.10, 1.38), mats["WhitePVC"], bevel=0.02))
    parts.append(add_box("Win_Glass", (1.55, 0.87, 1.92), (1.54, 0.04, 1.16), mats["Glass"], bevel=0.01))
    parts.append(add_box("Win_MullionV", (1.55, 0.90, 1.92), (0.07, 0.05, 1.24), mats["WhitePVC"], bevel=0.01))
    # Security iron grille bars in front of window
    for bx in (0.90, 1.16, 1.42, 1.68, 1.94, 2.20):
        parts.append(add_cyl(f"WinBar_V_{bx:.2f}", (bx, 0.95, 1.92), 0.014, 0.014, 1.34, 6, mats["WhitePVC"]))
    for bz in (1.42, 1.92, 2.42):
        parts.append(add_cyl(f"WinBar_H_{bz:.2f}", (1.55, 0.95, bz), 0.014, 0.014, 1.66, 6, mats["WhitePVC"], rot_euler=(0.0, math.pi * 0.5, 0.0)))

    # Overhead Grapevine Pergola (Asma Çardağı) extending across front yard (Y = 0.8..3.4, Z = 2.68)
    for px in (-2.8, 0.8, 4.4):
        parts.append(add_cyl(f"Pergola_Post_{px:.1f}", (px, 3.30, 1.35), 0.045, 0.045, 2.70, 8, mats["TrimDark"]))
    parts.append(add_box("Pergola_FrontBeam", (0.8, 3.30, 2.68), (7.6, 0.08, 0.08), mats["TrimDark"], bevel=0.01))
    for rx in (-3.0, -1.8, -0.6, 0.6, 1.8, 3.0, 4.2):
        parts.append(add_box(f"Pergola_Rafter_{rx:.1f}", (rx, 2.05, 2.74), (0.06, 2.75, 0.06), mats["DarkWood"], bevel=0.01))
    # Organic tangled grapevine canopy on top of pergola
    for vx in (-2.0, -0.6, 0.8, 2.2, 3.6):
        parts.append(add_box(f"Pergola_Vine_{vx:.1f}", (vx, 2.15, 2.86), (1.35, 2.10, 0.22), mats["VineBrown"], bevel=0.08, rot_euler=(0.05, 0.04, vx * 0.15)))

    # Weathered Concrete Garden Retaining Wall + Iconic Light-Blue Wrought-Iron Fence (X = -4.2..+4.6, Y = +3.55)
    parts.append(add_box("Garden_WallConcrete", (0.2, 3.55, 0.45), (8.9, 0.28, 0.90), mats["Concrete"], bevel=0.02))
    parts.append(add_box("Garden_LeftPillar", (4.65, 3.55, 1.05), (0.48, 0.48, 2.10), mats["Concrete"], bevel=0.03))
    parts.append(add_box("Garden_FenceRailBot", (0.2, 3.55, 0.96), (8.5, 0.04, 0.04), mats["FenceBlue"], bevel=0.008))
    parts.append(add_box("Garden_FenceRailTop", (0.2, 3.55, 1.86), (8.5, 0.04, 0.04), mats["FenceBlue"], bevel=0.008))
    for fx in range(-40, 44, 3):
        parts.append(add_cyl(f"Fence_Bar_{fx}", (fx * 0.1, 3.55, 1.41), 0.014, 0.014, 0.90, 6, mats["FenceBlue"]))

    # Iconic Double Light-Blue Wrought-Iron Driveway Gate with TWO round white 'P' signs on RIGHT (X = -8.8..-4.4, Y = +3.55)!
    parts.append(add_cyl("Gate_PostL", (-4.35, 3.55, 0.95), 0.11, 0.11, 1.90, 12, mats["FenceBlue"]))
    parts.append(add_cyl("Gate_PostR", (-8.85, 3.55, 0.95), 0.11, 0.11, 1.90, 12, mats["FenceBlue"]))
    parts.append(add_box("Gate_TopBar", (-6.60, 3.55, 1.78), (4.35, 0.06, 0.06), mats["FenceBlue"], bevel=0.01))
    parts.append(add_box("Gate_MidBar", (-6.60, 3.55, 0.98), (4.35, 0.06, 0.06), mats["FenceBlue"], bevel=0.01))
    parts.append(add_box("Gate_BotBar", (-6.60, 3.55, 0.16), (4.35, 0.06, 0.06), mats["FenceBlue"], bevel=0.01))
    for gx in range(-86, -45, 3):
        parts.append(add_cyl(f"Gate_Bar_{gx}", (gx * 0.1, 3.55, 0.97), 0.015, 0.015, 1.62, 6, mats["FenceBlue"]))
    # Two signature round white 'P' parking plaques on the blue gate (stem on viewer-left +X, loop on viewer-right -X!)
    for sign_x in (-5.95, -7.35):
        parts.append(add_cyl(f"Gate_PSignDisc_{sign_x:.2f}", (sign_x, 3.60, 1.02), 0.24, 0.24, 0.03, 18, mats["WhitePVC"], rot_euler=(math.pi * 0.5, 0.0, 0.0)))
        parts.append(add_box(f"Gate_PLetterStem_{sign_x:.2f}", (sign_x + 0.05, 3.62, 1.02), (0.06, 0.02, 0.24), mats["TrimDark"], bevel=0.005))
        parts.append(add_box(f"Gate_PLetterLoop_{sign_x:.2f}", (sign_x - 0.03, 3.62, 1.08), (0.14, 0.02, 0.12), mats["TrimDark"], bevel=0.005))

    # Concrete driveway side wall + garden greenery bushes behind fence
    parts.append(add_box("Driveway_SideWall", (-8.95, 0.2, 0.72), (0.30, 7.2, 1.44), mats["Concrete"], bevel=0.02))
    parts.append(add_box("Garden_ShrubL", (3.4, 2.4, 0.72), (1.6, 1.2, 1.1), mats["ShrubGreen"], bevel=0.22))
    parts.append(add_box("Garden_ShrubR", (-0.8, 2.6, 0.56), (2.2, 0.9, 0.78), mats["ShrubGreen"], bevel=0.18))

    return join_all(parts, "Sultanbeyli_Selman48_House")


# ---------------------------------------------------------------------------
# 2. UNFINISHED 3-STORY EXPOSED RED-BRICK CONSTRUCTION BUILDING (Behind right of No:48!)
# ---------------------------------------------------------------------------
def build_brick_construction(mats):
    parts = []
    # Footprint 11.4m x 9.6m, 3 floors (each 3.05m -> total height ~10.4m)
    # Concrete floor slabs + exposed hollow red-brick piers + recessed dark unglazed window openings!
    w, d = 11.4, 9.6
    for floor in range(4):
        slab_z = floor * 3.05 + 0.16
        overhang = 0.75 if floor == 3 else (0.55 if floor > 0 else 0.0)
        parts.append(add_box(f"Constr_Slab_{floor}", (0.0, 0.0, slab_z), (w + overhang * 2, d + overhang * 2, 0.32), mats["Concrete"], bevel=0.03))

    # Dark hollow interior core so all unglazed window cutouts look realistically deep & shadowy!
    parts.append(add_box("Constr_DarkCore", (0.0, 0.0, 4.65), (w - 0.65, d - 0.65, 8.9), mats["DarkInterior"], bevel=0.02))

    # Corner & mid structural reinforced concrete columns
    for sx in (-1.0, 0.0, 1.0):
        for sy in (-1.0, 1.0):
            parts.append(add_box(f"Constr_Col_{sx}_{sy}", (sx * (w * 0.48), sy * (d * 0.48), 4.70), (0.42, 0.42, 9.20), mats["Concrete"], bevel=0.02))

    # Exposed red hollow-brick wall bands (spandrels + piers between open unglazed windows on all 3 floors!)
    for floor in range(3):
        z_bot = floor * 3.05 + 0.32
        # Lower brick spandrel wall (1.05m tall) + upper lintel band (0.45m)
        parts.append(add_box(f"Brick_SpandrelF_{floor}", (0.0, d * 0.49, z_bot + 0.52), (w, 0.24, 1.04), mats["RawBrick"], bevel=0.015))
        parts.append(add_box(f"Brick_SpandrelB_{floor}", (0.0, -d * 0.49, z_bot + 0.52), (w, 0.24, 1.04), mats["RawBrick"], bevel=0.015))
        parts.append(add_box(f"Brick_LintelF_{floor}", (0.0, d * 0.49, z_bot + 2.52), (w, 0.24, 0.42), mats["RawBrick"], bevel=0.015))
        parts.append(add_box(f"Brick_LintelB_{floor}", (0.0, -d * 0.49, z_bot + 2.52), (w, 0.24, 0.42), mats["RawBrick"], bevel=0.015))

        # Side walls spandrel + lintels
        for sx in (-1.0, 1.0):
            parts.append(add_box(f"Brick_SideSp_{floor}_{sx}", (sx * w * 0.49, 0.0, z_bot + 0.52), (0.24, d, 1.04), mats["RawBrick"], bevel=0.015))
            parts.append(add_box(f"Brick_SideLi_{floor}_{sx}", (sx * w * 0.49, 0.0, z_bot + 2.52), (0.24, d, 0.42), mats["RawBrick"], bevel=0.015))
            for py in (-4.4, -1.6, 1.6, 4.4):
                parts.append(add_box(f"Brick_SidePier_{floor}_{sx}_{py}", (sx * w * 0.49, py, z_bot + 1.68), (0.24, 1.15, 1.40), mats["RawBrick"], bevel=0.01))

        # Brick vertical piers between 4 wide open window bays on front & back
        for px in (-5.2, -2.6, 0.0, 2.6, 5.2):
            parts.append(add_box(f"Brick_PierF_{floor}_{px}", (px, d * 0.49, z_bot + 1.68), (1.05, 0.24, 1.40), mats["RawBrick"], bevel=0.01))
            parts.append(add_box(f"Brick_PierB_{floor}_{px}", (px, -d * 0.49, z_bot + 1.68), (1.05, 0.24, 1.40), mats["RawBrick"], bevel=0.01))

        # Corner recessed brick balcony parapet
        parts.append(add_box(f"Brick_BalcParapet_{floor}", (w * 0.5 + 0.25, d * 0.26, z_bot + 0.52), (0.20, 3.6, 1.02), mats["RawBrick"], bevel=0.015))

    # Unfinished dark timber roof truss skeleton + partial sheet roofing on top!
    parts.append(add_box("Constr_RoofGable", (0.0, 0.0, 9.95), (10.6, 8.6, 1.15), mats["DarkWood"], bevel=0.08))
    parts.append(add_box("Constr_RoofSheetA", (-2.2, 0.0, 10.48), (5.2, 9.2, 0.14), mats["TrimDark"], bevel=0.02, rot_euler=(0.0, -0.18, 0.0)))
    parts.append(add_box("Constr_RoofSheetB", (2.2, 0.0, 10.48), (5.2, 9.2, 0.14), mats["TrimDark"], bevel=0.02, rot_euler=(0.0, 0.18, 0.0)))

    return join_all(parts, "Sultanbeyli_BrickConstruction")


# ---------------------------------------------------------------------------
# 3. YELLOW & TERRACOTTA-STRIPE 3-STORY SULTANBEYLİ APARTMAN (Behind left of No:48!)
# ---------------------------------------------------------------------------
def build_yellow_apartman(mats):
    parts = []
    w, d, h = 10.6, 9.8, 9.6
    parts.append(add_box("YelApt_Body", (0.0, 0.0, h * 0.5), (w, d, h), mats["YellowPlaster"], bevel=0.05))
    parts.append(add_box("YelApt_Base", (0.0, 0.0, 0.35), (w + 0.16, d + 0.16, 0.70), mats["Concrete"], bevel=0.03))
    # Signature vertical terracotta-orange decorative window-bay stripes!
    for sx in (-2.6, 2.6):
        parts.append(add_box(f"YelApt_StripeF_{sx}", (sx, d * 0.5 + 0.03, h * 0.52), (1.85, 0.08, h - 0.8), mats["OrangeStripe"], bevel=0.015))
        parts.append(add_box(f"YelApt_StripeB_{sx}", (sx, -d * 0.5 - 0.03, h * 0.52), (1.85, 0.08, h - 0.8), mats["OrangeStripe"], bevel=0.015))

    # White PVC windows across 3 floors
    for floor in range(3):
        wz = 1.85 + floor * 3.05
        for wx in (-2.6, 2.6):
            for sy in (-1.0, 1.0):
                parts.append(add_box(f"YelApt_WinF_{floor}_{wx}_{sy}", (wx, sy * (d * 0.5 + 0.06), wz), (1.42, 0.10, 1.48), mats["WhitePVC"], bevel=0.02))
                parts.append(add_box(f"YelApt_GlassF_{floor}_{wx}_{sy}", (wx, sy * (d * 0.5 + 0.10), wz), (1.24, 0.04, 1.30), mats["Glass"], bevel=0.01))
        for wy in (-2.4, 2.4):
            for sx in (-1.0, 1.0):
                parts.append(add_box(f"YelApt_WinS_{floor}_{wy}_{sx}", (sx * (w * 0.5 + 0.04), wy, wz), (0.10, 1.36, 1.42), mats["WhitePVC"], bevel=0.02))
                parts.append(add_box(f"YelApt_GlassS_{floor}_{wy}_{sx}", (sx * (w * 0.5 + 0.08), wy, wz), (0.04, 1.18, 1.24), mats["Glass"], bevel=0.01))

    # Overhanging concrete cornice + pitched tiled roof + satellite dish (uydu çanağı!)
    parts.append(add_box("YelApt_Cornice", (0.0, 0.0, h + 0.14), (w + 0.72, d + 0.72, 0.28), mats["CreamPaint"], bevel=0.04))
    parts.append(add_box("YelApt_RoofLow", (0.0, 0.0, h + 0.80), (w + 0.35, d + 0.35, 1.05), mats["RoofTile"], bevel=0.18))
    parts.append(add_box("YelApt_RoofTop", (0.0, 0.0, h + 1.55), (w * 0.62, d * 0.62, 0.75), mats["RoofTile"], bevel=0.14))
    parts.append(add_cyl("YelApt_SatDish", (-w * 0.5 - 0.32, 3.2, 6.2), 0.42, 0.12, 0.18, 14, mats["WhitePVC"], rot_euler=(0.45, 1.15, 0.0)))

    return join_all(parts, "Sultanbeyli_YellowApartman")


# ---------------------------------------------------------------------------
# 4. WHITE & OLIVE-GREEN 4-STORY MODERN SULTANBEYLİ APARTMAN (Far right of photo!)
# ---------------------------------------------------------------------------
def build_modern_apartman(mats):
    parts = []
    w, d, h = 11.2, 10.4, 12.6
    parts.append(add_box("ModApt_Body", (0.0, 0.0, h * 0.5), (w, d, h), mats["CreamPaint"], bevel=0.06))
    # Signature olive-green vertical facade bay
    parts.append(add_box("ModApt_OliveBayF", (0.0, d * 0.5 + 0.06, h * 0.52), (4.4, 0.16, h - 0.8), mats["OliveFacade"], bevel=0.03))
    parts.append(add_box("ModApt_OliveBayB", (0.0, -d * 0.5 - 0.06, h * 0.52), (4.4, 0.16, h - 0.8), mats["OliveFacade"], bevel=0.03))

    for floor in range(4):
        wz = 1.9 + floor * 3.0
        for wx in (-3.4, 0.0, 3.4):
            for sy in (-1.0, 1.0):
                parts.append(add_box(f"ModApt_Win_{floor}_{wx}_{sy}", (wx, sy * (d * 0.5 + 0.12), wz), (1.75, 0.12, 1.65), mats["TrimDark"], bevel=0.02))
                parts.append(add_box(f"ModApt_Gl_{floor}_{wx}_{sy}", (wx, sy * (d * 0.5 + 0.16), wz), (1.58, 0.05, 1.48), mats["Glass"], bevel=0.01))
        # Glass-railing modern balconies on floors 1..3
        if floor >= 1:
            parts.append(add_box(f"ModApt_BalcFloor_{floor}", (-3.4, d * 0.5 + 0.52, wz - 0.90), (2.4, 1.05, 0.16), mats["CreamPaint"], bevel=0.02))
            parts.append(add_box(f"ModApt_BalcGlass_{floor}", (-3.4, d * 0.5 + 0.98, wz - 0.35), (2.32, 0.06, 0.95), mats["Glass"], bevel=0.01))

    parts.append(add_box("ModApt_Parapet", (0.0, 0.0, h + 0.35), (w + 0.40, d + 0.40, 0.70), mats["CreamPaint"], bevel=0.04))
    parts.append(add_box("ModApt_RoofCap", (0.0, 0.0, h + 0.72), (w + 0.50, d + 0.50, 0.12), mats["TrimDark"], bevel=0.02))
    return join_all(parts, "Sultanbeyli_ModernApartman")


# ---------------------------------------------------------------------------
# 5. CLASSIC 3-STORY MAHALLE APARTMANI (With ground-floor neighborhood shopfront)
# ---------------------------------------------------------------------------
def build_mahalle_apartman(mats):
    parts = []
    w, d, h = 10.2, 9.4, 10.0
    parts.append(add_box("MahApt_Body", (0.0, 0.0, h * 0.5), (w, d, h), mats["CreamPaint"], bevel=0.05))
    # Ground-floor shopfront fascia & display glass
    parts.append(add_box("MahApt_ShopBase", (0.0, d * 0.5 + 0.05, 1.55), (w - 0.8, 0.14, 3.0), mats["TrimDark"], bevel=0.02))
    parts.append(add_box("MahApt_ShopGlass", (0.0, d * 0.5 + 0.10, 1.45), (w - 1.4, 0.06, 2.3), mats["Glass"], bevel=0.01))
    parts.append(add_box("MahApt_ShopSign", (0.0, d * 0.5 + 0.14, 3.10), (w - 0.4, 0.18, 0.65), mats["FenceBlue"], bevel=0.02))

    # Upper 2 residential floors with protruding balconies + windows
    for floor in (1, 2):
        wz = 1.85 + floor * 3.05
        for wx in (-3.0, 0.0, 3.0):
            parts.append(add_box(f"MahApt_Win_{floor}_{wx}", (wx, d * 0.5 + 0.04, wz), (1.45, 0.10, 1.48), mats["WhitePVC"], bevel=0.02))
            parts.append(add_box(f"MahApt_Gl_{floor}_{wx}", (wx, d * 0.5 + 0.08, wz), (1.28, 0.04, 1.30), mats["Glass"], bevel=0.01))
        # Center concrete balcony
        parts.append(add_box(f"MahApt_BalcSlab_{floor}", (0.0, d * 0.5 + 0.52, wz - 0.88), (3.1, 1.05, 0.18), mats["CreamPaint"], bevel=0.02))
        parts.append(add_box(f"MahApt_BalcWall_{floor}", (0.0, d * 0.5 + 0.98, wz - 0.36), (3.1, 0.12, 0.92), mats["CreamPaint"], bevel=0.02))

    parts.append(add_box("MahApt_Roof", (0.0, 0.0, h + 0.72), (w + 0.45, d + 0.45, 1.25), mats["RoofTile"], bevel=0.22))
    return join_all(parts, "Sultanbeyli_MahalleApartman")


# ---------------------------------------------------------------------------
# 6. RUSTED STEEL LATTICE UTILITY POLE (Left foreground of 48 Selman Sokağı!)
# ---------------------------------------------------------------------------
def build_power_pole_steel(mats):
    parts = []
    # Height 8.8m tapering slightly from base 0.52m to top 0.28m
    for sx in (-1.0, 1.0):
        parts.append(add_box(f"Lattice_Leg_{sx}", (sx * 0.19, 0.0, 4.40), (0.08, 0.08, 8.80), mats["RustSteel"], bevel=0.01))
    for lz in range(1, 11):
        z = lz * 0.82
        parts.append(add_box(f"Lattice_Rung_{lz}", (0.0, 0.0, z), (0.42, 0.07, 0.06), mats["RustSteel"], bevel=0.008))
    # Concrete footing
    parts.append(add_box("Lattice_Base", (0.0, 0.0, 0.22), (0.64, 0.42, 0.44), mats["Concrete"], bevel=0.03))
    # Top steel insulator crossarm + 4 white porcelain insulators
    parts.append(add_box("Lattice_CrossArm", (0.0, 0.0, 8.45), (1.45, 0.08, 0.08), mats["RustSteel"], bevel=0.01))
    for ix in (-0.60, -0.22, 0.22, 0.60):
        parts.append(add_cyl(f"Insulator_{ix:.2f}", (ix, 0.0, 8.58), 0.055, 0.045, 0.16, 8, mats["WhitePVC"]))
    return join_all(parts, "Sultanbeyli_PowerPole_Steel")


# ---------------------------------------------------------------------------
# 7. WOODEN UTILITY POLE (Right next to No:48 garden corner!)
# ---------------------------------------------------------------------------
def build_power_pole_wood(mats):
    parts = []
    parts.append(add_cyl("WoodPole_Shaft", (0.0, 0.0, 4.35), 0.14, 0.095, 8.70, 12, mats["DarkWood"]))
    parts.append(add_box("WoodPole_CrossArm", (0.0, 0.0, 8.20), (1.35, 0.08, 0.09), mats["RustSteel"], bevel=0.01))
    for ix in (-0.54, 0.0, 0.54):
        parts.append(add_cyl(f"WoodIns_{ix:.2f}", (ix, 0.0, 8.33), 0.05, 0.04, 0.15, 8, mats["WhitePVC"]))
    return join_all(parts, "Sultanbeyli_PowerPole_Wood")


def export_fbx(obj, fbx_name):
    out_path = os.path.join(OUT_FBX_DIR, fbx_name)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print(f"  Exported {fbx_name:<36} | verts={len(obj.data.vertices):5d} | tris={tris:5d}")


def render_selman48_recreation():
    """Render the EXACT 48 Selman Sokağı camera angle so we can compare side-by-side with Street View!"""
    reset_scene()
    mats = get_sultanbeyli_materials()

    bm_g = bmesh.new()
    bmesh.ops.create_grid(bm_g, x_segments=1, y_segments=1, size=60.0)
    make_obj("Ground", bm_g, make_mat("Asphalt", (0.22, 0.23, 0.24), roughness=0.85))

    # Recreate the exact 48 Selman Sokağı composition! (+X is viewer-left, -X is viewer-right)
    house48 = build_selman48_house(mats)
    house48.location = Vector((0.0, 0.0, 0.0))

    brick = build_brick_construction(mats)
    brick.location = Vector((-11.5, -10.5, 0.0))

    yel = build_yellow_apartman(mats)
    yel.location = Vector((8.8, -11.5, 0.0))
    yel.rotation_euler = Euler((0.0, 0.0, math.radians(8.0)), "XYZ")

    mod = build_modern_apartman(mats)
    mod.location = Vector((-23.0, -14.0, 0.0))

    p_steel = build_power_pole_steel(mats)
    p_steel.location = Vector((7.6, 5.2, 0.0))
    p_wood = build_power_pole_wood(mats)
    p_wood.location = Vector((4.8, 3.8, 0.0))

    world = bpy.data.worlds.new("SultanbeyliSky")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.66, 0.72, 0.78, 1.0)
        bg.inputs["Strength"].default_value = 0.95

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 2.8
    sun_data.color = (1.0, 0.96, 0.88)
    sun_obj = bpy.data.objects.new("Sun", sun_data)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = Euler((math.radians(46), math.radians(15), math.radians(145)), "XYZ")

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 26.0
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((1.2, 15.2, 2.3))
    cam_obj.rotation_euler = (Vector((-1.5, -1.0, 2.6)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_obj

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_Selman48_Showcase.blend"))
    bpy.ops.render.render(write_still=True)
    print(f"Saved 48 Selman Sokağı showcase render to: {PREVIEW_PATH}")


def main():
    builders = [
        ("Sultanbeyli_Selman48_House.fbx", build_selman48_house),
        ("Sultanbeyli_BrickConstruction.fbx", build_brick_construction),
        ("Sultanbeyli_YellowApartman.fbx", build_yellow_apartman),
        ("Sultanbeyli_ModernApartman.fbx", build_modern_apartman),
        ("Sultanbeyli_MahalleApartman.fbx", build_mahalle_apartman),
        ("Sultanbeyli_PowerPole_Steel.fbx", build_power_pole_steel),
        ("Sultanbeyli_PowerPole_Wood.fbx", build_power_pole_wood),
    ]
    print(f"[GRIDROAM Blender] Generating {len(builders)} authentic Sultanbeyli / 48 Selman Sokağı 3D models...")
    for fbx_name, fn in builders:
        reset_scene()
        mats = get_sultanbeyli_materials()
        obj = fn(mats)
        export_fbx(obj, fbx_name)
    render_selman48_recreation()


if __name__ == "__main__":
    main()
