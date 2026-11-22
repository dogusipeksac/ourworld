#!/usr/bin/env python3
"""
GRIDROAM Selman Sokağı 1-to-1 Photo-Accurate Blender 3D Asset Generator (Blender 5.3+)
Sculpts EVERY real building & street prop directly from our 10 Google Street View photos
of Selman Sokağı (Hamidiye Mah., Sultanbeyli, İstanbul — Ocak 2026):
  1.  Sultanbeyli_House66_Yellow.fbx       (66 Selman Sk. North: ochre-yellow 2-story house, cantilevered corner,
                                            exterior iron-railed stairs, dark-green mesh fence & vine arch)
  2.  Sultanbeyli_House66South_Hafsa.fbx   (66 Selman Sk. South: cream/peach 2-story house, cobalt-blue steel door,
                                            blue window bars, porch gate, hedge screen & 'HAFSA TESİSAT' shop!)
  3.  Sultanbeyli_HalfBrickHouse.fbx       (1 Gürsu Sk. / 52 Selman Sk.: grey cement ground floor + rusty dish,
                                            exposed raw red-brick upper floor with X-boarded timber window!)
  4.  Sultanbeyli_CoralHouse.fbx           (54 Selman Sk. North: coral-pink 1.5-story house, exterior stairs & AC unit)
  5.  Sultanbeyli_RustGarage63.fbx         (63 Selman Sk. North: rusted brown diamond-plate steel garage + turquoise gate)
  6.  Sultanbeyli_Shop58_ArchGate.fbx      (58 Selman Sk. North: orange shuttered dükkan + white fascia + ornate black/gold arch gate)
  7.  Sultanbeyli_Garage73_LilacGate.fbx   (73 Selman Sk. North: terracotta shutter garage + 'No Park' sign + lilac double gate)
  8.  Sultanbeyli_PinkApartman.fbx         (58/63 Selman Sk. backdrop: tall pastel-pink 3-story narrow apartment)
  9.  Sultanbeyli_BlueApartman81.fbx       (81 Selman Sk. South: powder-blue 3-story stepped apt + cream wall + black gate)
  10. Sultanbeyli_House81_Rose.fbx         (81 Selman Sk. North: dusty-rose 2.5-story garden house + brick garden shed)
  11. Sultanbeyli_PeachCornerHouse.fbx     (Gürsu Sk. South corner: peach single-story house + curved black iron window grilles)
  12. Sultanbeyli_HighVoltagePylon.fbx     (Iconic east-end Selman Sokağı lattice high-voltage transmission tower!)
  13. Sultanbeyli_Dumpster.fbx             (Sultanbeyli Belediyesi wheeled galvanized steel dumpster w/ red-white stripes)
  14. Sultanbeyli_SpeedBumpSign.fbx        (Çiçekçi Sk. corner red-triangle 'Kasis' warning sign)
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
    "GRIDROAM_SELMAN_STREET_PREVIEW",
    os.path.join(SCRIPT_DIR, "blender_selman_street_preview.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.62, metallic=0.0):
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
        "CreamPaint": make_mat("CreamPaint", (0.88, 0.87, 0.82), 0.72),
        "PeachPlaster": make_mat("PeachPlaster", (0.86, 0.72, 0.64), 0.74),
        "CoralPlaster": make_mat("CoralPlaster", (0.76, 0.42, 0.39), 0.74),
        "PinkPlaster": make_mat("PinkPlaster", (0.82, 0.62, 0.64), 0.74),
        "PowderBlue": make_mat("PowderBlue", (0.58, 0.68, 0.76), 0.72),
        "OrangeStripe": make_mat("OrangeStripe", (0.76, 0.42, 0.16), 0.72),
        "RedPlaster": make_mat("RedPlaster", (0.68, 0.21, 0.16), 0.74),
        "SagePlaster": make_mat("SagePlaster", (0.64, 0.67, 0.56), 0.74),
        "RawBrick": make_mat("RawBrick", (0.68, 0.31, 0.19), 0.86),
        "Concrete": make_mat("Concrete", (0.56, 0.56, 0.54), 0.84),
        "MeshGreen": make_mat("MeshGreen", (0.14, 0.28, 0.16), 0.82),
        "ShrubGreen": make_mat("ShrubGreen", (0.19, 0.34, 0.16), 0.86),
        "CobaltBlue": make_mat("CobaltBlue", (0.10, 0.38, 0.76), 0.44, 0.35),
        "LilacGate": make_mat("LilacGate", (0.66, 0.58, 0.76), 0.46, 0.45),
        "TurquoiseRust": make_mat("TurquoiseRust", (0.22, 0.56, 0.52), 0.64, 0.42),
        "RustSteel": make_mat("RustSteel", (0.34, 0.20, 0.14), 0.72, 0.58),
        "Galvanized": make_mat("Galvanized", (0.68, 0.70, 0.72), 0.44, 0.68),
        "WhitePVC": make_mat("WhitePVC", (0.94, 0.94, 0.93), 0.36),
        "Glass": make_mat("Glass", (0.10, 0.15, 0.20), 0.10, 0.25),
        "TrimDark": make_mat("TrimDark", (0.14, 0.15, 0.16), 0.56, 0.35),
        "GoldBrass": make_mat("GoldBrass", (0.82, 0.64, 0.20), 0.35, 0.82),
        "RoofTile": make_mat("RoofTile", (0.54, 0.25, 0.17), 0.74),
        "Wood": make_mat("Wood", (0.58, 0.38, 0.20), 0.66),
        "DarkWood": make_mat("DarkWood", (0.28, 0.19, 0.12), 0.72),
        "SignRed": make_mat("SignRed", (0.76, 0.12, 0.12), 0.42, 0.15),
        "VineBrown": make_mat("VineBrown", (0.30, 0.22, 0.16), 0.90),
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


def add_box(name, center, size, mat, bevel=0.015, rot_euler=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.22), segments=1, affect="EDGES")
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


def add_window(parts, prefix, x, y, z, w, h, mats, barred=False, bar_mat_key="TrimDark", facing_y_sign=1.0):
    fy = facing_y_sign
    parts.append(add_box(f"{prefix}_Frame", (x, y, z), (w, 0.12, h), mats["WhitePVC"], bevel=0.015))
    parts.append(add_box(f"{prefix}_Glass", (x, y + fy * 0.045, z), (w - 0.18, 0.04, h - 0.18), mats["Glass"], bevel=0.005))
    parts.append(add_box(f"{prefix}_Mull", (x, y + fy * 0.07, z), (0.06, 0.04, h - 0.12), mats["WhitePVC"], bevel=0.005))
    if barred:
        n_bars = max(3, int(w / 0.26))
        for i in range(n_bars):
            t = (i + 0.5) / n_bars - 0.5
            bx = x + t * (w - 0.12)
            parts.append(add_cyl(f"{prefix}_BV_{i}", (bx, y + fy * 0.12, z), 0.012, 0.012, h - 0.04, 6, mats[bar_mat_key]))
        for hz in (-h * 0.34, 0.0, h * 0.34):
            parts.append(add_cyl(f"{prefix}_BH_{hz:.2f}", (x, y + fy * 0.12, z + hz), 0.012, 0.012, w - 0.04, 6, mats[bar_mat_key], rot_euler=(0, math.pi * 0.5, 0)))


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


# All models face +Y (Unity +Z). Standing on the street at +Y looking toward -Y:
# +X is viewer-LEFT, -X is viewer-RIGHT.

# ---------------------------------------------------------------------------
# 1. 66 SELMAN SOKAĞI NORTH (Ochre-yellow 2-story house, recessed corner stairs,
#    dark-green mesh privacy fence, red street plaque & arched vine trellis!)
# ---------------------------------------------------------------------------
def build_house66_yellow(mats):
    parts = []
    # Main ground floor (X = -2.2..+4.2, Y = -6.0..+0.4, Z = 0..3.4)
    parts.append(add_box("H66_Ground", (1.0, -2.8, 1.75), (6.4, 6.4, 3.5), mats["YellowPlaster"], bevel=0.03))
    # Cantilevered upper floor overhanging right side over porch/stairs (X = -4.0..+4.2, Z = 3.4..6.6)
    parts.append(add_box("H66_Upper", (0.1, -2.8, 5.05), (8.2, 6.6, 3.2), mats["YellowPlaster"], bevel=0.03))
    # Rear right wing supporting back of stairs
    parts.append(add_box("H66_RightBack", (-3.1, -4.4, 1.75), (1.8, 3.2, 3.5), mats["YellowPlaster"], bevel=0.02))
    # Roof cornice overhang
    parts.append(add_box("H66_RoofSlab", (0.1, -2.8, 6.78), (8.8, 7.2, 0.26), mats["Concrete"], bevel=0.02))
    parts.append(add_box("H66_RoofCap", (0.1, -2.8, 7.10), (8.2, 6.6, 0.45), mats["RoofTile"], bevel=0.06))

    # Front Windows (matching exact 66 Selman Sokağı photo!)
    add_window(parts, "H66_W1", 2.6, 0.42, 2.05, 1.65, 1.35, mats, barred=True)       # Ground left barred window
    add_window(parts, "H66_W2", 2.6, 0.52, 5.15, 1.65, 1.35, mats, barred=False)      # Upper left window
    add_window(parts, "H66_W3", 0.3, 0.52, 5.25, 0.65, 0.85, mats, barred=False)      # Small upper center bathroom window
    add_window(parts, "H66_W4", -2.2, 0.52, 5.15, 1.85, 1.35, mats, barred=False)     # Upper right wide window

    # Recessed exterior staircase going up-left into front door under cantilever
    for step in range(8):
        sx = -3.4 + step * 0.24
        sz = 0.20 + step * 0.22
        parts.append(add_box(f"H66_Stair_{step}", (sx, -0.35, sz), (0.30, 1.15, 0.22), mats["Concrete"], bevel=0.01))
    # Black iron stair handrail
    parts.append(add_box("H66_RailTop", (-2.4, 0.25, 1.95), (2.25, 0.05, 0.05), mats["TrimDark"], rot_euler=(0, -0.68, 0)))
    for rx in (-3.2, -2.6, -2.0, -1.4):
        parts.append(add_cyl(f"H66_RailPost_{rx:.1f}", (rx, 0.25, 1.25), 0.016, 0.016, 1.15, 6, mats["TrimDark"]))
    # Front doorway under landing
    parts.append(add_box("H66_Door", (-1.15, -0.55, 2.55), (0.12, 0.95, 2.05), mats["DarkWood"], bevel=0.01))
    # Signature red 'SELMAN SOKAĞI' plaque on wall
    parts.append(add_box("H66_RedPlaque", (-0.15, 0.44, 2.45), (0.52, 0.04, 0.22), mats["SignRed"], bevel=0.005))

    # Signature full-width Dark-Green Mesh Privacy Fence (yeşil çit filesi) along Y = +3.1!
    parts.append(add_box("H66_WallCurb", (-0.5, 3.10, 0.22), (13.6, 0.26, 0.44), mats["Concrete"], bevel=0.015))
    parts.append(add_box("H66_GreenMesh", (-0.5, 3.10, 1.18), (13.4, 0.08, 1.55), mats["MeshGreen"], bevel=0.01))
    for px in (-6.8, -4.4, -2.0, 0.4, 2.8, 5.2):
        parts.append(add_cyl(f"H66_FencePost_{px:.1f}", (px, 3.10, 1.15), 0.035, 0.035, 1.85, 8, mats["TrimDark"]))

    # Arched garden vine trellis on the right side (-X = -5.6)
    for ang_i in range(7):
        ang = math.radians(ang_i * 30.0)
        ax = -5.6 + math.cos(ang) * 1.35
        az = 1.95 + math.sin(ang) * 1.15
        parts.append(add_box(f"H66_Arch_{ang_i}", (ax, 1.95, az), (0.48, 0.14, 0.12), mats["VineBrown"], rot_euler=(0, -ang + math.pi * 0.5, 0)))

    return join_all(parts, "Sultanbeyli_House66_Yellow")


# ---------------------------------------------------------------------------
# 2. 66 SELMAN SOKAĞI SOUTH (Cream/Peach 2-story house, Cobalt-Blue front door,
#    blue-barred PVC windows, recessed porch gate, dark-green hedge panel,
#    AND 'HAFSA TESİSAT' blue/yellow plumber shop right next door!)
# ---------------------------------------------------------------------------
def build_house66_south_hafsa(mats):
    parts = []
    # Main 2-story house: X = -2.2..+5.6 (7.8m wide), Y = -6.0..+0.8
    # Lower half peach-pink plaster, upper half weathered cream-grey plaster!
    parts.append(add_box("H66S_Lower", (1.7, -2.6, 1.55), (7.8, 6.8, 3.10), mats["PeachPlaster"], bevel=0.025))
    parts.append(add_box("H66S_Upper", (1.7, -2.6, 4.65), (7.8, 6.8, 3.10), mats["CreamPaint"], bevel=0.025))
    parts.append(add_box("H66S_Eave", (1.7, -2.6, 6.28), (8.3, 7.3, 0.22), mats["Concrete"], bevel=0.02))
    parts.append(add_box("H66S_Roof", (1.7, -2.6, 6.55), (7.9, 6.9, 0.38), mats["RoofTile"], bevel=0.05))

    # Recessed left porch niche + peach parapet wall + beige metal porch gate + dark-green fake hedge screen on far left!
    parts.append(add_box("H66S_PorchWall", (3.6, 1.35, 0.62), (3.8, 0.20, 1.24), mats["PeachPlaster"], bevel=0.015))
    parts.append(add_box("H66S_PorchGate", (1.85, 1.35, 0.65), (1.05, 0.08, 1.26), mats["CreamPaint"], bevel=0.01))
    parts.append(add_box("H66S_HedgeScreen", (4.5, 1.45, 1.85), (2.25, 0.14, 1.45), mats["ShrubGreen"], bevel=0.03))

    # Iconic COBALT-BLUE Metal Front Door + doorstep!
    parts.append(add_box("H66S_DoorStep", (0.55, 1.15, 0.12), (1.35, 0.65, 0.24), mats["Concrete"], bevel=0.01))
    parts.append(add_box("H66S_BlueDoor", (0.55, 0.85, 1.22), (0.98, 0.10, 2.08), mats["CobaltBlue"], bevel=0.015))
    parts.append(add_box("H66S_BlueVent", (-0.42, 0.84, 0.56), (0.56, 0.06, 0.34), mats["CobaltBlue"], bevel=0.01))

    # Front windows (ground windows have iconic blue bars!)
    add_window(parts, "H66S_WL", 2.75, 0.82, 1.78, 1.85, 1.28, mats, barred=True, bar_mat_key="CobaltBlue")
    add_window(parts, "H66S_WR", -1.05, 0.82, 1.78, 1.45, 1.28, mats, barred=True, bar_mat_key="CobaltBlue")
    add_window(parts, "H66S_WU1", 2.55, 0.82, 4.85, 1.75, 1.30, mats, barred=False)
    add_window(parts, "H66S_WU2", -0.55, 0.82, 4.85, 1.35, 1.25, mats, barred=False)

    # Adjacent right side ('HAFSA TESİSAT' plumber shop & concrete terrace wall with green mesh above!)
    parts.append(add_box("Hafsa_RetainWall", (-5.2, 0.45, 1.35), (6.0, 0.35, 2.70), mats["Concrete"], bevel=0.02))
    parts.append(add_box("Hafsa_UpperMesh", (-5.2, 0.45, 3.15), (6.0, 0.10, 1.10), mats["MeshGreen"], bevel=0.01))
    # Bright Cobalt-Blue 'HAFSA TESİSAT' storefront booth
    parts.append(add_box("Hafsa_ShopBody", (-6.2, -1.6, 1.45), (3.6, 4.4, 2.90), mats["CobaltBlue"], bevel=0.02))
    parts.append(add_box("Hafsa_ShopRoof", (-6.2, -1.6, 2.96), (3.9, 4.7, 0.16), mats["Galvanized"], bevel=0.015))
    parts.append(add_box("Hafsa_SignYellow", (-6.2, 0.64, 2.25), (3.1, 0.08, 0.72), mats["YellowPlaster"], bevel=0.01))
    parts.append(add_box("Hafsa_ShopGlass", (-6.2, 0.62, 1.10), (2.7, 0.06, 1.55), mats["Glass"], bevel=0.01))

    return join_all(parts, "Sultanbeyli_House66South_Hafsa")


# ---------------------------------------------------------------------------
# 3. 1 GÜRSU SK. / 52 SELMAN SK. (Half-finished 2-story: grey cement plaster ground
#    + satellite dish, raw exposed red hollow-brick upper floor w/ X-boarded window!)
# ---------------------------------------------------------------------------
def build_half_brick_house(mats):
    parts = []
    # Ground floor: weathered grey cement plaster
    parts.append(add_box("HB_Ground", (0.0, -3.0, 1.75), (9.2, 7.6, 3.50), mats["Concrete"], bevel=0.03))
    # Upper floor: raw exposed hollow red brick!
    parts.append(add_box("HB_UpperBrick", (0.0, -3.0, 4.85), (9.2, 7.6, 2.75), mats["RawBrick"], bevel=0.02))
    parts.append(add_box("HB_RoofSlab", (0.0, -3.0, 6.32), (9.6, 8.0, 0.22), mats["Concrete"], bevel=0.02))

    # Ground floor white PVC windows + red street plaque
    add_window(parts, "HB_W1", 2.9, 0.82, 1.95, 1.30, 1.35, mats)
    add_window(parts, "HB_W2", 0.3, 0.82, 1.95, 2.15, 1.35, mats)
    add_window(parts, "HB_W3", -2.8, 0.82, 1.95, 1.30, 1.35, mats)
    parts.append(add_box("HB_Plaque", (-4.1, 0.82, 1.55), (0.45, 0.04, 0.20), mats["SignRed"]))

    # Upper floor unfinished window cutouts + X-boarded wooden braces (exact photo detail!)
    for wx in (2.8, 0.4, -2.6):
        parts.append(add_box(f"HB_UW_{wx:.1f}", (wx, 0.82, 4.85), (1.35, 0.12, 1.40), mats["TrimDark"]))
    # X timber boards over right upper window
    parts.append(add_box("HB_Board1", (-2.6, 0.90, 4.85), (1.50, 0.06, 0.14), mats["Wood"], rot_euler=(0, 0.78, 0)))
    parts.append(add_box("HB_Board2", (-2.6, 0.90, 4.85), (1.50, 0.06, 0.14), mats["Wood"], rot_euler=(0, -0.78, 0)))
    # Twin satellite dishes on left wall
    parts.append(add_cyl("HB_Dish1", (3.7, 1.02, 3.45), 0.45, 0.38, 0.08, 16, mats["RustSteel"], rot_euler=(math.pi * 0.45, 0, -0.25)))
    parts.append(add_cyl("HB_Dish2", (4.5, 1.02, 3.35), 0.40, 0.32, 0.08, 16, mats["WhitePVC"], rot_euler=(math.pi * 0.45, 0, -0.15)))

    return join_all(parts, "Sultanbeyli_HalfBrickHouse")


# ---------------------------------------------------------------------------
# 4. 54 SELMAN SK. CORAL-PINK DETACHED HOUSE (Coral plaster, exterior side stairs,
#    white AC unit on facade & hipped roof — right next to Half-Brick House!)
# ---------------------------------------------------------------------------
def build_coral_house(mats):
    parts = []
    parts.append(add_box("CH_Basement", (0.0, -2.4, 0.70), (7.6, 6.8, 1.40), mats["CoralPlaster"], bevel=0.02))
    parts.append(add_box("CH_Main", (0.0, -2.4, 2.75), (7.6, 6.8, 2.85), mats["CoralPlaster"], bevel=0.03))
    # Hipped roof
    parts.append(add_box("CH_Eave", (0.0, -2.4, 4.26), (8.2, 7.4, 0.18), mats["WhitePVC"], bevel=0.02))
    parts.append(add_box("CH_Roof", (0.0, -2.4, 4.75), (7.2, 6.4, 0.90), mats["RoofTile"], bevel=0.35))
    # Windows + white AC outdoor unit
    add_window(parts, "CH_W1", 2.0, 1.02, 2.90, 1.55, 1.25, mats)
    add_window(parts, "CH_W2", -0.6, 1.02, 2.90, 1.45, 1.25, mats, barred=True)
    parts.append(add_box("CH_ACUnit", (0.55, 1.18, 1.72), (0.82, 0.34, 0.56), mats["WhitePVC"], bevel=0.02))
    parts.append(add_cyl("CH_ACFan", (0.72, 1.36, 1.72), 0.18, 0.18, 0.04, 14, mats["TrimDark"], rot_euler=(math.pi * 0.5, 0, 0)))
    # Side entry door + exterior concrete stair parapet on right (-X)
    parts.append(add_box("CH_Door", (-2.6, 1.02, 2.45), (0.95, 0.10, 2.05), mats["DarkWood"]))
    parts.append(add_box("CH_StairWall", (-2.8, 1.55, 1.05), (2.2, 0.24, 1.45), mats["CoralPlaster"], bevel=0.02))
    return join_all(parts, "Sultanbeyli_CoralHouse")


# ---------------------------------------------------------------------------
# 5. 63 SELMAN SK. RUSTED STEEL GARAGE + TURQUOISE GATE (Exact match to photo!)
# ---------------------------------------------------------------------------
def build_rust_garage_63(mats):
    parts = []
    # Corrugated steel box garage with rusted diamond-plate double doors
    parts.append(add_box("RG_Body", (0.0, -2.2, 1.65), (5.6, 5.2, 3.30), mats["RustSteel"], bevel=0.02))
    parts.append(add_box("RG_Roof", (0.0, -2.2, 3.42), (6.1, 5.7, 0.16), mats["TrimDark"], bevel=0.015, rot_euler=(0.06, 0, 0)))
    # Horizontal louver slats above double doors
    for lz in (2.78, 2.98, 3.18):
        parts.append(add_box(f"RG_Louver_{lz:.2f}", (0.0, 0.44, lz), (5.3, 0.08, 0.09), mats["TrimDark"]))
    # Double door vertical center seam & rusted side posts
    parts.append(add_box("RG_Seam", (0.0, 0.45, 1.35), (0.08, 0.08, 2.70), mats["TrimDark"]))
    # Adjacent peeling turquoise metal garden gate on LEFT (+X)
    parts.append(add_box("RG_TurqGateL", (4.05, 0.30, 0.85), (1.85, 0.10, 1.65), mats["TurquoiseRust"], bevel=0.015))
    parts.append(add_box("RG_TurqGateR", (5.85, 0.30, 0.85), (1.65, 0.10, 1.65), mats["TurquoiseRust"], bevel=0.015))
    return join_all(parts, "Sultanbeyli_RustGarage63")


# ---------------------------------------------------------------------------
# 6. 58 SELMAN SK. ORANGE SHUTTERED DÜKKAN + ORNATE BLACK/GOLD ARCHED GATE
# ---------------------------------------------------------------------------
def build_shop58_archgate(mats):
    parts = []
    # Orange single-story workshop/shop on LEFT (+X = +0.5..+7.5)
    parts.append(add_box("S58_Body", (4.0, -2.8, 1.75), (7.0, 6.8, 3.50), mats["OrangeStripe"], bevel=0.02))
    # Weathered white horizontal corrugated fascia sign across top front!
    parts.append(add_box("S58_SignFascia", (4.0, 0.72, 2.98), (7.3, 0.22, 1.15), mats["CreamPaint"], bevel=0.015))
    # Grey metal corrugated roller shutter on right half + barred glass shop door on left half
    parts.append(add_box("S58_Shutter", (1.65, 0.66, 1.25), (2.05, 0.12, 2.45), mats["Galvanized"], bevel=0.01))
    parts.append(add_box("S58_ShopDoor", (4.55, 0.64, 1.25), (2.75, 0.10, 2.40), mats["RustSteel"], bevel=0.01))
    # Iconic ornate BLACK & GOLD wrought-iron garden gate + spiked arch trellis on RIGHT (-X = -3.8..-0.6)!
    parts.append(add_box("S58_GatePanel", (-2.2, 0.45, 1.05), (3.10, 0.10, 2.10), mats["TrimDark"], bevel=0.015))
    for gx in (-3.15, -2.20, -1.25):
        parts.append(add_box(f"S58_GoldCrest_{gx:.2f}", (gx, 0.52, 0.95), (0.28, 0.04, 0.48), mats["GoldBrass"], bevel=0.01))
    parts.append(add_cyl("S58_GatePostL", (-0.60, 0.45, 1.25), 0.08, 0.08, 2.50, 8, mats["TrimDark"]))
    parts.append(add_cyl("S58_GatePostR", (-3.80, 0.45, 1.25), 0.08, 0.08, 2.50, 8, mats["TrimDark"]))
    for ang_i in range(9):
        ang = math.radians(ang_i * 22.5)
        ax = -2.2 + math.cos(ang) * 1.62
        az = 2.25 + math.sin(ang) * 1.25
        parts.append(add_box(f"S58_Arch_{ang_i}", (ax, 0.45, az), (0.44, 0.08, 0.08), mats["TrimDark"], rot_euler=(0, -ang + math.pi * 0.5, 0)))
    return join_all(parts, "Sultanbeyli_Shop58_ArchGate")


# ---------------------------------------------------------------------------
# 7. 73 SELMAN SK. TERRACOTTA ROLLER-SHUTTER GARAGE + LILAC DOUBLE GATE
# ---------------------------------------------------------------------------
def build_garage73_lilac(mats):
    parts = []
    # Terracotta plastered 1-story garage (X = -3.2..+3.2)
    parts.append(add_box("G73_Body", (0.0, -2.6, 1.68), (6.4, 6.2, 3.36), mats["CoralPlaster"], bevel=0.02))
    parts.append(add_box("G73_RoofTrim", (0.0, -2.6, 3.44), (7.1, 6.8, 0.14), mats["OrangeStripe"], bevel=0.015))
    # Corrugated grey roller shutter + shutter box + red No-Parking disc!
    parts.append(add_box("G73_ShutterBox", (0.0, 0.62, 2.74), (3.85, 0.34, 0.42), mats["Galvanized"], bevel=0.015))
    parts.append(add_box("G73_Shutter", (0.0, 0.55, 1.28), (3.60, 0.12, 2.55), mats["Galvanized"], bevel=0.01))
    parts.append(add_cyl("G73_NoParkDisc", (0.0, 0.64, 1.55), 0.24, 0.24, 0.03, 16, mats["SignRed"], rot_euler=(math.pi * 0.5, 0, 0)))
    parts.append(add_cyl("G73_NoParkInner", (0.0, 0.655, 1.55), 0.16, 0.16, 0.02, 16, mats["WhitePVC"], rot_euler=(math.pi * 0.5, 0, 0)))
    # Iconic LILAC/LAVENDER diamond-stamped double garden gate on LEFT (+X = +3.3..+6.4)!
    parts.append(add_box("G73_LilacWall", (4.85, 0.45, 0.88), (3.10, 0.10, 1.76), mats["LilacGate"], bevel=0.015))
    parts.append(add_box("G73_SideWallR", (-4.8, 0.45, 1.05), (3.2, 0.26, 2.10), mats["CoralPlaster"], bevel=0.02))
    return join_all(parts, "Sultanbeyli_Garage73_LilacGate")


# ---------------------------------------------------------------------------
# 8. PASTEL-PINK 3-STORY NARROW APARTMENT (Iconic backdrop behind No:58 & No:63!)
# ---------------------------------------------------------------------------
def build_pink_apartman(mats):
    parts = []
    w, d, h = 8.4, 8.8, 9.8
    parts.append(add_box("PA_Body", (0, 0, h * 0.5), (w, d, h), mats["PinkPlaster"], bevel=0.04))
    parts.append(add_box("PA_Cornice", (0, 0, h + 0.16), (w + 0.5, d + 0.5, 0.32), mats["CreamPaint"], bevel=0.02))
    parts.append(add_box("PA_Chimney", (1.8, 1.2, h + 1.05), (0.75, 0.75, 1.55), mats["Concrete"], bevel=0.02))
    for floor in range(3):
        wz = 1.8 + floor * 3.05
        add_window(parts, f"PA_WL_{floor}", 2.2, d * 0.5 + 0.02, wz, 1.20, 1.35, mats)
        add_window(parts, f"PA_WC_{floor}", -0.4, d * 0.5 + 0.02, wz, 2.25, 1.35, mats)
        add_window(parts, f"PA_WR_{floor}", -2.9, d * 0.5 + 0.02, wz, 1.20, 1.35, mats)
    return join_all(parts, "Sultanbeyli_PinkApartman")


# ---------------------------------------------------------------------------
# 9. 81 SELMAN SK. SOUTH — POWDER-BLUE 3-STORY STEPPED APARTMENT + CREAM WALL & BLACK GATE
# ---------------------------------------------------------------------------
def build_blue_apartman_81(mats):
    parts = []
    w, d = 8.8, 8.2
    parts.append(add_box("BA_Lower2", (0.0, -3.2, 3.20), (w, d, 6.40), mats["PowderBlue"], bevel=0.03))
    # Stepped top penthouse floor
    parts.append(add_box("BA_TopFloor", (0.0, -3.4, 7.80), (w - 0.8, d - 0.8, 2.85), mats["PowderBlue"], bevel=0.03))
    parts.append(add_box("BA_RoofTrim", (0.0, -3.4, 9.30), (w - 0.3, d - 0.3, 0.22), mats["Concrete"], bevel=0.02))
    # Front balcony parapet on 2nd floor
    parts.append(add_box("BA_BalcSlab", (1.6, 1.45, 3.25), (4.2, 1.3, 0.22), mats["Concrete"], bevel=0.02))
    parts.append(add_box("BA_BalcWall", (1.6, 2.02, 3.85), (4.2, 0.16, 1.05), mats["PowderBlue"], bevel=0.02))
    # Windows
    add_window(parts, "BA_W1", -2.2, 0.92, 4.55, 1.55, 1.30, mats)
    add_window(parts, "BA_W2", 1.8, 0.52, 7.90, 1.35, 1.15, mats)
    add_window(parts, "BA_W3", -1.6, 0.52, 7.90, 1.35, 1.15, mats)
    # Iconic Cream Concrete Courtyard Wall + Black Perforated Steel Double Gate at Y = +4.2!
    parts.append(add_box("BA_WallL", (4.5, 4.2, 1.05), (3.4, 0.28, 2.10), mats["CreamPaint"], bevel=0.02))
    parts.append(add_box("BA_WallR", (-4.8, 4.2, 1.05), (4.2, 0.28, 2.10), mats["CreamPaint"], bevel=0.02))
    parts.append(add_box("BA_GateL", (1.1, 4.2, 1.18), (2.2, 0.10, 2.36), mats["TrimDark"], bevel=0.015))
    parts.append(add_box("BA_GateR", (-1.3, 4.2, 1.18), (2.2, 0.10, 2.36), mats["TrimDark"], bevel=0.015))
    return join_all(parts, "Sultanbeyli_BlueApartman81")


# ---------------------------------------------------------------------------
# 10. 81 SELMAN SK. NORTH — DUSTY-ROSE 2.5-STORY GARDEN HOUSE + BRICK GARDEN SHED
# ---------------------------------------------------------------------------
def build_house81_rose(mats):
    parts = []
    parts.append(add_box("H81_Body", (0.0, -4.5, 3.15), (9.2, 8.2, 6.30), mats["PinkPlaster"], bevel=0.03))
    parts.append(add_box("H81_Attic", (0.0, -4.5, 7.05), (7.6, 6.8, 1.65), mats["RoofTile"], bevel=0.05))
    for floor in range(2):
        wz = 1.75 + floor * 3.05
        add_window(parts, f"H81_WL_{floor}", 2.2, -0.38, wz, 1.85, 1.35, mats, barred=(floor == 0))
        add_window(parts, f"H81_WR_{floor}", -2.0, -0.38, wz, 2.10, 1.35, mats, barred=(floor == 0))
    # Old brick garden shed on the right (-X = -6.8)
    parts.append(add_box("H81_Shed", (-6.8, -0.4, 1.25), (3.8, 5.4, 2.50), mats["Concrete"], bevel=0.02))
    parts.append(add_box("H81_ShedRoof", (-6.8, -0.4, 2.65), (4.1, 5.7, 0.22), mats["RoofTile"], bevel=0.02, rot_euler=(0, 0.12, 0)))
    # Low concrete fence posts along front garden edge Y = +3.6
    for px in (-4.0, -1.8, 0.4, 2.6, 4.8):
        parts.append(add_box(f"H81_Post_{px:.1f}", (px, 3.6, 0.62), (0.14, 0.14, 1.24), mats["Concrete"]))
    return join_all(parts, "Sultanbeyli_House81_Rose")


# ---------------------------------------------------------------------------
# 11. GÜRSU SK. CORNER — PEACH SINGLE-STORY HOUSE WITH CURVED BLACK WINDOW GRILLES
# ---------------------------------------------------------------------------
def build_peach_corner_house(mats):
    parts = []
    parts.append(add_box("PC_Body", (0.0, -2.5, 1.95), (9.4, 7.2, 3.90), mats["PeachPlaster"], bevel=0.03))
    parts.append(add_box("PC_Roof", (0.0, -2.5, 4.45), (9.8, 7.6, 1.15), mats["RoofTile"], bevel=0.35))
    add_window(parts, "PC_W1", 2.4, 1.12, 2.25, 2.15, 1.45, mats, barred=True)
    add_window(parts, "PC_W2", -2.2, 1.12, 2.25, 2.15, 1.45, mats, barred=True)
    # Basement horizontal windows w/ stone trim
    add_window(parts, "PC_BW1", 2.4, 1.12, 0.58, 1.75, 0.55, mats)
    add_window(parts, "PC_BW2", -2.2, 1.12, 0.58, 1.75, 0.55, mats)
    return join_all(parts, "Sultanbeyli_PeachCornerHouse")


# ---------------------------------------------------------------------------
# 12. ICONIC HIGH-VOLTAGE STEEL LATTICE TRANSMISSION PYLON (East end of Selman Sk.!)
# ---------------------------------------------------------------------------
def build_high_voltage_pylon(mats):
    parts = []
    # Tall 26m steel lattice transmission tower matching the iconic vista down Selman Sokağı!
    h = 26.0
    base_half = 3.2
    top_half = 0.75
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for ci, (sx, sy) in enumerate(corners):
        p0 = Vector((sx * base_half, sy * base_half, 0.0))
        p1 = Vector((sx * top_half, sy * top_half, h))
        mid = (p0 + p1) * 0.5
        d = p1 - p0
        rot = d.to_track_quat("Z", "Y").to_euler()
        parts.append(add_cyl(f"Pylon_Leg_{ci}", mid, 0.14, 0.09, d.length, 6, mats["Galvanized"], rot_euler=rot))

    # Horizontal & diagonal lattice bracings every 3.2m
    levels = 8
    for lv in range(1, levels):
        t = lv / float(levels)
        hw = base_half * (1.0 - t) + top_half * t
        z = t * h
        parts.append(add_box(f"Pylon_BraceX1_{lv}", (0, hw, z), (hw * 2.0, 0.10, 0.10), mats["Galvanized"]))
        parts.append(add_box(f"Pylon_BraceX2_{lv}", (0, -hw, z), (hw * 2.0, 0.10, 0.10), mats["Galvanized"]))
        parts.append(add_box(f"Pylon_BraceY1_{lv}", (hw, 0, z), (0.10, hw * 2.0, 0.10), mats["Galvanized"]))
        parts.append(add_box(f"Pylon_BraceY2_{lv}", (-hw, 0, z), (0.10, hw * 2.0, 0.10), mats["Galvanized"]))

    # Two wide cross-arms (at Z = 18.5m and Z = 23.0m) holding ceramic high-voltage insulators!
    for arm_i, (az, span) in enumerate([(18.5, 11.2), (23.0, 8.8)]):
        parts.append(add_box(f"Pylon_Arm_{arm_i}", (0.0, 0.0, az), (span, 0.45, 0.55), mats["Galvanized"], bevel=0.02))
        for side in (-1.0, 1.0):
            parts.append(add_cyl(f"Pylon_Ins_{arm_i}_{side}", (side * (span * 0.46), 0.0, az - 0.55), 0.12, 0.12, 0.85, 8, mats["WhitePVC"]))
    # Peak lightning mast
    parts.append(add_cyl("Pylon_Peak", (0, 0, h + 1.4), 0.08, 0.02, 2.8, 6, mats["Galvanized"]))
    return join_all(parts, "Sultanbeyli_HighVoltagePylon")


# ---------------------------------------------------------------------------
# 13. SULTANBEYLİ BELEDİYESİ GALVANIZED WHEELED DUMPSTER (Exact from street photos!)
# ---------------------------------------------------------------------------
def build_dumpster(mats):
    parts = []
    # Main galvanized metal hopper body (1.45m x 1.05m x 1.12m)
    parts.append(add_box("Dump_Body", (0.0, 0.0, 0.72), (1.42, 1.02, 0.96), mats["Galvanized"], bevel=0.03))
    parts.append(add_box("Dump_Lid", (0.0, 0.0, 1.24), (1.48, 1.08, 0.10), mats["Galvanized"], bevel=0.02))
    # Iconic red/white corner reflector stripes + dark caster wheels
    for sx in (-0.66, 0.66):
        parts.append(add_box(f"Dump_ReflF_{sx:.2f}", (sx, 0.52, 0.72), (0.12, 0.02, 0.52), mats["SignRed"]))
        parts.append(add_box(f"Dump_ReflB_{sx:.2f}", (sx, -0.52, 0.72), (0.12, 0.02, 0.52), mats["SignRed"]))
        for sy in (-0.36, 0.36):
            parts.append(add_cyl(f"Dump_Wheel_{sx:.2f}_{sy:.2f}", (sx * 0.85, sy, 0.11), 0.11, 0.11, 0.08, 12, mats["TrimDark"], rot_euler=(0, math.pi * 0.5, 0)))
    return join_all(parts, "Sultanbeyli_Dumpster")


# ---------------------------------------------------------------------------
# 14. RED-TRIANGLE SPEED BUMP WARNING SIGN ('Kasis' — exact from Çiçekçi Sk. corner!)
# ---------------------------------------------------------------------------
def build_speed_bump_sign(mats):
    parts = []
    parts.append(add_cyl("Sign_Pole", (0, 0, 1.10), 0.032, 0.032, 2.20, 8, mats["Galvanized"]))
    # Triangular red border + white inner face + black bump icon
    bm_r = bmesh.new()
    s = 0.42
    v0 = bm_r.verts.new(Vector((-s, 0.02, 1.95)))
    v1 = bm_r.verts.new(Vector((s, 0.02, 1.95)))
    v2 = bm_r.verts.new(Vector((0.0, 0.02, 1.95 + s * 1.68)))
    v3 = bm_r.verts.new(Vector((-s, -0.02, 1.95)))
    v4 = bm_r.verts.new(Vector((s, -0.02, 1.95)))
    v5 = bm_r.verts.new(Vector((0.0, -0.02, 1.95 + s * 1.68)))
    bm_r.faces.new((v0, v1, v2))
    bm_r.faces.new((v4, v3, v5))
    bm_r.faces.new((v0, v3, v4, v1))
    bm_r.faces.new((v1, v4, v5, v2))
    bm_r.faces.new((v2, v5, v3, v0))
    parts.append(make_obj("Sign_RedTri", bm_r, mats["SignRed"]))

    bm_w = bmesh.new()
    sw = 0.30
    w0 = bm_w.verts.new(Vector((-sw, 0.032, 2.02)))
    w1 = bm_w.verts.new(Vector((sw, 0.032, 2.02)))
    w2 = bm_w.verts.new(Vector((0.0, 0.032, 2.02 + sw * 1.62)))
    bm_w.faces.new((w0, w1, w2))
    parts.append(make_obj("Sign_WhiteTri", bm_w, mats["WhitePVC"]))
    parts.append(add_box("Sign_BumpIcon", (0.0, 0.042, 2.15), (0.34, 0.015, 0.08), mats["TrimDark"]))
    return join_all(parts, "Sultanbeyli_SpeedBumpSign")


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
    print(f"  Exported {fbx_name:<38} | verts={len(obj.data.vertices):5d} | tris={tris:5d}")


def render_street_showcase():
    reset_scene()
    mats = get_mats()
    bm_g = bmesh.new()
    bmesh.ops.create_grid(bm_g, x_segments=1, y_segments=1, size=140.0)
    make_obj("Asphalt", bm_g, make_mat("Asphalt", (0.22, 0.23, 0.24), 0.85))

    # Showcase row along Selman Sokağı!
    h66 = build_house66_yellow(mats); h66.location = Vector((-16.0, -7.5, 0.0))
    g73 = build_garage73_lilac(mats); g73.location = Vector((-3.5, -6.0, 0.0))
    s58 = build_shop58_archgate(mats); s58.location = Vector((8.5, -6.5, 0.0))
    hb = build_half_brick_house(mats); hb.location = Vector((23.0, -6.5, 0.0))
    hafsa = build_house66_south_hafsa(mats); hafsa.location = Vector((-14.0, 12.0, 0.0)); hafsa.rotation_euler = Euler((0, 0, math.pi), "XYZ")
    blue81 = build_blue_apartman_81(mats); blue81.location = Vector((4.0, 14.0, 0.0)); blue81.rotation_euler = Euler((0, 0, math.pi), "XYZ")
    pylon = build_high_voltage_pylon(mats); pylon.location = Vector((-42.0, 2.0, 0.0))
    dump = build_dumpster(mats); dump.location = Vector((12.0, 5.2, 0.0))
    sign = build_speed_bump_sign(mats); sign.location = Vector((9.5, 5.2, 0.0)); sign.rotation_euler = Euler((0, 0, math.pi * 0.5), "XYZ")

    world = bpy.data.worlds.new("Sky")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.68, 0.74, 0.80, 1.0)
        bg.inputs["Strength"].default_value = 1.0

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 2.8
    sun_obj = bpy.data.objects.new("Sun", sun_data)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = Euler((math.radians(48), math.radians(18), math.radians(140)), "XYZ")

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 24.0
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((26.0, 2.2, 2.4))
    cam_obj.rotation_euler = (Vector((-24.0, 0.8, 3.2)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_obj

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_SelmanStreet_FullShowcase.blend"))
    bpy.ops.render.render(write_still=True)
    print("Saved Selman Sokağı showcase render to:", PREVIEW_PATH)


def main():
    builders = [
        ("Sultanbeyli_House66_Yellow.fbx", build_house66_yellow),
        ("Sultanbeyli_House66South_Hafsa.fbx", build_house66_south_hafsa),
        ("Sultanbeyli_HalfBrickHouse.fbx", build_half_brick_house),
        ("Sultanbeyli_CoralHouse.fbx", build_coral_house),
        ("Sultanbeyli_RustGarage63.fbx", build_rust_garage_63),
        ("Sultanbeyli_Shop58_ArchGate.fbx", build_shop58_archgate),
        ("Sultanbeyli_Garage73_LilacGate.fbx", build_garage73_lilac),
        ("Sultanbeyli_PinkApartman.fbx", build_pink_apartman),
        ("Sultanbeyli_BlueApartman81.fbx", build_blue_apartman_81),
        ("Sultanbeyli_House81_Rose.fbx", build_house81_rose),
        ("Sultanbeyli_PeachCornerHouse.fbx", build_peach_corner_house),
        ("Sultanbeyli_HighVoltagePylon.fbx", build_high_voltage_pylon),
        ("Sultanbeyli_Dumpster.fbx", build_dumpster),
        ("Sultanbeyli_SpeedBumpSign.fbx", build_speed_bump_sign),
    ]
    print(f"[GRIDROAM Blender] Sculpting {len(builders)} real Street View buildings & props for Selman Sokağı...")
    for fbx_name, fn in builders:
        reset_scene()
        mats = get_mats()
        obj = fn(mats)
        export_fbx(obj, fbx_name)
    render_street_showcase()


if __name__ == "__main__":
    main()
