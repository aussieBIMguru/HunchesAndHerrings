"""Regenerates the Inno Setup wizard artwork (installer/WizardImage*.bmp, WizardSmallImage*.bmp).

Run from the repo root:  python tools/make_installer_art.py
Needs Pillow and make_icon.py (same folder). The calligraphic face is TeX Gyre Chorus; set CHORUS_FONT if it lives elsewhere.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(__file__))
import make_icon as m  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "installer")
CHORUS = os.environ.get("CHORUS_FONT", m.FONTS["chorus"])
ITALIC = os.environ.get("ITALIC_FONT", "/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyrepagella-italic.otf")


def side(w, h):
    tex = m.parchment(1024, seed=11).convert("RGB").resize((w * 3, h * 3)).crop((w, h // 2, w * 2, h // 2 + h))
    base = tex.convert("RGBA")
    vig = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(vig)
    for i in range(40):
        r = i / 40
        inset = int(r * min(w, h) * 0.25)
        d.rectangle([inset, inset, w - inset, h - inset], fill=int(255 * r ** 1.4))
    vig = vig.filter(ImageFilter.GaussianBlur(min(w, h) / 12))
    base = Image.composite(base, Image.new("RGBA", (w, h), (120, 78, 38, 255)), vig)

    logo = m.make(1024, "chorus").resize((int(w * 0.78), int(w * 0.78)), Image.LANCZOS)
    base.alpha_composite(logo, ((w - logo.width) // 2, int(h * 0.08)))
    d = ImageDraw.Draw(base)
    for i, line in enumerate(["Hunches", "&", "Herrings"]):
        f = ImageFont.truetype(CHORUS, int(w * (0.2 if line != "&" else 0.16)))
        bb = d.textbbox((0, 0), line, font=f)
        d.text(((w - (bb[2] - bb[0])) / 2 - bb[0], int(h * 0.48) + i * int(w * 0.2)), line, font=f, fill=(52, 34, 20, 255))
    f = ImageFont.truetype(ITALIC, int(w * 0.075))
    t = "A Victorian mystery"
    bb = d.textbbox((0, 0), t, font=f)
    d.text(((w - (bb[2] - bb[0])) / 2, int(h * 0.86)), t, font=f, fill=(90, 60, 30, 255))
    return base.convert("RGB")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for scale, suffix in ((1, ""), (2, "@2x")):
        side(164 * scale, 314 * scale).save(os.path.join(OUT, f"WizardImage{suffix}.bmp"))
        small = m.make(220, "chorus").resize((55 * scale, 55 * scale), Image.LANCZOS)
        flat = Image.new("RGB", small.size, (255, 255, 255))
        flat.paste(small, (0, 0), small)
        flat.save(os.path.join(OUT, f"WizardSmallImage{suffix}.bmp"))
    print("Wrote wizard images to", OUT)
