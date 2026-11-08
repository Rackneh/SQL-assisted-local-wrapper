#!/usr/bin/env python3
"""Convert raw DIB resources (from rip_resources.py) to PNG via PIL."""
import io, json, os, struct, sys
from PIL import Image

def dib_to_png(blob):
    if len(blob) < 40: return None
    hsize = struct.unpack_from('<I', blob, 0)[0]
    if hsize not in (40, 52, 56, 108, 124): return None
    w = struct.unpack_from('<i', blob, 4)[0]
    h = struct.unpack_from('<i', blob, 8)[0]
    bpp = struct.unpack_from('<H', blob, 14)[0]
    comp = struct.unpack_from('<I', blob, 16)[0]
    clrused = struct.unpack_from('<I', blob, 32)[0]
    if w <= 0 or abs(h) <= 0 or bpp not in (24, 32, 8, 16, 4, 1): return None
    topdown = h < 0
    h = abs(h)
    off = hsize
    if comp == 3:  # BI_BITFIELDS: masks follow header (or in header for V4/V5)
        if hsize == 40: off += 12
    ncolors = clrused or (1 << bpp if bpp <= 8 else 0)
    pal = b''
    if ncolors:
        pal = blob[off:off + ncolors * 4]
        off += ncolors * 4
    px = blob[off:]
    stride = ((w * bpp + 31) // 32) * 4
    if len(px) < stride * h: return None
    # BMP file = 14-byte file header + DIB info header + masks + palette + pixels.
    # blob[0:off] already contains header+masks+palette; pixels follow.
    dataoff = 14 + off
    filesize = dataoff + stride * h
    hdr = b'BM' + struct.pack('<IHHI', filesize, 0, 0, dataoff)
    bmp = hdr + blob[:off] + px
    try:
        im = Image.open(io.BytesIO(bmp))
        im.load()
        if im.mode == 'P': im = im.convert('RGBA')
        elif im.mode == 'RGB': im = im.convert('RGBA')
        return im
    except Exception:
        return None

def main(srcdir, dstdir):
    os.makedirs(dstdir, exist_ok=True)
    ok = fail = 0
    for fn in sorted(os.listdir(srcdir)):
        if not fn.endswith('.bin'): continue
        blob = open(os.path.join(srcdir, fn), 'rb').read()
        im = dib_to_png(blob)
        if im is None:
            fail += 1
            continue
        out = os.path.join(dstdir, fn[:-4] + '.png')
        im.save(out)
        ok += 1
    print(f"{ok} converted, {fail} skipped")

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
