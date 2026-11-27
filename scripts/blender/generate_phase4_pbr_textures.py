#!/usr/bin/env python3
"""
GRIDROAM Phase 4 – Shared Stylized-Realistic PBR Texture Library Generator (512x512 seamless)
Writes compact, reusable PBR albedo & normal maps into Assets/GRIDROAM/Resources/OurWorldTheme/Textures/
(auto-imported & ASTC compressed by OurWorldLifeModelImporter.cs):
  - animal_fur_short_normal.png       (Shared smooth velvety directional fur/hide normal: Cow, Dogs, Goat, Cat)
  - animal_feather_normal.png         (Shared scalloped overlapping avian plumage normal: Chicken, Pigeon, Duck, Swan)
  - animal_fish_scales_albedo/normal  (Overlapping iridescent Bosphorus fish scales)
  - wood_oak_albedo/normal            (Warm natural oak/walnut wood grain: Furniture, Docks, Stalls, Props)
  - fabric_linen_albedo/normal        (Soft woven upholstery fabric: Sofas, Cushions, Beds, Awnings)
  - metal_brushed_normal.png          (Brushed steel/aluminum micro-grain: Bikes, Moto, Garage, Street Props)
  - crop_leaf_albedo/normal           (Lush botanical veined leaf texture: Crops, Bushes, Shrubs)
Pure Python + Pillow (zero external deps).
"""

import math
import os
import random
from PIL import Image, ImageFilter

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldTheme", "Textures")
os.makedirs(OUT_DIR, exist_ok=True)


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else (hi if v > hi else v)


def smoothstep(e0, e1, x):
    t = clamp((x - e0) / max(1e-6, e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def make_noise_sampler(seed, n=24):
    rng = random.Random(seed)
    g = [[rng.random() for _ in range(n)] for _ in range(n)]

    def sample(u, v):
        gx = (u * n) % n
        gy = (v * n) % n
        x0, y0 = int(gx), int(gy)
        x1, y1 = (x0 + 1) % n, (y0 + 1) % n
        fx, fy = gx - x0, gy - y0
        fx = fx * fx * (3.0 - 2.0 * fx)
        fy = fy * fy * (3.0 - 2.0 * fy)
        return (g[y0][x0] * (1 - fx) + g[y0][x1] * fx) * (1 - fy) + (g[y1][x0] * (1 - fx) + g[y1][x1] * fx) * fy

    return sample


def height_to_normal(h, size, strength=2.0):
    img = Image.new("RGB", (size, size))
    px = img.load()
    for y in range(size):
        ym1, yp1 = (y - 1) % size, (y + 1) % size
        rm, r0, rp = h[ym1], h[y], h[yp1]
        for x in range(size):
            xm1, xp1 = (x - 1) % size, (x + 1) % size
            dx = ((rm[xp1] + 2.0 * r0[xp1] + rp[xp1]) - (rm[xm1] + 2.0 * r0[xm1] + rp[xm1])) * strength
            dy = ((rp[xm1] + 2.0 * rp[x] + rp[xp1]) - (rm[xm1] + 2.0 * rm[x] + rm[xp1])) * strength
            nx, ny, nz = -dx, -dy, 1.0
            il = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz)
            px[x, y] = (
                int(clamp((nx * il * 0.5 + 0.5) * 255.0, 0, 255)),
                int(clamp((ny * il * 0.5 + 0.5) * 255.0, 0, 255)),
                int(clamp((nz * il * 0.5 + 0.5) * 255.0, 0, 255)),
            )
    return img


def gen_fur_short_normal(size=512):
    n1 = make_noise_sampler(101, 24)
    n2 = make_noise_sampler(102, 48)
    h = [[0.0] * size for _ in range(size)]
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            sway = (n1(u, v) - 0.5) * 0.055
            f1 = 0.5 + 0.5 * math.sin((u + sway) * math.tau * 42.0 + n2(u, v) * 3.2)
            f2 = 0.5 + 0.5 * math.sin((u - sway * 0.7) * math.tau * 78.0 + v * math.tau * 4.0)
            tuft = n1(u * 2.0, v * 0.8)
            h[y][x] = 0.40 * tuft + 0.38 * f1 + 0.22 * f2
    img = height_to_normal(h, size, strength=1.55).filter(ImageFilter.GaussianBlur(0.4))
    img.save(os.path.join(OUT_DIR, "animal_fur_short_normal.png"), optimize=True)
    print("  Generated animal_fur_short_normal.png")


def gen_feather_normal(size=512):
    n1 = make_noise_sampler(201, 20)
    h = [[0.0] * size for _ in range(size)]
    cols, rows = 14, 10
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            best = 0.0
            for dr in (-1, 0, 1):
                r_idx = int(v * rows) + dr
                r_mod = r_idx % rows
                x_shift = 0.5 if (r_mod % 2 == 1) else 0.0
                for dc in (-1, 0, 1):
                    c_idx = int(u * cols - x_shift) + dc
                    cu = (c_idx + 0.5 + x_shift) / cols
                    cv = (r_idx + 0.5) / rows
                    du = (u - cu) * cols
                    dv = (v - cv) * rows
                    # Teardrop feather vane overlapping downwards
                    if dv < -0.65 or dv > 0.75:
                        continue
                    w = 0.58 * (1.0 - 0.35 * max(0.0, dv))
                    d2 = (du / max(0.1, w)) ** 2 + ((dv - 0.05) / 0.72) ** 2
                    if d2 < 1.0:
                        dome = math.sqrt(1.0 - d2)
                        barb = 0.88 + 0.12 * math.sin((abs(du) * 18.0 - dv * 12.0))
                        # Central quill shaft highlight
                        quill = 0.14 * math.exp(-((du * 9.0) ** 2))
                        val = clamp(dome * barb + quill, 0.0, 1.0)
                        if val > best:
                            best = val
            h[y][x] = best * 0.88 + n1(u * 2.0, v * 2.0) * 0.12
    img = height_to_normal(h, size, strength=1.8).filter(ImageFilter.GaussianBlur(0.5))
    img.save(os.path.join(OUT_DIR, "animal_feather_normal.png"), optimize=True)
    print("  Generated animal_feather_normal.png")


def gen_fish_scales(size=512):
    n1 = make_noise_sampler(301, 16)
    h = [[0.0] * size for _ in range(size)]
    alb = Image.new("RGB", (size, size))
    px = alb.load()
    cols, rows = 20, 18
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            best = 0.0
            for dr in (-1, 0, 1):
                ri = int(v * rows) + dr
                sh = 0.5 if ((ri % rows) % 2 == 1) else 0.0
                for dc in (-1, 0, 1):
                    ci = int(u * cols - sh) + dc
                    du = (u - (ci + 0.5 + sh) / cols) * cols
                    dv = (v - (ri + 0.5) / rows) * rows
                    d2 = du * du / 0.36 + dv * dv / 0.42
                    if d2 < 1.0:
                        val = math.sqrt(1.0 - d2)
                        if val > best:
                            best = val
            shimmer = n1(u * 2.0, v * 2.0)
            h[y][x] = best
            t = smoothstep(0.10, 0.92, best)
            r = 85.0 + t * 118.0 + shimmer * 14.0
            g = 122.0 + t * 102.0 + shimmer * 12.0
            b = 148.0 + t * 88.0
            px[x, y] = (int(clamp(r, 0, 255)), int(clamp(g, 0, 255)), int(clamp(b, 0, 255)))
    alb.save(os.path.join(OUT_DIR, "animal_fish_scales_albedo.png"), optimize=True)
    height_to_normal(h, size, strength=1.9).save(os.path.join(OUT_DIR, "animal_fish_scales_normal.png"), optimize=True)
    print("  Generated animal_fish_scales_albedo/normal.png")


def gen_wood_oak(size=512):
    n1 = make_noise_sampler(401, 16)
    n2 = make_noise_sampler(402, 48)
    h = [[0.0] * size for _ in range(size)]
    alb = Image.new("RGB", (size, size))
    px = alb.load()
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            warp = (n1(u, v * 0.5) - 0.5) * 0.14 + (n2(u * 2.0, v) - 0.5) * 0.03
            ring = math.sin((u * 9.0 + warp * 12.0) * math.tau)
            ring = 0.5 + 0.5 * ring
            ring = ring ** 0.72
            pore = 0.5 + 0.5 * math.sin((u * 96.0 + n2(u, v) * 3.0) * math.tau)
            # Subtle warm wood plank seams (4 planks across U)
            pu = (u * 4.0) % 1.0
            seam = smoothstep(0.0, 0.018, pu) * smoothstep(0.0, 0.018, 1.0 - pu)
            plank_tone = 0.94 + 0.12 * ((int(u * 4.0) * 37 % 5) / 4.0)
            val = clamp((0.62 * ring + 0.24 * n2(u, v * 0.5) + 0.14 * pore) * (0.45 + 0.55 * seam), 0.0, 1.0)
            h[y][x] = val
            r = (118.0 + val * 86.0) * plank_tone * (0.55 + 0.45 * seam)
            g = (78.0 + val * 64.0) * plank_tone * (0.55 + 0.45 * seam)
            b = (48.0 + val * 40.0) * plank_tone * (0.55 + 0.45 * seam)
            px[x, y] = (int(clamp(r, 0, 255)), int(clamp(g, 0, 255)), int(clamp(b, 0, 255)))
    alb.save(os.path.join(OUT_DIR, "wood_oak_albedo.png"), optimize=True)
    height_to_normal(h, size, strength=1.5).save(os.path.join(OUT_DIR, "wood_oak_normal.png"), optimize=True)
    print("  Generated wood_oak_albedo/normal.png")


def gen_fabric_linen(size=512):
    n1 = make_noise_sampler(501, 20)
    h = [[0.0] * size for _ in range(size)]
    alb = Image.new("RGB", (size, size))
    px = alb.load()
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            warp = 0.5 + 0.5 * math.sin(u * math.tau * 64.0)
            weft = 0.5 + 0.5 * math.sin(v * math.tau * 64.0)
            checker = 1.0 if ((int(u * 64.0) + int(v * 64.0)) % 2 == 0) else 0.0
            weave = warp * checker + weft * (1.0 - checker)
            fold = n1(u * 1.5, v * 1.5)
            val = clamp(0.55 * weave + 0.45 * fold, 0.0, 1.0)
            h[y][x] = val
            c = int(clamp(192.0 + val * 56.0, 0, 255))
            px[x, y] = (c, int(c * 0.99), int(c * 0.96))
    alb.save(os.path.join(OUT_DIR, "fabric_linen_albedo.png"), optimize=True)
    height_to_normal(h, size, strength=1.25).save(os.path.join(OUT_DIR, "fabric_linen_normal.png"), optimize=True)
    print("  Generated fabric_linen_albedo/normal.png")


def gen_metal_brushed_normal(size=512):
    n1 = make_noise_sampler(601, 32)
    h = [[0.0] * size for _ in range(size)]
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            streak = 0.5 + 0.5 * math.sin(v * math.tau * 110.0 + n1(u * 0.5, v * 2.0) * 2.2)
            h[y][x] = 0.65 * streak + 0.35 * n1(u, v)
    height_to_normal(h, size, strength=0.65).save(os.path.join(OUT_DIR, "metal_brushed_normal.png"), optimize=True)
    print("  Generated metal_brushed_normal.png")


def gen_crop_leaf(size=512):
    n1 = make_noise_sampler(701, 20)
    h = [[0.0] * size for _ in range(size)]
    alb = Image.new("RGB", (size, size))
    px = alb.load()
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            vein_mid = math.exp(-(((u - 0.5) * 14.0) ** 2))
            vein_side = 0.5 + 0.5 * math.sin((v * 18.0 - abs(u - 0.5) * 20.0) * math.tau)
            mottle = n1(u * 2.0, v * 2.0)
            val = clamp(0.45 * mottle + 0.32 * vein_side + 0.23 * vein_mid, 0.0, 1.0)
            h[y][x] = val
            r = 56.0 + val * 58.0
            g = 112.0 + val * 84.0
            b = 42.0 + val * 36.0
            px[x, y] = (int(clamp(r, 0, 255)), int(clamp(g, 0, 255)), int(clamp(b, 0, 255)))
    alb.save(os.path.join(OUT_DIR, "crop_leaf_albedo.png"), optimize=True)
    height_to_normal(h, size, strength=1.55).save(os.path.join(OUT_DIR, "crop_leaf_normal.png"), optimize=True)
    print("  Generated crop_leaf_albedo/normal.png")


if __name__ == "__main__":
    print(f"[GRIDROAM Phase 4 Textures] Generating shared stylized-realistic PBR library in {OUT_DIR}")
    gen_fur_short_normal(512)
    gen_feather_normal(512)
    gen_fish_scales(512)
    gen_wood_oak(512)
    gen_fabric_linen(512)
    gen_metal_brushed_normal(512)
    gen_crop_leaf(512)
    print("[GRIDROAM Phase 4 Textures] Complete!")
