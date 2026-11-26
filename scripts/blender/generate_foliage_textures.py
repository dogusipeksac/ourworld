#!/usr/bin/env python3
"""
GRIDROAM Botanical Foliage & Bark Texture Generator (PIL + NumPy)
Produces crisp 512x512 RGBA alpha-cutout foliage cluster atlases + tangent-space Normal Maps
for Unity 6 URP Alpha-Clipped double-sided vegetation:
  - foliage_plane_albedo.png / foliage_plane_normal.png    (Istanbul Cinar / Platanus 5-lobed leaves)
  - foliage_pine_albedo.png  / foliage_pine_normal.png     (Aydos Kizilcam / Mediterranean Pine needle sprays)
  - foliage_cypress_albedo.png / foliage_cypress_normal.png (Istanbul Selvi / Cypress feathery fronds)
  - bark_tree_albedo.png     / bark_tree_normal.png        (Natural furrowed tree bark)
"""

import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUT_TEX_DIR = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "OurWorldTheme", "Textures")
PREVIEW_PATH = os.path.join(SCRIPT_DIR, "foliage_textures_preview.png")

os.makedirs(OUT_TEX_DIR, exist_ok=True)


def height_img_to_normal(h_img, strength=2.6):
    """Convert an 'L' mode PIL height image into an RGB tangent-space normal map."""
    w, h = h_img.size
    src = h_img.load()
    out = Image.new("RGB", (w, h), (128, 128, 255))
    dst = out.load()
    inv255 = 1.0 / 255.0
    for y in range(h):
        ym = (y - 1) % h
        yp = (y + 1) % h
        for x in range(w):
            xm = (x - 1) % w
            xp = (x + 1) % w
            gx = (src[xp, y] - src[xm, y]) * inv255
            gy = (src[x, yp] - src[x, ym]) * inv255
            nx = -gx * strength
            ny = gy * strength
            nz = 1.0
            inv_n = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz)
            dst[x, y] = (
                max(0, min(255, int((nx * inv_n * 0.5 + 0.5) * 255.0))),
                max(0, min(255, int((ny * inv_n * 0.5 + 0.5) * 255.0))),
                max(0, min(255, int((nz * inv_n * 0.5 + 0.5) * 255.0))),
            )
    return out


def dilate_rgb_into_transparent(rgba_img, iterations=6):
    """Bleed leaf edge RGB colors into transparent pixels to prevent black mipmap fringes in Unity!"""
    w, h = rgba_img.size
    px = rgba_img.load()
    rgb = [[(px[x, y][0], px[x, y][1], px[x, y][2]) for x in range(w)] for y in range(h)]
    alpha = [[px[x, y][3] for x in range(w)] for y in range(h)]
    valid = [[alpha[y][x] > 24 for x in range(w)] for y in range(h)]

    for _ in range(iterations):
        updates = []
        for y in range(h):
            for x in range(w):
                if valid[y][x]:
                    continue
                r_s = g_s = b_s = cnt = 0
                if y > 0 and valid[y - 1][x]:
                    c = rgb[y - 1][x]; r_s += c[0]; g_s += c[1]; b_s += c[2]; cnt += 1
                if y + 1 < h and valid[y + 1][x]:
                    c = rgb[y + 1][x]; r_s += c[0]; g_s += c[1]; b_s += c[2]; cnt += 1
                if x > 0 and valid[y][x - 1]:
                    c = rgb[y][x - 1]; r_s += c[0]; g_s += c[1]; b_s += c[2]; cnt += 1
                if x + 1 < w and valid[y][x + 1]:
                    c = rgb[y][x + 1]; r_s += c[0]; g_s += c[1]; b_s += c[2]; cnt += 1
                if cnt > 0:
                    updates.append((x, y, (r_s // cnt, g_s // cnt, b_s // cnt)))
        if not updates:
            break
        for x, y, col in updates:
            rgb[y][x] = col
            valid[y][x] = True

    out = Image.new("RGBA", (w, h))
    opx = out.load()
    for y in range(h):
        for x in range(w):
            c = rgb[y][x]
            opx[x, y] = (c[0], c[1], c[2], alpha[y][x])
    return out


def draw_sycamore_leaf(draw_rgb, draw_h, cx, cy, scale, angle_rad, base_rgb, vein_rgb, h_val):
    """Draw a detailed 5-lobed serrated Istanbul Cinar (Platanus) maple-like leaf with central veins."""
    ca, sa = math.cos(angle_rad), math.sin(angle_rad)

    def to_xy(lx, ly):
        return (cx + (lx * ca - ly * sa) * scale, cy + (lx * sa + ly * ca) * scale)

    # 5 lobes with serrated sub-teeth in local leaf space (stem at 0,0; tip at 0,-1.05)
    pts_local = [
        (0.00,  0.08),
        (-0.22, 0.14), (-0.42, 0.08), (-0.54, 0.14), (-0.64, 0.02),
        (-0.56, -0.10), (-0.82, -0.16), (-0.70, -0.26), (-0.88, -0.38),
        (-0.66, -0.44), (-0.46, -0.36),
        (-0.58, -0.58), (-0.42, -0.66), (-0.50, -0.82), (-0.28, -0.76),
        (-0.18, -0.66),
        (-0.14, -0.90), (-0.06, -0.94), (0.00, -1.12), (0.06, -0.94), (0.14, -0.90),
        (0.18, -0.66),
        (0.28, -0.76), (0.50, -0.82), (0.42, -0.66), (0.58, -0.58),
        (0.46, -0.36), (0.66, -0.44), (0.88, -0.38), (0.70, -0.26),
        (0.82, -0.16), (0.56, -0.10), (0.64, 0.02), (0.54, 0.14),
        (0.42, 0.08), (0.22, 0.14),
    ]
    poly = [to_xy(x, y) for (x, y) in pts_local]

    # Subtle dark outline + sunlit leaf blade
    outline = (max(0, base_rgb[0] - 24), max(0, base_rgb[1] - 26), max(0, base_rgb[2] - 18), 255)
    draw_rgb.polygon(poly, fill=(base_rgb[0], base_rgb[1], base_rgb[2], 255), outline=outline)
    draw_h.polygon(poly, fill=int(h_val * 255))

    # Inner lighter lamina highlight toward upper lobes
    inner_poly = [to_xy(x * 0.72, y * 0.72 - 0.10) for (x, y) in pts_local]
    hi_rgb = (min(255, base_rgb[0] + 18), min(255, base_rgb[1] + 22), min(255, base_rgb[2] + 10), 175)
    draw_rgb.polygon(inner_poly, fill=hi_rgb)
    draw_h.polygon(inner_poly, fill=min(255, int((h_val + 0.12) * 255)))

    # 5 radiating botanical leaf veins + stem petiole
    veins = [
        ((0.0, 0.24), (0.0, -0.95)),
        ((0.0, 0.02), (-0.72, -0.32)),
        ((0.0, 0.02), (0.72, -0.32)),
        ((0.0, -0.06), (-0.42, -0.72)),
        ((0.0, -0.06), (0.42, -0.72)),
    ]
    w_vein = max(1, int(scale * 0.036))
    for p0, p1 in veins:
        draw_rgb.line([to_xy(*p0), to_xy(*p1)], fill=(vein_rgb[0], vein_rgb[1], vein_rgb[2], 255), width=w_vein)
        draw_h.line([to_xy(*p0), to_xy(*p1)], fill=250, width=max(1, w_vein))


def generate_plane_foliage(size=512):
    rng = random.Random(101)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    h_img = Image.new("L", (size, size), 0)
    dr = ImageDraw.Draw(img, "RGBA")
    dh = ImageDraw.Draw(h_img)

    # Woody central branching twig radiating from bottom-center
    twigs = [
        ((256, 488), (256, 250), 9),
        ((256, 360), (132, 235), 6),
        ((256, 355), (382, 230), 6),
        ((256, 270), (156, 138), 5),
        ((256, 265), (358, 134), 5),
        ((256, 255), (256, 108), 5),
    ]
    for p0, p1, w in twigs:
        dr.line([p0, p1], fill=(86, 66, 44, 255), width=w)
        dh.line([p0, p1], fill=140, width=w)

    # Layered cluster of 26 overlapping serrated plane-tree leaves (deep shadow base -> sunlit crown!)
    leaves = []
    # Outer ring & shadow under-leaves
    for i in range(14):
        ang = (2.0 * math.pi * i) / 14.0 + rng.uniform(-0.18, 0.18)
        dist = rng.uniform(118, 178)
        lx = 256 + math.cos(ang) * dist
        ly = 252 + math.sin(ang) * dist * 0.96
        sc = rng.uniform(62, 82)
        leaf_ang = ang + math.pi * 0.5 + rng.uniform(-0.35, 0.35)
        col = (rng.randint(48, 72), rng.randint(94, 128), rng.randint(36, 54))
        vein = (min(255, col[0] + 34), min(255, col[1] + 42), min(255, col[2] + 22))
        leaves.append((lx, ly, sc, leaf_ang, col, vein, 0.45 + 0.08 * (i % 3)))

    # Mid & sunlit top leaves
    for i in range(13):
        ang = (2.0 * math.pi * i) / 13.0 + rng.uniform(-0.22, 0.22)
        dist = rng.uniform(22, 118)
        lx = 256 + math.cos(ang) * dist
        ly = 248 + math.sin(ang) * dist * 0.92
        sc = rng.uniform(66, 88)
        leaf_ang = ang + math.pi * 0.5 + rng.uniform(-0.4, 0.4)
        col = (rng.randint(76, 114), rng.randint(136, 174), rng.randint(46, 72))
        vein = (min(255, col[0] + 36), min(255, col[1] + 38), min(255, col[2] + 20))
        leaves.append((lx, ly, sc, leaf_ang, col, vein, 0.68 + 0.07 * (i % 4)))

    for lx, ly, sc, leaf_ang, col, vein, hv in leaves:
        draw_sycamore_leaf(dr, dh, lx, ly, sc, leaf_ang, col, vein, hv)

    img = dilate_rgb_into_transparent(img, iterations=6)
    h_blur = h_img.filter(ImageFilter.GaussianBlur(radius=1.2))
    norm = height_img_to_normal(h_blur, strength=3.4)
    img.save(os.path.join(OUT_TEX_DIR, "foliage_plane_albedo.png"))
    norm.save(os.path.join(OUT_TEX_DIR, "foliage_plane_normal.png"))
    return img


def generate_pine_foliage(size=512):
    """Aydos Mediterranean Pine (Kizilcam / Pinus pinea) radiating evergreen needle rosettes."""
    rng = random.Random(202)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    h_img = Image.new("L", (size, size), 0)
    dr = ImageDraw.Draw(img, "RGBA")
    dh = ImageDraw.Draw(h_img)

    # Reddish-brown woody pine branchlets radiating outward
    branch_tips = [
        (256, 118), (150, 172), (362, 174),
        (114, 272), (398, 276), (168, 362), (344, 360),
        (256, 250), (206, 212), (308, 212), (256, 330),
    ]
    for tx, ty in branch_tips:
        dr.line([(256, 468), (tx, ty)], fill=(96, 62, 38, 255), width=7)
        dh.line([(256, 468), (tx, ty)], fill=130, width=7)

    # Draw dense radiating pine needle bursts around every branchlet tip!
    for idx, (cx, cy) in enumerate(branch_tips):
        is_top = cy < 250
        n_needles = 86 if idx < 7 else 68
        for k in range(n_needles):
            ang = (2.0 * math.pi * k) / n_needles + rng.uniform(-0.14, 0.14)
            r_in = rng.uniform(2, 14)
            r_out = rng.uniform(58, 102)
            x0, y0 = cx + math.cos(ang) * r_in, cy + math.sin(ang) * r_in
            x1, y1 = cx + math.cos(ang) * r_out, cy + math.sin(ang) * r_out
            if is_top:
                col = (rng.randint(46, 84), rng.randint(100, 148), rng.randint(44, 68), 255)
                hv = rng.randint(175, 245)
            else:
                col = (rng.randint(30, 56), rng.randint(72, 112), rng.randint(34, 52), 255)
                hv = rng.randint(120, 185)
            w = rng.choice([3, 3, 4, 5])
            dr.line([(x0, y0), (x1, y1)], fill=col, width=w)
            dh.line([(x0, y0), (x1, y1)], fill=hv, width=w)

        # Tiny subtle pine shoot bud in the center of each rosette
        dr.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=(96, 72, 42, 255))
        dh.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=200)

    img = dilate_rgb_into_transparent(img, iterations=6)
    h_blur = h_img.filter(ImageFilter.GaussianBlur(radius=1.0))
    norm = height_img_to_normal(h_blur, strength=3.6)
    img.save(os.path.join(OUT_TEX_DIR, "foliage_pine_albedo.png"))
    norm.save(os.path.join(OUT_TEX_DIR, "foliage_pine_normal.png"))
    return img


def generate_cypress_foliage(size=512):
    """Istanbul Selvi (Mediterranean Cypress) upward-sweeping feathery scale-leaf fronds."""
    rng = random.Random(303)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    h_img = Image.new("L", (size, size), 0)
    dr = ImageDraw.Draw(img, "RGBA")
    dh = ImageDraw.Draw(h_img)

    # Central tapered evergreen spray spine + upward curved feathery fronds
    dr.line([(256, 495), (256, 54)], fill=(58, 48, 34, 255), width=9)
    dh.line([(256, 495), (256, 54)], fill=135, width=9)

    for layer in range(2):
        count = 46 if layer == 0 else 38
        for i in range(count):
            t = i / float(count - 1)  # 0 bottom .. 1 apex
            base_y = 468 - t * 395
            spread = (1.0 - t * 0.72) * 145.0
            side = -1.0 if i % 2 == 0 else 1.0
            # Upward sweeping plume
            tx = 256 + side * rng.uniform(spread * 0.35, spread)
            ty = base_y - rng.uniform(36, 92)
            # Draw dense feathery scale sub-fronds along each plume
            steps = 9
            for s in range(steps):
                u = s / float(steps - 1)
                px = 256 + (tx - 256) * u
                py = base_y + (ty - base_y) * u
                sub_len = (1.0 - u * 0.55) * rng.uniform(22, 42)
                for fan in (-0.55, -0.22, 0.22, 0.55):
                    ang = math.atan2(ty - base_y, tx - 256) + fan
                    qx = px + math.cos(ang) * sub_len
                    qy = py + math.sin(ang) * sub_len
                    if layer == 0:
                        col = (rng.randint(24, 42), rng.randint(60, 88), rng.randint(36, 52), 255)
                        hv = rng.randint(120, 175)
                    else:
                        col = (rng.randint(38, 66), rng.randint(86, 122), rng.randint(46, 68), 255)
                        hv = rng.randint(175, 245)
                    dr.line([(px, py), (qx, qy)], fill=col, width=4)
                    dh.line([(px, py), (qx, qy)], fill=hv, width=4)

    img = dilate_rgb_into_transparent(img, iterations=6)
    h_blur = h_img.filter(ImageFilter.GaussianBlur(radius=1.0))
    norm = height_img_to_normal(h_blur, strength=3.2)
    img.save(os.path.join(OUT_TEX_DIR, "foliage_cypress_albedo.png"))
    norm.save(os.path.join(OUT_TEX_DIR, "foliage_cypress_normal.png"))
    return img


def generate_bark_texture(size=512):
    """Tileable organic furrowed Mediterranean tree bark albedo + normal map."""
    rng = random.Random(404)
    alb = Image.new("RGB", (size, size))
    h_img = Image.new("L", (size, size))
    apx = alb.load()
    hpx = h_img.load()
    two_pi = 2.0 * math.pi
    inv_s = two_pi / size
    for y in range(size):
        v = y * inv_s
        sin_v3 = 1.8 * math.sin(v * 3.0)
        sin_v11 = 1.4 * math.sin(v * 11.0)
        v5 = v * 5.0
        cos_v7 = 0.10 * math.cos(v * 7.0)
        for x in range(size):
            u = x * inv_s
            f = (
                0.42 * math.sin(u * 11.0 + sin_v3)
                + 0.26 * math.sin(u * 23.0 - 2.4 * math.cos(v5 + u * 2.0))
                + 0.16 * math.sin(u * 41.0 + sin_v11)
                + cos_v7
            )
            hv = max(0.0, min(1.0, 0.5 + 0.45 * f + rng.uniform(-0.07, 0.07)))
            hpx[x, y] = int(hv * 255.0)
            apx[x, y] = (
                int(max(0, min(255, (0.22 + 0.26 * hv) * 255.0))),
                int(max(0, min(255, (0.17 + 0.22 * hv) * 255.0))),
                int(max(0, min(255, (0.12 + 0.16 * hv) * 255.0))),
            )
    alb = alb.filter(ImageFilter.GaussianBlur(radius=0.6))
    norm = height_img_to_normal(h_img.filter(ImageFilter.GaussianBlur(radius=0.6)), strength=4.2)

    alb.save(os.path.join(OUT_TEX_DIR, "bark_tree_albedo.png"))
    norm.save(os.path.join(OUT_TEX_DIR, "bark_tree_normal.png"))
    return alb


def main():
    p_img = generate_plane_foliage()
    pi_img = generate_pine_foliage()
    c_img = generate_cypress_foliage()
    b_img = generate_bark_texture()

    # Compose 2x2 preview sheet over sky blue so alpha cutouts are clearly visible!
    sheet = Image.new("RGB", (1024, 1024), (110, 168, 224))
    sheet.paste(p_img, (0, 0), p_img)
    sheet.paste(pi_img, (512, 0), pi_img)
    sheet.paste(c_img, (0, 512), c_img)
    sheet.paste(b_img, (512, 512))
    sheet.save(PREVIEW_PATH)
    print(f"Generated 8 botanical textures in {OUT_TEX_DIR}")
    print(f"Saved preview sheet: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()
