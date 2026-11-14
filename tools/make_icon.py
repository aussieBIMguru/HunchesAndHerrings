import math, random, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

FONTS = {
 "chorus": "/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyrechorus-mediumitalic.otf",
 "bonum": "/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyrebonum-bolditalic.otf",
 "schola": "/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyreschola-bold.otf",
}
INK = (52, 34, 20)

def parchment(S, seed=7):
    rnd = random.Random(seed)
    # base mottled tone
    base = Image.new("RGB", (S, S), (236, 220, 180))
    noise = Image.effect_noise((S, S), 40).convert("L").filter(ImageFilter.GaussianBlur(S/40))
    tint = Image.merge("RGB", [noise.point(lambda v: 200 + v*0.22), noise.point(lambda v: 180 + v*0.2), noise.point(lambda v: 130 + v*0.18)])
    base = Image.blend(base, tint, 0.55)
    # burnt/darkened edges: radial vignette
    vig = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(vig)
    for i in range(60):
        r = i / 60
        inset = int(r * S * 0.18)
        d.rounded_rectangle([inset, inset, S - inset, S - inset], radius=int(S*0.14), fill=int(255 * (r ** 1.6)))
    vig = vig.filter(ImageFilter.GaussianBlur(S/30))
    dark = Image.new("RGB", (S, S), (120, 78, 38))
    base = Image.composite(base, dark, vig)
    # rough edge mask (slightly torn rounded square)
    mask = Image.new("L", (S, S), 0)
    md = ImageDraw.Draw(mask)
    pts = []
    m = S * 0.045
    rad = S * 0.16
    cx = cy = S / 2
    half = S / 2 - m
    for k in range(720):
        t = 2 * math.pi * k / 720
        # superellipse for rounded square
        ct, st = math.cos(t), math.sin(t)
        n = 5.0
        x = half * math.copysign(abs(ct) ** (2 / n), ct)
        y = half * math.copysign(abs(st) ** (2 / n), st)
        j = 1 + (rnd.random() - 0.5) * 0.018 + 0.006 * math.sin(k * 0.37)
        pts.append((cx + x * j, cy + y * j))
    md.polygon(pts, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(max(1, S/256)))
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.paste(base, (0, 0), mask)
    # scorched rim line
    rim = Image.new("RGBA", (S, S), (0,0,0,0))
    rd = ImageDraw.Draw(rim)
    rd.line(pts + [pts[0]], fill=(90, 55, 25, 200), width=max(1, int(S/90)))
    rim = rim.filter(ImageFilter.GaussianBlur(S/300 + 0.3))
    out = Image.alpha_composite(out, rim)
    return out

def make(S, font_key, small=False):
    img = parchment(S)
    d = ImageDraw.Draw(img)
    # double ornamental border
    if not small:
        for inset, w in ((0.13, max(1, S/110)), (0.16, max(1, S/220))):
            i = S * inset
            d.rounded_rectangle([i, i, S - i, S - i], radius=S*0.08, outline=INK + (210,), width=int(round(w)))
        # corner flourishes (small diamonds)
        if S >= 96:
            for (x, y) in ((0.145, 0.145), (0.855, 0.145), (0.145, 0.855), (0.855, 0.855)):
                c = (S*x, S*y); r = S*0.028
                d.polygon([(c[0], c[1]-r), (c[0]+r, c[1]), (c[0], c[1]+r), (c[0]-r, c[1])], fill=INK + (230,))
    # HH monogram
    text = "HH"
    layer = Image.new("RGBA", (S, S), (0,0,0,0))
    ld = ImageDraw.Draw(layer)
    target = S * (0.60 if not small else 0.80)
    size = int(S * 0.7)
    while True:
        font = ImageFont.truetype(FONTS[font_key], size)
        bbox = ld.textbbox((0, 0), text, font=font)
        if bbox[2] - bbox[0] <= target or size < 6:
            break
        size -= 1
    w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]
    x = (S - w)/2 - bbox[0]
    y = (S - h)/2 - bbox[1] - S*0.01
    # faint ink bleed shadow
    ld.text((x + S*0.012, y + S*0.014), text, font=font, fill=(80, 50, 20, 70))
    layer = layer.filter(ImageFilter.GaussianBlur(S/160 + 0.2))
    ld = ImageDraw.Draw(layer)
    ld.text((x, y), text, font=font, fill=INK + (255,), stroke_width=(int(S/40) if small else 0), stroke_fill=INK + (255,))
    # underline swash
    if not small:
        yy = (S + h)/2 + S*0.035
        ld.line([(S*0.27, yy), (S*0.73, yy)], fill=INK + (220,), width=max(1, int(S/120)))
        r = S*0.014
        ld.ellipse([S/2 - r, yy - r, S/2 + r, yy + r], fill=INK + (255,))
    img = Image.alpha_composite(img, layer)
    return img

if __name__ == "__main__":
    key = sys.argv[1]
    big = make(512, key)
    big.save(f"preview_{key}.png")

def build_ico(path, key="chorus"):
    sizes = [16, 24, 32, 48, 64, 128, 256]
    imgs = [make(s * 4, key, small=s <= 32).resize((s, s), Image.LANCZOS) for s in sizes]
    imgs[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
    make(1024, key).resize((512, 512), Image.LANCZOS).save(path.replace(".ico", ".png"))
