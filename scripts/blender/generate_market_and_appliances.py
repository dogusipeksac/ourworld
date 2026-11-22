#!/usr/bin/env python3
"""
GRIDROAM Realistic Market Equipment & Detailed Multi-Room Home Furniture Generator (Blender 5.3+)
Sculpts 8 brand-new realistic market & Turkish home interior 3D assets:
  Market Realism:
    1. Market_ManavReyonu.fbx       (Slanted wooden produce stand w/ tomatoes, peppers, oranges, lemons & crates)
    2. Market_SarkuteriDolabi.fbx   (Curved-glass deli/cheese/olive display fridge w/ cheese blocks, sucuk & trays)
    3. Market_DondurmaDolabi.fbx    (Sliding-glass chest ice-cream freezer w/ colorful tubs)
    4. Market_KasaBandi.fbx         (Supermarket checkout conveyor, POS screen, scanner, candy rack & red baskets)
  Multi-Room Home Details (1+0, 1+1, 2+1, 3+1):
    5. Interior_DiningSet.fbx       (Walnut Turkish dining table w/ 4 sculpted chairs & fruit bowl)
    6. Interior_Wardrobe.fbx        (3-door wooden bedroom wardrobe w/ center mirror & chrome handles)
    7. Interior_StudyDesk.fbx       (Desk, swivel chair, laptop/monitor screen, desk lamp & bookshelf stack)
    8. Interior_BathroomSet.fbx     (Ceramic sink vanity + mirror + front-loading Çamaşır Makinesi + toilet)
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
    "GRIDROAM_MARKET_PREVIEW",
    os.path.join(SCRIPT_DIR, "blender_market_appliances.png"),
)

os.makedirs(OUT_FBX_DIR, exist_ok=True)
os.makedirs(BLEND_OUT_DIR, exist_ok=True)


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


def get_mats():
    return {
        "Wood": make_mat("Wood", (0.54, 0.34, 0.18), 0.52),
        "DarkWood": make_mat("DarkWood", (0.28, 0.18, 0.10), 0.56),
        "WhitePVC": make_mat("WhitePVC", (0.94, 0.94, 0.93), 0.30),
        "CreamPaint": make_mat("CreamPaint", (0.90, 0.87, 0.80), 0.68),
        "TrimDark": make_mat("TrimDark", (0.12, 0.13, 0.15), 0.45, 0.35),
        "Chrome": make_mat("Chrome", (0.86, 0.88, 0.90), 0.14, 0.92),
        "Glass": make_mat("Glass", (0.12, 0.20, 0.28), 0.08, 0.22),
        "Galvanized": make_mat("Galvanized", (0.68, 0.70, 0.72), 0.42, 0.68),
        "SignRed": make_mat("SignRed", (0.78, 0.12, 0.12), 0.38, 0.25),
        "CobaltBlue": make_mat("CobaltBlue", (0.10, 0.38, 0.78), 0.42, 0.25),
        "CanRed": make_mat("CanRed", (0.82, 0.14, 0.12), 0.36),
        "CanGreen": make_mat("CanGreen", (0.14, 0.58, 0.22), 0.38),
        "CanOrange": make_mat("CanOrange", (0.92, 0.48, 0.10), 0.38),
        "CanBlue": make_mat("CanBlue", (0.15, 0.44, 0.82), 0.36),
        "AccentMustard": make_mat("AccentMustard", (0.92, 0.76, 0.16), 0.42),
        "AccentTeal": make_mat("AccentTeal", (0.16, 0.44, 0.46), 0.78),
        "LitScreen": make_mat("LitScreen", (0.88, 0.94, 1.0), 0.20),
        "RedPlaster": make_mat("RedPlaster", (0.62, 0.18, 0.14), 0.70),
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


def add_box(name, center, size, mat, bevel=0.014, rot_euler=None):
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


def add_sphere(name, center, radius, mat):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=radius)
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


# 1. MARKET MANAV REYONU (Slanted wooden produce stand w/ tomatoes, peppers, oranges, lemons!)
def build_manav_reyonu(mats):
    parts = []
    # Main wooden frame & slanted produce deck (2.1m wide x 0.92m deep)
    parts.append(add_box("Manav_Base", (0.0, 0.0, 0.36), (2.10, 0.88, 0.72), mats["DarkWood"], bevel=0.02))
    parts.append(add_box("Manav_SlantDeck", (0.0, 0.02, 0.84), (2.14, 0.92, 0.06), mats["Wood"], rot_euler=(0.24, 0, 0)))
    # Top green-and-white striped produce awning canopy frame
    parts.append(add_cyl("Manav_PostL", (-1.00, -0.36, 1.10), 0.03, 0.03, 1.95, 8, mats["TrimDark"]))
    parts.append(add_cyl("Manav_PostR", (1.00, -0.36, 1.10), 0.03, 0.03, 1.95, 8, mats["TrimDark"]))
    parts.append(add_box("Manav_Canopy", (0.0, -0.02, 2.02), (2.20, 0.86, 0.08), mats["CanGreen"], rot_euler=(0.22, 0, 0)))
    parts.append(add_box("Manav_Header", (0.0, 0.40, 1.92), (2.18, 0.05, 0.22), mats["SignRed"]))

    # 4 Wooden produce crates filled with tomatoes (red), oranges (orange), lemons (yellow), peppers/greens (green)!
    produce_mats = [mats["CanRed"], mats["CanOrange"], mats["AccentMustard"], mats["CanGreen"]]
    for ci, cx in enumerate((-0.76, -0.25, 0.25, 0.76)):
        parts.append(add_box(f"Crate_{ci}", (cx, 0.04, 0.93), (0.46, 0.68, 0.14), mats["Wood"], rot_euler=(0.24, 0, 0)))
        pm = produce_mats[ci]
        for gx in (-0.12, 0.0, 0.12):
            for gy in (-0.18, 0.0, 0.18):
                pz = 1.02 - gy * 0.24
                parts.append(add_sphere(f"Fruit_{ci}_{gx:.2f}_{gy:.2f}", (cx + gx, 0.04 + gy, pz), 0.058, pm))
    return join_all(parts, "Market_ManavReyonu")


# 2. MARKET ŞARKÜTERİ DOLABI (Curved-glass deli fridge w/ cheese blocks, olives & sucuk!)
def build_sarkuteri_dolabi(mats):
    parts = []
    # Lower chilled cabinet (2.05m wide x 0.95m deep x 1.28m tall)
    parts.append(add_box("Sark_Lower", (0.0, 0.0, 0.44), (2.05, 0.92, 0.88), mats["WhitePVC"], bevel=0.02))
    parts.append(add_box("Sark_FrontStripe", (0.0, 0.47, 0.42), (2.00, 0.03, 0.22), mats["SignRed"]))
    parts.append(add_box("Sark_SteelDeck", (0.0, 0.0, 0.89), (1.96, 0.82, 0.04), mats["Chrome"]))
    # Angled glass sneeze guard & top display shelf
    parts.append(add_box("Sark_GlassFront", (0.0, 0.36, 1.14), (2.00, 0.03, 0.52), mats["Glass"], rot_euler=(-0.30, 0, 0)))
    parts.append(add_box("Sark_GlassTop", (0.0, 0.16, 1.38), (2.02, 0.32, 0.04), mats["Glass"]))
    parts.append(add_box("Sark_SideL", (-1.01, 0.0, 1.12), (0.05, 0.88, 0.54), mats["Galvanized"]))
    parts.append(add_box("Sark_SideR", (1.01, 0.0, 1.12), (0.05, 0.88, 0.54), mats["Galvanized"]))
    # Trays of Beyaz Peynir (white cheese), Kaşar (yellow), Siyah Zeytin (dark), Sucuk (red)!
    trays = [(-0.68, mats["WhitePVC"]), (-0.22, mats["AccentMustard"]), (0.22, mats["TrimDark"]), (0.68, mats["RedPlaster"])]
    for ti, (tx, tm) in enumerate(trays):
        parts.append(add_box(f"Deli_Tray_{ti}", (tx, 0.02, 0.93), (0.38, 0.56, 0.04), mats["Chrome"]))
        parts.append(add_box(f"Deli_Food_{ti}", (tx, 0.02, 1.01), (0.32, 0.44, 0.12), tm, bevel=0.025))
    return join_all(parts, "Market_SarkuteriDolabi")


# 3. MARKET DONDURMA DOLABI (Classic Turkish sliding-glass ice-cream chest freezer!)
def build_dondurma_dolabi(mats):
    parts = []
    parts.append(add_box("Ice_Body", (0.0, 0.0, 0.44), (1.42, 0.72, 0.88), mats["WhitePVC"], bevel=0.025))
    parts.append(add_box("Ice_Band", (0.0, 0.37, 0.52), (1.38, 0.03, 0.34), mats["CobaltBlue"]))
    parts.append(add_box("Ice_Accent", (0.0, 0.38, 0.72), (1.38, 0.03, 0.10), mats["SignRed"]))
    parts.append(add_box("Ice_Rim", (0.0, 0.0, 0.90), (1.44, 0.74, 0.06), mats["TrimDark"]))
    parts.append(add_box("Ice_GlassTop", (0.0, 0.0, 0.92), (1.30, 0.60, 0.02), mats["Glass"]))
    # Colorful ice-cream tubs visible inside
    tub_mats = [mats["CanRed"], mats["AccentMustard"], mats["CanGreen"], mats["CanOrange"]]
    for i, tx in enumerate((-0.44, -0.15, 0.15, 0.44)):
        parts.append(add_box(f"Ice_Tub_{i}", (tx, 0.0, 0.80), (0.24, 0.44, 0.16), tub_mats[i], bevel=0.02))
    return join_all(parts, "Market_DondurmaDolabi")


# 4. MARKET KASA BANDI (Supermarket checkout conveyor + POS monitor + barcode scanner + red baskets!)
def build_kasa_bandi(mats):
    parts = []
    # Main checkout body (2.15m long x 0.88m deep x 0.88m tall)
    parts.append(add_box("Kasa_Body", (0.0, 0.0, 0.44), (2.15, 0.86, 0.88), mats["CobaltBlue"], bevel=0.02))
    parts.append(add_box("Kasa_TopTrim", (0.0, 0.0, 0.89), (2.18, 0.90, 0.04), mats["Chrome"]))
    # Black rubber conveyor belt on right half (+X)
    parts.append(add_box("Kasa_Belt", (0.42, 0.0, 0.92), (1.18, 0.50, 0.03), mats["TrimDark"]))
    # POS touchscreen monitor + barcode scanner + cash drawer on left half (-X)
    parts.append(add_box("Kasa_Drawer", (-0.62, 0.05, 0.96), (0.46, 0.42, 0.12), mats["TrimDark"]))
    parts.append(add_cyl("Kasa_Pole", (-0.68, -0.22, 1.18), 0.025, 0.025, 0.48, 8, mats["Chrome"]))
    parts.append(add_box("Kasa_Monitor", (-0.68, -0.18, 1.38), (0.38, 0.05, 0.28), mats["TrimDark"]))
    parts.append(add_box("Kasa_Screen", (-0.68, -0.15, 1.38), (0.34, 0.02, 0.24), mats["LitScreen"]))
    # Stack of red shopping baskets (Alışveriş Sepeti) right beside checkout!
    for bi in range(4):
        parts.append(add_box(f"Basket_{bi}", (1.32, 0.0, 0.14 + bi * 0.12), (0.42, 0.32, 0.18), mats["SignRed"], bevel=0.015))
    return join_all(parts, "Market_KasaBandi")


# 5. TURKISH DINING TABLE + 4 SCULPTED CHAIRS
def build_dining_set(mats):
    parts = []
    # Walnut table (1.55m x 0.90m x 0.76m)
    parts.append(add_box("Din_Top", (0.0, 0.0, 0.75), (1.55, 0.90, 0.055), mats["Wood"], bevel=0.015))
    for lx in (-0.66, 0.66):
        for ly in (-0.36, 0.36):
            parts.append(add_box(f"Din_Leg_{lx}_{ly}", (lx, ly, 0.37), (0.06, 0.06, 0.74), mats["DarkWood"]))
    # Fruit centerpiece bowl
    parts.append(add_cyl("Din_Bowl", (0.0, 0.0, 0.82), 0.12, 0.18, 0.09, 14, mats["WhitePVC"]))
    parts.append(add_sphere("Din_Apple1", (-0.04, 0.02, 0.89), 0.045, mats["CanRed"]))
    parts.append(add_sphere("Din_Apple2", (0.05, -0.02, 0.89), 0.045, mats["CanOrange"]))
    # 4 Dining chairs tucked neatly around table
    chair_spots = [(-0.42, 0.56, 0.0), (0.42, 0.56, 0.0), (-0.42, -0.56, math.pi), (0.42, -0.56, math.pi)]
    for ci, (cx, cy, rot_z) in enumerate(chair_spots):
        s_dir = 1.0 if cy > 0 else -1.0
        parts.append(add_box(f"Ch_Seat_{ci}", (cx, cy, 0.45), (0.44, 0.42, 0.05), mats["CreamPaint"], bevel=0.015))
        parts.append(add_box(f"Ch_Back_{ci}", (cx, cy + s_dir * 0.19, 0.74), (0.42, 0.05, 0.54), mats["DarkWood"], bevel=0.012))
        for dx in (-0.18, 0.18):
            for dy in (-0.17, 0.17):
                parts.append(add_box(f"Ch_Leg_{ci}_{dx}_{dy}", (cx + dx, cy + dy, 0.22), (0.04, 0.04, 0.44), mats["DarkWood"]))
    return join_all(parts, "Interior_DiningSet")


# 6. 3-DOOR BEDROOM WARDROBE (GARDIROP W/ CENTER MIRROR)
def build_wardrobe(mats):
    parts = []
    parts.append(add_box("Ward_Body", (0.0, 0.0, 1.05), (1.72, 0.58, 2.10), mats["Wood"], bevel=0.02))
    # 3 front doors (left wood, center mirror glass, right wood) + chrome handles
    parts.append(add_box("Ward_DoorL", (-0.56, 0.30, 1.05), (0.54, 0.03, 2.00), mats["CreamPaint"], bevel=0.01))
    parts.append(add_box("Ward_MirrorC", (0.0, 0.305, 1.08), (0.52, 0.025, 1.78), mats["Glass"]))
    parts.append(add_box("Ward_DoorR", (0.56, 0.30, 1.05), (0.54, 0.03, 2.00), mats["CreamPaint"], bevel=0.01))
    for hx in (-0.34, 0.34):
        parts.append(add_box(f"Ward_Handle_{hx:.2f}", (hx, 0.33, 1.06), (0.025, 0.04, 0.26), mats["Chrome"]))
    return join_all(parts, "Interior_Wardrobe")


# 7. STUDY & WORK DESK + CHAIR + LAPTOP/MONITOR + BOOKS + LAMP
def build_study_desk(mats):
    parts = []
    # Desk (1.35m x 0.65m x 0.76m)
    parts.append(add_box("Desk_Top", (0.0, 0.0, 0.75), (1.35, 0.65, 0.045), mats["Wood"], bevel=0.012))
    parts.append(add_box("Desk_Drawers", (0.44, 0.0, 0.37), (0.42, 0.58, 0.72), mats["CreamPaint"], bevel=0.015))
    parts.append(add_box("Desk_LegL1", (-0.62, -0.26, 0.37), (0.05, 0.05, 0.74), mats["TrimDark"]))
    parts.append(add_box("Desk_LegL2", (-0.62, 0.26, 0.37), (0.05, 0.05, 0.74), mats["TrimDark"]))
    # Desktop monitor + keyboard
    parts.append(add_box("PC_Stand", (-0.12, -0.16, 0.84), (0.14, 0.12, 0.14), mats["TrimDark"]))
    parts.append(add_box("PC_Bezel", (-0.12, -0.18, 1.06), (0.62, 0.04, 0.38), mats["TrimDark"], bevel=0.01))
    parts.append(add_box("PC_Screen", (-0.12, -0.155, 1.06), (0.56, 0.015, 0.32), mats["LitScreen"]))
    parts.append(add_box("PC_Keyboard", (-0.12, 0.08, 0.79), (0.42, 0.14, 0.025), mats["TrimDark"]))
    # Stack of colorful books + desk lamp
    parts.append(add_box("Book_1", (0.45, -0.08, 0.80), (0.22, 0.16, 0.045), mats["CanRed"]))
    parts.append(add_box("Book_2", (0.45, -0.08, 0.85), (0.20, 0.15, 0.04), mats["CobaltBlue"], rot_euler=(0, 0, 0.18)))
    # Office swivel chair tucked in front
    parts.append(add_cyl("Chair_Base", (-0.12, 0.52, 0.22), 0.24, 0.04, 0.44, 10, mats["TrimDark"]))
    parts.append(add_box("Chair_Seat", (-0.12, 0.52, 0.46), (0.48, 0.46, 0.07), mats["AccentTeal"], bevel=0.02))
    parts.append(add_box("Chair_Back", (-0.12, 0.74, 0.76), (0.46, 0.07, 0.54), mats["AccentTeal"], bevel=0.02))
    return join_all(parts, "Interior_StudyDesk")


# 8. BATHROOM VANITY SINK + MIRROR + FRONT-LOADING ÇAMAŞIR MAKİNESİ + TOILET
def build_bathroom_set(mats):
    parts = []
    # Front-loading White Washing Machine (Çamaşır Makinesi!) on left (-X = -0.82)
    parts.append(add_box("Wash_Body", (-0.82, 0.0, 0.44), (0.64, 0.60, 0.88), mats["WhitePVC"], bevel=0.02))
    parts.append(add_cyl("Wash_DrumRim", (-0.82, 0.31, 0.42), 0.21, 0.21, 0.04, 18, mats["Chrome"], rot_euler=(math.pi * 0.5, 0, 0)))
    parts.append(add_cyl("Wash_DrumGlass", (-0.82, 0.325, 0.42), 0.16, 0.16, 0.02, 16, mats["Glass"], rot_euler=(math.pi * 0.5, 0, 0)))
    parts.append(add_box("Wash_Panel", (-0.82, 0.31, 0.78), (0.56, 0.02, 0.14), mats["Galvanized"]))
    # Ceramic Vanity Sink + Wall Mirror Cabinet in center (X = 0.0)
    parts.append(add_box("Sink_Cab", (0.0, -0.02, 0.42), (0.76, 0.48, 0.82), mats["WhitePVC"], bevel=0.015))
    parts.append(add_box("Sink_Basin", (0.0, 0.0, 0.86), (0.78, 0.50, 0.08), mats["CreamPaint"], bevel=0.02))
    parts.append(add_cyl("Sink_Faucet", (0.0, -0.18, 0.98), 0.018, 0.015, 0.18, 8, mats["Chrome"]))
    parts.append(add_box("Sink_Mirror", (0.0, -0.22, 1.54), (0.68, 0.04, 0.78), mats["Glass"]))
    parts.append(add_box("Sink_MirrorFrame", (0.0, -0.23, 1.54), (0.74, 0.03, 0.84), mats["WhitePVC"]))
    # White ceramic toilet on right (+X = +0.78)
    parts.append(add_box("WC_Tank", (0.78, -0.18, 0.62), (0.42, 0.18, 0.46), mats["WhitePVC"], bevel=0.02))
    parts.append(add_cyl("WC_Bowl", (0.78, 0.04, 0.24), 0.18, 0.22, 0.44, 16, mats["WhitePVC"]))
    parts.append(add_cyl("WC_Seat", (0.78, 0.04, 0.47), 0.22, 0.22, 0.04, 16, mats["WhitePVC"]))
    return join_all(parts, "Interior_BathroomSet")


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


def render_preview():
    reset_scene()
    mats = get_mats()
    bm_g = bmesh.new()
    bmesh.ops.create_grid(bm_g, x_segments=1, y_segments=1, size=24.0)
    make_obj("Floor", bm_g, make_mat("Floor", (0.64, 0.62, 0.58), 0.60))

    m1 = build_manav_reyonu(mats); m1.location = Vector((-4.2, -1.6, 0))
    m2 = build_sarkuteri_dolabi(mats); m2.location = Vector((-1.4, -1.6, 0))
    m3 = build_dondurma_dolabi(mats); m3.location = Vector((1.2, -1.6, 0))
    m4 = build_kasa_bandi(mats); m4.location = Vector((4.0, -1.6, 0))

    h1 = build_dining_set(mats); h1.location = Vector((-3.8, 1.8, 0))
    h2 = build_wardrobe(mats); h2.location = Vector((-1.0, 1.8, 0))
    h3 = build_study_desk(mats); h3.location = Vector((1.5, 1.8, 0))
    h4 = build_bathroom_set(mats); h4.location = Vector((4.2, 1.8, 0))

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
    sun_obj.rotation_euler = Euler((math.radians(45), math.radians(15), math.radians(150)), "XYZ")

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 28.0
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((0.0, 9.2, 4.8))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 0.8)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam_obj

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_OUT_DIR, "GRIDROAM_Market_And_Appliances.blend"))
    bpy.ops.render.render(write_still=True)
    print("Saved market & appliances preview to:", PREVIEW_PATH)


def main():
    builders = [
        ("Market_ManavReyonu.fbx", build_manav_reyonu),
        ("Market_SarkuteriDolabi.fbx", build_sarkuteri_dolabi),
        ("Market_DondurmaDolabi.fbx", build_dondurma_dolabi),
        ("Market_KasaBandi.fbx", build_kasa_bandi),
        ("Interior_DiningSet.fbx", build_dining_set),
        ("Interior_Wardrobe.fbx", build_wardrobe),
        ("Interior_StudyDesk.fbx", build_study_desk),
        ("Interior_BathroomSet.fbx", build_bathroom_set),
    ]
    print(f"[GRIDROAM Blender] Sculpting {len(builders)} realistic market & multi-room home assets...")
    for fbx_name, fn in builders:
        reset_scene()
        mats = get_mats()
        obj = fn(mats)
        export_fbx(obj, fbx_name)
    render_preview()


if __name__ == "__main__":
    main()
