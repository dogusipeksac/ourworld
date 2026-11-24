#!/usr/bin/env python3
"""
Our World — Open-World Life Pack (Phase 1 Upgraded + Phase 2 + Phase 3)
100% Original Commercial-Safe Blender 5.3+ Sculpted & Animation-Pivoted FBX Assets!

Exports 18 smooth-shaded, beveled, multi-material FBX models directly into:
  Assets/GRIDROAM/Resources/OurWorldLife/
so Unity loads them seamlessly at runtime with full articulated pivots:

Phase 1 (Farm Animals & Hero Props):
  1.  OW_Cow.fbx                 (Body, SpotsGroup, HeadPivot, TailPivot, Leg_FL/FR/BL/BR)
  2.  OW_Sheep.fbx               (Body, WoolCoat [shearable!], HeadPivot, TailPivot, Leg_FL/FR/BL/BR)
  3.  OW_Chicken.fbx             (Body, HeadPivot, Wing_L, Wing_R, Leg_L, Leg_R)
  4.  OW_ShepherdDog.fbx         (Body, HeadPivot, TailPivot, Leg_FL/FR/BL/BR)
  5.  OW_PondDuck.fbx            (Body, HeadPivot, Wing_L, Wing_R)
  6.  OW_FarmTractor.fbx         (Chassis, Cab, Exhaust, Wheel_FL/FR/RL/RR)
  7.  OW_Windmill.fbx            (Tower, Roof, Door, WindmillBlades [spinning hub!])

Phase 2 (No:73 Oto Tamir & Car Modification):
  8.  OW_HydraulicCarLift.fbx    (FramePosts, LiftCarriage [raises/lowers!])
  9.  OW_GarageToolStation.fbx   (ToolChest, Compressor, OilDrum, TireStack)
  10. OW_CarSpoilerSport.fbx     (GT Rear Wing Spoiler for Player Sedan!)
  11. OW_CarRoofRack.fbx         (Overland Roof Rack & Camp Box for Player Sedan!)

Phase 3 (Sultanbeyli Lake Fishing, Car Wash & No:48 Home Decor):
  12. OW_FishingRod.fbx          (RodBody, ReelHandle [spinning!], BobberTip)
  13. OW_LakeFish.fbx            (FishBody, TailFinPivot [splashing/wiggling!])
  14. OW_CarWashGantry.fbx       (GantryArch, BrushRollerL, BrushRollerR, TopBrushRoller [all spin!])
  15. OW_HomeLuxurySofaSet.fbx   (SofaLShape, Cushions, CoffeeTable, AnatolianKilim)
  16. OW_HomeMediaAndConsole.fbx (MediaCabinet, SmartTV4K, GameConsole, FloorLamp)
  17. OW_HomeBackyardMangal.fbx  (TimberPergola, CharcoalMangal, BrassSemaver, GardenBenches)
  18. OW_HomeAquariumAndPlant.fbx(AquariumCabinet, WaterGlow, FishSwimmerA, FishSwimmerB, OliveTreePot)
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
PREVIEW_PATH = "/Users/dogusipeksac/.gemini/antigravity/brain/2dd72233-235e-4c47-8c3c-78844f1a7f7f/scratch/blender_life_pack_showcase.png"

os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(BLEND_DIR, exist_ok=True)
os.makedirs(os.path.dirname(PREVIEW_PATH), exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


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


def _join_mesh_parts(name, part_specs, pivot=(0.0, 0.0, 0.0)):
    """
    Build a single smooth-shaded multi-material mesh object whose Blender Origin sits at `pivot`!
    Each entry in part_specs is a dict:
      kind: 'sphere' | 'box' | 'cyl' | 'cone' | 'torus'
      loc: (x, y, z) in world model coords (relative to model origin)
      scale: (sx, sy, sz) or radius/depth params
      rot: (rx, ry, rz) in degrees (optional)
      mat: bpy.types.Material
      bevel: float (optional, for boxes)
      segs: int (optional)
    """
    bm = bmesh.new()
    materials = []
    px, py, pz = pivot

    def mat_idx(m):
        if m not in materials:
            materials.append(m)
        return materials.index(m)

    for sp in part_specs:
        kind = sp["kind"]
        lx, ly, lz = sp.get("loc", (0.0, 0.0, 0.0))
        rx, ry, rz = sp.get("rot", (0.0, 0.0, 0.0))
        m_idx = mat_idx(sp["mat"])
        tmp = bmesh.new()

        if kind == "sphere":
            segs = sp.get("segs", 14)
            rings = sp.get("rings", 10)
            bmesh.ops.create_uvsphere(tmp, u_segments=segs, v_segments=rings, radius=1.0)
            sx, sy, sz = sp.get("scale", (1.0, 1.0, 1.0))
            bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)
        elif kind == "box":
            bmesh.ops.create_cube(tmp, size=1.0)
            sx, sy, sz = sp.get("scale", (1.0, 1.0, 1.0))
            bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)
            bev = sp.get("bevel", 0.0)
            if bev > 0.0:
                bmesh.ops.bevel(
                    tmp,
                    geom=list(tmp.edges),
                    offset=bev,
                    segments=2,
                    affect="EDGES",
                    clamp_overlap=True,
                )
        elif kind == "cyl":
            segs = sp.get("segs", 14)
            r1 = sp.get("r1", sp.get("r", 0.5))
            r2 = sp.get("r2", r1)
            depth = sp.get("depth", 1.0)
            bmesh.ops.create_cone(
                tmp,
                cap_ends=True,
                cap_tris=False,
                segments=segs,
                radius1=r1,
                radius2=r2,
                depth=depth,
            )
        elif kind == "cone":
            segs = sp.get("segs", 14)
            r1 = sp.get("r1", 0.5)
            r2 = sp.get("r2", 0.01)
            depth = sp.get("depth", 1.0)
            bmesh.ops.create_cone(
                tmp,
                cap_ends=True,
                cap_tris=False,
                segments=segs,
                radius1=r1,
                radius2=r2,
                depth=depth,
            )

        rot_mat = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), "XYZ").to_matrix().to_4x4()
        trans_mat = Matrix.Translation((lx - px, ly - py, lz - pz))
        bmesh.ops.transform(tmp, matrix=trans_mat @ rot_mat, verts=tmp.verts)

        # Merge tmp into bm with material assignment
        vert_map = {}
        for v in tmp.verts:
            vert_map[v.index] = bm.verts.new(v.co)
        for f in tmp.faces:
            try:
                nf = bm.faces.new([vert_map[v.index] for v in f.verts])
                nf.material_index = m_idx
                nf.smooth = (kind == "sphere" or kind == "cyl" or kind == "cone")
            except ValueError:
                pass
        tmp.free()

    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    for m in materials:
        mesh.materials.append(m)

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = Vector((px, py, pz))
    return obj


def export_model(objs, filename):
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
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="AUTO",
    )
    tris = sum(sum(max(1, len(p.vertices) - 2) for p in o.data.polygons) for o in objs if o.type == "MESH")
    print(f"  [FBX] {filename:32s} | parts={len(objs):2d} | tris={tris:5d}")
    return out_path


# =============================================================================
# SHARED STYLIZED MATERIALS
# =============================================================================
def palette():
    return {
        "cow_white": mat("OW_CowWhite", (0.95, 0.94, 0.90), rough=0.72),
        "cow_spot": mat("OW_CowSpot", (0.12, 0.11, 0.12), rough=0.76),
        "cow_pink": mat("OW_CowPink", (0.94, 0.66, 0.68), rough=0.58),
        "horn": mat("OW_HornCream", (0.86, 0.78, 0.58), rough=0.45),
        "hoof": mat("OW_HoofDark", (0.16, 0.14, 0.13), rough=0.50),
        "eye": mat("OW_EyeDark", (0.06, 0.06, 0.07), rough=0.12, metal=0.10),
        "brass": mat("OW_GoldBrass", (0.92, 0.74, 0.24), rough=0.26, metal=0.85),
        "wool": mat("OW_SheepWool", (0.97, 0.95, 0.90), rough=0.92),
        "sheep_skin": mat("OW_SheepFace", (0.22, 0.20, 0.21), rough=0.75),
        "sheep_bare": mat("OW_SheepSheared", (0.86, 0.80, 0.72), rough=0.82),
        "hen_body": mat("OW_ChickenFeather", (0.92, 0.60, 0.26), rough=0.78),
        "hen_wing": mat("OW_ChickenWing", (0.82, 0.48, 0.18), rough=0.78),
        "comb_red": mat("OW_CombRed", (0.88, 0.14, 0.14), rough=0.45),
        "beak_gold": mat("OW_BeakYellow", (0.98, 0.76, 0.14), rough=0.42),
        "dog_tan": mat("OW_DogKangal", (0.84, 0.73, 0.55), rough=0.76),
        "dog_mask": mat("OW_DogBlackMask", (0.14, 0.13, 0.13), rough=0.68),
        "collar_red": mat("OW_CollarRed", (0.80, 0.18, 0.16), rough=0.45),
        "duck_white": mat("OW_DuckWhite", (0.96, 0.96, 0.95), rough=0.65),
        "duck_green": mat("OW_DuckEmerald", (0.10, 0.44, 0.28), rough=0.38, metal=0.15),
        "wood_dark": mat("OW_TimberDark", (0.38, 0.23, 0.12), rough=0.72),
        "wood_light": mat("OW_TimberOak", (0.68, 0.49, 0.28), rough=0.62),
        "stone_white": mat("OW_StoneWhite", (0.92, 0.90, 0.85), rough=0.78),
        "roof_tile": mat("OW_RoofTerracotta", (0.66, 0.24, 0.14), rough=0.65),
        "sail_cloth": mat("OW_SailCream", (0.95, 0.92, 0.84), rough=0.82),
        "tractor_red": mat("OW_TractorRed", (0.82, 0.14, 0.12), rough=0.28, metal=0.25),
        "metal_dark": mat("OW_SteelDark", (0.18, 0.19, 0.21), rough=0.38, metal=0.72),
        "metal_chrome": mat("OW_Chrome", (0.86, 0.88, 0.91), rough=0.16, metal=0.92),
        "rubber": mat("OW_TireRubber", (0.12, 0.12, 0.13), rough=0.86),
        "lift_blue": mat("OW_LiftBlue", (0.12, 0.42, 0.78), rough=0.32, metal=0.35),
        "safety_yellow": mat("OW_SafetyYellow", (0.96, 0.74, 0.12), rough=0.34, metal=0.20),
        "tool_red": mat("OW_ToolChestRed", (0.84, 0.16, 0.15), rough=0.30, metal=0.45),
        "carbon": mat("OW_CarbonFiber", (0.14, 0.15, 0.17), rough=0.26, metal=0.40),
        "teal_accent": mat("OW_TealAccent", (0.10, 0.66, 0.62), rough=0.30, metal=0.25),
        "brush_cyan": mat("OW_WashBrushCyan", (0.18, 0.78, 0.92), rough=0.65),
        "cork": mat("OW_CorkHandle", (0.76, 0.56, 0.34), rough=0.84),
        "rod_blue": mat("OW_FiberglassRod", (0.14, 0.48, 0.72), rough=0.25, metal=0.30),
        "bobber_red": mat("OW_BobberRed", (0.94, 0.18, 0.16), rough=0.22),
        "fish_scale": mat("OW_FishScale", (0.64, 0.78, 0.84), rough=0.20, metal=0.48),
        "fish_fin": mat("OW_FishFin", (0.92, 0.56, 0.22), rough=0.42),
        "sofa_teal": mat("OW_SofaVelvetTeal", (0.12, 0.44, 0.46), rough=0.84),
        "cushion_gold": mat("OW_CushionGold", (0.92, 0.72, 0.24), rough=0.80),
        "kilim_red": mat("OW_KilimRed", (0.68, 0.16, 0.16), rough=0.90),
        "kilim_cream": mat("OW_KilimCream", (0.92, 0.86, 0.72), rough=0.90),
        "tv_screen": mat("OW_SmartTVScreen", (0.18, 0.54, 0.82), rough=0.10, emission=1.2),
        "lamp_glow": mat("OW_WarmLampGlow", (1.0, 0.88, 0.62), rough=0.20, emission=2.2),
        "ember_glow": mat("OW_CharcoalEmber", (1.0, 0.36, 0.08), rough=0.45, emission=2.6),
        "aqua_water": mat("OW_AquariumGlow", (0.14, 0.72, 0.90), rough=0.10, emission=0.8),
        "olive_leaf": mat("OW_OliveLeaf", (0.28, 0.50, 0.24), rough=0.72),
        "terracotta_pot": mat("OW_TerracottaPot", (0.76, 0.38, 0.22), rough=0.68),
        "headlight_glow": mat("OW_HeadlightGlow", (1.0, 0.96, 0.82), rough=0.10, emission=2.0),
    }


# =============================================================================
# 1. OW_Cow.fbx (Facing +Y in Blender -> +Z in Unity!)
# =============================================================================
def build_cow(P):
    objs = []
    # Main torso + rump + chest + udder + collar bell
    objs.append(_join_mesh_parts("Body", [
        {"kind": "sphere", "loc": (0.0, 0.0, 1.06), "scale": (0.44, 0.86, 0.44), "mat": P["cow_white"], "segs": 16, "rings": 12},
        {"kind": "sphere", "loc": (0.0, 0.50, 1.08), "scale": (0.38, 0.38, 0.40), "mat": P["cow_white"]},
        {"kind": "sphere", "loc": (0.0, -0.56, 1.04), "scale": (0.39, 0.36, 0.39), "mat": P["cow_white"]},
        {"kind": "sphere", "loc": (0.0, -0.34, 0.68), "scale": (0.22, 0.24, 0.16), "mat": P["cow_pink"]},
        {"kind": "cyl", "loc": (0.0, 0.68, 1.12), "r1": 0.26, "r2": 0.24, "depth": 0.10, "rot": (55, 0, 0), "mat": P["collar_red"]},
        {"kind": "cone", "loc": (0.0, 0.82, 0.88), "r1": 0.09, "r2": 0.05, "depth": 0.14, "mat": P["brass"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # Sculpted organic Holstein spots wrapped closely around torso
    objs.append(_join_mesh_parts("SpotsGroup", [
        {"kind": "sphere", "loc": (0.31, 0.16, 1.14), "scale": (0.16, 0.34, 0.24), "mat": P["cow_spot"]},
        {"kind": "sphere", "loc": (-0.30, -0.22, 1.12), "scale": (0.17, 0.32, 0.26), "mat": P["cow_spot"]},
        {"kind": "sphere", "loc": (0.20, -0.52, 1.18), "scale": (0.22, 0.24, 0.20), "mat": P["cow_spot"]},
        {"kind": "sphere", "loc": (-0.22, 0.38, 1.20), "scale": (0.20, 0.22, 0.18), "mat": P["cow_spot"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # Articulated HeadPivot at neck base (0, 0.72, 1.24)
    objs.append(_join_mesh_parts("HeadPivot", [
        {"kind": "cyl", "loc": (0.0, 0.84, 1.28), "r1": 0.21, "r2": 0.18, "depth": 0.36, "rot": (62, 0, 0), "mat": P["cow_white"]},
        {"kind": "sphere", "loc": (0.0, 1.06, 1.38), "scale": (0.21, 0.26, 0.22), "mat": P["cow_white"]},
        {"kind": "sphere", "loc": (0.0, 1.28, 1.30), "scale": (0.19, 0.15, 0.14), "mat": P["cow_pink"]},
        {"kind": "sphere", "loc": (-0.07, 1.40, 1.32), "scale": (0.032, 0.025, 0.025), "mat": P["hoof"]},
        {"kind": "sphere", "loc": (0.07, 1.40, 1.32), "scale": (0.032, 0.025, 0.025), "mat": P["hoof"]},
        {"kind": "sphere", "loc": (-0.16, 1.16, 1.44), "scale": (0.042, 0.042, 0.042), "mat": P["eye"]},
        {"kind": "sphere", "loc": (0.16, 1.16, 1.44), "scale": (0.042, 0.042, 0.042), "mat": P["eye"]},
        # Ears
        {"kind": "sphere", "loc": (-0.26, 1.00, 1.46), "scale": (0.13, 0.06, 0.055), "rot": (0, -18, 12), "mat": P["cow_spot"]},
        {"kind": "sphere", "loc": (0.26, 1.00, 1.46), "scale": (0.13, 0.06, 0.055), "rot": (0, 18, -12), "mat": P["cow_white"]},
        # Curved horns
        {"kind": "cone", "loc": (-0.16, 1.02, 1.62), "r1": 0.048, "r2": 0.012, "depth": 0.24, "rot": (12, 24, 0), "mat": P["horn"]},
        {"kind": "cone", "loc": (0.16, 1.02, 1.62), "r1": 0.048, "r2": 0.012, "depth": 0.24, "rot": (12, -24, 0), "mat": P["horn"]},
    ], pivot=(0.0, 0.72, 1.24)))

    # Articulated TailPivot at rump (0, -0.84, 1.18)
    objs.append(_join_mesh_parts("TailPivot", [
        {"kind": "cyl", "loc": (0.0, -0.92, 0.94), "r1": 0.028, "r2": 0.036, "depth": 0.56, "rot": (-14, 0, 0), "mat": P["cow_white"]},
        {"kind": "sphere", "loc": (0.0, -0.98, 0.62), "scale": (0.065, 0.065, 0.11), "mat": P["cow_spot"]},
    ], pivot=(0.0, -0.84, 1.18)))

    # 4 Articulated Legs pivoted at hips/shoulders (z = 0.84) with rounded hip/shoulder socket spheres!
    leg_coords = [
        ("Leg_FL", -0.23, 0.46),
        ("Leg_FR",  0.23, 0.46),
        ("Leg_BL", -0.23, -0.46),
        ("Leg_BR",  0.23, -0.46),
    ]
    for lname, lx, ly in leg_coords:
        objs.append(_join_mesh_parts(lname, [
            {"kind": "sphere", "loc": (lx, ly, 0.84), "scale": (0.12, 0.13, 0.14), "mat": P["cow_white"], "segs": 12, "rings": 8},
            {"kind": "cyl", "loc": (lx, ly, 0.46), "r1": 0.074, "r2": 0.105, "depth": 0.76, "mat": P["cow_white"]},
            {"kind": "cyl", "loc": (lx, ly, 0.07), "r1": 0.088, "r2": 0.078, "depth": 0.14, "mat": P["hoof"]},
        ], pivot=(lx, ly, 0.84)))
    return objs


# =============================================================================
# 2. OW_Sheep.fbx (with Shearable WoolCoat!)
# =============================================================================
def build_sheep(P):
    objs = []
    # Bare sheared body underneath
    objs.append(_join_mesh_parts("Body", [
        {"kind": "sphere", "loc": (0.0, 0.0, 0.64), "scale": (0.28, 0.46, 0.26), "mat": P["sheep_bare"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # Lush sculpted multi-cloud WoolCoat (scales smoothly to 0 when sheared!)
    wool_parts = [
        {"kind": "sphere", "loc": (0.0, 0.0, 0.66), "scale": (0.40, 0.54, 0.36), "mat": P["wool"], "segs": 14, "rings": 10},
        {"kind": "sphere", "loc": (0.0, 0.34, 0.72), "scale": (0.31, 0.28, 0.29), "mat": P["wool"]},
        {"kind": "sphere", "loc": (0.0, -0.34, 0.68), "scale": (0.33, 0.28, 0.30), "mat": P["wool"]},
        {"kind": "sphere", "loc": (-0.24, 0.10, 0.72), "scale": (0.24, 0.28, 0.24), "mat": P["wool"]},
        {"kind": "sphere", "loc": (0.24, 0.10, 0.72), "scale": (0.24, 0.28, 0.24), "mat": P["wool"]},
        {"kind": "sphere", "loc": (-0.22, -0.18, 0.68), "scale": (0.24, 0.25, 0.24), "mat": P["wool"]},
        {"kind": "sphere", "loc": (0.22, -0.18, 0.68), "scale": (0.24, 0.25, 0.24), "mat": P["wool"]},
        {"kind": "sphere", "loc": (0.0, 0.02, 0.88), "scale": (0.30, 0.36, 0.20), "mat": P["wool"]},
    ]
    objs.append(_join_mesh_parts("WoolCoat", wool_parts, pivot=(0.0, 0.0, 0.66)))

    # Articulated HeadPivot
    objs.append(_join_mesh_parts("HeadPivot", [
        {"kind": "sphere", "loc": (0.0, 0.60, 0.78), "scale": (0.145, 0.20, 0.155), "mat": P["sheep_skin"]},
        {"kind": "sphere", "loc": (0.0, 0.54, 0.90), "scale": (0.14, 0.14, 0.10), "mat": P["wool"]},
        {"kind": "sphere", "loc": (-0.18, 0.56, 0.80), "scale": (0.095, 0.055, 0.04), "rot": (0, -28, 18), "mat": P["sheep_skin"]},
        {"kind": "sphere", "loc": (0.18, 0.56, 0.80), "scale": (0.095, 0.055, 0.04), "rot": (0, 28, -18), "mat": P["sheep_skin"]},
        {"kind": "sphere", "loc": (-0.11, 0.68, 0.82), "scale": (0.028, 0.028, 0.028), "mat": P["eye"]},
        {"kind": "sphere", "loc": (0.11, 0.68, 0.82), "scale": (0.028, 0.028, 0.028), "mat": P["eye"]},
    ], pivot=(0.0, 0.42, 0.74)))

    # Cute fluffy tail
    objs.append(_join_mesh_parts("TailPivot", [
        {"kind": "sphere", "loc": (0.0, -0.58, 0.64), "scale": (0.09, 0.11, 0.09), "mat": P["wool"]},
    ], pivot=(0.0, -0.50, 0.66)))

    for lname, lx, ly in [("Leg_FL", -0.17, 0.28), ("Leg_FR", 0.17, 0.28), ("Leg_BL", -0.17, -0.28), ("Leg_BR", 0.17, -0.28)]:
        objs.append(_join_mesh_parts(lname, [
            {"kind": "cyl", "loc": (lx, ly, 0.25), "r1": 0.048, "r2": 0.062, "depth": 0.46, "mat": P["sheep_skin"]},
            {"kind": "cyl", "loc": (lx, ly, 0.04), "r1": 0.056, "r2": 0.050, "depth": 0.08, "mat": P["hoof"]},
        ], pivot=(lx, ly, 0.48)))
    return objs


# =============================================================================
# 3. OW_Chicken.fbx (with Articulated Wings, Head Pecking & Strutting Legs!)
# =============================================================================
def build_chicken(P):
    objs = []
    objs.append(_join_mesh_parts("Body", [
        {"kind": "sphere", "loc": (0.0, 0.0, 0.30), "scale": (0.16, 0.22, 0.16), "mat": P["hen_body"]},
        {"kind": "sphere", "loc": (0.0, 0.14, 0.35), "scale": (0.12, 0.11, 0.13), "mat": P["hen_body"]},
        # Feathered tail fan
        {"kind": "sphere", "loc": (0.0, -0.21, 0.40), "scale": (0.08, 0.12, 0.14), "rot": (-32, 0, 0), "mat": P["hen_wing"]},
        {"kind": "sphere", "loc": (0.0, -0.24, 0.45), "scale": (0.05, 0.08, 0.12), "rot": (-44, 0, 0), "mat": P["hen_body"]},
    ], pivot=(0.0, 0.0, 0.0)))

    objs.append(_join_mesh_parts("HeadPivot", [
        {"kind": "cyl", "loc": (0.0, 0.18, 0.46), "r1": 0.072, "r2": 0.062, "depth": 0.14, "rot": (12, 0, 0), "mat": P["hen_body"]},
        {"kind": "sphere", "loc": (0.0, 0.20, 0.56), "scale": (0.082, 0.090, 0.082), "mat": P["hen_body"]},
        {"kind": "cone", "loc": (0.0, 0.31, 0.55), "r1": 0.032, "r2": 0.005, "depth": 0.09, "rot": (90, 0, 0), "mat": P["beak_gold"]},
        {"kind": "sphere", "loc": (0.0, 0.20, 0.65), "scale": (0.024, 0.062, 0.044), "mat": P["comb_red"]},
        {"kind": "sphere", "loc": (0.0, 0.26, 0.49), "scale": (0.026, 0.026, 0.036), "mat": P["comb_red"]},
        {"kind": "sphere", "loc": (-0.064, 0.24, 0.58), "scale": (0.018, 0.018, 0.018), "mat": P["eye"]},
        {"kind": "sphere", "loc": (0.064, 0.24, 0.58), "scale": (0.018, 0.018, 0.018), "mat": P["eye"]},
    ], pivot=(0.0, 0.16, 0.40)))

    objs.append(_join_mesh_parts("Wing_L", [
        {"kind": "sphere", "loc": (-0.17, -0.02, 0.32), "scale": (0.052, 0.16, 0.11), "rot": (0, 12, 0), "mat": P["hen_wing"]},
    ], pivot=(-0.14, 0.04, 0.36)))

    objs.append(_join_mesh_parts("Wing_R", [
        {"kind": "sphere", "loc": (0.17, -0.02, 0.32), "scale": (0.052, 0.16, 0.11), "rot": (0, -12, 0), "mat": P["hen_wing"]},
    ], pivot=(0.14, 0.04, 0.36)))

    for lname, lx in [("Leg_L", -0.065), ("Leg_R", 0.065)]:
        objs.append(_join_mesh_parts(lname, [
            {"kind": "cyl", "loc": (lx, 0.0, 0.10), "r1": 0.016, "r2": 0.022, "depth": 0.18, "mat": P["beak_gold"]},
            {"kind": "box", "loc": (lx, 0.03, 0.015), "scale": (0.065, 0.095, 0.022), "mat": P["beak_gold"], "bevel": 0.005},
        ], pivot=(lx, 0.0, 0.19)))
    return objs


# =============================================================================
# 4. OW_ShepherdDog.fbx (Anatolian Kangal "Karabaş"!)
# =============================================================================
def build_shepherd_dog(P):
    objs = []
    objs.append(_join_mesh_parts("Body", [
        {"kind": "sphere", "loc": (0.0, 0.0, 0.56), "scale": (0.21, 0.48, 0.21), "mat": P["dog_tan"]},
        {"kind": "sphere", "loc": (0.0, 0.28, 0.58), "scale": (0.22, 0.22, 0.23), "mat": P["dog_tan"]},
        {"kind": "cyl", "loc": (0.0, 0.38, 0.64), "r1": 0.15, "r2": 0.14, "depth": 0.07, "rot": (50, 0, 0), "mat": P["collar_red"]},
        {"kind": "sphere", "loc": (0.0, 0.46, 0.52), "scale": (0.04, 0.04, 0.04), "mat": P["brass"]},
    ], pivot=(0.0, 0.0, 0.0)))

    objs.append(_join_mesh_parts("HeadPivot", [
        {"kind": "cyl", "loc": (0.0, 0.44, 0.72), "r1": 0.12, "r2": 0.11, "depth": 0.22, "rot": (45, 0, 0), "mat": P["dog_tan"]},
        {"kind": "sphere", "loc": (0.0, 0.58, 0.82), "scale": (0.145, 0.165, 0.145), "mat": P["dog_tan"]},
        {"kind": "sphere", "loc": (0.0, 0.74, 0.76), "scale": (0.095, 0.12, 0.088), "mat": P["dog_mask"]},
        {"kind": "sphere", "loc": (0.0, 0.85, 0.79), "scale": (0.036, 0.032, 0.028), "mat": P["eye"]},
        {"kind": "sphere", "loc": (-0.14, 0.56, 0.82), "scale": (0.048, 0.07, 0.105), "rot": (0, 0, 12), "mat": P["dog_mask"]},
        {"kind": "sphere", "loc": (0.14, 0.56, 0.82), "scale": (0.048, 0.07, 0.105), "rot": (0, 0, -12), "mat": P["dog_mask"]},
        {"kind": "sphere", "loc": (-0.08, 0.68, 0.86), "scale": (0.026, 0.026, 0.026), "mat": P["eye"]},
        {"kind": "sphere", "loc": (0.08, 0.68, 0.86), "scale": (0.026, 0.026, 0.026), "mat": P["eye"]},
    ], pivot=(0.0, 0.36, 0.66)))

    objs.append(_join_mesh_parts("TailPivot", [
        {"kind": "cyl", "loc": (0.0, -0.54, 0.74), "r1": 0.038, "r2": 0.026, "depth": 0.38, "rot": (35, 0, 0), "mat": P["dog_tan"]},
    ], pivot=(0.0, -0.44, 0.62)))

    for lname, lx, ly in [("Leg_FL", -0.14, 0.30), ("Leg_FR", 0.14, 0.30), ("Leg_BL", -0.14, -0.32), ("Leg_BR", 0.14, -0.32)]:
        objs.append(_join_mesh_parts(lname, [
            {"kind": "cyl", "loc": (lx, ly, 0.24), "r1": 0.048, "r2": 0.068, "depth": 0.44, "mat": P["dog_tan"]},
            {"kind": "sphere", "loc": (lx, ly + 0.03, 0.04), "scale": (0.056, 0.074, 0.042), "mat": P["dog_tan"]},
        ], pivot=(lx, ly, 0.46)))
    return objs


# =============================================================================
# 5. OW_PondDuck.fbx
# =============================================================================
def build_pond_duck(P):
    objs = []
    objs.append(_join_mesh_parts("Body", [
        {"kind": "sphere", "loc": (0.0, 0.0, 0.14), "scale": (0.18, 0.29, 0.13), "mat": P["duck_white"]},
        {"kind": "cone", "loc": (0.0, -0.28, 0.20), "r1": 0.09, "r2": 0.02, "depth": 0.18, "rot": (-68, 0, 0), "mat": P["duck_white"]},
    ], pivot=(0.0, 0.0, 0.0)))

    objs.append(_join_mesh_parts("HeadPivot", [
        {"kind": "cyl", "loc": (0.0, 0.20, 0.26), "r1": 0.068, "r2": 0.056, "depth": 0.16, "rot": (16, 0, 0), "mat": P["duck_green"]},
        {"kind": "sphere", "loc": (0.0, 0.24, 0.36), "scale": (0.088, 0.105, 0.088), "mat": P["duck_green"]},
        {"kind": "sphere", "loc": (0.0, 0.38, 0.34), "scale": (0.058, 0.095, 0.032), "mat": P["beak_gold"]},
        {"kind": "sphere", "loc": (-0.068, 0.28, 0.38), "scale": (0.018, 0.018, 0.018), "mat": P["eye"]},
        {"kind": "sphere", "loc": (0.068, 0.28, 0.38), "scale": (0.018, 0.018, 0.018), "mat": P["eye"]},
    ], pivot=(0.0, 0.18, 0.20)))

    objs.append(_join_mesh_parts("Wing_L", [
        {"kind": "sphere", "loc": (-0.17, -0.02, 0.18), "scale": (0.05, 0.19, 0.09), "mat": P["sail_cloth"]},
    ], pivot=(-0.15, 0.04, 0.20)))

    objs.append(_join_mesh_parts("Wing_R", [
        {"kind": "sphere", "loc": (0.17, -0.02, 0.18), "scale": (0.05, 0.19, 0.09), "mat": P["sail_cloth"]},
    ], pivot=(0.15, 0.04, 0.20)))
    return objs


# =============================================================================
# 6. OW_FarmTractor.fbx (Sculpted Vintage Turkish Red Tractor!)
# =============================================================================
def build_farm_tractor(P):
    objs = []
    objs.append(_join_mesh_parts("Chassis", [
        # Main engine hood (beveled)
        {"kind": "box", "loc": (0.0, 0.85, 1.18), "scale": (0.96, 1.72, 0.86), "mat": P["tractor_red"], "bevel": 0.12},
        # Front chrome radiator grille
        {"kind": "box", "loc": (0.0, 1.72, 1.16), "scale": (0.72, 0.08, 0.68), "mat": P["metal_chrome"], "bevel": 0.02},
        # Headlights
        {"kind": "cyl", "loc": (-0.54, 1.54, 1.24), "r1": 0.11, "r2": 0.11, "depth": 0.14, "rot": (90, 0, 0), "mat": P["headlight_glow"]},
        {"kind": "cyl", "loc": (0.54, 1.54, 1.24), "r1": 0.11, "r2": 0.11, "depth": 0.14, "rot": (90, 0, 0), "mat": P["headlight_glow"]},
        # Under-frame & axles
        {"kind": "box", "loc": (0.0, 0.22, 0.66), "scale": (0.68, 2.35, 0.38), "mat": P["metal_dark"], "bevel": 0.04},
        # Rear fenders curving over big tires
        {"kind": "box", "loc": (-0.86, -0.58, 1.48), "scale": (0.44, 1.12, 0.10), "mat": P["tractor_red"], "bevel": 0.03},
        {"kind": "box", "loc": (0.86, -0.58, 1.48), "scale": (0.44, 1.12, 0.10), "mat": P["tractor_red"], "bevel": 0.03},
        # Driver seat & steering column + roll-bar roof
        {"kind": "box", "loc": (0.0, -0.52, 1.32), "scale": (0.58, 0.52, 0.16), "mat": P["rubber"], "bevel": 0.04},
        {"kind": "box", "loc": (0.0, -0.76, 1.56), "scale": (0.56, 0.14, 0.44), "mat": P["rubber"], "bevel": 0.04},
        {"kind": "cyl", "loc": (0.0, 0.02, 1.56), "r1": 0.04, "r2": 0.04, "depth": 0.62, "rot": (-28, 0, 0), "mat": P["metal_dark"]},
        {"kind": "cyl", "loc": (0.0, -0.12, 1.84), "r1": 0.22, "r2": 0.22, "depth": 0.05, "rot": (-28, 0, 0), "mat": P["rubber"]},
        # 4 roll-bar posts + canopy roof
        {"kind": "cyl", "loc": (-0.64, -0.92, 1.98), "r1": 0.045, "r2": 0.045, "depth": 1.18, "mat": P["metal_dark"]},
        {"kind": "cyl", "loc": (0.64, -0.92, 1.98), "r1": 0.045, "r2": 0.045, "depth": 1.18, "mat": P["metal_dark"]},
        {"kind": "cyl", "loc": (-0.64, -0.02, 1.98), "r1": 0.045, "r2": 0.045, "depth": 1.18, "mat": P["metal_dark"]},
        {"kind": "cyl", "loc": (0.64, -0.02, 1.98), "r1": 0.045, "r2": 0.045, "depth": 1.18, "mat": P["metal_dark"]},
        {"kind": "box", "loc": (0.0, -0.46, 2.58), "scale": (1.52, 1.24, 0.12), "mat": P["stone_white"], "bevel": 0.04},
        # Vertical exhaust stack
        {"kind": "cyl", "loc": (0.42, 1.18, 2.02), "r1": 0.055, "r2": 0.062, "depth": 1.18, "mat": P["metal_dark"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # 4 Wheels (deep rear tires + front tires with yellow rims)
    wheels = [
        ("Wheel_RL", -0.88, -0.58, 0.72, 0.72, 0.36),
        ("Wheel_RR",  0.88, -0.58, 0.72, 0.72, 0.36),
        ("Wheel_FL", -0.74,  1.26, 0.44, 0.44, 0.24),
        ("Wheel_FR",  0.74,  1.26, 0.44, 0.44, 0.24),
    ]
    for wname, wx, wy, wz, rad, w in wheels:
        objs.append(_join_mesh_parts(wname, [
            {"kind": "cyl", "loc": (wx, wy, wz), "r1": rad, "r2": rad, "depth": w, "rot": (0, 90, 0), "mat": P["rubber"], "segs": 18},
            {"kind": "cyl", "loc": (wx, wy, wz), "r1": rad * 0.56, "r2": rad * 0.56, "depth": w + 0.04, "rot": (0, 90, 0), "mat": P["safety_yellow"], "segs": 14},
            {"kind": "cyl", "loc": (wx, wy, wz), "r1": rad * 0.20, "r2": rad * 0.20, "depth": w + 0.08, "rot": (0, 90, 0), "mat": P["metal_dark"], "segs": 10},
        ], pivot=(wx, wy, wz)))
    return objs


# =============================================================================
# 7. OW_Windmill.fbx (Sculpted Whitewashed Stone Aydos Windmill + Spinning Blades!)
# =============================================================================
def build_windmill(P):
    objs = []
    # Tapered stone tower + stone base + arched door + windows + conical tiled roof
    objs.append(_join_mesh_parts("Tower", [
        {"kind": "cyl", "loc": (0.0, 0.0, 0.45), "r1": 2.9, "r2": 2.65, "depth": 0.90, "mat": P["wood_dark"], "segs": 16},
        {"kind": "cyl", "loc": (0.0, 0.0, 4.90), "r1": 2.55, "r2": 1.85, "depth": 8.20, "mat": P["stone_white"], "segs": 16},
        {"kind": "cyl", "loc": (0.0, 0.0, 9.15), "r1": 2.15, "r2": 2.15, "depth": 0.32, "mat": P["wood_dark"], "segs": 16},
        {"kind": "cone", "loc": (0.0, 0.0, 10.65), "r1": 2.38, "r2": 0.08, "depth": 2.80, "mat": P["roof_tile"], "segs": 16},
        {"kind": "sphere", "loc": (0.0, 0.0, 12.10), "scale": (0.18, 0.18, 0.18), "mat": P["brass"]},
        # Front door facing +Y (Unity +Z)
        {"kind": "box", "loc": (0.0, 2.42, 1.35), "scale": (1.28, 0.24, 2.20), "mat": P["wood_dark"], "bevel": 0.04},
        {"kind": "box", "loc": (0.0, 2.10, 5.20), "scale": (0.95, 0.24, 1.15), "mat": P["wood_light"], "bevel": 0.03},
        {"kind": "box", "loc": (0.0, 2.16, 5.20), "scale": (0.68, 0.16, 0.85), "mat": P["lamp_glow"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # WindmillBlades pivoted at hub (0, 2.15, 8.35) — spins smoothly around Y (Unity Z!)
    blade_parts = [
        {"kind": "cyl", "loc": (0.0, 2.15, 8.35), "r1": 0.34, "r2": 0.28, "depth": 0.65, "rot": (90, 0, 0), "mat": P["wood_dark"]},
    ]
    for i in range(4):
        ang = i * 90.0
        rad = math.radians(ang)
        cx, cz = math.sin(rad), math.cos(rad)
        # Main spar + canvas sail panel
        blade_parts.append({
            "kind": "box",
            "loc": (cx * 2.55, 2.38, 8.35 + cz * 2.55),
            "scale": (0.20, 0.14, 5.10),
            "rot": (0, ang, 0),
            "mat": P["wood_dark"],
            "bevel": 0.02,
        })
        sx = cx * 2.95 + math.cos(rad) * 0.44
        sz = 8.35 + cz * 2.95 - math.sin(rad) * 0.44
        blade_parts.append({
            "kind": "box",
            "loc": (sx, 2.42, sz),
            "scale": (0.86, 0.06, 3.65),
            "rot": (0, ang, 0),
            "mat": P["sail_cloth"],
            "bevel": 0.01,
        })
    objs.append(_join_mesh_parts("WindmillBlades", blade_parts, pivot=(0.0, 2.15, 8.35)))
    return objs


# =============================================================================
# 8. OW_HydraulicCarLift.fbx (Phase 2 — Two-Post Hydraulic Lift!)
# =============================================================================
def build_hydraulic_lift(P):
    objs = []
    objs.append(_join_mesh_parts("FramePosts", [
        # Base plates
        {"kind": "box", "loc": (-1.65, 0.0, 0.06), "scale": (0.64, 0.78, 0.12), "mat": P["metal_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (1.65, 0.0, 0.06), "scale": (0.64, 0.78, 0.12), "mat": P["metal_dark"], "bevel": 0.02},
        # Tall blue steel hydraulic columns
        {"kind": "box", "loc": (-1.65, 0.0, 1.85), "scale": (0.30, 0.34, 3.55), "mat": P["lift_blue"], "bevel": 0.03},
        {"kind": "box", "loc": (1.65, 0.0, 1.85), "scale": (0.30, 0.34, 3.55), "mat": P["lift_blue"], "bevel": 0.03},
        # Overhead gantry beam with yellow safety stripes
        {"kind": "box", "loc": (0.0, 0.0, 3.58), "scale": (3.65, 0.32, 0.24), "mat": P["lift_blue"], "bevel": 0.03},
        {"kind": "box", "loc": (0.0, 0.0, 3.42), "scale": (3.10, 0.20, 0.08), "mat": P["safety_yellow"]},
        # Hydraulic pump motor box on left column
        {"kind": "box", "loc": (-1.92, 0.14, 1.48), "scale": (0.32, 0.34, 0.56), "mat": P["tool_red"], "bevel": 0.03},
        {"kind": "cyl", "loc": (-1.92, 0.14, 1.98), "r1": 0.11, "r2": 0.11, "depth": 0.44, "mat": P["metal_dark"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # Animated LiftCarriage (moves vertically in Unity Y!)
    objs.append(_join_mesh_parts("LiftCarriage", [
        {"kind": "box", "loc": (-1.42, 0.0, 0.22), "scale": (0.28, 0.46, 0.42), "mat": P["safety_yellow"], "bevel": 0.02},
        {"kind": "box", "loc": (1.42, 0.0, 0.22), "scale": (0.28, 0.46, 0.42), "mat": P["safety_yellow"], "bevel": 0.02},
        # 4 articulated lifting arms + rubber pads
        {"kind": "box", "loc": (-0.98, 0.72, 0.16), "scale": (0.92, 0.16, 0.10), "rot": (0, 0, -32), "mat": P["metal_dark"], "bevel": 0.015},
        {"kind": "box", "loc": (-0.98, -0.72, 0.16), "scale": (0.92, 0.16, 0.10), "rot": (0, 0, 32), "mat": P["metal_dark"], "bevel": 0.015},
        {"kind": "box", "loc": (0.98, 0.72, 0.16), "scale": (0.92, 0.16, 0.10), "rot": (0, 0, 32), "mat": P["metal_dark"], "bevel": 0.015},
        {"kind": "box", "loc": (0.98, -0.72, 0.16), "scale": (0.92, 0.16, 0.10), "rot": (0, 0, -32), "mat": P["metal_dark"], "bevel": 0.015},
        # Lift runners (sleek drive-on ramps)
        {"kind": "box", "loc": (-0.84, 0.0, 0.12), "scale": (0.50, 3.85, 0.10), "mat": P["metal_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (0.84, 0.0, 0.12), "scale": (0.50, 3.85, 0.10), "mat": P["metal_dark"], "bevel": 0.02},
    ], pivot=(0.0, 0.0, 0.0)))
    return objs


# =============================================================================
# 9. OW_GarageToolStation.fbx
# =============================================================================
def build_garage_tool_station(P):
    objs = []
    objs.append(_join_mesh_parts("ToolStation", [
        # Red rolling mechanic cabinet
        {"kind": "box", "loc": (-0.65, 0.0, 0.62), "scale": (1.05, 0.56, 1.05), "mat": P["tool_red"], "bevel": 0.03},
        {"kind": "box", "loc": (-0.65, 0.0, 1.18), "scale": (1.10, 0.60, 0.08), "mat": P["metal_dark"], "bevel": 0.015},
        # Chrome drawer handles
        {"kind": "box", "loc": (-0.65, -0.29, 0.94), "scale": (0.82, 0.04, 0.04), "mat": P["metal_chrome"]},
        {"kind": "box", "loc": (-0.65, -0.29, 0.72), "scale": (0.82, 0.04, 0.04), "mat": P["metal_chrome"]},
        {"kind": "box", "loc": (-0.65, -0.29, 0.48), "scale": (0.82, 0.04, 0.04), "mat": P["metal_chrome"]},
        # Air compressor tank
        {"kind": "cyl", "loc": (0.35, 0.0, 0.44), "r1": 0.24, "r2": 0.24, "depth": 0.76, "rot": (0, 90, 0), "mat": P["lift_blue"]},
        {"kind": "box", "loc": (0.35, 0.0, 0.74), "scale": (0.32, 0.24, 0.24), "mat": P["metal_dark"], "bevel": 0.02},
        # Blue oil drum
        {"kind": "cyl", "loc": (1.10, 0.0, 0.48), "r1": 0.28, "r2": 0.28, "depth": 0.96, "mat": P["teal_accent"]},
        # Stacked spare tires
        {"kind": "cyl", "loc": (-1.52, 0.0, 0.14), "r1": 0.34, "r2": 0.34, "depth": 0.24, "mat": P["rubber"]},
        {"kind": "cyl", "loc": (-1.52, 0.0, 0.40), "r1": 0.34, "r2": 0.34, "depth": 0.24, "mat": P["rubber"]},
        {"kind": "cyl", "loc": (-1.52, 0.0, 0.40), "r1": 0.19, "r2": 0.19, "depth": 0.26, "mat": P["metal_chrome"]},
    ], pivot=(0.0, 0.0, 0.0)))
    return objs


# =============================================================================
# 10 & 11. Car Modification Kits (GT Spoiler & Overland Roof Rack!)
# =============================================================================
def build_car_spoiler(P):
    return [_join_mesh_parts("SpoilerWing", [
        {"kind": "box", "loc": (-0.44, 0.0, 0.14), "scale": (0.06, 0.22, 0.28), "rot": (-14, 0, 0), "mat": P["carbon"], "bevel": 0.01},
        {"kind": "box", "loc": (0.44, 0.0, 0.14), "scale": (0.06, 0.22, 0.28), "rot": (-14, 0, 0), "mat": P["carbon"], "bevel": 0.01},
        {"kind": "box", "loc": (0.0, -0.04, 0.28), "scale": (1.52, 0.28, 0.05), "rot": (-8, 0, 0), "mat": P["carbon"], "bevel": 0.015},
        {"kind": "box", "loc": (-0.76, -0.04, 0.29), "scale": (0.04, 0.34, 0.16), "mat": P["tool_red"], "bevel": 0.008},
        {"kind": "box", "loc": (0.76, -0.04, 0.29), "scale": (0.04, 0.34, 0.16), "mat": P["tool_red"], "bevel": 0.008},
    ], pivot=(0.0, 0.0, 0.0))]


def build_car_roof_rack(P):
    return [_join_mesh_parts("RoofRack", [
        {"kind": "box", "loc": (-0.54, 0.0, 0.06), "scale": (0.07, 1.48, 0.08), "mat": P["metal_dark"], "bevel": 0.015},
        {"kind": "box", "loc": (0.54, 0.0, 0.06), "scale": (0.07, 1.48, 0.08), "mat": P["metal_dark"], "bevel": 0.015},
        {"kind": "box", "loc": (0.0, 0.52, 0.08), "scale": (1.16, 0.07, 0.05), "mat": P["metal_chrome"], "bevel": 0.01},
        {"kind": "box", "loc": (0.0, 0.0, 0.08), "scale": (1.16, 0.07, 0.05), "mat": P["metal_chrome"], "bevel": 0.01},
        {"kind": "box", "loc": (0.0, -0.52, 0.08), "scale": (1.16, 0.07, 0.05), "mat": P["metal_chrome"], "bevel": 0.01},
        # Sleek aerodynamic luggage box + camp roll
        {"kind": "box", "loc": (-0.18, -0.04, 0.24), "scale": (0.62, 1.24, 0.26), "mat": P["carbon"], "bevel": 0.07},
        {"kind": "cyl", "loc": (0.34, -0.06, 0.22), "r1": 0.13, "r2": 0.13, "depth": 0.88, "rot": (90, 0, 0), "mat": P["teal_accent"]},
    ], pivot=(0.0, 0.0, 0.0))]


# =============================================================================
# 12 & 13. Fishing Rod + Animated Lake Fish!
# =============================================================================
def build_fishing_rod(P):
    objs = []
    # Rod pointing along +Y (Unity +Z forward!)
    objs.append(_join_mesh_parts("RodBody", [
        {"kind": "cyl", "loc": (0.0, 0.18, 0.0), "r1": 0.024, "r2": 0.024, "depth": 0.36, "rot": (90, 0, 0), "mat": P["cork"]},
        {"kind": "cyl", "loc": (0.0, 1.15, 0.02), "r1": 0.018, "r2": 0.008, "depth": 1.68, "rot": (88, 0, 0), "mat": P["rod_blue"]},
        {"kind": "cyl", "loc": (0.0, 2.28, 0.10), "r1": 0.008, "r2": 0.004, "depth": 0.72, "rot": (82, 0, 0), "mat": P["safety_yellow"]},
        # Spinning reel drum under cork handle
        {"kind": "cyl", "loc": (0.0, 0.38, -0.08), "r1": 0.055, "r2": 0.055, "depth": 0.08, "rot": (90, 0, 0), "mat": P["metal_chrome"]},
    ], pivot=(0.0, 0.0, 0.0)))

    objs.append(_join_mesh_parts("ReelHandle", [
        {"kind": "box", "loc": (0.08, 0.38, -0.08), "scale": (0.06, 0.018, 0.09), "mat": P["metal_dark"], "bevel": 0.004},
        {"kind": "sphere", "loc": (0.11, 0.38, -0.12), "scale": (0.02, 0.02, 0.02), "mat": P["brass"]},
    ], pivot=(0.06, 0.38, -0.08)))

    objs.append(_join_mesh_parts("BobberTip", [
        {"kind": "sphere", "loc": (0.0, 2.62, 0.16), "scale": (0.048, 0.048, 0.056), "mat": P["bobber_red"]},
        {"kind": "sphere", "loc": (0.0, 2.62, 0.11), "scale": (0.045, 0.045, 0.036), "mat": P["stone_white"]},
    ], pivot=(0.0, 2.62, 0.15)))
    return objs


def build_lake_fish(P):
    objs = []
    objs.append(_join_mesh_parts("FishBody", [
        {"kind": "sphere", "loc": (0.0, 0.0, 0.0), "scale": (0.085, 0.28, 0.125), "mat": P["fish_scale"], "segs": 16, "rings": 10},
        # Dorsal fin + side pectoral fins
        {"kind": "cone", "loc": (0.0, -0.02, 0.15), "r1": 0.09, "r2": 0.015, "depth": 0.12, "rot": (-25, 0, 0), "mat": P["fish_fin"]},
        {"kind": "sphere", "loc": (-0.09, 0.06, -0.03), "scale": (0.025, 0.085, 0.045), "rot": (0, 25, -20), "mat": P["fish_fin"]},
        {"kind": "sphere", "loc": (0.09, 0.06, -0.03), "scale": (0.025, 0.085, 0.045), "rot": (0, -25, 20), "mat": P["fish_fin"]},
        # Eyes
        {"kind": "sphere", "loc": (-0.062, 0.18, 0.03), "scale": (0.024, 0.024, 0.024), "mat": P["eye"]},
        {"kind": "sphere", "loc": (0.062, 0.18, 0.03), "scale": (0.024, 0.024, 0.024), "mat": P["eye"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # Wiggling TailFinPivot!
    objs.append(_join_mesh_parts("TailFinPivot", [
        {"kind": "cone", "loc": (0.0, -0.34, 0.05), "r1": 0.02, "r2": 0.09, "depth": 0.16, "rot": (65, 0, 0), "mat": P["fish_fin"]},
        {"kind": "cone", "loc": (0.0, -0.34, -0.05), "r1": 0.02, "r2": 0.08, "depth": 0.15, "rot": (115, 0, 0), "mat": P["fish_fin"]},
    ], pivot=(0.0, -0.25, 0.0)))
    return objs


# =============================================================================
# 14. OW_CarWashGantry.fbx (Phase 3 — Spinning Brush Car Wash!)
# =============================================================================
def build_car_wash_gantry(P):
    objs = []
    objs.append(_join_mesh_parts("GantryArch", [
        # Side columns + overhead turquoise fascia arch
        {"kind": "box", "loc": (-2.35, 0.0, 1.85), "scale": (0.58, 1.35, 3.70), "mat": P["stone_white"], "bevel": 0.08},
        {"kind": "box", "loc": (2.35, 0.0, 1.85), "scale": (0.58, 1.35, 3.70), "mat": P["stone_white"], "bevel": 0.08},
        {"kind": "box", "loc": (0.0, 0.0, 3.68), "scale": (5.50, 1.55, 0.72), "mat": P["teal_accent"], "bevel": 0.10},
        {"kind": "box", "loc": (0.0, -0.78, 3.68), "scale": (4.60, 0.08, 0.42), "mat": P["lamp_glow"]},
        # Side pressure washer hoses & soap tanks
        {"kind": "cyl", "loc": (-2.82, 0.25, 0.65), "r1": 0.26, "r2": 0.26, "depth": 1.15, "mat": P["lift_blue"]},
        {"kind": "cyl", "loc": (2.82, 0.25, 0.65), "r1": 0.26, "r2": 0.26, "depth": 1.15, "mat": P["safety_yellow"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # 3 Spinning Brush Rollers!
    for rname, rx in [("BrushRollerL", -1.42), ("BrushRollerR", 1.42)]:
        parts = [{"kind": "cyl", "loc": (rx, 0.0, 1.58), "r1": 0.14, "r2": 0.14, "depth": 2.45, "mat": P["metal_chrome"]}]
        for b in range(6):
            parts.append({"kind": "cyl", "loc": (rx, 0.0, 0.56 + b * 0.40), "r1": 0.46, "r2": 0.44, "depth": 0.30, "mat": P["brush_cyan"], "segs": 12})
        objs.append(_join_mesh_parts(rname, parts, pivot=(rx, 0.0, 1.58)))

    top_parts = [{"kind": "cyl", "loc": (0.0, 0.0, 2.74), "r1": 0.12, "r2": 0.12, "depth": 3.40, "rot": (0, 90, 0), "mat": P["metal_chrome"]}]
    for b in range(7):
        top_parts.append({"kind": "cyl", "loc": (-1.35 + b * 0.45, 0.0, 2.74), "r1": 0.42, "r2": 0.42, "depth": 0.32, "rot": (0, 90, 0), "mat": P["brush_cyan"], "segs": 12})
    objs.append(_join_mesh_parts("TopBrushRoller", top_parts, pivot=(0.0, 0.0, 2.74)))
    return objs


# =============================================================================
# 15-18. Phase 3 Home Decor & Furniture Blender Models (No:48 Hero House!)
# =============================================================================
def build_home_luxury_sofa(P):
    return [_join_mesh_parts("LuxurySofaSet", [
        # Woven Anatolian Kilim rug underneath
        {"kind": "box", "loc": (0.0, 0.45, 0.02), "scale": (3.10, 2.20, 0.03), "mat": P["kilim_red"], "bevel": 0.005},
        {"kind": "box", "loc": (0.0, 0.45, 0.036), "scale": (2.50, 1.65, 0.012), "mat": P["kilim_cream"]},
        {"kind": "box", "loc": (0.0, 0.45, 0.044), "scale": (1.60, 0.95, 0.010), "mat": P["teal_accent"]},
        # Modern L-shaped Velvet Teal Sofa
        {"kind": "box", "loc": (0.0, -0.45, 0.28), "scale": (2.55, 0.92, 0.44), "mat": P["sofa_teal"], "bevel": 0.08},
        {"kind": "box", "loc": (0.0, -0.82, 0.68), "scale": (2.55, 0.24, 0.62), "mat": P["sofa_teal"], "bevel": 0.08},
        {"kind": "box", "loc": (-0.96, 0.32, 0.28), "scale": (0.86, 1.15, 0.44), "mat": P["sofa_teal"], "bevel": 0.08},
        {"kind": "box", "loc": (1.26, -0.42, 0.50), "scale": (0.20, 0.92, 0.42), "mat": P["sofa_teal"], "bevel": 0.06},
        # Golden throw cushions
        {"kind": "sphere", "loc": (-0.82, -0.58, 0.64), "scale": (0.22, 0.14, 0.22), "mat": P["cushion_gold"]},
        {"kind": "sphere", "loc": (0.05, -0.58, 0.64), "scale": (0.22, 0.14, 0.22), "mat": P["cushion_gold"]},
        {"kind": "sphere", "loc": (0.88, -0.58, 0.64), "scale": (0.22, 0.14, 0.22), "mat": P["kilim_red"]},
        # Oak coffee table + Turkish tea tray
        {"kind": "box", "loc": (0.36, 0.64, 0.36), "scale": (1.15, 0.64, 0.08), "mat": P["wood_light"], "bevel": 0.025},
        {"kind": "cyl", "loc": (-0.08, 0.42, 0.18), "r1": 0.035, "r2": 0.025, "depth": 0.34, "mat": P["wood_dark"]},
        {"kind": "cyl", "loc": (0.80, 0.42, 0.18), "r1": 0.035, "r2": 0.025, "depth": 0.34, "mat": P["wood_dark"]},
        {"kind": "cyl", "loc": (-0.08, 0.86, 0.18), "r1": 0.035, "r2": 0.025, "depth": 0.34, "mat": P["wood_dark"]},
        {"kind": "cyl", "loc": (0.80, 0.86, 0.18), "r1": 0.035, "r2": 0.025, "depth": 0.34, "mat": P["wood_dark"]},
        {"kind": "cyl", "loc": (0.36, 0.64, 0.42), "r1": 0.20, "r2": 0.20, "depth": 0.03, "mat": P["brass"]},
    ], pivot=(0.0, 0.0, 0.0))]


def build_home_media_console(P):
    return [_join_mesh_parts("MediaAndConsole", [
        # Oak TV sideboard
        {"kind": "box", "loc": (0.0, 0.0, 0.32), "scale": (2.15, 0.50, 0.56), "mat": P["wood_light"], "bevel": 0.03},
        {"kind": "box", "loc": (0.0, 0.24, 0.32), "scale": (1.92, 0.04, 0.38), "mat": P["wood_dark"]},
        # 65-inch Smart 4K TV + slim bezel
        {"kind": "box", "loc": (0.0, -0.05, 1.18), "scale": (1.68, 0.08, 0.96), "mat": P["carbon"], "bevel": 0.02},
        {"kind": "box", "loc": (0.0, 0.00, 1.18), "scale": (1.58, 0.04, 0.86), "mat": P["tv_screen"]},
        {"kind": "box", "loc": (0.0, -0.05, 0.66), "scale": (0.48, 0.26, 0.10), "mat": P["metal_dark"], "bevel": 0.02},
        # Next-gen white/black console + speakers
        {"kind": "box", "loc": (-0.64, 0.04, 0.72), "scale": (0.14, 0.28, 0.34), "mat": P["stone_white"], "bevel": 0.02},
        {"kind": "box", "loc": (-1.22, 0.0, 0.54), "scale": (0.24, 0.26, 1.06), "mat": P["metal_dark"], "bevel": 0.02},
        # Warm glowing floor lamp on right
        {"kind": "cyl", "loc": (1.30, 0.0, 0.04), "r1": 0.22, "r2": 0.22, "depth": 0.06, "mat": P["brass"]},
        {"kind": "cyl", "loc": (1.30, 0.0, 0.88), "r1": 0.028, "r2": 0.028, "depth": 1.66, "mat": P["brass"]},
        {"kind": "cone", "loc": (1.30, 0.0, 1.72), "r1": 0.28, "r2": 0.16, "depth": 0.34, "mat": P["lamp_glow"]},
    ], pivot=(0.0, 0.0, 0.0))]


def build_home_backyard_mangal(P):
    return [_join_mesh_parts("BackyardMangalPergola", [
        # Raised timber deck
        {"kind": "box", "loc": (0.0, 0.0, 0.08), "scale": (4.6, 4.2, 0.16), "mat": P["wood_light"], "bevel": 0.03},
        # 4 corner posts + pergola rafters
        {"kind": "box", "loc": (-2.05, -1.85, 1.48), "scale": (0.18, 0.18, 2.75), "mat": P["wood_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (2.05, -1.85, 1.48), "scale": (0.18, 0.18, 2.75), "mat": P["wood_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (-2.05, 1.85, 1.48), "scale": (0.18, 0.18, 2.75), "mat": P["wood_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (2.05, 1.85, 1.48), "scale": (0.18, 0.18, 2.75), "mat": P["wood_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (0.0, 0.0, 2.86), "scale": (5.0, 4.6, 0.20), "mat": P["roof_tile"], "bevel": 0.04},
        # Charcoal Mangal Grill with glowing embers!
        {"kind": "box", "loc": (-1.15, 0.45, 0.88), "scale": (1.05, 0.58, 0.30), "mat": P["metal_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (-1.15, 0.45, 1.00), "scale": (0.88, 0.44, 0.08), "mat": P["ember_glow"]},
        {"kind": "cyl", "loc": (-1.56, 0.24, 0.42), "r1": 0.03, "r2": 0.03, "depth": 0.72, "mat": P["metal_dark"]},
        {"kind": "cyl", "loc": (-0.74, 0.24, 0.42), "r1": 0.03, "r2": 0.03, "depth": 0.72, "mat": P["metal_dark"]},
        {"kind": "cyl", "loc": (-1.56, 0.66, 0.42), "r1": 0.03, "r2": 0.03, "depth": 0.72, "mat": P["metal_dark"]},
        {"kind": "cyl", "loc": (-0.74, 0.66, 0.42), "r1": 0.03, "r2": 0.03, "depth": 0.72, "mat": P["metal_dark"]},
        # Picnic table + benches + glowing brass Semaver (Turkish tea urn!)
        {"kind": "box", "loc": (0.78, 0.0, 0.76), "scale": (1.55, 1.05, 0.10), "mat": P["wood_light"], "bevel": 0.02},
        {"kind": "box", "loc": (0.78, -0.82, 0.46), "scale": (1.55, 0.42, 0.08), "mat": P["wood_dark"], "bevel": 0.02},
        {"kind": "box", "loc": (0.78, 0.82, 0.46), "scale": (1.55, 0.42, 0.08), "mat": P["wood_dark"], "bevel": 0.02},
        {"kind": "cyl", "loc": (1.12, 0.12, 1.05), "r1": 0.14, "r2": 0.12, "depth": 0.48, "mat": P["brass"]},
        {"kind": "sphere", "loc": (1.12, 0.12, 1.38), "scale": (0.10, 0.10, 0.09), "mat": P["stone_white"]},
    ], pivot=(0.0, 0.0, 0.0))]


def build_home_aquarium_and_plant(P):
    objs = []
    objs.append(_join_mesh_parts("AquariumBase", [
        # Cabinet + glowing blue water tank
        {"kind": "box", "loc": (-0.42, 0.0, 0.42), "scale": (1.16, 0.46, 0.82), "mat": P["wood_dark"], "bevel": 0.03},
        {"kind": "box", "loc": (-0.42, 0.0, 1.12), "scale": (1.08, 0.40, 0.56), "mat": P["aqua_water"], "bevel": 0.02},
        {"kind": "box", "loc": (-0.42, 0.0, 1.44), "scale": (1.14, 0.44, 0.08), "mat": P["carbon"], "bevel": 0.015},
        # Potted Mediterranean Olive Tree next to aquarium
        {"kind": "cyl", "loc": (0.68, 0.0, 0.24), "r1": 0.18, "r2": 0.26, "depth": 0.48, "mat": P["terracotta_pot"]},
        {"kind": "cyl", "loc": (0.68, 0.0, 0.78), "r1": 0.05, "r2": 0.04, "depth": 0.72, "mat": P["wood_dark"]},
        {"kind": "sphere", "loc": (0.68, 0.0, 1.24), "scale": (0.38, 0.38, 0.34), "mat": P["olive_leaf"]},
        {"kind": "sphere", "loc": (0.50, 0.12, 1.38), "scale": (0.24, 0.24, 0.22), "mat": P["olive_leaf"]},
        {"kind": "sphere", "loc": (0.86, -0.10, 1.32), "scale": (0.24, 0.24, 0.22), "mat": P["olive_leaf"]},
    ], pivot=(0.0, 0.0, 0.0)))

    # Two animated mini tropical fish inside the tank!
    objs.append(_join_mesh_parts("FishSwimmerA", [
        {"kind": "sphere", "loc": (-0.42, 0.0, 1.16), "scale": (0.08, 0.03, 0.045), "mat": P["safety_yellow"]},
    ], pivot=(-0.42, 0.0, 1.16)))
    objs.append(_join_mesh_parts("FishSwimmerB", [
        {"kind": "sphere", "loc": (-0.42, 0.0, 1.04), "scale": (0.075, 0.028, 0.042), "mat": P["comb_red"]},
    ], pivot=(-0.42, 0.0, 1.04)))
    return objs


# =============================================================================
# 19. OW_CustomerCar.fbx (Sleek Sculpted Sedan for No:73 Hydraulic Car Lift!)
# =============================================================================
def build_customer_car(P):
    glass = mat("OW_CarGlass", (0.12, 0.22, 0.34), rough=0.12, metal=0.25)
    body = mat("OW_CarBodyPaint", (0.78, 0.16, 0.16), rough=0.22, metal=0.35)
    tail = mat("OW_TaillightRed", (0.94, 0.12, 0.10), rough=0.18, emission=1.4)
    return [_join_mesh_parts("CustomerCarBody", [
        # Sculpted lower chassis + beveled hood & trunk
        {"kind": "box", "loc": (0.0, 0.0, 0.56), "scale": (1.84, 4.22, 0.54), "mat": body, "bevel": 0.11},
        # Tapered upper passenger cabin
        {"kind": "box", "loc": (0.0, -0.18, 1.04), "scale": (1.56, 2.08, 0.52), "mat": body, "bevel": 0.12},
        # Sloped front & rear windshields + side windows
        {"kind": "box", "loc": (0.0, 0.86, 1.01), "scale": (1.42, 0.12, 0.42), "rot": (-24, 0, 0), "mat": glass},
        {"kind": "box", "loc": (0.0, -1.20, 1.01), "scale": (1.42, 0.12, 0.40), "rot": (24, 0, 0), "mat": glass},
        {"kind": "box", "loc": (0.0, -0.18, 1.03), "scale": (1.59, 1.66, 0.34), "mat": glass},
        # Front chrome grille, bumpers, glowing headlights & red taillights
        {"kind": "box", "loc": (0.0, 2.13, 0.36), "scale": (1.80, 0.16, 0.22), "mat": P["metal_dark"], "bevel": 0.03},
        {"kind": "box", "loc": (0.0, -2.13, 0.36), "scale": (1.80, 0.16, 0.22), "mat": P["metal_dark"], "bevel": 0.03},
        {"kind": "box", "loc": (0.0, 2.12, 0.58), "scale": (0.82, 0.06, 0.22), "mat": P["metal_chrome"], "bevel": 0.015},
        {"kind": "box", "loc": (-0.64, 2.11, 0.60), "scale": (0.34, 0.08, 0.18), "mat": P["headlight_glow"]},
        {"kind": "box", "loc": (0.64, 2.11, 0.60), "scale": (0.34, 0.08, 0.18), "mat": P["headlight_glow"]},
        {"kind": "box", "loc": (-0.64, -2.11, 0.62), "scale": (0.36, 0.08, 0.16), "mat": tail},
        {"kind": "box", "loc": (0.64, -2.11, 0.62), "scale": (0.36, 0.08, 0.16), "mat": tail},
        # Side mirrors
        {"kind": "box", "loc": (-0.96, 0.56, 0.90), "scale": (0.18, 0.12, 0.10), "mat": body, "bevel": 0.02},
        {"kind": "box", "loc": (0.96, 0.56, 0.90), "scale": (0.18, 0.12, 0.10), "mat": body, "bevel": 0.02},
        # 4 Alloy Wheels with chrome hubs
        {"kind": "cyl", "loc": (-0.88, 1.28, 0.33), "r1": 0.33, "r2": 0.33, "depth": 0.24, "rot": (0, 90, 0), "mat": P["rubber"], "segs": 18},
        {"kind": "cyl", "loc": (-0.89, 1.28, 0.33), "r1": 0.20, "r2": 0.20, "depth": 0.26, "rot": (0, 90, 0), "mat": P["metal_chrome"], "segs": 14},
        {"kind": "cyl", "loc": (0.88, 1.28, 0.33), "r1": 0.33, "r2": 0.33, "depth": 0.24, "rot": (0, 90, 0), "mat": P["rubber"], "segs": 18},
        {"kind": "cyl", "loc": (0.89, 1.28, 0.33), "r1": 0.20, "r2": 0.20, "depth": 0.26, "rot": (0, 90, 0), "mat": P["metal_chrome"], "segs": 14},
        {"kind": "cyl", "loc": (-0.88, -1.28, 0.33), "r1": 0.33, "r2": 0.33, "depth": 0.24, "rot": (0, 90, 0), "mat": P["rubber"], "segs": 18},
        {"kind": "cyl", "loc": (-0.89, -1.28, 0.33), "r1": 0.20, "r2": 0.20, "depth": 0.26, "rot": (0, 90, 0), "mat": P["metal_chrome"], "segs": 14},
        {"kind": "cyl", "loc": (0.88, -1.28, 0.33), "r1": 0.33, "r2": 0.33, "depth": 0.24, "rot": (0, 90, 0), "mat": P["rubber"], "segs": 18},
        {"kind": "cyl", "loc": (0.89, -1.28, 0.33), "r1": 0.20, "r2": 0.20, "depth": 0.26, "rot": (0, 90, 0), "mat": P["metal_chrome"], "segs": 14},
    ], pivot=(0.0, 0.0, 0.0))]


# =============================================================================
# BUILD ALL 19 FBX FILES + STUDIO SHOWCASE RENDER
# =============================================================================
def main():
    builders = [
        ("OW_Cow.fbx", build_cow),
        ("OW_Sheep.fbx", build_sheep),
        ("OW_Chicken.fbx", build_chicken),
        ("OW_ShepherdDog.fbx", build_shepherd_dog),
        ("OW_PondDuck.fbx", build_pond_duck),
        ("OW_FarmTractor.fbx", build_farm_tractor),
        ("OW_Windmill.fbx", build_windmill),
        ("OW_HydraulicCarLift.fbx", build_hydraulic_lift),
        ("OW_GarageToolStation.fbx", build_garage_tool_station),
        ("OW_CarSpoilerSport.fbx", build_car_spoiler),
        ("OW_CarRoofRack.fbx", build_car_roof_rack),
        ("OW_FishingRod.fbx", build_fishing_rod),
        ("OW_LakeFish.fbx", build_lake_fish),
        ("OW_CarWashGantry.fbx", build_car_wash_gantry),
        ("OW_HomeLuxurySofaSet.fbx", build_home_luxury_sofa),
        ("OW_HomeMediaAndConsole.fbx", build_home_media_console),
        ("OW_HomeBackyardMangal.fbx", build_home_backyard_mangal),
        ("OW_HomeAquariumAndPlant.fbx", build_home_aquarium_and_plant),
        ("OW_CustomerCar.fbx", build_customer_car),
    ]

    print(f"=== Sculpting {len(builders)} OurWorld Life Pack Blender FBX models ===")
    for fname, fn in builders:
        clear_scene()
        P = palette()
        objs = fn(P)
        export_model(objs, fname)

    # Assemble a clean showcase lineup in one scene, save .blend & render PNG!
    clear_scene()
    P = palette()
    # Floor
    _join_mesh_parts("StudioFloor", [
        {"kind": "box", "loc": (0.0, 0.0, -0.05), "scale": (36.0, 26.0, 0.10), "mat": mat("StudioFloorMat", (0.16, 0.20, 0.24), rough=0.80)},
    ])

    showcase_placements = [
        (build_cow, (-9.0, 4.0, 0.0), 150),
        (build_sheep, (-6.2, 4.0, 0.0), 160),
        (build_chicken, (-4.4, 3.6, 0.0), 165),
        (build_shepherd_dog, (-3.0, 3.8, 0.0), 160),
        (build_pond_duck, (-1.6, 3.4, 0.0), 165),
        (build_farm_tractor, (1.2, 4.4, 0.0), 145),
        (build_windmill, (7.0, 6.2, 0.0), 165),
        (build_hydraulic_lift, (-8.5, -2.5, 0.0), 180),
        (build_garage_tool_station, (-4.2, -2.5, 0.0), 180),
        (build_car_wash_gantry, (1.4, -2.5, 0.0), 180),
        (build_home_luxury_sofa, (7.5, -0.5, 0.0), 180),
        (build_home_media_console, (7.5, -4.0, 0.0), 180),
        (build_home_backyard_mangal, (-3.8, -7.8, 0.0), 180),
        (build_home_aquarium_and_plant, (2.2, -7.2, 0.0), 180),
        (build_lake_fish, (5.2, -7.2, 0.6), 90),
    ]
    for fn, pos, yaw in showcase_placements:
        root = bpy.data.objects.new("Wrap_" + fn.__name__, None)
        bpy.context.collection.objects.link(root)
        root.location = Vector(pos)
        root.rotation_euler = Euler((0.0, 0.0, math.radians(yaw)), "XYZ")
        for o in fn(P):
            o.parent = root

    blend_out = os.path.join(BLEND_DIR, "ourworld_life_pack.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_out)
    print(f"Saved source blend: {blend_out}")

    # Studio lighting + camera render
    world = bpy.data.worlds.new("StudioWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.55, 0.72, 0.88, 1.0)
        bg.inputs["Strength"].default_value = 0.7
    sun_d = bpy.data.lights.new("KeySun", type="SUN")
    sun_d.energy = 3.2
    sun = bpy.data.objects.new("KeySun", sun_d)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(48), math.radians(-18), math.radians(32)), "XYZ")

    cam_d = bpy.data.cameras.new("ShowcaseCam")
    cam_d.lens = 38
    cam = bpy.data.objects.new("ShowcaseCam", cam_d)
    bpy.context.collection.objects.link(cam)
    cam.location = Vector((-0.5, -21.5, 13.5))
    direction = Vector((-0.5, -0.5, 1.4)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in dir(bpy.types) else "CYCLES"
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except Exception:
        try:
            scene.render.engine = "BLENDER_EEVEE"
        except Exception:
            scene.render.engine = "CYCLES"
            scene.cycles.samples = 16
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    print(f"Rendered showcase preview: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()
