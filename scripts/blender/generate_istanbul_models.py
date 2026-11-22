#!/usr/bin/env python3
"""
GRIDROAM Istanbul Landmark 3D Asset Generator (Blender 5.3+ Python)
Generates 7 iconic, sculpted, smooth-shaded, UV-unwrapped Istanbul FBX assets for Unity 6 URP:
  1. Istanbul_GalataTower.fbx          (Galata Kulesi - 24.5m medieval cylindrical stone tower & conical roof)
  2. Istanbul_BosphorusBridgeTower.fbx (Boğaziçi Köprüsü - 34m steel H-frame suspension pylon)
  3. Istanbul_KizKulesi.fbx            (Kız Kulesi - Maiden's Tower fortress & spire)
  4. Istanbul_Mosque.fbx               (Osmanlı Boğaz Camii - Central lead dome, semi-domes & twin minarets)
  5. Istanbul_Tram.fbx                 (Nostaljik İstiklal Kırmızı Tramvayı + pantograph)
  6. Istanbul_Ferry.fbx                (Şehir Hatları Boğaz Vapuru - double-deck commuter ferry)
  7. Istanbul_SimitCart.fbx            (İstanbul Kırmızı Simitçi Arabası + golden simits)

Oriented cleanly for Unity: Blender +Y -> Unity +Z forward, +Z -> +Y up, +X -> +X right.
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
    "GRIDROAM_ISTANBUL_PREVIEW",
    os.path.join(SCRIPT_DIR, "blender_istanbul_preview.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.50, metallic=0.0, emission=None, emission_strength=0.0):
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


def get_istanbul_materials():
    return {
        "StoneTrim": make_mat("StoneTrim", (0.84, 0.80, 0.72), roughness=0.68),
        "Concrete": make_mat("Concrete", (0.68, 0.65, 0.60), roughness=0.78),
        "LeadRoof": make_mat("LeadRoof", (0.28, 0.31, 0.35), roughness=0.45, metallic=0.55),
        "BridgeSteel": make_mat("BridgeSteel", (0.86, 0.87, 0.89), roughness=0.32, metallic=0.72),
        "TramRed": make_mat("TramRed", (0.72, 0.10, 0.10), roughness=0.22, metallic=0.35),
        "CreamPaint": make_mat("CreamPaint", (0.94, 0.91, 0.84), roughness=0.36, metallic=0.05),
        "GoldBrass": make_mat("GoldBrass", (0.86, 0.68, 0.22), roughness=0.22, metallic=0.88),
        "TrimDark": make_mat("TrimDark", (0.12, 0.13, 0.15), roughness=0.58, metallic=0.25),
        "Wood": make_mat("Wood", (0.44, 0.26, 0.14), roughness=0.52),
        "Glass": make_mat("Glass", (0.08, 0.14, 0.20), roughness=0.08, metallic=0.25),
        "LitScreen": make_mat("LitScreen", (1.0, 0.86, 0.56), roughness=0.25, emission=(1.0, 0.82, 0.45), emission_strength=2.2),
        "FerryHull": make_mat("FerryHull", (0.10, 0.13, 0.18), roughness=0.42, metallic=0.20),
        "SimitGold": make_mat("SimitGold", (0.76, 0.46, 0.16), roughness=0.72),
        "RoofTile": make_mat("RoofTile", (0.74, 0.32, 0.22), roughness=0.62),
        "Chrome": make_mat("Chrome", (0.86, 0.88, 0.90), roughness=0.14, metallic=0.92),
    }


def apply_auto_smooth(obj, angle_deg=42.0):
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


def box_uv_project(obj, scale=0.35):
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


def make_obj(name, bm, mat, smooth_angle=44.0, uv_scale=0.35):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if mat is not None:
        obj.data.materials.append(mat)
    box_uv_project(obj, scale=uv_scale)
    apply_auto_smooth(obj, angle_deg=smooth_angle)
    return obj


def add_box(name, center, size, mat, bevel=0.04, segments=2, rot_euler=(0.0, 0.0, 0.0), uv_scale=0.35):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.2:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True)
    m = Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return make_obj(name, bm, mat, smooth_angle=38.0, uv_scale=uv_scale)


def add_cyl(name, center, r_bot, r_top, depth, segs, mat, rot_euler=(0.0, 0.0, 0.0), uv_scale=0.35):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r_bot, radius2=r_top, depth=depth)
    m = Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return make_obj(name, bm, mat, smooth_angle=52.0, uv_scale=uv_scale)


def add_revolved(name, profile_rz, segs, mat, center=(0.0, 0.0, 0.0), smooth_angle=48.0, uv_scale=0.35):
    bm = bmesh.new()
    rings = []
    for r, z in profile_rz:
        if r <= 0.001:
            v = bm.verts.new(Vector((center[0], center[1], center[2] + z)))
            rings.append([v])
        else:
            ring = []
            for s in range(segs):
                a = 2.0 * math.pi * s / segs
                ring.append(bm.verts.new(Vector((center[0] + math.cos(a) * r, center[1] + math.sin(a) * r, center[2] + z))))
            rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        if len(r0) == 1 and len(r1) == segs:
            for s in range(segs):
                sn = (s + 1) % segs
                bm.faces.new((r0[0], r1[sn], r1[s]))
        elif len(r0) == segs and len(r1) == 1:
            for s in range(segs):
                sn = (s + 1) % segs
                bm.faces.new((r0[s], r0[sn], r1[0]))
        elif len(r0) == segs and len(r1) == segs:
            for s in range(segs):
                sn = (s + 1) % segs
                bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))

    return make_obj(name, bm, mat, smooth_angle=smooth_angle, uv_scale=uv_scale)


def add_dome(name, center, radius, height, segs, rings_n, mat, uv_scale=0.35):
    profile = []
    for i in range(rings_n + 1):
        t = i / rings_n
        ang = t * (math.pi * 0.5)
        r = math.cos(ang) * radius
        z = math.sin(ang) * height
        profile.append((max(0.0, r), z))
    return add_revolved(name, profile, segs, mat, center=center, smooth_angle=62.0, uv_scale=uv_scale)


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
# 1. GALATA KULESİ (24.5m tall iconic medieval cylindrical stone tower)
# ---------------------------------------------------------------------------
def build_galata_tower(mats):
    parts = []
    # Main cylindrical stone shaft with stepped base plinth, stone stringcourses & observation balcony corbels
    shaft_profile = [
        (3.85, 0.00),
        (3.85, 0.85),
        (3.52, 1.05),
        (3.38, 6.20),
        (3.52, 6.45),
        (3.52, 6.72),
        (3.26, 6.90),
        (3.16, 13.60),
        # Corbelled stone observation gallery flare
        (3.55, 14.35),
        (3.92, 14.65),
        (3.92, 15.05),
        (3.05, 15.05),
        # Upper windowed arcade drum under conical roof
        (2.96, 18.20),
        (3.15, 18.45),
        (3.15, 18.75),
        (0.00, 18.75),
    ]
    parts.append(add_revolved("Galata_StoneShaft", shaft_profile, 28, mats["StoneTrim"], smooth_angle=52.0))

    # Ornate wrought-iron balcony railing ring + vertical balusters around gallery (z = 15.05..16.10)
    for b in range(28):
        ang = 2.0 * math.pi * b / 28
        ca, sa = math.cos(ang), math.sin(ang)
        parts.append(add_cyl(f"Galata_Baluster_{b}", (ca * 3.82, sa * 3.82, 15.55), 0.04, 0.04, 1.02, 6, mats["TrimDark"]))
    rail_prof = [(3.88, 16.02), (3.88, 16.12), (3.76, 16.12), (3.76, 16.02)]
    parts.append(add_revolved("Galata_BalconyRail", rail_prof, 28, mats["TrimDark"], smooth_angle=45.0))

    # Arched lit observation windows around gallery drum (14 arches) + lower shaft windows (8)
    for i in range(14):
        ang = 2.0 * math.pi * i / 14
        ca, sa = math.cos(ang), math.sin(ang)
        parts.append(add_box(f"Galata_UpperWin_{i}", (ca * 3.00, sa * 3.00, 16.65), (0.20, 0.68, 1.95), mats["LitScreen"], bevel=0.03, segments=1, rot_euler=(0.0, 0.0, ang)))
    for i in range(8):
        ang = 2.0 * math.pi * i / 8 + 0.2
        ca, sa = math.cos(ang), math.sin(ang)
        parts.append(add_box(f"Galata_MidWin_{i}", (ca * 3.24, sa * 3.24, 10.60), (0.22, 0.56, 1.45), mats["Glass"], bevel=0.03, segments=1, rot_euler=(0.0, 0.0, ang)))
        parts.append(add_box(f"Galata_LowWin_{i}", (ca * 3.44, sa * 3.44, 4.40), (0.22, 0.52, 1.25), mats["Glass"], bevel=0.03, segments=1, rot_euler=(0.0, 0.0, ang)))

    # Stone entrance portal at +X facing Beyoğlu Square
    parts.append(add_box("Galata_DoorPortal", (3.62, 0.0, 1.75), (0.65, 2.05, 3.30), mats["StoneTrim"], bevel=0.08, segments=2))
    parts.append(add_box("Galata_DoorWood", (3.92, 0.0, 1.48), (0.14, 1.45, 2.65), mats["Wood"], bevel=0.03, segments=1))

    # Iconic sweeping conical lead-slate roof (z = 18.65..24.20) + 4 miniature dormer turrets + golden alem spire!
    roof_prof = [
        (3.38, 18.55),
        (3.32, 18.85),
        (2.55, 20.60),
        (1.60, 22.35),
        (0.55, 23.85),
        (0.14, 24.35),
        (0.00, 24.45),
    ]
    parts.append(add_revolved("Galata_ConicalRoof", roof_prof, 28, mats["LeadRoof"], smooth_angle=55.0))
    for d in range(4):
        ang = d * math.pi * 0.5
        ca, sa = math.cos(ang), math.sin(ang)
        parts.append(add_box(f"Galata_Dormer_{d}", (ca * 2.50, sa * 2.50, 19.85), (0.65, 0.56, 1.25), mats["LeadRoof"], bevel=0.03, segments=1, rot_euler=(0.0, 0.0, ang)))
        parts.append(add_box(f"Galata_DormerWin_{d}", (ca * 2.82, sa * 2.82, 19.80), (0.08, 0.34, 0.72), mats["LitScreen"], bevel=0.01, segments=1, rot_euler=(0.0, 0.0, ang)))

    # Golden finial spire & spheres (alem)
    parts.append(add_cyl("Galata_Spire", (0.0, 0.0, 25.15), 0.06, 0.025, 1.65, 10, mats["GoldBrass"]))
    parts.append(add_dome("Galata_OrbA", (0.0, 0.0, 24.62), 0.22, 0.22, 12, 6, mats["GoldBrass"]))
    parts.append(add_dome("Galata_OrbB", (0.0, 0.0, 25.18), 0.14, 0.14, 10, 5, mats["GoldBrass"]))

    return join_all(parts, "Istanbul_GalataTower")


# ---------------------------------------------------------------------------
# 2. BOĞAZİÇİ KÖPRÜSÜ H-FRAME STEEL PYLON (34m tall, spans 16m roadway)
# ---------------------------------------------------------------------------
def build_bosphorus_bridge_tower(mats):
    parts = []
    # Oriented so roadway passes along X (pylon legs at Y = -8.4 and Y = +8.4)
    # Concrete caisson water piers (z = -2.0..+2.8)
    for sy in (-1.0, 1.0):
        parts.append(add_box(f"Bridge_Caisson_{sy}", (0.0, sy * 8.4, 0.4), (4.6, 4.2, 4.8), mats["Concrete"], bevel=0.18, segments=2))
        # Tapered chamfered steel main tower leg (z = 2.6..34.0)
        parts.append(add_box(f"Bridge_LegLower_{sy}", (0.0, sy * 8.4, 10.6), (2.4, 2.2, 16.0), mats["BridgeSteel"], bevel=0.14, segments=2))
        parts.append(add_box(f"Bridge_LegUpper_{sy}", (0.0, sy * 8.4, 26.2), (1.9, 1.8, 15.6), mats["BridgeSteel"], bevel=0.12, segments=2))
        # Cable saddle cap at top (z = 34.2)
        parts.append(add_box(f"Bridge_Saddle_{sy}", (0.0, sy * 8.4, 34.4), (2.6, 2.2, 0.95), mats["TrimDark"], bevel=0.10, segments=2))
        # Vertical warm-gold LED architectural light strips on outer faces
        parts.append(add_box(f"Bridge_LED_{sy}", (0.0, sy * 9.55, 19.0), (0.35, 0.14, 26.0), mats["LitScreen"], bevel=0.02, segments=1))

    # 3 iconic horizontal steel cross-beams connecting twin legs (below deck z=6.6, mid z=20.2, crown z=32.4)
    parts.append(add_box("Bridge_CrossDeck", (0.0, 0.0, 6.6), (2.1, 16.4, 1.5), mats["BridgeSteel"], bevel=0.08, segments=2))
    parts.append(add_box("Bridge_CrossMid", (0.0, 0.0, 20.2), (1.7, 16.2, 1.8), mats["BridgeSteel"], bevel=0.08, segments=2))
    parts.append(add_box("Bridge_CrossTop", (0.0, 0.0, 32.4), (1.6, 16.2, 2.1), mats["BridgeSteel"], bevel=0.08, segments=2))

    # Subtle glowing horizontal accent strip under crown beam
    parts.append(add_box("Bridge_CrownLight", (0.0, 0.0, 31.25), (1.72, 14.4, 0.24), mats["LitScreen"], bevel=0.02, segments=1))

    return join_all(parts, "Istanbul_BosphorusBridgeTower")


# ---------------------------------------------------------------------------
# 3. KIZ KULESİ (Maiden's Tower — White limestone fortress & central spire)
# ---------------------------------------------------------------------------
def build_kiz_kulesi(mats):
    parts = []
    # Base fortress courtyard perimeter wall (13m x 13m, height 3.6m) with battlement merlons
    parts.append(add_box("Kiz_FortressBody", (0.0, 0.0, 1.8), (11.6, 11.6, 3.6), mats["CreamPaint"], bevel=0.12, segments=2))
    parts.append(add_box("Kiz_FortressTrim", (0.0, 0.0, 3.65), (12.1, 12.1, 0.28), mats["StoneTrim"], bevel=0.05, segments=1))
    # Red pitched tile perimeter roof skirts
    parts.append(add_box("Kiz_RoofSkirtA", (0.0, -4.6, 4.15), (11.0, 2.6, 0.72), mats["RoofTile"], bevel=0.08, segments=2))
    parts.append(add_box("Kiz_RoofSkirtB", (0.0, 4.6, 4.15), (11.0, 2.6, 0.72), mats["RoofTile"], bevel=0.08, segments=2))

    # 4 corner watch-turrets
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            parts.append(add_cyl(f"Kiz_Turret_{sx}_{sy}", (sx * 5.7, sy * 5.7, 2.25), 0.78, 0.74, 4.5, 12, mats["CreamPaint"]))
            parts.append(add_cyl(f"Kiz_TurretCap_{sx}_{sy}", (sx * 5.7, sy * 5.7, 4.95), 0.88, 0.08, 0.95, 12, mats["LeadRoof"]))

    # Iconic central square tower rising from fortress (z = 3.5..11.2)
    parts.append(add_box("Kiz_MainTower", (0.0, 0.0, 7.35), (5.2, 5.2, 7.8), mats["CreamPaint"], bevel=0.10, segments=2))
    # Warm lit arched tower windows on all 4 sides
    for side_ang in (0.0, math.pi * 0.5, math.pi, math.pi * 1.5):
        ca, sa = math.cos(side_ang), math.sin(side_ang)
        parts.append(add_box(f"Kiz_WinUpper_{side_ang:.1f}", (ca * 2.62, sa * 2.62, 8.85), (0.14, 1.15, 1.65), mats["LitScreen"], bevel=0.03, segments=1, rot_euler=(0.0, 0.0, side_ang)))
        parts.append(add_box(f"Kiz_WinLower_{side_ang:.1f}", (ca * 2.62, sa * 2.62, 5.85), (0.14, 0.95, 1.25), mats["Glass"], bevel=0.03, segments=1, rot_euler=(0.0, 0.0, side_ang)))

    # Wooden cantilevered observation balcony + octagonal lantern cupola + tall spire
    parts.append(add_box("Kiz_BalconyDeck", (0.0, 0.0, 11.40), (6.4, 6.4, 0.36), mats["Wood"], bevel=0.05, segments=1))
    parts.append(add_box("Kiz_BalconyRail", (0.0, 0.0, 11.95), (6.2, 6.2, 0.78), mats["CreamPaint"], bevel=0.04, segments=1))
    parts.append(add_cyl("Kiz_LanternDrum", (0.0, 0.0, 13.05), 1.95, 1.88, 2.20, 8, mats["CreamPaint"]))
    for w in range(8):
        ang = 2.0 * math.pi * w / 8
        parts.append(add_box(f"Kiz_LanternWin_{w}", (math.cos(ang) * 1.90, math.sin(ang) * 1.90, 13.05), (0.12, 0.62, 1.30), mats["LitScreen"], bevel=0.02, segments=1, rot_euler=(0.0, 0.0, ang)))

    # Ribbed cupola dome & flagpole spire
    parts.append(add_dome("Kiz_Cupola", (0.0, 0.0, 14.10), 2.15, 2.45, 16, 8, mats["LeadRoof"]))
    parts.append(add_cyl("Kiz_Spire", (0.0, 0.0, 17.65), 0.08, 0.025, 2.40, 10, mats["GoldBrass"]))

    return join_all(parts, "Istanbul_KizKulesi")


# ---------------------------------------------------------------------------
# 4. OTTOMAN BOSPHORUS MOSQUE (Ortaköy / Sultanahmet style + Twin Minarets)
# ---------------------------------------------------------------------------
def build_istanbul_mosque(mats):
    parts = []
    # Stepped travertine stone podium
    parts.append(add_box("Mosque_Podium", (0.0, 0.0, 0.40), (19.5, 20.5, 0.80), mats["StoneTrim"], bevel=0.08, segments=2))
    # Main cubic limestone prayer hall (15.5m x 15.5m x 8.2m)
    parts.append(add_box("Mosque_Hall", (0.0, -1.0, 4.90), (15.6, 15.6, 8.20), mats["CreamPaint"], bevel=0.14, segments=2))
    parts.append(add_box("Mosque_Cornice", (0.0, -1.0, 9.10), (16.4, 16.4, 0.42), mats["StoneTrim"], bevel=0.06, segments=1))

    # Monumental central lead dome + windowed drum + golden alem crescent
    parts.append(add_cyl("Mosque_DomeDrum", (0.0, -1.0, 10.10), 6.20, 6.05, 2.00, 28, mats["CreamPaint"]))
    parts.append(add_dome("Mosque_MainDome", (0.0, -1.0, 11.00), 6.25, 5.20, 28, 12, mats["LeadRoof"]))
    parts.append(add_cyl("Mosque_MainAlem", (0.0, -1.0, 17.05), 0.09, 0.03, 1.85, 10, mats["GoldBrass"]))
    parts.append(add_dome("Mosque_MainOrb", (0.0, -1.0, 16.55), 0.26, 0.26, 10, 5, mats["GoldBrass"]))

    # 4 corner cascading semi-domes
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            parts.append(add_dome(f"Mosque_CornerDome_{sx}_{sy}", (sx * 5.2, -1.0 + sy * 5.2, 9.20), 2.35, 1.95, 18, 8, mats["LeadRoof"]))
            parts.append(add_cyl(f"Mosque_CornerAlem_{sx}_{sy}", (sx * 5.2, -1.0 + sy * 5.2, 11.55), 0.04, 0.015, 0.85, 8, mats["GoldBrass"]))

    # Warm arched stained-glass windows along all sides
    for side in (-1.0, 1.0):
        for w in (-4.4, -1.5, 1.5, 4.4):
            parts.append(add_box(f"Mosque_WinX_{side}_{w}", (side * 7.82, -1.0 + w, 4.20), (0.14, 1.35, 2.65), mats["LitScreen"], bevel=0.03, segments=1))
            parts.append(add_box(f"Mosque_WinXUp_{side}_{w}", (side * 7.82, -1.0 + w, 7.15), (0.14, 1.05, 1.45), mats["LitScreen"], bevel=0.03, segments=1))
        for w in (-4.4, 0.0, 4.4):
            parts.append(add_box(f"Mosque_WinBack_{w}", (w, -8.82, 4.50), (1.45, 0.14, 2.85), mats["LitScreen"], bevel=0.03, segments=1))

    # Front entrance portico (Son Cemaat Yeri) at +Y facing the plaza with 4 marble columns & 3 small domes
    for cx in (-5.4, -1.8, 1.8, 5.4):
        parts.append(add_cyl(f"Mosque_Col_{cx}", (cx, 8.6, 3.15), 0.28, 0.24, 4.70, 12, mats["StoneTrim"]))
    parts.append(add_box("Mosque_PorticoBeam", (0.0, 8.6, 5.65), (12.2, 0.72, 0.65), mats["StoneTrim"], bevel=0.06, segments=1))
    for dx in (-3.6, 0.0, 3.6):
        parts.append(add_dome(f"Mosque_PorticoDome_{dx}", (dx, 7.7, 5.90), 1.65, 1.45, 16, 7, mats["LeadRoof"]))

    # Grand wooden entrance portal
    parts.append(add_box("Mosque_PortalFrame", (0.0, 6.86, 2.85), (3.10, 0.36, 4.10), mats["StoneTrim"], bevel=0.08, segments=2))
    parts.append(add_box("Mosque_PortalDoor", (0.0, 6.98, 2.35), (2.10, 0.18, 3.15), mats["Wood"], bevel=0.04, segments=1))

    # TWIN SLENDER OTTOMAN MINARETS (height 23.5m) with carved şerefe balconies & conical lead caps!
    for sx in (-1.0, 1.0):
        mx, my = sx * 9.0, 6.4
        parts.append(add_box(f"Minaret_Base_{sx}", (mx, my, 2.40), (2.10, 2.10, 4.00), mats["StoneTrim"], bevel=0.08, segments=2))
        minaret_prof = [
            (0.95, 4.20),
            (0.76, 5.40),
            (0.68, 14.60),
            # Carved şerefe balcony
            (1.12, 15.15),
            (1.12, 15.75),
            (0.62, 15.75),
            (0.58, 18.80),
            (0.62, 19.00),
            (0.00, 19.00),
        ]
        parts.append(add_revolved(f"Minaret_Shaft_{sx}", minaret_prof, 18, mats["CreamPaint"], center=(mx, my, 0.0), smooth_angle=52.0))
        parts.append(add_cyl(f"Minaret_Cone_{sx}", (mx, my, 21.05), 0.66, 0.06, 4.20, 18, mats["LeadRoof"]))
        parts.append(add_cyl(f"Minaret_Alem_{sx}", (mx, my, 23.65), 0.05, 0.018, 1.15, 8, mats["GoldBrass"]))

    return join_all(parts, "Istanbul_Mosque")


# ---------------------------------------------------------------------------
# 5. NOSTALJİK İSTİKLAL KIRMIZI TRAMVAYI (Classic Taksim-Tünel T2 Tram)
# ---------------------------------------------------------------------------
def build_istanbul_tram(mats):
    parts = []
    # Length 8.4m (Y = -4.2..+4.2), Width 2.16m (X = -1.08..+1.08), Height 3.4m
    # Undercarriage chassis + twin steel bogies & flanged tram wheels
    parts.append(add_box("Tram_Chassis", (0.0, 0.0, 0.42), (1.92, 7.60, 0.34), mats["TrimDark"], bevel=0.04, segments=1))
    for wy in (-2.35, -1.35, 1.35, 2.35):
        for sx in (-1.0, 1.0):
            parts.append(add_cyl(f"Tram_Wheel_{wy}_{sx}", (sx * 0.78, wy, 0.30), 0.30, 0.30, 0.14, 16, mats["TrimDark"], rot_euler=(0.0, math.pi * 0.5, 0.0)))

    # Glossy crimson red lower coach body + curved front/rear cowcatchers
    parts.append(add_box("Tram_LowerRedBody", (0.0, 0.0, 1.12), (2.14, 8.20, 1.08), mats["TramRed"], bevel=0.10, segments=2))
    parts.append(add_box("Tram_GoldStripe", (0.0, 0.0, 1.62), (2.18, 8.24, 0.09), mats["GoldBrass"], bevel=0.015, segments=1))

    # Cream upper window cabin band + recessed dark glass windows + window pillars
    parts.append(add_box("Tram_UpperGlassCore", (0.0, 0.0, 2.18), (2.02, 7.86, 1.02), mats["Glass"], bevel=0.06, segments=2))
    parts.append(add_box("Tram_HeaderCream", (0.0, 0.0, 2.74), (2.14, 8.18, 0.24), mats["CreamPaint"], bevel=0.05, segments=2))
    for py in (-3.92, -2.75, -1.65, -0.55, 0.55, 1.65, 2.75, 3.92):
        for sx in (-1.0, 1.0):
            parts.append(add_box(f"Tram_Pillar_{py}_{sx}", (sx * 1.04, py, 2.18), (0.10, 0.16, 1.08), mats["CreamPaint"], bevel=0.02, segments=1))

    # Curved clerestory cream roof + overhead diamond steel pantograph!
    parts.append(add_box("Tram_MainRoof", (0.0, 0.0, 2.94), (2.22, 8.38, 0.22), mats["CreamPaint"], bevel=0.08, segments=2))
    parts.append(add_box("Tram_Clerestory", (0.0, 0.0, 3.14), (1.32, 6.20, 0.22), mats["CreamPaint"], bevel=0.06, segments=2))
    # Diamond pantograph
    for sy in (-1.0, 1.0):
        parts.append(add_box(f"Tram_PantoLow_{sy}", (0.0, sy * 0.45, 3.55), (0.08, 1.10, 0.05), mats["TrimDark"], bevel=0.01, segments=1, rot_euler=(sy * 0.52, 0.0, 0.0)))
        parts.append(add_box(f"Tram_PantoHigh_{sy}", (0.0, sy * 0.45, 4.02), (0.08, 1.10, 0.05), mats["TrimDark"], bevel=0.01, segments=1, rot_euler=(-sy * 0.52, 0.0, 0.0)))
    parts.append(add_box("Tram_PantoContactBar", (0.0, 0.0, 4.30), (1.45, 0.10, 0.05), mats["Chrome"], bevel=0.01, segments=1))

    # Iconic single glowing brass center headlamp + destination box + wooden side steps at both ends
    for sy in (-1.0, 1.0):
        parts.append(add_cyl(f"Tram_HeadlampRim_{sy}", (0.0, sy * 4.14, 1.24), 0.19, 0.19, 0.12, 16, mats["GoldBrass"], rot_euler=(math.pi * 0.5, 0.0, 0.0)))
        parts.append(add_cyl(f"Tram_HeadlampLens_{sy}", (0.0, sy * 4.20, 1.24), 0.14, 0.14, 0.04, 14, mats["LitScreen"], rot_euler=(math.pi * 0.5, 0.0, 0.0)))
        parts.append(add_box(f"Tram_Bumper_{sy}", (0.0, sy * 4.24, 0.56), (1.88, 0.22, 0.18), mats["TrimDark"], bevel=0.04, segments=1))
        parts.append(add_box(f"Tram_DestBox_{sy}", (0.0, sy * 4.08, 2.98), (1.10, 0.18, 0.26), mats["LitScreen"], bevel=0.02, segments=1))
        for sx in (-1.0, 1.0):
            parts.append(add_box(f"Tram_Step_{sy}_{sx}", (sx * 1.16, sy * 3.35, 0.34), (0.24, 1.10, 0.07), mats["Wood"], bevel=0.01, segments=1))

    return join_all(parts, "Istanbul_Tram")


# ---------------------------------------------------------------------------
# 6. ŞEHİR HATLARI BOĞAZ VAPURU (Classic Istanbul Commuter Ferry)
# ---------------------------------------------------------------------------
def build_istanbul_ferry(mats):
    parts = []
    # Length 22m (Y = -11..+11), Width 5.4m
    # Tapered dark navy ship hull + red waterline keel
    bm = bmesh.new()
    hull_stations = [
        (-10.8, 1.45, 2.05, 0.0, 1.55),
        (-8.2,  2.35, 2.68, -0.35, 1.72),
        (0.0,   2.45, 2.75, -0.40, 1.72),
        (7.5,   2.25, 2.60, -0.35, 1.80),
        (10.2,  1.10, 1.55, -0.15, 2.02),
        (11.4,  0.14, 0.24, 0.10, 2.25),
    ]
    rings = []
    for (y, hw_b, hw_t, z_b, z_t) in hull_stations:
        pts = [
            Vector((0.0, y, z_b)),
            Vector((hw_b, y, z_b + 0.25)),
            Vector((hw_t, y, z_t)),
            Vector((-hw_t, y, z_t)),
            Vector((-hw_b, y, z_b + 0.25)),
        ]
        rings.append([bm.verts.new(p) for p in pts])
    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for k in range(5):
            kn = (k + 1) % 5
            bm.faces.new((r0[k], r0[kn], r1[kn], r1[k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    parts.append(make_obj("Ferry_Hull", bm, mats["FerryHull"], smooth_angle=52.0))

    # Teak main deck surface + white lower passenger saloon + dark saloon windows
    parts.append(add_box("Ferry_MainDeck", (0.0, -0.2, 1.82), (5.0, 19.2, 0.18), mats["Wood"], bevel=0.05, segments=1))
    parts.append(add_box("Ferry_LowerCabin", (0.0, -0.4, 2.85), (4.3, 13.8, 1.95), mats["CreamPaint"], bevel=0.10, segments=2))
    parts.append(add_box("Ferry_LowerGlass", (0.0, -0.4, 3.05), (4.38, 12.6, 0.85), mats["Glass"], bevel=0.03, segments=1))

    # Upper deck roof + open-air upper cabin + forward captain's wheelhouse bridge
    parts.append(add_box("Ferry_UpperDeck", (0.0, -0.4, 3.92), (4.6, 15.2, 0.22), mats["CreamPaint"], bevel=0.06, segments=1))
    parts.append(add_box("Ferry_UpperCabin", (0.0, -0.8, 4.82), (3.5, 9.8, 1.68), mats["CreamPaint"], bevel=0.08, segments=2))
    parts.append(add_box("Ferry_UpperGlass", (0.0, -0.8, 4.95), (3.58, 8.8, 0.72), mats["LitScreen"], bevel=0.02, segments=1))
    parts.append(add_box("Ferry_BridgeHouse", (0.0, 5.2, 4.95), (3.1, 2.6, 1.85), mats["CreamPaint"], bevel=0.08, segments=2))
    parts.append(add_box("Ferry_BridgeGlass", (0.0, 5.4, 5.15), (3.18, 2.3, 0.68), mats["Glass"], bevel=0.02, segments=1))

    # Classic yellow-and-black Şehir Hatları smokestack funnel + radar mast
    parts.append(add_cyl("Ferry_FunnelBody", (0.0, -1.2, 6.75), 0.72, 0.62, 2.65, 16, mats["GoldBrass"], rot_euler=(-0.10, 0.0, 0.0)))
    parts.append(add_cyl("Ferry_FunnelTop", (0.0, -1.33, 8.05), 0.63, 0.60, 0.55, 16, mats["TrimDark"], rot_euler=(-0.10, 0.0, 0.0)))
    parts.append(add_cyl("Ferry_Mast", (0.0, 4.8, 7.25), 0.08, 0.04, 2.80, 8, mats["CreamPaint"]))

    return join_all(parts, "Istanbul_Ferry")


# ---------------------------------------------------------------------------
# 7. İSTANBUL SİMİTÇİ ARABASI (Iconic Red Street Simit Cart)
# ---------------------------------------------------------------------------
def build_simit_cart(mats):
    parts = []
    # Vintage spoked wheels
    for sx in (-1.0, 1.0):
        parts.append(add_cyl(f"Simit_Wheel_{sx}", (sx * 0.54, 0.0, 0.36), 0.36, 0.36, 0.05, 20, mats["TrimDark"], rot_euler=(0.0, math.pi * 0.5, 0.0)))
        parts.append(add_cyl(f"Simit_Hub_{sx}", (sx * 0.54, 0.0, 0.36), 0.08, 0.08, 0.08, 12, mats["GoldBrass"], rot_euler=(0.0, math.pi * 0.5, 0.0)))

    # Red lower cabinet + brass corner frames + clear display case
    parts.append(add_box("Simit_LowerBox", (0.0, 0.0, 0.64), (0.94, 1.28, 0.56), mats["TramRed"], bevel=0.03, segments=2))
    parts.append(add_box("Simit_GlassCase", (0.0, 0.0, 1.22), (0.86, 1.18, 0.58), mats["Glass"], bevel=0.02, segments=1))
    parts.append(add_box("Simit_RoofRed", (0.0, 0.0, 1.56), (1.04, 1.38, 0.12), mats["TramRed"], bevel=0.03, segments=2))
    parts.append(add_box("Simit_RoofTrim", (0.0, 0.0, 1.63), (0.98, 1.32, 0.04), mats["GoldBrass"], bevel=0.01, segments=1))

    # Brass corner posts & push handle
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            parts.append(add_cyl(f"Simit_Post_{sx}_{sy}", (sx * 0.44, sy * 0.60, 1.22), 0.022, 0.022, 0.64, 8, mats["GoldBrass"]))
    parts.append(add_cyl("Simit_PushBar", (0.0, -0.82, 0.94), 0.022, 0.022, 0.84, 10, mats["GoldBrass"], rot_euler=(0.0, math.pi * 0.5, 0.0)))

    # Stacked pyramids of golden sesame simits inside the glass display!
    for stack_x in (-0.22, 0.0, 0.22):
        for stack_y in (-0.28, 0.0, 0.28):
            for layer in range(4):
                parts.append(add_cyl(
                    f"SimitRing_{stack_x}_{stack_y}_{layer}",
                    (stack_x, stack_y, 0.98 + layer * 0.065),
                    0.095, 0.095, 0.050, 12, mats["SimitGold"],
                ))

    return join_all(parts, "Istanbul_SimitCart")


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
    print(f"  Exported {fbx_name:<34} | verts={len(obj.data.vertices):5d} | tris={tris:5d}")


def render_istanbul_showcase():
    reset_scene()
    mats = get_istanbul_materials()

    bm_g = bmesh.new()
    bmesh.ops.create_grid(bm_g, x_segments=1, y_segments=1, size=100.0)
    make_obj("StudioGround", bm_g, make_mat("SeaFloor", (0.14, 0.34, 0.46), roughness=0.35))

    galata = build_galata_tower(mats)
    galata.location = Vector((-18.0, 6.0, 0.0))

    bridge = build_bosphorus_bridge_tower(mats)
    bridge.location = Vector((0.0, 18.0, 0.0))
    bridge.rotation_euler = Euler((0.0, 0.0, math.radians(90.0)), "XYZ")

    mosque = build_istanbul_mosque(mats)
    mosque.location = Vector((20.0, 10.0, 0.0))

    kiz = build_kiz_kulesi(mats)
    kiz.location = Vector((-4.0, -4.0, 0.0))

    ferry = build_istanbul_ferry(mats)
    ferry.location = Vector((16.0, -10.0, 0.0))
    ferry.rotation_euler = Euler((0.0, 0.0, math.radians(65.0)), "XYZ")

    tram = build_istanbul_tram(mats)
    tram.location = Vector((-18.0, -10.0, 0.0))
    tram.rotation_euler = Euler((0.0, 0.0, math.radians(32.0)), "XYZ")

    simit = build_simit_cart(mats)
    simit.location = Vector((-10.0, -13.0, 0.0))

    world = bpy.data.worlds.new("IstanbulWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.56, 0.72, 0.90, 1.0)
        bg.inputs["Strength"].default_value = 0.90

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 3.2
    sun_data.color = (1.0, 0.94, 0.82)
    sun_obj = bpy.data.objects.new("Sun", sun_data)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = Euler((math.radians(48), math.radians(15), math.radians(-35)), "XYZ")

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 36.0
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((-4.0, -44.0, 22.0))
    cam_obj.rotation_euler = (Vector((2.0, 4.0, 7.0)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_obj

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_Istanbul_Showcase.blend"))
    bpy.ops.render.render(write_still=True)
    print(f"Saved Istanbul showcase render to: {PREVIEW_PATH}")


def main():
    builders = [
        ("Istanbul_GalataTower.fbx", build_galata_tower),
        ("Istanbul_BosphorusBridgeTower.fbx", build_bosphorus_bridge_tower),
        ("Istanbul_KizKulesi.fbx", build_kiz_kulesi),
        ("Istanbul_Mosque.fbx", build_istanbul_mosque),
        ("Istanbul_Tram.fbx", build_istanbul_tram),
        ("Istanbul_Ferry.fbx", build_istanbul_ferry),
        ("Istanbul_SimitCart.fbx", build_simit_cart),
    ]
    print(f"[GRIDROAM Blender] Generating {len(builders)} iconic Istanbul 3D landmarks...")
    for fbx_name, fn in builders:
        reset_scene()
        mats = get_istanbul_materials()
        obj = fn(mats)
        export_fbx(obj, fbx_name)
    render_istanbul_showcase()


if __name__ == "__main__":
    main()
