#!/usr/bin/env python3
"""
Generates seamless 512x512 PBR Albedo + OpenGL Normal textures for GRIDROAM characters,
wooden park benches, carved travertine fountain stone, and fabric clothing.
Runs via Blender's bundled Python (numpy + bpy.data.images).
"""

import os
import math
import numpy as np
import bpy

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Art", "Textures", "RealismPBR")
os.makedirs(OUT_DIR, exist_ok=True)


def save_rgba_png(path, rgb_float):
    """Save HxWx3 float32 [0..1] array as PNG via bpy.data.images."""
    h, w, _ = rgb_float.shape
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb_float, 0.0, 1.0)
    # Flip vertically for image convention
    pixels = rgba[::-1, :, :].ravel()
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h, alpha=False)
    img.pixels.foreach_set(pixels)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def height_to_normal_gl(height, strength=2.2):
    """Convert tileable HxW float heightmap [0..1] into OpenGL tangent-space Normal map RGB [0..1]."""
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5 * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5 * strength
    nx = -dx
    ny = dy  # OpenGL +Y up
    nz = np.ones_like(height)
    inv_len = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz + 1e-8)
    rgb = np.stack([nx * inv_len * 0.5 + 0.5, ny * inv_len * 0.5 + 0.5, nz * inv_len * 0.5 + 0.5], axis=-1)
    return rgb.astype(np.float32)


def tileable_value_noise(size, freq, seed):
    rng = np.random.default_rng(seed)
    grid = rng.random((freq, freq), dtype=np.float32)
    ys = np.linspace(0, freq, size, endpoint=False)
    xs = np.linspace(0, freq, size, endpoint=False)
    y0 = np.floor(ys).astype(int) % freq
    x0 = np.floor(xs).astype(int) % freq
    y1 = (y0 + 1) % freq
    x1 = (x0 + 1) % freq
    fy = (ys - np.floor(ys)).astype(np.float32)
    fx = (xs - np.floor(xs)).astype(np.float32)
    # Smoothstep
    fy = fy * fy * (3.0 - 2.0 * fy)
    fx = fx * fx * (3.0 - 2.0 * fx)
    c00 = grid[np.ix_(y0, x0)]
    c10 = grid[np.ix_(y0, x1)]
    c01 = grid[np.ix_(y1, x0)]
    c11 = grid[np.ix_(y1, x1)]
    top = c00 * (1.0 - fx[None, :]) + c10 * fx[None, :]
    bot = c01 * (1.0 - fx[None, :]) + c11 * fx[None, :]
    return top * (1.0 - fy[:, None]) + bot * fy[:, None]


def fbm_tileable(size, base_freq=8, octaves=4, seed=10):
    acc = np.zeros((size, size), dtype=np.float32)
    amp = 0.5
    norm = 0.0
    for o in range(octaves):
        acc += amp * tileable_value_noise(size, base_freq * (2 ** o), seed + o * 31)
        norm += amp
        amp *= 0.5
    return acc / norm


def generate_denim(size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    # Classic diagonal 3x1 twill weave + vertical slub yarn variation
    twill = 0.5 + 0.5 * np.sin((x * 1.0 + y * 1.0) * (2.0 * math.pi / 4.0))
    warp = 0.5 + 0.5 * np.sin(x * (2.0 * math.pi / 3.0))
    weft = 0.5 + 0.5 * np.sin(y * (2.0 * math.pi / 3.0))
    slub = tileable_value_noise(size, 64, 101) * 0.28 + tileable_value_noise(size, 16, 102) * 0.22
    h = np.clip(twill * 0.45 + warp * 0.20 + weft * 0.15 + slub * 0.40, 0.0, 1.0)
    # Neutral light greyscale tintable albedo (so Unity _BaseColor tints it cleanly!)
    lum = 0.80 + (h - 0.5) * 0.28
    albedo = np.stack([lum * 0.98, lum * 0.99, lum * 1.02], axis=-1)
    normal = height_to_normal_gl(h, strength=2.6)
    save_rgba_png(os.path.join(OUT_DIR, "Denim_Color.png"), albedo)
    save_rgba_png(os.path.join(OUT_DIR, "Denim_NormalGL.png"), normal)


def generate_cotton(size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    weave = (np.sin(x * (2.0 * math.pi / 4.0)) * np.sin(y * (2.0 * math.pi / 4.0))) * 0.5 + 0.5
    folds = fbm_tileable(size, base_freq=4, octaves=4, seed=201)
    micro = tileable_value_noise(size, 96, 202)
    h = np.clip(weave * 0.35 + folds * 0.45 + micro * 0.20, 0.0, 1.0)
    lum = 0.86 + (h - 0.5) * 0.22
    albedo = np.stack([lum, lum, lum], axis=-1)
    normal = height_to_normal_gl(h, strength=2.0)
    save_rgba_png(os.path.join(OUT_DIR, "Cotton_Color.png"), albedo)
    save_rgba_png(os.path.join(OUT_DIR, "Cotton_NormalGL.png"), normal)


def generate_hair(size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    # Fine vertical hair strands with gentle organic wave
    wave = fbm_tileable(size, base_freq=4, octaves=3, seed=301) * 14.0
    strands_fine = 0.5 + 0.5 * np.sin((x + wave) * (2.0 * math.pi / 3.5))
    strands_broad = 0.5 + 0.5 * np.sin((x * 0.5 + wave * 0.7) * (2.0 * math.pi / 9.0))
    clump = tileable_value_noise(size, 32, 302)
    h = np.clip(strands_fine * 0.50 + strands_broad * 0.30 + clump * 0.20, 0.0, 1.0)
    lum = 0.76 + (h - 0.5) * 0.42
    albedo = np.stack([lum, lum, lum], axis=-1)
    normal = height_to_normal_gl(h, strength=3.4)
    save_rgba_png(os.path.join(OUT_DIR, "Hair_Color.png"), albedo)
    save_rgba_png(os.path.join(OUT_DIR, "Hair_NormalGL.png"), normal)


def generate_skin_normal(size=512):
    pores = tileable_value_noise(size, 128, 401) * 0.65 + tileable_value_noise(size, 32, 402) * 0.35
    normal = height_to_normal_gl(pores, strength=0.65)
    save_rgba_png(os.path.join(OUT_DIR, "Skin_NormalGL.png"), normal)


def generate_teak_wood(size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    warp = fbm_tileable(size, base_freq=4, octaves=4, seed=501) * 18.0
    rings = 0.5 + 0.5 * np.sin((y * 0.35 + warp) * (2.0 * math.pi / 18.0))
    fibers = tileable_value_noise(size, 64, 502)
    fine = 0.5 + 0.5 * np.sin((y * 2.0 + warp * 0.4) * (2.0 * math.pi / 5.0))
    h = np.clip(rings * 0.55 + fibers * 0.25 + fine * 0.20, 0.0, 1.0)
    r = 0.46 + (h - 0.5) * 0.22
    g = 0.29 + (h - 0.5) * 0.15
    b = 0.16 + (h - 0.5) * 0.09
    albedo = np.stack([r, g, b], axis=-1)
    normal = height_to_normal_gl(h, strength=2.4)
    save_rgba_png(os.path.join(OUT_DIR, "TeakWood_Color.png"), albedo)
    save_rgba_png(os.path.join(OUT_DIR, "TeakWood_NormalGL.png"), normal)


def generate_travertine_stone(size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    base = fbm_tileable(size, base_freq=6, octaves=5, seed=601)
    veins = 0.5 + 0.5 * np.sin((y * 0.25 + base * 16.0) * (2.0 * math.pi / 32.0))
    pits = np.where(tileable_value_noise(size, 64, 602) < 0.12, 0.15, 0.85).astype(np.float32)
    h = np.clip(base * 0.55 + veins * 0.25 + pits * 0.20, 0.0, 1.0)
    r = 0.84 + (h - 0.5) * 0.16
    g = 0.80 + (h - 0.5) * 0.16
    b = 0.73 + (h - 0.5) * 0.15
    albedo = np.stack([r, g, b], axis=-1)
    normal = height_to_normal_gl(h, strength=2.1)
    save_rgba_png(os.path.join(OUT_DIR, "Travertine_Color.png"), albedo)
    save_rgba_png(os.path.join(OUT_DIR, "Travertine_NormalGL.png"), normal)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    generate_denim()
    generate_cotton()
    generate_hair()
    generate_skin_normal()
    generate_teak_wood()
    generate_travertine_stone()
    print(f"Generated all RealismPBR textures in {OUT_DIR}")


if __name__ == "__main__":
    main()
