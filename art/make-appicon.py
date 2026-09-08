"""Rebuild the app icon: art/AppIcon.png -> src/ForceAutoHDR.App/Assets/AppIcon.ico

    python art/make-appicon.py            # writes the .ico
    python art/make-appicon.py --check    # fails if the committed .ico is out of date

The .ico is the only icon the app has: <ApplicationIcon> embeds it in the exe, and the window
icon is read straight back out of the exe at runtime (MainWindow.SetIconFromExecutable), so
Explorer, the taskbar and Alt-Tab all end up showing this one file. Nothing loads it as a file --
publish does not copy loose assets next to the exe, which is the whole reason for that detour.

Two things here are not the naive way round, and both are on purpose.

Resampling happens in linear light on premultiplied alpha. Averaging sRGB values directly
dims a bright thing on a dark ground -- with a neon ring on near-black this is plainly
visible by 16 px -- and averaging colour that has not been weighted by its own alpha drags
the transparent pixels' colour into the edge.

Sizes up to 64 go in as uncompressed BGRA bitmaps rather than PNG. PNG-in-ICO needs Vista or
later, which is a given here, but plenty of icon readers outside the shell still only expect
it at 256, and at these sizes the compression saves a few kilobytes at most. The three large
entries, where it saves ~180 KB, are PNG.

Requires Pillow and numpy; neither is part of the build, this is run by hand when the art
changes.
"""

import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parent.parent
MASTER = REPO / 'art' / 'AppIcon.png'
ICON = REPO / 'src' / 'ForceAutoHDR.App' / 'Assets' / 'AppIcon.ico'

# 16..48 are the shell's own sizes across 100-300 % scaling; 64/96/128/256 are what Explorer's
# larger views and the Alt-Tab switcher ask for.
BITMAP_SIZES = (16, 20, 24, 32, 40, 48, 64)
PNG_SIZES = (96, 128, 256)


def to_linear(srgb: np.ndarray) -> np.ndarray:
    return np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)


def to_srgb(linear: np.ndarray) -> np.ndarray:
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * linear ** (1 / 2.4) - 0.055)


def downscale(master: Image.Image, size: int) -> Image.Image:
    """Lanczos in linear light, on premultiplied alpha."""
    rgba = np.asarray(master, dtype=np.float64) / 255.0
    alpha = rgba[:, :, 3:4]
    premultiplied = np.concatenate([to_linear(rgba[:, :, :3]) * alpha, alpha], axis=2)

    resized = np.stack([
        np.asarray(Image.fromarray(premultiplied[:, :, c].astype(np.float32), 'F')
                   .resize((size, size), Image.LANCZOS), dtype=np.float64)
        for c in range(4)
    ], axis=2)

    out_alpha = np.clip(resized[:, :, 3:4], 0.0, 1.0)
    # Lanczos overshoots; clipping colour only after unpremultiplying keeps a bright edge
    # bright instead of clamping it against a nearly transparent alpha.
    colour = np.divide(resized[:, :, :3], out_alpha, out=np.zeros_like(resized[:, :, :3]),
                       where=out_alpha > 1e-6)
    colour = to_srgb(np.clip(colour, 0.0, 1.0))

    pixels = np.round(np.concatenate([colour, out_alpha], axis=2) * 255.0).astype(np.uint8)
    return Image.fromarray(pixels, 'RGBA')


def as_bitmap(image: Image.Image) -> bytes:
    """A 32bpp BITMAPINFOHEADER DIB: bottom-up BGRA, then the legacy 1bpp AND mask."""
    width, height = image.size
    bgra = np.asarray(image)[::-1, :, [2, 1, 0, 3]]

    opaque = np.asarray(image)[::-1, :, 3] != 0
    mask = np.packbits(~opaque, axis=1)
    stride = ((width + 31) // 32) * 4
    mask = np.pad(mask, ((0, 0), (0, stride - mask.shape[1])))

    pixels = bgra.tobytes() + mask.tobytes()
    # biHeight is doubled because the DIB covers both the colour image and the mask.
    header = struct.pack('<IiiHHIIiiII', 40, width, height * 2, 1, 32, 0, len(pixels), 0, 0, 0, 0)
    return header + pixels


def build() -> bytes:
    master = Image.open(MASTER).convert('RGBA')
    if master.width != master.height:
        raise SystemExit(f'{MASTER} is {master.size}, expected a square')

    images = []
    for size in sorted(BITMAP_SIZES + PNG_SIZES, reverse=True):
        scaled = downscale(master, size)
        if size in PNG_SIZES:
            from io import BytesIO
            buffer = BytesIO()
            scaled.save(buffer, 'PNG', optimize=True)
            images.append((size, buffer.getvalue()))
        else:
            images.append((size, as_bitmap(scaled)))

    offset = 6 + 16 * len(images)
    directory = b''
    for size, data in images:
        # A width/height byte of 0 means 256 -- which is why the format stops there.
        directory += struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)

    return struct.pack('<HHH', 0, 1, len(images)) + directory + b''.join(d for _, d in images)


if __name__ == '__main__':
    icon = build()
    if '--check' in sys.argv:
        if not ICON.exists() or ICON.read_bytes() != icon:
            raise SystemExit(f'{ICON} is not what {MASTER} produces; rerun make-appicon.py')
        print(f'{ICON.relative_to(REPO)} is up to date')
    else:
        ICON.write_bytes(icon)
        print(f'{ICON.relative_to(REPO)}: {len(icon):,} bytes, {len(BITMAP_SIZES + PNG_SIZES)} sizes')
