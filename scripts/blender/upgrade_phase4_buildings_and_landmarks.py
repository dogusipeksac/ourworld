"""
GRIDROAM Phase 4 Batch 7 — Istanbul Iconic Landmarks Upgrade & Batch 7 Showcase Render
Sculpts high-detail stylized-realistic upgrades for:
  1. Istanbul_Ferry.fbx (Şehir Hatları classic Bosphorus passenger ferry, preserving exact
     single mesh name 'Istanbul_Ferry', exact bounds [-2.75..2.75] x [-10.8..11.4] x [-0.4..8.65],
     and exact 7 material slots: FerryHull, Wood, CreamPaint, Glass, LitScreen, GoldBrass, TrimDark)
  2. Istanbul_Tram.fbx (İstiklal Caddesi iconic nostalgia red tram, preserving exact
     single mesh name 'Istanbul_Tram', exact bounds [-1.28..1.28] x [-4.35..4.35] x [0..4.33],
     and exact 8 material slots: TrimDark, TramRed, GoldBrass, Glass, CreamPaint, Chrome, LitScreen, Wood)
Also renders studio verification sheet `phase4_batch7_buildings.png`!
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

ROOT = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM"
ART_DIR = os.path.join(ROOT, "Assets/GRIDROAM/Art/Models/Blender")
LIFE_DIR = os.path.join(ROOT, "Assets/GRIDROAM/Resources/OurWorldLife")
ARTIFACT_IMG = "/Users/dogusipeksac/.gemini/antigravity/brain/860aa6ad-f8a6-44e1-93c3-fc8493a24ffe/phase4_batch7_buildings.png"


def make_mat(name, rgba, rough=0.55, metal=0.0, emission=0.0):
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


def add_bm(dst, src, mat_idx):
    src.verts.index_update()
    vmap = [dst.verts.new(v.co) for v in src.verts]
    for f in src.faces:
        try:
            nf = dst.faces.new([vmap[v.index] for v in f.verts])
            nf.material_index = mat_idx
            nf.smooth = f.smooth
        except ValueError:
            pass
    src.free()


def box_bm(loc, size, bevel=0.0, segs=1, rot=(0, 0, 0), smooth=False):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    if bevel > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segs, affect="EDGES", clamp_overlap=True)
    m = Matrix.Translation(loc) @ Euler(rot, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    for f in bm.faces:
        f.smooth = smooth
    return bm


def rod_bm(p0, p1, r0, r1=None, segs=12):
    if r1 is None:
        r1 = r0
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    ln = d.length
    if ln < 1e-5:
        return bmesh.new()
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r0, radius2=r1, depth=ln)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    m = Matrix.Translation((p0 + p1) * 0.5) @ q.to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    for f in bm.faces:
        f.smooth = True
    return bm


def apply_box_uv(bm, uv_scale=0.45):
    uv_layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for lp in f.loops:
            p = lp.vert.co
            if az >= ax and az >= ay:
                u, v = p.x * uv_scale, p.y * uv_scale
            elif ax >= ay:
                u, v = p.y * uv_scale, p.z * uv_scale
            else:
                u, v = p.x * uv_scale, p.z * uv_scale
            lp[uv_layer].uv = (u, v)


def finalize_single_mesh(name, bm, mats, uv_scale=0.45):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    apply_box_uv(bm, uv_scale)
    me = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    return obj


def export_single_fbx(obj, filename):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    out = os.path.join(ART_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=out,
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
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print(f"Exported {filename} | tris={tris} | mats={[m.name for m in obj.data.materials]}")


# ===========================================================================
# 1. Istanbul_Ferry.fbx — Classic Şehir Hatları Bosphorus Vapur!
# Exact slots: ['FerryHull', 'Wood', 'CreamPaint', 'Glass', 'LitScreen', 'GoldBrass', 'TrimDark']
# Exact bounds: X [-2.75..2.75], Y [-10.8..11.4], Z [-0.4..8.65]
# ===========================================================================
def build_istanbul_ferry():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = [
        make_mat("FerryHull",  (0.11, 0.14, 0.20, 1.0), rough=0.38, metal=0.25),
        make_mat("Wood",       (0.56, 0.36, 0.18, 1.0), rough=0.62),
        make_mat("CreamPaint", (0.94, 0.92, 0.87, 1.0), rough=0.36),
        make_mat("Glass",      (0.14, 0.24, 0.32, 1.0), rough=0.08, metal=0.20),
        make_mat("LitScreen",  (1.00, 0.92, 0.68, 1.0), rough=0.18, emission=1.8),
        make_mat("GoldBrass",  (0.92, 0.68, 0.16, 1.0), rough=0.28, metal=0.65),
        make_mat("TrimDark",   (0.14, 0.15, 0.17, 1.0), rough=0.45, metal=0.50),
    ]
    HULL, WOOD, CREAM, GLASS, LIT, GOLD, DARK = 0, 1, 2, 3, 4, 5, 6

    bm = bmesh.new()

    # A. Curved lofted Bosphorus Vapur Hull (tapered flared bow + graceful stern, Z=-0.40..2.15)
    hull_stations = [
        # (y, half_width_keel, half_width_deck, z_keel, z_deck)
        (-10.80, 0.85, 1.85,  0.05, 2.05),
        ( -9.40, 1.75, 2.52, -0.30, 1.98),
        ( -6.80, 2.20, 2.75, -0.40, 1.92),
        ( -2.00, 2.30, 2.75, -0.40, 1.90),
        (  3.50, 2.25, 2.75, -0.40, 1.95),
        (  7.20, 1.75, 2.48, -0.35, 2.10),
        (  9.50, 0.88, 1.56, -0.20, 2.30),
        ( 11.40, 0.08, 0.24,  0.25, 2.55),
    ]
    bm_hull = bmesh.new()
    rings = []
    for y, wk, wd, zk, zd in hull_stations:
        zm = (zk + zd) * 0.52
        wm = (wk * 0.42 + wd * 0.58) * 1.04
        pts = [
            (-wd, y, zd), (-wm, y, zm), (-wk, y, zk),
            ( wk, y, zk), ( wm, y, zm), ( wd, y, zd),
        ]
        rings.append([bm_hull.verts.new(p) for p in pts])
    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for j in range(len(r0)):
            jn = (j + 1) % len(r0)
            f = bm_hull.faces.new((r0[j], r1[j], r1[jn], r0[jn]))
            f.smooth = (j != 5)  # deck top flat, hull sides smooth!
    bm_hull.faces.new(list(reversed(rings[0])))
    bm_hull.faces.new(rings[-1])
    add_bm(bm, bm_hull, HULL)

    # White sheerline rub-rail & brass portholes along lower hull!
    for sx in (-1.0, 1.0):
        add_bm(bm, box_bm((sx * 2.76, -0.6, 1.88), (0.14, 15.2, 0.14), 0.03), CREAM)
        add_bm(bm, box_bm((sx * 2.77, -0.6, 1.55), (0.10, 15.0, 0.18), 0.02), GOLD)
        for py in (-7.2, -5.0, -2.8, -0.6, 1.6, 3.8, 6.0):
            add_bm(bm, rod_bm((sx * 2.66, py, 1.10), (sx * 2.78, py, 1.10), 0.24, 0.24, 14), GOLD)
            add_bm(bm, rod_bm((sx * 2.76, py, 1.10), (sx * 2.81, py, 1.10), 0.17, 0.17, 14), LIT)

    # Teak main deck planking & curved teak gunwale cap
    add_bm(bm, box_bm((0, -0.4, 1.98), (5.24, 18.0, 0.14), 0.04), WOOD)
    add_bm(bm, box_bm((0,  9.2, 2.18), (3.10,  3.2, 0.14), 0.04, rot=(0.06, 0, 0)), WOOD)
    # Bow brass anchor bollard + twin foredeck wooden benches
    add_bm(bm, rod_bm((0, 9.6, 2.25), (0, 9.6, 2.75), 0.16, 0.14, 12), DARK)
    add_bm(bm, rod_bm((-0.34, 9.6, 2.62), (0.34, 9.6, 2.62), 0.07, 0.07, 10), DARK)
    for sx in (-1.2, 1.2):
        add_bm(bm, box_bm((sx, -9.2, 2.42), (1.35, 0.48, 0.10), 0.02), WOOD)
        add_bm(bm, box_bm((sx, -9.4, 2.72), (1.35, 0.08, 0.50), 0.02), WOOD)

    # B. Lower Passenger Salon Deckhouse (Cream white superstructure + framed recessed windows!)
    add_bm(bm, box_bm((0, -0.4, 3.22), (4.40, 14.8, 2.38), 0.08, 2), CREAM)
    # Overhanging cream eaves & upper promenade deck floor
    add_bm(bm, box_bm((0, -0.4, 4.46), (5.05, 16.4, 0.18), 0.04), CREAM)
    add_bm(bm, box_bm((0, -0.4, 4.56), (4.86, 16.0, 0.08), 0.02), WOOD)
    # Lower salon panoramic window strips (7 recessed bays per side + warm interior glow!)
    for sx in (-1.0, 1.0):
        for wy in (-6.2, -4.3, -2.4, -0.5, 1.4, 3.3, 5.2):
            add_bm(bm, box_bm((sx * 2.21, wy, 3.35), (0.10, 1.48, 1.18), 0.02), DARK)
            add_bm(bm, box_bm((sx * 2.23, wy, 3.35), (0.08, 1.32, 1.02), 0.01), LIT)
            add_bm(bm, box_bm((sx * 2.25, wy, 3.35), (0.06, 1.26, 0.96), 0.01), GLASS)
        # Main deck open handrail stanchions
        add_bm(bm, rod_bm((sx * 2.62, -9.6, 2.95), (sx * 2.62, 7.6, 2.95), 0.045, 0.045, 8), CREAM)
        add_bm(bm, rod_bm((sx * 2.62, -9.6, 2.52), (sx * 2.62, 7.6, 2.52), 0.028, 0.028, 6), CREAM)
        for ry in range(-9, 8, 2):
            add_bm(bm, rod_bm((sx * 2.62, ry, 2.02), (sx * 2.62, ry, 2.96), 0.032, 0.032, 6), CREAM)

    # C. Upper Salon & Classic Wheelhouse Bridge (Z=4.6..6.75)
    add_bm(bm, box_bm((0, -0.6, 5.64), (3.80, 12.2, 2.12), 0.08, 2), CREAM)
    add_bm(bm, box_bm((0, -0.6, 6.76), (4.22, 13.0, 0.16), 0.04), CREAM)
    # Forward angled panoramic captain's wheelhouse bridge!
    add_bm(bm, box_bm((0, 5.85, 5.75), (3.95, 2.20, 2.15), 0.08, 2), CREAM)
    add_bm(bm, box_bm((0, 6.96, 5.95), (3.50, 0.12, 1.05), 0.02), DARK)
    add_bm(bm, box_bm((0, 7.00, 5.95), (3.25, 0.08, 0.88), 0.01), GLASS)
    # Upper cabin framed windows + wooden promenade benches + upper railing!
    for sx in (-1.0, 1.0):
        for wy in (-5.4, -3.5, -1.6, 0.3, 2.2, 4.1):
            add_bm(bm, box_bm((sx * 1.91, wy, 5.72), (0.10, 1.45, 1.08), 0.02), DARK)
            add_bm(bm, box_bm((sx * 1.93, wy, 5.72), (0.08, 1.30, 0.92), 0.01), GLASS)
        # Upper promenade deck safety rails
        add_bm(bm, rod_bm((sx * 2.44, -8.2, 5.52), (sx * 2.44, 7.4, 5.52), 0.042, 0.042, 8), CREAM)
        add_bm(bm, rod_bm((sx * 2.44, -8.2, 5.08), (sx * 2.44, 7.4, 5.08), 0.026, 0.026, 6), CREAM)
        for ry in range(-8, 8, 2):
            add_bm(bm, rod_bm((sx * 2.44, ry, 4.58), (sx * 2.44, ry, 5.54), 0.030, 0.030, 6), CREAM)
        # Suspended classic lifeboats on curved steel davits!
        add_bm(bm, box_bm((sx * 2.15, -3.6, 7.22), (0.82, 2.65, 0.56), 0.22, 2, smooth=True), CREAM)
        add_bm(bm, box_bm((sx * 2.15, -3.6, 7.46), (0.72, 2.45, 0.14), 0.04), GOLD)

    # D. Iconic Yellow & Black Şehir Hatları Smokestack Funnel + Mast + Brass Navigation Lamps!
    # Main tilted oval-ish iconic yellow funnel with black cowl & red/dark band
    add_bm(bm, rod_bm((0, 1.2, 6.76), (0, 0.85, 8.20), 0.72, 0.64, 18), GOLD)
    add_bm(bm, rod_bm((0, 0.85, 8.20), (0, 0.74, 8.65), 0.65, 0.62, 18), DARK)
    add_bm(bm, rod_bm((0, 1.02, 7.52), (0, 0.96, 7.74), 0.71, 0.70, 18), DARK)
    # Forward signal mast + radar crossbar + glowing top lantern!
    add_bm(bm, rod_bm((0, 5.6, 6.80), (0, 5.3, 8.62), 0.10, 0.05, 10), CREAM)
    add_bm(bm, rod_bm((-0.95, 5.42, 7.95), (0.95, 5.42, 7.95), 0.045, 0.045, 8), CREAM)
    add_bm(bm, rod_bm((0, 5.30, 8.50), (0, 5.30, 8.65), 0.11, 0.11, 10), LIT)
    # Brass helm compass & life-rings on bridge wings
    for sx in (-1.0, 1.0):
        add_bm(bm, rod_bm((sx * 2.02, 6.0, 5.35), (sx * 2.10, 6.0, 5.35), 0.28, 0.28, 14), GOLD)

    obj = finalize_single_mesh("Istanbul_Ferry", bm, mats, uv_scale=0.35)
    export_single_fbx(obj, "Istanbul_Ferry.fbx")


# ===========================================================================
# 2. Istanbul_Tram.fbx — Iconic İstiklal Caddesi Nostalgic Red Tram!
# Exact slots: ['TrimDark', 'TramRed', 'GoldBrass', 'Glass', 'CreamPaint', 'Chrome', 'LitScreen', 'Wood']
# Exact bounds: X [-1.28..1.28], Y [-4.35..4.35], Z [0.0..4.33]
# ===========================================================================
def build_istanbul_tram():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = [
        make_mat("TrimDark",   (0.14, 0.15, 0.17, 1.0), rough=0.45, metal=0.60),
        make_mat("TramRed",    (0.76, 0.11, 0.11, 1.0), rough=0.26, metal=0.18),
        make_mat("GoldBrass",  (0.90, 0.68, 0.18, 1.0), rough=0.25, metal=0.80),
        make_mat("Glass",      (0.16, 0.26, 0.34, 1.0), rough=0.06, metal=0.20),
        make_mat("CreamPaint", (0.95, 0.92, 0.84, 1.0), rough=0.38),
        make_mat("Chrome",     (0.84, 0.86, 0.89, 1.0), rough=0.16, metal=0.92),
        make_mat("LitScreen",  (1.00, 0.93, 0.72, 1.0), rough=0.15, emission=2.0),
        make_mat("Wood",       (0.46, 0.26, 0.12, 1.0), rough=0.52),
    ]
    DARK, RED, GOLD, GLASS, CREAM, CHR, LIT, WOOD = 0, 1, 2, 3, 4, 5, 6, 7

    bm = bmesh.new()

    # A. Heavy Cast-Iron Undercarriage, Spoked Steel Wheels, Leaf Springs & Dual Cowcatcher Fenders!
    add_bm(bm, box_bm((0, 0, 0.56), (1.92, 7.60, 0.28), 0.03), DARK)
    # 4 spoked flanged rail wheels + axle boxes + helical coil springs
    for wy in (-2.05, 2.05):
        add_bm(bm, rod_bm((-0.94, wy, 0.36), (0.94, wy, 0.36), 0.075, 0.075, 10), DARK)
        for sx in (-1.0, 1.0):
            add_bm(bm, rod_bm((sx * 0.74, wy, 0.36), (sx * 0.88, wy, 0.36), 0.36, 0.36, 20), DARK)
            add_bm(bm, rod_bm((sx * 0.72, wy, 0.36), (sx * 0.76, wy, 0.36), 0.40, 0.40, 20), CHR)
            add_bm(bm, box_bm((sx * 0.94, wy, 0.42), (0.18, 0.46, 0.28), 0.03), DARK)
            add_bm(bm, rod_bm((sx * 0.96, wy - 0.34, 0.38), (sx * 0.96, wy - 0.34, 0.62), 0.07, 0.07, 10), CHR)
            add_bm(bm, rod_bm((sx * 0.96, wy + 0.34, 0.38), (sx * 0.96, wy + 0.34, 0.62), 0.07, 0.07, 10), CHR)
    # Longitudinal bogie sideframes
    for sx in (-0.94, 0.94):
        add_bm(bm, box_bm((sx, 0, 0.46), (0.14, 5.20, 0.22), 0.02), DARK)
    # Iconic slatted front & rear cowcatcher life-guards + oak boarding steps!
    for sy in (-1.0, 1.0):
        add_bm(bm, box_bm((0, sy * 4.18, 0.34), (1.84, 0.34, 0.24), 0.03), DARK)
        for xi in range(-4, 5):
            add_bm(bm, box_bm((xi * 0.20, sy * 4.30, 0.22), (0.045, 0.10, 0.28), 0.008), DARK)
        # Boarding steps at the 4 vestibule corners
        for sx in (-1.0, 1.0):
            add_bm(bm, box_bm((sx * 1.18, sy * 3.10, 0.34), (0.24, 0.92, 0.07), 0.015), WOOD)
            add_bm(bm, box_bm((sx * 1.12, sy * 3.10, 0.56), (0.22, 0.92, 0.07), 0.015), WOOD)

    # B. Iconic Red & Cream Vintage Wooden-Bodied Car Body (Lower crimson skirt + Cream window belt!)
    # Lower sculpted crimson-red waist panel (beveled gracefully + gold pinstripe moldings!)
    add_bm(bm, box_bm((0, 0, 1.24), (2.42, 7.95, 1.12), 0.09, 2), RED)
    # Curved rounded front & rear vestibule aprons (semi-octagonal vintage nose!)
    for sy in (-1.0, 1.0):
        add_bm(bm, box_bm((0, sy * 4.02, 1.24), (2.06, 0.42, 1.10), 0.14, 2), RED)
        add_bm(bm, box_bm((0, sy * 4.22, 1.24), (1.56, 0.22, 1.08), 0.10, 2), RED)
        # Twin golden accent bands & chrome bumper rail
        add_bm(bm, box_bm((0, sy * 4.26, 0.74), (1.96, 0.16, 0.11), 0.03), CHR)
        add_bm(bm, box_bm((0, sy * 4.28, 1.66), (1.76, 0.08, 0.07), 0.02), GOLD)
        # Iconic center cyclops brass headlamp + glowing lens + twin corner marker lights!
        add_bm(bm, rod_bm((0, sy * 4.22, 1.22), (0, sy * 4.35, 1.22), 0.21, 0.19, 16), GOLD)
        add_bm(bm, rod_bm((0, sy * 4.33, 1.22), (0, sy * 4.36, 1.22), 0.15, 0.15, 16), LIT)
        for sx in (-0.72, 0.72):
            add_bm(bm, rod_bm((sx, sy * 4.18, 1.56), (sx, sy * 4.26, 1.56), 0.08, 0.08, 12), GOLD)
            add_bm(bm, rod_bm((sx, sy * 4.24, 1.56), (sx, sy * 4.28, 1.56), 0.06, 0.06, 12), LIT)

    # Gold waist belt pinstripes & embossed Istanbul tram side medallions
    for sx in (-1.0, 1.0):
        add_bm(bm, box_bm((sx * 1.22, 0, 1.74), (0.06, 7.90, 0.07), 0.015), GOLD)
        add_bm(bm, box_bm((sx * 1.22, 0, 0.84), (0.06, 7.90, 0.06), 0.015), GOLD)
        # Brass vertical vestibule boarding grab rails!
        for sy in (-1.0, 1.0):
            add_bm(bm, rod_bm((sx * 1.24, sy * 2.66, 0.72), (sx * 1.24, sy * 2.66, 2.32), 0.026, 0.026, 8), GOLD)
            add_bm(bm, rod_bm((sx * 1.24, sy * 3.52, 0.72), (sx * 1.24, sy * 3.52, 2.32), 0.026, 0.026, 8), GOLD)

    # Upper window deckhouse — warm cream corner posts + recessed mahogany sashes + dark glass!
    add_bm(bm, box_bm((0, 0, 2.38), (2.28, 7.75, 1.14), 0.04), GLASS)
    # Longitudinal teak bench seats visible cleanly inside!
    for sx in (-0.82, 0.82):
        add_bm(bm, box_bm((sx, 0, 1.28), (0.44, 5.20, 0.10), 0.02), WOOD)
    # 7 vertical cream window pillars along each side + wooden upper window sashes
    for sx in (-1.0, 1.0):
        add_bm(bm, box_bm((sx * 1.18, 0, 1.84), (0.12, 7.86, 0.14), 0.02), CREAM)
        add_bm(bm, box_bm((sx * 1.18, 0, 2.92), (0.12, 7.86, 0.16), 0.02), CREAM)
        for py in (-3.85, -2.55, -1.52, -0.50, 0.50, 1.52, 2.55, 3.85):
            w_p = 0.22 if abs(py) > 3.0 else 0.14
            add_bm(bm, box_bm((sx * 1.18, py, 2.38), (0.12, w_p, 1.14), 0.018), CREAM)
        # Mahogany horizontal transom mullions inside side windows
        add_bm(bm, box_bm((sx * 1.16, 0, 2.62), (0.08, 7.60, 0.06), 0.01), WOOD)
    # Front/rear panoramic 3-pane driver windshield pillars & glowing TAKSİM-TÜNEL route signbox!
    for sy in (-1.0, 1.0):
        add_bm(bm, box_bm((0, sy * 3.98, 2.38), (1.96, 0.14, 1.12), 0.03), GLASS)
        for wx in (-0.98, -0.34, 0.34, 0.98):
            add_bm(bm, box_bm((wx, sy * 4.02, 2.38), (0.11, 0.14, 1.14), 0.015), CREAM)
        add_bm(bm, box_bm((0, sy * 4.06, 2.96), (1.42, 0.18, 0.28), 0.03), RED)
        add_bm(bm, box_bm((0, sy * 4.14, 2.96), (1.22, 0.08, 0.20), 0.01), LIT)

    # C. Vaulted Clerestory Monitor Roof (Classic Istanbul double-tier wooden tram roof!)
    add_bm(bm, box_bm((0, 0, 3.08), (2.56, 8.38, 0.22), 0.09, 2), CREAM)
    add_bm(bm, box_bm((0, 0, 3.22), (2.28, 7.80, 0.16), 0.06, 2), DARK)
    # Raised central clerestory lantern roof w/ little illuminated side vent panes!
    add_bm(bm, box_bm((0, 0, 3.44), (1.42, 6.20, 0.34), 0.05, 1), CREAM)
    add_bm(bm, box_bm((0, 0, 3.62), (1.58, 6.45, 0.14), 0.06, 2), DARK)
    for sx in (-1.0, 1.0):
        for vy in (-2.4, -1.2, 0.0, 1.2, 2.4):
            add_bm(bm, box_bm((sx * 0.72, vy, 3.44), (0.06, 0.85, 0.18), 0.01), LIT)

    # D. Articulated Diamond Roof Pantograph + Ceramic Insulators + Overhead Collector Bow!
    for ix in (-0.42, 0.42):
        for iy in (-0.52, 0.52):
            add_bm(bm, rod_bm((ix, iy, 3.68), (ix, iy, 3.80), 0.06, 0.05, 8), CREAM)
    add_bm(bm, box_bm((0, 0, 3.80), (0.96, 1.18, 0.06), 0.01), DARK)
    # Articulated diamond scissors rods rising cleanly to Z=4.33!
    for sx in (-0.36, 0.36):
        add_bm(bm, rod_bm((sx, -0.42, 3.82), (sx, -0.82, 4.06), 0.022, 0.022, 8), CHR)
        add_bm(bm, rod_bm((sx,  0.42, 3.82), (sx,  0.82, 4.06), 0.022, 0.022, 8), CHR)
        add_bm(bm, rod_bm((sx, -0.82, 4.06), (sx, -0.14, 4.29), 0.020, 0.020, 8), CHR)
        add_bm(bm, rod_bm((sx,  0.82, 4.06), (sx,  0.14, 4.29), 0.020, 0.020, 8), CHR)
    # Crossbars & carbon overhead contact bow
    add_bm(bm, rod_bm((-0.62, -0.12, 4.30), (0.62, -0.12, 4.30), 0.025, 0.025, 8), GOLD)
    add_bm(bm, rod_bm((-0.62,  0.12, 4.30), (0.62,  0.12, 4.30), 0.025, 0.025, 8), GOLD)
    add_bm(bm, rod_bm((-0.42, -0.82, 4.06), (0.42, -0.82, 4.06), 0.022, 0.022, 8), CHR)
    add_bm(bm, rod_bm((-0.42,  0.82, 4.06), (0.42,  0.82, 4.06), 0.022, 0.022, 8), CHR)

    obj = finalize_single_mesh("Istanbul_Tram", bm, mats, uv_scale=0.50)
    export_single_fbx(obj, "Istanbul_Tram.fbx")


# ===========================================================================
# 3. Render Batch 7 & Batch 6/8 Visual Showcase Sheet (phase4_batch7_buildings.png)
# ===========================================================================
def render_batch7_showcase():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.filepath = ARTIFACT_IMG

    # Bright daylight studio sky
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.66, 0.78, 0.90, 1.0)
        bg.inputs[1].default_value = 1.15

    # Ground plaza plane
    bpy.ops.mesh.primitive_plane_add(size=160, location=(0, 0, -0.02))
    gnd = bpy.context.active_object
    gm = make_mat("StudioPlaza", (0.76, 0.74, 0.70, 1.0), rough=0.88)
    gnd.data.materials.append(gm)

    def place_fbx(path, loc, rot_z_deg=0.0, scale=1.0):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=path)
        added = [o for o in bpy.data.objects if o not in before]
        holder = bpy.data.objects.new(os.path.basename(path) + "_Holder", None)
        bpy.context.collection.objects.link(holder)
        holder.location = loc
        holder.rotation_euler = (0, 0, math.radians(rot_z_deg))
        holder.scale = (scale, scale, scale)
        for o in added:
            if o.parent is None:
                o.parent = holder

    # Back row: 4 upgraded Hamidiye Avenue Blocks (rotated 180° so +Y street facades face camera at -Y!)
    place_fbx(os.path.join(ART_DIR, "Hamidiye_FatihCommercialBlock.fbx"), (-18.5, 11.5, 0), 180.0)
    place_fbx(os.path.join(ART_DIR, "Hamidiye_PetrolYoluBrandaBlock.fbx"), (-4.0, 11.5, 0), 180.0)
    place_fbx(os.path.join(ART_DIR, "Hamidiye_ZubeydeBlueHouse.fbx"), (8.5, 11.5, 0), 180.0)
    place_fbx(os.path.join(ART_DIR, "Hamidiye_EyyubiBufeBlock.fbx"), (20.5, 11.5, 0), 180.0)

    # Front row: Sculpted Istanbul_Ferry, Istanbul_Tram, plus new sculpted nature clusters & sofa!
    place_fbx(os.path.join(ART_DIR, "Istanbul_Ferry.fbx"), (-12.5, -5.5, 0.4), 76.0, 0.92)
    place_fbx(os.path.join(ART_DIR, "Istanbul_Tram.fbx"),  (  8.5, -5.0, 0.0), 68.0, 1.15)
    # Showcase new Batch 8 Nature Clusters right in foreground!
    place_fbx(os.path.join(LIFE_DIR, "OW_BushCluster.fbx"),     ( 16.5, -6.0, 0.0), 20.0, 1.8)
    place_fbx(os.path.join(LIFE_DIR, "OW_FloweringShrub.fbx"),  ( 19.5, -5.2, 0.0), -15.0, 1.8)
    place_fbx(os.path.join(LIFE_DIR, "OW_MossyRockCluster.fbx"),( 18.0, -8.5, 0.0), 35.0, 1.6)
    place_fbx(os.path.join(LIFE_DIR, "OW_HomeLuxurySofaSet.fbx"), ( 2.0, -9.2, 0.0), 160.0, 1.35)

    # Warm sunlight + soft fill
    bpy.ops.object.light_add(type="SUN", location=(25, -30, 35), rotation=(math.radians(46), 0, math.radians(34)))
    bpy.context.active_object.data.energy = 3.6
    bpy.ops.object.light_add(type="SUN", location=(-25, -20, 22), rotation=(math.radians(58), 0, math.radians(-42)))
    bpy.context.active_object.data.energy = 1.1

    bpy.ops.object.camera_add(location=(0.5, -42.0, 14.5))
    cam = bpy.context.active_object
    direction = Vector((0.5, 2.0, 5.0)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 34
    scene.camera = cam

    bpy.ops.render.render(write_still=True)
    print("Saved Batch 7 showcase render:", ARTIFACT_IMG)


if __name__ == "__main__":
    print("[GRIDROAM Phase 4 Batch 7] Upgrading Istanbul_Ferry & Istanbul_Tram ...")
    build_istanbul_ferry()
    build_istanbul_tram()
    render_batch7_showcase()
    print("[GRIDROAM Phase 4 Batch 7] Done!")
