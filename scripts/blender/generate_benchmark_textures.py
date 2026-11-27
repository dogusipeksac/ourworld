#!/usr/bin/env python3
"""
GRIDROAM Phase 3 Benchmark – Stylized-Realistic PBR Texture Generator
Generates compact, seamless 512x512 albedo & normal maps under
Assets/GRIDROAM/Resources/OurWorldTheme/Textures/ (auto-imported by OurWorldLifeModelImporter.cs):
  - animal_sheep_wool_albedo.png / animal_sheep_wool_normal.png  (Warm Anatolian curly wool fleece)
  - animal_sheep_face_albedo.png / animal_sheep_face_normal.png  (Velvety charcoal-espresso short fur)
  - tractor_paint_normal.png                                     (Stamped painted metal micro-sheen)
  - tractor_tire_albedo.png      / tractor_tire_normal.png       (Vulcanized dusty farm rubber + ribs)
Pure Python + Pillow (no numpy dependency required).
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


def smoothstep(edge0, edge1, x):
    t = clamp((x - edge0) / max(1e-6, edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def tile_dist2(dx, dy, size):
    if dx > size * 0.5:
        dx -= size
    elif dx < -size * 0.5:
        dx += size
    if dy > size * 0.5:
        dy -= size
    elif dy < -size * 0.5:
        dy += size
    return dx * dx + dy * dy


def height_to_normal_image(height_grid, size, strength=2.6):
    """Convert a seamless [0..1] height grid (list of rows) into an OpenGL/Unity tangent normal map."""
    img = Image.new("RGB", (size, size))
    px = img.load()
    for y in range(size):
        ym1 = (y - 1) % size
        yp1 = (y + 1) % size
        row_m = height_grid[ym1]
        row_0 = height_grid[y]
        row_p = height_grid[yp1]
        for x in range(size):
            xm1 = (x - 1) % size
            xp1 = (x + 1) % size
            # Sobel derivative on seamless toroidal grid
            dx = (
                (row_m[xp1] + 2.0 * row_0[xp1] + row_p[xp1])
                - (row_m[xm1] + 2.0 * row_0[xm1] + row_p[xm1])
            ) * strength
            dy = (
                (row_p[xm1] + 2.0 * row_p[x] + row_p[xp1])
                - (row_m[xm1] + 2.0 * row_m[x] + row_m[xp1])
            ) * strength
            nx = -dx
            ny = -dy
            nz = 1.0
            inv_len = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz)
            r = int(clamp((nx * inv_len * 0.5 + 0.5) * 255.0, 0.0, 255.0))
            g = int(clamp((ny * inv_len * 0.5 + 0.5) * 255.0, 0.0, 255.0))
            b = int(clamp((nz * inv_len * 0.5 + 0.5) * 255.0, 0.0, 255.0))
            px[x, y] = (r, g, b)
    return img


def generate_sheep_wool(size=512):
    """Seamless stylized-realistic Anatolian curly fleece tufts using domain-warped multi-scale Worley billows (0 gaps, 0 pinches)."""
    rng = random.Random(31415)

    def make_worley_grid(cells):
        pts = [[(cx + rng.uniform(0.15, 0.85), cy + rng.uniform(0.15, 0.85), rng.uniform(0.86, 1.0))
                for cx in range(cells)] for cy in range(cells)]
        return cells, pts

    g_macro = make_worley_grid(10)
    g_mid   = make_worley_grid(18)
    g_fine  = make_worley_grid(32)

    # Smooth low-freq seamless noise for domain warping + natural cream tint variation
    wn = 16
    wgrid = [[rng.random() for _ in range(wn)] for _ in range(wn)]

    def sample_warp(u, v):
        gx = (u * wn) % wn
        gy = (v * wn) % wn
        x0, y0 = int(gx), int(gy)
        x1, y1 = (x0 + 1) % wn, (y0 + 1) % wn
        fx, fy = gx - x0, gy - y0
        fx = fx * fx * (3.0 - 2.0 * fx)
        fy = fy * fy * (3.0 - 2.0 * fy)
        return (wgrid[y0][x0] * (1 - fx) + wgrid[y0][x1] * fx) * (1 - fy) + (wgrid[y1][x0] * (1 - fx) + wgrid[y1][x1] * fx) * fy

    def eval_worley(u, v, grid):
        cells, pts = grid
        gx = (u * cells) % cells
        gy = (v * cells) % cells
        ix, iy = int(gx), int(gy)
        d1 = 9.0
        d2 = 9.0
        best_amp = 1.0
        for oy in (-1, 0, 1):
            ny = (iy + oy) % cells
            row = pts[ny]
            for ox in (-1, 0, 1):
                nx = (ix + ox) % cells
                px, py, amp = row[nx]
                dx = (px + (ix + ox - nx)) - gx
                dy = (py + (iy + oy - ny)) - gy
                d = math.sqrt(dx * dx + dy * dy)
                if d < d1:
                    d2 = d1
                    d1 = d
                    best_amp = amp
                elif d < d2:
                    d2 = d
        # Smooth plump fleece tuft dome (high in center, soft crease along cell seams, NO center pinch)
        dome = clamp(1.0 - (d1 / 0.78) ** 2, 0.0, 1.0)
        seam = smoothstep(0.0, 0.36, d2 - d1)
        return math.sqrt(dome) * (0.35 + 0.65 * seam) * best_amp

    norm_h = [[0.0] * size for _ in range(size)]
    albedo = Image.new("RGB", (size, size))
    apx = albedo.load()

    for y in range(size):
        v0 = y / size
        for x in range(size):
            u0 = x / size
            # Organic curly swirl warp
            wx = (sample_warp(u0, v0) - 0.5) * 0.055 + (sample_warp(u0 * 2.0 + 0.3, v0 * 2.0 + 0.7) - 0.5) * 0.025
            wy = (sample_warp(u0 + 0.41, v0 + 0.19) - 0.5) * 0.055 + (sample_warp(u0 * 2.0 + 0.8, v0 * 2.0 + 0.1) - 0.5) * 0.025
            u = (u0 + wx) % 1.0
            v = (v0 + wy) % 1.0

            b1 = eval_worley(u, v, g_macro)
            b2 = eval_worley((u + 0.17) % 1.0, (v + 0.31) % 1.0, g_mid)
            b3 = eval_worley((u + 0.53) % 1.0, (v + 0.11) % 1.0, g_fine)
            # Soft woven curl micro-fibers
            fiber = 0.5 + 0.5 * math.sin((u * 48.0 + math.sin(v * math.tau * 8.0) * 1.4) * math.tau)
            h_val = clamp(0.50 * b1 + 0.34 * b2 + 0.12 * b3 + 0.04 * fiber, 0.0, 1.0)
            norm_h[y][x] = h_val

            # Rich warm Anatolian wool: soft cream highlights + cozy warm oat-parchment crevices
            warm_patch = sample_warp(u0 * 0.5, v0 * 0.5)
            t = smoothstep(0.12, 0.90, h_val)
            r = 204.0 + t * (248.0 - 204.0) + (warm_patch - 0.5) * 10.0
            g = 195.0 + t * (244.0 - 195.0) + (warm_patch - 0.5) * 8.0
            b = 179.0 + t * (232.0 - 179.0) + (warm_patch - 0.5) * 4.0
            apx[x, y] = (int(clamp(r, 0, 255)), int(clamp(g, 0, 255)), int(clamp(b, 0, 255)))

    albedo = albedo.filter(ImageFilter.GaussianBlur(radius=0.5))
    normal = height_to_normal_image(norm_h, size, strength=2.3)
    normal = normal.filter(ImageFilter.GaussianBlur(radius=0.5))

    albedo.save(os.path.join(OUT_DIR, "animal_sheep_wool_albedo.png"), optimize=True)
    normal.save(os.path.join(OUT_DIR, "animal_sheep_wool_normal.png"), optimize=True)
    print("  Generated animal_sheep_wool_albedo/normal.png (seamless Worley curly fleece)")


def generate_sheep_face(size=512):
    """Velvety warm espresso-charcoal short ovine hair (never flat black; catches warm highlights)."""
    rng = random.Random(27182)
    # Multi-frequency seamless value noise + fine combed short fur streaks
    grid_n = 32
    coarse = [[rng.random() for _ in range(grid_n)] for _ in range(grid_n)]

    def sample_coarse(u, v):
        gx = (u * grid_n) % grid_n
        gy = (v * grid_n) % grid_n
        x0 = int(gx)
        y0 = int(gy)
        x1 = (x0 + 1) % grid_n
        y1 = (y0 + 1) % grid_n
        fx = gx - x0
        fy = gy - y0
        fx = fx * fx * (3.0 - 2.0 * fx)
        fy = fy * fy * (3.0 - 2.0 * fy)
        c00 = coarse[y0][x0]
        c10 = coarse[y0][x1]
        c01 = coarse[y1][x0]
        c11 = coarse[y1][x1]
        return (c00 * (1 - fx) + c10 * fx) * (1 - fy) + (c01 * (1 - fx) + c11 * fx) * fy

    h = [[0.0] * size for _ in range(size)]
    albedo = Image.new("RGB", (size, size))
    apx = albedo.load()

    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            n1 = sample_coarse(u, v)
            n2 = sample_coarse(u * 2.0, v * 2.0)
            # Fine silky directional fur strands along V with gentle sway
            sway = math.sin(v * math.tau * 4.0 + n1 * 3.0) * 0.04
            hair = 0.5 + 0.5 * math.sin((u + sway) * math.tau * 56.0 + n2 * 4.0)
            hair2 = 0.5 + 0.5 * math.sin((u - sway * 0.6) * math.tau * 92.0 + v * math.tau * 6.0)
            val = clamp(0.45 * n1 + 0.25 * n2 + 0.20 * hair + 0.10 * hair2, 0.0, 1.0)
            h[y][x] = val
            # Warm velvety dark umber/charcoal (readable facial planes!)
            r = 58.0 + val * 36.0
            g = 51.0 + val * 33.0
            b = 48.0 + val * 30.0
            apx[x, y] = (int(clamp(r, 0, 255)), int(clamp(g, 0, 255)), int(clamp(b, 0, 255)))

    normal = height_to_normal_image(h, size, strength=1.45)
    albedo.save(os.path.join(OUT_DIR, "animal_sheep_face_albedo.png"), optimize=True)
    normal.save(os.path.join(OUT_DIR, "animal_sheep_face_normal.png"), optimize=True)
    print("  Generated animal_sheep_face_albedo/normal.png")


def generate_tractor_paint_normal(size=512):
    """Subtle stamped painted agricultural sheet-metal micro-grain + soft panel edge wear."""
    rng = random.Random(4242)
    grid_n = 48
    g = [[rng.random() for _ in range(grid_n)] for _ in range(grid_n)]

    def sample(u, v):
        gx = (u * grid_n) % grid_n
        gy = (v * grid_n) % grid_n
        x0, y0 = int(gx), int(gy)
        x1, y1 = (x0 + 1) % grid_n, (y0 + 1) % grid_n
        fx, fy = gx - x0, gy - y0
        fx = fx * fx * (3.0 - 2.0 * fx)
        fy = fy * fy * (3.0 - 2.0 * fy)
        return (g[y0][x0] * (1 - fx) + g[y0][x1] * fx) * (1 - fy) + (g[y1][x0] * (1 - fx) + g[y1][x1] * fx) * fy

    h = [[0.0] * size for _ in range(size)]
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            orange_peel = sample(u * 2.0, v * 2.0) * 0.55 + sample(u * 5.0, v * 5.0) * 0.30
            brushed = 0.15 * (0.5 + 0.5 * math.sin(u * math.tau * 36.0 + v * math.tau * 2.0))
            h[y][x] = orange_peel + brushed

    normal = height_to_normal_image(h, size, strength=0.65)
    normal.save(os.path.join(OUT_DIR, "tractor_paint_normal.png"), optimize=True)
    print("  Generated tractor_paint_normal.png")


def generate_tractor_tire(size=512):
    """Vulcanized matte dark agricultural tire rubber with subtle dusty farm soil recesses & rubber grain."""
    rng = random.Random(9001)
    grid_n = 32
    g = [[rng.random() for _ in range(grid_n)] for _ in range(grid_n)]

    def sample(u, v):
        gx = (u * grid_n) % grid_n
        gy = (v * grid_n) % grid_n
        x0, y0 = int(gx), int(gy)
        x1, y1 = (x0 + 1) % grid_n, (y0 + 1) % grid_n
        fx, fy = gx - x0, gy - y0
        fx = fx * fx * (3.0 - 2.0 * fx)
        fy = fy * fy * (3.0 - 2.0 * fy)
        return (g[y0][x0] * (1 - fx) + g[y0][x1] * fx) * (1 - fy) + (g[y1][x0] * (1 - fx) + g[y1][x1] * fx) * fy

    h = [[0.0] * size for _ in range(size)]
    albedo = Image.new("RGB", (size, size))
    apx = albedo.load()

    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            n1 = sample(u, v)
            n2 = sample(u * 3.0, v * 3.0)
            micro = rng.uniform(-0.04, 0.04)
            # Molded rubber rib lines + vulcanized pore grain
            rib = 0.5 + 0.5 * math.sin(v * math.tau * 24.0)
            val = clamp(0.45 * n1 + 0.30 * n2 + 0.18 * rib + 0.07 + micro, 0.0, 1.0)
            h[y][x] = val
            # Charcoal rubber with warm dry field-dust tint in subtle patches
            dust = smoothstep(0.62, 0.92, n1) * 0.42
            r = 34.0 + val * 18.0 + dust * 46.0
            g_col = 34.0 + val * 17.0 + dust * 38.0
            b = 36.0 + val * 16.0 + dust * 26.0
            apx[x, y] = (int(clamp(r, 0, 255)), int(clamp(g_col, 0, 255)), int(clamp(b, 0, 255)))

    normal = height_to_normal_image(h, size, strength=1.7)
    albedo.save(os.path.join(OUT_DIR, "tractor_tire_albedo.png"), optimize=True)
    normal.save(os.path.join(OUT_DIR, "tractor_tire_normal.png"), optimize=True)
    print("  Generated tractor_tire_albedo/normal.png")


if __name__ == "__main__":
    print(f"[GRIDROAM Textures] Writing stylized-realistic Phase 3 PBR textures to {OUT_DIR}")
    generate_sheep_wool(512)
    generate_sheep_face(512)
    generate_tractor_paint_normal(512)
    generate_tractor_tire(512)
    print("[GRIDROAM Textures] Done.")
