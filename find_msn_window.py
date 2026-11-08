import re, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')

with open('gui/static/index.html', 'r', encoding='utf-8') as f:
    text_html = f.read()

print('=== index.html IDs ===')
for m in re.finditer(r'id=["\']([^"\']+)["\']', text_html):
    i = m.group(1)
    if any(k in i.lower() for k in ['wlm', 'msn', 'persona', 'personaliz', 'chat', 'contact']):
        print(' ', i)

with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text_app = f.read()

print('\n=== app.js matching window creates ===')
for m in re.finditer(r'id=["\']([^"\']*(?:wlm|msn|persona|personaliz|chat|contact)[^"\']*)["\']', text_app, re.IGNORECASE):
    print(' ', m.group(1))

print('\n=== wlm_client.js ===')
with open('gui/static/js/wlm_client.js', 'r', encoding='utf-8') as f:
    text_wlm = f.read()
for m in re.finditer(r'id=["\']([^"\']*(?:wlm|msn|persona|personaliz|chat|contact)[^"\']*)["\']', text_wlm, re.IGNORECASE):
    print(' ', m.group(1))
