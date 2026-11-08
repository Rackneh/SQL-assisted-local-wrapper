import subprocess, time, json, os, urllib.request, base64

edge_path = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
art_dir = r'C:\Users\Rackneh\.gemini\antigravity\brain\351b7b3e-d5bc-4cb3-80d8-9ef18aac12f0'
user_data_dir = r'C:\Users\Rackneh\.gemini\antigravity\brain\351b7b3e-d5bc-4cb3-80d8-9ef18aac12f0\edge_wlm_fix_profile'
os.makedirs(user_data_dir, exist_ok=True)

proc = subprocess.Popen([
    edge_path,
    '--headless=new',
    '--remote-debugging-port=9224',
    f'--user-data-dir={user_data_dir}',
    '--window-size=1280,800',
    'http://127.0.0.1:5000/'
])

time.sleep(3)

try:
    import websockets, asyncio
    targets_req = urllib.request.urlopen('http://127.0.0.1:9224/json')
    targets = json.loads(targets_req.read().decode())
    page_target = next(t for t in targets if t.get('type') == 'page')
    ws_url = page_target['webSocketDebuggerUrl']

    async def run_test():
        async with websockets.connect(ws_url, max_size=20*1024*1024) as ws:
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

            async def capture_screen(filename):
                res = await send_cmd('Page.captureScreenshot', {'format': 'png'})
                img_data = base64.b64decode(res['data'])
                out_path = os.path.join(art_dir, filename)
                with open(out_path, 'wb') as f:
                    f.write(img_data)
                print(f'Captured: {filename} ({len(img_data)} bytes)')

            time.sleep(2)

            # 1. Check standby screen before booting workstation
            custom_win_state = await eval_js('''
                (() => {
                    const el = document.getElementById('unifiedWlmCustomWindow');
                    if (!el) return { exists: false };
                    const style = window.getComputedStyle(el);
                    return {
                        exists: true,
                        display: style.display,
                        visibility: style.visibility,
                        zIndex: style.zIndex,
                        rect: el.getBoundingClientRect()
                    };
                })()
            ''')
            print('Initial customWin state on page load:', custom_win_state)
            assert custom_win_state['display'] == 'none', f"Expected display: none, got {custom_win_state['display']}"
            await capture_screen('xp_fixed_wlm_01_standby_clean.png')

            # 2. Boot workstation or hide standby and check desktop
            await eval_js('''
                const standby = document.getElementById('xpStandbyScreen');
                if (standby) standby.style.display = 'none';
                if (window.XPCore && window.XPCore.stopScreensaver) window.XPCore.stopScreensaver();
            ''')
            time.sleep(1)

            desktop_custom_state = await eval_js('''
                (() => {
                    const el = document.getElementById('unifiedWlmCustomWindow');
                    return el ? window.getComputedStyle(el).display : 'none';
                })()
            ''')
            print('Desktop customWin display:', desktop_custom_state)
            assert desktop_custom_state == 'none', f"Expected display: none on desktop, got {desktop_custom_state}"
            await capture_screen('xp_fixed_wlm_02_desktop_clean.png')

            # 3. Test on-demand opening of Personalization dialog when user clicks theme button
            print('Opening Personalization on demand...')
            await eval_js('''
                const themeBtn = document.getElementById('wlmMainThemeBtn');
                if (themeBtn) themeBtn.click();
            ''')
            time.sleep(1)
            opened_state = await eval_js('''
                (() => {
                    const el = document.getElementById('unifiedWlmCustomWindow');
                    return el ? window.getComputedStyle(el).display : 'none';
                })()
            ''')
            print('Personalization display after clicking theme button:', opened_state)
            assert opened_state == 'flex', f"Expected display: flex when opened on demand, got {opened_state}"
            await capture_screen('xp_fixed_wlm_03_personalization_on_demand.png')

            # 4. Test closing Personalization dialog
            print('Closing Personalization dialog...')
            await eval_js('''
                const closeBtn = document.getElementById('customWinCloseBtn');
                if (closeBtn) closeBtn.click();
            ''')
            time.sleep(0.5)
            closed_state = await eval_js('''
                (() => {
                    const el = document.getElementById('unifiedWlmCustomWindow');
                    return el ? window.getComputedStyle(el).display : 'none';
                })()
            ''')
            print('Personalization display after close click:', closed_state)
            assert closed_state == 'none', f"Expected display: none after close, got {closed_state}"

            print('All WLM Personalization tests passed successfully!')

    asyncio.run(run_test())

finally:
    proc.terminate()
