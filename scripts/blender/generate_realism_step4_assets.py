#!/usr/bin/env python3
"""
GRIDROAM Step 4 Visual Realism 3D Asset Generator (Blender 5.3+)
Exports 6 high-detail custom FBX assets directly into Assets/GRIDROAM/Resources/OurWorldLife/:
  1. OW_SteeringWheel.fbx   — Contoured 3-spoke leather sport steering wheel w/ red 12-o'clock stripe & chrome hub
  2. OW_PlaneTree.fbx       — Multi-branch Istanbul Cinar w/ 112 arched 3D botanical alpha-cutout leaf cards
  3. OW_CypressTree.fbx     — Tall Istanbul Selvi w/ 72 feathered upward-sweeping botanical frond cards
  4. OW_AydosPineTree.fbx   — Iconic Aydos Kizilcam / Umbrella Pine w/ furrowed bark & 100 pine needle rosette cards
  5. OW_AydosCastle.fbx     — Historic Byzantine/Ottoman Aydos Kalesi (4 watchtowers, voussoir gate, portcullis, ramparts)
  6. OW_M5MetroEntrance.fbx — Authentic Istanbul M5 Metro glass barrel-vault canopy, steel ribs, escalators & red M totem

Run via:
  /Applications/Blender.app/Contents/MacOS/Blender --background --python scripts/blender/generate_realism_step4_assets.py
"""

import math
import os
import random
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_LIFE_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldLife")
TEX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldTheme", "Textures")
BLEND_DIR = os.path.join(SCRIPT_DIR, "blend_sources")
PREVIEW_PATH = os.path.join(SCRIPT_DIR, "blender_step4_showcase.png")

os.makedirs(OUT_LIFE_DIR, exist_ok=True)
os.makedirs(BLEND_DIR, exist_ok=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.55, metallic=0.0, emission=None, emission_strength=0.0, alpha=1.0):
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
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = alpha
        if emission is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def apply_auto_smooth(obj, angle_deg=42.0):
    mesh = obj.data
    if not isinstance(mesh, bpy.types.Mesh):
        return
    for p in mesh.polygons:
        p.use_smooth = True
    bm = bmesh.new()
    bm.from_mesh(mesh)
    thresh = math.radians(angle_deg)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            e.smooth = e.calc_face_angle(0.0) < thresh
        else:
            e.smooth = False
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def box_uv_project(obj, scale=0.45):
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


def make_obj(name, bm, mat, smooth_angle=42.0, uv_scale=0.45, auto_uv=True):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if auto_uv:
        box_uv_project(obj, scale=uv_scale)
    apply_auto_smooth(obj, angle_deg=smooth_angle)
    return obj


def add_box(name, center, size, mat, bevel=0.02, segments=1, rot_euler=(0.0, 0.0, 0.0), uv_scale=0.45):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.2:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segments, affect="EDGES", clamp_overlap=True)
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return make_obj(name, bm, mat, smooth_angle=35.0, uv_scale=uv_scale)


def add_cyl(name, center, r1, r2, depth, segs, mat, rot_euler=(0.0, 0.0, 0.0), cap_ends=True, uv_scale=0.45):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap_ends, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return make_obj(name, bm, mat, smooth_angle=52.0, uv_scale=uv_scale)


def add_tube(name, points, radii, sides, mat, cap_ends=True, uv_scale=0.55):
    bm = bmesh.new()
    rings = []
    n_pts = len(points)
    for i, pt in enumerate(points):
        p = Vector(pt)
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        if i == 0:
            tangent = (Vector(points[1]) - p).normalized()
        elif i == n_pts - 1:
            tangent = (p - Vector(points[i - 1])).normalized()
        else:
            tangent = (Vector(points[i + 1]) - Vector(points[i - 1])).normalized()
        up_ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.92 else Vector((1, 0, 0))
        side_x = tangent.cross(up_ref).normalized()
        side_y = side_x.cross(tangent).normalized()
        ring = []
        for s in range(sides):
            ang = (2.0 * math.pi * s) / sides
            ring.append(bm.verts.new(p + (side_x * math.cos(ang) + side_y * math.sin(ang)) * r))
        rings.append(ring)
    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(sides):
            sn = (s + 1) % sides
            bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
    if cap_ends and len(rings) >= 2:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    return make_obj(name, bm, mat, smooth_angle=58.0, uv_scale=uv_scale)


def add_torus_ring(name, center, major_r, minor_r, maj_segs, min_segs, mat, axis="Z"):
    bm = bmesh.new()
    rings = []
    for i in range(maj_segs):
        theta = (2.0 * math.pi * i) / maj_segs
        ct, st = math.cos(theta), math.sin(theta)
        ring = []
        for j in range(min_segs):
            phi = (2.0 * math.pi * j) / min_segs
            cp, sp = math.cos(phi), math.sin(phi)
            rr = major_r + minor_r * cp
            if axis == "Z":
                p = Vector((rr * ct, rr * st, minor_r * sp)) + Vector(center)
            else:
                p = Vector((minor_r * sp, rr * ct, rr * st)) + Vector(center)
            ring.append(bm.verts.new(p))
        rings.append(ring)
    for i in range(maj_segs):
        r0 = rings[i]
        r1 = rings[(i + 1) % maj_segs]
        for j in range(min_segs):
            jn = (j + 1) % min_segs
            bm.faces.new((r0[j], r0[jn], r1[jn], r1[j]))
    return make_obj(name, bm, mat, smooth_angle=65.0, uv_scale=2.0)


def add_foliage_cards(name, cards_specs, mat):
    """Build smooth arched 2x2 botanical foliage leaf cards with exact [0..1] per-card UVs."""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.verify()
    for (center, size_w, size_h, rot_euler, bend) in cards_specs:
        rot_m = Euler(rot_euler, "XYZ").to_matrix()
        c_vec = Vector(center)
        grid = []
        for iy in range(3):
            v_coord = iy * 0.5
            ly = (v_coord - 0.5) * size_h
            row = []
            for ix in range(3):
                u_coord = ix * 0.5
                lx = (u_coord - 0.5) * size_w
                dist2 = (u_coord - 0.5) ** 2 + (v_coord - 0.5) ** 2
                lz = -bend * dist2 * min(size_w, size_h)
                wp = c_vec + rot_m @ Vector((lx, ly, lz))
                row.append((bm.verts.new(wp), u_coord, v_coord))
            grid.append(row)
        for iy in range(2):
            for ix in range(2):
                v00, u00, w00 = grid[iy][ix]
                v10, u10, w10 = grid[iy][ix + 1]
                v11, u11, w11 = grid[iy + 1][ix + 1]
                v01, u01, w01 = grid[iy + 1][ix]
                f = bm.faces.new((v00, v10, v11, v01))
                f.smooth = True
                for loop, (uu, vv) in zip(f.loops, ((u00, w00), (u10, w10), (u11, w11), (u01, w01))):
                    loop[uv_layer].uv = (uu, vv)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def join_parts(parts, final_name):
    bpy.ops.object.select_all(action="DESELECT")
    valid = [p for p in parts if p is not None]
    for p in valid:
        p.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = final_name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


def export_life_fbx(obj, filename):
    out_path = os.path.join(OUT_LIFE_DIR, filename)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="AUTO",
    )
    tris = sum(max(1, len(p.vertices) - 2) for p in obj.data.polygons)
    mats = [m.name for m in obj.data.materials if m]
    print(f"  Exported {filename:26s} | verts={len(obj.data.vertices):5d} | tris={tris:5d} | mats={mats}")
    return out_path


# ---------------------------------------------------------------------------
# 1. OW_SteeringWheel.fbx — Contoured 3-Spoke Leather Sports Steering Wheel
#    Matches SteeringWheel local space (Euler 65,0,0 -> driver sees -Z, 12-o'clock at -Y)
# ---------------------------------------------------------------------------
def build_steering_wheel():
    leather = make_mat("WheelLeather", (0.11, 0.11, 0.12), roughness=0.64, metallic=0.04)
    chrome = make_mat("WheelChrome", (0.82, 0.84, 0.88), roughness=0.22, metallic=0.88)
    red_stripe = make_mat("WheelSportRed", (0.86, 0.12, 0.14), roughness=0.35, metallic=0.10)
    dark_trim = make_mat("WheelDarkTrim", (0.16, 0.17, 0.19), roughness=0.48, metallic=0.25)

    parts = []
    # Main ergonomic leather torus rim (radius 0.175m, grip radius 0.0195m)
    parts.append(add_torus_ring("Rim", (0, 0, 0), 0.175, 0.0195, 32, 12, leather, axis="Z"))
    # Red motorsport 12-o'clock top-center leather band (at -Y in Blender -> UP in Unity SteeringWheel!)
    parts.append(add_box("TopCenterStripe", (0.0, -0.175, 0.0), (0.022, 0.044, 0.043), red_stripe, bevel=0.006, segments=2))
    # Ergonomic 10-and-2 thumb bolsters
    for sx in (-1.0, 1.0):
        ang = math.radians(150 if sx > 0 else 30)
        bx, by = math.cos(ang) * 0.170, -math.sin(ang) * 0.170
        parts.append(add_box(f"ThumbBolster_{sx}", (bx, by, -0.004), (0.028, 0.036, 0.034), leather, bevel=0.008, segments=1, rot_euler=(0, 0, sx * 0.45)))

    # Sculpted 3 Metallic Spokes (9 o'clock, 3 o'clock, 6 o'clock at +Y!) + Multifunction Button Pods
    for sx in (-1.0, 1.0):
        parts.append(add_box(f"SpokeSide_{sx}", (sx * 0.108, 0.005, 0.006), (0.115, 0.036, 0.016), chrome, bevel=0.005, segments=1, rot_euler=(0, sx * -0.10, 0)))
        parts.append(add_box(f"ThumbButtons_{sx}", (sx * 0.095, -0.002, -0.006), (0.044, 0.030, 0.012), dark_trim, bevel=0.003, segments=1))
    # Split twin lower spoke at 6 o'clock (+Y)
    for sx in (-1.0, 1.0):
        parts.append(add_box(f"SpokeBottom_{sx}", (sx * 0.022, 0.106, 0.006), (0.020, 0.118, 0.016), chrome, bevel=0.004, segments=1, rot_euler=(-0.10, 0, sx * 0.10)))

    # Contoured central airbag horn boss + chrome emblem ring (facing driver at -Z AND clean back cap at +Z)
    parts.append(add_cyl("HornBoss", (0, 0, 0.002), 0.068, 0.056, 0.052, 20, leather))
    parts.append(add_torus_ring("EmblemRing", (0, 0, -0.025), 0.024, 0.0045, 18, 8, chrome, axis="Z"))
    parts.append(add_cyl("HubColumnNeck", (0, 0, 0.042), 0.038, 0.044, 0.045, 16, dark_trim))

    return join_parts(parts, "OW_SteeringWheel")


# ---------------------------------------------------------------------------
# 2. OW_PlaneTree.fbx — High-Detail Istanbul Cinar w/ 112 Botanical Alpha Leaf Cards
# ---------------------------------------------------------------------------
def build_ow_plane_tree():
    bark = make_mat("BarkTree", (0.34, 0.25, 0.18), roughness=0.90)
    leaf_a = make_mat("FoliagePlaneA", (0.94, 0.98, 0.92), roughness=0.72)
    leaf_b = make_mat("FoliagePlaneB", (0.86, 0.94, 0.84), roughness=0.75)

    parts = []
    rng = random.Random(701)
    trunk_pts = [(0, 0, -0.15), (0.04, -0.02, 0.55), (0.07, 0.04, 1.40), (0.02, 0.06, 2.30), (-0.03, 0.02, 3.25)]
    parts.append(add_tube("Trunk", trunk_pts, [0.38, 0.28, 0.22, 0.17, 0.11], 10, bark))
    for i in range(5):
        ang = (2.0 * math.pi * i) / 5.0 + 0.20
        ca, sa = math.cos(ang), math.sin(ang)
        parts.append(add_tube(f"Root_{i}", [(ca * 0.14, sa * 0.14, 0.65), (ca * 0.34, sa * 0.34, 0.18), (ca * 0.55, sa * 0.55, -0.08)], [0.13, 0.09, 0.05], 6, bark))

    nodes = [
        (Vector((0.0, 0.0, 4.65)), 1.65, "A"),
        (Vector((0.18, -0.14, 5.05)), 1.48, "A"),
        (Vector((-0.20, 0.18, 4.45)), 1.52, "B"),
        (Vector((0.68, 0.42, 4.68)), 1.38, "A"),
        (Vector((-0.68, -0.38, 4.62)), 1.38, "B"),
    ]
    boughs = [(0.12, 2.15, 1.68, 3.80), (1.18, 2.35, 1.62, 4.02), (2.22, 2.08, 1.56, 3.65),
              (3.28, 2.40, 1.65, 4.08), (4.32, 2.02, 1.52, 3.62), (5.38, 2.28, 1.58, 3.92)]
    for idx, (ang, sz, reach, tz) in enumerate(boughs):
        ca, sa = math.cos(ang), math.sin(ang)
        p0 = Vector((0, 0, sz))
        p1 = Vector((ca * reach * 0.52, sa * reach * 0.52, (sz + tz) * 0.52))
        p2 = Vector((ca * reach, sa * reach, tz))
        parts.append(add_tube(f"Bough_{idx}", [p0, p1, p2], [0.13, 0.085, 0.04], 7, bark))
        nodes.append((p2 + Vector((0, 0, 0.26)), 1.50, "A" if idx % 2 == 0 else "B"))
        nodes.append((p1 + Vector((ca * 0.24, sa * 0.24, 0.58)), 1.35, "B"))
        a2 = ang + 0.52
        p3 = p1 + Vector((math.cos(a2) * 0.94, math.sin(a2) * 0.94, 0.96))
        parts.append(add_tube(f"Twig_{idx}", [p1, p3], [0.068, 0.028], 5, bark))
        nodes.append((p3 + Vector((0, 0, 0.22)), 1.38, "A" if idx % 2 == 1 else "B"))

    cards_a, cards_b = [], []
    crown_c = Vector((0, 0, 4.25))
    for ni, (np_, sc, mk) in enumerate(nodes):
        dst = cards_a if mk == "A" else cards_b
        for k in range(5):
            az = (2.0 * math.pi * k) / 5.0 + rng.uniform(-0.35, 0.35) + ni * 0.55
            pos = np_ + Vector((math.cos(az) * 0.44 * sc, math.sin(az) * 0.44 * sc, rng.uniform(-0.26, 0.36) * sc))
            out_d = pos - crown_c
            yaw = math.atan2(out_d.y, out_d.x) + rng.uniform(-0.65, 0.65)
            pitch = rng.uniform(0.32, 1.15) if k < 3 else rng.uniform(-0.20, 0.38)
            roll = rng.uniform(-0.55, 0.55)
            dst.append((pos, sc * rng.uniform(0.95, 1.25), sc * rng.uniform(0.90, 1.20), (pitch, roll, yaw), rng.uniform(0.28, 0.46)))

    parts.append(add_foliage_cards("PlaneLeavesA", cards_a, leaf_a))
    parts.append(add_foliage_cards("PlaneLeavesB", cards_b, leaf_b))
    return join_parts(parts, "OW_PlaneTree")


# ---------------------------------------------------------------------------
# 3. OW_CypressTree.fbx — Tall Sculpted Istanbul Selvi w/ 72 Botanical Frond Cards
# ---------------------------------------------------------------------------
def build_ow_cypress_tree():
    bark = make_mat("BarkTree", (0.30, 0.22, 0.15), roughness=0.92)
    cyp_mat = make_mat("FoliageCypress", (0.94, 0.98, 0.94), roughness=0.76)

    parts = []
    rng = random.Random(702)
    parts.append(add_tube("CypressTrunk", [(0, 0, -0.15), (0.02, 0, 1.4), (-0.01, 0.02, 3.6), (0, 0, 5.8)], [0.22, 0.15, 0.09, 0.03], 8, bark))

    cards = []
    # 9 vertical tiers from z=1.15m to z=6.25m forming the classic tapered flame-shaped Istanbul Selvi!
    tiers = [
        (1.35, 0.58, 1.18, 1.45, 9),
        (1.95, 0.62, 1.22, 1.50, 9),
        (2.60, 0.58, 1.16, 1.46, 9),
        (3.25, 0.52, 1.08, 1.40, 8),
        (3.90, 0.44, 0.98, 1.34, 8),
        (4.52, 0.35, 0.88, 1.25, 8),
        (5.12, 0.25, 0.76, 1.15, 7),
        (5.68, 0.14, 0.62, 1.02, 6),
        (6.18, 0.04, 0.48, 0.88, 5),
    ]
    for ti, (tz, rad, cw, ch, cnt) in enumerate(tiers):
        for k in range(cnt):
            ang = (2.0 * math.pi * k) / cnt + ti * 0.42 + rng.uniform(-0.18, 0.18)
            pos = Vector((math.cos(ang) * rad, math.sin(ang) * rad, tz + rng.uniform(-0.12, 0.12)))
            # Vertical-ish feathery frond card tilted slightly inward toward spire apex
            yaw = ang + math.pi * 0.5 + rng.uniform(-0.25, 0.25)
            pitch = math.radians(78 + rng.uniform(-12, 10))
            roll = rng.uniform(-0.22, 0.22)
            cards.append((pos, cw * rng.uniform(0.90, 1.12), ch * rng.uniform(0.92, 1.10), (pitch, roll, yaw), 0.26))
    parts.append(add_foliage_cards("CypressFronds", cards, cyp_mat))
    return join_parts(parts, "OW_CypressTree")


# ---------------------------------------------------------------------------
# 4. OW_AydosPineTree.fbx — Authentic Aydos Kizilcam / Mediterranean Umbrella Pine
# ---------------------------------------------------------------------------
def build_ow_aydos_pine_tree():
    bark = make_mat("BarkTree", (0.38, 0.24, 0.16), roughness=0.88)
    pine_mat = make_mat("FoliagePine", (0.94, 0.98, 0.92), roughness=0.74)

    parts = []
    rng = random.Random(703)
    # Tall gently swaying reddish-brown Mediterranean pine trunk (Z = -0.15..5.6m)
    trunk_pts = [
        (0.0, 0.0, -0.15),
        (0.06, -0.04, 1.10),
        (0.12, 0.05, 2.50),
        (0.05, 0.10, 3.90),
        (-0.04, 0.04, 5.20),
        (0.0, 0.0, 6.20),
    ]
    parts.append(add_tube("PineTrunk", trunk_pts, [0.32, 0.25, 0.21, 0.17, 0.12, 0.06], 10, bark))
    for i in range(4):
        ang = (2.0 * math.pi * i) / 4.0 + 0.4
        parts.append(add_tube(f"PineRoot_{i}", [(math.cos(ang)*0.12, math.sin(ang)*0.12, 0.45), (math.cos(ang)*0.42, math.sin(ang)*0.42, -0.08)], [0.11, 0.05], 6, bark))

    # Spreading high umbrella pine boughs (classic Aydos Forest / Istanbul Fıstık Çamı silhouette!)
    nodes = [
        (Vector((0.0, 0.0, 6.65)), 1.65),
        (Vector((0.35, -0.25, 6.95)), 1.40),
        (Vector((-0.38, 0.28, 6.82)), 1.42),
    ]
    bough_tiers = [
        # Lower umbrella ring (z = 3.8..5.1)
        (0.0,  3.75, 2.10, 4.95),
        (1.05, 3.90, 2.05, 5.15),
        (2.10, 3.70, 2.15, 4.88),
        (3.15, 3.95, 1.98, 5.20),
        (4.20, 3.80, 2.08, 4.98),
        (5.25, 3.88, 2.00, 5.10),
        # Upper umbrella crown ring (z = 4.9..6.3)
        (0.55, 4.85, 1.55, 6.15),
        (1.80, 4.95, 1.48, 6.25),
        (3.05, 4.80, 1.52, 6.10),
        (4.30, 4.92, 1.45, 6.22),
        (5.55, 4.88, 1.50, 6.18),
    ]
    for bi, (ang, sz, reach, tz) in enumerate(bough_tiers):
        ca, sa = math.cos(ang), math.sin(ang)
        p0 = Vector((0, 0, sz))
        p1 = Vector((ca * reach * 0.56, sa * reach * 0.56, (sz + tz) * 0.50))
        p2 = Vector((ca * reach, sa * reach, tz))
        parts.append(add_tube(f"PineBough_{bi}", [p0, p1, p2], [0.10, 0.065, 0.032], 6, bark))
        nodes.append((p2 + Vector((0, 0, 0.18)), 1.48 if bi < 6 else 1.36))
        nodes.append((p1 + Vector((ca * 0.25, sa * 0.25, 0.42)), 1.24))

    cards = []
    crown_c = Vector((0, 0, 5.75))
    for ni, (np_, sc) in enumerate(nodes):
        for k in range(4):
            az = (2.0 * math.pi * k) / 4.0 + ni * 0.72 + rng.uniform(-0.3, 0.3)
            pos = np_ + Vector((math.cos(az) * 0.42 * sc, math.sin(az) * 0.42 * sc, rng.uniform(-0.18, 0.26) * sc))
            out_d = pos - crown_c
            yaw = math.atan2(out_d.y, out_d.x) + rng.uniform(-0.6, 0.6)
            # Flatter umbrella-like pine needle sprays layered horizontally & gently pitched!
            pitch = rng.uniform(0.10, 0.65) if k < 2 else rng.uniform(0.65, 1.15)
            roll = rng.uniform(-0.40, 0.40)
            cards.append((pos, sc * rng.uniform(0.95, 1.26), sc * rng.uniform(0.90, 1.18), (pitch, roll, yaw), 0.34))

    parts.append(add_foliage_cards("PineNeedles", cards, pine_mat))
    return join_parts(parts, "OW_AydosPineTree")


# ---------------------------------------------------------------------------
# 5. OW_AydosCastle.fbx — Historic Byzantine/Ottoman Aydos Kalesi Fortress
#    Matches courtyard floor at Z_blender=3.05 (X=-11..+11, Y=-10..+10), open West Gate at X=-10.5, Y=0!
# ---------------------------------------------------------------------------
def build_ow_aydos_castle():
    stone_wall = make_mat("CastleStoneWall", (0.64, 0.59, 0.50), roughness=0.88)
    stone_dark = make_mat("CastleStoneDark", (0.44, 0.40, 0.35), roughness=0.90)
    slate_roof = make_mat("CastleSlateRoof", (0.34, 0.22, 0.18), roughness=0.76)
    wood_oak = make_mat("CastleWoodOak", (0.40, 0.25, 0.14), roughness=0.78)
    iron_dark = make_mat("CastleIronDark", (0.14, 0.15, 0.16), roughness=0.45, metallic=0.78)

    parts = []
    cz = 3.05  # Courtyard elevation (matches SultanbeyliDistrictsBuilder.cs courtyard slab!)

    def add_watchtower(prefix, tx, ty, radius, height, segs=18, conical_roof=False, open_top_flag=False):
        # Battered stone fortress base skirt plunging down into hill terrace (Z=1.4..cz+1.4)
        parts.append(add_cyl(f"{prefix}_BatteredBase", (tx, ty, cz + 0.15), radius * 1.16, radius * 1.02, 2.75, segs, stone_dark))
        # Main cylindrical / octagonal ashlar watchtower shaft
        parts.append(add_cyl(f"{prefix}_Shaft", (tx, ty, cz + height * 0.5), radius, radius * 0.96, height, segs, stone_wall))
        # Mid horizontal stone string-course ring + recessed arrow-slit embrasures
        parts.append(add_cyl(f"{prefix}_MidBand", (tx, ty, cz + height * 0.52), radius * 1.04, radius * 1.04, 0.24, segs, stone_dark))
        for si in range(6):
            ang = (2.0 * math.pi * si) / 6.0
            sx = tx + math.cos(ang) * (radius * 0.99)
            sy = ty + math.sin(ang) * (radius * 0.99)
            parts.append(add_box(f"{prefix}_Slit_{si}", (sx, sy, cz + height * 0.72), (0.14, 0.24, 1.05), iron_dark, bevel=0.01, rot_euler=(0, 0, ang)))
            parts.append(add_box(f"{prefix}_SlitCross_{si}", (sx, sy, cz + height * 0.76), (0.14, 0.56, 0.14), iron_dark, bevel=0.01, rot_euler=(0, 0, ang)))

        # Stepped stone machicolation corbels + overhanging parapet ring!
        top_z = cz + height
        parts.append(add_cyl(f"{prefix}_MachRing", (tx, ty, top_z - 0.18), radius * 0.98, radius * 1.15, 0.45, segs, stone_dark))
        parts.append(add_cyl(f"{prefix}_Parapet", (tx, ty, top_z + 0.35), radius * 1.16, radius * 1.16, 0.65, segs, stone_wall))
        n_corb = 12
        for ci in range(n_corb):
            ang = (2.0 * math.pi * ci) / n_corb
            cx_ = tx + math.cos(ang) * (radius * 1.04)
            cy_ = ty + math.sin(ang) * (radius * 1.04)
            parts.append(add_box(f"{prefix}_Corb_{ci}", (cx_, cy_, top_z - 0.46), (0.34, 0.26, 0.48), stone_dark, bevel=0.02, rot_euler=(0, 0, ang)))
            # Crenellated stone merlons around tower crown!
            mx_ = tx + math.cos(ang) * (radius * 1.09)
            my_ = ty + math.sin(ang) * (radius * 1.09)
            parts.append(add_box(f"{prefix}_Merlon_{ci}", (mx_, my_, top_z + 0.96), (0.42, 0.56, 0.68), stone_wall, bevel=0.03, rot_euler=(0, 0, ang)))
            parts.append(add_box(f"{prefix}_MCap_{ci}", (mx_, my_, top_z + 1.32), (0.48, 0.62, 0.10), stone_dark, bevel=0.02, rot_euler=(0, 0, ang)))
        if conical_roof:
            parts.append(add_cyl(f"{prefix}_RoofCone", (tx, ty, top_z + 2.15), radius * 1.02, 0.08, 3.25, segs, slate_roof))
            parts.append(add_cyl(f"{prefix}_RoofSpire", (tx, ty, top_z + 3.95), 0.06, 0.02, 0.85, 8, iron_dark))

    # 1. Four Iconic Corner Stone Burçlar (Watchtowers):
    #    NE Main Keep (+10.5, +9.5) flat battlemented deck at Z=11.4m for Turkish flagpole!
    #    SE Octagonal Bastion (+10.5, -9.5) w/ conical Byzantine slate roof!
    #    NW & SW Gatehouse flanking towers (-10.5, +/-9.5)
    add_watchtower("Tower_NE_Keep",      10.5,  9.5, 2.65, 8.35, segs=20, conical_roof=False, open_top_flag=True)
    add_watchtower("Tower_SE_Bastion",   10.5, -9.5, 2.45, 7.60, segs=8,  conical_roof=True)
    add_watchtower("Tower_NW_Gate",     -10.5,  9.5, 2.20, 6.80, segs=18, conical_roof=False)
    add_watchtower("Tower_SW_Gate",     -10.5, -9.5, 2.20, 6.40, segs=18, conical_roof=False)

    # 2. Thick Buttressed Ashlar Curtain Walls (North, South, East + West flanking walls)
    wall_h = 4.45
    wall_z = cz + wall_h * 0.5
    parts.append(add_box("Wall_North", (0.0,  9.5, wall_z), (20.4, 1.55, wall_h), stone_wall, bevel=0.04))
    parts.append(add_box("Wall_South", (0.0, -9.5, wall_z), (20.4, 1.55, wall_h), stone_wall, bevel=0.04))
    parts.append(add_box("Wall_East",  (10.5, 0.0, wall_z), (1.55, 18.4, wall_h), stone_wall, bevel=0.04))
    # West wall split around wide open 5.4m Gatehouse Portal (Y = -2.7..+2.7 open!)
    parts.append(add_box("Wall_WestN", (-10.5,  6.1, wall_z), (1.55, 6.8, wall_h), stone_wall, bevel=0.04))
    parts.append(add_box("Wall_WestS", (-10.5, -6.1, wall_z), (1.55, 6.8, wall_h), stone_wall, bevel=0.04))

    # External battered stone buttresses + continuous crenellated battlements along all curtain walls!
    for bx in (-6.0, -2.0, 2.0, 6.0):
        for sy in (-1.0, 1.0):
            parts.append(add_box(f"ButNS_{bx}_{sy}", (bx, sy * 10.45, wall_z - 0.45), (0.85, 0.80, wall_h - 0.6), stone_dark, bevel=0.03))
            parts.append(add_box(f"MerNS_{bx}_{sy}", (bx, sy * 9.85, cz + wall_h + 0.52), (1.45, 0.62, 1.05), stone_wall, bevel=0.03))
            parts.append(add_box(f"MerCapNS_{bx}_{sy}", (bx, sy * 9.85, cz + wall_h + 1.08), (1.58, 0.72, 0.12), stone_dark, bevel=0.02))
    for by in (-5.5, -1.8, 1.8, 5.5):
        parts.append(add_box(f"ButE_{by}", (11.45, by, wall_z - 0.45), (0.80, 0.85, wall_h - 0.6), stone_dark, bevel=0.03))
        parts.append(add_box(f"MerE_{by}", (10.85, by, cz + wall_h + 0.52), (0.62, 1.45, 1.05), stone_wall, bevel=0.03))
        parts.append(add_box(f"MerCapE_{by}", (10.85, by, cz + wall_h + 1.08), (0.72, 1.58, 0.12), stone_dark, bevel=0.02))

    # 3. MONUMENTAL WEST ARCHED STONE GATEHOUSE (Kale Kapısı) w/ Voussoir Arch, Raised Iron Portcullis & Open Timber Doors!
    for gy in (-2.95, 2.95):
        parts.append(add_box(f"GatePier_{gy}", (-10.5, gy, cz + 2.75), (2.25, 1.15, 5.50), stone_dark, bevel=0.04))
    # Tall battlemented gatehouse bridge above the arched passage (clear height 4.1m!)
    parts.append(add_box("GateHouse_Top", (-10.5, 0.0, cz + 5.15), (2.15, 7.20, 1.85), stone_wall, bevel=0.04))
    for gm in (-2.4, -0.8, 0.8, 2.4):
        parts.append(add_box(f"GateMer_{gm}", (-11.15, gm, cz + 6.45), (0.65, 1.05, 0.85), stone_dark, bevel=0.03))
    # Curved stone voussoir arch stones spanning Y = -2.4..+2.4 at Z = cz + 3.0..4.35
    for vi in range(11):
        ang = math.radians(10 + vi * 16)
        vy = math.cos(ang) * 2.45
        vz = cz + 2.50 + math.sin(ang) * 1.85
        parts.append(add_box(f"Voussoir_{vi}", (-10.5, vy, vz), (2.28, 0.42, 0.55), stone_dark if vi % 2 == 0 else stone_wall, bevel=0.025, rot_euler=(ang - math.pi * 0.5, 0, 0)))
    # Raised iron portcullis grille tucked at arch top (Z = cz + 3.65..4.45 — player walks right under!)
    parts.append(add_box("Portcullis_Bar", (-10.5, 0.0, cz + 3.95), (0.14, 4.70, 0.12), iron_dark))
    for py in (-2.0, -1.4, -0.8, -0.2, 0.4, 1.0, 1.6, 2.0):
        parts.append(add_cyl(f"Portcullis_Spike_{py}", (-10.5, py, cz + 3.82), 0.045, 0.012, 0.85, 6, iron_dark))
    # Heavy iron-studded wooden castle doors swung wide open inside the gateway!
    for sy in (-1.0, 1.0):
        parts.append(add_box(f"GateDoor_{sy}", (-8.65, sy * 2.65, cz + 1.80), (2.10, 0.18, 3.55), wood_oak, bevel=0.02, rot_euler=(0, 0, -sy * 0.16)))
        for hz in (0.85, 1.85, 2.85):
            parts.append(add_box(f"GateStrap_{sy}_{hz}", (-8.65, sy * 2.55, cz + hz), (1.95, 0.06, 0.14), iron_dark, rot_euler=(0, 0, -sy * 0.16)))

    # 4. Elevated Wooden Rampart Catwalk along North & East Walls + Byzantine Ruined Cistern Arcade along East Wall!
    parts.append(add_box("RampartWalk_N", (0.0, 8.20, cz + 3.15), (17.5, 1.35, 0.18), wood_oak, bevel=0.02))
    parts.append(add_box("RampartWalk_E", (9.20, 0.0, cz + 3.15), (1.35, 16.0, 0.18), wood_oak, bevel=0.02))
    for rx in (-6.5, -2.2, 2.2, 6.5):
        parts.append(add_box(f"RampBraceN_{rx}", (rx, 8.45, cz + 2.65), (0.24, 1.15, 0.24), wood_oak, rot_euler=(0.55, 0, 0)))
    # Historic ruined stone cistern arches tucked neatly along East inner courtyard wall (X=+8.4, keeping center 100% open!)
    for ay in (-4.4, -1.6, 1.2, 4.0):
        parts.append(add_box(f"CisternPier_{ay}", (8.55, ay, cz + 1.45), (0.72, 0.72, 2.90), stone_dark, bevel=0.03))
    parts.append(add_box("CisternLintel", (8.55, -0.2, cz + 2.88), (0.76, 9.20, 0.42), stone_wall, bevel=0.03))

    return join_parts(parts, "OW_AydosCastle")


# ---------------------------------------------------------------------------
# 6. OW_M5MetroEntrance.fbx — Authentic Istanbul M5 Metro Glass Canopy & Escalators
#    Centered at origin (ground Z=0), entrance facing +Y (Unity +Z), M Totem at (-7.4, +2.2)
# ---------------------------------------------------------------------------
def build_ow_m5_metro_entrance():
    glass = make_mat("MetroGlass", (0.28, 0.56, 0.72), roughness=0.06, metallic=0.18, alpha=0.42)
    steel = make_mat("MetroSteel", (0.76, 0.79, 0.83), roughness=0.22, metallic=0.86)
    dark_steel = make_mat("MetroDarkSteel", (0.14, 0.15, 0.17), roughness=0.38, metallic=0.65)
    metro_red = make_mat("MetroCrimson", (0.84, 0.10, 0.14), roughness=0.32, metallic=0.20)
    granite = make_mat("MetroGranite", (0.56, 0.55, 0.54), roughness=0.62)
    tactile_yellow = make_mat("MetroYellow", (0.94, 0.78, 0.10), roughness=0.52)
    led_warm = make_mat("MetroLedGlow", (1.0, 0.96, 0.86), roughness=0.15, emission=(1.0, 0.95, 0.84), emission_strength=3.0)

    parts = []
    # 1. Sleek Curved Barrel-Vault Tempered Glass Canopy (X=-5.8..+5.8, Y=-3.2..+3.2, arch Z=3.75..4.65)
    #    5 arched brushed-stainless structural truss ribs + longitudinal purlins + segmented curved glass vault!
    rib_xs = (-5.5, -2.75, 0.0, 2.75, 5.5)
    n_arc = 10
    arc_pts = []
    for ai in range(n_arc + 1):
        t = ai / float(n_arc)
        ang = math.radians(155.0 - t * 130.0)  # sweeps from -Y back to +Y front
        yy = -math.cos(ang) * 3.55
        zz = 3.05 + math.sin(ang) * 1.68
        arc_pts.append((yy, zz))

    for rx in rib_xs:
        pts3 = [(rx, yy, zz) for (yy, zz) in arc_pts]
        parts.append(add_tube(f"ArchRib_{rx}", pts3, 0.085, 8, steel))
        # Front & back sculpted tapered stainless steel support columns
        if abs(rx) > 0.5:
            parts.append(add_cyl(f"ColF_{rx}", (rx,  2.95, 1.92), 0.14, 0.11, 3.85, 14, dark_steel))
            parts.append(add_cyl(f"ColB_{rx}", (rx, -2.95, 1.92), 0.14, 0.11, 3.85, 14, dark_steel))

    # Longitudinal steel purlins + curved glass vault panes
    for ai in range(n_arc):
        y0, z0 = arc_pts[ai]
        y1, z1 = arc_pts[ai + 1]
        ym, zm = (y0 + y1) * 0.5, (z0 + z1) * 0.5
        seg_len = math.hypot(y1 - y0, z1 - z0)
        seg_pitch = math.atan2(z1 - z0, y1 - y0)
        parts.append(add_box(f"GlassVault_{ai}", (0.0, ym, zm + 0.05), (11.35, seg_len + 0.04, 0.045), glass, bevel=0.004, rot_euler=(seg_pitch, 0, 0)))
        if ai % 2 == 0:
            parts.append(add_box(f"Purlin_{ai}", (0.0, y0, z0 - 0.02), (11.4, 0.06, 0.06), steel))

    # Signature Istanbul Metro Crimson-Red Front & Rear Architectural Fascia Beam + Warm LED Strip!
    parts.append(add_box("Metro_FrontFascia", (0.0,  3.22, 3.86), (11.75, 0.24, 0.62), metro_red, bevel=0.03, segments=2))
    parts.append(add_box("Metro_RearFascia",  (0.0, -3.22, 3.86), (11.75, 0.24, 0.62), metro_red, bevel=0.03, segments=2))
    parts.append(add_box("Metro_LedStripF",   (0.0,  2.85, 3.55), (10.60, 0.14, 0.06), led_warm, bevel=0.01))
    parts.append(add_box("Metro_LedStripM",   (0.0,  0.00, 4.52), (10.60, 0.18, 0.06), led_warm, bevel=0.01))

    # 2. Granite Perimeter Curb + Tempered Glass Side Windbreak Balustrades + Rear Wind Wall
    parts.append(add_box("Metro_RearWall", (0.0, -3.02, 1.55), (11.1, 0.26, 3.10), dark_steel, bevel=0.02))
    parts.append(add_box("Metro_RearGlass", (0.0, -2.86, 1.85), (10.2, 0.08, 2.25), glass, bevel=0.01))
    for sx in (-1.0, 1.0):
        parts.append(add_box(f"SideCurb_{sx}", (sx * 5.25, -0.15, 0.22), (0.36, 5.85, 0.44), granite, bevel=0.02))
        parts.append(add_box(f"SideGlass_{sx}", (sx * 5.25, -0.15, 1.02), (0.06, 5.55, 1.18), glass, bevel=0.006))
        parts.append(add_box(f"SideRail_{sx}",  (sx * 5.25, -0.15, 1.64), (0.10, 5.65, 0.07), steel, bevel=0.01))

    # 3. Descending Granite Staircase + Twin Brushed-Steel Escalators w/ Black Moving Handrails + Yellow Tactile Strip!
    parts.append(add_box("TactileStrip", (0.0, 2.42, 0.02), (8.6, 0.42, 0.04), tactile_yellow, bevel=0.005))
    # Dark descending subway pit illusion plate + stepped descending granite stairs (right half X=+0.5..+4.2)
    parts.append(add_box("StairwellPit", (0.0, -0.45, 0.03), (8.8, 4.6, 0.06), dark_steel, bevel=0.0))
    for st in range(7):
        parts.append(add_box(f"GraniteStep_{st}", (2.25, 1.65 - st * 0.56, 0.04 - st * 0.005), (3.4, 0.48, 0.08), granite, bevel=0.01))
        parts.append(add_box(f"EscStepL_{st}",   (-2.45, 1.65 - st * 0.56, 0.05 - st * 0.005), (1.25, 0.42, 0.06), steel, bevel=0.005))
        parts.append(add_box(f"EscStepR_{st}",   (-0.85, 1.65 - st * 0.56, 0.05 - st * 0.005), (1.25, 0.42, 0.06), steel, bevel=0.005))
    # 3 Sleek Stainless Escalator Balustrades + Black Rubber Handrail Caps!
    for ex in (-3.22, -1.65, -0.08):
        parts.append(add_box(f"EscBody_{ex}", (ex, -0.25, 0.46), (0.24, 4.65, 0.88), steel, bevel=0.03))
        parts.append(add_box(f"EscGlass_{ex}", (ex, -0.25, 0.96), (0.06, 4.45, 0.46), glass, bevel=0.005))
        parts.append(add_box(f"EscHandrail_{ex}", (ex, -0.25, 1.22), (0.14, 4.72, 0.09), dark_steel, bevel=0.025))
    # Center stair handrail
    parts.append(add_box("StairHandrail", (2.25, -0.20, 0.98), (0.07, 4.5, 0.07), steel, bevel=0.01))
    for py in (-2.0, 0.0, 1.8):
        parts.append(add_cyl(f"StairPost_{py}", (2.25, py, 0.50), 0.035, 0.035, 0.96, 8, steel))

    # 4. Iconic Tall Istanbul Red-White 'M' Totem Pylon at (-7.4, +2.2)!
    tx, ty = -7.4, 2.2
    parts.append(add_box("TotemBase", (tx, ty, 0.24), (1.15, 0.95, 0.48), granite, bevel=0.03))
    parts.append(add_box("TotemMast", (tx, ty, 3.15), (0.58, 0.44, 5.45), dark_steel, bevel=0.04, segments=2))
    parts.append(add_box("TotemTrimL", (tx - 0.31, ty, 3.15), (0.06, 0.36, 5.35), steel, bevel=0.01))
    parts.append(add_box("TotemTrimR", (tx + 0.31, ty, 3.15), (0.06, 0.36, 5.35), steel, bevel=0.01))
    parts.append(add_box("TotemMBox", (tx, ty, 6.42), (1.92, 0.58, 1.92), metro_red, bevel=0.06, segments=2))
    parts.append(add_box("TotemMFrame", (tx, ty, 6.42), (2.02, 0.42, 2.02), steel, bevel=0.05, segments=2))

    return join_parts(parts, "OW_M5MetroEntrance")


# ---------------------------------------------------------------------------
# Multi-Asset Studio Contact Sheet Preview Render
# ---------------------------------------------------------------------------
def bind_preview_leaf_tex(obj, mat_prefix, png_name):
    tex_path = os.path.join(TEX_DIR, png_name)
    if not os.path.exists(tex_path):
        return
    img = bpy.data.images.load(tex_path)
    for m in obj.data.materials:
        if m and m.name.startswith(mat_prefix):
            nt = m.node_tree
            bsdf = nt.nodes.get("Principled BSDF")
            tn = nt.nodes.new("ShaderNodeTexImage")
            tn.image = img
            nt.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
            nt.links.new(tn.outputs["Alpha"], bsdf.inputs["Alpha"])


def render_step4_showcase():
    reset_scene()
    # Ground plane
    g_mat = make_mat("Ground", (0.45, 0.50, 0.44), roughness=0.92)
    add_box("Ground", (0, 0, -0.25), (120, 120, 0.5), g_mat, bevel=0.0)

    # 1. Aydos Castle (back-left)
    castle = build_ow_aydos_castle()
    castle.location = Vector((-16.0, 14.0, -3.05))
    castle.rotation_euler = Euler((0, 0, math.radians(-22)), "XYZ")

    # 2. M5 Metro Entrance (front-right)
    metro = build_ow_m5_metro_entrance()
    metro.location = Vector((12.0, -2.0, 0.0))
    metro.rotation_euler = Euler((0, 0, math.radians(-158)), "XYZ")

    # 3. All 3 Botanical Alpha-Cutout Trees (center foreground!)
    t_plane = build_ow_plane_tree()
    t_plane.location = Vector((-4.8, -5.5, 0.0))
    bind_preview_leaf_tex(t_plane, "FoliagePlane", "foliage_plane_albedo.png")

    t_pine = build_ow_aydos_pine_tree()
    t_pine.location = Vector((0.6, -5.2, 0.0))
    bind_preview_leaf_tex(t_pine, "FoliagePine", "foliage_pine_albedo.png")

    t_cyp = build_ow_cypress_tree()
    t_cyp.location = Vector((4.6, -6.2, 0.0))
    bind_preview_leaf_tex(t_cyp, "FoliageCypress", "foliage_cypress_albedo.png")

    # 4. Imported Hz. Osman Camii exterior beauty check (back-right)
    mosque_fbx = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender", "Sultanbeyli_HzOsmanCamii.fbx")
    if os.path.exists(mosque_fbx):
        bpy.ops.import_scene.fbx(filepath=mosque_fbx)
        m_obj = bpy.data.objects.get("Sultanbeyli_HzOsmanCamii")
        if m_obj:
            m_obj.location = Vector((16.0, 18.0, 0.0))
            m_obj.rotation_euler = Euler((0, 0, math.radians(-155)), "XYZ")

    # Sun + Sky
    world = bpy.data.worlds.new("Step4Sky")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.52, 0.70, 0.90, 1.0)
    bg.inputs["Strength"].default_value = 0.95

    sun = bpy.data.lights.new("Sun", type="SUN")
    sun.energy = 3.2
    sun.color = (1.0, 0.95, 0.85)
    sun_obj = bpy.data.objects.new("Sun", sun)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = Euler((math.radians(48), math.radians(12), math.radians(-35)), "XYZ")

    cam_d = bpy.data.cameras.new("Cam")
    cam_d.lens = 35.0
    cam_o = bpy.data.objects.new("Cam", cam_d)
    bpy.context.collection.objects.link(cam_o)
    cam_o.location = Vector((0.0, -28.0, 11.5))
    cam_o.rotation_euler = (Vector((0.0, 4.0, 4.2)) - cam_o.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_o

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1440
    sc.render.resolution_y = 810
    sc.render.filepath = PREVIEW_PATH
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "GRIDROAM_Step4_Showcase.blend"))
    bpy.ops.render.render(write_still=True)
    print(f"Saved Step 4 Showcase render: {PREVIEW_PATH}")


def main():
    jobs = [
        ("OW_SteeringWheel.fbx",   build_steering_wheel),
        ("OW_PlaneTree.fbx",       build_ow_plane_tree),
        ("OW_CypressTree.fbx",     build_ow_cypress_tree),
        ("OW_AydosPineTree.fbx",   build_ow_aydos_pine_tree),
        ("OW_AydosCastle.fbx",     build_ow_aydos_castle),
        ("OW_M5MetroEntrance.fbx", build_ow_m5_metro_entrance),
    ]
    print(f"[GRIDROAM Step 4] Exporting {len(jobs)} custom Blender 5.3.0 models to {OUT_LIFE_DIR} ...")
    for fname, fn in jobs:
        reset_scene()
        obj = fn()
        export_life_fbx(obj, fname)
    render_step4_showcase()
    print("[GRIDROAM Step 4] All exports complete!")


if __name__ == "__main__":
    main()
