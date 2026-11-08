import subprocess, time, json, os, urllib.request, base64

edge_path = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
art_dir = r'C:\Users\Rackneh\.gemini\antigravity\brain\351b7b3e-d5bc-4cb3-80d8-9ef18aac12f0'
user_data_dir = r'C:\Users\Rackneh\.gemini\antigravity\brain\351b7b3e-d5bc-4cb3-80d8-9ef18aac12f0\edge_disc_test_profile'
os.makedirs(user_data_dir, exist_ok=True)

# Launch Edge with remote debugging
proc = subprocess.Popen([
    edge_path,
    '--headless=new',
    '--remote-debugging-port=9222',
    f'--user-data-dir={user_data_dir}',
    '--window-size=1280,800',
    'http://127.0.0.1:5000/'
])

time.sleep(3)

try:
    # Discover targets
    targets_req = urllib.request.urlopen('http://127.0.0.1:9222/json')
    targets = json.loads(targets_req.read().decode())
    page_target = next(t for t in targets if t.get('type') == 'page')
    ws_url = page_target['webSocketDebuggerUrl']
    print('Connected to page target via WebSocket:', ws_url)

    # Use websocket-client or custom simple WS to send CDP commands
    import asyncio
    import websockets

    async def run_cdp_tests():
        async with websockets.connect(ws_url, max_size=20*1024*1024) as ws:
            req_id = 1

            async def send_cmd(method, params=None):
                nonlocal req_id
                msg_id = req_id
                req_id += 1
                payload = {'id': msg_id, 'method': method, 'params': params or {}}
                await ws.send(json.dumps(payload))
                while True:
                    resp_raw = await ws.recv()
                    resp = json.loads(resp_raw)
                    if resp.get('id') == msg_id:
                        return resp.get('result', {})

            async def eval_js(expression):
                res = await send_cmd('Runtime.evaluate', {
                    'expression': expression,
                    'awaitPromise': True,
                    'returnByValue': True
                })
                return res.get('result', {}).get('value')

            async def capture_screen(filename):
                res = await send_cmd('Page.captureScreenshot', {'format': 'png'})
                img_data = base64.b64decode(res['data'])
                out_path = os.path.join(art_dir, filename)
                with open(out_path, 'wb') as f:
                    f.write(img_data)
                print(f'Captured screenshot: {filename} ({len(img_data)} bytes)')

            # Wait for app to be ready
            time.sleep(2)
            await eval_js("console.log('Test harness loaded')")

            # 1. Close standby/screensaver if active
            await eval_js('''
              if (document.getElementById('xpStandbyScreen')) {
                document.getElementById('xpStandbyScreen').style.display = 'none';
              }
              if (window.XPCore && window.XPCore.stopScreensaver) {
                window.XPCore.stopScreensaver();
              }
            ''')

            # 2. Test Explorer Browsing Windows XP CD-ROM Drive (E:)
            print('Navigating Explorer to E: CD-ROM drive...')
            await eval_js('''
              if (window.XPApps && window.XPApps.openExplorer) {
                window.XPApps.openExplorer('E:');
              }
            ''')
            time.sleep(2)
            await capture_screen('xp_disc_01_explorer_cdrom.png')

            # 3. Test Windows XP CD-ROM Autorun Launcher (SETUP.EXE)
            print('Opening Windows XP CD-ROM Autorun Launcher...')
            await eval_js('''
              if (window.XPApps && window.XPApps.openAutorun) {
                window.XPApps.openAutorun();
              }
            ''')
            time.sleep(1)
            await capture_screen('xp_disc_02_autorun_launcher.png')

            # 4. Test Display Properties CRT Monitor (185x163 genuine monitor graphic)
            print('Opening Display Properties with CRT Monitor...')
            await eval_js('''
              const ar = document.getElementById('unifiedAutorunWindow');
              if (ar) ar.style.display = 'none';
              if (window.XPApps && window.XPApps.openDisplayProperties) {
                window.XPApps.openDisplayProperties();
              }
            ''')
            time.sleep(1)
            await capture_screen('xp_disc_03_display_crt_monitor.png')

            # 5. Test Search Companion with Rover/Merlin
            print('Opening Search Companion...')
            await eval_js('''
              const dp = document.getElementById('unifiedDisplayPropertiesWindow');
              if (dp) dp.style.display = 'none';
              if (window.XPApps && window.XPApps.openSearchCompanion) {
                window.XPApps.openSearchCompanion();
              }
            ''')
            time.sleep(1)
            await capture_screen('xp_disc_04_search_companion.png')

            # 6. Test Windows XP GUI Setup (Phase 2 Billboard & Wizard)
            print('Testing Windows XP GUI Setup Billboard...')
            await eval_js('''
              const sc = document.getElementById('unifiedSearchCompanionWindow');
              if (sc) sc.style.display = 'none';
              if (window.XPInstaller && window.XPInstaller.startSetup) {
                // Trigger GUI Setup directly
                window.XPInstaller.startSetup();
              }
            ''')
            time.sleep(1.5)
            # Advance past text-mode or trigger GUI stage
            await eval_js('''
              const setupCont = document.getElementById('xpSetupScreen');
              if (setupCont) {
                // If text mode, press enter or invoke proceed
                const enterBtn = document.querySelector('.text-mode-btn');
                if (enterBtn) enterBtn.click();
              }
            ''')
            time.sleep(2)
            await capture_screen('xp_disc_05_gui_setup_billboard.png')

            # 7. Test OOBE Stage with User Account Tile Avatar Selector
            print('Testing OOBE Stage with User Account Tiles...')
            await eval_js('''
              // Trigger OOBE stage
              const overlay = document.getElementById('xpBootSequenceOverlay');
              if (overlay) overlay.style.display = 'none';
              // Check if we can open OOBE or open User Accounts tile picker
              if (window.XPApps && window.XPApps.openUserAccounts) {
                const sScreen = document.getElementById('xpSetupScreen');
                if (sScreen) sScreen.style.display = 'none';
                window.XPApps.openUserAccounts();
              }
            ''')
            time.sleep(1.5)
            await capture_screen('xp_disc_06_user_accounts_tiles.png')

            print('All disc test screenshots completed!')

    asyncio.run(run_cdp_tests())

finally:
    proc.terminate()
    try:
        proc.wait(timeout=3)
    except:
        proc.kill()
