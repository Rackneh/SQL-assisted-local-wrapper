import shutil, os

icons_dir = r'gui\static\xp_assets\icons'
wlm_icons_dir = r'gui\static\wlm\Icons'

copies = [
    (os.path.join(icons_dir, 'mspaint.png'), os.path.join(icons_dir, 'mspaint_1.png')),
    (os.path.join(icons_dir, 'calc.png'), os.path.join(icons_dir, 'calc_120.png')),
    (os.path.join(icons_dir, 'mmsys.png'), os.path.join(icons_dir, 'mmsys_100.png')),
    (os.path.join(icons_dir, 'appwiz.png'), os.path.join(icons_dir, 'appwiz_1.png')),
    (os.path.join(icons_dir, 'cmd.png'), os.path.join(icons_dir, 'cmd_1.png')),
    (os.path.join(wlm_icons_dir, 'app.ico'), os.path.join(wlm_icons_dir, 'msn.ico')),
    (os.path.join(r'gui\static\xp_assets\msgina', 'icon_1.ico'), r'gui\static\favicon.ico')
]

for src, dst in copies:
    if os.path.exists(src):
        shutil.copy2(src, dst)
        print(f'Copied {src} -> {dst}')
    else:
        print(f'WARNING: Source not found: {src}')

print('All missing icon files created!')
