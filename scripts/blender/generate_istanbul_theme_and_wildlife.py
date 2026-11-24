#!/usr/bin/env python3
"""
Our World — Istanbul Visual Theme, CC0 Refined Assets & Rigged Wildlife Pack (Blender 5.3.0)
Generates 15 commercial-safe FBX assets into Assets/GRIDROAM/Resources/OurWorldLife/:

A. Poly Haven CC0 3D Models (refined, decimated for mobile & material-normalized in Blender 5.3.0):
  1. OW_ParkBench.fbx           <- Poly Haven CC0 painted_wooden_bench
  2. OW_IstanbulLantern.fbx     <- Poly Haven CC0 Lantern_01

B. Quaternius CC0 Rigged Animals (re-sculpted, re-colored & multi-clip animated in Blender 5.3.0):
  3. OW_StreetDog.fbx           <- Quaternius CC0 Husky -> Friendly Golden Istanbul Street Dog (6 clips)
  4. OW_FarmGoat.fbx            <- Quaternius CC0 Sheep -> Horned Anatolian Farm Goat (4 clips)

C. Original Blender-Sculpted & Skeletal-Rigged Wildlife (multi-clip NLA baked FBX):
  5. OW_StreetCat.fbx           <- Fluffy Istanbul Street Cat (Idle, Walk, Sit, Happy)
  6. OW_Pigeon.fbx              <- Istanbul Plaza Pigeon (Peck, Walk, Fly)
  7. OW_LakeSwan.fbx            <- Graceful Sultanbeyli Lake Swan (Swim, WingSpread)

D. Original Blender-Sculpted Istanbul Mahalle Theme & Sports Hero Props:
  8.  OW_SimitCart.fbx          <- Classic Crimson & Brass Istanbul Simit Cart
  9.  OW_TeaGardenSet.fbx       <- Turkish Cay Bahcesi Table, 2 Chairs, Umbrella & Tulip Tea Glasses
  10. OW_IstanbulFountain.fbx   <- Carved Limestone Mahalle Cesmesi with Iznik Tile & Brass Taps
  11. OW_PlaneTree.fbx          <- Multi-Branch Sculpted Istanbul Cinar (Plane Tree)
  12. OW_CypressTree.fbx        <- Tall Sculpted Mediterranean Selvi (Cypress Tree)
  13. OW_ShopAwning.fbx         <- Striped Turkish Shop Awning (Tente) & Brass Lamp
  14. OW_FlowerPlanter.fbx      <- Travertine Planter with Tulips & Lavender
  15. OW_DivingBoard.fbx        <- Olympic Pool Springboard Diving Stand
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldLife")
BLEND_DIR = os.path.join(SCRIPT_DIR, "blend_sources")
SCRATCH = "/Users/dogusipeksac/.gemini/antigravity/brain/2dd72233-235e-4c47-8c3c-78844f1a7f7f/scratch"
QUAT_DIR = os.path.join(SCRATCH, "cc0_quaternius")
PH_DIR = os.path.join(SCRATCH, "cc0_polyhaven")

os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(BLEND_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 24


def mat(name, rgb, rough=0.62, metal=0.0, emission=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name=name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = metal
        if emission > 0.0 and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
            bsdf.inputs["Emission Strength"].default_value = emission
    return m


def palette():
    return {
        "crimson": mat("IstanbulCrimson", (0.76, 0.14, 0.14), rough=0.42),
        "terracotta": mat("TerracottaWarm", (0.78, 0.34, 0.22), rough=0.65),
        "cream": mat("IstanbulCream", (0.94, 0.90, 0.80), rough=0.70),
        "limestone": mat("LimestoneCarved", (0.86, 0.82, 0.72), rough=0.76),
        "marble": mat("MarbleLight", (0.93, 0.92, 0.89), rough=0.35),
        "iznik_teal": mat("IznikTurquoise", (0.10, 0.62, 0.66), rough=0.28),
        "iznik_blue": mat("IznikCobalt", (0.12, 0.28, 0.68), rough=0.30),
        "brass": mat("PolishedBrass", (0.88, 0.68, 0.24), rough=0.26, metal=0.85),
        "copper_patina": mat("CopperPatina", (0.26, 0.56, 0.48), rough=0.48, metal=0.55),
        "iron": mat("WroughtIron", (0.14, 0.16, 0.19), rough=0.45, metal=0.75),
        "chrome": mat("BrushedChrome", (0.84, 0.87, 0.90), rough=0.20, metal=0.92),
        "wood_warm": mat("WarmOakWood", (0.50, 0.31, 0.17), rough=0.68),
        "wood_dark": mat("DarkWalnut", (0.28, 0.17, 0.10), rough=0.72),
        "bark": mat("TreeBark", (0.34, 0.24, 0.16), rough=0.88),
        "leaf_deep": mat("LeafDeepOlive", (0.16, 0.42, 0.18), rough=0.76),
        "leaf_mid": mat("LeafMidEmerald", (0.24, 0.54, 0.22), rough=0.72),
        "leaf_sun": mat("LeafSunlitGreen", (0.38, 0.66, 0.26), rough=0.68),
        "cypress_dark": mat("CypressDark", (0.10, 0.30, 0.16), rough=0.78),
        "cypress_mid": mat("CypressMid", (0.16, 0.40, 0.21), rough=0.74),
        "simit_gold": mat("SimitSesameGold", (0.84, 0.55, 0.18), rough=0.72),
        "tea_amber": mat("TurkishTeaAmber", (0.78, 0.22, 0.06), rough=0.14, emission=0.25),
        "glass": mat("GlassClear", (0.76, 0.90, 0.96), rough=0.08, metal=0.05),
        "lamp_glow": mat("WarmLanternGlow", (1.0, 0.84, 0.44), rough=0.20, emission=2.6),
        "soil": mat("RichSoil", (0.22, 0.15, 0.10), rough=0.92),
        "tulip_red": mat("TulipRed", (0.88, 0.16, 0.18), rough=0.52),
        "tulip_gold": mat("TulipGold", (0.96, 0.76, 0.14), rough=0.52),
        "lavender": mat("LavenderPurple", (0.56, 0.34, 0.78), rough=0.62),
        "aqua_board": mat("DivingBoardAqua", (0.16, 0.62, 0.86), rough=0.38),
        "white_board": mat("DivingBoardWhite", (0.94, 0.96, 0.98), rough=0.35),
        # Animals
        "cat_fur": mat("CatFurPrimary", (0.88, 0.56, 0.24), rough=0.76),
        "cat_belly": mat("CatFurBelly", (0.96, 0.92, 0.84), rough=0.78),
        "cat_stripe": mat("CatFurAccent", (0.58, 0.32, 0.12), rough=0.78),
        "cat_nose": mat("CatPinkNose", (0.92, 0.58, 0.62), rough=0.45),
        "cat_eye": mat("CatAmberEye", (0.96, 0.74, 0.16), rough=0.15),
        "pupil": mat("AnimalPupil", (0.04, 0.04, 0.05), rough=0.10),
        "pigeon_body": mat("PigeonSlate", (0.56, 0.60, 0.66), rough=0.72),
        "pigeon_neck": mat("PigeonIridescent", (0.16, 0.52, 0.44), rough=0.38, metal=0.22),
        "pigeon_wing": mat("PigeonWingBar", (0.28, 0.31, 0.36), rough=0.70),
        "pigeon_foot": mat("PigeonCoralFoot", (0.82, 0.36, 0.32), rough=0.55),
        "swan_white": mat("SwanPlumage", (0.97, 0.97, 0.96), rough=0.58),
        "swan_beak": mat("SwanOrangeBeak", (0.94, 0.46, 0.10), rough=0.35),
        "swan_mask": mat("SwanBlackMask", (0.08, 0.08, 0.09), rough=0.30),
        "goat_coat": mat("GoatCoatCream", (0.90, 0.84, 0.72), rough=0.80),
        "goat_brown": mat("GoatCoatBrown", (0.44, 0.30, 0.20), rough=0.80),
        "horn": mat("AnimalHorn", (0.34, 0.28, 0.22), rough=0.52),
        "hoof": mat("AnimalHoof", (0.16, 0.14, 0.13), rough=0.48),
    }


def build_parts_mesh(name, specs, pivot=(0.0, 0.0, 0.0), vertex_groups_by_part=False):
    """Build a multi-material mesh with optional per-part vertex groups for skeletal rigging."""
    bm = bmesh.new()
    materials = []
    px, py, pz = pivot
    part_vert_indices = {}

    def mat_idx(m):
        if m not in materials:
            materials.append(m)
        return materials.index(m)

    for sp in specs:
        kind = sp["kind"]
        lx, ly, lz = sp.get("loc", (0.0, 0.0, 0.0))
        rx, ry, rz = sp.get("rot", (0.0, 0.0, 0.0))
        m_idx = mat_idx(sp["mat"])
        tmp = bmesh.new()

        if kind == "sphere":
            sx, sy, sz = sp.get("scale", (1.0, 1.0, 1.0))
            segs = sp.get("segs", 12)
            rings = sp.get("rings", 8)
            bmesh.ops.create_uvsphere(tmp, u_segments=segs, v_segments=rings, radius=1.0)
            bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)
        elif kind == "ico":
            sx, sy, sz = sp.get("scale", (1.0, 1.0, 1.0))
            sub = sp.get("subdiv", 2)
            bmesh.ops.create_icosphere(tmp, subdivisions=sub, radius=1.0)
            # Subtle organic lumpiness for foliage canopies
            lump = sp.get("lump", 0.0)
            if lump > 0.0:
                for v in tmp.verts:
                    n = math.sin(v.co.x * 4.3 + lx) * math.cos(v.co.y * 4.7 + ly) * math.sin(v.co.z * 3.9 + lz)
                    v.co *= (1.0 + n * lump)
            bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)
        elif kind == "box":
            sx, sy, sz = sp.get("scale", (1.0, 1.0, 1.0))
            bmesh.ops.create_cube(tmp, size=1.0)
            bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)
            bev = sp.get("bevel", 0.0)
            if bev > 0.0:
                bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bev, segments=2, affect="EDGES", clamp_overlap=True)
        elif kind == "cyl":
            r1 = sp.get("r1", 0.5)
            r2 = sp.get("r2", r1)
            depth = sp.get("depth", 1.0)
            segs = sp.get("segs", 12)
            bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
        elif kind == "torus":
            major = sp.get("major", 0.5)
            minor = sp.get("minor", 0.12)
            maj_s = sp.get("maj_segs", 16)
            min_s = sp.get("min_segs", 8)
            rings_v = []
            for i in range(maj_s):
                a = 2.0 * math.pi * i / maj_s
                ca, sa = math.cos(a), math.sin(a)
                ring = []
                for j in range(min_s):
                    b = 2.0 * math.pi * j / min_s
                    r = major + minor * math.cos(b)
                    ring.append(tmp.verts.new((r * ca, r * sa, minor * math.sin(b))))
                rings_v.append(ring)
            for i in range(maj_s):
                ni = (i + 1) % maj_s
                for j in range(min_s):
                    nj = (j + 1) % min_s
                    tmp.faces.new((rings_v[i][j], rings_v[ni][j], rings_v[ni][nj], rings_v[i][nj]))

        rot_mat = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), "XYZ").to_matrix().to_4x4()
        trans_mat = Matrix.Translation((lx - px, ly - py, lz - pz))
        bmesh.ops.transform(tmp, matrix=trans_mat @ rot_mat, verts=tmp.verts)

        vert_map = {}
        new_verts = []
        for v in tmp.verts:
            nv = bm.verts.new(v.co)
            vert_map[v.index] = nv
            new_verts.append(nv)
        for f in tmp.faces:
            try:
                nf = bm.faces.new([vert_map[v.index] for v in f.verts])
                nf.material_index = m_idx
                nf.smooth = (kind in ("sphere", "cyl", "ico", "torus"))
            except ValueError:
                pass

        if vertex_groups_by_part and "bone" in sp:
            bm.verts.ensure_lookup_table()
            bn = sp["bone"]
            part_vert_indices.setdefault(bn, []).extend([nv for nv in new_verts])

        tmp.free()

    bm.verts.index_update()
    vg_indices = {}
    if vertex_groups_by_part:
        for bn, vlist in part_vert_indices.items():
            vg_indices[bn] = [v.index for v in vlist]

    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    for m in materials:
        mesh.materials.append(m)

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = Vector((px, py, pz))

    if vertex_groups_by_part:
        for bn, idxs in vg_indices.items():
            vg = obj.vertex_groups.new(name=bn)
            vg.add(idxs, 1.0, "REPLACE")

    return obj


def export_static_fbx(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    out_path = os.path.join(OUT_DIR, filename)
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
        bake_anim=False,
        path_mode="COPY",
        embed_textures=False,
    )
    kb = os.path.getsize(out_path) // 1024
    print(f"  [FBX Static] {filename} ({kb} KB)")


def export_rigged_fbx(arm_obj, mesh_objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    arm_obj.select_set(True)
    for m in mesh_objs:
        m.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    out_path = os.path.join(OUT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.5,
        path_mode="COPY",
        embed_textures=False,
    )
    kb = os.path.getsize(out_path) // 1024
    print(f"  [FBX Rigged] {filename} ({kb} KB)")


def create_armature_and_bind(arm_name, bone_defs, mesh_obj):
    """
    bone_defs: list of (name, parent_name_or_None, head_xyz, tail_xyz)
    """
    arm_data = bpy.data.armatures.new(arm_name + "_Data")
    arm_obj = bpy.data.objects.new(arm_name, arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")

    created = {}
    for bname, pname, head, tail in bone_defs:
        eb = arm_data.edit_bones.new(bname)
        eb.head = Vector(head)
        eb.tail = Vector(tail)
        if pname and pname in created:
            eb.parent = created[pname]
        created[bname] = eb

    bpy.ops.object.mode_set(mode="OBJECT")

    mesh_obj.parent = arm_obj
    mod = mesh_obj.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = arm_obj
    return arm_obj


def add_nla_clip(arm_obj, clip_name, frame_end, keyframes_by_bone):
    """
    keyframes_by_bone: dict[bone_name] -> list of (frame, rot_deg_xyz_or_None, loc_xyz_or_None)
    Blender 5.3 compatible Action + NLA strip authoring!
    """
    if arm_obj.animation_data is None:
        arm_obj.animation_data_create()
    act = bpy.data.actions.new(name=clip_name)
    arm_obj.animation_data.action = act

    for pb in arm_obj.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)

    for bname, keys in keyframes_by_bone.items():
        pb = arm_obj.pose.bones.get(bname)
        if pb is None:
            continue
        pb.rotation_mode = "XYZ"
        for frame, rot_deg, loc in keys:
            if rot_deg is not None:
                pb.rotation_euler = Euler((math.radians(rot_deg[0]), math.radians(rot_deg[1]), math.radians(rot_deg[2])), "XYZ")
                pb.keyframe_insert(data_path="rotation_euler", frame=frame)
            if loc is not None:
                pb.location = Vector(loc)
                pb.keyframe_insert(data_path="location", frame=frame)

    track = arm_obj.animation_data.nla_tracks.new()
    track.name = clip_name
    strip = track.strips.new(clip_name, 1, act)
    strip.action_frame_start = 1
    strip.action_frame_end = frame_end
    arm_obj.animation_data.action = None


# =============================================================================
# A. POLY HAVEN CC0 3D MODELS (IMPORT, DECIMATE FOR MOBILE, GROUND, EXPORT)
# =============================================================================
def build_cc0_polyhaven_bench():
    clear_scene()
    P = palette()
    gltf_path = os.path.join(PH_DIR, "painted_wooden_bench", "painted_wooden_bench_1k.gltf")
    if not os.path.exists(gltf_path):
        print("  SKIP OW_ParkBench (missing Poly Haven gltf)")
        return
    bpy.ops.import_scene.gltf(filepath=gltf_path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        return
    # Join into one clean mobile-ready mesh
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = "OW_ParkBench"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Decimate to crisp mobile budget (~1200 tris)
    poly_cnt = len(obj.data.polygons)
    if poly_cnt > 1200:
        dec = obj.modifiers.new("MobileDecimate", "DECIMATE")
        dec.ratio = max(0.08, 1200.0 / poly_cnt)
        bpy.ops.object.modifier_apply(modifier=dec.name)

    # Normalize width to 1.80 m & ground Z = 0
    bbox = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs = [v.x for v in bbox]
    ys = [v.y for v in bbox]
    zs = [v.z for v in bbox]
    width = max(xs) - min(xs)
    depth = max(ys) - min(ys)
    # Orient long axis along X
    if depth > width:
        obj.rotation_euler = Euler((0.0, 0.0, math.radians(90.0)), "XYZ")
        bpy.ops.object.transform_apply(rotation=True)
        bbox = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        xs, ys, zs = [v.x for v in bbox], [v.y for v in bbox], [v.z for v in bbox]
        width = max(xs) - min(xs)
    if width > 0.01:
        s = 1.80 / width
        obj.scale = (s, s, s)
        bpy.ops.object.transform_apply(scale=True)
        bbox = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        xs, ys, zs = [v.x for v in bbox], [v.y for v in bbox], [v.z for v in bbox]
    cx, cy, minz = (min(xs) + max(xs)) * 0.5, (min(ys) + max(ys)) * 0.5, min(zs)
    obj.location = Vector((-cx, -cy, -minz))
    bpy.ops.object.transform_apply(location=True)

    # Replace image-dependent gltf materials with clean stylized Istanbul park green-wood + iron URP-safe materials
    obj.data.materials.clear()
    obj.data.materials.append(mat("IstanbulBenchTealWood", (0.18, 0.44, 0.40), rough=0.62))
    export_static_fbx([obj], "OW_ParkBench.fbx")


def build_cc0_polyhaven_lantern():
    clear_scene()
    P = palette()
    gltf_path = os.path.join(PH_DIR, "Lantern_01", "Lantern_01_1k.gltf")
    if not os.path.exists(gltf_path):
        print("  SKIP OW_IstanbulLantern (missing Poly Haven gltf)")
        return
    bpy.ops.import_scene.gltf(filepath=gltf_path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        return
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = "OW_IstanbulLantern"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    poly_cnt = len(obj.data.polygons)
    if poly_cnt > 900:
        dec = obj.modifiers.new("MobileDecimate", "DECIMATE")
        dec.ratio = max(0.06, 900.0 / poly_cnt)
        bpy.ops.object.modifier_apply(modifier=dec.name)

    bbox = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs, ys, zs = [v.x for v in bbox], [v.y for v in bbox], [v.z for v in bbox]
    h = max(zs) - min(zs)
    if h > 0.01:
        s = 0.52 / h
        obj.scale = (s, s, s)
        bpy.ops.object.transform_apply(scale=True)
        bbox = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        xs, ys, zs = [v.x for v in bbox], [v.y for v in bbox], [v.z for v in bbox]
    cx, cy, minz = (min(xs) + max(xs)) * 0.5, (min(ys) + max(ys)) * 0.5, min(zs)
    obj.location = Vector((-cx, -cy, -minz))
    bpy.ops.object.transform_apply(location=True)

    # Clean warm brass housing + glowing lantern glass core
    obj.data.materials.clear()
    obj.data.materials.append(P["brass"])
    glow = build_parts_mesh("LanternCore", [
        {"kind": "cyl", "loc": (0.0, 0.0, 0.24), "r1": 0.075, "r2": 0.085, "depth": 0.20, "mat": P["lamp_glow"], "segs": 10},
    ])
    export_static_fbx([obj, glow], "OW_IstanbulLantern.fbx")


# =============================================================================
# B. QUATERNIUS CC0 RIGGED ANIMALS (CUSTOMIZED IN BLENDER 5.3.0)
# =============================================================================
def build_cc0_street_dog():
    clear_scene()
    src = os.path.join(QUAT_DIR, "Quat_Husky.fbx")
    if not os.path.exists(src):
        print("  SKIP OW_StreetDog (missing Quat_Husky.fbx)")
        return
    bpy.ops.import_scene.fbx(filepath=src)
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not arms or not meshes:
        return
    arm = arms[0]
    # True friendly Istanbul street dog scale (~0.56 m shoulder height — matches OW_ShepherdDog!)
    arm.scale = (0.14, 0.14, 0.14)
    # Warm honey-gold coat with cream belly across all Quaternius material slot names!
    for m in meshes:
        for slot in m.material_slots:
            if slot.material is None:
                continue
            n = slot.material.name.lower()
            bsdf = slot.material.node_tree.nodes.get("Principled BSDF") if slot.material.use_nodes else None
            if not bsdf:
                continue
            if "eye" in n:
                bsdf.inputs["Base Color"].default_value = (0.12, 0.08, 0.05, 1.0)
            elif "nose" in n or "claw" in n:
                bsdf.inputs["Base Color"].default_value = (0.10, 0.07, 0.06, 1.0)
            elif "white" in n or "light" in n or "belly" in n or "cream" in n:
                bsdf.inputs["Base Color"].default_value = (0.96, 0.90, 0.78, 1.0)
            else:
                bsdf.inputs["Base Color"].default_value = (0.85, 0.58, 0.24, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.72

    # Push all imported actions onto NLA strips so Unity receives every clip cleanly
    if arm.animation_data is None:
        arm.animation_data_create()
    for act in list(bpy.data.actions):
        low = act.name.lower()
        if any(k in low for k in ("idle", "walk", "gallop", "eat", "happy", "run")):
            clean_name = act.name.split("|")[-1]
            tr = arm.animation_data.nla_tracks.new()
            tr.name = clean_name
            st = tr.strips.new(clean_name, 1, act)
            st.action_frame_start = act.frame_range[0]
            st.action_frame_end = act.frame_range[1]
    arm.animation_data.action = None
    export_rigged_fbx(arm, meshes, "OW_StreetDog.fbx")


def build_rigged_farm_goat():
    """Original sculpted & skeletal-rigged horned Anatolian Farm Goat (4 smooth looping clips!)."""
    clear_scene()
    P = palette()
    specs = [
        # Torso & Haunches
        {"kind": "sphere", "loc": (0.0, 0.0, 0.62), "scale": (0.24, 0.44, 0.25), "mat": P["goat_coat"], "bone": "Spine"},
        {"kind": "sphere", "loc": (0.0, -0.24, 0.64), "scale": (0.22, 0.22, 0.23), "mat": P["goat_brown"], "bone": "Spine"},
        {"kind": "sphere", "loc": (0.0, 0.24, 0.65), "scale": (0.21, 0.22, 0.22), "mat": P["goat_coat"], "bone": "Spine"},
        # Neck & Head + Goat Beard + Swept Horns
        {"kind": "cyl", "loc": (0.0, 0.40, 0.80), "r1": 0.12, "r2": 0.095, "depth": 0.34, "rot": (-28, 0, 0), "mat": P["goat_coat"], "bone": "Neck"},
        {"kind": "sphere", "loc": (0.0, 0.54, 0.96), "scale": (0.12, 0.19, 0.12), "rot": (14, 0, 0), "mat": P["goat_coat"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.0, 0.68, 0.92), "scale": (0.075, 0.10, 0.075), "mat": P["goat_brown"], "bone": "Head"},
        # Goat chin beard!
        {"kind": "cyl", "loc": (0.0, 0.62, 0.82), "r1": 0.015, "r2": 0.045, "depth": 0.14, "rot": (12, 0, 0), "mat": P["goat_coat"], "bone": "Head"},
        # Ears
        {"kind": "sphere", "loc": (-0.14, 0.50, 0.99), "scale": (0.09, 0.04, 0.03), "rot": (0, 22, -15), "mat": P["goat_brown"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.14, 0.50, 0.99), "scale": (0.09, 0.04, 0.03), "rot": (0, -22, 15), "mat": P["goat_brown"], "bone": "Head"},
        # Swept curved horns!
        {"kind": "cyl", "loc": (-0.07, 0.49, 1.12), "r1": 0.032, "r2": 0.014, "depth": 0.24, "rot": (32, -18, 0), "mat": P["horn"], "bone": "Head"},
        {"kind": "cyl", "loc": (0.07, 0.49, 1.12), "r1": 0.032, "r2": 0.014, "depth": 0.24, "rot": (32, 18, 0), "mat": P["horn"], "bone": "Head"},
        # Eyes
        {"kind": "sphere", "loc": (-0.095, 0.59, 0.98), "scale": (0.024, 0.024, 0.024), "mat": P["pupil"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.095, 0.59, 0.98), "scale": (0.024, 0.024, 0.024), "mat": P["pupil"], "bone": "Head"},
        # Perky short tail
        {"kind": "cyl", "loc": (0.0, -0.45, 0.72), "r1": 0.045, "r2": 0.02, "depth": 0.16, "rot": (-42, 0, 0), "mat": P["goat_brown"], "bone": "Tail"},
        # 4 Agile Legs + Hooves
        {"kind": "cyl", "loc": (-0.14, 0.26, 0.26), "r1": 0.038, "r2": 0.052, "depth": 0.50, "mat": P["goat_coat"], "bone": "Leg_FL"},
        {"kind": "cyl", "loc": (-0.14, 0.26, 0.03), "r1": 0.042, "r2": 0.038, "depth": 0.06, "mat": P["hoof"], "bone": "Leg_FL"},
        {"kind": "cyl", "loc": (0.14, 0.26, 0.26), "r1": 0.038, "r2": 0.052, "depth": 0.50, "mat": P["goat_coat"], "bone": "Leg_FR"},
        {"kind": "cyl", "loc": (0.14, 0.26, 0.03), "r1": 0.042, "r2": 0.038, "depth": 0.06, "mat": P["hoof"], "bone": "Leg_FR"},
        {"kind": "cyl", "loc": (-0.15, -0.26, 0.26), "r1": 0.040, "r2": 0.058, "depth": 0.50, "mat": P["goat_brown"], "bone": "Leg_BL"},
        {"kind": "cyl", "loc": (-0.15, -0.26, 0.03), "r1": 0.042, "r2": 0.038, "depth": 0.06, "mat": P["hoof"], "bone": "Leg_BL"},
        {"kind": "cyl", "loc": (0.15, -0.26, 0.26), "r1": 0.040, "r2": 0.058, "depth": 0.50, "mat": P["goat_brown"], "bone": "Leg_BR"},
        {"kind": "cyl", "loc": (0.15, -0.26, 0.03), "r1": 0.042, "r2": 0.038, "depth": 0.06, "mat": P["hoof"], "bone": "Leg_BR"},
    ]
    mesh = build_parts_mesh("OW_FarmGoat_MeshObj", specs, vertex_groups_by_part=True)
    bones = [
        ("Spine", None, (0.0, 0.0, 0.62), (0.0, 0.25, 0.65)),
        ("Neck", "Spine", (0.0, 0.34, 0.72), (0.0, 0.48, 0.90)),
        ("Head", "Neck", (0.0, 0.48, 0.90), (0.0, 0.66, 0.94)),
        ("Tail", "Spine", (0.0, -0.40, 0.68), (0.0, -0.52, 0.78)),
        ("Leg_FL", "Spine", (-0.14, 0.26, 0.50), (-0.14, 0.26, 0.02)),
        ("Leg_FR", "Spine", (0.14, 0.26, 0.50), (0.14, 0.26, 0.02)),
        ("Leg_BL", "Spine", (-0.15, -0.26, 0.50), (-0.15, -0.26, 0.02)),
        ("Leg_BR", "Spine", (0.15, -0.26, 0.50), (0.15, -0.26, 0.02)),
    ]
    arm = create_armature_and_bind("GoatArmature", bones, mesh)

    add_nla_clip(arm, "Idle", 48, {
        "Spine": [(1, (0, 0, 0), (0, 0, 0)), (24, (1.5, 0, 0), (0, 0, 0.015)), (48, (0, 0, 0), (0, 0, 0))],
        "Head": [(1, (0, 0, 0), None), (16, (-4, 0, 12), None), (34, (4, 0, -10), None), (48, (0, 0, 0), None)],
        "Tail": [(1, (0, 0, -8), None), (12, (0, 0, 14), None), (24, (0, 0, -10), None), (36, (0, 0, 14), None), (48, (0, 0, -8), None)],
    })
    add_nla_clip(arm, "Walk", 24, {
        "Spine": [(1, (0, 0, 0), (0, 0, 0)), (6, (2, 0, 2), (0, 0, 0.03)), (12, (0, 0, 0), (0, 0, 0)), (18, (2, 0, -2), (0, 0, 0.03)), (24, (0, 0, 0), (0, 0, 0))],
        "Leg_FL": [(1, (22, 0, 0), None), (12, (-22, 0, 0), None), (24, (22, 0, 0), None)],
        "Leg_BR": [(1, (22, 0, 0), None), (12, (-22, 0, 0), None), (24, (22, 0, 0), None)],
        "Leg_FR": [(1, (-22, 0, 0), None), (12, (22, 0, 0), None), (24, (-22, 0, 0), None)],
        "Leg_BL": [(1, (-22, 0, 0), None), (12, (22, 0, 0), None), (24, (-22, 0, 0), None)],
        "Head": [(1, (4, 0, 0), None), (12, (-4, 0, 0), None), (24, (4, 0, 0), None)],
    })
    add_nla_clip(arm, "Eating", 36, {
        "Neck": [(1, (54, 0, 0), None), (18, (62, 0, 0), None), (36, (54, 0, 0), None)],
        "Head": [(1, (-18, 0, 0), None), (9, (-6, 0, 6), None), (18, (-20, 0, 0), None), (27, (-6, 0, -6), None), (36, (-18, 0, 0), None)],
        "Tail": [(1, (0, 0, -14), None), (18, (0, 0, 14), None), (36, (0, 0, -14), None)],
    })
    add_nla_clip(arm, "Happy", 24, {
        "Spine": [(1, (0, 0, 0), (0, 0, 0)), (8, (-14, 0, 0), (0, 0, 0.22)), (16, (6, 0, 0), (0, 0, 0.04)), (24, (0, 0, 0), (0, 0, 0))],
        "Leg_FL": [(1, (0, 0, 0), None), (8, (-38, 0, 0), None), (24, (0, 0, 0), None)],
        "Leg_FR": [(1, (0, 0, 0), None), (8, (-38, 0, 0), None), (24, (0, 0, 0), None)],
        "Tail": [(1, (0, 0, -24), None), (12, (0, 0, 24), None), (24, (0, 0, -24), None)],
    })
    export_rigged_fbx(arm, [mesh], "OW_FarmGoat.fbx")


# =============================================================================
# C. ORIGINAL BLENDER-SCULPTED & SKELETAL-RIGGED WILDLIFE
# =============================================================================
def build_rigged_street_cat():
    clear_scene()
    P = palette()
    specs = [
        # Torso & Fluffy Chest
        {"kind": "sphere", "loc": (0.0, 0.0, 0.24), "scale": (0.115, 0.23, 0.115), "mat": P["cat_fur"], "bone": "Spine"},
        {"kind": "sphere", "loc": (0.0, 0.14, 0.23), "scale": (0.095, 0.11, 0.10), "mat": P["cat_belly"], "bone": "Spine"},
        {"kind": "sphere", "loc": (0.0, -0.02, 0.34), "scale": (0.085, 0.12, 0.025), "mat": P["cat_stripe"], "bone": "Spine"},
        # Head, Cheeks, Muzzle, Pointed Ears, Expressive Amber Eyes
        {"kind": "sphere", "loc": (0.0, 0.27, 0.36), "scale": (0.105, 0.105, 0.098), "mat": P["cat_fur"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.0, 0.35, 0.33), "scale": (0.062, 0.055, 0.048), "mat": P["cat_belly"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.0, 0.395, 0.345), "scale": (0.022, 0.018, 0.016), "mat": P["cat_nose"], "bone": "Head"},
        {"kind": "cyl", "loc": (-0.068, 0.26, 0.47), "r1": 0.042, "r2": 0.008, "depth": 0.085, "rot": (8, -16, 0), "mat": P["cat_fur"], "bone": "Head", "segs": 8},
        {"kind": "cyl", "loc": (0.068, 0.26, 0.47), "r1": 0.042, "r2": 0.008, "depth": 0.085, "rot": (8, 16, 0), "mat": P["cat_fur"], "bone": "Head", "segs": 8},
        {"kind": "sphere", "loc": (-0.052, 0.355, 0.375), "scale": (0.024, 0.020, 0.024), "mat": P["cat_eye"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.052, 0.355, 0.375), "scale": (0.024, 0.020, 0.024), "mat": P["cat_eye"], "bone": "Head"},
        {"kind": "sphere", "loc": (-0.052, 0.372, 0.375), "scale": (0.009, 0.010, 0.018), "mat": P["pupil"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.052, 0.372, 0.375), "scale": (0.009, 0.010, 0.018), "mat": P["pupil"], "bone": "Head"},
        # Expressive 2-segment curved upright tail!
        {"kind": "cyl", "loc": (0.0, -0.25, 0.34), "r1": 0.034, "r2": 0.028, "depth": 0.20, "rot": (-34, 0, 0), "mat": P["cat_fur"], "bone": "TailBase"},
        {"kind": "cyl", "loc": (0.0, -0.32, 0.49), "r1": 0.028, "r2": 0.022, "depth": 0.18, "rot": (12, 0, 0), "mat": P["cat_belly"], "bone": "TailTip"},
        # 4 Soft Legs + White Paws
        {"kind": "cyl", "loc": (-0.068, 0.14, 0.11), "r1": 0.028, "r2": 0.036, "depth": 0.20, "mat": P["cat_fur"], "bone": "Leg_FL"},
        {"kind": "sphere", "loc": (-0.068, 0.16, 0.022), "scale": (0.034, 0.044, 0.024), "mat": P["cat_belly"], "bone": "Leg_FL"},
        {"kind": "cyl", "loc": (0.068, 0.14, 0.11), "r1": 0.028, "r2": 0.036, "depth": 0.20, "mat": P["cat_fur"], "bone": "Leg_FR"},
        {"kind": "sphere", "loc": (0.068, 0.16, 0.022), "scale": (0.034, 0.044, 0.024), "mat": P["cat_belly"], "bone": "Leg_FR"},
        {"kind": "cyl", "loc": (-0.072, -0.14, 0.11), "r1": 0.030, "r2": 0.042, "depth": 0.20, "mat": P["cat_fur"], "bone": "Leg_BL"},
        {"kind": "sphere", "loc": (-0.072, -0.12, 0.022), "scale": (0.036, 0.048, 0.024), "mat": P["cat_belly"], "bone": "Leg_BL"},
        {"kind": "cyl", "loc": (0.072, -0.14, 0.11), "r1": 0.030, "r2": 0.042, "depth": 0.20, "mat": P["cat_fur"], "bone": "Leg_BR"},
        {"kind": "sphere", "loc": (0.072, -0.12, 0.022), "scale": (0.036, 0.048, 0.024), "mat": P["cat_belly"], "bone": "Leg_BR"},
    ]
    mesh = build_parts_mesh("OW_StreetCat_MeshObj", specs, vertex_groups_by_part=True)
    bones = [
        ("Spine", None, (0.0, 0.0, 0.24), (0.0, 0.16, 0.28)),
        ("Head", "Spine", (0.0, 0.22, 0.34), (0.0, 0.34, 0.38)),
        ("TailBase", "Spine", (0.0, -0.20, 0.27), (0.0, -0.30, 0.42)),
        ("TailTip", "TailBase", (0.0, -0.30, 0.42), (0.0, -0.32, 0.56)),
        ("Leg_FL", "Spine", (-0.068, 0.14, 0.21), (-0.068, 0.14, 0.02)),
        ("Leg_FR", "Spine", (0.068, 0.14, 0.21), (0.068, 0.14, 0.02)),
        ("Leg_BL", "Spine", (-0.072, -0.14, 0.21), (-0.072, -0.14, 0.02)),
        ("Leg_BR", "Spine", (0.072, -0.14, 0.21), (0.072, -0.14, 0.02)),
    ]
    arm = create_armature_and_bind("CatArmature", bones, mesh)

    add_nla_clip(arm, "Idle", 48, {
        "Spine": [(1, (0, 0, 0), (0, 0, 0)), (24, (1.5, 0, 0), (0, 0, 0.008)), (48, (0, 0, 0), (0, 0, 0))],
        "Head": [(1, (0, 0, 0), None), (16, (-6, 0, 14), None), (34, (3, 0, -12), None), (48, (0, 0, 0), None)],
        "TailBase": [(1, (0, -14, 0), None), (24, (0, 14, 0), None), (48, (0, -14, 0), None)],
        "TailTip": [(1, (0, -18, 0), None), (24, (0, 18, 0), None), (48, (0, -18, 0), None)],
    })
    add_nla_clip(arm, "Walk", 24, {
        "Spine": [(1, (0, 0, 0), (0, 0, 0)), (6, (0, 0, 2.5), (0, 0, 0.015)), (12, (0, 0, 0), (0, 0, 0)), (18, (0, 0, -2.5), (0, 0, 0.015)), (24, (0, 0, 0), (0, 0, 0))],
        "Leg_FL": [(1, (26, 0, 0), None), (12, (-24, 0, 0), None), (24, (26, 0, 0), None)],
        "Leg_BR": [(1, (24, 0, 0), None), (12, (-24, 0, 0), None), (24, (24, 0, 0), None)],
        "Leg_FR": [(1, (-24, 0, 0), None), (12, (26, 0, 0), None), (24, (-24, 0, 0), None)],
        "Leg_BL": [(1, (-24, 0, 0), None), (12, (24, 0, 0), None), (24, (-24, 0, 0), None)],
        "TailBase": [(1, (0, -10, 0), None), (12, (0, 10, 0), None), (24, (0, -10, 0), None)],
    })
    add_nla_clip(arm, "Sit", 48, {
        "Spine": [(1, (-26, 0, 0), (0, 0, -0.05)), (24, (-24, 0, 0), (0, 0, -0.045)), (48, (-26, 0, 0), (0, 0, -0.05))],
        "Head": [(1, (20, 0, -8), None), (24, (16, 0, 10), None), (48, (20, 0, -8), None)],
        "Leg_BL": [(1, (48, 0, 0), None), (48, (48, 0, 0), None)],
        "Leg_BR": [(1, (48, 0, 0), None), (48, (48, 0, 0), None)],
        "TailBase": [(1, (42, -16, 0), None), (24, (42, 16, 0), None), (48, (42, -16, 0), None)],
    })
    add_nla_clip(arm, "Happy", 24, {
        "Spine": [(1, (-6, 0, 0), (0, 0, 0.01)), (12, (-14, 0, 0), (0, 0, 0.04)), (24, (-6, 0, 0), (0, 0, 0.01))],
        "Head": [(1, (-10, 0, 16), None), (12, (-12, 0, -16), None), (24, (-10, 0, 16), None)],
        "TailBase": [(1, (-8, -22, 0), None), (12, (-8, 22, 0), None), (24, (-8, -22, 0), None)],
        "TailTip": [(1, (0, -28, 0), None), (12, (0, 28, 0), None), (24, (0, -28, 0), None)],
    })
    export_rigged_fbx(arm, [mesh], "OW_StreetCat.fbx")


def build_rigged_pigeon():
    clear_scene()
    P = palette()
    specs = [
        # Plump Torso & Tail Feathers
        {"kind": "sphere", "loc": (0.0, 0.0, 0.13), "scale": (0.075, 0.12, 0.072), "rot": (12, 0, 0), "mat": P["pigeon_body"], "bone": "Body"},
        {"kind": "box", "loc": (0.0, -0.14, 0.11), "scale": (0.085, 0.12, 0.014), "rot": (18, 0, 0), "mat": P["pigeon_wing"], "bone": "Body"},
        # Iridescent Neck & Round Head + Beak + Eyes
        {"kind": "cyl", "loc": (0.0, 0.08, 0.19), "r1": 0.048, "r2": 0.038, "depth": 0.09, "rot": (-12, 0, 0), "mat": P["pigeon_neck"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.0, 0.10, 0.25), "scale": (0.044, 0.050, 0.044), "mat": P["pigeon_body"], "bone": "Head"},
        {"kind": "cyl", "loc": (0.0, 0.16, 0.245), "r1": 0.016, "r2": 0.004, "depth": 0.042, "rot": (-98, 0, 0), "mat": P["pigeon_wing"], "bone": "Head", "segs": 8},
        {"kind": "sphere", "loc": (-0.032, 0.125, 0.26), "scale": (0.011, 0.011, 0.011), "mat": P["cat_eye"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.032, 0.125, 0.26), "scale": (0.011, 0.011, 0.011), "mat": P["cat_eye"], "bone": "Head"},
        # Articulated Flapping Wings (L & R)!
        {"kind": "box", "loc": (-0.11, -0.01, 0.15), "scale": (0.14, 0.14, 0.016), "rot": (4, -10, -8), "mat": P["pigeon_wing"], "bone": "Wing_L", "bevel": 0.005},
        {"kind": "box", "loc": (-0.21, -0.03, 0.155), "scale": (0.10, 0.11, 0.012), "rot": (4, -14, -14), "mat": P["pigeon_body"], "bone": "Wing_L", "bevel": 0.004},
        {"kind": "box", "loc": (0.11, -0.01, 0.15), "scale": (0.14, 0.14, 0.016), "rot": (4, 10, 8), "mat": P["pigeon_wing"], "bone": "Wing_R", "bevel": 0.005},
        {"kind": "box", "loc": (0.21, -0.03, 0.155), "scale": (0.10, 0.11, 0.012), "rot": (4, 14, 14), "mat": P["pigeon_body"], "bone": "Wing_R", "bevel": 0.004},
        # Coral Legs & Feet
        {"kind": "cyl", "loc": (-0.032, 0.01, 0.045), "r1": 0.009, "r2": 0.011, "depth": 0.085, "mat": P["pigeon_foot"], "bone": "Leg_L", "segs": 6},
        {"kind": "box", "loc": (-0.032, 0.025, 0.006), "scale": (0.030, 0.048, 0.010), "mat": P["pigeon_foot"], "bone": "Leg_L"},
        {"kind": "cyl", "loc": (0.032, 0.01, 0.045), "r1": 0.009, "r2": 0.011, "depth": 0.085, "mat": P["pigeon_foot"], "bone": "Leg_R", "segs": 6},
        {"kind": "box", "loc": (0.032, 0.025, 0.006), "scale": (0.030, 0.048, 0.010), "mat": P["pigeon_foot"], "bone": "Leg_R"},
    ]
    mesh = build_parts_mesh("OW_Pigeon_MeshObj", specs, vertex_groups_by_part=True)
    bones = [
        ("Body", None, (0.0, 0.0, 0.13), (0.0, 0.08, 0.15)),
        ("Head", "Body", (0.0, 0.08, 0.18), (0.0, 0.12, 0.26)),
        ("Wing_L", "Body", (-0.06, 0.0, 0.15), (-0.24, -0.02, 0.15)),
        ("Wing_R", "Body", (0.06, 0.0, 0.15), (0.24, -0.02, 0.15)),
        ("Leg_L", "Body", (-0.032, 0.01, 0.085), (-0.032, 0.01, 0.005)),
        ("Leg_R", "Body", (0.032, 0.01, 0.085), (0.032, 0.01, 0.005)),
    ]
    arm = create_armature_and_bind("PigeonArmature", bones, mesh)

    # Folded wings for ground clips, full wingbeat for Fly!
    add_nla_clip(arm, "Peck", 32, {
        "Body": [(1, (0, 0, 0), None), (10, (24, 0, 0), None), (18, (0, 0, 0), None), (25, (28, 0, 0), None), (32, (0, 0, 0), None)],
        "Head": [(1, (0, 0, 0), None), (10, (34, 0, 0), None), (18, (-6, 0, 12), None), (25, (38, 0, 0), None), (32, (0, 0, 0), None)],
        "Wing_L": [(1, (0, 62, 18), None), (32, (0, 62, 18), None)],
        "Wing_R": [(1, (0, -62, -18), None), (32, (0, -62, -18), None)],
    })
    add_nla_clip(arm, "Walk", 18, {
        "Head": [(1, (-10, 0, 0), (0, 0.02, 0)), (9, (12, 0, 0), (0, -0.015, 0)), (18, (-10, 0, 0), (0, 0.02, 0))],
        "Wing_L": [(1, (0, 60, 18), None), (18, (0, 60, 18), None)],
        "Wing_R": [(1, (0, -60, -18), None), (18, (0, -60, -18), None)],
        "Leg_L": [(1, (28, 0, 0), None), (9, (-28, 0, 0), None), (18, (28, 0, 0), None)],
        "Leg_R": [(1, (-28, 0, 0), None), (9, (28, 0, 0), None), (18, (-28, 0, 0), None)],
    })
    add_nla_clip(arm, "Fly", 12, {
        "Body": [(1, (-8, 0, 0), (0, 0, 0.02)), (6, (-4, 0, 0), (0, 0, -0.02)), (12, (-8, 0, 0), (0, 0, 0.02))],
        "Wing_L": [(1, (0, -48, 0), None), (6, (0, 44, 0), None), (12, (0, -48, 0), None)],
        "Wing_R": [(1, (0, 48, 0), None), (6, (0, -44, 0), None), (12, (0, 48, 0), None)],
        "Leg_L": [(1, (-55, 0, 0), None), (12, (-55, 0, 0), None)],
        "Leg_R": [(1, (-55, 0, 0), None), (12, (-55, 0, 0), None)],
    })
    export_rigged_fbx(arm, [mesh], "OW_Pigeon.fbx")


def build_rigged_lake_swan():
    clear_scene()
    P = palette()
    specs = [
        # Buoyant Oval White Body + Sculpted Tail Plume
        {"kind": "sphere", "loc": (0.0, 0.0, 0.18), "scale": (0.24, 0.42, 0.19), "mat": P["swan_white"], "bone": "Body"},
        {"kind": "sphere", "loc": (0.0, -0.36, 0.25), "scale": (0.14, 0.22, 0.10), "rot": (-22, 0, 0), "mat": P["swan_white"], "bone": "Body"},
        # Sculpted Folded/Spreading Wings
        {"kind": "sphere", "loc": (-0.21, -0.05, 0.26), "scale": (0.075, 0.34, 0.15), "rot": (-8, -14, -6), "mat": P["swan_white"], "bone": "Wing_L"},
        {"kind": "sphere", "loc": (0.21, -0.05, 0.26), "scale": (0.075, 0.34, 0.15), "rot": (-8, 14, 6), "mat": P["swan_white"], "bone": "Wing_R"},
        # Graceful S-Curved Neck (2 bones!) + Head + Orange-Black Beak
        {"kind": "cyl", "loc": (0.0, 0.30, 0.40), "r1": 0.078, "r2": 0.054, "depth": 0.34, "rot": (18, 0, 0), "mat": P["swan_white"], "bone": "NeckLower"},
        {"kind": "cyl", "loc": (0.0, 0.28, 0.68), "r1": 0.054, "r2": 0.044, "depth": 0.30, "rot": (-14, 0, 0), "mat": P["swan_white"], "bone": "NeckUpper"},
        {"kind": "sphere", "loc": (0.0, 0.36, 0.84), "scale": (0.062, 0.092, 0.064), "mat": P["swan_white"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.0, 0.43, 0.84), "scale": (0.036, 0.036, 0.028), "mat": P["swan_mask"], "bone": "Head"},
        {"kind": "cyl", "loc": (0.0, 0.50, 0.82), "r1": 0.028, "r2": 0.012, "depth": 0.11, "rot": (-98, 0, 0), "mat": P["swan_beak"], "bone": "Head"},
        {"kind": "sphere", "loc": (-0.046, 0.40, 0.855), "scale": (0.012, 0.012, 0.012), "mat": P["pupil"], "bone": "Head"},
        {"kind": "sphere", "loc": (0.046, 0.40, 0.855), "scale": (0.012, 0.012, 0.012), "mat": P["pupil"], "bone": "Head"},
    ]
    mesh = build_parts_mesh("OW_LakeSwan_MeshObj", specs, vertex_groups_by_part=True)
    bones = [
        ("Body", None, (0.0, 0.0, 0.18), (0.0, 0.25, 0.22)),
        ("NeckLower", "Body", (0.0, 0.30, 0.24), (0.0, 0.26, 0.54)),
        ("NeckUpper", "NeckLower", (0.0, 0.26, 0.54), (0.0, 0.32, 0.82)),
        ("Head", "NeckUpper", (0.0, 0.32, 0.82), (0.0, 0.50, 0.82)),
        ("Wing_L", "Body", (-0.18, 0.0, 0.26), (-0.28, -0.25, 0.32)),
        ("Wing_R", "Body", (0.18, 0.0, 0.26), (0.28, -0.25, 0.32)),
    ]
    arm = create_armature_and_bind("SwanArmature", bones, mesh)

    add_nla_clip(arm, "Swim", 48, {
        "Body": [(1, (0, 0, -2), (0, 0, 0)), (24, (2, 0, 2), (0, 0, 0.02)), (48, (0, 0, -2), (0, 0, 0))],
        "NeckLower": [(1, (-4, 0, 0), None), (24, (5, 0, 0), None), (48, (-4, 0, 0), None)],
        "NeckUpper": [(1, (4, 0, -6), None), (24, (-4, 0, 6), None), (48, (4, 0, -6), None)],
        "Wing_L": [(1, (0, 0, 0), None), (24, (4, -8, 0), None), (48, (0, 0, 0), None)],
        "Wing_R": [(1, (0, 0, 0), None), (24, (4, 8, 0), None), (48, (0, 0, 0), None)],
    })
    add_nla_clip(arm, "WingSpread", 36, {
        "Wing_L": [(1, (0, 0, 0), None), (18, (-16, -34, 18), None), (36, (0, 0, 0), None)],
        "Wing_R": [(1, (0, 0, 0), None), (18, (-16, 34, -18), None), (36, (0, 0, 0), None)],
        "NeckUpper": [(1, (0, 0, 0), None), (18, (-12, 0, 0), None), (36, (0, 0, 0), None)],
    })
    export_rigged_fbx(arm, [mesh], "OW_LakeSwan.fbx")


# =============================================================================
# D. ISTANBUL MAHALLE THEME & SPORTS HERO PROPS
# =============================================================================
def build_simit_cart():
    clear_scene()
    P = palette()
    specs = [
        # Lower crimson wooden cart cabinet with brass trim
        {"kind": "box", "loc": (0.0, 0.0, 0.62), "scale": (1.42, 0.74, 0.44), "mat": P["crimson"], "bevel": 0.02},
        {"kind": "box", "loc": (0.0, 0.0, 0.86), "scale": (1.48, 0.80, 0.05), "mat": P["brass"], "bevel": 0.01},
        # Glass showcase cabinet + 4 crimson-brass corner posts
        {"kind": "box", "loc": (0.0, 0.0, 1.24), "scale": (1.30, 0.66, 0.70), "mat": P["glass"]},
        {"kind": "box", "loc": (-0.67, -0.34, 1.26), "scale": (0.055, 0.055, 0.78), "mat": P["crimson"]},
        {"kind": "box", "loc": (0.67, -0.34, 1.26), "scale": (0.055, 0.055, 0.78), "mat": P["crimson"]},
        {"kind": "box", "loc": (-0.67, 0.34, 1.26), "scale": (0.055, 0.055, 0.78), "mat": P["crimson"]},
        {"kind": "box", "loc": (0.67, 0.34, 1.26), "scale": (0.055, 0.055, 0.78), "mat": P["crimson"]},
        # Ornate crimson & gold roof crown + 4 brass finials!
        {"kind": "box", "loc": (0.0, 0.0, 1.66), "scale": (1.50, 0.82, 0.09), "mat": P["crimson"], "bevel": 0.02},
        {"kind": "box", "loc": (0.0, 0.0, 1.75), "scale": (1.28, 0.64, 0.11), "mat": P["brass"], "bevel": 0.02},
        {"kind": "sphere", "loc": (-0.68, -0.35, 1.78), "scale": (0.05, 0.05, 0.07), "mat": P["brass"]},
        {"kind": "sphere", "loc": (0.68, -0.35, 1.78), "scale": (0.05, 0.05, 0.07), "mat": P["brass"]},
        {"kind": "sphere", "loc": (-0.68, 0.35, 1.78), "scale": (0.05, 0.05, 0.07), "mat": P["brass"]},
        {"kind": "sphere", "loc": (0.68, 0.35, 1.78), "scale": (0.05, 0.05, 0.07), "mat": P["brass"]},
        # 2 Large Spoked Red Wheels + Front Support Legs + Push Handles
        {"kind": "torus", "loc": (-0.42, 0.42, 0.34), "major": 0.31, "minor": 0.032, "rot": (90, 0, 0), "mat": P["crimson"]},
        {"kind": "torus", "loc": (-0.42, -0.42, 0.34), "major": 0.31, "minor": 0.032, "rot": (90, 0, 0), "mat": P["crimson"]},
        {"kind": "cyl", "loc": (-0.42, 0.0, 0.34), "r1": 0.028, "r2": 0.028, "depth": 0.90, "rot": (90, 0, 0), "mat": P["iron"]},
        {"kind": "cyl", "loc": (0.56, 0.30, 0.22), "r1": 0.03, "r2": 0.03, "depth": 0.44, "mat": P["iron"]},
        {"kind": "cyl", "loc": (0.56, -0.30, 0.22), "r1": 0.03, "r2": 0.03, "depth": 0.44, "mat": P["iron"]},
        {"kind": "cyl", "loc": (0.88, 0.26, 0.80), "r1": 0.024, "r2": 0.024, "depth": 0.44, "rot": (0, 90, 0), "mat": P["wood_warm"]},
        {"kind": "cyl", "loc": (0.88, -0.26, 0.80), "r1": 0.024, "r2": 0.024, "depth": 0.44, "rot": (0, 90, 0), "mat": P["wood_warm"]},
    ]
    # Wheel spokes
    for wy in (-0.42, 0.42):
        for ang in (0, 45, 90, 135):
            specs.append({"kind": "box", "loc": (-0.42, wy, 0.34), "scale": (0.58, 0.018, 0.024), "rot": (0, ang, 0), "mat": P["brass"]})
    # Pyramid stacks of golden sesame Simits inside the showcase!
    for sx in (-0.38, 0.0, 0.38):
        for sz_i in range(4):
            specs.append({"kind": "torus", "loc": (sx, 0.0, 0.95 + sz_i * 0.075), "major": 0.115, "minor": 0.032, "mat": P["simit_gold"], "maj_segs": 12, "min_segs": 6})
    obj = build_parts_mesh("OW_SimitCart", specs)
    export_static_fbx([obj], "OW_SimitCart.fbx")


def build_tea_garden_set():
    clear_scene()
    P = palette()
    specs = [
        # Round Marble Bistro Table + Wrought Iron Pedestal
        {"kind": "cyl", "loc": (0.0, 0.0, 0.74), "r1": 0.56, "r2": 0.56, "depth": 0.05, "mat": P["marble"], "segs": 18},
        {"kind": "torus", "loc": (0.0, 0.0, 0.74), "major": 0.56, "minor": 0.018, "mat": P["brass"]},
        {"kind": "cyl", "loc": (0.0, 0.0, 0.37), "r1": 0.045, "r2": 0.038, "depth": 0.72, "mat": P["iron"]},
        {"kind": "cyl", "loc": (0.0, 0.0, 0.03), "r1": 0.30, "r2": 0.24, "depth": 0.06, "mat": P["iron"]},
        # Silver Tea Tray + Brass Teapot + 2 Ince Belli Tulip Tea Glasses!
        {"kind": "cyl", "loc": (0.0, 0.0, 0.775), "r1": 0.22, "r2": 0.20, "depth": 0.018, "mat": P["chrome"]},
        {"kind": "sphere", "loc": (-0.06, 0.02, 0.85), "scale": (0.075, 0.075, 0.070), "mat": P["brass"]},
        {"kind": "sphere", "loc": (-0.06, 0.02, 0.93), "scale": (0.048, 0.048, 0.035), "mat": P["brass"]},
        {"kind": "cyl", "loc": (0.09, 0.07, 0.83), "r1": 0.026, "r2": 0.032, "depth": 0.09, "mat": P["tea_amber"], "segs": 10},
        {"kind": "cyl", "loc": (0.09, -0.07, 0.83), "r1": 0.026, "r2": 0.032, "depth": 0.09, "mat": P["tea_amber"], "segs": 10},
        # Tall Sun Umbrella (Terracotta + Cream Scalloped Canopy)
        {"kind": "cyl", "loc": (0.0, 0.0, 1.50), "r1": 0.032, "r2": 0.028, "depth": 2.30, "mat": P["wood_warm"]},
        {"kind": "cyl", "loc": (0.0, 0.0, 2.54), "r1": 1.38, "r2": 0.14, "depth": 0.44, "mat": P["terracotta"], "segs": 10},
        {"kind": "cyl", "loc": (0.0, 0.0, 2.31), "r1": 1.38, "r2": 1.38, "depth": 0.12, "mat": P["cream"], "segs": 10},
        {"kind": "sphere", "loc": (0.0, 0.0, 2.78), "scale": (0.06, 0.06, 0.08), "mat": P["brass"]},
    ]
    # 2 Facing Bistro Chairs
    for side in (-1.0, 1.0):
        cx = side * 0.88
        specs.extend([
            {"kind": "cyl", "loc": (cx, 0.0, 0.45), "r1": 0.22, "r2": 0.22, "depth": 0.04, "mat": P["wood_warm"]},
            {"kind": "box", "loc": (cx + side * 0.20, 0.0, 0.72), "scale": (0.04, 0.40, 0.46), "mat": P["iron"], "bevel": 0.01},
            {"kind": "box", "loc": (cx + side * 0.19, 0.0, 0.78), "scale": (0.03, 0.34, 0.22), "mat": P["terracotta"]},
            {"kind": "cyl", "loc": (cx - 0.14, -0.14, 0.22), "r1": 0.018, "r2": 0.018, "depth": 0.44, "mat": P["iron"]},
            {"kind": "cyl", "loc": (cx + 0.14, -0.14, 0.22), "r1": 0.018, "r2": 0.018, "depth": 0.44, "mat": P["iron"]},
            {"kind": "cyl", "loc": (cx - 0.14, 0.14, 0.22), "r1": 0.018, "r2": 0.018, "depth": 0.44, "mat": P["iron"]},
            {"kind": "cyl", "loc": (cx + 0.14, 0.14, 0.22), "r1": 0.018, "r2": 0.018, "depth": 0.44, "mat": P["iron"]},
        ])
    obj = build_parts_mesh("OW_TeaGardenSet", specs)
    export_static_fbx([obj], "OW_TeaGardenSet.fbx")


def build_istanbul_fountain():
    """Carved Limestone Ottoman / Istanbul Mahalle Cesmesi with Iznik Tile & Copper Dome."""
    clear_scene()
    P = palette()
    specs = [
        # Stepped Octagonal Marble Plinth
        {"kind": "cyl", "loc": (0.0, 0.0, 0.12), "r1": 1.48, "r2": 1.42, "depth": 0.24, "mat": P["limestone"], "segs": 8},
        # Main Carved Limestone Body + Side Pilasters
        {"kind": "box", "loc": (0.0, 0.0, 1.38), "scale": (1.66, 1.24, 2.28), "mat": P["limestone"], "bevel": 0.04},
        {"kind": "cyl", "loc": (-0.82, 0.58, 1.38), "r1": 0.11, "r2": 0.11, "depth": 2.26, "mat": P["marble"]},
        {"kind": "cyl", "loc": (0.82, 0.58, 1.38), "r1": 0.11, "r2": 0.11, "depth": 2.26, "mat": P["marble"]},
        {"kind": "cyl", "loc": (-0.82, -0.58, 1.38), "r1": 0.11, "r2": 0.11, "depth": 2.26, "mat": P["marble"]},
        {"kind": "cyl", "loc": (0.82, -0.58, 1.38), "r1": 0.11, "r2": 0.11, "depth": 2.26, "mat": P["marble"]},
        # Front & Back Iznik Turquoise Tile Medallions + Brass Spigots + Marble Water Basins!
        {"kind": "box", "loc": (0.0, 0.63, 1.56), "scale": (1.12, 0.05, 1.18), "mat": P["iznik_teal"], "bevel": 0.015},
        {"kind": "box", "loc": (0.0, 0.65, 1.98), "scale": (0.86, 0.04, 0.24), "mat": P["iznik_blue"]},
        {"kind": "cyl", "loc": (-0.28, 0.72, 1.12), "r1": 0.032, "r2": 0.022, "depth": 0.18, "rot": (90, 0, 0), "mat": P["brass"]},
        {"kind": "cyl", "loc": (0.28, 0.72, 1.12), "r1": 0.032, "r2": 0.022, "depth": 0.18, "rot": (90, 0, 0), "mat": P["brass"]},
        {"kind": "box", "loc": (0.0, 0.88, 0.50), "scale": (1.36, 0.56, 0.56), "mat": P["marble"], "bevel": 0.03},
        {"kind": "box", "loc": (0.0, 0.88, 0.76), "scale": (1.16, 0.40, 0.08), "mat": P["iznik_teal"]},
        {"kind": "box", "loc": (0.0, -0.63, 1.56), "scale": (1.12, 0.05, 1.18), "mat": P["iznik_teal"], "bevel": 0.015},
        {"kind": "box", "loc": (0.0, -0.88, 0.50), "scale": (1.36, 0.56, 0.56), "mat": P["marble"], "bevel": 0.03},
        # Wide Eaved Cornice + Oxidized Copper Ottoman Cupola + Brass Alem Finial!
        {"kind": "box", "loc": (0.0, 0.0, 2.60), "scale": (2.34, 1.96, 0.16), "mat": P["wood_dark"], "bevel": 0.03},
        {"kind": "cyl", "loc": (0.0, 0.0, 2.78), "r1": 1.16, "r2": 0.90, "depth": 0.22, "mat": P["copper_patina"], "segs": 8},
        {"kind": "sphere", "loc": (0.0, 0.0, 3.04), "scale": (0.86, 0.86, 0.62), "mat": P["copper_patina"], "segs": 14, "rings": 8},
        {"kind": "cyl", "loc": (0.0, 0.0, 3.82), "r1": 0.035, "r2": 0.012, "depth": 0.44, "mat": P["brass"]},
        {"kind": "sphere", "loc": (0.0, 0.0, 3.74), "scale": (0.09, 0.09, 0.09), "mat": P["brass"]},
        {"kind": "sphere", "loc": (0.0, 0.0, 3.92), "scale": (0.055, 0.055, 0.055), "mat": P["brass"]},
    ]
    obj = build_parts_mesh("OW_IstanbulFountain", specs)
    export_static_fbx([obj], "OW_IstanbulFountain.fbx")


def build_plane_tree():
    """Sculpted Multi-Branch Istanbul Cinar (Plane Tree) with Organic Layered Canopy."""
    clear_scene()
    P = palette()
    specs = [
        # Flared Root Buttresses + Sculpted Bark Trunk
        {"kind": "cyl", "loc": (0.0, 0.0, 0.35), "r1": 0.58, "r2": 0.34, "depth": 0.70, "mat": P["bark"], "segs": 9},
        {"kind": "cyl", "loc": (0.0, 0.0, 1.65), "r1": 0.34, "r2": 0.24, "depth": 2.10, "mat": P["bark"], "segs": 9},
        # 4 Spreading Primary Boughs
        {"kind": "cyl", "loc": (0.58, 0.35, 3.10), "r1": 0.16, "r2": 0.09, "depth": 1.65, "rot": (-28, 36, 0), "mat": P["bark"], "segs": 7},
        {"kind": "cyl", "loc": (-0.62, 0.22, 3.15), "r1": 0.15, "r2": 0.08, "depth": 1.60, "rot": (-22, -38, 0), "mat": P["bark"], "segs": 7},
        {"kind": "cyl", "loc": (0.18, -0.62, 3.05), "r1": 0.15, "r2": 0.08, "depth": 1.55, "rot": (38, 12, 0), "mat": P["bark"], "segs": 7},
        {"kind": "cyl", "loc": (-0.25, -0.52, 3.25), "r1": 0.14, "r2": 0.08, "depth": 1.45, "rot": (32, -18, 0), "mat": P["bark"], "segs": 7},
        # 10 Layered Organic Canopy Clusters (Deep Underside -> Mid Emerald -> Sunlit Crown!)
        {"kind": "ico", "loc": (0.0, 0.0, 4.10), "scale": (1.65, 1.55, 1.15), "mat": P["leaf_deep"], "subdiv": 2, "lump": 0.14},
        {"kind": "ico", "loc": (1.28, 0.68, 3.85), "scale": (1.25, 1.18, 0.92), "mat": P["leaf_mid"], "subdiv": 2, "lump": 0.16},
        {"kind": "ico", "loc": (-1.32, 0.55, 3.95), "scale": (1.28, 1.15, 0.96), "mat": P["leaf_mid"], "subdiv": 2, "lump": 0.15},
        {"kind": "ico", "loc": (0.45, -1.25, 3.80), "scale": (1.22, 1.15, 0.88), "mat": P["leaf_deep"], "subdiv": 2, "lump": 0.16},
        {"kind": "ico", "loc": (-0.78, -1.05, 4.00), "scale": (1.18, 1.14, 0.92), "mat": P["leaf_mid"], "subdiv": 2, "lump": 0.15},
        {"kind": "ico", "loc": (0.20, 1.15, 4.20), "scale": (1.15, 1.12, 0.90), "mat": P["leaf_mid"], "subdiv": 2, "lump": 0.14},
        {"kind": "ico", "loc": (0.85, 0.35, 4.85), "scale": (1.08, 1.02, 0.82), "mat": P["leaf_sun"], "subdiv": 2, "lump": 0.15},
        {"kind": "ico", "loc": (-0.82, 0.18, 4.92), "scale": (1.05, 1.00, 0.80), "mat": P["leaf_sun"], "subdiv": 2, "lump": 0.15},
        {"kind": "ico", "loc": (0.0, -0.40, 5.10), "scale": (1.12, 1.08, 0.84), "mat": P["leaf_sun"], "subdiv": 2, "lump": 0.14},
        {"kind": "ico", "loc": (0.05, 0.12, 5.55), "scale": (0.92, 0.88, 0.72), "mat": P["leaf_sun"], "subdiv": 2, "lump": 0.16},
    ]
    obj = build_parts_mesh("OW_PlaneTree", specs)
    export_static_fbx([obj], "OW_PlaneTree.fbx")


def build_cypress_tree():
    """Tall Sculpted Mediterranean Istanbul Selvi (Cypress Tree)."""
    clear_scene()
    P = palette()
    specs = [
        {"kind": "cyl", "loc": (0.0, 0.0, 0.55), "r1": 0.22, "r2": 0.14, "depth": 1.10, "mat": P["bark"], "segs": 8},
        {"kind": "ico", "loc": (0.0, 0.0, 1.60), "scale": (0.72, 0.72, 0.85), "mat": P["cypress_dark"], "subdiv": 2, "lump": 0.12},
        {"kind": "ico", "loc": (0.0, 0.0, 2.55), "scale": (0.68, 0.68, 0.92), "mat": P["cypress_dark"], "subdiv": 2, "lump": 0.14},
        {"kind": "ico", "loc": (0.0, 0.0, 3.55), "scale": (0.60, 0.60, 0.90), "mat": P["cypress_mid"], "subdiv": 2, "lump": 0.14},
        {"kind": "ico", "loc": (0.0, 0.0, 4.48), "scale": (0.50, 0.50, 0.85), "mat": P["cypress_mid"], "subdiv": 2, "lump": 0.13},
        {"kind": "ico", "loc": (0.0, 0.0, 5.30), "scale": (0.36, 0.36, 0.78), "mat": P["leaf_deep"], "subdiv": 2, "lump": 0.12},
        {"kind": "cyl", "loc": (0.0, 0.0, 6.08), "r1": 0.24, "r2": 0.03, "depth": 0.90, "mat": P["cypress_mid"], "segs": 8},
    ]
    obj = build_parts_mesh("OW_CypressTree", specs)
    export_static_fbx([obj], "OW_CypressTree.fbx")


def build_shop_awning():
    """Striped Turkish Shop Awning (Tente) with Scalloped Valance & Hanging Brass Lamp."""
    clear_scene()
    P = palette()
    specs = [
        # Wall mounting rail + Iron side arms
        {"kind": "box", "loc": (0.0, 0.0, 0.36), "scale": (3.80, 0.08, 0.08), "mat": P["iron"]},
        {"kind": "box", "loc": (-1.78, 0.68, 0.14), "scale": (0.05, 1.38, 0.05), "rot": (-18, 0, 0), "mat": P["iron"]},
        {"kind": "box", "loc": (1.78, 0.68, 0.14), "scale": (0.05, 1.38, 0.05), "rot": (-18, 0, 0), "mat": P["iron"]},
        # Hanging brass wall cafe lamp under awning
        {"kind": "cyl", "loc": (0.0, 0.25, -0.05), "r1": 0.015, "r2": 0.015, "depth": 0.26, "mat": P["brass"]},
        {"kind": "sphere", "loc": (0.0, 0.25, -0.22), "scale": (0.11, 0.11, 0.13), "mat": P["lamp_glow"]},
    ]
    # Alternating Crimson & Cream Canvas Stripes!
    stripes = 9
    w = 3.60 / stripes
    for i in range(stripes):
        sx = -1.80 + (i + 0.5) * w
        m = P["crimson"] if i % 2 == 0 else P["cream"]
        specs.append({"kind": "box", "loc": (sx, 0.72, 0.15), "scale": (w + 0.005, 1.48, 0.045), "rot": (-18, 0, 0), "mat": m})
        specs.append({"kind": "box", "loc": (sx, 1.42, -0.18), "scale": (w + 0.005, 0.045, 0.24), "mat": m})
    obj = build_parts_mesh("OW_ShopAwning", specs)
    export_static_fbx([obj], "OW_ShopAwning.fbx")


def build_flower_planter():
    """Carved Travertine Municipal Planter Box with Istanbul Tulips & Lavender."""
    clear_scene()
    P = palette()
    specs = [
        {"kind": "box", "loc": (0.0, 0.0, 0.28), "scale": (1.68, 0.68, 0.56), "mat": P["limestone"], "bevel": 0.03},
        {"kind": "box", "loc": (0.0, 0.0, 0.58), "scale": (1.78, 0.78, 0.08), "mat": P["marble"], "bevel": 0.02},
        {"kind": "box", "loc": (0.0, 0.0, 0.54), "scale": (1.54, 0.54, 0.08), "mat": P["soil"]},
    ]
    # Lush bushes + tulips + lavender spikes
    for i in range(6):
        fx = -0.60 + i * 0.24
        fy = 0.10 if i % 2 == 0 else -0.10
        specs.append({"kind": "ico", "loc": (fx, fy, 0.68), "scale": (0.22, 0.20, 0.16), "mat": P["leaf_mid"], "subdiv": 1, "lump": 0.15})
        fl_mat = P["tulip_red"] if i % 3 == 0 else (P["tulip_gold"] if i % 3 == 1 else P["lavender"])
        specs.append({"kind": "cyl", "loc": (fx, fy, 0.76), "r1": 0.012, "r2": 0.012, "depth": 0.24, "mat": P["leaf_deep"], "segs": 6})
        specs.append({"kind": "sphere", "loc": (fx, fy, 0.90), "scale": (0.07, 0.07, 0.10), "mat": fl_mat, "segs": 8})
        specs.append({"kind": "sphere", "loc": (fx + 0.06, -fy, 0.85), "scale": (0.06, 0.06, 0.09), "mat": P["tulip_gold"] if i % 2 == 0 else P["tulip_red"], "segs": 8})
    obj = build_parts_mesh("OW_FlowerPlanter", specs)
    export_static_fbx([obj], "OW_FlowerPlanter.fbx")


def build_diving_board():
    """Olympic Springboard Diving Stand for Hamidiye Acik Yuzme Havuzu!"""
    clear_scene()
    P = palette()
    specs = [
        # Heavy Concrete & Chrome Base Pedestal
        {"kind": "box", "loc": (0.0, -0.55, 0.45), "scale": (0.86, 1.35, 0.90), "mat": P["marble"], "bevel": 0.04},
        {"kind": "box", "loc": (0.0, -0.55, 0.92), "scale": (0.92, 1.40, 0.06), "mat": P["aqua_board"], "bevel": 0.02},
        # Fulcrum Roller Mechanism
        {"kind": "cyl", "loc": (0.0, 0.12, 1.02), "r1": 0.065, "r2": 0.065, "depth": 0.66, "rot": (0, 90, 0), "mat": P["chrome"]},
        # Fiberglass Springboard Plank jutting +2.4m over the pool!
        {"kind": "box", "loc": (0.0, 0.78, 1.12), "scale": (0.56, 3.15, 0.065), "mat": P["white_board"], "bevel": 0.015},
        {"kind": "box", "loc": (0.0, 0.80, 1.158), "scale": (0.46, 2.98, 0.012), "mat": P["aqua_board"]},
        # Brushed Chrome Side Handrails
        {"kind": "cyl", "loc": (-0.36, -0.85, 1.42), "r1": 0.024, "r2": 0.024, "depth": 0.96, "mat": P["chrome"]},
        {"kind": "cyl", "loc": (0.36, -0.85, 1.42), "r1": 0.024, "r2": 0.024, "depth": 0.96, "mat": P["chrome"]},
        {"kind": "cyl", "loc": (-0.36, 0.08, 1.42), "r1": 0.024, "r2": 0.024, "depth": 0.96, "mat": P["chrome"]},
        {"kind": "cyl", "loc": (0.36, 0.08, 1.42), "r1": 0.024, "r2": 0.024, "depth": 0.96, "mat": P["chrome"]},
        {"kind": "cyl", "loc": (-0.36, -0.38, 1.88), "r1": 0.026, "r2": 0.026, "depth": 1.00, "rot": (90, 0, 0), "mat": P["chrome"]},
        {"kind": "cyl", "loc": (0.36, -0.38, 1.88), "r1": 0.026, "r2": 0.026, "depth": 1.00, "rot": (90, 0, 0), "mat": P["chrome"]},
        # Rear Climbing Steps (3 steps)
        {"kind": "box", "loc": (0.0, -1.32, 0.25), "scale": (0.58, 0.22, 0.05), "mat": P["chrome"]},
        {"kind": "box", "loc": (0.0, -1.26, 0.56), "scale": (0.58, 0.22, 0.05), "mat": P["chrome"]},
        {"kind": "box", "loc": (0.0, -1.20, 0.87), "scale": (0.58, 0.22, 0.05), "mat": P["chrome"]},
    ]
    obj = build_parts_mesh("OW_DivingBoard", specs)
    export_static_fbx([obj], "OW_DivingBoard.fbx")


def main():
    print("=== Building 15 Commercial-Safe CC0 + Blender 5.3.0 Istanbul & Wildlife FBX Models ===")
    build_cc0_polyhaven_bench()
    build_cc0_polyhaven_lantern()
    build_cc0_street_dog()
    build_rigged_farm_goat()
    build_rigged_street_cat()
    build_rigged_pigeon()
    build_rigged_lake_swan()
    build_simit_cart()
    build_tea_garden_set()
    build_istanbul_fountain()
    build_plane_tree()
    build_cypress_tree()
    build_shop_awning()
    build_flower_planter()
    build_diving_board()
    print("=== ALL 15 BLENDER 5.3.0 FBX MODELS EXPORTED CLEANLY! ===")


if __name__ == "__main__":
    main()
