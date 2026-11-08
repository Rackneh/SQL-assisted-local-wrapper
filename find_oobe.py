with open(r'D:\Software\Windows\Windows\Windows.XP.Professional.SP3.x86\I386\txtsetup.sif', 'r', errors='ignore') as f:
    lines = f.readlines()

oobe_files = []
for line in lines:
    parts = line.strip().split('=')
    if len(parts) == 2:
        fname = parts[0].strip()
        rhs = parts[1].strip()
        rhs_parts = [p.strip() for p in rhs.split(',')]
        if len(rhs_parts) >= 8:
            dir_id = rhs_parts[7]
            if dir_id in ['63', '70', '71', '72', '73', '74', '75', '76', '77']:
                oobe_files.append((fname, dir_id))

print(f'Total OOBE files found in txtsetup.sif: {len(oobe_files)}')
for f, d in oobe_files[:50]:
    print(f'{f} -> dir {d}')
