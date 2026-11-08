import os, re, urllib.request

pattern = r'[\'"`](/(?:static|favicon)[^\'"`\s\)\>]+(?:png|jpg|jpeg|gif|ico|wav|bmp|svg))[\'"`]'

found_urls = set()
for root, dirs, files in os.walk('gui/static'):
    for f in files:
        if f.endswith(('.js', '.html', '.css')):
            p = os.path.join(root, f)
            with open(p, 'r', encoding='utf-8', errors='ignore') as fp:
                for match in re.findall(pattern, fp.read()):
                    found_urls.add(match)

print(f'Total unique static asset URLs found: {len(found_urls)}')
missing = []
for url in sorted(found_urls):
    # Check if local file exists
    rel = url.lstrip('/')
    local_path = os.path.join('gui', rel)
    if not os.path.exists(local_path):
        missing.append(url)

print(f'Missing assets count: {len(missing)}')
if missing:
    print('Missing assets:', missing[:20])
else:
    print('ALL ASSETS CONFIRMED PRESENT LOCALLY!')
