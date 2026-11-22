#!/usr/bin/env python3
"""
Upgrades the 5 key hero Selman Sokağı buildings into TRUE HOLLOW WALK-IN SHELLS
while preserving 100% of their Google Street View exteriors:
  1. Sultanbeyli_Selman48_House.fbx     -> Hollow 8.8x7.2m interior room + open porch doorway + open garden walk-in gap!
  2. Sultanbeyli_Shop58_ArchGate.fbx    -> Hollow 7.0x6.8m walk-in 'SELMAN BAKKAL' interior + open storefront doorway!
  3. Sultanbeyli_House66South_Hafsa.fbx -> Hollow 4.4x5.2m walk-in 'HAFSA TESİSAT' workshop + open front entrance!
  4. Sultanbeyli_Garage73_LilacGate.fbx -> Hollow 6.4x6.2m open-shutter walk-in/drive-in 'OTO TAMİR' bay!
  5. Sultanbeyli_House66_Yellow.fbx     -> Hollow 6.4x6.4m ground-floor interior + open garden gate gap + open doorway!
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_FBX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.65, metallic=0.0):
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
    return mat


def get_mats():
    return {
        "RedPlaster": make_mat("RedPlaster", (0.66, 0.21, 0.16), 0.76),
        "SagePlaster": make_mat("SagePlaster", (0.64, 0.67, 0.56), 0.76),
        "YellowPlaster": make_mat("YellowPlaster", (0.85, 0.76, 0.50), 0.74),
        "PeachPlaster": make_mat("PeachPlaster", (0.86, 0.72, 0.64), 0.74),
        "CoralPlaster": make_mat("CoralPlaster", (0.76, 0.42, 0.39), 0.74),
        "OrangeStripe": make_mat("OrangeStripe", (0.76, 0.42, 0.16), 0.72),
        "CreamPaint": make_mat("CreamPaint", (0.90, 0.88, 0.83), 0.72),
        "Concrete": make_mat("Concrete", (0.58, 0.57, 0.54), 0.84),
        "Wood": make_mat("Wood", (0.58, 0.38, 0.20), 0.64),
        "DarkWood": make_mat("DarkWood", (0.28, 0.19, 0.12), 0.72),
        "FenceBlue": make_mat("FenceBlue", (0.40, 0.56, 0.72), 0.42, 0.58),
        "CobaltBlue": make_mat("CobaltBlue", (0.10, 0.38, 0.76), 0.44, 0.35),
        "LilacGate": make_mat("LilacGate", (0.66, 0.58, 0.76), 0.46, 0.45),
        "WhitePVC": make_mat("WhitePVC", (0.94, 0.94, 0.93), 0.35),
        "Glass": make_mat("Glass", (0.10, 0.15, 0.20), 0.10, 0.25),
        "RoofTile": make_mat("RoofTile", (0.54, 0.25, 0.17), 0.74),
        "Galvanized": make_mat("Galvanized", (0.68, 0.70, 0.72), 0.44, 0.68),
        "TrimDark": make_mat("TrimDark", (0.14, 0.15, 0.16), 0.56, 0.30),
        "GoldBrass": make_mat("GoldBrass", (0.82, 0.64, 0.20), 0.35, 0.82),
        "VineBrown": make_mat("VineBrown", (0.30, 0.22, 0.16), 0.90),
        "ShrubGreen": make_mat("ShrubGreen", (0.20, 0.35, 0.16), 0.85),
        "MeshGreen": make_mat("MeshGreen", (0.14, 0.28, 0.16), 0.82),
        "DoorTeal": make_mat("DoorTeal", (0.12, 0.28, 0.36), 0.45, 0.20),
        "SignRed": make_mat("SignRed", (0.76, 0.12, 0.12), 0.42, 0.15),
    }


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


def make_obj(name, bm, mat):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    box_uv(obj)
    return obj


def add_box(name, center, size, mat, bevel=0.012, rot_euler=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.20), segments=1, affect="EDGES")
    if rot_euler is not None:
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Euler(rot_euler, "XYZ").to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return make_obj(name, bm, mat)


def add_cyl(name, center, r1, r2, depth, segs, mat, rot_euler=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    if rot_euler is not None:
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Euler(rot_euler, "XYZ").to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return make_obj(name, bm, mat)


def add_gable_prism(name, x_min, x_max, y_front, y_back, z_eave, z_peak, mat):
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
    return make_obj(name, bm, mat)


def add_window(parts, prefix, x, y, z, w, h, mats, barred=False, bar_mat_key="TrimDark"):
    parts.append(add_box(f"{prefix}_Frame", (x, y, z), (w, 0.10, h), mats["WhitePVC"]))
    parts.append(add_box(f"{prefix}_Glass", (x, y + 0.045, z), (w - 0.18, 0.03, h - 0.18), mats["Glass"]))
    parts.append(add_box(f"{prefix}_Mull", (x, y + 0.065, z), (0.06, 0.03, h - 0.12), mats["WhitePVC"]))
    if barred:
        n_bars = max(3, int(w / 0.26))
        for i in range(n_bars):
            bx = x + ((i + 0.5) / n_bars - 0.5) * (w - 0.12)
            parts.append(add_cyl(f"{prefix}_BV_{i}", (bx, y + 0.11, z), 0.012, 0.012, h - 0.04, 6, mats[bar_mat_key]))
        for hz in (-h * 0.34, 0.0, h * 0.34):
            parts.append(add_cyl(f"{prefix}_BH_{hz:.2f}", (x, y + 0.11, z + hz), 0.012, 0.012, w - 0.04, 6, mats[bar_mat_key], rot_euler=(0, math.pi * 0.5, 0)))


def add_hollow_room_walls(parts, prefix, x_min, x_max, y_back, y_front, z_bot, z_top, wall_t, ext_mat, int_mat, door_x, door_w, door_h, front_ext_override_fn=None):
    """Creates a true hollow room with floor, ceiling, 3 solid perimeter walls, and a front wall with a real walk-in doorway!"""
    w = x_max - x_min
    d = y_front - y_back
    h = z_top - z_bot
    cx = (x_min + x_max) * 0.5
    cy = (y_back + y_front) * 0.5
    cz = (z_bot + z_top) * 0.5

    # Floor slab & ceiling slab
    parts.append(add_box(f"{prefix}_Floor", (cx, cy, z_bot + 0.04), (w, d, 0.08), mats_global["Wood"] if int_mat == mats_global["CreamPaint"] else mats_global["Concrete"], bevel=0.0))
    parts.append(add_box(f"{prefix}_Ceiling", (cx, cy, z_top - 0.05), (w, d, 0.10), int_mat, bevel=0.0))

    # Back wall, Left (+X) wall, Right (-X) wall (thin perimeter shells -> completely open inside!)
    parts.append(add_box(f"{prefix}_BackW", (cx, y_back + wall_t * 0.5, cz), (w, wall_t, h), ext_mat, bevel=0.01))
    parts.append(add_box(f"{prefix}_BackIn", (cx, y_back + wall_t + 0.02, cz), (w - wall_t * 2, 0.04, h - 0.15), int_mat, bevel=0.0))
    parts.append(add_box(f"{prefix}_LeftW", (x_max - wall_t * 0.5, cy, cz), (wall_t, d, h), ext_mat, bevel=0.01))
    parts.append(add_box(f"{prefix}_LeftIn", (x_max - wall_t - 0.02, cy, cz), (0.04, d - wall_t * 2, h - 0.15), int_mat, bevel=0.0))
    parts.append(add_box(f"{prefix}_RightW", (x_min + wall_t * 0.5, cy, cz), (wall_t, d, h), ext_mat, bevel=0.01))
    parts.append(add_box(f"{prefix}_RightIn", (x_min + wall_t + 0.02, cy, cz), (0.04, d - wall_t * 2, h - 0.15), int_mat, bevel=0.0))

    # Front wall split around doorway [door_x - door_w/2 .. door_x + door_w/2] from z_bot .. z_bot + door_h!
    d_lo = door_x - door_w * 0.5
    d_hi = door_x + door_w * 0.5
    fy = y_front - wall_t * 0.5
    if d_lo > x_min + 0.05:
        seg_w = d_lo - x_min
        seg_cx = (x_min + d_lo) * 0.5
        m = front_ext_override_fn(seg_cx) if front_ext_override_fn else ext_mat
        parts.append(add_box(f"{prefix}_FrontR", (seg_cx, fy, cz), (seg_w, wall_t, h), m, bevel=0.01))
        parts.append(add_box(f"{prefix}_FrontRIn", (seg_cx, y_front - wall_t - 0.02, cz), (seg_w - 0.04, 0.04, h - 0.15), int_mat, bevel=0.0))
    if x_max > d_hi + 0.05:
        seg_w = x_max - d_hi
        seg_cx = (d_hi + x_max) * 0.5
        m = front_ext_override_fn(seg_cx) if front_ext_override_fn else ext_mat
        parts.append(add_box(f"{prefix}_FrontL", (seg_cx, fy, cz), (seg_w, wall_t, h), m, bevel=0.01))
        parts.append(add_box(f"{prefix}_FrontLIn", (seg_cx, y_front - wall_t - 0.02, cz), (seg_w - 0.04, 0.04, h - 0.15), int_mat, bevel=0.0))
    # Lintel above doorway
    lintel_h = z_top - (z_bot + door_h)
    if lintel_h > 0.05:
        parts.append(add_box(f"{prefix}_DoorLintel", (door_x, fy, z_bot + door_h + lintel_h * 0.5), (door_w + 0.04, wall_t, lintel_h), ext_mat, bevel=0.01))
    # Swung-open door leaf frame cleanly to the side so player walks straight through!
    parts.append(add_box(f"{prefix}_DoorFrameL", (d_hi + 0.04, fy, z_bot + door_h * 0.5), (0.08, wall_t + 0.04, door_h), mats_global["DarkWood"]))
    parts.append(add_box(f"{prefix}_DoorFrameR", (d_lo - 0.04, fy, z_bot + door_h * 0.5), (0.08, wall_t + 0.04, door_h), mats_global["DarkWood"]))


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
    obj.location = Vector((0, 0, 0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


mats_global = None


# 1. HOLLOW WALK-IN 48 SELMAN SOKAĞI HERO HOUSE
def build_hollow_selman48(mats):
    parts = []
    # Main hollow room: X = -4.2..+4.6 (8.8m wide), Y = -6.4..+0.8 (7.2m deep), Z = 0.04..3.35
    # Walk-in open doorway at X = -2.6 (right side porch, door width 1.35m, height 2.35m)!
    add_hollow_room_walls(
        parts, "H48", -4.2, 4.6, -6.4, 0.80, 0.04, 3.35, 0.24,
        mats["SagePlaster"], mats["CreamPaint"],
        door_x=-2.65, door_w=1.35, door_h=2.35,
        front_ext_override_fn=lambda x: mats["SagePlaster"],
    )
    # Signature Terracotta-Red left half facade overlay on front wall (X = +0.6..+4.6) & left side wall!
    parts.append(add_box("H48_RedFrontOverlay", (2.6, 0.81, 1.72), (4.02, 0.04, 3.30), mats["RedPlaster"]))
    parts.append(add_box("H48_RedSideOverlay", (4.61, -2.8, 1.72), (0.04, 7.22, 3.30), mats["RedPlaster"]))
    # Swung-open Teal front door leaf angled gracefully inside porch!
    parts.append(add_box("H48_OpenTealDoor", (-1.92, 0.22, 1.22), (0.98, 0.08, 2.25), mats["DoorTeal"], rot_euler=(0, 0, -1.25)))
    # Porch timber post + warm vertical timber upper-right gable cladding
    parts.append(add_box("H48_PorchPost", (-4.05, 0.65, 1.70), (0.18, 0.18, 3.25), mats["Wood"]))

    # Gable triangles (sage right + exact triangular red left + timber upper-right)
    parts.append(add_gable_prism("H48_GableMain", -4.2, 4.6, 0.80, -6.40, 3.35, 5.05, mats["SagePlaster"]))
    bm_rg = bmesh.new()
    z_split = 5.05 - (0.6 - 0.2) * (5.05 - 3.35) / 4.4
    v0 = bm_rg.verts.new(Vector((0.6, 0.83, 3.35)))
    v1 = bm_rg.verts.new(Vector((4.55, 0.83, 3.35)))
    v2 = bm_rg.verts.new(Vector((0.6, 0.83, z_split - 0.04)))
    v3 = bm_rg.verts.new(Vector((0.6, 0.77, 3.35)))
    v4 = bm_rg.verts.new(Vector((4.55, 0.77, 3.35)))
    v5 = bm_rg.verts.new(Vector((0.6, 0.77, z_split - 0.04)))
    bm_rg.faces.new((v0, v1, v2)); bm_rg.faces.new((v4, v3, v5))
    bm_rg.faces.new((v0, v3, v4, v1)); bm_rg.faces.new((v1, v4, v5, v2)); bm_rg.faces.new((v2, v5, v3, v0))
    parts.append(make_obj("H48_RedGable", bm_rg, mats["RedPlaster"]))

    bm_tg = bmesh.new()
    z_timb = 5.05 - (0.2 - (-2.1)) * (5.05 - 3.35) / 4.4
    t0 = bm_tg.verts.new(Vector((-4.18, 0.83, 2.75))); t1 = bm_tg.verts.new(Vector((-2.10, 0.83, 2.75)))
    t2 = bm_tg.verts.new(Vector((-2.10, 0.83, z_timb - 0.04))); t3 = bm_tg.verts.new(Vector((-4.18, 0.83, 3.35)))
    t4 = bm_tg.verts.new(Vector((-4.18, 0.65, 2.75))); t5 = bm_tg.verts.new(Vector((-2.10, 0.65, 2.75)))
    t6 = bm_tg.verts.new(Vector((-2.10, 0.65, z_timb - 0.04))); t7 = bm_tg.verts.new(Vector((-4.18, 0.65, 3.35)))
    bm_tg.faces.new((t0, t1, t2, t3)); bm_tg.faces.new((t5, t4, t7, t6))
    bm_tg.faces.new((t0, t4, t5, t1)); bm_tg.faces.new((t1, t5, t6, t2)); bm_tg.faces.new((t2, t6, t7, t3)); bm_tg.faces.new((t3, t7, t4, t0))
    parts.append(make_obj("H48_TimberGable", bm_tg, mats["Wood"]))

    # Pitched terracotta roof
    slope_angle = math.atan2(5.05 - 3.35, 4.4)
    slope_len = math.sqrt(4.4 * 4.4 + 1.70 * 1.70) + 0.55
    parts.append(add_box("H48_RoofL", (2.55, -2.70, 4.18), (slope_len, 8.20, 0.18), mats["RoofTile"], rot_euler=(0, slope_angle, 0)))
    parts.append(add_box("H48_RoofR", (-2.15, -2.70, 4.18), (slope_len, 8.20, 0.18), mats["RoofTile"], rot_euler=(0, -slope_angle, 0)))
    parts.append(add_box("H48_Ridge", (0.20, -2.70, 5.12), (0.32, 8.28, 0.18), mats["RoofTile"]))

    # Front barred white PVC window
    add_window(parts, "H48_Win", 1.55, 0.84, 1.92, 1.76, 1.38, mats, barred=True, bar_mat_key="WhitePVC")

    # Grapevine Pergola (Asma Çardağı)
    for px in (-2.8, 0.8, 4.4):
        parts.append(add_cyl(f"H48_PergP_{px:.1f}", (px, 3.30, 1.38), 0.045, 0.045, 2.76, 8, mats["TrimDark"]))
    parts.append(add_box("H48_PergBeam", (0.8, 3.30, 2.72), (7.6, 0.08, 0.08), mats["TrimDark"]))
    for rx in (-3.0, -1.8, -0.6, 0.6, 1.8, 3.0, 4.2):
        parts.append(add_box(f"H48_PergRaf_{rx:.1f}", (rx, 2.05, 2.78), (0.06, 2.75, 0.06), mats["DarkWood"]))
    for vx in (-2.0, -0.6, 0.8, 2.2, 3.6):
        parts.append(add_box(f"H48_Vine_{vx:.1f}", (vx, 2.15, 2.90), (1.35, 2.10, 0.22), mats["VineBrown"], bevel=0.06, rot_euler=(0.05, 0.04, vx * 0.15)))

    # Garden retaining wall + Light-Blue Wrought-Iron Fence WITH A CLEAN WALK-IN PEDESTRIAN GATE OPENING AT X = -4.2..-2.2!
    parts.append(add_box("H48_WallL", (1.4, 3.55, 0.45), (6.4, 0.28, 0.90), mats["Concrete"]))
    parts.append(add_box("H48_LeftPillar", (4.65, 3.55, 1.05), (0.48, 0.48, 2.10), mats["Concrete"]))
    parts.append(add_box("H48_FenceBot", (1.4, 3.55, 0.96), (6.3, 0.04, 0.04), mats["FenceBlue"]))
    parts.append(add_box("H48_FenceTop", (1.4, 3.55, 1.86), (6.3, 0.04, 0.04), mats["FenceBlue"]))
    for fx in range(-16, 44, 3):
        parts.append(add_cyl(f"H48_Bar_{fx}", (fx * 0.1, 3.55, 1.41), 0.014, 0.014, 0.90, 6, mats["FenceBlue"]))

    # Iconic Double Light-Blue 'P' Driveway Gate on right (X = -8.8..-4.3)
    parts.append(add_cyl("H48_GatePL", (-4.35, 3.55, 0.95), 0.11, 0.11, 1.90, 12, mats["FenceBlue"]))
    parts.append(add_cyl("H48_GatePR", (-8.85, 3.55, 0.95), 0.11, 0.11, 1.90, 12, mats["FenceBlue"]))
    for gz in (0.16, 0.98, 1.78):
        parts.append(add_box(f"H48_GBar_{gz:.2f}", (-6.60, 3.55, gz), (4.35, 0.06, 0.06), mats["FenceBlue"]))
    for gx in range(-86, -45, 3):
        parts.append(add_cyl(f"H48_GV_{gx}", (gx * 0.1, 3.55, 0.97), 0.015, 0.015, 1.62, 6, mats["FenceBlue"]))
    for sign_x in (-5.95, -7.35):
        parts.append(add_cyl(f"H48_PDisc_{sign_x:.2f}", (sign_x, 3.60, 1.02), 0.24, 0.24, 0.03, 18, mats["WhitePVC"], rot_euler=(math.pi * 0.5, 0, 0)))
        parts.append(add_box(f"H48_PStem_{sign_x:.2f}", (sign_x + 0.05, 3.62, 1.02), (0.06, 0.02, 0.24), mats["TrimDark"]))
        parts.append(add_box(f"H48_PLoop_{sign_x:.2f}", (sign_x - 0.03, 3.62, 1.08), (0.14, 0.02, 0.12), mats["TrimDark"]))

    parts.append(add_box("H48_DriveWall", (-8.95, 0.2, 0.72), (0.30, 7.2, 1.44), mats["Concrete"]))
    parts.append(add_box("H48_ShrubL", (3.2, 2.3, 0.68), (1.6, 1.1, 1.0), mats["ShrubGreen"], bevel=0.20))
    return join_all(parts, "Sultanbeyli_Selman48_House")


# 2. HOLLOW WALK-IN 58 SELMAN BAKKAL + ORNATE ARCHED GATE
def build_hollow_shop58(mats):
    parts = []
    # Hollow walk-in market interior (X = +0.5..+7.5, Y = -6.2..+0.6, Z = 0.04..3.50) with WIDE 2.4m OPEN STOREFRONT DOORWAY at X = +4.2!
    add_hollow_room_walls(
        parts, "S58", 0.5, 7.5, -6.2, 0.60, 0.04, 3.50, 0.22,
        mats["OrangeStripe"], mats["WhitePVC"],
        door_x=4.2, door_w=2.40, door_h=2.45,
    )
    # Signature weathered white fascia signboard across front
    parts.append(add_box("S58_Fascia", (4.0, 0.72, 3.05), (7.3, 0.22, 1.02), mats["CreamPaint"]))
    # Rolled-up shutter box on right bay + glass storefront window on left bay
    parts.append(add_box("S58_ShutterBox", (1.65, 0.68, 2.45), (2.15, 0.24, 0.34), mats["Galvanized"]))
    parts.append(add_box("S58_GlassWin", (1.65, 0.62, 1.25), (1.95, 0.06, 2.05), mats["Glass"]))
    # Ornate Black & Gold Arched Garden Gate on right (-X = -3.8..-0.6)
    parts.append(add_box("S58_Gate", (-2.2, 0.45, 1.05), (3.10, 0.10, 2.10), mats["TrimDark"]))
    for gx in (-3.15, -2.20, -1.25):
        parts.append(add_box(f"S58_Crest_{gx:.2f}", (gx, 0.52, 0.95), (0.28, 0.04, 0.48), mats["GoldBrass"]))
    parts.append(add_cyl("S58_GPL", (-0.60, 0.45, 1.25), 0.08, 0.08, 2.50, 8, mats["TrimDark"]))
    parts.append(add_cyl("S58_GPR", (-3.80, 0.45, 1.25), 0.08, 0.08, 2.50, 8, mats["TrimDark"]))
    for ang_i in range(9):
        ang = math.radians(ang_i * 22.5)
        ax = -2.2 + math.cos(ang) * 1.62
        az = 2.25 + math.sin(ang) * 1.25
        parts.append(add_box(f"S58_Arch_{ang_i}", (ax, 0.45, az), (0.44, 0.08, 0.08), mats["TrimDark"], rot_euler=(0, -ang + math.pi * 0.5, 0)))
    return join_all(parts, "Sultanbeyli_Shop58_ArchGate")


# 3. HOLLOW WALK-IN 66 SELMAN SOUTH + HAFSA TESİSAT WORKSHOP
def build_hollow_house66south_hafsa(mats):
    parts = []
    # Main 2-story peach/cream house (X = -2.2..+5.6)
    parts.append(add_box("H66S_Lower", (1.7, -2.6, 1.55), (7.8, 6.8, 3.10), mats["PeachPlaster"]))
    parts.append(add_box("H66S_Upper", (1.7, -2.6, 4.65), (7.8, 6.8, 3.10), mats["CreamPaint"]))
    parts.append(add_box("H66S_Eave", (1.7, -2.6, 6.28), (8.3, 7.3, 0.22), mats["Concrete"]))
    parts.append(add_box("H66S_Roof", (1.7, -2.6, 6.55), (7.9, 6.9, 0.38), mats["RoofTile"]))
    parts.append(add_box("H66S_PorchWall", (3.6, 1.35, 0.62), (3.8, 0.20, 1.24), mats["PeachPlaster"]))
    parts.append(add_box("H66S_PorchGate", (1.85, 1.35, 0.65), (1.05, 0.08, 1.26), mats["CreamPaint"]))
    parts.append(add_box("H66S_Hedge", (4.5, 1.45, 1.85), (2.25, 0.14, 1.45), mats["ShrubGreen"]))
    parts.append(add_box("H66S_Step", (0.55, 1.15, 0.12), (1.35, 0.65, 0.24), mats["Concrete"]))
    parts.append(add_box("H66S_BlueDoor", (0.55, 0.85, 1.22), (0.98, 0.10, 2.08), mats["CobaltBlue"]))
    add_window(parts, "H66S_WL", 2.75, 0.82, 1.78, 1.85, 1.28, mats, barred=True, bar_mat_key="CobaltBlue")
    add_window(parts, "H66S_WR", -1.05, 0.82, 1.78, 1.45, 1.28, mats, barred=True, bar_mat_key="CobaltBlue")
    add_window(parts, "H66S_WU1", 2.55, 0.82, 4.85, 1.75, 1.30, mats)
    add_window(parts, "H66S_WU2", -0.55, 0.82, 4.85, 1.35, 1.25, mats)

    # HOLLOW WALK-IN 'HAFSA TESİSAT' WORKSHOP (X = -8.6..-3.8, Y = -4.6..+0.6, Z = 0.04..3.10) w/ WIDE OPEN FRONT ENTRANCE!
    add_hollow_room_walls(
        parts, "Hafsa", -8.6, -3.8, -4.6, 0.60, 0.04, 3.10, 0.20,
        mats["CobaltBlue"], mats["WhitePVC"],
        door_x=-6.2, door_w=2.40, door_h=2.35,
    )
    parts.append(add_box("Hafsa_Roof", (-6.2, -2.0, 3.16), (5.2, 5.6, 0.16), mats["Galvanized"]))
    parts.append(add_box("Hafsa_SignBand", (-6.2, 0.66, 2.72), (4.4, 0.08, 0.62), mats["YellowPlaster"]))
    parts.append(add_box("Hafsa_UpperMesh", (-6.2, 0.55, 3.72), (5.0, 0.10, 0.95), mats["MeshGreen"]))
    return join_all(parts, "Sultanbeyli_House66South_Hafsa")


# 4. HOLLOW OPEN-SHUTTER WALK-IN/DRIVE-IN 73 SELMAN OTO TAMİR GARAGE + LILAC GATE
def build_hollow_garage73(mats):
    parts = []
    # Hollow 6.4m x 6.2m garage bay with WIDE 3.8m OPEN SHUTTER ENTRY (shutter rolled up cleanly to ceiling!)
    add_hollow_room_walls(
        parts, "G73", -3.2, 3.2, -5.7, 0.55, 0.04, 3.40, 0.22,
        mats["CoralPlaster"], mats["Concrete"],
        door_x=0.0, door_w=3.80, door_h=2.65,
    )
    parts.append(add_box("G73_RoofTrim", (0.0, -2.58, 3.46), (7.1, 6.8, 0.14), mats["OrangeStripe"]))
    # Rolled-up grey shutter housing above opening
    parts.append(add_box("G73_ShutterBox", (0.0, 0.62, 2.95), (4.10, 0.36, 0.44), mats["Galvanized"]))
    # Lilac double garden gate on left (+X = +3.3..+6.4) + side wall on right
    parts.append(add_box("G73_LilacGate", (4.85, 0.45, 0.88), (3.10, 0.10, 1.76), mats["LilacGate"]))
    parts.append(add_box("G73_SideWallR", (-4.8, 0.45, 1.05), (3.2, 0.26, 2.10), mats["CoralPlaster"]))
    return join_all(parts, "Sultanbeyli_Garage73_LilacGate")


# 5. HOLLOW WALK-IN 66 SELMAN NORTH YELLOW HOUSE + OPEN GREEN MESH FENCE GATE
def build_hollow_house66_yellow(mats):
    parts = []
    # Hollow ground floor salon (X = -2.2..+4.2, Y = -6.0..+0.4, Z = 0.04..3.45) with walk-in front doorway at X = -0.7!
    add_hollow_room_walls(
        parts, "H66G", -2.2, 4.2, -6.0, 0.40, 0.04, 3.45, 0.22,
        mats["YellowPlaster"], mats["CreamPaint"],
        door_x=-0.75, door_w=1.40, door_h=2.35,
    )
    # Upper floor overhanging right side
    parts.append(add_box("H66_Upper", (0.1, -2.8, 5.05), (8.2, 6.6, 3.2), mats["YellowPlaster"]))
    parts.append(add_box("H66_RightBack", (-3.1, -4.4, 1.75), (1.8, 3.2, 3.5), mats["YellowPlaster"]))
    parts.append(add_box("H66_RoofSlab", (0.1, -2.8, 6.78), (8.8, 7.2, 0.26), mats["Concrete"]))
    parts.append(add_box("H66_RoofCap", (0.1, -2.8, 7.10), (8.2, 6.6, 0.45), mats["RoofTile"]))

    add_window(parts, "H66_W1", 2.4, 0.42, 2.05, 1.65, 1.35, mats, barred=True)
    add_window(parts, "H66_W2", 2.6, 0.52, 5.15, 1.65, 1.35, mats)
    add_window(parts, "H66_W3", 0.3, 0.52, 5.25, 0.65, 0.85, mats)
    add_window(parts, "H66_W4", -2.2, 0.52, 5.15, 1.85, 1.35, mats)
    parts.append(add_box("H66_RedPlaque", (0.45, 0.44, 2.45), (0.52, 0.04, 0.22), mats["SignRed"]))

    # Signature Dark-Green Mesh Fence with a CLEAN 2.2m WALK-IN GARDEN GATE OPENING at X = -2.2..0.0!
    parts.append(add_box("H66_CurbL", (2.6, 3.10, 0.22), (5.4, 0.26, 0.44), mats["Concrete"]))
    parts.append(add_box("H66_MeshL", (2.6, 3.10, 1.18), (5.3, 0.08, 1.55), mats["MeshGreen"]))
    parts.append(add_box("H66_CurbR", (-4.8, 3.10, 0.22), (5.2, 0.26, 0.44), mats["Concrete"]))
    parts.append(add_box("H66_MeshR", (-4.8, 3.10, 1.18), (5.1, 0.08, 1.55), mats["MeshGreen"]))
    for px in (-6.8, -4.4, -2.2, 0.0, 2.6, 5.2):
        parts.append(add_cyl(f"H66_FP_{px:.1f}", (px, 3.10, 1.15), 0.035, 0.035, 1.85, 8, mats["TrimDark"]))
    for ang_i in range(7):
        ang = math.radians(ang_i * 30.0)
        ax = -5.6 + math.cos(ang) * 1.35
        az = 1.95 + math.sin(ang) * 1.15
        parts.append(add_box(f"H66_Arch_{ang_i}", (ax, 1.95, az), (0.48, 0.14, 0.12), mats["VineBrown"], rot_euler=(0, -ang + math.pi * 0.5, 0)))
    return join_all(parts, "Sultanbeyli_House66_Yellow")


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
    print(f"  Exported HOLLOW WALK-IN {fbx_name:<34} | verts={len(obj.data.vertices):5d} | tris={tris:5d}")


def main():
    global mats_global
    builders = [
        ("Sultanbeyli_Selman48_House.fbx", build_hollow_selman48),
        ("Sultanbeyli_Shop58_ArchGate.fbx", build_hollow_shop58),
        ("Sultanbeyli_House66South_Hafsa.fbx", build_hollow_house66south_hafsa),
        ("Sultanbeyli_Garage73_LilacGate.fbx", build_hollow_garage73),
        ("Sultanbeyli_House66_Yellow.fbx", build_hollow_house66_yellow),
    ]
    for fbx_name, fn in builders:
        reset_scene()
        mats_global = get_mats()
        obj = fn(mats_global)
        export_fbx(obj, fbx_name)


if __name__ == "__main__":
    main()
