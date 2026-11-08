#!/usr/bin/env python3
"""Minimal PE resource-section extractor.
Dumps every resource leaf as <outdir>/<type>_<name>_<lang>.bin plus an index.json.
"""
import json, os, struct, sys

def u16(b, o): return struct.unpack_from('<H', b, o)[0]
def u32(b, o): return struct.unpack_from('<I', b, o)[0]

def main(path, outdir):
    os.makedirs(outdir, exist_ok=True)
    data = open(path, 'rb').read()
    pe = u32(data, 0x3C)
    assert data[pe:pe+4] == b'PE\0\0'
    nsec = u16(data, pe + 6)
    opt_size = u16(data, pe + 20)
    opt = pe + 24
    magic = u16(data, opt)
    dd_base = opt + (96 if magic == 0x10B else 112)
    res_rva = u32(data, dd_base + 2 * 8)
    res_size = u32(data, dd_base + 2 * 8 + 4)
    sec_off = opt + opt_size
    sections = []
    for i in range(nsec):
        o = sec_off + i * 40
        name = data[o:o+8].rstrip(b'\0').decode('ascii', 'replace')
        vsize, vaddr, rsize, raddr = u32(data,o+8), u32(data,o+12), u32(data,o+16), u32(data,o+20)
        sections.append((name, vaddr, vsize, raddr, rsize))

    def rva2off(rva):
        for name, va, vs, ra, rs in sections:
            if va <= rva < va + max(vs, rs):
                return ra + (rva - va)
        return None

    TYPE_NAMES = {1:'CURSOR',2:'BITMAP',3:'ICON',4:'MENU',5:'DIALOG',6:'STRING',
                  7:'FONTDIR',8:'FONT',9:'ACCELERATOR',10:'RCDATA',11:'MESSAGETABLE',
                  12:'GROUP_CURSOR',13:'GROUP_ICON',14:'VERSION',15:'DLGINCLUDE',
                  16:'PLUGPLAY',17:'VXD',18:'ANICURSOR',19:'ANIICON',20:'HTML',21:'MANIFEST'}

    index = []
    count = 0

    def name_of(e):
        nid = e & 0x7FFFFFFF
        if e & 0x80000000:
            noff = res_off + nid
            ln = u16(data, noff)
            return data[noff+2:noff+2+ln*2].decode('utf-16-le', 'replace')
        return nid

    def walk(dir_rva, path_ids):
        nonlocal count
        off = rva2off(dir_rva)
        if off is None: return
        n_named = u16(data, off + 12)
        n_id = u16(data, off + 14)
        for i in range(n_named + n_id):
            e = off + 16 + i * 8
            nid = u32(data, e)
            sub = u32(data, e + 4)
            label = name_of(nid)
            ids = path_ids + [label]
            if sub & 0x80000000:
                walk(res_off_base + (sub & 0x7FFFFFFF), ids)
            else:
                de = rva2off(sub)
                if de is None: continue
                drva, dsize = u32(data, de), u32(data, de + 4)
                doff = rva2off(drva)
                if doff is None or dsize == 0: continue
                blob = data[doff:doff+dsize]
                tname = TYPE_NAMES.get(ids[0], str(ids[0]))
                safe = '_'.join(str(x).replace('/', '_').replace('\\', '_').replace(' ', '_')[:40] for x in ids[1:]) or 'noname'
                fn = f"{count:04d}_{tname}_{safe}.bin"
                open(os.path.join(outdir, fn), 'wb').write(blob)
                index.append({'file': fn, 'type': ids[0], 'name_path': ids, 'size': dsize,
                              'magic': blob[:8].hex()})
                count += 1

    res_off = rva2off(res_rva)
    res_off_base = res_rva  # offsets in resource dir are relative to resource section start (RVA space)
    walk(res_rva, [])
    json.dump(index, open(os.path.join(outdir, 'index.json'), 'w'), indent=1)
    print(f"{count} resources -> {outdir}")

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
