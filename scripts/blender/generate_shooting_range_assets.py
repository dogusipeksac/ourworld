#!/usr/bin/env python3
"""
Our World — Aydos Shooting Range & Tactical Sporting Firearms Pack (Blender 5.3.0)
Generates 7 original, commercial-safe, multi-part FBX assets into Assets/GRIDROAM/Resources/OurWorldLife/:

  1. OW_Weapon_Pistol.fbx      — 9mm Sport/Tactical Pistol (Part_Slide, Part_Mag, Part_Optic_RedDot, Part_Suppressor, Part_Muzzle, Part_Eject)
  2. OW_Weapon_Shotgun.fbx     — 12-Gauge Trap Pump Shotgun (Part_Pump, Part_Choke, Part_Muzzle, Part_Eject)
  3. OW_Weapon_Carbine.fbx     — 5.56 Tactical Sport Carbine (Part_Mag, Part_Optic_Holo, Part_MuzzleBrake, Part_Laser, Part_Muzzle, Part_Eject)
  4. OW_Weapon_Sniper.fbx      — Precision Bolt-Action Sniper (Part_Bolt, Part_Mag, Part_Scope_8x, Part_Bipod, Part_Muzzle, Part_Eject)
  5. OW_Range_TargetStand.fbx  — Timber & Steel Range Target Frame with Concentric Bullseye Rings
  6. OW_Range_SteelPlate.fbx   — IPSC Reactive Hinged Steel Plate Target (Part_Plate pivot at base hinge)
  7. OW_Range_ClayThrower.fbx  — Mechanical Trap/Skeet Clay Pigeon Launcher (Part_Arm throwing arm)

Coordinate Convention:
  - Weapons: Origin (0,0,0) = Right-hand grip center. +Y (Blender) = Barrel forward (+Z in Unity). +Z (Blender) = Up (+Y in Unity).
  - Range Props: Origin (0,0,0) = Ground center. +Y (Blender) = Facing player (+Z in Unity).
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldLife")
os.makedirs(OUT_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, rgb, rough=0.5, metal=0.0, emission=0.0):
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


def weapon_palette():
    return {
        "gunmetal":     make_mat("GunMetalDark",     (0.14, 0.15, 0.17), rough=0.32, metal=0.88),
        "steel_blued":  make_mat("BluedSteel",       (0.10, 0.12, 0.16), rough=0.25, metal=0.92),
        "steel_bright": make_mat("MachinedSteel",    (0.68, 0.71, 0.75), rough=0.22, metal=0.95),
        "polymer":      make_mat("TacticalPolymer",  (0.10, 0.11, 0.12), rough=0.72, metal=0.05),
        "tan_chassis":  make_mat("DesertOlive",      (0.26, 0.30, 0.22), rough=0.62, metal=0.08),
        "walnut":       make_mat("WalnutStock",      (0.36, 0.19, 0.10), rough=0.52, metal=0.00),
        "brass":        make_mat("ShellBrass",       (0.86, 0.66, 0.22), rough=0.24, metal=0.90),
        "optic_lens":   make_mat("OpticLensBlue",    (0.10, 0.45, 0.72), rough=0.06, metal=0.20, emission=0.35),
        "red_dot":      make_mat("RedDotGlow",       (1.00, 0.14, 0.10), rough=0.10, emission=3.5),
        "green_trit":   make_mat("TritiumGreen",     (0.20, 1.00, 0.35), rough=0.15, emission=2.6),
        "accent_gold":  make_mat("SportAccentGold",  (0.84, 0.62, 0.18), rough=0.30, metal=0.80),
        "rubber":       make_mat("GripRubber",       (0.07, 0.07, 0.08), rough=0.88, metal=0.00),
        "range_wood":   make_mat("RangeTimber",      (0.54, 0.35, 0.20), rough=0.72),
        "range_white":  make_mat("TargetPaperWhite", (0.94, 0.93, 0.89), rough=0.82),
        "range_black":  make_mat("TargetRingBlack",  (0.09, 0.10, 0.12), rough=0.80),
        "range_blue":   make_mat("TargetRingBlue",   (0.14, 0.46, 0.84), rough=0.75),
        "range_red":    make_mat("TargetRingRed",    (0.84, 0.16, 0.14), rough=0.72),
        "range_gold":   make_mat("TargetBullGold",   (0.95, 0.78, 0.12), rough=0.65),
        "steel_plate":  make_mat("IPSCPlateWhite",   (0.92, 0.94, 0.96), rough=0.38, metal=0.45),
        "clay_orange":  make_mat("TrapMachineOrange",(0.92, 0.34, 0.08), rough=0.40, metal=0.35),
    }


def add_part_mesh(name, specs, pivot=(0.0, 0.0, 0.0), parent=None):
    """Create a named mesh object whose local origin is `pivot` (specs use world coords)."""
    bm = bmesh.new()
    mats = []
    px, py, pz = pivot

    def mat_idx(m):
        if m not in mats:
            mats.append(m)
        return mats.index(m)

    for sp in specs:
        kind = sp["kind"]
        lx, ly, lz = sp.get("loc", (0.0, 0.0, 0.0))
        rx, ry, rz = sp.get("rot", (0.0, 0.0, 0.0))
        m_idx = mat_idx(sp["mat"])
        tmp = bmesh.new()

        if kind == "box":
            sx, sy, sz = sp.get("scale", (1.0, 1.0, 1.0))
            bmesh.ops.create_cube(tmp, size=1.0)
            bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)
            bev = sp.get("bevel", 0.0)
            if bev > 0.0:
                bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bev, segments=2, affect="EDGES", clamp_overlap=True)
        elif kind == "cyl":
            r1 = sp.get("r1", 0.02)
            r2 = sp.get("r2", r1)
            depth = sp.get("depth", 0.1)
            segs = sp.get("segs", 14)
            bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
        elif kind == "sphere":
            sx, sy, sz = sp.get("scale", (0.02, 0.02, 0.02))
            bmesh.ops.create_uvsphere(tmp, u_segments=12, v_segments=8, radius=1.0)
            bmesh.ops.scale(tmp, vec=(sx, sy, sz), verts=tmp.verts)

        mat_xform = Matrix.Translation((lx - px, ly - py, lz - pz)) @ Euler((rx, ry, rz), "XYZ").to_matrix().to_4x4()
        bmesh.ops.transform(tmp, matrix=mat_xform, verts=tmp.verts)

        vert_map = {}
        for v in tmp.verts:
            vert_map[v.index] = bm.verts.new(v.co)
        for f in tmp.faces:
            try:
                nf = bm.faces.new([vert_map[v.index] for v in f.verts])
                nf.material_index = m_idx
                nf.smooth = (kind != "box")
            except ValueError:
                pass
        tmp.free()

    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    for m in mats:
        mesh.materials.append(m)

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = (px, py, pz)
    if parent is not None:
        obj.parent = parent
        obj.location = (px - parent.location.x, py - parent.location.y, pz - parent.location.z)
    return obj


def add_marker(name, pos, parent, mat_ref):
    """Tiny non-obtrusive socket marker mesh so Unity can reliably read muzzle & ejection positions."""
    return add_part_mesh(name, [
        {"kind": "sphere", "loc": pos, "scale": (0.004, 0.004, 0.004), "mat": mat_ref}
    ], pivot=pos, parent=parent)


def export_hierarchy_fbx(root_obj, filename):
    bpy.ops.object.select_all(action="DESELECT")

    def select_recursive(o):
        o.select_set(True)
        for c in o.children:
            select_recursive(c)

    select_recursive(root_obj)
    bpy.context.view_layer.objects.active = root_obj
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
    kb = max(1, os.path.getsize(out_path) // 1024)
    print(f"  [FBX] {filename} ({kb} KB)")


# =============================================================================
# 1. 9mm SPORT / TACTICAL PISTOL
# =============================================================================
def build_pistol():
    clear_scene()
    p = weapon_palette()
    # Frame + Grip (Origin = grip center)
    root = add_part_mesh("Pistol_Frame", [
        # Ergonomic angled polymer grip
        {"kind": "box", "loc": (0.0, -0.012, -0.020), "rot": (-0.26, 0.0, 0.0), "scale": (0.032, 0.052, 0.125), "bevel": 0.005, "mat": p["polymer"]},
        # Rubber grip panels
        {"kind": "box", "loc": (0.0, -0.013, -0.022), "rot": (-0.26, 0.0, 0.0), "scale": (0.035, 0.038, 0.088), "bevel": 0.003, "mat": p["rubber"]},
        # Lower frame & dustcover rail
        {"kind": "box", "loc": (0.0, 0.072, 0.048), "scale": (0.030, 0.195, 0.026), "bevel": 0.003, "mat": p["polymer"]},
        # Picatinny under-rail notches
        {"kind": "box", "loc": (0.0, 0.132, 0.034), "scale": (0.022, 0.068, 0.008), "bevel": 0.001, "mat": p["gunmetal"]},
        # Trigger guard bottom + front
        {"kind": "box", "loc": (0.0, 0.044, 0.008), "scale": (0.016, 0.058, 0.007), "mat": p["polymer"]},
        {"kind": "box", "loc": (0.0, 0.072, 0.025), "scale": (0.016, 0.008, 0.036), "mat": p["polymer"]},
        # Curved trigger
        {"kind": "box", "loc": (0.0, 0.040, 0.026), "rot": (0.35, 0.0, 0.0), "scale": (0.008, 0.009, 0.024), "mat": p["accent_gold"]},
        # Inner steel barrel
        {"kind": "cyl", "loc": (0.0, 0.092, 0.072), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.009, "r2": 0.009, "depth": 0.175, "segs": 14, "mat": p["steel_bright"]},
        # Sport gold flared magwell
        {"kind": "box", "loc": (0.0, -0.028, -0.082), "rot": (-0.26, 0.0, 0.0), "scale": (0.038, 0.058, 0.014), "bevel": 0.002, "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.0, 0.0))

    # Reciprocating metallic slide
    add_part_mesh("Part_Slide", [
        {"kind": "box", "loc": (0.0, 0.068, 0.076), "scale": (0.032, 0.205, 0.036), "bevel": 0.004, "mat": p["gunmetal"]},
        # Rear iron sight
        {"kind": "box", "loc": (0.0, -0.026, 0.098), "scale": (0.022, 0.012, 0.010), "mat": p["steel_blued"]},
        {"kind": "box", "loc": (-0.007, -0.028, 0.099), "scale": (0.004, 0.004, 0.004), "mat": p["green_trit"]},
        {"kind": "box", "loc": (0.007, -0.028, 0.099), "scale": (0.004, 0.004, 0.004), "mat": p["green_trit"]},
        # Front tritium sight
        {"kind": "box", "loc": (0.0, 0.158, 0.098), "scale": (0.006, 0.012, 0.010), "mat": p["green_trit"]},
        # Gold match barrel hood accent
        {"kind": "box", "loc": (0.0, 0.055, 0.090), "scale": (0.022, 0.036, 0.012), "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.068, 0.076), parent=root)

    # Detachable Magazine
    add_part_mesh("Part_Mag", [
        {"kind": "box", "loc": (0.0, -0.024, -0.060), "rot": (-0.26, 0.0, 0.0), "scale": (0.024, 0.038, 0.105), "mat": p["steel_blued"]},
        {"kind": "box", "loc": (0.0, -0.036, -0.104), "rot": (-0.26, 0.0, 0.0), "scale": (0.032, 0.048, 0.016), "bevel": 0.002, "mat": p["polymer"]},
    ], pivot=(0.0, -0.024, -0.060), parent=root)

    # Upgrade 1: Mini Reflex Red-Dot Sight
    add_part_mesh("Part_Optic_RedDot", [
        {"kind": "box", "loc": (0.0, 0.004, 0.102), "scale": (0.032, 0.048, 0.012), "bevel": 0.002, "mat": p["steel_blued"]},
        {"kind": "box", "loc": (-0.014, 0.006, 0.120), "scale": (0.005, 0.032, 0.028), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.014, 0.006, 0.120), "scale": (0.005, 0.032, 0.028), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.006, 0.132), "scale": (0.032, 0.032, 0.005), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.006, 0.119), "scale": (0.022, 0.003, 0.020), "mat": p["optic_lens"]},
        {"kind": "sphere", "loc": (0.0, 0.005, 0.119), "scale": (0.003, 0.003, 0.003), "mat": p["red_dot"]},
    ], pivot=(0.0, 0.004, 0.102), parent=root)

    # Upgrade 2: Tactical Cylindrical Suppressor
    add_part_mesh("Part_Suppressor", [
        {"kind": "cyl", "loc": (0.0, 0.255, 0.074), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.019, "r2": 0.019, "depth": 0.155, "segs": 18, "mat": p["steel_blued"]},
        {"kind": "cyl", "loc": (0.0, 0.185, 0.074), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.021, "r2": 0.021, "depth": 0.016, "segs": 18, "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.180, 0.074), parent=root)

    add_marker("Part_Muzzle", (0.0, 0.185, 0.074), root, p["gunmetal"])
    add_marker("Part_Eject", (-0.020, 0.055, 0.088), root, p["brass"])
    export_hierarchy_fbx(root, "OW_Weapon_Pistol.fbx")


# =============================================================================
# 2. 12-GAUGE TRAP PUMP SHOTGUN
# =============================================================================
def build_shotgun():
    clear_scene()
    p = weapon_palette()
    root = add_part_mesh("Shotgun_Body", [
        # Walnut shoulder stock (swept back)
        {"kind": "box", "loc": (0.0, -0.045, -0.012), "rot": (-0.28, 0.0, 0.0), "scale": (0.040, 0.115, 0.052), "bevel": 0.008, "mat": p["walnut"]},
        {"kind": "box", "loc": (0.0, -0.205, -0.040), "rot": (-0.08, 0.0, 0.0), "scale": (0.044, 0.245, 0.108), "bevel": 0.010, "mat": p["walnut"]},
        # Rubber recoil buttpad
        {"kind": "box", "loc": (0.0, -0.330, -0.046), "scale": (0.046, 0.024, 0.114), "bevel": 0.004, "mat": p["rubber"]},
        # Steel receiver
        {"kind": "box", "loc": (0.0, 0.095, 0.022), "scale": (0.044, 0.195, 0.068), "bevel": 0.006, "mat": p["steel_blued"]},
        # Gold ejection port bolt carrier
        {"kind": "box", "loc": (-0.021, 0.105, 0.032), "scale": (0.006, 0.068, 0.024), "mat": p["steel_bright"]},
        # Trigger guard + trigger
        {"kind": "box", "loc": (0.0, 0.040, -0.024), "scale": (0.018, 0.062, 0.008), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.036, -0.008), "rot": (0.25, 0.0, 0.0), "scale": (0.008, 0.010, 0.022), "mat": p["accent_gold"]},
        # Long 12-gauge vented barrel
        {"kind": "cyl", "loc": (0.0, 0.445, 0.042), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.014, "r2": 0.013, "depth": 0.540, "segs": 16, "mat": p["steel_blued"]},
        # Top ventilated sighting rib
        {"kind": "box", "loc": (0.0, 0.445, 0.058), "scale": (0.012, 0.520, 0.007), "mat": p["gunmetal"]},
        # Glowing red fiber-optic trap bead sight
        {"kind": "sphere", "loc": (0.0, 0.705, 0.066), "scale": (0.006, 0.008, 0.006), "mat": p["red_dot"]},
        # Tubular under-barrel magazine
        {"kind": "cyl", "loc": (0.0, 0.365, 0.012), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.013, "r2": 0.013, "depth": 0.380, "segs": 14, "mat": p["gunmetal"]},
        # Barrel-to-mag clamp
        {"kind": "box", "loc": (0.0, 0.545, 0.026), "scale": (0.026, 0.022, 0.048), "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.0, 0.0))

    # Sliding Walnut Pump Forend
    add_part_mesh("Part_Pump", [
        {"kind": "cyl", "loc": (0.0, 0.330, 0.012), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.024, "r2": 0.023, "depth": 0.195, "segs": 14, "mat": p["walnut"]},
        {"kind": "box", "loc": (0.0, 0.330, -0.004), "scale": (0.046, 0.175, 0.024), "bevel": 0.005, "mat": p["walnut"]},
    ], pivot=(0.0, 0.330, 0.012), parent=root)

    # Upgrade: Extended Trap Choke Tube (ported brass/gold muzzle extension)
    add_part_mesh("Part_Choke", [
        {"kind": "cyl", "loc": (0.0, 0.745, 0.042), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.016, "r2": 0.016, "depth": 0.068, "segs": 16, "mat": p["accent_gold"]},
        {"kind": "cyl", "loc": (0.0, 0.778, 0.042), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.015, "r2": 0.014, "depth": 0.018, "segs": 16, "mat": p["steel_blued"]},
    ], pivot=(0.0, 0.720, 0.042), parent=root)

    add_marker("Part_Muzzle", (0.0, 0.725, 0.042), root, p["gunmetal"])
    add_marker("Part_Eject", (-0.026, 0.105, 0.036), root, p["brass"])
    export_hierarchy_fbx(root, "OW_Weapon_Shotgun.fbx")


# =============================================================================
# 3. 5.56 TACTICAL SPORT CARBINE
# =============================================================================
def build_carbine():
    clear_scene()
    p = weapon_palette()
    root = add_part_mesh("Carbine_Body", [
        # Angled tactical pistol grip (origin)
        {"kind": "box", "loc": (0.0, -0.008, -0.018), "rot": (-0.28, 0.0, 0.0), "scale": (0.032, 0.046, 0.115), "bevel": 0.005, "mat": p["rubber"]},
        # Upper + Lower Receiver
        {"kind": "box", "loc": (0.0, 0.078, 0.056), "scale": (0.042, 0.215, 0.068), "bevel": 0.005, "mat": p["gunmetal"]},
        # Full-length Top Picatinny Rail
        {"kind": "box", "loc": (0.0, 0.205, 0.094), "scale": (0.024, 0.440, 0.010), "mat": p["steel_blued"]},
        # Buffer tube + adjustable carbine stock
        {"kind": "cyl", "loc": (0.0, -0.125, 0.066), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.018, "r2": 0.018, "depth": 0.205, "segs": 14, "mat": p["steel_blued"]},
        {"kind": "box", "loc": (0.0, -0.215, 0.034), "scale": (0.042, 0.125, 0.115), "bevel": 0.006, "mat": p["polymer"]},
        {"kind": "box", "loc": (0.0, -0.280, 0.034), "scale": (0.044, 0.018, 0.122), "bevel": 0.003, "mat": p["rubber"]},
        # Ventilated M-LOK Handguard
        {"kind": "box", "loc": (0.0, 0.305, 0.064), "scale": (0.046, 0.255, 0.054), "bevel": 0.006, "mat": p["tan_chassis"]},
        # Vertical angled foregrip
        {"kind": "box", "loc": (0.0, 0.315, 0.012), "rot": (0.18, 0.0, 0.0), "scale": (0.028, 0.034, 0.072), "bevel": 0.004, "mat": p["rubber"]},
        # Barrel
        {"kind": "cyl", "loc": (0.0, 0.465, 0.064), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.011, "r2": 0.010, "depth": 0.285, "segs": 14, "mat": p["steel_blued"]},
        # Flip-up iron front & rear sights
        {"kind": "box", "loc": (0.0, 0.415, 0.112), "scale": (0.014, 0.016, 0.028), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.002, 0.112), "scale": (0.018, 0.016, 0.026), "mat": p["gunmetal"]},
        # Trigger guard + trigger
        {"kind": "box", "loc": (0.0, 0.052, 0.010), "scale": (0.016, 0.052, 0.007), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.048, 0.024), "rot": (0.28, 0.0, 0.0), "scale": (0.007, 0.008, 0.022), "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.0, 0.0))

    # Curved 30-rd Magazine
    add_part_mesh("Part_Mag", [
        {"kind": "box", "loc": (0.0, 0.125, -0.022), "rot": (0.12, 0.0, 0.0), "scale": (0.026, 0.062, 0.110), "bevel": 0.003, "mat": p["polymer"]},
        {"kind": "box", "loc": (0.0, 0.140, -0.100), "rot": (0.26, 0.0, 0.0), "scale": (0.026, 0.060, 0.088), "bevel": 0.003, "mat": p["polymer"]},
    ], pivot=(0.0, 0.125, 0.010), parent=root)

    # Upgrade 1: Holographic Sight
    add_part_mesh("Part_Optic_Holo", [
        {"kind": "box", "loc": (0.0, 0.105, 0.114), "scale": (0.042, 0.078, 0.022), "bevel": 0.003, "mat": p["steel_blued"]},
        {"kind": "box", "loc": (-0.019, 0.098, 0.142), "scale": (0.006, 0.048, 0.042), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.019, 0.098, 0.142), "scale": (0.006, 0.048, 0.042), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.098, 0.161), "scale": (0.044, 0.048, 0.006), "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.098, 0.142), "scale": (0.032, 0.003, 0.032), "mat": p["optic_lens"]},
        {"kind": "sphere", "loc": (0.0, 0.096, 0.142), "scale": (0.004, 0.004, 0.004), "mat": p["red_dot"]},
    ], pivot=(0.0, 0.105, 0.114), parent=root)

    # Upgrade 2: Ported Muzzle Brake / Compensator
    add_part_mesh("Part_MuzzleBrake", [
        {"kind": "box", "loc": (0.0, 0.625, 0.064), "scale": (0.028, 0.065, 0.024), "bevel": 0.003, "mat": p["steel_bright"]},
        {"kind": "box", "loc": (0.0, 0.625, 0.064), "scale": (0.032, 0.012, 0.012), "mat": p["steel_blued"]},
    ], pivot=(0.0, 0.595, 0.064), parent=root)

    # Upgrade 3: Side-Mounted Tactical Red Laser Module
    add_part_mesh("Part_Laser", [
        {"kind": "box", "loc": (-0.034, 0.375, 0.066), "scale": (0.024, 0.072, 0.024), "bevel": 0.003, "mat": p["steel_blued"]},
        {"kind": "cyl", "loc": (-0.036, 0.415, 0.066), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.007, "r2": 0.007, "depth": 0.014, "segs": 12, "mat": p["red_dot"]},
    ], pivot=(-0.034, 0.375, 0.066), parent=root)

    add_marker("Part_Muzzle", (0.0, 0.610, 0.064), root, p["gunmetal"])
    add_marker("Part_Eject", (-0.024, 0.110, 0.074), root, p["brass"])
    export_hierarchy_fbx(root, "OW_Weapon_Carbine.fbx")


# =============================================================================
# 4. PRECISION 100m BOLT-ACTION SNIPER RIFLE
# =============================================================================
def build_sniper():
    clear_scene()
    p = weapon_palette()
    root = add_part_mesh("Sniper_Chassis", [
        # Precision ergonomic grip
        {"kind": "box", "loc": (0.0, -0.010, -0.020), "rot": (-0.22, 0.0, 0.0), "scale": (0.034, 0.048, 0.120), "bevel": 0.005, "mat": p["rubber"]},
        # Olive precision chassis receiver + long forend
        {"kind": "box", "loc": (0.0, 0.195, 0.048), "scale": (0.048, 0.460, 0.056), "bevel": 0.006, "mat": p["tan_chassis"]},
        # Precision skeletonized stock + adjustable cheek rest
        {"kind": "box", "loc": (0.0, -0.185, 0.046), "scale": (0.038, 0.270, 0.040), "bevel": 0.005, "mat": p["tan_chassis"]},
        {"kind": "box", "loc": (0.0, -0.175, 0.082), "scale": (0.042, 0.135, 0.028), "bevel": 0.005, "mat": p["rubber"]},
        {"kind": "box", "loc": (0.0, -0.320, 0.022), "scale": (0.042, 0.028, 0.118), "bevel": 0.004, "mat": p["rubber"]},
        {"kind": "box", "loc": (0.0, -0.185, -0.022), "scale": (0.022, 0.250, 0.018), "mat": p["gunmetal"]},
        # Upper action block & scope rail
        {"kind": "cyl", "loc": (0.0, 0.095, 0.076), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.022, "r2": 0.022, "depth": 0.210, "segs": 16, "mat": p["steel_blued"]},
        {"kind": "box", "loc": (0.0, 0.115, 0.102), "scale": (0.024, 0.230, 0.010), "mat": p["gunmetal"]},
        # Long fluted heavy match barrel
        {"kind": "cyl", "loc": (0.0, 0.510, 0.076), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.015, "r2": 0.013, "depth": 0.640, "segs": 16, "mat": p["steel_blued"]},
        # Heavy sniper muzzle brake
        {"kind": "box", "loc": (0.0, 0.845, 0.076), "scale": (0.036, 0.072, 0.028), "bevel": 0.004, "mat": p["gunmetal"]},
        # Default 4x standard scope (always visible compact tube; upgraded 8x adds sunshade + larger bell!)
        {"kind": "cyl", "loc": (0.0, 0.115, 0.136), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.018, "r2": 0.018, "depth": 0.260, "segs": 16, "mat": p["steel_blued"]},
        {"kind": "cyl", "loc": (0.0, 0.250, 0.136), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.026, "r2": 0.019, "depth": 0.055, "segs": 16, "mat": p["gunmetal"]},
        {"kind": "cyl", "loc": (0.0, -0.018, 0.136), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.021, "r2": 0.023, "depth": 0.042, "segs": 16, "mat": p["gunmetal"]},
        {"kind": "cyl", "loc": (0.0, 0.278, 0.136), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.023, "r2": 0.023, "depth": 0.006, "segs": 16, "mat": p["optic_lens"]},
        # Scope mounting rings
        {"kind": "box", "loc": (0.0, 0.055, 0.118), "scale": (0.032, 0.022, 0.038), "mat": p["accent_gold"]},
        {"kind": "box", "loc": (0.0, 0.180, 0.118), "scale": (0.032, 0.022, 0.038), "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.0, 0.0))

    # Bolt Handle (cycles up/back on every shot!)
    add_part_mesh("Part_Bolt", [
        {"kind": "cyl", "loc": (-0.026, 0.042, 0.076), "rot": (0.0, math.pi * 0.5, 0.0), "r1": 0.006, "r2": 0.006, "depth": 0.042, "segs": 10, "mat": p["steel_bright"]},
        {"kind": "sphere", "loc": (-0.048, 0.038, 0.068), "scale": (0.011, 0.011, 0.011), "mat": p["steel_bright"]},
    ], pivot=(0.0, 0.042, 0.076), parent=root)

    # Box 5-rd Magazine
    add_part_mesh("Part_Mag", [
        {"kind": "box", "loc": (0.0, 0.135, -0.012), "scale": (0.030, 0.088, 0.095), "bevel": 0.003, "mat": p["steel_blued"]},
    ], pivot=(0.0, 0.135, 0.012), parent=root)

    # Upgrade 1: 8x Extended Precision Scope Sunshade + Turrets
    add_part_mesh("Part_Scope_8x", [
        {"kind": "cyl", "loc": (0.0, 0.320, 0.136), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.028, "r2": 0.028, "depth": 0.095, "segs": 18, "mat": p["gunmetal"]},
        {"kind": "cyl", "loc": (0.0, 0.115, 0.162), "r1": 0.012, "r2": 0.012, "depth": 0.022, "segs": 12, "mat": p["accent_gold"]},
        {"kind": "cyl", "loc": (0.024, 0.115, 0.136), "rot": (0.0, math.pi * 0.5, 0.0), "r1": 0.010, "r2": 0.010, "depth": 0.020, "segs": 12, "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.115, 0.136), parent=root)

    # Upgrade 2: Folding Front Tactical Bipod
    add_part_mesh("Part_Bipod", [
        {"kind": "box", "loc": (0.0, 0.395, 0.012), "scale": (0.048, 0.036, 0.024), "mat": p["steel_blued"]},
        {"kind": "cyl", "loc": (-0.055, 0.420, -0.075), "rot": (0.25, -0.35, 0.0), "r1": 0.007, "r2": 0.006, "depth": 0.195, "segs": 10, "mat": p["gunmetal"]},
        {"kind": "cyl", "loc": (0.055, 0.420, -0.075), "rot": (0.25, 0.35, 0.0), "r1": 0.007, "r2": 0.006, "depth": 0.195, "segs": 10, "mat": p["gunmetal"]},
        {"kind": "sphere", "loc": (-0.088, 0.440, -0.165), "scale": (0.012, 0.012, 0.010), "mat": p["rubber"]},
        {"kind": "sphere", "loc": (0.088, 0.440, -0.165), "scale": (0.012, 0.012, 0.010), "mat": p["rubber"]},
    ], pivot=(0.0, 0.395, 0.012), parent=root)

    add_marker("Part_Muzzle", (0.0, 0.885, 0.076), root, p["gunmetal"])
    add_marker("Part_Eject", (-0.025, 0.095, 0.086), root, p["brass"])
    export_hierarchy_fbx(root, "OW_Weapon_Sniper.fbx")


# =============================================================================
# 5. RANGE TARGET STAND (CLASSIC CONCENTRIC BULLSEYE)
# =============================================================================
def build_target_stand():
    clear_scene()
    p = weapon_palette()
    # Ground origin (0,0,0), bullseye center at Z=1.42m facing +Y
    specs = [
        # Heavy H-base steel feet
        {"kind": "box", "loc": (-0.42, 0.0, 0.04), "scale": (0.10, 0.78, 0.08), "bevel": 0.01, "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.42, 0.0, 0.04), "scale": (0.10, 0.78, 0.08), "bevel": 0.01, "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.0, 0.04), "scale": (0.90, 0.12, 0.08), "mat": p["gunmetal"]},
        # Timber vertical posts
        {"kind": "box", "loc": (-0.42, 0.0, 1.05), "scale": (0.07, 0.07, 1.98), "bevel": 0.008, "mat": p["range_wood"]},
        {"kind": "box", "loc": (0.42, 0.0, 1.05), "scale": (0.07, 0.07, 1.98), "bevel": 0.008, "mat": p["range_wood"]},
        # Crossbeams & backboard
        {"kind": "box", "loc": (0.0, 0.0, 2.02), "scale": (0.94, 0.09, 0.09), "bevel": 0.01, "mat": p["range_wood"]},
        {"kind": "box", "loc": (0.0, 0.0, 0.90), "scale": (0.90, 0.06, 0.07), "mat": p["range_wood"]},
        {"kind": "box", "loc": (0.0, -0.01, 1.44), "scale": (0.86, 0.03, 1.02), "mat": p["range_wood"]},
        # Target paper sheet facing +Y
        {"kind": "box", "loc": (0.0, 0.015, 1.44), "scale": (0.78, 0.012, 0.92), "mat": p["range_white"]},
        # Concentric Olympic Rings (stacked forward slightly to prevent z-fighting!)
        {"kind": "cyl", "loc": (0.0, 0.025, 1.44), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.34, "r2": 0.34, "depth": 0.006, "segs": 28, "mat": p["range_black"]},
        {"kind": "cyl", "loc": (0.0, 0.030, 1.44), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.27, "r2": 0.27, "depth": 0.006, "segs": 28, "mat": p["range_blue"]},
        {"kind": "cyl", "loc": (0.0, 0.035, 1.44), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.19, "r2": 0.19, "depth": 0.006, "segs": 26, "mat": p["range_red"]},
        {"kind": "cyl", "loc": (0.0, 0.040, 1.44), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.10, "r2": 0.10, "depth": 0.006, "segs": 24, "mat": p["range_gold"]},
        {"kind": "cyl", "loc": (0.0, 0.045, 1.44), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.038, "r2": 0.038, "depth": 0.006, "segs": 18, "mat": p["range_black"]},
    ]
    root = add_part_mesh("Range_TargetStand", specs, pivot=(0.0, 0.0, 0.0))
    export_hierarchy_fbx(root, "OW_Range_TargetStand.fbx")


# =============================================================================
# 6. IPSC REACTIVE HINGED STEEL PLATE TARGET
# =============================================================================
def build_steel_plate():
    clear_scene()
    p = weapon_palette()
    # Heavy armored steel base
    root = add_part_mesh("SteelPlate_Base", [
        {"kind": "box", "loc": (0.0, 0.0, 0.06), "scale": (0.52, 0.62, 0.12), "bevel": 0.015, "mat": p["gunmetal"]},
        {"kind": "box", "loc": (0.0, 0.18, 0.16), "rot": (0.55, 0.0, 0.0), "scale": (0.48, 0.04, 0.22), "mat": p["steel_blued"]},
        {"kind": "cyl", "loc": (0.0, 0.0, 0.14), "rot": (0.0, math.pi * 0.5, 0.0), "r1": 0.025, "r2": 0.025, "depth": 0.36, "segs": 12, "mat": p["steel_bright"]},
    ], pivot=(0.0, 0.0, 0.0))

    # Hinged Reactive Plate (pivots at (0,0,0.14) so Unity rotates X backward cleanly on hit!)
    add_part_mesh("Part_Plate", [
        # Neck stem
        {"kind": "box", "loc": (0.0, 0.0, 0.42), "scale": (0.11, 0.035, 0.56), "bevel": 0.006, "mat": p["steel_plate"]},
        # Classic Pepper-Popper / Torso Circle Head
        {"kind": "cyl", "loc": (0.0, 0.0, 0.86), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.26, "r2": 0.26, "depth": 0.038, "segs": 24, "mat": p["steel_plate"]},
        # Bright center aim disc
        {"kind": "cyl", "loc": (0.0, 0.022, 0.86), "rot": (math.pi * 0.5, 0.0, 0.0), "r1": 0.10, "r2": 0.10, "depth": 0.008, "segs": 18, "mat": p["clay_orange"]},
    ], pivot=(0.0, 0.0, 0.14), parent=root)

    export_hierarchy_fbx(root, "OW_Range_SteelPlate.fbx")


# =============================================================================
# 7. MECHANICAL TRAP / SKEET CLAY PIGEON THROWER
# =============================================================================
def build_clay_thrower():
    clear_scene()
    p = weapon_palette()
    root = add_part_mesh("ClayThrower_Base", [
        # Heavy tripod steel frame
        {"kind": "box", "loc": (0.0, 0.0, 0.06), "scale": (0.95, 0.95, 0.10), "bevel": 0.015, "mat": p["gunmetal"]},
        {"kind": "cyl", "loc": (0.0, 0.0, 0.48), "r1": 0.12, "r2": 0.10, "depth": 0.82, "segs": 14, "mat": p["clay_orange"]},
        # Carousel magazine holding stacked clay pigeons
        {"kind": "cyl", "loc": (0.28, -0.10, 0.92), "r1": 0.18, "r2": 0.18, "depth": 0.42, "segs": 18, "mat": p["steel_blued"]},
        {"kind": "cyl", "loc": (0.28, -0.10, 1.15), "r1": 0.14, "r2": 0.12, "depth": 0.05, "segs": 18, "mat": p["clay_orange"]},
        # Angled steel launch deck
        {"kind": "box", "loc": (0.0, 0.14, 0.92), "rot": (0.34, 0.0, 0.0), "scale": (0.48, 0.62, 0.05), "bevel": 0.01, "mat": p["steel_bright"]},
        # Tension coil spring
        {"kind": "cyl", "loc": (-0.22, -0.18, 0.82), "rot": (math.pi * 0.5, 0.0, 0.3), "r1": 0.045, "r2": 0.045, "depth": 0.42, "segs": 12, "mat": p["accent_gold"]},
    ], pivot=(0.0, 0.0, 0.0))

    # Sweeping Throwing Arm
    add_part_mesh("Part_Arm", [
        {"kind": "box", "loc": (0.0, 0.24, 0.98), "rot": (0.34, 0.0, 0.0), "scale": (0.06, 0.58, 0.03), "bevel": 0.005, "mat": p["accent_gold"]},
        {"kind": "box", "loc": (0.0, 0.50, 1.07), "rot": (0.34, 0.0, 0.0), "scale": (0.12, 0.10, 0.04), "mat": p["rubber"]},
    ], pivot=(0.0, 0.0, 0.92), parent=root)

    export_hierarchy_fbx(root, "OW_Range_ClayThrower.fbx")


def main():
    print("=== Generating Our World Shooting Range & Sporting Firearms FBX Pack ===")
    build_pistol()
    build_shotgun()
    build_carbine()
    build_sniper()
    build_target_stand()
    build_steel_plate()
    build_clay_thrower()
    print("=== All 7 FBX assets generated successfully! ===")


if __name__ == "__main__":
    main()
