"""Clutch uygulama simgesini (app.ico) ve README logosunu üretir.

Logo, UI/Theme.xaml'daki geometriyle aynıdır (24×24 kutu): saat yönünde 300°'lik halka,
ucunda ok başı, ortada yeşil kayıt noktası. Simgede koyu, yuvarlak köşeli bir zemin üzerinde durur.

Kullanım:  python tools/make_icon.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SUPER = 1024  # önce büyük çizilip küçültülür, kenarlar yumuşak olsun

BACKGROUND = (27, 27, 29, 255)
INK = (242, 242, 243, 255)
ACCENT = (118, 185, 0, 255)


def draw_logo(size: int, with_background: bool) -> Image.Image:
    img = Image.new("RGBA", (SUPER, SUPER), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    if with_background:
        d.rounded_rectangle((0, 0, SUPER - 1, SUPER - 1), radius=int(SUPER * 0.22), fill=BACKGROUND)
        scale = SUPER * 0.70 / 24      # logo zeminin %70'i
        offset = SUPER * 0.15
    else:
        scale = SUPER / 24
        offset = 0

    def p(x: float, y: float) -> tuple[float, float]:
        return offset + x * scale, offset + y * scale

    # Halka: merkez (12,12), r=9, saat yönünde tepeden (270°) sol üste (210°) 300°.
    # PIL çizgi kalınlığını kutunun içine doğru çizer; çizginin ortası r'ye denk gelsin diye kutu w/2 büyütülür.
    stroke = 2.2 * scale
    r = 9 * scale
    cx, cy = p(12, 12)
    R = r + stroke / 2
    d.arc((cx - R, cy - R, cx + R, cy + R), start=270, end=570, fill=INK, width=round(stroke))

    # Sol üstteki uç yuvarlak
    x, y = p(4.2, 7.5)
    h = stroke / 2
    d.ellipse((x - h, y - h, x + h, y + h), fill=INK)

    # Tepedeki uçta saat yönünün tersini (geri sarma) gösteren ok başı
    d.polygon([p(12, 0.6), p(8.8, 3), p(12, 5.4)], fill=INK)

    # Kayıt noktası
    dr = 3.6 * scale
    d.ellipse((cx - dr, cy - dr, cx + dr, cy + dr), fill=ACCENT)

    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    icon = draw_logo(256, with_background=True)
    icon_path = ROOT / "src" / "Clutch" / "app.ico"
    icon.save(icon_path, format="ICO", sizes=[(s, s) for s in sizes])

    docs = ROOT / "docs"
    docs.mkdir(exist_ok=True)
    draw_logo(512, with_background=True).save(docs / "logo.png")

    print(f"{icon_path} ve {docs / 'logo.png'} yazıldı")


if __name__ == "__main__":
    main()
