#!/usr/bin/env python3
"""Generates the App Store (iOS 27+) Product Page "Header and Search Results" creative.

Produces one Universal asset per locale (16:9, 5244 x 2950, PNG, no alpha) that App Store
Connect uses for both the product page header (21:9 crop) and the search results card (3:2 crop).
Key art + the short phrase stay inside Apple's Universal Art Safe Area
(1402 x 962 px at x=1921, y=660); the Auxiliary Art Area only carries background/side cards.

Reuses the palette and floating-card renderer from generate_store_posters.py.
Output: release/store_screenshots/app_store_header/<locale>/universal_5244x2950.png
"""
import os

from PIL import Image, ImageDraw

import generate_store_posters as gp

W, H = 5244, 2950
SAFE_X, SAFE_Y, SAFE_W, SAFE_H = 1921, 660, 1402, 962
SAFE_CX = SAFE_X + SAFE_W // 2

OUT_ROOT = os.path.join(gp.SCRIPT_DIR, "app_store_header")

PHRASES = {
    "tr": "İstanbul'da Kendi Hayatını Yaşa",
    "en-US": "Live Your Own Istanbul Life",
}


def shot(name, crop_bottom=0, night=False):
    return gp.load_and_grade_screenshot(os.path.join(gp.RAW_DIR, name), crop_bottom_px=crop_bottom, warm_night_boost=night)


def card(img, w, h, accent, scale, angle=0.0):
    c = gp.render_floating_screen(img, w, h, accent, scale)
    if angle:
        c = c.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
    return c


def paste_centered(canvas, layer, cx, cy):
    canvas.alpha_composite(layer, (int(cx - layer.size[0] / 2), int(cy - layer.size[1] / 2)))


def build(locale):
    hero = shot("shot2_selman_driving.png")
    football = shot("live_football_3v3.png", crop_bottom=42)
    farm = shot("live_aydos_farm.png")
    tennis = shot("live_tennis_1v1.png", crop_bottom=42)
    shooting = shot("live_shooting_range.png")

    canvas = gp.create_vibrant_backdrop(
        W, H,
        c_tl=(14, 34, 88),
        c_br=(8, 22, 54),
        glow_a=(36, 224, 212, 130),
        glow_b=(255, 184, 52, 128),
        bg_shot=shot("shot5_street_football_life.png"),
        seed=2027,
    )

    band_cy = SAFE_Y + 360  # cards sit on the upper part of the safe area, phrase below

    # Auxiliary area: a dimmed, slightly tilted wall of real gameplay so every crop (21:9 header,
    # 3:2 search card) is filled with game content instead of empty gradient.
    wall_names = [
        ("live_basketball_2v2.png", 42), ("live_volleyball_2v2.png", 42), ("live_swim_50m.png", 0),
        ("live_lake_swans.png", 0), ("shot3_hz_osman_camii.png", 0), ("live_npc_neighbors.png", 42),
        ("shot6_night_istanbul_lights.png", 0), ("live_selman_day.png", 0), ("shot4_petrol_aziziye_avenues.png", 0),
        ("live_npc_mission.png", 42), ("shot1_fatih_saat_kulesi.png", 0), ("live_selman_night.png", 0),
    ]
    wall_imgs = [shot(n, c) for n, c in wall_names]
    wall = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    tile_w, tile_h, gap_x = 1060, 596, 1200
    k = 0
    for row, cy in enumerate((330, 1060, 1790, 2520)):
        offset = 0 if row % 2 == 0 else gap_x // 2
        for col in range(-1, 6):
            cx = col * gap_x + offset + 140
            tile = card(wall_imgs[k % len(wall_imgs)], tile_w, tile_h,
                        (54, 228, 214) if k % 2 == 0 else (255, 186, 62), 1.8)
            paste_centered(wall, tile, cx, cy)
            k += 1
    wall = wall.rotate(-6, resample=Image.Resampling.BICUBIC, center=(W // 2, H // 2))
    canvas.alpha_composite(wall)
    # Dim the wall, darker around the safe area so the hero card & phrase pop
    scrim = Image.new("RGBA", (W, H), (6, 14, 34, 150))
    sdraw = ImageDraw.Draw(scrim)
    sdraw.ellipse([SAFE_CX - 1500, SAFE_Y - 650, SAFE_CX + 1500, SAFE_Y + SAFE_H + 650], fill=(6, 14, 34, 215))
    scrim = scrim.filter(gp.ImageFilter.GaussianBlur(220))
    canvas.alpha_composite(scrim)

    # Side cards next to the hero (aux area, close enough to read in the header crop)
    paste_centered(canvas, card(tennis, 1000, 563, (255, 186, 62), 1.9, 8), SAFE_CX - 2650, band_cy + 140)
    paste_centered(canvas, card(shooting, 1000, 563, (66, 236, 152), 1.9, -8), SAFE_CX + 2650, band_cy + 140)
    paste_centered(canvas, card(football, 1160, 653, (66, 236, 152), 2.1, 5), SAFE_CX - 1420, band_cy + 60)
    paste_centered(canvas, card(farm, 1160, 653, (255, 186, 62), 2.1, -5), SAFE_CX + 1420, band_cy + 60)

    # Safe area: hero gameplay card
    paste_centered(canvas, card(hero, 1230, 692, (54, 228, 214), 2.4), SAFE_CX, band_cy)

    # Safe area: short localized phrase under the hero card
    draw = ImageDraw.Draw(canvas)
    phrase = PHRASES[locale]
    font = gp.fit_font(gp.FONT_BOLD_PATH, phrase, 118, SAFE_W - 40, 60)
    bb = font.getbbox(phrase)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    tx = SAFE_CX - tw // 2 - bb[0]
    ty = SAFE_Y + SAFE_H - th - 70 - bb[1]
    gp.draw_text_with_glow(draw, tx, ty, phrase, font, (255, 255, 255, 255), sh_off=7)
    # Gold accent underline (app icon palette)
    uy = SAFE_Y + SAFE_H - 40
    draw.rounded_rectangle([SAFE_CX - 220, uy, SAFE_CX + 220, uy + 14], radius=7, fill=(255, 206, 84, 255))

    return canvas.convert("RGB")  # Universal asset: PNG without alpha


def main():
    for locale in PHRASES:
        out_dir = os.path.join(OUT_ROOT, locale)
        os.makedirs(out_dir, exist_ok=True)
        out_file = os.path.join(out_dir, "universal_5244x2950.png")
        img = build(locale)
        assert img.size == (W, H) and img.mode == "RGB"
        img.save(out_file, "PNG", optimize=True)
        print(f"-> {out_file} ({os.path.getsize(out_file) / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()
