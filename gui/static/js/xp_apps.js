/**

 * Windows XP Simulator - Built-in Applications Subsystem (xp_apps.js)

 * Implements: Display Properties (desk.cpl), Windows Task Manager (taskmgr.exe),

 * Multi-Drive File Explorer (explorer.exe), Command Prompt (cmd.exe),

 * Notepad (notepad.exe), and the Run Dialog (rundll32.exe).

 */



window.XPApps = (function() {



  // -------------------------------------------------------------------------

  // 1. Display Properties / Personalization Screen (desk.cpl)

  // -------------------------------------------------------------------------

  let displayPropWin = null;



  function openDisplayProperties() {

    if (!displayPropWin) {

      displayPropWin = document.createElement('div');

      displayPropWin.className = 'xp-window xp-display-properties-window';

      displayPropWin.id = 'unifiedDisplayPropertiesWindow';

      displayPropWin.style.left = '160px';

      displayPropWin.style.top = '40px';

      displayPropWin.style.width = '420px';

      displayPropWin.style.height = '490px';



      displayPropWin.innerHTML = `

        <div class="title-bar" id="dispPropTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/desk_100.png" class="xp-title-icon" alt="Display">

            <span>Display Properties</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="dispPropCloseBtn" title="Close"></button>

          </div>

        </div>



        <div class="xp-dialog-tabs">

          <button class="xp-dialog-tab-btn active" data-tab="tabThemes">Themes</button>

          <button class="xp-dialog-tab-btn" data-tab="tabDesktop">Desktop</button>

          <button class="xp-dialog-tab-btn" data-tab="tabScreenSaver">Screen Saver</button>

          <button class="xp-dialog-tab-btn" data-tab="tabAppearance">Appearance</button>

          <button class="xp-dialog-tab-btn" data-tab="tabSettings">Settings</button>

        </div>



        <div class="xp-dialog-body" style="padding:10px 14px; flex:1; overflow-y:auto; background:#ece9d8;">

          <!-- Tab 1: Themes -->

          <div id="tabThemes" class="xp-tab-panel">

            <div style="font-size:11px; margin-bottom:10px; color:#1e293b;">

              A theme is a background plus a set of sounds, icons, and other elements to help you personalize your computer with one click.

            </div>

            <div style="margin-bottom:8px;">

              <label style="font-size:11px; font-weight:700;">Theme:</label>

              <select id="themeSelectDropdown" class="form-select" style="width:100%; margin-top:3px; padding:3px; font-size:11px;">

                <option value="luna-blue">Windows XP (Default / Luna Blue)</option>

                <option value="luna-olive">Windows XP (Olive Green)</option>

                <option value="luna-silver">Windows XP (Silver)</option>

                <option value="royale">Windows XP Royale (Energy Blue)</option>

                <option value="zune">Windows XP Zune (Noir)</option>

                <option value="classic">Windows Classic</option>

              </select>

            </div>

            <!-- Monitor Preview Box -->

            <div class="xp-cpl-monitor-box">

              <div class="xp-cpl-crt-frame">

                <div class="xp-cpl-crt-screen" id="themePreviewScreen" style="background-image:url('/static/img/bliss.jpg');">

                  <div class="xp-cpl-sample-window" id="themeSampleWin">

                    <div class="xp-cpl-sample-title" id="themeSampleTitle">Active Window</div>

                    <div style="padding:4px; font-size:9px; background:#ece9d8;">Window Text</div>

                  </div>

                </div>

              </div>

            </div>

          </div>



          <!-- Tab 2: Desktop Wallpapers -->

          <div id="tabDesktop" class="xp-tab-panel" style="display:none;">

            <div class="xp-cpl-monitor-box" style="margin-bottom:8px;">

              <div class="xp-cpl-crt-frame">

                <div class="xp-cpl-crt-screen" id="wallpaperPreviewScreen" style="background-image:url('/static/img/bliss.jpg');"></div>

              </div>

            </div>

            <div style="display:flex; gap:12px; margin-top:8px;">

              <div style="flex:1;">

                <label style="font-size:11px; font-weight:700;">Background:</label>

                <select id="wallpaperSelectDropdown" size="5" style="width:100%; height:90px; margin-top:3px; font-size:11px; border:1px solid #716f64; background:#ffffff;">

                  <option value="/static/img/bliss.jpg" selected>Bliss</option>

                  <option value="/static/wlm/wlm/scenes/0002.jpg">Autumn</option>

                  <option value="/static/wlm/wlm/background_blue.jpg">Azul</option>

                  <option value="/static/wlm/wlm/background_red.jpg">Red Moon Desert</option>

                  <option value="/static/wlm/wlm/scenes/0004.png">Green Field</option>

                  <option value="/static/wlm/wlm/scenes/0007.png">Future City</option>

                  <option value="/static/wlm/wlm/scenes/Graffiti.jpg">Graffiti Street</option>

                  <option value="/static/wlm/wlm/scenes/zune_04.jpg">Zune Skulls</option>

                </select>

              </div>

              <div style="width:120px; display:flex; flex-direction:column; gap:6px;">

                <label style="font-size:11px; font-weight:700;">Position:</label>

                <select id="wallpaperPositionSelect" style="width:100%; font-size:11px; padding:2px;">

                  <option value="cover" selected>Stretch</option>

                  <option value="contain">Fit</option>

                  <option value="auto">Center</option>

                </select>

                <button class="btn btn-secondary btn-sm" id="btnBrowseWallpaper" style="margin-top:6px; font-size:10px;">Browse...</button>

              </div>

            </div>

          </div>



          <!-- Tab 3: Screen Saver -->

          <div id="tabScreenSaver" class="xp-tab-panel" style="display:none;">

            <div class="xp-cpl-monitor-box" style="margin-bottom:8px;">

              <div class="xp-cpl-crt-frame">

                <canvas id="saverPreviewCanvas" width="160" height="110" style="background:#000000; border-radius:3px;"></canvas>

              </div>

            </div>

            <div style="display:flex; align-items:center; gap:8px; margin-top:8px;">

              <label style="font-size:11px; font-weight:700;">Screen saver:</label>

              <select id="screensaverSelectDropdown" style="flex:1; padding:3px; font-size:11px;">

                <option value="pipes" selected>3D Pipes</option>

                <option value="starfield">Starfield (Hyperspace)</option>

                <option value="flying_windows">3D Flying Windows</option>

              </select>

              <button class="btn btn-secondary btn-sm" id="btnPreviewScreensaver">Preview</button>

            </div>

            <div style="font-size:11px; margin-top:10px; display:flex; align-items:center; gap:6px;">

              <label>Wait:</label>

              <input type="number" value="10" min="1" max="99" style="width:45px; padding:2px; font-size:11px;">

              <span>minutes</span>

            </div>

          </div>



          <!-- Tab 4: Appearance -->

          <div id="tabAppearance" class="xp-tab-panel" style="display:none;">

            <div class="xp-cpl-monitor-box" style="margin-bottom:8px;">

              <div class="xp-cpl-crt-frame">

                <div class="xp-cpl-crt-screen" id="appearancePreviewScreen">

                  <div class="xp-cpl-sample-window active" id="appSampleActive">

                    <div class="xp-cpl-sample-title">Active Window</div>

                    <div style="padding:4px; font-size:9px; background:#ece9d8;">Message Text</div>

                  </div>

                </div>

              </div>

            </div>

            <div style="margin-bottom:8px;">

              <label style="font-size:11px; font-weight:700;">Windows and buttons:</label>

              <select id="winButtonsStyleSelect" style="width:100%; padding:3px; font-size:11px;">

                <option value="xp" selected>Windows XP style</option>

                <option value="classic">Windows Classic style</option>

              </select>

            </div>

            <div>

              <label style="font-size:11px; font-weight:700;">Color scheme:</label>

              <select id="colorSchemeSelect" style="width:100%; padding:3px; font-size:11px;">

                <option value="luna-blue" selected>Default (blue)</option>

                <option value="luna-olive">Olive Green</option>

                <option value="luna-silver">Silver</option>

                <option value="royale">Royale (Energy Blue)</option>

                <option value="zune">Zune (Noir)</option>

              </select>

            </div>

          </div>



          <!-- Tab 5: Settings -->

          <div id="tabSettings" class="xp-tab-panel" style="display:none;">

            <div style="font-size:11px; margin-bottom:8px;">Display: Plug and Play Monitor on NVIDIA Nemotron Dory Adapter</div>

            <div style="margin-bottom:12px;">

              <label style="font-size:11px; font-weight:700;">Screen resolution:</label>

              <input type="range" min="1" max="4" value="3" id="screenResRange" style="width:100%; margin-top:4px;">

              <div style="font-size:11px; text-align:center; color:#1e293b; margin-top:2px;" id="screenResLabel">1280 by 800 pixels</div>

            </div>

            <div>

              <label style="font-size:11px; font-weight:700;">Color quality:</label>

              <select style="width:100%; padding:3px; font-size:11px;">

                <option selected>Highest (32 bit)</option>

                <option>Medium (16 bit)</option>

              </select>

            </div>

          </div>

        </div>



        <div class="xp-dialog-footer" style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end; gap:6px;">

          <button class="btn btn-primary btn-sm" id="dispPropOkBtn" style="min-width:70px; background:#0284c7; color:#fff;">OK</button>

          <button class="btn btn-secondary btn-sm" id="dispPropCancelBtn" style="min-width:70px;">Cancel</button>

          <button class="btn btn-secondary btn-sm" id="dispPropApplyBtn" style="min-width:70px;">Apply</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(displayPropWin);

      window.XPCore.makeDraggable(displayPropWin, displayPropWin.querySelector('#dispPropTitleHandle'));

      window.XPCore.makeResizable(displayPropWin, 380, 420);



      // Tab switching

      displayPropWin.querySelectorAll('.xp-dialog-tab-btn').forEach(btn => {

        btn.addEventListener('click', () => {

          displayPropWin.querySelectorAll('.xp-dialog-tab-btn').forEach(b => b.classList.remove('active'));

          displayPropWin.querySelectorAll('.xp-tab-panel').forEach(p => p.style.display = 'none');

          btn.classList.add('active');

          const panel = displayPropWin.querySelector(`#${btn.dataset.tab}`);

          if (panel) panel.style.display = 'block';



          if (btn.dataset.tab === 'tabScreenSaver') {

            const canvas = displayPropWin.querySelector('#saverPreviewCanvas');

            const saverMode = displayPropWin.querySelector('#screensaverSelectDropdown').value;

            window.XPCore.startScreensaver(saverMode, canvas);

          }

        });

      });



      // Close / Cancel

      const closeDisp = () => { displayPropWin.style.display = 'none'; };

      displayPropWin.querySelector('#dispPropCloseBtn').addEventListener('click', closeDisp);

      displayPropWin.querySelector('#dispPropCancelBtn').addEventListener('click', closeDisp);



      // Apply / OK

      const applySelection = () => {

        const theme = displayPropWin.querySelector('#themeSelectDropdown').value;

        const wallpaper = displayPropWin.querySelector('#wallpaperSelectDropdown').value;

        window.XPCore.applyTheme(theme, wallpaper);

        window.XPCore.Audio.playDing();

      };



      displayPropWin.querySelector('#dispPropApplyBtn').addEventListener('click', applySelection);

      displayPropWin.querySelector('#dispPropOkBtn').addEventListener('click', () => {

        applySelection();

        closeDisp();

      });



      // Color scheme & theme synchronization in Appearance tab

      const colorSchemeSelect = displayPropWin.querySelector('#colorSchemeSelect');

      if (colorSchemeSelect) {

        colorSchemeSelect.addEventListener('change', (e) => {

          const themeSelect = displayPropWin.querySelector('#themeSelectDropdown');

          if (themeSelect) themeSelect.value = e.target.value;

          const themeScreen = displayPropWin.querySelector('#themePreviewScreen');

          if (themeScreen && window.XPCore.THEMES[e.target.value]) {

            themeScreen.style.backgroundColor = window.XPCore.THEMES[e.target.value].previewColor;

          }

        });

      }



      // Theme dropdown updates colorSchemeSelect

      const themeSelect = displayPropWin.querySelector('#themeSelectDropdown');

      if (themeSelect) {

        themeSelect.addEventListener('change', (e) => {

          if (colorSchemeSelect) colorSchemeSelect.value = e.target.value;

          const themeScreen = displayPropWin.querySelector('#themePreviewScreen');

          if (themeScreen && window.XPCore.THEMES[e.target.value]) {

            themeScreen.style.backgroundColor = window.XPCore.THEMES[e.target.value].previewColor;

          }

        });

      }



      // Wallpaper select preview

      displayPropWin.querySelector('#wallpaperSelectDropdown').addEventListener('change', (e) => {

        const url = e.target.value;

        displayPropWin.querySelector('#wallpaperPreviewScreen').style.backgroundImage = `url('${url}')`;

      });



      // Screensaver live preview & full-screen button

      displayPropWin.querySelector('#screensaverSelectDropdown').addEventListener('change', (e) => {

        const canvas = displayPropWin.querySelector('#saverPreviewCanvas');

        window.XPCore.startScreensaver(e.target.value, canvas);

      });

      displayPropWin.querySelector('#btnPreviewScreensaver').addEventListener('click', () => {

        const mode = displayPropWin.querySelector('#screensaverSelectDropdown').value;

        window.XPCore.startScreensaver(mode);

      });



      // Resolution slider

      const resMap = { '1': '800 by 600 pixels', '2': '1024 by 768 pixels', '3': '1280 by 800 pixels', '4': '1920 by 1080 pixels' };

      displayPropWin.querySelector('#screenResRange').addEventListener('input', (e) => {

        displayPropWin.querySelector('#screenResLabel').textContent = resMap[e.target.value] || '1280 by 800 pixels';

      });

    }



    displayPropWin.style.display = 'flex';

    displayPropWin.classList.remove('minimized');

    window.XPCore.bringToFront(displayPropWin);

  }



  // -------------------------------------------------------------------------

  // 2. Windows Task Manager (taskmgr.exe)

  // -------------------------------------------------------------------------

  let taskMgrWin = null;

  let taskMgrPerfInterval = null;



  function openTaskManager() {

    if (!taskMgrWin) {

      taskMgrWin = document.createElement('div');

      taskMgrWin.className = 'xp-window xp-taskmanager-window';

      taskMgrWin.id = 'unifiedTaskManagerWindow';

      taskMgrWin.style.left = '220px';

      taskMgrWin.style.top = '60px';

      taskMgrWin.style.width = '480px';

      taskMgrWin.style.height = '460px';



      taskMgrWin.innerHTML = `

        <div class="title-bar" id="taskMgrTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/taskmgr_100.png" class="xp-title-icon" alt="Task Manager">

            <span>Windows Task Manager</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn min" id="taskMgrMinBtn" title="Minimize"></button>

            <button class="xp-caption-btn max" id="taskMgrMaxBtn" title="Maximize"></button>

            <button class="xp-caption-btn close" id="taskMgrCloseBtn" title="Close"></button>

          </div>

        </div>



        <div class="xp-window-menubar">

          <span><u>F</u>ile</span>

          <span><u>O</u>ptions</span>

          <span><u>V</u>iew</span>

          <span><u>W</u>indows</span>

          <span><u>H</u>elp</span>

        </div>



        <div class="xp-dialog-tabs">

          <button class="xp-dialog-tab-btn active" data-tab="tmTabApps">Applications</button>

          <button class="xp-dialog-tab-btn" data-tab="tmTabProcs">Processes</button>

          <button class="xp-dialog-tab-btn" data-tab="tmTabPerf">Performance</button>

          <button class="xp-dialog-tab-btn" data-tab="tmTabNet">Networking</button>

          <button class="xp-dialog-tab-btn" data-tab="tmTabUsers">Users</button>

        </div>



        <div class="xp-dialog-body" style="padding:8px; flex:1; overflow-y:auto; background:#ece9d8; display:flex; flex-direction:column;">

          <!-- Tab 1: Applications -->

          <div id="tmTabApps" class="tm-tab-panel" style="flex:1; display:flex; flex-direction:column;">

            <div class="tm-table-wrapper" style="flex:1; background:#ffffff; border:1px solid #716f64; overflow-y:auto;">

              <table class="tm-table" id="tmAppsTable" style="width:100%; border-collapse:collapse; font-size:11px;">

                <thead>

                  <tr style="background:#ece9d8; border-bottom:1px solid #d4d0c8; font-weight:700;">

                    <th style="padding:3px 6px; text-align:left;">Task</th>

                    <th style="padding:3px 6px; text-align:left; width:90px;">Status</th>

                  </tr>

                </thead>

                <tbody id="tmAppsList"></tbody>

              </table>

            </div>

            <div style="display:flex; justify-content:flex-end; gap:6px; margin-top:8px;">

              <button class="btn btn-secondary btn-sm" id="tmBtnEndTask">End Task</button>

              <button class="btn btn-secondary btn-sm" id="tmBtnSwitchTo">Switch To</button>

              <button class="btn btn-secondary btn-sm" id="tmBtnNewTask">New Task...</button>

            </div>

          </div>



          <!-- Tab 2: Processes -->

          <div id="tmTabProcs" class="tm-tab-panel" style="display:none; flex:1; flex-direction:column;">

            <div class="tm-table-wrapper" style="flex:1; background:#ffffff; border:1px solid #716f64; overflow-y:auto;">

              <table class="tm-table" style="width:100%; border-collapse:collapse; font-size:11px;">

                <thead>

                  <tr style="background:#ece9d8; border-bottom:1px solid #d4d0c8; font-weight:700;">

                    <th style="padding:3px 6px; text-align:left;">Image Name</th>

                    <th style="padding:3px 6px; text-align:right;">PID</th>

                    <th style="padding:3px 6px; text-align:right;">CPU</th>

                    <th style="padding:3px 6px; text-align:right;">Mem Usage</th>

                  </tr>

                </thead>

                <tbody id="tmProcsList"></tbody>

              </table>

            </div>

            <div style="display:flex; justify-content:flex-end; gap:6px; margin-top:8px;">

              <button class="btn btn-secondary btn-sm" id="tmBtnEndProc">End Process</button>

            </div>

          </div>



          <!-- Tab 3: Performance (Green Oscilloscope Graphs) -->

          <div id="tmTabPerf" class="tm-tab-panel" style="display:none; flex:1; flex-direction:column; gap:8px;">

            <div style="display:flex; gap:8px; height:130px;">

              <div style="flex:1; background:#000000; border:1px solid #334155; border-radius:3px; padding:4px; display:flex; flex-direction:column;">

                <div style="font-size:10px; color:#22c55e; font-weight:700; margin-bottom:2px;">CPU Usage: <span id="tmCpuPct">14%</span></div>

                <canvas id="tmCpuHistoryCanvas" width="200" height="95" style="flex:1; width:100%; height:100%;"></canvas>

              </div>

              <div style="flex:1; background:#000000; border:1px solid #334155; border-radius:3px; padding:4px; display:flex; flex-direction:column;">

                <div style="font-size:10px; color:#22c55e; font-weight:700; margin-bottom:2px;">PF Usage: <span id="tmPfUsage">512 MB</span></div>

                <canvas id="tmPfHistoryCanvas" width="200" height="95" style="flex:1; width:100%; height:100%;"></canvas>

              </div>

            </div>

            <!-- System Stats Grid -->

            <div style="display:flex; gap:8px; font-size:10.5px; background:#ece9d8; border:1px solid #d4d0c8; padding:6px; border-radius:2px;">

              <div style="flex:1;">

                <div style="font-weight:700; border-bottom:1px solid #cbd5e1; margin-bottom:3px;">Totals</div>

                <div>Handles: 8,420</div>

                <div>Threads: 412</div>

                <div>Processes: 38</div>

              </div>

              <div style="flex:1;">

                <div style="font-weight:700; border-bottom:1px solid #cbd5e1; margin-bottom:3px;">Commit Charge (K)</div>

                <div>Total: 524,288</div>

                <div>Limit: 2,097,152</div>

                <div>Peak: 614,400</div>

              </div>

              <div style="flex:1;">

                <div style="font-weight:700; border-bottom:1px solid #cbd5e1; margin-bottom:3px;">Physical Memory (K)</div>

                <div>Total: 2,097,152</div>

                <div>Available: 1,482,864</div>

                <div>System Cache: 410,240</div>

              </div>

            </div>

          </div>



          <!-- Tab 4: Networking -->

          <div id="tmTabNet" class="tm-tab-panel" style="display:none; flex:1; flex-direction:column;">

            <div style="font-size:11px; margin-bottom:6px; font-weight:700;">Local Area Connection (100 Mbps)</div>

            <div style="flex:1; background:#000000; border:1px solid #334155; border-radius:3px; padding:6px;">

              <canvas id="tmNetHistoryCanvas" width="420" height="150" style="width:100%; height:100%;"></canvas>

            </div>

          </div>



          <!-- Tab 5: Users -->

          <div id="tmTabUsers" class="tm-tab-panel" style="display:none; flex:1; flex-direction:column;">

            <table class="tm-table" style="width:100%; border-collapse:collapse; font-size:11px; background:#ffffff; border:1px solid #716f64;">

              <thead>

                <tr style="background:#ece9d8; border-bottom:1px solid #d4d0c8; font-weight:700;">

                  <th style="padding:3px 6px; text-align:left;">User</th>

                  <th style="padding:3px 6px; text-align:left;">ID</th>

                  <th style="padding:3px 6px; text-align:left;">Status</th>

                  <th style="padding:3px 6px; text-align:left;">Client Name</th>

                </tr>

              </thead>

              <tbody>

                <tr>

                  <td style="padding:4px 6px;">Rackneh</td>

                  <td style="padding:4px 6px;">0</td>

                  <td style="padding:4px 6px; color:#16a34a; font-weight:700;">Active</td>

                  <td style="padding:4px 6px;">Console</td>

                </tr>

              </tbody>

            </table>

          </div>

        </div>



        <div class="xp-statusbar" style="padding:2px 8px; font-size:10.5px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:space-between; color:#475569;">

          <span>Processes: 38</span>

          <span>CPU Usage: 14%</span>

          <span>Commit Charge: 512M / 2048M</span>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(taskMgrWin);

      window.XPCore.makeDraggable(taskMgrWin, taskMgrWin.querySelector('#taskMgrTitleHandle'));

      window.XPCore.makeResizable(taskMgrWin, 360, 320);



      // Tab switching

      taskMgrWin.querySelectorAll('.xp-dialog-tab-btn').forEach(btn => {

        btn.addEventListener('click', () => {

          taskMgrWin.querySelectorAll('.xp-dialog-tab-btn').forEach(b => b.classList.remove('active'));

          taskMgrWin.querySelectorAll('.tm-tab-panel').forEach(p => p.style.display = 'none');

          btn.classList.add('active');

          const panel = taskMgrWin.querySelector(`#${btn.dataset.tab}`);

          if (panel) panel.style.display = 'flex';

        });

      });



      // Close / Min / Max

      taskMgrWin.querySelector('#taskMgrCloseBtn').addEventListener('click', () => {

        taskMgrWin.style.display = 'none';

        if (taskMgrPerfInterval) clearInterval(taskMgrPerfInterval);

      });

      taskMgrWin.querySelector('#taskMgrMinBtn').addEventListener('click', () => {

        taskMgrWin.classList.add('minimized');

        taskMgrWin.style.display = 'none';

      });



      // Populate Applications List

      const updateAppsList = () => {

        const tbody = taskMgrWin.querySelector('#tmAppsList');

        if (!tbody) return;

        tbody.innerHTML = '';

        const wins = [

          { title: 'Windows Live Messenger - Contacts', id: 'unifiedWlmMainWindow' },

          { title: 'Windows Live Messenger - Chat', id: 'unifiedWlmWindow' },

          { title: 'My Computer - Windows Explorer', id: 'unifiedExplorerWindow' },

          { title: 'SQL Authority Studio 3D', id: 'unifiedSqlSuiteWindow' },

          { title: 'Command Prompt', id: 'unifiedCmdWindow' },

          { title: 'Untitled - Notepad', id: 'unifiedNotepadWindow' },

          { title: 'Sandbox Security Center', id: 'unifiedSandboxWindow' }

        ];



        wins.forEach(w => {

          const el = document.getElementById(w.id);

          const isVisible = el && el.style.display !== 'none' && !el.classList.contains('minimized');

          const tr = document.createElement('tr');

          tr.style.cursor = 'pointer';

          tr.innerHTML = `

            <td style="padding:3px 6px;">${w.title}</td>

            <td style="padding:3px 6px; color:${isVisible ? '#16a34a' : '#64748b'};">${isVisible ? 'Running' : 'Not Responding'}</td>

          `;

          tr.addEventListener('click', () => {

            tbody.querySelectorAll('tr').forEach(r => r.style.background = '#ffffff');

            tr.style.background = '#316ac5';

            tr.style.color = '#ffffff';

            tr.dataset.selectedWinId = w.id;

          });

          tbody.appendChild(tr);

        });

      };



      // Populate Processes List

      const procs = [

        { name: 'System', pid: 4, cpu: 0, mem: '240 K' },

        { name: 'smss.exe', pid: 512, cpu: 0, mem: '380 K' },

        { name: 'csrss.exe', pid: 604, cpu: 1, mem: '4,120 K' },

        { name: 'winlogon.exe', pid: 628, cpu: 0, mem: '2,840 K' },

        { name: 'services.exe', pid: 672, cpu: 0, mem: '3,410 K' },

        { name: 'lsass.exe', pid: 684, cpu: 0, mem: '2,190 K' },

        { name: 'svchost.exe', pid: 840, cpu: 0, mem: '14,280 K' },

        { name: 'explorer.exe', pid: 1420, cpu: 2, mem: '22,460 K' },

        { name: 'msnmsgr.exe', pid: 1980, cpu: 3, mem: '18,340 K' },

        { name: 'sqlauthority.exe', pid: 2140, cpu: 4, mem: '28,900 K' },

        { name: 'dory_engine.exe', pid: 2840, cpu: 12, mem: '480,200 K' },

        { name: 'llama-server.exe', pid: 3100, cpu: 6, mem: '340,100 K' },

        { name: 'sandbox_vm.bin', pid: 4096, cpu: 1, mem: '64,000 K' }

      ];



      const updateProcsList = () => {

        const tbody = taskMgrWin.querySelector('#tmProcsList');

        if (!tbody) return;

        tbody.innerHTML = '';

        procs.forEach(p => {

          const tr = document.createElement('tr');

          tr.style.cursor = 'pointer';

          tr.innerHTML = `

            <td style="padding:2px 6px;">${p.name}</td>

            <td style="padding:2px 6px; text-align:right;">${p.pid}</td>

            <td style="padding:2px 6px; text-align:right;">${p.cpu}</td>

            <td style="padding:2px 6px; text-align:right;">${p.mem}</td>

          `;

          tr.addEventListener('click', () => {

            tbody.querySelectorAll('tr').forEach(r => r.style.background = '#ffffff');

            tr.style.background = '#316ac5';

            tr.style.color = '#ffffff';

          });

          tbody.appendChild(tr);

        });

      };



      updateAppsList();

      updateProcsList();



      // Performance Graph Animation (CRT green oscilloscope line grid)

      const cpuCanvas = taskMgrWin.querySelector('#tmCpuHistoryCanvas');

      const cpuCtx = cpuCanvas ? cpuCanvas.getContext('2d') : null;

      const cpuPoints = new Array(30).fill(14);



      const drawPerf = () => {

        if (!cpuCtx) return;

        // Jitter CPU

        const nextVal = Math.max(5, Math.min(85, cpuPoints[cpuPoints.length - 1] + (Math.random() * 12 - 6)));

        cpuPoints.shift();

        cpuPoints.push(nextVal);



        taskMgrWin.querySelector('#tmCpuPct').textContent = `${Math.round(nextVal)}%`;



        cpuCtx.fillStyle = '#000000';

        cpuCtx.fillRect(0, 0, cpuCanvas.width, cpuCanvas.height);



        // Draw green grid

        cpuCtx.strokeStyle = 'rgba(22, 101, 52, 0.4)';

        cpuCtx.lineWidth = 1;

        for (let x = 0; x < cpuCanvas.width; x += 15) {

          cpuCtx.beginPath(); cpuCtx.moveTo(x, 0); cpuCtx.lineTo(x, cpuCanvas.height); cpuCtx.stroke();

        }

        for (let y = 0; y < cpuCanvas.height; y += 15) {

          cpuCtx.beginPath(); cpuCtx.moveTo(0, y); cpuCtx.lineTo(cpuCanvas.width, y); cpuCtx.stroke();

        }



        // Draw oscilloscope line

        cpuCtx.strokeStyle = '#22c55e';

        cpuCtx.lineWidth = 2;

        cpuCtx.beginPath();

        const step = cpuCanvas.width / (cpuPoints.length - 1);

        cpuPoints.forEach((val, i) => {

          const px = i * step;

          const py = cpuCanvas.height - (val / 100) * cpuCanvas.height;

          if (i === 0) cpuCtx.moveTo(px, py);

          else cpuCtx.lineTo(px, py);

        });

        cpuCtx.stroke();

      };



      taskMgrPerfInterval = setInterval(drawPerf, 1000);

      drawPerf();



      // End Task & Switch To buttons

      taskMgrWin.querySelector('#tmBtnEndTask').addEventListener('click', () => {

        const selected = taskMgrWin.querySelector('#tmAppsList tr[style*="background: rgb(49, 106, 197)"]');

        if (selected && selected.dataset.selectedWinId) {

          const win = document.getElementById(selected.dataset.selectedWinId);

          if (win) {

            win.style.display = 'none';

            win.classList.add('minimized');

            updateAppsList();

          }

        }

      });

      taskMgrWin.querySelector('#tmBtnSwitchTo').addEventListener('click', () => {

        const selected = taskMgrWin.querySelector('#tmAppsList tr[style*="background: rgb(49, 106, 197)"]');

        if (selected && selected.dataset.selectedWinId) {

          const win = document.getElementById(selected.dataset.selectedWinId);

          if (win) {

            win.style.display = 'flex';

            win.classList.remove('minimized');

            window.XPCore.bringToFront(win);

          }

        }

      });

      taskMgrWin.querySelector('#tmBtnNewTask').addEventListener('click', () => {

        openRunDialog();

      });

    }



    taskMgrWin.style.display = 'flex';

    taskMgrWin.classList.remove('minimized');

    window.XPCore.bringToFront(taskMgrWin);

  }



  // -------------------------------------------------------------------------

  // 3. Run Dialog (rundll32.exe)

  // -------------------------------------------------------------------------

  let runDialogWin = null;



  function openRunDialog() {

    if (!runDialogWin) {

      runDialogWin = document.createElement('div');

      runDialogWin.className = 'xp-window xp-run-dialog';

      runDialogWin.id = 'unifiedRunDialog';

      runDialogWin.style.left = '60px';

      runDialogWin.style.bottom = '40px';

      runDialogWin.style.width = '370px';

      runDialogWin.style.height = '180px';



      runDialogWin.innerHTML = `

        <div class="title-bar" id="runDialogTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/shell32_24.png" class="xp-title-icon" alt="Run">

            <span>Run</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="runDialogCloseBtn" title="Close"></button>

          </div>

        </div>

        <div style="padding:12px; display:flex; gap:12px; align-items:flex-start; background:#ece9d8;">

          <img src="/static/xp_assets/icons/shell32_24.png" width="32" height="32" alt="Run" style="margin-top:2px;">

          <div style="flex:1;">

            <div style="font-size:11px; margin-bottom:10px; color:#1e293b;">

              Type the name of a program, folder, document, or Internet resource, and Windows will open it for you.

            </div>

            <div style="display:flex; align-items:center; gap:8px;">

              <label style="font-size:11px; font-weight:700;">Open:</label>

              <input type="text" id="runDialogInput" class="form-control" style="flex:1; padding:3px 6px; font-size:11px; border:1px solid #716f64;" value="cmd">

            </div>

          </div>

        </div>

        <div style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end; gap:6px;">

          <button class="btn btn-primary btn-sm" id="runDialogOkBtn" style="min-width:65px; background:#0284c7; color:#fff;">OK</button>

          <button class="btn btn-secondary btn-sm" id="runDialogCancelBtn" style="min-width:65px;">Cancel</button>

          <button class="btn btn-secondary btn-sm" id="runDialogBrowseBtn" style="min-width:65px;">Browse...</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(runDialogWin);

      window.XPCore.makeDraggable(runDialogWin, runDialogWin.querySelector('#runDialogTitleHandle'));



      const closeRun = () => { runDialogWin.style.display = 'none'; };

      runDialogWin.querySelector('#runDialogCloseBtn').addEventListener('click', closeRun);

      runDialogWin.querySelector('#runDialogCancelBtn').addEventListener('click', closeRun);



      const executeRun = () => {

        const val = runDialogWin.querySelector('#runDialogInput').value.trim().toLowerCase();

        closeRun();

        if (val === 'cmd' || val === 'command') {

          const cmdWin = document.getElementById('unifiedCmdWindow');

          if (cmdWin) { cmdWin.style.display = 'flex'; cmdWin.classList.remove('minimized'); window.XPCore.bringToFront(cmdWin); }

        } else if (val === 'notepad') {

          const npWin = document.getElementById('unifiedNotepadWindow');

          if (npWin) { npWin.style.display = 'flex'; npWin.classList.remove('minimized'); window.XPCore.bringToFront(npWin); }

        } else if (val === 'taskmgr' || val === 'taskmanager') {

          openTaskManager();

        } else if (val === 'desk.cpl' || val === 'display' || val === 'properties') {

          openDisplayProperties();

        } else if (val === 'control' || val === 'control panel') {

          openControlPanel();

        } else if (val === 'calc' || val === 'calculator') {

          openCalculator();

        } else if (val === 'mspaint' || val === 'pbrush' || val === 'paint') {

          openPaint();

        } else if (val === 'sol' || val === 'solitaire') {

          if (window.XPGames) window.XPGames.openSolitaire();

        } else if (val === 'winmine' || val === 'minesweeper') {

          if (window.XPGames) window.XPGames.openMinesweeper();

        } else if (val === 'pinball') {

          if (window.XPGames) window.XPGames.openPinball();

        } else if (val === 'sysdm.cpl' || val === 'system') {

          openSystemProperties();

        } else if (val === 'mmsys.cpl' || val === 'sounds') {

          openSoundsAndAudio();

        } else if (val === 'nusrmgr.cpl' || val === 'users') {

          openUserAccounts();

        } else if (val === 'timedate.cpl' || val === 'time' || val === 'date') {

          openDateTime();

        } else if (val === 'ncpa.cpl' || val === 'network') {

          openNetworkConnections();

        } else if (val === 'appwiz.cpl') {

          openAddRemovePrograms();

        } else if (val === 'regedit' || val === 'regedt32') {

          openRegedit();

        } else if (val === 'winver' || val === 'about') {

          openWinver();

        } else if (val === 'dory' || val === 'bot' || val === 'settings') {

          openDorySettings();

        } else if (val === 'explorer' || val === 'my computer') {

          const expWin = document.getElementById('unifiedExplorerWindow');

          if (expWin) { expWin.style.display = 'flex'; expWin.classList.remove('minimized'); window.XPCore.bringToFront(expWin); }

        } else if (val === 'msnmsgr' || val === 'wlm' || val === 'messenger') {

          const wlmWin = document.getElementById('unifiedWlmMainWindow');

          if (wlmWin) { wlmWin.style.display = 'flex'; wlmWin.classList.remove('minimized'); window.XPCore.bringToFront(wlmWin); }

        } else if (val === 'setup' || val === 'autorun' || val === 'setup.exe') {

          openAutorun();

        } else if (val === 'syssetup' || val === 'install' || val === 'oobe') {

          openSetupWizard();

        } else if (val === 'search' || val === 'find' || val === 'rover') {

          openSearchCompanion();

        } else if (val.startsWith('http')) {

          window.open(val, '_blank');

        } else {

          window.XPCore.Audio.playError();

          alert(`Windows cannot find '${val}'. Make sure you typed the name correctly, and then try again.`);

        }

      };



      runDialogWin.querySelector('#runDialogOkBtn').addEventListener('click', executeRun);

      runDialogWin.querySelector('#runDialogInput').addEventListener('keydown', (e) => {

        if (e.key === 'Enter') executeRun();

      });

    }



    runDialogWin.style.display = 'flex';

    runDialogWin.classList.remove('minimized');

    window.XPCore.bringToFront(runDialogWin);

    runDialogWin.querySelector('#runDialogInput').focus();

  }



  // -------------------------------------------------------------------------

  // 4. Control Panel (control.exe)

  // -------------------------------------------------------------------------

  let controlPanelWin = null;

  let isCplClassicView = false;



  function openControlPanel() {

    if (!controlPanelWin) {

      controlPanelWin = document.createElement('div');

      controlPanelWin.className = 'xp-window xp-cpl-window';

      controlPanelWin.id = 'unifiedControlPanelWindow';

      controlPanelWin.style.left = '120px';

      controlPanelWin.style.top = '50px';

      controlPanelWin.style.width = '640px';

      controlPanelWin.style.height = '460px';



      renderControlPanelContent();

      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(controlPanelWin);

      window.XPCore.makeDraggable(controlPanelWin, controlPanelWin.querySelector('#cplTitleHandle'));

      window.XPCore.makeResizable(controlPanelWin, 500, 360);

    }



    controlPanelWin.style.display = 'flex';

    controlPanelWin.classList.remove('minimized');

    window.XPCore.bringToFront(controlPanelWin);

  }



  function renderControlPanelContent() {

    controlPanelWin.innerHTML = `

      <div class="title-bar" id="cplTitleHandle">

        <div class="title-bar-text">

          <img src="/static/xp_assets/icons/shell32_22.png" class="xp-title-icon" alt="Control Panel"> <span>Control Panel</span>

        </div>

        <div class="title-bar-controls">

          <button class="xp-caption-btn min" id="cplMinBtn" title="Minimize"></button>

          <button class="xp-caption-btn max" id="cplMaxBtn" title="Maximize"></button>

          <button class="xp-caption-btn close" id="cplCloseBtn" title="Close"></button>

        </div>

      </div>

      <div class="xp-window-menubar">

        <div class="xp-menu-item">File

          <div class="xp-dropdown-menu">

            <div class="xp-drop-item" onclick="document.getElementById('unifiedControlPanelWindow').style.display='none'">Close</div>

          </div>

        </div>

        <div class="xp-menu-item">Edit</div>

        <div class="xp-menu-item">View

          <div class="xp-dropdown-menu">

            <div class="xp-drop-item" id="cplMenuViewToggle">${isCplClassicView ? 'Switch to Category View' : 'Switch to Classic View'}</div>

          </div>

        </div>

        <div class="xp-menu-item">Favorites</div>

        <div class="xp-menu-item">Tools</div>

        <div class="xp-menu-item">Help</div>

      </div>

      <div class="xp-window-toolbar">

        <button class="xp-tb-btn" id="cplTbBack" title="Back"><span>◀ Back</span></button>

        <button class="xp-tb-btn" id="cplTbFwd" title="Forward"><span>▶</span></button>

        <button class="xp-tb-btn" id="cplTbUp" title="Up"><span>▲ Up</span></button>

        <div class="xp-tb-separator"></div>

        <button class="xp-tb-btn" id="cplTbSearch" title="Search"><img src="/static/xp_assets/icons/shell32_22.png" class="xp-tb-icon" alt="Search"> <span>Search</span></button>

        <button class="xp-tb-btn" id="cplTbFolders" title="Folders"><img src="/static/xp_assets/icons/shell32_4.png" class="xp-tb-icon" alt="Folders"> <span>Folders</span></button>

      </div>



      <div class="xp-cpl-body">

        <div class="xp-cpl-left-sidebar">

          <div class="cpl-side-box">

            <div class="cpl-side-title">Control Panel</div>

            <div class="cpl-side-link" id="cplToggleViewLink">

              <img src="/static/xp_assets/icons/shell32_238.png" class="xp-title-icon" alt="View">

              <span>${isCplClassicView ? 'Switch to Category View' : 'Switch to Classic View'}</span>

            </div>

          </div>

          <div class="cpl-side-box">

            <div class="cpl-side-title">See Also</div>

            <div class="cpl-side-link" id="cplSeeWindowsUpdate"><span>Windows Update</span></div>

            <div class="cpl-side-link" id="cplSeeHelp"><span>Help and Support</span></div>

          </div>

        </div>



        <div class="xp-cpl-main-view" id="cplMainView">

          ${isCplClassicView ? renderCplClassic() : renderCplCategories()}

        </div>

      </div>

    `;



    // Wire events

    controlPanelWin.querySelector('#cplCloseBtn').addEventListener('click', () => {

      controlPanelWin.style.display = 'none';

    });

    controlPanelWin.querySelector('#cplMinBtn').addEventListener('click', () => {

      controlPanelWin.classList.add('minimized');

      controlPanelWin.style.display = 'none';

    });



    const toggleView = () => {

      isCplClassicView = !isCplClassicView;

      renderControlPanelContent();

      window.XPCore.makeDraggable(controlPanelWin, controlPanelWin.querySelector('#cplTitleHandle'));

    };



    const vLink = controlPanelWin.querySelector('#cplToggleViewLink');

    if (vLink) vLink.addEventListener('click', toggleView);

    const mItem = controlPanelWin.querySelector('#cplMenuViewToggle');

    if (mItem) mItem.addEventListener('click', toggleView);



    // Applet launch wiring

    wireCplApplets();

  }



  function renderCplCategories() {

    return `

      <div class="cpl-cat-header">

        <h3>Pick a category</h3>

      </div>

      <div class="cpl-cat-grid">

        <div class="cpl-cat-card" data-applet="desk.cpl">

          <img src="/static/xp_assets/icons/icon_107.png" class="cat-icon" alt="Appearance">

          <div class="cat-info">

            <h4>Appearance and Themes</h4>

            <p>Change the appearance of desktop items, apply themes, or change screen saver.</p>

          </div>

        </div>

        <div class="cpl-cat-card" data-applet="ncpa.cpl">

          <img src="/static/xp_assets/icons/icon_106.png" class="cat-icon" alt="Network">

          <div class="cat-info">

            <h4>Network and Internet Connections</h4>

            <p>Connect to the Internet, configure virtual sandbox network, or adjust proxy settings.</p>

          </div>

        </div>

        <div class="cpl-cat-card" data-applet="nusrmgr.cpl">

          <img src="/static/xp_assets/icons/icon_108.png" class="cat-icon" alt="User Accounts">

          <div class="cat-info">

            <h4>User Accounts</h4>

            <p>Change your account picture, password, or configure computer logon settings.</p>

          </div>

        </div>

        <div class="cpl-cat-card" data-applet="mmsys.cpl">

          <img src="/static/xp_assets/icons/icon_109.png" class="cat-icon" alt="Sounds">

          <div class="cat-info">

            <h4>Sounds, Speech, and Audio Devices</h4>

            <p>Change the sound scheme for your computer, or configure speaker settings.</p>

          </div>

        </div>

        <div class="cpl-cat-card" data-applet="sysdm.cpl">

          <img src="/static/xp_assets/icons/icon_13.png" class="cat-icon" alt="Performance">

          <div class="cat-info">

            <h4>Performance and Maintenance</h4>

            <p>View system properties, processor offload layers, and virtual memory swap.</p>

          </div>

        </div>

        <div class="cpl-cat-card" data-applet="timedate.cpl">

          <img src="/static/xp_assets/icons/icon_105.png" class="cat-icon" alt="Date Time">

          <div class="cat-info">

            <h4>Date, Time, Language, and Regional Options</h4>

            <p>Change date, time, time zone, and language standards for your system.</p>

          </div>

        </div>

        <div class="cpl-cat-card" data-applet="appwiz.cpl">

          <img src="/static/xp_assets/icons/icon_10.png" class="cat-icon" alt="Add/Remove">

          <div class="cat-info">

            <h4>Add or Remove Programs</h4>

            <p>Install or remove software components and Windows features.</p>

          </div>

        </div>

        <div class="cpl-cat-card" data-applet="wscui.cpl">

          <img src="/static/xp_assets/icons/icon_11.png" class="cat-icon" alt="Security Center">

          <div class="cat-info">

            <h4>Security Center</h4>

            <p>Manage workspace jail, path containment, and firewall enforcement.</p>

          </div>

        </div>

      </div>

    `;

  }



  function renderCplClassic() {

    const applets = [

      { name: "Appearance and Themes", icon: "/static/xp_assets/icons/icon_107.png", applet: "desk.cpl" },

      { name: "User Accounts", icon: "/static/xp_assets/icons/icon_108.png", applet: "nusrmgr.cpl" },

      { name: "System Properties", icon: "/static/xp_assets/icons/icon_13.png", applet: "sysdm.cpl" },

      { name: "Sounds and Audio Devices", icon: "/static/xp_assets/icons/icon_109.png", applet: "mmsys.cpl" },

      { name: "Network Connections", icon: "/static/xp_assets/icons/icon_106.png", applet: "ncpa.cpl" },

      { name: "Date and Time", icon: "/static/xp_assets/icons/icon_105.png", applet: "timedate.cpl" },

      { name: "Add or Remove Programs", icon: "/static/xp_assets/icons/icon_10.png", applet: "appwiz.cpl" },

      { name: "Security Center", icon: "/static/xp_assets/icons/icon_11.png", applet: "wscui.cpl" },

      { name: "Calculator", icon: "/static/xp_assets/icons/icon_100.png", applet: "calc" },

      { name: "Paint", icon: "/static/xp_assets/icons/icon_101.png", applet: "mspaint" }

    ];



    return `

      <div class="cpl-classic-grid">

        ${applets.map(a => `

          <div class="cpl-classic-item" data-applet="${a.applet}">

            <img src="${a.icon}" alt="${a.name}">

            <span>${a.name}</span>

          </div>

        `).join('')}

      </div>

    `;

  }



  function wireCplApplets() {

    controlPanelWin.querySelectorAll('[data-applet]').forEach(el => {

      el.addEventListener('click', () => {

        const app = el.dataset.applet;

        if (app === 'desk.cpl') openDisplayProperties();

        else if (app === 'nusrmgr.cpl') openUserAccounts();

        else if (app === 'sysdm.cpl') openSystemProperties();

        else if (app === 'mmsys.cpl') openSoundsAndAudio();

        else if (app === 'ncpa.cpl') openNetworkConnections();

        else if (app === 'timedate.cpl') openDateTime();

        else if (app === 'appwiz.cpl') openAddRemovePrograms();

        else if (app === 'wscui.cpl') {

          const s = document.getElementById('unifiedSandboxWindow');

          if (s) { s.style.display = 'flex'; s.classList.remove('minimized'); window.XPCore.bringToFront(s); }

        }

        else if (app === 'calc') openCalculator();

        else if (app === 'mspaint') openPaint();

      });

    });

  }



  // -------------------------------------------------------------------------

  // 5. User Accounts Applet (nusrmgr.cpl)

  // -------------------------------------------------------------------------

  let userAccountsWin = null;



  function openUserAccounts() {

    if (!userAccountsWin) {

      userAccountsWin = document.createElement('div');

      userAccountsWin.className = 'xp-window xp-cpl-dialog';

      userAccountsWin.id = 'unifiedUserAccountsWindow';

      userAccountsWin.style.left = '180px';

      userAccountsWin.style.top = '60px';

      userAccountsWin.style.width = '520px';

      userAccountsWin.style.height = '420px';



      renderUserAccountsContent();

      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(userAccountsWin);

      window.XPCore.makeDraggable(userAccountsWin, userAccountsWin.querySelector('#usrAcctTitleHandle'));

    }



    userAccountsWin.style.display = 'flex';

    userAccountsWin.classList.remove('minimized');

    window.XPCore.bringToFront(userAccountsWin);

  }



  function renderUserAccountsContent() {

    let cfg = window.XPInstaller ? window.XPInstaller.getInstallConfig() : { userName: 'Rackneh', userTile: '/static/xp_assets/usertiles/dog.bmp' };



    userAccountsWin.innerHTML = `

      <div class="title-bar" id="usrAcctTitleHandle">

        <div class="title-bar-text">

          <img src="/static/xp_assets/icons/icon_108.png" width="16" height="16" style="vertical-align:middle; margin-right:4px;">

          <span>User Accounts</span>

        </div>

        <div class="title-bar-controls">

          <button class="xp-caption-btn close" id="usrAcctCloseBtn" title="Close"></button>

        </div>

      </div>

      <div class="xp-cpl-dialog-body" style="padding:14px; background:#ece9d8; flex:1; overflow-y:auto;">

        <h4 style="margin:0 0 10px 0; font-size:13px; color:#0c4a6e;">Pick an account to change</h4>

        <div class="xp-usr-card-banner" style="display:flex; align-items:center; gap:12px; padding:10px; background:#ffffff; border:1px solid #7f9db9; border-radius:4px; margin-bottom:14px;">

          <img src="${cfg.userTile || '/static/xp_assets/usertiles/dog.bmp'}" width="48" height="48" style="border:2px solid #0284c7; border-radius:4px;" id="usrCurrentAvatarImg">

          <div>

            <div style="font-size:14px; font-weight:700; color:#0f172a;" id="usrCurrentNameTxt">${cfg.userName}</div>

            <div style="font-size:11px; color:#64748b;">Computer Administrator • Password Protected</div>

          </div>

        </div>



        <h5 style="margin:8px 0 6px 0; font-size:11px; font-weight:700;">Choose a new picture for your account:</h5>

        <div class="xp-usertiles-grid" id="usrTilesGrid" style="display:grid; grid-template-columns:repeat(6, 1fr); gap:8px; max-height:160px; overflow-y:auto; padding:6px; background:#ffffff; border:1px solid #7f9db9;">

          <!-- 23 Official User Tiles Populated -->

        </div>



        <div style="margin-top:14px; display:flex; justify-content:space-between; align-items:center;">

          <button class="btn btn-primary btn-sm" id="usrSaveTileBtn" style="background:#0284c7; color:#fff;">Change Picture</button>

          <button class="btn btn-secondary btn-sm" id="usrDoneBtn">Done</button>

        </div>

      </div>

    `;



    const tiles = [

      'airplane.bmp', 'astronaut.bmp', 'beach.bmp', 'butterfly.bmp', 'car.bmp',

      'cat.bmp', 'chess.bmp', 'dirtbike.bmp', 'dog.bmp', 'drip.bmp',

      'duck.bmp', 'fish.bmp', 'frog.bmp', 'guest.bmp', 'guitar.bmp',

      'horses.bmp', 'liftoff.bmp', 'palmtree.bmp', 'pinkflower.bmp', 'redflower.bmp',

      'skater.bmp', 'snowflake.bmp', 'soccer.bmp'

    ];



    const grid = userAccountsWin.querySelector('#usrTilesGrid');

    let selectedTile = cfg.userTile || `/static/xp_assets/usertiles/dog.bmp`;



    tiles.forEach(t => {

      const tileUrl = `/static/xp_assets/usertiles/${t}`;

      const img = document.createElement('img');

      img.src = tileUrl;

      img.width = 44;

      img.height = 44;

      img.style.cursor = 'pointer';

      img.style.padding = '2px';

      img.style.border = (tileUrl === selectedTile) ? '2px solid #0284c7' : '2px solid transparent';

      img.style.borderRadius = '3px';



      img.addEventListener('click', () => {

        grid.querySelectorAll('img').forEach(i => i.style.border = '2px solid transparent');

        img.style.border = '2px solid #0284c7';

        selectedTile = tileUrl;

        userAccountsWin.querySelector('#usrCurrentAvatarImg').src = tileUrl;

      });

      grid.appendChild(img);

    });



    userAccountsWin.querySelector('#usrSaveTileBtn').addEventListener('click', () => {

      cfg.userTile = selectedTile;

      if (window.XPInstaller) window.XPInstaller.saveInstallConfig(cfg);

      window.XPCore.Audio.playDing();

      alert('Your account picture has been updated!');

    });



    const closeAcct = () => { userAccountsWin.style.display = 'none'; };

    userAccountsWin.querySelector('#usrAcctCloseBtn').addEventListener('click', closeAcct);

    userAccountsWin.querySelector('#usrDoneBtn').addEventListener('click', closeAcct);

  }



  // -------------------------------------------------------------------------

  // 6. System Properties Applet (sysdm.cpl)

  // -------------------------------------------------------------------------

  let sysPropWin = null;



  function openSystemProperties() {

    if (!sysPropWin) {

      sysPropWin = document.createElement('div');

      sysPropWin.className = 'xp-window xp-cpl-dialog';

      sysPropWin.id = 'unifiedSystemPropertiesWindow';

      sysPropWin.style.left = '200px';

      sysPropWin.style.top = '70px';

      sysPropWin.style.width = '420px';

      sysPropWin.style.height = '450px';



      let cfg = window.XPInstaller ? window.XPInstaller.getInstallConfig() : { userName: 'Rackneh', organization: 'Inference Lighting Lab', computerName: 'RACKNEH-XP' };



      sysPropWin.innerHTML = `

        <div class="title-bar" id="sysTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/sysdm_1.png" class="xp-title-icon" alt="System"> <span>System Properties</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="sysCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-dialog-tabs">

          <button class="xp-dialog-tab-btn active">General</button>

          <button class="xp-dialog-tab-btn">Computer Name</button>

          <button class="xp-dialog-tab-btn">Hardware</button>

          <button class="xp-dialog-tab-btn">Advanced</button>

        </div>

        <div class="xp-dialog-body" style="padding:14px; background:#ece9d8; flex:1; font-size:11px;">

          <div style="display:flex; gap:16px; margin-bottom:14px;">

            <img src="/static/xp_assets/sysdm_cpl/bmp_1.png" style="width:118px; height:113px; object-fit:contain;" alt="System">

            <div style="line-height:1.45;">

              <strong>System:</strong><br>

              Microsoft Windows XP<br>

              Professional<br>

              Version 2002<br>

              Service Pack 3 (Integral Edition)

            </div>

          </div>

          <div style="border-top:1px solid #716f64; padding-top:10px; margin-bottom:14px;">

            <strong>Registered to:</strong><br>

            <span>${cfg.userName}</span><br>

            <span>${cfg.organization}</span><br>

            <span style="font-family:monospace; color:#475569;">76487-OEM-0011903-00100</span>

          </div>

          <div style="border-top:1px solid #716f64; padding-top:10px;">

            <strong>Computer:</strong><br>

            AMD Athlon(tm) 64 X2 Dual Core Processor 3800+<br>

            AMD Radeon RX 580 (8GB VRAM Offloaded)<br>

            2.00 GHz, 2.00 GB of RAM (Physical)<br>

            IBA O(1) Cognitive Engine Architecture

          </div>

        </div>

        <div style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end;">

          <button class="btn btn-primary btn-sm" id="sysOkBtn" style="min-width:65px; background:#0284c7; color:#fff;">OK</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(sysPropWin);

      window.XPCore.makeDraggable(sysPropWin, sysPropWin.querySelector('#sysTitleHandle'));



      const closeSys = () => { sysPropWin.style.display = 'none'; };

      sysPropWin.querySelector('#sysCloseBtn').addEventListener('click', closeSys);

      sysPropWin.querySelector('#sysOkBtn').addEventListener('click', closeSys);

    }



    sysPropWin.style.display = 'flex';

    sysPropWin.classList.remove('minimized');

    window.XPCore.bringToFront(sysPropWin);

  }



  // -------------------------------------------------------------------------

  // 7. Sounds and Audio Devices Applet (mmsys.cpl)

  // -------------------------------------------------------------------------

  let mmsysWin = null;



  function openSoundsAndAudio() {

    if (!mmsysWin) {

      mmsysWin = document.createElement('div');

      mmsysWin.className = 'xp-window xp-cpl-dialog';

      mmsysWin.id = 'unifiedMmsysWindow';

      mmsysWin.style.left = '220px';

      mmsysWin.style.top = '80px';

      mmsysWin.style.width = '390px';

      mmsysWin.style.height = '380px';



      mmsysWin.innerHTML = `

        <div class="title-bar" id="mmsysTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/mmsys_100.png" class="xp-title-icon" alt="Audio"> <span>Sounds and Audio Devices Properties</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="mmsysCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-dialog-tabs">

          <button class="xp-dialog-tab-btn active">Volume</button>

          <button class="xp-dialog-tab-btn">Sounds</button>

          <button class="xp-dialog-tab-btn">Audio</button>

        </div>

        <div class="xp-dialog-body" style="padding:14px; background:#ece9d8; flex:1; font-size:11px;">

          <div style="display:flex; align-items:center; gap:12px; margin-bottom:14px;">

            <img src="/static/xp_assets/icons/icon_109.png" width="40" height="40">

            <div>

              <strong>SoundMAX Integrated Digital Audio</strong><br>

              <small style="color:#64748b;">Device working properly</small>

            </div>

          </div>

          <div style="border:1px solid #716f64; padding:10px; background:#ffffff; border-radius:3px; margin-bottom:14px;">

            <label style="font-weight:700;">Device volume:</label>

            <div style="display:flex; align-items:center; gap:8px; margin-top:6px;">

              <span>Low</span>

              <input type="range" min="0" max="100" value="75" id="mmsysVolSlider" style="flex:1;">

              <span>High</span>

            </div>

            <div style="margin-top:8px;">

              <input type="checkbox" id="mmsysMuteChk"> <label for="mmsysMuteChk">Mute</label>

            </div>

          </div>

          <div style="display:flex; justify-content:space-between; align-items:center;">

            <span>Test Sound:</span>

            <button class="btn btn-secondary btn-sm" id="mmsysTestSoundBtn">▶ Play Ding</button>

          </div>

        </div>

        <div style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end;">

          <button class="btn btn-primary btn-sm" id="mmsysOkBtn" style="min-width:65px; background:#0284c7; color:#fff;">OK</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(mmsysWin);

      window.XPCore.makeDraggable(mmsysWin, mmsysWin.querySelector('#mmsysTitleHandle'));



      const closeMm = () => { mmsysWin.style.display = 'none'; };

      mmsysWin.querySelector('#mmsysCloseBtn').addEventListener('click', closeMm);

      mmsysWin.querySelector('#mmsysOkBtn').addEventListener('click', closeMm);

      mmsysWin.querySelector('#mmsysTestSoundBtn').addEventListener('click', () => {

        window.XPCore.Audio.playDing();

      });

    }



    mmsysWin.style.display = 'flex';

    mmsysWin.classList.remove('minimized');

    window.XPCore.bringToFront(mmsysWin);

  }



  // -------------------------------------------------------------------------

  // 8. Network Connections Applet (ncpa.cpl)

  // -------------------------------------------------------------------------

  let ncpaWin = null;



  function openNetworkConnections() {

    if (!ncpaWin) {

      ncpaWin = document.createElement('div');

      ncpaWin.className = 'xp-window xp-cpl-dialog';

      ncpaWin.id = 'unifiedNcpaWindow';

      ncpaWin.style.left = '240px';

      ncpaWin.style.top = '90px';

      ncpaWin.style.width = '480px';

      ncpaWin.style.height = '340px';



      ncpaWin.innerHTML = `

        <div class="title-bar" id="ncpaTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/icon_106.png" width="16" height="16" style="vertical-align:middle; margin-right:4px;">

            <span>Network Connections</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="ncpaCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-dialog-body" style="padding:14px; background:#ffffff; flex:1; font-size:11px; overflow-y:auto;">

          <h4 style="margin:0 0 10px 0; color:#0c4a6e;">LAN or High-Speed Internet</h4>

          <div style="display:flex; flex-direction:column; gap:8px;">

            <div style="display:flex; align-items:center; gap:12px; padding:8px; border:1px solid #e2e8f0; border-radius:4px; background:#f8fafc;">

              <img src="/static/xp_assets/icons/icon_106.png" width="32" height="32">

              <div style="flex:1;">

                <strong>Local Area Connection</strong><br>

                <span>Realtek RTL8168/8111 PCI-E Gigabit Ethernet • <span style="color:#16a34a; font-weight:700;">Connected</span> (1000 Mbps)</span>

              </div>

            </div>

            <div style="display:flex; align-items:center; gap:12px; padding:8px; border:1px solid #e2e8f0; border-radius:4px; background:#f8fafc;">

              <img src="/static/xp_assets/icons/icon_106.png" width="32" height="32">

              <div style="flex:1;">

                <strong>Virtual Jail VM Adapter (Z: Mapped Drive)</strong><br>

                <span>Tiny Linux Sandbox Bridge • <span style="color:#16a34a; font-weight:700;">Active</span> (10.0.2.15/24)</span>

              </div>

            </div>

          </div>

        </div>

        <div style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end;">

          <button class="btn btn-secondary btn-sm" id="ncpaDoneBtn">Close</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(ncpaWin);

      window.XPCore.makeDraggable(ncpaWin, ncpaWin.querySelector('#ncpaTitleHandle'));



      const closeNc = () => { ncpaWin.style.display = 'none'; };

      ncpaWin.querySelector('#ncpaCloseBtn').addEventListener('click', closeNc);

      ncpaWin.querySelector('#ncpaDoneBtn').addEventListener('click', closeNc);

    }



    ncpaWin.style.display = 'flex';

    ncpaWin.classList.remove('minimized');

    window.XPCore.bringToFront(ncpaWin);

  }



  // -------------------------------------------------------------------------

  // 9. Add or Remove Programs (appwiz.cpl)

  // -------------------------------------------------------------------------

  let appwizWin = null;



  function openAddRemovePrograms() {

    if (!appwizWin) {

      appwizWin = document.createElement('div');

      appwizWin.className = 'xp-window xp-cpl-dialog';

      appwizWin.id = 'unifiedAppwizWindow';

      appwizWin.style.left = '210px';

      appwizWin.style.top = '75px';

      appwizWin.style.width = '520px';

      appwizWin.style.height = '400px';



      appwizWin.innerHTML = `

        <div class="title-bar" id="appwizTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/appwiz_1.png" class="xp-title-icon" alt="Programs"> <span>Add or Remove Programs</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="appwizCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-dialog-body" style="padding:10px; background:#ece9d8; flex:1; display:flex; flex-direction:column;">

          <div style="font-size:11px; margin-bottom:8px; font-weight:700;">Currently installed programs:</div>

          <div style="flex:1; background:#ffffff; border:1px solid #716f64; overflow-y:auto; font-size:11px;">

            <div style="display:flex; justify-content:space-between; padding:6px 10px; border-bottom:1px solid #e2e8f0; align-items:center;">

              <div><strong>NVIDIA Nemotron Dory Studio 1.0</strong><br><small style="color:#64748b;">Size: 4.20 GB • Used frequently</small></div>

              <span style="color:#16a34a; font-weight:700;">Installed</span>

            </div>

            <div style="display:flex; justify-content:space-between; padding:6px 10px; border-bottom:1px solid #e2e8f0; align-items:center;">

              <div><strong>Windows Live Messenger 2009 (Wave 4 Revival)</strong><br><small style="color:#64748b;">Size: 34.2 MB • Used frequently</small></div>

              <span style="color:#16a34a; font-weight:700;">Installed</span>

            </div>

            <div style="display:flex; justify-content:space-between; padding:6px 10px; border-bottom:1px solid #e2e8f0; align-items:center;">

              <div><strong>SQL Authority Studio 3D</strong><br><small style="color:#64748b;">Size: 18.5 MB • Used frequently</small></div>

              <span style="color:#16a34a; font-weight:700;">Installed</span>

            </div>

            <div style="display:flex; justify-content:space-between; padding:6px 10px; border-bottom:1px solid #e2e8f0; align-items:center;">

              <div><strong>3D Pinball: Space Cadet</strong><br><small style="color:#64748b;">Size: 4.8 MB • Built-in game</small></div>

              <span style="color:#16a34a; font-weight:700;">Installed</span>

            </div>

          </div>

        </div>

        <div style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end;">

          <button class="btn btn-secondary btn-sm" id="appwizDoneBtn">Close</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(appwizWin);

      window.XPCore.makeDraggable(appwizWin, appwizWin.querySelector('#appwizTitleHandle'));



      const closeAppw = () => { appwizWin.style.display = 'none'; };

      appwizWin.querySelector('#appwizCloseBtn').addEventListener('click', closeAppw);

      appwizWin.querySelector('#appwizDoneBtn').addEventListener('click', closeAppw);

    }



    appwizWin.style.display = 'flex';

    appwizWin.classList.remove('minimized');

    window.XPCore.bringToFront(appwizWin);

  }



  // -------------------------------------------------------------------------

  // 10. Date and Time Properties (timedate.cpl)

  // -------------------------------------------------------------------------

  let timedateWin = null;

  let clockInterval = null;



  function openDateTime() {

    if (!timedateWin) {

      timedateWin = document.createElement('div');

      timedateWin.className = 'xp-window xp-cpl-dialog';

      timedateWin.id = 'unifiedTimedateWindow';

      timedateWin.style.left = '230px';

      timedateWin.style.top = '85px';

      timedateWin.style.width = '380px';

      timedateWin.style.height = '390px';



      timedateWin.innerHTML = `

        <div class="title-bar" id="timedateTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/timedate_100.png" class="xp-title-icon" alt="Date"> <span>Date and Time Properties</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="timedateCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-dialog-tabs">

          <button class="xp-dialog-tab-btn active">Date & Time</button>

          <button class="xp-dialog-tab-btn">Time Zone</button>

          <button class="xp-dialog-tab-btn">Internet Time</button>

        </div>

        <div class="xp-dialog-body" style="padding:14px; background:#ece9d8; flex:1; font-size:11px;">

          <div style="display:flex; gap:16px; align-items:center;">

            <div style="flex:1;">

              <label style="font-weight:700;">Date:</label><br>

              <div style="background:#ffffff; border:1px solid #716f64; padding:8px; border-radius:3px; margin-top:4px; text-align:center;">

                <b id="tdCurrentDateStr">${new Date().toDateString()}</b>

              </div>

            </div>

            <!-- Analog SVG Clock -->

            <div style="width:100px; height:100px; position:relative;">

              <svg width="100" height="100" viewBox="0 0 100 100">

                <circle cx="50" cy="50" r="46" fill="#ffffff" stroke="#716f64" stroke-width="2"/>

                <circle cx="50" cy="50" r="2.5" fill="#000000"/>

                <line id="analogHourHand" x1="50" y1="50" x2="50" y2="28" stroke="#000000" stroke-width="3" stroke-linecap="round"/>

                <line id="analogMinuteHand" x1="50" y1="50" x2="50" y2="18" stroke="#000000" stroke-width="2" stroke-linecap="round"/>

                <line id="analogSecondHand" x1="50" y1="50" x2="50" y2="14" stroke="#e11d48" stroke-width="1"/>

              </svg>

            </div>

          </div>

          <div style="margin-top:16px;">

            <label style="font-weight:700;">Current Time:</label>

            <div id="tdCurrentTimeDisplay" style="font-size:18px; font-weight:700; color:#0284c7; margin-top:2px; font-family:monospace;">

              ${new Date().toLocaleTimeString()}

            </div>

          </div>

        </div>

        <div style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end;">

          <button class="btn btn-primary btn-sm" id="timedateOkBtn" style="min-width:65px; background:#0284c7; color:#fff;">OK</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(timedateWin);

      window.XPCore.makeDraggable(timedateWin, timedateWin.querySelector('#timedateTitleHandle'));



      const closeTd = () => {

        timedateWin.style.display = 'none';

        if (clockInterval) clearInterval(clockInterval);

      };

      timedateWin.querySelector('#timedateCloseBtn').addEventListener('click', closeTd);

      timedateWin.querySelector('#timedateOkBtn').addEventListener('click', closeTd);



      // Analog clock animation

      const updateClock = () => {

        const d = new Date();

        const hr = d.getHours() % 12;

        const mn = d.getMinutes();

        const sc = d.getSeconds();



        const hrAngle = (hr * 30) + (mn * 0.5);

        const mnAngle = mn * 6;

        const scAngle = sc * 6;



        const hHand = timedateWin.querySelector('#analogHourHand');

        const mHand = timedateWin.querySelector('#analogMinuteHand');

        const sHand = timedateWin.querySelector('#analogSecondHand');

        const tDisp = timedateWin.querySelector('#tdCurrentTimeDisplay');



        if (hHand) hHand.setAttribute('transform', `rotate(${hrAngle} 50 50)`);

        if (mHand) mHand.setAttribute('transform', `rotate(${mnAngle} 50 50)`);

        if (sHand) sHand.setAttribute('transform', `rotate(${scAngle} 50 50)`);

        if (tDisp) tDisp.textContent = d.toLocaleTimeString();

      };

      clockInterval = setInterval(updateClock, 1000);

      updateClock();

    }



    timedateWin.style.display = 'flex';

    timedateWin.classList.remove('minimized');

    window.XPCore.bringToFront(timedateWin);

  }



  // -------------------------------------------------------------------------

  // 11. Classic Windows Calculator (calc.exe)

  // -------------------------------------------------------------------------

  let calcWin = null;

  let calcCurrentVal = '0';

  let calcPrevVal = null;

  let calcOp = null;

  let calcClearOnNext = false;



  function openCalculator() {

    if (!calcWin) {

      calcWin = document.createElement('div');

      calcWin.className = 'xp-window xp-calc-window';

      calcWin.id = 'unifiedCalcWindow';

      calcWin.style.left = '250px';

      calcWin.style.top = '100px';

      calcWin.style.width = '240px';

      calcWin.style.height = '290px';



      calcWin.innerHTML = `

        <div class="title-bar" id="calcTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/calc_120.png" class="xp-title-icon" alt="Calculator"> <span>Calculator</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="calcCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-window-menubar">

          <div class="xp-menu-item">Edit</div>

          <div class="xp-menu-item">View</div>

          <div class="xp-menu-item">Help</div>

        </div>

        <div style="padding:8px; background:#ece9d8; flex:1; display:flex; flex-direction:column; gap:6px;">

          <!-- LCD Display -->

          <div id="calcLcd" style="background:#ffffff; border:1px solid #716f64; padding:4px 8px; font-size:16px; font-weight:700; text-align:right; font-family:monospace; color:#000000; overflow:hidden;">

            0

          </div>

          <!-- Grid -->

          <div style="display:grid; grid-template-columns:repeat(5, 1fr); gap:4px; flex:1;">

            <button class="btn btn-sm xp-calc-btn" data-act="back">←</button>

            <button class="btn btn-sm xp-calc-btn" data-act="ce">CE</button>

            <button class="btn btn-sm xp-calc-btn" data-act="c">C</button>

            <button class="btn btn-sm xp-calc-btn" data-act="pm">±</button>

            <button class="btn btn-sm xp-calc-btn" data-act="sqrt">√</button>



            <button class="btn btn-sm xp-calc-btn num" data-val="7">7</button>

            <button class="btn btn-sm xp-calc-btn num" data-val="8">8</button>

            <button class="btn btn-sm xp-calc-btn num" data-val="9">9</button>

            <button class="btn btn-sm xp-calc-btn op" data-op="/">/</button>

            <button class="btn btn-sm xp-calc-btn" data-act="pct">%</button>



            <button class="btn btn-sm xp-calc-btn num" data-val="4">4</button>

            <button class="btn btn-sm xp-calc-btn num" data-val="5">5</button>

            <button class="btn btn-sm xp-calc-btn num" data-val="6">6</button>

            <button class="btn btn-sm xp-calc-btn op" data-op="*">*</button>

            <button class="btn btn-sm xp-calc-btn" data-act="inv">1/x</button>



            <button class="btn btn-sm xp-calc-btn num" data-val="1">1</button>

            <button class="btn btn-sm xp-calc-btn num" data-val="2">2</button>

            <button class="btn btn-sm xp-calc-btn num" data-val="3">3</button>

            <button class="btn btn-sm xp-calc-btn op" data-op="-">-</button>

            <button class="btn btn-sm xp-calc-btn" data-act="eq" style="grid-row:span 2; background:#0284c7; color:#fff;">=</button>



            <button class="btn btn-sm xp-calc-btn num" data-val="0" style="grid-column:span 2;">0</button>

            <button class="btn btn-sm xp-calc-btn" data-act="dot">.</button>

            <button class="btn btn-sm xp-calc-btn op" data-op="+">+</button>

          </div>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(calcWin);

      window.XPCore.makeDraggable(calcWin, calcWin.querySelector('#calcTitleHandle'));



      calcWin.querySelector('#calcCloseBtn').addEventListener('click', () => {

        calcWin.style.display = 'none';

      });



      const lcd = calcWin.querySelector('#calcLcd');

      const updateLcd = () => { lcd.textContent = calcCurrentVal; };



      calcWin.querySelectorAll('.xp-calc-btn').forEach(btn => {

        btn.addEventListener('click', () => {

          const val = btn.dataset.val;

          const act = btn.dataset.act;

          const op = btn.dataset.op;



          if (val !== undefined) {

            if (calcClearOnNext || calcCurrentVal === '0') {

              calcCurrentVal = val;

              calcClearOnNext = false;

            } else {

              calcCurrentVal += val;

            }

            updateLcd();

          } else if (op) {

            calcPrevVal = parseFloat(calcCurrentVal);

            calcOp = op;

            calcClearOnNext = true;

          } else if (act === 'eq') {

            if (calcOp && calcPrevVal !== null) {

              const cur = parseFloat(calcCurrentVal);

              let res = 0;

              if (calcOp === '+') res = calcPrevVal + cur;

              else if (calcOp === '-') res = calcPrevVal - cur;

              else if (calcOp === '*') res = calcPrevVal * cur;

              else if (calcOp === '/') res = cur !== 0 ? calcPrevVal / cur : 0;

              calcCurrentVal = String(res);

              calcOp = null;

              calcClearOnNext = true;

              updateLcd();

            }

          } else if (act === 'c') {

            calcCurrentVal = '0';

            calcPrevVal = null;

            calcOp = null;

            calcClearOnNext = false;

            updateLcd();

          } else if (act === 'ce') {

            calcCurrentVal = '0';

            updateLcd();

          } else if (act === 'dot') {

            if (!calcCurrentVal.includes('.')) calcCurrentVal += '.';

            updateLcd();

          }

        });

      });

    }



    calcWin.style.display = 'flex';

    calcWin.classList.remove('minimized');

    window.XPCore.bringToFront(calcWin);

  }



  // -------------------------------------------------------------------------

  // 12. Classic MS Paint (mspaint.exe)

  // -------------------------------------------------------------------------

  let paintWin = null;



  function openPaint() {

    if (!paintWin) {

      paintWin = document.createElement('div');

      paintWin.className = 'xp-window xp-paint-window';

      paintWin.id = 'unifiedPaintWindow';

      paintWin.style.left = '150px';

      paintWin.style.top = '40px';

      paintWin.style.width = '620px';

      paintWin.style.height = '460px';



      paintWin.innerHTML = `

        <div class="title-bar" id="paintTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/mspaint_1.png" class="xp-title-icon" alt="Paint"> <span>untitled - Paint</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn min" id="paintMinBtn" title="Minimize"></button>

            <button class="xp-caption-btn max" id="paintMaxBtn" title="Maximize"></button>

            <button class="xp-caption-btn close" id="paintCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-window-menubar">

          <div class="xp-menu-item">File

            <div class="xp-dropdown-menu">

              <div class="xp-drop-item" id="paintActClear"><u>N</u>ew</div>

              <div class="xp-drop-separator"></div>

              <div class="xp-drop-item" onclick="document.getElementById('unifiedPaintWindow').style.display='none'">E<u>x</u>it</div>

            </div>

          </div>

          <div class="xp-menu-item">Edit</div>

          <div class="xp-menu-item">View</div>

          <div class="xp-menu-item">Image</div>

          <div class="xp-menu-item">Colors</div>

          <div class="xp-menu-item">Help</div>

        </div>

        <div style="display:flex; flex:1; background:#ece9d8; overflow:hidden;">

          <!-- Tools Sidebar -->

          <div style="width:48px; border-right:1px solid #716f64; padding:4px; display:flex; flex-direction:column; gap:4px; align-items:center;">

            <button class="btn btn-sm active" id="ptToolPencil" title="Pencil" style="padding:2px 4px; font-size:10px; font-weight:bold;">Pen</button>

            <button class="btn btn-sm" id="ptToolBrush" title="Brush" style="padding:2px 4px; font-size:10px; font-weight:bold;">Brush</button>

            <button class="btn btn-sm" id="ptToolEraser" title="Eraser" style="padding:2px 4px; font-size:10px; font-weight:bold;">Erase</button>

            <button class="btn btn-sm" id="ptToolRect" title="Rectangle" style="padding:4px 6px;">▭</button>

          </div>

          <!-- Canvas Area -->

          <div style="flex:1; background:#808080; padding:10px; overflow:auto;">

            <canvas id="paintCanvas" width="500" height="320" style="background:#ffffff; border:1px solid #000; display:block; cursor:crosshair;"></canvas>

          </div>

        </div>

        <!-- Color Palette -->

        <div style="padding:4px 8px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; align-items:center; gap:6px;">

          <span>Color:</span>

          <div id="paintColors" style="display:flex; gap:3px;">

            ${['#000000', '#ffffff', '#7f7f7f', '#880015', '#ed1c24', '#ff7f27', '#fff200', '#22b14c', '#00a2e8', '#3f48cc', '#a349a4'].map(c => `

              <div class="paint-color-swatch" data-color="${c}" style="width:16px; height:16px; background:${c}; border:1px solid #000; cursor:pointer;"></div>

            `).join('')}

          </div>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(paintWin);

      window.XPCore.makeDraggable(paintWin, paintWin.querySelector('#paintTitleHandle'));

      window.XPCore.makeResizable(paintWin, 400, 300);



      paintWin.querySelector('#paintCloseBtn').addEventListener('click', () => { paintWin.style.display = 'none'; });

      paintWin.querySelector('#paintMinBtn').addEventListener('click', () => { paintWin.classList.add('minimized'); paintWin.style.display = 'none'; });



      // Canvas Drawing Logic

      const canvas = paintWin.querySelector('#paintCanvas');

      const ctx = canvas.getContext('2d');

      ctx.fillStyle = '#ffffff';

      ctx.fillRect(0, 0, canvas.width, canvas.height);



      let drawing = false;

      let curColor = '#000000';

      let curTool = 'pencil';



      paintWin.querySelectorAll('.paint-color-swatch').forEach(sw => {

        sw.addEventListener('click', () => { curColor = sw.dataset.color; });

      });



      paintWin.querySelector('#paintActClear').addEventListener('click', () => {

        ctx.fillStyle = '#ffffff';

        ctx.fillRect(0, 0, canvas.width, canvas.height);

      });



      canvas.addEventListener('mousedown', (e) => {

        drawing = true;

        ctx.beginPath();

        const r = canvas.getBoundingClientRect();

        ctx.moveTo(e.clientX - r.left, e.clientY - r.top);

      });



      canvas.addEventListener('mousemove', (e) => {

        if (!drawing) return;

        const r = canvas.getBoundingClientRect();

        ctx.strokeStyle = curColor;

        ctx.lineWidth = curTool === 'pencil' ? 1 : 4;

        ctx.lineTo(e.clientX - r.left, e.clientY - r.top);

        ctx.stroke();

      });



      window.addEventListener('mouseup', () => { drawing = false; });

    }



    paintWin.style.display = 'flex';

    paintWin.classList.remove('minimized');

    window.XPCore.bringToFront(paintWin);

  }



  function openExplorer(path = '') {

    const explorerWindow = document.getElementById('unifiedExplorerWindow');

    const taskBtnExplorer = document.getElementById('taskBtnExplorer');

    if (explorerWindow) {

      explorerWindow.classList.remove('minimized');

      explorerWindow.style.display = 'flex';

      if (window.XPCore) window.XPCore.bringToFront(explorerWindow, taskBtnExplorer);

      if (window.loadExplorerDirectory) window.loadExplorerDirectory(path);

    }

  }



  function openNotepad(filePath = '', content = '') {

    const notepadWindow = document.getElementById('unifiedNotepadWindow');

    const taskBtnNotepad = document.getElementById('taskBtnNotepad');

    if (notepadWindow) {

      notepadWindow.classList.remove('minimized');

      notepadWindow.style.display = 'flex';

      if (window.XPCore) window.XPCore.bringToFront(notepadWindow, taskBtnNotepad);

      if (content) {

        const ta = notepadWindow.querySelector('textarea');

        if (ta) ta.value = content;

      }

    }

  }



  function openCommandPrompt() {

    const cmdWindow = document.getElementById('cmdWindow');

    const taskBtnCmd = document.getElementById('taskBtnCmd');

    if (cmdWindow) {

      cmdWindow.classList.remove('minimized');

      cmdWindow.style.display = 'flex';

      if (window.XPCore) window.XPCore.bringToFront(cmdWindow, taskBtnCmd);

    }

  }







  // -------------------------------------------------------------------------

  // 15. Registry Editor (regedit.exe)

  // -------------------------------------------------------------------------

  let regeditWin = null;

  let activeRegistryPath = 'My Computer\\HKEY_CURRENT_USER\\Software\\Dory\\Settings';



  const REGISTRY_DATA = {

    'My Computer': {

      type: 'root',

      children: ['HKEY_CLASSES_ROOT', 'HKEY_CURRENT_USER', 'HKEY_LOCAL_MACHINE', 'HKEY_USERS', 'HKEY_CURRENT_CONFIG'],

      values: []

    },

    'My Computer\\HKEY_CLASSES_ROOT': {

      type: 'key',

      children: ['.txt', '.png', '.wav', '.sqlite', 'Directory', 'Drive'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CLASSES_ROOT\\.txt': {

      type: 'key',

      children: [],

      values: [{ name: '(Default)', type: 'REG_SZ', data: 'txtfile' }]

    },

    'My Computer\\HKEY_CLASSES_ROOT\\.png': {

      type: 'key',

      children: [],

      values: [{ name: '(Default)', type: 'REG_SZ', data: 'pngfile' }]

    },

    'My Computer\\HKEY_CLASSES_ROOT\\.wav': {

      type: 'key',

      children: [],

      values: [{ name: '(Default)', type: 'REG_SZ', data: 'soundfile' }]

    },

    'My Computer\\HKEY_CLASSES_ROOT\\.sqlite': {

      type: 'key',

      children: [],

      values: [{ name: '(Default)', type: 'REG_SZ', data: 'sqlitedb' }]

    },

    'My Computer\\HKEY_CLASSES_ROOT\\Directory': {

      type: 'key',

      children: ['shell'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: 'File Folder' }]

    },

    'My Computer\\HKEY_CLASSES_ROOT\\Drive': {

      type: 'key',

      children: ['shell'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: 'Drive' }]

    },

    'My Computer\\HKEY_CURRENT_USER': {

      type: 'key',

      children: ['Control Panel', 'Software'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_USER\\Control Panel': {

      type: 'key',

      children: ['Desktop', 'Appearance', 'Colors'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_USER\\Control Panel\\Desktop': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'Wallpaper', type: 'REG_SZ', data: 'bliss.bmp' },

        { name: 'TileWallpaper', type: 'REG_SZ', data: '0' },

        { name: 'ScreenSaveActive', type: 'REG_SZ', data: '1' },

        { name: 'ScreenSaveTimeOut', type: 'REG_SZ', data: '600' }

      ]

    },

    'My Computer\\HKEY_CURRENT_USER\\Control Panel\\Appearance': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'CurrentTheme', type: 'REG_SZ', data: 'Luna (Blue)' },

        { name: 'FontSize', type: 'REG_SZ', data: 'Normal' }

      ]

    },

    'My Computer\\HKEY_CURRENT_USER\\Control Panel\\Colors': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'ActiveCaption', type: 'REG_SZ', data: '0 84 227' },

        { name: 'GradientActiveCaption', type: 'REG_SZ', data: '61 149 255' },

        { name: 'Background', type: 'REG_SZ', data: '0 78 152' }

      ]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software': {

      type: 'key',

      children: ['Dory', 'Microsoft'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software\\Dory': {

      type: 'key',

      children: ['Settings'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software\\Dory\\Settings': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'CognitiveEngine', type: 'REG_SZ', data: 'NVIDIA Nemotron 4B' },

        { name: 'RecurrentLoops', type: 'REG_DWORD', data: '0x00000002 (2)' },

        { name: 'Temperature', type: 'REG_SZ', data: '0.7' },

        { name: 'TopP', type: 'REG_SZ', data: '0.9' },

        { name: 'MaxTokens', type: 'REG_DWORD', data: '0x00001000 (4096)' },

        { name: 'ReasoningEffort', type: 'REG_SZ', data: 'high' },

        { name: 'SandboxVM', type: 'REG_SZ', data: 'Z:\\workspace (Linux Isolation)' },

        { name: 'SystemPrompt', type: 'REG_SZ', data: 'You are Dory, an expert autonomous cognitive agent.' }

      ]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software\\Microsoft': {

      type: 'key',

      children: ['Windows', 'Windows Live Messenger'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software\\Microsoft\\Windows Live Messenger': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'UserName', type: 'REG_SZ', data: 'User' },

        { name: 'Status', type: 'REG_SZ', data: 'Available' },

        { name: 'ClientVersion', type: 'REG_SZ', data: 'Wave 4 (14.0.8117.416)' }

      ]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software\\Microsoft\\Windows': {

      type: 'key',

      children: ['CurrentVersion'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion': {

      type: 'key',

      children: ['Explorer'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'ShellState', type: 'REG_BINARY', data: '24 00 00 00 3c 28 00 00' },

        { name: 'CleanShutdown', type: 'REG_DWORD', data: '0x00000001 (1)' }

      ]

    },

    'My Computer\\HKEY_LOCAL_MACHINE': {

      type: 'key',

      children: ['HARDWARE', 'SOFTWARE', 'SYSTEM'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\HARDWARE': {

      type: 'key',

      children: ['DESCRIPTION'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION': {

      type: 'key',

      children: ['System'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System': {

      type: 'key',

      children: ['CentralProcessor'],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'Identifier', type: 'REG_SZ', data: 'AT/AT COMPATIBLE' },

        { name: 'SystemBiosDate', type: 'REG_SZ', data: '08/04/08' }

      ]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor': {

      type: 'key',

      children: ['0'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'ProcessorNameString', type: 'REG_SZ', data: 'AMD Ryzen / EPYC 16-Core Processor' },

        { name: '~MHz', type: 'REG_DWORD', data: '0x00000dbba (3500)' }

      ]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE': {

      type: 'key',

      children: ['Microsoft'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft': {

      type: 'key',

      children: ['Windows NT'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT': {

      type: 'key',

      children: ['CurrentVersion'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'ProductName', type: 'REG_SZ', data: 'Microsoft Windows XP' },

        { name: 'CurrentVersion', type: 'REG_SZ', data: '5.1' },

        { name: 'CurrentBuildNumber', type: 'REG_SZ', data: '2600' },

        { name: 'CSDVersion', type: 'REG_SZ', data: 'Service Pack 3' },

        { name: 'CSDBuildNumber', type: 'REG_SZ', data: '1058' },

        { name: 'RegisteredOwner', type: 'REG_SZ', data: 'Rackneh' },

        { name: 'RegisteredOrganization', type: 'REG_SZ', data: 'IBA Laboratory' },

        { name: 'SystemRoot', type: 'REG_SZ', data: 'C:\\WINDOWS' }

      ]

    },

    'My Computer\\HKEY_USERS': {

      type: 'key',

      children: ['.DEFAULT'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_USERS\\.DEFAULT': {

      type: 'key',

      children: ['Control Panel'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_CONFIG': {

      type: 'key',

      children: ['Display', 'System'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_CONFIG\\Display': {

      type: 'key',

      children: ['Settings'],

      values: [{ name: '(Default)', type: 'REG_SZ', data: '(value not set)' }]

    },

    'My Computer\\HKEY_CURRENT_CONFIG\\Display\\Settings': {

      type: 'key',

      children: [],

      values: [

        { name: '(Default)', type: 'REG_SZ', data: '(value not set)' },

        { name: 'Resolution', type: 'REG_SZ', data: '1920x1080' },

        { name: 'BitsPerPixel', type: 'REG_DWORD', data: '0x00000020 (32)' }

      ]

    }

  };



  function openRegedit() {

    if (!regeditWin) {

      regeditWin = document.createElement('div');

      regeditWin.className = 'xp-window xp-regedit-window';

      regeditWin.id = 'unifiedRegeditWindow';

      regeditWin.style.left = '90px';

      regeditWin.style.top = '50px';

      regeditWin.style.width = '640px';

      regeditWin.style.height = '440px';



      regeditWin.innerHTML = `

        <div class="title-bar" id="regeditTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/regedit_100.png" class="xp-title-icon" alt="Registry">

            <span>Registry Editor</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn minimize" id="regeditMinBtn" title="Minimize"></button>

            <button class="xp-caption-btn maximize" id="regeditMaxBtn" title="Maximize"></button>

            <button class="xp-caption-btn close" id="regeditCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-regedit-menubar">

          <span>File</span>

          <span>Edit</span>

          <span>View</span>

          <span>Favorites</span>

          <span>Help</span>

        </div>

        <div class="xp-regedit-split">

          <div class="xp-regedit-tree" id="regeditTreeCont"></div>

          <div class="xp-regedit-list" id="regeditListCont">

            <table class="xp-regedit-table">

              <thead>

                <tr>

                  <th style="width:40%;">Name</th>

                  <th style="width:25%;">Type</th>

                  <th style="width:35%;">Data</th>

                </tr>

              </thead>

              <tbody id="regeditTableBody"></tbody>

            </table>

          </div>

        </div>

        <div class="xp-regedit-statusbar" id="regeditStatusPath">

          My Computer\\HKEY_CURRENT_USER\\Software\\Dory\\Settings

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(regeditWin);

      window.XPCore.makeDraggable(regeditWin, regeditWin.querySelector('#regeditTitleHandle'));

      window.XPCore.makeResizable(regeditWin, 420, 300);



      const renderTree = () => {

        const treeCont = regeditWin.querySelector('#regeditTreeCont');

        if (!treeCont) return;

        treeCont.innerHTML = '';



        const roots = ['My Computer', 'My Computer\\HKEY_CLASSES_ROOT', 'My Computer\\HKEY_CURRENT_USER', 'My Computer\\HKEY_CURRENT_USER\\Software\\Dory\\Settings', 'My Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion'];



        Object.keys(REGISTRY_DATA).forEach(path => {

          const parts = path.split('\\');

          const depth = parts.length - 1;

          const nodeName = parts[parts.length - 1];

          const isSelected = path === activeRegistryPath;

          const node = document.createElement('div');

          node.className = `xp-regedit-tree-node ${isSelected ? 'selected' : ''}`;

          node.style.paddingLeft = `${depth * 14 + 4}px`;



          const isRoot = path === 'My Computer';

          const iconSrc = isRoot ? '/static/xp_assets/icons/my_computer.png' : (isSelected ? '/static/xp_assets/icons/shell32_5.png' : '/static/xp_assets/icons/shell32_4.png');



          node.innerHTML = `

            <img src="${iconSrc}" width="16" height="16" alt="folder">

            <span>${nodeName}</span>

          `;



          node.addEventListener('click', () => {

            activeRegistryPath = path;

            renderTree();

            renderValues(path);

            const status = regeditWin.querySelector('#regeditStatusPath');

            if (status) status.textContent = path;

          });



          treeCont.appendChild(node);

        });

      };



      const renderValues = (path) => {

        const body = regeditWin.querySelector('#regeditTableBody');

        if (!body) return;

        body.innerHTML = '';

        const entry = REGISTRY_DATA[path] || { values: [] };



        entry.values.forEach(v => {

          const tr = document.createElement('tr');

          const isString = v.type === 'REG_SZ';

          const icon = isString ? '/static/xp_assets/icons/regedit_205.png' : '/static/xp_assets/icons/regedit_206.png';



          tr.innerHTML = `

            <td style="display:flex; align-items:center; gap:6px;">

              <img src="${icon}" width="16" height="16" alt="${v.type}">

              <span>${v.name}</span>

            </td>

            <td>${v.type}</td>

            <td>${v.data}</td>

          `;



          tr.addEventListener('click', () => {

            body.querySelectorAll('tr').forEach(r => r.classList.remove('selected'));

            tr.classList.add('selected');

          });



          body.appendChild(tr);

        });

      };



      renderTree();

      renderValues(activeRegistryPath);



      const closeReg = () => { regeditWin.style.display = 'none'; };

      regeditWin.querySelector('#regeditCloseBtn').addEventListener('click', closeReg);

      regeditWin.querySelector('#regeditMinBtn').addEventListener('click', closeReg);

      regeditWin.querySelector('#regeditMaxBtn').addEventListener('click', () => {

        regeditWin.classList.toggle('maximized');

      });

    }



    regeditWin.style.display = 'flex';

    regeditWin.classList.remove('minimized');

    window.XPCore.bringToFront(regeditWin);

  }



  // -------------------------------------------------------------------------

  // 16. About Windows (winver.exe)

  // -------------------------------------------------------------------------

  let winverWin = null;

  function openWinver() {

    if (!winverWin) {

      winverWin = document.createElement('div');

      winverWin.className = 'xp-window xp-winver-window';

      winverWin.id = 'unifiedWinverWindow';

      winverWin.style.left = '180px';

      winverWin.style.top = '100px';

      winverWin.style.width = '420px';

      winverWin.style.height = '370px';



      winverWin.innerHTML = `

        <div class="title-bar" id="winverTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/shell32_24.png" class="xp-title-icon" alt="Winver">

            <span>About Windows</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="winverCloseBtn" title="Close"></button>

          </div>

        </div>

        <div class="xp-winver-banner">

          <img src="/static/xp_assets/bitmaps/msgina_bmp_101.png" alt="Microsoft Windows XP">

        </div>

        <div class="xp-winver-body">

          <div style="font-weight:bold; font-size:12px; margin-bottom:4px;">Microsoft® Windows® XP Professional</div>

          <div style="margin-bottom:6px; color:#333;">Version 5.1 (Build 2600.xpsp_sp3_gdr.080814-1236 : Service Pack 3)</div>

          <div style="margin-bottom:10px; color:#444;">Copyright © 1985-2001 Microsoft Corporation</div>

          <div class="xp-winver-hr"></div>

          <div style="font-size:11px; margin-bottom:8px;">

            An experimental agentic workstation for Dory LLM and the IBA Laboratory workspace.

          </div>

          <div style="font-size:11px; margin-bottom:2px;">This product is licensed to:</div>

          <div style="font-weight:bold; font-size:11px; margin-left:14px;">Rackneh</div>

          <div style="font-weight:bold; font-size:11px; margin-left:14px; margin-bottom:10px;">IBA Laboratory / Inference Lighting Lab</div>

          <div class="xp-winver-hr"></div>

          <div style="font-size:11px; color:#333;">Physical memory available to Windows: 16,777,216 KB</div>

          <div style="margin-top:16px; display:flex; justify-content:flex-end;">

            <button class="btn btn-primary btn-sm" id="winverOkBtn" style="min-width:75px; background:#0284c7; color:#fff;">OK</button>

          </div>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(winverWin);

      window.XPCore.makeDraggable(winverWin, winverWin.querySelector('#winverTitleHandle'));



      const closeWv = () => { winverWin.style.display = 'none'; };

      winverWin.querySelector('#winverCloseBtn').addEventListener('click', closeWv);

      winverWin.querySelector('#winverOkBtn').addEventListener('click', () => {

        if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();

        closeWv();

      });

    }



    winverWin.style.display = 'flex';

    winverWin.classList.remove('minimized');

    window.XPCore.bringToFront(winverWin);

  }



  // -------------------------------------------------------------------------

  // 17. Dory Bot & Neural Engine Properties (dory_settings.cpl)

  // -------------------------------------------------------------------------

  let dorySettingsWin = null;

  function openDorySettings() {

    if (!dorySettingsWin) {

      dorySettingsWin = document.createElement('div');

      dorySettingsWin.className = 'xp-window xp-dory-settings-window';

      dorySettingsWin.id = 'unifiedDorySettingsWindow';

      dorySettingsWin.style.left = '140px';

      dorySettingsWin.style.top = '60px';

      dorySettingsWin.style.width = '450px';

      dorySettingsWin.style.height = '480px';



      dorySettingsWin.innerHTML = `

        <div class="title-bar" id="dorySettingsTitleHandle">

          <div class="title-bar-text">

            <img src="/static/xp_assets/icons/shell32_167.png" class="xp-title-icon" alt="Settings">

            <span>Dory Bot &amp; Neural Engine Properties</span>

          </div>

          <div class="title-bar-controls">

            <button class="xp-caption-btn close" id="dorySettingsCloseBtn" title="Close"></button>

          </div>

        </div>



        <div class="xp-dialog-tabs">

          <button class="xp-dialog-tab-btn active" data-tab="tabDoryModel">Model &amp; Recurrence</button>

          <button class="xp-dialog-tab-btn" data-tab="tabDorySampling">Sampling</button>

          <button class="xp-dialog-tab-btn" data-tab="tabDoryPersona">Persona</button>

          <button class="xp-dialog-tab-btn" data-tab="tabDorySandbox">Sandbox VM</button>

        </div>



        <div class="xp-dialog-body" style="padding:10px 14px; flex:1; overflow-y:auto; background:#ece9d8;">

          <!-- Tab 1: Model & Recurrence -->

          <div id="tabDoryModel" class="xp-tab-panel active">

            <div class="xp-cpl-field-group">

              <div class="xp-cpl-field-title">Cognitive Engine Architecture</div>

              <div style="font-size:11px; margin-bottom:8px; color:#475569;">

                Configure the recurrent cognitive loop engine for autonomous multi-step execution.

              </div>

              <div style="margin-bottom:8px;">

                <label style="font-size:11px; font-weight:700;">Base Model:</label>

                <input type="text" id="doryModelNameInput" class="form-control" style="width:100%; padding:3px 6px; font-size:11px;" value="NVIDIA Nemotron 4B Instruct" readonly>

              </div>

              <div style="margin-bottom:8px;">

                <label style="font-size:11px; font-weight:700;">Recurrent Loops per Turn:</label>

                <select id="doryLoopsSelect" class="form-select" style="width:100%; padding:3px; font-size:11px;">

                  <option value="1">1 Loop (Fast Standard Response)</option>

                  <option value="2" selected>2 Loops (Self-Critique &amp; Refinement - Recommended)</option>

                  <option value="4">4 Loops (Deep Multi-Pass Reasoning)</option>

                </select>

              </div>

              <div style="margin-bottom:4px;">

                <label style="font-size:11px; font-weight:700;">Reasoning Effort:</label>

                <select id="doryReasoningEffortSelect" class="form-select" style="width:100%; padding:3px; font-size:11px;">

                  <option value="low">Low (Brief rationale)</option>

                  <option value="medium">Medium (Detailed breakdown)</option>

                  <option value="high" selected>High (Comprehensive deep think)</option>

                </select>

              </div>

            </div>

          </div>



          <!-- Tab 2: Sampling -->

          <div id="tabDorySampling" class="xp-tab-panel">

            <div class="xp-cpl-field-group">

              <div class="xp-cpl-field-title">Generation Parameters</div>

              <div style="margin-bottom:8px;">

                <div style="display:flex; justify-content:space-between; font-size:11px; font-weight:700;">

                  <span>Temperature:</span> <span id="doryTempValLabel">0.7</span>

                </div>

                <input type="range" id="doryTempSlider" min="0" max="1.5" step="0.05" value="0.7" style="width:100%;">

              </div>

              <div style="margin-bottom:8px;">

                <div style="display:flex; justify-content:space-between; font-size:11px; font-weight:700;">

                  <span>Top-P:</span> <span id="doryTopPValLabel">0.9</span>

                </div>

                <input type="range" id="doryTopPSlider" min="0.1" max="1.0" step="0.05" value="0.9" style="width:100%;">

              </div>

              <div style="margin-bottom:8px;">

                <div style="display:flex; justify-content:space-between; font-size:11px; font-weight:700;">

                  <span>Max Tokens:</span> <span id="doryMaxTokensValLabel">4096</span>

                </div>

                <input type="range" id="doryMaxTokensSlider" min="512" max="8192" step="256" value="4096" style="width:100%;">

              </div>

            </div>

          </div>



          <!-- Tab 3: Persona -->

          <div id="tabDoryPersona" class="xp-tab-panel">

            <div class="xp-cpl-field-group">

              <div class="xp-cpl-field-title">System Prompt &amp; Identity</div>

              <div style="margin-bottom:8px;">

                <label style="font-size:11px; font-weight:700;">Bot Display Name:</label>

                <input type="text" id="doryDisplayNameInput" class="form-control" style="width:100%; padding:3px 6px; font-size:11px;" value="Dory 1.0 (4B Bot)">

              </div>

              <div style="margin-bottom:4px;">

                <label style="font-size:11px; font-weight:700;">System Prompt:</label>

                <textarea id="dorySystemPromptInput" class="form-control" style="width:100%; height:110px; font-family:monospace; font-size:11px; padding:4px;">You are Dory, an expert autonomous cognitive agent operating within the IBA project workspace and Windows XP workstation simulator.</textarea>

              </div>

            </div>

          </div>



          <!-- Tab 4: Sandbox VM -->

          <div id="tabDorySandbox" class="xp-tab-panel">

            <div class="xp-cpl-field-group">

              <div class="xp-cpl-field-title">Linux Sandbox VM (Network Drive Z:) Isolation</div>

              <div style="font-size:11px; color:#475569; margin-bottom:10px;">

                All automated file edits, bash scripts, and builds run strictly inside the sandbox environment.

              </div>

              <div style="display:flex; align-items:center; gap:8px; margin-bottom:8px;">

                <input type="checkbox" id="doryStrictIsolationChk" checked>

                <label for="doryStrictIsolationChk" style="font-size:11px; font-weight:700;">Enable Strict Path Jail (Z:\\workspace)</label>

              </div>

              <div style="display:flex; align-items:center; gap:8px;">

                <input type="checkbox" id="doryHostApprovalChk" checked>

                <label for="doryHostApprovalChk" style="font-size:11px; font-weight:700;">Require User Approval Card for Host Actions</label>

              </div>

            </div>

          </div>

        </div>



        <div style="padding:8px 14px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end; gap:6px;">

          <button class="btn btn-primary btn-sm" id="dorySettingsOkBtn" style="min-width:65px; background:#0284c7; color:#fff;">OK</button>

          <button class="btn btn-secondary btn-sm" id="dorySettingsCancelBtn" style="min-width:65px;">Cancel</button>

          <button class="btn btn-secondary btn-sm" id="dorySettingsApplyBtn" style="min-width:65px;">Apply</button>

        </div>

      `;



      const desktop = document.getElementById('unifiedXpDesktop');

      if (desktop) desktop.appendChild(dorySettingsWin);

      window.XPCore.makeDraggable(dorySettingsWin, dorySettingsWin.querySelector('#dorySettingsTitleHandle'));



      // Tab switching

      dorySettingsWin.querySelectorAll('.xp-dialog-tab-btn').forEach(btn => {

        btn.addEventListener('click', () => {

          dorySettingsWin.querySelectorAll('.xp-dialog-tab-btn').forEach(b => b.classList.remove('active'));

          dorySettingsWin.querySelectorAll('.xp-tab-panel').forEach(p => p.classList.remove('active'));

          btn.classList.add('active');

          const panel = dorySettingsWin.querySelector(`#${btn.dataset.tab}`);

          if (panel) panel.classList.add('active');

        });

      });



      // Live sliders

      const tempSlider = dorySettingsWin.querySelector('#doryTempSlider');

      const topPSlider = dorySettingsWin.querySelector('#doryTopPSlider');

      const maxTokensSlider = dorySettingsWin.querySelector('#doryMaxTokensSlider');

      tempSlider.addEventListener('input', () => { dorySettingsWin.querySelector('#doryTempValLabel').textContent = tempSlider.value; });

      topPSlider.addEventListener('input', () => { dorySettingsWin.querySelector('#doryTopPValLabel').textContent = topPSlider.value; });

      maxTokensSlider.addEventListener('input', () => { dorySettingsWin.querySelector('#doryMaxTokensValLabel').textContent = maxTokensSlider.value; });



      // Apply changes

      const applyDorySettings = () => {

        const loops = dorySettingsWin.querySelector('#doryLoopsSelect').value;

        const temp = tempSlider.value;

        const topP = topPSlider.value;

        const maxTok = maxTokensSlider.value;

        const prompt = dorySettingsWin.querySelector('#dorySystemPromptInput').value;



        // Synchronize with main UI controls

        const mainTemp = document.getElementById('tempRange');

        if (mainTemp) { mainTemp.value = temp; document.getElementById('tempVal').textContent = temp; }

        const mainTopP = document.getElementById('topPRange');

        if (mainTopP) { mainTopP.value = topP; document.getElementById('topPVal').textContent = topP; }

        const mainMaxTok = document.getElementById('maxTokens');

        if (mainMaxTok) { mainMaxTok.value = maxTok; document.getElementById('maxTokensVal').textContent = maxTok; }

        const mainPrompt = document.getElementById('systemPrompt');

        if (mainPrompt) mainPrompt.value = prompt;



        // Update Registry

        if (REGISTRY_DATA['My Computer\\HKEY_CURRENT_USER\\Software\\Dory\\Settings']) {

          const vals = REGISTRY_DATA['My Computer\\HKEY_CURRENT_USER\\Software\\Dory\\Settings'].values;

          vals.forEach(v => {

            if (v.name === 'RecurrentLoops') v.data = `0x0000000${loops} (${loops})`;

            if (v.name === 'Temperature') v.data = temp;

            if (v.name === 'TopP') v.data = topP;

            if (v.name === 'MaxTokens') v.data = `0x${parseInt(maxTok).toString(16).padStart(8, '0')} (${maxTok})`;

            if (v.name === 'SystemPrompt') v.data = prompt;

          });

        }



        if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playDing();

      };



      const closeDorySettings = () => { dorySettingsWin.style.display = 'none'; };

      dorySettingsWin.querySelector('#dorySettingsCloseBtn').addEventListener('click', closeDorySettings);

      dorySettingsWin.querySelector('#dorySettingsCancelBtn').addEventListener('click', closeDorySettings);

      dorySettingsWin.querySelector('#dorySettingsApplyBtn').addEventListener('click', applyDorySettings);

      dorySettingsWin.querySelector('#dorySettingsOkBtn').addEventListener('click', () => {

        applyDorySettings();

        closeDorySettings();

      });

    }



    dorySettingsWin.style.display = 'flex';

    dorySettingsWin.classList.remove('minimized');

    window.XPCore.bringToFront(dorySettingsWin);

  }



  
  // -------------------------------------------------------------------------
  // Windows XP CD-ROM Autorun Launcher (SETUP.EXE)
  // -------------------------------------------------------------------------
  let autorunWin = null;
  function openAutorun() {
    if (!autorunWin) {
      autorunWin = document.createElement('div');
      autorunWin.className = 'xp-window xp-autorun-window';
      autorunWin.id = 'unifiedAutorunWindow';
      autorunWin.style.left = '120px';
      autorunWin.style.top = '60px';
      autorunWin.style.width = '600px';
      autorunWin.style.height = '430px';

      autorunWin.innerHTML = `
        <div class="title-bar" id="autorunTitleHandle">
          <div class="title-bar-text">
            <img src="/static/xp_assets/icons/drive.png" class="xp-title-icon" alt="CD">
            <span>Welcome to Microsoft Windows XP</span>
          </div>
          <div class="title-bar-controls">
            <button class="xp-caption-btn close" id="autorunCloseBtn" title="Close"></button>
          </div>
        </div>
        <div class="xp-autorun-header"></div>
        <div class="xp-autorun-body">
          <div class="xp-autorun-sidebar"></div>
          <div class="xp-autorun-menu" id="autorunMainMenu">
            <div class="xp-autorun-title">What do you want to do?</div>
            <div class="xp-autorun-item" id="arInstallBtn">
              <div class="xp-autorun-item-arrow"></div>
              <span>Install Windows XP</span>
            </div>
            <div class="xp-autorun-item" id="arTasksBtn">
              <div class="xp-autorun-item-arrow"></div>
              <span>Perform additional tasks</span>
            </div>
            <div class="xp-autorun-item" id="arCheckBtn">
              <div class="xp-autorun-item-arrow"></div>
              <span>Check system compatibility</span>
            </div>
            <div class="xp-autorun-item" id="arExitBtn" style="margin-top:auto;">
              <div class="xp-autorun-item-arrow"></div>
              <span>Exit</span>
            </div>
          </div>
          <!-- Submenu -->
          <div class="xp-autorun-menu" id="autorunSubMenu" style="display:none;">
            <div class="xp-autorun-title">Additional Tasks</div>
            <div class="xp-autorun-item" id="arBrowseCdBtn">
              <div class="xp-autorun-item-arrow"></div>
              <span>Browse this CD-ROM in Windows Explorer</span>
            </div>
            <div class="xp-autorun-item" id="arRemoteDesktopBtn">
              <div class="xp-autorun-item-arrow"></div>
              <span>Set up Remote Desktop Connection</span>
            </div>
            <div class="xp-autorun-item" id="arNetworkWizBtn">
              <div class="xp-autorun-item-arrow"></div>
              <span>Set up a home or small office network</span>
            </div>
            <div class="xp-autorun-item" id="arSubBackBtn" style="margin-top:auto;">
              <div class="xp-autorun-item-arrow"></div>
              <span>&lt; Back</span>
            </div>
          </div>
        </div>
      `;

      getDesktopContainer().appendChild(autorunWin);
      if (window.XPCore) {
        window.XPCore.makeDraggable(autorunWin, autorunWin.querySelector('#autorunTitleHandle'));
      }

      autorunWin.querySelector('#autorunCloseBtn').onclick = () => { autorunWin.style.display = 'none'; };
      autorunWin.querySelector('#arExitBtn').onclick = () => { autorunWin.style.display = 'none'; };

      autorunWin.querySelector('#arInstallBtn').onclick = () => {
        autorunWin.style.display = 'none';
        if (window.XPInstaller && window.XPInstaller.startSetup) {
          window.XPInstaller.startSetup();
        }
      };

      autorunWin.querySelector('#arTasksBtn').onclick = () => {
        autorunWin.querySelector('#autorunMainMenu').style.display = 'none';
        autorunWin.querySelector('#autorunSubMenu').style.display = 'flex';
      };

      autorunWin.querySelector('#arSubBackBtn').onclick = () => {
        autorunWin.querySelector('#autorunSubMenu').style.display = 'none';
        autorunWin.querySelector('#autorunMainMenu').style.display = 'flex';
      };

      autorunWin.querySelector('#arBrowseCdBtn').onclick = () => {
        autorunWin.style.display = 'none';
        if (window.XPApps && window.XPApps.openExplorer) {
          window.XPApps.openExplorer('E:');
        }
      };

      autorunWin.querySelector('#arCheckBtn').onclick = () => {
        alert("Microsoft Windows XP Compatibility Check\n\nAll components passed compatibility inspection.\n\n• Processor: Intel Pentium / Core compatible (Passed)\n• Memory: 3.25 GB RAM detected (Passed)\n• Video: DirectDraw / DirectX 9.0c hardware acceleration (Passed)\n• Disk: 24.6 GB available space (Passed)\n\nYour computer is 100% ready for Windows XP.");
      };

      autorunWin.querySelector('#arRemoteDesktopBtn').onclick = () => {
        alert("Remote Desktop Connection is already installed and ready on this workstation.");
      };

      autorunWin.querySelector('#arNetworkWizBtn').onclick = () => {
        if (window.XPApps && window.XPApps.openNetworkConnections) {
          window.XPApps.openNetworkConnections();
        }
      };
    }

    autorunWin.style.display = 'flex';
    autorunWin.classList.remove('minimized');
    if (window.XPCore) window.XPCore.bringToFront(autorunWin);
  }

  // -------------------------------------------------------------------------
  // Search Companion with Rover the Dog / Merlin
  // -------------------------------------------------------------------------
  let searchWin = null;
  function openSearchCompanion() {
    if (!searchWin) {
      searchWin = document.createElement('div');
      searchWin.className = 'xp-window xp-search-companion-window';
      searchWin.id = 'unifiedSearchCompanionWindow';
      searchWin.style.left = '80px';
      searchWin.style.top = '50px';
      searchWin.style.width = '640px';
      searchWin.style.height = '480px';

      searchWin.innerHTML = `
        <div class="title-bar" id="searchTitleHandle">
          <div class="title-bar-text">
            <img src="/static/xp_assets/icons/search.png" class="xp-title-icon" alt="Search">
            <span>Search Results - Windows Explorer</span>
          </div>
          <div class="title-bar-controls">
            <button class="xp-caption-btn close" id="searchCloseBtn" title="Close"></button>
          </div>
        </div>
        <div class="xp-explorer-menubar">
          <span class="xp-menu-top-item">File</span>
          <span class="xp-menu-top-item">Edit</span>
          <span class="xp-menu-top-item">View</span>
          <span class="xp-menu-top-item">Favorites</span>
          <span class="xp-menu-top-item">Tools</span>
          <span class="xp-menu-top-item">Help</span>
        </div>
        <div class="xp-explorer-toolbar">
          <button class="xp-tb-back-btn" title="Back"></button>
          <button class="xp-tb-fwd-btn" title="Forward"></button>
          <div class="xp-tb-sep"></div>
          <button class="xp-tb-btn active"><img src="/static/xp_assets/icons/search.png"><span>Search</span></button>
          <button class="xp-tb-btn"><img src="/static/xp_assets/icons/shell32_45.png"><span>Folders</span></button>
        </div>
        <div style="flex:1; display:flex; background:#ffffff; overflow:hidden;">
          <!-- Left Search Companion Sidebar with Rover/Merlin -->
          <div style="width:230px; background:#e3e9ef; border-right:1px solid #7f9db9; display:flex; flex-direction:column; padding:12px; box-sizing:border-box;">
            <div class="xp-search-rover-bubble" id="searchBubble">
              What are you searching for? Type all or part of a file name below.
            </div>
            <div style="text-align:center; margin-bottom:12px;">
              <img src="/static/xp_assets/tour/MERLIN.GIf" id="searchAssistantImg" style="width:68px; height:71px; image-rendering:pixelated;" alt="Assistant">
            </div>
            <div style="font-size:11px; font-weight:700; color:#0c4a6e; margin-bottom:4px;">All or part of the file name:</div>
            <input type="text" id="searchInputQuery" placeholder="e.g. *.txt, code, model" style="width:100%; font-size:11px; padding:3px 6px; border:1px solid #7f9db9; margin-bottom:8px; box-sizing:border-box;">
            <div style="font-size:11px; font-weight:700; color:#0c4a6e; margin-bottom:4px;">Look in:</div>
            <select id="searchLookIn" style="width:100%; font-size:11px; padding:2px; border:1px solid #7f9db9; margin-bottom:14px; box-sizing:border-box;">
              <option value="workspace">Active Project Workspace (D:)</option>
              <option value="C:">Local Hard Drives (C:)</option>
              <option value="E:">Windows XP CD-ROM (E:)</option>
              <option value="all">My Computer (All Drives)</option>
            </select>
            <div style="display:flex; justify-content:flex-end; gap:6px;">
              <button class="btn btn-primary btn-sm" id="searchExecBtn" style="background:#0284c7; color:#fff; font-size:11px; padding:3px 12px;">Search</button>
            </div>
          </div>
          <!-- Right Results View -->
          <div style="flex:1; display:flex; flex-direction:column; background:#ffffff;">
            <div id="searchResultsList" style="flex:1; overflow-y:auto; padding:10px;">
              <div style="color:#64748b; font-size:11px; font-style:italic;">Enter search criteria and click Search to begin.</div>
            </div>
            <div class="xp-explorer-statusbar">
              <span id="searchResultCount">0 object(s) found</span>
            </div>
          </div>
        </div>
      `;

      getDesktopContainer().appendChild(searchWin);
      if (window.XPCore) window.XPCore.makeDraggable(searchWin, searchWin.querySelector('#searchTitleHandle'));
      searchWin.querySelector('#searchCloseBtn').onclick = () => { searchWin.style.display = 'none'; };

      const execSearch = async () => {
        const query = searchWin.querySelector('#searchInputQuery').value.trim().toLowerCase();
        const lookIn = searchWin.querySelector('#searchLookIn').value;
        const resBox = searchWin.querySelector('#searchResultsList');
        const countBox = searchWin.querySelector('#searchResultCount');
        resBox.innerHTML = '<div style="color:#0284c7; font-size:11px;">Searching files...</div>';

        try {
          let pathArg = lookIn === 'C:' ? 'C:' : (lookIn === 'E:' ? 'E:' : '');
          const resp = await fetch(`/api/iba/fs/browse?subpath=${encodeURIComponent(pathArg)}`);
          if (resp.ok) {
            const data = await resp.json();
            const items = data.items || [];
            const filtered = query ? items.filter(it => it.name.toLowerCase().includes(query)) : items;
            countBox.textContent = `${filtered.length} object(s) found`;
            if (filtered.length === 0) {
              resBox.innerHTML = '<div style="color:#64748b; font-size:11px;">No matching items found.</div>';
            } else {
              resBox.innerHTML = '';
              filtered.forEach(it => {
                const row = document.createElement('div');
                row.style.cssText = 'display:flex; align-items:center; gap:8px; padding:4px 6px; cursor:pointer; font-size:11px; border-bottom:1px solid #f1f5f9;';
                row.innerHTML = `<img src="/static/xp_assets/icons/${it.is_dir ? 'folder.png' : 'notepad.png'}" style="width:16px; height:16px;"> <span>${it.name}</span> <span style="margin-left:auto; color:#94a3b8; font-size:10px;">${it.is_dir ? 'Folder' : (it.size + ' bytes')}</span>`;
                row.onmouseover = () => { row.style.background = '#e0f2fe'; };
                row.onmouseout = () => { row.style.background = 'transparent'; };
                row.ondblclick = () => {
                  if (it.is_dir && window.XPApps) window.XPApps.openExplorer(it.path);
                  else if (window.openFileInNotepad) window.openFileInNotepad(it.path);
                };
                resBox.appendChild(row);
              });
            }
          }
        } catch (e) {
          resBox.innerHTML = '<div style="color:#dc2626; font-size:11px;">Search error.</div>';
        }
      };

      searchWin.querySelector('#searchExecBtn').onclick = execSearch;
      searchWin.querySelector('#searchInputQuery').onkeydown = (e) => { if (e.key === 'Enter') execSearch(); };
    }

    searchWin.style.display = 'flex';
    searchWin.classList.remove('minimized');
    if (window.XPCore) window.XPCore.bringToFront(searchWin);
  }

  function openSetupWizard() {
    if (window.XPInstaller && window.XPInstaller.startSetup) {
      window.XPInstaller.startSetup();
    }
  }

return {

    openDisplayProperties,

    openTaskManager,

    openRunDialog,

    openControlPanel,

    openUserAccounts,

    openSystemProperties,

    openSoundsAndAudio,

    openNetworkConnections,

    openAddRemovePrograms,

    openDateTime,

    openCalculator,

    openPaint,

    openExplorer,

    openNotepad,

    openCommandPrompt,

    openRegedit,

    openWinver,

    openDorySettings

  };

})();

