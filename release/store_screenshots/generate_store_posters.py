#!/usr/bin/env python3
"""
OurWorld — Universal Store Marketing Screenshot & Poster Studio (ported from Kube 2026 Studio)
Generates high-converting landscape store marketing posters across all required mobile & tablet sizes
in BOTH Turkish (tr) and English (en-US):
  - Google Play Phone (1920 x 1080)
  - Apple App Store 6.7" iPhone (2796 x 1290)
  - Apple App Store 6.5" iPhone (2688 x 1242)
  - Apple App Store 12.9"/13" iPad Pro (2732 x 2048)
  - Google Play 7" Tablet (1920 x 1200)
  - Google Play 10" Tablet (2560 x 1600)
  - Google Play Feature Graphic (1024 x 500 RGB)

Usage:
  python3 release/store_screenshots/generate_store_posters.py --lang tr
  python3 release/store_screenshots/generate_store_posters.py --lang en
"""

import os
import random
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
RAW_DIR = os.path.join(SCRIPT_DIR, "raw")

TARGETS = {
    "google_play_phone":     {"w": 1920, "h": 1080, "is_tablet": False},
    "app_store_6.7":         {"w": 2796, "h": 1290, "is_tablet": False},
    "app_store_6.5":         {"w": 2688, "h": 1242, "is_tablet": False},
    "app_store_ipad_12.9":   {"w": 2732, "h": 2048, "is_tablet": True},
    "google_play_tablet_7":  {"w": 1920, "h": 1200, "is_tablet": True},
    "google_play_tablet_10": {"w": 2560, "h": 1600, "is_tablet": True},
}

OUT_FEATURE = os.path.join(SCRIPT_DIR, "google_play_feature")

FONT_CANDIDATES = [
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    "/System/Library/Fonts/Helvetica.ttc",
    "/System/Library/Fonts/SFNS.ttf",
]
FONT_SYSTEM_PATH = next((p for p in FONT_CANDIDATES if os.path.exists(p)), "")

_FONT_CACHE = {}


def get_font(path, size):
    key = (path, int(size))
    if key not in _FONT_CACHE:
        try:
            _FONT_CACHE[key] = ImageFont.truetype(path, int(size))
        except Exception:
            _FONT_CACHE[key] = ImageFont.load_default()
    return _FONT_CACHE[key]


_RAW_CACHE = {}


def load_and_clean_screenshot(raw_path):
    if raw_path in _RAW_CACHE:
        return _RAW_CACHE[raw_path]
    if not os.path.exists(raw_path):
        raise FileNotFoundError(
            f"Missing raw screenshot: {raw_path}\n"
            f"Run Unity menu 'GRIDROAM -> Store -> 1. Capture Raw Store Screenshots' first."
        )

    im = Image.open(raw_path).convert("RGBA")
    w, h = im.size
    if w == 2622 and h == 1206:
        # iPhone landscape Dynamic Island crop
        im = im.crop((175, 0, w, h))
    elif w == 2424 and h == 1080:
        # Android Pixel landscape cutout + status bar crop
        im = im.crop((145, 138, 2424, 1080))

    _RAW_CACHE[raw_path] = im
    return im


def draw_diamond_icon(draw, cx, cy, r, fill):
    pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)]
    draw.polygon(pts, fill=fill)


def create_stylized_background(width, height, top_col, bot_col, glow_col, seed=42):
    grad_1px = Image.new("RGBA", (1, height))
    for y in range(height):
        t = y / float(max(1, height - 1))
        r = int(top_col[0] * (1 - t) + bot_col[0] * t)
        g = int(top_col[1] * (1 - t) + bot_col[1] * t)
        b = int(top_col[2] * (1 - t) + bot_col[2] * t)
        grad_1px.putpixel((0, y), (r, g, b, 255))
    img = grad_1px.resize((width, height), Image.Resampling.NEAREST)

    glow = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    cx1, cy1 = int(width * 0.5), int(height * 0.52)
    rad1 = int(width * 0.48)
    gdraw.ellipse([cx1 - rad1, cy1 - rad1, cx1 + rad1, cy1 + rad1], fill=glow_col)

    cx2, cy2 = int(width * 0.5), int(height * 0.12)
    rad2 = int(width * 0.30)
    top_glow = (glow_col[0], glow_col[1], glow_col[2], max(24, glow_col[3] // 2))
    gdraw.ellipse([cx2 - rad2, cy2 - rad2, cx2 + rad2, cy2 + rad2], fill=top_glow)

    glow = glow.filter(ImageFilter.GaussianBlur(max(1, rad1 // 3)))
    img = Image.alpha_composite(img, glow)

    fx = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    fdraw = ImageDraw.Draw(fx)
    accent_rgb = glow_col[:3]

    beam_w = int(width * 0.08)
    for offset in [-0.22, 0.36, 0.78]:
        x0 = int(width * offset)
        pts = [
            (x0, 0),
            (x0 + beam_w, 0),
            (x0 - int(height * 0.25) + beam_w, int(height * 0.45)),
            (x0 - int(height * 0.25), int(height * 0.45)),
        ]
        fdraw.polygon(pts, fill=accent_rgb + (8,))

    horizon_y = int(height * 0.78)
    for gy in range(horizon_y, height, max(16, int(height * 0.028))):
        alpha = int(20 * ((gy - horizon_y) / float(max(1, height - horizon_y))))
        fdraw.line([(0, gy), (width, gy)], fill=accent_rgb + (alpha,), width=1)
    for i in range(-6, 7):
        top_x = width // 2 + int(i * width * 0.06)
        bot_x = width // 2 + int(i * width * 0.18)
        fdraw.line([(top_x, horizon_y), (bot_x, height)], fill=accent_rgb + (14,), width=1)

    rng = random.Random(seed)
    for _ in range(20):
        px = rng.randint(int(width * 0.04), int(width * 0.96))
        py = rng.randint(int(height * 0.04), int(height * 0.94))
        pr = rng.randint(max(3, int(width * 0.003)), max(7, int(width * 0.011)))
        pa = rng.randint(24, 68)
        fdraw.ellipse([px - pr, py - pr, px + pr, py + pr], fill=accent_rgb + (pa,))

    return Image.alpha_composite(img, fx)


def render_device_frame(content_img, target_w, target_h, accent_col=(64, 210, 196), bezel_scale=1.0):
    bezel = max(10, int(14 * bezel_scale))
    corner = max(24, int(40 * bezel_scale))
    pad = max(20, int(30 * bezel_scale))
    frame_w = target_w + bezel * 2
    frame_h = target_h + bezel * 2

    frame = Image.new("RGBA", (frame_w + pad * 2, frame_h + pad * 2), (0, 0, 0, 0))
    fdraw = ImageDraw.Draw(frame)

    steps = 14
    for i in range(steps, 0, -2):
        ratio = 1.0 - (i / float(steps))
        alpha_shadow = int(22 * ratio)
        fdraw.rounded_rectangle(
            [pad - i, pad + i // 2, pad + frame_w + i, pad + frame_h + i + 4],
            radius=corner + i,
            fill=(0, 0, 0, alpha_shadow),
        )
    for i in range(6, 0, -2):
        fdraw.rounded_rectangle(
            [pad - i, pad - i, pad + frame_w + i, pad + frame_h + i],
            radius=corner + i,
            outline=accent_col[:3] + (int(42 * (1.0 - i / 7.0)),),
            width=2,
        )

    body_col = (
        min(42, 14 + accent_col[0] // 10),
        min(52, 22 + accent_col[1] // 10),
        min(68, 32 + accent_col[2] // 8),
        255,
    )
    fdraw.rounded_rectangle(
        [pad, pad, pad + frame_w, pad + frame_h],
        radius=corner,
        fill=body_col,
        outline=accent_col[:3] + (210,),
        width=max(2, int(3 * bezel_scale)),
    )

    screen_mask = Image.new("L", (target_w, target_h), 0)
    sdraw = ImageDraw.Draw(screen_mask)
    sdraw.rounded_rectangle([0, 0, target_w, target_h], radius=max(8, corner - bezel), fill=255)

    resized_content = content_img.resize((target_w, target_h), Image.Resampling.LANCZOS)
    frame.paste(resized_content, (pad + bezel, pad + bezel), screen_mask)

    gloss = Image.new("RGBA", (target_w, target_h), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(gloss)
    gdraw.polygon([(0, 0), (int(target_w * 0.36), 0), (0, int(target_h * 0.28))], fill=(255, 255, 255, 10))
    frame.paste(gloss, (pad + bezel, pad + bezel), gloss)

    return frame


def ui_scale(width, height, is_tablet=False):
    s = min(width / 2400.0, height / 1080.0) * 0.78
    return s * (1.14 if is_tablet else 1.0)


def _fit_font(path, text, size, max_w, min_size):
    f = get_font(path, size)
    while (f.getbbox(text)[2] - f.getbbox(text)[0]) > max_w and size > min_size:
        size -= 2
        f = get_font(path, size)
    return f


def _draw_centered(draw, width, y, text, font, fill, shadow=None, sh_off=3):
    bb = font.getbbox(text)
    x = (width - (bb[2] - bb[0])) // 2 - bb[0]
    if shadow:
        draw.text((x + sh_off, y - bb[1] + sh_off), text, font=font, fill=shadow)
    draw.text((x, y - bb[1]), text, font=font, fill=fill)
    return bb[3] - bb[1]


def draw_header(canvas, width, height, badge_text, title_text, subtitle_text, glow_col, is_tablet=False):
    draw = ImageDraw.Draw(canvas)
    s = ui_scale(width, height, is_tablet)
    max_w = width * 0.90

    f_badge = _fit_font(FONT_SYSTEM_PATH, badge_text, int(28 * s), max_w * 0.85, 14)
    f_title = _fit_font(FONT_SYSTEM_PATH, title_text, int(86 * s), max_w, 28)
    f_sub = _fit_font(FONT_SYSTEM_PATH, subtitle_text, int(36 * s), max_w, 16)

    y = int(height * 0.032)

    bb = f_badge.getbbox(badge_text)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    pad_x = int(44 * s)
    bw, bh = tw + pad_x * 2, th + int(22 * s)
    bx = (width - bw) // 2
    draw.rounded_rectangle(
        [bx, y, bx + bw, y + bh],
        radius=bh // 2,
        fill=(12, 26, 40, 240),
        outline=glow_col[:3] + (235,),
        width=max(2, int(3 * s)),
    )
    diam_r = max(4, int(6 * s))
    draw_diamond_icon(draw, bx + int(20 * s), y + bh // 2, diam_r, glow_col[:3] + (255,))
    draw_diamond_icon(draw, bx + bw - int(20 * s), y + bh // 2, diam_r, glow_col[:3] + (255,))
    draw.text((bx + (bw - tw) // 2 - bb[0], y + (bh - th) // 2 - bb[1]), badge_text, font=f_badge, fill=glow_col[:3] + (255,))
    y += bh + int(20 * s)

    h = _draw_centered(draw, width, y, title_text, f_title, (255, 255, 255, 255), shadow=(0, 0, 0, 220), sh_off=max(2, int(4 * s)))
    y += h + int(14 * s)

    h = _draw_centered(draw, width, y, subtitle_text, f_sub, (212, 236, 248, 255), shadow=(0, 0, 0, 180), sh_off=2)
    return y + h + int(24 * s)


def draw_footer_pill(canvas, width, height, footer_text, glow_col, is_tablet=False):
    draw = ImageDraw.Draw(canvas)
    s = ui_scale(width, height, is_tablet)
    f_foot = _fit_font(FONT_SYSTEM_PATH, footer_text, int(28 * s), width * 0.84, 14)
    bb = f_foot.getbbox(footer_text)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    bw, bh = tw + int(76 * s), th + int(26 * s)
    bx = (width - bw) // 2
    by = height - bh - int(height * 0.025)
    draw.rounded_rectangle(
        [bx, by, bx + bw, by + bh],
        radius=bh // 2,
        fill=(12, 24, 38, 235),
        outline=glow_col[:3] + (210,),
        width=max(2, int(3 * s)),
    )
    diam_r = max(4, int(6 * s))
    draw_diamond_icon(draw, bx + int(22 * s), by + bh // 2, diam_r, glow_col[:3] + (255,))
    draw_diamond_icon(draw, bx + bw - int(22 * s), by + bh // 2, diam_r, glow_col[:3] + (255,))
    draw.text((bx + (bw - tw) // 2 - bb[0], by + (bh - th) // 2 - bb[1]), footer_text, font=f_foot, fill=(235, 248, 255, 255))
    return by - int(20 * s)


def _frame_extra(s):
    return max(10, int(14 * s)) + max(20, int(30 * s))


def _fit_phone(raw, max_w, max_h, s):
    ex = _frame_extra(s) * 2
    ar = raw.size[0] / float(raw.size[1])
    h = max_h - ex
    w = int(h * ar)
    if w > max_w - ex:
        w = max_w - ex
        h = int(w / ar)
    return max(160, w), max(90, h)


def compose_single(canvas, width, height, top_y, bot_y, raw_path, glow_col, is_tablet=False):
    raw = load_and_clean_screenshot(raw_path)
    s = ui_scale(width, height, is_tablet)
    avail_h = bot_y - top_y
    w, h = _fit_phone(raw, int(width * (0.86 if is_tablet else 0.78)), avail_h, s)
    dev = render_device_frame(raw, w, h, glow_col, s)
    canvas.paste(dev, ((width - dev.size[0]) // 2, top_y + (avail_h - dev.size[1]) // 2), dev)


def compose_dual(canvas, width, height, top_y, bot_y, front_path, back_path, glow_col, is_tablet=False):
    raw_front = load_and_clean_screenshot(front_path)
    raw_back = load_and_clean_screenshot(back_path)
    s = ui_scale(width, height, is_tablet)
    avail_h = bot_y - top_y

    fw, fh = _fit_phone(raw_front, int(width * 0.62), avail_h, s)
    bw, bh = int(fw * 0.86), int(fh * 0.86)
    dev_f = render_device_frame(raw_front, fw, fh, glow_col, s)
    dev_b = render_device_frame(raw_back, bw, bh, glow_col, s * 0.86)

    fx = int(width * 0.05)
    fy = top_y + (avail_h - dev_f.size[1]) // 2
    tilted = dev_b.rotate(-5, resample=Image.Resampling.BICUBIC, expand=True)
    bx = width - tilted.size[0] - int(width * 0.03)
    by = top_y + (avail_h - tilted.size[1]) // 2 + int(18 * s)

    canvas.paste(tilted, (bx, by), tilted)
    canvas.paste(dev_f, (fx, fy), dev_f)


def build_poster(spec, width, height, is_tablet=False):
    canvas = create_stylized_background(width, height, spec["top_col"], spec["bot_col"], spec["glow_col"], seed=spec["seed"])
    top_y = draw_header(canvas, width, height, spec["badge"], spec["title"], spec["sub"], spec["glow_col"], is_tablet)
    bot_y = draw_footer_pill(canvas, width, height, spec["footer"], spec["glow_col"], is_tablet)

    if spec["layout"] == "dual":
        compose_dual(canvas, width, height, top_y, bot_y, spec["front_raw"], spec["back_raw"], spec["glow_col"], is_tablet)
    else:
        compose_single(canvas, width, height, top_y, bot_y, spec["raw"], spec["glow_col"], is_tablet)

    return canvas


def build_feature_graphic():
    os.makedirs(OUT_FEATURE, exist_ok=True)
    fg = create_stylized_background(1024, 500, (10, 26, 40), (6, 14, 24), (64, 214, 198, 92), seed=777)
    fdraw = ImageDraw.Draw(fg)

    f_badge = get_font(FONT_SYSTEM_PATH, 18)
    badge = "OURWORLD • OYUNCULARLA BÜYÜYEN AÇIK DÜNYA"
    fdraw.rounded_rectangle([42, 42, 495, 78], radius=18, fill=(14, 34, 48, 240), outline=(64, 214, 198, 235), width=2)
    fdraw.text((62, 50), badge, font=f_badge, fill=(96, 238, 222, 255))

    f_title = get_font(FONT_SYSTEM_PATH, 68)
    fdraw.text((44, 96), "OURWORLD", font=f_title, fill=(0, 0, 0, 190))
    fdraw.text((42, 94), "OURWORLD", font=f_title, fill=(255, 255, 255, 255))

    f_tag = get_font(FONT_SYSTEM_PATH, 24)
    fdraw.text((42, 178), "Yürü • Sür • Keşfet • Fikrini Yaz", font=f_tag, fill=(205, 234, 248, 255))

    bullets = [
        "• Gerçek Sokaklardan İlham Alan 3D Açık Dünya",
        "• Çoklu Araç Sürüşü, Drift & Nitro + Radyo",
        "• Girilebilir Evler, Dükkânlar, Cami & Yan İşler",
        "• Oyun İçi Canlı Topluluk Öneri Panosu",
    ]
    f_b = get_font(FONT_SYSTEM_PATH, 19)
    by = 236
    for b in bullets:
        fdraw.text((42, by), b, font=f_b, fill=(94, 240, 210, 255))
        by += 36

    raw_hero = load_and_clean_screenshot(os.path.join(RAW_DIR, "shot1_fatih_saat_kulesi.png"))
    hero_w = 430
    hero_h = int(hero_w * raw_hero.size[1] / float(raw_hero.size[0]))
    hero = render_device_frame(raw_hero, hero_w, hero_h, (64, 214, 198), 0.78)
    fg.paste(hero, (1024 - hero.size[0] - 10, (500 - hero.size[1]) // 2), hero)

    out_file = os.path.join(OUT_FEATURE, "feature_graphic_1024x500.png")
    fg.convert("RGB").save(out_file, "PNG", optimize=True)
    print(f"  -> Saved {out_file} (1024x500)")


POSTERS_TR = [
    {
        "id": "01_open_world_landmark",
        "layout": "single",
        "badge": "OURWORLD • GERÇEK SOKAKLARDAN AÇIK DÜNYA",
        "title": "YAŞAYAN ŞEHİRDE ÖZGÜRCE DOLAŞ",
        "sub": "Saat Kulesi meydanından ara sokaklara kadar her caddeyi keşfet!",
        "footer": "8 ANA CADDE  •  GERÇEKÇİ 3D MİMARİ  •  CANLI GPS",
        "raw": os.path.join(RAW_DIR, "shot1_fatih_saat_kulesi.png"),
        "top_col": (10, 26, 40), "bot_col": (6, 13, 22),
        "glow_col": (64, 214, 198, 92),
        "seed": 101,
    },
    {
        "id": "02_drive_and_explore",
        "layout": "single",
        "badge": "ÇOKLU ARAÇ • DRİFT, NİTRO & RADYO",
        "title": "İSTEDİĞİN ARACA BİN VE SÜR",
        "sub": "Sokaklarda gez, driftle nitro doldur, Türkçe radyo aç!",
        "footer": "GERÇEKÇİ HASAR FİZİĞİ  •  KORNA & FARLAR  •  60 FPS",
        "raw": os.path.join(RAW_DIR, "shot2_selman_driving.png"),
        "top_col": (14, 28, 46), "bot_col": (7, 14, 26),
        "glow_col": (80, 198, 255, 92),
        "seed": 102,
    },
    {
        "id": "03_enterable_buildings",
        "layout": "single",
        "badge": "100% GİRİLEBİLİR İÇ MEKÂNLAR",
        "title": "EVLERE, DÜKKÂNLARA VE CAMİYE GİR",
        "sub": "Koltuğa otur, ev boya, mahalle esnafıyla sohbet et!",
        "footer": "DETAYLI İÇ MEKÂNLAR  •  HZ. OSMAN CAMİİ  •  EV BOYAMA",
        "raw": os.path.join(RAW_DIR, "shot3_hz_osman_camii.png"),
        "top_col": (24, 22, 14), "bot_col": (12, 11, 8),
        "glow_col": (246, 192, 76, 92),
        "seed": 103,
    },
    {
        "id": "04_avenues_and_jobs",
        "layout": "dual",
        "badge": "4 FARKLI YAN İŞ & 10 HİKÂYE GÖREVİ",
        "title": "PİZZA, TAKSİ, TEMİZLİK VE BOYAMA",
        "sub": "Görevlerden para kazan, yeni araçlar ve mahalleler keşfet!",
        "footer": "PİZZA TESLİMATI  •  TAKSİCİLİK  •  MAHALLE EKONOMİSİ",
        "front_raw": os.path.join(RAW_DIR, "shot4_petrol_aziziye_avenues.png"),
        "back_raw": os.path.join(RAW_DIR, "shot2_selman_driving.png"),
        "top_col": (14, 30, 38), "bot_col": (7, 15, 22),
        "glow_col": (66, 232, 186, 92),
        "seed": 104,
    },
    {
        "id": "05_living_neighborhood",
        "layout": "single",
        "badge": "YAŞAYAN MAHALLE & MİNİ OYUNLAR",
        "title": "SOKAK FUTBOLU, KEDİLER & DAHA FAZLASI",
        "sub": "Sokak kedilerini sev, mahalle sahasında gol at, toplulukla oyunu büyüt!",
        "footer": "SOKAK FUTBOLU  •  İSTANBUL KEDİLERİ  •  CANLI TOPLULUK",
        "raw": os.path.join(RAW_DIR, "shot5_street_football_life.png"),
        "top_col": (12, 32, 28), "bot_col": (6, 16, 16),
        "glow_col": (92, 236, 158, 92),
        "seed": 105,
    },
    {
        "id": "06_day_night_cycle",
        "layout": "dual",
        "badge": "DİNAMİK GÜN / GECE DÖNGÜSÜ",
        "title": "GÜNDÜZDEN GECE IŞIKLARINA",
        "sub": "Sabah güneşinden sokak lambalarına uzanan canlı atmosfer!",
        "footer": "SOKAK LAMBALARI  •  TELEFON HARİTASI  •  OYUNCULARLA BÜYÜR",
        "front_raw": os.path.join(RAW_DIR, "shot6_night_istanbul_lights.png"),
        "back_raw": os.path.join(RAW_DIR, "shot1_fatih_saat_kulesi.png"),
        "top_col": (18, 16, 38), "bot_col": (8, 9, 22),
        "glow_col": (250, 186, 86, 92),
        "seed": 106,
    },
]

POSTERS_EN = [
    {
        "id": "01_open_world_landmark",
        "layout": "single",
        "badge": "OURWORLD • BUILT TOGETHER WITH PLAYERS",
        "title": "ROAM FREE IN A LIVING 3D WORLD",
        "sub": "Explore clock-tower plazas, bustling avenues & hidden side streets!",
        "footer": "8 MAIN AVENUES  •  HANDCRAFTED 3D STREETS  •  LIVE GPS",
        "raw": os.path.join(RAW_DIR, "shot1_fatih_saat_kulesi.png"),
        "top_col": (10, 26, 40), "bot_col": (6, 13, 22),
        "glow_col": (64, 214, 198, 92),
        "seed": 101,
    },
    {
        "id": "02_drive_and_explore",
        "layout": "single",
        "badge": "MULTI-VEHICLE DRIVING • DRIFT & NITRO",
        "title": "HOP INTO ANY CAR AND DRIVE",
        "sub": "Drift to charge nitro, honk at traffic & tune the car radio!",
        "footer": "REAL-TIME CRASH DAMAGE  •  HEADLIGHTS & HORN  •  60 FPS",
        "raw": os.path.join(RAW_DIR, "shot2_selman_driving.png"),
        "top_col": (14, 28, 46), "bot_col": (7, 14, 26),
        "glow_col": (80, 198, 255, 92),
        "seed": 102,
    },
    {
        "id": "03_enterable_buildings",
        "layout": "single",
        "badge": "100% ENTERABLE INTERIORS",
        "title": "STEP INSIDE HOMES, SHOPS & LANDMARKS",
        "sub": "Sit on benches, paint houses & explore detailed interiors!",
        "footer": "WALK-IN BUILDINGS  •  HOUSE PAINTING  •  LOCAL LIFE",
        "raw": os.path.join(RAW_DIR, "shot3_hz_osman_camii.png"),
        "top_col": (24, 22, 14), "bot_col": (12, 11, 8),
        "glow_col": (246, 192, 76, 92),
        "seed": 103,
    },
    {
        "id": "04_avenues_and_jobs",
        "layout": "dual",
        "badge": "4 SIDE JOBS & 10 STORY MISSIONS",
        "title": "PIZZA, TAXI, CLEANUP & PAINTING",
        "sub": "Earn cash from open-world jobs & unlock every corner of the city!",
        "footer": "PIZZA DELIVERY  •  TAXI FARES  •  OPEN-WORLD ECONOMY",
        "front_raw": os.path.join(RAW_DIR, "shot4_petrol_aziziye_avenues.png"),
        "back_raw": os.path.join(RAW_DIR, "shot2_selman_driving.png"),
        "top_col": (14, 30, 38), "bot_col": (7, 15, 22),
        "glow_col": (66, 232, 186, 92),
        "seed": 104,
    },
    {
        "id": "05_living_neighborhood",
        "layout": "single",
        "badge": "LIVING STREETS & MINI-GAMES",
        "title": "STREET FOOTBALL, CATS & COMMUNITY",
        "sub": "Pet friendly street cats, score goals & suggest features in-game!",
        "footer": "STREET FOOTBALL  •  STREET CATS  •  PLAYER IDEAS BOARD",
        "raw": os.path.join(RAW_DIR, "shot5_street_football_life.png"),
        "top_col": (12, 32, 28), "bot_col": (6, 16, 16),
        "glow_col": (92, 236, 158, 92),
        "seed": 105,
    },
    {
        "id": "06_day_night_cycle",
        "layout": "dual",
        "badge": "DYNAMIC DAY / NIGHT CYCLE",
        "title": "FROM SUNNY MORNINGS TO NIGHT LIGHTS",
        "sub": "Watch streetlamps glow at dusk and cruise under the stars!",
        "footer": "GLOWING STREETLAMPS  •  PHONE GPS MAP  •  REGULAR UPDATES",
        "front_raw": os.path.join(RAW_DIR, "shot6_night_istanbul_lights.png"),
        "back_raw": os.path.join(RAW_DIR, "shot1_fatih_saat_kulesi.png"),
        "top_col": (18, 16, 38), "bot_col": (8, 9, 22),
        "glow_col": (250, 186, 86, 92),
        "seed": 106,
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

    total = 0
    for target_name, cfg in TARGETS.items():
        w, h, is_tab = cfg["w"], cfg["h"], cfg["is_tablet"]
        out_dir = os.path.join(root_dir, target_name)
        os.makedirs(out_dir, exist_ok=True)
        print(f"=== Generating [{active_lang}] {target_name} ({w} x {h}, tablet={is_tab}) ===")
        for spec in posters:
            img = build_poster(spec, w, h, is_tab)
            if target_name.startswith("app_store"):
                img = img.convert("RGB")
            out_file = os.path.join(out_dir, f"{spec['id']}.png")
            img.save(out_file, "PNG", optimize=True)
            total += 1
            print(f"  -> {out_file}")

    if active_lang != "en":
        print("=== Generating Google Play Feature Graphic (1024 x 500) ===")
        build_feature_graphic()
        total += 1

    print(f"=== Generated {total} OurWorld Store Marketing Assets ({active_lang})! ===")


if __name__ == "__main__":
    main()
