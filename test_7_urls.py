import urllib.request

urls = [
    'http://127.0.0.1:5000/static/xp_assets/icons/mspaint_1.png',
    'http://127.0.0.1:5000/static/xp_assets/icons/calc_120.png',
    'http://127.0.0.1:5000/static/xp_assets/icons/mmsys_100.png',
    'http://127.0.0.1:5000/static/xp_assets/icons/appwiz_1.png',
    'http://127.0.0.1:5000/static/wlm/Icons/msn.ico',
    'http://127.0.0.1:5000/static/xp_assets/icons/cmd_1.png',
    'http://127.0.0.1:5000/favicon.ico'
]

all_ok = True
for u in urls:
    try:
        r = urllib.request.urlopen(u)
        ct = r.headers.get('content-type')
        print(f'{u.split("5000")[1]} -> HTTP {r.status} (Type: {ct})')
    except Exception as e:
        print(f'{u.split("5000")[1]} -> FAILED: {e}')
        all_ok = False

if all_ok:
    print('ALL 7 PREVIOUS 404 ENDPOINTS NOW RETURN HTTP 200!')
