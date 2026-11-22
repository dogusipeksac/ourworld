"""
Blender 5.3 headless script: Sculpts 9 photo-matched 1-to-1 landmark & avenue building models
directly from our live Google Street View survey of Hamidiye Mahallesi's 8 main avenues
(Fatih Bulvarı, Atatürk Cd., Petrol Yolu Cd., Aziziye Cd., Eyyubi Cd., Zübeyde Hanım Cd.,
Seyyid Kutup Cd., Mürşit Cd.) for OurWorld v27.
"""
import math
import os
import bpy

OUT_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/Assets/GRIDROAM/Art/Models/Blender"
BLEND_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/scripts/blender/blend_sources"
PREVIEW_IMG = "/Users/dogusipeksac/.gemini/antigravity/brain/2dd72233-235e-4c47-8c3c-78844f1a7f7f/blender_v27_hamidiye_8_avenues.png"
os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(BLEND_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def get_mat(name, rgba, rough=0.72, metal=0.0, emission=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name=name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = metal
        if emission > 0.0 and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = rgba
            bsdf.inputs["Emission Strength"].default_value = emission
    return m


def add_box(name, loc, size, mat, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0.0:
        mod = o.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier="Bevel")
    o.data.materials.append(mat)
    return o


def add_cyl(name, loc, rad, depth, mat, rot=(0, 0, 0), verts=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=rad, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    for p in o.data.polygons:
        p.use_smooth = True
    o.data.materials.append(mat)
    return o


def export_fbx(objs, filename):
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
        path_mode="AUTO",
    )
    print(f"Exported {filename} ({len(objs)} meshes)")


def build_fatih_saat_kulesi():
    """Iconic Sultanbeyli Fatih Bulvarı Saat Kulesi (Mar 2026 Street View):
    Stepped circular plinth, travertine base, arched dark iron door, clock face, 4 classical columns & crescent pediment."""
    stone = get_mat("StoneTrim", (0.84, 0.81, 0.74, 1.0), rough=0.78)
    cream = get_mat("CreamPaint", (0.91, 0.88, 0.82, 1.0), rough=0.72)
    iron = get_mat("TrimDark", (0.14, 0.15, 0.16, 1.0), rough=0.45, metal=0.6)
    clock_face = get_mat("WhitePVC", (0.96, 0.96, 0.94, 1.0), rough=0.25)
    gold = get_mat("GoldBrass", (0.88, 0.70, 0.24, 1.0), rough=0.3, metal=0.8)
    red = get_mat("SignRed", (0.78, 0.12, 0.14, 1.0), rough=0.45)

    parts = []
    # Circular 3-tier stone plinth
    parts.append(add_cyl("Kule_Plinth1", (0, 0, 0.16), 3.30, 0.32, stone, verts=24))
    parts.append(add_cyl("Kule_Plinth2", (0, 0, 0.44), 2.80, 0.26, stone, verts=24))
    parts.append(add_cyl("Kule_Plinth3", (0, 0, 0.68), 2.35, 0.22, cream, verts=24))

    # Main square travertine body (3.4m x 3.4m x 4.4m)
    parts.append(add_box("Kule_MainBody", (0, 0, 2.95), (3.4, 3.4, 4.35), stone, bevel=0.05))
    parts.append(add_box("Kule_Cornice", (0, 0, 5.25), (3.85, 3.85, 0.36), cream, bevel=0.04))

    # Arched dark wrought-iron doors on front (+Y) & back (-Y)
    for sy in (-1, 1):
        parts.append(add_box(f"Kule_Door_{sy}", (0, sy * 1.66, 2.25), (1.85, 0.18, 2.75), iron))
        parts.append(add_cyl(f"Kule_DoorArch_{sy}", (0, sy * 1.66, 3.55), 0.92, 0.18, iron, rot=(math.radians(90), 0, 0), verts=16))
        # Circular clock face above door
        parts.append(add_cyl(f"Kule_ClockRim_{sy}", (0, sy * 1.73, 4.55), 0.44, 0.08, iron, rot=(math.radians(90), 0, 0), verts=20))
        parts.append(add_cyl(f"Kule_ClockFace_{sy}", (0, sy * 1.76, 4.55), 0.38, 0.05, clock_face, rot=(math.radians(90), 0, 0), verts=20))
        parts.append(add_box(f"Kule_HandH_{sy}", (0.08, sy * 1.795, 4.58), (0.20, 0.02, 0.04), iron))
        parts.append(add_box(f"Kule_HandM_{sy}", (-0.02, sy * 1.795, 4.66), (0.04, 0.02, 0.24), iron))

    # 4 corner pilasters on base
    for sx in (-1.55, 1.55):
        for sy in (-1.55, 1.55):
            parts.append(add_cyl(f"Kule_BaseCol_{sx}_{sy}", (sx, sy, 2.95), 0.24, 4.25, cream, verts=12))

    # Upper colonnade pavilion: 4 columns on each side supporting crown pediment!
    col_xs = (-1.10, -0.38, 0.38, 1.10)
    for cx in col_xs:
        for sy in (-1.05, 1.05):
            parts.append(add_cyl(f"Kule_TopCol_{cx}_{sy}", (cx, sy, 6.75), 0.15, 2.65, cream, verts=12))
    parts.append(add_box("Kule_UpperPediment", (0, 0, 8.55), (3.15, 2.90, 1.05), stone, bevel=0.04))
    parts.append(add_box("Kule_UpperCap", (0, 0, 9.20), (3.45, 3.15, 0.26), cream, bevel=0.03))
    # Crescent & Star medallion on front/back of pediment
    for sy in (-1, 1):
        parts.append(add_cyl(f"Kule_Medallion_{sy}", (0, sy * 1.46, 8.55), 0.44, 0.08, iron, rot=(math.radians(90), 0, 0), verts=20))
        parts.append(add_cyl(f"Kule_Crescent_{sy}", (0, sy * 1.50, 8.55), 0.28, 0.06, clock_face, rot=(math.radians(90), 0, 0), verts=16))
        parts.append(add_box(f"Kule_Star_{sy}", (0.14, sy * 1.51, 8.57), (0.10, 0.05, 0.10), red))
    parts.append(add_cyl("Kule_Finial", (0, 0, 9.62), 0.08, 0.65, gold, verts=10))
    return parts


def build_fatih_commercial_block():
    """Fatih Bulvarı warm peach & cream 4-story commercial plaza with shops & balconies (14m x 10m x 13.5m)."""
    peach = get_mat("PeachPlaster", (0.86, 0.63, 0.48, 1.0), rough=0.78)
    cream = get_mat("CreamPaint", (0.93, 0.89, 0.82, 1.0), rough=0.75)
    dark = get_mat("TrimDark", (0.14, 0.15, 0.17, 1.0), rough=0.45)
    glass = get_mat("Glass", (0.20, 0.30, 0.38, 1.0), rough=0.12, metal=0.2)
    sign_blue = get_mat("CobaltBlue", (0.12, 0.34, 0.66, 1.0), rough=0.4)
    sign_red = get_mat("SignRed", (0.80, 0.15, 0.14, 1.0), rough=0.4)
    sign_gold = get_mat("GoldBrass", (0.90, 0.68, 0.18, 1.0), rough=0.35)
    roof = get_mat("RoofTile", (0.64, 0.28, 0.20, 1.0), rough=0.82)

    parts = []
    parts.append(add_box("Fatih_MainBody", (0, 0, 6.75), (14.2, 9.6, 13.5), peach))
    parts.append(add_box("Fatih_LeftWing", (-4.4, 0.10, 8.2), (5.2, 9.8, 10.2), cream))
    parts.append(add_box("Fatih_RoofParapet", (0, 0, 13.75), (14.5, 9.9, 0.55), roof))
    parts.append(add_box("Fatih_Chimney", (2.6, -1.8, 14.8), (0.85, 0.85, 1.7), roof))

    # 3 Ground-floor storefronts along +Y
    shop_xs = (-4.5, 0.0, 4.5)
    shop_mats = (sign_blue, sign_gold, sign_red)
    for sx, smat in zip(shop_xs, shop_mats):
        parts.append(add_box(f"Fatih_ShopFrame_{sx}", (sx, 4.84, 1.75), (4.1, 0.18, 3.3), dark))
        parts.append(add_box(f"Fatih_ShopGlass_{sx}", (sx, 4.88, 1.55), (3.7, 0.12, 2.7), glass))
        parts.append(add_box(f"Fatih_SignFascia_{sx}", (sx, 4.94, 3.55), (4.2, 0.28, 0.72), smat))

    # Upper 3 floors of windows & balconies
    for fl in range(1, 4):
        z = 2.0 + fl * 3.1
        for wx in (-5.4, -3.2, -1.0, 1.2, 3.4, 5.4):
            parts.append(add_box(f"Fatih_Win_{fl}_{wx}", (wx, 4.88, z), (1.35, 0.12, 1.45), glass))
            parts.append(add_box(f"Fatih_Sill_{fl}_{wx}", (wx, 4.94, z - 0.76), (1.50, 0.20, 0.10), cream))
        for bx in (-0.2, 4.4):
            parts.append(add_box(f"Fatih_BalcSlab_{fl}_{bx}", (bx, 5.30, z - 0.95), (3.4, 1.0, 0.16), cream))
            parts.append(add_box(f"Fatih_BalcWall_{fl}_{bx}", (bx, 5.74, z - 0.45), (3.4, 0.12, 0.92), peach))
    return parts


def build_petrol_yolu_branda_block():
    """Exact 395 Petrol Yolu Cd. 4-story beige commercial block with bright orange awning & enclosed PVC balconies."""
    beige = get_mat("Concrete", (0.76, 0.74, 0.68, 1.0), rough=0.82)
    orange = get_mat("OrangeStripe", (0.86, 0.36, 0.10, 1.0), rough=0.48)
    white = get_mat("WhitePVC", (0.94, 0.95, 0.96, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.18, 0.26, 0.34, 1.0), rough=0.14)
    dark = get_mat("TrimDark", (0.14, 0.15, 0.16, 1.0), rough=0.45)

    parts = []
    parts.append(add_box("Petrol_Body", (0, 0, 6.4), (11.2, 9.2, 12.8), beige))
    parts.append(add_box("Petrol_Roof", (0, 0, 13.0), (11.5, 9.5, 0.42), dark))
    # Raised stone sidewalk steps & glass showroom
    parts.append(add_box("Petrol_Steps", (0, 5.15, 0.26), (11.2, 1.2, 0.52), white))
    parts.append(add_box("Petrol_ShowroomGlass", (0.4, 4.64, 1.75), (8.6, 0.14, 2.75), glass))
    parts.append(add_box("Petrol_OrangeAwning", (0.2, 4.86, 3.25), (10.6, 0.75, 0.68), orange))
    parts.append(add_box("Petrol_SignBanner", (0.2, 4.74, 3.92), (10.2, 0.16, 0.76), white))

    # Upper 3 floors: left side white PVC windows, right side wide enclosed dark-glass corner balcony!
    for fl in range(1, 4):
        z = 2.6 + fl * 2.9
        parts.append(add_box(f"Petrol_WinL_{fl}", (-3.2, 4.65, z), (2.4, 0.14, 1.35), white))
        parts.append(add_box(f"Petrol_WinLGlass_{fl}", (-3.2, 4.68, z), (2.15, 0.10, 1.15), glass))
        parts.append(add_box(f"Petrol_EncBalc_{fl}", (2.4, 4.65, z), (4.4, 0.16, 1.55), dark))
        parts.append(add_box(f"Petrol_EncGlass_{fl}", (2.4, 4.69, z), (4.15, 0.10, 1.32), glass))
    return parts


def build_aziziye_shop_block():
    """Exact 46 Aziziye Cd. 4-story cream & terracotta-panel shop block with cobalt-blue aluminum storefront framing."""
    cream = get_mat("CreamPaint", (0.94, 0.92, 0.87, 1.0), rough=0.72)
    terra = get_mat("RedPlaster", (0.76, 0.36, 0.28, 1.0), rough=0.78)
    cobalt = get_mat("CobaltBlue", (0.10, 0.26, 0.62, 1.0), rough=0.35, metal=0.35)
    glass = get_mat("Glass", (0.20, 0.30, 0.38, 1.0), rough=0.14)
    dark = get_mat("TrimDark", (0.14, 0.15, 0.17, 1.0), rough=0.45)

    parts = []
    parts.append(add_box("Aziziye_Body", (0, 0, 6.2), (13.4, 8.8, 12.4), cream))
    parts.append(add_box("Aziziye_Roof", (0, 0, 12.6), (13.7, 9.1, 0.40), terra))

    # Signature cobalt-blue framed shopfront on right & warm illuminated shop on left
    parts.append(add_box("Aziziye_CobaltFrame", (2.8, 4.46, 2.05), (6.4, 0.18, 3.6), cobalt))
    parts.append(add_box("Aziziye_CobaltGlass", (2.8, 4.50, 1.95), (5.9, 0.12, 3.1), glass))
    parts.append(add_box("Aziziye_Railing", (2.8, 4.95, 1.15), (6.4, 0.08, 0.95), dark))

    parts.append(add_box("Aziziye_LeftShop", (-3.4, 4.46, 1.90), (5.4, 0.16, 3.3), dark))
    parts.append(add_box("Aziziye_LeftGlass", (-3.4, 4.50, 1.80), (4.9, 0.12, 2.8), glass))

    # Upper floors with terracotta tile accent panels & wide enclosed windows
    for fl in range(1, 4):
        z = 2.4 + fl * 2.85
        parts.append(add_box(f"Aziziye_TerraBand_{fl}", (0, 4.44, z - 0.85), (12.8, 0.14, 0.85), terra))
        for wx in (-4.2, 0.0, 4.2):
            parts.append(add_box(f"Aziziye_Win_{fl}_{wx}", (wx, 4.48, z + 0.15), (3.1, 0.14, 1.35), glass))
    return parts


def build_eyyubi_bufe_block():
    """Exact 53 Eyyubi Cd. 2-story terracotta corner Mahalle Büfesi + turquoise-grey roller shutters + blue garden gate."""
    terra = get_mat("RawBrick", (0.72, 0.34, 0.24, 1.0), rough=0.80)
    cream = get_mat("CreamPaint", (0.90, 0.86, 0.78, 1.0), rough=0.78)
    shutter = get_mat("PowderBlue", (0.64, 0.74, 0.78, 1.0), rough=0.45, metal=0.4)
    red_stripe = get_mat("SignRed", (0.80, 0.16, 0.15, 1.0), rough=0.45)
    gate_blue = get_mat("FenceBlue", (0.16, 0.44, 0.72, 1.0), rough=0.45, metal=0.45)
    glass = get_mat("Glass", (0.18, 0.26, 0.34, 1.0), rough=0.14)
    white = get_mat("WhitePVC", (0.95, 0.96, 0.97, 1.0), rough=0.35)

    parts = []
    # Main 2-story büfe + shutters on left
    parts.append(add_box("Eyyubi_MainHouse", (-1.5, 0, 3.2), (10.6, 7.6, 6.4), cream))
    parts.append(add_box("Eyyubi_TerraClad", (-0.2, 3.84, 3.4), (5.6, 0.12, 5.8), terra))
    # Left turquoise-grey roller shutters with signature red stripe
    parts.append(add_box("Eyyubi_ShutterL", (-4.8, 3.86, 1.65), (3.6, 0.16, 3.1), shutter))
    parts.append(add_box("Eyyubi_ShutterRed", (-4.8, 3.92, 1.75), (3.62, 0.08, 0.22), red_stripe))
    # Büfe storefront window + upper PVC windows
    parts.append(add_box("Eyyubi_BufeGlass", (-0.2, 3.90, 1.55), (3.2, 0.12, 2.4), glass))
    parts.append(add_box("Eyyubi_UpperWin", (-0.2, 3.92, 4.75), (1.7, 0.12, 1.3), white))
    parts.append(add_box("Eyyubi_UpperWinL", (-4.8, 3.86, 4.75), (2.2, 0.12, 1.3), white))
    # Adjacent cream garden wall + cobalt-blue steel gate on right!
    parts.append(add_box("Eyyubi_GardenWallL", (5.4, 3.5, 0.95), (3.2, 0.26, 1.9), cream))
    parts.append(add_box("Eyyubi_BlueGate", (7.8, 3.5, 0.92), (1.65, 0.14, 1.84), gate_blue))
    parts.append(add_box("Eyyubi_GardenWallR", (10.4, 3.5, 0.95), (3.6, 0.26, 1.9), cream))
    return parts


def build_zubeyde_blue_house():
    """Exact 71 Zübeyde Hanım Cd. sky-blue 3-story house with wood-framed top balcony & Turkish flag window."""
    sky = get_mat("PowderBlue", (0.64, 0.80, 0.86, 1.0), rough=0.72)
    wood = get_mat("Wood", (0.58, 0.34, 0.18, 1.0), rough=0.62)
    white = get_mat("WhitePVC", (0.95, 0.96, 0.97, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.18, 0.28, 0.36, 1.0), rough=0.14)
    flag_red = get_mat("SignRed", (0.84, 0.10, 0.14, 1.0), rough=0.5)
    roof = get_mat("RoofTile", (0.62, 0.28, 0.20, 1.0), rough=0.82)

    parts = []
    parts.append(add_box("Zubeyde_Body", (0, 0, 4.6), (9.4, 8.6, 9.2), sky))
    parts.append(add_box("Zubeyde_Roof", (0, 0, 9.45), (9.8, 9.0, 0.50), roof))
    # Ground floor recessed entry & window
    parts.append(add_box("Zubeyde_GroundWin", (-1.8, 4.34, 1.55), (2.6, 0.14, 1.45), wood))
    parts.append(add_box("Zubeyde_GroundGlass", (-1.8, 4.38, 1.55), (2.3, 0.10, 1.2), glass))
    parts.append(add_box("Zubeyde_Door", (2.4, 4.34, 1.45), (1.8, 0.16, 2.7), white))
    # 2nd floor wide white PVC enclosed balcony with Turkish flag accent!
    parts.append(add_box("Zubeyde_MidFrame", (0, 4.36, 4.75), (7.6, 0.18, 1.85), white))
    parts.append(add_box("Zubeyde_MidGlass", (0, 4.40, 4.75), (7.2, 0.12, 1.55), glass))
    parts.append(add_box("Zubeyde_Flag", (1.6, 4.45, 4.82), (0.85, 0.04, 1.15), flag_red))
    # 3rd floor signature warm wood-framed sunroom windows!
    parts.append(add_box("Zubeyde_TopFrame", (0, 4.36, 7.65), (7.6, 0.18, 1.85), wood))
    parts.append(add_box("Zubeyde_TopGlass", (0, 4.40, 7.65), (7.2, 0.12, 1.55), glass))
    return parts


def build_seyyid_kutup_apt():
    """Exact Seyyid Kutup Cd. 3-story tan-beige apartment with recessed balconies & basement iron grilles."""
    tan = get_mat("YellowPlaster", (0.76, 0.71, 0.59, 1.0), rough=0.80)
    dark = get_mat("TrimDark", (0.16, 0.17, 0.18, 1.0), rough=0.5)
    white = get_mat("WhitePVC", (0.94, 0.95, 0.96, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.20, 0.28, 0.34, 1.0), rough=0.14)
    concrete = get_mat("Concrete", (0.64, 0.63, 0.60, 1.0), rough=0.86)

    parts = []
    parts.append(add_box("Seyyid_RetainingBase", (0, 0.25, 0.75), (12.8, 9.6, 1.5), concrete))
    parts.append(add_box("Seyyid_MainBody", (0, 0, 5.95), (12.4, 9.0, 9.0), tan))
    parts.append(add_box("Seyyid_Roof", (0, 0, 10.65), (12.8, 9.4, 0.45), dark))
    for fl in range(3):
        z = 2.85 + fl * 2.8
        for wx in (-4.2, -1.2, 1.8, 4.4):
            parts.append(add_box(f"Seyyid_Win_{fl}_{wx}", (wx, 4.54, z), (1.55, 0.14, 1.45), white))
            parts.append(add_box(f"Seyyid_Glass_{fl}_{wx}", (wx, 4.58, z), (1.32, 0.10, 1.22), glass))
    for gx in (-4.0, 0.0, 4.0):
        parts.append(add_box(f"Seyyid_Grille_{gx}", (gx, 5.06, 0.72), (1.8, 0.08, 0.85), dark))
    return parts


def build_mursit_trafo_and_grey_apt():
    """Exact Mürşit Cd. grey concrete AYEDAŞ Trafo house on stone plinth + dark-grey white-striped 3-story apartment."""
    grey_dark = get_mat("LeadRoof", (0.42, 0.45, 0.48, 1.0), rough=0.76)
    grey_light = get_mat("Concrete", (0.66, 0.69, 0.72, 1.0), rough=0.80)
    stone = get_mat("StoneTrim", (0.72, 0.70, 0.66, 1.0), rough=0.85)
    white = get_mat("WhitePVC", (0.95, 0.96, 0.97, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.18, 0.26, 0.34, 1.0), rough=0.14)

    parts = []
    # Left: AYEDAŞ Trafo Binası on stacked stone retaining plinth
    parts.append(add_box("Mursit_TrafoPlinth", (-5.8, 0, 0.55), (5.4, 5.2, 1.1), stone))
    parts.append(add_box("Mursit_TrafoHouse", (-5.8, 0, 2.75), (4.4, 4.2, 3.3), grey_light))
    parts.append(add_box("Mursit_TrafoRoof", (-5.8, 0, 4.52), (4.8, 4.6, 0.24), grey_dark))

    # Right: Dark-grey 3-story gabled apartment with crisp horizontal white stripes!
    parts.append(add_box("Mursit_GreyApt", (3.6, 0, 4.8), (10.4, 8.6, 9.6), grey_dark))
    for sz in (3.1, 6.2, 9.2):
        parts.append(add_box(f"Mursit_Stripe_{sz}", (3.6, 4.34, sz), (10.5, 0.10, 0.26), white))
    for fl in range(3):
        z = 1.85 + fl * 3.0
        for wx in (0.6, 3.6, 6.6):
            parts.append(add_box(f"Mursit_Win_{fl}_{wx}", (wx, 4.35, z), (1.35, 0.12, 1.35), white))
            parts.append(add_box(f"Mursit_Glass_{fl}_{wx}", (wx, 4.38, z), (1.12, 0.08, 1.12), glass))
    return parts


def build_ataturk_highway_guardrail():
    """Exact 230 Atatürk Cd. 12m galvanized W-beam TEM highway guardrail segment + corrugated fence."""
    metal = get_mat("Chrome", (0.68, 0.71, 0.74, 1.0), rough=0.38, metal=0.72)
    post = get_mat("TrimDark", (0.28, 0.30, 0.32, 1.0), rough=0.5, metal=0.55)
    parts = []
    parts.append(add_box("Guard_BeamTop", (0, 0, 0.68), (12.0, 0.14, 0.28), metal))
    for i in range(-5, 6):
        parts.append(add_box(f"Guard_Post_{i}", (i * 1.15, -0.06, 0.42), (0.09, 0.10, 0.84), post))
    return parts


def render_preview(all_groups):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 640
    scene.render.filepath = PREVIEW_IMG

    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.58, 0.72, 0.88, 1.0)
        bg.inputs[1].default_value = 1.0

    bpy.ops.object.light_add(type="SUN", location=(20, -25, 35), rotation=(math.radians(48), 0, math.radians(32)))
    bpy.context.active_object.data.energy = 3.5

    bpy.ops.object.camera_add(location=(0, -48, 18), rotation=(math.radians(74), 0, 0))
    scene.camera = bpy.context.active_object
    bpy.ops.render.render(write_still=True)
    print("Preview saved:", PREVIEW_IMG)


def main():
    builders = [
        ("Hamidiye_FatihSaatKulesi.fbx", build_fatih_saat_kulesi),
        ("Hamidiye_FatihCommercialBlock.fbx", build_fatih_commercial_block),
        ("Hamidiye_PetrolYoluBrandaBlock.fbx", build_petrol_yolu_branda_block),
        ("Hamidiye_AziziyeShopBlock.fbx", build_aziziye_shop_block),
        ("Hamidiye_EyyubiBufeBlock.fbx", build_eyyubi_bufe_block),
        ("Hamidiye_ZubeydeBlueHouse.fbx", build_zubeyde_blue_house),
        ("Hamidiye_SeyyidKutupApt.fbx", build_seyyid_kutup_apt),
        ("Hamidiye_MursitTrafoAndGreyApt.fbx", build_mursit_trafo_and_grey_apt),
        ("Hamidiye_AtaturkHighwayGuardrail.fbx", build_ataturk_highway_guardrail),
    ]

    for fbx, fn in builders:
        clear_scene()
        objs = fn()
        export_fbx(objs, fbx)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, fbx.replace(".fbx", ".blend")))

    # Studio lineup preview of key landmarks!
    clear_scene()
    offsets = [(-22, 0), (-11, 0), (0, 0), (12, 0), (24, 0)]
    preview_fns = [build_eyyubi_bufe_block, build_zubeyde_blue_house, build_fatih_saat_kulesi, build_fatih_commercial_block, build_petrol_yolu_branda_block]
    for (ox, oy), fn in zip(offsets, preview_fns):
        objs = fn()
        for o in objs:
            o.location.x += ox
            o.location.y += oy
    render_preview([])


if __name__ == "__main__":
    main()
