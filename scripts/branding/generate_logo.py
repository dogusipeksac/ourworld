#!/usr/bin/env python3
"""Generates the Bizim Dünyamız / Our World wordmark logos used by the main menu.

Outputs (transparent PNG, 1600x640):
  Assets/GRIDROAM/Resources/UI/Branding/ourworld_logo_tr.png
  Assets/GRIDROAM/Resources/UI/Branding/ourworld_logo_en.png
  Assets/GRIDROAM/Resources/UI/Branding/ourworld_emblem.png   (512x512 round globe badge)

Fonts: Sora Bold + Hanken Grotesk Bold (SIL OFL, bundled in Resources/UI/Fonts).
Run:  python3 scripts/branding/generate_logo.py
"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONTS = os.path.join(ROOT, "Assets/GRIDROAM/Resources/UI/Fonts")
OUT = os.path.join(ROOT, "Assets/GRIDROAM/Resources/UI/Branding")
ICON = os.path.join(ROOT, "Assets/GRIDROAM/Art/Store/AppIcon_1024.png")

SORA = os.path.join(FONTS, "Sora-Bold.ttf")
HANKEN = os.path.join(FONTS, "HankenGrotesk-Bold.ttf")

INK = (8, 18, 38, 255)          # deep navy outline
INK_SOFT = (8, 18, 38, 150)
GOLD_TOP = (255, 236, 160)
GOLD_MID = (255, 190, 48)
GOLD_BOT = (236, 116, 18)
TEAL = (30, 196, 178)
TEAL_DARK = (12, 120, 112)
WHITE = (248, 250, 255, 255)

SS = 2  # supersample factor


def vertical_gradient(size, stops):
    """stops: list of (t, (r,g,b))"""
    w, h = size
    grad = Image.new("RGBA", (1, h))
    px = grad.load()
    for y in range(h):
        t = y / max(1, h - 1)
        for i in range(len(stops) - 1):
            t0, c0 = stops[i]
            t1, c1 = stops[i + 1]
            if t0 <= t <= t1:
                k = (t - t0) / max(1e-6, (t1 - t0))
                px[0, y] = tuple(int(c0[j] + (c1[j] - c0[j]) * k) for j in range(3)) + (255,)
                break
    return grad.resize((w, h))


def text_mask(text, font, tracking, size):
    """Renders tracked text into an L mask; returns (mask, width, height_box)."""
    w, h = size
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    x = 0
    asc, desc = font.getmetrics()
    for ch in text:
        d.text((x, 0), ch, font=font, fill=255)
        x += font.getlength(ch) + tracking
    x -= tracking
    bbox = mask.getbbox() or (0, 0, 1, 1)
    return mask.crop((0, 0, int(x) + 2, asc + desc)), bbox


def dilate(mask, radius):
    if radius <= 0:
        return mask
    # MaxFilter needs odd size; apply iteratively for large radius
    out = mask
    remaining = radius
    while remaining > 0:
        step = min(remaining, 6)
        out = out.filter(ImageFilter.MaxFilter(step * 2 + 1))
        remaining -= step
    return out


def make_emblem(diameter):
    """Round badge: globe from the app icon inside a gold + navy ring."""
    d = diameter * SS
    icon = Image.open(ICON).convert("RGBA")
    # crop around the planet
    cx, cy, r = 520, 500, 330
    globe = icon.crop((cx - r, cy - r, cx + r, cy + r)).resize((d, d), Image.LANCZOS)

    badge = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    full = Image.new("L", (d, d), 0)
    ImageDraw.Draw(full).ellipse((0, 0, d - 1, d - 1), fill=255)

    # navy outer disc
    navy = Image.new("RGBA", (d, d), INK)
    badge.paste(navy, (0, 0), full)

    # gold ring (gradient)
    ring_out = int(d * 0.035)
    ring_mask = Image.new("L", (d, d), 0)
    ImageDraw.Draw(ring_mask).ellipse((ring_out, ring_out, d - 1 - ring_out, d - 1 - ring_out), fill=255)
    gold = vertical_gradient((d, d), [(0, GOLD_TOP), (0.5, GOLD_MID), (1, GOLD_BOT)])
    badge.paste(gold, (0, 0), ring_mask)

    # globe inner
    inner = int(d * 0.085)
    inner_mask = Image.new("L", (d, d), 0)
    ImageDraw.Draw(inner_mask).ellipse((inner, inner, d - 1 - inner, d - 1 - inner), fill=255)
    gsize = d - 2 * inner
    g = globe.resize((gsize, gsize), Image.LANCZOS)
    layer = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    layer.paste(g, (inner, inner))
    badge.paste(layer, (0, 0), inner_mask)

    # glossy top highlight
    hl = Image.new("L", (d, d), 0)
    ImageDraw.Draw(hl).ellipse((int(d * 0.16), int(d * 0.10), int(d * 0.84), int(d * 0.52)), fill=60)
    hl = hl.filter(ImageFilter.GaussianBlur(d * 0.03))
    hl = ImageChops.multiply(hl, inner_mask)
    badge = Image.alpha_composite(badge, Image.merge("RGBA", (
        Image.new("L", (d, d), 255), Image.new("L", (d, d), 255), Image.new("L", (d, d), 255), hl)))

    # thin inner navy line between ring and globe
    line = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    ImageDraw.Draw(line).ellipse((inner, inner, d - 1 - inner, d - 1 - inner), outline=INK, width=max(2, int(d * 0.012)))
    badge = Image.alpha_composite(badge, line)
    return badge.resize((diameter, diameter), Image.LANCZOS)


def stroked_word(text, font, tracking, fill_img_fn, stroke, shadow_offset):
    mask, _ = text_mask(text, font, tracking, (font.size * (len(text) + 2), int(font.size * 1.6)))
    pad = stroke + shadow_offset + 8 * SS
    w, h = mask.size
    W, H = w + pad * 2, h + pad * 2
    m = Image.new("L", (W, H), 0)
    m.paste(mask, (pad, pad))

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # drop shadow (hard 3D extrusion + soft)
    outline = dilate(m, stroke)
    soft = outline.filter(ImageFilter.GaussianBlur(6 * SS))
    soft_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    soft_layer.paste(Image.new("RGBA", (W, H), (0, 0, 0, 120)), (0, shadow_offset + 4 * SS), soft)
    out = Image.alpha_composite(out, soft_layer)
    for i in range(shadow_offset, 0, -SS):
        ext = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ext.paste(Image.new("RGBA", (W, H), INK), (0, i), outline)
        out = Image.alpha_composite(out, ext)
    # outline
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ol.paste(Image.new("RGBA", (W, H), INK), (0, 0), outline)
    out = Image.alpha_composite(out, ol)
    # fill
    fill = fill_img_fn((W, H), m)
    fl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fl.paste(fill, (0, 0), m)
    out = Image.alpha_composite(out, fl)
    # top bevel highlight: mask minus shifted mask
    shifted = Image.new("L", (W, H), 0)
    shifted.paste(m, (0, 3 * SS))
    bevel = ImageChops.subtract(m, shifted).filter(ImageFilter.GaussianBlur(1 * SS))
    bl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bl.paste(Image.new("RGBA", (W, H), (255, 255, 255, 170)), (0, 0), bevel)
    out = Image.alpha_composite(out, bl)
    return out.crop(out.getbbox())


def gold_fill(size, mask):
    bbox = mask.getbbox()
    w, h = size
    g = Image.new("RGBA", size, (0, 0, 0, 0))
    gh = bbox[3] - bbox[1]
    grad = vertical_gradient((w, gh), [(0, GOLD_TOP), (0.45, GOLD_MID), (1, GOLD_BOT)])
    g.paste(grad, (0, bbox[1]))
    return g


def white_fill(size, mask):
    bbox = mask.getbbox()
    w, h = size
    g = Image.new("RGBA", size, (0, 0, 0, 0))
    gh = bbox[3] - bbox[1]
    grad = vertical_gradient((w, gh), [(0, (255, 255, 255)), (1, (206, 226, 246))])
    g.paste(grad, (0, bbox[1]))
    return g


def ribbon(text, height):
    h = height * SS
    font = ImageFont.truetype(HANKEN, int(h * 0.50))
    tracking = int(h * 0.09)
    tw = sum(font.getlength(c) + tracking for c in text) - tracking
    padx = int(h * 0.62)
    w = int(tw + padx * 2)
    img = Image.new("RGBA", (w + 8 * SS, h + 12 * SS), (0, 0, 0, 0))
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).rounded_rectangle((4 * SS, 4 * SS, 4 * SS + w, 4 * SS + h), radius=h // 2, fill=255)
    # shadow
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sh.paste(Image.new("RGBA", img.size, INK), (0, 4 * SS), m)
    img = Image.alpha_composite(img, sh)
    grad = vertical_gradient(img.size, [(0, (46, 214, 194)), (1, TEAL_DARK)])
    body = Image.new("RGBA", img.size, (0, 0, 0, 0))
    body.paste(grad, (0, 0), m)
    img = Image.alpha_composite(img, body)
    border = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(border).rounded_rectangle((4 * SS, 4 * SS, 4 * SS + w, 4 * SS + h), radius=h // 2,
                                             outline=(190, 255, 245, 200), width=2 * SS)
    img = Image.alpha_composite(img, border)
    d = ImageDraw.Draw(img)
    x = 4 * SS + padx
    asc, desc = font.getmetrics()
    y = 4 * SS + (h - (asc + desc)) // 2 + int(h * 0.02)
    for c in text:
        d.text((x, y), c, font=font, fill=WHITE)
        x += font.getlength(c) + tracking
    return img


def build_logo(line1, line2, tag, out_path):
    W, H = 1600 * SS, 640 * SS
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    emblem_d = 420
    emblem = make_emblem(emblem_d).resize((emblem_d * SS, emblem_d * SS), Image.LANCZOS)

    f1 = ImageFont.truetype(SORA, 118 * SS)
    f2 = ImageFont.truetype(SORA, 188 * SS)
    w1 = stroked_word(line1, f1, int(14 * SS), white_fill, stroke=9 * SS, shadow_offset=7 * SS)
    w2 = stroked_word(line2, f2, int(4 * SS), gold_fill, stroke=12 * SS, shadow_offset=11 * SS)
    rb = ribbon(tag, 62)

    text_w = max(w1.width, w2.width)
    gap = 34 * SS
    total_w = emblem.width + gap + text_w
    scale = min(1.0, (W - 40 * SS) / total_w)
    if scale < 1.0:
        emblem = emblem.resize((int(emblem.width * scale), int(emblem.height * scale)), Image.LANCZOS)
        w1 = w1.resize((int(w1.width * scale), int(w1.height * scale)), Image.LANCZOS)
        w2 = w2.resize((int(w2.width * scale), int(w2.height * scale)), Image.LANCZOS)
        rb = rb.resize((int(rb.width * scale), int(rb.height * scale)), Image.LANCZOS)
        gap = int(gap * scale)
        text_w = max(w1.width, w2.width)

    stack_h = w1.height + w2.height + rb.height - 30 * SS
    x0 = 20 * SS
    ey = (H - emblem.height) // 2
    tx = x0 + emblem.width + gap
    ty = (H - stack_h) // 2

    # emblem glow
    glow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(glow).ellipse((x0 - 10 * SS, ey - 10 * SS, x0 + emblem.width + 10 * SS, ey + emblem.height + 10 * SS), fill=110)
    glow = glow.filter(ImageFilter.GaussianBlur(26 * SS))
    gl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gl.paste(Image.new("RGBA", (W, H), (255, 190, 60, 255)), (0, 0), glow)
    canvas = Image.alpha_composite(canvas, gl)

    canvas.alpha_composite(emblem, (x0, ey))
    canvas.alpha_composite(w1, (tx + 6 * SS, ty))
    canvas.alpha_composite(w2, (tx, ty + w1.height - 22 * SS))
    canvas.alpha_composite(rb, (tx + 6 * SS, ty + w1.height + w2.height - 30 * SS))

    canvas = canvas.crop(canvas.getbbox())
    final = canvas.resize((canvas.width // SS, canvas.height // SS), Image.LANCZOS)
    final.save(out_path, optimize=True)
    print("wrote", out_path, final.size)


def main():
    os.makedirs(OUT, exist_ok=True)
    build_logo("BİZİM", "DÜNYAMIZ", "AÇIK DÜNYA · İSTANBUL", os.path.join(OUT, "ourworld_logo_tr.png"))
    build_logo("OUR", "WORLD", "OPEN WORLD · ISTANBUL", os.path.join(OUT, "ourworld_logo_en.png"))
    em = make_emblem(512)
    em.save(os.path.join(OUT, "ourworld_emblem.png"), optimize=True)
    print("wrote emblem", em.size)


if __name__ == "__main__":
    main()
