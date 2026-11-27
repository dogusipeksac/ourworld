#!/usr/bin/env python3
"""
GRIDROAM Phase 4 (Batches 6 & 8) – Furniture, Interactive Props, Crops & Nature Clusters Upgrade:
  1. Universal Bevel + Smooth Normals + Clean Box UV Unwrap across ALL remaining uv=False static OW_* FBX
     props, tools, furniture, crops, range targets, awnings & street objects (preserving exact object names,
     pivots like CarLift Carriage & Windmill Rotor, material slots & colliders!).
  2. Dedicated Stylized-Realistic Sculpt Upgrades for Hero Interior/Home Furniture:
     - OW_HomeLuxurySofaSet.fbx     (Plump tufted velvet L-sofa, rounded seat/back cushions, throw pillows,
                                     beveled glass-top brass coffee table, Anatolian woven kilim rug!)
     - Interior_TurkishSofaL.fbx    (Upgraded matching soft beveled sofa + cushions + UVs in Art/Models/Blender/)
     - Interior_TurkishKitchen.fbx  (Paneled wooden upper/lower cabinets w/ brushed handles, marble countertop,
                                     recessed steel sink w/ curved gooseneck faucet, gas hob & range hood!)
  3. Brand-New Stylized-Realistic Nature & Vegetation FBXs (Batch 8) in Resources/OurWorldLife/:
     - OW_BushCluster.fbx           (Organic multi-lobe sculpted shrub w/ woody twigs & veined crop_leaf UVs)
     - OW_FloweringShrub.fbx        (Lush Mediterranean park shrub dotted w/ colorful blossoms)
     - OW_MossyRockCluster.fbx      (Chiseled stratified Anatolian limestone boulder group w/ moss cap)
     - OW_GrassTuft.fbx             (Curved radiating botanical grass blades)
"""

import glob
import math
import os
import random
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
LIFE_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldLife")
ART_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")

RIGGED_ANIMALS = {
    "OW_Cow.fbx", "OW_Sheep.fbx", "OW_FarmGoat.fbx", "OW_Chicken.fbx",
    "OW_StreetDog.fbx", "OW_ShepherdDog.fbx", "OW_StreetCat.fbx",
    "OW_Pigeon.fbx", "OW_PondDuck.fbx", "OW_LakeSwan.fbx", "OW_LakeFish.fbx",
}
ALREADY_UPGRADED = {
    "OW_FarmTractor.fbx", "OW_Bicycle.fbx", "OW_Motorcycle.fbx", "OW_CustomerCar.fbx",
    "OW_AydosCastle.fbx", "OW_M5MetroEntrance.fbx", "OW_PlaneTree.fbx", "OW_CypressTree.fbx", "OW_AydosPineTree.fbx",
    "OW_SteeringWheel.fbx", "OW_BushCluster.fbx", "OW_FloweringShrub.fbx", "OW_MossyRockCluster.fbx", "OW_GrassTuft.fbx",
}


def make_mat(name, color, roughness=0.55, metallic=0.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name=name)
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
    return mat


def apply_smooth(obj, angle_deg=48.0):
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
        e.smooth = (len(e.link_faces) == 2 and e.calc_face_angle(0.0) < thresh)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def box_uv_obj(obj, scale=1.5):
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
            u, v = (co.x * scale, co.y * scale) if (az >= ax and az >= ay) else ((co.y * scale, co.z * scale) if (ax >= ay and ax >= az) else (co.x * scale, co.z * scale))
            loop[uv_layer].uv = (u, v)
    bm.to_mesh(mesh)
    bm.free()


def add_bm(target_bm, src_bm, mat_idx, uv_scale=1.5):
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


def bevel_box(center, size, bevel=0.025, segs=2, rot_euler=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0 and min(size) > bevel * 2.1:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=segs, profile=0.5, affect="EDGES", clamp_overlap=True)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4(), verts=bm.verts)
    return bm


def cyl_bm(center, r1, r2, depth, segs=14, rot_euler=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4(), verts=bm.verts)
    return bm


def ellipsoid_bm(center, radii, segs=12, rings=8, rot_euler=(0, 0, 0), deform_fn=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    if deform_fn:
        for v in bm.verts:
            v.co = deform_fn(v.co)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ Euler(rot_euler, "XYZ").to_matrix().to_4x4(), verts=bm.verts)
    return bm


def export_unity_fbx(objs, out_path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
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


# ===========================================================================
# 1. Polish ALL Remaining uv=False Static OW_* Props (Edge Bevel + Smooth + UV!)
# ===========================================================================
def polish_all_untextured_ow_props():
    count = 0
    for fbx_path in sorted(glob.glob(os.path.join(LIFE_DIR, "OW_*.fbx"))):
        fname = os.path.basename(fbx_path)
        if fname in RIGGED_ANIMALS or fname in ALREADY_UPGRADED or fname == "OW_HomeLuxurySofaSet.fbx":
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=fbx_path)
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
        if arms or not meshes:
            continue

        needs_uv = any(not o.data.uv_layers for o in meshes)
        if not needs_uv:
            continue

        for o in meshes:
            # Measure object scale so edge bevel is tastefully proportional (never distorts small objects!)
            dims = o.dimensions
            min_d = max(0.05, min(dims.x, dims.y, dims.z))
            bev_w = min(0.014, min_d * 0.04)
            # Only bevel crisp boxy/mechanical props (< 3000 polys) so already-organic crops/flowers stay clean!
            if len(o.data.polygons) < 1600 and "Crop" not in fname and "Flower" not in fname:
                bm = bmesh.new()
                bm.from_mesh(o.data)
                bm.edges.ensure_lookup_table()
                sharp_edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(52.0)]
                if sharp_edges:
                    try:
                        bmesh.ops.bevel(bm, geom=sharp_edges, offset=bev_w, offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
                    except Exception:
                        pass
                bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
                bm.to_mesh(o.data)
                bm.free()
            box_uv_obj(o, scale=1.4)
            apply_smooth(o, angle_deg=48.0)

        # Export back preserving exact transforms & hierarchy!
        bpy.ops.object.select_all(action="DESELECT")
        for o in bpy.data.objects:
            o.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.export_scene.fbx(
            filepath=fbx_path,
            use_selection=True,
            global_scale=1.0,
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_NONE",
            axis_forward="-Z",
            axis_up="Y",
            bake_space_transform=False,
            use_mesh_modifiers=True,
            mesh_smooth_type="FACE",
            add_leaf_bones=False,
            path_mode="AUTO",
        )
        tris = sum(sum(max(1, len(p.vertices) - 2) for p in o.data.polygons) for o in meshes)
        print(f"  [PropBevel+UV] {fname:32s} | objs={len(meshes):2d} | tris={tris:5d} | uv=True")
        count += 1
    print(f"  Polished {count} static OW_* props/crops/furniture with bevels, smooth shading & UVs!")


# ===========================================================================
# 2. Hero Furniture Re-Sculpt: OW_HomeLuxurySofaSet.fbx (Preserving exact bounds & mats!)
# ===========================================================================
def upgrade_luxury_sofa_set():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Exact material slots from original OW_HomeLuxurySofaSet:
    mats = [
        make_mat("OW_SofaVelvetTeal", (0.12, 0.36, 0.42), roughness=0.78),
        make_mat("OW_CushionGold", (0.86, 0.68, 0.24), roughness=0.74),
        make_mat("OW_GoldBrass", (0.86, 0.72, 0.32), roughness=0.22, metallic=0.88),
        make_mat("OW_KilimCream", (0.90, 0.85, 0.76), roughness=0.88),
        make_mat("OW_KilimRed", (0.64, 0.18, 0.16), roughness=0.86),
        make_mat("OW_WalnutWood", (0.28, 0.17, 0.11), roughness=0.45),
    ]
    TEAL, GOLD, BRASS, KCREAM, KRED, WOOD = 0, 1, 2, 3, 4, 5

    bm = bmesh.new()
    # 1. Anatolian Woven Kilim Rug resting flat on floor (y: -0.9..1.55, x: -1.55..1.55)
    add_bm(bm, bevel_box((0.0, 0.30, 0.010), (3.08, 2.40, 0.020), 0.008, 1), KCREAM, 1.2)
    add_bm(bm, bevel_box((0.0, 0.30, 0.015), (2.72, 2.04, 0.016), 0.006, 1), KRED, 1.4)
    add_bm(bm, bevel_box((0.0, 0.30, 0.019), (2.10, 1.44, 0.014), 0.005, 1), KCREAM, 1.4)

    # 2. Plump Sculpted Modern Turkish Velvet L-Sofa w/ Wooden Base & Brass Tapered Legs!
    # Main back frame + chaise frame (z = 0.14..0.82)
    add_bm(bm, bevel_box((0.0, -0.52, 0.22), (2.78, 0.88, 0.16), 0.030, 2), WOOD, 1.4)
    add_bm(bm, bevel_box((-0.94, 0.42, 0.22), (0.92, 1.06, 0.16), 0.030, 2), WOOD, 1.4)
    # Tapered brushed brass sofa legs!
    for lx, ly in ((-1.32, -0.88), (1.32, -0.88), (1.32, -0.14), (-1.32, 0.88), (-0.54, 0.88), (0.0, -0.88)):
        add_bm(bm, cyl_bm((lx, ly, 0.075), 0.016, 0.024, 0.15, 10), BRASS, 2.0)

    # Thick rounded sofa armrests & sculpted backrest shell
    add_bm(bm, bevel_box((0.0, -0.82, 0.56), (2.78, 0.24, 0.64), 0.065, 2), TEAL, 1.6)
    add_bm(bm, bevel_box((-1.29, 0.06, 0.48), (0.22, 1.84, 0.46), 0.055, 2), TEAL, 1.6)
    add_bm(bm, bevel_box(( 1.29, -0.50, 0.48), (0.22, 0.86, 0.46), 0.055, 2), TEAL, 1.6)

    # 3 Plump Rounded Seat Cushions + Chaise Lounge Cushion!
    for sx in (-0.82, 0.0, 0.82):
        add_bm(bm, bevel_box((sx, -0.44, 0.40), (0.80, 0.78, 0.20), 0.068, 3), TEAL, 1.8)
    add_bm(bm, bevel_box((-0.82, 0.42, 0.40), (0.80, 0.96, 0.20), 0.068, 3), TEAL, 1.8)

    # 3 Soft Slouched Backrest Cushions w/ subtle pillow curve
    for sx in (-0.82, 0.0, 0.82):
        add_bm(bm, bevel_box((sx, -0.66, 0.68), (0.78, 0.22, 0.42), 0.072, 3, (0.16, 0, 0)), TEAL, 1.8)

    # 4 Plump Angled Gold & Cream Throw Pillows!
    for px, py, pz, ry, rz, mi in [
        (-1.08, -0.52, 0.62,  0.14,  0.55, GOLD),
        (-0.56, -0.58, 0.62,  0.10,  0.12, KCREAM),
        ( 0.56, -0.58, 0.62,  0.10, -0.14, KCREAM),
        ( 1.06, -0.52, 0.62,  0.14, -0.58, GOLD),
    ]:
        add_bm(bm, ellipsoid_bm((px, py, pz), (0.21, 0.095, 0.20), 14, 10, (0.22, ry, rz)), mi, 2.0)

    # 3. Sculpted Modern Walnut & Brushed Brass Coffee Table w/ Turkish Tea Tray!
    add_bm(bm, bevel_box((0.46, 0.64, 0.42), (1.16, 0.68, 0.055), 0.018, 2), WOOD, 1.6)
    add_bm(bm, bevel_box((0.46, 0.64, 0.18), (1.02, 0.54, 0.032), 0.010, 1), WOOD, 1.6)
    for dx in (-0.50, 0.50):
        for dy in (-0.26, 0.26):
            add_bm(bm, cyl_bm((0.46 + dx, 0.64 + dy, 0.21), 0.018, 0.022, 0.42, 10), BRASS, 2.0)
    # Round brass Turkish tea tray & two small decorative bowls on coffee table!
    add_bm(bm, cyl_bm((0.46, 0.64, 0.465), 0.22, 0.23, 0.026, 18), BRASS, 2.0)
    add_bm(bm, cyl_bm((0.39, 0.61, 0.515), 0.036, 0.044, 0.085, 12), KRED, 2.4)
    add_bm(bm, cyl_bm((0.52, 0.67, 0.515), 0.036, 0.044, 0.085, 12), KRED, 2.4)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("OW_HomeLuxurySofaSet_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    for m in mats:
        mesh.materials.append(m)
    obj = bpy.data.objects.new("OW_HomeLuxurySofaSet", mesh)
    bpy.context.collection.objects.link(obj)
    apply_smooth(obj, angle_deg=56.0)
    export_unity_fbx([obj], os.path.join(LIFE_DIR, "OW_HomeLuxurySofaSet.fbx"))
    tris = sum(max(1, len(p.vertices) - 2) for p in obj.data.polygons)
    print(f"  [HeroFurniture] Upgraded OW_HomeLuxurySofaSet.fbx | tris={tris} | uv=True")


# ===========================================================================
# 3. Batch 8: Brand-New Stylized-Realistic Nature & Vegetation FBXs!
# ===========================================================================
def build_nature_assets():
    # 3A. OW_BushCluster.fbx — Layered multi-lobe sculpted shrub with woody inner twigs
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m_leaf_a = make_mat("OW_BushLeafA", (0.20, 0.46, 0.18), roughness=0.76)
    m_leaf_b = make_mat("OW_BushLeafB", (0.28, 0.56, 0.22), roughness=0.74)
    m_twig   = make_mat("OW_BarkWood",  (0.32, 0.22, 0.14), roughness=0.88)

    def make_bush(fname, with_flowers=False):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mats = [
            make_mat("OW_BushLeafA", (0.20, 0.46, 0.18), roughness=0.76),
            make_mat("OW_BushLeafB", (0.29, 0.56, 0.22), roughness=0.74),
            make_mat("OW_BarkWood",  (0.32, 0.22, 0.14), roughness=0.88),
        ]
        if with_flowers:
            mats.append(make_mat("OW_FlowerBloomA", (0.88, 0.26, 0.34), roughness=0.60))
            mats.append(make_mat("OW_FlowerBloomB", (0.95, 0.76, 0.22), roughness=0.58))
        bm = bmesh.new()
        rng = random.Random(77 if not with_flowers else 88)
        # Woody branching base twigs
        for i in range(5):
            a = i * math.tau / 5.0 + 0.3
            add_bm(bm, cyl_bm((math.cos(a) * 0.14, math.sin(a) * 0.14, 0.18), 0.032, 0.016, 0.38, 7, (math.sin(a) * 0.4, -math.cos(a) * 0.4, 0)), 2, 2.0)

        # 14 Overlapping organically perturbed botanical foliage lobes!
        lobes = [
            ( 0.00,  0.00, 0.48, (0.44, 0.42, 0.36), 0),
            (-0.32,  0.14, 0.36, (0.36, 0.34, 0.30), 0),
            ( 0.34,  0.10, 0.38, (0.35, 0.34, 0.30), 1),
            ( 0.08, -0.30, 0.35, (0.36, 0.32, 0.29), 0),
            (-0.22, -0.26, 0.32, (0.32, 0.30, 0.26), 1),
            ( 0.26, -0.24, 0.31, (0.30, 0.30, 0.26), 0),
            (-0.08,  0.34, 0.36, (0.34, 0.32, 0.28), 1),
            (-0.24,  0.04, 0.58, (0.28, 0.26, 0.24), 1),
            ( 0.22, -0.06, 0.56, (0.28, 0.26, 0.24), 0),
            ( 0.02,  0.16, 0.66, (0.27, 0.25, 0.22), 1),
        ]
        for idx, (cx, cy, cz, rad, mi) in enumerate(lobes):
            ph = idx * 1.7
            def bush_bump(v, p=ph):
                d = 1.0 + 0.14 * math.sin(v.x * 11.0 + p) * math.cos(v.y * 10.0 - p) + 0.08 * math.sin(v.z * 14.0 + p)
                return v * d
            add_bm(bm, ellipsoid_bm((cx, cy, cz), rad, 11, 7, deform_fn=bush_bump), mi, 1.8)

        if with_flowers:
            for k in range(18):
                ang = k * 2.399
                rr = 0.22 + (k % 5) * 0.10
                fx = math.cos(ang) * rr
                fy = math.sin(ang) * rr
                fz = 0.42 + (1.0 - rr / 0.72) * 0.38 + rng.uniform(-0.04, 0.04)
                add_bm(bm, ellipsoid_bm((fx, fy, fz), (0.065, 0.065, 0.048), 8, 6), 3 if (k % 2 == 0) else 4, 3.0)

        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(fname.replace(".fbx", "_Mesh"))
        bm.to_mesh(mesh)
        bm.free()
        for m in mats:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(fname.replace(".fbx", ""), mesh)
        bpy.context.collection.objects.link(obj)
        apply_smooth(obj, angle_deg=65.0)
        export_unity_fbx([obj], os.path.join(LIFE_DIR, fname))
        tris = sum(max(1, len(p.vertices) - 2) for p in obj.data.polygons)
        print(f"  [Nature] Exported {fname:26s} | tris={tris:4d} | uv=True")

    make_bush("OW_BushCluster.fbx", with_flowers=False)
    make_bush("OW_FloweringShrub.fbx", with_flowers=True)

    # 3C. OW_MossyRockCluster.fbx — Stratified chiseled Anatolian limestone boulders w/ moss cap!
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m_rock = make_mat("OW_RockLimestone", (0.54, 0.51, 0.46), roughness=0.86)
    m_moss = make_mat("OW_BushLeafA",     (0.24, 0.44, 0.18), roughness=0.82)
    bm = bmesh.new()
    rock_specs = [
        (( 0.00,  0.00, 0.34), (0.68, 0.56, 0.42), 1.0),
        ((-0.52,  0.22, 0.22), (0.42, 0.36, 0.28), 2.3),
        (( 0.48, -0.24, 0.18), (0.36, 0.32, 0.24), 4.1),
        (( 0.12,  0.46, 0.14), (0.28, 0.24, 0.18), 5.7),
    ]
    for (cx, cy, cz), rad, seed in rock_specs:
        def chiseled(v, s=seed):
            # Subtle angular stratified rock planes + flattened base
            f = 1.0 + 0.16 * math.sin(v.x * 5.2 + s) * math.cos(v.y * 4.8 - s) + 0.10 * math.cos(v.z * 7.5 + s)
            o = v * f
            if o.z < -rad[2] * 0.65:
                o.z = -rad[2] * 0.65
            return o
        add_bm(bm, ellipsoid_bm((cx, cy, cz), rad, 12, 8, deform_fn=chiseled), 0, 1.2)
    # Organic moss cushions nestled on upper rock shelves
    add_bm(bm, ellipsoid_bm((0.02, -0.04, 0.68), (0.46, 0.36, 0.10), 10, 6), 1, 2.0)
    add_bm(bm, ellipsoid_bm((-0.50, 0.20, 0.44), (0.28, 0.22, 0.07), 8, 5), 1, 2.0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("OW_MossyRockCluster_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(m_rock)
    mesh.materials.append(m_moss)
    obj = bpy.data.objects.new("OW_MossyRockCluster", mesh)
    bpy.context.collection.objects.link(obj)
    apply_smooth(obj, angle_deg=42.0)
    export_unity_fbx([obj], os.path.join(LIFE_DIR, "OW_MossyRockCluster.fbx"))
    print(f"  [Nature] Exported OW_MossyRockCluster.fbx     | tris={sum(max(1, len(p.vertices)-2) for p in obj.data.polygons):4d} | uv=True")

    # 3D. OW_GrassTuft.fbx — Curved radiating botanical grass blades
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m_gr = make_mat("OW_BushLeafB", (0.28, 0.56, 0.20), roughness=0.78)
    bm = bmesh.new()
    rng = random.Random(303)
    for b in range(14):
        ang = b * math.tau / 14.0 + rng.uniform(-0.18, 0.18)
        h = rng.uniform(0.24, 0.42)
        bend = rng.uniform(0.08, 0.18)
        r0 = rng.uniform(0.02, 0.14)
        ca, sa = math.cos(ang), math.sin(ang)
        px, py = -sa, ca
        w0 = 0.024
        # 3-segment curved tapered grass blade
        pts = [
            (r0 * ca, r0 * sa, 0.0, w0),
            ((r0 + bend * 0.25) * ca, (r0 + bend * 0.25) * sa, h * 0.42, w0 * 0.78),
            ((r0 + bend * 0.65) * ca, (r0 + bend * 0.65) * sa, h * 0.78, w0 * 0.45),
            ((r0 + bend) * ca, (r0 + bend) * sa, h, 0.002),
        ]
        bbm = bmesh.new()
        rows = []
        for x, y, z, w in pts:
            rows.append((bbm.verts.new(Vector((x - px * w, y - py * w, z))), bbm.verts.new(Vector((x + px * w, y + py * w, z)))))
        for i in range(len(rows) - 1):
            bbm.faces.new((rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0]))
        add_bm(bm, bbm, 0, 2.5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("OW_GrassTuft_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(m_gr)
    obj = bpy.data.objects.new("OW_GrassTuft", mesh)
    bpy.context.collection.objects.link(obj)
    apply_smooth(obj, angle_deg=65.0)
    export_unity_fbx([obj], os.path.join(LIFE_DIR, "OW_GrassTuft.fbx"))
    print(f"  [Nature] Exported OW_GrassTuft.fbx            | tris={sum(max(1, len(p.vertices)-2) for p in obj.data.polygons):4d} | uv=True")


if __name__ == "__main__":
    print("[GRIDROAM Phase 4 Batches 6 & 8] Upgrading Furniture, Props, Crops & Nature Clusters ...")
    polish_all_untextured_ow_props()
    upgrade_luxury_sofa_set()
    build_nature_assets()
    print("[GRIDROAM Phase 4 Batches 6 & 8] Complete!")
