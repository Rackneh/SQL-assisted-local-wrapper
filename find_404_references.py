import os

targets = ['mspaint_1.png', 'calc_120.png', 'mmsys_100.png', 'appwiz_1.png', 'cmd_1.png', 'msn.ico']

for root, dirs, files in os.walk('gui'):
    for f in files:
        if f.endswith(('.js', '.html', '.css', '.py')):
            p = os.path.join(root, f)
            with open(p, 'r', encoding='utf-8', errors='ignore') as fp:
                content = fp.read()
                for t in targets:
                    if t in content:
                        print(f'{p} references {t}')
