#!/usr/bin/env python3
"""
GRIDROAM Phase 4 (Batches 3 & 4) – Complete Rigged Animal Stylized-Realism Upgrade!
Upgrades ALL 10 remaining rigged farm, domestic & wildlife animals in Resources/OurWorldLife/
while preserving 100% of Armature hierarchies & embedded SkeletalClipPlayer animation clips:

Group A (56-65 bone multi-clip rigs -> Catmull-Clark organic subdivision, smooth shading, seamless UVs):
  - OW_Cow.fbx          (56 bones, 7 clips)
  - OW_StreetDog.fbx    (65 bones, 11 clips)
  - OW_ShepherdDog.fbx  (65 bones, 6 clips)
  - OW_LakeFish.fbx     (11 bones, 1 clip)

Group B (Primitive-capsule/cone rigs -> FULL stylized-realistic anatomical re-sculpt + region-safe skinning + UVs):
  - OW_FarmGoat.fbx     (8 bones, 4 clips)  - Lean caprine body, swept ridged horns, goatee, concave ears, cloven hooves
  - OW_StreetCat.fbx    (8 bones, 4 clips)  - Lithe feline torso, pointed cupped ears, almond eyes, S-curved tail, sculpted paws
  - OW_Chicken.fbx      (10 bones, 4 clips) - Plump feathered body, layered wing & tail plumes, comb/wattle/beak, 3-toe claws
  - OW_Pigeon.fbx       (6 bones, 3 clips)  - Classic Istanbul street pigeon w/ puffed breast, layered wing bars, wedge tail
  - OW_PondDuck.fbx     (7 bones, 2 clips)  - Contoured mallard hull, curved neck, broad flat bill, layered folded wings
  - OW_LakeSwan.fbx     (6 bones, 2 clips)  - Regal double-S neck, cupped raised swan wing plumes, black lore mask & orange bill
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
REF_DIR = os.path.join(SCRIPT_DIR, "reference_rigs")
os.makedirs(REF_DIR, exist_ok=True)


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else (hi if v > hi else v)


def smoothstep(e0, e1, x):
    t = clamp((x - e0) / max(1e-6, e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def apply_smooth(obj, angle_deg=66.0):
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


def box_uv_obj(obj, scale=2.0):
    mesh = obj.data
    if not isinstance(mesh, bpy.types.Mesh):
        return
    bm = bmesh.new()
    bm.from_mesh(mesh)
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
    bm.to_mesh(mesh)
    bm.free()


def add_bm(target_bm, src_bm, mat_idx, uv_scale=2.0, tag_layer=None, tag_val=0):
    bmesh.ops.recalc_face_normals(src_bm, faces=src_bm.faces)
    uv_layer = target_bm.loops.layers.uv.verify()
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
            nf.smooth = True
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
                loop[uv_layer].uv = (u, v)
        except ValueError:
            pass
    src_bm.free()


def ellipsoid_bm(center, radii, segs=14, rings=10, rot_euler=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    rot_m = Euler(rot_euler, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot_m, verts=bm.verts)
    return bm


def tube_bm(pts_radii, segments=12, cap_start=True, cap_end=True, rounded=True):
    bm = bmesh.new()
    n = len(pts_radii)
    rings, frames = [], []
    prev_up = Vector((0, 0, 1))
    for i, (pt, rx, ry) in enumerate(pts_radii):
        p = Vector(pt)
        if i == 0:
            tan = (Vector(pts_radii[1][0]) - p).normalized()
        elif i == n - 1:
            tan = (p - Vector(pts_radii[i - 1][0])).normalized()
        else:
            tan = (Vector(pts_radii[i + 1][0]) - Vector(pts_radii[i - 1][0])).normalized()
        if tan.length < 1e-6:
            tan = Vector((0, 0, 1))
        ref = prev_up if abs(tan.dot(prev_up)) < 0.90 else Vector((1, 0, 0))
        if abs(tan.dot(ref)) >= 0.90:
            ref = Vector((0, 1, 0))
        side = tan.cross(ref).normalized()
        up = side.cross(tan).normalized()
        prev_up = up
        frames.append((p, tan, side, up, rx, ry))
        ring = [bm.verts.new(p + side * (math.cos(2 * math.pi * s / segments) * rx) + up * (math.sin(2 * math.pi * s / segments) * ry)) for s in range(segments)]
        rings.append(ring)
    for i in range(n - 1):
        for s in range(segments):
            sn = (s + 1) % segments
            bm.faces.new((rings[i][s], rings[i][sn], rings[i + 1][sn], rings[i + 1][s]))

    def cap(frame, br, flip):
        p, tan, side, up, rx, ry = frame
        sign = -1.0 if flip else 1.0
        ra = 0.5 * (rx + ry)
        if rounded:
            pr = br
            for df, rf in ((0.32, 0.85), (0.58, 0.54)):
                cp = p + tan * (sign * ra * df)
                nr = [bm.verts.new(cp + side * (math.cos(2 * math.pi * s / segments) * rx * rf) + up * (math.sin(2 * math.pi * s / segments) * ry * rf)) for s in range(segments)]
                for s in range(segments):
                    sn = (s + 1) % segments
                    bm.faces.new((nr[s], nr[sn], pr[sn], pr[s]) if flip else (pr[s], pr[sn], nr[sn], nr[s]))
                pr = nr
            ap = bm.verts.new(p + tan * (sign * ra * 0.72))
            for s in range(segments):
                sn = (s + 1) % segments
                bm.faces.new((ap, pr[sn], pr[s]) if flip else (ap, pr[s], pr[sn]))
        else:
            ap = bm.verts.new(p + tan * (sign * 0.002))
            for s in range(segments):
                sn = (s + 1) % segments
                bm.faces.new((ap, br[sn], br[s]) if flip else (ap, br[s], br[sn]))

    if cap_start:
        cap(frames[0], rings[0], True)
    if cap_end:
        cap(frames[-1], rings[-1], False)
    return bm


def load_orig_rig(fbx_name):
    fbx_path = os.path.join(LIFE_DIR, fbx_name)
    backup = os.path.join(REF_DIR, fbx_name.replace(".fbx", "_orig.fbx"))
    if not os.path.exists(backup):
        shutil.copy2(fbx_path, backup)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=backup)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh_o = next(o for o in bpy.data.objects if o.type == "MESH")
    arm.data.pose_position = "REST"
    bpy.context.view_layer.update()
    # Normalize action names to short names so export writes clean {Armature}|{Clip}
    for act in list(bpy.data.actions):
        act.name = act.name.split("|")[-1]
    if arm.animation_data:
        idle = bpy.data.actions.get("Idle") or (bpy.data.actions[0] if bpy.data.actions else None)
        if idle:
            arm.animation_data.action = idle
    return fbx_path, arm, mesh_o


def export_rigged(fbx_path, arm, mesh_o):
    arm.data.pose_position = "POSE"
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh_o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
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
    tris = sum(max(1, len(p.vertices) - 2) for p in mesh_o.data.polygons)
    print(f"  [Animal] {os.path.basename(fbx_path):20s} | tris={tris:5d} | bones={len(arm.data.bones):2d} | clips={len(bpy.data.actions):2d} | uv={bool(mesh_o.data.uv_layers)}")


# ===========================================================================
# GROUP A: High-Bone-Count Rigged Animals (Catmull-Clark Smooth + Seam-Free UVs!)
# ===========================================================================
def upgrade_group_a_subsurf(fbx_name, uv_scale=1.8, subsurf_lvl=1):
    fbx_path, arm, mesh_o = load_orig_rig(fbx_name)
    # Apply Catmull-Clark Subdivision BEFORE the Armature modifier so all 56-65 bone weights
    # smoothly interpolate onto the high-resolution organic mesh!
    arm_mod = next((m for m in mesh_o.modifiers if m.type == "ARMATURE"), None)
    sub = mesh_o.modifiers.new(name="SmoothOrganic", type="SUBSURF")
    sub.levels = subsurf_lvl
    sub.render_levels = subsurf_lvl
    bpy.ops.object.select_all(action="DESELECT")
    mesh_o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_o
    # Move Subsurf above Armature before applying!
    if arm_mod:
        bpy.ops.object.modifier_move_to_index(modifier=sub.name, index=0)
    bpy.ops.object.modifier_apply(modifier=sub.name)

    # Subtle gentle organic smoothing + clean seamless box UV projection + 4-bone mobile limit!
    box_uv_obj(mesh_o, scale=uv_scale)
    apply_smooth(mesh_o, angle_deg=68.0)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

    export_rigged(fbx_path, arm, mesh_o)


# ===========================================================================
# GROUP B: Full Anatomical Stylized-Realistic Re-Sculpts (Preserving Exact Rig!)
# ===========================================================================
def finish_resculpt(fbx_path, arm, orig_mesh_o, bm, tag_layer, tag_bone_fn, uv_scale=2.0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    v_tags = [v[tag_layer] for v in bm.verts]
    v_wcos = [v.co.copy() for v in bm.verts]

    new_mesh = bpy.data.meshes.new(orig_mesh_o.data.name + "_Upgraded")
    bm.to_mesh(new_mesh)
    bm.free()
    for m in orig_mesh_o.data.materials:
        new_mesh.materials.append(m)

    new_obj = bpy.data.objects.new(orig_mesh_o.name + "_New", new_mesh)
    bpy.context.collection.objects.link(new_obj)
    apply_smooth(new_obj, angle_deg=66.0)

    # Create all vertex groups from original mesh
    for vg in orig_mesh_o.vertex_groups:
        new_obj.vertex_groups.new(name=vg.name)
    # Also ensure every deform bone exists as a vertex group
    for b in arm.data.bones:
        if b.name not in new_obj.vertex_groups:
            new_obj.vertex_groups.new(name=b.name)

    vg_map = {vg.name: vg for vg in new_obj.vertex_groups}
    for vi, (tag, wco) in enumerate(zip(v_tags, v_wcos)):
        weights = tag_bone_fn(tag, wco)
        for bname, w in weights:
            if bname in vg_map and w > 0.005:
                vg_map[bname].add([vi], w, "REPLACE")

    bpy.ops.object.select_all(action="DESELECT")
    new_obj.select_set(True)
    bpy.context.view_layer.objects.active = new_obj
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

    old_name, old_mname = orig_mesh_o.name, orig_mesh_o.data.name
    bpy.data.objects.remove(orig_mesh_o, do_unlink=True)
    new_obj.name = old_name
    new_obj.data.name = old_mname
    new_obj.parent = arm
    new_obj.matrix_parent_inverse = arm.matrix_world.inverted()
    arm_mod = new_obj.modifiers.new(name="Armature", type="ARMATURE")
    arm_mod.object = arm
    export_rigged(fbx_path, arm, new_obj)


# 1. OW_FarmGoat.fbx (8 bones, facing +Y! Mats: 0=Cream, 1=Brown, 2=Horn, 3=Pupil, 4=Hoof)
def upgrade_goat():
    fbx_path, arm, orig = load_orig_rig("OW_FarmGoat.fbx")
    bm = bmesh.new()
    tl = bm.verts.layers.int.new("tag")
    # Tags: 0=Spine, 1=Neck, 2=Head, 3=Tail, 4=FL, 5=FR, 6=BL, 7=BR
    CREAM, BROWN, HORN, EYE, HOOF = 0, 1, 2, 3, 4

    # Lean caprine sculpted torso (higher withers, tucked flank, bony rump!)
    torso = [
        (Vector((0, -0.39, 0.65)), 0.145, 0.150),
        (Vector((0, -0.22, 0.67)), 0.185, 0.195),
        (Vector((0,  0.02, 0.64)), 0.180, 0.200),
        (Vector((0,  0.24, 0.66)), 0.175, 0.210),
        (Vector((0,  0.37, 0.68)), 0.138, 0.165),
    ]
    add_bm(bm, tube_bm(torso, 18), CREAM, 2.0, tl, 0)
    # Brown saddle / shoulder coat marking
    add_bm(bm, ellipsoid_bm((0, 0.04, 0.72), (0.190, 0.235, 0.135), 16, 10), BROWN, 2.0, tl, 0)
    # Graceful upright caprine neck
    neck = [
        (Vector((0, 0.30, 0.69)), 0.115, 0.125),
        (Vector((0, 0.39, 0.79)), 0.088, 0.098),
        (Vector((0, 0.48, 0.90)), 0.072, 0.082),
    ]
    add_bm(bm, tube_bm(neck, 14), CREAM, 2.2, tl, 1)

    # Sculpted Goat Skull, Tapered Snout, Goatee Beard, Expressive Eyes, Curved Ridged Horns & Perky Ears!
    head = [
        (Vector((0, 0.46, 0.93)), 0.078, 0.082),
        (Vector((0, 0.56, 0.95)), 0.076, 0.076),
        (Vector((0, 0.67, 0.92)), 0.052, 0.056),
        (Vector((0, 0.76, 0.89)), 0.038, 0.040),
    ]
    add_bm(bm, tube_bm(head, 16), CREAM, 2.4, tl, 2)
    # Classic Anatolian goat goatee beard under chin!
    beard = [
        (Vector((0, 0.67, 0.86)), 0.028, 0.032),
        (Vector((0, 0.69, 0.80)), 0.022, 0.024),
        (Vector((0, 0.70, 0.74)), 0.012, 0.012),
    ]
    add_bm(bm, tube_bm(beard, 10), BROWN, 2.6, tl, 2)
    # Swept curved goat horns + perky cupped ears + glossy eyes w/ brow ridge!
    for sx in (-1.0, 1.0):
        h_pts = [
            (Vector((sx * 0.038, 0.52, 1.00)), 0.024, 0.026),
            (Vector((sx * 0.075, 0.46, 1.11)), 0.020, 0.022),
            (Vector((sx * 0.118, 0.36, 1.19)), 0.014, 0.016),
            (Vector((sx * 0.148, 0.25, 1.18)), 0.007, 0.008),
        ]
        add_bm(bm, tube_bm(h_pts, 10), HORN, 2.6, tl, 2)
        e_pts = [
            (Vector((sx * 0.068, 0.50, 0.96)), 0.012, 0.020),
            (Vector((sx * 0.132, 0.50, 0.97)), 0.011, 0.028),
            (Vector((sx * 0.188, 0.49, 0.94)), 0.007, 0.016),
        ]
        add_bm(bm, tube_bm(e_pts, 8), BROWN, 2.4, tl, 2)
        add_bm(bm, ellipsoid_bm((sx * 0.068, 0.58, 0.965), (0.014, 0.017, 0.014), 10, 8), EYE, 2.0, tl, 2)
        add_bm(bm, ellipsoid_bm((sx * 0.016, 0.785, 0.892), (0.007, 0.008, 0.006), 8, 5), EYE, 2.0, tl, 2)

    # Cute upturned fluffy goat tail
    t_pts = [
        (Vector((0, -0.39, 0.69)), 0.036, 0.038),
        (Vector((0, -0.47, 0.77)), 0.032, 0.034),
        (Vector((0, -0.52, 0.86)), 0.020, 0.024),
    ]
    add_bm(bm, tube_bm(t_pts, 10), BROWN, 2.2, tl, 3)

    # 4 Jointed Caprine Legs w/ knees/hocks & Split Cloven Hooves!
    legs = [
        (4, -0.14,  0.26,  0.00), (5, 0.14,  0.26,  0.00),
        (6, -0.15, -0.26, -0.05), (7, 0.15, -0.26, -0.05),
    ]
    for ltag, lx, ly, hock_dy in legs:
        l_pts = [
            (Vector((lx, ly, 0.54)), 0.062, 0.068),
            (Vector((lx, ly + hock_dy * 0.5, 0.41)), 0.044, 0.048),
            (Vector((lx, ly + hock_dy, 0.27)), 0.034, 0.038),
            (Vector((lx, ly + hock_dy * 0.3, 0.12)), 0.025, 0.027),
            (Vector((lx, ly, 0.04)), 0.028, 0.032),
        ]
        add_bm(bm, tube_bm(l_pts, 10), CREAM if ltag in (4, 5) else BROWN, 2.4, tl, ltag)
        for tx in (-0.013, 0.013):
            add_bm(bm, ellipsoid_bm((lx + tx, ly + 0.012, 0.020), (0.014, 0.034, 0.020), 8, 6), HOOF, 2.4, tl, ltag)

    def weights(tag, wco):
        if tag == 0:
            t_n = smoothstep(0.28, 0.40, wco.y)
            return [("Spine", 1.0 - t_n * 0.4), ("Neck", t_n * 0.4)]
        if tag == 1:
            t_h = smoothstep(0.82, 0.93, wco.z)
            return [("Neck", 1.0 - t_h * 0.5), ("Head", t_h * 0.5)]
        if tag == 2:
            return [("Head", 1.0)]
        if tag == 3:
            return [("Tail", 1.0)]
        bname = {4: "Leg_FL", 5: "Leg_FR", 6: "Leg_BL", 7: "Leg_BR"}[tag]
        t_top = smoothstep(0.42, 0.56, wco.z)
        return [(bname, 1.0 - t_top * 0.35), ("Spine", t_top * 0.35)]

    finish_resculpt(fbx_path, arm, orig, bm, tl, weights)


# 2. OW_StreetCat.fbx (8 bones, facing +Y! Mats: 0=Primary, 1=Belly, 2=Accent, 3=Nose, 4=AmberEye, 5=Pupil)
def upgrade_cat():
    fbx_path, arm, orig = load_orig_rig("OW_StreetCat.fbx")
    bm = bmesh.new()
    tl = bm.verts.layers.int.new("tag")
    # Tags: 0=Spine, 1=Head, 2=Tail, 3=FL, 4=FR, 5=BL, 6=BR
    PRI, BELLY, ACC, NOSE, AMBER, PUPIL = 0, 1, 2, 3, 4, 5

    # Lithe supple Istanbul street cat torso (graceful waist tuck, rounded haunches, soft chest ruff)
    body = [
        (Vector((0, -0.20, 0.255)), 0.070, 0.072),
        (Vector((0, -0.10, 0.270)), 0.088, 0.090),
        (Vector((0,  0.03, 0.252)), 0.080, 0.082),
        (Vector((0,  0.16, 0.265)), 0.084, 0.088),
        (Vector((0,  0.23, 0.310)), 0.062, 0.068),
    ]
    add_bm(bm, tube_bm(body, 18), PRI, 2.8, tl, 0)
    # Soft cream chest ruff & belly fur
    add_bm(bm, ellipsoid_bm((0, 0.165, 0.230), (0.066, 0.082, 0.068), 14, 10), BELLY, 2.8, tl, 0)
    add_bm(bm, ellipsoid_bm((0, -0.02, 0.215), (0.064, 0.145, 0.052), 14, 8), BELLY, 2.8, tl, 0)
    # Subtle tabby saddle stripe patches
    for sy in (-0.08, 0.02, 0.11):
        add_bm(bm, ellipsoid_bm((0, sy, 0.312), (0.072, 0.032, 0.032), 12, 8), ACC, 2.8, tl, 0)

    # Expressive Sculpted Feline Head (soft cheeks, whiskered muzzle pads, pink triangle nose, almond eyes, pointed cupped ears!)
    add_bm(bm, ellipsoid_bm((0, 0.285, 0.365), (0.076, 0.080, 0.068), 18, 12), PRI, 3.0, tl, 1)
    # Muzzle pads & pink triangle nose + soft white chin
    for sx in (-1.0, 1.0):
        add_bm(bm, ellipsoid_bm((sx * 0.022, 0.352, 0.348), (0.024, 0.026, 0.020), 10, 8), BELLY, 3.0, tl, 1)
        # Almond glossy amber eyes + vertical slit pupils!
        add_bm(bm, ellipsoid_bm((sx * 0.036, 0.345, 0.378), (0.017, 0.014, 0.015), 12, 8, (0, 0, -sx * 0.28)), AMBER, 2.0, tl, 1)
        add_bm(bm, ellipsoid_bm((sx * 0.037, 0.354, 0.378), (0.005, 0.009, 0.013), 10, 8), PUPIL, 2.0, tl, 1)
        # Pointed triangular cupped feline ears!
        ear = [
            (Vector((sx * 0.044, 0.285, 0.415)), 0.026, 0.018),
            (Vector((sx * 0.058, 0.288, 0.452)), 0.018, 0.013),
            (Vector((sx * 0.068, 0.290, 0.482)), 0.007, 0.006),
        ]
        add_bm(bm, tube_bm(ear, 8), ACC, 3.0, tl, 1)
        # Inner pink ear blush
        add_bm(bm, ellipsoid_bm((sx * 0.054, 0.296, 0.442), (0.011, 0.006, 0.020), 8, 6, (0, sx * 0.2, 0)), NOSE, 3.0, tl, 1)
    add_bm(bm, ellipsoid_bm((0, 0.372, 0.354), (0.012, 0.010, 0.009), 10, 6), NOSE, 3.0, tl, 1)
    add_bm(bm, ellipsoid_bm((0, 0.342, 0.330), (0.024, 0.024, 0.016), 10, 6), BELLY, 3.0, tl, 1)

    # Graceful S-Curved Plush Feline Tail (aligned smoothly along TailBase -> TailTip!)
    tail = [
        (Vector((0, -0.20, 0.27)), 0.026, 0.026),
        (Vector((0, -0.27, 0.34)), 0.025, 0.025),
        (Vector((0, -0.31, 0.44)), 0.024, 0.024),
        (Vector((0, -0.32, 0.53)), 0.022, 0.022),
        (Vector((0, -0.29, 0.59)), 0.018, 0.018),
    ]
    add_bm(bm, tube_bm(tail, 10), ACC, 2.8, tl, 2)

    # 4 Contoured Feline Legs + Cute Rounded Cream Paws!
    legs = [(3, -0.068, 0.145, 0.0), (4, 0.068, 0.145, 0.0), (5, -0.070, -0.140, -0.03), (6, 0.070, -0.140, -0.03)]
    for ltag, lx, ly, hdy in legs:
        l_pts = [
            (Vector((lx, ly, 0.22)), 0.036, 0.042),
            (Vector((lx, ly + hdy, 0.13)), 0.024, 0.026),
            (Vector((lx, ly + hdy * 0.3, 0.05)), 0.020, 0.022),
            (Vector((lx, ly + 0.015, 0.018)), 0.022, 0.026),
        ]
        add_bm(bm, tube_bm(l_pts, 10), PRI, 2.8, tl, ltag)
        add_bm(bm, ellipsoid_bm((lx, ly + 0.022, 0.016), (0.024, 0.032, 0.016), 10, 6), BELLY, 2.8, tl, ltag)

    def weights(tag, wco):
        if tag == 0: return [("Spine", 1.0)]
        if tag == 1: return [("Head", 1.0)]
        if tag == 2:
            t_tip = smoothstep(0.38, 0.50, wco.z)
            return [("TailBase", 1.0 - t_tip), ("TailTip", t_tip)]
        bname = {3: "Leg_FL", 4: "Leg_FR", 5: "Leg_BL", 6: "Leg_BR"}[tag]
        t_top = smoothstep(0.17, 0.23, wco.z)
        return [(bname, 1.0 - t_top * 0.3), ("Spine", t_top * 0.3)]

    finish_resculpt(fbx_path, arm, orig, bm, tl, weights)


# 3. OW_Chicken.fbx (10 bones, facing -Y! Mats: 0=Feather, 1=Wing, 2=Tail, 3=Comb, 4=Beak, 5=Eye)
def upgrade_chicken():
    fbx_path, arm, orig = load_orig_rig("OW_Chicken.fbx")
    bm = bmesh.new()
    tl = bm.verts.layers.int.new("tag")
    FEATH, WING, TAIL, COMB, BEAK, EYE = 0, 1, 2, 3, 4, 5
    # Tags: 0=Spine, 1=Neck, 2=Head, 3=Wing_L, 4=Wing_R, 5=Tail, 6=Leg_L, 7=Leg_R

    # Plump feathered hen body
    body = [
        (Vector((0,  0.19, 0.40)), 0.095, 0.095),
        (Vector((0,  0.06, 0.37)), 0.148, 0.145),
        (Vector((0, -0.09, 0.36)), 0.142, 0.146),
        (Vector((0, -0.19, 0.42)), 0.096, 0.102),
    ]
    add_bm(bm, tube_bm(body, 18), FEATH, 2.6, tl, 0)
    # Feathered neck curving up gracefully
    neck = [
        (Vector((0, -0.16, 0.42)), 0.080, 0.082),
        (Vector((0, -0.20, 0.48)), 0.062, 0.064),
        (Vector((0, -0.23, 0.54)), 0.052, 0.054),
    ]
    add_bm(bm, tube_bm(neck, 14), FEATH, 2.8, tl, 1)

    # Sculpted chicken head, scalloped red comb, hanging double wattles, curved beak & glossy eyes!
    add_bm(bm, ellipsoid_bm((0, -0.245, 0.565), (0.054, 0.066, 0.056), 16, 10), FEATH, 3.0, tl, 2)
    # Scalloped 4-lobe red comb along crown
    for ci, (cy, cz, cr) in enumerate(((-0.265, 0.625, 0.022), (-0.240, 0.640, 0.026), (-0.215, 0.635, 0.024), (-0.195, 0.620, 0.019))):
        add_bm(bm, ellipsoid_bm((0, cy, cz), (0.012, cr * 0.85, cr * 1.15), 10, 8), COMB, 3.0, tl, 2)
    # Double red wattles under beak
    for sx in (-1.0, 1.0):
        add_bm(bm, ellipsoid_bm((sx * 0.016, -0.288, 0.515), (0.011, 0.014, 0.024), 8, 6), COMB, 3.0, tl, 2)
        add_bm(bm, ellipsoid_bm((sx * 0.045, -0.268, 0.576), (0.012, 0.013, 0.012), 10, 8), EYE, 2.0, tl, 2)
    # Curved tapered yellow chicken beak (upper + lower mandible)
    ub = [(Vector((0, -0.295, 0.564)), 0.020, 0.016), (Vector((0, -0.345, 0.556)), 0.013, 0.011), (Vector((0, -0.382, 0.544)), 0.005, 0.005)]
    lb = [(Vector((0, -0.295, 0.546)), 0.016, 0.011), (Vector((0, -0.348, 0.542)), 0.008, 0.007)]
    add_bm(bm, tube_bm(ub, 10), BEAK, 3.0, tl, 2)
    add_bm(bm, tube_bm(lb, 8), BEAK, 3.0, tl, 2)

    # Layered overlapping folded wing feather plumes!
    for wtag, sx in ((3, -1.0), (4, 1.0)):
        for fi, (dz, dy, s_len) in enumerate(((0.025, 0.00, 0.14), (0.00, 0.02, 0.155), (-0.025, 0.04, 0.13))):
            w_pts = [
                (Vector((sx * 0.125, -0.07 + dy * 0.5, 0.38 + dz)), 0.022, 0.032),
                (Vector((sx * 0.160,  0.02 + dy,       0.36 + dz)), 0.020, 0.036),
                (Vector((sx * 0.142,  0.02 + dy + s_len, 0.34 + dz)), 0.010, 0.022),
            ]
            add_bm(bm, tube_bm(w_pts, 8), WING, 2.8, tl, wtag)

    # Fanned 5-feather layered chicken tail!
    for ti in range(-2, 3):
        tx = ti * 0.024
        fan = 0.025 - abs(ti) * 0.008
        t_pts = [
            (Vector((tx * 0.5, 0.17, 0.40)), 0.018, 0.028),
            (Vector((tx * 1.1, 0.25, 0.47 + fan)), 0.016, 0.034),
            (Vector((tx * 1.4, 0.32, 0.53 + fan * 1.3)), 0.008, 0.022),
        ]
        add_bm(bm, tube_bm(t_pts, 8), TAIL, 2.8, tl, 5)

    # Feathered drumstick thighs + scaly legs + 3-toe claws!
    for ltag, lx in ((6, -0.072), (7, 0.072)):
        add_bm(bm, ellipsoid_bm((lx, 0.01, 0.24), (0.044, 0.052, 0.062), 10, 8), FEATH, 2.8, tl, ltag)
        l_pts = [(Vector((lx, 0.01, 0.22)), 0.016, 0.017), (Vector((lx, 0.02, 0.12)), 0.012, 0.013), (Vector((lx, 0.00, 0.018)), 0.013, 0.013)]
        add_bm(bm, tube_bm(l_pts, 8), BEAK, 3.0, tl, ltag)
        for ta in (-0.42, 0.0, 0.42):
            toe = [(Vector((lx, 0.00, 0.015)), 0.008, 0.008), (Vector((lx + math.sin(ta) * 0.058, -math.cos(ta) * 0.062, 0.010)), 0.005, 0.005)]
            add_bm(bm, tube_bm(toe, 6), BEAK, 3.0, tl, ltag)
        btoe = [(Vector((lx, 0.00, 0.016)), 0.008, 0.008), (Vector((lx, 0.036, 0.012)), 0.005, 0.005)]
        add_bm(bm, tube_bm(btoe, 6), BEAK, 3.0, tl, ltag)

    def weights(tag, wco):
        return [{0: ("Spine", 1.0), 1: ("Neck", 1.0), 2: ("Head", 1.0), 3: ("Wing_L", 1.0), 4: ("Wing_R", 1.0), 5: ("Tail", 1.0), 6: ("Leg_L", 1.0), 7: ("Leg_R", 1.0)}[tag]]

    finish_resculpt(fbx_path, arm, orig, bm, tl, weights)


# 4. OW_Pigeon.fbx (6 bones, facing +Y! Mats: 0=Slate, 1=WingBar, 2=Iridescent, 3=AmberEye, 4=CoralFoot)
def upgrade_pigeon():
    fbx_path, arm, orig = load_orig_rig("OW_Pigeon.fbx")
    bm = bmesh.new()
    tl = bm.verts.layers.int.new("tag")
    SLATE, BAR, IRID, EYE, FOOT = 0, 1, 2, 3, 4
    # Tags: 0=Body, 1=Head, 2=Wing_L, 3=Wing_R, 4=Leg_L, 5=Leg_R

    body = [
        (Vector((0, -0.18, 0.115)), 0.034, 0.016),
        (Vector((0, -0.08, 0.135)), 0.062, 0.056),
        (Vector((0,  0.03, 0.145)), 0.070, 0.068),
        (Vector((0,  0.09, 0.180)), 0.048, 0.046),
    ]
    add_bm(bm, tube_bm(body, 16), SLATE, 3.0, tl, 0)
    # Iridescent emerald-purple neck sheen collar
    add_bm(bm, ellipsoid_bm((0, 0.082, 0.195), (0.044, 0.042, 0.036), 12, 8), IRID, 3.0, tl, 1)
    # Sculpted small rounded pigeon head, cere-rimmed beak & glossy amber eyes!
    add_bm(bm, ellipsoid_bm((0, 0.100, 0.242), (0.034, 0.040, 0.034), 14, 10), SLATE, 3.0, tl, 1)
    beak = [(Vector((0, 0.134, 0.238)), 0.010, 0.009), (Vector((0, 0.172, 0.232)), 0.004, 0.004)]
    add_bm(bm, tube_bm(beak, 8), BAR, 3.0, tl, 1)
    for sx in (-1.0, 1.0):
        add_bm(bm, ellipsoid_bm((sx * 0.028, 0.114, 0.248), (0.008, 0.009, 0.008), 8, 6), EYE, 2.0, tl, 1)
    # Sculpted layered feathered wings w/ dark wing bars!
    for wtag, sx in ((2, -1.0), (3, 1.0)):
        w1 = [(Vector((sx * 0.056, 0.04, 0.155)), 0.018, 0.028), (Vector((sx * 0.082, -0.04, 0.142)), 0.016, 0.030), (Vector((sx * 0.048, -0.15, 0.122)), 0.008, 0.018)]
        add_bm(bm, tube_bm(w1, 8), SLATE, 3.0, tl, wtag)
        w2 = [(Vector((sx * 0.068, 0.00, 0.150)), 0.010, 0.026), (Vector((sx * 0.072, -0.09, 0.134)), 0.008, 0.022)]
        add_bm(bm, tube_bm(w2, 6), BAR, 3.0, tl, wtag)
    # Fanned tail wedge
    for ti in (-1, 0, 1):
        t_pts = [(Vector((ti * 0.016, -0.11, 0.125)), 0.016, 0.010), (Vector((ti * 0.026, -0.20, 0.105)), 0.014, 0.007)]
        add_bm(bm, tube_bm(t_pts, 6), BAR, 3.0, tl, 0)
    # Delicate coral-red legs & 3 toes
    for ltag, lx in ((4, -0.030), (5, 0.030)):
        add_bm(bm, tube_bm([(Vector((lx, 0.01, 0.092)), 0.008, 0.008), (Vector((lx, 0.01, 0.010)), 0.006, 0.006)], 6), FOOT, 3.0, tl, ltag)
        for ta in (-0.4, 0.0, 0.4):
            add_bm(bm, tube_bm([(Vector((lx, 0.01, 0.008)), 0.004, 0.004), (Vector((lx + math.sin(ta) * 0.032, 0.01 + math.cos(ta) * 0.034, 0.006)), 0.003, 0.003)], 5), FOOT, 3.0, tl, ltag)

    def weights(tag, wco):
        return [{0: ("Body", 1.0), 1: ("Head", 1.0), 2: ("Wing_L", 1.0), 3: ("Wing_R", 1.0), 4: ("Leg_L", 1.0), 5: ("Leg_R", 1.0)}[tag]]

    finish_resculpt(fbx_path, arm, orig, bm, tl, weights)


# 5. OW_PondDuck.fbx (7 bones, facing -Y! Mats: 0=White, 1=Wing, 2=Emerald, 3=Collar, 4=Bill, 5=Eye)
def upgrade_duck():
    fbx_path, arm, orig = load_orig_rig("OW_PondDuck.fbx")
    bm = bmesh.new()
    tl = bm.verts.layers.int.new("tag")
    WHITE, WING, EMER, COLLAR, BILL, EYE = 0, 1, 2, 3, 4, 5
    # Tags: 0=Body, 1=Neck, 2=Head, 3=Tail, 4=Wing_L, 5=Wing_R

    hull = [
        (Vector((0,  0.24, 0.175)), 0.088, 0.074),
        (Vector((0,  0.08, 0.155)), 0.152, 0.118),
        (Vector((0, -0.10, 0.155)), 0.148, 0.120),
        (Vector((0, -0.24, 0.195)), 0.102, 0.095),
    ]
    add_bm(bm, tube_bm(hull, 18), WHITE, 2.4, tl, 0)
    # Mallard white neck ring collar + emerald curved neck
    neck = [(Vector((0, -0.21, 0.215)), 0.072, 0.074), (Vector((0, -0.24, 0.285)), 0.056, 0.058), (Vector((0, -0.26, 0.355)), 0.054, 0.056)]
    add_bm(bm, tube_bm(neck, 14), EMER, 2.6, tl, 1)
    add_bm(bm, ellipsoid_bm((0, -0.228, 0.258), (0.064, 0.066, 0.018), 14, 8), COLLAR, 2.6, tl, 1)
    # Rounded emerald drake head, glossy eyes & broad flat sculpted duck bill!
    add_bm(bm, ellipsoid_bm((0, -0.280, 0.375), (0.058, 0.072, 0.058), 16, 10), EMER, 2.8, tl, 2)
    bill = [(Vector((0, -0.338, 0.362)), 0.036, 0.022), (Vector((0, -0.395, 0.352)), 0.034, 0.015), (Vector((0, -0.436, 0.348)), 0.024, 0.010)]
    add_bm(bm, tube_bm(bill, 12), BILL, 2.8, tl, 2)
    for sx in (-1.0, 1.0):
        add_bm(bm, ellipsoid_bm((sx * 0.048, -0.305, 0.390), (0.012, 0.013, 0.012), 10, 8), EYE, 2.0, tl, 2)
    # Upswept feathered duck tail curl
    t_pts = [(Vector((0, 0.22, 0.185)), 0.068, 0.042), (Vector((0, 0.34, 0.245)), 0.042, 0.024), (Vector((0, 0.40, 0.295)), 0.018, 0.012)]
    add_bm(bm, tube_bm(t_pts, 10), WING, 2.6, tl, 3)
    # Sculpted folded wings
    for wtag, sx in ((4, -1.0), (5, 1.0)):
        w_pts = [(Vector((sx * 0.138, -0.10, 0.185)), 0.028, 0.055), (Vector((sx * 0.172, 0.04, 0.185)), 0.026, 0.058), (Vector((sx * 0.132, 0.22, 0.195)), 0.014, 0.032)]
        add_bm(bm, tube_bm(w_pts, 10), WING, 2.4, tl, wtag)

    def weights(tag, wco):
        return [{0: ("Body", 1.0), 1: ("Neck", 1.0), 2: ("Head", 1.0), 3: ("Tail", 1.0), 4: ("Wing_L", 1.0), 5: ("Wing_R", 1.0)}[tag]]

    finish_resculpt(fbx_path, arm, orig, bm, tl, weights)


# 6. OW_LakeSwan.fbx (6 bones, facing +Y! Mats: 0=Plumage, 1=BlackMask, 2=OrangeBeak, 3=Pupil)
def upgrade_swan():
    fbx_path, arm, orig = load_orig_rig("OW_LakeSwan.fbx")
    bm = bmesh.new()
    tl = bm.verts.layers.int.new("tag")
    PLUM, MASK, BEAK, EYE = 0, 1, 2, 3
    # Tags: 0=Body, 1=NeckLower, 2=NeckUpper, 3=Head, 4=Wing_L, 5=Wing_R

    # Regal buoyant mute swan body + tapered graceful tail
    body = [
        (Vector((0, -0.52, 0.22)), 0.042, 0.024),
        (Vector((0, -0.32, 0.20)), 0.150, 0.115),
        (Vector((0, -0.04, 0.19)), 0.225, 0.165),
        (Vector((0,  0.22, 0.20)), 0.205, 0.160),
        (Vector((0,  0.34, 0.24)), 0.130, 0.118),
    ]
    add_bm(bm, tube_bm(body, 20), PLUM, 2.0, tl, 0)
    # Graceful double-curved S-neck (NeckLower -> NeckUpper)
    n_lo = [(Vector((0, 0.30, 0.24)), 0.095, 0.095), (Vector((0, 0.28, 0.39)), 0.064, 0.066), (Vector((0, 0.26, 0.54)), 0.054, 0.056)]
    add_bm(bm, tube_bm(n_lo, 14), PLUM, 2.2, tl, 1)
    n_hi = [(Vector((0, 0.26, 0.52)), 0.054, 0.056), (Vector((0, 0.28, 0.68)), 0.048, 0.050), (Vector((0, 0.32, 0.82)), 0.046, 0.048)]
    add_bm(bm, tube_bm(n_hi, 14), PLUM, 2.2, tl, 2)

    # Refined swan head, characteristic black basal knob & lore mask, orange curved bill & glossy eyes!
    add_bm(bm, ellipsoid_bm((0, 0.375, 0.825), (0.054, 0.082, 0.056), 16, 10), PLUM, 2.4, tl, 3)
    add_bm(bm, ellipsoid_bm((0, 0.445, 0.832), (0.042, 0.036, 0.036), 12, 8), MASK, 2.4, tl, 3)
    bill = [(Vector((0, 0.455, 0.818)), 0.034, 0.028), (Vector((0, 0.535, 0.800)), 0.028, 0.020), (Vector((0, 0.585, 0.785)), 0.018, 0.012)]
    add_bm(bm, tube_bm(bill, 12), BEAK, 2.6, tl, 3)
    for sx in (-1.0, 1.0):
        add_bm(bm, ellipsoid_bm((sx * 0.044, 0.415, 0.840), (0.012, 0.013, 0.012), 10, 8), EYE, 2.0, tl, 3)

    # Iconic arched cupped swan wing plumes (layered feathers!)
    for wtag, sx in ((4, -1.0), (5, 1.0)):
        for li, (dx, dz, len_b) in enumerate(((0.0, 0.02, -0.34), (0.02, 0.07, -0.28), (-0.01, -0.02, -0.40))):
            w_pts = [
                (Vector((sx * (0.16 + dx),  0.12, 0.27 + dz)), 0.032, 0.068),
                (Vector((sx * (0.24 + dx), -0.08, 0.34 + dz)), 0.030, 0.082),
                (Vector((sx * (0.21 + dx),  len_b, 0.31 + dz)), 0.018, 0.052),
            ]
            add_bm(bm, tube_bm(w_pts, 10), PLUM, 2.0, tl, wtag)

    def weights(tag, wco):
        if tag == 1:
            t = smoothstep(0.46, 0.56, wco.z)
            return [("NeckLower", 1.0 - t * 0.5), ("NeckUpper", t * 0.5)]
        if tag == 2:
            t = smoothstep(0.76, 0.84, wco.z)
            return [("NeckUpper", 1.0 - t * 0.5), ("Head", t * 0.5)]
        return [{0: ("Body", 1.0), 3: ("Head", 1.0), 4: ("Wing_L", 1.0), 5: ("Wing_R", 1.0)}[tag]]

    finish_resculpt(fbx_path, arm, orig, bm, tl, weights)


if __name__ == "__main__":
    print("[GRIDROAM Phase 4] Upgrading ALL 10 remaining rigged animals (preserving all bones & clips)...")
    # Group A: Multi-bone anatomical rigs (Catmull-Clark smooth + UVs)
    upgrade_group_a_subsurf("OW_Cow.fbx", uv_scale=1.6, subsurf_lvl=1)
    upgrade_group_a_subsurf("OW_StreetDog.fbx", uv_scale=0.9, subsurf_lvl=1)
    upgrade_group_a_subsurf("OW_ShepherdDog.fbx", uv_scale=1.6, subsurf_lvl=1)
    upgrade_group_a_subsurf("OW_LakeFish.fbx", uv_scale=3.2, subsurf_lvl=1)
    # Group B: Full anatomical stylized-realistic re-sculpts
    upgrade_goat()
    upgrade_cat()
    upgrade_chicken()
    upgrade_pigeon()
    upgrade_duck()
    upgrade_swan()
    print("[GRIDROAM Phase 4] All 10 remaining animals upgraded cleanly!")
