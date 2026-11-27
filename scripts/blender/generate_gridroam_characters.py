#!/usr/bin/env python3
"""
GRIDROAM High-Quality Stylized-Realism 3D Human Character Generator (Blender 5.3+ Python)
Generates anatomically proportioned, sculpted, smooth-shaded, mobile-optimized FBX character rigs:
  - Character_CasualMale.fbx   (Phase 3 Quality Benchmark Hero: anatomically sculpted face, continuous
                                nasal bridge/tip/alae, recessed eyes w/ eyelids & subtle lash line,
                                smooth Catmull-Rom ears & swept multi-lock hair, tailored open jacket
                                with folded collar, flush lapels, pockets & folds, denim w/ belt loops
                                & open buckle, smooth 5-finger hands, contoured laced sneakers)
  - Character_CasualFemale.fbx / Character_Worker.fbx / Character_Child.fbx / Character_BossKuzgun.fbx

Preserves EXACT 14 part names, EXACT mesh datablock names, and EXACT material slot order for
Unity's Player_GRIDROAM.prefab & ProceduralHumanoid.cs / RealisticCharacterFactory.cs.
"""

import math
import os
import sys
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_FBX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")
RES_CHAR_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldCharacters")
BLEND_OUT_DIR = os.path.join(SCRIPT_DIR, "blend_sources")

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(RES_CHAR_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Math & Smooth Curve Helpers
# ---------------------------------------------------------------------------
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else (hi if v > hi else v)


def smoothstep(e0, e1, x):
    t = clamp((x - e0) / max(1e-6, e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def gauss(dx, sx):
    t = dx / max(1e-6, sx)
    return math.exp(-0.5 * t * t)


def resample_catmull_rom(pts_radii, samples_per_seg=3):
    """Smoothly interpolate a control polyline of (Vector, rx, ry) via Catmull-Rom splines."""
    n = len(pts_radii)
    if n < 2 or samples_per_seg <= 1:
        return [(Vector(p), rx, ry) for (p, rx, ry) in pts_radii]

    def cr(p0, p1, p2, p3, t):
        t2 = t * t
        t3 = t2 * t
        return 0.5 * (
            (2.0 * p1)
            + (-p0 + p2) * t
            + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
            + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3
        )

    pts = [Vector(item[0]) for item in pts_radii]
    rxs = [float(item[1]) for item in pts_radii]
    rys = [float(item[2]) for item in pts_radii]

    out = []
    for i in range(n - 1):
        p0 = pts[max(0, i - 1)]
        p1 = pts[i]
        p2 = pts[i + 1]
        p3 = pts[min(n - 1, i + 2)]
        rx0, rx1 = rxs[i], rxs[i + 1]
        ry0, ry1 = rys[i], rys[i + 1]
        steps = samples_per_seg
        for k in range(steps):
            t = k / steps
            p = cr(p0, p1, p2, p3, t)
            rx = rx0 + (rx1 - rx0) * t
            ry = ry0 + (ry1 - ry0) * t
            out.append((p, rx, ry))
    out.append((pts[-1], rxs[-1], rys[-1]))
    return out


def make_mat(name, color, roughness=0.55, metallic=0.0):
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


def get_character_materials():
    return {
        "CharSkin": make_mat("CharSkin", (0.83, 0.63, 0.50), roughness=0.52, metallic=0.0),
        "CharShirt": make_mat("CharShirt", (0.17, 0.30, 0.46), roughness=0.78, metallic=0.0),
        "CharUndershirt": make_mat("CharUndershirt", (0.91, 0.89, 0.85), roughness=0.82, metallic=0.0),
        "CharPants": make_mat("CharPants", (0.15, 0.21, 0.30), roughness=0.84, metallic=0.0),
        "CharBelt": make_mat("CharBelt", (0.19, 0.11, 0.07), roughness=0.45, metallic=0.0),
        "CharBuckle": make_mat("CharBuckle", (0.82, 0.84, 0.86), roughness=0.22, metallic=0.88),
        "CharShoes": make_mat("CharShoes", (0.14, 0.14, 0.16), roughness=0.58, metallic=0.0),
        "CharShoeSole": make_mat("CharShoeSole", (0.92, 0.91, 0.88), roughness=0.68, metallic=0.0),
        "CharHair": make_mat("CharHair", (0.12, 0.08, 0.055), roughness=0.56, metallic=0.0),
        "CharEyeWhite": make_mat("CharEyeWhite", (0.95, 0.94, 0.92), roughness=0.20, metallic=0.0),
        "CharEyeIris": make_mat("CharEyeIris", (0.09, 0.06, 0.045), roughness=0.10, metallic=0.05),
        "CharLips": make_mat("CharLips", (0.68, 0.41, 0.36), roughness=0.45, metallic=0.0),
    }


def apply_smooth(obj, angle_deg=60.0):
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


def box_uv(obj, scale=2.4):
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


def apply_subsurf(obj, levels=1, smooth_angle=70.0):
    mod = obj.modifiers.new(name="Subsurf", type="SUBSURF")
    mod.levels = levels
    mod.render_levels = levels
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    box_uv(obj, scale=2.4)
    apply_smooth(obj, angle_deg=smooth_angle)
    return obj


def make_obj_from_bm(name, bm, mat, smooth_angle=62.0, subsurf_levels=0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if mat is not None:
        obj.data.materials.append(mat)
    box_uv(obj, scale=2.4)
    apply_smooth(obj, angle_deg=smooth_angle)
    if subsurf_levels > 0:
        apply_subsurf(obj, levels=subsurf_levels, smooth_angle=smooth_angle)
    return obj


def loft_oval_rings(
    name,
    ring_specs,
    segments,
    mat,
    cap_bottom=True,
    cap_top=True,
    smooth_angle=68.0,
    subsurf_levels=0,
    deform_fn=None,
):
    """Loft an organic body/garment segment from oval cross-section specs."""
    bm = bmesh.new()
    rings = []
    for i, (z, rx, ry_f, ry_b, y_off, x_off) in enumerate(ring_specs):
        ring = []
        for s in range(segments):
            ang = (2.0 * math.pi * s) / segments
            ca = math.cos(ang)
            sa = math.sin(ang)
            ry = ry_f if sa >= 0.0 else ry_b
            co = Vector((x_off + rx * ca, y_off + ry * sa, z))
            if deform_fn is not None:
                co = deform_fn(i, s, ang, co)
            ring.append(bm.verts.new(co))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))

    if cap_bottom:
        c_bot = bm.verts.new(Vector((ring_specs[0][5], ring_specs[0][4], ring_specs[0][0] - 0.002)))
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((c_bot, rings[0][sn], rings[0][s]))
    if cap_top:
        c_top = bm.verts.new(Vector((ring_specs[-1][5], ring_specs[-1][4], ring_specs[-1][0] + 0.002)))
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((c_top, rings[-1][s], rings[-1][sn]))

    return make_obj_from_bm(name, bm, mat, smooth_angle=smooth_angle, subsurf_levels=subsurf_levels)


def loft_tube_along_path(
    name,
    pts_radii,
    segments,
    mat,
    cap_start=True,
    cap_end=True,
    rounded_caps=True,
    spline_samples=1,
    smooth_angle=70.0,
    subsurf_levels=0,
):
    """
    Loft a smooth organic tube along a 3D centerline with optional Catmull-Rom smoothing
    and 2-ring hemispherical rounded end caps (so fingertips & locks NEVER look like pointed spikes!).
    """
    if spline_samples > 1:
        pts_radii = resample_catmull_rom(pts_radii, samples_per_seg=spline_samples)

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

    def build_cap(frame, base_ring, flip_dir):
        p, tan, side, up, rx, ry = frame
        sign = -1.0 if flip_dir else 1.0
        r_avg = 0.5 * (rx + ry)
        if rounded_caps:
            # Smooth blunt-rounded anatomical fingertip / lock dome (NEVER a pointy cone!)
            domes = [(0.30, 0.88), (0.54, 0.56)]
            prev_r = base_ring
            for (d_frac, r_frac) in domes:
                nr = []
                cp = p + tan * (sign * r_avg * d_frac)
                for s in range(segments):
                    ang = (2.0 * math.pi * s) / segments
                    co = cp + side * (math.cos(ang) * rx * r_frac) + up * (math.sin(ang) * ry * r_frac)
                    nr.append(bm.verts.new(co))
                for s in range(segments):
                    sn = (s + 1) % segments
                    if flip_dir:
                        bm.faces.new((nr[s], nr[sn], prev_r[sn], prev_r[s]))
                    else:
                        bm.faces.new((prev_r[s], prev_r[sn], nr[sn], nr[s]))
                prev_r = nr
            apex = bm.verts.new(p + tan * (sign * r_avg * 0.68))
            for s in range(segments):
                sn = (s + 1) % segments
                if flip_dir:
                    bm.faces.new((apex, prev_r[sn], prev_r[s]))
                else:
                    bm.faces.new((apex, prev_r[s], prev_r[sn]))
        else:
            apex = bm.verts.new(p + tan * (sign * r_avg * 0.25))
            for s in range(segments):
                sn = (s + 1) % segments
                if flip_dir:
                    bm.faces.new((apex, base_ring[sn], base_ring[s]))
                else:
                    bm.faces.new((apex, base_ring[s], base_ring[sn]))

    if cap_start:
        build_cap(frames[0], rings[0], flip_dir=True)
    if cap_end:
        build_cap(frames[-1], rings[-1], flip_dir=False)

    return make_obj_from_bm(name, bm, mat, smooth_angle=smooth_angle, subsurf_levels=subsurf_levels)


def add_ellipsoid(name, center, radii, mat, segs=12, rings=8, rot_euler=(0.0, 0.0, 0.0), smooth_angle=68.0):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return make_obj_from_bm(name, bm, mat, smooth_angle=smooth_angle)


def add_bevel_box(name, center, size, mat, bevel=0.005, segments=1, rot_euler=(0.0, 0.0, 0.0), smooth_angle=48.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.1:
        bmesh.ops.bevel(
            bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET",
            segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True,
        )
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return make_obj_from_bm(name, bm, mat, smooth_angle=smooth_angle)


def add_curved_patch(name, z0, z1, ang0_deg, ang1_deg, rx, ry_f, ry_b, y_off, thick, mat, u_segs=4, v_segs=3, z_tilt=0.0):
    """Build a smoothly curved pocket/flap/belt-loop panel hugging an elliptical garment surface."""
    bm = bmesh.new()
    out_grid, in_grid = [], []
    for iv in range(v_segs + 1):
        tv = iv / v_segs
        z = z0 + (z1 - z0) * tv
        r_boost = z_tilt * (tv - 0.5)
        ro, ri = [], []
        for iu in range(u_segs + 1):
            tu = iu / u_segs
            ang = math.radians(ang0_deg + (ang1_deg - ang0_deg) * tu)
            ca, sa = math.cos(ang), math.sin(ang)
            ry = ry_f if sa >= 0.0 else ry_b
            edge_soft = math.sin(tu * math.pi) * math.sin(tv * math.pi)
            t_loc = thick * (0.45 + 0.55 * math.sqrt(max(0.0, edge_soft)))
            ro.append(bm.verts.new(Vector(((rx + r_boost + t_loc) * ca, y_off + (ry + r_boost + t_loc) * sa, z))))
            ri.append(bm.verts.new(Vector(((rx + r_boost - 0.002) * ca, y_off + (ry + r_boost - 0.002) * sa, z))))
        out_grid.append(ro)
        in_grid.append(ri)

    for iv in range(v_segs):
        for iu in range(u_segs):
            bm.faces.new((out_grid[iv][iu], out_grid[iv][iu + 1], out_grid[iv + 1][iu + 1], out_grid[iv + 1][iu]))
            bm.faces.new((in_grid[iv][iu + 1], in_grid[iv][iu], in_grid[iv + 1][iu], in_grid[iv + 1][iu + 1]))
    for iu in range(u_segs):
        bm.faces.new((in_grid[0][iu], in_grid[0][iu + 1], out_grid[0][iu + 1], out_grid[0][iu]))
        bm.faces.new((out_grid[-1][iu], out_grid[-1][iu + 1], in_grid[-1][iu + 1], in_grid[-1][iu]))
    for iv in range(v_segs):
        bm.faces.new((in_grid[iv][0], out_grid[iv][0], out_grid[iv + 1][0], in_grid[iv + 1][0]))
        bm.faces.new((out_grid[iv][-1], in_grid[iv][-1], in_grid[iv + 1][-1], out_grid[iv + 1][-1]))

    return make_obj_from_bm(name, bm, mat, smooth_angle=56.0, subsurf_levels=0)


def join_parts_at_origin(parts, final_name, smooth_angle=None, exact_mesh_name=None):
    bpy.ops.object.select_all(action="DESELECT")
    valid = [p for p in parts if p is not None]
    for p in valid:
        p.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = final_name
    if exact_mesh_name is not None:
        joined.data.name = exact_mesh_name
    joined.location = Vector((0.0, 0.0, 0.0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if smooth_angle is not None:
        apply_smooth(joined, angle_deg=smooth_angle)
    return joined


def make_pivot(name, parent, local_pos):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_size = 0.08
    bpy.context.collection.objects.link(empty)
    if parent is not None:
        empty.parent = parent
    empty.location = Vector(local_pos)
    return empty


def attach_mesh_to_pivot(mesh_obj, pivot_obj):
    mesh_obj.parent = pivot_obj
    mesh_obj.location = Vector((0.0, 0.0, 0.0))
    mesh_obj.rotation_euler = Euler((0.0, 0.0, 0.0), "XYZ")
    mesh_obj.scale = Vector((1.0, 1.0, 1.0))


# ---------------------------------------------------------------------------
# 1. Pelvis, Denim Tailoring, Belt Loops & Open Chamfered Buckle
#    Exact slots: ['CharPants', 'CharBelt', 'CharBuckle']
# ---------------------------------------------------------------------------
def build_pelvis_mesh(mats, is_female=False):
    parts = []
    hip_w = 0.178 if is_female else 0.171
    waist_w = 0.138 if is_female else 0.153

    def pelvis_deform(i, s, ang, v):
        ca, sa = math.cos(ang), math.sin(ang)
        if sa < -0.55 and -0.082 <= v.z <= 0.025:
            cleft = gauss(v.x, 0.026) * smoothstep(-0.55, -0.95, sa)
            glute = (1.0 - cleft * 0.85) * gauss(abs(v.x) - 0.068, 0.044)
            v.y += cleft * 0.010 - glute * 0.007
        if sa > 0.70 and -0.055 <= v.z <= 0.048:
            v.y += gauss(v.x - 0.004, 0.011) * 0.0032
        return v

    specs = [
        (-0.114, hip_w * 0.46, 0.064, 0.078, -0.004, 0.0),
        (-0.090, hip_w * 0.82, 0.098, 0.118, -0.004, 0.0),
        (-0.044, hip_w * 0.99, 0.116, 0.136, -0.004, 0.0),
        (0.008,  hip_w * 0.97, 0.115, 0.128, -0.002, 0.0),
        (0.046,  waist_w + 0.010, 0.110, 0.118, 0.0, 0.0),
        (0.084,  waist_w, 0.105, 0.110, 0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Pelvis_Jeans", specs, 24, mats["CharPants"], smooth_angle=70.0, subsurf_levels=0, deform_fn=pelvis_deform))

    # Flush contoured back denim patch pockets (hugging seat contour tightly — NO boxy shelf!)
    parts.append(add_curved_patch("Pelvis_BackPocket_L", -0.056, 0.016, 228.0, 260.0, hip_w * 0.95, 0.114, 0.131, -0.003, 0.0035, mats["CharPants"], u_segs=4, v_segs=3, z_tilt=-0.010))
    parts.append(add_curved_patch("Pelvis_BackPocket_R", -0.056, 0.016, 280.0, 312.0, hip_w * 0.95, 0.114, 0.131, -0.003, 0.0035, mats["CharPants"], u_segs=4, v_segs=3, z_tilt=-0.010))

    # 5 Raised Denim Belt Loops straddling the leather belt (CharPants — slot 0)
    for idx, loop_deg in enumerate((58.0, 122.0, 210.0, 270.0, 330.0)):
        parts.append(add_curved_patch(f"Pelvis_BeltLoop_{idx}", 0.044, 0.082, loop_deg - 3.6, loop_deg + 3.6, waist_w + 0.013, 0.118, 0.122, 0.0, 0.0045, mats["CharPants"], u_segs=2, v_segs=2))

    # Leather Belt with clean thickness (CharBelt — slot 1)
    bm_b = bmesh.new()
    segs = 24
    rz_bot, rz_top = 0.047, 0.079
    r_in_x, r_out_x = waist_w + 0.003, waist_w + 0.012
    r_in_yf, r_out_yf = 0.109, 0.118
    r_in_yb, r_out_yb = 0.113, 0.122
    ring_bo, ring_to, ring_bi, ring_ti = [], [], [], []
    for s in range(segs):
        ang = (2.0 * math.pi * s) / segs
        ca, sa = math.cos(ang), math.sin(ang)
        ryo = r_out_yf if sa >= 0 else r_out_yb
        ryi = r_in_yf if sa >= 0 else r_in_yb
        ring_bo.append(bm_b.verts.new(Vector((r_out_x * ca, ryo * sa, rz_bot))))
        ring_to.append(bm_b.verts.new(Vector((r_out_x * ca, ryo * sa, rz_top))))
        ring_bi.append(bm_b.verts.new(Vector((r_in_x * ca, ryi * sa, rz_bot))))
        ring_ti.append(bm_b.verts.new(Vector((r_in_x * ca, ryi * sa, rz_top))))
    for s in range(segs):
        sn = (s + 1) % segs
        bm_b.faces.new((ring_bo[s], ring_bo[sn], ring_to[sn], ring_to[s]))
        bm_b.faces.new((ring_ti[s], ring_ti[sn], ring_to[sn], ring_to[s]))
        bm_b.faces.new((ring_bo[s], ring_bi[s], ring_bi[sn], ring_bo[sn]))
    parts.append(make_obj_from_bm("Pelvis_Belt", bm_b, mats["CharBelt"], smooth_angle=46.0))
    parts.append(add_bevel_box("Belt_Keeper", (-0.036, 0.116, 0.063), (0.011, 0.014, 0.036), mats["CharBelt"], bevel=0.0025, segments=1))

    # Open-Frame Chamfered Metallic Buckle + Prong (CharBuckle — slot 2)
    by, bz = 0.120, 0.063
    parts.append(add_bevel_box("Buckle_Top", (0.0, by, bz + 0.016), (0.044, 0.0085, 0.0065), mats["CharBuckle"], bevel=0.0018, segments=1))
    parts.append(add_bevel_box("Buckle_Bot", (0.0, by, bz - 0.016), (0.044, 0.0085, 0.0065), mats["CharBuckle"], bevel=0.0018, segments=1))
    parts.append(add_bevel_box("Buckle_L",   (-0.019, by, bz), (0.0065, 0.0085, 0.038), mats["CharBuckle"], bevel=0.0018, segments=1))
    parts.append(add_bevel_box("Buckle_R",   ( 0.019, by, bz), (0.0065, 0.0085, 0.038), mats["CharBuckle"], bevel=0.0018, segments=1))
    parts.append(add_bevel_box("Buckle_Prong", (0.004, by + 0.002, bz), (0.025, 0.0048, 0.0042), mats["CharBuckle"], bevel=0.0012, segments=1))

    return join_parts_at_origin(parts, "Pelvis_Mesh", exact_mesh_name="Pelvis_Jeans_Mesh")


# ---------------------------------------------------------------------------
# 2. Tailored Open Jacket (Seamless Shoulders, Folded Collar, Flush Lapels & Pockets)
#    Exact slots for male: ['CharUndershirt', 'CharShirt']
# ---------------------------------------------------------------------------
def build_torso_mesh(mats, variant="male"):
    parts = []
    is_female = variant in ("female", "elder_female")
    is_worker = (variant == "worker")
    is_child = (variant == "child")
    is_boss = (variant == "boss")
    is_elder_m = (variant == "elder_male")
    is_elder_f = (variant == "elder_female")
    is_sporty = (variant == "sporty")
    is_shop = (variant == "shopkeeper")

    def torso_profile(inflate):
        if is_female or is_child:
            f_mul = 1.04 if is_elder_f else 1.0
            base = [
                (-0.050, 0.150 * f_mul, 0.118 * f_mul, 0.124 * f_mul, -0.002),
                (0.040,  0.130 * f_mul, 0.110 * f_mul, 0.114 * f_mul,  0.000),
                (0.140,  0.142 * f_mul, 0.120 * f_mul, 0.118 * f_mul,  0.003),
                (0.230,  (0.156 if is_child else 0.162) * f_mul, (0.130 if is_child else 0.148) * f_mul, 0.120 * f_mul, 0.006 if is_child else 0.008),
                (0.300,  (0.164 if is_child else 0.169) * f_mul, (0.132 if is_child else 0.150) * f_mul, 0.124 * f_mul, 0.006 if is_child else 0.008),
                (0.350,  (0.174 if is_child else 0.176) * f_mul, (0.124 if is_child else 0.128) * f_mul, 0.122 * f_mul, 0.004),
                (0.392,  (0.170 if is_child else 0.172) * f_mul, 0.108 * f_mul, 0.110 * f_mul, 0.002),
                (0.425,  0.132 * f_mul, 0.090 * f_mul, 0.096 * f_mul,  0.000),
                (0.455,  0.094 * f_mul, 0.078 * f_mul, 0.084 * f_mul, -0.002),
                (0.480,  0.068 * f_mul, 0.066 * f_mul, 0.072 * f_mul, -0.003),
            ]
        else:
            b_mul = 1.07 if is_boss else (1.04 if is_elder_m else (0.97 if is_sporty else 1.0))
            w_bulge = 1.07 if is_elder_m else (0.95 if is_sporty else 1.0)
            base = [
                (-0.052, 0.160 * b_mul * w_bulge, 0.120 * b_mul * w_bulge, 0.126 * b_mul, -0.002),
                (0.015,  0.151 * b_mul * w_bulge, 0.116 * b_mul * w_bulge, 0.120 * b_mul,  0.000),
                (0.090,  0.156 * b_mul * w_bulge, 0.122 * b_mul * w_bulge, 0.122 * b_mul,  0.002),
                (0.170,  0.173 * b_mul,           0.132 * b_mul * w_bulge, 0.126 * b_mul,  0.004),
                (0.250,  0.193 * b_mul,           0.145 * b_mul,           0.133 * b_mul,  0.007),
                (0.315,  0.205 * b_mul,           0.148 * b_mul,           0.138 * b_mul,  0.007),
                (0.362,  0.208 * b_mul,           0.135 * b_mul,           0.132 * b_mul,  0.004),
                (0.398,  0.194 * b_mul,           0.116 * b_mul,           0.118 * b_mul,  0.001),
                (0.428,  0.146 * b_mul,           0.096 * b_mul,           0.102 * b_mul, -0.001),
                (0.456,  0.102 * b_mul,           0.082 * b_mul,           0.088 * b_mul, -0.003),
                (0.478,  0.074 * b_mul,           0.072 * b_mul,           0.078 * b_mul, -0.004),
            ]
        return [(z, rx + inflate, f + inflate, b + inflate, y, 0.0) for (z, rx, f, b, y) in base]

    # Inner Crew-Neck T-Shirt / Core Shell (Slot 0)
    def inner_deform(i, s, ang, v):
        ca, sa = math.cos(ang), math.sin(ang)
        if sa > 0.35:
            pec = gauss(abs(v.x) - 0.070, 0.048) * gauss(v.z - 0.305, 0.055)
            stern = gauss(v.x, 0.024) * gauss(v.z - 0.305, 0.060)
            fold = 0.0026 * math.sin(v.x * 44.0 + v.z * 8.0) * smoothstep(0.24, 0.02, v.z)
            v.y += (pec * 0.006 - stern * 0.003 + fold) * sa
        return v

    inner_specs = torso_profile(-0.011)
    inner_mat = mats["CharShirt"] if (is_worker or is_child or is_sporty or is_shop) else mats["CharUndershirt"]
    parts.append(loft_oval_rings("Torso_Inner", inner_specs, 24, inner_mat, smooth_angle=68.0, subsurf_levels=0, deform_fn=inner_deform))
    chest_f = inner_specs[5][2] + 0.011

    if not (is_worker or is_child or is_sporty or is_shop):
        crew_specs = [
            (0.436, 0.085, 0.082, 0.080, -0.001, 0.0),
            (0.450, 0.083, 0.083, 0.081, -0.001, 0.0),
            (0.460, 0.075, 0.075, 0.077, -0.002, 0.0),
        ]
        parts.append(loft_oval_rings("Torso_CrewNeckRim", crew_specs, 20, mats["CharUndershirt"], cap_bottom=False, cap_top=False, smooth_angle=52.0, subsurf_levels=0))

    # Main Outer Garment per Archetype (preserving exact slot order on existing 5 FBXs!)
    if is_child:
        # Cozy Pullover Hoodie w/ ribbed waist hem, curved kangaroo pouch, resting hood & contoured school backpack!
        # Slot order: ['CharShirt', 'CharBelt', 'CharPants', 'CharBuckle']
        hoodie_specs = torso_profile(0.012)
        parts.append(loft_oval_rings("Torso_ChildHoodie", hoodie_specs, 22, mats["CharShirt"], smooth_angle=68.0, subsurf_levels=0))
        parts.append(add_curved_patch("Hoodie_KangarooPocket", 0.030, 0.145, 45.0, 135.0, 0.162, 0.132, 0.128, 0.002, 0.012, mats["CharShirt"], u_segs=6, v_segs=3))
        # Soft contoured resting hood draped behind neck
        hood_pts = [
            (Vector((-0.075, 0.045, 0.435)), 0.022, 0.016),
            (Vector((-0.095, -0.055, 0.430)), 0.036, 0.028),
            (Vector(( 0.000, -0.115, 0.415)), 0.052, 0.040),
            (Vector(( 0.095, -0.055, 0.430)), 0.036, 0.028),
            (Vector(( 0.075, 0.045, 0.435)), 0.022, 0.016),
        ]
        parts.append(loft_tube_along_path("Hoodie_RestingHood", hood_pts, 10, mats["CharShirt"], rounded_caps=True, spline_samples=2, smooth_angle=70.0))
        # Contoured School Backpack (CharBelt -> CharPants -> CharBuckle)
        parts.append(add_bevel_box("Child_BackpackMain", (0.0, -0.185, 0.255), (0.225, 0.112, 0.295), mats["CharBelt"], bevel=0.032, segments=2))
        for sx in (-1.0, 1.0):
            parts.append(add_bevel_box(f"Child_StrapTop_{sx}", (sx * 0.098, -0.018, 0.415), (0.030, 0.205, 0.014), mats["CharBelt"], bevel=0.005, segments=1, rot_euler=(0.14, 0.0, 0.0)))
            parts.append(add_bevel_box(f"Child_StrapFront_{sx}", (sx * 0.098, chest_f - 0.004, 0.268), (0.026, 0.012, 0.250), mats["CharBelt"], bevel=0.004, segments=1, rot_euler=(-0.08, 0.0, 0.0)))
        parts.append(add_bevel_box("Child_BackpackFrontPouch", (0.0, -0.244, 0.182), (0.175, 0.044, 0.138), mats["CharPants"], bevel=0.016, segments=1))
        parts.append(add_bevel_box("Child_BackpackZipper", (0.0, -0.265, 0.236), (0.155, 0.008, 0.009), mats["CharBuckle"], bevel=0.003, segments=1))
    elif is_worker:
        # Rugged Collared Utility Work Jacket w/ folded collar, front zip placket & dual flap chest pockets!
        # Slot order: ['CharShirt', 'CharBelt', 'CharPants']
        jacket_specs = torso_profile(0.012)
        parts.append(loft_oval_rings("Torso_WorkJacket", jacket_specs, 24, mats["CharShirt"], smooth_angle=68.0, subsurf_levels=0))
        # Turned-down work jacket collar (CharShirt)
        wcol_pts = [
            (Vector((-0.056, 0.088, 0.445)), 0.018, 0.014),
            (Vector((-0.098, 0.000, 0.464)), 0.024, 0.018),
            (Vector(( 0.000, -0.104, 0.468)), 0.028, 0.020),
            (Vector(( 0.098, 0.000, 0.464)), 0.024, 0.018),
            (Vector(( 0.056, 0.088, 0.445)), 0.018, 0.014),
        ]
        parts.append(loft_tube_along_path("Work_FoldedCollar", wcol_pts, 8, mats["CharShirt"], rounded_caps=True, spline_samples=2, smooth_angle=66.0))
        for sx in (-1.0, 1.0):
            a_p0 = 42.0 if sx > 0 else 112.0
            a_p1 = 68.0 if sx > 0 else 138.0
            parts.append(add_curved_patch(f"Work_Pocket_{sx}", 0.242, 0.314, a_p0, a_p1, 0.205, 0.160, 0.146, 0.007, 0.007, mats["CharShirt"], u_segs=4, v_segs=2))
        parts.append(add_bevel_box("Work_ZipPlacket", (0.0, chest_f + 0.011, 0.205), (0.022, 0.012, 0.42), mats["CharBelt"], bevel=0.004, segments=1))
        for sx in (-1.0, 1.0):
            a_p0 = 40.5 if sx > 0 else 110.5
            a_p1 = 69.5 if sx > 0 else 139.5
            parts.append(add_curved_patch(f"Work_PocketFlap_{sx}", 0.298, 0.324, a_p0, a_p1, 0.209, 0.164, 0.148, 0.007, 0.008, mats["CharPants"], u_segs=4, v_segs=2))
    elif is_sporty:
        # Athletic Zip Track Jacket w/ raised stand-up athletic collar & contrast chest chevron stripe!
        sp_specs = torso_profile(0.011)
        parts.append(loft_oval_rings("Torso_TrackJacket", sp_specs, 24, mats["CharShirt"], smooth_angle=68.0, subsurf_levels=0))
        # Raised athletic stand-up zip collar
        scol = [
            (0.452, 0.094, 0.088, 0.092, -0.004, 0.0),
            (0.498, 0.088, 0.084, 0.090, -0.005, 0.0),
        ]
        parts.append(loft_oval_rings("Sporty_StandCollar", scol, 20, mats["CharShirt"], cap_bottom=False, cap_top=True, smooth_angle=58.0))
        # Crisp white athletic chest band & front zip track
        parts.append(add_curved_patch("Sporty_ChestStripe", 0.255, 0.305, 25.0, 155.0, 0.206, 0.158, 0.145, 0.006, 0.006, mats["CharUndershirt"], u_segs=10, v_segs=2))
        parts.append(add_bevel_box("Sporty_ZipLine", (0.0, chest_f + 0.011, 0.215), (0.012, 0.009, 0.44), mats["CharUndershirt"], bevel=0.003, segments=1))
    elif is_shop:
        # Turkish Esnaf / Shopkeeper: Collared shirt + sculpted full-bib shopkeeper apron w/ front utility pocket & waist ties!
        sh_specs = torso_profile(0.008)
        parts.append(loft_oval_rings("Torso_ShopShirt", sh_specs, 24, mats["CharShirt"], smooth_angle=68.0, subsurf_levels=0))
        scol_pts = [
            (Vector((-0.055, 0.086, 0.444)), 0.017, 0.013),
            (Vector((-0.095, 0.000, 0.462)), 0.022, 0.016),
            (Vector(( 0.000, -0.100, 0.465)), 0.026, 0.018),
            (Vector(( 0.095, 0.000, 0.462)), 0.022, 0.016),
            (Vector(( 0.055, 0.086, 0.444)), 0.017, 0.013),
        ]
        parts.append(loft_tube_along_path("Shop_ShirtCollar", scol_pts, 8, mats["CharShirt"], rounded_caps=True, spline_samples=2, smooth_angle=66.0))
        # Full-bib Esnaf Apron draping cleanly down past waist over front + neck loop & waist band!
        parts.append(add_curved_patch("Shop_ApronBib",   0.145, 0.360, 52.0, 128.0, 0.204, 0.160, 0.144, 0.007, 0.009, mats["CharPants"], u_segs=6, v_segs=4))
        parts.append(add_curved_patch("Shop_ApronSkirt", -0.105, 0.155, 22.0, 158.0, 0.180, 0.142, 0.134, 0.001, 0.010, mats["CharPants"], u_segs=10, v_segs=4))
        parts.append(add_curved_patch("Shop_ApronPocket", -0.015, 0.085, 58.0, 122.0, 0.185, 0.150, 0.136, 0.001, 0.008, mats["CharBelt"], u_segs=5, v_segs=2))
        # Neck apron strap & rear tie belt
        apron_neck = [
            (Vector((-0.072, 0.118, 0.355)), 0.009, 0.005),
            (Vector((-0.088, 0.010, 0.458)), 0.009, 0.005),
            (Vector(( 0.000, -0.095, 0.468)), 0.009, 0.005),
            (Vector(( 0.088, 0.010, 0.458)), 0.009, 0.005),
            (Vector(( 0.072, 0.118, 0.355)), 0.009, 0.005),
        ]
        parts.append(loft_tube_along_path("Shop_ApronNeckStrap", apron_neck, 6, mats["CharBelt"], rounded_caps=True, spline_samples=2, smooth_angle=62.0))
        parts.append(add_curved_patch("Shop_ApronWaistTie", 0.115, 0.155, 160.0, 380.0, 0.172, 0.134, 0.135, 0.001, 0.008, mats["CharBelt"], u_segs=12, v_segs=2))
    else:
        # Open-front stylized-realistic Jacket / Coat / Cardigan / Boss Trenchcoat!
        bm = bmesh.new()
        segs = 28
        ring_specs = torso_profile(0.015 if is_boss else 0.013)
        if is_boss:
            hem_low = (-0.165, ring_specs[0][1] + 0.034, ring_specs[0][2] + 0.022, ring_specs[0][3] + 0.036, -0.006, 0.0)
            ring_specs = [hem_low] + ring_specs
        elif is_elder_f or is_female:
            # Graceful mid-hip tailored coat / cardigan drape
            hem_f = (-0.110 if is_elder_f else -0.085, ring_specs[0][1] + 0.022, ring_specs[0][2] + 0.016, ring_specs[0][3] + 0.024, -0.004, 0.0)
            ring_specs = [hem_f] + ring_specs
        else:
            ring_specs[0] = (ring_specs[0][0] - 0.004, ring_specs[0][1] + 0.004, ring_specs[0][2] + 0.004, ring_specs[0][3] + 0.004, ring_specs[0][4], 0.0)
            ring_specs[1] = (ring_specs[1][0], ring_specs[1][1] + 0.010, ring_specs[1][2] + 0.008, ring_specs[1][3] + 0.010, ring_specs[1][4], 0.0)

        out_rings, in_rings = [], []
        thick = 0.016 if is_boss else 0.014
        for idx_r, (z, rx, ry_f, ry_b, y_off, x_off) in enumerate(ring_specs):
            open_half_deg = (13.0 if is_elder_m else 17.5) + smoothstep(0.28, 0.47, z) * (20.0 if is_elder_m else 16.0)
            a_start = 90.0 + open_half_deg
            a_span = 360.0 - 2.0 * open_half_deg
            ro, ri = [], []
            for s in range(segs):
                u = s / (segs - 1)
                ang = math.radians(a_start + u * a_span)
                ca, sa = math.cos(ang), math.sin(ang)
                ry = ry_f if sa >= 0.0 else ry_b
                back_drape = 0.0042 * math.sin(ang * 5.0 + z * 4.2) * smoothstep(0.0, -0.85, sa) * smoothstep(0.38, 0.04, z)
                waist_crease = 0.0030 * math.sin(z * 52.0 + abs(ca) * 3.0) * gauss(z - 0.042, 0.052)
                spine_seam = -0.0042 * gauss(ca, 0.08) * smoothstep(-0.6, -0.98, sa) * smoothstep(0.44, 0.10, z)
                hem_step = -0.0035 if (variant == "male" and idx_r == 0) else 0.0
                lapel_edge = (gauss(u, 0.045) + gauss(1.0 - u, 0.045)) * (0.0050 + 0.0060 * smoothstep(0.22, 0.41, z))
                d_rad = back_drape + waist_crease + spine_seam + hem_step + lapel_edge
                ro.append(bm.verts.new(Vector((x_off + (rx + d_rad) * ca, y_off + (ry + d_rad) * sa, z))))
                ri.append(bm.verts.new(Vector((x_off + (rx - thick) * ca, y_off + (ry - thick) * sa, z))))
            out_rings.append(ro)
            in_rings.append(ri)

        for i in range(len(out_rings) - 1):
            for s in range(segs - 1):
                bm.faces.new((out_rings[i][s], out_rings[i][s + 1], out_rings[i + 1][s + 1], out_rings[i + 1][s]))
                bm.faces.new((in_rings[i][s + 1], in_rings[i][s], in_rings[i + 1][s], in_rings[i + 1][s + 1]))
            bm.faces.new((in_rings[i][0], out_rings[i][0], out_rings[i + 1][0], in_rings[i + 1][0]))
            bm.faces.new((out_rings[i][-1], in_rings[i][-1], in_rings[i + 1][-1], out_rings[i + 1][-1]))
        for s in range(segs - 1):
            bm.faces.new((in_rings[0][s], in_rings[0][s + 1], out_rings[0][s + 1], out_rings[0][s]))
            bm.faces.new((out_rings[-1][s], out_rings[-1][s + 1], in_rings[-1][s + 1], in_rings[-1][s]))

        parts.append(make_obj_from_bm("Torso_OpenJacket", bm, mats["CharShirt"], smooth_angle=66.0, subsurf_levels=0))

        # REAL TURNED-DOWN FOLDED COLLAR (or warm Draped Shawl Collar for ElderFemale!)
        bm_c = bmesh.new()
        c_segs = 24
        c_prof = [
            (0.448, 0.088, 0.082, 0.088, -0.005),
            (0.484, 0.090, 0.084, 0.094, -0.008),
            (0.506 if is_boss else 0.492, 0.104, 0.094, 0.106, -0.010),
            (0.478 if is_boss else 0.464, 0.128, 0.108, 0.124, -0.011),
            (0.450 if is_boss else 0.440, 0.135, 0.114, 0.130, -0.011),
            (0.444 if is_boss else 0.438, 0.120, 0.102, 0.116, -0.010),
        ]
        c_rings = []
        a0_c, a1_c = 122.0, 418.0
        for z, rx, ryf, ryb, yoff in c_prof:
            ring = []
            for s in range(c_segs):
                u = s / (c_segs - 1)
                ang = math.radians(a0_c + u * (a1_c - a0_c))
                ca, sa = math.cos(ang), math.sin(ang)
                ry = ryf if sa >= 0.0 else ryb
                tip_drop = (gauss(u, 0.10) + gauss(1.0 - u, 0.10)) * 0.020
                ring.append(bm_c.verts.new(Vector((rx * ca, yoff + ry * sa, z - tip_drop))))
            c_rings.append(ring)
        for i in range(len(c_rings)):
            inext = (i + 1) % len(c_rings)
            for s in range(c_segs - 1):
                bm_c.faces.new((c_rings[i][s], c_rings[i][s + 1], c_rings[inext][s + 1], c_rings[inext][s]))
        bm_c.faces.new([c_rings[i][0] for i in range(len(c_rings))])
        bm_c.faces.new([c_rings[i][-1] for i in reversed(range(len(c_rings)))])
        parts.append(make_obj_from_bm("Torso_FoldedCollar", bm_c, mats["CharShirt"], smooth_angle=62.0, subsurf_levels=0))

        if is_elder_f:
            # Cozy draped Turkish knit shawl resting warmly over shoulders!
            shawl_pts = [
                (Vector((-0.072, 0.135, 0.285)), 0.024, 0.012),
                (Vector((-0.115, 0.070, 0.422)), 0.036, 0.016),
                (Vector(( 0.000, -0.112, 0.435)), 0.044, 0.018),
                (Vector(( 0.115, 0.070, 0.422)), 0.036, 0.016),
                (Vector(( 0.072, 0.135, 0.285)), 0.024, 0.012),
            ]
            parts.append(loft_tube_along_path("ElderF_Shawl", shawl_pts, 10, mats["CharUndershirt"], rounded_caps=True, spline_samples=2, smooth_angle=68.0))
        elif not is_boss:
            for sx in (-1.0, 1.0):
                a_p0 = 42.0 if sx > 0 else 112.0
                a_p1 = 68.0 if sx > 0 else 138.0
                rx_p = 0.174 if is_female else 0.205
                ry_p = 0.152 if is_female else 0.159
                if not is_female:
                    parts.append(add_curved_patch(f"Jacket_ChestPocket_{sx}", 0.242, 0.308, a_p0, a_p1, rx_p, ry_p, 0.146, 0.007, 0.0055, mats["CharShirt"], u_segs=4, v_segs=2))
                    parts.append(add_curved_patch(f"Jacket_ChestFlap_{sx}", 0.294, 0.318, a_p0 - 1.5, a_p1 + 1.5, rx_p + 0.003, ry_p + 0.003, 0.148, 0.007, 0.0068, mats["CharShirt"], u_segs=4, v_segs=2))
                a_w0 = 26.0 if sx > 0 else 124.0
                a_w1 = 56.0 if sx > 0 else 154.0
                parts.append(add_curved_patch(f"Jacket_SideWelt_{sx}", 0.032, 0.120, a_w0, a_w1, 0.155 if is_female else 0.168, 0.130 if is_female else 0.136, 0.131, 0.001, 0.0050, mats["CharShirt"], u_segs=4, v_segs=2))
        else:
            for sx in (-1.0, 1.0):
                parts.append(add_bevel_box(f"Boss_LapelTrim_{sx}", (sx * 0.082, chest_f + 0.012, 0.345), (0.036, 0.014, 0.195), mats["CharBuckle"], bevel=0.004, segments=1, rot_euler=(-0.10, sx * 0.16, 0.0)))
                parts.append(add_bevel_box(f"Boss_Epaulette_{sx}", (sx * 0.168, 0.002, 0.422), (0.072, 0.038, 0.016), mats["CharBuckle"], bevel=0.004, segments=1, rot_euler=(0.0, sx * 0.28, 0.0)))

    return join_parts_at_origin(parts, "Chest", exact_mesh_name="Torso_Inner_Mesh")


# ---------------------------------------------------------------------------
# 3. Anatomical Neck
#    Exact slot: ['CharSkin']
# ---------------------------------------------------------------------------
def build_neck_mesh(mats):
    def neck_deform(i, s, ang, v):
        sa = math.sin(ang)
        if sa > 0.15:
            scm_x = 0.014 + smoothstep(0.0, 0.095, v.z) * 0.032
            scm = gauss(abs(v.x) - scm_x, 0.014) * smoothstep(-0.02, 0.03, v.z)
            v.y += scm * 0.0032
            v.y += gauss(v.x, 0.012) * gauss(v.z - 0.042, 0.016) * 0.0032
        return v

    specs = [
        (-0.032, 0.084, 0.072, 0.080, -0.004, 0.0),
        (0.004,  0.064, 0.060, 0.066, -0.001, 0.0),
        (0.044,  0.056, 0.056, 0.058,  0.001, 0.0),
        (0.096,  0.056, 0.058, 0.058,  0.004, 0.0),
    ]
    neck = loft_oval_rings("Neck_Mesh", specs, 18, mats["CharSkin"], smooth_angle=68.0, subsurf_levels=0, deform_fn=neck_deform)
    neck.data.name = "Neck_Mesh_Mesh"
    return neck


# ---------------------------------------------------------------------------
# 4. Anatomically Sculpted Face, Crisp Continuous Nose, Recessed Eyes w/ Eyelids
#    & Lash Line, Smooth Catmull-Rom Ears & Swept Multi-Lock Stylized Hair
#    Exact slots: ['CharSkin', 'CharEyeWhite', 'CharEyeIris', 'CharHair', 'CharLips']
# ---------------------------------------------------------------------------
def build_sculpted_ear(name, side_sign, center, mat):
    """Smooth Catmull-Rom cupped anatomical human ear nestled cleanly against the head."""
    cx, cy, cz = center
    sx = side_sign
    helix_pts = [
        (Vector((cx - sx * 0.003, cy + 0.007, cz + 0.018)), 0.0042, 0.0038),
        (Vector((cx + sx * 0.005, cy + 0.002, cz + 0.027)), 0.0048, 0.0045),
        (Vector((cx + sx * 0.010, cy - 0.010, cz + 0.021)), 0.0050, 0.0046),
        (Vector((cx + sx * 0.011, cy - 0.016, cz + 0.006)), 0.0048, 0.0045),
        (Vector((cx + sx * 0.009, cy - 0.014, cz - 0.010)), 0.0048, 0.0046),
        (Vector((cx + sx * 0.004, cy - 0.005, cz - 0.022)), 0.0060, 0.0054),
        (Vector((cx - sx * 0.004, cy + 0.004, cz - 0.019)), 0.0045, 0.0042),
    ]
    helix = loft_tube_along_path(name + "_Helix", helix_pts, 8, mat, rounded_caps=True, spline_samples=2, smooth_angle=72.0)
    bowl = add_ellipsoid(name + "_Concha", (cx + sx * 0.002, cy - 0.005, cz + 0.001), (0.0062, 0.012, 0.020), mat, segs=10, rings=7, rot_euler=(0.0, 0.0, -sx * 0.28))
    tragus = add_ellipsoid(name + "_Tragus", (cx + sx * 0.003, cy + 0.008, cz - 0.004), (0.0040, 0.0048, 0.0068), mat, segs=7, rings=5)
    return join_parts_at_origin([helix, bowl, tragus], name)


def build_hair_lock(name, pts_widths, mat):
    """Smooth Catmull-Rom tapered sculpted hair lock with graceful organic tips."""
    return loft_tube_along_path(
        name,
        pts_widths,
        segments=7,
        mat=mat,
        cap_start=True,
        cap_end=True,
        rounded_caps=True,
        spline_samples=3,
        smooth_angle=74.0,
        subsurf_levels=0,
    )


def build_head_mesh(mats, variant="male"):
    parts = []
    is_female = variant in ("female", "elder_female")
    is_worker = (variant == "worker")
    is_child = (variant == "child")
    is_boss = (variant == "boss")
    is_elder_m = (variant == "elder_male")
    is_elder_f = (variant == "elder_female")
    is_sporty = (variant == "sporty")
    is_shop = (variant == "shopkeeper")

    jaw_w = 0.063 if is_female else (0.074 if (is_boss or is_elder_m) else 0.072)
    cheek_w = 0.081 if is_female else (0.088 if is_elder_m else 0.086)
    brow_w = 0.084 if is_female else 0.088
    z_shift = -0.022

    eye_x = 0.0315
    eye_z = 0.115 + z_shift
    nose_tip_z = 0.084 + z_shift
    mouth_z = 0.056 + z_shift

    # --- A. CONTINUOUS ANATOMICALLY SCULPTED HEAD & FACE (CharSkin — slot 0) ---
    # Apply facial landmark sculpting cleanly AFTER Subsurf so the nose bridge, nasal tip, alae,
    # orbital sockets, brow ridge, cheekbones, philtrum & chin stay CRISP and NEVER wash out!
    head_specs = [
        (-0.004 + z_shift, 0.032, 0.044, 0.024, 0.034, 0.0),
        (0.009 + z_shift,  jaw_w * 0.54, 0.068, 0.038, 0.024, 0.0),
        (0.023 + z_shift,  jaw_w * 0.78, 0.084, 0.052, 0.018, 0.0),
        (0.039 + z_shift,  jaw_w * 0.92, 0.089, 0.065, 0.014, 0.0),
        (0.055 + z_shift,  jaw_w * 0.99, 0.094, 0.075, 0.012, 0.0),
        (0.071 + z_shift,  cheek_w * 0.95, 0.097, 0.085, 0.010, 0.0),
        (0.085 + z_shift,  cheek_w * 0.99, 0.100, 0.093, 0.008, 0.0),
        (0.100 + z_shift,  cheek_w * 1.01, 0.101, 0.099, 0.006, 0.0),
        (0.115 + z_shift,  brow_w * 0.98,  0.102, 0.104, 0.004, 0.0),
        (0.132 + z_shift,  brow_w * 1.00,  0.104, 0.108, 0.003, 0.0),
        (0.153 + z_shift,  brow_w * 0.99,  0.102, 0.110, 0.001, 0.0),
        (0.176 + z_shift,  brow_w * 0.95,  0.096, 0.108, -0.002, 0.0),
        (0.199 + z_shift,  brow_w * 0.82,  0.084, 0.096, -0.004, 0.0),
        (0.217 + z_shift,  0.048,          0.052, 0.058, -0.006, 0.0),
    ]
    cranium = loft_oval_rings("Head_Cranium", head_specs, 28, mats["CharSkin"], smooth_angle=72.0, subsurf_levels=1)

    # Sculpt anatomical facial landmarks directly onto the subdivided smooth mesh!
    for vert in cranium.data.vertices:
        v = vert.co
        ax = abs(v.x)
        z = v.z
        r_xy = math.hypot(v.x, v.y)
        if r_xy < 1e-5:
            continue
        ca = v.x / r_xy
        sa = v.y / r_xy

        # Temple flatness & defined jaw angle
        if abs(ca) > 0.65 and (0.112 + z_shift) < z < (0.178 + z_shift):
            temple = smoothstep(0.65, 0.96, abs(ca)) * gauss(z - (0.145 + z_shift), 0.026) * smoothstep(-0.2, 0.35, sa)
            v.x *= (1.0 - temple * 0.045)
        if not is_female and (0.026 + z_shift) < z < (0.078 + z_shift) and -0.25 < sa < 0.65:
            jaw_ang = gauss(z - (0.048 + z_shift), 0.018) * gauss(sa - 0.15, 0.35)
            v.x += math.copysign(0.0048 * jaw_ang, v.x)

        if sa > 0.25:
            front_w = smoothstep(0.25, 0.72, sa)

            # 1. Deep clean orbital eye sockets (so eyeballs sit naturally inside skull!)
            for sx in (-1.0, 1.0):
                sock = gauss(v.x - sx * eye_x, 0.0175) * gauss(z - eye_z, 0.0135)
                v.y -= sock * 0.0185 * front_w

            # 2. Masculine supraorbital brow ridge & glabella
            brow = gauss(z - (eye_z + 0.017), 0.009) * gauss(ax - 0.025, 0.028)
            v.y += brow * (0.0040 if is_female else 0.0075) * front_w

            # 3. Zygomatic cheekbones + gentle masculine buccal hollow
            cheek = gauss(ax - 0.049, 0.019) * gauss(z - (eye_z - 0.021), 0.016)
            v.y += cheek * 0.0082 * front_w
            v.x += math.copysign(cheek * 0.0045, v.x)
            hollow = gauss(ax - 0.047, 0.018) * gauss(z - (0.065 + z_shift), 0.018)
            v.y -= hollow * (0.0025 if is_female else 0.0058) * front_w

            # 4. SILKY ROUNDED ANATOMICAL NOSE (gentle ramp, natural rounded tip, soft alae — ZERO sharp crease!)
            if (nose_tip_z - 0.018) <= z <= (eye_z + 0.016):
                t_b = clamp((eye_z + 0.006 - z) / max(1e-4, (eye_z + 0.006 - nose_tip_z)), 0.0, 1.0)
                env_z = smoothstep(nose_tip_z - 0.014, nose_tip_z + 0.006, z) * smoothstep(eye_z + 0.014, eye_z - 0.002, z)
                bridge_w = 0.0105 + t_b * 0.0055
                bridge_h = (0.0070 + 0.0120 * (t_b ** 0.80)) * env_z
                v.y += gauss(v.x, bridge_w) * bridge_h * front_w
                # Broad softly rounded nasal tip bulb (never spiky in profile!)
                tip = gauss(v.x, 0.0130) * gauss(z - (nose_tip_z + 0.003), 0.0110)
                v.y += tip * 0.0068 * front_w
                # Soft sculpted nostril wings (alae)
                ala = gauss(ax - 0.0138, 0.0076) * gauss(z - (nose_tip_z - 0.001), 0.0075)
                v.y += ala * 0.0085 * front_w
                v.x += math.copysign(ala * 0.0022, v.x)
                # Subtle nostril shadow indent
                nostril = gauss(ax - 0.0082, 0.0042) * gauss(z - (nose_tip_z - 0.0085), 0.0040)
                v.y -= nostril * 0.0045 * front_w

            # 5. Mouth barrel + philtrum + labiomental chin cleft + square-rounded chin prominence
            mouth_mound = gauss(v.x, 0.029) * gauss(z - mouth_z, 0.016)
            v.y += mouth_mound * 0.0062 * front_w
            philtrum = gauss(v.x, 0.0048) * gauss(z - (mouth_z + 0.014), 0.0062)
            v.y -= philtrum * 0.0038 * front_w
            mento_hollow = gauss(v.x, 0.022) * gauss(z - (mouth_z - 0.015), 0.0072)
            v.y -= mento_hollow * 0.0056 * front_w
            chin_boss = gauss(v.x, 0.022 if not is_female else 0.016) * gauss(z - (0.023 + z_shift), 0.012)
            v.y += chin_boss * (0.0055 if is_female else 0.0095) * front_w

    cranium.data.update()
    apply_smooth(cranium, angle_deg=72.0)
    parts.append(cranium)

    # --- B. SMOOTH SCULPTED ANATOMICAL EARS (CharSkin — slot 0) ---
    for sx in (-1.0, 1.0):
        parts.append(build_sculpted_ear(
            f"Head_Ear_{sx}",
            sx,
            (sx * (cheek_w - 0.004), -0.007, 0.104 + z_shift),
            mats["CharSkin"],
        ))

    # --- C. RECESSED EYELIDS HUGGING EYES NATURALLY (CharSkin — slot 0) ---
    eye_y = 0.0858  # Properly recessed inside the sculpted orbital socket!
    for sx in (-1.0, 1.0):
        ex = sx * eye_x
        up_lid = [
            (Vector((ex - sx * 0.0140, eye_y + 0.0020, eye_z - 0.0004)), 0.0026, 0.0024),
            (Vector((ex - sx * 0.0055, eye_y + 0.0088, eye_z + 0.0054)), 0.0036, 0.0032),
            (Vector((ex + sx * 0.0050, eye_y + 0.0088, eye_z + 0.0054)), 0.0038, 0.0032),
            (Vector((ex + sx * 0.0140, eye_y + 0.0020, eye_z + 0.0002)), 0.0026, 0.0024),
        ]
        parts.append(loft_tube_along_path(f"Eye_UpperLid_{sx}", up_lid, 8, mats["CharSkin"], rounded_caps=True, spline_samples=2, smooth_angle=68.0))
        lo_lid = [
            (Vector((ex - sx * 0.0132, eye_y + 0.0018, eye_z - 0.0010)), 0.0020, 0.0019),
            (Vector((ex,               eye_y + 0.0078, eye_z - 0.0054)), 0.0026, 0.0023),
            (Vector((ex + sx * 0.0135, eye_y + 0.0020, eye_z - 0.0006)), 0.0021, 0.0019),
        ]
        parts.append(loft_tube_along_path(f"Eye_LowerLid_{sx}", lo_lid, 7, mats["CharSkin"], rounded_caps=True, spline_samples=2, smooth_angle=68.0))

    # --- D. GLOSSY RECESSED EYES (Slots 1 & 2: CharEyeWhite, CharEyeIris) ---
    for sx in (-1.0, 1.0):
        ex = sx * eye_x
        parts.append(add_ellipsoid(f"Eye_Sclera_{sx}", (ex, eye_y + 0.0010, eye_z), (0.0124, 0.0112, 0.0094), mats["CharEyeWhite"], segs=12, rings=8))
    for sx in (-1.0, 1.0):
        ex = sx * eye_x
        parts.append(add_ellipsoid(f"Eye_Iris_{sx}", (ex, eye_y + 0.0074, eye_z + 0.0002), (0.0066, 0.0064, 0.0066), mats["CharEyeIris"], segs=12, rings=8))

    # --- E. EYEBROWS, SUBTLE UPPER LASH LINE & SMOOTH MULTI-LOCK HAIR (Slot 3: CharHair) ---
    brow_thick = 0.0030 if is_female else 0.0040
    for sx in (-1.0, 1.0):
        ex = sx * eye_x
        # Subtle crisp upper eyelash line tucked just beneath upper eyelid — brings eyes to life!
        lash_pts = [
            (Vector((ex - sx * 0.0125, eye_y + 0.0035, eye_z + 0.0002)), 0.0013, 0.0012),
            (Vector((ex,               eye_y + 0.0102, eye_z + 0.0040)), 0.0016, 0.0014),
            (Vector((ex + sx * 0.0132, eye_y + 0.0038, eye_z + 0.0008)), 0.0015, 0.0013),
        ]
        parts.append(loft_tube_along_path(f"Eye_LashLine_{sx}", lash_pts, 6, mats["CharHair"], rounded_caps=True, spline_samples=2, smooth_angle=62.0))

        # Smooth arched sculpted eyebrows
        brow_pts = [
            (Vector((ex - sx * 0.0155, eye_y + 0.0155, eye_z + 0.0145)), brow_thick * 1.02, brow_thick * 0.82),
            (Vector((ex - sx * 0.0025, eye_y + 0.0172, eye_z + 0.0172)), brow_thick * 0.96, brow_thick * 0.78),
            (Vector((ex + sx * 0.0095, eye_y + 0.0145, eye_z + 0.0180)), brow_thick * 0.78, brow_thick * 0.65),
            (Vector((ex + sx * 0.0195, eye_y + 0.0060, eye_z + 0.0135)), brow_thick * 0.42, brow_thick * 0.40),
        ]
        parts.append(loft_tube_along_path(f"Eyebrow_{sx}", brow_pts, 7, mats["CharHair"], rounded_caps=True, spline_samples=2, smooth_angle=66.0))

    if is_elder_f:
        # Neat mature parted hair gathered into a warm low chignon bun at the nape!
        crown_specs = [
            (0.118 + z_shift, 0.087, 0.056, 0.110, -0.016, 0.0),
            (0.165 + z_shift, 0.092, 0.092, 0.116, -0.008, 0.0),
            (0.200 + z_shift, 0.084, 0.090, 0.106, -0.006, 0.0),
            (0.222 + z_shift, 0.050, 0.056, 0.064, -0.006, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_ElderFCrown", crown_specs, 22, mats["CharHair"], smooth_angle=70.0, subsurf_levels=0))
        # Swept side-wave framing locks leading smoothly back into low bun
        for sx in (-1.0, 1.0):
            wave_pts = [
                (Vector((sx * 0.014, 0.088, 0.194 + z_shift)), 0.018, 0.012),
                (Vector((sx * 0.066, 0.068, 0.174 + z_shift)), 0.020, 0.013),
                (Vector((sx * 0.092, 0.004, 0.132 + z_shift)), 0.018, 0.012),
                (Vector((sx * 0.054, -0.090, 0.092 + z_shift)), 0.016, 0.011),
            ]
            parts.append(build_hair_lock(f"Hair_ElderFWave_{sx}", wave_pts, mats["CharHair"]))
        # Sculpted braided/twisted low nape chignon bun
        parts.append(add_ellipsoid("Hair_ElderFBun", (0.0, -0.114, 0.094 + z_shift), (0.054, 0.044, 0.046), mats["CharHair"], segs=14, rings=10))
    elif is_female:
        # Flowing Multi-Lock Stylized-Realistic Shoulder-Length Hair + Side-Swept Fringe!
        crown_specs = [
            (0.118 + z_shift, 0.088, 0.062, 0.112, -0.014, 0.0),
            (0.168 + z_shift, 0.092, 0.095, 0.116, -0.008, 0.0),
            (0.200 + z_shift, 0.084, 0.090, 0.108, -0.006, 0.0),
            (0.222 + z_shift, 0.050, 0.056, 0.064, -0.006, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_FemaleCrown", crown_specs, 22, mats["CharHair"], smooth_angle=70.0, subsurf_levels=0))
        # 7 flowing layered shoulder-length back hair curtains (Catmull-Rom smooth!)
        for k in range(-3, 4):
            bx = k * 0.026
            arch = 1.0 - (abs(k) / 4.2) ** 2
            back_pts = [
                (Vector((bx * 0.72, -0.058 - arch * 0.028, 0.202 + z_shift - abs(k) * 0.004)), 0.020, 0.013),
                (Vector((bx * 1.02, -0.092 - arch * 0.032, 0.130 + z_shift)), 0.024, 0.016),
                (Vector((bx * 1.08, -0.104 - arch * 0.028, 0.038 + z_shift)), 0.024, 0.016),
                (Vector((bx * 1.00, -0.102 - arch * 0.020, -0.042 + z_shift)), 0.017, 0.011),
            ]
            parts.append(build_hair_lock(f"Hair_FemBack_{k+3}", back_pts, mats["CharHair"]))
        # Graceful cheek-framing layered side locks & soft side-swept bangs!
        for sx in (-1.0, 1.0):
            s_pts = [
                (Vector((sx * 0.042, 0.062, 0.194 + z_shift)), 0.018, 0.012),
                (Vector((sx * 0.086, 0.036, 0.145 + z_shift)), 0.021, 0.015),
                (Vector((sx * 0.092, 0.006, 0.062 + z_shift)), 0.021, 0.015),
                (Vector((sx * 0.084, -0.012, -0.022 + z_shift)), 0.014, 0.010),
            ]
            parts.append(build_hair_lock(f"Hair_FemSide_{sx}", s_pts, mats["CharHair"]))
        bang_pts = [
            (Vector((-0.044, 0.072, 0.192 + z_shift)), 0.014, 0.010),
            (Vector(( 0.014, 0.095, 0.194 + z_shift)), 0.022, 0.013),
            (Vector(( 0.068, 0.076, 0.172 + z_shift)), 0.018, 0.011),
            (Vector(( 0.088, 0.042, 0.132 + z_shift)), 0.012, 0.008),
        ]
        parts.append(build_hair_lock("Hair_FemBangs", bang_pts, mats["CharHair"]))
    elif is_worker:
        nape_specs = [
            (0.072 + z_shift, 0.080, 0.014, 0.092, -0.028, 0.0),
            (0.142 + z_shift, 0.090, 0.048, 0.106, -0.020, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_WorkerShort", nape_specs, 18, mats["CharHair"], smooth_angle=66.0, subsurf_levels=0))
        for sx in (-1.0, 1.0):
            sb_pts = [
                (Vector((sx * 0.087, 0.038, 0.150 + z_shift)), 0.010, 0.013),
                (Vector((sx * 0.084, 0.018, 0.106 + z_shift)), 0.006, 0.009),
            ]
            parts.append(build_hair_lock(f"Hair_WorkerSideburn_{sx}", sb_pts, mats["CharHair"]))
    else:
        # SMOOTH CATMULL-ROM LAYERED MEN'S / SPORTY / ELDER / SHOPKEEPER / BOSS HAIR
        def scalp_deform(i, s, ang, v):
            ca, sa = math.cos(ang), math.sin(ang)
            if abs(ca) > 0.72 and v.z < (0.142 + z_shift):
                ear_clear = smoothstep(0.72, 0.95, abs(ca)) * gauss(v.y + 0.007, 0.025)
                v.x *= (1.0 - ear_clear * 0.045)
            return v

        hair_base = [
            (0.068 + z_shift, 0.078, 0.006, 0.086, -0.030, 0.0),
            (0.112 + z_shift, 0.087, 0.036, 0.105, -0.020, 0.0),
            (0.148 + z_shift, 0.092, 0.072, 0.114, -0.012, 0.0),
            (0.178 + z_shift, 0.093, 0.092, 0.114, -0.006, 0.0),
            (0.205 + z_shift, 0.081, 0.086, 0.100, -0.004, 0.0),
            (0.222 + z_shift, 0.046, 0.052, 0.058, -0.004, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_MaleScalp", hair_base, 24, mats["CharHair"], smooth_angle=70.0, subsurf_levels=0, deform_fn=scalp_deform))

        # Silhouette-distinct top hair waves per archetype!
        lift = 0.012 if is_sporty else (-0.004 if is_elder_m else 0.0)
        flowing_locks = [
            [((-0.042, 0.064, 0.184 + z_shift), 0.015, 0.011),
             ((-0.004, 0.084, 0.206 + z_shift + lift), 0.024, 0.016),
             (( 0.044, 0.086, 0.208 + z_shift + lift), 0.022, 0.014),
             (( 0.082, 0.058, 0.186 + z_shift), 0.012, 0.009)],
            [((-0.024, 0.076, 0.180 + z_shift), 0.013, 0.010),
             (( 0.016, 0.094, 0.194 + z_shift + lift * 0.7), 0.018, 0.012),
             (( 0.056, 0.086, 0.186 + z_shift), 0.014, 0.009),
             (( 0.082, 0.062, 0.168 + z_shift), 0.008, 0.006)],
            [((-0.044, 0.064, 0.186 + z_shift), 0.015, 0.011),
             ((-0.074, 0.056, 0.174 + z_shift), 0.017, 0.012),
             ((-0.088, 0.036, 0.150 + z_shift), 0.013, 0.009)],
            [((-0.042, 0.028, 0.202 + z_shift), 0.019, 0.013),
             (( 0.006, 0.042, 0.224 + z_shift + lift * 0.8), 0.028, 0.016),
             (( 0.058, 0.036, 0.212 + z_shift), 0.023, 0.014),
             (( 0.090, 0.016, 0.174 + z_shift), 0.013, 0.009)],
            [((-0.036, -0.014, 0.206 + z_shift), 0.020, 0.013),
             (( 0.012, -0.008, 0.226 + z_shift + lift * 0.5), 0.029, 0.016),
             (( 0.064, -0.016, 0.210 + z_shift), 0.023, 0.014),
             (( 0.092, -0.024, 0.168 + z_shift), 0.013, 0.009)],
            [((-0.028, -0.052, 0.200 + z_shift), 0.020, 0.013),
             (( 0.016, -0.056, 0.218 + z_shift), 0.027, 0.015),
             (( 0.064, -0.054, 0.198 + z_shift), 0.020, 0.013),
             (( 0.086, -0.052, 0.158 + z_shift), 0.012, 0.008)],
            [((-0.044, 0.012, 0.204 + z_shift), 0.019, 0.013),
             ((-0.076, 0.006, 0.190 + z_shift), 0.020, 0.013),
             ((-0.091, -0.004, 0.156 + z_shift), 0.014, 0.009)],
            [((-0.044, -0.038, 0.200 + z_shift), 0.019, 0.013),
             ((-0.076, -0.042, 0.184 + z_shift), 0.019, 0.012),
             ((-0.090, -0.046, 0.150 + z_shift), 0.013, 0.009)],
        ]
        for idx_l, lock_pts in enumerate(flowing_locks):
            parts.append(build_hair_lock(f"Hair_TopLock_{idx_l}", lock_pts, mats["CharHair"]))

        for k in range(-2, 3):
            bx = k * 0.028
            arch = 1.0 - (abs(k) / 3.2) ** 2
            back_lock = [
                (Vector((bx * 0.72, -0.066 - arch * 0.026, 0.204 + z_shift - abs(k) * 0.004)), 0.020, 0.012),
                (Vector((bx * 0.96, -0.088 - arch * 0.030, 0.164 + z_shift - abs(k) * 0.005)), 0.022, 0.013),
                (Vector((bx * 0.98, -0.088 - arch * 0.026, 0.120 + z_shift - abs(k) * 0.004)), 0.017, 0.010),
                (Vector((bx * 0.80, -0.076 - arch * 0.018, 0.080 + z_shift)), 0.010, 0.007),
            ]
            parts.append(build_hair_lock(f"Hair_BackLock_{k+2}", back_lock, mats["CharHair"]))

        for sx in (-1.0, 1.0):
            sb_pts = [
                (Vector((sx * 0.087, 0.042, 0.168 + z_shift)), 0.012, 0.015),
                (Vector((sx * 0.086, 0.026, 0.138 + z_shift)), 0.009, 0.014),
                (Vector((sx * 0.083, 0.016, 0.106 + z_shift)), 0.006, 0.009),
            ]
            parts.append(build_hair_lock(f"Hair_Sideburn_{sx}", sb_pts, mats["CharHair"]))

        # Authentic Turkish Mustache for ElderMale & Shopkeeper (CharHair — slot 3!)
        if is_elder_m or is_shop:
            stache_pts = [
                (Vector((-0.032, 0.095, mouth_z + 0.006)), 0.0038, 0.0042),
                (Vector((-0.014, 0.107, mouth_z + 0.011)), 0.0058, 0.0060),
                (Vector(( 0.000, 0.109, mouth_z + 0.010)), 0.0056, 0.0058),
                (Vector(( 0.014, 0.107, mouth_z + 0.011)), 0.0058, 0.0060),
                (Vector(( 0.032, 0.095, mouth_z + 0.006)), 0.0038, 0.0042),
            ]
            parts.append(loft_tube_along_path("Facial_Mustache", stache_pts, 8, mats["CharHair"], rounded_caps=True, spline_samples=2, smooth_angle=66.0))

    if is_boss:
        # Boss trimmed jawline beard (CharHair — slot 3) + aviator lenses (CharEyeIris — slot 2)
        beard_specs = [
            (-0.002 + z_shift, 0.036, 0.046, 0.020, 0.040, 0.0),
            (0.018 + z_shift, 0.064, 0.078, 0.036, 0.024, 0.0),
            (0.044 + z_shift, 0.074, 0.090, 0.046, 0.016, 0.0),
            (0.068 + z_shift, 0.076, 0.094, 0.042, 0.012, 0.0),
        ]
        parts.append(loft_oval_rings("Boss_TrimmedBeard", beard_specs, 18, mats["CharHair"], smooth_angle=66.0, subsurf_levels=0))
        for sx in (-1.0, 1.0):
            parts.append(add_bevel_box(f"Boss_ShadeLens_{sx}", (sx * 0.034, 0.112, 0.116 + z_shift), (0.046, 0.009, 0.028), mats["CharEyeIris"], bevel=0.004, segments=1))

    # --- F. SCULPTED ANATOMICAL LIPS (Slot 4: CharLips — MUST come before Worker cap & Boss gold frames!) ---
    upper_lip_pts = [
        (Vector((-0.0245, 0.0955, mouth_z + 0.0018)), 0.0020, 0.0020),
        (Vector((-0.0110, 0.1066, mouth_z + 0.0044)), 0.0036, 0.0034),
        (Vector(( 0.0000, 0.1082, mouth_z + 0.0032)), 0.0038, 0.0033),
        (Vector(( 0.0110, 0.1066, mouth_z + 0.0044)), 0.0036, 0.0034),
        (Vector(( 0.0245, 0.0955, mouth_z + 0.0018)), 0.0020, 0.0020),
    ]
    lower_lip_pts = [
        (Vector((-0.0225, 0.0955, mouth_z + 0.0004)), 0.0019, 0.0019),
        (Vector((-0.0100, 0.1060, mouth_z - 0.0036)), 0.0044, 0.0040),
        (Vector(( 0.0000, 0.1076, mouth_z - 0.0044)), 0.0048, 0.0042),
        (Vector(( 0.0100, 0.1060, mouth_z - 0.0036)), 0.0044, 0.0040),
        (Vector(( 0.0225, 0.0955, mouth_z + 0.0004)), 0.0019, 0.0019),
    ]
    parts.append(loft_tube_along_path("Mouth_UpperLip", upper_lip_pts, 8, mats["CharLips"], rounded_caps=True, spline_samples=2, smooth_angle=68.0))
    parts.append(loft_tube_along_path("Mouth_LowerLip", lower_lip_pts, 8, mats["CharLips"], rounded_caps=True, spline_samples=2, smooth_angle=68.0))

    # --- G. HEADGEAR & EYEWEAR ACCESSORIES (placed AFTER CharLips to preserve exact FBX slot order!) ---
    if is_worker:
        # Sculpted contoured baseball/mechanic cap (CharShirt slot 5, CharPants slot 6)
        cap_specs = [
            (0.140 + z_shift, 0.096, 0.106, 0.114, -0.004, 0.0),
            (0.178 + z_shift, 0.095, 0.104, 0.112, -0.006, 0.0),
            (0.212 + z_shift, 0.080, 0.086, 0.094, -0.008, 0.0),
            (0.232 + z_shift, 0.040, 0.042, 0.046, -0.008, 0.0),
        ]
        parts.append(loft_oval_rings("Worker_CapCrown", cap_specs, 20, mats["CharShirt"], smooth_angle=64.0, subsurf_levels=0))
        parts.append(add_ellipsoid("Worker_CapBrim", (0.0, 0.126, 0.146 + z_shift), (0.088, 0.068, 0.010), mats["CharPants"], segs=16, rings=6, rot_euler=(0.15, 0.0, 0.0)))
        parts.append(add_ellipsoid("Worker_CapButton", (0.0, -0.008, 0.236 + z_shift), (0.012, 0.012, 0.006), mats["CharPants"], segs=8, rings=6))
    elif is_boss:
        # Gold aviator frames (CharBuckle slot 5)
        for sx in (-1.0, 1.0):
            parts.append(add_bevel_box(f"Boss_ShadeArm_{sx}", (sx * 0.082, 0.055, 0.119 + z_shift), (0.007, 0.115, 0.007), mats["CharBuckle"], bevel=0.002, segments=1))
        parts.append(add_bevel_box("Boss_ShadeBridge", (0.0, 0.114, 0.123 + z_shift), (0.120, 0.009, 0.008), mats["CharBuckle"], bevel=0.002, segments=1))
    elif is_elder_m:
        # Classic Elder reading glasses (metallic rim!)
        for sx in (-1.0, 1.0):
            parts.append(add_bevel_box(f"Elder_GlassArm_{sx}", (sx * 0.082, 0.052, 0.118 + z_shift), (0.005, 0.112, 0.005), mats["CharBuckle"], bevel=0.0015, segments=1))
            parts.append(add_bevel_box(f"Elder_GlassTop_{sx}", (sx * 0.032, 0.106, 0.125 + z_shift), (0.036, 0.005, 0.004), mats["CharBuckle"], bevel=0.001, segments=1))
            parts.append(add_bevel_box(f"Elder_GlassBot_{sx}", (sx * 0.032, 0.106, 0.105 + z_shift), (0.034, 0.005, 0.004), mats["CharBuckle"], bevel=0.001, segments=1))
        parts.append(add_bevel_box("Elder_GlassBridge", (0.0, 0.107, 0.120 + z_shift), (0.022, 0.005, 0.004), mats["CharBuckle"], bevel=0.001, segments=1))

    head = join_parts_at_origin(parts, "Head_Mesh", exact_mesh_name="Head_Cranium_Mesh")
    h_scale = 1.15 if is_child else 1.07
    head.scale = Vector((h_scale, h_scale, h_scale))
    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return head


# ---------------------------------------------------------------------------
# 5. Seamless Deltoid & Tailored Jacket Upper Arm
#    Exact slot: ['CharShirt']
# ---------------------------------------------------------------------------
def build_upper_arm_mesh(mats, is_female=False, exact_mesh_name="UpperArm_Sleeve_Mesh"):
    parts = []
    r_sh = 0.053 if is_female else 0.060
    r_mid = 0.048 if is_female else 0.054
    r_el = 0.044 if is_female else 0.050

    def sleeve_folds(i, s, ang, v):
        if -0.295 <= v.z <= -0.180:
            ripple = 0.0020 * math.sin(v.z * 78.0 + math.sin(ang) * 2.0) * gauss(v.z + 0.245, 0.040)
            v.x += math.cos(ang) * ripple
            v.y += math.sin(ang) * ripple
        return v

    specs = [
        (-0.334, r_el * 0.94, r_el * 0.94, r_el * 0.95, 0.0, 0.0),
        (-0.295, r_el,        r_el * 1.01, r_el * 1.03, 0.0, 0.0),
        (-0.235, r_el * 1.02, r_el * 1.04, r_el * 1.04, 0.001, 0.0),
        (-0.145, r_mid,       r_mid * 1.08, r_mid * 1.04, 0.002, 0.0),
        (-0.065, r_sh * 1.02, r_sh * 1.06, r_sh * 1.06, 0.0, 0.0),
        (-0.012, r_sh * 1.01, r_sh * 1.04, r_sh * 1.04, 0.0, 0.0),
        (0.022,  r_sh * 0.88, r_sh * 0.90, r_sh * 0.90, 0.0, 0.0),
        (0.046,  r_sh * 0.56, r_sh * 0.58, r_sh * 0.58, 0.0, 0.0),
    ]
    parts.append(loft_oval_rings("UpperArm_Sleeve", specs, 18, mats["CharShirt"], smooth_angle=72.0, subsurf_levels=0, deform_fn=sleeve_folds))
    return join_parts_at_origin(parts, "UpperArm_Mesh", exact_mesh_name=exact_mesh_name)


# ---------------------------------------------------------------------------
# 6. Forearm Sleeve w/ Ribbed Cuff + SMOOTH ANATOMICAL 5-FINGER HUMAN HAND!
#    Exact slots: ['CharShirt', 'CharSkin']
# ---------------------------------------------------------------------------
def build_forearm_and_hand_mesh(mats, side_sign=1.0, is_female=False, exact_mesh_name="Forearm_Sleeve_Mesh"):
    parts = []
    r_el = 0.043 if is_female else 0.049
    r_wr = 0.031 if is_female else 0.036

    def forearm_folds(i, s, ang, v):
        if -0.215 <= v.z <= -0.140:
            ripple = 0.0018 * math.sin(v.z * 90.0 + math.cos(ang) * 1.5) * gauss(v.z + 0.180, 0.026)
            v.x += math.cos(ang) * ripple
            v.y += math.sin(ang) * ripple
        return v

    sleeve_specs = [
        (-0.228, r_wr + 0.005, r_wr + 0.006, r_wr + 0.006, 0.0, 0.0),
        (-0.212, r_wr + 0.008, r_wr + 0.009, r_wr + 0.009, 0.0, 0.0),
        (-0.198, r_wr + 0.013, r_wr + 0.014, r_wr + 0.013, 0.0, 0.0),
        (-0.140, r_wr + 0.013, r_wr + 0.014, r_wr + 0.013, 0.0, 0.0),
        (-0.065, r_el * 0.96,  r_el * 0.99,  r_el * 0.96,  0.0, 0.0),
        (-0.008, r_el * 1.00,  r_el * 1.01,  r_el * 1.02,  0.0, 0.0),
        (0.028,  r_el * 0.96,  r_el * 0.96,  r_el * 0.96,  0.0, 0.0),
        (0.050,  r_el * 0.78,  r_el * 0.78,  r_el * 0.78,  0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Forearm_Sleeve", sleeve_specs, 18, mats["CharShirt"], smooth_angle=68.0, subsurf_levels=0, deform_fn=forearm_folds))

    k = 0.90 if is_female else 1.0
    inward = -side_sign

    wrist_specs = [
        (-0.262, r_wr * 0.66, r_wr * 0.86, r_wr * 0.84, 0.002, 0.0),
        (-0.240, r_wr * 0.76, r_wr * 0.86, r_wr * 0.84, 0.001, 0.0),
        (-0.214, r_wr * 0.88, r_wr * 0.90, r_wr * 0.90, 0.000, 0.0),
    ]
    parts.append(loft_oval_rings("Wrist_Skin", wrist_specs, 14, mats["CharSkin"], smooth_angle=66.0, subsurf_levels=0))

    # Smooth cupped palm with subtle integrated knuckle wave (ZERO protruding marble beads!)
    def palm_deform(i, s, ang, v):
        if v.x * inward > 0.002 and -0.322 < v.z < -0.275:
            cup = gauss(v.y - 0.001, 0.022) * gauss(v.z + 0.298, 0.018)
            v.x -= inward * cup * 0.0036
        # Gentle organic metacarpal knuckle undulation on dorsal side at bottom edge of palm
        if v.x * inward < -0.002 and v.z < -0.312:
            kn = 0.0016 * (0.5 + 0.5 * math.cos((v.y - 0.001) * 300.0)) * smoothstep(-0.312, -0.330, v.z)
            v.x -= inward * kn
        return v

    palm_specs = [
        (-0.254, 0.0155 * k, 0.027 * k, 0.025 * k, 0.001, 0.0),
        (-0.280, 0.0178 * k, 0.040 * k, 0.036 * k, 0.002, inward * 0.001),
        (-0.308, 0.0168 * k, 0.043 * k, 0.040 * k, 0.002, inward * 0.0025),
        (-0.330, 0.0145 * k, 0.042 * k, 0.039 * k, 0.002, inward * 0.0040),
    ]
    parts.append(loft_oval_rings("Hand_Palm", palm_specs, 18, mats["CharSkin"], smooth_angle=68.0, subsurf_levels=0, deform_fn=palm_deform))

    parts.append(add_ellipsoid("Hand_ThenarPad", (inward * 0.008 * k, 0.023 * k, -0.282), (0.0115 * k, 0.017 * k, 0.023 * k), mats["CharSkin"], segs=10, rings=6, rot_euler=(0.22, 0.0, 0.0)))
    parts.append(add_ellipsoid("Hand_Hypothenar", (inward * 0.005 * k, -0.022 * k, -0.292), (0.0095 * k, 0.0125 * k, 0.025 * k), mats["CharSkin"], segs=8, rings=6))

    # 4 SEPARATE Anatomically Proportioned 3-Segment Sculpted Fingers w/ SMOOTH ROUNDED FINGERTIPS (zero thigh clip!)
    finger_defs = [
        ("Index",  0.0305 * k, -0.326, 0.064 * k, 0.0078 * k, 0.000),
        ("Middle", 0.0105 * k, -0.329, 0.071 * k, 0.0080 * k, 0.002),
        ("Ring",  -0.0098 * k, -0.327, 0.065 * k, 0.0076 * k, 0.004),
        ("Pinky", -0.0290 * k, -0.321, 0.052 * k, 0.0066 * k, 0.006),
    ]
    for fname, fy, fz, flen, fr, curl in finger_defs:
        f_pts = [
            (Vector((inward * 0.001 * k,                 fy, fz + 0.006)),          fr * 1.04, fr * 1.00),
            (Vector((inward * (0.002 + curl * 0.15) * k, fy, fz - flen * 0.36)),    fr * 0.96, fr * 0.94),
            (Vector((inward * (0.005 + curl * 0.35) * k, fy, fz - flen * 0.70)),    fr * 0.90, fr * 0.88),
            (Vector((inward * (0.009 + curl * 0.55) * k, fy, fz - flen * 0.94)),    fr * 0.84, fr * 0.80),
        ]
        parts.append(loft_tube_along_path(
            f"Hand_Finger_{fname}",
            f_pts,
            segments=8,
            mat=mats["CharSkin"],
            rounded_caps=True,
            spline_samples=2,
            smooth_angle=70.0,
        ))

    # Opposed 2-Segment Anatomical Thumb w/ smooth rounded tip
    thumb_pts = [
        (Vector((inward * 0.003 * k, 0.031 * k, -0.270)), 0.0104 * k, 0.0108 * k),
        (Vector((inward * 0.007 * k, 0.047 * k, -0.298)), 0.0095 * k, 0.0096 * k),
        (Vector((inward * 0.011 * k, 0.053 * k, -0.322)), 0.0088 * k, 0.0086 * k),
        (Vector((inward * 0.014 * k, 0.054 * k, -0.338)), 0.0082 * k, 0.0078 * k),
    ]
    parts.append(loft_tube_along_path("Hand_Thumb", thumb_pts, 8, mats["CharSkin"], rounded_caps=True, spline_samples=2, smooth_angle=70.0))

    return join_parts_at_origin(parts, "ForearmHand_Mesh", exact_mesh_name=exact_mesh_name)


# ---------------------------------------------------------------------------
# 7. Tailored Denim Thighs & Shins with Natural Knee Creases & Cuffed Hems
# ---------------------------------------------------------------------------
def build_thigh_mesh(mats, is_female=False, exact_mesh_name="Thigh_Mesh_Mesh"):
    r_top = 0.092 if is_female else 0.098
    r_mid = 0.084 if is_female else 0.090
    r_kn = 0.066 if is_female else 0.071

    def denim_thigh_deform(i, s, ang, v):
        ca, sa = math.cos(ang), math.sin(ang)
        knee_fold = 0.0026 * math.sin(v.z * 70.0 + sa * 2.0) * gauss(v.z + 0.395, 0.055)
        seam = 0.0020 * gauss(abs(ca) - 1.0, 0.14)
        v.x += ca * (knee_fold + seam)
        v.y += sa * knee_fold
        return v

    specs = [
        (-0.478, r_kn * 0.94, r_kn * 0.98, r_kn * 0.94, 0.0, 0.0),
        (-0.438, r_kn,        r_kn * 1.06, r_kn * 1.00, 0.002, 0.0),
        (-0.375, r_kn * 1.04, r_kn * 1.08, r_kn * 1.02, 0.002, 0.0),
        (-0.250, r_mid * 0.98, r_mid * 1.12, r_mid * 1.04, 0.006, 0.0),
        (-0.125, r_mid * 1.04, r_mid * 1.12, r_mid * 1.10, 0.004, 0.0),
        (-0.040, r_top,       r_top * 1.06, r_top * 1.12, 0.0, 0.0),
        (0.020,  r_top * 0.94, r_top * 0.96, r_top * 0.98, 0.0, 0.0),
        (0.062,  r_top * 0.70, r_top * 0.72, r_top * 0.72, 0.0, 0.0),
    ]
    th = loft_oval_rings("Thigh_Mesh", specs, 20, mats["CharPants"], smooth_angle=68.0, subsurf_levels=0, deform_fn=denim_thigh_deform)
    th.data.name = exact_mesh_name
    return th


def build_shin_mesh(mats, is_female=False, exact_mesh_name="Shin_Trouser_Mesh"):
    parts = []
    r_kn = 0.066 if is_female else 0.071
    r_calf = 0.063 if is_female else 0.070
    r_hem = 0.052 if is_female else 0.057

    def denim_shin_deform(i, s, ang, v):
        ca, sa = math.cos(ang), math.sin(ang)
        stack = 0.0024 * math.sin(v.z * 85.0 + sa * 1.8) * gauss(v.z + 0.340, 0.045)
        seam = 0.0018 * gauss(abs(ca) - 1.0, 0.14)
        v.x += ca * (stack + seam)
        v.y += sa * stack
        return v

    specs = [
        (-0.398, r_hem * 0.97, r_hem * 1.01, r_hem * 1.01, 0.0, 0.0),
        (-0.392, r_hem * 1.06, r_hem * 1.10, r_hem * 1.10, 0.0, 0.0),
        (-0.368, r_hem * 1.05, r_hem * 1.09, r_hem * 1.09, 0.0, 0.0),
        (-0.354, r_hem * 1.00, r_hem * 1.03, r_hem * 1.04, -0.001, 0.0),
        (-0.290, r_hem * 1.05, r_hem * 1.07, r_hem * 1.10, -0.003, 0.0),
        (-0.175, r_calf,       r_calf * 0.98, r_calf * 1.15, -0.006, 0.0),
        (-0.025, r_kn,         r_kn * 1.04,   r_kn * 1.00,   0.002, 0.0),
        (0.025,  r_kn * 0.99,  r_kn * 1.02,   r_kn * 0.98,   0.0, 0.0),
        (0.060,  r_kn * 0.86,  r_kn * 0.88,   r_kn * 0.86,   0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Shin_Trouser", specs, 20, mats["CharPants"], smooth_angle=68.0, subsurf_levels=0, deform_fn=denim_shin_deform))

    sock_specs = [
        (-0.436, 0.041, 0.045, 0.045, 0.0, 0.0),
        (-0.382, 0.045, 0.049, 0.049, 0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Ankle_Sock", sock_specs, 14, mats["CharShoeSole"], smooth_angle=58.0, subsurf_levels=0))
    return join_parts_at_origin(parts, "Shin_Mesh", exact_mesh_name=exact_mesh_name)


# ---------------------------------------------------------------------------
# 8. Contoured Modern Urban Sneakers (Tight Ankle Collar, Flush Eyelets,
#    Criss-Cross 3D Laces & Sculpted Midsole)
#    Exact slots: ['CharShoes', 'CharShoeSole']
# ---------------------------------------------------------------------------
def loft_superellipse_y(name, specs, segments, mat, exponent=2.5, subsurf_levels=0):
    bm = bmesh.new()
    rings = []
    for (y, hw, zb, zt) in specs:
        zc, hz = (zb + zt) * 0.5, (zt - zb) * 0.5
        ring = []
        for s in range(segments):
            a = 2.0 * math.pi * s / segments
            ca, sa = math.cos(a), math.sin(a)
            x = hw * math.copysign(abs(ca) ** (2.0 / exponent), ca)
            z = zc + hz * math.copysign(abs(sa) ** (2.0 / exponent), sa)
            ring.append(bm.verts.new(Vector((x, y, z))))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((rings[i][s], rings[i][sn], rings[i + 1][sn], rings[i + 1][s]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    return make_obj_from_bm(name, bm, mat, smooth_angle=64.0, subsurf_levels=subsurf_levels)


def build_foot_shoe_mesh(mats, is_worker=False, exact_mesh_name="Shoe_Upper_Mesh"):
    parts = []
    sole_mat = mats["CharBelt"] if is_worker else mats["CharShoeSole"]
    w = 1.06 if is_worker else 1.0

    # Contoured sneaker upper (CharShoes — slot 0)
    upper = [
        (-0.078, 0.028 * w, -0.066, -0.030),
        (-0.066, 0.044 * w, -0.068,  0.004 if is_worker else -0.002),
        (-0.036, 0.049 * w, -0.068,  0.020 if is_worker else  0.008),
        (0.010,  0.050 * w, -0.068,  0.012 if is_worker else  0.002),
        (0.060,  0.050 * w, -0.068, -0.017),
        (0.112,  0.051 * w, -0.068, -0.032),
        (0.156,  0.046 * w, -0.066, -0.039),
        (0.184,  0.034 * w, -0.063, -0.045),
        (0.198,  0.017 * w, -0.058, -0.051),
    ]
    parts.append(loft_superellipse_y("Shoe_Upper", upper, 18, mats["CharShoes"], exponent=2.4, subsurf_levels=0))

    # Smooth integrated heel counter & heel pull tab hugging the rear upper cleanly!
    heel_counter = [
        (-0.080, 0.030 * w, -0.066, -0.020),
        (-0.058, 0.046 * w, -0.066, -0.006),
        (-0.026, 0.050 * w, -0.066, -0.026),
    ]
    parts.append(loft_superellipse_y("Shoe_HeelCounter", heel_counter, 14, mats["CharShoes"], exponent=2.4, subsurf_levels=0))
    parts.append(add_bevel_box("Shoe_HeelTab", (0.0, -0.062, 0.002), (0.013, 0.007, 0.016), mats["CharShoes"], bevel=0.002, segments=1, rot_euler=(0.24, 0.0, 0.0)))

    # Flush angled eyelet lace stays nested into vamp!
    for sx in (-1.0, 1.0):
        parts.append(add_bevel_box(f"Shoe_EyeletStay_{sx}", (sx * 0.025 * w, 0.072, -0.018), (0.011, 0.074, 0.007), mats["CharShoes"], bevel=0.002, segments=1, rot_euler=(-0.38, sx * 0.22, 0.0)))

    # Sculpted Athletic Midsole + Side Accent + Cushioned Tongue + Criss-Cross 3D Laces (CharShoeSole — slot 1)
    sole = [
        (-0.083, 0.033 * w, -0.080, -0.060),
        (-0.064, 0.048 * w, -0.081, -0.059),
        (-0.020, 0.052 * w, -0.081, -0.061),
        (0.040,  0.051 * w, -0.081, -0.063),
        (0.115,  0.054 * w, -0.081, -0.063),
        (0.165,  0.049 * w, -0.079, -0.062),
        (0.192,  0.036 * w, -0.075, -0.060),
        (0.203,  0.020 * w, -0.070, -0.058),
    ]
    parts.append(loft_superellipse_y("Shoe_Sole", sole, 16, sole_mat, exponent=3.0, subsurf_levels=0))
    parts.append(add_bevel_box("Shoe_Tongue", (0.0, 0.040, -0.004), (0.042, 0.046, 0.012), sole_mat if not is_worker else mats["CharShoes"], bevel=0.004, segments=1, rot_euler=(-0.44, 0.0, 0.0)))

    for sx in (-1.0, 1.0):
        parts.append(add_bevel_box(f"Shoe_SideStripe_{sx}", (sx * 0.048 * w, 0.032, -0.043), (0.005, 0.068, 0.009), sole_mat, bevel=0.002, segments=1, rot_euler=(-0.16, 0.0, 0.0)))

    for li in range(4):
        ly = 0.040 + li * 0.020
        lz = -0.002 - li * 0.0085
        for tilt in (-0.32, 0.32):
            parts.append(add_bevel_box(
                f"Shoe_Lace_{li}_{tilt:+.1f}",
                (0.0, ly, lz),
                (0.046 * w, 0.0050, 0.0044),
                mats["CharShoeSole"],
                bevel=0.0014, segments=1,
                rot_euler=(-0.38, 0.0, tilt),
            ))

    return join_parts_at_origin(parts, "Foot_Shoe", exact_mesh_name=exact_mesh_name)


# ---------------------------------------------------------------------------
# Rig Assembly & Flat Part Export (Exact 14 Pivots & Prefab Verification)
# ---------------------------------------------------------------------------
def build_character_rig(rig_name, variant, mats):
    is_female = (variant == "female" or variant == "child")
    is_worker = (variant == "worker" or variant == "boss")
    shldr_x = 0.172 if is_female else (0.212 if variant == "boss" else 0.200)
    hip_x = 0.102 if is_female else 0.106

    root = make_pivot(rig_name, None, (0.0, 0.0, 0.0))

    hips = make_pivot("Hips", root, (0.0, 0.0, 0.95))
    attach_mesh_to_pivot(build_pelvis_mesh(mats, is_female=is_female), hips)

    spine = make_pivot("Spine", hips, (0.0, 0.0, 0.06))
    attach_mesh_to_pivot(build_torso_mesh(mats, variant=variant), spine)

    neck = make_pivot("Neck", spine, (0.0, 0.0, 0.46))
    attach_mesh_to_pivot(build_neck_mesh(mats), neck)

    head = make_pivot("Head", neck, (0.0, 0.0, 0.09))
    attach_mesh_to_pivot(build_head_mesh(mats, variant=variant), head)

    for side, sx in (("L", -1.0), ("R", 1.0)):
        shldr = make_pivot(f"{side}_Shoulder", spine, (sx * shldr_x, 0.0, 0.382))
        attach_mesh_to_pivot(build_upper_arm_mesh(mats, is_female=is_female), shldr)
        elbow = make_pivot(f"{side}_Elbow", shldr, (0.0, 0.0, -0.295))
        attach_mesh_to_pivot(build_forearm_and_hand_mesh(mats, side_sign=sx, is_female=is_female), elbow)

    for side, sx in (("L", -1.0), ("R", 1.0)):
        hip = make_pivot(f"{side}_Hip", hips, (sx * hip_x, 0.0, -0.02))
        attach_mesh_to_pivot(build_thigh_mesh(mats, is_female=is_female), hip)
        knee = make_pivot(f"{side}_Knee", hip, (0.0, 0.0, -0.43))
        attach_mesh_to_pivot(build_shin_mesh(mats, is_female=is_female), knee)
        ankle = make_pivot(f"{side}_Ankle", knee, (0.0, 0.0, -0.42))
        attach_mesh_to_pivot(build_foot_shoe_mesh(mats, is_worker=is_worker), ankle)

    return root


def build_flat_export_parts(variant, mats):
    is_female = variant in ("female", "child", "elder_female")
    is_worker = variant in ("worker", "boss", "elder_male")

    parts = [
        ("Part_Pelvis",      build_pelvis_mesh(mats, is_female=is_female),                                                                  "Pelvis_Jeans_Mesh"),
        ("Part_Chest",       build_torso_mesh(mats, variant=variant),                                                                       "Torso_Inner_Mesh"),
        ("Part_Neck",        build_neck_mesh(mats),                                                                                         "Neck_Mesh_Mesh"),
        ("Part_Head",        build_head_mesh(mats, variant=variant),                                                                        "Head_Cranium_Mesh"),
        ("Part_L_UpperArm",  build_upper_arm_mesh(mats, is_female=is_female, exact_mesh_name="UpperArm_Sleeve_Mesh"),                       "UpperArm_Sleeve_Mesh"),
        ("Part_L_Forearm",   build_forearm_and_hand_mesh(mats, side_sign=-1.0, is_female=is_female, exact_mesh_name="Forearm_Sleeve_Mesh"), "Forearm_Sleeve_Mesh"),
        ("Part_R_UpperArm",  build_upper_arm_mesh(mats, is_female=is_female, exact_mesh_name="UpperArm_Sleeve_Mesh.001"),                   "UpperArm_Sleeve_Mesh.001"),
        ("Part_R_Forearm",   build_forearm_and_hand_mesh(mats, side_sign=1.0, is_female=is_female, exact_mesh_name="Forearm_Sleeve_Mesh.001"), "Forearm_Sleeve_Mesh.001"),
        ("Part_L_Thigh",     build_thigh_mesh(mats, is_female=is_female, exact_mesh_name="Thigh_Mesh_Mesh"),                                "Thigh_Mesh_Mesh"),
        ("Part_L_Shin",      build_shin_mesh(mats, is_female=is_female, exact_mesh_name="Shin_Trouser_Mesh"),                               "Shin_Trouser_Mesh"),
        ("Part_L_Foot",      build_foot_shoe_mesh(mats, is_worker=is_worker, exact_mesh_name="Shoe_Upper_Mesh"),                            "Shoe_Upper_Mesh"),
        ("Part_R_Thigh",     build_thigh_mesh(mats, is_female=is_female, exact_mesh_name="Thigh_Mesh_Mesh.001"),                            "Thigh_Mesh_Mesh.001"),
        ("Part_R_Shin",      build_shin_mesh(mats, is_female=is_female, exact_mesh_name="Shin_Trouser_Mesh.001"),                           "Shin_Trouser_Mesh.001"),
        ("Part_R_Foot",      build_foot_shoe_mesh(mats, is_worker=is_worker, exact_mesh_name="Shoe_Upper_Mesh.001"),                        "Shoe_Upper_Mesh.001"),
    ]
    out = []
    for pname, obj, mname in parts:
        obj.name = pname
        obj.data.name = mname
        obj.location = Vector((0.0, 0.0, 0.0))
        obj.rotation_euler = Euler((0.0, 0.0, 0.0), "XYZ")
        obj.scale = Vector((1.0, 1.0, 1.0))
        out.append(obj)

    # Guardrail verification for ALL 5 existing prefab-referenced characters!
    expected_by_variant = {
        "male":   {"Part_Chest": ["CharUndershirt", "CharShirt"], "Part_Head": ["CharSkin", "CharEyeWhite", "CharEyeIris", "CharHair", "CharLips"]},
        "female": {"Part_Chest": ["CharUndershirt", "CharShirt"], "Part_Head": ["CharSkin", "CharEyeWhite", "CharEyeIris", "CharHair", "CharLips"]},
        "worker": {"Part_Chest": ["CharShirt", "CharBelt", "CharPants"], "Part_Head": ["CharSkin", "CharEyeWhite", "CharEyeIris", "CharHair", "CharLips", "CharShirt", "CharPants"]},
        "child":  {"Part_Chest": ["CharShirt", "CharBelt", "CharPants", "CharBuckle"], "Part_Head": ["CharSkin", "CharEyeWhite", "CharEyeIris", "CharHair", "CharLips"]},
        "boss":   {"Part_Chest": ["CharUndershirt", "CharShirt", "CharBuckle"], "Part_Head": ["CharSkin", "CharEyeWhite", "CharEyeIris", "CharHair", "CharLips", "CharBuckle"]},
    }
    if variant in expected_by_variant:
        for obj in out:
            if obj.name in expected_by_variant[variant]:
                got = [m.name for m in obj.data.materials]
                exp = expected_by_variant[variant][obj.name]
                assert got == exp, f"Material slot mismatch on {variant}/{obj.name}: got {got}, expected {exp}"
    return out


def export_parts_fbx(part_objs, filename, out_dir=None):
    filepath = os.path.join(out_dir if out_dir else OUT_FBX_DIR, filename)
    bpy.ops.object.select_all(action="DESELECT")
    for t in part_objs:
        t.select_set(True)
    bpy.context.view_layer.objects.active = part_objs[0]
    bpy.ops.export_scene.fbx(
        filepath=filepath,
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
    verts = sum(len(t.data.vertices) for t in part_objs if t.type == "MESH")
    tris = sum(sum(max(1, len(p.vertices) - 2) for p in t.data.polygons) for t in part_objs if t.type == "MESH")
    mat_names = sorted({m.name for t in part_objs if t.type == "MESH" for m in t.data.materials if m})
    print(f"  Exported {filename:28s} | verts={verts:5d} | tris={tris:5d} | parts={len(part_objs):2d} | mats={mat_names}")
    return filepath


def parse_only_variants():
    argv = sys.argv
    if "--" in argv:
        extra = argv[argv.index("--") + 1:]
        if "--only" in extra:
            idx = extra.index("--only")
            if idx + 1 < len(extra):
                return set(extra[idx + 1].split(","))
    return None


def main():
    only = parse_only_variants()
    variants = [
        ("Character_CasualMale.fbx",   "Character_CasualMale",   "male"),
        ("Character_CasualFemale.fbx", "Character_CasualFemale", "female"),
        ("Character_Worker.fbx",       "Character_Worker",       "worker"),
        ("Character_Child.fbx",        "Character_Child",        "child"),
        ("Character_BossKuzgun.fbx",   "Character_BossKuzgun",   "boss"),
        ("Character_ElderMale.fbx",    "Character_ElderMale",    "elder_male"),
        ("Character_ElderFemale.fbx",  "Character_ElderFemale",  "elder_female"),
        ("Character_SportyYouth.fbx",  "Character_SportyYouth",  "sporty"),
        ("Character_Shopkeeper.fbx",   "Character_Shopkeeper",   "shopkeeper"),
    ]
    if only is not None:
        variants = [v for v in variants if v[2] in only]

    print(f"[GRIDROAM Blender] Generating {len(variants)} stylized-realistic human FBX rig(s)...")
    for fbx_name, rig_name, variant in variants:
        reset_scene()
        mats = get_character_materials()
        part_objs = build_flat_export_parts(variant, mats)
        export_parts_fbx(part_objs, fbx_name, out_dir=RES_CHAR_DIR)
        export_parts_fbx(part_objs, fbx_name, out_dir=OUT_FBX_DIR)
    print("[GRIDROAM Blender] Export complete!")


if __name__ == "__main__":
    main()
