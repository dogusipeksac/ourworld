#!/usr/bin/env python3
"""
GRIDROAM Realistic 3D Asset Generator (Blender 5.3+ Python)
Generates mobile-optimized, sculpted, beveled, smooth-shaded, UV-unwrapped FBX assets
for Unity 6 URP (oriented cleanly: Blender +Y -> Unity +Z forward, +Z -> +Y up, +X -> +X right).

Run via:
  blender --background --python scripts/blender/generate_gridroam_models.py
"""

import math
import os
import random
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_FBX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")
BLEND_OUT_DIR = os.path.join(PROJECT_ROOT, "scripts", "blender", "blend_sources")
PREVIEW_PATH = os.environ.get(
    "GRIDROAM_PREVIEW_PATH",
    os.path.join(SCRIPT_DIR, "blender_models_preview.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Scene & Material Utilities
# ---------------------------------------------------------------------------
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.45, metallic=0.0, emission=None, emission_strength=0.0):
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


def get_common_materials():
    return {
        "BodyPaint": make_mat("BodyPaint", (0.68, 0.14, 0.14), roughness=0.22, metallic=0.42),
        "TrimDark": make_mat("TrimDark", (0.10, 0.11, 0.13), roughness=0.62, metallic=0.12),
        "Chrome": make_mat("Chrome", (0.86, 0.88, 0.90), roughness=0.14, metallic=0.92),
        "Glass": make_mat("Glass", (0.06, 0.10, 0.15), roughness=0.08, metallic=0.25),
        "Headlight": make_mat("Headlight", (1.0, 0.94, 0.78), roughness=0.12, metallic=0.0, emission=(1.0, 0.90, 0.65), emission_strength=2.2),
        "Taillight": make_mat("Taillight", (0.85, 0.08, 0.07), roughness=0.18, metallic=0.0, emission=(0.95, 0.10, 0.08), emission_strength=2.0),
        "ReverseLight": make_mat("ReverseLight", (0.90, 0.90, 0.88), roughness=0.20, metallic=0.0),
        "WheelRubber": make_mat("WheelRubber", (0.09, 0.09, 0.10), roughness=0.82, metallic=0.0),
        "InteriorDark": make_mat("InteriorDark", (0.12, 0.12, 0.13), roughness=0.80, metallic=0.0),
        "BrakeCaliper": make_mat("BrakeCaliper", (0.76, 0.11, 0.09), roughness=0.32, metallic=0.35),
        "Bark": make_mat("Bark", (0.34, 0.23, 0.15), roughness=0.88, metallic=0.0),
        "FoliageA": make_mat("FoliageA", (0.28, 0.48, 0.22), roughness=0.78, metallic=0.0),
        "FoliageB": make_mat("FoliageB", (0.38, 0.56, 0.25), roughness=0.76, metallic=0.0),
        "StoneTrim": make_mat("StoneTrim", (0.78, 0.75, 0.70), roughness=0.72, metallic=0.0),
        "Concrete": make_mat("Concrete", (0.64, 0.64, 0.62), roughness=0.82, metallic=0.0),
        "Water": make_mat("Water", (0.08, 0.28, 0.42), roughness=0.08, metallic=0.10),
        "AccentTeal": make_mat("AccentTeal", (0.14, 0.45, 0.48), roughness=0.35, metallic=0.18),
        "AccentMustard": make_mat("AccentMustard", (0.82, 0.62, 0.16), roughness=0.38, metallic=0.10),
        "AccentBlue": make_mat("AccentBlue", (0.16, 0.34, 0.58), roughness=0.36, metallic=0.22),
        "LitScreen": make_mat("LitScreen", (0.92, 0.86, 0.66), roughness=0.22, metallic=0.0, emission=(0.95, 0.86, 0.62), emission_strength=1.5),
        "CanRed": make_mat("CanRed", (0.78, 0.14, 0.14), roughness=0.28, metallic=0.55),
        "CanGreen": make_mat("CanGreen", (0.14, 0.56, 0.28), roughness=0.28, metallic=0.55),
        "CanOrange": make_mat("CanOrange", (0.90, 0.46, 0.10), roughness=0.28, metallic=0.55),
        "CanBlue": make_mat("CanBlue", (0.14, 0.38, 0.76), roughness=0.28, metallic=0.55),
        "ContainerPaint": make_mat("ContainerPaint", (0.68, 0.22, 0.17), roughness=0.52, metallic=0.45),
    }


def apply_auto_smooth(obj, angle_deg=36.0):
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
            ang = edge.calc_face_angle(0.0)
            edge.smooth = ang < thresh
        else:
            edge.smooth = False
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def box_uv_project(obj, scale=1.0):
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


def create_mesh_object(name, bm, mat=None, smooth_angle=36.0, uv_scale=1.0):
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


def add_beveled_box(name, center, size, mat, bevel=0.03, segments=2, rot_euler=(0.0, 0.0, 0.0), smooth_angle=35.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.2:
        bmesh.ops.bevel(
            bm,
            geom=list(bm.edges),
            offset=bevel,
            offset_type="OFFSET",
            segments=segments,
            profile=0.5,
            affect="EDGES",
            clamp_overlap=True,
        )
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return create_mesh_object(name, bm, mat=mat, smooth_angle=smooth_angle)


def add_cylinder_part(name, center, radius_bottom, radius_top, height, segments, mat, rot_euler=(0.0, 0.0, 0.0), smooth_angle=42.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        cap_tris=False,
        segments=segments,
        radius1=radius_bottom,
        radius2=radius_top,
        depth=height,
    )
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return create_mesh_object(name, bm, mat=mat, smooth_angle=smooth_angle)


def add_revolved_profile(name, profile_rz, segments, mat, center=(0.0, 0.0, 0.0), axis="Z", smooth_angle=44.0):
    """Revolve a 2D (radius, axial_coord) profile into a smooth solid mesh."""
    bm = bmesh.new()
    rings = []
    for r, z in profile_rz:
        ring = []
        for s in range(segments):
            ang = (2.0 * math.pi * s) / segments
            ca, sa = math.cos(ang), math.sin(ang)
            if axis == "Z":
                co = Vector((r * ca, r * sa, z))
            elif axis == "X":
                co = Vector((z, r * ca, r * sa))
            else:
                co = Vector((r * ca, z, r * sa))
            ring.append(bm.verts.new(co))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(segments):
            sn = (s + 1) % segments
            try:
                bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
            except ValueError:
                pass

    # Cap bottom & top ONLY for open solid profiles (NOT closed ring/torus profiles like tires & rims!)
    is_closed_loop = (
        abs(profile_rz[0][0] - profile_rz[-1][0]) < 0.002
        and abs(profile_rz[0][1] - profile_rz[-1][1]) < 0.002
    )
    if not is_closed_loop:
        if profile_rz[0][0] > 0.002:
            try:
                bm.faces.new(list(reversed(rings[0])))
            except ValueError:
                pass
        if profile_rz[-1][0] > 0.002:
            try:
                bm.faces.new(rings[-1])
            except ValueError:
                pass

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)), verts=bm.verts)
    return create_mesh_object(name, bm, mat=mat, smooth_angle=smooth_angle)


def add_tube_along_points(name, points, radii, sides, mat, cap_ends=True, smooth_angle=50.0):
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
            offset = (side_x * math.cos(ang) + side_y * math.sin(ang)) * r
            ring.append(bm.verts.new(p + offset))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(sides):
            sn = (s + 1) % sides
            try:
                bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
            except ValueError:
                pass

    if cap_ends and len(rings) >= 2:
        try:
            bm.faces.new(list(reversed(rings[0])))
        except ValueError:
            pass
        try:
            bm.faces.new(rings[-1])
        except ValueError:
            pass

    return create_mesh_object(name, bm, mat=mat, smooth_angle=smooth_angle)


def join_objects(objs, final_name, smooth_angle=None):
    bpy.ops.object.select_all(action="DESELECT")
    valid = [o for o in objs if o is not None]
    for o in valid:
        o.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = final_name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if smooth_angle is not None:
        apply_auto_smooth(joined, angle_deg=smooth_angle)
    return joined


def export_fbx(obj, filename):
    filepath = os.path.join(OUT_FBX_DIR, filename)
    bpy.ops.object.select_all(action="DESELECT")
    targets = [obj] + list(obj.children_recursive)
    for t in targets:
        t.select_set(True)
    bpy.context.view_layer.objects.active = obj
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
    verts = sum(len(t.data.vertices) for t in targets if t.type == "MESH")
    tris = sum(sum(max(1, len(p.vertices) - 2) for p in t.data.polygons) for t in targets if t.type == "MESH")
    mat_names = sorted({m.name for t in targets if t.type == "MESH" for m in t.data.materials if m})
    print(f"  Exported {filename:26s} | verts={verts:5d} | tris={tris:5d} | parts={len(targets)} | mats={mat_names}")
    return filepath


def finalize_car_with_light_children(body_parts, head_parts, tail_parts, rev_parts, car_name):
    body = join_objects(body_parts, car_name)
    for child_parts, child_name in (
        (head_parts, "Car_Headlights"),
        (tail_parts, "Car_Taillights"),
        (rev_parts, "Car_ReverseLights"),
    ):
        if child_parts:
            c_obj = join_objects(child_parts, child_name)
            c_obj.parent = body
    return body


# ---------------------------------------------------------------------------
# 1. Realistic Sculpted Car Wheel (Tire + 5-Twin-Spoke Alloy Rim + Brake Caliper)
# ---------------------------------------------------------------------------
def build_car_wheel(mats):
    parts = []
    # Revolved tire profile centered at origin, axle along X (width 0.24m, outer radius 0.34m, rim inner 0.215m)
    tire_profile = [
        (0.210, -0.105),
        (0.232, -0.118),
        (0.278, -0.124),
        (0.320, -0.118),
        (0.336, -0.102),
        (0.340, -0.065),
        (0.341, 0.000),
        (0.340, 0.065),
        (0.336, 0.102),
        (0.320, 0.118),
        (0.278, 0.124),
        (0.232, 0.118),
        (0.210, 0.105),
        (0.205, 0.088),
        (0.205, -0.088),
        (0.210, -0.105),
    ]
    parts.append(add_revolved_profile("Tire", tire_profile, 28, mats["WheelRubber"], axis="X", smooth_angle=52.0))

    # Metallic outer rim barrel & lip
    rim_profile = [
        (0.185, -0.095),
        (0.214, -0.098),
        (0.216, -0.075),
        (0.196, -0.065),
        (0.196, 0.065),
        (0.216, 0.075),
        (0.214, 0.098),
        (0.185, 0.095),
        (0.185, -0.095),
    ]
    parts.append(add_revolved_profile("RimBarrel", rim_profile, 24, mats["Chrome"], axis="X", smooth_angle=44.0))

    # Concave center hub
    parts.append(add_cylinder_part("RimHub", (0.0, 0.0, 0.0), 0.064, 0.056, 0.13, 18, mats["Chrome"], rot_euler=(0.0, math.pi * 0.5, 0.0)))
    parts.append(add_cylinder_part("CenterCap_L", (-0.068, 0.0, 0.0), 0.036, 0.034, 0.014, 16, mats["TrimDark"], rot_euler=(0.0, math.pi * 0.5, 0.0)))
    parts.append(add_cylinder_part("CenterCap_R", (0.068, 0.0, 0.0), 0.036, 0.034, 0.014, 16, mats["TrimDark"], rot_euler=(0.0, math.pi * 0.5, 0.0)))

    # 5 sculpted twin alloy spokes (visible cleanly from both left and right sides)
    for i in range(5):
        base_ang = (2.0 * math.pi * i) / 5.0
        for side_sign in (-1.0, 1.0):
            for twin_off in (-0.11, 0.11):
                ang = base_ang + twin_off
                cy = math.cos(ang) * 0.125
                cz = math.sin(ang) * 0.125
                # Spoke slightly angled outward toward rim lip
                bm = bmesh.new()
                bmesh.ops.create_cube(bm, size=1.0)
                bmesh.ops.scale(bm, vec=Vector((0.022, 0.145, 0.024)), verts=bm.verts)
                bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.004, segments=1, affect="EDGES", clamp_overlap=True)
                rot_x = Matrix.Rotation(ang, 4, "X")
                tilt = Matrix.Rotation(side_sign * 0.12, 4, "Z")
                bmesh.ops.transform(
                    bm,
                    matrix=Matrix.Translation(Vector((side_sign * 0.058, cy, cz))) @ rot_x @ tilt,
                    verts=bm.verts,
                )
                parts.append(create_mesh_object(f"Spoke_{i}_{side_sign}_{twin_off}", bm, mat=mats["Chrome"], smooth_angle=35.0))

        # 5 lug nuts on each side
        ly = math.cos(base_ang) * 0.044
        lz = math.sin(base_ang) * 0.044
        parts.append(add_cylinder_part(f"LugL_{i}", (-0.064, ly, lz), 0.008, 0.007, 0.016, 6, mats["TrimDark"], rot_euler=(0.0, math.pi * 0.5, 0.0)))
        parts.append(add_cylinder_part(f"LugR_{i}", (0.064, ly, lz), 0.008, 0.007, 0.016, 6, mats["TrimDark"], rot_euler=(0.0, math.pi * 0.5, 0.0)))

    # Brake rotor disc + red brake caliper tucked inside rim
    parts.append(add_cylinder_part("BrakeDisc", (0.0, 0.0, 0.0), 0.150, 0.150, 0.026, 20, mats["TrimDark"], rot_euler=(0.0, math.pi * 0.5, 0.0)))
    parts.append(add_beveled_box("BrakeCaliper", (0.0, 0.115, 0.045), (0.056, 0.068, 0.095), mats["BrakeCaliper"], bevel=0.010, segments=1))

    return join_objects(parts, "Car_Wheel")


# ---------------------------------------------------------------------------
# 2. Lofted Sculpted Vehicle Body Helper (with Wheel Arch Cutouts!)
# ---------------------------------------------------------------------------
def build_lofted_lower_hull(name, sections, mat, front_axle_y, rear_axle_y, arch_radius, arch_center_z):
    """
    Build a smooth, realistic lower vehicle body lofted across Y stations.
    sections: list of (y, half_width_bottom, half_width_mid, half_width_top, z_bottom, z_mid, z_top_side, z_top_crown)
    Automatically arches the bottom sill upward over the front & rear wheel wells!
    """
    bm = bmesh.new()
    rings = []

    for (y, hw_b, hw_m, hw_t, z_b, z_m, z_ts, z_tc) in sections:
        # Check if this Y station falls inside front or rear wheel arch
        arch_lift = 0.0
        for ay in (front_axle_y, rear_axle_y):
            dy = abs(y - ay)
            if dy < arch_radius:
                arch_z = arch_center_z + math.sqrt(max(0.0, arch_radius * arch_radius - dy * dy))
                arch_lift = max(arch_lift, max(0.0, arch_z - z_b))

        z_sill = z_b + arch_lift
        z_mid_adj = max(z_m, z_sill + 0.05)

        # 10-point symmetric closed cross-section (clockwise when looking from +Y)
        pts = [
            Vector((0.0, y, z_b)),
            Vector((hw_b * 0.72, y, z_b + 0.02)),
            Vector((hw_b, y, z_sill + 0.03)),
            Vector((hw_m, y, z_mid_adj)),
            Vector((hw_t, y, z_ts)),
            Vector((hw_t * 0.52, y, (z_ts + z_tc) * 0.5 + 0.012)),
            Vector((0.0, y, z_tc)),
            Vector((-hw_t * 0.52, y, (z_ts + z_tc) * 0.5 + 0.012)),
            Vector((-hw_t, y, z_ts)),
            Vector((-hw_m, y, z_mid_adj)),
            Vector((-hw_b, y, z_sill + 0.03)),
            Vector((-hw_b * 0.72, y, z_b + 0.02)),
        ]
        rings.append([bm.verts.new(p) for p in pts])

    n_ring = len(rings[0])
    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for k in range(n_ring):
            kn = (k + 1) % n_ring
            bm.faces.new((r0[k], r0[kn], r1[kn], r1[k]))

    # Cap rear and front
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))

    return create_mesh_object(name, bm, mat=mat, smooth_angle=38.0)


def build_wheel_arch_flares_and_wells(parts, half_wid, front_y, rear_y, arch_r, arch_z, mats):
    """Add realistic dark recessed inner wheel-well arches and subtle fender lip flares."""
    for label, ay in (("F", front_y), ("R", rear_y)):
        for side_label, sx in (("L", -1.0), ("R", 1.0)):
            # Semicircular outer fender flare arch
            arc_pts = []
            for k in range(13):
                t = k / 12.0
                ang = math.pi * t  # 0..pi
                py = ay + math.cos(ang) * (arch_r + 0.015)
                pz = arch_z + math.sin(ang) * (arch_r + 0.015)
                arc_pts.append((sx * (half_wid + 0.012), py, pz))
            parts.append(add_tube_along_points(f"Flare_{label}_{side_label}", arc_pts, 0.032, 6, mats["TrimDark"], cap_ends=True, smooth_angle=45.0))

            # Dark recessed wheel-well liner shell behind the arch
            well_r = arch_r - 0.03
            bm = bmesh.new()
            inner_x = sx * (half_wid - 0.34)
            outer_x = sx * (half_wid - 0.02)
            ring_in, ring_out = [], []
            for k in range(11):
                ang = math.pi * (k / 10.0)
                py = ay + math.cos(ang) * well_r
                pz = arch_z + math.sin(ang) * well_r
                ring_in.append(bm.verts.new(Vector((inner_x, py, pz))))
                ring_out.append(bm.verts.new(Vector((outer_x, py, pz))))
            for k in range(10):
                bm.faces.new((ring_in[k], ring_in[k + 1], ring_out[k + 1], ring_out[k]))
            # Back plate closing wheel well
            center_v = bm.verts.new(Vector((inner_x, ay, arch_z)))
            for k in range(10):
                bm.faces.new((center_v, ring_in[k + 1], ring_in[k]))
            parts.append(create_mesh_object(f"Well_{label}_{side_label}", bm, mat=mats["TrimDark"], smooth_angle=45.0))


def build_cabin_greenhouse(parts, y_back_base, y_back_roof, y_front_roof, y_front_base, z_belt, z_roof, hw_belt, hw_roof, mats, rear_window_open=True):
    """
    Build a realistic tapered car cabin with sculpted A/B/C pillars, recessed dark glass,
    curved roof panel, and subtle dark interior seating/dashboard!
    """
    # 1. Recessed tinted glass core (slightly inset inside pillars so pillars stand proud!)
    bm = bmesh.new()
    inset = 0.032
    y_bb = y_back_base + 0.03
    y_br = y_back_roof + 0.02
    y_fr = y_front_roof - 0.02
    y_fb = y_front_base - 0.03
    hwb = hw_belt - inset
    hwr = hw_roof - inset
    zr = z_roof - 0.045

    v0 = bm.verts.new(Vector((-hwb, y_bb, z_belt)))
    v1 = bm.verts.new(Vector((hwb, y_bb, z_belt)))
    v2 = bm.verts.new(Vector((hwb, y_fb, z_belt)))
    v3 = bm.verts.new(Vector((-hwb, y_fb, z_belt)))

    v4 = bm.verts.new(Vector((-hwr, y_br, zr)))
    v5 = bm.verts.new(Vector((hwr, y_br, zr)))
    v6 = bm.verts.new(Vector((hwr, y_fr, zr)))
    v7 = bm.verts.new(Vector((-hwr, y_fr, zr)))

    bm.faces.new((v0, v1, v5, v4))  # Rear glass
    bm.faces.new((v2, v3, v7, v6))  # Front windshield
    bm.faces.new((v1, v2, v6, v5))  # Right side glass
    bm.faces.new((v3, v0, v4, v7))  # Left side glass
    bm.faces.new((v4, v5, v6, v7))  # Top cap
    parts.append(create_mesh_object("Cabin_GlassCore", bm, mat=mats["Glass"], smooth_angle=28.0))

    # 2. Sculpted crowned roof cap (BodyPaint)
    bm_r = bmesh.new()
    crown = 0.055
    ro_y0, ro_y1 = y_back_roof - 0.04, y_front_roof + 0.04
    ro_x = hw_roof + 0.015
    # 3 longitudinal strips for smooth crowned roof
    ys = [ro_y0, (ro_y0 + ro_y1) * 0.5, ro_y1]
    roof_rings = []
    for yi, yy in enumerate(ys):
        end_drop = -0.022 if yi != 1 else 0.0
        pts = [
            Vector((-ro_x, yy, zr - 0.015)),
            Vector((-ro_x * 0.94, yy, z_roof - 0.01 + end_drop)),
            Vector((-ro_x * 0.45, yy, z_roof + crown * 0.75 + end_drop)),
            Vector((0.0, yy, z_roof + crown + end_drop)),
            Vector((ro_x * 0.45, yy, z_roof + crown * 0.75 + end_drop)),
            Vector((ro_x * 0.94, yy, z_roof - 0.01 + end_drop)),
            Vector((ro_x, yy, zr - 0.015)),
        ]
        roof_rings.append([bm_r.verts.new(p) for p in pts])
    for i in range(len(roof_rings) - 1):
        r0, r1 = roof_rings[i], roof_rings[i + 1]
        for k in range(len(r0) - 1):
            bm_r.faces.new((r0[k], r0[k + 1], r1[k + 1], r1[k]))
    # Close underside + front/rear roof header rails
    for i in range(len(roof_rings) - 1):
        r0, r1 = roof_rings[i], roof_rings[i + 1]
        bm_r.faces.new((r0[-1], r0[0], r1[0], r1[-1]))
    bm_r.faces.new(roof_rings[0])
    bm_r.faces.new(list(reversed(roof_rings[-1])))
    parts.append(create_mesh_object("Cabin_RoofPanel", bm_r, mat=mats["BodyPaint"], smooth_angle=45.0))

    # 3. Sculpted A-pillars, B-pillars, C-pillars & window sill trim
    for sx in (-1.0, 1.0):
        # A-pillar (front windshield frame)
        p_a0 = Vector((sx * (hw_belt - 0.01), y_front_base - 0.02, z_belt - 0.02))
        p_a1 = Vector((sx * (hw_roof - 0.005), y_front_roof + 0.01, z_roof - 0.02))
        parts.append(add_tube_along_points(f"Pillar_A_{sx}", [p_a0, p_a1], [0.048, 0.042], 6, mats["BodyPaint"], smooth_angle=35.0))

        # C-pillar (rear quarter pillar - wider, painted)
        p_c0 = Vector((sx * (hw_belt - 0.01), y_back_base + 0.04, z_belt - 0.02))
        p_c1 = Vector((sx * (hw_roof - 0.005), y_back_roof - 0.01, z_roof - 0.02))
        parts.append(add_tube_along_points(f"Pillar_C_{sx}", [p_c0, p_c1], [0.072, 0.056], 6, mats["BodyPaint"], smooth_angle=35.0))

        # B-pillar (center matte-black divider between front & rear doors)
        y_mid_b = (y_back_base + y_front_base) * 0.46
        if abs(y_front_roof - y_back_roof) > 1.15:
            p_b0 = Vector((sx * (hw_belt - 0.018), y_mid_b, z_belt))
            p_b1 = Vector((sx * (hw_roof - 0.015), y_mid_b - 0.03, zr))
            parts.append(add_tube_along_points(f"Pillar_B_{sx}", [p_b0, p_b1], [0.036, 0.032], 6, mats["TrimDark"], smooth_angle=30.0))

        # Lower chrome window beltline trim strip
        parts.append(add_tube_along_points(
            f"WindowBeltTrim_{sx}",
            [Vector((sx * (hw_belt + 0.004), y_back_base + 0.06, z_belt + 0.01)),
             Vector((sx * (hw_belt + 0.004), y_front_base - 0.04, z_belt + 0.01))],
            [0.014, 0.014], 6, mats["Chrome"], smooth_angle=35.0,
        ))

    # Subtle shark-fin roof antenna
    parts.append(add_beveled_box("RoofAntenna", (0.0, y_back_roof + 0.22, z_roof + crown + 0.03), (0.04, 0.14, 0.07), mats["TrimDark"], bevel=0.01, segments=1))


def add_car_trim_details(parts, head_parts, tail_parts, rev_parts, half_wid, half_len, front_y, rear_y, z_bumper, z_light, mirror_y, mirror_z, door_handle_ys, mats, is_pickup=False):
    """Add contoured front/rear bumpers, sculpted grille, separate-child lights, mirrors, handles, plates, exhaust."""
    # Contoured front bumper tucked cleanly to tapered nose width + chin lip
    bumper_w = half_wid * (1.82 if is_pickup else 1.72)
    parts.append(add_beveled_box("FrontBumper", (0.0, half_len - 0.08, z_bumper), (bumper_w, 0.22, 0.22), mats["TrimDark"], bevel=0.065, segments=2))
    parts.append(add_beveled_box("FrontChinSpoiler", (0.0, half_len - 0.03, z_bumper - 0.085), (bumper_w - 0.14, 0.14, 0.055), mats["TrimDark"], bevel=0.018, segments=1))
    # Lower bumper mesh air intake
    parts.append(add_beveled_box("LowerIntake", (0.0, half_len + 0.015, z_bumper + 0.01), (bumper_w * 0.52, 0.05, 0.11), mats["InteriorDark"], bevel=0.015, segments=1))

    # Sculpted chrome grille surround + horizontal chrome bars
    grille_w = 0.80 if not is_pickup else 0.96
    grille_h = 0.20 if not is_pickup else 0.28
    parts.append(add_beveled_box("GrilleFrame", (0.0, half_len - 0.05, z_light - 0.03), (grille_w, 0.08, grille_h), mats["Chrome"], bevel=0.022, segments=2))
    parts.append(add_beveled_box("GrilleRecess", (0.0, half_len - 0.03, z_light - 0.03), (grille_w - 0.07, 0.07, grille_h - 0.06), mats["TrimDark"], bevel=0.01, segments=1))
    for k in range(3):
        gz = (z_light - 0.03) + (k - 1) * (grille_h * 0.24)
        parts.append(add_beveled_box(f"GrilleSlat_{k}", (0.0, half_len - 0.005, gz), (grille_w - 0.08, 0.025, 0.018), mats["Chrome"], bevel=0.005, segments=1))

    # Recessed wraparound Headlights + fog lights tucked cleanly inside tapered nose
    hl_x = bumper_w * 0.5 - 0.23
    for sx in (-1.0, 1.0):
        parts.append(add_beveled_box(f"HeadlightHousing_{sx}", (sx * hl_x, half_len - 0.10, z_light), (0.34, 0.14, 0.14), mats["TrimDark"], bevel=0.028, segments=2, rot_euler=(0.0, 0.0, -sx * 0.20)))
        head_parts.append(add_beveled_box(f"HeadlightLens_{sx}", (sx * hl_x, half_len - 0.07, z_light), (0.30, 0.12, 0.105), mats["Headlight"], bevel=0.022, segments=2, rot_euler=(0.0, 0.0, -sx * 0.20)))
        parts.append(add_beveled_box(f"FogLight_{sx}", (sx * (hl_x - 0.02), half_len - 0.01, z_bumper + 0.01), (0.14, 0.06, 0.06), mats["Chrome"], bevel=0.012, segments=1))

    # Contoured rear bumper + diffuser + dual chrome exhaust tips
    rear_w = half_wid * (1.88 if is_pickup else 1.78)
    parts.append(add_beveled_box("RearBumper", (0.0, -half_len + 0.07, z_bumper + 0.02), (rear_w, 0.20, 0.23), mats["TrimDark"], bevel=0.06, segments=2))
    for sx in (-1.0, 1.0):
        parts.append(add_cylinder_part(f"Exhaust_{sx}", (sx * (half_wid * 0.50), -half_len - 0.03, z_bumper - 0.07), 0.042, 0.042, 0.15, 12, mats["Chrome"], rot_euler=(math.pi * 0.5, 0.0, 0.0)))

    # Sculpted Taillights + Reverse Lights (separate children for runtime brake/reverse glow!)
    tl_z = z_light + 0.04
    tl_x = rear_w * 0.5 - 0.22
    for sx in (-1.0, 1.0):
        tail_parts.append(add_beveled_box(f"Taillight_{sx}", (sx * tl_x, -half_len + 0.06, tl_z), (0.36, 0.11, 0.13), mats["Taillight"], bevel=0.024, segments=2, rot_euler=(0.0, 0.0, sx * 0.12)))
        rev_parts.append(add_beveled_box(f"ReverseLight_{sx}", (sx * (tl_x - 0.22), -half_len + 0.05, tl_z - 0.01), (0.12, 0.09, 0.095), mats["ReverseLight"], bevel=0.016, segments=1))

    # License plates front & rear
    parts.append(add_beveled_box("PlateFront", (0.0, half_len + 0.03, z_bumper + 0.01), (0.44, 0.024, 0.105), mats["ReverseLight"], bevel=0.005, segments=1))
    parts.append(add_beveled_box("PlateRear", (0.0, -half_len - 0.03, z_bumper + 0.14), (0.44, 0.024, 0.105), mats["ReverseLight"], bevel=0.005, segments=1))

    # Aerodynamic side mirrors on angled stalks
    for sx in (-1.0, 1.0):
        parts.append(add_tube_along_points(
            f"MirrorStalk_{sx}",
            [Vector((sx * (half_wid - 0.06), mirror_y, mirror_z - 0.04)),
             Vector((sx * (half_wid + 0.06), mirror_y - 0.03, mirror_z + 0.01))],
            [0.022, 0.018], 6, mats["TrimDark"],
        ))
        parts.append(add_beveled_box(f"MirrorCap_{sx}", (sx * (half_wid + 0.11), mirror_y - 0.03, mirror_z + 0.02), (0.16, 0.10, 0.10), mats["BodyPaint"], bevel=0.032, segments=2))
        parts.append(add_beveled_box(f"MirrorGlass_{sx}", (sx * (half_wid + 0.11), mirror_y - 0.078, mirror_z + 0.02), (0.12, 0.015, 0.075), mats["Glass"], bevel=0.004, segments=1))

    # Recessed Door Handles + Rocker Sills
    for sx in (-1.0, 1.0):
        for di, dy in enumerate(door_handle_ys):
            parts.append(add_beveled_box(f"DoorHandle_{sx}_{di}", (sx * (half_wid + 0.002), dy, z_light + 0.11), (0.032, 0.14, 0.030), mats["Chrome"], bevel=0.008, segments=1))
        parts.append(add_beveled_box(f"RockerSkirt_{sx}", (sx * (half_wid - 0.02), (front_y + rear_y) * 0.5, z_bumper - 0.08), (0.08, (front_y - rear_y) - 0.84, 0.09), mats["TrimDark"], bevel=0.02, segments=1))


# ---------------------------------------------------------------------------
# 3. Vehicle Models: Sedan, Hatchback, Pickup
# ---------------------------------------------------------------------------
def build_car_sedan(mats):
    parts = []
    # Length 4.42m (y = -2.21..+2.21), Width 1.88m (hw = 0.94), frontAxle = +1.38, rearAxle = -1.32
    front_y, rear_y = 1.38, -1.32
    arch_r, arch_z = 0.42, 0.40
    # (y, hw_bot, hw_mid, hw_top, z_bot, z_mid, z_top_side, z_top_crown)
    sections = [
        (-2.19, 0.74, 0.84, 0.78, 0.34, 0.62, 0.92, 0.94),
        (-2.05, 0.84, 0.91, 0.85, 0.30, 0.64, 0.96, 0.99),
        (-1.74, 0.88, 0.94, 0.88, 0.27, 0.65, 0.98, 1.01),
        (-1.55, 0.89, 0.94, 0.88, 0.27, 0.66, 0.98, 1.01),
        (-1.32, 0.90, 0.94, 0.88, 0.27, 0.66, 0.98, 1.01),
        (-1.05, 0.89, 0.94, 0.88, 0.27, 0.66, 0.97, 1.00),
        (-0.88, 0.89, 0.93, 0.87, 0.27, 0.65, 0.96, 0.99),
        (0.00,  0.89, 0.93, 0.87, 0.26, 0.64, 0.95, 0.98),
        (0.84,  0.89, 0.93, 0.87, 0.26, 0.64, 0.94, 0.98),
        (1.08,  0.89, 0.94, 0.87, 0.26, 0.64, 0.92, 0.96),
        (1.38,  0.89, 0.94, 0.86, 0.26, 0.63, 0.89, 0.94),
        (1.66,  0.88, 0.93, 0.84, 0.27, 0.62, 0.85, 0.90),
        (1.98,  0.83, 0.89, 0.78, 0.29, 0.60, 0.80, 0.85),
        (2.19,  0.72, 0.80, 0.70, 0.34, 0.58, 0.74, 0.78),
    ]
    parts.append(build_lofted_lower_hull("Sedan_LowerBody", sections, mats["BodyPaint"], front_y, rear_y, arch_r, arch_z))
    build_wheel_arch_flares_and_wells(parts, 0.93, front_y, rear_y, arch_r, arch_z, mats)
    build_cabin_greenhouse(
        parts,
        y_back_base=-1.40, y_back_roof=-0.92,
        y_front_roof=0.44, y_front_base=0.98,
        z_belt=0.95, z_roof=1.42,
        hw_belt=0.84, hw_roof=0.69,
        mats=mats,
    )
    # Subtle rear trunk lip spoiler
    parts.append(add_beveled_box("TrunkSpoiler", (0.0, -2.08, 0.98), (1.52, 0.14, 0.045), mats["BodyPaint"], bevel=0.015, segments=1))
    head_parts, tail_parts, rev_parts = [], [], []
    add_car_trim_details(
        parts, head_parts, tail_parts, rev_parts, half_wid=0.94, half_len=2.21, front_y=front_y, rear_y=rear_y,
        z_bumper=0.39, z_light=0.71, mirror_y=0.62, mirror_z=0.97,
        door_handle_ys=(-0.58, 0.20), mats=mats, is_pickup=False,
    )
    return finalize_car_with_light_children(parts, head_parts, tail_parts, rev_parts, "Car_Sedan")


def build_car_hatchback(mats):
    parts = []
    # Length 3.90m (y = -1.95..+1.95), Width 1.80m (hw = 0.90), frontAxle = +1.22, rearAxle = -1.22
    front_y, rear_y = 1.22, -1.22
    arch_r, arch_z = 0.40, 0.40
    sections = [
        (-1.93, 0.72, 0.82, 0.76, 0.32, 0.62, 0.94, 0.96),
        (-1.76, 0.82, 0.89, 0.83, 0.27, 0.64, 0.96, 0.98),
        (-1.50, 0.85, 0.90, 0.84, 0.25, 0.64, 0.96, 0.98),
        (-1.22, 0.86, 0.90, 0.84, 0.25, 0.64, 0.96, 0.98),
        (-0.92, 0.85, 0.90, 0.84, 0.25, 0.64, 0.95, 0.97),
        (0.00,  0.85, 0.90, 0.84, 0.24, 0.63, 0.94, 0.96),
        (0.76,  0.85, 0.90, 0.84, 0.24, 0.62, 0.92, 0.95),
        (1.00,  0.85, 0.90, 0.83, 0.24, 0.61, 0.90, 0.93),
        (1.22,  0.85, 0.90, 0.82, 0.24, 0.60, 0.87, 0.91),
        (1.50,  0.83, 0.88, 0.79, 0.26, 0.59, 0.83, 0.87),
        (1.78,  0.78, 0.84, 0.74, 0.29, 0.57, 0.78, 0.82),
        (1.93,  0.68, 0.76, 0.66, 0.33, 0.55, 0.72, 0.76),
    ]
    parts.append(build_lofted_lower_hull("Hatch_LowerBody", sections, mats["BodyPaint"], front_y, rear_y, arch_r, arch_z))
    build_wheel_arch_flares_and_wells(parts, 0.89, front_y, rear_y, arch_r, arch_z, mats)
    build_cabin_greenhouse(
        parts,
        y_back_base=-1.76, y_back_roof=-1.34,
        y_front_roof=0.38, y_front_base=0.88,
        z_belt=0.93, z_roof=1.40,
        hw_belt=0.80, hw_roof=0.67,
        mats=mats,
    )
    # Sporty roof hatch spoiler
    parts.append(add_beveled_box("HatchRoofSpoiler", (0.0, -1.44, 1.40), (1.32, 0.20, 0.055), mats["BodyPaint"], bevel=0.018, segments=1, rot_euler=(-0.12, 0.0, 0.0)))
    head_parts, tail_parts, rev_parts = [], [], []
    add_car_trim_details(
        parts, head_parts, tail_parts, rev_parts, half_wid=0.90, half_len=1.95, front_y=front_y, rear_y=rear_y,
        z_bumper=0.37, z_light=0.69, mirror_y=0.56, mirror_z=0.95,
        door_handle_ys=(-0.56, 0.16), mats=mats, is_pickup=False,
    )
    return finalize_car_with_light_children(parts, head_parts, tail_parts, rev_parts, "Car_Hatchback")


def build_car_pickup(mats):
    parts = []
    # Length 5.00m (y = -2.50..+2.50), Width 1.94m (hw = 0.97), frontAxle = +1.56, rearAxle = -1.56
    front_y, rear_y = 1.56, -1.56
    arch_r, arch_z = 0.46, 0.44

    # Front + Cab lower body (y = -0.35..+2.48)
    cab_sections = [
        (-0.36, 0.92, 0.97, 0.91, 0.32, 0.72, 1.06, 1.08),
        (0.30,  0.92, 0.97, 0.91, 0.32, 0.72, 1.06, 1.08),
        (1.06,  0.92, 0.97, 0.91, 0.32, 0.72, 1.06, 1.10),
        (1.30,  0.92, 0.97, 0.91, 0.32, 0.72, 1.05, 1.10),
        (1.56,  0.92, 0.97, 0.90, 0.32, 0.72, 1.04, 1.09),
        (1.90,  0.91, 0.96, 0.88, 0.33, 0.71, 1.02, 1.07),
        (2.26,  0.86, 0.92, 0.84, 0.35, 0.70, 0.99, 1.03),
        (2.48,  0.78, 0.86, 0.78, 0.40, 0.69, 0.94, 0.98),
    ]
    parts.append(build_lofted_lower_hull("Pickup_FrontBody", cab_sections, mats["BodyPaint"], front_y, rear_y, arch_r, arch_z))

    # Real hollowed open cargo bed (y = -2.48..-0.38): bed floor, ribbed liner, side fenders & tailgate!
    parts.append(add_beveled_box("Pickup_BedBase", (0.0, -1.42, 0.48), (1.84, 2.08, 0.28), mats["TrimDark"], bevel=0.03, segments=1))
    # Ribbed floor strips inside truck bed
    for rx in (-0.64, -0.42, -0.20, 0.0, 0.20, 0.42, 0.64):
        parts.append(add_beveled_box(f"BedRib_{rx}", (rx, -1.42, 0.625), (0.06, 1.94, 0.024), mats["TrimDark"], bevel=0.005, segments=1))

    # Left & right sculpted rear bed fender walls (beveled)
    for sx in (-1.0, 1.0):
        parts.append(add_beveled_box(f"Pickup_BedSideWall_{sx}", (sx * 0.88, -1.42, 0.84), (0.16, 2.10, 0.48), mats["BodyPaint"], bevel=0.035, segments=2))
        parts.append(add_beveled_box(f"Pickup_BedRailCap_{sx}", (sx * 0.88, -1.42, 1.08), (0.17, 2.12, 0.038), mats["TrimDark"], bevel=0.01, segments=1))
    # Front bed bulkhead + Tailgate with chrome latch handle
    parts.append(add_beveled_box("Pickup_BedFrontWall", (0.0, -0.44, 0.84), (1.76, 0.14, 0.48), mats["BodyPaint"], bevel=0.025, segments=1))
    parts.append(add_beveled_box("Pickup_Tailgate", (0.0, -2.42, 0.83), (1.82, 0.14, 0.48), mats["BodyPaint"], bevel=0.03, segments=2))
    parts.append(add_beveled_box("Pickup_TailgateHandle", (0.0, -2.50, 0.96), (0.24, 0.04, 0.07), mats["TrimDark"], bevel=0.01, segments=1))

    build_wheel_arch_flares_and_wells(parts, 0.96, front_y, rear_y, arch_r, arch_z, mats)

    # Pickup Cab Greenhouse
    build_cabin_greenhouse(
        parts,
        y_back_base=-0.34, y_back_roof=-0.24,
        y_front_roof=0.68, y_front_base=1.18,
        z_belt=1.05, z_roof=1.62,
        hw_belt=0.87, hw_roof=0.75,
        mats=mats,
    )
    # Cab roof rack rails + protective roll bar behind cab
    for sx in (-1.0, 1.0):
        parts.append(add_tube_along_points(
            f"RollBar_{sx}",
            [Vector((sx * 0.78, -0.52, 1.08)),
             Vector((sx * 0.74, -0.46, 1.62)),
             Vector((sx * 0.0, -0.46, 1.62))],
            [0.036, 0.036, 0.036], 8, mats["TrimDark"],
        ))
        # Side step running boards
        parts.append(add_beveled_box(f"SideStep_{sx}", (sx * 0.96, 0.25, 0.34), (0.14, 1.48, 0.05), mats["Chrome"], bevel=0.012, segments=1))

    head_parts, tail_parts, rev_parts = [], [], []
    add_car_trim_details(
        parts, head_parts, tail_parts, rev_parts, half_wid=0.97, half_len=2.50, front_y=front_y, rear_y=rear_y,
        z_bumper=0.45, z_light=0.82, mirror_y=0.86, mirror_z=1.10,
        door_handle_ys=(0.30,), mats=mats, is_pickup=True,
    )
    return finalize_car_with_light_children(parts, head_parts, tail_parts, rev_parts, "Car_Pickup")


# ---------------------------------------------------------------------------
# 4. Organic Plane Tree (Çınar Ağacı) — Flared Roots, Forking Limbs & Multi-Cluster Canopy
# ---------------------------------------------------------------------------
def build_plane_tree(mats):
    parts = []
    rng = random.Random(2026)

    # Main trunk gently curving upward from ground (z = -0.15..2.95)
    trunk_pts = [
        (0.0, 0.0, -0.15),
        (0.02, -0.02, 0.45),
        (0.05, 0.04, 1.25),
        (0.01, 0.07, 2.15),
        (-0.04, 0.03, 2.95),
    ]
    trunk_radii = [0.34, 0.26, 0.21, 0.17, 0.12]
    parts.append(add_tube_along_points("Tree_MainTrunk", trunk_pts, trunk_radii, 9, mats["Bark"], smooth_angle=55.0))

    # 5 flared buttress roots grounding the trunk naturally
    for i in range(5):
        ang = (2.0 * math.pi * i) / 5.0 + 0.25
        ca, sa = math.cos(ang), math.sin(ang)
        r_pts = [
            (ca * 0.14, sa * 0.14, 0.58),
            (ca * 0.30, sa * 0.30, 0.18),
            (ca * 0.48, sa * 0.48, -0.08),
        ]
        parts.append(add_tube_along_points(f"Tree_Root_{i}", r_pts, [0.12, 0.09, 0.05], 6, mats["Bark"], smooth_angle=55.0))

    # 5 primary sweeping boughs + secondary forks
    canopy_centers = [
        (Vector((0.0, 0.0, 4.65)), 1.55, 1.15, "FoliageA"),
        (Vector((0.15, -0.10, 5.15)), 1.10, 0.85, "FoliageB"),
    ]
    bough_specs = [
        (0.20, 2.05, 1.48, 3.65),
        (1.45, 2.25, 1.52, 3.85),
        (2.70, 2.10, 1.44, 3.55),
        (3.95, 2.35, 1.50, 3.92),
        (5.20, 1.95, 1.38, 3.48),
    ]
    for idx, (ang, start_z, reach, top_z) in enumerate(bough_specs):
        ca, sa = math.cos(ang), math.sin(ang)
        p0 = Vector((0.0, 0.0, start_z))
        p1 = Vector((ca * reach * 0.55, sa * reach * 0.55, (start_z + top_z) * 0.52))
        p2 = Vector((ca * reach, sa * reach, top_z))
        parts.append(add_tube_along_points(f"Tree_Bough_{idx}", [p0, p1, p2], [0.12, 0.085, 0.048], 6, mats["Bark"], smooth_angle=55.0))

        mat_k = "FoliageA" if idx % 2 == 0 else "FoliageB"
        canopy_centers.append((p2 + Vector((0, 0, 0.35)), 1.25, 0.88, mat_k))

        # Secondary side twig + upper bough cluster
        ang2 = ang + 0.52
        p3 = p1 + Vector((math.cos(ang2) * 0.85, math.sin(ang2) * 0.85, 0.85))
        parts.append(add_tube_along_points(f"Tree_Twig_{idx}", [p1, p3], [0.065, 0.032], 5, mats["Bark"], smooth_angle=55.0))
        mat_k2 = "FoliageB" if idx % 2 == 0 else "FoliageA"
        canopy_centers.append((p3 + Vector((0, 0, 0.25)), 1.02, 0.74, mat_k2))

    # Sculpted lush layered foliage clusters (18 organically sculpted sub-boughs for rich, natural silhouette)
    extra_crown = [
        (Vector((0.75, 0.65, 4.35)), 1.05, 0.78, "FoliageB"),
        (Vector((-0.82, 0.52, 4.25)), 1.08, 0.76, "FoliageA"),
        (Vector((0.55, -0.82, 4.15)), 1.02, 0.74, "FoliageA"),
        (Vector((-0.68, -0.74, 4.30)), 1.06, 0.78, "FoliageB"),
        (Vector((0.0, 0.88, 3.85)), 0.92, 0.68, "FoliageA"),
        (Vector((-0.12, -0.92, 3.75)), 0.95, 0.70, "FoliageB"),
    ]
    canopy_centers.extend(extra_crown)

    for ci, (center, rx, rz, mat_key) in enumerate(canopy_centers):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
        seed_off = ci * 3.17
        for v in bm.verts:
            p = v.co
            lobe = (
                1.0
                + 0.24 * math.sin(p.x * 4.2 + seed_off) * math.cos(p.y * 3.8 - seed_off)
                + 0.16 * math.sin(p.z * 5.0 + p.x * 3.1 + seed_off)
                + 0.09 * rng.uniform(-1.0, 1.0)
            )
            z_scale = rz * (0.70 if p.z < 0 else 1.0)
            v.co = Vector((p.x * rx * lobe, p.y * rx * lobe, p.z * z_scale * lobe)) + center
        parts.append(create_mesh_object(f"CanopyCluster_{ci}", bm, mat=mats[mat_key], smooth_angle=75.0, uv_scale=0.6))

    return join_objects(parts, "Tree_PlaneTree")


# ---------------------------------------------------------------------------
# 5. Classical Carved Stone Town Square Fountain
# ---------------------------------------------------------------------------
def build_town_fountain(mats):
    parts = []
    # Stepped octagonal basin wall (outer radius 3.1m, height 0.72m)
    basin_profile = [
        (3.26, 0.00),
        (3.26, 0.14),
        (3.14, 0.16),
        (3.10, 0.62),
        (3.22, 0.64),
        (3.22, 0.74),
        (2.74, 0.74),
        (2.74, 0.64),
        (2.82, 0.60),
        (2.80, 0.16),
        (0.00, 0.16),
    ]
    parts.append(add_revolved_profile("Fountain_OctagonalBasin", basin_profile, 8, mats["StoneTrim"], smooth_angle=25.0))

    # Basin water surface disc
    parts.append(add_cylinder_part("Fountain_WaterPool", (0.0, 0.0, 0.56), 2.84, 2.84, 0.04, 16, mats["Water"], smooth_angle=45.0))

    # Carved central tiered pedestal + scalloped bowls + turned stone finial
    center_profile = [
        (0.88, 0.14),
        (0.86, 0.30),
        (0.70, 0.36),
        (0.56, 0.44),
        (0.50, 1.15),
        (0.64, 1.24),
        (0.76, 1.38),
        # Lower scallop bowl flare
        (1.22, 1.56),
        (1.32, 1.72),
        (1.26, 1.78),
        (1.12, 1.76),
        (0.92, 1.62),
        (0.42, 1.56),
        # Upper fluted column
        (0.36, 1.72),
        (0.32, 2.38),
        (0.42, 2.48),
        # Upper bowl
        (0.76, 2.66),
        (0.82, 2.78),
        (0.76, 2.82),
        (0.62, 2.76),
        (0.28, 2.68),
        # Turned finial spire
        (0.22, 2.86),
        (0.28, 3.04),
        (0.22, 3.22),
        (0.11, 3.38),
        (0.02, 3.55),
    ]
    parts.append(add_revolved_profile("Fountain_CenterSculpture", center_profile, 20, mats["StoneTrim"], smooth_angle=46.0))

    # Water inside lower & upper bowls
    parts.append(add_cylinder_part("Fountain_WaterLower", (0.0, 0.0, 1.70), 1.12, 1.12, 0.03, 18, mats["Water"], smooth_angle=45.0))
    parts.append(add_cylinder_part("Fountain_WaterUpper", (0.0, 0.0, 2.74), 0.64, 0.64, 0.03, 16, mats["Water"], smooth_angle=45.0))

    return join_objects(parts, "Town_Fountain")


# ---------------------------------------------------------------------------
# 6. Modern Service-Station Fuel Pump (with Curved 3D Hoses & Nozzles)
# ---------------------------------------------------------------------------
def build_fuel_pump(mats):
    parts = []
    # Concrete island plinth
    parts.append(add_beveled_box("Pump_Plinth", (0.0, 0.0, 0.09), (1.18, 0.72, 0.18), mats["Concrete"], bevel=0.04, segments=2))
    # Lower steel dispenser cabinet
    parts.append(add_beveled_box("Pump_LowerCabinet", (0.0, 0.0, 0.64), (0.86, 0.52, 0.94), mats["StoneTrim"], bevel=0.04, segments=2))
    # Upper recessed meter head + arch canopy
    parts.append(add_beveled_box("Pump_MeterHead", (0.0, 0.0, 1.46), (0.84, 0.48, 0.72), mats["TrimDark"], bevel=0.035, segments=2))
    parts.append(add_beveled_box("Pump_TopArch", (0.0, 0.0, 1.98), (1.06, 0.58, 0.24), mats["AccentTeal"], bevel=0.055, segments=2))
    # Side brushed-metal arch columns
    for sx in (-1.0, 1.0):
        parts.append(add_beveled_box(f"Pump_SideArch_{sx}", (sx * 0.47, 0.0, 1.08), (0.11, 0.50, 1.82), mats["Chrome"], bevel=0.025, segments=2))

    # Front & back backlit price/litre LCD screens + keypad
    for sy in (-1.0, 1.0):
        parts.append(add_beveled_box(f"Pump_LCD_{sy}", (0.0, sy * 0.245, 1.54), (0.64, 0.03, 0.34), mats["LitScreen"], bevel=0.008, segments=1))
        parts.append(add_beveled_box(f"Pump_Keypad_{sy}", (-0.18, sy * 0.25, 1.24), (0.24, 0.03, 0.18), mats["Chrome"], bevel=0.006, segments=1))
        parts.append(add_beveled_box(f"Pump_CardReader_{sy}", (0.18, sy * 0.25, 1.24), (0.22, 0.03, 0.16), mats["AccentTeal"], bevel=0.006, segments=1))

    # Dual hanging curved rubber fuel hoses + metal fuel nozzles on both sides
    for sx in (-1.0, 1.0):
        hose_pts = [
            (sx * 0.48, 0.0, 1.82),
            (sx * 0.62, 0.06, 1.45),
            (sx * 0.68, 0.10, 0.92),
            (sx * 0.62, 0.08, 0.58),
            (sx * 0.55, 0.04, 0.78),
            (sx * 0.50, 0.0, 1.12),
        ]
        parts.append(add_tube_along_points(f"Pump_Hose_{sx}", hose_pts, 0.020, 8, mats["WheelRubber"], smooth_angle=55.0))
        # Nozzle holster + nozzle handle
        parts.append(add_beveled_box(f"Pump_Holster_{sx}", (sx * 0.52, 0.0, 1.18), (0.09, 0.16, 0.24), mats["TrimDark"], bevel=0.015, segments=1))
        parts.append(add_cylinder_part(f"Pump_NozzleSpout_{sx}", (sx * 0.53, 0.0, 1.28), 0.014, 0.012, 0.18, 8, mats["Chrome"], rot_euler=(0.25, 0.0, 0.0)))

    return join_objects(parts, "Gas_FuelPump")


# ---------------------------------------------------------------------------
# 7. Detailed Beverage Vending Machine (Glass Front + 3D Stocked Cans)
# ---------------------------------------------------------------------------
def build_vending_machine(mats):
    parts = []
    # Main cabinet back & outer shell with recessed display front (+Y is front)
    parts.append(add_beveled_box("VM_CabinetBack", (0.0, -0.06, 0.99), (1.00, 0.68, 1.98), mats["AccentTeal"], bevel=0.035, segments=2))
    # Side cheeks & top/bottom bezels framing display window
    parts.append(add_beveled_box("VM_LeftCheek", (-0.45, 0.30, 0.99), (0.10, 0.12, 1.96), mats["AccentTeal"], bevel=0.02, segments=1))
    parts.append(add_beveled_box("VM_RightPanel", (0.36, 0.30, 0.99), (0.28, 0.12, 1.96), mats["TrimDark"], bevel=0.02, segments=1))
    parts.append(add_beveled_box("VM_TopHeader", (0.0, 0.30, 1.88), (0.98, 0.12, 0.18), mats["TrimDark"], bevel=0.02, segments=1))
    parts.append(add_beveled_box("VM_BottomBase", (0.0, 0.30, 0.28), (0.98, 0.12, 0.56), mats["TrimDark"], bevel=0.02, segments=1))

    # Recessed pickup chute at bottom
    parts.append(add_beveled_box("VM_PickupRecess", (-0.09, 0.35, 0.30), (0.64, 0.08, 0.24), mats["InteriorDark"], bevel=0.015, segments=1))
    parts.append(add_beveled_box("VM_PickupFlap", (-0.09, 0.37, 0.34), (0.58, 0.03, 0.12), mats["Chrome"], bevel=0.008, segments=1, rot_euler=(0.18, 0.0, 0.0)))

    # Control panel keypad + backlit screen + card tap
    parts.append(add_beveled_box("VM_DisplaySmall", (0.36, 0.37, 1.48), (0.18, 0.02, 0.12), mats["LitScreen"], bevel=0.006, segments=1))
    parts.append(add_beveled_box("VM_Keypad", (0.36, 0.37, 1.24), (0.16, 0.02, 0.22), mats["Chrome"], bevel=0.005, segments=1))
    parts.append(add_beveled_box("VM_CoinSlot", (0.36, 0.37, 0.98), (0.14, 0.025, 0.14), mats["AccentMustard"], bevel=0.005, segments=1))

    # Illuminated interior backdrop behind product shelves
    parts.append(add_beveled_box("VM_Backlight", (-0.09, 0.22, 1.18), (0.62, 0.02, 1.22), mats["LitScreen"], bevel=0.004, segments=1))

    # 4 interior metal shelves stocked with colorful 3D soda cans!
    can_mats = [mats["CanRed"], mats["CanBlue"], mats["CanGreen"], mats["CanOrange"]]
    for row in range(4):
        sz = 0.72 + row * 0.28
        parts.append(add_beveled_box(f"VM_Shelf_{row}", (-0.09, 0.28, sz), (0.60, 0.14, 0.02), mats["Chrome"], bevel=0.003, segments=1))
        for col in range(5):
            cx = -0.31 + col * 0.11
            parts.append(add_cylinder_part(f"VM_Can_{row}_{col}", (cx, 0.29, sz + 0.075), 0.034, 0.034, 0.125, 10, can_mats[(row + col) % len(can_mats)]))

    # Crisp metallic window bezel bars framing the open display so all 20 colorful 3D soda cans stay clearly visible!
    parts.append(add_beveled_box("VM_FrameTop", (-0.09, 0.365, 1.79), (0.66, 0.024, 0.032), mats["Chrome"], bevel=0.005, segments=1))
    parts.append(add_beveled_box("VM_FrameBot", (-0.09, 0.365, 0.58), (0.66, 0.024, 0.032), mats["Chrome"], bevel=0.005, segments=1))
    parts.append(add_beveled_box("VM_FrameLeft", (-0.405, 0.365, 1.185), (0.032, 0.024, 1.22), mats["Chrome"], bevel=0.005, segments=1))
    parts.append(add_beveled_box("VM_FrameRight", (0.225, 0.365, 1.185), (0.032, 0.024, 1.22), mats["Chrome"], bevel=0.005, segments=1))

    return join_objects(parts, "Town_VendingMachine")


# ---------------------------------------------------------------------------
# 8. Classic Street Mailbox
# ---------------------------------------------------------------------------
def build_mailbox(mats):
    parts = []
    # Flared cast-iron pedestal base + fluted post
    base_prof = [
        (0.18, 0.00),
        (0.18, 0.05),
        (0.14, 0.08),
        (0.08, 0.18),
        (0.062, 0.24),
        (0.058, 0.96),
        (0.11, 1.00),
        (0.11, 1.04),
    ]
    parts.append(add_revolved_profile("Mailbox_Pedestal", base_prof, 14, mats["TrimDark"], smooth_angle=38.0))

    # Curved barrel-top mailbox body
    parts.append(add_beveled_box("Mailbox_BoxLower", (0.0, 0.0, 1.19), (0.44, 0.38, 0.34), mats["AccentBlue"], bevel=0.025, segments=2))
    parts.append(add_cylinder_part("Mailbox_BarrelTop", (0.0, 0.0, 1.36), 0.19, 0.19, 0.44, 16, mats["AccentBlue"], rot_euler=(0.0, math.pi * 0.5, 0.0), smooth_angle=45.0))
    # Mail slot rain hood + emblem plaque on front (+Y)
    parts.append(add_beveled_box("Mailbox_RainHood", (0.0, 0.21, 1.34), (0.32, 0.07, 0.06), mats["AccentBlue"], bevel=0.015, segments=1, rot_euler=(0.35, 0.0, 0.0)))
    parts.append(add_beveled_box("Mailbox_SlotDark", (0.0, 0.195, 1.30), (0.26, 0.02, 0.032), mats["InteriorDark"], bevel=0.003, segments=1))
    parts.append(add_beveled_box("Mailbox_Badge", (0.0, 0.196, 1.15), (0.24, 0.018, 0.12), mats["AccentMustard"], bevel=0.006, segments=1))

    return join_objects(parts, "Town_Mailbox")


# ---------------------------------------------------------------------------
# 9. Corrugated ISO Shipping Container (3.2m W x 3.0m H x 8.5m L)
# ---------------------------------------------------------------------------
def build_shipping_container(mats):
    parts = []
    w, h, l = 3.20, 3.00, 8.50
    hw, hl = w * 0.5, l * 0.5

    # Core structural box slightly inset so corrugations & corner rails stand proud
    parts.append(add_beveled_box("Cont_Core", (0.0, 0.0, h * 0.5), (w - 0.12, l - 0.14, h - 0.14), mats["ContainerPaint"], bevel=0.02, segments=1))

    # Corner posts + top & bottom structural frame rails
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            parts.append(add_beveled_box(f"Cont_CornerPost_{sx}_{sy}", (sx * (hw - 0.09), sy * (hl - 0.09), h * 0.5), (0.18, 0.18, h), mats["ContainerPaint"], bevel=0.015, segments=1))
            # Corner ISO casting blocks (dark steel)
            for sz in (0.09, h - 0.09):
                parts.append(add_beveled_box(f"Cont_Cast_{sx}_{sy}_{sz}", (sx * (hw - 0.09), sy * (hl - 0.09), sz), (0.20, 0.20, 0.18), mats["TrimDark"], bevel=0.01, segments=1))
        # Longitudinal top & bottom beams
        parts.append(add_beveled_box(f"Cont_RailTop_{sx}", (sx * (hw - 0.08), 0.0, h - 0.09), (0.16, l - 0.16, 0.18), mats["ContainerPaint"], bevel=0.012, segments=1))
        parts.append(add_beveled_box(f"Cont_RailBot_{sx}", (sx * (hw - 0.08), 0.0, 0.09), (0.16, l - 0.16, 0.18), mats["ContainerPaint"], bevel=0.012, segments=1))

    # Transverse end beams
    for sy in (-1.0, 1.0):
        parts.append(add_beveled_box(f"Cont_EndTop_{sy}", (0.0, sy * (hl - 0.08), h - 0.09), (w - 0.16, 0.16, 0.18), mats["ContainerPaint"], bevel=0.012, segments=1))
        parts.append(add_beveled_box(f"Cont_EndBot_{sy}", (0.0, sy * (hl - 0.08), 0.09), (w - 0.16, 0.16, 0.18), mats["ContainerPaint"], bevel=0.012, segments=1))

    # Authentic 3D vertical corrugated ribs along both long sides & roof!
    n_ribs = 18
    for i in range(n_ribs):
        ry = -hl + 0.42 + i * ((l - 0.84) / (n_ribs - 1))
        for sx in (-1.0, 1.0):
            parts.append(add_beveled_box(f"Cont_SideRib_{sx}_{i}", (sx * (hw - 0.045), ry, h * 0.5), (0.07, 0.20, h - 0.34), mats["ContainerPaint"], bevel=0.012, segments=1))
        parts.append(add_beveled_box(f"Cont_RoofRib_{i}", (0.0, ry, h - 0.04), (w - 0.34, 0.20, 0.06), mats["ContainerPaint"], bevel=0.01, segments=1))

    # Recessed double cargo doors on +Y end with 4 vertical galvanized locking rods
    parts.append(add_beveled_box("Cont_DoorSeam", (0.0, hl - 0.06, h * 0.5), (0.04, 0.05, h - 0.36), mats["TrimDark"], bevel=0.005, segments=1))
    for rx in (-0.98, -0.38, 0.38, 0.98):
        parts.append(add_cylinder_part(f"Cont_LockRod_{rx}", (rx, hl - 0.03, h * 0.5), 0.022, 0.022, h - 0.32, 8, mats["Chrome"]))
        parts.append(add_beveled_box(f"Cont_Handle_{rx}", (rx + 0.07, hl - 0.01, 1.15), (0.14, 0.03, 0.045), mats["Chrome"], bevel=0.006, segments=1))

    return join_objects(parts, "Shipping_Container")


# ---------------------------------------------------------------------------
# 10. Sculpted Cantilevered Traffic Light Pole (with Hooded Signal Visors)
# ---------------------------------------------------------------------------
def build_traffic_light_pole(mats):
    parts = []
    # Matches exact Unity TrafficLightCycler bulb positions:
    # Bulb_Red = (1.05, 5.05, -0.17), Bulb_Amber = (1.05, 4.75, -0.17), Bulb_Green = (1.05, 4.45, -0.17)
    # In Blender coords (Unity X -> +X, Unity Y -> +Z, Unity Z -> +Y):
    # Signal head centered at Blender (1.05, 0.0, 4.75), lenses face -Y (Unity -Z)
    base_prof = [
        (0.24, 0.00),
        (0.24, 0.10),
        (0.18, 0.16),
        (0.13, 0.35),
        (0.105, 0.45),
        (0.075, 5.38),
        (0.095, 5.44),
        (0.02, 5.52),
    ]
    parts.append(add_revolved_profile("TL_MainPole", base_prof, 14, mats["TrimDark"], smooth_angle=44.0))

    # Cantilevered curved mast arm from pole (x=0, z=5.15) to signal head (x=1.05, z=5.20)
    arm_pts = [
        (0.04, 0.0, 5.10),
        (0.45, 0.0, 5.20),
        (0.88, 0.0, 5.22),
        (1.05, 0.0, 5.18),
    ]
    parts.append(add_tube_along_points("TL_MastArm", arm_pts, [0.068, 0.056, 0.050, 0.048], 10, mats["TrimDark"], smooth_angle=45.0))
    # Angled strut brace
    parts.append(add_tube_along_points("TL_Brace", [(0.05, 0.0, 4.65), (0.58, 0.0, 5.18)], [0.032, 0.028], 8, mats["TrimDark"]))

    # Beveled signal head box + backplate
    parts.append(add_beveled_box("TL_SignalBox", (1.05, 0.02, 4.75), (0.38, 0.32, 1.06), mats["TrimDark"], bevel=0.04, segments=2))
    parts.append(add_beveled_box("TL_Backplate", (1.05, 0.08, 4.75), (0.52, 0.03, 1.20), mats["InteriorDark"], bevel=0.02, segments=1))

    # 3 curved protective sun-visor hoods arching over the Red/Amber/Green lenses (-Y face)
    for bi, bz in enumerate((5.05, 4.75, 4.45)):
        bm = bmesh.new()
        r_in, r_out = 0.115, 0.135
        y_back, y_front_top, y_front_bot = -0.14, -0.34, -0.22
        segs = 10
        ring_b_in, ring_b_out, ring_f_in, ring_f_out = [], [], [], []
        for s in range(segs + 1):
            ang = math.pi * (s / segs)  # 0 (right) -> pi/2 (top) -> pi (left)
            ca, sa = math.cos(ang), math.sin(ang)
            yf = y_front_bot + (y_front_top - y_front_bot) * sa
            ring_b_in.append(bm.verts.new(Vector((1.05 + r_in * ca, y_back, bz + r_in * sa))))
            ring_b_out.append(bm.verts.new(Vector((1.05 + r_out * ca, y_back, bz + r_out * sa))))
            ring_f_in.append(bm.verts.new(Vector((1.05 + r_in * ca, yf, bz + r_in * sa))))
            ring_f_out.append(bm.verts.new(Vector((1.05 + r_out * ca, yf, bz + r_out * sa))))
        for s in range(segs):
            bm.faces.new((ring_b_out[s], ring_b_out[s + 1], ring_f_out[s + 1], ring_f_out[s]))
            bm.faces.new((ring_f_in[s], ring_f_in[s + 1], ring_b_in[s + 1], ring_b_in[s]))
            bm.faces.new((ring_f_out[s], ring_f_out[s + 1], ring_f_in[s + 1], ring_f_in[s]))
        parts.append(create_mesh_object(f"TL_Visor_{bi}", bm, mat=mats["TrimDark"], smooth_angle=45.0))

    return join_objects(parts, "Traffic_Light_Pole")


# ---------------------------------------------------------------------------
# 11. Classical Urban Street Lamp (5.20m tall, upright, tight pole footprint!)
# ---------------------------------------------------------------------------
def build_street_lamp(mats):
    parts = []
    # Fluted cast-iron base + tapered cylindrical post up to 4.95m
    pole_prof = [
        (0.22, 0.00),
        (0.22, 0.14),
        (0.17, 0.24),
        (0.15, 0.78),
        (0.11, 0.92),
        (0.085, 1.05),
        (0.062, 4.85),
        (0.088, 4.94),
        (0.030, 5.10),
    ]
    parts.append(add_revolved_profile("Lamp_Post", pole_prof, 14, mats["TrimDark"], smooth_angle=44.0))

    # Graceful curved bracket arm extending forward (+Y) to lantern head
    arm_pts = [
        (0.0, 0.04, 4.55),
        (0.0, 0.32, 4.95),
        (0.0, 0.68, 5.12),
        (0.0, 0.92, 5.05),
    ]
    parts.append(add_tube_along_points("Lamp_BracketArm", arm_pts, [0.044, 0.038, 0.034, 0.032], 8, mats["TrimDark"], smooth_angle=48.0))
    # Decorative scroll brace under bracket arm
    parts.append(add_tube_along_points("Lamp_Brace", [(0.0, 0.06, 4.25), (0.0, 0.36, 4.66), (0.0, 0.56, 4.98)], [0.020, 0.018, 0.018], 6, mats["TrimDark"]))

    # Sculpted modern lantern hood + glowing glass diffuser
    parts.append(add_beveled_box("Lamp_HoodTop", (0.0, 0.96, 5.06), (0.34, 0.52, 0.09), mats["TrimDark"], bevel=0.025, segments=2))
    parts.append(add_beveled_box("Lamp_GlowLens", (0.0, 0.96, 4.98), (0.26, 0.42, 0.07), mats["Headlight"], bevel=0.018, segments=1))

    return join_objects(parts, "Street_Lamp")


# ---------------------------------------------------------------------------
# 12. Clean Single Urban Street Trash Can (0.48m x 0.88m)
# ---------------------------------------------------------------------------
def build_street_trash_can(mats):
    parts = []
    # Tapered ribbed metal bin with flanged rim and domed hooded top
    body_prof = [
        (0.20, 0.00),
        (0.21, 0.06),
        (0.22, 0.64),
        (0.24, 0.66),
        (0.24, 0.70),
        (0.21, 0.70),
        (0.19, 0.08),
    ]
    parts.append(add_revolved_profile("Bin_Body", body_prof, 16, mats["TrimDark"], smooth_angle=44.0))
    # Horizontal stainless reinforcement bands
    for bz in (0.22, 0.44, 0.65):
        parts.append(add_cylinder_part(f"Bin_Band_{bz}", (0.0, 0.0, bz), 0.232, 0.232, 0.032, 16, mats["Chrome"]))
    # Hooded top dome with side struts
    parts.append(add_cylinder_part("Bin_TopCap", (0.0, 0.0, 0.86), 0.24, 0.14, 0.10, 16, mats["AccentTeal"]))
    for sx in (-1.0, 1.0):
        parts.append(add_beveled_box(f"Bin_Strut_{sx}", (sx * 0.21, 0.0, 0.75), (0.032, 0.06, 0.14), mats["TrimDark"], bevel=0.006, segments=1))
    return join_objects(parts, "Street_TrashCan")


# ---------------------------------------------------------------------------
# 13. Clean Single Tripod Work Floodlight (0.76m x 2.12m)
# ---------------------------------------------------------------------------
def build_work_floodlight(mats):
    parts = []
    # 3 tripod legs meeting at central collar (z = 0.68)
    for i in range(3):
        ang = (2.0 * math.pi * i) / 3.0 + 0.4
        ca, sa = math.cos(ang), math.sin(ang)
        parts.append(add_tube_along_points(
            f"Flood_Leg_{i}",
            [(ca * 0.42, sa * 0.42, 0.0), (ca * 0.05, sa * 0.05, 0.68)],
            [0.020, 0.022], 6, mats["TrimDark"],
        ))
    # Telescoping central pole + crossbar
    parts.append(add_cylinder_part("Flood_PoleLow", (0.0, 0.0, 1.02), 0.028, 0.026, 0.90, 10, mats["AccentMustard"]))
    parts.append(add_cylinder_part("Flood_PoleHigh", (0.0, 0.0, 1.66), 0.020, 0.020, 0.56, 10, mats["Chrome"]))
    parts.append(add_beveled_box("Flood_Crossbar", (0.0, 0.0, 1.94), (0.64, 0.05, 0.05), mats["AccentMustard"], bevel=0.008, segments=1))
    # Twin LED halogen lamp housings + glowing lenses
    for sx in (-1.0, 1.0):
        parts.append(add_beveled_box(f"Flood_Head_{sx}", (sx * 0.22, 0.02, 2.06), (0.24, 0.12, 0.18), mats["AccentMustard"], bevel=0.018, segments=2, rot_euler=(0.14, 0.0, 0.0)))
        parts.append(add_beveled_box(f"Flood_Lens_{sx}", (sx * 0.22, 0.085, 2.055), (0.20, 0.025, 0.14), mats["Headlight"], bevel=0.006, segments=1, rot_euler=(0.14, 0.0, 0.0)))
    return join_objects(parts, "Work_FloodLight")


# ---------------------------------------------------------------------------
# Studio Showcase Render (to visually verify all exported Blender models!)
# ---------------------------------------------------------------------------
def render_showcase(preview_entries):
    reset_scene()
    mats = get_common_materials()

    # Ground sweep
    bm_g = bmesh.new()
    bmesh.ops.create_grid(bm_g, x_segments=1, y_segments=1, size=40.0)
    ground_mat = make_mat("StudioFloor", (0.20, 0.22, 0.25), roughness=0.72, metallic=0.0)
    create_mesh_object("StudioGround", bm_g, mat=ground_mat)

    # Re-build & lay out key hero models neatly in a 2-row studio grid for crisp visual inspection
    # Row 1 (front): Sedan + 4 wheels, Hatchback + 4 wheels, Pickup + 4 wheels
    # Row 2 (back): PlaneTree, Fountain, FuelPump, VendingMachine, Mailbox, Container, TrafficLight
    wheel_proto = build_car_wheel(mats)
    wheel_proto.location = Vector((0.0, -50.0, 0.0))  # hide prototype off-camera

    def place_car_with_wheels(car_obj, pos_xy, yaw_deg, wheel_x, front_y, rear_y, wheel_r, paint_rgb):
        car_obj.location = Vector((pos_xy[0], pos_xy[1], 0.0))
        car_obj.rotation_euler = Euler((0.0, 0.0, math.radians(yaw_deg)), "XYZ")
        # Override BodyPaint copy so each car shows its signature color
        for i, m in enumerate(car_obj.data.materials):
            if m and m.name == "BodyPaint":
                m_copy = m.copy()
                m_copy.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*paint_rgb, 1.0)
                car_obj.data.materials[i] = m_copy
        for wx in (-wheel_x, wheel_x):
            for wy in (front_y, rear_y):
                w = wheel_proto.copy()
                w.data = wheel_proto.data
                bpy.context.collection.objects.link(w)
                sc = wheel_r / 0.34
                w.scale = Vector((sc, sc, sc))
                rot = car_obj.rotation_euler.to_matrix()
                w.location = car_obj.location + rot @ Vector((wx, wy, wheel_r))
                w.rotation_euler = car_obj.rotation_euler

    place_car_with_wheels(build_car_sedan(mats), (-4.4, -2.5), 148.0, 0.84, 1.38, -1.32, 0.34, (0.68, 0.14, 0.14))
    place_car_with_wheels(build_car_hatchback(mats), (0.0, -2.5), -32.0, 0.80, 1.22, -1.22, 0.32, (0.14, 0.36, 0.56))
    place_car_with_wheels(build_car_pickup(mats), (4.6, -2.5), 148.0, 0.87, 1.56, -1.56, 0.37, (0.88, 0.88, 0.86))

    tree = build_plane_tree(mats)
    tree.location = Vector((-7.2, 4.2, 0.0))

    fountain = build_town_fountain(mats)
    fountain.location = Vector((-1.6, 4.8, 0.0))

    pump = build_fuel_pump(mats)
    pump.location = Vector((2.8, 3.2, 0.0))
    pump.rotation_euler = Euler((0.0, 0.0, math.radians(-18.0)), "XYZ")

    vm = build_vending_machine(mats)
    vm.location = Vector((4.8, 3.2, 0.0))
    vm.rotation_euler = Euler((0.0, 0.0, math.radians(160.0)), "XYZ")

    mb = build_mailbox(mats)
    mb.location = Vector((6.3, 3.0, 0.0))
    mb.rotation_euler = Euler((0.0, 0.0, math.radians(160.0)), "XYZ")

    tl = build_traffic_light_pole(mats)
    tl.location = Vector((7.6, 3.6, 0.0))

    cont = build_shipping_container(mats)
    cont.location = Vector((4.5, 9.2, 0.0))
    cont.rotation_euler = Euler((0.0, 0.0, math.radians(78.0)), "XYZ")

    # 3-point sunlit outdoor lighting + sky world
    world = bpy.data.worlds.new("StudioWorld")
    bpy.context.scene.world = world
    if world.node_tree is None:
        world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.58, 0.72, 0.88, 1.0)
        bg.inputs["Strength"].default_value = 0.85

    sun_data = bpy.data.lights.new("KeySun", type="SUN")
    sun_data.energy = 3.2
    sun_data.color = (1.0, 0.94, 0.84)
    sun_obj = bpy.data.objects.new("KeySun", sun_data)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = Euler((math.radians(52), math.radians(15), math.radians(-38)), "XYZ")

    fill_data = bpy.data.lights.new("FillSun", type="SUN")
    fill_data.energy = 0.9
    fill_data.color = (0.72, 0.84, 1.0)
    fill_obj = bpy.data.objects.new("FillSun", fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = Euler((math.radians(40), 0.0, math.radians(135)), "XYZ")

    cam_data = bpy.data.cameras.new("StudioCam")
    cam_data.lens = 42.0
    cam_obj = bpy.data.objects.new("StudioCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((0.6, -15.2, 7.2))
    direction = Vector((0.4, 2.0, 1.3)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_obj

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_Assets_Showcase.blend"))
    bpy.ops.render.render(write_still=True)
    print(f"Saved studio showcase render to: {PREVIEW_PATH}")


# ---------------------------------------------------------------------------
# Main Export Pipeline
# ---------------------------------------------------------------------------
def main():
    builders = [
        ("Car_Sedan.fbx", build_car_sedan),
        ("Car_Hatchback.fbx", build_car_hatchback),
        ("Car_Pickup.fbx", build_car_pickup),
        ("Car_Wheel.fbx", build_car_wheel),
        ("Tree_PlaneTree.fbx", build_plane_tree),
        ("Town_Fountain.fbx", build_town_fountain),
        ("Gas_FuelPump.fbx", build_fuel_pump),
        ("Town_VendingMachine.fbx", build_vending_machine),
        ("Town_Mailbox.fbx", build_mailbox),
        ("Shipping_Container.fbx", build_shipping_container),
        ("Traffic_Light_Pole.fbx", build_traffic_light_pole),
        ("Street_Lamp.fbx", build_street_lamp),
        ("Street_TrashCan.fbx", build_street_trash_can),
        ("Work_FloodLight.fbx", build_work_floodlight),
    ]

    print(f"[GRIDROAM Blender] Generating {len(builders)} realistic 3D FBX models into {OUT_FBX_DIR} ...")
    for fbx_name, fn in builders:
        reset_scene()
        mats = get_common_materials()
        obj = fn(mats)
        export_fbx(obj, fbx_name)

    render_showcase(builders)
    print("[GRIDROAM Blender] All 11 FBX assets generated successfully!")


if __name__ == "__main__":
    main()
