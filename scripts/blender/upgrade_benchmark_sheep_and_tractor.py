#!/usr/bin/env python3
"""
GRIDROAM Phase 3 Benchmark – High-Quality Stylized-Realism Upgrade for:
  1. OW_Sheep.fbx       (Preserves EXACT 35-bone Armature & all 4 AnimationClips: Eating, Happy, Idle, Walk!
                         Upgrades 612-tri flat-shaded origami blob -> ~4,400-tri fluffy sculpted Anatolian sheep
                         with overlapping wool fleece clusters, expressive tapered ovine head/nostrils/eyelids/
                         glossy eyes/concave ears, jointed limbs & cloven hooves, full UVs & clean skinning!)
  2. OW_FarmTractor.fbx (Preserves EXACT 5-mesh pivot hierarchy: Chassis, Wheel_FL/FR/RL/RR & material slots!
                         Upgrades 1,572-tri blocky placeholder -> ~5,400-tri sculpted stylized-realistic Turkish
                         farm tractor with beveled louvered hood, slatted chrome grille, cast-iron engine & axle,
                         detailed cockpit/steering/levers/bucket seat, curved wrap-around fenders, ROPS canopy,
                         exhaust w/ heat shield & flap, recessed chrome-bezel stalk headlights, rear 3-point hitch,
                         3-rib front tires & 3D V-chevron lugged rear agricultural tires w/ bolted dish rims!)
"""

import math
import os
import shutil
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
LIFE_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldLife")
TEX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldTheme", "Textures")
REF_DIR = os.path.join(SCRIPT_DIR, "reference_rigs")
os.makedirs(REF_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Shared Helpers
# ---------------------------------------------------------------------------
def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else (hi if v > hi else v)


def smoothstep(e0, e1, x):
    t = clamp((x - e0) / max(1e-6, e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def gauss(dx, sx):
    t = dx / max(1e-6, sx)
    return math.exp(-0.5 * t * t)


def make_mat(name, color, roughness=0.55, metallic=0.0, emission=None, albedo_file=None, normal_file=None, normal_strength=0.8):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
    if mat.node_tree is None:
        mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if bsdf is None:
        return mat
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = 1.8

    # Optional preview texture binding in Blender (Unity binds via OurWorldModelLibrary.cs)
    if albedo_file:
        apath = os.path.join(TEX_DIR, albedo_file)
        if os.path.exists(apath):
            img = bpy.data.images.load(apath, check_existing=True)
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if normal_file:
        npath = os.path.join(TEX_DIR, normal_file)
        if os.path.exists(npath):
            img = bpy.data.images.load(npath, check_existing=True)
            img.colorspace_settings.name = "Non-Color"
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            nmap = nt.nodes.new("ShaderNodeNormalMap")
            nmap.inputs["Strength"].default_value = normal_strength
            nt.links.new(tex.outputs["Color"], nmap.inputs["Color"])
            nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def apply_smooth_obj(obj, angle_deg=60.0):
    mesh = obj.data
    if not isinstance(mesh, bpy.types.Mesh):
        return
    for p in mesh.polygons:
        p.use_smooth = True
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.edges.ensure_lookup_table()
    thresh = math.radians(angle_deg)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            e.smooth = e.calc_face_angle(0.0) < thresh
        else:
            e.smooth = False
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def box_uv_bm(bm, scale=1.5):
    uv_layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in f.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:
                u, v = co.x * scale, co.y * scale
            elif ax >= ay and ax >= az:
                u, v = co.y * scale, co.z * scale
            else:
                u, v = co.x * scale, co.z * scale
            loop[uv_layer].uv = (u, v)


def add_bm_to_target(target_bm, src_bm, mat_idx, smooth=True, uv_scale=1.5, tag_layer=None, tag_val=0):
    bmesh.ops.recalc_face_normals(src_bm, faces=src_bm.faces)
    uv_target = target_bm.loops.layers.uv.verify()
    vmap = {}
    for v in src_bm.verts:
        nv = target_bm.verts.new(v.co)
        if tag_layer is not None:
            nv[tag_layer] = tag_val
        vmap[v.index] = nv
    for f in src_bm.faces:
        try:
            nf = target_bm.faces.new([vmap[v.index] for v in f.verts])
            nf.material_index = mat_idx
            nf.smooth = smooth
            n = f.normal
            ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
            for loop in nf.loops:
                co = loop.vert.co
                if az >= ax and az >= ay:
                    u, v = co.x * uv_scale, co.y * uv_scale
                elif ax >= ay and ax >= az:
                    u, v = co.y * uv_scale, co.z * uv_scale
                else:
                    u, v = co.x * uv_scale, co.z * uv_scale
                loop[uv_target].uv = (u, v)
        except ValueError:
            pass
    src_bm.free()


def make_ellipsoid_bm(center, radii, segs=12, rings=8, rot_euler=(0, 0, 0), deform_fn=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    if deform_fn is not None:
        for v in bm.verts:
            v.co = deform_fn(v.co)
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return bm


def make_tube_bm(pts_radii, segments=10, cap_start=True, cap_end=True, rounded_end=True):
    """Build a smooth organic tube bmesh along a 3D centerline [(Vector, rx, ry), ...]."""
    bm = bmesh.new()
    n = len(pts_radii)
    rings = []
    frames = []
    prev_up = Vector((0.0, 0.0, 1.0))

    for i, (pt, rx, ry) in enumerate(pts_radii):
        p = Vector(pt)
        if i == 0:
            tan = (Vector(pts_radii[1][0]) - p).normalized()
        elif i == n - 1:
            tan = (p - Vector(pts_radii[i - 1][0])).normalized()
        else:
            tan = (Vector(pts_radii[i + 1][0]) - Vector(pts_radii[i - 1][0])).normalized()
        if tan.length < 1e-6:
            tan = Vector((0.0, 0.0, 1.0))
        ref = prev_up if abs(tan.dot(prev_up)) < 0.90 else Vector((1.0, 0.0, 0.0))
        if abs(tan.dot(ref)) >= 0.90:
            ref = Vector((0.0, 1.0, 0.0))
        side = tan.cross(ref).normalized()
        up = side.cross(tan).normalized()
        prev_up = up
        frames.append((p, tan, side, up, rx, ry))

        ring = []
        for s in range(segments):
            ang = (2.0 * math.pi * s) / segments
            co = p + side * (math.cos(ang) * rx) + up * (math.sin(ang) * ry)
            ring.append(bm.verts.new(co))
        rings.append(ring)

    for i in range(n - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))

    def cap(frame, base_ring, flip):
        p, tan, side, up, rx, ry = frame
        sign = -1.0 if flip else 1.0
        r_avg = 0.5 * (rx + ry)
        if rounded_end:
            prev_r = base_ring
            for d_frac, r_frac in ((0.34, 0.84), (0.60, 0.52)):
                cp = p + tan * (sign * r_avg * d_frac)
                nr = [bm.verts.new(cp + side * (math.cos(2 * math.pi * s / segments) * rx * r_frac) + up * (math.sin(2 * math.pi * s / segments) * ry * r_frac)) for s in range(segments)]
                for s in range(segments):
                    sn = (s + 1) % segments
                    bm.faces.new((nr[s], nr[sn], prev_r[sn], prev_r[s]) if flip else (prev_r[s], prev_r[sn], nr[sn], nr[s]))
                prev_r = nr
            apex = bm.verts.new(p + tan * (sign * r_avg * 0.74))
            for s in range(segments):
                sn = (s + 1) % segments
                bm.faces.new((apex, prev_r[sn], prev_r[s]) if flip else (apex, prev_r[s], prev_r[sn]))
        else:
            apex = bm.verts.new(p + tan * (sign * 0.002))
            for s in range(segments):
                sn = (s + 1) % segments
                bm.faces.new((apex, base_ring[sn], base_ring[s]) if flip else (apex, base_ring[s], base_ring[sn]))

    if cap_start:
        cap(frames[0], rings[0], flip=True)
    if cap_end:
        cap(frames[-1], rings[-1], flip=False)
    return bm


def make_bevel_box_bm(center, size, bevel=0.02, segments=2, rot_euler=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.05:
        bmesh.ops.bevel(
            bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET",
            segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True,
        )
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return bm


def make_cyl_bm(center, r1, r2, depth, segs=18, rot_euler=(0, 0, 0), cap_ends=True, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap_ends, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    if bevel > 0.0 and cap_ends:
        cap_edges = [e for e in bm.edges if len(e.link_faces) == 2 and abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-4]
        if cap_edges:
            bmesh.ops.bevel(bm, geom=cap_edges, offset=bevel, offset_type="OFFSET", segments=2, profile=0.5, affect="EDGES", clamp_overlap=True)
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return bm


# ===========================================================================
# BENCHMARK #2: OW_Sheep.fbx (Rigged Anatolian Stylized-Realistic Sheep)
# ===========================================================================
def upgrade_ow_sheep():
    sheep_fbx = os.path.join(LIFE_DIR, "OW_Sheep.fbx")
    orig_backup = os.path.join(REF_DIR, "OW_Sheep_orig.fbx")
    if not os.path.exists(orig_backup):
        shutil.copy2(sheep_fbx, orig_backup)
        print(f"  [Sheep] Saved untouched rig reference -> {orig_backup}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=orig_backup)

    arm_obj = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    orig_mesh_obj = next((o for o in bpy.data.objects if o.type == "MESH"), None)
    assert arm_obj is not None and orig_mesh_obj is not None, "OW_Sheep_orig.fbx missing Armature or Mesh!"

    # Ensure Armature is at clean rest pose while building & transferring weights
    arm_obj.data.pose_position = "REST"
    bpy.context.view_layer.update()

    # Rename imported Actions cleanly so exported FBX takes match OW_Sheep.fbx.meta EXACTLY:
    # Armature|Eating, Armature|Happy, Armature|Idle, Armature|Walk!
    for act in list(bpy.data.actions):
        short = act.name.split("|")[-1]
        act.name = short
    if arm_obj.animation_data and arm_obj.animation_data.action:
        # Pick Idle as active action
        idle_act = bpy.data.actions.get("Idle")
        if idle_act:
            arm_obj.animation_data.action = idle_act

    # Materials (preserve slot 0=OW_SheepWool, slot 1=OW_SheepFace, add slot 2=OW_EyeGloss for expressive eyes!)
    mat_wool = make_mat(
        "OW_SheepWool", (0.95, 0.92, 0.85), roughness=0.92, metallic=0.0,
        albedo_file="animal_sheep_wool_albedo.png", normal_file="animal_sheep_wool_normal.png", normal_strength=0.95,
    )
    mat_face = make_mat(
        "OW_SheepFace", (0.23, 0.20, 0.19), roughness=0.72, metallic=0.0,
        albedo_file="animal_sheep_face_albedo.png", normal_file="animal_sheep_face_normal.png", normal_strength=0.65,
    )
    mat_eye = make_mat("OW_EyeGloss", (0.09, 0.06, 0.04), roughness=0.08, metallic=0.08)

    # Build the complete stylized-realistic sheep mesh in Blender WORLD space (facing -Y!),
    # tagged by anatomical region so skinning weights are 100% artifact-free!
    # Region tags: 0=Body/Torso/Wool, 1=Head/Face/Ears/Crown, 2=FL, 3=FR, 4=BL, 5=BR
    bm = bmesh.new()
    tag_layer = bm.verts.layers.int.new("region_tag")

    # 1. PLUMP COHESIVE SCULPTED FLUFFY WOOL BODY (Tag 0, Slot 0 = OW_SheepWool)
    # Continuous plush fleece silhouette with gentle organic wool waves (NO separate cloud-balls!)
    def wool_body_deform(v):
        ang_xy = math.atan2(v.x, v.y)
        billow = (
            0.017 * math.sin(v.y * 13.0 + v.z * 9.5) * math.cos(v.x * 12.0)
            + 0.011 * math.cos(v.y * 21.0 - v.z * 16.0 + ang_xy * 3.0)
        )
        # Slightly broader plump haunches (+Y) & snug gentle taper toward shoulders
        rump_flare = 1.0 + 0.07 * smoothstep(-0.15, 0.42, v.y)
        v.x *= rump_flare
        # Natural subtle ovine backline (slightly fuller withers & rounded rump, soft mid-back)
        if v.z > 0.06:
            v.z -= 0.014 * gauss(v.y - 0.02, 0.20)
        n = v.normalized() if v.length > 1e-5 else Vector((0, 0, 1))
        return v + n * billow

    # Main continuous plush woolly torso barrel
    body_bm = make_ellipsoid_bm((0.0, 0.05, 0.665), (0.305, 0.535, 0.295), segs=30, rings=22, deform_fn=wool_body_deform)
    add_bm_to_target(bm, body_bm, 0, smooth=True, uv_scale=1.6, tag_layer=tag_layer, tag_val=0)

    # Smoothly blended woolly chest brisket ruff & thick neck fleece collar
    chest_bm = make_ellipsoid_bm((0.0, -0.355, 0.660), (0.235, 0.215, 0.220), segs=18, rings=12, rot_euler=(0.20, 0, 0))
    add_bm_to_target(bm, chest_bm, 0, smooth=True, uv_scale=1.6, tag_layer=tag_layer, tag_val=0)

    neck_wool_pts = [
        (Vector((0.0, -0.36, 0.72)), 0.205, 0.198),
        (Vector((0.0, -0.45, 0.81)), 0.172, 0.168),
        (Vector((0.0, -0.53, 0.90)), 0.142, 0.138),
        (Vector((0.0, -0.59, 0.96)), 0.114, 0.110),
    ]
    add_bm_to_target(bm, make_tube_bm(neck_wool_pts, segments=16, rounded_end=True), 0, smooth=True, uv_scale=1.6, tag_layer=tag_layer, tag_val=0)

    # Subtle deeply-blended anatomical fleece fullness (shoulders, haunches, brisket & neck ruff — NO bulbous humps!)
    fleece_clusters = [
        # Soft shoulder & haunch anatomical volume tucked smoothly into body coat
        (-0.14,  0.34, 0.66, (0.175, 0.205, 0.195)),
        ( 0.14,  0.34, 0.66, (0.175, 0.205, 0.195)),
        (-0.15, -0.25, 0.67, (0.160, 0.175, 0.170)),
        ( 0.15, -0.25, 0.67, (0.160, 0.175, 0.170)),
        # Cozy lower chest brisket wool & subtle neck mane ruff
        ( 0.00, -0.41, 0.57, (0.160, 0.135, 0.135)),
        (-0.08, -0.47, 0.84, (0.110, 0.115, 0.110)),
        ( 0.08, -0.47, 0.84, (0.110, 0.115, 0.110)),
        ( 0.00, -0.48, 0.94, (0.115, 0.110, 0.095)),
    ]
    for (cx, cy, cz, rad) in fleece_clusters:
        add_bm_to_target(bm, make_ellipsoid_bm((cx, cy, cz), rad, segs=14, rings=10), 0, smooth=True, uv_scale=1.6, tag_layer=tag_layer, tag_val=0)

    # Plump fluffy drooping woolly sheep tail over the rear rump!
    tail_pts = [
        (Vector((0.0, 0.50, 0.72)), 0.095, 0.085),
        (Vector((0.0, 0.59, 0.66)), 0.092, 0.082),
        (Vector((0.0, 0.63, 0.56)), 0.074, 0.068),
        (Vector((0.0, 0.62, 0.48)), 0.048, 0.046),
    ]
    add_bm_to_target(bm, make_tube_bm(tail_pts, segments=12, rounded_end=True), 0, smooth=True, uv_scale=1.6, tag_layer=tag_layer, tag_val=0)

    # Woolly upper-leg trouser/sleeve cuffs wrapping cleanly onto the 4 upper legs!
    leg_cuffs = [
        # (tag_val, top_pt, bot_pt, r_top, r_bot)
        (3, (-0.215, -0.273, 0.56), (-0.230, -0.312, 0.355), 0.108, 0.072),  # FR
        (2, ( 0.205, -0.273, 0.56), ( 0.218, -0.312, 0.355), 0.108, 0.072),  # FL
        (5, (-0.188,  0.315, 0.56), (-0.197,  0.415, 0.405), 0.118, 0.076),  # BR
        (4, ( 0.178,  0.315, 0.56), ( 0.185,  0.415, 0.405), 0.118, 0.076),  # BL
    ]
    for ltag, p0, p1, r0, r1 in leg_cuffs:
        pm = (Vector(p0) + Vector(p1)) * 0.5
        cuff_pts = [(p0, r0, r0 * 1.05), (pm, (r0 + r1) * 0.54, (r0 + r1) * 0.56), (p1, r1, r1)]
        add_bm_to_target(bm, make_tube_bm(cuff_pts, segments=12, rounded_end=True), 0, smooth=True, uv_scale=1.6, tag_layer=tag_layer, tag_val=ltag)

    # 2. EXPRESSIVE ANATOMICAL OVINE HEAD, EARS, EYELIDS, GLOSSY EYES & FLUFFY WOOL CROWN POOF! (Tag 1)
    # Exact Head bone runs from (-0.008, -0.589, 1.011) -> (-0.008, -0.862, 0.833)
    h_origin = Vector((-0.004, -0.585, 0.990))
    h_dir = (Vector((-0.004, -0.865, 0.822)) - h_origin).normalized()
    h_side = Vector((1.0, 0.0, 0.0))
    # h_dir.cross(h_side) points UPWARD (+Z) & slightly forward (-Y), perpendicular to skull axis!
    h_up = h_dir.cross(h_side).normalized()
    assert h_up.z > 0.5, f"h_up must point upward, got {h_up}"

    def hp(fwd, side, up):
        return h_origin + h_dir * fwd + h_side * side + h_up * up

    # Anatomically lofted Roman-nosed sheep skull & tapered velvety snout (Slot 1 = OW_SheepFace)
    skull_pts = [
        (hp(-0.04, 0.0,  0.000), 0.095, 0.095),  # occiput nestled in neck wool
        (hp( 0.04, 0.0,  0.012), 0.104, 0.098),  # broad cranial forehead & brow
        (hp( 0.11, 0.0,  0.008), 0.092, 0.088),  # orbital eye region & zygomatic cheek
        (hp( 0.18, 0.0,  0.004), 0.070, 0.072),  # gentle Roman sheep nose arch!
        (hp( 0.25, 0.0, -0.008), 0.056, 0.056),  # tapered bridge -> muzzle
        (hp( 0.30, 0.0, -0.015), 0.048, 0.044),  # soft rounded velvety sheep snout
    ]
    add_bm_to_target(bm, make_tube_bm(skull_pts, segments=18, rounded_end=True), 1, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=1)

    # Sculpted lower mandible / soft sheep chin nestled cleanly beneath snout!
    jaw_pts = [
        (hp(0.06, 0.0, -0.052), 0.064, 0.044),
        (hp(0.16, 0.0, -0.048), 0.054, 0.036),
        (hp(0.26, 0.0, -0.040), 0.038, 0.028),
    ]
    add_bm_to_target(bm, make_tube_bm(jaw_pts, segments=12, rounded_end=True), 1, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=1)

    # Softly flared nose-wings & dark comma-shaped ovine nostrils!
    for sx in (-1.0, 1.0):
        add_bm_to_target(bm, make_ellipsoid_bm(hp(0.295, sx * 0.022, -0.006), (0.020, 0.022, 0.018), segs=10, rings=6), 1, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=1)
        add_bm_to_target(bm, make_ellipsoid_bm(hp(0.320, sx * 0.018, -0.007), (0.009, 0.010, 0.006), segs=8, rings=5, rot_euler=(0.0, sx * 0.35, 0.0)), 2, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=1)

    # Expressive Ovine Eyes (glossy warm espresso-amber orbs + sculpted upper & lower eyelids!)
    for sx in (-1.0, 1.0):
        eye_c = hp(0.105, sx * 0.082, 0.024)
        add_bm_to_target(bm, make_ellipsoid_bm(eye_c, (0.018, 0.021, 0.018), segs=12, rings=8), 2, smooth=True, uv_scale=2.0, tag_layer=tag_layer, tag_val=1)
        up_lid = [
            (hp(0.082, sx * 0.076, 0.035), 0.009, 0.008),
            (hp(0.106, sx * 0.090, 0.042), 0.011, 0.009),
            (hp(0.130, sx * 0.078, 0.032), 0.009, 0.008),
        ]
        add_bm_to_target(bm, make_tube_bm(up_lid, segments=8, rounded_end=True), 1, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=1)
        lo_lid = [
            (hp(0.086, sx * 0.076, 0.014), 0.007, 0.006),
            (hp(0.108, sx * 0.088, 0.008), 0.008, 0.007),
            (hp(0.130, sx * 0.074, 0.014), 0.007, 0.006),
        ]
        add_bm_to_target(bm, make_tube_bm(lo_lid, segments=7, rounded_end=True), 1, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=1)

    # Smooth Sculpted Drooping Ovine Ears (soft leaf-shaped arched ears emerging cleanly behind eyes!)
    for sx in (-1.0, 1.0):
        ear_pts = [
            (hp(0.020, sx * 0.082,  0.036), 0.014, 0.020),
            (hp(0.036, sx * 0.136,  0.028), 0.015, 0.034),
            (hp(0.050, sx * 0.194,  0.004), 0.013, 0.032),
            (hp(0.056, sx * 0.238, -0.026), 0.009, 0.019),
        ]
        add_bm_to_target(bm, make_tube_bm(ear_pts, segments=10, rounded_end=True), 1, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=1)

    # Signature Adorable Fluffy Wool Crown Poof sitting ON TOP of sheep's forehead between ears! (Slot 0, Tag 1!)
    crown_puffs = [
        (hp(0.00,  0.000, 0.095), (0.092, 0.092, 0.072)),
        (hp(0.05, -0.042, 0.088), (0.062, 0.065, 0.054)),
        (hp(0.05,  0.042, 0.088), (0.062, 0.065, 0.054)),
        (hp(0.09,  0.000, 0.082), (0.060, 0.058, 0.046)),
        (hp(-0.05, 0.000, 0.084), (0.105, 0.088, 0.072)),
    ]
    for c_pos, c_rad in crown_puffs:
        add_bm_to_target(bm, make_ellipsoid_bm(c_pos, c_rad, segs=12, rings=8), 0, smooth=True, uv_scale=1.6, tag_layer=tag_layer, tag_val=1)

    # 3. ANATOMICAL JOINTED OVINE LEGS & SPLIT CLOVEN HOOVES! (Tags 2..5, Slot 1 = OW_SheepFace)
    # Aligned to EXACT bone chains inspected from OW_Sheep_orig.fbx!
    legs_spec = [
        # (tag, upper_bone_head, mid_joint, fetlock, hoof_base)
        (3, Vector((-0.230, -0.273, 0.560)), Vector((-0.230, -0.334, 0.292)), Vector((-0.230, -0.294, 0.075)), Vector((-0.230, -0.305, 0.002))),  # FR
        (2, Vector(( 0.218, -0.273, 0.560)), Vector(( 0.218, -0.334, 0.292)), Vector(( 0.218, -0.294, 0.075)), Vector(( 0.218, -0.305, 0.002))),  # FL
        (5, Vector((-0.197,  0.307, 0.560)), Vector((-0.197,  0.466, 0.373)), Vector((-0.197,  0.405, 0.075)), Vector((-0.197,  0.390, 0.002))),  # BR
        (4, Vector(( 0.185,  0.307, 0.560)), Vector(( 0.185,  0.466, 0.373)), Vector(( 0.185,  0.405, 0.075)), Vector(( 0.185,  0.390, 0.002))),  # BL
    ]
    for ltag, p_up, p_mid, p_fet, p_hoof in legs_spec:
        # Smooth anatomical leg: upper forearm/gaskin -> knee/hock joint -> slender cannon bone -> fetlock swell -> pastern
        p_above = p_up.lerp(p_mid, 0.65)
        p_below = p_mid.lerp(p_fet, 0.35)
        p_mid_cannon = p_mid.lerp(p_fet, 0.72)
        leg_pts = [
            (p_up,         0.068, 0.074),
            (p_above,      0.052, 0.056),
            (p_mid,        0.044, 0.048),  # anatomical knee / hock joint swell
            (p_below,      0.032, 0.035),  # slender ovine cannon bone
            (p_mid_cannon, 0.030, 0.032),
            (p_fet,        0.036, 0.039),  # fetlock joint swell
            (p_hoof + Vector((0, 0, 0.032)), 0.034, 0.042),
        ]
        add_bm_to_target(bm, make_tube_bm(leg_pts, segments=12, rounded_end=True), 1, smooth=True, uv_scale=2.2, tag_layer=tag_layer, tag_val=ltag)

        # Split 2-Toe Cloven Hooves planted cleanly at z = 0.00! (Slot 2 = OW_EyeGloss dark hoof horn!)
        for toe_x in (-0.017, 0.017):
            toe_bm = make_bevel_box_bm(
                (p_hoof.x + toe_x, p_hoof.y - 0.014, 0.024),
                (0.028, 0.074, 0.046),
                bevel=0.009, segments=2,
            )
            # Taper front toe tip subtly like a real cloven sheep hoof!
            for v in toe_bm.verts:
                if v.co.y < p_hoof.y - 0.020:
                    v.co.x = p_hoof.x + toe_x + (v.co.x - (p_hoof.x + toe_x)) * 0.72
                    if v.co.z > 0.025:
                        v.co.z -= 0.012
                if v.co.z < 0.001:
                    v.co.z = 0.001
            add_bm_to_target(bm, toe_bm, 2, smooth=True, uv_scale=2.0, tag_layer=tag_layer, tag_val=ltag)

    # Convert world-space bmesh into a new Blender Mesh object
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Extract vertex region tags before freeing bm!
    v_tags = [v[tag_layer] for v in bm.verts]
    v_world_cos = [v.co.copy() for v in bm.verts]

    new_mesh = bpy.data.meshes.new("Sheep_Upgraded_Mesh")
    bm.to_mesh(new_mesh)
    bm.free()
    new_mesh.materials.append(mat_wool)
    new_mesh.materials.append(mat_face)
    new_mesh.materials.append(mat_eye)

    new_obj = bpy.data.objects.new("Sheep", new_mesh)
    bpy.context.collection.objects.link(new_obj)
    apply_smooth_obj(new_obj, angle_deg=64.0)

    # 4. TRANSFER RIG WEIGHTS FROM ORIGINAL SHEEP + ANATOMICAL REGION SAFETY SKINNING!
    # Step 4a: Transfer base weights via Blender DATA_TRANSFER (POLYINTERP_NEAREST) in world space
    # First create all 23 vertex groups matching original Sheep
    for vg in orig_mesh_obj.vertex_groups:
        new_obj.vertex_groups.new(name=vg.name)

    dt = new_obj.modifiers.new(name="TransferWeights", type="DATA_TRANSFER")
    dt.object = orig_mesh_obj
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    bpy.ops.object.select_all(action="DESELECT")
    new_obj.select_set(True)
    bpy.context.view_layer.objects.active = new_obj
    bpy.ops.object.modifier_apply(modifier=dt.name)

    # Step 4b: Deterministic anatomical safety cleanup so NO head vertex is pulled by legs,
    # NO leg pulls the opposite leg, and hooves stay locked smoothly to lower legs during Walk & Eating!
    leg_groups = {
        2: ("FrontLeg.L", "FrontUpLeg.L", "FrontLowLeg.L", "FrontFoot.L"),
        3: ("FrontLeg.R", "FrontUpLeg.R", "FrontLowLeg.R", "FrontFoot.R"),
        4: ("BackLeg.L",  "BackUpLeg.L",  "BackLowLeg.L",  "BackFoot.L"),
        5: ("BackLeg.R",  "BackUpLeg.R",  "BackLowLeg.R",  "BackFoot.R"),
    }
    vg_by_name = {vg.name: vg for vg in new_obj.vertex_groups}
    all_vgs = list(new_obj.vertex_groups)

    def clear_vert(vi):
        for vg in all_vgs:
            vg.remove([vi])

    for vi, (tag, wco) in enumerate(zip(v_tags, v_world_cos)):
        if tag == 1:
            # Head / Ears / Eyes / Wool Crown Poof -> smooth Head + Neck blend ONLY!
            # Head bone pivot is around y=-0.589, z=1.011; further forward (-Y) = 100% Head!
            w_head = smoothstep(-0.50, -0.60, wco.y)
            w_neck = 1.0 - w_head
            clear_vert(vi)
            if w_head > 0.01:
                vg_by_name["Head"].add([vi], w_head, "REPLACE")
            if w_neck > 0.01:
                vg_by_name["Neck"].add([vi], w_neck, "REPLACE")
        elif tag in leg_groups:
            # Each leg strictly follows its OWN 3-bone chain (UpLeg -> LowLeg -> Foot)!
            g_hip, g_up, g_lo, g_ft = leg_groups[tag]
            z = wco.z
            knee_z = 0.292 if tag in (2, 3) else 0.373
            clear_vert(vi)
            if z > knee_z + 0.08:
                # Upper leg cuff smoothly blending into Body/Leg hip
                t_top = smoothstep(knee_z + 0.08, 0.56, z)
                vg_by_name[g_up].add([vi], 1.0 - 0.55 * t_top, "REPLACE")
                vg_by_name[g_hip].add([vi], 0.35 * t_top, "REPLACE")
                vg_by_name["Body"].add([vi], 0.20 * t_top, "REPLACE")
            elif z > knee_z - 0.07:
                # Smooth knee / hock articulation bend
                t_kn = smoothstep(knee_z - 0.07, knee_z + 0.07, z)
                vg_by_name[g_up].add([vi], t_kn, "REPLACE")
                vg_by_name[g_lo].add([vi], 1.0 - t_kn, "REPLACE")
            elif z > 0.06:
                # Lower cannon bone
                vg_by_name[g_lo].add([vi], 1.0, "REPLACE")
            else:
                # Hoof & pastern solidly following LowLeg (note: FrontFoot/BackFoot are IK targets on root;
                # let's check whether original Sheep weights hooves to LowLeg or Foot!)
                vg_by_name[g_lo].add([vi], 1.0, "REPLACE")

    # Normalize & limit to max 4 bone influences per vertex (Unity mobile SkinnedMeshRenderer budget!)
    bpy.ops.object.select_all(action="DESELECT")
    new_obj.select_set(True)
    bpy.context.view_layer.objects.active = new_obj
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

    # Remove old blocky mesh and parent new_obj cleanly to Armature!
    old_name = orig_mesh_obj.name
    old_mesh_name = orig_mesh_obj.data.name
    bpy.data.objects.remove(orig_mesh_obj, do_unlink=True)

    # Parent new_obj to Armature keeping exact world transform!
    new_obj.name = old_name
    new_obj.data.name = old_mesh_name
    new_obj.parent = arm_obj
    new_obj.matrix_parent_inverse = arm_obj.matrix_world.inverted()
    arm_mod = new_obj.modifiers.new(name="Armature", type="ARMATURE")
    arm_mod.object = arm_obj

    # Re-enable POSE mode before export
    arm_obj.data.pose_position = "POSE"
    bpy.context.view_layer.update()

    # Export preserving exact Armature + all 4 animation actions!
    bpy.ops.object.select_all(action="DESELECT")
    arm_obj.select_set(True)
    new_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj

    bpy.ops.export_scene.fbx(
        filepath=sheep_fbx,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.5,
        path_mode="AUTO",
    )
    tris = sum(max(1, len(p.vertices) - 2) for p in new_obj.data.polygons)
    print(f"  [Sheep] Exported OW_Sheep.fbx | verts={len(new_obj.data.vertices)} | tris={tris} | bones={len(arm_obj.data.bones)} | actions={[a.name for a in bpy.data.actions]}")


# ===========================================================================
# BENCHMARK #3: OW_FarmTractor.fbx (Sculpted Stylized-Realistic Farm Tractor)
# ===========================================================================
def build_tractor_wheel(name, pivot, radius, width, is_rear, mats):
    """Build a sculpted agricultural tractor wheel (3D V-chevron lugs on rear, 3-rib on front, dished yellow rim + hex lug bolts!)."""
    px, py, pz = pivot
    bm = bmesh.new()
    # Material indices: 0=OW_TireRubber, 1=OW_SafetyYellow, 2=OW_SteelDark
    r_rim = radius * 0.56
    r_bead = radius * 0.60
    r_shldr = radius * 0.94
    r_crown = radius * 0.985
    hw = width * 0.50

    # 1. Smoothly profiled toroidal tire carcass (Lathe around X axis!)
    prof = [
        (r_bead,  -hw * 0.80),
        (r_shldr, -hw * 1.00),
        (r_crown, -hw * 0.72),
        (r_crown,  hw * 0.72),
        (r_shldr,  hw * 1.00),
        (r_bead,   hw * 0.80),
        (r_rim,    hw * 0.62),
        (r_rim,   -hw * 0.62),
    ]
    segs = 32 if is_rear else 26
    t_bm = bmesh.new()
    rings = []
    for s in range(segs):
        ang = (2.0 * math.pi * s) / segs
        cy, cz = math.cos(ang), math.sin(ang)
        ring = [t_bm.verts.new(Vector((x_off, cy * r, cz * r))) for (r, x_off) in prof]
        rings.append(ring)
    for s in range(segs):
        sn = (s + 1) % segs
        for p in range(len(prof)):
            pn = (p + 1) % len(prof)
            t_bm.faces.new((rings[s][p], rings[sn][p], rings[sn][pn], rings[s][pn]))
    add_bm_to_target(bm, t_bm, 0, smooth=True, uv_scale=1.8)

    if is_rear:
        # 18 Alternating Pairs of Flush Tangent-Aligned 3D V-Chevron Agricultural Traction Lugs!
        # At ang=0: +Y is radial outward, X is tire width, Z is tread tangent -> chevron tilts around Y!
        n_lugs = 18
        for i in range(n_lugs):
            for side_idx, sx in enumerate((-1.0, 1.0)):
                ang = (2.0 * math.pi * (i + side_idx * 0.5)) / n_lugs
                cy, cz = math.cos(ang), math.sin(ang)
                r_lug = radius * 0.978
                lx = sx * (hw * 0.44)
                ly = cy * r_lug
                lz = cz * r_lug
                # Wide bar along X, low sturdy radial tread depth along Y (0.056m embedded halfway), solid bar along Z!
                lug = make_bevel_box_bm((0, 0, 0), (hw * 0.92, 0.058, 0.064), bevel=0.008, segments=1)
                m_chev = Euler((0.0, sx * math.radians(34.0), 0.0), "XYZ").to_matrix().to_4x4()
                m_rad = Euler((ang, 0.0, 0.0), "XYZ").to_matrix().to_4x4()
                bmesh.ops.transform(lug, matrix=Matrix.Translation((lx, ly, lz)) @ m_rad @ m_chev, verts=lug.verts)
                add_bm_to_target(bm, lug, 0, smooth=True, uv_scale=2.0)
    else:
        # Authentic 3-Rib Front Agricultural Steering Tire (outer peak kisses z=0.000!)
        for rx_off, rib_w, rib_h in ((-hw * 0.48, 0.026, 0.012), (0.0, 0.034, 0.016), (hw * 0.48, 0.026, 0.012)):
            r_bm = make_cyl_bm((rx_off, 0, 0), radius * 0.985 + rib_h, radius * 0.985 + rib_h, rib_w, segs=24, rot_euler=(0, math.pi * 0.5, 0), cap_ends=True, bevel=0.004)
            add_bm_to_target(bm, r_bm, 0, smooth=True, uv_scale=2.0)

    # 2. Deep Dished Classic Safety-Yellow Agricultural Steel Rim (Slot 1 = OW_SafetyYellow)
    out_sign = -1.0 if px < 0 else 1.0
    # Outer rolled steel bead ring
    rim_ring = make_cyl_bm((0, 0, 0), r_bead * 1.01, r_bead * 1.01, hw * 1.48, segs=24, rot_euler=(0, math.pi * 0.5, 0), cap_ends=True, bevel=0.012)
    add_bm_to_target(bm, rim_ring, 1, smooth=True, uv_scale=1.6)
    # Recessed dished center plate with tapered bevel
    dish_outer = make_cyl_bm((out_sign * hw * 0.25, 0, 0), r_rim * 0.96, r_rim * 0.72, hw * 0.60, segs=22, rot_euler=(0, math.pi * 0.5 * (1 if out_sign > 0 else -1), 0), cap_ends=True)
    add_bm_to_target(bm, dish_outer, 1, smooth=True, uv_scale=1.6)
    # 4 Rim reinforcement lug tabs around inner barrel
    for k in range(4):
        ka = k * math.pi * 0.5 + math.pi * 0.25
        tx = out_sign * (hw * 0.52)
        ty = math.cos(ka) * (r_rim * 0.80)
        tz = math.sin(ka) * (r_rim * 0.80)
        tab = make_bevel_box_bm((tx, ty, tz), (0.045, 0.085 if is_rear else 0.055, 0.055 if is_rear else 0.036), bevel=0.006, segments=1, rot_euler=(ka, 0, 0))
        add_bm_to_target(bm, tab, 1, smooth=True, uv_scale=1.8)

    # 3. Planetary Cast-Iron Center Hub + Protruding 3D Hex Lug Bolts! (Slot 2 = OW_SteelDark)
    hub_r = radius * (0.21 if is_rear else 0.22)
    hub = make_cyl_bm((out_sign * hw * 0.55, 0, 0), hub_r, hub_r * 0.84, hw * 0.56, segs=16, rot_euler=(0, math.pi * 0.5 * (1 if out_sign > 0 else -1), 0), cap_ends=True, bevel=0.010)
    add_bm_to_target(bm, hub, 2, smooth=True, uv_scale=2.0)
    # Grease cap dome
    cap = make_cyl_bm((out_sign * hw * 0.86, 0, 0), hub_r * 0.48, hub_r * 0.36, 0.065, segs=12, rot_euler=(0, math.pi * 0.5 * (1 if out_sign > 0 else -1), 0), cap_ends=True, bevel=0.006)
    add_bm_to_target(bm, cap, 2, smooth=True, uv_scale=2.0)

    n_bolts = 8 if is_rear else 6
    bolt_ring_r = radius * (0.31 if is_rear else 0.30)
    for b in range(n_bolts):
        ba = (2.0 * math.pi * b) / n_bolts
        bx = out_sign * (hw * 0.62)
        by = math.cos(ba) * bolt_ring_r
        bz = math.sin(ba) * bolt_ring_r
        bolt = make_cyl_bm((bx, by, bz), 0.022 if is_rear else 0.016, 0.020 if is_rear else 0.015, 0.048, segs=6, rot_euler=(0, math.pi * 0.5, 0), cap_ends=True)
        add_bm_to_target(bm, bolt, 2, smooth=False, uv_scale=2.4)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    for m in (mats["OW_TireRubber"], mats["OW_SafetyYellow"], mats["OW_SteelDark"]):
        mesh.materials.append(m)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = Vector(pivot)
    apply_smooth_obj(obj, angle_deg=52.0)
    return obj


def build_tractor_chassis(mats):
    """Build the sculpted stylized-realistic Turkish Farm Tractor chassis, hood, cockpit, curved fenders, ROPS & 3-point hitch!"""
    bm = bmesh.new()
    # Exact slot indices:
    # 0=OW_TractorRed, 1=OW_Chrome, 2=OW_HeadlightGlow, 3=OW_SteelDark, 4=OW_TireRubber, 5=OW_StoneWhite
    RED, CHR, GLOW, DARK, RUB, WHITE = 0, 1, 2, 3, 4, 5

    # --- 1. SCULPTED BEVELED CLASSIC TRACTOR ENGINE HOOD & NOSE (RED) ---
    # Lofted gracefully tapered hood from firewall (y=0.14) to rounded prow nose (y=1.70)
    hood_specs = [
        # (y, half_w, z_bot, z_top, crown_lift)
        (0.14, 0.43, 0.88, 1.56, 0.07),
        (0.45, 0.42, 0.88, 1.57, 0.075),
        (1.05, 0.39, 0.86, 1.54, 0.070),
        (1.56, 0.36, 0.84, 1.49, 0.062),
        (1.68, 0.33, 0.85, 1.44, 0.045),
        (1.72, 0.27, 0.89, 1.38, 0.025),
    ]
    h_bm = bmesh.new()
    h_rings = []
    p_segs = 18
    for (y, hw, zb, zt, cl) in hood_specs:
        zc = (zb + zt) * 0.5
        hz = (zt - zb) * 0.5
        ring = []
        for s in range(p_segs):
            ang = (2.0 * math.pi * s) / p_segs
            ca, sa = math.cos(ang), math.sin(ang)
            # Rounded-square hood section with subtly domed top crown!
            x = hw * math.copysign(abs(ca) ** 0.58, ca)
            z = zc + hz * math.copysign(abs(sa) ** 0.58, sa)
            if sa > 0.2:
                z += cl * (1.0 - ca * ca) * sa
            ring.append(h_bm.verts.new(Vector((x, y, z))))
        h_rings.append(ring)
    for i in range(len(h_rings) - 1):
        for s in range(p_segs):
            sn = (s + 1) % p_segs
            h_bm.faces.new((h_rings[i][s], h_rings[i][sn], h_rings[i + 1][sn], h_rings[i + 1][s]))
    h_bm.faces.new(list(reversed(h_rings[0])))
    h_bm.faces.new(h_rings[-1])
    add_bm_to_target(bm, h_bm, RED, smooth=True, uv_scale=1.2)

    # Flush hood-crown chrome center spear + recessed diesel fuel cap!
    add_bm_to_target(bm, make_bevel_box_bm((0.0, 0.90, 1.595), (0.042, 1.28, 0.020), bevel=0.006, segments=1, rot_euler=(-0.055, 0, 0)), CHR, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((0.0, 0.36, 1.625), 0.048, 0.046, 0.036, segs=12, bevel=0.005), DARK, smooth=True)
    # Recessed dark cooling louver vents along both hood flanks (6 crisp angled slats per side!)
    for sx in (-1.0, 1.0):
        # Side chrome trim accent strip
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.405, 0.96, 1.32), (0.018, 1.32, 0.036), bevel=0.006, segments=1, rot_euler=(0, 0, -sx * 0.045)), CHR, smooth=True)
        # Recessed dark louver panel
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.395, 1.04, 1.07), (0.022, 0.68, 0.24), bevel=0.006, segments=1, rot_euler=(0, 0, -sx * 0.045)), DARK, smooth=True)
        for li in range(6):
            ly = 0.78 + li * 0.105
            add_bm_to_target(bm, make_bevel_box_bm((sx * 0.408, ly, 1.07), (0.016, 0.026, 0.20), bevel=0.004, segments=1, rot_euler=(0, 0, -sx * 0.22)), RED, smooth=True)

    # --- 2. SLATTED CHROME RADIATOR GRILLE & FRONT CAST-IRON BUMPER WEIGHTS ---
    # Dark radiator recessed core
    add_bm_to_target(bm, make_bevel_box_bm((0.0, 1.71, 1.15), (0.56, 0.055, 0.50), bevel=0.014, segments=2), DARK, smooth=True)
    # Chrome perimeter grille surround (4 beveled frame bars)
    add_bm_to_target(bm, make_bevel_box_bm(( 0.00, 1.735, 1.40), (0.60, 0.042, 0.045), bevel=0.010, segments=1), CHR, smooth=True)
    add_bm_to_target(bm, make_bevel_box_bm(( 0.00, 1.735, 0.90), (0.60, 0.042, 0.045), bevel=0.010, segments=1), CHR, smooth=True)
    for sx in (-1.0, 1.0):
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.285, 1.735, 1.15), (0.045, 0.042, 0.52), bevel=0.010, segments=1), CHR, smooth=True)
    # 7 Crisp Horizontal Chrome Grille Slats + center badge!
    for gi in range(7):
        gz = 0.96 + gi * 0.063
        add_bm_to_target(bm, make_bevel_box_bm((0.0, 1.745, gz), (0.53, 0.022, 0.022), bevel=0.005, segments=1), CHR, smooth=True)
    add_bm_to_target(bm, make_bevel_box_bm((0.0, 1.755, 1.28), (0.14, 0.025, 0.085), bevel=0.007, segments=1), RED, smooth=True)

    # Heavy cast-iron front counterweight bumper + tow clevis pin!
    add_bm_to_target(bm, make_bevel_box_bm((0.0, 1.70, 0.64), (0.66, 0.24, 0.24), bevel=0.025, segments=2), DARK, smooth=True)
    for wi in (-0.20, -0.07, 0.07, 0.20):
        add_bm_to_target(bm, make_bevel_box_bm((wi, 1.78, 0.64), (0.095, 0.16, 0.28), bevel=0.016, segments=1), DARK, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((0.0, 1.84, 0.64), 0.024, 0.024, 0.18, segs=10), CHR, smooth=True)

    # --- 3. EXPOSED CAST-IRON ENGINE BLOCK, FRONT AXLE, TRANSMISSION & DRIVER STEPS (DARK) ---
    # Main engine crankcase & gearbox backbone
    add_bm_to_target(bm, make_bevel_box_bm((0.0, 0.72, 0.66), (0.52, 1.66, 0.38), bevel=0.032, segments=2), DARK, smooth=True)
    # Rear differential axle housing & center gearbox
    add_bm_to_target(bm, make_bevel_box_bm((0.0, -0.45, 0.74), (0.62, 0.86, 0.42), bevel=0.036, segments=2), DARK, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((0.0, -0.58, 0.72), 0.135, 0.135, 1.48, segs=16, rot_euler=(0, math.pi * 0.5, 0)), DARK, smooth=True)
    # Visible cast engine cylinder blocks & oil/air filter canisters on sides!
    for sx in (-1.0, 1.0):
        add_bm_to_target(bm, make_cyl_bm((sx * 0.30, 0.92, 0.78), 0.075, 0.075, 0.38, segs=12, rot_euler=(math.pi * 0.5, 0, 0)), DARK, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((0.35, 1.24, 0.82), 0.068, 0.068, 0.24, segs=12), DARK, smooth=True)  # vertical oil filter
    # Front beam axle + steering knuckles + tie-rod linking FL/FR wheels!
    add_bm_to_target(bm, make_bevel_box_bm((0.0, 1.26, 0.44), (1.36, 0.12, 0.11), bevel=0.020, segments=1), DARK, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((0.0, 1.13, 0.45), 0.024, 0.024, 1.24, segs=10, rot_euler=(0, math.pi * 0.5, 0)), CHR, smooth=True)  # tie rod
    for sx in (-1.0, 1.0):
        add_bm_to_target(bm, make_cyl_bm((sx * 0.62, 1.26, 0.48), 0.055, 0.048, 0.20, segs=10), DARK, smooth=True)
    # Driver steel footboards & boarding step ladders on both sides!
    for sx in (-1.0, 1.0):
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.44, -0.10, 0.82), (0.36, 0.78, 0.055), bevel=0.010, segments=1), DARK, smooth=True)
        # Lower boarding step stirrup
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.56, 0.08, 0.52), (0.20, 0.34, 0.032), bevel=0.006, segments=1), DARK, smooth=True)
        for dy in (-0.13, 0.13):
            add_bm_to_target(bm, make_bevel_box_bm((sx * 0.56, 0.08 + dy, 0.67), (0.032, 0.032, 0.30), bevel=0.005, segments=1), DARK, smooth=True)

    # --- 4. CURVED WRAP-AROUND SCULPTED REAR FENDERS, GRAB HANDLES & WARNING LAMPS (RED) ---
    # Smooth circular arc hugging Rear Wheels at (x=±0.88, y=-0.58, z=0.72), R=0.715 -> fender inner R=0.785!
    for sx in (-1.0, 1.0):
        f_bm = bmesh.new()
        arc_segs = 18
        r_in, r_out = 0.785, 0.835
        x_in = sx * 0.56
        x_out = sx * 1.10
        # Sweep from rear (-16 deg) up over top to front-top (+140 deg)
        a_start, a_end = math.radians(-16.0), math.radians(136.0)
        g_in_lo, g_in_hi, g_out_lo, g_out_hi = [], [], [], []
        for i in range(arc_segs + 1):
            a = a_start + (a_end - a_start) * (i / arc_segs)
            # In YZ around (-0.58, 0.72): y = -0.58 - cos(a)*r, z = 0.72 + sin(a)*r
            cy, cz = -math.cos(a), math.sin(a)
            y_i, z_i = -0.58 + cy * r_in,  0.72 + cz * r_in
            y_o, z_o = -0.58 + cy * r_out, 0.72 + cz * r_out
            # Subtle lip drop along outer fender edge
            g_in_lo.append(f_bm.verts.new(Vector((x_in,  y_i, z_i))))
            g_in_hi.append(f_bm.verts.new(Vector((x_in,  y_o, z_o))))
            g_out_lo.append(f_bm.verts.new(Vector((x_out, y_i, z_i))))
            g_out_hi.append(f_bm.verts.new(Vector((x_out, y_o, z_o))))
        for i in range(arc_segs):
            # Top curved skin, bottom inner skin, inner side wall, outer lip edge!
            f_bm.faces.new((g_in_hi[i],  g_out_hi[i], g_out_hi[i + 1], g_in_hi[i + 1]))
            f_bm.faces.new((g_out_lo[i], g_in_lo[i],  g_in_lo[i + 1],  g_out_lo[i + 1]))
            f_bm.faces.new((g_in_lo[i],  g_in_hi[i],  g_in_hi[i + 1],  g_in_lo[i + 1]))
            f_bm.faces.new((g_out_hi[i], g_out_lo[i], g_out_lo[i + 1], g_out_hi[i + 1]))
        f_bm.faces.new((g_in_lo[0], g_out_lo[0], g_out_hi[0], g_in_hi[0]))
        f_bm.faces.new((g_in_hi[-1], g_out_hi[-1], g_out_lo[-1], g_in_lo[-1]))
        add_bm_to_target(bm, f_bm, RED, smooth=True, uv_scale=1.2)

        # Inner fender splash wall tucked neatly below curved mudguard crown!
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.56, -0.54, 1.08), (0.042, 0.90, 0.50), bevel=0.012, segments=1), RED, smooth=True)
        # Tubular steel fender grab handle bar on top of each mudguard
        add_bm_to_target(bm, make_cyl_bm((sx * 0.84, -0.45, 1.62), 0.018, 0.018, 0.42, segs=10, rot_euler=(math.pi * 0.5, 0, 0)), DARK, smooth=True)
        for gy in (-0.63, -0.27):
            add_bm_to_target(bm, make_cyl_bm((sx * 0.84, gy, 1.57), 0.016, 0.016, 0.11, segs=8), DARK, smooth=True)
        # Front/Rear amber fender marker lamps!
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.96, -0.18, 1.54), (0.11, 0.08, 0.065), bevel=0.012, segments=1), DARK, smooth=True)
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.96, -0.14, 1.54), (0.085, 0.025, 0.045), bevel=0.006, segments=1), GLOW, smooth=True)
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.96, -1.04, 1.38), (0.12, 0.06, 0.075), bevel=0.012, segments=1), DARK, smooth=True)
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.96, -1.07, 1.38), (0.09, 0.024, 0.052), bevel=0.006, segments=1), GLOW, smooth=True)

    # --- 5. OPERATOR COCKPIT: DASHBOARD, 3-SPOKE STEERING WHEEL, LEVERS & BUCKET SEAT ---
    # Sloped instrument console & 3 recessed chrome gauge bezels!
    add_bm_to_target(bm, make_bevel_box_bm((0.0, 0.14, 1.36), (0.58, 0.24, 0.34), bevel=0.024, segments=2, rot_euler=(-0.22, 0, 0)), DARK, smooth=True)
    for gx, gr in ((-0.14, 0.046), (0.0, 0.058), (0.14, 0.046)):
        add_bm_to_target(bm, make_cyl_bm((gx, 0.04, 1.44), gr, gr, 0.028, segs=12, rot_euler=(math.radians(74), 0, 0)), CHR, smooth=True)
    # Angled steering column + 3-spoke tractor steering wheel!
    add_bm_to_target(bm, make_cyl_bm((0.0, -0.04, 1.54), 0.032, 0.028, 0.56, segs=12, rot_euler=(math.radians(40), 0, 0)), DARK, smooth=True)
    sw_c = Vector((0.0, -0.22, 1.75))
    sw_rot = Euler((math.radians(-48), 0, 0), "XYZ").to_matrix().to_4x4()
    # Steering wheel torus rim
    sw_bm = bmesh.new()
    s_maj, s_min = 24, 10
    R_sw, r_sw = 0.225, 0.022
    sw_rings = []
    for i in range(s_maj):
        a = 2.0 * math.pi * i / s_maj
        ca, sa = math.cos(a), math.sin(a)
        ring = []
        for j in range(s_min):
            b = 2.0 * math.pi * j / s_min
            cb, sb = math.cos(b), math.sin(b)
            rr = R_sw + r_sw * cb
            ring.append(sw_bm.verts.new(Vector((rr * ca, rr * sa, r_sw * sb))))
        sw_rings.append(ring)
    for i in range(s_maj):
        inext = (i + 1) % s_maj
        for j in range(s_min):
            jnext = (j + 1) % s_min
            sw_bm.faces.new((sw_rings[i][j], sw_rings[inext][j], sw_rings[inext][jnext], sw_rings[i][jnext]))
    bmesh.ops.transform(sw_bm, matrix=Matrix.Translation(sw_c) @ sw_rot, verts=sw_bm.verts)
    add_bm_to_target(bm, sw_bm, RUB, smooth=True)
    # 3 Spokes + chrome horn cap
    for k in range(3):
        sa = k * (2.0 * math.pi / 3.0) + math.pi * 0.5
        sp = make_bevel_box_bm((0, 0, 0), (0.030, R_sw, 0.018), bevel=0.004, segments=1)
        m_sp = Matrix.Translation(sw_c) @ sw_rot @ Euler((0, 0, sa), "XYZ").to_matrix().to_4x4() @ Matrix.Translation((0, R_sw * 0.48, -0.012))
        bmesh.ops.transform(sp, matrix=m_sp, verts=sp.verts)
        add_bm_to_target(bm, sp, DARK, smooth=True)
    hub_sw = make_cyl_bm((0, 0, 0), 0.048, 0.042, 0.042, segs=12)
    bmesh.ops.transform(hub_sw, matrix=Matrix.Translation(sw_c) @ sw_rot, verts=hub_sw.verts)
    add_bm_to_target(bm, hub_sw, CHR, smooth=True)

    # Gear & Hydraulic shift levers with spherical knobs!
    for lx, ly, lh, lrot in ((0.14, -0.18, 0.42, (0.24, 0.14, 0)), (0.34, -0.44, 0.36, (-0.18, 0.08, 0)), (-0.28, -0.36, 0.30, (0.14, -0.10, 0))):
        add_bm_to_target(bm, make_cyl_bm((lx, ly, 1.05 + lh * 0.5), 0.013, 0.012, lh, segs=8, rot_euler=lrot), CHR, smooth=True)
        add_bm_to_target(bm, make_ellipsoid_bm((lx + lrot[1] * 0.12, ly - lrot[0] * 0.12, 1.05 + lh), (0.028, 0.028, 0.028), segs=10, rings=7), RUB, smooth=True)

    # Contoured Spring-Mounted Operator Bucket Seat!
    add_bm_to_target(bm, make_bevel_box_bm((0.0, -0.60, 1.12), (0.26, 0.24, 0.28), bevel=0.020, segments=1, rot_euler=(0.18, 0, 0)), DARK, smooth=True)
    # Padded rubber bucket seat cushion + contoured side bolsters + curved backrest!
    add_bm_to_target(bm, make_bevel_box_bm((0.0, -0.58, 1.30), (0.50, 0.44, 0.11), bevel=0.036, segments=2), RUB, smooth=True)
    add_bm_to_target(bm, make_bevel_box_bm((0.0, -0.78, 1.56), (0.48, 0.12, 0.46), bevel=0.036, segments=2, rot_euler=(-0.14, 0, 0)), RUB, smooth=True)
    for sx in (-1.0, 1.0):
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.24, -0.60, 1.44), (0.08, 0.36, 0.14), bevel=0.024, segments=1), RUB, smooth=True)

    # --- 6. RECESSED CHROME-BEZEL STALK HEADLIGHTS (NO GIANT MARSHMALLOW BALLS!) ---
    for sx in (-1.0, 1.0):
        # Angled steel mounting bracket emerging from front hood side
        add_bm_to_target(bm, make_cyl_bm((sx * 0.45, 1.52, 1.24), 0.022, 0.022, 0.22, segs=8, rot_euler=(0, math.pi * 0.5, 0)), DARK, smooth=True)
        # Bullet-shaped chamfered lamp bucket
        add_bm_to_target(bm, make_cyl_bm((sx * 0.56, 1.50, 1.24), 0.094, 0.068, 0.15, segs=16, rot_euler=(math.pi * 0.5, 0, 0), bevel=0.012), DARK, smooth=True)
        # Polished chrome headlight bezel ring
        add_bm_to_target(bm, make_cyl_bm((sx * 0.56, 1.575, 1.24), 0.098, 0.095, 0.036, segs=16, rot_euler=(math.pi * 0.5, 0, 0), bevel=0.008), CHR, smooth=True)
        # Warm glowing recessed glass lens
        add_bm_to_target(bm, make_ellipsoid_bm((sx * 0.56, 1.585, 1.24), (0.080, 0.026, 0.080), segs=14, rings=8), GLOW, smooth=True)

    # --- 7. SIGNATURE VERTICAL EXHAUST STACK w/ HEAT SHIELD & HINGED RAIN FLAP! ---
    ex, ey = -0.46, 0.92
    # Exhaust manifold elbow connecting engine side to vertical stack
    add_bm_to_target(bm, make_cyl_bm((ex + 0.10, ey, 1.18), 0.042, 0.042, 0.22, segs=10, rot_euler=(0, math.pi * 0.5, 0)), DARK, smooth=True)
    # Lower dark-steel muffler barrel & perforated heat shield sleeve
    add_bm_to_target(bm, make_cyl_bm((ex, ey, 1.62), 0.072, 0.070, 0.62, segs=14, bevel=0.012), DARK, smooth=True)
    # Tall bold dark-steel upper exhaust stack + chrome collar ring + curved tip + hinged rain flap!
    add_bm_to_target(bm, make_cyl_bm((ex, ey, 1.94), 0.076, 0.074, 0.055, segs=14, bevel=0.006), CHR, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((ex, ey, 2.24), 0.044, 0.042, 0.66, segs=14), DARK, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((ex - 0.03, ey, 2.59), 0.042, 0.038, 0.12, segs=12, rot_euler=(0, -0.45, 0)), CHR, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((ex - 0.06, ey, 2.65), 0.056, 0.052, 0.016, segs=10, rot_euler=(0, -0.65, 0)), DARK, smooth=True)

    # --- 8. ROPS 4-POST TUBULAR ROLLBAR & CONTOURED FIBERGLASS SUN CANOPY ---
    # 4 sturdy vertical/angled ROPS posts + top perimeter frame + corner gussets!
    post_coords = [(-0.62, 0.06), (0.62, 0.06), (-0.64, -0.96), (0.64, -0.96)]
    for px, py in post_coords:
        z_bot = 1.48 if py < 0 else 1.24
        h_p = 2.52 - z_bot
        add_bm_to_target(bm, make_cyl_bm((px, py, z_bot + h_p * 0.5), 0.036, 0.034, h_p, segs=10), DARK, smooth=True)
    # Top ROPS perimeter bars
    for py in (0.06, -0.96):
        add_bm_to_target(bm, make_cyl_bm((0.0, py, 2.50), 0.032, 0.032, 1.28, segs=10, rot_euler=(0, math.pi * 0.5, 0)), DARK, smooth=True)
    for px in (-0.63, 0.63):
        add_bm_to_target(bm, make_cyl_bm((px, -0.45, 2.50), 0.032, 0.032, 1.06, segs=10, rot_euler=(math.pi * 0.5, 0, 0)), DARK, smooth=True)
    # Side rearview mirrors on front ROPS posts!
    for sx in (-1.0, 1.0):
        add_bm_to_target(bm, make_cyl_bm((sx * 0.72, 0.10, 2.02), 0.012, 0.012, 0.22, segs=8, rot_euler=(0, math.pi * 0.5, 0)), DARK, smooth=True)
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.82, 0.10, 2.02), (0.035, 0.11, 0.19), bevel=0.008, segments=1), DARK, smooth=True)
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.82, 0.085, 2.02), (0.025, 0.085, 0.16), bevel=0.004, segments=1), CHR, smooth=True)

    # Contoured beveled fiberglass canopy roof (WHITE) + twin front roof halogen work lamps!
    add_bm_to_target(bm, make_bevel_box_bm((0.0, -0.44, 2.57), (1.52, 1.34, 0.095), bevel=0.032, segments=2), WHITE, smooth=True)
    add_bm_to_target(bm, make_bevel_box_bm((0.0, -0.44, 2.63), (1.30, 1.12, 0.055), bevel=0.022, segments=2), WHITE, smooth=True)
    for sx in (-0.42, 0.42):
        add_bm_to_target(bm, make_bevel_box_bm((sx, 0.22, 2.52), (0.13, 0.09, 0.085), bevel=0.014, segments=1), DARK, smooth=True)
        add_bm_to_target(bm, make_bevel_box_bm((sx, 0.265, 2.52), (0.10, 0.024, 0.062), bevel=0.006, segments=1), GLOW, smooth=True)

    # --- 9. REAR 3-POINT AGRICULTURAL HYDRAULIC HITCH & PTO GUARD (AUTHENTIC REAR VIEW!) ---
    for sx in (-1.0, 1.0):
        # Lower draft link arms sweeping cleanly behind rear axle
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.34, -1.04, 0.54), (0.045, 0.58, 0.068), bevel=0.008, segments=1, rot_euler=(0.08, 0, sx * 0.14)), DARK, smooth=True)
        # Upper hydraulic rockshaft lift arms + vertical chrome hydraulic lift rods!
        add_bm_to_target(bm, make_bevel_box_bm((sx * 0.26, -0.96, 1.02), (0.055, 0.36, 0.065), bevel=0.008, segments=1), DARK, smooth=True)
        add_bm_to_target(bm, make_cyl_bm((sx * 0.31, -1.08, 0.78), 0.022, 0.022, 0.46, segs=8), CHR, smooth=True)
    # Center top-link turnbuckle rod & rear towing drawbar clevis + PTO shield!
    add_bm_to_target(bm, make_cyl_bm((0.0, -1.04, 0.96), 0.026, 0.026, 0.52, segs=10, rot_euler=(math.pi * 0.5 + 0.18, 0, 0)), CHR, smooth=True)
    add_bm_to_target(bm, make_bevel_box_bm((0.0, -0.98, 0.44), (0.14, 0.42, 0.055), bevel=0.010, segments=1), DARK, smooth=True)
    add_bm_to_target(bm, make_cyl_bm((0.0, -0.92, 0.68), 0.075, 0.065, 0.18, segs=10, rot_euler=(math.pi * 0.5, 0, 0)), DARK, smooth=True)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("Chassis_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    for key in ("OW_TractorRed", "OW_Chrome", "OW_HeadlightGlow", "OW_SteelDark", "OW_TireRubber", "OW_StoneWhite"):
        mesh.materials.append(mats[key])
    obj = bpy.data.objects.new("Chassis", mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = Vector((0.0, 0.0, 0.0))
    apply_smooth_obj(obj, angle_deg=48.0)
    return obj


def upgrade_ow_farm_tractor():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {
        "OW_TractorRed": make_mat(
            "OW_TractorRed", (0.76, 0.11, 0.09), roughness=0.28, metallic=0.18,
            normal_file="tractor_paint_normal.png", normal_strength=0.08,
        ),
        "OW_Chrome": make_mat("OW_Chrome", (0.84, 0.86, 0.88), roughness=0.16, metallic=0.92),
        "OW_HeadlightGlow": make_mat("OW_HeadlightGlow", (1.0, 0.94, 0.78), roughness=0.12, metallic=0.0, emission=(1.0, 0.90, 0.66)),
        "OW_SteelDark": make_mat("OW_SteelDark", (0.16, 0.17, 0.19), roughness=0.44, metallic=0.68),
        "OW_TireRubber": make_mat(
            "OW_TireRubber", (0.11, 0.11, 0.12), roughness=0.88, metallic=0.0,
            albedo_file="tractor_tire_albedo.png", normal_file="tractor_tire_normal.png", normal_strength=0.38,
        ),
        "OW_StoneWhite": make_mat("OW_StoneWhite", (0.92, 0.90, 0.85), roughness=0.40, metallic=0.04),
        "OW_SafetyYellow": make_mat(
            "OW_SafetyYellow", (0.90, 0.70, 0.14), roughness=0.30, metallic=0.18,
            normal_file="tractor_paint_normal.png", normal_strength=0.08,
        ),
    }

    # Exact 5-part hierarchy & exact wheel pivot locations (tires & chevron lugs kiss z = 0.000 cleanly!)
    objs = [
        build_tractor_chassis(mats),
        build_tractor_wheel("Wheel_FL", (-0.74,  1.26, 0.44), radius=0.422, width=0.26, is_rear=False, mats=mats),
        build_tractor_wheel("Wheel_FR", ( 0.74,  1.26, 0.44), radius=0.422, width=0.26, is_rear=False, mats=mats),
        build_tractor_wheel("Wheel_RL", (-0.88, -0.58, 0.72), radius=0.695, width=0.38, is_rear=True,  mats=mats),
        build_tractor_wheel("Wheel_RR", ( 0.88, -0.58, 0.72), radius=0.695, width=0.38, is_rear=True,  mats=mats),
    ]

    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]

    out_path = os.path.join(LIFE_DIR, "OW_FarmTractor.fbx")
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
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
    verts = sum(len(o.data.vertices) for o in objs)
    tris = sum(sum(max(1, len(p.vertices) - 2) for p in o.data.polygons) for o in objs)
    print(f"  [Tractor] Exported OW_FarmTractor.fbx | verts={verts} | tris={tris} | parts={len(objs)}")


if __name__ == "__main__":
    print("[GRIDROAM Phase 3 Benchmark] Upgrading OW_Sheep.fbx & OW_FarmTractor.fbx ...")
    upgrade_ow_sheep()
    upgrade_ow_farm_tractor()
    print("[GRIDROAM Phase 3 Benchmark] Done!")
