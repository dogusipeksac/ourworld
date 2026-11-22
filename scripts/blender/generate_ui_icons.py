#!/usr/bin/env python3
"""
GRIDROAM / OurWorld — Mobile HUD icon generator (Blender 5.3+, run headless):

  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P scripts/blender/generate_ui_icons.py

Builds each icon as a softly bevelled, extruded 3D glyph, lights it from the top-left and renders
a 256x256 transparent PNG into Assets/GRIDROAM/Resources/UI/Icons/. Icons are rendered in
off-white so Unity can tint them; HudIconSkin.cs mounts them onto the existing HUD buttons.
"""

import math
import os
import bpy
import bmesh
from mathutils import Vector, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_DIR = os.environ.get("GRIDROAM_ICON_OUT", os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "UI", "Icons"))
SIZE = 256
os.makedirs(OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Scene helpers
# ---------------------------------------------------------------------------
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = SIZE
    scene.render.resolution_y = SIZE
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass

    world = bpy.data.worlds.new("IconWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.55, 0.58, 0.62, 1.0)
    bg.inputs["Strength"].default_value = 0.9

    key = bpy.data.objects.new("Key", bpy.data.lights.new("Key", type="SUN"))
    key.data.energy = 3.4
    key.rotation_euler = Euler((math.radians(38), math.radians(-28), 0.0), "XYZ")
    bpy.context.collection.objects.link(key)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.5
    cam.location = (0.0, 0.0, 10.0)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam


def glyph_material():
    mat = bpy.data.materials.new("IconWhite")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.93, 0.94, 0.95, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.42
    return mat


def finish(obj, depth=0.16, bevel=0.045):
    """Give a flat 2D mesh volume + rounded edges."""
    obj.data.materials.append(glyph_material())
    sol = obj.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = depth
    sol.offset = 1.0
    bev = obj.modifiers.new("Bevel", "BEVEL")
    bev.width = bevel
    bev.segments = 4
    bev.limit_method = "ANGLE"
    bev.angle_limit = math.radians(40)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def mesh_from_bm(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def poly(name, pts, depth=0.16, bevel=0.045):
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, 0.0)) for (x, y) in pts]
    face = bm.faces.new(vs)
    if face.normal.z < 0:
        face.normal_flip()
    return finish(mesh_from_bm(name, bm), depth, bevel)


def rounded_rect_pts(cx, cy, w, h, r, seg=6):
    pts = []
    corners = [(cx + w / 2 - r, cy + h / 2 - r, 0), (cx - w / 2 + r, cy + h / 2 - r, 90),
               (cx - w / 2 + r, cy - h / 2 + r, 180), (cx + w / 2 - r, cy - h / 2 + r, 270)]
    for (ox, oy, start) in corners:
        for i in range(seg + 1):
            a = math.radians(start + 90.0 * i / seg)
            pts.append((ox + r * math.cos(a), oy + r * math.sin(a)))
    return pts


def rrect(name, cx, cy, w, h, r, depth=0.16, bevel=0.045):
    return poly(name, rounded_rect_pts(cx, cy, w, h, r), depth, bevel)


def annulus(name, cx, cy, r_out, r_in, seg=48, a0=0.0, a1=360.0, depth=0.16, bevel=0.04):
    bm = bmesh.new()
    full = abs(a1 - a0) >= 359.9
    n = seg if full else seg + 1
    outer, inner = [], []
    for i in range(n):
        a = math.radians(a0 + (a1 - a0) * i / seg)
        outer.append(bm.verts.new((cx + r_out * math.cos(a), cy + r_out * math.sin(a), 0.0)))
        inner.append(bm.verts.new((cx + r_in * math.cos(a), cy + r_in * math.sin(a), 0.0)))
    count = n if full else n - 1
    for i in range(count):
        j = (i + 1) % n
        bm.faces.new((inner[i], outer[i], outer[j], inner[j]))
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    return finish(mesh_from_bm(name, bm), depth, bevel)


def disc(name, cx, cy, r, seg=40, depth=0.16, bevel=0.04):
    pts = [(cx + r * math.cos(2 * math.pi * i / seg), cy + r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    return poly(name, pts, depth, bevel)


def text_glyph(name, body, cx, cy, size=1.0):
    curve = bpy.data.curves.new(name, type="FONT")
    curve.body = body
    curve.size = size
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.extrude = 0.07
    curve.bevel_depth = 0.025
    curve.bevel_resolution = 3
    obj = bpy.data.objects.new(name, curve)
    obj.location = (cx, cy, 0.08)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(glyph_material())
    return obj


def render(icon_name):
    path = os.path.join(OUT_DIR, f"{icon_name}.png")
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"  icon -> {path}")


# ---------------------------------------------------------------------------
# Icon designs (unit space roughly [-1, 1])
# ---------------------------------------------------------------------------
def icon_jump():
    poly("Arrow", [(0.0, 0.95), (0.62, 0.30), (0.24, 0.30), (0.24, -0.35), (-0.24, -0.35), (-0.24, 0.30), (-0.62, 0.30)])
    rrect("Ground", 0.0, -0.72, 1.30, 0.20, 0.09)


def icon_sprint():
    poly("Bolt", [(0.18, 1.0), (-0.52, -0.08), (-0.04, -0.08), (-0.24, -1.0), (0.56, 0.16), (0.06, 0.16), (0.32, 1.0)])


def icon_attack():
    # Stylised fist: four rounded fingers, palm block and folded thumb
    for i in range(4):
        rrect(f"Finger{i}", -0.48 + i * 0.32, 0.42, 0.30, 0.46, 0.14)
    rrect("Palm", 0.0, -0.18, 1.26, 0.78, 0.22)
    poly("Thumb", rounded_rect_pts(-0.12, 0.02, 0.86, 0.26, 0.12), depth=0.24)
    rrect("Wrist", 0.0, -0.78, 0.86, 0.34, 0.10)


def icon_interact():
    disc("Dot", 0.0, 0.0, 0.26)
    annulus("Ring1", 0.0, 0.0, 0.62, 0.48)
    annulus("Ring2", 0.0, 0.0, 0.98, 0.86, a0=20, a1=160)
    annulus("Ring3", 0.0, 0.0, 0.98, 0.86, a0=200, a1=340)


def icon_phone():
    bm = bmesh.new()
    outer = [bm.verts.new((x, y, 0)) for (x, y) in rounded_rect_pts(0.0, 0.0, 1.14, 1.86, 0.22)]
    inner = [bm.verts.new((x, y, 0)) for (x, y) in rounded_rect_pts(0.0, 0.08, 0.86, 1.34, 0.08)]
    # Frame = bridge between outer and inner loops (same vertex count)
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((inner[i], outer[i], outer[j], inner[j]))
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    finish(mesh_from_bm("PhoneFrame", bm), 0.18, 0.04)
    disc("HomeBtn", 0.0, -0.76, 0.07, seg=24, depth=0.12, bevel=0.02)
    rrect("Screen", 0.0, 0.08, 0.70, 1.18, 0.06, depth=0.05, bevel=0.015)


def pedal(w, h):
    rrect("Pedal", 0.0, 0.0, w, h, 0.16)
    rows = 4 if h > w else 2
    for i in range(rows):
        y = (i - (rows - 1) / 2.0) * (h / (rows + 1))
        rrect(f"Grip{i}", 0.0, y, w * 0.70, 0.07, 0.03, depth=0.24, bevel=0.02)


def icon_gas():
    pedal(0.86, 1.80)


def icon_brake():
    pedal(1.50, 1.10)


def icon_handbrake():
    annulus("Circle", 0.0, 0.0, 0.70, 0.56)
    annulus("ArcL", 0.0, 0.0, 1.00, 0.88, a0=125, a1=235)
    annulus("ArcR", 0.0, 0.0, 1.00, 0.88, a0=-55, a1=55)
    text_glyph("P", "P", 0.0, -0.02, size=0.86)


def icon_exit_car():
    # Open car door + outward arrow
    poly("Door", [(-0.98, 0.10), (-0.58, 0.86), (-0.02, 0.86), (-0.02, -0.88), (-0.98, -0.88)], depth=0.14)
    rrect("Window", -0.44, 0.42, 0.40, 0.04, 0.02, depth=0.22, bevel=0.015)
    rrect("Handle", -0.22, 0.02, 0.26, 0.08, 0.04, depth=0.22, bevel=0.02)
    poly("Arrow", [(0.98, 0.0), (0.56, 0.42), (0.56, 0.16), (0.14, 0.16), (0.14, -0.16), (0.56, -0.16), (0.56, -0.42)], depth=0.22)


def icon_steer(direction):
    s = -1.0 if direction == "left" else 1.0
    poly("Chevron", [(s * 0.70, 0.0), (s * -0.10, 0.88), (s * -0.50, 0.88), (s * 0.28, 0.0), (s * -0.50, -0.88), (s * -0.10, -0.88)])


ICONS = {
    "icon_jump": icon_jump,
    "icon_sprint": icon_sprint,
    "icon_attack": icon_attack,
    "icon_interact": icon_interact,
    "icon_phone": icon_phone,
    "icon_gas": icon_gas,
    "icon_brake": icon_brake,
    "icon_handbrake": icon_handbrake,
    "icon_exit_car": icon_exit_car,
    "icon_steer_left": lambda: icon_steer("left"),
    "icon_steer_right": lambda: icon_steer("right"),
}


def main():
    print(f"[GRIDROAM Icons] Rendering {len(ICONS)} HUD icons into {OUT_DIR}")
    for name, build in ICONS.items():
        reset()
        build()
        render(name)
    print("[GRIDROAM Icons] Done.")


if __name__ == "__main__":
    main()
