#!/usr/bin/env python3
"""
Our World / Bizim Dünyamız — Next-Gen Store Marketing Poster, Header & Search Studio
Generates ultra-vibrant, Search-optimized store screenshots & headers across ALL mobile & tablet sizes
in BOTH Turkish (tr) and English (en-US):
  - Google Play Phone (1920 x 1080)
  - Apple App Store 6.7" iPhone (2796 x 1290)
  - Apple App Store 6.5" iPhone (2688 x 1242)
  - Apple App Store 12.9"/13" iPad Pro (2732 x 2048)
  - Google Play 7" Tablet (1920 x 1200)
  - Google Play 10" Tablet (2560 x 1600)
  - Google Play Feature Graphic / Store Header (1024 x 500 RGB — TR & EN + synced to StoreAssets/)

Usage:
  python3 release/store_screenshots/generate_store_posters.py --lang tr
  python3 release/store_screenshots/generate_store_posters.py --lang en
"""

import math
import os
import random
import shutil
import sys
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
RAW_DIR = os.path.join(SCRIPT_DIR, "raw")
ICON_PATH = os.path.join(PROJECT_DIR, "StoreAssets", "AppStore_Icon_1024.png")
OUT_FEATURE = os.path.join(SCRIPT_DIR, "google_play_feature")
STORE_FEATURE_DST = os.path.join(PROJECT_DIR, "StoreAssets", "GooglePlay_FeatureGraphic_1024x500.png")

TARGETS = {
    "google_play_phone":     {"w": 1920, "h": 1080, "is_tablet": False},
    "app_store_6.7":         {"w": 2796, "h": 1290, "is_tablet": False},
    "app_store_6.5":         {"w": 2688, "h": 1242, "is_tablet": False},
    "app_store_ipad_12.9":   {"w": 2732, "h": 2048, "is_tablet": True},
    "google_play_tablet_7":  {"w": 1920, "h": 1200, "is_tablet": True},
    "google_play_tablet_10": {"w": 2560, "h": 1600, "is_tablet": True},
}

FONT_BOLD_CANDIDATES = [
    "/System/Library/Fonts/Supplemental/Arial Black.ttf",
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    "/System/Library/Fonts/Helvetica.ttc",
    "/System/Library/Fonts/SFNS.ttf",
]
FONT_MED_CANDIDATES = [
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    "/System/Library/Fonts/Helvetica.ttc",
    "/System/Library/Fonts/SFNS.ttf",
]
FONT_BOLD_PATH = next((p for p in FONT_BOLD_CANDIDATES if os.path.exists(p)), "")
FONT_MED_PATH = next((p for p in FONT_MED_CANDIDATES if os.path.exists(p)), FONT_BOLD_PATH)

_FONT_CACHE = {}
_RAW_CACHE = {}


def get_font(path, size):
    key = (path, max(10, int(size)))
    if key not in _FONT_CACHE:
        try:
            _FONT_CACHE[key] = ImageFont.truetype(path, max(10, int(size)))
        except Exception:
            _FONT_CACHE[key] = ImageFont.load_default()
    return _FONT_CACHE[key]


def fit_font(path, text, size, max_w, min_size=14):
    s = int(size)
    f = get_font(path, s)
    while (f.getbbox(text)[2] - f.getbbox(text)[0]) > max_w and s > min_size:
        s -= 2
        f = get_font(path, s)
    return f


def load_and_grade_screenshot(raw_path, crop_bottom_px=0, warm_night_boost=False):
    """Loads a raw gameplay capture, optionally trims desktop bottom key-hint bar,
    and applies vibrant sunlit OLED studio color grading so shots pop in Store & Search."""
    cache_key = (raw_path, crop_bottom_px, warm_night_boost)
    if cache_key in _RAW_CACHE:
        return _RAW_CACHE[cache_key]

    if not os.path.exists(raw_path):
        raise FileNotFoundError(f"Missing raw screenshot: {raw_path}")

    im = Image.open(raw_path).convert("RGB")
    w, h = im.size
    if w == 2622 and h == 1206:
        im = im.crop((175, 0, w, h))
    elif w == 2424 and h == 1080:
        im = im.crop((145, 138, 2424, 1080))

    if crop_bottom_px > 0 and im.size[1] > crop_bottom_px + 200:
        cw, ch = im.size
        im = im.crop((0, 0, cw, ch - crop_bottom_px))

    if warm_night_boost:
        # Lift dark night asphalt/buildings cleanly while saturating warm evening amber lights
        im = ImageEnhance.Brightness(im).enhance(1.38)
        im = ImageEnhance.Contrast(im).enhance(1.16)
        im = ImageEnhance.Color(im).enhance(1.36)
        im = ImageEnhance.Sharpness(im).enhance(1.20)
        rgba = im.convert("RGBA")
        # Add subtle warm golden streetlamp atmospheric glow halos along the avenue lamps
        glow = Image.new("RGBA", rgba.size, (0, 0, 0, 0))
        gdraw = ImageDraw.Draw(glow)
        gw, gh = rgba.size
        lamp_spots = [
            (0.41, 0.25, 0.08),
            (0.55, 0.35, 0.07),
            (0.64, 0.43, 0.06),
            (0.89, 0.45, 0.07),
            (0.52, 0.56, 0.14),
            (0.28, 0.64, 0.16),
        ]
        for lx, ly, lr in lamp_spots:
            cx, cy, r = int(gw * lx), int(gh * ly), int(gw * lr)
            gdraw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 198, 92, 68))
        glow = glow.filter(ImageFilter.GaussianBlur(max(12, gw // 24)))
        rgba = Image.alpha_composite(rgba, glow)
    else:
        # Vibrant, crisp sunlit OLED store color grade
        im = ImageEnhance.Brightness(im).enhance(1.10)
        im = ImageEnhance.Contrast(im).enhance(1.14)
        im = ImageEnhance.Color(im).enhance(1.30)
        im = ImageEnhance.Sharpness(im).enhance(1.22)
        rgba = im.convert("RGBA")
        # Gentle warm top sunlight glaze
        sun = Image.new("RGBA", rgba.size, (0, 0, 0, 0))
        sdraw = ImageDraw.Draw(sun)
        sw, sh = rgba.size
        sdraw.ellipse([int(sw * 0.55), -int(sh * 0.35), int(sw * 1.25), int(sh * 0.55)], fill=(255, 224, 150, 26))
        sun = sun.filter(ImageFilter.GaussianBlur(max(16, sw // 18)))
        rgba = Image.alpha_composite(rgba, sun)

    _RAW_CACHE[cache_key] = rgba
    return rgba


def load_rounded_icon(size_px):
    if not os.path.exists(ICON_PATH):
        return None
    ic = Image.open(ICON_PATH).convert("RGBA").resize((size_px, size_px), Image.Resampling.LANCZOS)
    mask = Image.new("L", (size_px, size_px), 0)
    mdraw = ImageDraw.Draw(mask)
    r = int(size_px * 0.225)
    mdraw.rounded_rectangle([0, 0, size_px, size_px], radius=r, fill=255)
    out = Image.new("RGBA", (size_px, size_px), (0, 0, 0, 0))
    out.paste(ic, (0, 0), mask)
    # Crisp luminous rim
    odraw = ImageDraw.Draw(out)
    odraw.rounded_rectangle([1, 1, size_px - 2, size_px - 2], radius=r, outline=(255, 255, 255, 145), width=max(2, size_px // 42))
    return out


def create_vibrant_backdrop(width, height, c_tl, c_br, glow_a, glow_b, bg_shot=None, seed=42):
    """Builds a rich, vibrant multi-stop diagonal studio gradient with atmospheric blurred gameplay depth,
    luminous twin glows, sunlit volumetric rays, and bokeh sparkles."""
    # Smooth diagonal 2D gradient via low-res bilinear upscale (super fast & silky smooth!)
    gw, gh = 64, 36
    grad = Image.new("RGBA", (gw, gh))
    px = grad.load()
    for y in range(gh):
        ty = y / float(gh - 1)
        for x in range(gw):
            tx = x / float(gw - 1)
            t = min(1.0, max(0.0, tx * 0.45 + ty * 0.55))
            r = int(c_tl[0] * (1 - t) + c_br[0] * t)
            g = int(c_tl[1] * (1 - t) + c_br[1] * t)
            b = int(c_tl[2] * (1 - t) + c_br[2] * t)
            px[x, y] = (r, g, b, 255)
    canvas = grad.resize((width, height), Image.Resampling.BICUBIC)

    # Subtle blurred atmospheric world image blended underneath glows (eliminates dead flat background!)
    if bg_shot is not None:
        bw, bh = bg_shot.size
        scale = max(width / float(bw), height / float(bh))
        nw, nh = int(bw * scale), int(bh * scale)
        bg = bg_shot.resize((nw, nh), Image.Resampling.BILINEAR)
        ox, oy = (nw - width) // 2, (nh - height) // 2
        bg = bg.crop((ox, oy, ox + width, oy + height))
        bg = bg.filter(ImageFilter.GaussianBlur(max(18, width // 55)))
        bg_alpha = Image.new("L", (width, height), 58)
        bg.putalpha(bg_alpha)
        canvas = Image.alpha_composite(canvas, bg)

    # Twin luminous radiant glows (inspired by the App Icon's cyan + warm gold energy rings!)
    glow = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)

    r1 = int(width * 0.42)
    gdraw.ellipse([int(width * 0.14) - r1, int(height * 0.22) - r1, int(width * 0.14) + r1, int(height * 0.22) + r1], fill=glow_a)

    r2 = int(width * 0.44)
    gdraw.ellipse([int(width * 0.84) - r2, int(height * 0.72) - r2, int(width * 0.84) + r2, int(height * 0.72) + r2], fill=glow_b)

    r3 = int(width * 0.36)
    center_glow = (
        min(255, (glow_a[0] + glow_b[0]) // 2 + 20),
        min(255, (glow_a[1] + glow_b[1]) // 2 + 20),
        min(255, (glow_a[2] + glow_b[2]) // 2 + 20),
        75,
    )
    gdraw.ellipse([width // 2 - r3, int(height * 0.56) - r3, width // 2 + r3, int(height * 0.56) + r3], fill=center_glow)

    glow = glow.filter(ImageFilter.GaussianBlur(max(24, width // 14)))
    canvas = Image.alpha_composite(canvas, glow)

    # Diagonal sunlit light beams + glowing bokeh orbs
    fx = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    fdraw = ImageDraw.Draw(fx)
    beam_w = int(width * 0.09)
    for frac, alpha in [(-0.08, 18), (0.28, 14), (0.68, 16)]:
        x0 = int(width * frac)
        pts = [
            (x0, 0),
            (x0 + beam_w, 0),
            (x0 - int(height * 0.35) + beam_w, height),
            (x0 - int(height * 0.35), height),
        ]
        fdraw.polygon(pts, fill=(255, 255, 255, alpha))

    # Soft decorative curved luminous accent arc (echoes the app icon's iconic orbital rings!)
    rng = random.Random(seed)
    for _ in range(28):
        px_x = rng.randint(int(width * 0.02), int(width * 0.98))
        px_y = rng.randint(int(height * 0.03), int(height * 0.96))
        pr = rng.randint(max(3, int(width * 0.0025)), max(8, int(width * 0.009)))
        col = glow_a[:3] if rng.random() < 0.55 else glow_b[:3]
        pa = rng.randint(45, 130)
        fdraw.ellipse([px_x - pr, px_y - pr, px_x + pr, px_y + pr], fill=col + (pa,))
        # Tiny bright core sparkle
        cr = max(1, pr // 3)
        fdraw.ellipse([px_x - cr, px_y - cr, px_x + cr, px_y + cr], fill=(255, 255, 255, min(220, pa + 70)))

    canvas = Image.alpha_composite(canvas, fx)

    # Gentle edge contrast vignette to keep the center & header razor-sharp
    vig = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    vdraw = ImageDraw.Draw(vig)
    vdraw.rectangle([0, 0, width, int(height * 0.24)], fill=(6, 12, 26, 72))
    vig = vig.filter(ImageFilter.GaussianBlur(max(14, height // 18)))
    return Image.alpha_composite(canvas, vig)


def render_floating_screen(content_img, target_w, target_h, accent_rgb, scale=1.0):
    """Renders a sleek, crisp modern floating display card with luminous rim glow & soft realistic drop shadow."""
    bezel = max(7, int(10 * scale))
    corner = max(22, int(34 * scale))
    pad = max(26, int(44 * scale))
    fw = target_w + bezel * 2
    fh = target_h + bezel * 2

    frame = Image.new("RGBA", (fw + pad * 2, fh + pad * 2), (0, 0, 0, 0))

    # Soft multi-pass ambient shadow + outer color glow
    shadow_layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(shadow_layer)
    sdraw.rounded_rectangle(
        [pad - 4, pad + int(12 * scale), pad + fw + 4, pad + fh + int(18 * scale)],
        radius=corner + 6,
        fill=(4, 8, 18, 175),
    )
    shadow_layer = shadow_layer.filter(ImageFilter.GaussianBlur(max(10, int(18 * scale))))
    frame = Image.alpha_composite(frame, shadow_layer)

    glow_layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow_layer)
    gdraw.rounded_rectangle(
        [pad - 3, pad - 3, pad + fw + 3, pad + fh + 3],
        radius=corner + 3,
        outline=accent_rgb + (155,),
        width=max(4, int(7 * scale)),
    )
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(max(6, int(10 * scale))))
    frame = Image.alpha_composite(frame, glow_layer)

    fdraw = ImageDraw.Draw(frame)
    # Sleek anodized midnight frame + bright specular inner rim
    fdraw.rounded_rectangle(
        [pad, pad, pad + fw, pad + fh],
        radius=corner,
        fill=(10, 16, 28, 255),
        outline=accent_rgb + (245,),
        width=max(2, int(3.5 * scale)),
    )
    fdraw.rounded_rectangle(
        [pad + 2, pad + 2, pad + fw - 2, pad + fh - 2],
        radius=max(6, corner - 2),
        outline=(255, 255, 255, 95),
        width=max(1, int(1.6 * scale)),
    )

    # Rounded viewport content
    mask = Image.new("L", (target_w, target_h), 0)
    mdraw = ImageDraw.Draw(mask)
    mdraw.rounded_rectangle([0, 0, target_w, target_h], radius=max(10, corner - bezel), fill=255)
    resized = content_img.resize((target_w, target_h), Image.Resampling.LANCZOS)
    frame.paste(resized, (pad + bezel, pad + bezel), mask)

    # Subtle diagonal glass highlight across upper-left corner
    gloss = Image.new("RGBA", (target_w, target_h), (0, 0, 0, 0))
    gl_draw = ImageDraw.Draw(gloss)
    gl_draw.polygon([(0, 0), (int(target_w * 0.34), 0), (0, int(target_h * 0.28))], fill=(255, 255, 255, 16))
    frame.paste(gloss, (pad + bezel, pad + bezel), gloss)

    return frame


def ui_scale(width, height, is_tablet=False):
    s = min(width / 2350.0, height / 1080.0) * 0.84
    return s * (1.16 if is_tablet else 1.0)


def draw_text_with_glow(draw, x, y, text, font, fill, shadow_col=(4, 10, 22, 230), sh_off=4):
    # Clean crisp drop shadow + crisp foreground for instant readability at tiny Search sizes
    for ox, oy in [(sh_off, sh_off), (0, sh_off), (sh_off // 2, sh_off)]:
        draw.text((x + ox, y + oy), text, font=font, fill=shadow_col)
    draw.text((x, y), text, font=font, fill=fill)


def draw_modern_header(canvas, width, height, spec, is_tablet=False, show_icon=False):
    """High-impact, Search-legible poster header with luminous badge pill, huge bold headline,
    vibrant accent color highlight, and crisp subtitle."""
    draw = ImageDraw.Draw(canvas)
    s = ui_scale(width, height, is_tablet)
    accent = spec["accent_rgb"]
    accent2 = spec["accent2_rgb"]
    max_w = width * (0.88 if not show_icon else 0.78)

    y = int(height * 0.028)

    # Top glowing pill badge (auto-sized dynamically so text NEVER overflows!)
    f_badge = fit_font(FONT_MED_PATH, spec["badge"], int(27 * s), max_w * 0.90, 14)
    bb = f_badge.getbbox(spec["badge"])
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    pad_x = int(34 * s)
    bw, bh = tw + pad_x * 2, th + int(20 * s)
    bx = (width - bw) // 2
    draw.rounded_rectangle(
        [bx, y, bx + bw, y + bh],
        radius=bh // 2,
        fill=(10, 20, 42, 225),
        outline=accent + (245,),
        width=max(2, int(3 * s)),
    )
    dot_r = max(4, int(5.5 * s))
    draw.ellipse([bx + int(16 * s) - dot_r, y + bh // 2 - dot_r, bx + int(16 * s) + dot_r, y + bh // 2 + dot_r], fill=accent2 + (255,))
    draw.ellipse([bx + bw - int(16 * s) - dot_r, y + bh // 2 - dot_r, bx + bw - int(16 * s) + dot_r, y + bh // 2 + dot_r], fill=accent2 + (255,))
    draw.text((bx + (bw - tw) // 2 - bb[0], y + (bh - th) // 2 - bb[1]), spec["badge"], font=f_badge, fill=(242, 250, 255, 255))
    y += bh + int(16 * s)

    # Giant Search-stopping Headline (split highlight word in golden/accent tint!)
    title = spec["title"]
    f_title = fit_font(FONT_BOLD_PATH, title, int(92 * s), max_w, 30)
    tbb = f_title.getbbox(title)
    title_w, title_h = tbb[2] - tbb[0], tbb[3] - tbb[1]

    icon_sz = int(title_h * 1.15) if show_icon else 0
    icon_gap = int(20 * s) if show_icon else 0
    total_row_w = title_w + (icon_sz + icon_gap if show_icon else 0)
    start_x = (width - total_row_w) // 2

    if show_icon:
        ic = load_rounded_icon(icon_sz)
        if ic is not None:
            canvas.paste(ic, (start_x, y - int(title_h * 0.08)), ic)
            start_x += icon_sz + icon_gap

    tx = start_x - tbb[0]
    ty = y - tbb[1]
    highlight = spec.get("highlight", "")
    sh = max(3, int(5 * s))
    if highlight and highlight in title:
        before, after = title.split(highlight, 1)
        cur_x = tx
        if before:
            draw_text_with_glow(draw, cur_x, ty, before, f_title, (255, 255, 255, 255), sh_off=sh)
            cur_x += int(draw.textlength(before, font=f_title))
        draw_text_with_glow(draw, cur_x, ty, highlight, f_title, accent2 + (255,), sh_off=sh)
        cur_x += int(draw.textlength(highlight, font=f_title))
        if after:
            draw_text_with_glow(draw, cur_x, ty, after, f_title, (255, 255, 255, 255), sh_off=sh)
    else:
        draw_text_with_glow(draw, tx, ty, title, f_title, (255, 255, 255, 255), sh_off=sh)

    y += title_h + int(14 * s)

    # Crisp legible subtitle
    sub = spec["sub"]
    f_sub = fit_font(FONT_MED_PATH, sub, int(36 * s), width * 0.88, 16)
    sbb = f_sub.getbbox(sub)
    sw, sh_h = sbb[2] - sbb[0], sbb[3] - sbb[1]
    sx = (width - sw) // 2 - sbb[0]
    draw_text_with_glow(draw, sx, y - sbb[1], sub, f_sub, (226, 244, 255, 255), shadow_col=(4, 10, 22, 210), sh_off=max(2, int(3 * s)))
    return y + sh_h + int(16 * s)


def draw_footer_chips(canvas, width, height, chips, accent_rgb, accent2_rgb, is_tablet=False):
    """Draws 3 crisp, modern pill feature chips at the bottom instead of one long plain bar."""
    draw = ImageDraw.Draw(canvas)
    s = ui_scale(width, height, is_tablet)
    f_chip = get_font(FONT_MED_PATH, int(25 * s))
    gap = int(18 * s)
    pad_x = int(28 * s)
    pad_y = int(12 * s)

    chip_dims = []
    total_w = 0
    for text in chips:
        bb = f_chip.getbbox(text)
        cw = (bb[2] - bb[0]) + pad_x * 2
        ch = (bb[3] - bb[1]) + pad_y * 2
        chip_dims.append((text, bb, cw, ch))
        total_w += cw
    total_w += gap * max(0, len(chips) - 1)

    # Scale font slightly down if needed on narrow aspects
    if total_w > width * 0.90:
        scale_fix = (width * 0.90) / float(total_w)
        f_chip = get_font(FONT_MED_PATH, max(13, int(25 * s * scale_fix)))
        chip_dims = []
        total_w = 0
        for text in chips:
            bb = f_chip.getbbox(text)
            cw = (bb[2] - bb[0]) + int(pad_x * scale_fix) * 2
            ch = (bb[3] - bb[1]) + int(pad_y * scale_fix) * 2
            chip_dims.append((text, bb, cw, ch))
            total_w += cw
        total_w += gap * max(0, len(chips) - 1)

    max_h = max(c[3] for c in chip_dims)
    by = height - max_h - int(height * 0.022)
    cx = (width - total_w) // 2

    for idx, (text, bb, cw, ch) in enumerate(chip_dims):
        rim = accent_rgb if idx % 2 == 0 else accent2_rgb
        draw.rounded_rectangle(
            [cx, by, cx + cw, by + ch],
            radius=ch // 2,
            fill=(10, 18, 36, 228),
            outline=rim + (235,),
            width=max(2, int(2.6 * s)),
        )
        tw, th = bb[2] - bb[0], bb[3] - bb[1]
        draw.text((cx + (cw - tw) // 2 - bb[0], by + (ch - th) // 2 - bb[1]), text, font=f_chip, fill=(244, 250, 255, 255))
        cx += cw + gap

    return by - int(14 * s)


def _frame_extra(s):
    return (max(7, int(10 * s)) + max(26, int(44 * s))) * 2


def _fit_box(raw, max_w, max_h, s):
    ex = _frame_extra(s)
    ar = raw.size[0] / float(raw.size[1])
    h = max_h - ex
    w = int(h * ar)
    if w > max_w - ex:
        w = max_w - ex
        h = int(w / ar)
    return max(200, w), max(112, h)


def compose_single(canvas, width, height, top_y, bot_y, raw_img, accent_rgb, is_tablet=False):
    s = ui_scale(width, height, is_tablet)
    avail_h = bot_y - top_y
    w, h = _fit_box(raw_img, int(width * (0.90 if is_tablet else 0.86)), int(avail_h * 1.04), s)
    dev = render_floating_screen(raw_img, w, h, accent_rgb, s)
    canvas.paste(dev, ((width - dev.size[0]) // 2, top_y + (avail_h - dev.size[1]) // 2), dev)


def compose_dual(canvas, width, height, top_y, bot_y, front_img, back_img, accent_rgb, accent2_rgb, is_tablet=False):
    s = ui_scale(width, height, is_tablet)
    avail_h = bot_y - top_y

    fw, fh = _fit_box(front_img, int(width * 0.64), int(avail_h * 1.02), s)
    bw, bh = int(fw * 0.86), int(fh * 0.86)
    dev_f = render_floating_screen(front_img, fw, fh, accent_rgb, s)
    dev_b = render_floating_screen(back_img, bw, bh, accent2_rgb, s * 0.88)

    tilted_b = dev_b.rotate(-4.5, resample=Image.Resampling.BICUBIC, expand=True)
    bx = width - tilted_b.size[0] - int(width * 0.022)
    by = top_y + (avail_h - tilted_b.size[1]) // 2 + int(10 * s)

    fx = int(width * 0.036)
    fy = top_y + (avail_h - dev_f.size[1]) // 2 - int(4 * s)

    canvas.paste(tilted_b, (bx, by), tilted_b)
    canvas.paste(dev_f, (fx, fy), dev_f)


def build_poster(spec, width, height, is_tablet=False):
    if spec["layout"] == "dual":
        front_img = load_and_grade_screenshot(spec["front_raw"], spec.get("front_crop_bot", 0), spec.get("front_night", False))
        back_img = load_and_grade_screenshot(spec["back_raw"], spec.get("back_crop_bot", 0), spec.get("back_night", False))
        bg_ref = front_img
    else:
        front_img = load_and_grade_screenshot(spec["raw"], spec.get("crop_bot", 0), spec.get("night", False))
        back_img = None
        bg_ref = front_img

    canvas = create_vibrant_backdrop(
        width, height,
        spec["c_tl"], spec["c_br"],
        spec["glow_a"], spec["glow_b"],
        bg_shot=bg_ref,
        seed=spec["seed"],
    )
    top_y = draw_modern_header(canvas, width, height, spec, is_tablet=is_tablet, show_icon=spec.get("show_icon", False))
    bot_y = draw_footer_chips(canvas, width, height, spec["chips"], spec["accent_rgb"], spec["accent2_rgb"], is_tablet=is_tablet)

    if spec["layout"] == "dual":
        compose_dual(canvas, width, height, top_y, bot_y, front_img, back_img, spec["accent_rgb"], spec["accent2_rgb"], is_tablet)
    else:
        compose_single(canvas, width, height, top_y, bot_y, front_img, spec["accent_rgb"], is_tablet)

    return canvas


def build_feature_graphic(lang="tr"):
    """Generates a razor-sharp, luminous 1024x500 Google Play Store Header / Feature Graphic
    with zero text overflow, matching the App Icon palette and showcasing a 3-shot dynamic action collage."""
    os.makedirs(OUT_FEATURE, exist_ok=True)
    w, h = 1024, 500

    hero_drive = load_and_grade_screenshot(os.path.join(RAW_DIR, "shot2_selman_driving.png"))
    hero_foot = load_and_grade_screenshot(os.path.join(RAW_DIR, "live_football_3v3.png"), crop_bottom_px=42)
    hero_tennis = load_and_grade_screenshot(os.path.join(RAW_DIR, "live_tennis_1v1.png"), crop_bottom_px=42)

    fg = create_vibrant_backdrop(
        w, h,
        c_tl=(14, 34, 88),
        c_br=(8, 22, 54),
        glow_a=(36, 224, 212, 130),
        glow_b=(255, 184, 52, 128),
        bg_shot=hero_drive,
        seed=777,
    )

    # Darken left half softly so left-aligned header typography has 100% WCAG AAA contrast
    scrim = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sc_px = scrim.load()
    for x in range(w):
        t = max(0.0, min(1.0, 1.0 - (x / float(w * 0.62))))
        a = int(150 * (t * t))
        for y in range(h):
            sc_px[x, y] = (6, 14, 34, a)
    fg = Image.alpha_composite(fg, scrim)

    # Right side: Dynamic 3-shot fanned action showcase!
    card_back_top = render_floating_screen(hero_foot, 255, 148, (66, 236, 152), 0.48).rotate(-6, resample=Image.Resampling.BICUBIC, expand=True)
    card_back_bot = render_floating_screen(hero_tennis, 255, 148, (255, 186, 62), 0.48).rotate(6, resample=Image.Resampling.BICUBIC, expand=True)
    card_front = render_floating_screen(hero_drive, 378, 218, (54, 228, 214), 0.64)

    fg.paste(card_back_top, (w - card_back_top.size[0] - 12, 16), card_back_top)
    fg.paste(card_back_bot, (w - card_back_bot.size[0] - 14, h - card_back_bot.size[1] - 14), card_back_bot)
    fg.paste(card_front, (w - card_front.size[0] - 88, (h - card_front.size[1]) // 2 + 4), card_front)

    # Left side: App Icon + auto-sized Badge + Giant Brand Title + 3 Feature Chips (ZERO overflow!)
    draw = ImageDraw.Draw(fg)
    left_x = 42

    ic = load_rounded_icon(88)
    if ic is not None:
        fg.paste(ic, (left_x, 42), ic)

    is_en = lang == "en"
    badge_txt = "LIVING 3D OPEN WORLD" if is_en else "YAŞAYAN 3D AÇIK DÜNYA"
    f_badge = fit_font(FONT_MED_PATH, badge_txt, 17, 340, 13)
    bbb = f_badge.getbbox(badge_txt)
    btw, bth = bbb[2] - bbb[0], bbb[3] - bbb[1]
    bx = left_x + 104
    by = 54
    bw, bh = btw + 36, bth + 18
    draw.rounded_rectangle([bx, by, bx + bw, by + bh], radius=bh // 2, fill=(10, 24, 52, 235), outline=(54, 228, 214, 245), width=2)
    draw.text((bx + (bw - btw) // 2 - bbb[0], by + (bh - bth) // 2 - bbb[1]), badge_txt, font=f_badge, fill=(116, 246, 234, 255))

    # Brand subtitle next to icon under badge
    sub_small = "Our World · Istanbul" if is_en else "Our World · Hamidiye"
    f_small = get_font(FONT_MED_PATH, 19)
    draw_text_with_glow(draw, bx + 4, by + bh + 10, sub_small, f_small, (255, 206, 84, 255), sh_off=2)

    # Giant two-line / hero title
    title_main = "OUR WORLD" if is_en else "BİZİM DÜNYAMIZ"
    f_title = fit_font(FONT_BOLD_PATH, title_main, 54, 468, 34)
    draw_text_with_glow(draw, left_x, 152, title_main, f_title, (255, 255, 255, 255), sh_off=4)

    tagline = "Drive • Play Sports • Meet Smart NPCs" if is_en else "Sür • Spor Yap • Mahalleni Yaşa"
    f_tag = fit_font(FONT_MED_PATH, tagline, 24, 468, 16)
    draw_text_with_glow(draw, left_x, 224, tagline, f_tag, (255, 212, 92, 255), sh_off=2)

    # 3 crisp glowing feature rows with colorful dots
    rows = (
        [
            ("32 Story Missions, Farm, Garage & Fishing", (54, 228, 214)),
            ("5 Live Sports & 4-Mode Shooting Range", (92, 242, 162)),
            ("10 TR & 6 EN Voices • Ad-Free Offline 3D", (255, 196, 72)),
        ]
        if is_en
        else [
            ("32 Hikâye Görevi, Çiftlik, Tamir & Balık", (54, 228, 214)),
            ("5 Canlı Spor & 4 Modlu Atış Poligonu", (92, 242, 162)),
            ("10 Türkçe & 6 İngilizce Ses • Reklamsız", (255, 196, 72)),
        ]
    )
    f_row = get_font(FONT_MED_PATH, 19)
    ry = 282
    for label, col in rows:
        draw.ellipse([left_x + 2, ry + 6, left_x + 14, ry + 18], fill=col + (255,))
        draw_text_with_glow(draw, left_x + 24, ry, label, f_row, (238, 248, 255, 255), sh_off=2)
        ry += 38

    # Bottom crisp pill CTA bar
    cta = "FREE 3D SANDBOX  •  OFFLINE PLAY" if is_en else "ÜCRETSİZ 3D AÇIK DÜNYA  •  ÇEVRİMDIŞI"
    f_cta = fit_font(FONT_MED_PATH, cta, 16, 440, 12)
    cbb = f_cta.getbbox(cta)
    ctw, cth = cbb[2] - cbb[0], cbb[3] - cbb[1]
    cw, ch = ctw + 38, cth + 18
    cy = h - ch - 26
    draw.rounded_rectangle([left_x, cy, left_x + cw, cy + ch], radius=ch // 2, fill=(14, 34, 62, 235), outline=(255, 196, 72, 240), width=2)
    draw.text((left_x + (cw - ctw) // 2 - cbb[0], cy + (ch - cth) // 2 - cbb[1]), cta, font=f_cta, fill=(255, 238, 186, 255))

    fname = "feature_graphic_en_1024x500.png" if is_en else "feature_graphic_1024x500.png"
    out_file = os.path.join(OUT_FEATURE, fname)
    fg.convert("RGB").save(out_file, "PNG", optimize=True)
    if not is_en:
        shutil.copyfile(out_file, STORE_FEATURE_DST)
    print(f"  -> Saved {out_file} (1024x500)")
    return out_file


POSTERS_TR = [
    {
        "id": "01_open_world_landmark",
        "layout": "dual",
        "show_icon": True,
        "badge": "BİZİM DÜNYAMIZ • 3D AÇIK DÜNYA",
        "title": "YAŞAYAN İSTANBUL'U KEŞFET",
        "highlight": "İSTANBUL'U",
        "sub": "8 ana cadde, 32 hikâye görevi, çiftlik, sürüş ve özgür mahalle hayatı!",
        "chips": ["8 ANA CADDE", "32 HİKÂYE GÖREVİ", "ÇEVRİMDIŞI & REKLAMSIZ"],
        "front_raw": os.path.join(RAW_DIR, "shot2_selman_driving.png"),
        "back_raw": os.path.join(RAW_DIR, "live_menu_hub.png"),
        "c_tl": (14, 42, 108), "c_br": (8, 22, 54),
        "glow_a": (36, 228, 214, 138), "glow_b": (255, 188, 56, 132),
        "accent_rgb": (48, 232, 216), "accent2_rgb": (255, 204, 72),
        "seed": 201,
    },
    {
        "id": "02_drive_and_explore",
        "layout": "single",
        "badge": "NO:73 OTO TAMİR • DRİFT, NİTRO & MODİFİYE",
        "title": "ARABANI MODİFİYE ET, SÜR",
        "highlight": "MODİFİYE ET,",
        "sub": "Oto tamirhanede modifiye yap, araba/motor sür, drift & nitro aç!",
        "chips": ["ARABA & MOTOSİKLET", "NO:73 OTO TAMİR", "DRİFT + NİTRO"],
        "raw": os.path.join(RAW_DIR, "live_driving_mobile.png"),
        "crop_bot": 44,
        "c_tl": (12, 46, 118), "c_br": (8, 18, 48),
        "glow_a": (52, 198, 255, 140), "glow_b": (255, 126, 76, 115),
        "accent_rgb": (74, 208, 255), "accent2_rgb": (255, 194, 74),
        "seed": 202,
    },
    {
        "id": "03_live_sports_5games",
        "layout": "dual",
        "badge": "5 OYNANABİLİR SPOR • İSTEDİĞİN AN MAÇ",
        "title": "HALI SAHA FUTBOL & TENİS",
        "highlight": "FUTBOL & TENİS",
        "sub": "3v3 halı sahada gol at, toprak kortta 1v1 tenis maçını kazan!",
        "chips": ["3v3 HALI SAHA", "1v1 TOPRAK KORT TENİS", "5 CANLI SPOR"],
        "front_raw": os.path.join(RAW_DIR, "live_football_3v3.png"),
        "front_crop_bot": 42,
        "back_raw": os.path.join(RAW_DIR, "live_tennis_1v1.png"),
        "back_crop_bot": 42,
        "c_tl": (10, 68, 56), "c_br": (8, 26, 36),
        "glow_a": (54, 240, 148, 138), "glow_b": (255, 162, 62, 126),
        "accent_rgb": (68, 242, 154), "accent2_rgb": (255, 196, 72),
        "seed": 203,
    },
    {
        "id": "04_smart_npcs_32missions",
        "layout": "dual",
        "badge": "6 BÖLÜM • 32 HİKÂYE GÖREVİ • 10 TR & 6 EN SES",
        "title": "SESLİ KONUŞAN MAHALLE",
        "highlight": "SESLİ KONUŞAN",
        "sub": "10 Türkçe & 6 İngilizce seslendirme, akıllı komşular ve 32 görev!",
        "chips": ["32 HİKÂYE GÖREVİ", "10 TR & 6 EN SES", "SOKAK KEDİLERİ"],
        "front_raw": os.path.join(RAW_DIR, "live_npc_mission.png"),
        "front_crop_bot": 42,
        "back_raw": os.path.join(RAW_DIR, "live_npc_neighbors.png"),
        "back_crop_bot": 42,
        "c_tl": (74, 42, 18), "c_br": (24, 16, 38),
        "glow_a": (255, 182, 58, 145), "glow_b": (48, 224, 206, 125),
        "accent_rgb": (255, 196, 68), "accent2_rgb": (62, 234, 214),
        "seed": 204,
    },
    {
        "id": "05_basketball_volleyball_pool",
        "layout": "dual",
        "badge": "SOKAK SPORLARI • 50M YÜZME & TRAMPLEN",
        "title": "BASKETBOL, VOLEYBOL & HAVUZ",
        "highlight": "BASKETBOL,",
        "sub": "2v2 basketbolda şut at, voleybolda smaç vur, 50m yüzme yarışına çık!",
        "chips": ["2v2 BASKETBOL", "2v2 VOLEYBOL", "50M YÜZME & DALIŞ"],
        "front_raw": os.path.join(RAW_DIR, "live_basketball_2v2.png"),
        "front_crop_bot": 42,
        "back_raw": os.path.join(RAW_DIR, "live_volleyball_2v2.png"),
        "back_crop_bot": 42,
        "c_tl": (82, 34, 28), "c_br": (16, 24, 58),
        "glow_a": (255, 134, 58, 142), "glow_b": (52, 222, 232, 130),
        "accent_rgb": (255, 162, 68), "accent2_rgb": (64, 234, 226),
        "seed": 205,
    },
    {
        "id": "06_farm_lake_fishing",
        "layout": "dual",
        "badge": "AYDOS ÇİFTLİĞİ • SULTANBEYLİ GÖLETİ",
        "title": "ÇİFTLİK, HAYVANLAR & BALIK",
        "highlight": "ÇİFTLİK,",
        "sub": "16 tarlada hasat yap, hayvanlarını besle & gölet iskelesinden balık tut!",
        "chips": ["16 BOSTAN TARLASI", "HAYVANCILIK & PAZAR", "GÖLET BALIKÇILIĞI"],
        "front_raw": os.path.join(RAW_DIR, "live_aydos_farm.png"),
        "back_raw": os.path.join(RAW_DIR, "live_lake_swans.png"),
        "c_tl": (18, 72, 46), "c_br": (10, 28, 44),
        "glow_a": (78, 236, 132, 138), "glow_b": (62, 214, 248, 132),
        "accent_rgb": (86, 240, 142), "accent2_rgb": (64, 224, 248),
        "seed": 207,
    },
    {
        "id": "07_shooting_range_swim",
        "layout": "dual",
        "badge": "AYDOS ATIŞ POLİGONU • AÇIK YÜZME HAVUZU",
        "title": "ATIŞ POLİGONU & 50M YÜZME",
        "highlight": "POLİGONU &",
        "sub": "4 silah & 4 sportif hedef modunda yarış, havuzda kulaç & tramplen atla!",
        "chips": ["4 SİLAH & 4 MOD", "HEDEF & TRAP", "50M YÜZME YARIŞI"],
        "front_raw": os.path.join(RAW_DIR, "live_shooting_range.png"),
        "back_raw": os.path.join(RAW_DIR, "live_swim_50m.png"),
        "c_tl": (64, 34, 22), "c_br": (12, 34, 62),
        "glow_a": (255, 174, 56, 142), "glow_b": (48, 218, 246, 134),
        "accent_rgb": (255, 192, 68), "accent2_rgb": (58, 226, 248),
        "seed": 208,
    },
    {
        "id": "08_day_night_cycle",
        "layout": "dual",
        "badge": "CANLI MENÜ • IŞIKLI İSTANBUL GECELERİ",
        "title": "GÜNDÜZDEN GECE IŞIKLARINA",
        "highlight": "GECE IŞIKLARINA",
        "sub": "İstediğin bölgeden başla, sokak lambalarıyla akşamları gez!",
        "chips": ["BÖLGE SEÇ & BAŞLA", "GECE SOKAK LAMBALARI", "TOPLULUKLA BÜYÜR"],
        "front_raw": os.path.join(RAW_DIR, "live_menu_hub.png"),
        "back_raw": os.path.join(RAW_DIR, "live_selman_night.png"),
        "back_night": True,
        "c_tl": (28, 22, 82), "c_br": (10, 14, 40),
        "glow_a": (255, 186, 64, 140), "glow_b": (62, 214, 242, 132),
        "accent_rgb": (255, 198, 76), "accent2_rgb": (74, 228, 244),
        "seed": 206,
    },
]

POSTERS_EN = [
    {
        "id": "01_open_world_landmark",
        "layout": "dual",
        "show_icon": True,
        "badge": "OUR WORLD • LIVING 3D OPEN WORLD",
        "title": "EXPLORE A LIVING 3D CITY",
        "highlight": "LIVING 3D",
        "sub": "8 avenues, 32 story missions, farming, driving & total freedom!",
        "chips": ["8 MAIN AVENUES", "32 STORY MISSIONS", "OFFLINE & AD-FREE"],
        "front_raw": os.path.join(RAW_DIR, "shot2_selman_driving.png"),
        "back_raw": os.path.join(RAW_DIR, "live_menu_hub.png"),
        "c_tl": (14, 42, 108), "c_br": (8, 22, 54),
        "glow_a": (36, 228, 214, 138), "glow_b": (255, 188, 56, 132),
        "accent_rgb": (48, 232, 216), "accent2_rgb": (255, 204, 72),
        "seed": 201,
    },
    {
        "id": "02_drive_and_explore",
        "layout": "single",
        "badge": "NO:73 AUTO GARAGE • DRIFT, NITRO & TUNING",
        "title": "TUNE YOUR CAR & DRIVE FREE",
        "highlight": "DRIVE FREE",
        "sub": "Customize & repair at No:73 Auto Garage, drift, hit nitro & play radio!",
        "chips": ["CARS & MOTORCYCLES", "NO:73 AUTO GARAGE", "DRIFT + NITRO"],
        "raw": os.path.join(RAW_DIR, "live_driving_mobile.png"),
        "crop_bot": 44,
        "c_tl": (12, 46, 118), "c_br": (8, 18, 48),
        "glow_a": (52, 198, 255, 140), "glow_b": (255, 126, 76, 115),
        "accent_rgb": (74, 208, 255), "accent2_rgb": (255, 194, 74),
        "seed": 202,
    },
    {
        "id": "03_live_sports_5games",
        "layout": "dual",
        "badge": "5 PLAYABLE SPORTS • JUMP IN ANYTIME",
        "title": "STREET FOOTBALL & TENNIS",
        "highlight": "FOOTBALL & TENNIS",
        "sub": "Score goals in 3v3 football & win rallies on the clay tennis court!",
        "chips": ["3v3 STREET FOOTBALL", "1v1 CLAY TENNIS", "5 LIVE SPORTS"],
        "front_raw": os.path.join(RAW_DIR, "live_football_3v3.png"),
        "front_crop_bot": 42,
        "back_raw": os.path.join(RAW_DIR, "live_tennis_1v1.png"),
        "back_crop_bot": 42,
        "c_tl": (10, 68, 56), "c_br": (8, 26, 36),
        "glow_a": (54, 240, 148, 138), "glow_b": (255, 162, 62, 126),
        "accent_rgb": (68, 242, 154), "accent2_rgb": (255, 196, 72),
        "seed": 203,
    },
    {
        "id": "04_smart_npcs_32missions",
        "layout": "dual",
        "badge": "6 CHAPTERS • 32 MISSIONS • EN & TR VOICES",
        "title": "VOICED LIVING NEIGHBORS",
        "highlight": "VOICED LIVING",
        "sub": "6 English & 10 Turkish voices, smart neighbors, street cats & 32 quests!",
        "chips": ["32 STORY MISSIONS", "EN & TR VOICEOVER", "STREET CATS"],
        "front_raw": os.path.join(RAW_DIR, "live_npc_mission.png"),
        "front_crop_bot": 42,
        "back_raw": os.path.join(RAW_DIR, "live_npc_neighbors.png"),
        "back_crop_bot": 42,
        "c_tl": (74, 42, 18), "c_br": (24, 16, 38),
        "glow_a": (255, 182, 58, 145), "glow_b": (48, 224, 206, 125),
        "accent_rgb": (255, 196, 68), "accent2_rgb": (62, 234, 214),
        "seed": 204,
    },
    {
        "id": "05_basketball_volleyball_pool",
        "layout": "dual",
        "badge": "STREET COURTS • 50M SWIM & DIVING",
        "title": "HOOPS, VOLLEYBALL & POOL",
        "highlight": "HOOPS,",
        "sub": "Drain 2v2 jump shots, spike volleyball winners & race 50m freestyle!",
        "chips": ["2v2 BASKETBALL", "2v2 VOLLEYBALL", "50M SWIM & DIVING"],
        "front_raw": os.path.join(RAW_DIR, "live_basketball_2v2.png"),
        "front_crop_bot": 42,
        "back_raw": os.path.join(RAW_DIR, "live_volleyball_2v2.png"),
        "back_crop_bot": 42,
        "c_tl": (82, 34, 28), "c_br": (16, 24, 58),
        "glow_a": (255, 134, 58, 142), "glow_b": (52, 222, 232, 130),
        "accent_rgb": (255, 162, 68), "accent2_rgb": (64, 234, 226),
        "seed": 205,
    },
    {
        "id": "06_farm_lake_fishing",
        "layout": "dual",
        "badge": "AYDOS VALLEY FARM • SULTANBEYLI LAKE",
        "title": "FARMING, ANIMALS & FISHING",
        "highlight": "FARMING,",
        "sub": "Grow 16 crop beds, care for livestock & cast off the lakeside pier!",
        "chips": ["16 CROP PLOTS", "LIVESTOCK & MARKET", "5 FISH SPECIES"],
        "front_raw": os.path.join(RAW_DIR, "live_aydos_farm.png"),
        "back_raw": os.path.join(RAW_DIR, "live_lake_swans.png"),
        "c_tl": (18, 72, 46), "c_br": (10, 28, 44),
        "glow_a": (78, 236, 132, 138), "glow_b": (62, 214, 248, 132),
        "accent_rgb": (86, 240, 142), "accent2_rgb": (64, 224, 248),
        "seed": 207,
    },
    {
        "id": "07_shooting_range_swim",
        "layout": "dual",
        "badge": "AYDOS SHOOTING RANGE • 50M SWIM RACE",
        "title": "TARGET RANGE & SWIMMING",
        "highlight": "TARGET RANGE",
        "sub": "Master 4 sporting target modes, race 50m lanes & dive off the board!",
        "chips": ["4 SPORTING MODES", "TARGET & CLAY TRAP", "50M SWIM RACE"],
        "front_raw": os.path.join(RAW_DIR, "live_shooting_range.png"),
        "back_raw": os.path.join(RAW_DIR, "live_swim_50m.png"),
        "c_tl": (64, 34, 22), "c_br": (12, 34, 62),
        "glow_a": (255, 174, 56, 142), "glow_b": (48, 218, 246, 134),
        "accent_rgb": (255, 192, 68), "accent2_rgb": (58, 226, 248),
        "seed": 208,
    },
    {
        "id": "08_day_night_cycle",
        "layout": "dual",
        "badge": "LIVING MENU • GLOWING NIGHT STREETS",
        "title": "FROM SUNNY DAYS TO LIT NIGHTS",
        "highlight": "LIT NIGHTS",
        "sub": "Start in any district & explore under glowing evening streetlamps!",
        "chips": ["DISTRICT FAST-START", "NIGHT STREETLAMPS", "PLAYER IDEAS"],
        "front_raw": os.path.join(RAW_DIR, "live_menu_hub.png"),
        "back_raw": os.path.join(RAW_DIR, "live_selman_night.png"),
        "back_night": True,
        "c_tl": (28, 22, 82), "c_br": (10, 14, 40),
        "glow_a": (255, 186, 64, 140), "glow_b": (62, 214, 242, 132),
        "accent_rgb": (255, 198, 76), "accent2_rgb": (74, 228, 244),
        "seed": 206,
    },
]


def main():
    active_lang = "tr"
    if "--lang" in sys.argv:
        idx = sys.argv.index("--lang")
        if idx + 1 < len(sys.argv):
            active_lang = sys.argv[idx + 1].lower()

    posters = POSTERS_EN if active_lang == "en" else POSTERS_TR
    root_dir = os.path.join(SCRIPT_DIR, "en-US") if active_lang == "en" else SCRIPT_DIR

    # Remove stale poster filenames from previous versions so sets stay clean & exact (6 files)
    valid_ids = {f"{p['id']}.png" for p in posters}

    total = 0
    for target_name, cfg in TARGETS.items():
        w, h, is_tab = cfg["w"], cfg["h"], cfg["is_tablet"]
        out_dir = os.path.join(root_dir, target_name)
        os.makedirs(out_dir, exist_ok=True)
        for existing in os.listdir(out_dir):
            if existing.endswith(".png") and existing not in valid_ids:
                os.remove(os.path.join(out_dir, existing))

        print(f"=== Generating [{active_lang}] {target_name} ({w} x {h}, tablet={is_tab}) ===")
        for spec in posters:
            img = build_poster(spec, w, h, is_tab)
            if target_name.startswith("app_store"):
                img = img.convert("RGB")
            out_file = os.path.join(out_dir, f"{spec['id']}.png")
            img.save(out_file, "PNG", optimize=True)
            total += 1
            print(f"  -> {out_file}")

    print(f"=== Generating Google Play Header / Feature Graphic ({active_lang}, 1024 x 500) ===")
    build_feature_graphic(active_lang)
    total += 1

    print(f"=== Generated {total} Vibrant Store Marketing Assets ({active_lang})! ===")


if __name__ == "__main__":
    main()
