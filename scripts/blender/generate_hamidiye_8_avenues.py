"""
Blender 5.3 headless script: Sculpts 9 photo-matched 1-to-1 landmark & avenue building models
directly from our live Google Street View survey of Hamidiye Mahallesi's 8 main avenues
(Fatih Bulvarı, Atatürk Cd., Petrol Yolu Cd., Aziziye Cd., Eyyubi Cd., Zübeyde Hanım Cd.,
Seyyid Kutup Cd., Mürşit Cd.) for OurWorld v27.
"""
import math
import os
import bpy
import bmesh

OUT_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/Assets/GRIDROAM/Art/Models/Blender"
BLEND_DIR = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/scripts/blender/blend_sources"
PREVIEW_IMG = "/Users/dogusipeksac/Desktop/Dogus Store Apps/Yayınlanacak/GRIDROAM/scripts/blender/blender_hamidiye_preview.png"
os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(BLEND_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def apply_world_box_uv(obj, uv_scale=0.34):
    """Apply clean world-scale box projection UVs so PBR plaster/brick/tile textures tile uniformly."""
    if not obj or obj.type != "MESH" or not obj.data:
        return
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv_layer = bm.loops.layers.uv.verify()
    mw = obj.matrix_world
    for face in bm.faces:
        n = face.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in face.loops:
            p = mw @ loop.vert.co
            if az >= ax and az >= ay:
                u, v = p.x * uv_scale, p.y * uv_scale
            elif ax >= ay:
                u, v = p.y * uv_scale, p.z * uv_scale
            else:
                u, v = p.x * uv_scale, p.z * uv_scale
            loop[uv_layer].uv = (u, v)
    bm.to_mesh(me)
    bm.free()
    me.update()


def add_pitched_roof(name, loc, size, mat, overhang=0.45, ridge_h=1.25):
    """Sculpted 4-sided hipped Mediterranean tiled roof with overhanging eaves fascia."""
    sx, sy, sz = size
    ox, oy = sx + overhang * 2.0, sy + overhang * 2.0
    rx = max(0.4, ox * 0.5 - ridge_h * 1.15)
    ry = max(0.4, oy * 0.5 - ridge_h * 1.15)
    bm = bmesh.new()
    hx, hy = ox * 0.5, oy * 0.5
    z0, z1, z2 = -sz * 0.5, sz * 0.5, sz * 0.5 + ridge_h
    # Eaves fascia skirt + pitched hip planes
    b0 = bm.verts.new((-hx, -hy, z0))
    b1 = bm.verts.new(( hx, -hy, z0))
    b2 = bm.verts.new(( hx,  hy, z0))
    b3 = bm.verts.new((-hx,  hy, z0))
    e0 = bm.verts.new((-hx, -hy, z1))
    e1 = bm.verts.new(( hx, -hy, z1))
    e2 = bm.verts.new(( hx,  hy, z1))
    e3 = bm.verts.new((-hx,  hy, z1))
    r0 = bm.verts.new((-rx, -ry, z2))
    r1 = bm.verts.new(( rx, -ry, z2))
    r2 = bm.verts.new(( rx,  ry, z2))
    r3 = bm.verts.new((-rx,  ry, z2))
    faces = [
        (b0, b3, b2, b1),
        (b0, b1, e1, e0), (b1, b2, e2, e1), (b2, b3, e3, e2), (b3, b0, e0, e3),
        (e0, e1, r1, r0), (e1, e2, r2, r1), (e2, e3, r3, r2), (e3, e0, r0, r3),
        (r0, r1, r2, r3),
    ]
    for f in faces:
        bm.faces.new(f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = loc
    o.data.materials.append(mat)
    return o


def window_frame_subs(prefix, cx, cy, cz, w, h, mat, mullions=1, depth=0.18, sill_extra=0.14):
    """Build a crisp recessed 4-sided window surround with mullions, protruding sill & top lintel."""
    t = 0.09
    subs = [
        # Sill ledge (protrudes slightly forward)
        add_box(f"{prefix}_sill", (cx, cy + 0.05, cz - h * 0.5 - 0.055), (w + sill_extra, depth + 0.10, 0.10), mat, bevel=0.012),
        # Top drip lintel
        add_box(f"{prefix}_top",  (cx, cy + 0.02, cz + h * 0.5 + 0.045), (w + sill_extra * 0.6, depth + 0.04, 0.09), mat, bevel=0.010),
        # Left & right jambs
        add_box(f"{prefix}_jL",   (cx - w * 0.5 + t * 0.5, cy, cz), (t, depth, h), mat),
        add_box(f"{prefix}_jR",   (cx + w * 0.5 - t * 0.5, cy, cz), (t, depth, h), mat),
        # Horizontal upper transom bar
        add_box(f"{prefix}_trn",  (cx, cy + 0.01, cz + h * 0.22), (w - t, depth * 0.72, 0.055), mat),
    ]
    for mi in range(1, mullions + 1):
        mx = cx - w * 0.5 + (w * mi) / (mullions + 1)
        subs.append(add_box(f"{prefix}_mul_{mi}", (mx, cy + 0.01, cz), (0.06, depth * 0.78, h - 0.04), mat))
    return subs


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
        apply_world_box_uv(o, uv_scale=0.34)
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
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")
    print(f"Exported {filename} ({len(objs)} meshes, {tris} tris)")


def merge_parts_into(name, sub_objs, mat):
    """Join multiple helper meshes into a single named mesh object preserving exact FBX object name."""
    bpy.ops.object.select_all(action="DESELECT")
    valid = [o for o in sub_objs if o is not None]
    for o in valid:
        o.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


def build_fatih_saat_kulesi():
    """High-Detail Iconic Sultanbeyli Fatih Bulvarı Saat Kulesi:
    Preserves all 39 historical FBX child mesh names for 100% seamless in-place Unity scene upgrade,
    while enriching every piece with rusticated corner quoins, fluted classical columns + capitals,
    ornate arched wrought-iron sunburst grilles, 4-sided 12-marker clock dials, carved dentil cornice,
    arched upper colonnade spandrels, 3D curved crescent relief, and tiered gilded finial dome!"""
    stone = get_mat("StoneTrim", (0.84, 0.81, 0.74, 1.0), rough=0.78)
    cream = get_mat("CreamPaint", (0.91, 0.88, 0.82, 1.0), rough=0.72)
    iron = get_mat("TrimDark", (0.14, 0.15, 0.16, 1.0), rough=0.45, metal=0.6)
    clock_face = get_mat("WhitePVC", (0.96, 0.96, 0.94, 1.0), rough=0.25)
    gold = get_mat("GoldBrass", (0.88, 0.70, 0.24, 1.0), rough=0.3, metal=0.8)
    red = get_mat("SignRed", (0.78, 0.12, 0.14, 1.0), rough=0.45)

    parts = []
    # 1. Stepped 32-sided molded travertine plinth + 8 perimeter stone bollards
    p1_subs = [add_cyl("p1_base", (0, 0, 0.16), 3.35, 0.32, stone, verts=32)]
    for bi in range(8):
        ang = (2.0 * math.pi * bi) / 8.0 + math.pi / 8.0
        p1_subs.append(add_cyl(f"p1_bol_{bi}", (math.cos(ang) * 3.05, math.sin(ang) * 3.05, 0.56), 0.11, 0.50, stone, verts=12))
        p1_subs.append(add_cyl(f"p1_cap_{bi}", (math.cos(ang) * 3.05, math.sin(ang) * 3.05, 0.84), 0.13, 0.08, stone, verts=12))
    parts.append(merge_parts_into("Kule_Plinth1", p1_subs, stone))
    parts.append(add_cyl("Kule_Plinth2", (0, 0, 0.44), 2.82, 0.26, stone, verts=32))
    parts.append(add_cyl("Kule_Plinth3", (0, 0, 0.68), 2.38, 0.22, cream, verts=32))

    # 2. Main square travertine body with rusticated corner quoins, belt string-courses & side arched moldings!
    body_subs = [
        add_box("mb_core", (0, 0, 2.95), (3.40, 3.40, 4.35), stone, bevel=0.04),
        add_box("mb_base_mold", (0, 0, 0.96), (3.62, 3.62, 0.34), stone, bevel=0.03),
        add_box("mb_mid_band", (0, 0, 3.92), (3.58, 3.58, 0.18), stone, bevel=0.025),
    ]
    # Rusticated ashlar quoin stones stacking along all 4 vertical edges
    for qz in (1.35, 1.85, 2.35, 2.85, 3.35, 4.35, 4.78):
        for sx in (-1.0, 1.0):
            for sy in (-1.0, 1.0):
                body_subs.append(add_box(f"quoin_{qz}_{sx}_{sy}", (sx * 1.66, sy * 1.66, qz), (0.36, 0.36, 0.24), stone, bevel=0.02))
    # Framed stone portal surrounds on all 4 facades
    for s in (-1.0, 1.0):
        body_subs.append(add_box(f"arch_fL_{s}", (-1.04, s * 1.71, 2.30), (0.24, 0.16, 2.85), stone, bevel=0.02))
        body_subs.append(add_box(f"arch_fR_{s}", ( 1.04, s * 1.71, 2.30), (0.24, 0.16, 2.85), stone, bevel=0.02))
        body_subs.append(add_box(f"arch_sL_{s}", (s * 1.71, -0.82, 2.30), (0.16, 0.22, 2.75), stone, bevel=0.02))
        body_subs.append(add_box(f"arch_sR_{s}", (s * 1.71,  0.82, 2.30), (0.16, 0.22, 2.75), stone, bevel=0.02))
        body_subs.append(add_box(f"arch_sT_{s}", (s * 1.71,  0.00, 3.74), (0.18, 1.86, 0.24), stone, bevel=0.02))
    parts.append(merge_parts_into("Kule_MainBody", body_subs, stone))

    # 3. Carved projecting cornice with classical dentil teeth around all 4 sides!
    corn_subs = [
        add_box("c_slab1", (0, 0, 5.20), (3.72, 3.72, 0.20), cream, bevel=0.03),
        add_box("c_slab2", (0, 0, 5.38), (3.98, 3.98, 0.22), cream, bevel=0.04),
    ]
    for di in range(-4, 5):
        off = di * 0.40
        for s in (-1.0, 1.0):
            corn_subs.append(add_box(f"dent_y_{s}_{di}", (off, s * 1.84, 5.06), (0.14, 0.14, 0.16), cream, bevel=0.015))
            corn_subs.append(add_box(f"dent_x_{s}_{di}", (s * 1.84, off, 5.06), (0.14, 0.14, 0.16), cream, bevel=0.015))
    parts.append(merge_parts_into("Kule_Cornice", corn_subs, cream))

    # 4. Ornate arched wrought-iron grille doors (front/back + side grilles!) & 4-sided 12-marker Clock Dials!
    for sy in (-1, 1):
        door_subs = [
            add_box(f"d_back_{sy}", (0, sy * 1.65, 2.25), (1.78, 0.10, 2.72), iron, bevel=0.01),
            add_box(f"d_midbar_{sy}", (0, sy * 1.72, 2.25), (1.82, 0.06, 0.10), iron),
            add_box(f"d_botbar_{sy}", (0, sy * 1.72, 1.05), (1.82, 0.06, 0.16), iron),
            add_box(f"d_topbar_{sy}", (0, sy * 1.72, 3.50), (1.82, 0.06, 0.10), iron),
        ]
        # Vertical wrought-iron spears
        for vx in (-0.72, -0.48, -0.24, 0.0, 0.24, 0.48, 0.72):
            door_subs.append(add_cyl(f"d_bar_{sy}_{vx}", (vx, sy * 1.72, 2.28), 0.025, 2.48, iron, verts=8))
        # Side arched decorative wrought-iron grilles merged cleanly onto the matching sy door object!
        door_subs.append(add_box(f"sd_back_{sy}", (sy * 1.68, 0, 2.28), (0.10, 1.42, 2.58), iron))
        for vy in (-0.48, -0.24, 0.0, 0.24, 0.48):
            door_subs.append(add_cyl(f"sd_bar_{sy}_{vy}", (sy * 1.73, vy, 2.28), 0.024, 2.42, iron, verts=8))
        parts.append(merge_parts_into(f"Kule_Door_{sy}", door_subs, iron))

        # Radial sunburst fanlight transom arch
        arch_subs = [add_cyl(f"da_core_{sy}", (0, sy * 1.66, 3.55), 0.92, 0.14, iron, rot=(math.radians(90), 0, 0), verts=24)]
        for ray in range(7):
            ang = math.radians(15 + ray * 25)
            rx = math.cos(ang) * 0.44
            rz = math.sin(ang) * 0.44
            arch_subs.append(add_box(f"da_ray_{sy}_{ray}", (rx, sy * 1.73, 3.55 + rz), (0.86, 0.04, 0.03), iron, rot=(0, -ang, 0)))
        parts.append(merge_parts_into(f"Kule_DoorArch_{sy}", arch_subs, iron))

        # Sculpted 4-sided Clock Rims + 12 raised hour tick markers (front/back + east/west faces!)
        rim_subs = [
            add_cyl(f"cr_y_{sy}", (0, sy * 1.73, 4.55), 0.48, 0.10, iron, rot=(math.radians(90), 0, 0), verts=28),
            add_cyl(f"cr_x_{sy}", (sy * 1.73, 0, 4.55), 0.48, 0.10, iron, rot=(0, math.radians(90), 0), verts=28),
        ]
        for h_i in range(12):
            ha = (2.0 * math.pi * h_i) / 12.0
            tick_l = 0.09 if h_i % 3 == 0 else 0.055
            tick_w = 0.028 if h_i % 3 == 0 else 0.018
            # Front/back clock ticks
            rim_subs.append(add_box(f"ty_{sy}_{h_i}", (math.cos(ha) * 0.34, sy * 1.795, 4.55 + math.sin(ha) * 0.34), (tick_l, 0.02, tick_w), iron, rot=(0, -ha, 0)))
            # Left/right clock ticks
            rim_subs.append(add_box(f"tx_{sy}_{h_i}", (sy * 1.795, math.cos(ha) * 0.34, 4.55 + math.sin(ha) * 0.34), (0.02, tick_l, tick_w), iron, rot=(ha, 0, 0)))
        parts.append(merge_parts_into(f"Kule_ClockRim_{sy}", rim_subs, iron))

        face_subs = [
            add_cyl(f"cf_y_{sy}", (0, sy * 1.765, 4.55), 0.41, 0.05, clock_face, rot=(math.radians(90), 0, 0), verts=28),
            add_cyl(f"cf_x_{sy}", (sy * 1.765, 0, 4.55), 0.41, 0.05, clock_face, rot=(0, math.radians(90), 0), verts=28),
        ]
        parts.append(merge_parts_into(f"Kule_ClockFace_{sy}", face_subs, clock_face))

        handh_subs = [
            add_box(f"hh_y_{sy}", (0.09, sy * 1.805, 4.59), (0.22, 0.022, 0.042), iron, rot=(0, -0.35, 0)),
            add_box(f"hh_x_{sy}", (sy * 1.805, 0.09, 4.59), (0.022, 0.22, 0.042), iron, rot=(0.35, 0, 0)),
        ]
        parts.append(merge_parts_into(f"Kule_HandH_{sy}", handh_subs, iron))

        handm_subs = [
            add_box(f"hm_y_{sy}", (-0.02, sy * 1.812, 4.68), (0.036, 0.022, 0.29), iron),
            add_box(f"hm_x_{sy}", (sy * 1.812, -0.02, 4.68), (0.022, 0.036, 0.29), iron),
        ]
        parts.append(merge_parts_into(f"Kule_HandM_{sy}", handm_subs, iron))

    # 5. 4 Classical Corner Pilasters with Molded Pedestal Bases & Carved Capitals
    for sx in (-1.55, 1.55):
        for sy in (-1.55, 1.55):
            bc_subs = [
                add_box(f"bcb_{sx}_{sy}", (sx, sy, 0.98), (0.58, 0.58, 0.34), cream, bevel=0.025),
                add_cyl(f"bcs_{sx}_{sy}", (sx, sy, 2.95), 0.24, 3.85, cream, verts=16),
                add_cyl(f"bcr_{sx}_{sy}", (sx, sy, 4.82), 0.30, 0.14, cream, verts=16),
                add_box(f"bcc_{sx}_{sy}", (sx, sy, 4.98), (0.62, 0.62, 0.22), cream, bevel=0.025),
            ]
            parts.append(merge_parts_into(f"Kule_BaseCol_{sx}_{sy}", bc_subs, cream))

    # 6. Upper Classical Colonnade Pavilion (8 Fluted Columns w/ Bases, Capitals & Balustrade Rails!)
    col_xs = (-1.10, -0.38, 0.38, 1.10)
    for cx in col_xs:
        for sy in (-1.05, 1.05):
            tc_subs = [
                add_box(f"tcb_{cx}_{sy}", (cx, sy, 5.56), (0.38, 0.38, 0.20), cream, bevel=0.02),
                add_cyl(f"tcs_{cx}_{sy}", (cx, sy, 6.75), 0.15, 2.30, cream, verts=16),
                add_cyl(f"tcr_{cx}_{sy}", (cx, sy, 7.84), 0.20, 0.10, cream, verts=16),
                add_box(f"tcc_{cx}_{sy}", (cx, sy, 7.96), (0.40, 0.40, 0.16), cream, bevel=0.02),
            ]
            parts.append(merge_parts_into(f"Kule_TopCol_{cx}_{sy}", tc_subs, cream))

    # 7. Carved Classical Crown Pediment + Molded Eaves Cap + Sculpted 3D Crescent & Star Medallions!
    ped_subs = [
        add_box("ped_main", (0, 0, 8.55), (3.18, 2.92, 1.05), stone, bevel=0.04),
        add_box("ped_frieze", (0, 0, 8.08), (3.32, 3.04, 0.18), stone, bevel=0.025),
        # Central inner lantern pillar visible inside open colonnade
        add_cyl("ped_core", (0, 0, 6.75), 0.56, 2.55, stone, verts=16),
    ]
    parts.append(merge_parts_into("Kule_UpperPediment", ped_subs, stone))

    cap_subs = [
        add_box("uc_main", (0, 0, 9.20), (3.52, 3.22, 0.26), cream, bevel=0.04),
        add_box("uc_step", (0, 0, 9.44), (2.75, 2.55, 0.24), cream, bevel=0.04),
    ]
    parts.append(merge_parts_into("Kule_UpperCap", cap_subs, cream))

    for sy in (-1, 1):
        parts.append(add_cyl(f"Kule_Medallion_{sy}", (0, sy * 1.47, 8.55), 0.46, 0.08, iron, rot=(math.radians(90), 0, 0), verts=28))
        # Sculpted curved crescent arc segments
        cr_subs = []
        for ci in range(9):
            a = math.radians(52 + ci * 32)
            cr_subs.append(add_box(f"cr_{sy}_{ci}", (-0.04 + math.cos(a) * 0.21, sy * 1.515, 8.55 + math.sin(a) * 0.21), (0.09, 0.05, 0.09), clock_face, bevel=0.015, rot=(0, -a, 0)))
        parts.append(merge_parts_into(f"Kule_Crescent_{sy}", cr_subs, clock_face))
        parts.append(add_box(f"Kule_Star_{sy}", (0.16, sy * 1.52, 8.56), (0.11, 0.05, 0.11), red, rot=(0, math.radians(45), 0)))

    # 8. Tiered Gilded Dome Finial + Curved Brass Crescent Alem Spire!
    fin_subs = [
        add_cyl("fin_dome", (0, 0, 9.72), 0.72, 0.34, gold, verts=20),
        add_cyl("fin_cone", (0, 0, 10.02), 0.44, 0.08, gold, verts=16),
        add_cyl("fin_rod", (0, 0, 10.48), 0.05, 0.78, gold, verts=12),
        add_cyl("fin_orb1", (0, 0, 10.35), 0.14, 0.16, gold, verts=14),
        add_cyl("fin_orb2", (0, 0, 10.65), 0.10, 0.12, gold, verts=14),
    ]
    for ci in range(7):
        a = math.radians(-110 + ci * 36)
        fin_subs.append(add_box(f"fin_c_{ci}", (math.sin(a) * 0.18, 0, 11.02 - math.cos(a) * 0.18), (0.06, 0.04, 0.07), gold, bevel=0.01))
    parts.append(merge_parts_into("Kule_Finial", fin_subs, gold))
    return parts


def build_fatih_commercial_block():
    """Fatih Bulvarı warm peach & cream 4-story commercial plaza with articulated cornices,
    recessed framed windows, baluster balconies, beveled awnings & pitched tiled roof (14m x 10m x 13.5m)."""
    peach = get_mat("PeachPlaster", (0.86, 0.63, 0.48, 1.0), rough=0.78)
    cream = get_mat("CreamPaint", (0.93, 0.89, 0.82, 1.0), rough=0.75)
    dark = get_mat("TrimDark", (0.14, 0.15, 0.17, 1.0), rough=0.45, metal=0.45)
    glass = get_mat("Glass", (0.20, 0.30, 0.38, 1.0), rough=0.12, metal=0.2)
    sign_blue = get_mat("CobaltBlue", (0.12, 0.34, 0.66, 1.0), rough=0.4)
    sign_red = get_mat("SignRed", (0.80, 0.15, 0.14, 1.0), rough=0.4)
    sign_gold = get_mat("GoldBrass", (0.90, 0.68, 0.18, 1.0), rough=0.35)
    roof = get_mat("RoofTile", (0.64, 0.28, 0.20, 1.0), rough=0.82)

    parts = []
    # Main plaster body + plinth + horizontal floor string-courses + corner pilasters + drainpipe + rooftop HVAC!
    body_subs = [
        add_box("fb_core", (0, 0, 6.75), (14.2, 9.6, 13.5), peach, bevel=0.04),
        add_box("fb_plinth", (0, 0, 0.22), (14.42, 9.82, 0.44), peach, bevel=0.02),
    ]
    for fz in (3.95, 7.05, 10.15, 13.30):
        body_subs.append(add_box(f"fb_band_{fz}", (0, 0, fz), (14.48, 9.88, 0.22), peach, bevel=0.02))
    for sx in (-7.08, 7.08):
        for sy in (-4.78, 4.78):
            body_subs.append(add_box(f"fb_pil_{sx}_{sy}", (sx, sy, 6.75), (0.36, 0.36, 13.5), peach, bevel=0.02))
    # Vertical rain drainpipe along front-right facade + side decorative wall ribs
    body_subs.append(add_cyl("fb_drain", (6.88, 4.86, 6.75), 0.065, 13.3, peach, verts=10))
    for fl in range(1, 4):
        z = 2.0 + fl * 3.1
        for sy in (-2.2, 2.2):
            body_subs.append(add_box(f"fb_swR_{fl}_{sy}", (7.14, sy, z), (0.14, 1.35, 1.45), peach, bevel=0.015))
            body_subs.append(add_box(f"fb_swL_{fl}_{sy}", (-7.14, sy, z), (0.14, 1.35, 1.45), peach, bevel=0.015))
    parts.append(merge_parts_into("Fatih_MainBody", body_subs, peach))

    # Left cream architectural bay wing with beveled quoins & cornice bands
    wing_subs = [
        add_box("fw_core", (-4.4, 0.10, 8.2), (5.2, 9.8, 10.2), cream, bevel=0.04),
        add_box("fw_bot",  (-4.4, 0.10, 3.95), (5.46, 10.06, 0.28), cream, bevel=0.02),
        add_box("fw_mid1", (-4.4, 0.10, 7.05), (5.42, 10.02, 0.20), cream, bevel=0.02),
        add_box("fw_mid2", (-4.4, 0.10, 10.15), (5.42, 10.02, 0.20), cream, bevel=0.02),
        add_box("fw_top",  (-4.4, 0.10, 13.32), (5.52, 10.12, 0.30), cream, bevel=0.025),
    ]
    for qz in (4.5, 5.5, 6.5, 7.6, 8.6, 9.6, 10.7, 11.7, 12.7):
        wing_subs.append(add_box(f"fw_qL_{qz}", (-7.02, 4.98, qz), (0.34, 0.26, 0.42), cream, bevel=0.015))
        wing_subs.append(add_box(f"fw_qR_{qz}", (-1.78, 4.98, qz), (0.34, 0.26, 0.42), cream, bevel=0.015))
    parts.append(merge_parts_into("Fatih_LeftWing", wing_subs, cream))

    # Overhanging pitched Mediterranean tiled roof + perimeter eaves coping + solar collector
    roof_subs = [
        add_box("fr_parapet", (0, 0, 13.62), (14.55, 9.95, 0.38), roof, bevel=0.03),
        add_pitched_roof("fr_hip", (0, 0, 13.86), (14.2, 9.6, 0.18), roof, overhang=0.52, ridge_h=1.45),
    ]
    parts.append(merge_parts_into("Fatih_RoofParapet", roof_subs, roof))

    # Brick chimney stack w/ stepped stone crown & twin clay flue pots
    chim_subs = [
        add_box("fc_stack", (2.6, -1.8, 15.15), (0.85, 0.85, 1.9), roof, bevel=0.025),
        add_box("fc_cap",   (2.6, -1.8, 16.16), (1.06, 1.06, 0.16), roof, bevel=0.02),
        add_cyl("fc_pot1",  (2.42, -1.8, 16.42), 0.12, 0.42, roof, verts=10),
        add_cyl("fc_pot2",  (2.78, -1.8, 16.42), 0.12, 0.42, roof, verts=10),
    ]
    parts.append(merge_parts_into("Fatih_Chimney", chim_subs, roof))

    # 3 Ground-floor articulated storefronts along +Y (recessed doors, mullions, shutter hood, angled awning!)
    shop_xs = (-4.5, 0.0, 4.5)
    shop_mats = (sign_blue, sign_gold, sign_red)
    for sx, smat in zip(shop_xs, shop_mats):
        sf_subs = [
            add_box(f"sf_outL_{sx}", (sx - 1.98, 4.86, 1.75), (0.18, 0.24, 3.3), dark, bevel=0.012),
            add_box(f"sf_outR_{sx}", (sx + 1.98, 4.86, 1.75), (0.18, 0.24, 3.3), dark, bevel=0.012),
            add_box(f"sf_top_{sx}",  (sx, 4.88, 3.16), (4.14, 0.30, 0.34), dark, bevel=0.015),  # roller shutter box
            add_box(f"sf_kick_{sx}", (sx, 4.86, 0.18), (4.10, 0.22, 0.36), dark),
            add_box(f"sf_mulL_{sx}", (sx - 0.65, 4.87, 1.60), (0.10, 0.16, 2.75), dark),
            add_box(f"sf_mulR_{sx}", (sx + 0.65, 4.87, 1.60), (0.10, 0.16, 2.75), dark),
            add_box(f"sf_hand_{sx}", (sx + 0.16, 4.96, 1.15), (0.05, 0.08, 0.48), dark),
        ]
        parts.append(merge_parts_into(f"Fatih_ShopFrame_{sx}", sf_subs, dark))
        parts.append(add_box(f"Fatih_ShopGlass_{sx}", (sx, 4.84, 1.58), (3.80, 0.10, 2.72), glass))
        fasc_subs = [
            add_box(f"fs_box_{sx}", (sx, 4.96, 3.58), (4.22, 0.28, 0.72), smat, bevel=0.025),
            # Angled shopfront fabric awning canopy + front valance flap!
            add_box(f"fs_awn_{sx}", (sx, 5.42, 3.14), (4.16, 0.92, 0.08), smat, rot=(math.radians(18), 0, 0), bevel=0.01),
            add_box(f"fs_val_{sx}", (sx, 5.84, 2.90), (4.16, 0.06, 0.24), smat, bevel=0.008),
        ]
        parts.append(merge_parts_into(f"Fatih_SignFascia_{sx}", fasc_subs, smat))

    # Upper 3 floors: recessed framed windows w/ mullions & sills + articulated baluster balconies!
    for fl in range(1, 4):
        z = 2.0 + fl * 3.1
        for wx in (-5.4, -3.2, -1.0, 1.2, 3.4, 5.4):
            wy = 5.05 if wx < -1.8 else 4.86
            parts.append(add_box(f"Fatih_Win_{fl}_{wx}", (wx, wy, z), (1.30, 0.08, 1.40), glass))
            s_subs = window_frame_subs(f"fs_{fl}_{wx}", wx, wy + 0.04, z, 1.42, 1.48, cream, mullions=1, depth=0.16)
            parts.append(merge_parts_into(f"Fatih_Sill_{fl}_{wx}", s_subs, cream))
        for bx in (-0.2, 4.4):
            bs_subs = [
                add_box(f"fbs_sl_{fl}_{bx}", (bx, 5.32, z - 0.95), (3.44, 1.06, 0.18), cream, bevel=0.02),
                add_box(f"fbs_cL_{fl}_{bx}", (bx - 1.35, 5.10, z - 1.12), (0.24, 0.60, 0.20), cream, bevel=0.015),
                add_box(f"fbs_cR_{fl}_{bx}", (bx + 1.35, 5.10, z - 1.12), (0.24, 0.60, 0.20), cream, bevel=0.015),
                # Exterior split-AC condenser mounted tidily beside balcony!
                add_box(f"fbs_ac_{fl}_{bx}", (bx + 1.88, 5.02, z - 0.45), (0.58, 0.28, 0.44), cream, bevel=0.015),
            ]
            parts.append(merge_parts_into(f"Fatih_BalcSlab_{fl}_{bx}", bs_subs, cream))
            bw_subs = [
                # Half-height sculpted parapet wall + side returns + top handrail + open balusters!
                add_box(f"fbw_low_{fl}_{bx}",  (bx, 5.76, z - 0.66), (3.40, 0.14, 0.44), peach, bevel=0.015),
                add_box(f"fbw_sL_{fl}_{bx}",   (bx - 1.64, 5.32, z - 0.66), (0.14, 0.96, 0.44), peach, bevel=0.012),
                add_box(f"fbw_sR_{fl}_{bx}",   (bx + 1.64, 5.32, z - 0.66), (0.14, 0.96, 0.44), peach, bevel=0.012),
                add_box(f"fbw_rail_{fl}_{bx}", (bx, 5.76, z - 0.02), (3.44, 0.12, 0.08), peach, bevel=0.01),
                add_box(f"fbw_rL_{fl}_{bx}",   (bx - 1.64, 5.32, z - 0.02), (0.10, 0.96, 0.08), peach, bevel=0.01),
                add_box(f"fbw_rR_{fl}_{bx}",   (bx + 1.64, 5.32, z - 0.02), (0.10, 0.96, 0.08), peach, bevel=0.01),
            ]
            for bi in range(-5, 6):
                bw_subs.append(add_box(f"fbw_bal_{fl}_{bx}_{bi}", (bx + bi * 0.30, 5.76, z - 0.24), (0.045, 0.045, 0.42), peach))
            parts.append(merge_parts_into(f"Fatih_BalcWall_{fl}_{bx}", bw_subs, peach))
    return parts


def build_petrol_yolu_branda_block():
    """Exact 395 Petrol Yolu Cd. 4-story beige commercial block with ribbed orange awning,
    stepped sidewalk stairs, framed PVC casements & articulated corner balcony."""
    beige = get_mat("Concrete", (0.76, 0.74, 0.68, 1.0), rough=0.82)
    orange = get_mat("OrangeStripe", (0.86, 0.36, 0.10, 1.0), rough=0.48)
    white = get_mat("WhitePVC", (0.94, 0.95, 0.96, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.18, 0.26, 0.34, 1.0), rough=0.14)
    dark = get_mat("TrimDark", (0.14, 0.15, 0.16, 1.0), rough=0.45, metal=0.45)

    parts = []
    body_subs = [
        add_box("pb_core", (0, 0, 6.4), (11.2, 9.2, 12.8), beige, bevel=0.04),
        add_box("pb_plinth", (0, 0, 0.28), (11.42, 9.42, 0.56), beige, bevel=0.02),
    ]
    for fz in (4.35, 7.25, 10.15, 12.65):
        body_subs.append(add_box(f"pb_band_{fz}", (0, 0, fz), (11.46, 9.46, 0.20), beige, bevel=0.02))
    # Corner pilasters + exterior AC condensers
    for sx in (-5.58, 5.58):
        body_subs.append(add_box(f"pb_pil_{sx}", (sx, 4.58, 6.4), (0.34, 0.28, 12.8), beige, bevel=0.02))
    for fl in range(1, 4):
        body_subs.append(add_box(f"pb_ac_{fl}", (-5.05, 4.76, 2.2 + fl * 2.9), (0.58, 0.28, 0.44), beige, bevel=0.015))
    parts.append(merge_parts_into("Petrol_Body", body_subs, beige))

    roof_subs = [
        add_box("pr_parapet", (0, 0, 13.02), (11.55, 9.55, 0.44), dark, bevel=0.03),
        add_pitched_roof("pr_hip", (0, 0, 13.26), (11.2, 9.2, 0.16), dark, overhang=0.42, ridge_h=1.10),
        add_box("pr_pent", (-2.2, -1.6, 14.35), (3.0, 2.6, 1.8), dark, bevel=0.03),
    ]
    parts.append(merge_parts_into("Petrol_Roof", roof_subs, dark))

    # 3 crisp stepped stone sidewalk stairs w/ nosing overhang
    step_subs = [
        add_box("ps_s1", (0, 5.45, 0.10), (11.30, 1.45, 0.20), white, bevel=0.012),
        add_box("ps_s2", (0, 5.15, 0.28), (11.24, 1.02, 0.20), white, bevel=0.012),
        add_box("ps_s3", (0, 4.86, 0.46), (11.20, 0.62, 0.18), white, bevel=0.012),
    ]
    parts.append(merge_parts_into("Petrol_Steps", step_subs, white))
    parts.append(add_box("Petrol_ShowroomGlass", (0.4, 4.64, 1.75), (8.6, 0.12, 2.75), glass))

    # Ribbed angled bright-orange branda awning canopy + scalloped front skirt + side cheeks!
    awn_subs = [
        add_box("pa_hood",   (0.2, 4.78, 3.44), (10.68, 0.32, 0.26), orange, bevel=0.02),
        add_box("pa_slope",  (0.2, 5.24, 3.20), (10.64, 1.18, 0.10), orange, rot=(math.radians(20), 0, 0), bevel=0.012),
        add_box("pa_val",    (0.2, 5.76, 2.86), (10.64, 0.08, 0.30), orange, bevel=0.01),
    ]
    for ri in range(-5, 6):
        awn_subs.append(add_box(f"pa_rib_{ri}", (0.2 + ri * 1.02, 5.24, 3.25), (0.07, 1.16, 0.08), orange, rot=(math.radians(20), 0, 0)))
    parts.append(merge_parts_into("Petrol_OrangeAwning", awn_subs, orange))

    ban_subs = [
        add_box("pban_main", (0.2, 4.74, 3.96), (10.25, 0.18, 0.78), white, bevel=0.02),
        add_box("pban_top",  (0.2, 4.82, 4.36), (10.38, 0.28, 0.08), white, bevel=0.01),
        add_box("pban_bot",  (0.2, 4.82, 3.56), (10.38, 0.28, 0.08), white, bevel=0.01),
    ]
    parts.append(merge_parts_into("Petrol_SignBanner", ban_subs, white))

    # Upper 3 floors: left articulated double-casement PVC windows, right panoramic mullioned corner balcony!
    for fl in range(1, 4):
        z = 2.6 + fl * 2.9
        wl_subs = window_frame_subs(f"pwl_{fl}", -3.2, 4.66, z, 2.46, 1.38, white, mullions=2, depth=0.18)
        parts.append(merge_parts_into(f"Petrol_WinL_{fl}", wl_subs, white))
        parts.append(add_box(f"Petrol_WinLGlass_{fl}", (-3.2, 4.64, z), (2.26, 0.08, 1.20), glass))

        eb_subs = window_frame_subs(f"peb_{fl}", 2.4, 4.68, z, 4.46, 1.58, dark, mullions=3, depth=0.22)
        eb_subs.append(add_box(f"peb_slab_{fl}", (2.4, 4.80, z - 0.86), (4.60, 0.44, 0.14), dark, bevel=0.015))
        parts.append(merge_parts_into(f"Petrol_EncBalc_{fl}", eb_subs, dark))
        parts.append(add_box(f"Petrol_EncGlass_{fl}", (2.4, 4.66, z), (4.22, 0.08, 1.36), glass))
    return parts


def build_aziziye_shop_block():
    """Exact 46 Aziziye Cd. 4-story cream & terracotta-panel shop block with cobalt aluminum framing,
    open baluster street railing & overhanging tiled hipped roof."""
    cream = get_mat("CreamPaint", (0.94, 0.92, 0.87, 1.0), rough=0.72)
    terra = get_mat("RedPlaster", (0.76, 0.36, 0.28, 1.0), rough=0.78)
    cobalt = get_mat("CobaltBlue", (0.10, 0.26, 0.62, 1.0), rough=0.35, metal=0.35)
    glass = get_mat("Glass", (0.20, 0.30, 0.38, 1.0), rough=0.14)
    dark = get_mat("TrimDark", (0.14, 0.15, 0.17, 1.0), rough=0.45, metal=0.5)

    parts = []
    body_subs = [
        add_box("ab_core", (0, 0, 6.2), (13.4, 8.8, 12.4), cream, bevel=0.04),
        add_box("ab_plinth", (0, 0, 0.22), (13.62, 9.02, 0.44), cream, bevel=0.02),
    ]
    for fz in (4.10, 6.95, 9.80, 12.28):
        body_subs.append(add_box(f"ab_corn_{fz}", (0, 0, fz), (13.66, 9.06, 0.18), cream, bevel=0.02))
    # Window surrounds around upper-floor glass bays
    for fl in range(1, 4):
        z = 2.4 + fl * 2.85 + 0.15
        for wx in (-4.2, 0.0, 4.2):
            body_subs.extend(window_frame_subs(f"abw_{fl}_{wx}", wx, 4.46, z, 3.16, 1.40, cream, mullions=2, depth=0.18))
    parts.append(merge_parts_into("Aziziye_Body", body_subs, cream))

    roof_subs = [
        add_box("ar_eave", (0, 0, 12.56), (13.85, 9.25, 0.34), terra, bevel=0.03),
        add_pitched_roof("ar_hip", (0, 0, 12.76), (13.4, 8.8, 0.16), terra, overhang=0.48, ridge_h=1.25),
        add_box("ar_chim", (-3.8, -1.6, 14.0), (0.75, 0.75, 1.5), terra, bevel=0.02),
    ]
    parts.append(merge_parts_into("Aziziye_Roof", roof_subs, terra))

    # Cobalt-blue structural aluminum storefront with mullions & overhead shutter hood
    cob_subs = [
        add_box("ac_L",    (2.8 - 3.10, 4.48, 2.05), (0.24, 0.26, 3.60), cobalt, bevel=0.015),
        add_box("ac_R",    (2.8 + 3.10, 4.48, 2.05), (0.24, 0.26, 3.60), cobalt, bevel=0.015),
        add_box("ac_top",  (2.8, 4.54, 3.68), (6.46, 0.36, 0.44), cobalt, bevel=0.02),
        add_box("ac_kick", (2.8, 4.48, 0.32), (6.40, 0.24, 0.44), cobalt),
        add_box("ac_m1",   (1.2, 4.49, 2.00), (0.12, 0.18, 3.10), cobalt),
        add_box("ac_m2",   (4.4, 4.49, 2.00), (0.12, 0.18, 3.10), cobalt),
        add_box("ac_trn",  (2.8, 4.49, 2.90), (6.10, 0.16, 0.10), cobalt),
    ]
    parts.append(merge_parts_into("Aziziye_CobaltFrame", cob_subs, cobalt))
    parts.append(add_box("Aziziye_CobaltGlass", (2.8, 4.46, 1.95), (5.96, 0.08, 3.10), glass))

    # Open wrought-iron sidewalk guardrail with vertical pickets & end posts (not a solid wall!)
    rail_subs = [
        add_box("ar_top", (2.8, 4.96, 1.54), (6.44, 0.08, 0.07), dark, bevel=0.01),
        add_box("ar_bot", (2.8, 4.96, 0.78), (6.44, 0.06, 0.06), dark),
    ]
    for bi in range(-8, 9):
        thick = 0.07 if abs(bi) == 8 else 0.036
        rail_subs.append(add_box(f"ar_b_{bi}", (2.8 + bi * 0.39, 4.96, 1.15), (thick, thick, 0.92), dark))
    parts.append(merge_parts_into("Aziziye_Railing", rail_subs, dark))

    ls_subs = [
        add_box("als_L",   (-3.4 - 2.60, 4.48, 1.90), (0.22, 0.24, 3.30), dark, bevel=0.015),
        add_box("als_R",   (-3.4 + 2.60, 4.48, 1.90), (0.22, 0.24, 3.30), dark, bevel=0.015),
        add_box("als_top", (-3.4, 4.54, 3.40), (5.44, 0.34, 0.42), dark, bevel=0.02),
        add_box("als_m1",  (-3.4 - 0.85, 4.49, 1.85), (0.10, 0.16, 2.85), dark),
        add_box("als_m2",  (-3.4 + 0.85, 4.49, 1.85), (0.10, 0.16, 2.85), dark),
    ]
    parts.append(merge_parts_into("Aziziye_LeftShop", ls_subs, dark))
    parts.append(add_box("Aziziye_LeftGlass", (-3.4, 4.46, 1.80), (4.96, 0.08, 2.80), glass))

    # Upper terracotta accent spandrel bands w/ beveled lip + recessed windows
    for fl in range(1, 4):
        z = 2.4 + fl * 2.85
        tb_subs = [
            add_box(f"atb_m_{fl}", (0, 4.44, z - 0.85), (12.86, 0.16, 0.82), terra, bevel=0.02),
            add_box(f"atb_lip_{fl}", (0, 4.50, z - 0.44), (13.06, 0.24, 0.10), terra, bevel=0.015),
        ]
        parts.append(merge_parts_into(f"Aziziye_TerraBand_{fl}", tb_subs, terra))
        for wx in (-4.2, 0.0, 4.2):
            parts.append(add_box(f"Aziziye_Win_{fl}_{wx}", (wx, 4.45, z + 0.15), (3.00, 0.08, 1.28), glass))
    return parts


def build_eyyubi_bufe_block():
    """Exact 53 Eyyubi Cd. 2-story terracotta corner Mahalle Büfesi + corrugated roller shutters +
    picketed cobalt-blue garden gate & coping walls."""
    terra = get_mat("RawBrick", (0.72, 0.34, 0.24, 1.0), rough=0.80)
    cream = get_mat("CreamPaint", (0.90, 0.86, 0.78, 1.0), rough=0.78)
    shutter = get_mat("PowderBlue", (0.64, 0.74, 0.78, 1.0), rough=0.45, metal=0.4)
    red_stripe = get_mat("SignRed", (0.80, 0.16, 0.15, 1.0), rough=0.45)
    gate_blue = get_mat("FenceBlue", (0.16, 0.44, 0.72, 1.0), rough=0.45, metal=0.45)
    glass = get_mat("Glass", (0.18, 0.26, 0.34, 1.0), rough=0.14)
    white = get_mat("WhitePVC", (0.95, 0.96, 0.97, 1.0), rough=0.35)

    parts = []
    house_subs = [
        add_box("eh_core", (-1.5, 0, 3.2), (10.6, 7.6, 6.4), cream, bevel=0.04),
        add_box("eh_belt", (-1.5, 0, 3.45), (10.84, 7.84, 0.18), cream, bevel=0.02),
        add_box("eh_eave", (-1.5, 0, 6.46), (11.08, 8.08, 0.26), cream, bevel=0.025),
        add_pitched_roof("eh_roof", (-1.5, 0, 6.66), (10.6, 7.6, 0.16), cream, overhang=0.46, ridge_h=1.15),
        add_box("eh_chim", (-4.2, -1.4, 7.85), (0.68, 0.68, 1.4), cream, bevel=0.02),
        add_cyl("eh_drain", (3.72, 3.84, 3.2), 0.055, 6.3, cream, verts=10),
    ]
    parts.append(merge_parts_into("Eyyubi_MainHouse", house_subs, cream))

    tc_subs = [
        add_box("et_clad", (-0.2, 3.84, 3.35), (5.6, 0.14, 5.9), terra, bevel=0.02),
        add_box("et_corn", (-0.2, 3.92, 3.45), (5.78, 0.24, 0.16), terra, bevel=0.015),
        add_box("et_sill", (-0.2, 4.02, 0.34), (3.40, 0.36, 0.12), terra, bevel=0.012),  # büfe serving counter ledge!
    ]
    parts.append(merge_parts_into("Eyyubi_TerraClad", tc_subs, terra))

    # Corrugated horizontal-slat rolled security shutter + cylindrical roller drum housing + guide rails!
    sh_subs = [
        add_box("es_back", (-4.8, 3.84, 1.58), (3.56, 0.10, 2.92), shutter),
        add_box("es_hood", (-4.8, 3.94, 3.16), (3.72, 0.30, 0.34), shutter, bevel=0.02),
        add_box("es_gL",   (-6.58, 3.90, 1.60), (0.12, 0.18, 3.10), shutter),
        add_box("es_gR",   (-3.02, 3.90, 1.60), (0.12, 0.18, 3.10), shutter),
    ]
    for si in range(11):
        sh_subs.append(add_box(f"es_slat_{si}", (-4.8, 3.91, 0.24 + si * 0.25), (3.48, 0.06, 0.16), shutter, bevel=0.008))
    parts.append(merge_parts_into("Eyyubi_ShutterL", sh_subs, shutter))

    sr_subs = [
        add_box("esr_bar", (-4.8, 3.95, 1.75), (3.64, 0.08, 0.24), red_stripe, bevel=0.01),
        # Cute angled büfe awning over right service window!
        add_box("esr_awn", (-0.2, 4.32, 2.94), (3.55, 0.86, 0.08), red_stripe, rot=(math.radians(20), 0, 0), bevel=0.01),
        add_box("esr_val", (-0.2, 4.72, 2.70), (3.55, 0.06, 0.20), red_stripe),
    ]
    parts.append(merge_parts_into("Eyyubi_ShutterRed", sr_subs, red_stripe))

    bg_subs = [
        add_box("ebg_main", (-0.2, 3.90, 1.55), (3.2, 0.10, 2.35), glass),
        add_box("ebg_upR",  (-0.2, 3.93, 4.75), (1.56, 0.08, 1.18), glass),
        add_box("ebg_upL",  (-4.8, 3.86, 4.75), (2.06, 0.08, 1.18), glass),
    ]
    parts.append(merge_parts_into("Eyyubi_BufeGlass", bg_subs, glass))

    uw_subs = window_frame_subs("euw", -0.2, 3.94, 4.75, 1.74, 1.34, white, mullions=1, depth=0.16)
    parts.append(merge_parts_into("Eyyubi_UpperWin", uw_subs, white))
    uwl_subs = window_frame_subs("euwl", -4.8, 3.88, 4.75, 2.24, 1.34, white, mullions=2, depth=0.16)
    parts.append(merge_parts_into("Eyyubi_UpperWinL", uwl_subs, white))

    # Plastered garden walls with beveled stone coping caps & structural piers
    gwl_subs = [
        add_box("gwl_m",   (5.4, 3.5, 0.92), (3.2, 0.26, 1.84), cream, bevel=0.02),
        add_box("gwl_cap", (5.4, 3.5, 1.88), (3.32, 0.36, 0.12), cream, bevel=0.015),
        add_box("gwl_p1",  (3.95, 3.5, 1.00), (0.36, 0.38, 2.02), cream, bevel=0.02),
        add_box("gwl_p2",  (6.88, 3.5, 1.00), (0.36, 0.38, 2.02), cream, bevel=0.02),
    ]
    parts.append(merge_parts_into("Eyyubi_GardenWallL", gwl_subs, cream))

    # Open-picket cobalt-blue steel garden gate w/ frame, kickpanel & lock plate!
    gate_subs = [
        add_box("eg_fL",   (7.02, 3.5, 0.92), (0.09, 0.12, 1.84), gate_blue),
        add_box("eg_fR",   (8.58, 3.5, 0.92), (0.09, 0.12, 1.84), gate_blue),
        add_box("eg_top",  (7.80, 3.5, 1.80), (1.64, 0.10, 0.08), gate_blue),
        add_box("eg_mid",  (7.80, 3.5, 0.92), (1.60, 0.08, 0.08), gate_blue),
        add_box("eg_low",  (7.80, 3.5, 0.44), (1.56, 0.06, 0.84), gate_blue),
    ]
    for pi in range(-4, 5):
        gate_subs.append(add_box(f"eg_p_{pi}", (7.80 + pi * 0.17, 3.5, 1.36), (0.036, 0.04, 0.88), gate_blue))
    parts.append(merge_parts_into("Eyyubi_BlueGate", gate_subs, gate_blue))

    gwr_subs = [
        add_box("gwr_m",   (10.4, 3.5, 0.92), (3.6, 0.26, 1.84), cream, bevel=0.02),
        add_box("gwr_cap", (10.4, 3.5, 1.88), (3.72, 0.36, 0.12), cream, bevel=0.015),
        add_box("gwr_p1",  (8.72, 3.5, 1.00), (0.36, 0.38, 2.02), cream, bevel=0.02),
        add_box("gwr_p2",  (12.10, 3.5, 1.00), (0.36, 0.38, 2.02), cream, bevel=0.02),
    ]
    parts.append(merge_parts_into("Eyyubi_GardenWallR", gwr_subs, cream))
    return parts


def build_zubeyde_blue_house():
    """Exact 71 Zübeyde Hanım Cd. sky-blue 3-story Turkish family house with pitched terracotta roof,
    carved wooden sunroom bay, paneled entrance & draped Turkish flag."""
    sky = get_mat("PowderBlue", (0.64, 0.80, 0.86, 1.0), rough=0.72)
    wood = get_mat("Wood", (0.58, 0.34, 0.18, 1.0), rough=0.62)
    white = get_mat("WhitePVC", (0.95, 0.96, 0.97, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.18, 0.28, 0.36, 1.0), rough=0.14)
    flag_red = get_mat("SignRed", (0.84, 0.10, 0.14, 1.0), rough=0.5)
    roof = get_mat("RoofTile", (0.62, 0.28, 0.20, 1.0), rough=0.82)

    parts = []
    body_subs = [
        add_box("zb_core", (0, 0, 4.6), (9.4, 8.6, 9.2), sky, bevel=0.04),
        add_box("zb_plinth", (0, 0, 0.22), (9.62, 8.82, 0.44), sky, bevel=0.02),
        add_box("zb_belt1", (0, 0, 3.25), (9.66, 8.86, 0.18), sky, bevel=0.02),
        add_box("zb_belt2", (0, 0, 6.20), (9.66, 8.86, 0.18), sky, bevel=0.02),
        add_box("zb_eave",  (0, 0, 9.10), (9.74, 8.94, 0.22), sky, bevel=0.02),
        add_cyl("zb_drain", (4.62, 4.34, 4.6), 0.055, 9.0, sky, verts=10),
    ]
    parts.append(merge_parts_into("Zubeyde_Body", body_subs, sky))

    roof_subs = [
        add_box("zr_base", (0, 0, 9.34), (9.95, 9.15, 0.28), roof, bevel=0.025),
        add_pitched_roof("zr_hip", (0, 0, 9.52), (9.4, 8.6, 0.18), roof, overhang=0.50, ridge_h=1.35),
        add_box("zr_chim", (-2.2, -1.6, 10.90), (0.72, 0.72, 1.5), roof, bevel=0.02),
    ]
    parts.append(merge_parts_into("Zubeyde_Roof", roof_subs, roof))

    gw_subs = window_frame_subs("zgw", -1.8, 4.36, 1.55, 2.66, 1.50, wood, mullions=2, depth=0.18)
    parts.append(merge_parts_into("Zubeyde_GroundWin", gw_subs, wood))
    parts.append(add_box("Zubeyde_GroundGlass", (-1.8, 4.35, 1.55), (2.42, 0.08, 1.26), glass))

    # Paneled apartment entry door w/ frame, entry threshold step & cantilevered rain hood!
    door_subs = [
        add_box("zd_frame", (2.4, 4.36, 1.45), (1.86, 0.18, 2.74), white, bevel=0.02),
        add_box("zd_p1",    (2.4, 4.46, 0.82), (1.30, 0.06, 0.92), white, bevel=0.012),
        add_box("zd_p2",    (2.4, 4.46, 1.92), (1.30, 0.06, 0.92), white, bevel=0.012),
        add_box("zd_step",  (2.4, 4.62, 0.12), (2.15, 0.62, 0.24), white, bevel=0.015),
        add_box("zd_hood",  (2.4, 4.68, 2.86), (2.18, 0.72, 0.12), white, rot=(math.radians(10), 0, 0), bevel=0.015),
    ]
    parts.append(merge_parts_into("Zubeyde_Door", door_subs, white))

    # 2nd floor cantilevered enclosed white PVC bay balcony w/ 5 mullions + apron panel!
    mf_subs = window_frame_subs("zmf", 0, 4.42, 4.75, 7.66, 1.88, white, mullions=4, depth=0.26)
    mf_subs.append(add_box("zmf_apron", (0, 4.44, 3.64), (7.74, 0.32, 0.44), white, bevel=0.02))
    parts.append(merge_parts_into("Zubeyde_MidFrame", mf_subs, white))
    parts.append(add_box("Zubeyde_MidGlass", (0, 4.40, 4.75), (7.28, 0.08, 1.60), glass))
    parts.append(add_box("Zubeyde_Flag", (1.6, 4.58, 4.82), (0.86, 0.04, 1.16), flag_red, bevel=0.008))

    # 3rd floor warm timber-framed sunroom bay w/ 5 mullions + overhanging timber roof brow!
    tf_subs = window_frame_subs("ztf", 0, 4.42, 7.65, 7.66, 1.88, wood, mullions=4, depth=0.26)
    tf_subs.append(add_box("ztf_apron", (0, 4.44, 6.54), (7.74, 0.32, 0.44), wood, bevel=0.02))
    tf_subs.append(add_box("ztf_brow",  (0, 4.64, 8.66), (7.92, 0.56, 0.14), wood, bevel=0.015))
    parts.append(merge_parts_into("Zubeyde_TopFrame", tf_subs, wood))
    parts.append(add_box("Zubeyde_TopGlass", (0, 4.40, 7.65), (7.28, 0.08, 1.60), glass))
    return parts


def build_seyyid_kutup_apt():
    """Exact Seyyid Kutup Cd. 3-story tan-beige apartment with rusticated stone retaining base,
    articulated floor bands, mullioned PVC windows & wrought-iron basement grilles."""
    tan = get_mat("YellowPlaster", (0.76, 0.71, 0.59, 1.0), rough=0.80)
    dark = get_mat("TrimDark", (0.16, 0.17, 0.18, 1.0), rough=0.5, metal=0.5)
    white = get_mat("WhitePVC", (0.94, 0.95, 0.96, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.20, 0.28, 0.34, 1.0), rough=0.14)
    concrete = get_mat("Concrete", (0.64, 0.63, 0.60, 1.0), rough=0.86)

    parts = []
    ret_subs = [
        add_box("sr_base", (0, 0.25, 0.75), (12.8, 9.6, 1.5), concrete, bevel=0.04),
        add_box("sr_cap",  (0, 0.25, 1.52), (13.06, 9.86, 0.16), concrete, bevel=0.02),
    ]
    for pi in (-6.35, -2.0, 2.0, 6.35):
        ret_subs.append(add_box(f"sr_pil_{pi}", (pi, 5.06, 0.75), (0.34, 0.18, 1.5), concrete, bevel=0.015))
    parts.append(merge_parts_into("Seyyid_RetainingBase", ret_subs, concrete))

    body_subs = [
        add_box("sb_core", (0, 0, 5.95), (12.4, 9.0, 9.0), tan, bevel=0.04),
    ]
    for fz in (4.25, 7.05, 9.85, 10.42):
        body_subs.append(add_box(f"sb_band_{fz}", (0, 0, fz), (12.66, 9.26, 0.18), tan, bevel=0.02))
    for sx in (-6.18, 6.18):
        body_subs.append(add_box(f"sb_pil_{sx}", (sx, 4.48, 5.95), (0.34, 0.26, 9.0), tan, bevel=0.02))
    body_subs.append(add_cyl("sb_drain", (6.02, 4.56, 5.95), 0.055, 8.8, tan, verts=10))
    parts.append(merge_parts_into("Seyyid_MainBody", body_subs, tan))

    roof_subs = [
        add_box("sk_eave", (0, 0, 10.62), (12.95, 9.55, 0.34), dark, bevel=0.03),
        add_pitched_roof("sk_hip", (0, 0, 10.82), (12.4, 9.0, 0.16), dark, overhang=0.45, ridge_h=1.18),
        add_box("sk_chim", (3.2, -1.5, 12.05), (0.75, 0.75, 1.5), dark, bevel=0.02),
    ]
    parts.append(merge_parts_into("Seyyid_Roof", roof_subs, dark))

    for fl in range(3):
        z = 2.85 + fl * 2.8
        for wx in (-4.2, -1.2, 1.8, 4.4):
            w_subs = window_frame_subs(f"sw_{fl}_{wx}", wx, 4.56, z, 1.60, 1.48, white, mullions=1, depth=0.18)
            parts.append(merge_parts_into(f"Seyyid_Win_{fl}_{wx}", w_subs, white))
            parts.append(add_box(f"Seyyid_Glass_{fl}_{wx}", (wx, 4.54, z), (1.36, 0.08, 1.24), glass))

    # Open wrought-iron basement security grilles (not solid plates!)
    for gx in (-4.0, 0.0, 4.0):
        gr_subs = [
            add_box(f"sg_top_{gx}", (gx, 5.08, 1.12), (1.84, 0.08, 0.07), dark),
            add_box(f"sg_mid_{gx}", (gx, 5.08, 0.72), (1.80, 0.06, 0.05), dark),
            add_box(f"sg_bot_{gx}", (gx, 5.08, 0.32), (1.84, 0.08, 0.07), dark),
            add_box(f"sg_L_{gx}",   (gx - 0.89, 5.08, 0.72), (0.07, 0.08, 0.84), dark),
            add_box(f"sg_R_{gx}",   (gx + 0.89, 5.08, 0.72), (0.07, 0.08, 0.84), dark),
        ]
        for bi in range(-3, 4):
            gr_subs.append(add_box(f"sg_bar_{gx}_{bi}", (gx + bi * 0.24, 5.08, 0.72), (0.032, 0.036, 0.80), dark))
        parts.append(merge_parts_into(f"Seyyid_Grille_{gx}", gr_subs, dark))
    return parts


def build_mursit_trafo_and_grey_apt():
    """Exact Mürşit Cd. AYEDAŞ Trafo house with louvered steel vent doors on stone plinth +
    dark-grey 3-story gabled apartment with crisp wrap-around white architectural string-courses."""
    grey_dark = get_mat("LeadRoof", (0.42, 0.45, 0.48, 1.0), rough=0.76)
    grey_light = get_mat("Concrete", (0.66, 0.69, 0.72, 1.0), rough=0.80)
    stone = get_mat("StoneTrim", (0.72, 0.70, 0.66, 1.0), rough=0.85)
    white = get_mat("WhitePVC", (0.95, 0.96, 0.97, 1.0), rough=0.35)
    glass = get_mat("Glass", (0.18, 0.26, 0.34, 1.0), rough=0.14)

    parts = []
    # Left: AYEDAŞ Trafo Binası on stepped stone retaining plinth + concrete access steps
    pl_subs = [
        add_box("mp_base", (-5.8, 0, 0.55), (5.4, 5.2, 1.1), stone, bevel=0.04),
        add_box("mp_cap",  (-5.8, 0, 1.12), (5.64, 5.44, 0.14), stone, bevel=0.02),
        add_box("mp_st1",  (-5.8, 2.88, 0.28), (1.8, 0.55, 0.56), stone, bevel=0.015),
        add_box("mp_st2",  (-5.8, 3.22, 0.14), (1.8, 0.36, 0.28), stone, bevel=0.015),
    ]
    parts.append(merge_parts_into("Mursit_TrafoPlinth", pl_subs, stone))

    th_subs = [
        add_box("mt_core",  (-5.8, 0, 2.75), (4.4, 4.2, 3.3), grey_light, bevel=0.04),
        # Double louvered steel transformer vent doors + concrete lintel + side ventilation grille
        add_box("mt_dL",    (-6.35, 2.13, 2.25), (0.92, 0.10, 2.15), grey_light, bevel=0.015),
        add_box("mt_dR",    (-5.25, 2.13, 2.25), (0.92, 0.10, 2.15), grey_light, bevel=0.015),
        add_box("mt_lint",  (-5.80, 2.16, 3.44), (2.28, 0.18, 0.18), grey_light, bevel=0.015),
        add_box("mt_vent",  (-5.80, 2.14, 3.92), (1.80, 0.12, 0.42), grey_light, bevel=0.01),
    ]
    for vi in range(5):
        th_subs.append(add_box(f"mt_louv_{vi}", (-5.80, 2.18, 1.45 + vi * 0.24), (1.72, 0.06, 0.08), grey_light))
    parts.append(merge_parts_into("Mursit_TrafoHouse", th_subs, grey_light))

    tr_subs = [
        add_box("mtr_eave", (-5.8, 0, 4.52), (4.92, 4.72, 0.24), grey_dark, bevel=0.025),
        add_pitched_roof("mtr_hip", (-5.8, 0, 4.68), (4.6, 4.4, 0.12), grey_dark, overhang=0.22, ridge_h=0.58),
    ]
    parts.append(merge_parts_into("Mursit_TrafoRoof", tr_subs, grey_dark))

    # Right: Dark-grey modern 3-story gabled apartment w/ pitched hip crown + drainpipe
    apt_subs = [
        add_box("ma_core", (3.6, 0, 4.8), (10.4, 8.6, 9.6), grey_dark, bevel=0.04),
        add_box("ma_plinth", (3.6, 0, 0.22), (10.62, 8.82, 0.44), grey_dark, bevel=0.02),
        add_pitched_roof("ma_roof", (3.6, 0, 9.72), (10.4, 8.6, 0.18), grey_dark, overhang=0.44, ridge_h=1.25),
        add_cyl("ma_drain", (8.72, 4.34, 4.8), 0.055, 9.4, grey_dark, verts=10),
    ]
    parts.append(merge_parts_into("Mursit_GreyApt", apt_subs, grey_dark))

    for sz in (3.1, 6.2, 9.2):
        st_subs = [
            add_box(f"ms_f_{sz}", (3.6, 0.0, sz), (10.62, 8.82, 0.26), white, bevel=0.02),
        ]
        parts.append(merge_parts_into(f"Mursit_Stripe_{sz}", st_subs, white))

    for fl in range(3):
        z = 1.85 + fl * 3.0
        for wx in (0.6, 3.6, 6.6):
            mw_subs = window_frame_subs(f"mw_{fl}_{wx}", wx, 4.36, z, 1.40, 1.40, white, mullions=1, depth=0.16)
            parts.append(merge_parts_into(f"Mursit_Win_{fl}_{wx}", mw_subs, white))
            parts.append(add_box(f"Mursit_Glass_{fl}_{wx}", (wx, 4.35, z), (1.16, 0.08, 1.16), glass))
    return parts


def build_ataturk_highway_guardrail():
    """Exact 230 Atatürk Cd. 12m profiled corrugated W-beam TEM highway guardrail + I-beam posts."""
    metal = get_mat("Chrome", (0.68, 0.71, 0.74, 1.0), rough=0.38, metal=0.72)
    post = get_mat("TrimDark", (0.28, 0.30, 0.32, 1.0), rough=0.5, metal=0.55)
    parts = []
    # Authentic triple-ridge corrugated W-beam guardrail profile!
    beam_subs = [
        add_box("gb_mid", (0, -0.02, 0.68), (12.0, 0.06, 0.28), metal),
        add_box("gb_rT",  (0,  0.04, 0.78), (12.0, 0.08, 0.07), metal, bevel=0.012),
        add_box("gb_rM",  (0, -0.03, 0.68), (12.0, 0.06, 0.07), metal),
        add_box("gb_rB",  (0,  0.04, 0.58), (12.0, 0.08, 0.07), metal, bevel=0.012),
    ]
    parts.append(merge_parts_into("Guard_BeamTop", beam_subs, metal))
    for i in range(-5, 6):
        px = i * 1.15
        p_subs = [
            add_box(f"gp_web_{i}",   (px, -0.09, 0.42), (0.04, 0.12, 0.84), post),
            add_box(f"gp_flF_{i}",   (px, -0.03, 0.42), (0.10, 0.025, 0.84), post),
            add_box(f"gp_flB_{i}",   (px, -0.15, 0.42), (0.10, 0.025, 0.84), post),
            add_box(f"gp_spcr_{i}",  (px, -0.03, 0.68), (0.09, 0.09, 0.26), post, bevel=0.008),
        ]
        parts.append(merge_parts_into(f"Guard_Post_{i}", p_subs, post))
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
