#!/usr/bin/env python3
"""
GRIDROAM Hz. Osman Camii (Hamidiye Mah., Sultanbeyli, İstanbul) Hollow Walk-In Mosque
& Authentic Turkish Mosque Interior Generator (Blender 5.3+):
  1. Sultanbeyli_HzOsmanCamii.fbx (True hollow 15x15m Ottoman sanctuary, high vaulted dome,
                                   4 semi-domes, twin minarets w/ şerefe & gold alem,
                                   wide arched entrance portal & open carved walnut doors!)
  2. Mosque_InteriorProps.fbx     (Emerald saf-lined carpet w/ gold saf stripes, carved marble
                                   & İznik-tile Mihrab, stepped walnut Minber, Vaaz Kürsüsü,
                                   grand suspended Ottoman ring chandelier & entrance ayakkabılık!)
  3. Mosque_Sadirvan.fbx          (Octagonal courtyard marble Şadırvan ablution fountain w/ canopy & taps!)
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
PREVIEW_PATH = os.environ.get(
    "GRIDROAM_MOSQUE_PREVIEW",
    os.path.join(SCRIPT_DIR, "blender_hz_osman_camii.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.58, metallic=0.0):
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
        "StoneTrim": make_mat("StoneTrim", (0.91, 0.88, 0.81), 0.65),
        "CreamPaint": make_mat("CreamPaint", (0.94, 0.92, 0.87), 0.70),
        "LeadRoof": make_mat("LeadRoof", (0.30, 0.34, 0.39), 0.45, 0.60),
        "GoldBrass": make_mat("GoldBrass", (0.88, 0.70, 0.22), 0.24, 0.90),
        "CobaltBlue": make_mat("CobaltBlue", (0.10, 0.40, 0.76), 0.32, 0.15),
        "AccentTeal": make_mat("AccentTeal", (0.10, 0.44, 0.36), 0.82),
        "CanGreen": make_mat("CanGreen", (0.10, 0.46, 0.28), 0.84),
        "Wood": make_mat("Wood", (0.48, 0.28, 0.14), 0.48),
        "DarkWood": make_mat("DarkWood", (0.26, 0.15, 0.08), 0.52),
        "Glass": make_mat("Glass", (0.14, 0.24, 0.34), 0.10, 0.20),
        "LitScreen": make_mat("LitScreen", (1.0, 0.90, 0.65), 0.20),
        "WhitePVC": make_mat("WhitePVC", (0.95, 0.95, 0.94), 0.35),
        "TrimDark": make_mat("TrimDark", (0.14, 0.15, 0.16), 0.50, 0.35),
        "Chrome": make_mat("Chrome", (0.86, 0.88, 0.90), 0.14, 0.92),
        "Water": make_mat("Water", (0.12, 0.62, 0.80), 0.08, 0.10),
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


def add_dome(name, center, radius, z_scale, mat, segs=24, rings=10, invert_inner=False):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings * 2, radius=radius)
    for v in list(bm.verts):
        if v.co.z < -0.05:
            bm.verts.remove(v)
    for v in bm.verts:
        v.co.z *= z_scale
        v.co += Vector(center)
    if invert_inner:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return make_obj(name, bm, mat)


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


# ---------------------------------------------------------------------------
# 1. HOLLOW WALK-IN HZ. OSMAN CAMİİ (15.6m x 15.6m, front entrance faces +Y / Unity +Z!)
# ---------------------------------------------------------------------------
def build_hz_osman_camii(mats):
    parts = []
    half = 7.8
    h_wall = 6.6
    wt = 0.45

    # Raised marble plinth & interior floor slab (y = -7.8..+7.8, top at Z = 0.14)
    parts.append(add_box("Mosque_Floor", (0, 0, 0.07), (half * 2 + 0.6, half * 2 + 0.6, 0.14), mats["StoneTrim"], bevel=0.02))
    # Wide marble entry porch steps on +Y front (Y = +7.8..+9.2)
    parts.append(add_box("Mosque_EntryStep", (0, 8.45, 0.05), (4.6, 1.3, 0.10), mats["StoneTrim"], bevel=0.015))

    # 3 Solid Perimeter Walls (South Kıble back wall -Y, Left +X, Right -X) — HOLLOW INSIDE!
    parts.append(add_box("Wall_KibleBack", (0, -half + wt * 0.5, h_wall * 0.5), (half * 2, wt, h_wall), mats["StoneTrim"]))
    parts.append(add_box("Wall_KibleIn", (0, -half + wt + 0.03, h_wall * 0.5), (half * 2 - wt * 2, 0.06, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))
    parts.append(add_box("Wall_Left", (half - wt * 0.5, 0, h_wall * 0.5), (wt, half * 2, h_wall), mats["StoneTrim"]))
    parts.append(add_box("Wall_LeftIn", (half - wt - 0.03, 0, h_wall * 0.5), (0.06, half * 2 - wt * 2, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))
    parts.append(add_box("Wall_Right", (-half + wt * 0.5, 0, h_wall * 0.5), (wt, half * 2, h_wall), mats["StoneTrim"]))
    parts.append(add_box("Wall_RightIn", (-half + wt + 0.03, 0, h_wall * 0.5), (0.06, half * 2 - wt * 2, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))

    # Front Wall (+Y) split cleanly around a WIDE 3.2m x 3.6m OPEN ENTRANCE PORTAL!
    door_half = 1.60
    door_h = 3.60
    side_w = half - door_half
    for s in (-1.0, 1.0):
        scx = s * (door_half + side_w * 0.5)
        parts.append(add_box(f"Wall_Front_{s}", (scx, half - wt * 0.5, h_wall * 0.5), (side_w, wt, h_wall), mats["StoneTrim"]))
        parts.append(add_box(f"Wall_FrontIn_{s}", (scx, half - wt - 0.03, h_wall * 0.5), (side_w - 0.06, 0.06, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))
    # Lintel above wide arched portal
    parts.append(add_box("Wall_FrontLintel", (0, half - wt * 0.5, door_h + (h_wall - door_h) * 0.5), (door_half * 2 + 0.1, wt, h_wall - door_h), mats["StoneTrim"]))
    # Grand portal frame + open double carved walnut doors swung gracefully inward!
    parts.append(add_box("Portal_FrameL", (door_half + 0.22, half + 0.14, 2.2), (0.42, 0.52, 4.4), mats["StoneTrim"]))
    parts.append(add_box("Portal_FrameR", (-door_half - 0.22, half + 0.14, 2.2), (0.42, 0.52, 4.4), mats["StoneTrim"]))
    parts.append(add_box("Portal_TopCap", (0, half + 0.16, 4.25), (4.4, 0.58, 0.65), mats["CobaltBlue"]))
    parts.append(add_box("Portal_DoorOpenL", (door_half - 0.12, half - 1.15, 1.75), (0.10, 1.45, 3.35), mats["DarkWood"]))
    parts.append(add_box("Portal_DoorOpenR", (-door_half + 0.12, half - 1.15, 1.75), (0.10, 1.45, 3.35), mats["DarkWood"]))

    # Exterior decorative stained-glass arched windows around left, right & back walls
    for wy in (-4.4, 0.0, 4.4):
        for sx in (-1.0, 1.0):
            parts.append(add_box(f"WinS_{sx}_{wy}", (sx * (half + 0.03), wy, 3.4), (0.08, 1.55, 2.40), mats["CobaltBlue"]))
            parts.append(add_box(f"WinSG_{sx}_{wy}", (sx * (half + 0.06), wy, 3.4), (0.04, 1.32, 2.15), mats["Glass"]))
    for wx in (-4.4, 4.4):
        parts.append(add_box(f"WinB_{wx}", (wx, -half - 0.03, 3.4), (1.55, 0.08, 2.40), mats["CobaltBlue"]))
        parts.append(add_box(f"WinBG_{wx}", (wx, -half - 0.06, 3.4), (1.32, 0.04, 2.15), mats["Glass"]))

    # 4 Interior Structural Marble Columns supporting the dome drum
    for cx in (-4.6, 4.6):
        for cy in (-4.6, 4.6):
            parts.append(add_cyl(f"Col_{cx}_{cy}", (cx, cy, h_wall * 0.5), 0.36, 0.32, h_wall, 16, mats["StoneTrim"]))
            parts.append(add_box(f"ColCap_{cx}_{cy}", (cx, cy, h_wall - 0.25), (0.95, 0.95, 0.45), mats["GoldBrass"]))

    # Roof deck framing a wide central circular skylight/dome vault (4 perimeter slabs leaving center 10m open to dome!)
    slab_w = 3.2
    parts.append(add_box("RoofDeck_N", (0, half - slab_w * 0.5, h_wall + 0.18), (half * 2 + 0.4, slab_w, 0.36), mats["StoneTrim"]))
    parts.append(add_box("RoofDeck_S", (0, -half + slab_w * 0.5, h_wall + 0.18), (half * 2 + 0.4, slab_w, 0.36), mats["StoneTrim"]))
    parts.append(add_box("RoofDeck_E", (half - slab_w * 0.5, 0, h_wall + 0.18), (slab_w, half * 2 - slab_w * 2, 0.36), mats["StoneTrim"]))
    parts.append(add_box("RoofDeck_W", (-half + slab_w * 0.5, 0, h_wall + 0.18), (slab_w, half * 2 - slab_w * 2, 0.36), mats["StoneTrim"]))

    # Grand Ottoman Lead Dome (Outer + Hollow Painted Inner Dome visible when looking up inside!) + Gold Brass Alem!
    parts.append(add_dome("Dome_Outer", (0, 0, h_wall + 0.30), 6.2, 0.78, mats["LeadRoof"], segs=28, rings=12, invert_inner=False))
    parts.append(add_dome("Dome_Inner", (0, 0, h_wall + 0.24), 5.85, 0.76, mats["CreamPaint"], segs=24, rings=10, invert_inner=True))
    # Gold Alem crescent spire atop main dome
    parts.append(add_cyl("Alem_Main", (0, 0, h_wall + 5.75), 0.08, 0.03, 1.85, 10, mats["GoldBrass"]))
    parts.append(add_cyl("Alem_Ball", (0, 0, h_wall + 6.40), 0.22, 0.22, 0.24, 12, mats["GoldBrass"]))
    # 4 Corner mini lead domes
    for sx in (-5.6, 5.6):
        for sy in (-5.6, 5.6):
            parts.append(add_dome(f"MiniDome_{sx}_{sy}", (sx, sy, h_wall + 0.32), 1.95, 0.72, mats["LeadRoof"], segs=16, rings=7))

    # TWIN SLENDER OTTOMAN MINARETS on front-left & front-right corners (matching real Hz. Osman Camii!)
    for side in (-1.0, 1.0):
        mx = side * (half + 1.35)
        my = half - 0.8
        parts.append(add_box(f"Min_Base_{side}", (mx, my, 2.2), (2.2, 2.2, 4.4), mats["StoneTrim"]))
        parts.append(add_cyl(f"Min_Shaft_{side}", (mx, my, 12.2), 0.78, 0.62, 16.0, 18, mats["StoneTrim"]))
        # Şerefe (balcony) with gold trim
        parts.append(add_cyl(f"Min_Serefe_{side}", (mx, my, 15.2), 1.12, 1.18, 0.85, 18, mats["StoneTrim"]))
        parts.append(add_cyl(f"Min_SerefeGold_{side}", (mx, my, 15.65), 1.16, 1.16, 0.14, 18, mats["GoldBrass"]))
        # Conical lead spire + gold alem finial
        parts.append(add_cyl(f"Min_Cone_{side}", (mx, my, 22.4), 0.66, 0.04, 4.6, 18, mats["LeadRoof"]))
        parts.append(add_cyl(f"Min_Alem_{side}", (mx, my, 25.1), 0.05, 0.02, 1.2, 8, mats["GoldBrass"]))

    return join_all(parts, "Sultanbeyli_HzOsmanCamii")


# ---------------------------------------------------------------------------
# 2. AUTHENTIC TURKISH MOSQUE INTERIOR (Saf Carpet, Mihrab, Minber, Kürsü, Chandelier, Shoe Rack)
# ---------------------------------------------------------------------------
def build_mosque_interior(mats):
    parts = []
    # Wall-to-wall Emerald-Green Cami Halısı (14.4m x 14.4m at Z = 0.16)
    parts.append(add_box("Carpet_Base", (0, 0, 0.16), (14.4, 14.4, 0.04), mats["CanGreen"], bevel=0.0))
    # Crisp Gold Horizontal Saf Lines (Namaz Safları) facing South (-Y = Kıble)!
    for row_y in (-5.2, -3.6, -2.0, -0.4, 1.2, 2.8, 4.4):
        parts.append(add_box(f"Saf_Line_{row_y:.1f}", (0, row_y, 0.185), (13.6, 0.09, 0.012), mats["GoldBrass"], bevel=0.0))

    # CARVED MARBLE & İZNİK-TILE MİHRAB centered on South Kıble wall (Y = -7.05)
    parts.append(add_box("Mihrab_Frame", (0, -7.05, 2.35), (2.85, 0.42, 4.40), mats["StoneTrim"], bevel=0.03))
    parts.append(add_box("Mihrab_TileBorder", (0, -6.86, 2.35), (2.35, 0.10, 3.85), mats["CobaltBlue"], bevel=0.02))
    parts.append(add_box("Mihrab_Niche", (0, -6.92, 2.10), (1.45, 0.34, 3.25), mats["CreamPaint"], bevel=0.02))
    parts.append(add_box("Mihrab_GoldCrown", (0, -6.82, 4.25), (2.45, 0.14, 0.32), mats["GoldBrass"], bevel=0.02))

    # STEPPED CARVED WALNUT MİNBER (to the right of Mihrab: X = -3.6, Y = -4.8..-7.0)
    parts.append(add_box("Minber_Body", (-3.8, -5.8, 1.65), (1.35, 2.9, 3.10), mats["Wood"], bevel=0.02))
    for st in range(7):
        parts.append(add_box(f"Minber_Step_{st}", (-3.8, -4.35 - st * 0.28, 0.30 + st * 0.28), (1.15, 0.30, 0.26), mats["DarkWood"]))
    parts.append(add_box("Minber_Canopy", (-3.8, -6.6, 3.85), (1.42, 1.35, 0.16), mats["Wood"]))
    parts.append(add_cyl("Minber_Spire", (-3.8, -6.6, 4.75), 0.62, 0.04, 1.75, 12, mats["DarkWood"]))
    parts.append(add_cyl("Minber_Alem", (-3.8, -6.6, 5.75), 0.04, 0.02, 0.45, 8, mats["GoldBrass"]))

    # VAAZ KÜRSÜSÜ (carved wooden preaching lectern to the left of Mihrab: X = +4.2, Y = -6.0)
    parts.append(add_box("Kursu_Base", (4.2, -6.0, 0.55), (1.45, 1.45, 0.85), mats["Wood"], bevel=0.02))
    parts.append(add_box("Kursu_Rail", (4.2, -5.32, 1.25), (1.45, 0.12, 0.72), mats["DarkWood"]))
    parts.append(add_box("Kursu_Desk", (4.2, -5.45, 1.58), (0.95, 0.42, 0.06), mats["Wood"], rot_euler=(0.35, 0, 0)))

    # GRAND SUSPENDED OTTOMAN RING CHANDELIER (AVİZE) hanging down to Z = 3.8m!
    parts.append(add_cyl("Chandelier_Chain", (0, 0, 7.6), 0.05, 0.05, 7.2, 8, mats["GoldBrass"]))
    for ring_i, (r, z, bulbs) in enumerate([(2.4, 4.0, 16), (1.35, 4.7, 10)]):
        for bi in range(bulbs):
            ang = 2.0 * math.pi * bi / bulbs
            bx = math.cos(ang) * r
            by = math.sin(ang) * r
            parts.append(add_box(f"ChRing_{ring_i}_{bi}", (bx, by, z), (0.36, 0.10, 0.10), mats["GoldBrass"], rot_euler=(0, 0, ang + math.pi * 0.5)))
            parts.append(add_cyl(f"ChBulb_{ring_i}_{bi}", (bx, by, z + 0.18), 0.08, 0.06, 0.22, 8, mats["LitScreen"]))

    # Entrance Wooden Shoe Racks (Ayakkabılık) flanking the inside of the front door (+Y = +6.9)
    for sx in (-3.6, 3.6):
        parts.append(add_box(f"ShoeRack_{sx:.1f}", (sx, 6.85, 0.62), (2.2, 0.42, 1.05), mats["Wood"], bevel=0.015))

    return join_all(parts, "Mosque_InteriorProps")


# ---------------------------------------------------------------------------
# 3. OCTAGONAL MARBLE COURTYARD ŞADIRVAN (ABDEST ALMA ÇEŞMESİ)
# ---------------------------------------------------------------------------
def build_sadirvan(mats):
    parts = []
    # Octagonal marble water basin + central pedestal + 8 brass faucets + stone stools!
    parts.append(add_cyl("Sad_Step", (0, 0, 0.10), 2.55, 2.50, 0.20, 8, mats["StoneTrim"]))
    parts.append(add_cyl("Sad_Basin", (0, 0, 0.62), 1.65, 1.72, 0.92, 8, mats["StoneTrim"]))
    parts.append(add_cyl("Sad_Water", (0, 0, 1.02), 1.48, 1.48, 0.08, 8, mats["Water"]))
    parts.append(add_cyl("Sad_CenterCol", (0, 0, 1.55), 0.36, 0.28, 1.30, 12, mats["StoneTrim"]))
    # 8 perimeter columns + octagonal lead dome canopy + gold alem!
    for i in range(8):
        ang = 2.0 * math.pi * i / 8.0
        px = math.cos(ang) * 2.18
        py = math.sin(ang) * 2.18
        parts.append(add_cyl(f"Sad_Col_{i}", (px, py, 1.75), 0.11, 0.10, 3.10, 10, mats["StoneTrim"]))
        # Brass faucet & stone seat stool on each side
        fx = math.cos(ang + 0.39) * 1.78
        fy = math.sin(ang + 0.39) * 1.78
        parts.append(add_box(f"Sad_Tap_{i}", (fx, fy, 0.78), (0.14, 0.06, 0.06), mats["GoldBrass"], rot_euler=(0, 0, ang + 0.39)))
        sx = math.cos(ang + 0.39) * 2.38
        sy = math.sin(ang + 0.39) * 2.38
        parts.append(add_cyl(f"Sad_Stool_{i}", (sx, sy, 0.32), 0.20, 0.20, 0.42, 8, mats["StoneTrim"]))

    parts.append(add_cyl("Sad_RoofEave", (0, 0, 3.38), 2.85, 2.65, 0.22, 8, mats["LeadRoof"]))
    parts.append(add_dome("Sad_Dome", (0, 0, 3.45), 2.55, 0.65, mats["LeadRoof"], segs=16, rings=8))
    parts.append(add_cyl("Sad_Alem", (0, 0, 5.45), 0.05, 0.02, 0.75, 8, mats["GoldBrass"]))
    return join_all(parts, "Mosque_Sadirvan")


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


def render_preview():
    reset_scene()
    mats = get_mats()
    m = build_hz_osman_camii(mats)
    i = build_mosque_interior(mats)
    s = build_sadirvan(mats); s.location = Vector((0, 14.5, 0))

    world = bpy.data.worlds.new("Sky")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.72, 0.78, 0.86, 1.0)
        bg.inputs["Strength"].default_value = 0.9

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 2.6
    sun_obj = bpy.data.objects.new("Sun", sun_data)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = Euler((math.radians(46), math.radians(15), math.radians(155)), "XYZ")

    # Interior shot looking right at Mihrab, Minber, Kürsü & Emerald Saf Carpet!
    pt_data = bpy.data.lights.new("InteriorLamp", type="POINT")
    pt_data.energy = 850.0
    pt_data.color = (1.0, 0.92, 0.78)
    pt_obj = bpy.data.objects.new("InteriorLamp", pt_data)
    bpy.context.collection.objects.link(pt_obj)
    pt_obj.location = Vector((0, 0, 4.5))

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 22.0
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((0.8, 5.8, 2.1))
    cam_obj.rotation_euler = (Vector((-0.4, -7.0, 2.2)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_obj

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_HzOsmanCamii.blend"))
    bpy.ops.render.render(write_still=True)
    print("Saved Hz. Osman Camii interior preview to:", PREVIEW_PATH)


def main():
    builders = [
        ("Sultanbeyli_HzOsmanCamii.fbx", build_hz_osman_camii),
        ("Mosque_InteriorProps.fbx", build_mosque_interior),
        ("Mosque_Sadirvan.fbx", build_sadirvan),
    ]
    print(f"[GRIDROAM Blender] Sculpting {len(builders)} authentic Hz. Osman Camii models...")
    for fbx_name, fn in builders:
        reset_scene()
        mats = get_mats()
        obj = fn(mats)
        export_fbx(obj, fbx_name)
    render_preview()


if __name__ == "__main__":
    main()
