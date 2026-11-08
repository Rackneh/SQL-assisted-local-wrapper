/**

 * Windows XP Professional Setup & OOBE Subsystem (xp_installer.js)

 * Implements:

 * 1. Blue Text-Mode Setup (partitioning, NTFS format, file copy)

 * 2. GUI Setup (39-minute billboard, personalization, product key, credentials)

 * 3. Out-Of-Box Experience (OOBE Welcome & Sound)

 * 4. Logon Screen with Credentials Post-it Note

 */



window.XPInstaller = (function() {

  const STORAGE_KEY = 'xp_system_install_config';

  const INSTALLED_KEY = 'xp_system_installed';



  let defaultConfig = {

    userName: 'Rackneh',

    organization: 'Inference Lighting Lab',

    productKey: 'FCKGW-RHQQ2-YXRKT-8TG6W-2B7Q8',

    computerName: 'RACKNEH-XP',

    adminPassword: '',

    timeZone: '(GMT-08:00) Pacific Time (US & Canada)',

    userTile: '/static/xp_assets/usertiles/dog.bmp',

    installedAt: null

  };



  const OPENED_REGISTRY_KEY = 'xp_opened_workstations';



  function getDesktopContainer() {

    return document.getElementById('unifiedXpDesktop') ||

           document.getElementById('messagesContainer') ||

           document.querySelector('.chat-workspace') ||

           document.body;

  }



  function resolveWorkspace(projectInfo = null) {

    return projectInfo || window.currentActiveWorkspace || (window._cachedWorkspaces && window._cachedWorkspaces[0]) || null;

  }



  function getProjectKey(projectInfo) {

    const ws = resolveWorkspace(projectInfo);

    if (!ws) return 'default';

    if (typeof ws === 'string') {

      const parts = ws.replace(/\\/g, '/').split('/');

      const base = parts[parts.length - 1] || ws;

      return base.replace(/[^a-zA-Z0-9_-]/g, '_');

    }

    if (ws.name) {

      return ws.name.replace(/[^a-zA-Z0-9_-]/g, '_');

    }

    if (ws.path) {

      const parts = ws.path.replace(/\\/g, '/').split('/');

      const base = parts[parts.length - 1] || ws.path;

      return base.replace(/[^a-zA-Z0-9_-]/g, '_');

    }

    return 'default';

  }



  function _old_getProjectKey(projectInfo) {

    if (!projectInfo) return 'default';

    if (typeof projectInfo === 'string') {

      const parts = projectInfo.replace(/\\/g, '/').split('/');

      const base = parts[parts.length - 1] || projectInfo;

      return base.replace(/[^a-zA-Z0-9_-]/g, '_');

    }

    if (projectInfo.name) {

      return projectInfo.name.replace(/[^a-zA-Z0-9_-]/g, '_');

    }

    if (projectInfo.path) {

      const parts = projectInfo.path.replace(/\\/g, '/').split('/');

      const base = parts[parts.length - 1] || projectInfo.path;

      return base.replace(/[^a-zA-Z0-9_-]/g, '_');

    }

    return 'default';

  }



  function getInstallConfig(projectInfo = null) {

    const pKey = getProjectKey(projectInfo);

    const key = STORAGE_KEY + '_' + pKey;

    try {

      const saved = localStorage.getItem(key);

      if (saved) {

        return Object.assign({}, defaultConfig, JSON.parse(saved));

      }

    } catch (e) {}

    const cfg = Object.assign({}, defaultConfig);

    if (projectInfo) {

      const pName = (typeof projectInfo === 'string') ? projectInfo : (projectInfo.name || 'XP-WORKSTATION');

      const clean = pName.toUpperCase().replace(/[^A-Z0-9_-]/g, '');

      if (clean) cfg.computerName = clean;

    }

    return cfg;

  }



  function markInstalled(projectInfo = null, customConfig = null) {

    const pKey = getProjectKey(projectInfo);

    try {

      sessionStorage.removeItem('xp_force_reinstall_' + pKey);

    } catch(e) {}

    const instKey = INSTALLED_KEY + '_' + pKey;

    try {

      localStorage.setItem(instKey, 'true');



      let opened = JSON.parse(localStorage.getItem(OPENED_REGISTRY_KEY) || '[]');

      if (!opened.includes(pKey)) {

        opened.push(pKey);

        localStorage.setItem(OPENED_REGISTRY_KEY, JSON.stringify(opened));

      }



      const cfgKey = STORAGE_KEY + '_' + pKey;

      if (!localStorage.getItem(cfgKey)) {

        const cfg = customConfig || getInstallConfig(projectInfo);

        localStorage.setItem(cfgKey, JSON.stringify(cfg));

      }

    } catch (e) {}

  }



  function saveInstallConfig(cfg, projectInfo = null) {

    const pKey = getProjectKey(projectInfo);

    const key = STORAGE_KEY + '_' + pKey;

    const instKey = INSTALLED_KEY + '_' + pKey;

    try {

      localStorage.setItem(key, JSON.stringify(cfg));

      localStorage.setItem(instKey, 'true');

      markInstalled(projectInfo, cfg);

    } catch (e) {}

  }



  function isInstalled(projectInfo = null) {

    const ws = resolveWorkspace(projectInfo);

    const pKey = getProjectKey(ws);

    if (sessionStorage.getItem('xp_force_reinstall_' + pKey) === 'true') {

      return false;

    }

    const instKey = INSTALLED_KEY + '_' + pKey;



    // 1. Explicit installed flag in localStorage

    if (localStorage.getItem(instKey) === 'true') {

      return true;

    }



    // 2. Saved installation config exists in localStorage

    const cfgKey = STORAGE_KEY + '_' + pKey;

    if (localStorage.getItem(cfgKey)) {

      return true;

    }



    // 3. Check opened workstations registry

    try {

      const opened = JSON.parse(localStorage.getItem(OPENED_REGISTRY_KEY) || '[]');

      if (opened.includes(pKey)) return true;

    } catch (e) {}



    // 4. Check workspace backend metadata / stats

    const wsObj = (projectInfo && typeof projectInfo === 'object') ? projectInfo : null;

    if (wsObj && wsObj.stats) {

      if (wsObj.stats.db_initialized && ((wsObj.stats.sessions_count || 0) > 0 || (wsObj.stats.tasks_count || 0) > 0 || (wsObj.stats.steps_count || 0) > 0)) {

        markInstalled(wsObj);

        return true;

      }

    }



    // 5. Check global cachedWorkspaces list

    const list = window._cachedWorkspaces || window.cachedWorkspaces;

    if (list && Array.isArray(list)) {

      const found = list.find(w => getProjectKey(w) === pKey);

      if (found && found.stats && found.stats.db_initialized && ((found.stats.sessions_count || 0) > 0 || (found.stats.tasks_count || 0) > 0 || (found.stats.steps_count || 0) > 0)) {

        markInstalled(found);

        return true;

      }

    }



    // Default fallback

    if (pKey === 'default') {

      return localStorage.getItem(INSTALLED_KEY) === 'true';

    }



    return false;

  }



  function resetInstallation(projectInfo = null) {

    const pKey = getProjectKey(projectInfo);

    const key = STORAGE_KEY + '_' + pKey;

    const instKey = INSTALLED_KEY + '_' + pKey;

    try {

      localStorage.removeItem(instKey);

      localStorage.removeItem(key);

      let opened = JSON.parse(localStorage.getItem(OPENED_REGISTRY_KEY) || '[]');

      opened = opened.filter(k => k !== pKey);

      localStorage.setItem(OPENED_REGISTRY_KEY, JSON.stringify(opened));

      sessionStorage.setItem('xp_force_reinstall_' + pKey, 'true');

    } catch (e) {}

  }



  // -------------------------------------------------------------------------

  // Launch Windows XP Setup Process (Contained within XP Desktop)

  // -------------------------------------------------------------------------

  function startSetup(onComplete, projectInfo = null) {

    let currentConfig = getInstallConfig(projectInfo);

    if (projectInfo) {

      const pName = (typeof projectInfo === 'string') ? projectInfo : (projectInfo.name || 'XP-WORKSTATION');

      const clean = pName.toUpperCase().replace(/[^A-Z0-9_-]/g, '');

      if (clean) currentConfig.computerName = clean;

    }



    const parentContainer = getDesktopContainer();

    let setupContainer = document.getElementById('xpSetupFullscreen');

    if (!setupContainer) {

      setupContainer = document.createElement('div');

      setupContainer.className = 'xp-setup-fullscreen-container';

      setupContainer.id = 'xpSetupFullscreen';

      parentContainer.appendChild(setupContainer);

    } else {

      setupContainer.style.display = 'flex';

      setupContainer.classList.remove('fade-out');

      if (setupContainer.parentElement !== parentContainer) {

        parentContainer.appendChild(setupContainer);

      }

    }



    // =======================================================================

    // STAGE 1: Blue Text-Mode Setup

    // =======================================================================

    function renderTextModeSetup() {

      setupContainer.innerHTML = `

        <div class="xp-text-setup-screen">

          <div class="xp-text-setup-header">Windows XP Professional Setup</div>

          <div class="xp-text-setup-body" id="textSetupBody">

            <p>Setup is inspecting your computer's hardware configuration...</p>

            <p id="txtLoadingFiles" style="margin-top:14px; color:#fef08a;">Setup is loading files (Windows Executive, Kernel, HAL, NTFS, IDE Bus)...</p>

            <div id="txtSetupPartitionBox" style="display:none; margin-top:18px;">

              <p style="color:#ffffff;">The following list shows the existing partitions and unpartitioned space on this computer:</p>

              <div class="xp-text-partition-table">

                <div>- C: Partition1 [NTFS]    40960 MB ( 40960 MB free )</div>

                <div style="color:#38bdf8;">- D: Partition2 [Host WS] 1024000 MB ( Project Volume: InferenceLightingLab )</div>

                <div style="color:#94a3b8;">- Unpartitioned space     8 MB</div>

              </div>

              <p style="margin-top:14px;">• Press ENTER to install Windows XP on the selected partition.</p>

              <p>• Press C to create a partition in the unpartitioned space.</p>

              <p>• Press F3 to quit Setup without installing Windows XP.</p>

              <div style="margin-top:20px;">

                <button class="xp-text-setup-btn" id="btnContinuePartition">ENTER = Continue Setup</button>

              </div>

            </div>

            <div id="txtFormatProgressBox" style="display:none; margin-top:20px;">

              <p>Please wait while Setup formats the partition...</p>

              <div class="xp-text-progress-bar-frame">

                <div class="xp-text-progress-fill" id="txtFormatBar" style="width:0%;"></div>

              </div>

              <p id="txtFormatStatus" style="font-size:12px; margin-top:8px;">Formatting drive C: (NTFS File System)... 0%</p>

            </div>

          </div>

          <div class="xp-text-setup-footer" id="textSetupFooter">ENTER=Continue  F3=Quit</div>

        </div>

      `;



      setTimeout(() => {

        const partBox = setupContainer.querySelector('#txtSetupPartitionBox');

        if (partBox) partBox.style.display = 'block';



        const contBtn = setupContainer.querySelector('#btnContinuePartition');

        if (contBtn) {

          contBtn.addEventListener('click', proceedToFormat);

        }



        // Auto proceed after 3 seconds if not clicked

        setTimeout(() => {

          proceedToFormat();

        }, 3200);

      }, 1600);



      function proceedToFormat() {

        const partBox = setupContainer.querySelector('#txtSetupPartitionBox');

        const progBox = setupContainer.querySelector('#txtFormatProgressBox');

        if (!progBox) return;

        if (partBox) partBox.style.display = 'none';

        progBox.style.display = 'block';



        let pct = 0;

        const bar = setupContainer.querySelector('#txtFormatBar');

        const stat = setupContainer.querySelector('#txtFormatStatus');

        const timer = setInterval(() => {

          pct += 10;

          if (pct > 100) pct = 100;

          if (bar) bar.style.width = pct + '%';

          if (stat) stat.textContent = `Formatting drive C: (NTFS File System)... ${pct}%`;

          if (pct >= 100) {

            clearInterval(timer);

            setTimeout(() => {

              renderGuiSetup();

            }, 1000);

          }

        }, 120);

      }

    }



    // =======================================================================

    // STAGE 2: GUI 39-Minute Billboard Setup

    // =======================================================================

    function renderGuiSetup() {

      setupContainer.innerHTML = `

        <div class="xp-gui-setup-screen">

          <div class="xp-gui-setup-header" style="display:flex; justify-content:space-between; align-items:center; padding:12px 24px;">

            <img src="/static/xp_assets/setup_billboard/bmp_102.png" style="height:38px; width:auto; filter:drop-shadow(0 2px 4px rgba(0,0,0,0.5));" alt="Windows XP Professional Setup">

            <div style="color:#ffffff; font-size:12px; font-weight:700; text-shadow:1px 1px 2px #000;">Setup</div>

          </div>



          <div class="xp-gui-setup-main">

            <!-- Left Billboard -->

            <div class="xp-gui-setup-billboard">

              <div class="xp-gui-billboard-steps">

                <div class="billboard-step done"><span>✔</span> Collecting information</div>

                <div class="billboard-step done"><span>✔</span> Dynamic update</div>

                <div class="billboard-step done"><span>✔</span> Preparing installation</div>

                <div class="billboard-step active"><span>➔</span> Installing Windows</div>

                <div class="billboard-step"><span></span> Finalizing installation</div>

              </div>



              <div class="xp-gui-billboard-features">

                <h3 id="billboardTitle">An exciting new look</h3>

                <p id="billboardText">Windows XP Professional sports a fresh visual design that makes common tasks easier and puts your most important files within easy reach.</p>

              </div>



              <div class="xp-gui-time-remaining">

                <div style="font-size:12px; color:#1e293b; font-weight:700;">Setup will complete in approximately:</div>

                <div style="font-size:24px; color:#0369a1; font-weight:900; margin-top:3px;" id="timeRemainingText">39 minutes</div>

              </div>



              <!-- Animated Green Chaser -->

              <div class="xp-gui-activity-chaser">

                <span class="dot"></span>

                <span class="dot"></span>

                <span class="dot"></span>

              </div>

            </div>



            <!-- Right Interactive Wizard Window -->

            <div class="xp-gui-wizard-box" id="guiWizardBox">

              <!-- Wizard Steps Injected Dynamically -->

            </div>

          </div>

        </div>

      `;



      // Cycle billboard features

      const features = [
        { 
          title: "An exciting new look", 
          text: "Windows XP Professional sports a visual design that combines a sleek look, clean lines, and appealing colors with a task-oriented design and exceptionally streamlined navigation. The redesigned Start menu makes it easier to find important information and to access the programs you use most frequently." 
        },
        { 
          title: "The connected world", 
          text: "Windows XP makes it easy to share hardware, network drives, and files securely across your home or office. With integrated Internet Connection Sharing, Windows Firewall, and remote desktop access, staying connected has never been simpler." 
        },
        { 
          title: "Rock-solid reliability", 
          text: "Designed for maximum dependability and minimal restarts. Built on the proven NT 32-bit kernel architecture, Windows XP delivers bulletproof stability, protected memory architecture, and seamless system restore capabilities." 
        },
        { 
          title: "Advanced security and privacy", 
          text: "Windows XP gives you peace of mind with Encrypting File System (EFS), advanced user permission controls, and automatic updates to protect against security threats." 
        }
      ];

      let featIdx = 0;

      const featTimer = setInterval(() => {

        featIdx = (featIdx + 1) % features.length;

        const t = setupContainer.querySelector('#billboardTitle');

        const p = setupContainer.querySelector('#billboardText');

        if (t && p) {

          t.textContent = features[featIdx].title;

          p.textContent = features[featIdx].text;

        }

      }, 5000);



      renderWizardStep1();

    }



    // Step 1: Regional and Language Options

    function renderWizardStep1() {

      const box = setupContainer.querySelector('#guiWizardBox');

      if (!box) return;

      box.innerHTML = `

        <div class="xp-wizard-card">

          <div class="xp-wizard-header">

            <h4>Regional and Language Options</h4>

            <p>You can customize Windows XP for different regions and languages.</p>

          </div>

          <div class="xp-wizard-body">

            <p>Regional settings allow you to customize settings for handling numbers, currencies, time, and dates.</p>

            <div class="xp-wizard-panel" style="margin:12px 0; padding:10px; background:#ffffff; border:1px solid #7f9db9;">

              <div>Current standards and formats are set to <strong>English (United States)</strong>.</div>

            </div>

            <p>Text input languages allow you to enter text in many different languages using your keyboard.</p>

            <div class="xp-wizard-panel" style="margin:12px 0; padding:10px; background:#ffffff; border:1px solid #7f9db9;">

              <div>Default text input language is set to <strong>US - Keyboard</strong>.</div>

            </div>

          </div>

          <div class="xp-wizard-footer">

            <button class="btn btn-secondary btn-sm" disabled>&lt; Back</button>

            <button class="btn btn-primary btn-sm" id="wizNextBtn1" style="background:#0284c7; color:#fff;">Next &gt;</button>

          </div>

        </div>

      `;

      box.querySelector('#wizNextBtn1').addEventListener('click', renderWizardStep2);

    }



    // Step 2: Personalize Your Software (Name & Organization)

    function renderWizardStep2() {

      const box = setupContainer.querySelector('#guiWizardBox');

      if (!box) return;

      box.innerHTML = `

        <div class="xp-wizard-card">

          <div class="xp-wizard-header">

            <h4>Personalize Your Software</h4>

            <p>Setup uses this information to personalize your Windows XP installation.</p>

          </div>

          <div class="xp-wizard-body">

            <p>Type your full name and the name of your company or organization.</p>

            <div class="form-group" style="margin-top:14px;">

              <label style="display:block; font-size:12px; font-weight:700; margin-bottom:4px;"><u>N</u>ame:</label>

              <input type="text" id="wizInputName" value="${currentConfig.userName}" style="width:280px; padding:3px 6px; border:1px solid #7f9db9; font-size:12px;">

            </div>

            <div class="form-group" style="margin-top:12px;">

              <label style="display:block; font-size:12px; font-weight:700; margin-bottom:4px;"><u>O</u>rganization:</label>

              <input type="text" id="wizInputOrg" value="${currentConfig.organization}" style="width:280px; padding:3px 6px; border:1px solid #7f9db9; font-size:12px;">

            </div>

          </div>

          <div class="xp-wizard-footer">

            <button class="btn btn-secondary btn-sm" id="wizBackBtn2">&lt; Back</button>

            <button class="btn btn-primary btn-sm" id="wizNextBtn2" style="background:#0284c7; color:#fff;">Next &gt;</button>

          </div>

        </div>

      `;

      box.querySelector('#wizBackBtn2').addEventListener('click', renderWizardStep1);

      box.querySelector('#wizNextBtn2').addEventListener('click', () => {

        const nameVal = box.querySelector('#wizInputName').value.trim();

        const orgVal = box.querySelector('#wizInputOrg').value.trim();

        if (nameVal) currentConfig.userName = nameVal;

        if (orgVal) currentConfig.organization = orgVal;

        renderWizardStep3();

      });

    }



    // Step 3: Your Product Key

    function renderWizardStep3() {

      const box = setupContainer.querySelector('#guiWizardBox');

      if (!box) return;

      const parts = currentConfig.productKey.split('-');

      box.innerHTML = `

        <div class="xp-wizard-card">

          <div class="xp-wizard-header">

            <h4>Your Product Key</h4>

            <p>Your product key identifies your copy of Windows XP.</p>

          </div>

          <div class="xp-wizard-body">

            <p>The 25-character product key is found on the yellow sticker on the back of your Windows CD folder or on your Certificate of Authenticity.</p>

            <div style="display:flex; gap:6px; align-items:center; margin-top:20px;">

              <input type="text" class="pk-box" maxlength="5" value="${parts[0] || 'FCKGW'}"> -

              <input type="text" class="pk-box" maxlength="5" value="${parts[1] || 'RHQQ2'}"> -

              <input type="text" class="pk-box" maxlength="5" value="${parts[2] || 'YXRKT'}"> -

              <input type="text" class="pk-box" maxlength="5" value="${parts[3] || '8TG6W'}"> -

              <input type="text" class="pk-box" maxlength="5" value="${parts[4] || '2B7Q8'}">

            </div>

            <p style="font-size:11px; color:#64748b; margin-top:10px;">Genuine Microsoft Product Key detected & verified.</p>

          </div>

          <div class="xp-wizard-footer">

            <button class="btn btn-secondary btn-sm" id="wizBackBtn3">&lt; Back</button>

            <button class="btn btn-primary btn-sm" id="wizNextBtn3" style="background:#0284c7; color:#fff;">Next &gt;</button>

          </div>

        </div>

      `;

      box.querySelector('#wizBackBtn3').addEventListener('click', renderWizardStep2);

      box.querySelector('#wizNextBtn3').addEventListener('click', renderWizardStep4);

    }



    // Step 4: Computer Name and Administrator Password

    function renderWizardStep4() {

      const box = setupContainer.querySelector('#guiWizardBox');

      if (!box) return;

      box.innerHTML = `

        <div class="xp-wizard-card">

          <div class="xp-wizard-header">

            <h4>Computer Name and Administrator Password</h4>

            <p>You must provide a name and an Administrator password for your computer.</p>

          </div>

          <div class="xp-wizard-body">

            <p>Every computer on the network must have a unique name.</p>

            <div class="form-group" style="margin-top:10px;">

              <label style="display:block; font-size:12px; font-weight:700; margin-bottom:3px;"><u>C</u>omputer name:</label>

              <input type="text" id="wizInputComp" value="${currentConfig.computerName}" style="width:260px; padding:3px 6px; border:1px solid #7f9db9; font-size:12px;">

            </div>

            <p style="margin-top:12px;">Type a password for the Administrator account:</p>

            <div class="form-group" style="margin-top:6px;">

              <label style="display:block; font-size:12px; font-weight:700; margin-bottom:3px;"><u>A</u>dministrator password:</label>

              <input type="password" id="wizInputPw1" value="${currentConfig.adminPassword}" placeholder="Optional password" style="width:260px; padding:3px 6px; border:1px solid #7f9db9; font-size:12px;">

            </div>

            <div class="form-group" style="margin-top:6px;">

              <label style="display:block; font-size:12px; font-weight:700; margin-bottom:3px;"><u>C</u>onfirm password:</label>

              <input type="password" id="wizInputPw2" value="${currentConfig.adminPassword}" placeholder="Confirm password" style="width:260px; padding:3px 6px; border:1px solid #7f9db9; font-size:12px;">

            </div>

          </div>

          <div class="xp-wizard-footer">

            <button class="btn btn-secondary btn-sm" id="wizBackBtn4">&lt; Back</button>

            <button class="btn btn-primary btn-sm" id="wizNextBtn4" style="background:#0284c7; color:#fff;">Next &gt;</button>

          </div>

        </div>

      `;

      box.querySelector('#wizBackBtn4').addEventListener('click', renderWizardStep3);

      box.querySelector('#wizNextBtn4').addEventListener('click', () => {

        const compVal = box.querySelector('#wizInputComp').value.trim();

        const pw1 = box.querySelector('#wizInputPw1').value;

        const pw2 = box.querySelector('#wizInputPw2').value;

        if (pw1 !== pw2) {

          alert('Passwords do not match. Please verify your password.');

          return;

        }

        if (compVal) currentConfig.computerName = compVal;

        currentConfig.adminPassword = pw1;

        renderWizardStep5();

      });

    }



    // Step 5: Date and Time Settings

    function renderWizardStep5() {

      const box = setupContainer.querySelector('#guiWizardBox');

      if (!box) return;

      box.innerHTML = `

        <div class="xp-wizard-card">

          <div class="xp-wizard-header">

            <h4>Date and Time Settings</h4>

            <p>Set the correct date, time, and time zone for your computer.</p>

          </div>

          <div class="xp-wizard-body">

            <div style="display:flex; gap:14px; align-items:center; margin-top:10px;">

              <div>

                <label style="font-size:11px; font-weight:700;">Date:</label><br>

                <input type="text" value="${new Date().toLocaleDateString()}" readonly style="padding:3px 6px; width:110px; background:#f1f5f9; border:1px solid #7f9db9; font-size:11px;">

              </div>

              <div>

                <label style="font-size:11px; font-weight:700;">Time:</label><br>

                <input type="text" value="${new Date().toLocaleTimeString()}" readonly style="padding:3px 6px; width:110px; background:#f1f5f9; border:1px solid #7f9db9; font-size:11px;">

              </div>

            </div>

            <div style="margin-top:14px;">

              <label style="font-size:12px; font-weight:700;">Time Zone:</label><br>

              <select id="wizSelectTz" style="width:340px; padding:3px; font-size:11px; border:1px solid #7f9db9; margin-top:3px;">

                <option value="(GMT-08:00) Pacific Time (US & Canada)">(GMT-08:00) Pacific Time (US & Canada)</option>

                <option value="(GMT-05:00) Eastern Time (US & Canada)">(GMT-05:00) Eastern Time (US & Canada)</option>

                <option value="(GMT+00:00) Greenwich Mean Time : Dublin, London">(GMT+00:00) Greenwich Mean Time : Dublin, London</option>

                <option value="(GMT+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna" selected>(GMT+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna</option>

              </select>

            </div>

          </div>

          <div class="xp-wizard-footer">

            <button class="btn btn-secondary btn-sm" id="wizBackBtn5">&lt; Back</button>

            <button class="btn btn-primary btn-sm" id="wizNextBtn5" style="background:#0284c7; color:#fff;">Next &gt;</button>

          </div>

        </div>

      `;

      box.querySelector('#wizBackBtn5').addEventListener('click', renderWizardStep4);

      box.querySelector('#wizNextBtn5').addEventListener('click', () => {

        const tz = box.querySelector('#wizSelectTz').value;

        currentConfig.timeZone = tz;

        renderFinalizingStage();

      });

    }



    // Step 6: Finalizing Installation Progress Bar

    function renderFinalizingStage() {

      const box = setupContainer.querySelector('#guiWizardBox');

      const timeTxt = setupContainer.querySelector('#timeRemainingText');

      if (timeTxt) timeTxt.textContent = '1 minute';

      if (!box) return;



      box.innerHTML = `

        <div class="xp-wizard-card">

          <div class="xp-wizard-header">

            <h4>Finalizing Installation</h4>

            <p>Please wait while Setup registers components and finalizes your system.</p>

          </div>

          <div class="xp-wizard-body">

            <div style="margin-top:20px;">

              <div id="finStepText" style="font-size:12px; font-weight:700; color:#1e293b; margin-bottom:8px;">Registering components...</div>

              <div class="xp-setup-progress-track">

                <div class="xp-setup-progress-fill" id="finProgressBar" style="width:0%;"></div>

              </div>

            </div>

          </div>

        </div>

      `;



      let progress = 0;

      const bar = box.querySelector('#finProgressBar');

      const txt = box.querySelector('#finStepText');



      const finTimer = setInterval(() => {

        progress += 8;

        if (progress === 32 && txt) txt.textContent = 'Installing Start Menu items & Games...';

        if (progress === 64 && txt) txt.textContent = 'Registering SQL Authority 3D & WLM Revival...';

        if (progress === 88 && txt) txt.textContent = 'Saving settings and removing temporary files...';

        if (progress > 100) progress = 100;

        if (bar) bar.style.width = progress + '%';



        if (progress >= 100) {

          clearInterval(finTimer);

          setTimeout(() => {

            renderOOBEStage();

          }, 800);

        }

      }, 100);

    }



    // =======================================================================

    // STAGE 3: Out-of-Box Experience (OOBE)

    // =======================================================================

    function renderOOBEStage() {

      currentConfig.installedAt = new Date().toISOString();

      saveInstallConfig(currentConfig, projectInfo);



      let oobeStep = 1;



      function renderStep() {

        if (oobeStep === 1) {

          // Step 1: Automatic Updates / Protection

          setupContainer.innerHTML = `

            <div class="xp-oobe-screen">

              <div class="xp-oobe-top">

                <img src="/static/xp_assets/logon/bmp_100.png" style="width:120px; height:auto; margin-right:16px;">

                <h2>Welcome to Microsoft Windows</h2>

              </div>

              <div class="xp-oobe-center">

                <div class="xp-oobe-card" style="max-width:540px;">

                  <div style="display:flex; gap:16px; align-items:flex-start;">

                    <img src="/static/xp_assets/icons/security.png" style="width:48px; height:48px;">

                    <div>

                      <h3>Help protect your PC</h3>

                      <p style="margin-top:6px; font-size:12px; line-height:1.4;">With Automatic Updates, Windows can routinely check for the latest important updates for your computer and install them automatically.</p>

                      <div style="margin-top:14px; background:#f8fafc; padding:10px; border:1px solid #cbd5e1; border-radius:4px;">

                        <label style="display:flex; align-items:center; gap:8px; cursor:pointer; font-weight:700;">

                          <input type="radio" name="oobeUpdates" checked style="accent-color:#0284c7;">

                          <span>Help protect my PC by turning on Automatic Updates now (Recommended)</span>

                        </label>

                        <label style="display:flex; align-items:center; gap:8px; cursor:pointer; margin-top:8px; color:#64748b;">

                          <input type="radio" name="oobeUpdates" style="accent-color:#0284c7;">

                          <span>Not right now</span>

                        </label>

                      </div>

                    </div>

                  </div>

                </div>

              </div>

              <div class="xp-oobe-bottom">

                <div class="xp-oobe-question">Need Help? Press F1</div>

                <button class="xp-oobe-btn-finish" id="btnNextOOBE">Next &gt;</button>

              </div>

            </div>

          `;

          setupContainer.querySelector('#btnNextOOBE').onclick = () => {

            oobeStep = 2;

            renderStep();

          };

        } else if (oobeStep === 2) {
          // Step 2: User Accounts Setup with User Account Tile Avatar Selector
          setupContainer.innerHTML = `
            <div class="xp-oobe-screen">
              <div class="xp-oobe-top">
                <img src="/static/xp_assets/logon/bmp_100.png" style="width:140px; height:auto; margin-right:16px; filter:drop-shadow(0 2px 6px rgba(0,0,0,0.4));">
                <h2 style="font-size:22px; font-weight:700; text-shadow:1px 1px 3px rgba(0,0,0,0.5);">Welcome to Microsoft Windows</h2>
              </div>
              <div class="xp-oobe-center">
                <div class="xp-oobe-card" style="max-width:580px;">
                  <h3 style="color:#003399; font-size:16px;">Who will use this computer?</h3>
                  <p style="font-size:12px; color:#334155; margin-top:4px;">Type the name of each person who will use this computer. A personalized user account will be created for each person. Click an avatar to choose from the authentic Windows XP account pictures.</p>
                  
                  <div style="margin-top:14px; display:flex; flex-direction:column; gap:12px;">
                    <div style="display:flex; align-items:center; gap:12px;">
                      <div class="xp-logon-avatar-box" id="oobeAvatarPickBtn" title="Click to change account tile" style="width:48px; height:48px; cursor:pointer; border:2px solid #0284c7; border-radius:4px; overflow:hidden; background:#000;">
                        <img id="oobeAvatarImg" src="${currentConfig.userTile || '/static/xp_assets/usertiles/dog.png'}" style="width:100%; height:100%; object-fit:cover;">
                      </div>
                      <div>
                        <div style="font-size:11px; color:#64748b;">Primary User Name:</div>
                        <input type="text" id="oobeUserNameInput" value="${currentConfig.userName}" style="padding:4px 8px; font-size:13px; font-weight:700; width:230px; border:1px solid #7f9db9;">
                      </div>
                    </div>
                    
                    <div style="display:flex; align-items:center; gap:12px;">
                      <div class="xp-logon-avatar-box" style="width:48px; height:48px; border:2px solid #cbd5e1; border-radius:4px; overflow:hidden; background:#000;">
                        <img src="/static/xp_assets/usertiles/duck.png" style="width:100%; height:100%; object-fit:cover;">
                      </div>
                      <div>
                        <div style="font-size:11px; color:#64748b;">User 2 (AI Assistant):</div>
                        <input type="text" id="oobeUser2" value="Dory AI Bot" style="padding:3px 6px; font-size:12px; width:230px; border:1px solid #cbd5e1;">
                      </div>
                    </div>

                    <div style="display:flex; align-items:center; gap:12px;">
                      <div class="xp-logon-avatar-box" style="width:48px; height:48px; border:2px solid #cbd5e1; border-radius:4px; overflow:hidden; background:#000;">
                        <img src="/static/xp_assets/usertiles/chess.png" style="width:100%; height:100%; object-fit:cover;">
                      </div>
                      <div>
                        <div style="font-size:11px; color:#64748b;">User 3 (Optional):</div>
                        <input type="text" id="oobeUser3" placeholder="Guest / Colleague" style="padding:3px 6px; font-size:12px; width:230px; border:1px solid #cbd5e1;">
                      </div>
                    </div>
                  </div>

                  <!-- Inline Avatar Selector Grid Modal -->
                  <div id="oobeAvatarModal" style="display:none; margin-top:14px; padding:10px; background:#f1f5f9; border:1px solid #94a3b8; border-radius:4px;">
                    <div style="font-size:11px; font-weight:700; margin-bottom:8px;">Choose an authentic Windows XP user account tile:</div>
                    <div style="display:grid; grid-template-columns:repeat(8, 1fr); gap:6px; max-height:120px; overflow-y:auto;" id="oobeTileGrid"></div>
                  </div>
                </div>
              </div>
              <div class="xp-oobe-bottom">
                <button class="btn btn-secondary btn-sm" id="btnBackOOBE">&lt; Back</button>
                <button class="xp-oobe-btn-finish" id="btnFinishOOBE">Finish &gt;</button>
              </div>
            </div>
          `;

          const avModal = setupContainer.querySelector('#oobeAvatarModal');
          const avBtn = setupContainer.querySelector('#oobeAvatarPickBtn');
          if (avBtn && avModal) {
            avBtn.onclick = () => {
              avModal.style.display = avModal.style.display === 'none' ? 'block' : 'none';
              const grid = avModal.querySelector('#oobeTileGrid');
              if (grid && !grid.hasChildNodes()) {
                const sampleTiles = ['dog.png', 'duck.png', 'chess.png', 'car.png', 'guitar.png', 'butterfly.png', 'airplane.png', 'astro.png', 'beach.png', 'cat.png', 'horses.png', 'palmtree.png', 'soccer.png', 'skater.png', 'snwflake.png', 'frog.png'];
                sampleTiles.forEach(tileName => {
                  const tImg = document.createElement('img');
                  tImg.src = '/static/xp_assets/usertiles/' + tileName;
                  tImg.style.cssText = 'width:36px; height:36px; cursor:pointer; border:1px solid #7f9db9; border-radius:2px; object-fit:cover;';
                  tImg.onclick = () => {
                    currentConfig.userTile = '/static/xp_assets/usertiles/' + tileName;
                    setupContainer.querySelector('#oobeAvatarImg').src = currentConfig.userTile;
                    avModal.style.display = 'none';
                  };
                  grid.appendChild(tImg);
                });
              }
            };
          }

          setupContainer.querySelector('#btnBackOOBE').onclick = () => {

            oobeStep = 1;

            renderStep();

          };

          setupContainer.querySelector('#btnFinishOOBE').onclick = () => {

            const u = setupContainer.querySelector('#oobeUserNameInput').value.trim();

            if (u) currentConfig.userName = u;

            saveInstallConfig(currentConfig, projectInfo);



            if (window.XPCore && window.XPCore.Audio) {

              window.XPCore.Audio.playTada();

            }



            setupContainer.classList.add('fade-out');

            setTimeout(() => {

              setupContainer.remove();

              updateSimulatorUserInfo(currentConfig);

              if (window.XPCore && window.XPCore.triggerLogonScreen) {

                window.XPCore.triggerLogonScreen(currentConfig, onComplete);

              } else if (onComplete) {

                onComplete(currentConfig);

              }

            }, 600);

          };

        }

      }



      renderStep();

    }



    renderTextModeSetup();

  }



  function updateSimulatorUserInfo(cfg) {

    if (!cfg) return;

    // Update Start Menu user name

    const smUser = document.querySelector('.xp-start-user-name');

    if (smUser) smUser.textContent = cfg.userName;



    // Update WLM user profile

    if (window.WLMClient && window.WLMClient.updateUserIdentity) {

      window.WLMClient.updateUserIdentity(cfg.userName);

    }

  }



  return {

    getInstallConfig,

    saveInstallConfig,

    isInstalled,

    markInstalled,

    resetInstallation,

    startSetup,

    updateSimulatorUserInfo

  };

})();

