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


def add_cyl(name, center, r1, r2, depth, segs, mat, rot_euler=None, cap_ends=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap_ends, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
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


def enforce_mat_order(obj, ordered_names):
    mesh = obj.data
    old_mats = list(mesh.materials)
    by_name = {m.name: m for m in old_mats if m is not None}
    new_mats = [by_name[n] for n in ordered_names if n in by_name]
    for m in old_mats:
        if m is not None and m not in new_mats:
            new_mats.append(m)
    remap = {oi: new_mats.index(om) for oi, om in enumerate(old_mats) if om in new_mats}
    p_idx = [remap.get(p.material_index, 0) for p in mesh.polygons]
    mesh.materials.clear()
    for m in new_mats:
        mesh.materials.append(m)
    for p, ni in zip(mesh.polygons, p_idx):
        p.material_index = ni
    mesh.update()


def add_brass_crescent_alem(parts, prefix, cx, cy, base_z, scale, mat):
    """Authentic 3D Ottoman brass alem spire with tiered orbs & true curved crescent moon!"""
    parts.append(add_cyl(f"{prefix}_Rod", (cx, cy, base_z + 0.90 * scale), 0.075 * scale, 0.032 * scale, 1.85 * scale, 12, mat))
    parts.append(add_cyl(f"{prefix}_Orb1", (cx, cy, base_z + 0.60 * scale), 0.24 * scale, 0.24 * scale, 0.26 * scale, 14, mat))
    parts.append(add_cyl(f"{prefix}_Orb2", (cx, cy, base_z + 1.18 * scale), 0.16 * scale, 0.16 * scale, 0.18 * scale, 14, mat))
    # True 3D curved crescent opening upward
    for ci in range(9):
        ang = math.radians(-125 + ci * 31.25)
        px = cx + math.sin(ang) * 0.34 * scale
        pz = base_z + 1.98 * scale - math.cos(ang) * 0.34 * scale
        thick = (0.10 - abs(ci - 4) * 0.015) * scale
        parts.append(add_box(f"{prefix}_Cr_{ci}", (px, cy, pz), (thick, 0.07 * scale, thick * 1.15), mat, bevel=0.01 * scale, rot_euler=(0, ang, 0)))


# ---------------------------------------------------------------------------
# 1. HOLLOW WALK-IN HZ. OSMAN CAMİİ (15.6m x 15.6m, front entrance faces +Y / Unity +Z!)
#    High-detail Ottoman architecture: ribbed main dome + 16-window clerestory drum,
#    4 semi-domes, 4 corner cupolas, 3-bay domed Revak entrance portico, double-tier arched
#    stained-glass windows w/ stone mullions, fluted twin minarets w/ double carved şerefes!
# ---------------------------------------------------------------------------
def build_hz_osman_camii(mats):
    parts = []
    half = 7.8
    h_wall = 6.6
    wt = 0.45

    # Raised marble plinth, cornice & interior floor slab (y = -7.8..+7.8, top at Z = 0.14)
    parts.append(add_box("Mosque_Floor", (0, 0, 0.07), (half * 2 + 0.8, half * 2 + 0.8, 0.14), mats["StoneTrim"], bevel=0.02))
    # Wide stepped marble entry porch + Revak colonnade platform on +Y front (Y = +7.8..+10.8)
    parts.append(add_box("Mosque_RevakFloor", (0, 9.25, 0.07), (half * 2 - 0.6, 2.95, 0.14), mats["StoneTrim"], bevel=0.02))
    parts.append(add_box("Mosque_EntryStep", (0, 10.95, 0.04), (5.2, 0.95, 0.08), mats["StoneTrim"], bevel=0.015))

    # 3 Solid Perimeter Walls (South Kıble back wall -Y, Left +X, Right -X) — HOLLOW INSIDE!
    parts.append(add_box("Wall_KibleBack", (0, -half + wt * 0.5, h_wall * 0.5), (half * 2, wt, h_wall), mats["StoneTrim"]))
    parts.append(add_box("Wall_KibleIn", (0, -half + wt + 0.03, h_wall * 0.5), (half * 2 - wt * 2, 0.06, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))
    parts.append(add_box("Wall_Left", (half - wt * 0.5, 0, h_wall * 0.5), (wt, half * 2, h_wall), mats["StoneTrim"]))
    parts.append(add_box("Wall_LeftIn", (half - wt - 0.03, 0, h_wall * 0.5), (0.06, half * 2 - wt * 2, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))
    parts.append(add_box("Wall_Right", (-half + wt * 0.5, 0, h_wall * 0.5), (wt, half * 2, h_wall), mats["StoneTrim"]))
    parts.append(add_box("Wall_RightIn", (-half + wt + 0.03, 0, h_wall * 0.5), (0.06, half * 2 - wt * 2, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))

    # Exterior perimeter stone string-courses & upper eaves cornice (4 perimeter strips — leaves interior 100% HOLLOW!)
    for s in (-1.0, 1.0):
        # East & West side string-course + cornice
        parts.append(add_box(f"Ext_MidSide_{s}", (s * (half + 0.06), 0, 3.65), (0.16, half * 2 + 0.24, 0.18), mats["StoneTrim"], bevel=0.02))
        parts.append(add_box(f"Ext_TopSide_{s}", (s * (half + 0.12), 0, h_wall + 0.04), (0.30, half * 2 + 0.48, 0.32), mats["StoneTrim"], bevel=0.03))
        # North & South cornice
        parts.append(add_box(f"Ext_TopNS_{s}", (0, s * (half + 0.12), h_wall + 0.04), (half * 2 + 0.48, 0.30, 0.32), mats["StoneTrim"], bevel=0.03))
    # South back string-course + Front left/right string-course flanking the open portal
    parts.append(add_box("Ext_MidBack", (0, -half - 0.06, 3.65), (half * 2 + 0.24, 0.16, 0.18), mats["StoneTrim"], bevel=0.02))
    for s in (-1.0, 1.0):
        parts.append(add_box(f"Ext_MidFront_{s}", (s * 4.95, half + 0.06, 3.65), (5.80, 0.16, 0.18), mats["StoneTrim"], bevel=0.02))
    # Corner stone buttress pilasters
    for cx in (-half, half):
        for cy in (-half, half):
            parts.append(add_box(f"CornerPil_{cx}_{cy}", (cx, cy, h_wall * 0.5), (0.78, 0.78, h_wall + 0.12), mats["StoneTrim"], bevel=0.03))

    # Front Wall (+Y) split cleanly around a WIDE 3.2m x 3.6m OPEN ENTRANCE PORTAL!
    door_half = 1.60
    door_h = 3.60
    side_w = half - door_half
    for s in (-1.0, 1.0):
        scx = s * (door_half + side_w * 0.5)
        parts.append(add_box(f"Wall_Front_{s}", (scx, half - wt * 0.5, h_wall * 0.5), (side_w, wt, h_wall), mats["StoneTrim"]))
        parts.append(add_box(f"Wall_FrontIn_{s}", (scx, half - wt - 0.03, h_wall * 0.5), (side_w - 0.06, 0.06, h_wall - 0.2), mats["CreamPaint"], bevel=0.0))
    parts.append(add_box("Wall_FrontLintel", (0, half - wt * 0.5, door_h + (h_wall - door_h) * 0.5), (door_half * 2 + 0.1, wt, h_wall - door_h), mats["StoneTrim"]))

    # Monumental Carved Ottoman Muqarnas Taç Kapı (Entrance Portal) + Swung-Open Carved Walnut Doors!
    parts.append(add_box("Portal_FrameL", (door_half + 0.26, half + 0.18, 2.55), (0.52, 0.62, 5.10), mats["StoneTrim"], bevel=0.03))
    parts.append(add_box("Portal_FrameR", (-door_half - 0.26, half + 0.18, 2.55), (0.52, 0.62, 5.10), mats["StoneTrim"], bevel=0.03))
    parts.append(add_box("Portal_TileBand", (0, half + 0.20, 4.35), (4.35, 0.64, 0.72), mats["CobaltBlue"], bevel=0.02))
    parts.append(add_box("Portal_GoldCalligraphy", (0, half + 0.54, 4.35), (3.10, 0.05, 0.36), mats["GoldBrass"], bevel=0.01))
    parts.append(add_box("Portal_Crown", (0, half + 0.22, 4.92), (4.65, 0.72, 0.38), mats["StoneTrim"], bevel=0.03))
    parts.append(add_box("Portal_DoorOpenL", (door_half - 0.12, half - 1.15, 1.75), (0.10, 1.45, 3.35), mats["DarkWood"]))
    parts.append(add_box("Portal_DoorOpenR", (-door_half + 0.12, half - 1.15, 1.75), (0.10, 1.45, 3.35), mats["DarkWood"]))

    # Authentic 3-Bay Front Ottoman Revak (Domed Entrance Colonnade Portico at Y = +10.25m)!
    revak_xs = (-6.2, -2.2, 2.2, 6.2)
    for rx in revak_xs:
        parts.append(add_box(f"RevColBase_{rx}", (rx, 10.25, 0.32), (0.58, 0.58, 0.42), mats["StoneTrim"], bevel=0.02))
        parts.append(add_cyl(f"RevCol_{rx}", (rx, 10.25, 2.55), 0.21, 0.18, 4.10, 14, mats["StoneTrim"]))
        parts.append(add_box(f"RevColCap_{rx}", (rx, 10.25, 4.72), (0.62, 0.62, 0.28), mats["GoldBrass"], bevel=0.02))
    # Continuous Revak lintel beam + 3 pointed Ottoman arches + 3 lead portico domes!
    parts.append(add_box("Revak_ArchBeam", (0, 10.25, 5.15), (13.2, 0.48, 0.55), mats["StoneTrim"], bevel=0.03))
    parts.append(add_box("Revak_RoofDeck", (0, 9.15, 5.42), (13.4, 2.75, 0.24), mats["StoneTrim"], bevel=0.02))
    for bay_x in (-4.2, 0.0, 4.2):
        parts.append(add_dome(f"Revak_Dome_{bay_x}", (bay_x, 9.15, 5.50), 1.48, 0.72, mats["LeadRoof"], segs=18, rings=8))
        parts.append(add_cyl(f"Revak_Fin_{bay_x}", (bay_x, 9.15, 6.72), 0.04, 0.02, 0.55, 8, mats["GoldBrass"]))

    # Double-Tier Arched Ottoman Stained-Glass Windows w/ Stone Frames & Vertical Mullions (Left, Right & Back)!
    for wy in (-4.6, 0.0, 4.6):
        for sx in (-1.0, 1.0):
            # Lower rectangular framed window
            parts.append(add_box(f"WinSL_{sx}_{wy}", (sx * (half + 0.04), wy, 2.15), (0.10, 1.62, 1.95), mats["StoneTrim"], bevel=0.02))
            parts.append(add_box(f"WinSLG_{sx}_{wy}", (sx * (half + 0.07), wy, 2.15), (0.05, 1.34, 1.65), mats["Glass"]))
            parts.append(add_box(f"WinSLM_{sx}_{wy}", (sx * (half + 0.10), wy, 2.15), (0.04, 0.08, 1.65), mats["StoneTrim"]))
            # Upper arched cobalt-trimmed stained-glass lancet window
            parts.append(add_box(f"WinSU_{sx}_{wy}", (sx * (half + 0.04), wy, 4.95), (0.10, 1.38, 1.75), mats["CobaltBlue"], bevel=0.02))
            parts.append(add_box(f"WinSUG_{sx}_{wy}", (sx * (half + 0.07), wy, 4.95), (0.05, 1.12, 1.48), mats["Glass"]))
            parts.append(add_cyl(f"WinSUA_{sx}_{wy}", (sx * (half + 0.06), wy, 5.68), 0.68, 0.68, 0.10, 14, mats["CobaltBlue"], rot_euler=(0, math.pi * 0.5, 0)))
    for wx in (-4.6, 0.0, 4.6):
        parts.append(add_box(f"WinBL_{wx}", (wx, -half - 0.04, 2.15), (1.62, 0.10, 1.95), mats["StoneTrim"], bevel=0.02))
        parts.append(add_box(f"WinBLG_{wx}", (wx, -half - 0.07, 2.15), (1.34, 0.05, 1.65), mats["Glass"]))
        parts.append(add_box(f"WinBU_{wx}", (wx, -half - 0.04, 4.95), (1.38, 0.10, 1.75), mats["CobaltBlue"], bevel=0.02))
        parts.append(add_box(f"WinBUG_{wx}", (wx, -half - 0.07, 4.95), (1.12, 0.05, 1.48), mats["Glass"]))

    # 4 Interior Structural Marble Columns supporting the dome drum
    for cx in (-4.6, 4.6):
        for cy in (-4.6, 4.6):
            parts.append(add_cyl(f"Col_{cx}_{cy}", (cx, cy, h_wall * 0.5), 0.38, 0.34, h_wall, 18, mats["StoneTrim"]))
            parts.append(add_box(f"ColBase_{cx}_{cy}", (cx, cy, 0.35), (0.98, 0.98, 0.44), mats["StoneTrim"], bevel=0.02))
            parts.append(add_box(f"ColCap_{cx}_{cy}", (cx, cy, h_wall - 0.25), (1.02, 1.02, 0.48), mats["GoldBrass"], bevel=0.02))

    # Roof deck framing the wide open central vault + low roof parapet
    slab_w = 3.2
    parts.append(add_box("RoofDeck_N", (0, half - slab_w * 0.5, h_wall + 0.18), (half * 2 + 0.4, slab_w, 0.36), mats["StoneTrim"]))
    parts.append(add_box("RoofDeck_S", (0, -half + slab_w * 0.5, h_wall + 0.18), (half * 2 + 0.4, slab_w, 0.36), mats["StoneTrim"]))
    parts.append(add_box("RoofDeck_E", (half - slab_w * 0.5, 0, h_wall + 0.18), (slab_w, half * 2 - slab_w * 2, 0.36), mats["StoneTrim"]))
    parts.append(add_box("RoofDeck_W", (-half + slab_w * 0.5, 0, h_wall + 0.18), (slab_w, half * 2 - slab_w * 2, 0.36), mats["StoneTrim"]))

    # Hollow Cylindrical Clerestory Dome Drum (cap_ends=False so interior vault opens straight up into the dome!)
    drum_z = h_wall + 1.05
    parts.append(add_cyl("Dome_Drum", (0, 0, drum_z), 6.18, 6.10, 1.65, 32, mats["StoneTrim"], cap_ends=False))
    parts.append(add_cyl("Dome_DrumIn", (0, 0, drum_z), 5.86, 5.84, 1.65, 28, mats["CreamPaint"], cap_ends=False))
    parts.append(add_cyl("Dome_DrumCornice", (0, 0, drum_z + 0.86), 6.32, 6.26, 0.22, 32, mats["StoneTrim"], cap_ends=False))
    for wi in range(16):
        ang = (2.0 * math.pi * wi) / 16.0
        wx, wy = math.cos(ang) * 6.16, math.sin(ang) * 6.16
        parts.append(add_box(f"DrumWinF_{wi}", (wx, wy, drum_z + 0.05), (0.14, 0.78, 1.05), mats["CobaltBlue"], rot_euler=(0, 0, ang)))
        parts.append(add_box(f"DrumWinG_{wi}", (math.cos(ang) * 6.20, math.sin(ang) * 6.20, drum_z + 0.05), (0.08, 0.56, 0.84), mats["Glass"], rot_euler=(0, 0, ang)))
        # Small drum buttress fin between windows
        bang = ang + math.pi / 16.0
        parts.append(add_box(f"DrumBut_{wi}", (math.cos(bang) * 6.28, math.sin(bang) * 6.28, drum_z - 0.10), (0.42, 0.26, 1.35), mats["StoneTrim"], bevel=0.02, rot_euler=(0, 0, bang)))

    # Grand Ribbed Ottoman Lead Dome (Outer + Hollow Cream Inner Vault) + 16 Standing-Seam Lead Ribs!
    dome_base_z = drum_z + 0.88
    parts.append(add_dome("Dome_Outer", (0, 0, dome_base_z), 6.08, 0.80, mats["LeadRoof"], segs=32, rings=14, invert_inner=False))
    parts.append(add_dome("Dome_Inner", (0, 0, h_wall + 0.30), 5.82, 0.96, mats["CreamPaint"], segs=24, rings=12, invert_inner=True))
    for ri in range(16):
        ang = (2.0 * math.pi * ri) / 16.0
        for step in range(5):
            phi = math.radians(14 + step * 16)
            r_s = 6.12 * math.cos(phi)
            z_s = dome_base_z + 6.08 * 0.80 * math.sin(phi)
            parts.append(add_box(f"DomeRib_{ri}_{step}", (math.cos(ang) * r_s, math.sin(ang) * r_s, z_s), (0.12, 0.11, 0.82), mats["LeadRoof"], rot_euler=(0, math.pi * 0.5 - phi, ang)))

    # True 3D Curved Brass Crescent Alem atop Main Dome!
    add_brass_crescent_alem(parts, "Alem_Main", 0.0, 0.0, dome_base_z + 4.82, 1.15, mats["GoldBrass"])

    # 4 Stepped Semi-Domes (N/S/E/W) + 4 Corner Cupolas w/ Brass Finials
    for (sx, sy) in ((0.0, -5.9), (0.0, 5.9), (-5.9, 0.0), (5.9, 0.0)):
        parts.append(add_dome(f"SemiDome_{sx}_{sy}", (sx, sy, h_wall + 0.34), 2.55, 0.78, mats["LeadRoof"], segs=18, rings=8))
    for sx in (-5.7, 5.7):
        for sy in (-5.7, 5.7):
            parts.append(add_dome(f"MiniDome_{sx}_{sy}", (sx, sy, h_wall + 0.34), 1.92, 0.74, mats["LeadRoof"], segs=18, rings=8))
            parts.append(add_cyl(f"MiniFin_{sx}_{sy}", (sx, sy, h_wall + 1.95), 0.04, 0.02, 0.55, 8, mats["GoldBrass"]))

    # TWIN FLUTED OTTOMAN MINARETS w/ Square-to-Octagonal Pabuç Base, DOUBLE Carved Şerefe Balconies & 3D Crescent Alems!
    for side in (-1.0, 1.0):
        mx = side * (half + 1.45)
        my = half - 0.85
        # Square base + chamfered octagonal pabuç transition
        parts.append(add_box(f"Min_Base_{side}", (mx, my, 2.25), (2.35, 2.35, 4.50), mats["StoneTrim"], bevel=0.04))
        parts.append(add_cyl(f"Min_Pabuc_{side}", (mx, my, 5.35), 1.32, 0.84, 1.75, 8, mats["StoneTrim"]))
        # Tall fluted stone shaft
        parts.append(add_cyl(f"Min_Shaft_{side}", (mx, my, 13.6), 0.80, 0.62, 15.2, 20, mats["StoneTrim"]))
        for fi in range(8):
            fa = (2.0 * math.pi * fi) / 8.0
            parts.append(add_cyl(f"Min_Flute_{side}_{fi}", (mx + math.cos(fa) * 0.71, my + math.sin(fa) * 0.71, 13.4), 0.075, 0.060, 14.4, 6, mats["CreamPaint"]))
        # DOUBLE Carved Şerefe Balconies (Lower at Z=13.4m, Upper at Z=18.2m) with Stepped Muqarnas Corbels & Gold Trim!
        for sh_i, sh_z in enumerate((13.4, 18.2)):
            r_sc = 1.0 - sh_i * 0.08
            parts.append(add_cyl(f"Min_Corb1_{side}_{sh_i}", (mx, my, sh_z - 0.45), 0.76 * r_sc, 1.02 * r_sc, 0.42, 16, mats["StoneTrim"]))
            parts.append(add_cyl(f"Min_Corb2_{side}_{sh_i}", (mx, my, sh_z - 0.10), 1.02 * r_sc, 1.24 * r_sc, 0.34, 16, mats["StoneTrim"]))
            parts.append(add_cyl(f"Min_Parap_{side}_{sh_i}", (mx, my, sh_z + 0.42), 1.24 * r_sc, 1.24 * r_sc, 0.74, 16, mats["StoneTrim"]))
            parts.append(add_cyl(f"Min_Gold_{side}_{sh_i}", (mx, my, sh_z + 0.82), 1.28 * r_sc, 1.28 * r_sc, 0.12, 16, mats["GoldBrass"]))
        # Conical lead spire + true 3D curved brass crescent alem!
        parts.append(add_cyl(f"Min_Cone_{side}", (mx, my, 23.6), 0.66, 0.04, 4.9, 20, mats["LeadRoof"]))
        add_brass_crescent_alem(parts, f"MinAlem_{side}", mx, my, 26.0, 0.72, mats["GoldBrass"])

    mosque = join_all(parts, "Sultanbeyli_HzOsmanCamii")
    enforce_mat_order(mosque, ["StoneTrim", "CreamPaint", "CobaltBlue", "DarkWood", "Glass", "GoldBrass", "LeadRoof"])
    return mosque


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
        parts.append(add_box(f"Sad_ColBase_{i}", (px, py, 0.30), (0.30, 0.30, 0.24), mats["StoneTrim"], bevel=0.015, rot_euler=(0, 0, ang)))
        parts.append(add_cyl(f"Sad_Col_{i}", (px, py, 1.75), 0.11, 0.10, 2.85, 12, mats["StoneTrim"]))
        parts.append(add_box(f"Sad_ColCap_{i}", (px, py, 3.16), (0.32, 0.32, 0.18), mats["GoldBrass"], bevel=0.015, rot_euler=(0, 0, ang)))
        # Stone arch lintel span between adjacent columns
        mid_a = ang + math.pi / 8.0
        parts.append(add_box(f"Sad_Lintel_{i}", (math.cos(mid_a) * 2.02, math.sin(mid_a) * 2.02, 3.22), (0.24, 1.62, 0.24), mats["StoneTrim"], bevel=0.02, rot_euler=(0, 0, mid_a)))
        # Brass faucet & stone seat stool on each side
        fx = math.cos(ang + 0.39) * 1.78
        fy = math.sin(ang + 0.39) * 1.78
        parts.append(add_box(f"Sad_Tap_{i}", (fx, fy, 0.78), (0.14, 0.06, 0.06), mats["GoldBrass"], rot_euler=(0, 0, ang + 0.39)))
        sx = math.cos(ang + 0.39) * 2.38
        sy = math.sin(ang + 0.39) * 2.38
        parts.append(add_cyl(f"Sad_Stool_{i}", (sx, sy, 0.32), 0.20, 0.20, 0.42, 8, mats["StoneTrim"]))

    parts.append(add_cyl("Sad_RoofEave", (0, 0, 3.40), 2.92, 2.70, 0.24, 8, mats["LeadRoof"]))
    parts.append(add_dome("Sad_Dome", (0, 0, 3.48), 2.58, 0.66, mats["LeadRoof"], segs=24, rings=10))
    add_brass_crescent_alem(parts, "Sad_Alem", 0.0, 0.0, 5.15, 0.48, mats["GoldBrass"])
    sad = join_all(parts, "Mosque_Sadirvan")
    enforce_mat_order(sad, ["StoneTrim", "Water", "GoldBrass", "LeadRoof"])
    return sad


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
