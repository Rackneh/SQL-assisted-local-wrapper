import urllib.request, json, subprocess, time

edge_path = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
user_data_dir = r'C:\Users\Rackneh\.gemini\antigravity\brain\351b7b3e-d5bc-4cb3-80d8-9ef18aac12f0\edge_inspect_profile'

proc = subprocess.Popen([
    edge_path,
    '--headless=new',
    '--remote-debugging-port=9223',
    f'--user-data-dir={user_data_dir}',
    '--window-size=1280,800',
    'http://127.0.0.1:5000/'
])

time.sleep(3)

try:
    import websockets, asyncio
    targets_req = urllib.request.urlopen('http://127.0.0.1:9223/json')
    targets = json.loads(targets_req.read().decode())
    page_target = next(t for t in targets if t.get('type') == 'page')
    ws_url = page_target['webSocketDebuggerUrl']

    async def inspect_dom():
        async with websockets.connect(ws_url) as ws:
            req_id = 1
            async def send_cmd(method, params=None):
                nonlocal req_id
                msg_id = req_id
                req_id += 1
                await ws.send(json.dumps({'id': msg_id, 'method': method, 'params': params or {}}))
                while True:
                    resp = json.loads(await ws.recv())
                    if resp.get('id') == msg_id:
                        return resp.get('result', {})

            async def eval_js(expr):
                res = await send_cmd('Runtime.evaluate', {'expression': expr, 'returnByValue': True})
                return res.get('result', {}).get('value')

            # List all visible elements with z-index or windows
            time.sleep(2)
            visible_windows = await eval_js('''
                (() => {
                    const wins = Array.from(document.querySelectorAll('.xp-window, .window, [class*="modal"], [class*="dialog"], [class*="wlm"], [class*="persona"], [class*="chat"]'));
                    return wins.map(w => ({
                        id: w.id,
                        className: w.className,
                        display: window.getComputedStyle(w).display,
                        visibility: window.getComputedStyle(w).visibility,
                        zIndex: window.getComputedStyle(w).zIndex,
                        rect: w.getBoundingClientRect()
                    })).filter(w => w.display !== 'none' && w.visibility !== 'hidden' && w.rect.width > 0 && w.rect.height > 0);
                })()
            ''')
            print('Visible windows / modals on page load:')
            for w in visible_windows:
                print(' ', w)

    asyncio.run(inspect_dom())
finally:
    proc.terminate()
