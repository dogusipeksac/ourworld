#!/usr/bin/env python3
"""
Upgrades EVERY remaining street-facing building on Selman Sokağı into a TRUE HOLLOW
WALK-IN SHELL (open ground-floor doorway, open garden gate gap, real interior floor & ceiling)
AND sculpts the 3 newly surveyed real Street View buildings from 46 & 95 Selman Sokağı:
  Upgraded Hollow Walk-In Shells:
    1. Sultanbeyli_HalfBrickHouse.fbx     (52 Selman Sk. & Çiçekçi Sk. corner — open ground doorway!)
    2. Sultanbeyli_CoralHouse.fbx         (54 & 50S Selman Sk. — open doorway into coral salon!)
    3. Sultanbeyli_RustGarage63.fbx       (63 Selman Sk. — open rusted steel double garage doors!)
    4. Sultanbeyli_House81_Rose.fbx       (81 Selman Sk. North — open garden & ground doorway!)
    5. Sultanbeyli_PeachCornerHouse.fbx   (Gürsu Sk. South corner — open doorway into peach home!)
    6. Sultanbeyli_BlueApartman81.fbx     (81 Selman Sk. South — open black double gate & ground lobby!)
    7. Sultanbeyli_House66South_Hafsa.fbx (BOTH Cobalt-Door House AND Hafsa Tesisat hollow walk-in!)
    8. Sultanbeyli_YellowApartman.fbx     (Hollow walk-in ground-floor apartment!)
    9. Sultanbeyli_ModernApartman.fbx     (Hollow walk-in ground-floor apartment!)
  3 Brand-New Hollow Walk-In Buildings from Extended Street View Tour:
   10. Sultanbeyli_GreyPinkHouse46.fbx    (46 Selman Sk. South — grey cement + pink band + open cobalt door!)
   11. Sultanbeyli_CornerShop95.fbx       (95 Selman Sk. / Abdülkadir Udeh corner — coral Çay Ocağı + brick shed!)
   12. Sultanbeyli_WhiteVillaEast.fbx     (Abdülkadir Udeh East corner — crisp white 2-story villa + white wall!)
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_FBX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")
BLEND_OUT_DIR = os.path.join(SCRIPT_DIR, "blend_sources")

mats_global = None


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
        "YellowPlaster": make_mat("YellowPlaster", (0.85, 0.76, 0.50), 0.74),
        "OrangeStripe": make_mat("OrangeStripe", (0.76, 0.42, 0.16), 0.72),
        "OliveFacade": make_mat("OliveFacade", (0.35, 0.44, 0.31), 0.68),
        "CreamPaint": make_mat("CreamPaint", (0.90, 0.88, 0.83), 0.72),
        "PeachPlaster": make_mat("PeachPlaster", (0.86, 0.72, 0.64), 0.74),
        "CoralPlaster": make_mat("CoralPlaster", (0.76, 0.42, 0.39), 0.74),
        "PinkPlaster": make_mat("PinkPlaster", (0.82, 0.62, 0.64), 0.74),
        "PowderBlue": make_mat("PowderBlue", (0.58, 0.68, 0.76), 0.72),
        "RedPlaster": make_mat("RedPlaster", (0.68, 0.21, 0.16), 0.74),
        "RawBrick": make_mat("RawBrick", (0.68, 0.31, 0.19), 0.86),
        "Concrete": make_mat("Concrete", (0.56, 0.56, 0.54), 0.84),
        "MeshGreen": make_mat("MeshGreen", (0.14, 0.28, 0.16), 0.82),
        "ShrubGreen": make_mat("ShrubGreen", (0.19, 0.34, 0.16), 0.86),
        "CobaltBlue": make_mat("CobaltBlue", (0.10, 0.38, 0.76), 0.44, 0.35),
        "TurquoiseRust": make_mat("TurquoiseRust", (0.22, 0.56, 0.52), 0.64, 0.42),
        "RustSteel": make_mat("RustSteel", (0.34, 0.20, 0.14), 0.72, 0.58),
        "Galvanized": make_mat("Galvanized", (0.68, 0.70, 0.72), 0.44, 0.68),
        "WhitePVC": make_mat("WhitePVC", (0.94, 0.94, 0.93), 0.36),
        "Glass": make_mat("Glass", (0.10, 0.15, 0.20), 0.10, 0.25),
        "TrimDark": make_mat("TrimDark", (0.14, 0.15, 0.16), 0.56, 0.35),
        "RoofTile": make_mat("RoofTile", (0.54, 0.25, 0.17), 0.74),
        "Wood": make_mat("Wood", (0.58, 0.38, 0.20), 0.64),
        "DarkWood": make_mat("DarkWood", (0.28, 0.19, 0.12), 0.72),
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


def add_hollow_ground(parts, prefix, x_min, x_max, y_back, y_front, z_bot, z_top, wall_t, ext_mat, int_mat, door_x, door_w, door_h, door_leaf_mat=None):
    w = x_max - x_min
    d = y_front - y_back
    h = z_top - z_bot
    cx = (x_min + x_max) * 0.5
    cy = (y_back + y_front) * 0.5
    cz = (z_bot + z_top) * 0.5

    parts.append(add_box(f"{prefix}_Floor", (cx, cy, z_bot + 0.04), (w, d, 0.08), mats_global["Wood"], bevel=0.0))
    parts.append(add_box(f"{prefix}_Ceil", (cx, cy, z_top - 0.05), (w, d, 0.10), int_mat, bevel=0.0))

    parts.append(add_box(f"{prefix}_Back", (cx, y_back + wall_t * 0.5, cz), (w, wall_t, h), ext_mat))
    parts.append(add_box(f"{prefix}_BackIn", (cx, y_back + wall_t + 0.02, cz), (w - wall_t * 2, 0.04, h - 0.16), int_mat, bevel=0.0))
    parts.append(add_box(f"{prefix}_Left", (x_max - wall_t * 0.5, cy, cz), (wall_t, d, h), ext_mat))
    parts.append(add_box(f"{prefix}_LeftIn", (x_max - wall_t - 0.02, cy, cz), (0.04, d - wall_t * 2, h - 0.16), int_mat, bevel=0.0))
    parts.append(add_box(f"{prefix}_Right", (x_min + wall_t * 0.5, cy, cz), (wall_t, d, h), ext_mat))
    parts.append(add_box(f"{prefix}_RightIn", (x_min + wall_t + 0.02, cy, cz), (0.04, d - wall_t * 2, h - 0.16), int_mat, bevel=0.0))

    d_lo = door_x - door_w * 0.5
    d_hi = door_x + door_w * 0.5
    fy = y_front - wall_t * 0.5
    if d_lo > x_min + 0.05:
        sw = d_lo - x_min
        scx = (x_min + d_lo) * 0.5
        parts.append(add_box(f"{prefix}_FR", (scx, fy, cz), (sw, wall_t, h), ext_mat))
        parts.append(add_box(f"{prefix}_FRIn", (scx, y_front - wall_t - 0.02, cz), (sw - 0.04, 0.04, h - 0.16), int_mat, bevel=0.0))
    if x_max > d_hi + 0.05:
        sw = x_max - d_hi
        scx = (d_hi + x_max) * 0.5
        parts.append(add_box(f"{prefix}_FL", (scx, fy, cz), (sw, wall_t, h), ext_mat))
        parts.append(add_box(f"{prefix}_FLIn", (scx, y_front - wall_t - 0.02, cz), (sw - 0.04, 0.04, h - 0.16), int_mat, bevel=0.0))
    lh = z_top - (z_bot + door_h)
    if lh > 0.05:
        parts.append(add_box(f"{prefix}_Lintel", (door_x, fy, z_bot + door_h + lh * 0.5), (door_w + 0.04, wall_t, lh), ext_mat))
    parts.append(add_box(f"{prefix}_JambL", (d_hi + 0.04, fy, z_bot + door_h * 0.5), (0.08, wall_t + 0.04, door_h), mats_global["DarkWood"]))
    parts.append(add_box(f"{prefix}_JambR", (d_lo - 0.04, fy, z_bot + door_h * 0.5), (0.08, wall_t + 0.04, door_h), mats_global["DarkWood"]))
    if door_leaf_mat is not None:
        # Swung-open door leaf tucked cleanly inside so player walks right in!
        parts.append(add_box(f"{prefix}_OpenLeaf", (d_hi - 0.10, y_front - 0.58, z_bot + door_h * 0.48), (0.07, 0.92, door_h - 0.08), door_leaf_mat))


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


# 1. HOLLOW WALK-IN HALF-BRICK HOUSE (No:52 & Çiçekçi Corner)
def build_hollow_half_brick(mats):
    parts = []
    # Ground floor hollow room X = -4.6..+4.6, Y = -6.8..+0.8, open doorway at X = -2.8, W = 1.45!
    add_hollow_ground(parts, "HB", -4.6, 4.6, -6.8, 0.80, 0.04, 3.50, 0.24, mats["Concrete"], mats["CreamPaint"], door_x=-2.8, door_w=1.45, door_h=2.35, door_leaf_mat=mats["DarkWood"])
    # Upper exposed red brick story (Z = 3.50..6.25)
    parts.append(add_box("HB_UpperBrick", (0.0, -3.0, 4.88), (9.2, 7.6, 2.75), mats["RawBrick"]))
    parts.append(add_box("HB_RoofSlab", (0.0, -3.0, 6.36), (9.6, 8.0, 0.22), mats["Concrete"]))
    add_window(parts, "HB_W1", 2.9, 0.82, 1.95, 1.30, 1.35, mats)
    add_window(parts, "HB_W2", 0.4, 0.82, 1.95, 2.05, 1.35, mats)
    parts.append(add_box("HB_Plaque", (-4.1, 0.82, 1.55), (0.45, 0.04, 0.20), mats["SignRed"]))
    for wx in (2.8, 0.4, -2.6):
        parts.append(add_box(f"HB_UW_{wx:.1f}", (wx, 0.82, 4.88), (1.35, 0.12, 1.40), mats["TrimDark"]))
    parts.append(add_box("HB_Board1", (-2.6, 0.90, 4.88), (1.50, 0.06, 0.14), mats["Wood"], rot_euler=(0, 0.78, 0)))
    parts.append(add_box("HB_Board2", (-2.6, 0.90, 4.88), (1.50, 0.06, 0.14), mats["Wood"], rot_euler=(0, -0.78, 0)))
    parts.append(add_cyl("HB_Dish1", (3.7, 1.02, 3.45), 0.45, 0.38, 0.08, 16, mats["RustSteel"], rot_euler=(math.pi * 0.45, 0, -0.25)))
    parts.append(add_cyl("HB_Dish2", (4.5, 1.02, 3.35), 0.40, 0.32, 0.08, 16, mats["WhitePVC"], rot_euler=(math.pi * 0.45, 0, -0.15)))
    return join_all(parts, "Sultanbeyli_HalfBrickHouse")


# 2. HOLLOW WALK-IN CORAL HOUSE (No:54 & No:50S)
def build_hollow_coral(mats):
    parts = []
    # Hollow ground room X = -3.8..+3.8, Y = -5.8..+1.0, open doorway at X = -2.4, W = 1.40!
    add_hollow_ground(parts, "CH", -3.8, 3.8, -5.8, 1.00, 0.04, 3.55, 0.22, mats["CoralPlaster"], mats["CreamPaint"], door_x=-2.4, door_w=1.40, door_h=2.35, door_leaf_mat=mats["DarkWood"])
    parts.append(add_box("CH_Eave", (0.0, -2.4, 3.64), (8.2, 7.4, 0.18), mats["WhitePVC"]))
    parts.append(add_box("CH_Roof", (0.0, -2.4, 4.18), (7.4, 6.6, 0.95), mats["RoofTile"], bevel=0.35))
    add_window(parts, "CH_W1", 2.0, 1.02, 2.05, 1.55, 1.25, mats)
    add_window(parts, "CH_W2", -0.3, 1.02, 2.05, 1.35, 1.25, mats, barred=True)
    parts.append(add_box("CH_ACUnit", (0.85, 1.18, 0.98), (0.82, 0.34, 0.56), mats["WhitePVC"]))
    parts.append(add_cyl("CH_ACFan", (1.02, 1.36, 0.98), 0.18, 0.18, 0.04, 14, mats["TrimDark"], rot_euler=(math.pi * 0.5, 0, 0)))
    return join_all(parts, "Sultanbeyli_CoralHouse")


# 3. HOLLOW OPEN-DOOR RUSTED STEEL GARAGE 63 + TURQUOISE GATE
def build_hollow_rust_garage_63(mats):
    parts = []
    add_hollow_ground(parts, "RG", -2.8, 2.8, -4.8, 0.40, 0.04, 3.30, 0.18, mats["RustSteel"], mats["Concrete"], door_x=0.0, door_w=3.20, door_h=2.65)
    parts.append(add_box("RG_Roof", (0.0, -2.2, 3.42), (6.1, 5.7, 0.16), mats["TrimDark"], rot_euler=(0.06, 0, 0)))
    # Swung-open rusted diamond-plate double doors folded outward at the sides!
    parts.append(add_box("RG_OpenDoorL", (1.95, 1.05, 1.32), (0.10, 1.45, 2.55), mats["RustSteel"], rot_euler=(0, 0, -0.35)))
    parts.append(add_box("RG_OpenDoorR", (-1.95, 1.05, 1.32), (0.10, 1.45, 2.55), mats["RustSteel"], rot_euler=(0, 0, 0.35)))
    for lz in (2.82, 3.04, 3.22):
        parts.append(add_box(f"RG_Louver_{lz:.2f}", (0.0, 0.44, lz), (5.3, 0.08, 0.09), mats["TrimDark"]))
    # Peeling turquoise garden gate on left (+X)
    parts.append(add_box("RG_TurqGateL", (4.05, 0.30, 0.85), (1.85, 0.10, 1.65), mats["TurquoiseRust"]))
    parts.append(add_box("RG_TurqGateR", (5.85, 0.30, 0.85), (1.65, 0.10, 1.65), mats["TurquoiseRust"]))
    return join_all(parts, "Sultanbeyli_RustGarage63")


# 4. HOLLOW WALK-IN 81 SELMAN NORTH DUSTY-ROSE GARDEN HOUSE
def build_hollow_house81_rose(mats):
    parts = []
    # Hollow ground floor X = -4.6..+4.6, Y = -8.6..-0.4, open front doorway at X = 0.0, W = 1.50!
    add_hollow_ground(parts, "H81G", -4.6, 4.6, -8.6, -0.40, 0.04, 3.25, 0.24, mats["PinkPlaster"], mats["CreamPaint"], door_x=0.0, door_w=1.50, door_h=2.35, door_leaf_mat=mats["DarkWood"])
    # Upper story + attic roof
    parts.append(add_box("H81_Upper", (0.0, -4.5, 4.80), (9.2, 8.2, 3.10), mats["PinkPlaster"]))
    parts.append(add_box("H81_Attic", (0.0, -4.5, 7.15), (7.6, 6.8, 1.65), mats["RoofTile"], bevel=0.05))
    add_window(parts, "H81_WL0", 2.7, -0.38, 1.75, 1.75, 1.35, mats, barred=True)
    add_window(parts, "H81_WR0", -2.7, -0.38, 1.75, 1.75, 1.35, mats, barred=True)
    add_window(parts, "H81_WL1", 2.2, -0.38, 4.80, 1.85, 1.35, mats)
    add_window(parts, "H81_WR1", -2.0, -0.38, 4.80, 2.10, 1.35, mats)
    # Brick garden shed on right
    parts.append(add_box("H81_Shed", (-6.8, -0.4, 1.25), (3.8, 5.4, 2.50), mats["Concrete"]))
    parts.append(add_box("H81_ShedRoof", (-6.8, -0.4, 2.65), (4.1, 5.7, 0.22), mats["RoofTile"], rot_euler=(0, 0.12, 0)))
    # Concrete garden posts along Y = +3.6 leaving center wide open!
    for px in (-4.2, -2.2, 2.2, 4.2):
        parts.append(add_box(f"H81_Post_{px:.1f}", (px, 3.6, 0.62), (0.14, 0.14, 1.24), mats["Concrete"]))
    return join_all(parts, "Sultanbeyli_House81_Rose")


# 5. HOLLOW WALK-IN PEACH CORNER HOUSE (Gürsu Sk. South Corner)
def build_hollow_peach_corner(mats):
    parts = []
    add_hollow_ground(parts, "PC", -4.7, 4.7, -6.1, 1.10, 0.04, 3.65, 0.24, mats["PeachPlaster"], mats["CreamPaint"], door_x=0.0, door_w=1.50, door_h=2.35, door_leaf_mat=mats["DarkWood"])
    parts.append(add_box("PC_Roof", (0.0, -2.5, 4.25), (9.8, 7.6, 1.15), mats["RoofTile"], bevel=0.35))
    add_window(parts, "PC_W1", 2.8, 1.12, 2.05, 1.95, 1.40, mats, barred=True)
    add_window(parts, "PC_W2", -2.8, 1.12, 2.05, 1.95, 1.40, mats, barred=True)
    return join_all(parts, "Sultanbeyli_PeachCornerHouse")


# 6. HOLLOW WALK-IN 81 SELMAN SOUTH POWDER-BLUE APARTMENT + OPEN BLACK DOUBLE GATE!
def build_hollow_blue_apt_81(mats):
    parts = []
    # Hollow ground floor X = -4.4..+4.4, Y = -7.3..+0.9, open doorway at X = 0.0, W = 1.60!
    add_hollow_ground(parts, "BAG", -4.4, 4.4, -7.3, 0.90, 0.04, 3.25, 0.24, mats["PowderBlue"], mats["CreamPaint"], door_x=0.0, door_w=1.60, door_h=2.35, door_leaf_mat=mats["TrimDark"])
    parts.append(add_box("BA_MidFloor", (0.0, -3.2, 4.85), (8.8, 8.2, 3.20), mats["PowderBlue"]))
    parts.append(add_box("BA_TopFloor", (0.0, -3.4, 7.85), (8.0, 7.4, 2.85), mats["PowderBlue"]))
    parts.append(add_box("BA_RoofTrim", (0.0, -3.4, 9.35), (8.5, 7.9, 0.22), mats["Concrete"]))
    parts.append(add_box("BA_BalcSlab", (1.6, 1.45, 3.25), (4.2, 1.3, 0.22), mats["Concrete"]))
    parts.append(add_box("BA_BalcWall", (1.6, 2.02, 3.85), (4.2, 0.16, 1.05), mats["PowderBlue"]))
    add_window(parts, "BA_W0L", 2.7, 0.92, 1.75, 1.45, 1.25, mats, barred=True)
    add_window(parts, "BA_W0R", -2.7, 0.92, 1.75, 1.45, 1.25, mats, barred=True)
    add_window(parts, "BA_W1", -2.2, 0.92, 4.65, 1.55, 1.30, mats)
    add_window(parts, "BA_W2", 1.8, 0.52, 7.95, 1.35, 1.15, mats)
    add_window(parts, "BA_W3", -1.6, 0.52, 7.95, 1.35, 1.15, mats)
    # Cream Courtyard Wall with WIDE OPEN BLACK DOUBLE GATE (swung open inside!)
    parts.append(add_box("BA_WallL", (4.5, 4.2, 1.05), (3.4, 0.28, 2.10), mats["CreamPaint"]))
    parts.append(add_box("BA_WallR", (-4.8, 4.2, 1.05), (4.2, 0.28, 2.10), mats["CreamPaint"]))
    parts.append(add_box("BA_OpenGateL", (2.65, 3.25, 1.18), (0.12, 1.95, 2.36), mats["TrimDark"]))
    parts.append(add_box("BA_OpenGateR", (-2.55, 3.25, 1.18), (0.12, 1.95, 2.36), mats["TrimDark"]))
    return join_all(parts, "Sultanbeyli_BlueApartman81")


# 7. BOTH SIDES HOLLOW WALK-IN: 66 SELMAN SOUTH COBALT-BLUE DOOR HOUSE + HAFSA TESİSAT!
def build_hollow_house66south_both(mats):
    parts = []
    # Main Peach/Cream house ground floor HOLLOW WALK-IN (X = -2.2..+5.6, Y = -6.0..+0.8, open cobalt-blue door at X = +0.55, W = 1.45!)
    add_hollow_ground(parts, "H66SG", -2.2, 5.6, -6.0, 0.80, 0.04, 3.15, 0.22, mats["PeachPlaster"], mats["CreamPaint"], door_x=0.55, door_w=1.45, door_h=2.30, door_leaf_mat=mats["CobaltBlue"])
    parts.append(add_box("H66S_Upper", (1.7, -2.6, 4.70), (7.8, 6.8, 3.10), mats["CreamPaint"]))
    parts.append(add_box("H66S_Eave", (1.7, -2.6, 6.32), (8.3, 7.3, 0.22), mats["Concrete"]))
    parts.append(add_box("H66S_Roof", (1.7, -2.6, 6.58), (7.9, 6.9, 0.38), mats["RoofTile"]))
    # Front porch wall on far left only (leaving path to cobalt-blue door completely clear!)
    parts.append(add_box("H66S_PorchWall", (3.8, 1.35, 0.62), (3.4, 0.20, 1.24), mats["PeachPlaster"]))
    parts.append(add_box("H66S_Hedge", (4.5, 1.45, 1.85), (2.25, 0.14, 1.45), mats["ShrubGreen"]))
    add_window(parts, "H66S_WL", 3.15, 0.82, 1.78, 1.75, 1.28, mats, barred=True, bar_mat_key="CobaltBlue")
    add_window(parts, "H66S_WR", -1.15, 0.82, 1.78, 1.35, 1.28, mats, barred=True, bar_mat_key="CobaltBlue")
    add_window(parts, "H66S_WU1", 2.55, 0.82, 4.88, 1.75, 1.30, mats)
    add_window(parts, "H66S_WU2", -0.55, 0.82, 4.88, 1.35, 1.25, mats)

    # HOLLOW WALK-IN HAFSA TESİSAT WORKSHOP (X = -8.6..-3.8, Y = -4.6..+0.6, open door at X = -6.2, W = 2.40!)
    add_hollow_ground(parts, "Hafsa", -8.6, -3.8, -4.6, 0.60, 0.04, 3.10, 0.20, mats["CobaltBlue"], mats["WhitePVC"], door_x=-6.2, door_w=2.40, door_h=2.35)
    parts.append(add_box("Hafsa_Roof", (-6.2, -2.0, 3.16), (5.2, 5.6, 0.16), mats["Galvanized"]))
    parts.append(add_box("Hafsa_SignBand", (-6.2, 0.66, 2.72), (4.4, 0.08, 0.62), mats["YellowPlaster"]))
    parts.append(add_box("Hafsa_UpperMesh", (-6.2, 0.55, 3.72), (5.0, 0.10, 0.95), mats["MeshGreen"]))
    return join_all(parts, "Sultanbeyli_House66South_Hafsa")


# 8. HOLLOW WALK-IN GROUND FLOOR: YELLOW APARTMAN
def build_hollow_yellow_apt(mats):
    parts = []
    # Ground floor hollow apartment X = -5.3..+5.3, Y = -4.9..+4.9, open doorway at X = 0.0, W = 1.60!
    add_hollow_ground(parts, "YAG", -5.3, 5.3, -4.9, 4.9, 0.04, 3.25, 0.26, mats["YellowPlaster"], mats["CreamPaint"], door_x=0.0, door_w=1.60, door_h=2.35, door_leaf_mat=mats["DarkWood"])
    parts.append(add_box("YA_Upper", (0.0, 0.0, 6.45), (10.6, 9.8, 6.40), mats["YellowPlaster"]))
    parts.append(add_box("YA_Cornice", (0.0, 0.0, 9.75), (11.1, 10.3, 0.28), mats["Concrete"]))
    parts.append(add_box("YA_Roof", (0.0, 0.0, 10.25), (10.2, 9.4, 0.85), mats["RoofTile"], bevel=0.35))
    # Signature vertical orange terracotta window bands
    for bx in (-3.0, 3.0):
        parts.append(add_box(f"YA_Band_{bx:.1f}", (bx, 4.92, 4.90), (2.2, 0.06, 9.40), mats["OrangeStripe"]))
        for fl in range(3):
            add_window(parts, f"YA_W_{bx:.1f}_{fl}", bx, 4.96, 1.75 + fl * 3.05, 1.55, 1.35, mats)
    return join_all(parts, "Sultanbeyli_YellowApartman")


# 9. HOLLOW WALK-IN GROUND FLOOR: MODERN WHITE & OLIVE APARTMAN
def build_hollow_modern_apt(mats):
    parts = []
    add_hollow_ground(parts, "MAG", -5.9, 5.9, -5.1, 5.1, 0.04, 3.30, 0.26, mats["CreamPaint"], mats["WhitePVC"], door_x=0.0, door_w=1.70, door_h=2.40, door_leaf_mat=mats["TrimDark"])
    parts.append(add_box("MA_Upper", (0.0, 0.0, 7.95), (11.8, 10.2, 9.30), mats["CreamPaint"]))
    parts.append(add_box("MA_OliveBand", (3.2, 5.12, 6.40), (3.4, 0.08, 12.2), mats["OliveFacade"]))
    parts.append(add_box("MA_Roof", (0.0, 0.0, 12.75), (12.2, 10.6, 0.30), mats["Concrete"]))
    for fl in range(4):
        wz = 1.80 + fl * 3.05
        add_window(parts, f"MA_WL_{fl}", 3.2, 5.18, wz, 1.75, 1.40, mats)
        add_window(parts, f"MA_WR_{fl}", -3.2, 5.18, wz, 1.75, 1.40, mats)
        if fl > 0:
            parts.append(add_box(f"MA_Balc_{fl}", (-3.2, 5.65, wz - 0.85), (3.2, 1.0, 0.16), mats["Concrete"]))
            parts.append(add_box(f"MA_Rail_{fl}", (-3.2, 6.10, wz - 0.35), (3.1, 0.08, 0.90), mats["Glass"]))
    return join_all(parts, "Sultanbeyli_ModernApartman")


# 10. NEW FROM 46 SELMAN SOKAĞI SOUTH PHOTO (step 1369): GREY-CEMENT & PINK-BAND 3-STORY HOUSE (HOLLOW WALK-IN!)
def build_grey_pink_house_46(mats):
    parts = []
    # Ground floor hollow room X = -4.5..+4.5, Y = -6.5..+0.8, open cobalt-blue door at X = -1.6, W = 1.40!
    add_hollow_ground(parts, "GP46G", -4.5, 4.5, -6.5, 0.80, 0.04, 3.10, 0.24, mats["CreamPaint"], mats["CreamPaint"], door_x=-1.6, door_w=1.40, door_h=2.30, door_leaf_mat=mats["CobaltBlue"])
    # Signature pastel-pink band across 2nd floor sill + weathered grey cement upper 2 stories!
    parts.append(add_box("GP46_PinkBand", (0.0, -2.85, 3.65), (9.1, 7.5, 1.15), mats["PinkPlaster"]))
    parts.append(add_box("GP46_UpperGrey", (0.0, -2.85, 6.85), (9.0, 7.4, 5.30), mats["Concrete"]))
    parts.append(add_box("GP46_Roof", (0.0, -2.85, 9.65), (9.5, 7.9, 0.28), mats["RoofTile"]))
    add_window(parts, "GP46_WG", 1.2, 0.82, 1.65, 1.40, 1.25, mats, barred=True)
    add_window(parts, "GP46_W1L", 2.2, 0.86, 5.10, 1.85, 1.35, mats)
    add_window(parts, "GP46_W1R", -1.8, 0.86, 5.10, 2.40, 1.35, mats)
    add_window(parts, "GP46_W2L", 2.2, 0.86, 8.10, 1.85, 1.35, mats)
    add_window(parts, "GP46_W2R", -1.8, 0.86, 8.10, 2.40, 1.35, mats)
    # Front garden arched trellis on left (+X = +3.2)
    for ang_i in range(7):
        ang = math.radians(ang_i * 30.0)
        ax = 3.1 + math.cos(ang) * 1.45
        az = 1.95 + math.sin(ang) * 0.95
        parts.append(add_box(f"GP46_Arch_{ang_i}", (ax, 2.55, az), (0.50, 0.08, 0.08), mats["TrimDark"], rot_euler=(0, -ang + math.pi * 0.5, 0)))
    return join_all(parts, "Sultanbeyli_GreyPinkHouse46")


# 11. NEW FROM 95 SELMAN SOKAĞI CORNER PHOTO (step 1360): CORAL CORNER ÇAY OCAĞI + BRICK ANNEX (HOLLOW WALK-IN!)
def build_corner_shop_95(mats):
    parts = []
    # Hollow walk-in coral corner shop X = -4.2..+4.2, Y = -5.6..+0.8, wide open glass storefront door at X = +0.8, W = 2.20!
    add_hollow_ground(parts, "CS95", -4.2, 4.2, -5.6, 0.80, 0.04, 3.35, 0.22, mats["CoralPlaster"], mats["CreamPaint"], door_x=0.8, door_w=2.20, door_h=2.40, door_leaf_mat=mats["TrimDark"])
    parts.append(add_box("CS95_Eave", (0.0, -2.4, 3.45), (8.9, 6.9, 0.16), mats["WhitePVC"]))
    parts.append(add_box("CS95_Roof", (0.0, -2.4, 3.88), (8.5, 6.5, 0.72), mats["RoofTile"], bevel=0.25))
    # Side black service door + red SELMAN SOKAĞI wall plaque
    parts.append(add_box("CS95_BlackDoor", (-2.6, 0.82, 1.15), (0.95, 0.08, 2.20), mats["TrimDark"]))
    parts.append(add_box("CS95_RedPlaque", (-1.1, 0.84, 2.45), (0.52, 0.04, 0.22), mats["SignRed"]))
    # Signature low exposed red-brick annex shed on right (-X = -6.8)
    parts.append(add_box("CS95_BrickShed", (-6.6, -1.6, 1.05), (4.6, 4.8, 2.10), mats["RawBrick"]))
    parts.append(add_box("CS95_ShedRoof", (-6.6, -1.6, 2.22), (4.9, 5.1, 0.22), mats["Concrete"]))
    return join_all(parts, "Sultanbeyli_CornerShop95")


# 12. NEW FROM 95 SELMAN SOKAĞI EAST PHOTO (step 1359): BRIGHT-WHITE 2-STORY CORNER VILLA + WHITE WALL (HOLLOW WALK-IN!)
def build_white_villa_east(mats):
    parts = []
    add_hollow_ground(parts, "WVE", -5.2, 5.2, -6.4, 1.20, 0.04, 3.25, 0.24, mats["WhitePVC"], mats["CreamPaint"], door_x=0.0, door_w=1.60, door_h=2.35, door_leaf_mat=mats["DarkWood"])
    parts.append(add_box("WV_Upper", (0.0, -2.6, 4.85), (10.4, 7.6, 3.20), mats["WhitePVC"]))
    parts.append(add_box("WV_Roof", (0.0, -2.6, 6.85), (10.9, 8.1, 0.90), mats["RoofTile"], bevel=0.35))
    for fl in range(2):
        wz = 1.75 + fl * 3.10
        add_window(parts, f"WV_WL_{fl}", 2.8, 1.22, wz, 1.65, 1.35, mats)
        add_window(parts, f"WV_WR_{fl}", -2.8, 1.22, wz, 1.65, 1.35, mats)
    # Crisp white concrete courtyard wall with wide open center walk-in entrance!
    parts.append(add_box("WV_WallL", (3.8, 4.1, 0.92), (4.2, 0.26, 1.84), mats["WhitePVC"]))
    parts.append(add_box("WV_WallR", (-3.8, 4.1, 0.92), (4.2, 0.26, 1.84), mats["WhitePVC"]))
    return join_all(parts, "Sultanbeyli_WhiteVillaEast")


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
        ("Sultanbeyli_HalfBrickHouse.fbx", build_hollow_half_brick),
        ("Sultanbeyli_CoralHouse.fbx", build_hollow_coral),
        ("Sultanbeyli_RustGarage63.fbx", build_hollow_rust_garage_63),
        ("Sultanbeyli_House81_Rose.fbx", build_hollow_house81_rose),
        ("Sultanbeyli_PeachCornerHouse.fbx", build_hollow_peach_corner),
        ("Sultanbeyli_BlueApartman81.fbx", build_hollow_blue_apt_81),
        ("Sultanbeyli_House66South_Hafsa.fbx", build_hollow_house66south_both),
        ("Sultanbeyli_YellowApartman.fbx", build_hollow_yellow_apt),
        ("Sultanbeyli_ModernApartman.fbx", build_hollow_modern_apt),
        ("Sultanbeyli_GreyPinkHouse46.fbx", build_grey_pink_house_46),
        ("Sultanbeyli_CornerShop95.fbx", build_corner_shop_95),
        ("Sultanbeyli_WhiteVillaEast.fbx", build_white_villa_east),
    ]
    print(f"[GRIDROAM Blender] Exporting {len(builders)} hollow walk-in Selman Sokağı buildings...")
    for fbx_name, fn in builders:
        reset_scene()
        mats_global = get_mats()
        obj = fn(mats_global)
        export_fbx(obj, fbx_name)


if __name__ == "__main__":
    main()
