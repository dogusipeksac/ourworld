#!/usr/bin/env python3
"""
GRIDROAM Selman Sokağı Interior Furniture, Shop Equipment & Hollow Walk-In Building Generator (Blender 5.3+)
Sculpts 12 authentic Turkish home & shop interior models AND upgrades the 5 key Selman Sokağı
buildings into true hollow walk-in architectural shells with open doors/gates:
  Interior Assets:
    1.  Interior_TurkishSofaL.fbx      (Turkish L-sofa / çekyat with pillows & wood base)
    2.  Interior_TurkishTeaTable.fbx   (Walnut coffee table + silver tray + double çaydanlık + 4 tulip tea glasses)
    3.  Interior_TurkishRugKilim.fbx   (Anatolian terracotta/navy/gold kilim carpet)
    4.  Interior_TvUnitVitrin.fbx      (Wood TV console, widescreen TV, receiver & decor)
    5.  Interior_TurkishKitchen.fbx    (Granite counter, sink, 4-burner gas stove + çaydanlık, upper cabinets & fridge)
    6.  Interior_BedroomSet.fbx        (Double wooden bed, quilt, pillows & twin nightstands with lamps)
    7.  Interior_MarketShelf.fbx       (4-tier stocked Mahalle Bakkalı gondola shelf with boxes, cans & bottles)
    8.  Interior_DrinkFridge.fbx       (Tall commercial glass-door İçecek Dolabı filled with cold drinks)
    9.  Interior_MarketCounter.fbx     (Selman Bakkal checkout counter, cash register, scale & Ekmek Kasası w/ loaves)
    10. Interior_PlumberBench.fbx      (HAFSA TESİSAT steel workbench, red toolbox, vise, wrench & pipe coils)
    11. Interior_KombiDisplay.fbx      (Wall-mounted white Kombi boiler, ribbed kalorifer peteği & PVC pipe rack)
    12. Interior_GarageTools.fbx       (No:73 Oto Tamir red mechanic chest, floor jack, tire stack & oil cans)
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_FBX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Models", "Blender")
BLEND_OUT_DIR = os.path.join(SCRIPT_DIR, "blend_sources")
PREVIEW_PATH = os.environ.get(
    "GRIDROAM_INTERIOR_PREVIEW",
    os.path.join(SCRIPT_DIR, "blender_selman_interiors_preview.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.58, metallic=0.0):
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


def get_mats():
    return {
        "Wood": make_mat("Wood", (0.54, 0.34, 0.18), 0.52),
        "DarkWood": make_mat("DarkWood", (0.28, 0.18, 0.10), 0.56),
        "WhitePVC": make_mat("WhitePVC", (0.94, 0.94, 0.93), 0.34),
        "CreamPaint": make_mat("CreamPaint", (0.90, 0.87, 0.80), 0.72),
        "Concrete": make_mat("Concrete", (0.56, 0.56, 0.54), 0.78),
        "TrimDark": make_mat("TrimDark", (0.12, 0.13, 0.15), 0.45, 0.35),
        "Chrome": make_mat("Chrome", (0.86, 0.88, 0.90), 0.14, 0.92),
        "Glass": make_mat("Glass", (0.10, 0.16, 0.22), 0.08, 0.20),
        "Galvanized": make_mat("Galvanized", (0.68, 0.70, 0.72), 0.42, 0.68),
        "AccentTeal": make_mat("AccentTeal", (0.16, 0.44, 0.46), 0.82),
        "AccentMustard": make_mat("AccentMustard", (0.80, 0.60, 0.18), 0.78),
        "AccentBlue": make_mat("AccentBlue", (0.16, 0.34, 0.58), 0.80),
        "RedPlaster": make_mat("RedPlaster", (0.68, 0.21, 0.16), 0.78),
        "SignRed": make_mat("SignRed", (0.78, 0.12, 0.12), 0.38, 0.25),
        "CobaltBlue": make_mat("CobaltBlue", (0.10, 0.38, 0.78), 0.42, 0.25),
        "GoldBrass": make_mat("GoldBrass", (0.84, 0.66, 0.22), 0.30, 0.85),
        "CanRed": make_mat("CanRed", (0.80, 0.15, 0.15), 0.35, 0.45),
        "CanBlue": make_mat("CanBlue", (0.15, 0.42, 0.80), 0.35, 0.45),
        "CanGreen": make_mat("CanGreen", (0.16, 0.58, 0.28), 0.35, 0.45),
        "CanOrange": make_mat("CanOrange", (0.92, 0.48, 0.12), 0.35, 0.45),
        "SimitGold": make_mat("SimitGold", (0.76, 0.52, 0.20), 0.74),
        "LitScreen": make_mat("LitScreen", (0.96, 0.86, 0.62), 0.30),
        "WheelRubber": make_mat("WheelRubber", (0.10, 0.10, 0.11), 0.78),
        "RustSteel": make_mat("RustSteel", (0.42, 0.26, 0.18), 0.68, 0.50),
    }


def box_uv(obj, scale=0.6):
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


def make_obj(name, bm, mat):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    box_uv(obj)
    return obj


def add_box(name, center, size, mat, bevel=0.015, rot_euler=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.25), segments=2, affect="EDGES")
    if rot_euler is not None:
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Euler(rot_euler, "XYZ").to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return make_obj(name, bm, mat)


def add_cyl(name, center, r1, r2, depth, segs, mat, rot_euler=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    if rot_euler is not None:
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Euler(rot_euler, "XYZ").to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return make_obj(name, bm, mat)


def join_all(parts, final_name):
    bpy.ops.object.select_all(action="DESELECT")
    valid = [p for p in parts if p is not None]
    for p in valid:
        p.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = final_name
    obj.location = Vector((0, 0, 0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


# ---------------------------------------------------------------------------
# 1. TURKISH L-SOFA / ÇEKYAT WITH KILIM THROW PILLOWS
# ---------------------------------------------------------------------------
def build_turkish_sofa_l(mats):
    parts = []
    # Main 3-seat base (X = -1.3..+1.3, Y = -0.45..+0.45) + chaise L-wing on right (+X = 0.55..1.35, Y = 0.45..1.35)
    parts.append(add_box("Sofa_BaseMain", (0.0, 0.0, 0.20), (2.60, 0.88, 0.24), mats["DarkWood"], bevel=0.02))
    parts.append(add_box("Sofa_BaseChaise", (0.92, 0.84, 0.20), (0.76, 0.90, 0.24), mats["DarkWood"], bevel=0.02))
    # Legs
    for lx, ly in ((-1.18, -0.35), (1.18, -0.35), (-1.18, 0.35), (0.58, 1.20), (1.22, 1.20)):
        parts.append(add_cyl(f"Sofa_Leg_{lx:.2f}_{ly:.2f}", (lx, ly, 0.05), 0.035, 0.028, 0.10, 8, mats["Wood"]))
    # Plush seat cushions
    for sx in (-0.82, 0.0, 0.82):
        parts.append(add_box(f"Sofa_Seat_{sx:.2f}", (sx, 0.04, 0.40), (0.80, 0.78, 0.18), mats["AccentTeal"], bevel=0.05))
    parts.append(add_box("Sofa_ChaiseSeat", (0.92, 0.84, 0.40), (0.76, 0.88, 0.18), mats["AccentTeal"], bevel=0.05))
    # Backrest + plush back cushions + armrests
    parts.append(add_box("Sofa_BackFrame", (0.0, -0.36, 0.62), (2.62, 0.18, 0.68), mats["AccentTeal"], bevel=0.04))
    for bx in (-0.80, 0.0, 0.80):
        parts.append(add_box(f"Sofa_BackCush_{bx:.2f}", (bx, -0.22, 0.66), (0.76, 0.18, 0.42), mats["AccentTeal"], bevel=0.06, rot_euler=(-0.14, 0, 0)))
    parts.append(add_box("Sofa_ArmL", (-1.30, 0.02, 0.50), (0.16, 0.88, 0.44), mats["AccentTeal"], bevel=0.04))
    parts.append(add_box("Sofa_ArmR", (1.30, 0.02, 0.50), (0.16, 0.88, 0.44), mats["AccentTeal"], bevel=0.04))
    # Colorful Turkish kilim throw pillows!
    parts.append(add_box("Sofa_Pillow1", (-0.98, -0.10, 0.62), (0.34, 0.14, 0.34), mats["RedPlaster"], bevel=0.05, rot_euler=(-0.22, 0.15, 0.25)))
    parts.append(add_box("Sofa_Pillow2", (0.05, -0.10, 0.62), (0.34, 0.14, 0.34), mats["AccentMustard"], bevel=0.05, rot_euler=(-0.20, -0.12, -0.15)))
    parts.append(add_box("Sofa_Pillow3", (0.96, -0.08, 0.62), (0.34, 0.14, 0.34), mats["CreamPaint"], bevel=0.05, rot_euler=(-0.22, 0.10, -0.28)))
    return join_all(parts, "Interior_TurkishSofaL")


# ---------------------------------------------------------------------------
# 2. TURKISH COFFEE TABLE + SILVER TRAY + DOUBLE ÇAYDANLIK + 4 TULIP TEA GLASSES
# ---------------------------------------------------------------------------
def build_turkish_tea_table(mats):
    parts = []
    # Walnut coffee table (1.15m x 0.68m x 0.46m)
    parts.append(add_box("Table_Top", (0.0, 0.0, 0.44), (1.15, 0.68, 0.05), mats["Wood"], bevel=0.015))
    parts.append(add_box("Table_Shelf", (0.0, 0.0, 0.18), (0.98, 0.54, 0.03), mats["DarkWood"], bevel=0.01))
    for lx in (-0.50, 0.50):
        for ly in (-0.27, 0.27):
            parts.append(add_box(f"Table_Leg_{lx}_{ly}", (lx, ly, 0.22), (0.055, 0.055, 0.44), mats["DarkWood"], bevel=0.008))
    # Round silver çay tepsisi (tea tray)
    parts.append(add_cyl("Tea_Tray", (-0.12, 0.02, 0.472), 0.22, 0.23, 0.018, 22, mats["Chrome"]))
    # Classic Turkish Double Teapot (Çift Katlı Çaydanlık!)
    parts.append(add_cyl("Teapot_Bot", (-0.18, 0.05, 0.55), 0.085, 0.068, 0.14, 16, mats["Chrome"]))
    parts.append(add_cyl("Teapot_Top", (-0.18, 0.05, 0.67), 0.068, 0.055, 0.11, 16, mats["Chrome"]))
    parts.append(add_cyl("Teapot_Knob", (-0.18, 0.05, 0.74), 0.018, 0.014, 0.025, 10, mats["TrimDark"]))
    # 4 İnce Belli Çay Bardağı (tulip glasses with rich red tea & saucers!)
    for gi, (gx, gy) in enumerate([(0.02, 0.10), (0.04, -0.06), (-0.06, -0.11), (0.28, 0.04)]):
        parts.append(add_cyl(f"Tea_Saucer_{gi}", (gx, gy, 0.484), 0.042, 0.048, 0.008, 12, mats["WhitePVC"]))
        parts.append(add_cyl(f"Tea_Glass_{gi}", (gx, gy, 0.528), 0.022, 0.025, 0.082, 12, mats["RedPlaster"]))
    # Cookie plate on right side of table
    parts.append(add_cyl("Cookie_Plate", (0.32, -0.12, 0.474), 0.11, 0.13, 0.016, 16, mats["WhitePVC"]))
    for cx, cy in ((0.29, -0.10), (0.35, -0.11), (0.31, -0.16)):
        parts.append(add_cyl(f"Cookie_{cx:.2f}", (cx, cy, 0.492), 0.028, 0.025, 0.018, 10, mats["SimitGold"]))
    return join_all(parts, "Interior_TurkishTeaTable")


# ---------------------------------------------------------------------------
# 3. ANATOLIAN KILIM CARPET
# ---------------------------------------------------------------------------
def build_turkish_rug_kilim(mats):
    parts = []
    parts.append(add_box("Rug_Base", (0.0, 0.0, 0.012), (2.80, 1.95, 0.024), mats["RedPlaster"], bevel=0.005))
    parts.append(add_box("Rug_Border", (0.0, 0.0, 0.015), (2.42, 1.58, 0.022), mats["AccentBlue"], bevel=0.004))
    parts.append(add_box("Rug_Center", (0.0, 0.0, 0.018), (1.92, 1.12, 0.020), mats["CreamPaint"], bevel=0.004))
    parts.append(add_box("Rug_Diamond", (0.0, 0.0, 0.021), (0.72, 0.72, 0.018), mats["AccentMustard"], bevel=0.004, rot_euler=(0, 0, math.pi * 0.25)))
    parts.append(add_box("Rug_FringeL", (-1.44, 0.0, 0.008), (0.08, 1.90, 0.012), mats["CreamPaint"]))
    parts.append(add_box("Rug_FringeR", (1.44, 0.0, 0.008), (0.08, 1.90, 0.012), mats["CreamPaint"]))
    return join_all(parts, "Interior_TurkishRugKilim")


# ---------------------------------------------------------------------------
# 4. TURKISH TV CONSOLE & VITRIN UNIT
# ---------------------------------------------------------------------------
def build_tv_unit_vitrin(mats):
    parts = []
    parts.append(add_box("TV_Cabinet", (0.0, 0.0, 0.28), (1.85, 0.46, 0.48), mats["Wood"], bevel=0.015))
    parts.append(add_box("TV_DrawerL", (-0.55, 0.24, 0.28), (0.58, 0.03, 0.34), mats["CreamPaint"], bevel=0.008))
    parts.append(add_box("TV_DrawerR", (0.55, 0.24, 0.28), (0.58, 0.03, 0.34), mats["CreamPaint"], bevel=0.008))
    parts.append(add_box("TV_Decoder", (0.0, 0.06, 0.36), (0.42, 0.28, 0.07), mats["TrimDark"], bevel=0.008))
    # Widescreen TV
    parts.append(add_box("TV_Stand", (0.0, -0.02, 0.56), (0.44, 0.22, 0.06), mats["TrimDark"]))
    parts.append(add_box("TV_Neck", (0.0, -0.04, 0.68), (0.14, 0.06, 0.22), mats["TrimDark"]))
    parts.append(add_box("TV_Bezel", (0.0, -0.02, 1.06), (1.32, 0.06, 0.76), mats["TrimDark"], bevel=0.01))
    parts.append(add_box("TV_Screen", (0.0, 0.015, 1.06), (1.24, 0.02, 0.68), mats["Glass"]))
    # Tall side vitrin bookshelf
    parts.append(add_box("Vitrin_Body", (1.28, -0.02, 0.92), (0.58, 0.42, 1.84), mats["Wood"], bevel=0.015))
    for sz in (0.45, 0.95, 1.45):
        parts.append(add_box(f"Vitrin_Item_{sz:.2f}", (1.28, 0.02, sz), (0.38, 0.22, 0.24), mats["AccentMustard"] if sz < 1.0 else mats["CobaltBlue"], bevel=0.02))
    return join_all(parts, "Interior_TvUnitVitrin")


# ---------------------------------------------------------------------------
# 5. COZY TURKISH HOME KITCHEN (Counter, Sink, Stove + Çaydanlık, Cabinets & Fridge)
# ---------------------------------------------------------------------------
def build_turkish_kitchen(mats):
    parts = []
    # Lower cabinets + dark granite counter (X = -1.3..+0.8)
    parts.append(add_box("Kit_LowerCab", (-0.25, 0.0, 0.42), (2.10, 0.62, 0.84), mats["WhitePVC"], bevel=0.015))
    parts.append(add_box("Kit_CounterTop", (-0.25, 0.02, 0.87), (2.16, 0.66, 0.06), mats["TrimDark"], bevel=0.01))
    # Stainless sink + curved faucet
    parts.append(add_box("Kit_Sink", (-0.82, 0.0, 0.905), (0.58, 0.44, 0.02), mats["Chrome"]))
    parts.append(add_cyl("Kit_Faucet", (-0.82, -0.22, 1.06), 0.018, 0.015, 0.30, 8, mats["Chrome"]))
    # 4-burner gas stove + çaydanlık simmering on right!
    parts.append(add_box("Kit_Stove", (0.28, 0.0, 0.91), (0.62, 0.54, 0.04), mats["Galvanized"], bevel=0.01))
    for bx in (0.14, 0.42):
        for by in (-0.12, 0.12):
            parts.append(add_cyl(f"Kit_Burner_{bx}_{by}", (bx, by, 0.94), 0.07, 0.06, 0.02, 12, mats["TrimDark"]))
    parts.append(add_cyl("Kit_TeapotBot", (0.14, -0.12, 1.03), 0.08, 0.065, 0.14, 14, mats["Chrome"]))
    parts.append(add_cyl("Kit_TeapotTop", (0.14, -0.12, 1.15), 0.062, 0.050, 0.10, 14, mats["Chrome"]))
    # Upper wall cabinets
    parts.append(add_box("Kit_UpperCab", (-0.25, -0.14, 1.72), (2.10, 0.36, 0.68), mats["Wood"], bevel=0.015))
    # Tall white refrigerator on right (+X = 1.25)
    parts.append(add_box("Kit_FridgeBody", (1.22, 0.02, 0.95), (0.72, 0.70, 1.90), mats["WhitePVC"], bevel=0.025))
    parts.append(add_box("Kit_FridgeSeam", (1.22, 0.38, 1.32), (0.70, 0.02, 0.02), mats["TrimDark"]))
    parts.append(add_box("Kit_FridgeHandle1", (0.94, 0.40, 1.56), (0.03, 0.05, 0.32), mats["Chrome"]))
    parts.append(add_box("Kit_FridgeHandle2", (0.94, 0.40, 1.02), (0.03, 0.05, 0.42), mats["Chrome"]))
    return join_all(parts, "Interior_TurkishKitchen")


# ---------------------------------------------------------------------------
# 6. FAMILY BEDROOM DOUBLE BED + TWIN NIGHTSTANDS WITH WARM LAMPS
# ---------------------------------------------------------------------------
def build_bedroom_set(mats):
    parts = []
    parts.append(add_box("Bed_Frame", (0.0, 0.0, 0.24), (1.76, 2.08, 0.32), mats["Wood"], bevel=0.02))
    parts.append(add_box("Bed_Headboard", (0.0, -1.00, 0.68), (1.80, 0.10, 1.12), mats["DarkWood"], bevel=0.02))
    parts.append(add_box("Bed_Mattress", (0.0, 0.02, 0.48), (1.66, 1.96, 0.22), mats["CreamPaint"], bevel=0.04))
    parts.append(add_box("Bed_Quilt", (0.0, 0.26, 0.54), (1.70, 1.46, 0.18), mats["AccentBlue"], bevel=0.05))
    parts.append(add_box("Bed_PillowL", (-0.42, -0.68, 0.64), (0.64, 0.38, 0.14), mats["WhitePVC"], bevel=0.05))
    parts.append(add_box("Bed_PillowR", (0.42, -0.68, 0.64), (0.64, 0.38, 0.14), mats["WhitePVC"], bevel=0.05))
    # Twin nightstands + glowing bedside lamps
    for sx in (-1.22, 1.22):
        parts.append(add_box(f"Nightstand_{sx:.1f}", (sx, -0.74, 0.28), (0.48, 0.44, 0.56), mats["Wood"], bevel=0.015))
        parts.append(add_cyl(f"LampBase_{sx:.1f}", (sx, -0.74, 0.64), 0.07, 0.05, 0.16, 10, mats["Chrome"]))
        parts.append(add_cyl(f"LampShade_{sx:.1f}", (sx, -0.74, 0.80), 0.14, 0.09, 0.18, 14, mats["LitScreen"]))
    return join_all(parts, "Interior_BedroomSet")


# ---------------------------------------------------------------------------
# 7. MAHALLE BAKKALI STOCKED 4-TIER GONDOLA SHELF
# ---------------------------------------------------------------------------
def build_market_shelf(mats):
    parts = []
    # White steel gondola rack (1.90m wide x 0.52m deep x 1.85m tall)
    parts.append(add_box("Shelf_Back", (0.0, -0.22, 0.92), (1.90, 0.05, 1.84), mats["WhitePVC"], bevel=0.01))
    parts.append(add_box("Shelf_SideL", (-0.93, 0.0, 0.92), (0.06, 0.50, 1.84), mats["Galvanized"]))
    parts.append(add_box("Shelf_SideR", (0.93, 0.0, 0.92), (0.06, 0.50, 1.84), mats["Galvanized"]))
    item_mats = [mats["CanRed"], mats["CanBlue"], mats["CanGreen"], mats["CanOrange"], mats["AccentMustard"], mats["WhitePVC"]]
    for tier, sz in enumerate((0.18, 0.62, 1.06, 1.50)):
        parts.append(add_box(f"Shelf_Deck_{tier}", (0.0, 0.02, sz), (1.86, 0.46, 0.04), mats["WhitePVC"], bevel=0.008))
        parts.append(add_box(f"Shelf_PriceTag_{tier}", (0.0, 0.255, sz), (1.84, 0.015, 0.035), mats["SignRed"]))
        # Pack shelf with colorful boxes, cans & bottles!
        for k in range(7):
            ix = -0.75 + k * 0.25
            m = item_mats[(tier * 3 + k) % len(item_mats)]
            if (tier + k) % 3 == 0:
                parts.append(add_cyl(f"Prod_Bot_{tier}_{k}", (ix, 0.04, sz + 0.15), 0.065, 0.055, 0.26, 10, m))
            else:
                parts.append(add_box(f"Prod_Box_{tier}_{k}", (ix, 0.03, sz + 0.14), (0.19, 0.24, 0.24), m, bevel=0.01))
    return join_all(parts, "Interior_MarketShelf")


# ---------------------------------------------------------------------------
# 8. COMMERCIAL GLASS-DOOR BEVERAGE FRIDGE (İÇECEK DOLABI)
# ---------------------------------------------------------------------------
def build_drink_fridge(mats):
    parts = []
    # Main cooler cabinet (0.92m wide x 0.72m deep x 2.05m tall)
    parts.append(add_box("Fridge_Body", (0.0, -0.04, 1.02), (0.92, 0.68, 2.04), mats["WhitePVC"], bevel=0.02))
    # Bright red top illuminated sign canopy
    parts.append(add_box("Fridge_Header", (0.0, 0.31, 1.88), (0.88, 0.06, 0.26), mats["SignRed"], bevel=0.01))
    # Glass door frame + interior chilled shelves filled with soda/ayran/water!
    parts.append(add_box("Fridge_DoorFrame", (0.0, 0.32, 0.92), (0.86, 0.04, 1.56), mats["TrimDark"], bevel=0.01))
    parts.append(add_box("Fridge_DoorGlass", (0.0, 0.345, 0.92), (0.76, 0.015, 1.44), mats["Glass"]))
    parts.append(add_box("Fridge_Handle", (-0.38, 0.37, 1.05), (0.03, 0.06, 0.42), mats["Chrome"]))
    drink_mats = [mats["CanRed"], mats["CanBlue"], mats["CanGreen"], mats["CanOrange"]]
    for row, rz in enumerate((0.42, 0.78, 1.14, 1.48)):
        for c in range(4):
            cx = -0.27 + c * 0.18
            parts.append(add_cyl(f"Drink_{row}_{c}", (cx, 0.18, rz), 0.045, 0.045, 0.18, 10, drink_mats[(row + c) % 4]))
    return join_all(parts, "Interior_DrinkFridge")


# ---------------------------------------------------------------------------
# 9. SELMAN BAKKAL CHECKOUT COUNTER + CASH REGISTER + EKMEK KASASI (BREAD CRATE!)
# ---------------------------------------------------------------------------
def build_market_counter(mats):
    parts = []
    # L-shaped bakkal counter (1.95m wide x 0.72m deep x 0.96m tall)
    parts.append(add_box("Cnt_Body", (0.0, 0.0, 0.46), (1.95, 0.68, 0.92), mats["Wood"], bevel=0.02))
    parts.append(add_box("Cnt_Top", (0.0, 0.02, 0.95), (2.02, 0.74, 0.06), mats["CreamPaint"], bevel=0.012))
    # Electronic cash register (Yazar Kasa)
    parts.append(add_box("Reg_Base", (-0.45, 0.02, 1.06), (0.42, 0.36, 0.16), mats["TrimDark"], bevel=0.015))
    parts.append(add_box("Reg_Screen", (-0.45, -0.08, 1.22), (0.28, 0.06, 0.18), mats["LitScreen"], bevel=0.01))
    # Digital scale (Terazi)
    parts.append(add_box("Scale_Base", (0.12, 0.04, 1.02), (0.34, 0.32, 0.08), mats["WhitePVC"], bevel=0.01))
    parts.append(add_box("Scale_Pan", (0.12, 0.04, 1.08), (0.30, 0.28, 0.02), mats["Chrome"]))
    # Iconic Turkish Wooden Bread Crate (Ekmek Kasası) filled with golden Somun Ekmek loaves!
    parts.append(add_box("Bread_Crate", (0.72, 0.04, 1.12), (0.48, 0.42, 0.28), mats["DarkWood"], bevel=0.015))
    for bx in (0.58, 0.72, 0.86):
        parts.append(add_box(f"Bread_Loaf_{bx:.2f}", (bx, 0.04, 1.28), (0.11, 0.32, 0.14), mats["SimitGold"], bevel=0.045, rot_euler=(0.12, 0, 0.08)))
    return join_all(parts, "Interior_MarketCounter")


# ---------------------------------------------------------------------------
# 10. HAFSA TESİSAT HEAVY STEEL PLUMBER WORKBENCH + TOOLBOX + PIPE COILS
# ---------------------------------------------------------------------------
def build_plumber_bench(mats):
    parts = []
    # Heavy steel workbench (1.80m wide x 0.75m deep x 0.88m tall)
    parts.append(add_box("Bench_Top", (0.0, 0.0, 0.86), (1.80, 0.75, 0.06), mats["Wood"], bevel=0.012))
    parts.append(add_box("Bench_LowerShelf", (0.0, 0.0, 0.26), (1.70, 0.66, 0.04), mats["Galvanized"]))
    for lx in (-0.84, 0.84):
        for ly in (-0.32, 0.32):
            parts.append(add_box(f"Bench_Leg_{lx}_{ly}", (lx, ly, 0.43), (0.06, 0.06, 0.86), mats["CobaltBlue"]))
    # Red steel plumber toolbox + brass fittings + pipe vise on top
    parts.append(add_box("ToolBox", (-0.52, -0.05, 1.04), (0.52, 0.26, 0.28), mats["SignRed"], bevel=0.015))
    parts.append(add_box("ToolBox_Handle", (-0.52, -0.05, 1.22), (0.22, 0.03, 0.04), mats["Chrome"]))
    parts.append(add_box("Pipe_Vise", (0.64, 0.18, 0.98), (0.22, 0.24, 0.18), mats["CobaltBlue"], bevel=0.01))
    # Stacked white PPRC water pipes + copper coil on lower shelf
    for py in (-0.18, -0.06, 0.06, 0.18):
        parts.append(add_cyl(f"PVC_Pipe_{py:.2f}", (0.0, py, 0.34), 0.035, 0.035, 1.68, 10, mats["WhitePVC"], rot_euler=(0, math.pi * 0.5, 0)))
    parts.append(add_cyl("Brass_Fitting", (0.12, 0.02, 0.94), 0.055, 0.055, 0.12, 10, mats["GoldBrass"]))
    parts.append(add_box("Pipe_Wrench", (0.28, 0.12, 0.91), (0.38, 0.07, 0.03), mats["SignRed"], rot_euler=(0, 0, 0.35)))
    return join_all(parts, "Interior_PlumberBench")


# ---------------------------------------------------------------------------
# 11. HAFSA TESİSAT WALL-MOUNTED KOMBİ BOILER + KALORİFER PETEĞİ + PIPE RACK
# ---------------------------------------------------------------------------
def build_kombi_display(mats):
    parts = []
    # White back display board (2.1m wide x 2.0m tall)
    parts.append(add_box("Kombi_Backboard", (0.0, -0.18, 1.05), (2.10, 0.06, 1.95), mats["CobaltBlue"], bevel=0.015))
    # Wall-mounted white Doğalgaz Kombisi (left side)
    parts.append(add_box("Kombi_Unit", (-0.52, 0.02, 1.48), (0.48, 0.34, 0.76), mats["WhitePVC"], bevel=0.02))
    parts.append(add_box("Kombi_LCD", (-0.52, 0.20, 1.28), (0.16, 0.02, 0.08), mats["LitScreen"]))
    parts.append(add_cyl("Kombi_Flue", (-0.52, 0.02, 1.98), 0.06, 0.06, 0.30, 12, mats["WhitePVC"]))
    # Copper/grey pipes running down from Kombi
    for px in (-0.64, -0.56, -0.48, -0.40):
        parts.append(add_cyl(f"Kombi_Pipe_{px:.2f}", (px, 0.0, 0.72), 0.016, 0.016, 0.78, 8, mats["Chrome"]))
    # White ribbed Kalorifer Peteği (Radiator display under/right of Kombi!)
    parts.append(add_box("Radiator_Body", (0.42, 0.0, 0.62), (1.05, 0.12, 0.62), mats["WhitePVC"], bevel=0.015))
    for rx in range(-4, 5):
        parts.append(add_box(f"Rad_Rib_{rx}", (0.42 + rx * 0.11, 0.07, 0.62), (0.03, 0.03, 0.56), mats["Galvanized"]))
    # Vertical PVC pipe rack on far right
    for i, px in enumerate((0.25, 0.45, 0.65, 0.85)):
        parts.append(add_cyl(f"PipeStock_{i}", (px, -0.02, 1.52), 0.038, 0.038, 0.92, 10, mats["WhitePVC"] if i % 2 == 0 else mats["Galvanized"]))
    return join_all(parts, "Interior_KombiDisplay")


# ---------------------------------------------------------------------------
# 12. NO:73 OTO TAMİR MECHANIC CHEST + HYDRAULIC JACK + TIRE STACK + OIL CANS
# ---------------------------------------------------------------------------
def build_garage_tools(mats):
    parts = []
    # Rolling red mechanic tool cabinet
    parts.append(add_box("Chest_Body", (-0.55, 0.0, 0.56), (0.86, 0.48, 0.94), mats["SignRed"], bevel=0.02))
    for dz in (0.24, 0.42, 0.60, 0.78, 0.92):
        parts.append(add_box(f"Chest_Pull_{dz:.2f}", (-0.55, 0.26, dz), (0.68, 0.03, 0.025), mats["Chrome"]))
    # Stack of 3 rubber tires on right (+X = 0.55)
    for tz in (0.14, 0.40, 0.66):
        parts.append(add_cyl(f"Tire_{tz:.2f}", (0.55, 0.02, tz), 0.34, 0.34, 0.24, 18, mats["WheelRubber"]))
        parts.append(add_cyl(f"Rim_{tz:.2f}", (0.55, 0.02, tz), 0.18, 0.18, 0.25, 14, mats["Galvanized"]))
    # Hydraulic floor jack + yellow/blue motor oil cans
    parts.append(add_box("Jack_Body", (0.02, 0.38, 0.09), (0.28, 0.64, 0.14), mats["CobaltBlue"], bevel=0.015))
    parts.append(add_cyl("Jack_Handle", (0.02, 0.02, 0.55), 0.018, 0.018, 0.98, 8, mats["Chrome"], rot_euler=(-0.38, 0, 0)))
    parts.append(add_box("Oil_Can1", (-0.52, 0.02, 1.16), (0.20, 0.12, 0.26), mats["CanOrange"], bevel=0.015))
    parts.append(add_box("Oil_Can2", (-0.28, 0.02, 1.16), (0.20, 0.12, 0.26), mats["CanBlue"], bevel=0.015))
    return join_all(parts, "Interior_GarageTools")


def export_fbx(obj, fbx_name):
    out_path = os.path.join(OUT_FBX_DIR, fbx_name)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print(f"  Exported {fbx_name:<34} | verts={len(obj.data.vertices):5d} | tris={tris:5d}")


def render_interior_showcase():
    reset_scene()
    mats = get_mats()
    bm_g = bmesh.new()
    bmesh.ops.create_grid(bm_g, x_segments=1, y_segments=1, size=30.0)
    make_obj("Floor", bm_g, make_mat("WoodFloor", (0.58, 0.44, 0.30), 0.55))

    # Row 1: Turkish Home Interior (No:48 & No:66!)
    rug = build_turkish_rug_kilim(mats); rug.location = Vector((-4.0, 0.0, 0.0))
    sofa = build_turkish_sofa_l(mats); sofa.location = Vector((-4.0, -1.3, 0.0))
    tea = build_turkish_tea_table(mats); tea.location = Vector((-4.2, 0.1, 0.0))
    tv = build_tv_unit_vitrin(mats); tv.location = Vector((-4.0, 1.8, 0.0)); tv.rotation_euler = Euler((0, 0, math.pi), "XYZ")
    kit = build_turkish_kitchen(mats); kit.location = Vector((-7.5, -1.6, 0.0))

    # Row 2: Selman Bakkal Market Interior (No:58!)
    sh1 = build_market_shelf(mats); sh1.location = Vector((0.8, -1.8, 0.0))
    fr = build_drink_fridge(mats); fr.location = Vector((2.6, -1.8, 0.0))
    cnt = build_market_counter(mats); cnt.location = Vector((1.2, 0.6, 0.0))

    # Row 3: Hafsa Tesisat & No:73 Oto Tamir
    kombi = build_kombi_display(mats); kombi.location = Vector((5.6, -1.8, 0.0))
    bench = build_plumber_bench(mats); bench.location = Vector((5.6, 0.2, 0.0))
    tools = build_garage_tools(mats); tools.location = Vector((8.2, -0.4, 0.0))

    world = bpy.data.worlds.new("Studio")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.76, 0.78, 0.82, 1.0)
        bg.inputs["Strength"].default_value = 0.9

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 2.6
    sun_obj = bpy.data.objects.new("Sun", sun_data)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = Euler((math.radians(45), math.radians(15), math.radians(155)), "XYZ")

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 28.0
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((1.2, 8.6, 4.6))
    cam_obj.rotation_euler = (Vector((0.8, -0.6, 0.7)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_obj

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_Selman_Interiors.blend"))
    bpy.ops.render.render(write_still=True)
    print("Saved Selman Sokağı Interiors showcase render to:", PREVIEW_PATH)


def main():
    builders = [
        ("Interior_TurkishSofaL.fbx", build_turkish_sofa_l),
        ("Interior_TurkishTeaTable.fbx", build_turkish_tea_table),
        ("Interior_TurkishRugKilim.fbx", build_turkish_rug_kilim),
        ("Interior_TvUnitVitrin.fbx", build_tv_unit_vitrin),
        ("Interior_TurkishKitchen.fbx", build_turkish_kitchen),
        ("Interior_BedroomSet.fbx", build_bedroom_set),
        ("Interior_MarketShelf.fbx", build_market_shelf),
        ("Interior_DrinkFridge.fbx", build_drink_fridge),
        ("Interior_MarketCounter.fbx", build_market_counter),
        ("Interior_PlumberBench.fbx", build_plumber_bench),
        ("Interior_KombiDisplay.fbx", build_kombi_display),
        ("Interior_GarageTools.fbx", build_garage_tools),
    ]
    print(f"[GRIDROAM Blender] Sculpting {len(builders)} authentic Turkish Home & Shop Interior 3D models...")
    for fbx_name, fn in builders:
        reset_scene()
        mats = get_mats()
        obj = fn(mats)
        export_fbx(obj, fbx_name)
    render_interior_showcase()


if __name__ == "__main__":
    main()
