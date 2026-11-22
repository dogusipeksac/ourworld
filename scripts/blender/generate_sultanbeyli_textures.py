#!/usr/bin/env python3
"""
Generates authentic Sultanbeyli street PBR textures (512x512 Albedo + OpenGL Normal):
  - KilitParke_Color.png / KilitParke_NormalGL.png (Red & grey interlocking Turkish sidewalk paving bricks)
  - RawBrick_Color.png   / RawBrick_NormalGL.png   (Exposed hollow red Turkish construction bricks with grey mortar)
"""

import os
import math
import numpy as np
import bpy

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Textures", "RealismPBR")
os.makedirs(OUT_DIR, exist_ok=True)


def save_png(path, rgb_float):
    h, w, _ = rgb_float.shape
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb_float, 0.0, 1.0)
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h, alpha=False)
    img.pixels.foreach_set(rgba[::-1, :, :].ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def height_to_normal_gl(height, strength=3.0):
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5 * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5 * strength
    nx, ny, nz = -dx, dy, np.ones_like(height)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz + 1e-8)
    return np.stack([nx * inv * 0.5 + 0.5, ny * inv * 0.5 + 0.5, nz * inv * 0.5 + 0.5], axis=-1).astype(np.float32)


def generate_kilit_parke(size=512):
    rng = np.random.default_rng(771)
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    # Authentic Turkish interlocking kilit parke bricks (red border stripes + grey stone center!)
    bw, bh = 32.0, 64.0
    row = np.floor(y / bh).astype(int)
    # Zig-zag interlocking offset
    zig = np.sin((y / bh) * 2.0 * math.pi) * 4.5
    u = (x + (row % 2) * (bw * 0.5) + zig) % bw
    v = y % bh

    seam_u = np.minimum(u, bw - u)
    seam_v = np.minimum(v, bh - v)
    seam = np.minimum(seam_u, seam_v)
    bevel = np.clip(seam / 3.2, 0.0, 1.0)

    col_idx = np.floor((x + (row % 2) * (bw * 0.5)) / bw).astype(int) % 16
    # Subtle grain noise
    grain = rng.normal(0.0, 0.04, (size, size)).astype(np.float32)

    # Red & grey alternating bands exactly like 48 Selman Sokağı sidewalk!
    is_red = ((col_idx // 3) % 2 == 0)
    r = np.where(is_red, 0.58, 0.52) + grain
    g = np.where(is_red, 0.28, 0.51) + grain
    b = np.where(is_red, 0.24, 0.49) + grain

    # Darken grout joints
    grout = 0.35 + 0.65 * bevel
    rgb = np.stack([r * grout, g * grout, b * grout], axis=-1)
    h = np.clip(bevel * 0.82 + grain * 1.2 + 0.12, 0.0, 1.0)

    save_png(os.path.join(OUT_DIR, "KilitParke_Color.png"), rgb)
    save_png(os.path.join(OUT_DIR, "KilitParke_NormalGL.png"), height_to_normal_gl(h, strength=3.4))


def generate_raw_brick(size=512):
    rng = np.random.default_rng(772)
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    # Exposed hollow Turkish construction brick (tuğla + grey cement mortar)
    bw, bh = 64.0, 32.0
    row = np.floor(y / bh).astype(int)
    u = (x + (row % 2) * (bw * 0.5)) % bw
    v = y % bh

    mortar_w, mortar_h = 3.5, 3.2
    in_brick = (u > mortar_w) & (u < bw - mortar_w) & (v > mortar_h) & (v < bh - mortar_h)

    col = np.floor((x + (row % 2) * (bw * 0.5)) / bw).astype(int)
    brick_var = (((row * 37 + col * 19) % 11) / 11.0 - 0.5) * 0.14
    grain = rng.normal(0.0, 0.035, (size, size)).astype(np.float32)
    # Horizontal ribbed grooves on Turkish tuğla
    ribs = 0.03 * np.sin(v * 2.0 * math.pi / 6.0)

    r = np.where(in_brick, 0.68 + brick_var + ribs, 0.56) + grain
    g = np.where(in_brick, 0.30 + brick_var * 0.6 + ribs, 0.55) + grain
    b = np.where(in_brick, 0.18 + brick_var * 0.4, 0.52) + grain

    h = np.where(in_brick, 0.78 + ribs * 2.5 + grain, 0.26 + grain)
    save_png(os.path.join(OUT_DIR, "RawBrick_Color.png"), np.stack([r, g, b], axis=-1))
    save_png(os.path.join(OUT_DIR, "RawBrick_NormalGL.png"), height_to_normal_gl(h, strength=3.8))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    generate_kilit_parke()
    generate_raw_brick()
    print(f"Generated Sultanbeyli PBR textures in {OUT_DIR}")


if __name__ == "__main__":
    main()
