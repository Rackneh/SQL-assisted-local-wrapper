#!/usr/bin/env python3
"""Reconstruct .ico files from rip_resources.py output of a PE with icons.
RT_GROUP_ICON (type 14) resources point at RT_ICON (type 3) image data.
.ico image data == RT_ICON blob verbatim (DIB XOR/AND masks, no file header).
"""
import json, os, struct, sys

def main(srcdir, dstdir):
    os.makedirs(dstdir, exist_ok=True)
    idx = json.load(open(os.path.join(srcdir, 'index.json')))
    icons = {}   # numeric id -> bytes
    groups = []  # (name_path, bytes)
    for e in idx:
        p = os.path.join(srcdir, e['file'])
        if e['type'] == 3 and len(e['name_path']) > 1:
            try: icons[int(e['name_path'][1])] = open(p, 'rb').read()
            except ValueError: pass
        elif e['type'] == 14 and len(e['name_path']) > 1:
            groups.append((str(e['name_path'][1]), open(p, 'rb').read()))
    made = 0
    for gid, blob in groups:
        if len(blob) < 6: continue
        res, typ, count = struct.unpack_from('<HHH', blob, 0)
        entries = []
        datas = []
        ok = True
        for i in range(count):
            o = 6 + i * 14
            if o + 14 > len(blob): ok = False; break
            w, h, cc, r, planes, bpp, sz, iid = struct.unpack_from('<BBBBHHIH', blob, o)
            img = icons.get(iid)
            if img is None or len(img) < sz:
                ok = False; break
            entries.append(struct.pack('<BBBBHHII', w, h, cc, r, planes, bpp, sz, len(datas) and 0 or 0))
            datas.append(img[:sz])
        if not ok: continue
        # fix image offsets: ICONDIR(6) + count*16
        base = 6 + count * 16
        offs = []
        cur = base
        for d in datas:
            offs.append(cur); cur += len(d)
        out = bytearray()
        out += struct.pack('<HHH', 0, 1, count)
        for i, (w, h, cc, r, planes, bpp, sz, iid) in enumerate(
                [struct.unpack_from('<BBBBHHIH', blob, 6 + j * 14) for j in range(count)]):
            out += struct.pack('<BBBBHHII', w, h, cc, r, planes, bpp, len(datas[i]), offs[i])
        for d in datas: out += d
        fn = os.path.join(dstdir, f'icon_{gid}.ico')
        open(fn, 'wb').write(bytes(out))
        made += 1
    print(f'{made} icons reconstructed')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
