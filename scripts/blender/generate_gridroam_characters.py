#!/usr/bin/env python3
"""
GRIDROAM Realistic 3D Human Character Generator (Blender 5.3+ Python)
Generates 3 anatomically proportioned, sculpted, smooth-shaded, mobile-optimized FBX character rigs:
  - Character_CasualMale.fbx   (Open layered jacket, inner tee, jeans, leather belt, sneakers, sculpted face & styled hair)
  - Character_CasualFemale.fbx (Tailored casual coat, inner top, trousers, boots/sneakers, softer sculpted face & shoulder-length hair)
  - Character_Worker.fbx       (Collared work jacket with chest pockets, mechanic cap, rugged trousers & work boots)

Each FBX exports with the EXACT 14 anatomical pivot transforms driven by Unity's ProceduralHumanoid.cs:
  Hips -> Spine -> Neck -> Head
                -> L_Shoulder -> L_Elbow
                -> R_Shoulder -> R_Elbow
       -> L_Hip -> L_Knee -> L_Ankle
       -> R_Hip -> R_Knee -> R_Ankle
so walking, sprinting, breathing, head-look IK, jumping, waving, bench sitting, and driving all work seamlessly!
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
    "GRIDROAM_CHAR_PREVIEW_PATH",
    os.path.join(SCRIPT_DIR, "blender_characters_preview.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


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
        "CharSkin": make_mat("CharSkin", (0.86, 0.67, 0.54), roughness=0.56, metallic=0.0),
        "CharShirt": make_mat("CharShirt", (0.18, 0.34, 0.54), roughness=0.72, metallic=0.0),
        "CharUndershirt": make_mat("CharUndershirt", (0.90, 0.89, 0.86), roughness=0.78, metallic=0.0),
        "CharPants": make_mat("CharPants", (0.16, 0.20, 0.27), roughness=0.80, metallic=0.0),
        "CharBelt": make_mat("CharBelt", (0.18, 0.11, 0.07), roughness=0.48, metallic=0.0),
        "CharBuckle": make_mat("CharBuckle", (0.82, 0.84, 0.86), roughness=0.22, metallic=0.88),
        "CharShoes": make_mat("CharShoes", (0.14, 0.13, 0.13), roughness=0.58, metallic=0.0),
        "CharShoeSole": make_mat("CharShoeSole", (0.91, 0.90, 0.87), roughness=0.68, metallic=0.0),
        "CharHair": make_mat("CharHair", (0.14, 0.09, 0.06), roughness=0.62, metallic=0.0),
        "CharEyeWhite": make_mat("CharEyeWhite", (0.95, 0.95, 0.94), roughness=0.25, metallic=0.0),
        "CharEyeIris": make_mat("CharEyeIris", (0.08, 0.07, 0.06), roughness=0.14, metallic=0.05),
        "CharLips": make_mat("CharLips", (0.68, 0.42, 0.38), roughness=0.48, metallic=0.0),
    }


def apply_smooth(obj, angle_deg=48.0):
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


def box_uv(obj, scale=2.0):
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


def apply_subsurf(obj, levels=1, smooth_angle=68.0):
    mod = obj.modifiers.new(name="Subsurf", type="SUBSURF")
    mod.levels = levels
    mod.render_levels = levels
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    box_uv(obj, scale=2.6)
    apply_smooth(obj, angle_deg=smooth_angle)
    return obj


def make_obj_from_bm(name, bm, mat, smooth_angle=52.0, subsurf_levels=0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if mat is not None:
        obj.data.materials.append(mat)
    box_uv(obj, scale=2.6)
    apply_smooth(obj, angle_deg=smooth_angle)
    if subsurf_levels > 0:
        apply_subsurf(obj, levels=subsurf_levels, smooth_angle=smooth_angle)
    return obj


def loft_oval_rings(name, ring_specs, segments, mat, cap_bottom=True, cap_top=True, smooth_angle=64.0, subsurf_levels=1):
    """
    Loft an anatomical organic body segment from oval cross-section specs.
    Each spec: (z, rx, ry_front, ry_back, y_offset, x_offset)
    Note: In Blender local coords, +Y = Front, +X = Right, +Z = Up.
    """
    bm = bmesh.new()
    rings = []
    for (z, rx, ry_f, ry_b, y_off, x_off) in ring_specs:
        ring = []
        for s in range(segments):
            ang = (2.0 * math.pi * s) / segments
            ca = math.cos(ang)  # X
            sa = math.sin(ang)  # Y (+1 front, -1 back)
            ry = ry_f if sa >= 0.0 else ry_b
            co = Vector((x_off + rx * ca, y_off + ry * sa, z))
            ring.append(bm.verts.new(co))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))

    if cap_bottom:
        c_bot = bm.verts.new(Vector((ring_specs[0][5], ring_specs[0][4], ring_specs[0][0] - 0.004)))
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((c_bot, rings[0][sn], rings[0][s]))
    if cap_top:
        c_top = bm.verts.new(Vector((ring_specs[-1][5], ring_specs[-1][4], ring_specs[-1][0] + 0.004)))
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((c_top, rings[-1][s], rings[-1][sn]))

    return make_obj_from_bm(name, bm, mat, smooth_angle=smooth_angle, subsurf_levels=subsurf_levels)


def add_ellipsoid(name, center, radii, mat, segs=14, rings=10, rot_euler=(0.0, 0.0, 0.0), smooth_angle=65.0):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return make_obj_from_bm(name, bm, mat, smooth_angle=smooth_angle)


def add_bevel_box(name, center, size, mat, bevel=0.008, segments=2, rot_euler=(0.0, 0.0, 0.0), smooth_angle=42.0):
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


def join_parts_at_origin(parts, final_name, smooth_angle=None):
    bpy.ops.object.select_all(action="DESELECT")
    valid = [p for p in parts if p is not None]
    for p in valid:
        p.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = final_name
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
# Anatomical Sculpted Body Segment Builders
# ---------------------------------------------------------------------------
def build_pelvis_mesh(mats, is_female=False):
    parts = []
    hip_w = 0.178 if is_female else 0.172
    waist_w = 0.138 if is_female else 0.154
    specs = [
        (-0.092, hip_w * 0.90, 0.108, 0.128, -0.004, 0.0),
        (-0.040, hip_w * 0.99, 0.118, 0.138, -0.004, 0.0),
        (0.014,  hip_w * 0.97, 0.116, 0.130, -0.002, 0.0),
        (0.056,  waist_w + 0.008, 0.110, 0.118, 0.0, 0.0),
        (0.086,  waist_w, 0.106, 0.110, 0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Pelvis_Jeans", specs, 18, mats["CharPants"], smooth_angle=70.0, subsurf_levels=1))

    # Leather belt wrapping cleanly around waist top + brushed metal buckle
    belt_specs = [
        (0.048, waist_w + 0.011, 0.116, 0.120, 0.0, 0.0),
        (0.078, waist_w + 0.011, 0.116, 0.120, 0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Pelvis_Belt", belt_specs, 18, mats["CharBelt"], cap_bottom=False, cap_top=False, smooth_angle=48.0, subsurf_levels=0))
    parts.append(add_bevel_box("Belt_Buckle", (0.0, 0.120, 0.063), (0.044, 0.014, 0.036), mats["CharBuckle"], bevel=0.004, segments=1))
    return join_parts_at_origin(parts, "Pelvis_Mesh")


def build_torso_mesh(mats, variant="male"):
    parts = []
    is_female = (variant == "female")
    is_worker = (variant == "worker")

    waist_w = 0.144 if is_female else 0.162
    chest_w = 0.184 if is_female else 0.214
    shldr_w = 0.188 if is_female else 0.216
    chest_f = 0.146 if is_female else 0.150
    chest_b = 0.124 if is_female else 0.136

    # Inner undershirt core (full realistic ribcage depth — zero cardboard-thin silhouette!)
    inner_specs = [
        (-0.005, waist_w - 0.005, 0.108, 0.112, 0.0, 0.0),
        (0.14,   waist_w + 0.006, 0.126, 0.118, 0.004, 0.0),
        (0.29,   chest_w - 0.014, chest_f + 0.002, chest_b - 0.010, 0.006, 0.0),
        (0.39,   shldr_w - 0.036, 0.106, 0.104, 0.002, 0.0),
        (0.44,   0.104, 0.086, 0.088, 0.0, 0.0),
        (0.475,  0.072, 0.070, 0.072, 0.0, 0.0),
    ]
    inner_mat = mats["CharShirt"] if is_worker else mats["CharUndershirt"]
    parts.append(loft_oval_rings("Torso_Inner", inner_specs, 18, inner_mat, smooth_angle=68.0, subsurf_levels=1))

    # Main outer sculpted jacket / shirt shell with natural sloped trapezius shoulders & clean hem
    if is_worker:
        jacket_specs = [
            (-0.022, waist_w + 0.020, 0.130, 0.134, 0.0, 0.0),
            (0.06,   waist_w + 0.016, 0.130, 0.130, 0.002, 0.0),
            (0.19,   (waist_w + chest_w) * 0.5 + 0.010, 0.142, 0.136, 0.005, 0.0),
            (0.31,   chest_w + 0.010, chest_f + 0.010, chest_b + 0.010, 0.006, 0.0),
            (0.388,  shldr_w + 0.008, 0.122, 0.122, 0.002, 0.0),
            (0.438,  0.122, 0.098, 0.102, 0.0, 0.0),
            (0.474,  0.090, 0.084, 0.086, -0.003, 0.0),
        ]
        parts.append(loft_oval_rings("Torso_WorkJacket", jacket_specs, 20, mats["CharShirt"], smooth_angle=68.0, subsurf_levels=1))
        parts.append(add_bevel_box("Work_ZipPlacket", (0.0, chest_f + 0.010, 0.21), (0.024, 0.014, 0.41), mats["CharBelt"], bevel=0.004, segments=1))
        for sx in (-1.0, 1.0):
            parts.append(add_bevel_box(f"Work_Pocket_{sx}", (sx * 0.086, chest_f + 0.008, 0.295), (0.068, 0.018, 0.072), mats["CharShirt"], bevel=0.006, segments=1))
            parts.append(add_bevel_box(f"Work_PocketFlap_{sx}", (sx * 0.086, chest_f + 0.012, 0.326), (0.072, 0.014, 0.022), mats["CharPants"], bevel=0.004, segments=1))
    else:
        # Open-front casual jacket: sculpted lapel drape & rich athletic chest volume
        bm = bmesh.new()
        segs = 22
        ring_specs = [
            (-0.052, waist_w + 0.026, 0.136, 0.144, -0.002, 0.0),
            (0.03,   waist_w + 0.020, 0.132, 0.136, 0.0, 0.0),
            (0.16,   (waist_w + chest_w) * 0.5 + 0.010, 0.140, 0.136, 0.004, 0.0),
            (0.30,   chest_w + 0.010, chest_f + 0.012, chest_b + 0.012, 0.006, 0.0),
            (0.388,  shldr_w + 0.006, 0.122, 0.122, 0.002, 0.0),
            (0.438,  0.120, 0.098, 0.102, 0.0, 0.0),
            (0.474,  0.090, 0.084, 0.088, -0.004, 0.0),
        ]
        jacket_angles = [math.radians(108.0 + (s / (segs - 1)) * 324.0) for s in range(segs)]
        out_rings, in_rings = [], []
        thick = 0.015
        for (z, rx, ry_f, ry_b, y_off, x_off) in ring_specs:
            ro, ri = [], []
            for ang in jacket_angles:
                ca, sa = math.cos(ang), math.sin(ang)
                ry = ry_f if sa >= 0.0 else ry_b
                ro.append(bm.verts.new(Vector((x_off + rx * ca, y_off + ry * sa, z))))
                ri.append(bm.verts.new(Vector((x_off + (rx - thick) * ca, y_off + (ry - thick) * sa, z))))
            out_rings.append(ro)
            in_rings.append(ri)

        for i in range(len(out_rings) - 1):
            for s in range(len(jacket_angles) - 1):
                bm.faces.new((out_rings[i][s], out_rings[i][s + 1], out_rings[i + 1][s + 1], out_rings[i + 1][s]))
                bm.faces.new((in_rings[i][s + 1], in_rings[i][s], in_rings[i + 1][s], in_rings[i + 1][s + 1]))
            bm.faces.new((in_rings[i][0], out_rings[i][0], out_rings[i + 1][0], in_rings[i + 1][0]))
            bm.faces.new((out_rings[i][-1], in_rings[i][-1], in_rings[i + 1][-1], out_rings[i + 1][-1]))
        for s in range(len(jacket_angles) - 1):
            bm.faces.new((in_rings[0][s], in_rings[0][s + 1], out_rings[0][s + 1], out_rings[0][s]))
            bm.faces.new((out_rings[-1][s], out_rings[-1][s + 1], in_rings[-1][s + 1], in_rings[-1][s]))

        parts.append(make_obj_from_bm("Torso_OpenJacket", bm, mats["CharShirt"], smooth_angle=66.0, subsurf_levels=1))

    # Rolled jacket collar sitting snugly around neck base
    collar_specs = [
        (0.455, 0.094, 0.088, 0.092, -0.005, 0.0),
        (0.498, 0.086, 0.082, 0.088, -0.007, 0.0),
    ]
    parts.append(loft_oval_rings("Torso_Collar", collar_specs, 18, mats["CharShirt"], cap_bottom=False, cap_top=True, smooth_angle=56.0, subsurf_levels=0))

    return join_parts_at_origin(parts, "Chest")


def build_neck_mesh(mats):
    specs = [
        (-0.010, 0.062, 0.062, 0.060, 0.0, 0.0),
        (0.035,  0.052, 0.054, 0.054, 0.002, 0.0),
        (0.095,  0.056, 0.058, 0.058, 0.004, 0.0),
    ]
    return loft_oval_rings("Neck_Mesh", specs, 14, mats["CharSkin"], smooth_angle=65.0)


def build_head_mesh(mats, variant="male"):
    parts = []
    is_female = (variant == "female")
    is_worker = (variant == "worker")

    jaw_w = 0.064 if is_female else 0.072
    cheek_w = 0.082 if is_female else 0.086
    brow_w = 0.085 if is_female else 0.089
    z_shift = -0.022  # Natural proportional neck length!

    # Anatomically lofted head & jaw silhouette
    head_specs = [
        (0.002 + z_shift, 0.026, 0.046, 0.022, 0.024, 0.0),
        (0.026 + z_shift, jaw_w * 0.82, 0.078, 0.052, 0.016, 0.0),
        (0.056 + z_shift, jaw_w, 0.092, 0.076, 0.012, 0.0),
        (0.092 + z_shift, cheek_w, 0.102, 0.096, 0.008, 0.0),
        (0.128 + z_shift, brow_w, 0.105, 0.106, 0.004, 0.0),
        (0.166 + z_shift, brow_w * 0.98, 0.102, 0.110, 0.0, 0.0),
        (0.198 + z_shift, brow_w * 0.84, 0.088, 0.098, -0.004, 0.0),
        (0.218 + z_shift, 0.050, 0.054, 0.060, -0.006, 0.0),
    ]
    parts.append(loft_oval_rings("Head_Cranium", head_specs, 20, mats["CharSkin"], smooth_angle=70.0))

    # Sculpted nose (bridge + tip)
    nose_bm = bmesh.new()
    bmesh.ops.create_cube(nose_bm, size=1.0)
    bmesh.ops.scale(nose_bm, vec=Vector((0.022, 0.030, 0.042)), verts=nose_bm.verts)
    for v in nose_bm.verts:
        if v.co.z > 0:
            v.co.x *= 0.62
            v.co.y *= 0.58
    bmesh.ops.bevel(nose_bm, geom=list(nose_bm.edges), offset=0.006, segments=2, affect="EDGES", clamp_overlap=True)
    bmesh.ops.transform(nose_bm, matrix=Matrix.Translation(Vector((0.0, 0.105, 0.094 + z_shift))) @ Euler((0.20, 0.0, 0.0), "XYZ").to_matrix().to_4x4(), verts=nose_bm.verts)
    parts.append(make_obj_from_bm("Head_Nose", nose_bm, mats["CharSkin"], smooth_angle=62.0))

    # Sculpted left & right ears
    for sx in (-1.0, 1.0):
        parts.append(add_ellipsoid(
            f"Head_Ear_{sx}",
            (sx * (cheek_w + 0.002), -0.006, 0.105 + z_shift),
            (0.012, 0.019, 0.030),
            mats["CharSkin"],
            segs=10, rings=8,
            rot_euler=(0.0, 0.0, -sx * 0.25),
        ))

    # Realistic Eyes (recessed sclera + iris + upper eyelid/lash contour + natural eyebrows)
    eye_x = 0.031
    eye_y = 0.095
    eye_z = 0.117 + z_shift
    brow_h = 0.0045 if is_female else 0.0062
    for sx in (-1.0, 1.0):
        parts.append(add_ellipsoid(f"Eye_Sclera_{sx}", (sx * eye_x, eye_y, eye_z), (0.0135, 0.010, 0.0085), mats["CharEyeWhite"], segs=12, rings=8))
        parts.append(add_ellipsoid(f"Eye_Iris_{sx}", (sx * eye_x, eye_y + 0.0068, eye_z), (0.0068, 0.006, 0.0068), mats["CharEyeIris"], segs=10, rings=8))
        parts.append(add_ellipsoid(f"Eye_UpperLid_{sx}", (sx * eye_x, eye_y + 0.003, eye_z + 0.0055), (0.0155, 0.009, 0.0042), mats["CharSkin"], segs=10, rings=6))
        parts.append(add_bevel_box(
            f"Eyebrow_{sx}",
            (sx * eye_x, eye_y + 0.007, eye_z + 0.0185),
            (0.028, 0.007, brow_h),
            mats["CharHair"],
            bevel=0.002, segments=1,
            rot_euler=(0.0, sx * 0.06, -sx * 0.10),
        ))

    # Subtle sculpted lips
    parts.append(add_ellipsoid("Mouth_UpperLip", (0.0, 0.096, 0.060 + z_shift), (0.021, 0.0095, 0.0050), mats["CharLips"], segs=10, rings=6))
    parts.append(add_ellipsoid("Mouth_LowerLip", (0.0, 0.094, 0.052 + z_shift), (0.018, 0.0095, 0.0052), mats["CharLips"], segs=10, rings=6))

    # Natural Multi-Layered Sculpted Hairstyles (zero Playmobil helmet or hollow cylinder!)
    if is_female:
        # Close-fitting natural scalp crown
        crown_specs = [
            (0.125 + z_shift, 0.088, 0.072, 0.112, -0.014, 0.0),
            (0.168 + z_shift, 0.092, 0.096, 0.116, -0.008, 0.0),
            (0.200 + z_shift, 0.084, 0.092, 0.108, -0.006, 0.0),
            (0.222 + z_shift, 0.052, 0.058, 0.066, -0.006, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_FemaleCrown", crown_specs, 20, mats["CharHair"], smooth_angle=70.0, subsurf_levels=1))
        # Tapered layered back hair curving naturally down over neck/upper back
        back_hair = [
            (-0.045 + z_shift, 0.076, 0.022, 0.036, -0.098, 0.0),
            (0.030 + z_shift,  0.094, 0.028, 0.046, -0.088, 0.0),
            (0.100 + z_shift,  0.096, 0.032, 0.054, -0.076, 0.0),
            (0.162 + z_shift,  0.088, 0.036, 0.058, -0.062, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_FemaleBackFlow", back_hair, 18, mats["CharHair"], smooth_angle=70.0, subsurf_levels=1))
        # Soft tapered side locks framing cheeks naturally
        for sx in (-1.0, 1.0):
            side_lock = [
                (-0.015 + z_shift, 0.018, 0.028, 0.030, -0.015, sx * 0.082),
                (0.055 + z_shift,  0.022, 0.042, 0.042, -0.012, sx * 0.086),
                (0.125 + z_shift,  0.024, 0.050, 0.048, -0.010, sx * 0.084),
                (0.180 + z_shift,  0.022, 0.042, 0.040, -0.008, sx * 0.068),
            ]
            parts.append(loft_oval_rings(f"Hair_FemaleSide_{sx}", side_lock, 12, mats["CharHair"], smooth_angle=70.0, subsurf_levels=1))
        # Soft side-swept fringe
        parts.append(add_ellipsoid("Hair_FemaleBangs", (0.022, 0.064, 0.186 + z_shift), (0.068, 0.040, 0.024), mats["CharHair"], segs=14, rings=8, rot_euler=(-0.15, 0.16, -0.18)))
    elif is_worker:
        nape_specs = [
            (0.082 + z_shift, 0.084, 0.032, 0.096, -0.026, 0.0),
            (0.145 + z_shift, 0.090, 0.048, 0.106, -0.020, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_WorkerShort", nape_specs, 14, mats["CharHair"], smooth_angle=60.0, subsurf_levels=1))
        cap_specs = [
            (0.142 + z_shift, 0.096, 0.108, 0.114, -0.004, 0.0),
            (0.180 + z_shift, 0.094, 0.104, 0.112, -0.006, 0.0),
            (0.214 + z_shift, 0.080, 0.086, 0.094, -0.008, 0.0),
            (0.232 + z_shift, 0.040, 0.042, 0.046, -0.008, 0.0),
        ]
        parts.append(loft_oval_rings("Worker_CapCrown", cap_specs, 18, mats["CharShirt"], smooth_angle=60.0, subsurf_levels=1))
        parts.append(add_ellipsoid("Worker_CapBrim", (0.0, 0.124, 0.148 + z_shift), (0.088, 0.066, 0.011), mats["CharPants"], segs=14, rings=6, rot_euler=(0.16, 0.0, 0.0)))
        parts.append(add_ellipsoid("Worker_CapButton", (0.0, -0.008, 0.236 + z_shift), (0.012, 0.012, 0.006), mats["CharPants"], segs=8, rings=6))
    else:
        # Realistic tapered modern men's haircut (snug tapered sides + natural swept top, no helmet rim!)
        hair_specs = [
            (0.088 + z_shift, 0.084, 0.018, 0.094, -0.028, 0.0),
            (0.132 + z_shift, 0.090, 0.056, 0.108, -0.018, 0.0),
            (0.172 + z_shift, 0.092, 0.092, 0.112, -0.008, 0.0),
            (0.204 + z_shift, 0.082, 0.088, 0.102, -0.005, 0.0),
            (0.224 + z_shift, 0.050, 0.058, 0.064, 0.000, 0.0),
        ]
        parts.append(loft_oval_rings("Hair_MaleBase", hair_specs, 20, mats["CharHair"], smooth_angle=68.0, subsurf_levels=1))
        parts.append(add_ellipsoid("Hair_MaleTopSweep", (0.010, 0.042, 0.204 + z_shift), (0.068, 0.058, 0.026), mats["CharHair"], segs=14, rings=8, rot_euler=(-0.14, 0.10, -0.14)))

    return join_parts_at_origin(parts, "Head_Mesh")


def build_upper_arm_mesh(mats, is_female=False):
    parts = []
    r_sh = 0.056 if is_female else 0.064
    r_mid = 0.050 if is_female else 0.058
    r_el = 0.047 if is_female else 0.054
    # Smooth overlapping elbow/shoulder ends (no sausage pinch under Subsurf!)
    specs = [
        (-0.325, r_el * 0.88, r_el * 0.88, r_el * 0.88, 0.0, 0.0),
        (-0.295, r_el,        r_el,        r_el,        0.0, 0.0),
        (-0.265, r_el * 1.02, r_el * 1.02, r_el * 1.02, 0.0, 0.0),
        (-0.145, r_mid,       r_mid * 1.04, r_mid * 1.02, 0.0, 0.0),
        (-0.040, r_sh,        r_sh * 1.02, r_sh * 1.02, 0.0, 0.0),
        (0.005,  r_sh * 0.96, r_sh * 0.96, r_sh * 0.96, 0.0, 0.0),
        (0.038,  r_sh * 0.72, r_sh * 0.74, r_sh * 0.74, 0.0, 0.0),
    ]
    parts.append(loft_oval_rings("UpperArm_Sleeve", specs, 16, mats["CharShirt"], smooth_angle=70.0, subsurf_levels=1))
    return join_parts_at_origin(parts, "UpperArm_Mesh")


def build_forearm_and_hand_mesh(mats, side_sign=1.0, is_female=False):
    parts = []
    r_el = 0.047 if is_female else 0.054
    r_wr = 0.032 if is_female else 0.037
    sleeve_specs = [
        (-0.225, r_wr + 0.009, r_wr + 0.009, r_wr + 0.009, 0.0, 0.0),
        (-0.205, r_wr + 0.011, r_wr + 0.011, r_wr + 0.011, 0.0, 0.0),
        (-0.100, r_el * 0.90,  r_el * 0.92,  r_el * 0.90,  0.0, 0.0),
        (-0.015, r_el * 1.00,  r_el * 1.00,  r_el * 1.00,  0.0, 0.0),
        (0.018,  r_el * 0.96,  r_el * 0.96,  r_el * 0.96,  0.0, 0.0),
        (0.042,  r_el * 0.72,  r_el * 0.72,  r_el * 0.72,  0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Forearm_Sleeve", sleeve_specs, 14, mats["CharShirt"], smooth_angle=66.0, subsurf_levels=1))

    wrist_specs = [
        (-0.258, r_wr * 0.92, r_wr * 0.82, r_wr * 0.82, 0.004, 0.0),
        (-0.215, r_wr,        r_wr * 0.92, r_wr * 0.92, 0.0,   0.0),
    ]
    parts.append(loft_oval_rings("Wrist_Skin", wrist_specs, 12, mats["CharSkin"], smooth_angle=62.0, subsurf_levels=0))

    palm_x = 0.026 if is_female else 0.030
    palm_y = 0.068 if is_female else 0.076
    parts.append(add_bevel_box("Hand_Palm", (0.0, 0.006, -0.292), (palm_x, palm_y, 0.076), mats["CharSkin"], bevel=0.010, segments=2))
    parts.append(add_bevel_box(
        "Hand_Fingers",
        (-side_sign * 0.005, 0.008, -0.352),
        (palm_x * 0.80, palm_y * 0.92, 0.070),
        mats["CharSkin"],
        bevel=0.008, segments=2,
        rot_euler=(0.0, side_sign * 0.14, 0.0),
    ))
    parts.append(add_bevel_box(
        "Hand_Thumb",
        (-side_sign * 0.008, 0.046, -0.305),
        (0.019, 0.022, 0.054),
        mats["CharSkin"],
        bevel=0.006, segments=1,
        rot_euler=(-0.38, side_sign * 0.15, 0.0),
    ))
    return join_parts_at_origin(parts, "ForearmHand_Mesh")


def build_thigh_mesh(mats, is_female=False):
    r_top = 0.092 if is_female else 0.098
    r_mid = 0.084 if is_female else 0.090
    r_kn = 0.066 if is_female else 0.071
    specs = [
        (-0.468, r_kn * 0.86, r_kn * 0.88, r_kn * 0.86, 0.0, 0.0),
        (-0.432, r_kn,        r_kn * 1.04, r_kn * 1.00, 0.002, 0.0),
        (-0.395, r_kn * 1.02, r_kn * 1.06, r_kn * 1.02, 0.002, 0.0),
        (-0.230, r_mid,       r_mid * 1.10, r_mid * 1.06, 0.004, 0.0),
        (-0.060, r_top,       r_top * 1.06, r_top * 1.10, 0.0, 0.0),
        (0.015,  r_top * 0.94, r_top * 0.96, r_top * 0.98, 0.0, 0.0),
        (0.060,  r_top * 0.68, r_top * 0.70, r_top * 0.70, 0.0, 0.0),
    ]
    return loft_oval_rings("Thigh_Mesh", specs, 16, mats["CharPants"], smooth_angle=68.0, subsurf_levels=1)


def build_shin_mesh(mats, is_female=False):
    parts = []
    r_kn = 0.066 if is_female else 0.071
    r_calf = 0.063 if is_female else 0.070
    r_hem = 0.052 if is_female else 0.058
    specs = [
        (-0.395, r_hem,        r_hem * 1.04, r_hem * 1.04, 0.0, 0.0),
        (-0.340, r_hem * 1.04, r_hem * 1.06, r_hem * 1.08, -0.002, 0.0),
        (-0.180, r_calf,       r_calf * 0.98, r_calf * 1.14, -0.006, 0.0),
        (-0.025, r_kn,         r_kn * 1.04,   r_kn * 1.00,   0.002, 0.0),
        (0.020,  r_kn * 0.98,  r_kn * 1.00,   r_kn * 0.98,   0.0, 0.0),
        (0.052,  r_kn * 0.76,  r_kn * 0.78,   r_kn * 0.76,   0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Shin_Trouser", specs, 16, mats["CharPants"], smooth_angle=68.0, subsurf_levels=1))
    sock_specs = [
        (-0.435, 0.042, 0.046, 0.046, 0.0, 0.0),
        (-0.385, 0.045, 0.049, 0.049, 0.0, 0.0),
    ]
    parts.append(loft_oval_rings("Ankle_Sock", sock_specs, 12, mats["CharShoeSole"], smooth_angle=55.0, subsurf_levels=0))
    return join_parts_at_origin(parts, "Shin_Mesh")


def build_foot_shoe_mesh(mats, is_worker=False):
    parts = []
    # Ankle pivot is at world z = 0.08 -> shoe sole bottom sits flat at local z = -0.080!
    sole_mat = mats["CharBelt"] if is_worker else mats["CharShoeSole"]
    parts.append(add_bevel_box("Shoe_Sole", (0.0, 0.054, -0.069), (0.096, 0.244, 0.022), sole_mat, bevel=0.008, segments=2))
    parts.append(add_bevel_box("Shoe_UpperMain", (0.0, 0.046, -0.037), (0.092, 0.224, 0.048), mats["CharShoes"], bevel=0.018, segments=2))
    parts.append(add_ellipsoid("Shoe_ToeCap", (0.0, 0.140, -0.045), (0.044, 0.040, 0.023), mats["CharShoes"], segs=12, rings=8))
    parts.append(add_bevel_box("Shoe_AnkleCollar", (0.0, -0.006, -0.003), (0.088, 0.104, 0.054), mats["CharShoes"], bevel=0.016, segments=2))
    parts.append(add_bevel_box("Shoe_Tongue", (0.0, 0.064, -0.009), (0.058, 0.042, 0.020), sole_mat, bevel=0.007, segments=1, rot_euler=(-0.55, 0.0, 0.0)))
    for li in range(3):
        parts.append(add_bevel_box(
            f"Shoe_Lace_{li}",
            (0.0, 0.070 + li * 0.023, -0.015 - li * 0.008),
            (0.054, 0.009, 0.007),
            sole_mat, bevel=0.002, segments=1,
        ))
    return join_parts_at_origin(parts, "Foot_Shoe")


# ---------------------------------------------------------------------------
# Full Articulated Character Rig Assembly (Exact 14 Pivots for Unity!)
# ---------------------------------------------------------------------------
def build_character_rig(rig_name, variant, mats):
    is_female = (variant == "female")
    is_worker = (variant == "worker")
    shldr_x = 0.174 if is_female else 0.200
    hip_x = 0.102 if is_female else 0.106

    root = make_pivot(rig_name, None, (0.0, 0.0, 0.0))

    # 1. Hips (0, 0, 0.95)
    hips = make_pivot("Hips", root, (0.0, 0.0, 0.95))
    attach_mesh_to_pivot(build_pelvis_mesh(mats, is_female=is_female), hips)

    # 2. Spine (child of Hips at 0, 0, 0.06)
    spine = make_pivot("Spine", hips, (0.0, 0.0, 0.06))
    attach_mesh_to_pivot(build_torso_mesh(mats, variant=variant), spine)

    # 3. Neck -> Head
    neck = make_pivot("Neck", spine, (0.0, 0.0, 0.46))
    attach_mesh_to_pivot(build_neck_mesh(mats), neck)

    head = make_pivot("Head", neck, (0.0, 0.0, 0.09))
    attach_mesh_to_pivot(build_head_mesh(mats, variant=variant), head)

    # 4. Arms (L = -X, R = +X) — shoulder height 0.382 blends organically with trapezius slope
    for side, sx in (("L", -1.0), ("R", 1.0)):
        shldr = make_pivot(f"{side}_Shoulder", spine, (sx * shldr_x, 0.0, 0.382))
        attach_mesh_to_pivot(build_upper_arm_mesh(mats, is_female=is_female), shldr)

        elbow = make_pivot(f"{side}_Elbow", shldr, (0.0, 0.0, -0.295))
        attach_mesh_to_pivot(build_forearm_and_hand_mesh(mats, side_sign=sx, is_female=is_female), elbow)

    # 5. Legs (L = -X, R = +X)
    for side, sx in (("L", -1.0), ("R", 1.0)):
        hip = make_pivot(f"{side}_Hip", hips, (sx * hip_x, 0.0, -0.02))
        attach_mesh_to_pivot(build_thigh_mesh(mats, is_female=is_female), hip)

        knee = make_pivot(f"{side}_Knee", hip, (0.0, 0.0, -0.43))
        attach_mesh_to_pivot(build_shin_mesh(mats, is_female=is_female), knee)

        ankle = make_pivot(f"{side}_Ankle", knee, (0.0, 0.0, -0.42))
        attach_mesh_to_pivot(build_foot_shoe_mesh(mats, is_worker=is_worker), ankle)

    return root


def build_flat_export_parts(variant, mats):
    """
    Build all 14 sculpted body parts centered cleanly at (0,0,0) in local pivot space,
    unparented so Blender's FBX bake_space_transform bakes exact Unity (+X right, +Y up, +Z forward)
    directly into vertices with identity transforms!
    Unity's ProceduralHumanoid mounts each named part onto its exact corresponding skeleton pivot.
    """
    is_female = (variant == "female")
    is_worker = (variant == "worker")

    parts = [
        ("Part_Pelvis",      build_pelvis_mesh(mats, is_female=is_female)),
        ("Part_Chest",       build_torso_mesh(mats, variant=variant)),
        ("Part_Neck",        build_neck_mesh(mats)),
        ("Part_Head",        build_head_mesh(mats, variant=variant)),
        ("Part_L_UpperArm",  build_upper_arm_mesh(mats, is_female=is_female)),
        ("Part_L_Forearm",   build_forearm_and_hand_mesh(mats, side_sign=-1.0, is_female=is_female)),
        ("Part_R_UpperArm",  build_upper_arm_mesh(mats, is_female=is_female)),
        ("Part_R_Forearm",   build_forearm_and_hand_mesh(mats, side_sign=1.0, is_female=is_female)),
        ("Part_L_Thigh",     build_thigh_mesh(mats, is_female=is_female)),
        ("Part_L_Shin",      build_shin_mesh(mats, is_female=is_female)),
        ("Part_L_Foot",      build_foot_shoe_mesh(mats, is_worker=is_worker)),
        ("Part_R_Thigh",     build_thigh_mesh(mats, is_female=is_female)),
        ("Part_R_Shin",      build_shin_mesh(mats, is_female=is_female)),
        ("Part_R_Foot",      build_foot_shoe_mesh(mats, is_worker=is_worker)),
    ]
    out = []
    for pname, obj in parts:
        obj.name = pname
        obj.location = Vector((0.0, 0.0, 0.0))
        obj.rotation_euler = Euler((0.0, 0.0, 0.0), "XYZ")
        obj.scale = Vector((1.0, 1.0, 1.0))
        out.append(obj)
    return out


def export_parts_fbx(part_objs, filename):
    filepath = os.path.join(OUT_FBX_DIR, filename)
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


# ---------------------------------------------------------------------------
# Studio Preview Render of All 3 Characters (Front 3/4 + Side/Back!)
# ---------------------------------------------------------------------------
def render_character_showcase():
    reset_scene()
    mats_base = get_character_materials()

    # Studio floor
    bm_g = bmesh.new()
    bmesh.ops.create_grid(bm_g, x_segments=1, y_segments=1, size=14.0)
    create_floor = make_obj_from_bm("StudioFloor", bm_g, make_mat("Floor", (0.20, 0.22, 0.25), roughness=0.72))

    # Build 4 showcase poses: Player (blue jacket), Elena (teal coat), Marco (orange work jacket + cap), Back 3/4 Pedestrian (burgundy)
    configs = [
        ("Showcase_Player",   "male",   (-1.55, 0.0, 0.0),  162.0, (0.16, 0.30, 0.48), (0.14, 0.16, 0.22), (0.12, 0.08, 0.05)),
        ("Showcase_Elena",    "female", (-0.50, 0.0, 0.0),  175.0, (0.16, 0.54, 0.46), (0.16, 0.18, 0.22), (0.22, 0.13, 0.07)),
        ("Showcase_Marco",    "worker", (0.55,  0.0, 0.0),  192.0, (0.76, 0.42, 0.14), (0.18, 0.20, 0.24), (0.14, 0.10, 0.07)),
        ("Showcase_BackView", "male",   (1.58,  0.0, 0.0),  -38.0, (0.46, 0.14, 0.24), (0.20, 0.22, 0.26), (0.18, 0.12, 0.08)),
    ]

    for cname, variant, pos, yaw_deg, shirt_rgb, pants_rgb, hair_rgb in configs:
        m = dict(mats_base)
        m["CharShirt"] = make_mat(f"{cname}_Shirt", shirt_rgb, roughness=0.72)
        m["CharPants"] = make_mat(f"{cname}_Pants", pants_rgb, roughness=0.80)
        m["CharHair"] = make_mat(f"{cname}_Hair", hair_rgb, roughness=0.60)
        rig = build_character_rig(cname, variant, m)
        rig.location = Vector(pos)
        rig.rotation_euler = Euler((0.0, 0.0, math.radians(yaw_deg)), "XYZ")

    # Lighting + World
    world = bpy.data.worlds.new("CharStudioWorld")
    bpy.context.scene.world = world
    if world.node_tree is None:
        world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.60, 0.72, 0.86, 1.0)
        bg.inputs["Strength"].default_value = 0.85

    sun1_d = bpy.data.lights.new("KeySun", type="SUN")
    sun1_d.energy = 3.2
    sun1_d.color = (1.0, 0.95, 0.88)
    sun1 = bpy.data.objects.new("KeySun", sun1_d)
    bpy.context.collection.objects.link(sun1)
    sun1.rotation_euler = Euler((math.radians(48), math.radians(10), math.radians(-35)), "XYZ")

    sun2_d = bpy.data.lights.new("FillSun", type="SUN")
    sun2_d.energy = 1.0
    sun2_d.color = (0.76, 0.86, 1.0)
    sun2 = bpy.data.objects.new("FillSun", sun2_d)
    bpy.context.collection.objects.link(sun2)
    sun2.rotation_euler = Euler((math.radians(35), 0.0, math.radians(140)), "XYZ")

    cam_d = bpy.data.cameras.new("CharCam")
    cam_d.lens = 50.0
    cam = bpy.data.objects.new("CharCam", cam_d)
    bpy.context.collection.objects.link(cam)
    cam.location = Vector((0.0, -5.2, 1.25))
    direction = Vector((0.0, 0.0, 0.95)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_Characters_Showcase.blend"))
    bpy.ops.render.render(write_still=True)
    print(f"Saved character showcase render to: {PREVIEW_PATH}")


def main():
    variants = [
        ("Character_CasualMale.fbx",   "Character_CasualMale",   "male"),
        ("Character_CasualFemale.fbx", "Character_CasualFemale", "female"),
        ("Character_Worker.fbx",       "Character_Worker",       "worker"),
    ]
    print(f"[GRIDROAM Blender] Generating {len(variants)} articulated realistic human FBX rigs into {OUT_FBX_DIR} ...")
    for fbx_name, rig_name, variant in variants:
        reset_scene()
        mats = get_character_materials()
        part_objs = build_flat_export_parts(variant, mats)
        export_parts_fbx(part_objs, fbx_name)

    render_character_showcase()
    print("[GRIDROAM Blender] All 3 articulated human character rigs exported successfully!")


if __name__ == "__main__":
    main()
