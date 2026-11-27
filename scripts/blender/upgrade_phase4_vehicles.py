#!/usr/bin/env python3
"""
GRIDROAM Phase 4 (Batch 5) – Stylized-Realistic Vehicles & Mobility Upgrade:
  1. OW_Bicycle.fbx      (New sculpted city/touring 3D bicycle w/ tubular diamond frame, curved fenders,
                          ergonomic saddle, luggage rack, spoked wheels, brake discs, chainring crank & headlamp;
                          exact pivots for BicycleController: Frame, Fork, Wheel_Front, Wheel_Rear, Crank)
  2. OW_Motorcycle.fbx   (New sculpted cafe-racer/naked street motorcycle w/ contoured teardrop tank, stitched
                          stepped leather seat, finned twin engine, trellis frame, dual chrome mufflers, twin
                          coil shocks, telescopic forks, gauges, mirrors, 5-spoke alloy wheels & round headlamp;
                          exact pivots for BicycleController: Frame, Fork, Wheel_Front, Wheel_Rear)
  3. OW_CustomerCar.fbx  (Replaces legacy 3k untextured shell with our sculpted 10k-tri stylized-realistic
                          Car_Sedan + 4 sculpted Car_Wheel alloy rims, preserving exact OW_CarPaint / Windows /
                          Headlights / TailLights material slots & clean UVs!)
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
LIFE_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldLife")
ART_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")


def make_mat(name, color, roughness=0.45, metallic=0.0, emission=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name=name)
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if emission is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 1.8
    return mat


def apply_smooth(obj, angle_deg=54.0):
    mesh = obj.data
    for p in mesh.polygons:
        p.use_smooth = True
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.edges.ensure_lookup_table()
    thresh = math.radians(angle_deg)
    for e in bm.edges:
        e.smooth = (len(e.link_faces) == 2 and e.calc_face_angle(0.0) < thresh)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def add_bm(target_bm, src_bm, mat_idx, uv_scale=2.0):
    bmesh.ops.recalc_face_normals(src_bm, faces=src_bm.faces)
    uv_layer = target_bm.loops.layers.uv.verify()
    vmap = {v.index: target_bm.verts.new(v.co) for v in src_bm.verts}
    for f in src_bm.faces:
        try:
            nf = target_bm.faces.new([vmap[v.index] for v in f.verts])
            nf.material_index = mat_idx
            nf.smooth = True
            n = f.normal
            ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
            for loop in nf.loops:
                co = loop.vert.co
                u, v = (co.x * uv_scale, co.y * uv_scale) if (az >= ax and az >= ay) else ((co.y * uv_scale, co.z * uv_scale) if (ax >= ay and ax >= az) else (co.x * uv_scale, co.z * uv_scale))
                loop[uv_layer].uv = (u, v)
        except ValueError:
            pass
    src_bm.free()


def rod_bm(p0, p1, r0, r1=None, segs=10):
    p0, p1 = Vector(p0), Vector(p1)
    if r1 is None:
        r1 = r0
    diff = p1 - p0
    length = diff.length
    if length < 1e-5:
        return bmesh.new()
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r0, radius2=r1, depth=length)
    quat = Vector((0, 0, 1)).rotation_difference(diff.normalized())
    bmesh.ops.transform(bm, matrix=Matrix.Translation((p0 + p1) * 0.5) @ quat.to_matrix().to_4x4(), verts=bm.verts)
    return bm


def torus_x_bm(center, R, r, maj_segs=28, min_segs=12):
    """Torus lying in YZ plane (wheel around X axis)."""
    bm = bmesh.new()
    rings = []
    for i in range(maj_segs):
        a = 2.0 * math.pi * i / maj_segs
        cy, cz = math.cos(a), math.sin(a)
        ring = []
        for j in range(min_segs):
            b = 2.0 * math.pi * j / min_segs
            cb, sb = math.cos(b), math.sin(b)
            rr = R + r * cb
            ring.append(bm.verts.new(Vector((center[0] + r * sb, center[1] + rr * cy, center[2] + rr * cz))))
        rings.append(ring)
    for i in range(maj_segs):
        inext = (i + 1) % maj_segs
        for j in range(min_segs):
            jnext = (j + 1) % min_segs
            bm.faces.new((rings[i][j], rings[inext][j], rings[inext][jnext], rings[i][jnext]))
    return bm


def arc_fender_x_bm(center, R, half_w, thick, a0_deg, a1_deg, segs=16):
    bm = bmesh.new()
    g_il, g_ih, g_ol, g_oh = [], [], [], []
    cx, cy, cz = center
    for i in range(segs + 1):
        a = math.radians(a0_deg + (a1_deg - a0_deg) * (i / segs))
        sy, sz = math.cos(a), math.sin(a)
        ri, ro = R, R + thick
        g_il.append(bm.verts.new(Vector((cx - half_w, cy + sy * ri, cz + sz * ri))))
        g_ih.append(bm.verts.new(Vector((cx - half_w, cy + sy * ro, cz + sz * ro))))
        g_ol.append(bm.verts.new(Vector((cx + half_w, cy + sy * ri, cz + sz * ri))))
        g_oh.append(bm.verts.new(Vector((cx + half_w, cy + sy * ro, cz + sz * ro))))
    for i in range(segs):
        bm.faces.new((g_il[i], g_ol[i], g_ol[i + 1], g_il[i + 1]))
        bm.faces.new((g_oh[i], g_ih[i], g_ih[i + 1], g_oh[i + 1]))
        bm.faces.new((g_il[i], g_ih[i], g_ih[i + 1], g_il[i + 1]))
        bm.faces.new((g_oh[i], g_ol[i], g_ol[i + 1], g_oh[i + 1]))
    bm.faces.new((g_il[0], g_ol[0], g_oh[0], g_ih[0]))
    bm.faces.new((g_ih[-1], g_oh[-1], g_ol[-1], g_il[-1]))
    return bm


def bevel_box_bm(center, size, bevel=0.012, segs=2, rot_euler=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.05:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=segs, profile=0.5, affect="EDGES", clamp_overlap=True)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4(), verts=bm.verts)
    return bm


def ellipsoid_bm(center, radii, segs=14, rings=10, rot_euler=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4(), verts=bm.verts)
    return bm


def finalize_obj(name, bm, mat_list, pivot=(0, 0, 0), smooth_ang=54.0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    for m in mat_list:
        mesh.materials.append(m)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = Vector(pivot)
    apply_smooth(obj, angle_deg=smooth_ang)
    return obj


def export_fbx_unity(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    out_path = os.path.join(LIFE_DIR, filename)
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
    tris = sum(sum(max(1, len(p.vertices) - 2) for p in o.data.polygons) for o in objs if o.type == "MESH")
    print(f"  [Vehicle] Exported {filename:22s} | parts={len(objs)} | tris={tris:5d}")


# ===========================================================================
# 1. OW_Bicycle.fbx (Sculpted Touring City Bicycle — Exact BicycleController Pivots!)
# ===========================================================================
def build_bicycle():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = [
        make_mat("OW_BikePaint", (0.14, 0.44, 0.56), roughness=0.26, metallic=0.35),
        make_mat("OW_Chrome", (0.86, 0.88, 0.90), roughness=0.16, metallic=0.92),
        make_mat("OW_TireRubber", (0.11, 0.11, 0.12), roughness=0.86, metallic=0.0),
        make_mat("OW_LeatherDark", (0.18, 0.11, 0.07), roughness=0.48, metallic=0.0),
        make_mat("OW_HeadlightGlow", (1.0, 0.94, 0.78), roughness=0.14, emission=(1.0, 0.90, 0.65)),
        make_mat("OW_TailGlow", (0.95, 0.14, 0.12), roughness=0.18, emission=(0.95, 0.12, 0.10)),
    ]
    PAINT, CHR, RUB, LEATH, GLOW, TAIL = 0, 1, 2, 3, 4, 5

    # Exact Unity pivots mapped to Blender (+Y = Unity +Z forward, +Z = Unity +Y up!)
    front_axle = Vector((0.0,  0.57, 0.34))
    rear_axle  = Vector((0.0, -0.55, 0.34))
    bb         = Vector((0.0, -0.07, 0.36))
    head_top   = Vector((0.0,  0.43, 0.87))
    head_bot   = Vector((0.0,  0.46, 0.73))
    seat_top   = Vector((0.0, -0.24, 0.82))

    # --- A. Frame ---
    bm_f = bmesh.new()
    # Welded tubular diamond frame + double rear stays!
    add_bm(bm_f, rod_bm(bb, seat_top, 0.021), PAINT)
    add_bm(bm_f, rod_bm(bb, head_bot, 0.025), PAINT)
    add_bm(bm_f, rod_bm(seat_top, head_top, 0.019), PAINT)
    add_bm(bm_f, rod_bm(head_bot, head_top, 0.026), PAINT)
    for sx in (-1.0, 1.0):
        add_bm(bm_f, rod_bm(seat_top + Vector((sx * 0.016, 0, -0.03)), rear_axle + Vector((sx * 0.055, 0, 0)), 0.012), PAINT)
        add_bm(bm_f, rod_bm(bb + Vector((sx * 0.020, 0, 0)), rear_axle + Vector((sx * 0.055, 0, 0)), 0.013), PAINT)
        # Rear tubular luggage rack + vertical stays
        add_bm(bm_f, rod_bm(Vector((sx * 0.068, -0.34, 0.72)), Vector((sx * 0.068, -0.76, 0.72)), 0.008), CHR)
        add_bm(bm_f, rod_bm(rear_axle + Vector((sx * 0.058, 0, 0)), Vector((sx * 0.068, -0.72, 0.72)), 0.007), CHR)
        add_bm(bm_f, rod_bm(seat_top + Vector((sx * 0.018, 0, -0.08)), Vector((sx * 0.068, -0.36, 0.72)), 0.007), CHR)
    for ry in (-0.42, -0.56, -0.70):
        add_bm(bm_f, rod_bm(Vector((-0.068, ry, 0.72)), Vector((0.068, ry, 0.72)), 0.007), CHR)
    # Curved rear fender mudguard + LED tail reflector!
    add_bm(bm_f, arc_fender_x_bm(rear_axle, 0.355, 0.036, 0.008, 62, 208, 16), PAINT)
    add_bm(bm_f, bevel_box_bm((0.0, -0.785, 0.715), (0.068, 0.024, 0.032), 0.006, 1), TAIL)
    # Chain guard + bottom bracket barrel + water bottle cage on downtube!
    add_bm(bm_f, rod_bm(Vector((-0.045, -0.07, 0.36)), Vector((0.045, -0.07, 0.36)), 0.026), CHR)
    add_bm(bm_f, bevel_box_bm((0.052, -0.30, 0.37), (0.022, 0.50, 0.075), 0.008, 1), PAINT)
    # Chrome seatpost + ergonomic sculpted leather touring saddle + twin springs!
    add_bm(bm_f, rod_bm(seat_top, seat_top + Vector((0, -0.02, 0.14)), 0.014), CHR)
    add_bm(bm_f, ellipsoid_bm((0.0, -0.26, 0.975), (0.082, 0.138, 0.032), 14, 8), LEATH)
    add_bm(bm_f, ellipsoid_bm((0.0, -0.15, 0.972), (0.036, 0.075, 0.024), 10, 6), LEATH)
    for sx in (-0.042, 0.042):
        add_bm(bm_f, rod_bm(Vector((sx, -0.31, 0.915)), Vector((sx, -0.31, 0.955)), 0.012), CHR)
    obj_frame = finalize_obj("Frame", bm_f, mats, pivot=(0, 0, 0))

    # --- B. Steering Fork & Handlebars (centered at front_axle pivot!) ---
    bm_k = bmesh.new()
    # Relative to front_axle (0, 0.57, 0.34)
    crown = Vector((0.0, -0.11, 0.39))
    stem_top = Vector((0.0, -0.14, 0.67))
    for sx in (-1.0, 1.0):
        # Tapered curved fork blades
        mid_f = Vector((sx * 0.052, -0.04, 0.20))
        add_bm(bm_k, rod_bm(Vector((sx * 0.054, 0, 0)), mid_f, 0.012, 0.015), PAINT)
        add_bm(bm_k, rod_bm(mid_f, crown + Vector((sx * 0.038, 0, 0)), 0.015, 0.017), PAINT)
        # Swept-back ergonomic city handlebars + stitched leather grips + brake levers!
        bar_mid = stem_top + Vector((sx * 0.18, -0.03, 0.015))
        bar_end = stem_top + Vector((sx * 0.28, -0.11, 0.010))
        add_bm(bm_k, rod_bm(stem_top, bar_mid, 0.012), CHR)
        add_bm(bm_k, rod_bm(bar_mid, bar_end, 0.017), LEATH)
        add_bm(bm_k, rod_bm(bar_mid + Vector((sx * 0.01, 0.03, -0.012)), bar_end + Vector((-sx * 0.01, 0.035, -0.020)), 0.006), CHR)
    add_bm(bm_k, rod_bm(crown, stem_top, 0.016), CHR)
    add_bm(bm_k, rod_bm(crown + Vector((-0.05, 0, 0)), crown + Vector((0.05, 0, 0)), 0.018), CHR)
    # Curved front fender mudguard + classic chrome bullet headlamp!
    add_bm(bm_k, arc_fender_x_bm((0, 0, 0), 0.355, 0.036, 0.008, 40, 155, 14), PAINT)
    add_bm(bm_k, rod_bm(crown + Vector((0, 0.02, 0.08)), crown + Vector((0, 0.07, 0.10)), 0.026, 0.034, 12), CHR)
    add_bm(bm_k, ellipsoid_bm(crown + Vector((0, 0.075, 0.10)), (0.030, 0.012, 0.030), 12, 8), GLOW)
    obj_fork = finalize_obj("Fork", bm_k, mats, pivot=front_axle)

    # --- C. Spoked Bicycle Wheels (Front & Rear) ---
    def make_bike_wheel(wname, piv):
        bm_w = bmesh.new()
        add_bm(bm_w, torus_x_bm((0, 0, 0), 0.312, 0.026, 30, 10), RUB)
        add_bm(bm_w, torus_x_bm((0, 0, 0), 0.284, 0.012, 28, 8), CHR)
        add_bm(bm_w, rod_bm((-0.048, 0, 0), (0.048, 0, 0), 0.022, 0.022, 12), CHR)
        # Brake rotor disc
        add_bm(bm_w, rod_bm((-0.032, 0, 0), (-0.026, 0, 0), 0.082, 0.082, 16), CHR)
        # 12 crisp thin tension spokes
        for i in range(12):
            a = math.pi * i / 12.0
            dy, dz = math.cos(a) * 0.282, math.sin(a) * 0.282
            sx = 0.018 if (i % 2 == 0) else -0.018
            add_bm(bm_w, rod_bm((sx, 0, 0), (0, dy, dz), 0.0032, 0.0032, 5), CHR)
            add_bm(bm_w, rod_bm((-sx, 0, 0), (0, -dy, -dz), 0.0032, 0.0032, 5), CHR)
        return finalize_obj(wname, bm_w, mats, pivot=piv)

    obj_wf = make_bike_wheel("Wheel_Front", front_axle)
    obj_wr = make_bike_wheel("Wheel_Rear", rear_axle)

    # --- D. Crank & Pedals (centered at bottomBracket pivot!) ---
    bm_c = bmesh.new()
    add_bm(bm_c, rod_bm((0.034, 0, 0), (0.046, 0, 0), 0.088, 0.088, 18), CHR)  # front chainring
    for sx in (-1.0, 1.0):
        ped = Vector((sx * 0.145, sx * 0.155, 0.0))
        add_bm(bm_c, rod_bm((sx * 0.055, 0, 0), (sx * 0.075, sx * 0.155, 0), 0.011), CHR)
        add_bm(bm_c, rod_bm((sx * 0.075, sx * 0.155, 0), ped, 0.008), CHR)
        add_bm(bm_c, bevel_box_bm(ped, (0.092, 0.072, 0.024), 0.005, 1), RUB)
    obj_crank = finalize_obj("Crank", bm_c, mats, pivot=bb)

    export_fbx_unity([obj_frame, obj_fork, obj_wf, obj_wr, obj_crank], "OW_Bicycle.fbx")


# ===========================================================================
# 2. OW_Motorcycle.fbx (Sculpted Cafe-Racer / Naked Street Motorcycle!)
# ===========================================================================
def build_motorcycle():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = [
        make_mat("OW_BikePaint", (0.64, 0.13, 0.08), roughness=0.22, metallic=0.38),
        make_mat("OW_Chrome", (0.86, 0.88, 0.90), roughness=0.14, metallic=0.94),
        make_mat("OW_SteelDark", (0.16, 0.17, 0.19), roughness=0.44, metallic=0.70),
        make_mat("OW_TireRubber", (0.10, 0.10, 0.11), roughness=0.86, metallic=0.0),
        make_mat("OW_LeatherDark", (0.10, 0.07, 0.05), roughness=0.45, metallic=0.0),
        make_mat("OW_HeadlightGlow", (1.0, 0.93, 0.74), roughness=0.12, emission=(1.0, 0.88, 0.58)),
        make_mat("OW_TailGlow", (0.95, 0.12, 0.10), roughness=0.16, emission=(0.95, 0.12, 0.10)),
    ]
    PAINT, CHR, DARK, RUB, LEATH, GLOW, TAIL = 0, 1, 2, 3, 4, 5, 6

    front = Vector((0.0,  0.73, 0.36))
    rear  = Vector((0.0, -0.73, 0.36))

    # --- A. Frame, Sculpted Teardrop Tank, Finned Twin Engine, Seat & Dual Exhausts ---
    bm_f = bmesh.new()
    # Contoured teardrop metallic fuel tank with recessed knee contours + chrome filler cap!
    add_bm(bm_f, ellipsoid_bm((0.0, 0.16, 0.84), (0.205, 0.335, 0.158), 20, 12, (-0.16, 0, 0)), PAINT)
    add_bm(bm_f, rod_bm((0.0, 0.24, 0.985), (0.0, 0.24, 1.008), 0.036, 0.036, 12), CHR)
    # Sculpted stepped stitched leather cafe-racer seat + sculpted rear tail cowl!
    add_bm(bm_f, bevel_box_bm((0.0, -0.34, 0.825), (0.31, 0.56, 0.105), 0.042, 2, (0.04, 0, 0)), LEATH)
    add_bm(bm_f, ellipsoid_bm((0.0, -0.66, 0.82), (0.165, 0.22, 0.105), 16, 10, (0.12, 0, 0)), PAINT)
    # Steel trellis chassis cradle tubes + rear swingarm + twin angled coil-over rear shocks!
    for sx in (-1.0, 1.0):
        add_bm(bm_f, rod_bm((sx * 0.13, 0.44, 0.84), (sx * 0.14, -0.14, 0.68), 0.017), DARK)
        add_bm(bm_f, rod_bm((sx * 0.13, 0.44, 0.84), (sx * 0.13,  0.22, 0.38), 0.017), DARK)
        add_bm(bm_f, rod_bm((sx * 0.13, 0.22, 0.38), (sx * 0.14, -0.18, 0.38), 0.018), DARK)
        add_bm(bm_f, rod_bm((sx * 0.14, -0.18, 0.38), (sx * 0.14, -0.73, 0.36), 0.022), DARK)  # swingarm
        add_bm(bm_f, rod_bm((sx * 0.15, -0.66, 0.40), (sx * 0.14, -0.44, 0.76), 0.022), CHR)   # rear coil shock
        add_bm(bm_f, rod_bm((sx * 0.14, -0.12, 0.34), (sx * 0.27, -0.12, 0.34), 0.016), RUB)   # rider footpegs
        # Twin curved chrome exhaust headers & tapered megaphones!
        add_bm(bm_f, rod_bm((sx * 0.15, 0.20, 0.50), (sx * 0.20, 0.22, 0.30), 0.024), CHR)
        add_bm(bm_f, rod_bm((sx * 0.20, 0.22, 0.30), (sx * 0.20, -0.34, 0.28), 0.026), CHR)
        add_bm(bm_f, rod_bm((sx * 0.20, -0.34, 0.28), (sx * 0.21, -0.84, 0.34), 0.036, 0.052, 14), CHR)
    # Air-cooled twin-cylinder engine block with 7 sculpted cooling fins & chrome side crankcases!
    add_bm(bm_f, bevel_box_bm((0.0, 0.04, 0.52), (0.28, 0.34, 0.32), 0.028, 2), DARK)
    for fi in range(7):
        add_bm(bm_f, bevel_box_bm((0.0, 0.08, 0.44 + fi * 0.034), (0.36, 0.28, 0.014), 0.004, 1), CHR)
    for sx in (-1.0, 1.0):
        add_bm(bm_f, rod_bm((sx * 0.14, 0.02, 0.42), (sx * 0.19, 0.02, 0.42), 0.095, 0.082, 14), CHR)
    # Rear curved fender, license plate holder, glowing LED taillight & amber turn blinkers!
    add_bm(bm_f, arc_fender_x_bm(rear, 0.395, 0.095, 0.014, 58, 165, 14), DARK)
    add_bm(bm_f, bevel_box_bm((0.0, -0.92, 0.76), (0.18, 0.05, 0.068), 0.012, 1), TAIL)
    add_bm(bm_f, bevel_box_bm((0.0, -0.94, 0.62), (0.16, 0.02, 0.11), 0.004, 1, (0.26, 0, 0)), CHR)
    obj_frame = finalize_obj("Frame", bm_f, mats, pivot=(0, 0, 0))

    # --- B. Telescopic Front Fork, Handlebars, Gauges, Mirrors & Round Chrome Headlamp ---
    bm_k = bmesh.new()
    for sx in (-1.0, 1.0):
        top_f = Vector((sx * 0.105, -0.23, 0.58))
        add_bm(bm_k, rod_bm((sx * 0.105, 0, 0), top_f, 0.028, 0.026, 12), CHR)
        add_bm(bm_k, rod_bm((sx * 0.105, -0.06, 0.16), top_f, 0.034, 0.032, 12), DARK)  # upper fork sliders
        # Swept naked-bike handlebars + rubber grips + bar mirrors
        hb_mid = top_f + Vector((sx * 0.18, -0.08, 0.12))
        hb_end = top_f + Vector((sx * 0.31, -0.15, 0.11))
        add_bm(bm_k, rod_bm(top_f + Vector((0, 0, 0.02)), hb_mid, 0.015), CHR)
        add_bm(bm_k, rod_bm(hb_mid, hb_end, 0.022), RUB)
        mir = hb_mid + Vector((sx * 0.07, 0.06, 0.16))
        add_bm(bm_k, rod_bm(hb_mid, mir, 0.008), CHR)
        add_bm(bm_k, bevel_box_bm(mir, (0.13, 0.026, 0.078), 0.009, 1), DARK)
        # Dual round instrument gauges
        add_bm(bm_k, rod_bm((sx * 0.056, -0.16, 0.61), (sx * 0.056, -0.19, 0.67), 0.044, 0.042, 12), CHR)
    # Triple-tree bridge clamps + sculpted front fender + bullet chrome headlamp!
    add_bm(bm_k, bevel_box_bm((0.0, -0.22, 0.56), (0.26, 0.07, 0.035), 0.008, 1), DARK)
    add_bm(bm_k, bevel_box_bm((0.0, -0.15, 0.38), (0.26, 0.07, 0.035), 0.008, 1), DARK)
    add_bm(bm_k, arc_fender_x_bm((0, 0, 0), 0.385, 0.086, 0.015, 45, 148, 14), PAINT)
    # Round chrome cafe-racer bullet headlamp
    add_bm(bm_k, rod_bm((0.0, -0.14, 0.49), (0.0, -0.02, 0.49), 0.082, 0.112, 16), DARK)
    add_bm(bm_k, rod_bm((0.0, -0.02, 0.49), (0.0,  0.01, 0.49), 0.116, 0.112, 16), CHR)
    add_bm(bm_k, ellipsoid_bm((0.0, 0.012, 0.49), (0.098, 0.030, 0.098), 14, 8), GLOW)
    obj_fork = finalize_obj("Fork", bm_k, mats, pivot=front)

    # --- C. Fat Sport-Touring Motorcycle Wheels (5-Spoke Cast Alloy + Drilled Brake Rotors!) ---
    def make_moto_wheel(wname, piv, tire_r, tire_w):
        bm_w = bmesh.new()
        add_bm(bm_w, torus_x_bm((0, 0, 0), tire_r - tire_w, tire_w, 30, 12), RUB)
        add_bm(bm_w, torus_x_bm((0, 0, 0), tire_r - tire_w * 1.95, 0.018, 28, 8), DARK)
        add_bm(bm_w, rod_bm((-0.082, 0, 0), (0.082, 0, 0), 0.056, 0.056, 14), DARK)
        # Dual drilled chrome front/rear brake discs!
        for sx in (-0.068, 0.068):
            add_bm(bm_w, rod_bm((sx - 0.005, 0, 0), (sx + 0.005, 0, 0), 0.155, 0.155, 18), CHR)
        # 5 bold cast-alloy Y-spokes
        for k in range(5):
            a = 2.0 * math.pi * k / 5.0
            dy, dz = math.cos(a) * (tire_r - tire_w * 1.9), math.sin(a) * (tire_r - tire_w * 1.9)
            add_bm(bm_w, rod_bm((0, 0, 0), (0, dy, dz), 0.020, 0.015, 8), DARK)
        return finalize_obj(wname, bm_w, mats, pivot=piv)

    obj_wf = make_moto_wheel("Wheel_Front", front, 0.358, 0.062)
    obj_wr = make_moto_wheel("Wheel_Rear",  rear,  0.360, 0.082)

    export_fbx_unity([obj_frame, obj_fork, obj_wf, obj_wr], "OW_Motorcycle.fbx")


# ===========================================================================
# 3. OW_CustomerCar.fbx (Upgrade legacy boxy car to our 10k-tri sculpted Car_Sedan!)
# ===========================================================================
def upgrade_customer_car():
    sedan_fbx = os.path.join(ART_DIR, "Car_Sedan.fbx")
    wheel_fbx = os.path.join(ART_DIR, "Car_Wheel.fbx")
    if not (os.path.exists(sedan_fbx) and os.path.exists(wheel_fbx)):
        print("  [CustomerCar] Skipped: Car_Sedan.fbx / Car_Wheel.fbx not found.")
        return

    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Import sculpted Car_Sedan body
    bpy.ops.import_scene.fbx(filepath=sedan_fbx)
    body_objs = [o for o in bpy.data.objects if o.type == "MESH"]

    # Map Car_Sedan material names -> exact OW_CustomerCar slot names (OW_CarPaint, Windows, Headlights, TailLights, Black, Grey)
    mat_paint = make_mat("OW_CarPaint", (0.16, 0.34, 0.62), roughness=0.18, metallic=0.45)
    mat_accent = make_mat("OW_CarPaintAccent", (0.14, 0.30, 0.56), roughness=0.22, metallic=0.40)
    mat_win = make_mat("Windows", (0.08, 0.13, 0.18), roughness=0.05, metallic=0.15)
    mat_blk = make_mat("Black", (0.10, 0.10, 0.11), roughness=0.78, metallic=0.0)
    mat_grey = make_mat("Grey", (0.76, 0.78, 0.82), roughness=0.22, metallic=0.85)
    mat_hl = make_mat("Headlights", (1.0, 0.96, 0.84), roughness=0.10, emission=(1.0, 0.94, 0.78))
    mat_tl = make_mat("TailLights", (0.92, 0.10, 0.08), roughness=0.14, emission=(0.92, 0.08, 0.06))

    for o in body_objs:
        for i, m in enumerate(o.data.materials):
            n = (m.name if m else "").lower()
            if "paint" in n or "body" in n:
                o.data.materials[i] = mat_paint
            elif "glass" in n or "wind" in n:
                o.data.materials[i] = mat_win
            elif "head" in n or "front" in n:
                o.data.materials[i] = mat_hl
            elif "tail" in n or "brake" in n:
                o.data.materials[i] = mat_tl
            elif "chrome" in n or "silver" in n or "rim" in n or "reverse" in n or "plate" in n:
                o.data.materials[i] = mat_grey
            else:
                o.data.materials[i] = mat_blk

    # Join body meshes into a single clean 'SportsCar' object matching original OW_CustomerCar hierarchy
    bpy.ops.object.select_all(action="DESELECT")
    for o in body_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = body_objs[0]
    if len(body_objs) > 1:
        bpy.ops.object.join()
    car_body = bpy.context.active_object
    car_body.name = "SportsCar"
    # Car_Sedan faces +Y in Blender; OW_CustomerCar had front facing +Z in Unity (same as +Y in Blender when exported w/ axis_forward='Z'!).
    # Verify scale & ground clearance (lift slightly so wheel centers sit at z=0.33, bottom at z=0.0)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Import 4 sculpted Car_Wheel.fbx alloy rims at exact Sedan wheel wells!
    wheel_objs = []
    for wname, wpos, flip in [
        ("SportsCar_FrontLeftWheel",  (-0.82,  1.34, 0.34), False),
        ("SportsCar_FrontRightWheel", ( 0.82,  1.34, 0.34), True),
        ("SportsCar_RearLeftWheel",   (-0.82, -1.34, 0.34), False),
        ("SportsCar_RearRightWheel",  ( 0.82, -1.34, 0.34), True),
    ]:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=wheel_fbx)
        added = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        bpy.ops.object.select_all(action="DESELECT")
        for o in added:
            o.select_set(True)
        bpy.context.view_layer.objects.active = added[0]
        if len(added) > 1:
            bpy.ops.object.join()
        wo = bpy.context.active_object
        wo.name = wname
        wo.location = Vector(wpos)
        if flip:
            wo.rotation_euler = Euler((0, 0, math.pi), "XYZ")
        for i, m in enumerate(wo.data.materials):
            n = (m.name if m else "").lower()
            wo.data.materials[i] = mat_grey if ("rim" in n or "chrome" in n or "alloy" in n or "disc" in n) else mat_blk
        wheel_objs.append(wo)

    export_fbx_unity([car_body] + wheel_objs, "OW_CustomerCar.fbx")


if __name__ == "__main__":
    print("[GRIDROAM Phase 4 Batch 5] Generating sculpted 3D OW_Bicycle, OW_Motorcycle & OW_CustomerCar ...")
    build_bicycle()
    build_motorcycle()
    upgrade_customer_car()
    print("[GRIDROAM Phase 4 Batch 5] Done!")
