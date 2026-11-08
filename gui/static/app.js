// Dory GUI Application Logic with Unified Windows XP Workspace, WLM Revival Client & 3D SQL Authority



document.addEventListener('DOMContentLoaded', () => {

  // State

  let selectedLoops = 2;

  let activeAbortController = null;

  let chatHistory = [];

  let isGenerating = false;

  let autoScrollLogs = true;

  let lastLogId = 0;

  let currentSessionId = null;

  currentActiveSessionId = 'default';



  // DOM Elements

  const statusDot = document.getElementById('statusDot');

  const statusText = document.getElementById('statusText');

  const applyEngineBtn = document.getElementById('applyEngineBtn');

  const loopButtons = document.querySelectorAll('#recurrentLoopsGroup .loop-btn');

  const kvCacheMode = document.getElementById('kvCacheMode');

  const cpuThreads = document.getElementById('cpuThreads');

  const threadsVal = document.getElementById('threadsVal');

  const ctxSize = document.getElementById('ctxSize');



  const tempRange = document.getElementById('tempRange');

  const tempVal = document.getElementById('tempVal');

  const topPRange = document.getElementById('topPRange');

  const topPVal = document.getElementById('topPVal');

  const maxTokens = document.getElementById('maxTokens');

  const maxTokensVal = document.getElementById('maxTokensVal');

  const reasoningMode = document.getElementById('reasoningMode');

  const showReasoningInline = document.getElementById('showReasoningInline');



  const presetSelect = document.getElementById('presetSelect');

  const systemPrompt = document.getElementById('systemPrompt');

  const clearChatBtn = document.getElementById('clearChatBtn');

  const exportChatBtn = document.getElementById('exportChatBtn');



  const messagesContainer = document.getElementById('messagesContainer');

  const welcomeCard = document.getElementById('welcomeCard');

  const userInput = document.getElementById('userInput');

  const sendBtn = document.getElementById('sendBtn');

  const stopBtn = document.getElementById('stopBtn');



  const chipLoops = document.getElementById('chipLoops');

  const chipCache = document.getElementById('chipCache');

  const chipContext = document.getElementById('chipContext');

  const chipGpu = document.getElementById('chipGpu');

  const gpuLayers = document.getElementById('gpuLayers');

  const gpuLayersVal = document.getElementById('gpuLayersVal');

  const tokenSpeedStat = document.getElementById('tokenSpeedStat');

  const serverPortStat = document.getElementById('serverPortStat');

  const toggleSidebarBtn = document.getElementById('toggleSidebarBtn');

  const sidebar = document.getElementById('sidebar');



  // IBA Projects, Workspaces & SQL Memory Controls

  const ibaModeToggle = document.getElementById('ibaModeToggle');

  const variableNewconvToggle = document.getElementById('variableNewconvToggle');

  if (variableNewconvToggle) {

    const savedVnc = localStorage.getItem('iba_variable_newconv');

    if (savedVnc !== null) variableNewconvToggle.checked = savedVnc === '1';

    variableNewconvToggle.addEventListener('change', () => {

      localStorage.setItem('iba_variable_newconv', variableNewconvToggle.checked ? '1' : '0');

    });

  }

  const workspaceSelect = document.getElementById('workspaceSelect');

  const workspacePathDisplay = document.getElementById('workspacePathDisplay');

  const sessionSelect = document.getElementById('sessionSelect');

  const newSessionBtn = document.getElementById('newSessionBtn');

  const openAddWorkspaceModalBtn = document.getElementById('openAddWorkspaceModalBtn');

  const openPinModalBtn = document.getElementById('openPinModalBtn');

  const openInspectorModalBtn = document.getElementById('openInspectorModalBtn');

  const taskCountBadge = document.getElementById('taskCountBadge');

  const chipWorkspace = document.getElementById('chipWorkspace');

  const chipSession = document.getElementById('chipSession');

  const chipIbaMode = document.getElementById('chipIbaMode');

  const ibaCtxButtons = document.querySelectorAll('.iba-ctx-btn');



  // Middle Collapsible Session Panel Controls

  const sessionSidebar = document.getElementById('sessionSidebar');

  const toggleSessionSidebarBtn = document.getElementById('toggleSessionSidebarBtn');

  const openSessionSidebarBtn = document.getElementById('openSessionSidebarBtn');

  const sessionWsSelect = document.getElementById('sessionWsSelect');

  const sessionWsPath = document.getElementById('sessionWsPath');

  const sessionAddWsBtn = document.getElementById('sessionAddWsBtn');

  const sessionNewBtn = document.getElementById('sessionNewBtn');

  const sessionCardsList = document.getElementById('sessionCardsList');

  const sessionPinRuleBtn = document.getElementById('sessionPinRuleBtn');

  const sessionTaskLogBtn = document.getElementById('sessionTaskLogBtn');

  const sessionTaskCount = document.getElementById('sessionTaskCount');



  // Modals

  const addWorkspaceModal = document.getElementById('addWorkspaceModal');

  const newWsName = document.getElementById('newWsName');

  const newWsPath = document.getElementById('newWsPath');

  const newWsDesc = document.getElementById('newWsDesc');

  const saveWorkspaceBtn = document.getElementById('saveWorkspaceBtn');

  const newSessionModal = document.getElementById('newSessionModal');

  const newSessionName = document.getElementById('newSessionName');

  const newSessionDesc = document.getElementById('newSessionDesc');

  const createSessionBtn = document.getElementById('createSessionBtn');

  const newSessionError = document.getElementById('newSessionError');



  const pinModal = document.getElementById('pinModal');

  const newPinInput = document.getElementById('newPinInput');

  const savePinBtn = document.getElementById('savePinBtn');

  const pinsListContainer = document.getElementById('pinsListContainer');



  const inspectorModal = document.getElementById('inspectorModal');

  const inspectWsPath = document.getElementById('inspectWsPath');

  const inspectSession = document.getElementById('inspectSession');

  const tasksTableBody = document.getElementById('tasksTableBody');

  const taskStepsSection = document.getElementById('taskStepsSection');

  const currentInspectedTaskId = document.getElementById('currentInspectedTaskId');

  const stepsListContainer = document.getElementById('stepsListContainer');

  const closeStepDetailsBtn = document.getElementById('closeStepDetailsBtn');

  const refreshInspectorBtn = document.getElementById('refreshInspectorBtn');



  // Mental Map Modal

  const codeMapModal = document.getElementById('codeMapModal');

  const openCodeMapModalBtn = document.getElementById('openCodeMapModalBtn');

  const sessionCodeMapBtn = document.getElementById('sessionCodeMapBtn');

  const refreshCodeMapBtn = document.getElementById('refreshCodeMapBtn');

  const codeMapListContainer = document.getElementById('codeMapListContainer');

  const codeMapCountBadge = document.getElementById('codeMapCountBadge');

  const sessionCodeMapCount = document.getElementById('sessionCodeMapCount');



  // Real-Time Activity & Live Console Elements

  const headerActivityBadge = document.getElementById('headerActivityBadge');

  const activityPulseDot = document.getElementById('activityPulseDot');

  const activityText = document.getElementById('activityText');

  const toggleConsoleBtn = document.getElementById('toggleConsoleBtn');



  const activityDrawer = document.getElementById('activityDrawer');

  const drawerHeader = document.getElementById('drawerHeader');

  const drawerPulseDot = document.getElementById('drawerPulseDot');

  const drawerStatusTitle = document.getElementById('drawerStatusTitle');

  const drawerTaskChip = document.getElementById('drawerTaskChip');

  const drawerPromptChip = document.getElementById('drawerPromptChip');

  const drawerGenChip = document.getElementById('drawerGenChip');

  const drawerSpeedChip = document.getElementById('drawerSpeedChip');

  const drawerClearBtn = document.getElementById('drawerClearBtn');

  const drawerCopyBtn = document.getElementById('drawerCopyBtn');

  const drawerAutoscrollBtn = document.getElementById('drawerAutoscrollBtn');

  const drawerExpandBtn = document.getElementById('drawerExpandBtn');

  const drawerToggleIcon = document.getElementById('drawerToggleIcon');

  const terminalContainer = document.getElementById('terminalContainer');



  // Audio Player Helper

  function playWlmSound(soundName) {

    try {

      const audio = new Audio(`/static/wlm/wlm/sounds/${soundName}`);

      audio.volume = 0.5;

      audio.play().catch(() => {});

    } catch (e) {}

  }



  // -------------------------------------------------------------------------

  // Unified Persistent Windows XP Desktop Architecture

  // -------------------------------------------------------------------------

  let desktopZIndex = 30;



  function bringToFront(win, taskBtn) {

    desktopZIndex += 2;

    win.style.zIndex = desktopZIndex;

    document.querySelectorAll('.xp-task-btn').forEach(b => b.classList.remove('active'));

    if (taskBtn) taskBtn.classList.add('active');

  }



  

  // -------------------------------------------------------------------------

  // Universal Window Resizing System (Edges, Corners & XP Grip)

  // -------------------------------------------------------------------------

  function makeResizable(win, minW = 280, minH = 200) {

    const directions = ['n', 's', 'e', 'w', 'nw', 'ne', 'sw', 'se'];

    directions.forEach(dir => {

      const handle = document.createElement('div');

      handle.className = `xp-resize-handle xp-resizer-${dir}`;

      handle.dataset.dir = dir;

      win.appendChild(handle);



      handle.addEventListener('mousedown', (e) => {

        e.preventDefault();

        e.stopPropagation();

        bringToFront(win);



        const startX = e.clientX;

        const startY = e.clientY;

        const startW = win.offsetWidth;

        const startH = win.offsetHeight;

        const startLeft = win.offsetLeft;

        const startTop = win.offsetTop;



        function onMouseMove(ev) {

          const dx = ev.clientX - startX;

          const dy = ev.clientY - startY;



          if (dir.includes('e')) {

            const newW = Math.max(minW, startW + dx);

            win.style.width = `${newW}px`;

          }

          if (dir.includes('s')) {

            const newH = Math.max(minH, startH + dy);

            win.style.height = `${newH}px`;

          }

          if (dir.includes('w')) {

            const newW = Math.max(minW, startW - dx);

            if (newW > minW) {

              win.style.width = `${newW}px`;

              win.style.left = `${startLeft + dx}px`;

            }

          }

          if (dir.includes('n')) {

            const newH = Math.max(minH, startH - dy);

            if (newH > minH) {

              win.style.height = `${newH}px`;

              win.style.top = `${startTop + dy}px`;

            }

          }

        }



        function onMouseUp() {

          document.removeEventListener('mousemove', onMouseMove);

          document.removeEventListener('mouseup', onMouseUp);

        }



        document.addEventListener('mousemove', onMouseMove);

        document.addEventListener('mouseup', onMouseUp);

      });

    });



    const grip = document.createElement('div');

    grip.className = 'xp-window-resizegrip';

    win.appendChild(grip);

  }



function makeDraggable(win, handle) {

    let isDragging = false;

    let startX = 0, startY = 0;

    let origLeft = 0, origTop = 0;



    handle.addEventListener('mousedown', (e) => {

      if (e.target.closest('button') || e.target.closest('.wlm-window-controls') || e.target.closest('.title-bar-controls') || e.target.closest('input') || e.target.closest('textarea')) return;

      isDragging = true;

      startX = e.clientX;

      startY = e.clientY;

      const rect = win.getBoundingClientRect();

      const parentRect = win.parentElement.getBoundingClientRect();

      origLeft = rect.left - parentRect.left;

      origTop = rect.top - parentRect.top;

      bringToFront(win);

      document.body.style.userSelect = 'none';

    });



    window.addEventListener('mousemove', (e) => {

      if (!isDragging) return;

      const dx = e.clientX - startX;

      const dy = e.clientY - startY;

      const newLeft = Math.max(0, Math.min(origLeft + dx, win.parentElement.clientWidth - 80));

      const newTop = Math.max(0, Math.min(origTop + dy, win.parentElement.clientHeight - 60));

      win.style.left = `${newLeft}px`;

      win.style.top = `${newTop}px`;

    });



    window.addEventListener('mouseup', () => {

      if (isDragging) {

        isDragging = false;

        document.body.style.userSelect = '';

      }

    });



    win.addEventListener('mousedown', () => {

      bringToFront(win);

    });

  }



  // Build the Single Persistent XP Desktop Workspace

  function buildUnifiedXpDesktop() {

    if (welcomeCard) welcomeCard.style.display = 'none';

    messagesContainer.innerHTML = '';

    messagesContainer.style.padding = '0';

    messagesContainer.style.overflow = 'hidden';



    const desktop = document.createElement('div');

    desktop.className = 'xp-desktop unified-desktop';

    desktop.id = 'unifiedXpDesktop';



    // Unselected Standby CRT Screen

    const standbyScreen = document.createElement('div');

    standbyScreen.className = 'xp-standby-screen';

    standbyScreen.id = 'xpStandbyScreen';

    standbyScreen.innerHTML = `

      <div class="xp-standby-crt-box">

        <div class="xp-standby-badge">STATUS: STANDBY (OFFLINE)</div>

        <div class="xp-standby-title">MICROSOFT WINDOWS XP WORKSTATION HOST</div>

        <div class="xp-standby-desc">

          No active workstation session is loaded.<br>

          Select a project or click the Power button on a 3D PC tower in 'Workspaces & Sessions' to initialize the simulated workstation.

        </div>

        <button type="button" class="xp-standby-boot-btn" id="standbyPowerOnBtn">

          ⏻ Power On Workstation

        </button>

      </div>

    `;

    desktop.appendChild(standbyScreen);





    // Authentic Official Extracted Windows XP Desktop Icons

    const iconsCol = document.createElement('div');

    iconsCol.className = 'xp-desktop-icons';

    iconsCol.innerHTML = `

      <div class="xp-desktop-icon" id="xpIconComp" title="My Computer (Windows Explorer)">

        <img src="/static/xp_assets/icons/my_computer.png" class="xp-desktop-img" alt="My Computer">

        <span class="xp-desktop-icon-label">My Computer</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconWlm" title="Windows Live Messenger (Contacts & Personas)">

        <img src="/static/wlm/Icons/icon_32.png" class="xp-desktop-img" alt="Messenger">

        <span class="xp-desktop-icon-label">Messenger</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconSql" title="SQL Authority Studio 3D Suite">

        <img src="/static/xp_assets/icons/icon_103.png" class="xp-desktop-img" alt="SQL Authority 3D">

        <span class="xp-desktop-icon-label">SQL Authority 3D</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconCmd" title="Command Prompt">

        <img src="/static/xp_assets/icons/cmd.png" class="xp-desktop-img" alt="Command Prompt">

        <span class="xp-desktop-icon-label">Command Prompt</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconSandbox" title="Sandbox Security Center">

        <img src="/static/xp_assets/icons/security.png" class="xp-desktop-img" alt="Security Center">

        <span class="xp-desktop-icon-label">Security Center</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconDisplay" title="Display Properties (Themes, Wallpaper, Screen Saver)">

        <img src="/static/xp_assets/icons/display.png" class="xp-desktop-img" alt="Personalize">

        <span class="xp-desktop-icon-label">Personalize</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconTaskmgr" title="Windows Task Manager">

        <img src="/static/xp_assets/icons/taskmgr.png" class="xp-desktop-img" alt="Task Manager">

        <span class="xp-desktop-icon-label">Task Manager</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconSandboxDrive" title="Linux Sandbox VM (Network Drive Z:)">

        <img src="/static/xp_assets/icons/network_drive.png" class="xp-desktop-img" alt="Sandbox (Z:)">

        <span class="xp-desktop-icon-label">Sandbox (Z:)</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconSolitaire" title="Solitaire (sol.exe)">

        <img src="/static/xp_assets/icons/solitaire.png" class="xp-desktop-img" alt="Solitaire">

        <span class="xp-desktop-icon-label">Solitaire</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconPinball" title="3D Pinball for Windows - Space Cadet (pinball.exe)">

        <img src="/static/xp_assets/icons/pinball.png" class="xp-desktop-img" alt="3D Pinball">

        <span class="xp-desktop-icon-label">3D Pinball</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconMinesweeper" title="Minesweeper (winmine.exe)">

        <img src="/static/xp_assets/icons/winmine_100.png" class="xp-desktop-img" alt="Minesweeper">

        <span class="xp-desktop-icon-label">Minesweeper</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconNotepad" title="Notepad (notepad.exe)">

        <img src="/static/xp_assets/icons/notepad_2.png" class="xp-desktop-img" alt="Notepad">

        <span class="xp-desktop-icon-label">Notepad</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconPaint" title="Paint (mspaint.exe)">

        <img src="/static/xp_assets/icons/mspaint_1.png" class="xp-desktop-img" alt="Paint">

        <span class="xp-desktop-icon-label">Paint</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconCalc" title="Calculator (calc.exe)">

        <img src="/static/xp_assets/icons/calc_120.png" class="xp-desktop-img" alt="Calculator">

        <span class="xp-desktop-icon-label">Calculator</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconDorySettings" title="Dory Bot & Neural Engine Settings">

        <img src="/static/xp_assets/icons/shell32_167.png" class="xp-desktop-img" alt="Dory Settings">

        <span class="xp-desktop-icon-label">Dory Settings</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconRegedit" title="Registry Editor (regedit.exe)">

        <img src="/static/xp_assets/icons/regedit_100.png" class="xp-desktop-img" alt="Registry Editor">

        <span class="xp-desktop-icon-label">Registry Editor</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconMyDocs" title="My Documents">

        <img src="/static/xp_assets/icons/shell32_235.png" class="xp-desktop-img" alt="My Documents">

        <span class="xp-desktop-icon-label">My Documents</span>

      </div>

      <div class="xp-desktop-icon" id="xpIconSetupCd" title="Windows XP Professional CD-ROM (SETUP.EXE)">
        <img src="/static/xp_assets/icons/shell32_11.png" class="xp-desktop-img" alt="XP Setup CD">
        <span class="xp-desktop-icon-label">XP Setup CD</span>
      </div>
      <div class="xp-desktop-icon" id="xpIconSearch" title="Search Companion (Find files with Rover)">
        <img src="/static/xp_assets/icons/search.png" class="xp-desktop-img" alt="Search">
        <span class="xp-desktop-icon-label">Search</span>
      </div>
      <div class="xp-desktop-icon" id="xpIconRecycle" title="Recycle Bin">

        <img src="/static/xp_assets/icons/recycle_empty.png" id="recycleBinIconImg" class="xp-desktop-img" alt="Recycle Bin">

        <span class="xp-desktop-icon-label">Recycle Bin</span>

      </div>

    `;



    // Seated Robot Workstation at bottom-left

    const workstation = document.createElement('div');

    workstation.className = 'xp-seated-robot-station';

    workstation.id = 'unifiedWorkstation';

    workstation.innerHTML = `

      <div class="xp-robot-seated" id="unifiedRobotSeated">

        <div class="xp-robot-head" id="unifiedRobotHeadNode" title="Click Head to Open 3D Cognitive Hologram!">

          <div class="xp-robot-eyes">

            <span class="xp-robot-eye left"></span>

            <span class="xp-robot-eye right"></span>

          </div>

          <div class="xp-robot-mouth"></div>

        </div>

        <div class="xp-robot-torso">

          <div class="xp-robot-chest-screen">

            <span class="status-pulse-dot"></span>

            <span id="unifiedRobotChestStatus">O(1)</span>

          </div>

        </div>

        <div class="xp-robot-arm arm-left"></div>

        <div class="xp-robot-arm arm-right"></div>

        <div class="xp-robot-legs">

          <div class="xp-robot-leg left"></div>

          <div class="xp-robot-leg right"></div>

        </div>

      </div>

      <div class="xp-workstation-desk">

        <div class="xp-crt-monitor">

          <div class="xp-crt-screen">

            <div class="xp-crt-scanline"></div>

            <div class="xp-crt-code" id="unifiedCrtCodeDisplay">&gt; Dory ready_</div>

          </div>

          <div class="xp-crt-base"></div>

        </div>

        <div class="xp-keyboard"></div>

        <div class="xp-mouse"></div>

      </div>

    `;



    // White Cloud Thought Bubble with Black Text

    const thoughtContainer = document.createElement('div');

    thoughtContainer.className = 'xp-cloud-thought-container';

    thoughtContainer.id = 'unifiedThoughtContainer';

    thoughtContainer.innerHTML = `

      <div class="xp-cloud-thought-box">

        <div class="xp-cloud-thought-header">

          <div class="xp-cloud-thought-title">

            <span style="font-size:14px;">💭</span>

            <span>Dory Internal Thought</span>

          </div>

          <span class="xp-cloud-thought-badge" id="unifiedThoughtBadge">Recurrent</span>

        </div>

        <div class="xp-cloud-thought-body" id="unifiedThoughtBody">Ready for next instruction. Send a task in Messenger to begin!</div>

      </div>

      <div class="xp-thought-tail">

        <div class="xp-bubble-dot dot-3"></div>

        <div class="xp-bubble-dot dot-2"></div>

        <div class="xp-bubble-dot dot-1"></div>

      </div>

    `;



    // 3D Cognitive Hologram Window over Robot Head

    const headHoloWindow = document.createElement('div');

    headHoloWindow.className = 'xp-head-holo-window';

    headHoloWindow.id = 'unifiedHeadHoloWindow';

    headHoloWindow.style.cssText = `

      position: absolute;

      left: 30px;

      bottom: 275px;

      width: 250px;

      background: rgba(2, 6, 23, 0.95);

      border: 1.5px solid #00e5ff;

      border-radius: 6px;

      box-shadow: 0 0 18px rgba(0, 229, 255, 0.45);

      z-index: 45;

      display: none;

      flex-direction: column;

      overflow: hidden;

      font-family: monospace;

      font-size: 10.5px;

      color: #76b900;

    `;

    headHoloWindow.innerHTML = `

      <div style="background:linear-gradient(90deg, #0284c7, #082f49); color:#fff; padding:3px 6px; font-size:10px; display:flex; justify-content:space-between; align-items:center;">

        <span>🛸 3D Cognitive Hologram</span>

        <span id="closeHoloBtn" style="cursor:pointer; font-weight:bold;">&times;</span>

      </div>

      <canvas id="holoCanvas" width="250" height="110" style="background:#020610; display:block;"></canvas>

      <div id="holoStats" style="padding:4px 6px; font-size:9.5px; color:#38bdf8; border-top:1px solid #1e293b; background:#040d1a;">

        SQL WAL: Active • Cognitive Nodes: 4

      </div>

    `;



    // Luna Taskbar (Strict Horizontal Ordering - Pic 1 Fix)

    const taskbar = document.createElement('div');

    taskbar.className = 'xp-taskbar';

    taskbar.id = 'unifiedTaskbar';

    taskbar.innerHTML = `

      <button class="xp-start-btn" id="unifiedStartBtn">

        <svg viewBox="0 0 16 16" width="16" height="16">

          <path d="M 1 2 L 7 1 L 7 7 L 1 7 Z" fill="#eb5e28"/>

          <path d="M 8 1 L 15 0 L 15 7 L 8 7 Z" fill="#76b900"/>

          <path d="M 1 8 L 7 8 L 7 14 L 1 13 Z" fill="#0055ea"/>

          <path d="M 8 8 L 15 8 L 15 15 L 8 14 Z" fill="#ffd600"/>

        </svg>

        <span>start</span>

      </button>

      <div class="xp-task-buttons" id="unifiedTaskButtons">

        <div class="xp-task-btn" id="taskBtnWlmContacts" title="Windows Live Messenger - Contacts">

          <img src="/static/wlm/Icons/icon_32.png" width="16" height="16" alt="WLM">

          <span>Contacts (MSN)</span>

        </div>

        <div class="xp-task-btn active" id="taskBtnWlm" title="Windows Live Messenger - Chat">

          <img src="/static/wlm/Icons/app_online.ico" width="16" height="16" alt="WLM Chat">

          <span id="taskBtnWlmLabel">Dory Chat</span>

        </div>

        <div class="xp-task-btn" id="taskBtnExplorer" title="My Computer - Explorer">

          <img src="/static/xp_assets/icons/my_computer.png" width="16" height="16" alt="Explorer">

          <span>My Computer</span>

        </div>

        <div class="xp-task-btn" id="taskBtnSql" title="SQL Authority Studio 3D">

          <img src="/static/xp_assets/icons/icon_103.png" width="16" height="16" alt="SQL">

          <span>SQL Authority 3D</span>

        </div>

        <div class="xp-task-btn" id="taskBtnCmd" title="Command Prompt">

          <img src="/static/xp_assets/icons/cmd.png" width="16" height="16" alt="CMD">

          <span>Command Prompt</span>

        </div>

        <div class="xp-task-btn" id="taskBtnNotepad" title="Notepad">

          <img src="/static/xp_assets/icons/notepad_2.png" width="16" height="16" alt="Notepad">

          <span>Notepad</span>

        </div>

        <div class="xp-task-btn" id="taskBtnSandbox" title="Sandbox Security Center">

          <img src="/static/xp_assets/icons/security.png" width="16" height="16" alt="Security">

          <span>Sandbox Security</span>

        </div>

      </div>

      <div class="xp-system-tray">

        <img src="/static/wlm/Icons/app_online.ico" class="xp-tray-icon" id="wlmTrayAppIcon" title="Windows Live Messenger (Right-click for menu)" alt="WLM">

        <img src="/static/xp_assets/icons/security.png" class="xp-tray-icon" title="Workspace Sandbox Active" id="traySandboxIcon" alt="Sandbox">

        <img src="/static/xp_assets/icons/mmsys_100.png" class="xp-tray-icon" title="Volume 100%" alt="Volume" style="cursor:pointer;" id="trayVolumeIcon">

        <span class="xp-tray-clock" id="unifiedTrayClock">12:00 PM</span>

      </div>

    `;



    // Start Menu

    const startMenu = document.createElement('div');

    startMenu.className = 'xp-start-menu';

    startMenu.id = 'unifiedStartMenu';

    startMenu.innerHTML = `

      <div class="xp-start-menu-header">

        <div class="xp-start-user-avatar">

          <img src="/static/wlm/wlm/usertiles/usertile_flare.png" width="28" height="28" style="border-radius:3px;">

        </div>

        <span class="xp-start-user-name">User</span>

      </div>

      <div class="xp-start-menu-content">

        <div class="xp-start-left-col">

          <div class="xp-start-item" id="smWlmContacts">

            <img src="/static/wlm/Icons/icon_32.png" width="22" height="22">

            <div><strong>MSN Contact List</strong><br><small style="color:#64748b;">All session personas</small></div>

          </div>

          <div class="xp-start-item" id="smWlm">

            <img src="/static/wlm/Icons/app_online.ico" width="22" height="22">

            <div><strong>Windows Live Messenger</strong><br><small style="color:#64748b;">Chat with agent</small></div>

          </div>

          <div class="xp-start-item" id="smSql">

            <img src="/static/xp_assets/icons/icon_103.png" width="22" height="22">

            <div><strong>SQL Authority Studio 3D</strong><br><small style="color:#64748b;">3D Cognitive Node Graph</small></div>

          </div>

          <div class="xp-start-item" id="smExplorer">

            <img src="/static/xp_assets/icons/my_computer.png" width="22" height="22">

            <div><strong>Windows Explorer</strong><br><small style="color:#64748b;">Browse Files & Folders</small></div>

          </div>

          <div class="xp-start-item" id="smCmd">

            <img src="/static/xp_assets/icons/cmd.png" width="22" height="22">

            <div><strong>Command Prompt</strong><br><small style="color:#64748b;">Tool execution log</small></div>

          </div>

          <div class="xp-start-item" id="smSandbox">

            <img src="/static/xp_assets/icons/security.png" width="22" height="22">

            <div><strong>Sandbox Security Center</strong><br><small style="color:#64748b;">Path jail enforcement</small></div>

          </div>

          <div class="xp-start-item" id="smNotepad">

            <img src="/static/xp_assets/icons/notepad.png" width="22" height="22">

            <div><strong>Notepad</strong><br><small style="color:#64748b;">Text viewer</small></div>

          </div>

          <div class="xp-start-item" id="smDorySettings">

            <img src="/static/xp_assets/icons/shell32_167.png" width="22" height="22">

            <div><strong>Dory Bot Settings</strong><br><small style="color:#64748b;">Neural loops &amp; parameters</small></div>

          </div>

          <div class="xp-all-programs-btn" id="smAllProgramsBtn">

            <span>All Programs</span> <span>▶</span>

            <div class="xp-all-programs-menu" id="smProgramsFlyout">

              <div class="xp-prog-item has-sub" id="piAccessories">

                <span style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/folder.png" width="16" height="16"> Accessories</span>

                <div class="xp-prog-sub-flyout">

                  <div class="xp-prog-item" id="piCalc"><img src="/static/xp_assets/icons/calc.png" width="16" height="16"> <span>Calculator</span></div>

                  <div class="xp-prog-item" id="piPaint"><img src="/static/xp_assets/icons/mspaint.png" width="16" height="16"> <span>Paint</span></div>

                  <div class="xp-prog-item" id="piNotepad"><img src="/static/xp_assets/icons/notepad.png" width="16" height="16"> <span>Notepad</span></div>

                  <div class="xp-prog-item" id="piCmd"><img src="/static/xp_assets/icons/cmd.png" width="16" height="16"> <span>Command Prompt</span></div>

                  <div class="xp-prog-item" id="piSounds"><img src="/static/xp_assets/icons/audio.png" width="16" height="16"> <span>Volume Control</span></div>

                  <div class="xp-prog-item" id="piRegedit"><img src="/static/xp_assets/icons/regedit_100.png" width="16" height="16"> <span>Registry Editor</span></div>

                  <div class="xp-prog-item" id="piWinver"><img src="/static/xp_assets/icons/shell32_24.png" width="16" height="16"> <span>About Windows</span></div>

                </div>

              </div>

              <div class="xp-prog-item has-sub" id="piGames">

                <span style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/folder.png" width="16" height="16"> Games</span>

                <div class="xp-prog-sub-flyout">

                  <div class="xp-prog-item" id="piPinball"><img src="/static/xp_assets/icons/pinball.png" width="16" height="16"> <span>3D Pinball</span></div>

                  <div class="xp-prog-item" id="piSolitaire"><img src="/static/xp_assets/icons/solitaire.png" width="16" height="16"> <span>Solitaire</span></div>

                  <div class="xp-prog-item" id="piMinesweeper"><img src="/static/xp_assets/icons/minesweeper.png" width="16" height="16"> <span>Minesweeper</span></div>

                </div>

              </div>

              <div class="xp-prog-item" id="piWlm"><img src="/static/wlm/Icons/icon_32.png" width="16" height="16"> <span>Windows Live Messenger</span></div>

              <div class="xp-prog-item" id="piSql"><img src="/static/xp_assets/icons/icon_103.png" width="16" height="16"> <span>SQL Authority Studio 3D</span></div>

              <div class="xp-prog-item" id="piAutorun"><img src="/static/xp_assets/icons/shell32_11.png" width="16" height="16"> <span>Windows XP Setup CD</span></div>

              <div class="xp-prog-item" id="piSetupWiz"><img src="/static/xp_assets/icons/shell32_22.png" width="16" height="16"> <span>Reinstall / Setup Wizard</span></div>

              <div class="xp-prog-item" id="piExplorer"><img src="/static/xp_assets/icons/my_computer.png" width="16" height="16"> <span>Windows Explorer</span></div>

              <div class="xp-prog-item" id="piControl"><img src="/static/xp_assets/icons/control_panel.png" width="16" height="16"> <span>Control Panel</span></div>

            </div>

          </div>

        </div>

        <div class="xp-start-right-col">

          <div class="xp-start-item" id="smMyComp" style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/my_computer.png" width="18" height="18"> <strong>My Computer</strong></div>

          <div class="xp-start-item" id="smMyDocs" style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/folder.png" width="18" height="18"> My Documents</div>

          <div class="xp-start-item" id="smControl" style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/control_panel.png" width="18" height="18"> Control Panel</div>

          <div class="xp-start-item" id="smHelp" style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/help.png" width="18" height="18"> Help &amp; Support</div>

          <div class="xp-start-item" id="smSearch" style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/search.png" width="18" height="18"> Search</div>

          <div class="xp-start-item" id="smRun" style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/run.png" width="18" height="18"> <strong>Run...</strong></div>

        </div>

      </div>

      <div class="xp-start-menu-footer">

        <span style="cursor:pointer; display:flex; align-items:center; gap:6px;" id="smLogOff"><img src="/static/xp_assets/icons/logoff.png" width="16" height="16"> Log Off</span>

        <span style="cursor:pointer; display:flex; align-items:center; gap:6px;" id="smTurnOff"><img src="/static/xp_assets/icons/shutdown.png" width="16" height="16"> Turn Off Computer</span>

      </div></div>

    `;



    // Context Menus (Desktop & MSN System Tray)

    const desktopContextMenu = document.createElement('div');

    desktopContextMenu.className = 'xp-context-menu';

    desktopContextMenu.id = 'xpDesktopContextMenu';

    desktopContextMenu.innerHTML = `

      <div class="xp-context-menu-item" id="ctxDesktopArrange">

        <span>Arrange Icons By</span> <span>▶</span>

        <div class="xp-context-submenu">

          <div class="xp-context-menu-item" id="ctxArrangeName">Name</div>

          <div class="xp-context-menu-item" id="ctxArrangeSize">Size</div>

          <div class="xp-context-menu-item" id="ctxArrangeType">Type</div>

          <div class="xp-context-menu-item" id="ctxArrangeModified">Modified</div>

        </div>

      </div>

      <div class="xp-context-menu-item" id="ctxDesktopRefresh">

        <span>Refresh</span>

      </div>

      <div class="xp-context-menu-separator"></div>

      <div class="xp-context-menu-item" id="ctxDesktopNew">

        <span>New</span> <span>▶</span>

        <div class="xp-context-submenu">

          <div class="xp-context-menu-item" id="ctxNewNotepad">Text Document</div>

          <div class="xp-context-menu-item" id="ctxNewSession">IBA Session</div>

        </div>

      </div>

      <div class="xp-context-menu-separator"></div>

      <div class="xp-context-menu-item" id="ctxDesktopProps">

        <span>Properties</span>

      </div>

      <div class="xp-context-menu-separator"></div>

      <div class="xp-context-menu-item" id="ctxDesktopReinstall" style="color:#b91c1c;">

        <span><img src="/static/xp_assets/icons/shell32_11.png" class="xp-title-icon" alt="CD"> Reinstall Windows XP (Factory Reset)</span>

      </div>

    `;



    const taskbarContextMenu = document.createElement('div');

    taskbarContextMenu.className = 'xp-context-menu';

    taskbarContextMenu.id = 'xpTaskbarContextMenu';

    taskbarContextMenu.innerHTML = `

      <div class="xp-context-menu-item" id="ctxTaskbarCascade">Cascade Windows</div>

      <div class="xp-context-menu-item" id="ctxTaskbarTileH">Tile Windows Horizontally</div>

      <div class="xp-context-menu-item" id="ctxTaskbarTileV">Tile Windows Vertically</div>

      <div class="xp-context-menu-item" id="ctxTaskbarShowDesk">Show the Desktop</div>

      <div class="xp-context-menu-separator"></div>

      <div class="xp-context-menu-item default-action" id="ctxTaskbarTaskmgr">Task Manager</div>

      <div class="xp-context-menu-separator"></div>

      <div class="xp-context-menu-item" id="ctxTaskbarLock">✓ Lock the Taskbar</div>

      <div class="xp-context-menu-item" id="ctxTaskbarProps">Properties</div>

    `;



    const trayContextMenu = document.createElement('div');

    trayContextMenu.className = 'xp-context-menu';

    trayContextMenu.id = 'xpTrayContextMenu';

    trayContextMenu.innerHTML = `

      <div class="xp-context-menu-item default-action" id="ctxTrayOpenWlm">

        <span>Open Windows Live Messenger</span>

      </div>

      <div class="xp-context-menu-separator"></div>

      <div class="xp-context-menu-item" id="ctxTrayStatus">

        <span>My Status</span> <span>▶</span>

        <div class="xp-context-submenu">

          <div class="xp-context-menu-item" data-status="Available"><img src="/static/wlm/Icons/app_online.ico" class="xp-title-icon"> Available</div>

          <div class="xp-context-menu-item" data-status="Busy"><img src="/static/wlm/Icons/app_busy.ico" class="xp-title-icon"> Busy</div>

          <div class="xp-context-menu-item" data-status="Away"><img src="/static/wlm/Icons/app_away.ico" class="xp-title-icon"> Away</div>

          <div class="xp-context-menu-item" data-status="Appear Offline"><img src="/static/wlm/Icons/app_offline.ico" class="xp-title-icon"> Appear Offline</div>

        </div>

      </div>

      <div class="xp-context-menu-separator"></div>

      <div class="xp-context-menu-item" id="ctxTraySignOut">

        <span>Sign Out</span>

      </div>

      <div class="xp-context-menu-item" id="ctxTrayExit">

        <span>Exit</span>

      </div>

    `;



    // -----------------------------------------------------------------------

    // WINDOW 0: WLM Revival Client - Main Window (Wave-4 Contact List)

    // -----------------------------------------------------------------------

    const wlmMainWindow = document.createElement('div');

    wlmMainWindow.className = 'wlm-window wlm-contact-window';

    wlmMainWindow.id = 'unifiedWlmMainWindow';

    wlmMainWindow.style.left = '460px';

    wlmMainWindow.style.top = '15px';

    wlmMainWindow.style.width = '330px';

    wlmMainWindow.style.height = '560px';

    wlmMainWindow.style.display = 'flex';



    wlmMainWindow.innerHTML = `

      <div class="wlm-main-header" id="wlmMainHeaderHandle" style="background-image: url('/static/wlm/wlm/background_blue.jpg');">

        <div class="wlm-window-controls">

          <button class="wlm-win-btn" id="wlmMainMinBtn" title="Minimize">_</button>

          <button class="wlm-win-btn close" id="wlmMainCloseBtn" title="Close">&times;</button>

        </div>

        <div class="wlm-main-user-row">

          <div class="wlm-main-user-tile-wrapper">

            <img src="/static/wlm/wlm/status_frame_online_small.png" class="wlm-status-frame" alt="Frame">

            <img src="/static/wlm/wlm/usertiles/usertile_flare.png" class="wlm-main-user-dp" id="wlmCurrentUserDp" alt="User">

          </div>

          <div class="wlm-main-user-meta">

            <div class="wlm-main-user-name">User <span class="wlm-status-tag" id="wlmCurrentStatusTag">(Available) ▾</span></div>

            <div class="wlm-main-user-bubble" title="Personal status message">

              <input type="text" class="wlm-main-pm-input" id="wlmUserPmInput" value="IBA Autonomous Agentic Workspace • Windows XP">

            </div>

          </div>

        </div>

        <div class="wlm-main-action-bar">

          <button class="wlm-main-action-btn" id="wlmMainMailBtn" title="Mailbox"><img src="/static/wlm/wlm/mail.png" width="16" height="12"> <span>Inbox</span></button>

          <button class="wlm-main-action-btn" id="wlmMainNewSessionBtn" title="Create New Session"><img src="/static/wlm/wlm/add_contact.png" width="16" height="14"> <span>New Session</span></button>

          <button class="wlm-main-action-btn" id="wlmMainThemeBtn" title="Cycle Personalization Theme"><img src="/static/wlm/wlm/select_background.png" width="15" height="14"></button>

        </div>

      </div>



      <div class="wlm-search-bar">

        <input type="text" class="wlm-search-input" id="wlmContactSearchInput" placeholder="Search sessions or personas...">

        <span class="wlm-search-icon" style="font-size:11px; margin-left:4px;">🔍</span>

      </div>



      <div class="wlm-contact-list-body" id="wlmContactListBody">

        <div class="wlm-group-header" id="wlmGroupSessionsHeader">

          <span class="wlm-group-arrow">▼</span>

          <span class="wlm-group-title">Project Sessions &amp; Personas</span>

          <span class="wlm-group-count" id="wlmSessionsCount">(0)</span>

        </div>

        <div class="wlm-group-items" id="wlmSessionsGroupItems">

          <div style="padding:10px 16px; color:#94a3b8; font-size:11px;">Loading sessions...</div>

        </div>



        <div class="wlm-group-header" style="margin-top:6px;">

          <span class="wlm-group-arrow">▶</span>

          <span class="wlm-group-title">Core Agents (Offline)</span>

          <span class="wlm-group-count">(3)</span>

        </div>

      </div>



      <div class="wlm-main-footer">

        <img src="/static/wlm/wlm/footer_logo.jpg" style="height:18px; object-fit:contain;" alt="Windows Live">

        <span style="font-size:10px; color:#64748b;">Live Revival V2</span>

      </div>

    `;



    // -----------------------------------------------------------------------

    // WINDOW 1: WLM Revival Client Window (Chat view with Image Attachments)

    // -----------------------------------------------------------------------

    const wlmWindow = document.createElement('div');

    wlmWindow.className = 'wlm-window wlm-chat-window';

    wlmWindow.id = 'unifiedWlmWindow';

    wlmWindow.style.left = '20px';

    wlmWindow.style.top = '15px';

    wlmWindow.style.width = '425px';

    wlmWindow.style.height = '560px';

    wlmWindow.style.display = 'flex';



    wlmWindow.innerHTML = `

      <!-- Wave-4 Chat Header -->

      <div class="wlm-chat-header" id="wlmChatHeaderHandle">

        <div class="wlm-window-controls">

          <button class="wlm-win-btn" id="wlmMinBtn" title="Minimize">_</button>

          <button class="wlm-win-btn close" id="wlmCloseBtn" title="Close">&times;</button>

        </div>

        <div class="wlm-chat-contact-tile" id="wlmContactTile">

          <img src="/static/wlm/wlm/usertiles/usertile_robot.png" alt="Dory">

        </div>

        <div class="wlm-chat-contact-info">

          <div class="wlm-chat-contact-name">Dory (NVIDIA Nemotron 4B)</div>

          <div class="wlm-chat-contact-pm">Available • Recurrent Loop Cognition • O(1) SQL Memory</div>

        </div>

      </div>



      <!-- Wave-4 Toolbar with Authentic NUDGE & EMOTICONS -->

      <div class="wlm-toolbar">

        <button class="wlm-tb-btn" id="wlmTbPhotos" title="Attach Photos / Screenshots">

          <img src="/static/wlm/wlm/add_contact.png" alt="Photo" style="filter:hue-rotate(90deg);">

          <span>Photos</span>

        </button>

        <button class="wlm-tb-btn" id="wlmTbFiles" title="Send Files">

          <img src="/static/xp_assets/icons/shell32_4.png" width="16" height="16" alt="Files">

          <span>Files</span>

        </button>

        <button class="wlm-tb-btn" id="wlmTbEmoticons" title="Emoticons">

          <img src="/static/wlm/wlm/emoticons/wink.gif" alt="Emoticon">

          <span>Emoticons</span>

        </button>

        <button class="wlm-tb-btn" id="wlmTbWinks" title="Send a Wink (Full Screen Animated Wink Clip!)" style="color:#0284c7; font-weight:700;">

          <img src="/static/wlm/wlm/select_wink.png" alt="Wink" width="16" height="16">

          <span>Winks</span>

        </button>

        <button class="wlm-tb-btn" id="wlmTbNudge" title="Send Nudge to Dory (Shakes Window!)" style="font-weight:700; color:#b45309;">

          <img src="/static/wlm/wlm/arrow.png" width="14" height="14" alt="Nudge" style="transform:rotate(45deg);">

          <span>Nudge</span>

        </button>

        <div style="flex:1;"></div>

        <button class="wlm-tb-btn" id="wlmTbBotSettings" title="Dory Bot & Neural Engine Settings">

          <img src="/static/xp_assets/icons/shell32_167.png" width="16" height="16" alt="Settings">

          <span>Bot Settings</span>

        </button>

        <button class="wlm-tb-btn" id="wlmTbSwitchView" title="Toggle Contact List / Session Personas">

          <img src="/static/wlm/Icons/icon_32.png" width="16" height="16" alt="Contacts">

          <span>Contacts</span>

        </button>

      </div>



      <!-- Full Screen Winks Animation Video Overlay (Pic 1 Match) -->

      <div class="wlm-wink-overlay" id="wlmWinkOverlay" style="display:none;"></div>



      <!-- Winks Picker Popup -->

      <div class="wlm-wink-picker-popup" id="wlmWinksPopup">

        <div class="wlm-wink-picker-title">

          <span>Choose a Wink to send</span>

          <span style="cursor:pointer;" id="wlmCloseWinksBtn">&times;</span>

        </div>

        <div class="wlm-wink-grid" id="wlmWinksGrid">

          <div class="wlm-wink-grid-item" data-wink="water_balloon" title="Water Balloon">

            <img src="/static/wlm/wlm/winks/water_balloon.png">

            <span>Water Balloon</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="frog" title="Frog">

            <img src="/static/wlm/wlm/winks/frog.png">

            <span>Frog</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="guitar_smash" title="Guitar Smash">

            <img src="/static/wlm/wlm/winks/guitar_smash.png">

            <span>Guitar</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="dancing_pig" title="Dancing Pig">

            <img src="/static/wlm/wlm/winks/dancing_pig.png">

            <span>Pig</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="knock" title="Knock on Screen">

            <img src="/static/wlm/wlm/winks/knock.png">

            <span>Knock</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="heartkey" title="Heart &amp; Key">

            <img src="/static/wlm/wlm/winks/heart.png">

            <span>Heart</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="kiss" title="Kiss">

            <img src="/static/wlm/wlm/winks/kiss.png">

            <span>Kiss</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="laughing_girl" title="Laughing Girl">

            <img src="/static/wlm/wlm/winks/laughing_girl.png">

            <span>Laugh</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="fartguy" title="Whoopee Cushion">

            <img src="/static/wlm/wlm/winks/crying.png">

            <span>Whoopee</span>

          </div>

          <div class="wlm-wink-grid-item" data-wink="bow" title="Formal Bow">

            <img src="/static/wlm/wlm/winks/bow.jpg">

            <span>Bow</span>

          </div>

        </div>

      </div>



      <!-- Emoticon Picker Popup -->

      <div class="wlm-emoticon-picker" id="wlmEmoticonPicker">

        <div class="wlm-emoticon-item" data-code=";)" title="Wink"><img src="/static/wlm/wlm/emoticons/wink.gif"></div>

        <div class="wlm-emoticon-item" data-code=":-S" title="Thinking"><img src="/static/wlm/wlm/emoticons/thinking.gif"></div>

        <div class="wlm-emoticon-item" data-code="(co)" title="Computer"><img src="/static/wlm/wlm/emoticons/computer.gif"></div>

        <div class="wlm-emoticon-item" data-code="8-)" title="Nerd"><img src="/static/wlm/wlm/emoticons/nerd.gif"></div>

        <div class="wlm-emoticon-item" data-code="(li)" title="Lightning"><img src="/static/wlm/wlm/emoticons/lightning.gif"></div>

        <div class="wlm-emoticon-item" data-code="<:o)" title="Party"><img src="/static/wlm/wlm/emoticons/party.gif"></div>

        <div class="wlm-emoticon-item" data-code=":'(" title="Cry"><img src="/static/wlm/wlm/emoticons/cry.gif"></div>

        <div class="wlm-emoticon-item" data-code="(^)" title="Cake"><img src="/static/wlm/wlm/emoticons/cake.gif"></div>

        <div class="wlm-emoticon-item" data-code="(pi)" title="Pizza"><img src="/static/wlm/wlm/emoticons/pizza.gif"></div>

        <div class="wlm-emoticon-item" data-code=":-@" title="Mad"><img src="/static/wlm/wlm/emoticons/mad.gif"></div>

        <div class="wlm-emoticon-item" data-code=":O" title="Huh"><img src="/static/wlm/wlm/emoticons/huh.gif"></div>

        <div class="wlm-emoticon-item" data-code="(robot)" title="Dory Bot"><img src="/static/wlm/wlm/usertiles/usertile_robot.png"></div>

      </div>



      <!-- Centered Step Progression Flow Bar inside WLM Chat Window -->

      <div class="xp-step-flow-bar-wrapper" style="border-bottom: 1px solid #cbd5e1; background:#f1f5f9;">

        <div class="xp-step-flow-bar" id="wlmStepFlowBar">

          <div class="xp-flow-step is-active" id="wlmStepWelcome">

            <span class="xp-step-label"><span class="status-pulse-dot"></span> Ready for Tasks</span>

            <span class="iba-step-pill">System</span>

          </div>

        </div>

        <div class="xp-jump-bottom-btn" id="wlmStepJumpBtn">

          <span>⬇</span> <span>Jump to active</span>

        </div>

      </div>



      <!-- Chat History Message Stream -->

      <div class="wlm-chat-history" id="wlmChatHistory">

        <div class="wlm-chat-msg-row">

          <div class="wlm-chat-msg-header bot">

            <img src="/static/wlm/wlm/usertiles/usertile_robot.png" width="16" height="16" style="border-radius:2px;">

            <span>IBA says:</span>

            <span class="wlm-chat-msg-time">${new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'})}</span>

          </div>

          <div class="wlm-chat-msg-body">

            Hello! I am Dory, your NVIDIA Nemotron AI assistant. I run with <strong>latent recurrent loops</strong> and <strong>O(1) SQLite Memory</strong> inside this Windows XP workstation.

            Ask me any coding, shader, or logic task!

          </div>

        </div>

      </div>



      <!-- WLM Chat Input Bar with Image Attachments -->

      <div class="wlm-chat-input-area">

        <div class="wlm-chat-attachment-bar" id="wlmAttachmentBar" style="display:none;"></div>

        <div class="wlm-chat-input-toolbar">

          <span style="font-weight:bold; cursor:pointer;" title="Font">A</span>

          <span style="cursor:pointer;" id="wlmToolbarSmiley" title="Emoticons"><img src="/static/wlm/wlm/emoticons/smile.gif" width="14" height="14" alt="Smiley"></span>

          <span style="cursor:pointer;" id="wlmToolbarAttach" title="Attach Image Screenshot"><img src="/static/xp_assets/icons/shell32_1.png" width="14" height="14" alt="Attach"></span>

          <span style="cursor:pointer;" title="Sound Clips"><img src="/static/xp_assets/icons/mmsys_100.png" width="14" height="14" alt="Audio"></span>

          <span style="flex:1;"></span>

          <span style="font-size:10px; color:#94a3b8;">Enter to send • Shift+Enter for newline • Paste images with Ctrl+V</span>

        </div>

        <div class="wlm-chat-input-row">

          <div class="wlm-chat-user-tile-left" id="wlmChatUserTileLeft" title="Click to customize profile &amp; DP">

            <img src="/static/wlm/wlm/usertiles/usertile_dog.png" id="wlmBottomUserDpImg" alt="You">

          </div>

          <textarea class="wlm-chat-textarea" id="wlmChatTextarea" placeholder="Write a message to contact..."></textarea>

          <button class="wlm-aero-btn" id="wlmChatSendBtn">

            <span>Send</span>

          </button>

        </div>

        <input type="file" id="wlmImageUploadInput" accept="image/*" multiple style="display:none;">

      </div>

    `;



    // -----------------------------------------------------------------------

    // WINDOW 2: File Explorer ("My Computer") - Authentic Luna Titlebar (Pic 3)

    // -----------------------------------------------------------------------

    const explorerWindow = document.createElement('div');

    explorerWindow.className = 'xp-window xp-explorer-window minimized';

    explorerWindow.id = 'unifiedExplorerWindow';

    explorerWindow.style.left = '100px';

    explorerWindow.style.top = '30px';



    explorerWindow.innerHTML = `
      <div class="title-bar" id="explorerTitleHandle">
        <div class="title-bar-text" id="explorerTitleText">
          <img src="/static/xp_assets/icons/explorer_100.png" class="xp-title-icon" alt="Explorer">
          <span>My Computer - Windows Explorer</span>
        </div>
        <div class="title-bar-controls">
          <button class="xp-caption-btn min" aria-label="Minimize" id="expMinBtn" title="Minimize"></button>
          <button class="xp-caption-btn max" aria-label="Maximize" id="expMaxBtn" title="Maximize"></button>
          <button class="xp-caption-btn close" aria-label="Close" id="expCloseBtn" title="Close"></button>
        </div>
      </div>
      <div class="xp-window-menubar">
        <div class="xp-menu-item" id="expMenuFile">File
          <div class="xp-dropdown-menu">
            <div class="xp-drop-item" id="expActNewFolder">New Folder</div>
            <div class="xp-drop-item" id="expActNewDoc">New Text Document</div>
            <div class="xp-drop-separator"></div>
            <div class="xp-drop-item" id="expActClose">Close</div>
          </div>
        </div>
        <div class="xp-menu-item" id="expMenuEdit">Edit
          <div class="xp-dropdown-menu">
            <div class="xp-drop-item" id="expActCut">Cut</div>
            <div class="xp-drop-item" id="expActCopy">Copy</div>
            <div class="xp-drop-item" id="expActPaste">Paste</div>
            <div class="xp-drop-separator"></div>
            <div class="xp-drop-item" id="expActSelectAll">Select All</div>
          </div>
        </div>
        <div class="xp-menu-item" id="expMenuView">View
          <div class="xp-dropdown-menu">
            <div class="xp-drop-item" id="expViewThumbnails">Thumbnails</div>
            <div class="xp-drop-item" id="expViewTiles">Tiles</div>
            <div class="xp-drop-item" id="expViewIcons">Icons</div>
            <div class="xp-drop-item" id="expViewList">List</div>
            <div class="xp-drop-item" id="expViewDetails">Details</div>
            <div class="xp-drop-separator"></div>
            <div class="xp-drop-item" id="expActRefresh">Refresh</div>
          </div>
        </div>
        <div class="xp-menu-item" id="expMenuFavorites">Favorites</div>
        <div class="xp-menu-item" id="expMenuTools">Tools
          <div class="xp-dropdown-menu">
            <div class="xp-drop-item" id="expActMapDrive">Map Network Drive (Z:)...</div>
            <div class="xp-drop-item" id="expActFolderOpts">Folder Options...</div>
          </div>
        </div>
        <div class="xp-menu-item" id="expMenuHelp">Help</div>
        <div class="xp-menubar-throbber">
          <img src="/static/xp_assets/icons/xp_throbber_flag.png" class="xp-throbber-flag" alt="Windows XP">
        </div>
      </div>
      <div class="xp-explorer-toolbar">
        <button class="xp-tb-back-btn" id="expBackBtn" title="Back">
          <img src="/static/xp_assets/icons/xp_btn_back.png" alt="Back">
          <span>Back</span>
          <span style="font-size:8px; margin-left:1px;">▼</span>
        </button>
        <button class="xp-tb-fwd-btn" id="expFwdBtn" title="Forward">
          <img src="/static/xp_assets/icons/xp_btn_fwd.png" alt="Forward">
          <span style="font-size:8px;">▼</span>
        </button>
        <button class="xp-tb-btn" id="expUpBtn" title="Up">
          <img src="/static/xp_assets/icons/shell32_147.png" alt="Up">
        </button>
        <div class="xp-tb-sep"></div>
        <button class="xp-tb-btn" id="expSearchBtn">
          <img src="/static/xp_assets/icons/shell32_22.png" alt="Search">
          <span>Search</span>
        </button>
        <button class="xp-tb-btn" id="expFoldersBtn">
          <img src="/static/xp_assets/icons/shell32_45.png" alt="Folders">
          <span>Folders</span>
        </button>
        <button class="xp-tb-btn" id="expViewsBtn" title="Views">
          <img src="/static/xp_assets/icons/shell32_242.png" alt="Views">
          <span style="font-size:8px;">▼</span>
        </button>
      </div>
      <div class="xp-explorer-address-bar">
        <span class="xp-addr-label">Address</span>
        <div class="xp-addr-combo">
          <img src="/static/xp_assets/icons/shell32_4.png" id="expAddrIcon" alt="Folder">
          <input type="text" class="xp-address-input" id="expAddressInput" value="My Computer">
          <button type="button" class="xp-addr-arrow-btn">▼</button>
        </div>
        <button type="button" class="xp-exp-go-btn" id="expGoBtn">
          <img src="/static/xp_assets/icons/xp_btn_go.png" alt="Go">
          <span>Go</span>
        </button>
      </div>
      <div class="xp-explorer-body">
        <div class="xp-explorer-task-pane">
          <div class="xp-task-pane-group">
            <div class="xp-task-pane-header">File and Folder Tasks</div>
            <div class="xp-task-pane-content">
              <a class="xp-task-link" id="expRefreshTask"><img src="/static/xp_assets/icons/shell32_238.png" class="xp-title-icon" alt="Refresh"> Refresh Files</a>
              <a class="xp-task-link" id="expNewDocTask"><img src="/static/xp_assets/icons/notepad_2.png" class="xp-title-icon" alt="Note"> Create Note</a>
            </div>
          </div>
          <div class="xp-task-pane-group">
            <div class="xp-task-pane-header">Other Places</div>
            <div class="xp-task-pane-content">
              <a class="xp-task-link" id="expRootPlace"><img src="/static/xp_assets/icons/shell32_4.png" class="xp-title-icon" alt="Folder"> Workspace Root</a>
              <a class="xp-task-link" id="expAssetsPlace"><img src="/static/xp_assets/icons/mspaint_1.png" class="xp-title-icon" alt="Assets"> Assets</a>
              <a class="xp-task-link" id="expRuntimePlace"><img src="/static/xp_assets/icons/shell32_167.png" class="xp-title-icon" alt="Runtime"> Runtime</a>
            </div>
          </div>
          <div class="xp-task-pane-group">
            <div class="xp-task-pane-header">Details</div>
            <div class="xp-task-pane-content" id="expDetailsPane">
              Select an item to view details.
            </div>
          </div>
        </div>
        <div class="xp-explorer-file-view" id="expFileGrid">
          <div style="padding:20px; color:#64748b;">Loading workspace files...</div>
        </div>
      </div>
      <div class="xp-explorer-statusbar">
        <div class="xp-sb-left" id="expStatusBar">0 objects</div>
        <div class="xp-sb-right">
          <img src="/static/xp_assets/icons/my_computer.png" alt="My Computer">
          <span>My Computer</span>
        </div>
      </div>
    `;



    // -----------------------------------------------------------------------

    // WINDOW 3: SQL Authority Studio 3D - Authentic Luna Titlebar (Pic 3)

    // -----------------------------------------------------------------------

    const sqlSuiteWindow = document.createElement('div');

    sqlSuiteWindow.className = 'xp-window xp-sql-suite-window minimized';

    sqlSuiteWindow.id = 'unifiedSqlSuiteWindow';

    sqlSuiteWindow.style.left = '60px';

    sqlSuiteWindow.style.top = '20px';



    sqlSuiteWindow.innerHTML = `

      <div class="title-bar" id="sqlSuiteTitleHandle">

        <div class="title-bar-text">

          <img src="/static/xp_assets/icons/shell32_221.png" class="xp-title-icon" alt="SQL Authority">

          <span>SQL Authority Studio 3D - Multi-Database Cognitive Engine</span>

        </div>

        <div class="title-bar-controls">

          <button class="xp-caption-btn min" aria-label="Minimize" id="sqlMinBtn" title="Minimize"></button>

          <button class="xp-caption-btn max" aria-label="Maximize" id="sqlMaxBtn" title="Maximize"></button>

          <button class="xp-caption-btn close" aria-label="Close" id="sqlCloseBtn" title="Close"></button>

        </div>

      </div>

      <div class="xp-window-menubar">

        <span>Database</span>

        <span>View</span>

        <span>Cognition</span>

        <span>Traversal</span>

        <span>Help</span>

      </div>

      <div class="xp-sql-suite-toolbar">

        <div class="xp-sql-db-tabs">

          <button class="xp-sql-db-btn active" data-db="all" id="dbBtnAll">🌐 Federated View</button>

          <button class="xp-sql-db-btn" data-db="control.sqlite" id="dbBtnControl">⚙️ control.sqlite</button>

          <button class="xp-sql-db-btn" data-db="codemap.sqlite" id="dbBtnCodemap">🗺️ codemap.sqlite</button>

          <button class="xp-sql-db-btn" data-db="higher_memory.sqlite" id="dbBtnHigher">🧠 higher_memory.sqlite</button>

        </div>

        <div class="xp-sql-view-modes">

          <button class="xp-sql-mode-btn active" id="modeBtnGreater">🌌 Greater Concepts</button>

          <button class="xp-sql-mode-btn" id="modeBtnEntries">🔬 Node Entries</button>

        </div>

      </div>

      <div class="xp-sql-breadcrumbs" id="sqlBreadcrumbs">

        <span class="xp-sql-crumb-link" id="crumbHome">Home (Greater Concepts)</span>

        <span>&gt;</span>

        <span id="crumbActive" style="color:#0f172a; font-weight:700;">Federated Cognitive Mesh</span>

      </div>

      <div class="xp-sql-suite-body">

        <div class="xp-sql-canvas-wrap">

          <canvas class="xp-sql-canvas" id="sql3dCanvas" width="550" height="380"></canvas>

          <div class="xp-sql-canvas-overlay" id="sqlCanvasOverlay">

            Mode: Greater Concepts • Orbit: Drag mouse • Zoom: Wheel

          </div>

          <div class="xp-sql-canvas-controls">

            <button class="xp-sql-canvas-btn" id="sqlResetCamBtn">🎥 Reset View</button>

            <button class="xp-sql-canvas-btn" id="sqlAutoRotateBtn">🔄 Auto-Rotate: ON</button>

          </div>

        </div>

        <div class="xp-sql-inspector" id="sqlInspectorPane">

          <div class="xp-sql-insp-header">

            <span>Inspector</span>

            <span id="sqlInspTypeBadge" style="font-size:10px; background:#1e293b; padding:1px 4px; border-radius:3px;">CONCEPT</span>

          </div>

          <div class="xp-sql-insp-field">

            <span class="xp-sql-insp-label">Selected Node</span>

            <span class="xp-sql-insp-val" id="sqlInspNodeTitle">Click any node to inspect</span>

          </div>

          <div class="xp-sql-insp-field">

            <span class="xp-sql-insp-label">Database & Table</span>

            <span class="xp-sql-insp-val" id="sqlInspDbTable">--</span>

          </div>

          <div class="xp-sql-insp-field">

            <span class="xp-sql-insp-label">Content Digest</span>

            <div class="xp-sql-insp-val" id="sqlInspDigest" style="max-height:100px;">None selected.</div>

          </div>

          <div class="xp-sql-related-section">

            <span class="xp-sql-related-title">🔗 Related Entries in Same/Other DBs</span>

            <div class="xp-sql-related-list" id="sqlRelatedEntriesList">

              <span style="font-size:10px; color:#64748b;">No related entries linked.</span>

            </div>

          </div>

        </div>

      </div>

      <div class="status-bar">

        <div class="status-bar-field" id="sqlStatusBar">Graph loaded • 4 Greater Concepts</div>

        <div class="status-bar-field">SQLite WAL Mode Active</div>

      </div>

    `;



    // -----------------------------------------------------------------------

    // WINDOW 4: Workspace Sandbox Security Center - Authentic Luna Titlebar

    // -----------------------------------------------------------------------

    const sandboxWindow = document.createElement('div');

    sandboxWindow.className = 'xp-window xp-sandbox-window minimized';

    sandboxWindow.id = 'unifiedSandboxWindow';

    sandboxWindow.style.left = '160px';

    sandboxWindow.style.top = '40px';



    sandboxWindow.innerHTML = `

      <div class="title-bar" id="sandboxTitleHandle">

        <div class="title-bar-text">

          <img src="/static/xp_assets/icons/appwiz_1.png" class="xp-title-icon" alt="Security Center">

          <span>Windows XP Security Center - Workspace Sandbox Protection</span>

        </div>

        <div class="title-bar-controls">

          <button class="xp-caption-btn min" aria-label="Minimize" id="sandboxMinBtn" title="Minimize"></button>

          <button class="xp-caption-btn max" aria-label="Maximize" id="sandboxMaxBtn" title="Maximize"></button>

          <button class="xp-caption-btn close" aria-label="Close" id="sandboxCloseBtn" title="Close"></button>

        </div>

      </div>

      <div class="xp-window-menubar">

        <span>Security</span>

        <span>Policies</span>

        <span>Audit Log</span>

        <span>Help</span>

      </div>

      <div class="xp-sandbox-header">

        <div class="xp-sandbox-shield-icon"><img src="/static/xp_assets/icons/security.png" width="48" height="48" alt="Shield"></div>

        <div class="xp-sandbox-status-text">

          <h3>Workspace Sandbox Jail: ACTIVE</h3>

          <p>Restricting path traversal & sanitizing execution for Dory LLM wrapper</p>

        </div>

      </div>

      <div class="xp-sandbox-body">

        <div class="xp-sandbox-card">

          <div style="font-weight:700; margin-bottom:4px; font-size:11px;">Active Workspace Boundary</div>

          <code id="sandboxWsRootDisplay" style="color:#0369a1; font-size:11px; word-break:break-all;">Loading...</code>

        </div>

        <div class="xp-sandbox-card">

          <div style="font-weight:700; margin-bottom:4px; font-size:11px;">Security Policy Rules</div>

          <ul style="margin:0; padding-left:16px; font-size:11px; color:#334155;">

            <li><strong>Path Jail:</strong> Blocks all filesystem reads/writes outside workspace root.</li>

            <li><strong>Sensitive Filters:</strong> Blocks access to <code>.env*</code>, <code>id_rsa*</code>, <code>*.pem</code>, <code>*.key</code>.</li>

            <li><strong>Command Sanitizer:</strong> Blocks disk format, raw system shutdown, and suspicious piping.</li>

            <li><strong>Environment Scrubbing:</strong> Strips host system secrets and API tokens from subprocesses.</li>

          </ul>

        </div>

        <div class="xp-sandbox-card flex-1" style="display:flex; flex-direction:column;">

          <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">

            <span style="font-weight:700; font-size:11px;">Recent Sandbox Audit Events (SQLite Memory)</span>

            <button class="btn btn-secondary btn-sm" id="refreshAuditBtn" style="padding:1px 6px; font-size:10px;">Refresh Log</button>

          </div>

          <div style="max-height:140px; overflow-y:auto; border:1px solid #e2e8f0;">

            <table class="xp-sandbox-audit-table" id="sandboxAuditTable">

              <thead>

                <tr>

                  <th>Time</th>

                  <th>Operation</th>

                  <th>Target</th>

                  <th>Status</th>

                </tr>

              </thead>

              <tbody id="sandboxAuditTableBody">

                <tr><td colspan="4" style="text-align:center; color:#94a3b8;">No audit events recorded yet.</td></tr>

              </tbody>

            </table>

          </div>

        </div>

      </div>

    `;



    // -----------------------------------------------------------------------

    // WINDOW 5: Command Prompt - Authentic Luna Titlebar

    // -----------------------------------------------------------------------

    const cmdWindow = document.createElement('div');

    cmdWindow.className = 'xp-window xp-cmd-window minimized';

    cmdWindow.id = 'cmdWindow';

    cmdWindow.style.left = '220px';

    cmdWindow.style.top = '60px';



    cmdWindow.innerHTML = `

      <div class="title-bar" id="cmdTitleHandle">

        <div class="title-bar-text">

          <img src="/static/xp_assets/icons/cmd_1.png" class="xp-title-icon" alt="CMD">

          <span>C:\\Windows\\System32\\cmd.exe - Dory Tool Execution</span>

        </div>

        <div class="title-bar-controls">

          <button class="xp-caption-btn min" aria-label="Minimize" id="cmdMinBtn" title="Minimize"></button>

          <button class="xp-caption-btn max" aria-label="Maximize" id="cmdMaxBtn" title="Maximize"></button>

          <button class="xp-caption-btn close" aria-label="Close" id="cmdCloseBtn" title="Close"></button>

        </div>

      </div>

      <div class="xp-cmd-body" id="cmdBodyText">

Microsoft Windows XP [Version 5.1.2600]

(C) Copyright 1985-2001 Microsoft Corp.



C:\\DoryLLM&gt; echo "IBA Autonomous Tool Engine Ready."

"IBA Autonomous Tool Engine Ready."

C:\\DoryLLM&gt;

      </div>

    `;



    // -----------------------------------------------------------------------

    // WINDOW 6: Notepad - Exact Match for Picture 3!

    // -----------------------------------------------------------------------

    const notepadWindow = document.createElement('div');

    notepadWindow.className = 'xp-window xp-notepad-window minimized';

    notepadWindow.id = 'unifiedNotepadWindow';

    notepadWindow.style.left = '260px';

    notepadWindow.style.top = '50px';



    notepadWindow.innerHTML = `

      <div class="title-bar" id="notepadTitleHandle">

        <div class="title-bar-text" id="notepadTitleText">

          <img src="/static/xp_assets/icons/notepad_2.png" class="xp-title-icon" alt="Notepad">

          <span>Untitled - Notepad</span>

        </div>

        <div class="title-bar-controls">

          <button class="xp-caption-btn min" aria-label="Minimize" id="npMinBtn" title="Minimize"></button>

          <button class="xp-caption-btn max" aria-label="Maximize" id="npMaxBtn" title="Maximize"></button>

          <button class="xp-caption-btn close" aria-label="Close" id="npCloseBtn" title="Close"></button>

        </div>

      </div>

      <div class="xp-window-menubar">

        <span>File</span>

        <span>Edit</span>

        <span>Format</span>

        <span>View</span>

        <span>Help</span>

      </div>

      <textarea class="xp-notepad-body" id="unifiedNotepadBody" placeholder="Notepad file contents will appear here..."></textarea>

    `;



    // Assemble Desktop

    desktop.appendChild(iconsCol);

    desktop.appendChild(thoughtContainer);

    desktop.appendChild(headHoloWindow);

    desktop.appendChild(workstation);

    desktop.appendChild(wlmMainWindow);

    desktop.appendChild(wlmWindow);

    desktop.appendChild(explorerWindow);

    desktop.appendChild(sqlSuiteWindow);

    desktop.appendChild(sandboxWindow);

    desktop.appendChild(cmdWindow);

    desktop.appendChild(notepadWindow);

    desktop.appendChild(startMenu);

    desktop.appendChild(desktopContextMenu);

    desktop.appendChild(trayContextMenu);

    desktop.appendChild(taskbarContextMenu);

    desktop.appendChild(taskbar);



    messagesContainer.appendChild(desktop);

    if (window.XPCore && window.XPCore.loadSavedTheme) {

      window.XPCore.loadSavedTheme();

    }

    if (window.WLMClient && window.WLMClient.applyUserProfile) {

      window.WLMClient.applyUserProfile();

    }



    // Make windows draggable

    makeDraggable(wlmMainWindow, wlmMainWindow.querySelector('#wlmMainHeaderHandle'));

    makeDraggable(wlmWindow, wlmWindow.querySelector('#wlmChatHeaderHandle'));

    makeDraggable(explorerWindow, explorerWindow.querySelector('#explorerTitleHandle'));

    makeDraggable(sqlSuiteWindow, sqlSuiteWindow.querySelector('#sqlSuiteTitleHandle'));

    makeDraggable(sandboxWindow, sandboxWindow.querySelector('#sandboxTitleHandle'));

    makeDraggable(cmdWindow, cmdWindow.querySelector('#cmdTitleHandle'));

    makeDraggable(notepadWindow, notepadWindow.querySelector('#notepadTitleHandle'));



    // Universal Window Resizing

    makeResizable(wlmMainWindow, 260, 360);

    makeResizable(wlmWindow, 340, 320);

    makeResizable(explorerWindow, 380, 260);

    makeResizable(sqlSuiteWindow, 450, 320);

    makeResizable(sandboxWindow, 360, 240);

    makeResizable(cmdWindow, 340, 200);

    makeResizable(notepadWindow, 300, 200);



    // Window Maximization & Restore Helper

    const toggleMaximize = (win, maxBtn) => {

      if (win.classList.contains('maximized')) {

        win.classList.remove('maximized');

        if (win._prevRect) {

          win.style.left = win._prevRect.left;

          win.style.top = win._prevRect.top;

          win.style.width = win._prevRect.width;

          win.style.height = win._prevRect.height;

        }

        if (maxBtn) {

          maxBtn.setAttribute('aria-label', 'Maximize');

          maxBtn.classList.remove('restore');

          maxBtn.classList.add('max');

        }

      } else {

        win._prevRect = {

          left: win.style.left,

          top: win.style.top,

          width: win.style.width,

          height: win.style.height

        };

        win.classList.add('maximized');

        if (maxBtn) {

          maxBtn.setAttribute('aria-label', 'Restore');

          maxBtn.classList.remove('max');

          maxBtn.classList.add('restore');

        }

      }

      bringToFront(win);

    };



    // Setup Window Controls Helper

    const setupWindowControls = (win, minBtn, maxBtn, closeBtn, taskBtn, handle) => {

      if (minBtn) minBtn.addEventListener('click', () => toggleWindow(win, taskBtn));

      if (maxBtn) maxBtn.addEventListener('click', () => toggleMaximize(win, maxBtn));

      if (closeBtn) closeBtn.addEventListener('click', () => {

        win.classList.add('minimized');

        win.style.display = 'none';

        if (taskBtn) taskBtn.classList.remove('active');

      });

      if (handle && maxBtn) {

        handle.addEventListener('dblclick', (e) => {

          if (e.target.closest('button') || e.target.closest('input')) return;

          toggleMaximize(win, maxBtn);

        });

      }

    };



    // Wire Taskbar Buttons

    const taskBtnWlmContacts = taskbar.querySelector('#taskBtnWlmContacts');

    const taskBtnWlm = taskbar.querySelector('#taskBtnWlm');

    const taskBtnExplorer = taskbar.querySelector('#taskBtnExplorer');

    const taskBtnSql = taskbar.querySelector('#taskBtnSql');

    const taskBtnCmd = taskbar.querySelector('#taskBtnCmd');

    const taskBtnNotepad = taskbar.querySelector('#taskBtnNotepad');

    const taskBtnSandbox = taskbar.querySelector('#taskBtnSandbox');



    const toggleWindow = (win, btn) => {

      if (win.classList.contains('minimized') || win.style.display === 'none') {

        win.classList.remove('minimized');

        win.style.display = 'flex';

        bringToFront(win, btn);

      } else if (win.style.zIndex == desktopZIndex) {

        win.classList.add('minimized');

        win.style.display = 'none';

        if (btn) btn.classList.remove('active');

      } else {

        bringToFront(win, btn);

      }

    };



    taskBtnWlmContacts.addEventListener('click', () => toggleWindow(wlmMainWindow, taskBtnWlmContacts));

    taskBtnWlm.addEventListener('click', () => toggleWindow(wlmWindow, taskBtnWlm));

    taskBtnExplorer.addEventListener('click', () => toggleWindow(explorerWindow, taskBtnExplorer));

    taskBtnSql.addEventListener('click', () => toggleWindow(sqlSuiteWindow, taskBtnSql));

    taskBtnCmd.addEventListener('click', () => toggleWindow(cmdWindow, taskBtnCmd));

    taskBtnNotepad.addEventListener('click', () => toggleWindow(notepadWindow, taskBtnNotepad));

    taskBtnSandbox.addEventListener('click', () => toggleWindow(sandboxWindow, taskBtnSandbox));



    // Wire Window Caption Controls (Pic 3 Style)

    setupWindowControls(wlmMainWindow, wlmMainWindow.querySelector('#wlmMainMinBtn'), null, wlmMainWindow.querySelector('#wlmMainCloseBtn'), taskBtnWlmContacts, wlmMainWindow.querySelector('#wlmMainHeaderHandle'));

    wlmWindow.querySelector('#wlmMinBtn').addEventListener('click', () => toggleWindow(wlmWindow, taskBtnWlm));

    wlmWindow.querySelector('#wlmCloseBtn').addEventListener('click', () => toggleWindow(wlmWindow, taskBtnWlm));

    setupWindowControls(explorerWindow, explorerWindow.querySelector('#expMinBtn'), explorerWindow.querySelector('#expMaxBtn'), explorerWindow.querySelector('#expCloseBtn'), taskBtnExplorer, explorerWindow.querySelector('#explorerTitleHandle'));

    setupWindowControls(sqlSuiteWindow, sqlSuiteWindow.querySelector('#sqlMinBtn'), sqlSuiteWindow.querySelector('#sqlMaxBtn'), sqlSuiteWindow.querySelector('#sqlCloseBtn'), taskBtnSql, sqlSuiteWindow.querySelector('#sqlSuiteTitleHandle'));



    // Wire SQL Authority DB Tabs & 3D Controls

    sqlSuiteWindow.querySelectorAll('.xp-sql-db-btn').forEach(btn => {

      btn.addEventListener('click', () => {

        sqlSuiteWindow.querySelectorAll('.xp-sql-db-btn').forEach(b => b.classList.remove('active'));

        btn.classList.add('active');

        const db = btn.dataset.db || 'all';

        if (window.SQLAuthority && window.SQLAuthority.loadSqlGraph) {

          window.SQLAuthority.loadSqlGraph(db);

        }

      });

    });



    const sqlResetCamBtn = sqlSuiteWindow.querySelector('#sqlResetCamBtn');

    if (sqlResetCamBtn) {

      sqlResetCamBtn.addEventListener('click', () => {

        if (window.SQLAuthority && window.SQLAuthority.resetView) {

          window.SQLAuthority.resetView();

        }

      });

    }



    const sqlAutoRotateBtn = sqlSuiteWindow.querySelector('#sqlAutoRotateBtn');

    if (sqlAutoRotateBtn) {

      sqlAutoRotateBtn.addEventListener('click', () => {

        if (window.SQLAuthority && window.SQLAuthority.toggleAutoRotate) {

          const state = window.SQLAuthority.toggleAutoRotate();

          sqlAutoRotateBtn.textContent = `🔄 Auto-Rotate: ${state ? 'ON' : 'OFF'}`;

        }

      });

    }



    const crumbHome = sqlSuiteWindow.querySelector('#crumbHome');

    if (crumbHome) {

      crumbHome.addEventListener('click', () => {

        if (window.SQLAuthority && window.SQLAuthority.loadSqlGraph) {

          window.SQLAuthority.loadSqlGraph('all');

        }

      });

    }



    setupWindowControls(sandboxWindow, sandboxWindow.querySelector('#sandboxMinBtn'), sandboxWindow.querySelector('#sandboxMaxBtn'), sandboxWindow.querySelector('#sandboxCloseBtn'), taskBtnSandbox, sandboxWindow.querySelector('#sandboxTitleHandle'));

    setupWindowControls(cmdWindow, cmdWindow.querySelector('#cmdMinBtn'), cmdWindow.querySelector('#cmdMaxBtn'), cmdWindow.querySelector('#cmdCloseBtn'), taskBtnCmd, cmdWindow.querySelector('#cmdTitleHandle'));

    setupWindowControls(notepadWindow, notepadWindow.querySelector('#npMinBtn'), notepadWindow.querySelector('#npMaxBtn'), notepadWindow.querySelector('#npCloseBtn'), taskBtnNotepad, notepadWindow.querySelector('#notepadTitleHandle'));



    // Right-Click Context Menus Wiring (Desktop & MSN System Tray)

    const wlmTrayAppIcon = taskbar.querySelector('#wlmTrayAppIcon');

    wlmTrayAppIcon.addEventListener('contextmenu', (e) => {

      e.preventDefault();

      e.stopPropagation();

      desktopContextMenu.style.display = 'none';

      trayContextMenu.style.display = 'block';

      trayContextMenu.style.left = '';

      trayContextMenu.style.right = '8px';

      trayContextMenu.style.bottom = '32px';

      trayContextMenu.style.top = '';

    });

    wlmTrayAppIcon.addEventListener('click', () => {

      toggleWindow(wlmMainWindow, taskBtnWlmContacts);

    });

    wlmTrayAppIcon.addEventListener('dblclick', () => {

      wlmMainWindow.classList.remove('minimized');

      wlmMainWindow.style.display = 'flex';

      bringToFront(wlmMainWindow, taskBtnWlmContacts);

    });



    const traySandbox = taskbar.querySelector('#traySandboxIcon');

    if (traySandbox) {

      traySandbox.addEventListener('click', () => {

        sandboxWindow.classList.remove('minimized');

        sandboxWindow.style.display = 'flex';

        bringToFront(sandboxWindow, taskBtnSandbox);

        loadSandboxStatus();

      });

    }



    trayContextMenu.querySelector('#ctxTrayOpenWlm').addEventListener('click', () => {

      wlmMainWindow.classList.remove('minimized');

      wlmMainWindow.style.display = 'flex';

      bringToFront(wlmMainWindow, taskBtnWlmContacts);

      trayContextMenu.style.display = 'none';

    });

    trayContextMenu.querySelectorAll('[data-status]').forEach(item => {

      item.addEventListener('click', () => {

        const st = item.dataset.status;

        const tag = wlmMainWindow.querySelector('#wlmCurrentStatusTag');

        if (tag) tag.textContent = `(${st}) ▾`;

        trayContextMenu.style.display = 'none';

      });

    });

    trayContextMenu.querySelector('#ctxTraySignOut').addEventListener('click', () => {

      alert('Windows Live Messenger: Signed out.');

      trayContextMenu.style.display = 'none';

    });

    trayContextMenu.querySelector('#ctxTrayExit').addEventListener('click', () => {

      wlmMainWindow.classList.add('minimized');

      wlmMainWindow.style.display = 'none';

      wlmWindow.classList.add('minimized');

      wlmWindow.style.display = 'none';

      trayContextMenu.style.display = 'none';

    });



    // Desktop Context Menu: the dynamic desktop subsystem below builds the
    // menu at the exact right-click position (see showDesktopContextMenu).


    // Taskbar Context Menu Wiring

    taskbar.addEventListener('contextmenu', (e) => {

      if (e.target.closest('.xp-task-btn') || e.target.closest('#wlmTrayAppIcon')) return;

      e.preventDefault();

      e.stopPropagation();

      desktopContextMenu.style.display = 'none';

      trayContextMenu.style.display = 'none';

      taskbarContextMenu.style.display = 'block';

      const deskRect = desktop.getBoundingClientRect();

      taskbarContextMenu.style.left = `${Math.min(e.clientX - deskRect.left, desktop.clientWidth - 190)}px`;

      taskbarContextMenu.style.bottom = '32px';

      taskbarContextMenu.style.top = '';

    });



    taskbarContextMenu.querySelector('#ctxTaskbarTaskmgr').addEventListener('click', () => {

      if (window.XPApps && window.XPApps.openTaskManager) {

        window.XPApps.openTaskManager();

      }

      taskbarContextMenu.style.display = 'none';

    });



    taskbarContextMenu.querySelector('#ctxTaskbarProps').addEventListener('click', () => {

      if (window.XPApps && window.XPApps.openDisplayProperties) {

        window.XPApps.openDisplayProperties();

      }

      taskbarContextMenu.style.display = 'none';

    });



    taskbarContextMenu.querySelector('#ctxTaskbarShowDesk').addEventListener('click', () => {

      [wlmMainWindow, wlmWindow, explorerWindow, sqlSuiteWindow, cmdWindow, notepadWindow, sandboxWindow].forEach(w => {

        w.classList.add('minimized');

        w.style.display = 'none';

      });

      taskbar.querySelectorAll('.xp-task-btn').forEach(b => b.classList.remove('active'));

      taskbarContextMenu.style.display = 'none';

    });



    taskbarContextMenu.querySelector('#ctxTaskbarCascade').addEventListener('click', () => {

      let offset = 20;

      [wlmMainWindow, wlmWindow, explorerWindow, sqlSuiteWindow, cmdWindow, notepadWindow].forEach(w => {

        if (!w.classList.contains('minimized') && w.style.display !== 'none') {

          w.style.left = `${offset}px`;

          w.style.top = `${offset}px`;

          bringToFront(w);

          offset += 25;

        }

      });

      taskbarContextMenu.style.display = 'none';

    });



    taskbarContextMenu.querySelector('#ctxTaskbarTileH').addEventListener('click', () => {

      const openWins = [wlmMainWindow, wlmWindow, explorerWindow, sqlSuiteWindow, cmdWindow, notepadWindow].filter(w => !w.classList.contains('minimized') && w.style.display !== 'none');

      if (openWins.length) {

        const h = Math.floor((desktop.clientHeight - 34) / openWins.length);

        openWins.forEach((w, idx) => {

          w.style.left = '0px';

          w.style.top = `${idx * h}px`;

          w.style.width = `${desktop.clientWidth}px`;

          w.style.height = `${h}px`;

          bringToFront(w);

        });

      }

      taskbarContextMenu.style.display = 'none';

    });



    taskbarContextMenu.querySelector('#ctxTaskbarTileV').addEventListener('click', () => {

      const openWins = [wlmMainWindow, wlmWindow, explorerWindow, sqlSuiteWindow, cmdWindow, notepadWindow].filter(w => !w.classList.contains('minimized') && w.style.display !== 'none');

      if (openWins.length) {

        const w = Math.floor(desktop.clientWidth / openWins.length);

        openWins.forEach((win, idx) => {

          win.style.left = `${idx * w}px`;

          win.style.top = '0px';

          win.style.width = `${w}px`;

          win.style.height = `${desktop.clientHeight - 34}px`;

          bringToFront(win);

        });

      }

      taskbarContextMenu.style.display = 'none';

    });



    document.addEventListener('keydown', (e) => {

      if (e.ctrlKey && e.shiftKey && (e.key === 'Escape' || e.code === 'Escape')) {

        e.preventDefault();

        if (window.XPApps && window.XPApps.openTaskManager) {

          window.XPApps.openTaskManager();

        }

      }

    });



    document.addEventListener('click', (e) => {

      if (!e.target.closest('.xp-context-menu')) {

        desktopContextMenu.style.display = 'none';

        trayContextMenu.style.display = 'none';

        taskbarContextMenu.style.display = 'none';

      }

    });

    document.addEventListener('keydown', (e) => {

      if (e.key === 'Escape') {

        desktopContextMenu.style.display = 'none';

        trayContextMenu.style.display = 'none';

      }

    });



    desktopContextMenu.querySelector('#ctxDesktopRefresh').addEventListener('click', () => {

      loadSessions();

      loadExplorerDirectory('');

      desktopContextMenu.style.display = 'none';

    });

    desktopContextMenu.querySelector('#ctxNewNotepad').addEventListener('click', () => {

      notepadWindow.classList.remove('minimized');

      notepadWindow.style.display = 'flex';

      bringToFront(notepadWindow, taskBtnNotepad);

      desktopContextMenu.style.display = 'none';

    });

    desktopContextMenu.querySelector('#ctxNewSession').addEventListener('click', () => {

      promptCreateSession();

      desktopContextMenu.style.display = 'none';

    });

    desktopContextMenu.querySelector('#ctxDesktopProps').addEventListener('click', () => {

      if (window.XPApps && window.XPApps.openDisplayProperties) {

        window.XPApps.openDisplayProperties();

      } else {

        sandboxWindow.classList.remove('minimized');

        sandboxWindow.style.display = 'flex';

        bringToFront(sandboxWindow, taskBtnSandbox);

      }

      desktopContextMenu.style.display = 'none';

    });



    // Create New Session Modal Helper

    // window.prompt() is not supported in the embedded session-review browser,
    // so session creation uses the real .modal-backdrop instead.

    function showSessionModalError(message) {

      if (newSessionError) {

        newSessionError.textContent = message;

        newSessionError.style.display = 'block';

      }

    }

    const promptCreateSession = () => {

      if (newSessionName) newSessionName.value = '';

      if (newSessionDesc) newSessionDesc.value = '';

      if (newSessionError) newSessionError.style.display = 'none';

      if (newSessionModal) openModal(newSessionModal);

    };

    window.IBAHost = window.IBAHost || {};

    window.IBAHost.openNewSessionModal = promptCreateSession;

    if (createSessionBtn) {

      createSessionBtn.addEventListener('click', async () => {

        const sessName = newSessionName.value.trim();

        if (!sessName) {

          showSessionModalError('Please enter a session name (e.g. compute_shader_refactor).');

          return;

        }

        const initialTask = newSessionDesc.value.trim();

        try {

          createSessionBtn.disabled = true;

          createSessionBtn.textContent = 'Creating...';

          if (newSessionError) newSessionError.style.display = 'none';

          const resp = await fetch('/api/iba/sessions/create', {

            method: 'POST',

            headers: { 'Content-Type': 'application/json' },

            body: JSON.stringify({ session_id: sessName, description: initialTask })

          });

          const data = await resp.json().catch(() => ({}));

          if (resp.ok) {

            closeModal(newSessionModal);

            loadSessions();

          } else {

            showSessionModalError('Failed to create session: ' + (data.detail || 'Unknown error.'));

          }

        } catch (err) {

          showSessionModalError('Network error creating session: ' + err.message);

        } finally {

          createSessionBtn.disabled = false;

          createSessionBtn.textContent = 'Create & Switch';

        }

      });

      const submitOnEnter = (e) => { if (e.key === 'Enter') { e.preventDefault(); createSessionBtn.click(); } };

      if (newSessionName) newSessionName.addEventListener('keydown', submitOnEnter);

      if (newSessionDesc) newSessionDesc.addEventListener('keydown', submitOnEnter);

    }

    wlmMainWindow.querySelector('#wlmMainNewSessionBtn').addEventListener('click', promptCreateSession);

    const wlmThemeBtn = wlmMainWindow.querySelector('#wlmMainThemeBtn');

    if (wlmThemeBtn) {

      wlmThemeBtn.addEventListener('click', () => {

        if (window.WLMClient && window.WLMClient.openCustomizationModal) {

          window.WLMClient.openCustomizationModal();

        }

      });

    }



    // Desktop Icon Audio & Selection & Open Handlers

    // =========================================================================
    // AUTHENTIC WINDOWS XP DESKTOP SUBSYSTEM: DRAGGING, AUTO-ARRANGE, CUSTOM FOLDERS
    // =========================================================================
    const CELL_W = 76;
    const CELL_H = 76;
    const MARGIN_X = 14;
    const MARGIN_Y = 12;

    let xpAlignToGrid = localStorage.getItem('xp_align_to_grid') !== 'false'; // default true
    let xpAutoArrange = localStorage.getItem('xp_auto_arrange') === 'true';   // default false
    let desktopIconPositions = {};
    try {
      desktopIconPositions = JSON.parse(localStorage.getItem('xp_desktop_positions') || '{}');
    } catch (e) {
      desktopIconPositions = {};
    }

    let desktopCustomFolders = [];
    try {
      desktopCustomFolders = JSON.parse(localStorage.getItem('xp_desktop_custom_folders') || '[]');
    } catch (e) {
      desktopCustomFolders = [];
    }

    // Default icon arrangement in columns (top-to-bottom, left-to-right) matching Windows XP (Image 2)
    const DEFAULT_ICON_ORDER = [
      'xpIconMyDocs',
      'xpIconComp',
      'xpIconRecycle',
      'xpIconWlm',
      'xpIconSql',
      'xpIconCmd',
      'xpIconSandbox',
      'xpIconPinball',
      'xpIconMinesweeper',
      'xpIconSolitaire',
      'xpIconNotepad',
      'xpIconPaint',
      'xpIconCalc',
      'xpIconDisplay',
      'xpIconTaskmgr',
      'xpIconSandboxDrive',
      'xpIconDorySettings',
      'xpIconRegedit'
    ];

    function saveIconPositions() {
      localStorage.setItem('xp_desktop_positions', JSON.stringify(desktopIconPositions));
    }

    function saveCustomFolders() {
      localStorage.setItem('xp_desktop_custom_folders', JSON.stringify(desktopCustomFolders));
    }

    function autoArrangeIcons(sortMode = 'default') {
      const allIcons = Array.from(iconsCol.querySelectorAll('.xp-desktop-icon'));
      if (sortMode === 'name') {
        allIcons.sort((a, b) => {
          const nameA = (a.querySelector('.xp-desktop-icon-label') || {}).textContent || '';
          const nameB = (b.querySelector('.xp-desktop-icon-label') || {}).textContent || '';
          return nameA.localeCompare(nameB);
        });
      } else {
        allIcons.sort((a, b) => {
          const idxA = DEFAULT_ICON_ORDER.indexOf(a.id);
          const idxB = DEFAULT_ICON_ORDER.indexOf(b.id);
          if (idxA !== -1 && idxB !== -1) return idxA - idxB;
          if (idxA !== -1) return -1;
          if (idxB !== -1) return 1;
          return 0;
        });
      }

      const deskHeight = desktop.clientHeight || 700;
      const maxRows = Math.max(1, Math.floor((deskHeight - MARGIN_Y - 40) / CELL_H));

      allIcons.forEach((el, index) => {
        const col = Math.floor(index / maxRows);
        const row = index % maxRows;
        const x = col * CELL_W + MARGIN_X;
        const y = row * CELL_H + MARGIN_Y;
        el.style.left = `${x}px`;
        el.style.top = `${y}px`;
        if (el.id) desktopIconPositions[el.id] = { left: x, top: y };
        if (el.dataset.folderId) {
          const f = desktopCustomFolders.find(cf => cf.id === el.dataset.folderId);
          if (f) { f.left = x; f.top = y; }
        }
      });
      saveIconPositions();
      saveCustomFolders();
    }

    function makeDesktopIconDraggable(el) {
      let isDragging = false;
      let startX = 0, startY = 0;
      let initLeft = 0, initTop = 0;
      let hasMoved = false;

      el.addEventListener('mousedown', (e) => {
        if (e.button !== 0) return;
        if (e.target.tagName === 'INPUT') return;

        isDragging = true;
        hasMoved = false;
        startX = e.clientX;
        startY = e.clientY;
        initLeft = parseInt(el.style.left, 10) || el.offsetLeft;
        initTop = parseInt(el.style.top, 10) || el.offsetTop;

        iconsCol.querySelectorAll('.xp-desktop-icon').forEach(i => i.classList.remove('selected'));
        el.classList.add('selected');
        el.style.zIndex = '100';

        function onMouseMove(ev) {
          if (!isDragging) return;
          const dx = ev.clientX - startX;
          const dy = ev.clientY - startY;
          if (!hasMoved && Math.hypot(dx, dy) > 4) {
            hasMoved = true;
            el.classList.add('dragging');
          }
          if (hasMoved) {
            el.style.left = `${initLeft + dx}px`;
            el.style.top = `${initTop + dy}px`;
          }
        }

        function onMouseUp(ev) {
          if (!isDragging) return;
          isDragging = false;
          window.removeEventListener('mousemove', onMouseMove);
          window.removeEventListener('mouseup', onMouseUp);
          el.classList.remove('dragging');
          el.style.zIndex = '10';

          if (hasMoved) {
            let finalLeft = parseInt(el.style.left, 10) || initLeft;
            let finalTop = parseInt(el.style.top, 10) || initTop;

            if (xpAlignToGrid) {
              const col = Math.max(0, Math.round((finalLeft - MARGIN_X) / CELL_W));
              const row = Math.max(0, Math.round((finalTop - MARGIN_Y) / CELL_H));
              finalLeft = col * CELL_W + MARGIN_X;
              finalTop = row * CELL_H + MARGIN_Y;
            }

            finalLeft = Math.max(MARGIN_X, Math.min(finalLeft, (desktop.clientWidth || 1000) - CELL_W));
            finalTop = Math.max(MARGIN_Y, Math.min(finalTop, (desktop.clientHeight || 700) - CELL_H - 40));

            el.style.left = `${finalLeft}px`;
            el.style.top = `${finalTop}px`;

            if (el.id) {
              desktopIconPositions[el.id] = { left: finalLeft, top: finalTop };
              saveIconPositions();
            } else if (el.dataset.folderId) {
              const f = desktopCustomFolders.find(cf => cf.id === el.dataset.folderId);
              if (f) {
                f.left = finalLeft;
                f.top = finalTop;
                saveCustomFolders();
              }
            }
          }
        }

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
      });
    }

    function positionAllDesktopIcons() {
      const deskHeight = desktop.clientHeight || 700;
      const maxRows = Math.max(1, Math.floor((deskHeight - MARGIN_Y - 40) / CELL_H));

      DEFAULT_ICON_ORDER.forEach((iconId, index) => {
        const el = document.getElementById(iconId);
        if (!el) return;
        makeDesktopIconDraggable(el);

        if (desktopIconPositions[iconId] && !xpAutoArrange) {
          el.style.left = `${desktopIconPositions[iconId].left}px`;
          el.style.top = `${desktopIconPositions[iconId].top}px`;
        } else {
          const col = Math.floor(index / maxRows);
          const row = index % maxRows;
          const x = col * CELL_W + MARGIN_X;
          const y = row * CELL_H + MARGIN_Y;
          el.style.left = `${x}px`;
          el.style.top = `${y}px`;
          desktopIconPositions[iconId] = { left: x, top: y };
        }
      });

      renderCustomFolders();
    }

    function renderCustomFolders() {
      iconsCol.querySelectorAll('.xp-custom-folder-icon').forEach(el => el.remove());

      const deskHeight = desktop.clientHeight || 700;
      const maxRows = Math.max(1, Math.floor((deskHeight - MARGIN_Y - 40) / CELL_H));

      desktopCustomFolders.forEach((folder, idx) => {
        const fEl = document.createElement('div');
        fEl.className = 'xp-desktop-icon xp-custom-folder-icon';
        fEl.dataset.folderId = folder.id;
        fEl.title = folder.name;

        const totalIndex = DEFAULT_ICON_ORDER.length + idx;
        if (folder.left !== undefined && folder.top !== undefined && !xpAutoArrange) {
          fEl.style.left = `${folder.left}px`;
          fEl.style.top = `${folder.top}px`;
        } else {
          const col = Math.floor(totalIndex / maxRows);
          const row = totalIndex % maxRows;
          const x = col * CELL_W + MARGIN_X;
          const y = row * CELL_H + MARGIN_Y;
          fEl.style.left = `${x}px`;
          fEl.style.top = `${y}px`;
          folder.left = x;
          folder.top = y;
        }

        fEl.innerHTML = `
          <img src="${folder.icon || '/static/xp_assets/icons/shell32_4.png'}" class="xp-desktop-img" alt="Folder">
          <span class="xp-desktop-icon-label">${escapeHtml(folder.name)}</span>
        `;

        fEl.addEventListener('click', (e) => {
          e.stopPropagation();
          iconsCol.querySelectorAll('.xp-desktop-icon').forEach(i => i.classList.remove('selected'));
          fEl.classList.add('selected');
          if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();
        });

        fEl.addEventListener('dblclick', (e) => {
          e.stopPropagation();
          if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();
          explorerWindow.classList.remove('minimized');
          explorerWindow.style.display = 'flex';
          bringToFront(explorerWindow, taskBtnExplorer);
          loadExplorerDirectory(folder.name);
        });

        fEl.addEventListener('contextmenu', (e) => {
          e.preventDefault();
          e.stopPropagation();
          showFolderContextMenu(e.clientX, e.clientY, folder, fEl);
        });

        makeDesktopIconDraggable(fEl);
        iconsCol.appendChild(fEl);
      });
      saveCustomFolders();
    }

    function createNewDesktopFolder() {
      const id = 'folder_' + Date.now();
      let name = 'New Folder';
      let counter = 2;
      while (desktopCustomFolders.some(f => f.name.toLowerCase() === name.toLowerCase())) {
        name = `New Folder (${counter++})`;
      }

      const newFolder = {
        id: id,
        name: name,
        icon: '/static/xp_assets/icons/shell32_4.png'
      };
      desktopCustomFolders.push(newFolder);
      renderCustomFolders();

      setTimeout(() => {
        const el = iconsCol.querySelector(`[data-folder-id="${id}"]`);
        if (el) renameDesktopFolder(newFolder, el);
      }, 50);
    }

    function renameDesktopFolder(folder, fEl) {
      const labelEl = fEl.querySelector('.xp-desktop-icon-label');
      if (!labelEl) return;

      const input = document.createElement('input');
      input.type = 'text';
      input.value = folder.name;
      input.style.width = '70px';
      input.style.fontSize = '11px';
      input.style.fontFamily = 'Tahoma';
      input.style.textAlign = 'center';
      input.style.border = '1px solid #0055ea';
      input.style.background = '#ffffff';
      input.style.color = '#000000';
      input.style.outline = 'none';

      labelEl.replaceWith(input);
      input.focus();
      input.select();

      function commit() {
        const newName = input.value.trim() || folder.name;
        folder.name = newName;
        saveCustomFolders();
        renderCustomFolders();
      }

      input.addEventListener('blur', commit);
      input.addEventListener('keydown', (ev) => {
        if (ev.key === 'Enter') {
          input.removeEventListener('blur', commit);
          commit();
        } else if (ev.key === 'Escape') {
          input.removeEventListener('blur', commit);
          renderCustomFolders();
        }
      });
    }

    function openChangeIconDialog(folder, fEl) {
      const existing = document.getElementById('xpChangeIconDialog');
      if (existing) existing.remove();

      const ICONS_CATALOG = [
        'shell32_4.png', 'shell32_5.png', 'shell32_22.png', 'shell32_45.png',
        'shell32_167.png', 'shell32_235.png', 'shell32_147.png', 'shell32_18.png',
        'shell32_23.png', 'shell32_24.png', 'shell32_241.png', 'shell32_242.png',
        'shell32_244.png', 'shell32_246.png', 'shell32_172.png', 'shell32_238.png',
        'explorer_100.png', 'notepad_2.png', 'calc_120.png', 'mspaint_1.png',
        'cmd.png', 'regedit_100.png', 'taskmgr.png', 'security.png',
        'recycle_empty.png', 'my_computer.png', 'network_drive.png', 'display.png'
      ];

      let selectedIcon = folder.icon || '/static/xp_assets/icons/shell32_4.png';

      const dlg = document.createElement('div');
      dlg.id = 'xpChangeIconDialog';
      dlg.className = 'xp-window xp-change-icon-dialog';
      dlg.style.left = 'calc(50% - 180px)';
      dlg.style.top = '120px';

      dlg.innerHTML = `
        <div class="title-bar" style="cursor:move;">
          <div class="title-bar-text">
            <span>Change Icon - ${escapeHtml(folder.name)}</span>
          </div>
          <div class="title-bar-controls">
            <button class="xp-caption-btn close" id="iconDlgCloseBtn" title="Close"></button>
          </div>
        </div>
        <div style="padding:10px 14px; background:#ece9d8; font-size:11px; font-family:Tahoma;">
          <div style="margin-bottom:8px; color:#222;">Select an icon from the list below:</div>
          <div class="xp-icon-grid-sunken" id="iconPickerGrid"></div>
          <div style="display:flex; justify-content:flex-end; gap:8px; margin-top:12px;">
            <button type="button" class="btn btn-primary btn-sm" id="iconDlgOkBtn" style="min-width:65px; height:23px; font-size:11px;">OK</button>
            <button type="button" class="btn btn-secondary btn-sm" id="iconDlgCancelBtn" style="min-width:65px; height:23px; font-size:11px;">Cancel</button>
          </div>
        </div>
      `;

      const grid = dlg.querySelector('#iconPickerGrid');
      ICONS_CATALOG.forEach(iconName => {
        const fullPath = `/static/xp_assets/icons/${iconName}`;
        const cell = document.createElement('div');
        cell.className = 'xp-icon-pick-cell' + (selectedIcon === fullPath ? ' selected' : '');
        cell.innerHTML = `<img src="${fullPath}" alt="icon">`;
        cell.onclick = () => {
          grid.querySelectorAll('.xp-icon-pick-cell').forEach(c => c.classList.remove('selected'));
          cell.classList.add('selected');
          selectedIcon = fullPath;
        };
        cell.ondblclick = () => {
          selectedIcon = fullPath;
          dlg.querySelector('#iconDlgOkBtn').click();
        };
        grid.appendChild(cell);
      });

      desktop.appendChild(dlg);

      dlg.querySelector('#iconDlgCloseBtn').onclick = () => dlg.remove();
      dlg.querySelector('#iconDlgCancelBtn').onclick = () => dlg.remove();
      dlg.querySelector('#iconDlgOkBtn').onclick = () => {
        folder.icon = selectedIcon;
        saveCustomFolders();
        renderCustomFolders();
        dlg.remove();
      };

      if (window.XPCore) window.XPCore.makeDraggable(dlg, dlg.querySelector('.title-bar'));
    }

    function showDesktopContextMenu(x, y) {
      // x/y arrive as viewport (clientX/clientY) coordinates. The menu is
      // positioned absolutely inside the desktop element, so translate them
      // first or the menu drifts right/down by the desktop's page offset.
      removeContextMenus();

      const deskRect = desktop.getBoundingClientRect();
      const localX = x - deskRect.left;
      const localY = y - deskRect.top;

      const menu = document.createElement('div');
      menu.id = 'xpDesktopContextMenu';
      menu.className = 'xp-desktop-ctx-menu';
      menu.style.left = `${localX}px`;
      menu.style.top = `${localY}px`;
      menu.style.visibility = 'hidden';

      menu.innerHTML = `
        <div class="xp-ctx-item has-submenu">
          <span>Arrange Icons By</span>
          <div class="xp-ctx-submenu">
            <div class="xp-ctx-item" id="ctxSortName"><span>Name</span></div>
            <div class="xp-ctx-item" id="ctxSortType"><span>Type</span></div>
            <div class="xp-ctx-sep"></div>
            <div class="xp-ctx-item" id="ctxToggleAutoArrange">
              <span class="xp-ctx-check">${xpAutoArrange ? '✓' : ''}</span>
              <span style="margin-left:14px;">Auto Arrange</span>
            </div>
            <div class="xp-ctx-item" id="ctxToggleAlignGrid">
              <span class="xp-ctx-check">${xpAlignToGrid ? '✓' : ''}</span>
              <span style="margin-left:14px;">Align to Grid</span>
            </div>
          </div>
        </div>
        <div class="xp-ctx-item" id="ctxRefreshDesktop"><span>Refresh</span></div>
        <div class="xp-ctx-sep"></div>
        <div class="xp-ctx-item has-submenu">
          <span>New</span>
          <div class="xp-ctx-submenu">
            <div class="xp-ctx-item" id="ctxNewFolder">
              <img src="/static/xp_assets/icons/shell32_4.png" style="width:14px; height:14px; margin-right:6px;">
              <span>Folder</span>
            </div>
            <div class="xp-ctx-item" id="ctxNewDoc">
              <img src="/static/xp_assets/icons/notepad_2.png" style="width:14px; height:14px; margin-right:6px;">
              <span>Text Document</span>
            </div>
            <div class="xp-ctx-item" id="ctxNewIbaSession">
              <img src="/static/xp_assets/icons/shell32_24.png" style="width:14px; height:14px; margin-right:6px;">
              <span>IBA Session</span>
            </div>
          </div>
        </div>
        <div class="xp-ctx-sep"></div>
        <div class="xp-ctx-item" id="ctxProperties"><span>Properties</span></div>
      `;

      desktop.appendChild(menu);

      // Clamp against the desktop bounds now that the real menu size is known.
      const mw = menu.offsetWidth || 180;
      const mh = menu.offsetHeight || 200;
      menu.style.left = `${Math.max(0, Math.min(localX, desktop.clientWidth - mw))}px`;
      menu.style.top = `${Math.max(0, Math.min(localY, desktop.clientHeight - mh - 34))}px`;
      menu.style.visibility = 'visible';

      menu.querySelector('#ctxNewIbaSession').onclick = () => {
        removeContextMenus();
        promptCreateSession();
      };

      menu.querySelector('#ctxSortName').onclick = () => { autoArrangeIcons('name'); removeContextMenus(); };
      menu.querySelector('#ctxSortType').onclick = () => { autoArrangeIcons('type'); removeContextMenus(); };
      menu.querySelector('#ctxToggleAutoArrange').onclick = () => {
        xpAutoArrange = !xpAutoArrange;
        localStorage.setItem('xp_auto_arrange', xpAutoArrange ? 'true' : 'false');
        if (xpAutoArrange) autoArrangeIcons();
        removeContextMenus();
      };
      menu.querySelector('#ctxToggleAlignGrid').onclick = () => {
        xpAlignToGrid = !xpAlignToGrid;
        localStorage.setItem('xp_align_to_grid', xpAlignToGrid ? 'true' : 'false');
        removeContextMenus();
      };
      menu.querySelector('#ctxRefreshDesktop').onclick = () => {
        positionAllDesktopIcons();
        removeContextMenus();
      };
      menu.querySelector('#ctxNewFolder').onclick = () => {
        createNewDesktopFolder();
        removeContextMenus();
      };
      menu.querySelector('#ctxNewDoc').onclick = () => {
        const docWin = document.getElementById('unifiedNotepadWindow');
        if (docWin) {
          docWin.classList.remove('minimized');
          docWin.style.display = 'flex';
          bringToFront(docWin, taskBtnNotepad);
        }
        removeContextMenus();
      };
      menu.querySelector('#ctxProperties').onclick = () => {
        if (window.XPApps && window.XPApps.openDisplayProperties) {
          window.XPApps.openDisplayProperties();
        }
        removeContextMenus();
      };
    }

    function showFolderContextMenu(x, y, folder, fEl) {
      // Same viewport -> desktop-local translation as the desktop menu.
      removeContextMenus();

      const deskRect = desktop.getBoundingClientRect();
      const localX = x - deskRect.left;
      const localY = y - deskRect.top;

      const menu = document.createElement('div');
      menu.id = 'xpFolderContextMenu';
      menu.className = 'xp-desktop-ctx-menu';
      menu.style.left = `${localX}px`;
      menu.style.top = `${localY}px`;
      menu.style.visibility = 'hidden';

      menu.innerHTML = `
        <div class="xp-ctx-item" id="fctxOpen"><strong>Open</strong></div>
        <div class="xp-ctx-sep"></div>
        <div class="xp-ctx-item" id="fctxRename"><span>Rename</span></div>
        <div class="xp-ctx-item" id="fctxChangeIcon"><span>Change Icon...</span></div>
        <div class="xp-ctx-sep"></div>
        <div class="xp-ctx-item" id="fctxDelete"><span>Delete</span></div>
      `;

      desktop.appendChild(menu);

      const fmw = menu.offsetWidth || 160;
      const fmh = menu.offsetHeight || 120;
      menu.style.left = `${Math.max(0, Math.min(localX, desktop.clientWidth - fmw))}px`;
      menu.style.top = `${Math.max(0, Math.min(localY, desktop.clientHeight - fmh - 34))}px`;
      menu.style.visibility = 'visible';

      menu.querySelector('#fctxOpen').onclick = () => {
        explorerWindow.classList.remove('minimized');
        explorerWindow.style.display = 'flex';
        bringToFront(explorerWindow, taskBtnExplorer);
        loadExplorerDirectory(folder.name);
        removeContextMenus();
      };
      menu.querySelector('#fctxRename').onclick = () => {
        renameDesktopFolder(folder, fEl);
        removeContextMenus();
      };
      menu.querySelector('#fctxChangeIcon').onclick = () => {
        openChangeIconDialog(folder, fEl);
        removeContextMenus();
      };
      menu.querySelector('#fctxDelete').onclick = () => {
        desktopCustomFolders = desktopCustomFolders.filter(cf => cf.id !== folder.id);
        saveCustomFolders();
        renderCustomFolders();
        removeContextMenus();
      };
    }

    function removeContextMenus() {
      const ctx = document.getElementById('xpDesktopContextMenu');
      if (ctx) ctx.remove();
      const fctx = document.getElementById('xpFolderContextMenu');
      if (fctx) fctx.remove();
    }

    desktop.addEventListener('click', () => {
      removeContextMenus();
      iconsCol.querySelectorAll('.xp-desktop-icon').forEach(i => i.classList.remove('selected'));
    });

    desktop.addEventListener('contextmenu', (e) => {
      if (e.target === desktop || e.target === iconsCol || e.target.classList.contains('xp-wallpaper') || e.target.id === 'unifiedXpDesktop' || e.target.closest('.xp-desktop-icon')) {
        e.preventDefault();
        showDesktopContextMenu(e.clientX, e.clientY);
      }
    });

    function wireDesktopAppIcon(iconSelector, onOpen) {
      const el = iconsCol.querySelector(iconSelector);
      if (!el) return;
      makeDesktopIconDraggable(el);

      el.addEventListener('click', (e) => {
        e.stopPropagation();
        removeContextMenus();
        iconsCol.querySelectorAll('.xp-desktop-icon').forEach(i => i.classList.remove('selected'));
        el.classList.add('selected');
        if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();
      });

      el.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();
        if (onOpen) onOpen();
      });
    }

    setTimeout(positionAllDesktopIcons, 80);



    wireDesktopAppIcon('#xpIconSetupCd', () => {
      if (window.XPApps && window.XPApps.openAutorun) window.XPApps.openAutorun();
    });
    wireDesktopAppIcon('#xpIconSearch', () => {
      if (window.XPApps && window.XPApps.openSearchCompanion) window.XPApps.openSearchCompanion();
    });
    wireDesktopAppIcon('#xpIconWlm', () => {

      wlmMainWindow.classList.remove('minimized');

      wlmMainWindow.style.display = 'flex';

      bringToFront(wlmMainWindow, taskBtnWlmContacts);

    });

    wireDesktopAppIcon('#xpIconComp', () => {

      explorerWindow.classList.remove('minimized');

      explorerWindow.style.display = 'flex';

      bringToFront(explorerWindow, taskBtnExplorer);

      loadExplorerDirectory('');

    });

    wireDesktopAppIcon('#xpIconSql', () => {

      sqlSuiteWindow.classList.remove('minimized');

      sqlSuiteWindow.style.display = 'flex';

      bringToFront(sqlSuiteWindow, taskBtnSql);

      loadSqlGraph('all');

    });

    wireDesktopAppIcon('#xpIconCmd', () => {

      cmdWindow.classList.remove('minimized');

      cmdWindow.style.display = 'flex';

      bringToFront(cmdWindow, taskBtnCmd);

    });

    wireDesktopAppIcon('#xpIconSandbox', () => {

      sandboxWindow.classList.remove('minimized');

      sandboxWindow.style.display = 'flex';

      bringToFront(sandboxWindow, taskBtnSandbox);

      loadSandboxStatus();

    });

    wireDesktopAppIcon('#xpIconDisplay', () => {

      if (window.XPApps && window.XPApps.openDisplayProperties) {

        window.XPApps.openDisplayProperties();

      }

    });

    wireDesktopAppIcon('#xpIconTaskmgr', () => {

      if (window.XPApps && window.XPApps.openTaskManager) {

        window.XPApps.openTaskManager();

      }

    });

    wireDesktopAppIcon('#xpIconSandboxDrive', () => {

      explorerWindow.classList.remove('minimized');

      explorerWindow.style.display = 'flex';

      bringToFront(explorerWindow, taskBtnExplorer);

      loadExplorerDirectory('Z:');

    });

    wireDesktopAppIcon('#xpIconSolitaire', () => {

      if (window.XPGames && window.XPGames.openSolitaire) {

        window.XPGames.openSolitaire();

      }

    });

    wireDesktopAppIcon('#xpIconPinball', () => {

      if (window.XPGames && window.XPGames.openPinball) {

        window.XPGames.openPinball();

      }

    });

    wireDesktopAppIcon('#xpIconMinesweeper', () => {

      if (window.XPGames && window.XPGames.openMinesweeper) {

        window.XPGames.openMinesweeper();

      }

    });

    wireDesktopAppIcon('#xpIconNotepad', () => {

      notepadWindow.classList.remove('minimized');

      notepadWindow.style.display = 'flex';

      bringToFront(notepadWindow, taskBtnNotepad);

    });

    wireDesktopAppIcon('#xpIconPaint', () => {

      if (window.XPApps && window.XPApps.openPaint) {

        window.XPApps.openPaint();

      }

    });

    wireDesktopAppIcon('#xpIconCalc', () => {

      if (window.XPApps && window.XPApps.openCalculator) {

        window.XPApps.openCalculator();

      }

    });

    wireDesktopAppIcon('#xpIconMyDocs', () => {

      explorerWindow.classList.remove('minimized');

      explorerWindow.style.display = 'flex';

      bringToFront(explorerWindow, taskBtnExplorer);

      loadExplorerDirectory('C:/Documents and Settings/Rackneh/My Documents');

    });



    const xpRecycleEl = iconsCol.querySelector('#xpIconRecycle');

    if (xpRecycleEl) {

      xpRecycleEl.addEventListener('click', (e) => {

        e.stopPropagation();

        iconsCol.querySelectorAll('.xp-desktop-icon').forEach(i => i.classList.remove('selected'));

        xpRecycleEl.classList.add('selected');

        if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();

      });

      xpRecycleEl.addEventListener('dblclick', (e) => {

        e.stopPropagation();

        if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playRecycle();

        alert('Recycle Bin (Empty)\nLocation: C:\\RECYCLER\\\n0 items found. Storage is optimized.');

      });

    }



    // Start Menu Toggle & Actions

    const startBtn = taskbar.querySelector('#unifiedStartBtn');

    startBtn.addEventListener('click', (e) => {

      e.stopPropagation();

      startMenu.classList.toggle('open');

    });

    desktop.addEventListener('click', () => {

      startMenu.classList.remove('open');

    });



    startMenu.querySelector('#smWlmContacts').addEventListener('click', () => {

      wlmMainWindow.classList.remove('minimized');

      wlmMainWindow.style.display = 'flex';

      bringToFront(wlmMainWindow, taskBtnWlmContacts);

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smWlm').addEventListener('click', () => {

      wlmWindow.classList.remove('minimized');

      wlmWindow.style.display = 'flex';

      bringToFront(wlmWindow, taskBtnWlm);

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smSql').addEventListener('click', () => {

      sqlSuiteWindow.classList.remove('minimized');

      sqlSuiteWindow.style.display = 'flex';

      bringToFront(sqlSuiteWindow, taskBtnSql);

      loadSqlGraph('all');

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smExplorer').addEventListener('click', () => {

      explorerWindow.classList.remove('minimized');

      explorerWindow.style.display = 'flex';

      bringToFront(explorerWindow, taskBtnExplorer);

      loadExplorerDirectory('');

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smMyComp').addEventListener('click', () => {

      explorerWindow.classList.remove('minimized');

      explorerWindow.style.display = 'flex';

      bringToFront(explorerWindow, taskBtnExplorer);

      loadExplorerDirectory('');

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smCmd').addEventListener('click', () => {

      cmdWindow.classList.remove('minimized');

      cmdWindow.style.display = 'flex';

      bringToFront(cmdWindow, taskBtnCmd);

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smSandbox').addEventListener('click', () => {

      sandboxWindow.classList.remove('minimized');

      sandboxWindow.style.display = 'flex';

      bringToFront(sandboxWindow, taskBtnSandbox);

      loadSandboxStatus();

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smNotepad').addEventListener('click', () => {

      notepadWindow.classList.remove('minimized');

      notepadWindow.style.display = 'flex';

      bringToFront(notepadWindow, taskBtnNotepad);

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smMyDocs').addEventListener('click', () => {

      explorerWindow.classList.remove('minimized');

      explorerWindow.style.display = 'flex';

      bringToFront(explorerWindow, taskBtnExplorer);

      loadExplorerDirectory('C:/Documents and Settings/Rackneh/My Documents');

      startMenu.classList.remove('open');

    });

    startMenu.querySelector('#smControl').addEventListener('click', () => {

      if (window.XPApps && window.XPApps.openControlPanel) {

        window.XPApps.openControlPanel();

      } else if (window.XPApps && window.XPApps.openDisplayProperties) {

        window.XPApps.openDisplayProperties();

      }

      startMenu.classList.remove('open');

    });



    // All Programs Cascading Flyout Wiring

    const allProgBtn = startMenu.querySelector('#smAllProgramsBtn');

    const progFlyout = startMenu.querySelector('#smProgramsFlyout');

    if (allProgBtn && progFlyout) {

      allProgBtn.addEventListener('mouseenter', () => {

        // Inline styles lose to the !important override block in style.css,
        // so visibility is driven through the .open class instead.

        progFlyout.classList.add('open');

      });

      allProgBtn.addEventListener('click', (e) => {

        e.stopPropagation();

        progFlyout.classList.toggle('open');

      });

      startMenu.addEventListener('mouseleave', () => {

        progFlyout.classList.remove('open');

      });



      // Flyout app launchers

      const closeMenu = () => {

        startMenu.classList.remove('open');

        progFlyout.classList.remove('open');

      };



      const piCalc = startMenu.querySelector('#piCalc');

      if (piCalc) piCalc.addEventListener('click', () => { closeMenu(); if (window.XPApps) window.XPApps.openCalculator(); });



      const piPaint = startMenu.querySelector('#piPaint');

      if (piPaint) piPaint.addEventListener('click', () => { closeMenu(); if (window.XPApps) window.XPApps.openPaint(); });



      const piNotepad = startMenu.querySelector('#piNotepad');

      if (piNotepad) piNotepad.addEventListener('click', () => {

        closeMenu();

        notepadWindow.classList.remove('minimized');

        notepadWindow.style.display = 'flex';

        bringToFront(notepadWindow, taskBtnNotepad);

      });



      const piCmd = startMenu.querySelector('#piCmd');

      if (piCmd) piCmd.addEventListener('click', () => {

        closeMenu();

        cmdWindow.classList.remove('minimized');

        cmdWindow.style.display = 'flex';

        bringToFront(cmdWindow, taskBtnCmd);

      });



      const piSounds = startMenu.querySelector('#piSounds');

      if (piSounds) piSounds.addEventListener('click', () => { closeMenu(); if (window.XPApps) window.XPApps.openSoundsAndAudio(); });



      const piPinball = startMenu.querySelector('#piPinball');

      if (piPinball) piPinball.addEventListener('click', () => { closeMenu(); if (window.XPGames) window.XPGames.openPinball(); });



      const piSolitaire = startMenu.querySelector('#piSolitaire');

      if (piSolitaire) piSolitaire.addEventListener('click', () => { closeMenu(); if (window.XPGames) window.XPGames.openSolitaire(); });



      const piMinesweeper = startMenu.querySelector('#piMinesweeper');

      if (piMinesweeper) piMinesweeper.addEventListener('click', () => { closeMenu(); if (window.XPGames) window.XPGames.openMinesweeper(); });



      const piWlm = startMenu.querySelector('#piWlm');

      if (piWlm) piWlm.addEventListener('click', () => {

        closeMenu();

        wlmMainWindow.classList.remove('minimized');

        wlmMainWindow.style.display = 'flex';

        bringToFront(wlmMainWindow, taskBtnWlmContacts);

      });



      const piSql = startMenu.querySelector('#piSql');

      if (piSql) piSql.addEventListener('click', () => {

        closeMenu();

        sqlSuiteWindow.classList.remove('minimized');

        sqlSuiteWindow.style.display = 'flex';

        bringToFront(sqlSuiteWindow, taskBtnSql);

        loadSqlGraph('all');

      });



      const piExplorer = startMenu.querySelector('#piExplorer');

      if (piExplorer) piExplorer.addEventListener('click', () => {

        closeMenu();

        explorerWindow.classList.remove('minimized');

        explorerWindow.style.display = 'flex';

        bringToFront(explorerWindow, taskBtnExplorer);

        loadExplorerDirectory('');

      });



      const piControl = startMenu.querySelector('#piControl');

      if (piControl) piControl.addEventListener('click', () => { closeMenu(); if (window.XPApps) window.XPApps.openControlPanel(); });

    }

    startMenu.querySelector('#smHelp').addEventListener('click', () => {

      startMenu.classList.remove('open');

      alert('Windows XP Help and Support Center:\n\n• Windows Live Messenger: Manage sessions & personas.\n• My Computer: Browse C: (System), D: (Project Workspace), and Z: (Sandbox VM).\n• SQL Authority Studio 3D: Inspect multi-database cognitive memory and relational links.\n• Personalization: Right-click desktop -> Properties to change themes, wallpapers, and screensavers.');

    });

    const smSearch = startMenu.querySelector('#smSearch');
    if (smSearch) {
      smSearch.addEventListener('click', () => {
        startMenu.style.display = 'none';
        if (window.XPApps && window.XPApps.openSearchCompanion) window.XPApps.openSearchCompanion();
      });
    }

    const piAutorun = startMenu.querySelector('#piAutorun');
    if (piAutorun) {
      piAutorun.addEventListener('click', () => {
        startMenu.style.display = 'none';
        if (window.XPApps && window.XPApps.openAutorun) window.XPApps.openAutorun();
      });
    }

    const piSetupWiz = startMenu.querySelector('#piSetupWiz');
    if (piSetupWiz) {
      piSetupWiz.addEventListener('click', () => {
        startMenu.style.display = 'none';
        if (window.XPApps && window.XPApps.openSetupWizard) window.XPApps.openSetupWizard();
      });
    }

    const smRunEl = startMenu.querySelector('#smRun');

    if (smRunEl) {

      smRunEl.addEventListener('click', () => {

        if (window.XPApps && window.XPApps.openRunDialog) {

          window.XPApps.openRunDialog();

        }

        startMenu.classList.remove('open');

      });

    }

    startMenu.querySelector('#smLogOff').addEventListener('click', () => {

      startMenu.classList.remove('open');

      if (window.XPCore && window.XPCore.triggerBootSequence) {

        window.XPCore.triggerBootSequence();

      }

    });

    startMenu.querySelector('#smTurnOff').addEventListener('click', () => {

      startMenu.classList.remove('open');

      if (window.XPCore && window.XPCore.showShutdownDialog) {

        window.XPCore.showShutdownDialog();

      }

    });



    // Live Digital Clock in System Tray

    const trayClock = taskbar.querySelector('#unifiedTrayClock');

    const updateClock = () => {

      const d = new Date();

      trayClock.textContent = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    };

    setInterval(updateClock, 1000);

    updateClock();



    // Robot Head Click -> 3D Hologram Window Over Head

    const deskHead = workstation.querySelector('#unifiedRobotHeadNode');

    deskHead.addEventListener('click', (e) => {

      e.stopPropagation();

      const isVisible = headHoloWindow.style.display === 'flex';

      headHoloWindow.style.display = isVisible ? 'none' : 'flex';

      if (!isVisible) {

        startHeadHoloAnimation();

      }

    });

    headHoloWindow.querySelector('#closeHoloBtn').addEventListener('click', () => {

      headHoloWindow.style.display = 'none';

    });



    // WLM Nudge Button Action

    const wlmNudgeBtn = wlmWindow.querySelector('#wlmTbNudge');

    wlmNudgeBtn.addEventListener('click', () => {

      playWlmSound('nudge.mp3');

      wlmWindow.classList.remove('wlm-nudge-active');

      void wlmWindow.offsetWidth;

      wlmWindow.classList.add('wlm-nudge-active');

      setTimeout(() => wlmWindow.classList.remove('wlm-nudge-active'), 500);



      const note = document.createElement('div');

      note.className = 'wlm-chat-nudge-note';

      note.textContent = 'You just sent a Nudge!';

      wlmChatHistory.appendChild(note);

      wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;

    });



    // WLM Emoticon Picker

    const wlmEmoticonBtn = wlmWindow.querySelector('#wlmTbEmoticons');

    const wlmEmoticonPicker = wlmWindow.querySelector('#wlmEmoticonPicker');

    wlmEmoticonBtn.addEventListener('click', (e) => {

      e.stopPropagation();

      wlmEmoticonPicker.classList.toggle('open');

    });

    wlmEmoticonPicker.querySelectorAll('.wlm-emoticon-item').forEach(item => {

      item.addEventListener('click', () => {

        const code = item.dataset.code;

        const textarea = wlmWindow.querySelector('#wlmChatTextarea');

        textarea.value += (textarea.value ? ' ' : '') + code + ' ';

        textarea.focus();

        wlmEmoticonPicker.classList.remove('open');

      });

    });

    desktop.addEventListener('click', () => {

      wlmEmoticonPicker.classList.remove('open');

    });



    // WLM Image Attachment Logic

    let attachedImages = [];

    const wlmAttachmentBar = wlmWindow.querySelector('#wlmAttachmentBar');

    const wlmImageUploadInput = wlmWindow.querySelector('#wlmImageUploadInput');



    const renderAttachments = () => {

      if (!wlmAttachmentBar) return;

      if (!attachedImages.length) {

        wlmAttachmentBar.style.display = 'none';

        wlmAttachmentBar.innerHTML = '';

        return;

      }

      wlmAttachmentBar.style.display = 'flex';

      wlmAttachmentBar.innerHTML = '';

      attachedImages.forEach((att, idx) => {

        const chip = document.createElement('div');

        chip.className = 'wlm-attachment-chip';

        chip.innerHTML = `

          <img src="${att.dataUri}" alt="Thumbnail">

          <span>${escapeHtml(att.name || 'Image')}</span>

          <span class="remove-att-btn" title="Remove">&times;</span>

        `;

        chip.querySelector('.remove-att-btn').addEventListener('click', (e) => {

          e.stopPropagation();

          attachedImages.splice(idx, 1);

          renderAttachments();

        });

        wlmAttachmentBar.appendChild(chip);

      });

    };



    const addImageAttachment = (dataUri, name) => {

      attachedImages.push({ dataUri, name: name || `image_${Date.now()}.png` });

      renderAttachments();

    };



    const clearAttachedImages = () => {

      attachedImages = [];

      renderAttachments();

    };



    wlmWindow.querySelector('#wlmTbPhotos').addEventListener('click', () => {

      wlmImageUploadInput.click();

    });

    const tbAttach = wlmWindow.querySelector('#wlmToolbarAttach');

    if (tbAttach) {

      tbAttach.addEventListener('click', () => {

        wlmImageUploadInput.click();

      });

    }



    wlmImageUploadInput.addEventListener('change', (e) => {

      Array.from(e.target.files).forEach(file => {

        const reader = new FileReader();

        reader.onload = (ev) => {

          addImageAttachment(ev.target.result, file.name);

        };

        reader.readAsDataURL(file);

      });

      wlmImageUploadInput.value = '';

    });



    // Support Ctrl+V paste of images directly into chat textarea

    const wlmChatTextarea = wlmWindow.querySelector('#wlmChatTextarea');

    wlmChatTextarea.addEventListener('paste', (e) => {

      const items = (e.clipboardData || e.originalEvent.clipboardData).items;

      for (const item of items) {

        if (item.type.indexOf('image') !== -1) {

          const file = item.getAsFile();

          const reader = new FileReader();

          reader.onload = (ev) => {

            addImageAttachment(ev.target.result, file.name || 'Pasted_Image.png');

          };

          reader.readAsDataURL(file);

        }

      }

    });



    // Toggle contact list button in chat toolbar

    wlmWindow.querySelector('#wlmTbSwitchView').addEventListener('click', () => {

      toggleWindow(wlmMainWindow, taskBtnWlmContacts);

    });



    // WLM Chat Send

    const doSendMessage = () => {

      const msg = wlmChatTextarea.value.trim();

      const imgs = attachedImages.map(a => a.dataUri);

      if (!msg && !imgs.length) return;

      wlmChatTextarea.value = '';

      clearAttachedImages();

      sendChatMessage(msg, imgs);

    };



    const wlmChatSendBtn = wlmWindow.querySelector('#wlmChatSendBtn');

    wlmChatSendBtn.addEventListener('click', doSendMessage);

    wlmChatTextarea.addEventListener('keydown', (e) => {

      if (e.key === 'Enter' && !e.shiftKey) {

        e.preventDefault();

        doSendMessage();

      }

    });



    // WLM Contact List & Procedural Persona Rendering

    let allSessionsData = [];

    let currentActiveSessionId = 'default';



    // User Profile Personalization (Pic 1 Match: THE GOAT, puppy DP, crimson banner)

    let userProfile = {

      name: 'THE GOAT',

      email: 't1e@example.com (Available)',

      avatar: '/static/wlm/wlm/usertiles/usertile_dog.png',

      banner: '/static/wlm/wlm/background_red.jpg',

      status: 'Available'

    };



    try {

      const savedProf = localStorage.getItem('wlm_user_profile');

      if (savedProf) {

        userProfile = Object.assign(userProfile, JSON.parse(savedProf));

      }

    } catch (e) {}



    const applyUserProfile = () => {

      // Update Main Window Header

      const mainHeader = wlmMainWindow.querySelector('#wlmMainHeaderHandle');

      const mainDp = wlmMainWindow.querySelector('#wlmCurrentUserDp');

      const mainName = wlmMainWindow.querySelector('.wlm-main-user-name');

      const mainPm = wlmMainWindow.querySelector('#wlmUserPmInput');

      const bottomChatDp = wlmWindow.querySelector('#wlmBottomUserDpImg');



      if (mainHeader && userProfile.banner) {

        mainHeader.style.backgroundImage = `url('${userProfile.banner}')`;

        mainHeader.style.backgroundSize = 'cover';

        mainHeader.style.backgroundPosition = 'center';

      }

      if (mainDp && userProfile.avatar) mainDp.src = userProfile.avatar;

      if (bottomChatDp && userProfile.avatar) bottomChatDp.src = userProfile.avatar;

      if (mainName) {

        mainName.innerHTML = `${escapeHtml(userProfile.name)} <span class="wlm-status-tag" id="wlmCurrentStatusTag">(${escapeHtml(userProfile.status)}) ▾</span>`;

      }

      if (mainPm) mainPm.value = userProfile.email;

    };



    applyUserProfile();



    // -----------------------------------------------------------------------

    // Winks Playback & Video Overlay System (Pic 1 Match)

    // -----------------------------------------------------------------------

    const wlmWinkOverlay = wlmWindow.querySelector('#wlmWinkOverlay');

    const wlmWinksPopup = wlmWindow.querySelector('#wlmWinksPopup');

    const wlmTbWinks = wlmWindow.querySelector('#wlmTbWinks');

    const wlmCloseWinksBtn = wlmWindow.querySelector('#wlmCloseWinksBtn');



    const playWink = (winkId, senderName = 'You') => {

      if (!wlmWinkOverlay) return;

      const winkExt = winkId === 'fartguy' ? 'webm' : 'mp4';

      const videoUrl = `/static/wlm/wlm/winks/videos/wink_${winkId}.${winkExt}`;



      wlmWinkOverlay.innerHTML = `

        <video src="${videoUrl}" autoplay playsinline style="max-width:85%; max-height:85%; filter:drop-shadow(0 12px 25px rgba(0,0,0,0.6)); pointer-events:none;"></video>

      `;

      wlmWinkOverlay.style.display = 'flex';

      playWlmSound('wink.mp3');



      const vid = wlmWinkOverlay.querySelector('video');

      if (vid) {

        vid.onended = () => {

          wlmWinkOverlay.style.display = 'none';

          wlmWinkOverlay.innerHTML = '';

        };

        setTimeout(() => {

          wlmWinkOverlay.style.display = 'none';

          wlmWinkOverlay.innerHTML = '';

        }, 8000);

      }



      // Append system message to chat history (Pic 1 match)

      const wlmChatHistory = document.getElementById('wlmChatHistory');

      if (wlmChatHistory) {

        const row = document.createElement('div');

        row.className = 'wlm-sys-msg-row';

        row.textContent = `${senderName} sent a wink.`;

        wlmChatHistory.appendChild(row);

        wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;

      }

    };



    if (wlmTbWinks) {

      wlmTbWinks.addEventListener('click', (e) => {

        e.stopPropagation();

        if (wlmWinksPopup) wlmWinksPopup.classList.toggle('open');

      });

    }



    if (wlmCloseWinksBtn) {

      wlmCloseWinksBtn.addEventListener('click', () => {

        if (wlmWinksPopup) wlmWinksPopup.classList.remove('open');

      });

    }



    wlmWindow.querySelectorAll('.wlm-wink-grid-item').forEach(item => {

      item.addEventListener('click', () => {

        const wink = item.dataset.wink;

        if (wink) {

          playWink(wink, userProfile.name || 'You');

          if (wlmWinksPopup) wlmWinksPopup.classList.remove('open');

        }

      });

    });



    document.addEventListener('click', (e) => {

      if (wlmWinksPopup && !e.target.closest('#wlmWinksPopup') && !e.target.closest('#wlmTbWinks')) {

        wlmWinksPopup.classList.remove('open');

      }

    });



    // -----------------------------------------------------------------------

    // User Personalization Options Window

    // -----------------------------------------------------------------------

    const customWin = document.createElement('div');

    customWin.className = 'xp-window wlm-customization-window';

    customWin.id = 'unifiedWlmCustomWindow';
    customWin.style.display = 'none'; // Hidden by default

    customWin.innerHTML = `

      <div class="title-bar" id="customWinHandle">

        <div class="title-bar-text">

          <img src="/static/wlm/Icons/msn.ico" class="xp-title-icon" alt="Options">

          <span>Personalization & Options - Windows Live Messenger</span>

        </div>

        <div class="title-bar-controls">

          <button class="xp-caption-btn close" id="customWinCloseBtn" title="Close"></button>

        </div>

      </div>

      <div class="wlm-custom-tabs">

        <button class="wlm-custom-tab-btn active" data-tab="profileTab">Profile</button>

        <button class="wlm-custom-tab-btn" data-tab="dpTab">Display Picture</button>

        <button class="wlm-custom-tab-btn" data-tab="sceneTab">Scene Banner</button>

      </div>

      <div class="wlm-custom-tab-body">

        <div id="profileTab" class="custom-tab-panel">

          <div style="margin-bottom:12px;">

            <label style="font-weight:700; display:block; margin-bottom:4px;">Display Name:</label>

            <input type="text" id="custNameInput" class="form-control" style="width:100%; padding:5px 8px; border:1px solid #716f64; font-size:12px;" value="${escapeHtml(userProfile.name)}">

          </div>

          <div style="margin-bottom:12px;">

            <label style="font-weight:700; display:block; margin-bottom:4px;">Personal Message / Email:</label>

            <input type="text" id="custEmailInput" class="form-control" style="width:100%; padding:5px 8px; border:1px solid #716f64; font-size:12px;" value="${escapeHtml(userProfile.email)}">

          </div>

          <div style="margin-bottom:12px;">

            <label style="font-weight:700; display:block; margin-bottom:4px;">Status:</label>

            <select id="custStatusSelect" style="width:100%; padding:4px; border:1px solid #716f64;">

              <option value="Available">Available (Online)</option>

              <option value="Busy">Busy</option>

              <option value="Away">Away</option>

              <option value="Appear Offline">Appear Offline</option>

            </select>

          </div>

        </div>



        <div id="dpTab" class="custom-tab-panel" style="display:none;">

          <div style="font-weight:700; margin-bottom:8px;">Choose a Display Picture:</div>

          <div class="wlm-custom-grid" id="custDpGrid"></div>

        </div>



        <div id="sceneTab" class="custom-tab-panel" style="display:none;">

          <div style="font-weight:700; margin-bottom:8px;">Choose a Scene Banner:</div>

          <div class="wlm-custom-grid" id="custSceneGrid"></div>

        </div>

      </div>

      <div style="padding:8px 12px; background:#ece9d8; border-top:1px solid #d4d0c8; display:flex; justify-content:flex-end; gap:8px;">

        <button class="btn btn-secondary btn-sm" id="custCancelBtn">Cancel</button>

        <button class="btn btn-primary btn-sm" id="custSaveBtn" style="background:#0284c7; color:#fff; font-weight:700;">OK</button>

      </div>

    `;



    desktop.appendChild(customWin);

    makeDraggable(customWin, customWin.querySelector('#customWinHandle'));

    makeResizable(customWin, 320, 360);



    const availableDps = [

      '/static/wlm/wlm/usertiles/usertile_dog.png',

      '/static/wlm/wlm/usertiles/usertile_kitten.png',

      '/static/wlm/wlm/usertiles/usertile_soccer.gif',

      '/static/wlm/wlm/usertiles/usertile_flare.png',

      '/static/wlm/wlm/usertiles/usertile_robot.png',

      '/static/wlm/wlm/usertiles/usertile_chess.png',

      '/static/wlm/wlm/usertiles/usertile_bonsai.png',

      '/static/wlm/wlm/usertiles/usertile_fish.png',

      '/static/wlm/wlm/usertiles/usertile_chef.png',

      '/static/wlm/wlm/usertiles/usertile_rocket.png',

      '/static/wlm/wlm/usertiles/usertile_electric_guitar.png',

      '/static/wlm/wlm/usertiles/usertile_bike.png'

    ];



    const availableScenes = [

      { name: 'Soccer Field', url: '/static/wlm/wlm/scenes/0001.png' },

      { name: 'Purple Skulls', url: '/static/wlm/wlm/scenes/zune_04.jpg' },

      { name: 'Crimson Flame', url: '/static/wlm/wlm/background_red.jpg' },

      { name: 'Mountains', url: '/static/wlm/wlm/scenes/0002.jpg' },

      { name: 'Green Meadow', url: '/static/wlm/wlm/scenes/0004.png' },

      { name: 'Future Blue', url: '/static/wlm/wlm/scenes/0007.png' },

      { name: 'Robot Graffiti', url: '/static/wlm/wlm/scenes/Robot.jpg' },

      { name: 'Street Art', url: '/static/wlm/wlm/scenes/Graffiti.jpg' },

      { name: 'Carbon Fiber', url: '/static/wlm/wlm/scenes/CarbonFiber.jpg' },

      { name: 'Butterflies', url: '/static/wlm/wlm/scenes/ButterflyPattern.png' },

      { name: 'Luna Blue', url: '/static/wlm/wlm/background_blue.jpg' },

      { name: 'Luna Teal', url: '/static/wlm/wlm/background_teal.jpg' }

    ];



    const dpGrid = customWin.querySelector('#custDpGrid');

    availableDps.forEach(dp => {

      const item = document.createElement('div');

      item.className = `wlm-custom-grid-item ${userProfile.avatar === dp ? 'selected' : ''}`;

      item.innerHTML = `<img src="${dp}">`;

      item.addEventListener('click', () => {

        dpGrid.querySelectorAll('.wlm-custom-grid-item').forEach(i => i.classList.remove('selected'));

        item.classList.add('selected');

        userProfile.avatar = dp;

      });

      dpGrid.appendChild(item);

    });



    const sceneGrid = customWin.querySelector('#custSceneGrid');

    availableScenes.forEach(sc => {

      const item = document.createElement('div');

      item.className = `wlm-custom-grid-item ${userProfile.banner === sc.url ? 'selected' : ''}`;

      item.innerHTML = `<img src="${sc.url}" style="height:36px; width:70px;"><span style="font-size:9.5px; margin-top:2px;">${escapeHtml(sc.name)}</span>`;

      item.addEventListener('click', () => {

        sceneGrid.querySelectorAll('.wlm-custom-grid-item').forEach(i => i.classList.remove('selected'));

        item.classList.add('selected');

        userProfile.banner = sc.url;

      });

      sceneGrid.appendChild(item);

    });



    // Tab switching in customization window

    customWin.querySelectorAll('.wlm-custom-tab-btn').forEach(btn => {

      btn.addEventListener('click', () => {

        customWin.querySelectorAll('.wlm-custom-tab-btn').forEach(b => b.classList.remove('active'));

        customWin.querySelectorAll('.custom-tab-panel').forEach(p => p.style.display = 'none');

        btn.classList.add('active');

        const tabEl = customWin.querySelector(`#${btn.dataset.tab}`);

        if (tabEl) tabEl.style.display = 'block';

      });

    });



    const openCustomWindow = () => {

      customWin.classList.add('open');

      customWin.style.display = 'flex';

      bringToFront(customWin);

    };



    const closeCustomWindow = () => {

      customWin.classList.remove('open');

      customWin.style.display = 'none';

    };



    customWin.querySelector('#customWinCloseBtn').addEventListener('click', closeCustomWindow);

    customWin.querySelector('#custCancelBtn').addEventListener('click', closeCustomWindow);

    customWin.querySelector('#custSaveBtn').addEventListener('click', () => {

      userProfile.name = customWin.querySelector('#custNameInput').value.trim() || 'THE GOAT';

      userProfile.email = customWin.querySelector('#custEmailInput').value.trim() || 't1e@example.com (Available)';

      userProfile.status = customWin.querySelector('#custStatusSelect').value;

      try {

        localStorage.setItem('wlm_user_profile', JSON.stringify(userProfile));

      } catch (e) {}

      applyUserProfile();

      closeCustomWindow();

    });



    wlmMainWindow.querySelector('#wlmMainThemeBtn').addEventListener('click', openCustomWindow);

    wlmMainWindow.querySelector('#wlmCurrentUserDp').addEventListener('click', openCustomWindow);

    const bottomChatDp = wlmWindow.querySelector('#wlmChatUserTileLeft');

    if (bottomChatDp) bottomChatDp.addEventListener('click', openCustomWindow);



    // -----------------------------------------------------------------------

    // Interactive Host Permission & Question System (Chat GUI Cards)

    // -----------------------------------------------------------------------

    const renderHostPermissionCard = (perm) => {

      if (window.WLMClient && window.WLMClient.renderHostPermissionCard) {

        window.WLMClient.renderHostPermissionCard(perm);

      }

    };



    const renderQuestionCard = (q) => {

      if (window.WLMClient && window.WLMClient.renderQuestionCard) {

        window.WLMClient.renderQuestionCard(q);

      }

    };



    const renderWlmContactList = (sessions, activeSessionId) => {

      if (window.WLMClient && window.WLMClient.renderContactList) {

        window.WLMClient.renderContactList(sessions, activeSessionId);

      }

    };



    // -----------------------------------------------------------------------

    // Wave-4 Chat Window Header Personalization & Scene Banners (Pic 1 Match)

    // -----------------------------------------------------------------------

    const switchWlmChatToPersona = async (sessionId, persona) => {

      currentActiveSessionId = sessionId;

      if (sessionSelect) sessionSelect.value = sessionId;

      if (chipSession) chipSession.textContent = '💬 ' + sessionId;

      if (window.WLMClient && window.WLMClient.switchChatToPersona) {

        await window.WLMClient.switchChatToPersona(sessionId, persona);

      }

    };



    // WLM Step Flow Bar Logic

    const wlmStepFlowBar = wlmWindow.querySelector('#wlmStepFlowBar');

    const wlmStepJumpBtn = wlmWindow.querySelector('#wlmStepJumpBtn');

    const stepsMap = new Map();

    let currentActiveStepNum = null;

    let isUserScrolled = false;



    const addStepToWlm = (stepNumber, stepTitle) => {

      if (stepsMap.has(stepNumber)) {

        const existing = stepsMap.get(stepNumber);

        const labelElem = existing.elem.querySelector('.xp-step-label');

        if (labelElem) labelElem.innerHTML = `<span class="status-pulse-dot"></span> Step ${stepNumber}: ${escapeHtml(stepTitle)}`;

        return;

      }

      const stepElem = document.createElement('div');

      stepElem.className = 'xp-flow-step';

      stepElem.id = `wlmStep_${stepNumber}`;

      stepElem.innerHTML = `

        <span class="xp-step-label"><span class="status-pulse-dot"></span> Step ${stepNumber}: ${escapeHtml(stepTitle)}</span>

        <span class="iba-step-pill">Loop</span>

      `;

      stepElem.addEventListener('click', () => {

        setStepActive(stepNumber, false);

      });

      wlmStepFlowBar.appendChild(stepElem);

      stepsMap.set(stepNumber, { elem: stepElem, title: stepTitle });



      if (!isUserScrolled) {

        setStepActive(stepNumber, false);

      } else {

        wlmStepJumpBtn.classList.add('visible');

      }

    };



    const setStepActive = (stepNumber, smooth = true) => {

      currentActiveStepNum = stepNumber;

      wlmStepFlowBar.querySelectorAll('.xp-flow-step').forEach(s => s.classList.remove('is-active'));

      const activeObj = stepsMap.get(stepNumber);

      if (activeObj) {

        activeObj.elem.classList.add('is-active');

        const targetTop = activeObj.elem.offsetTop - (wlmStepFlowBar.clientHeight / 2) + (activeObj.elem.clientHeight / 2);

        wlmStepFlowBar.scrollTo({ top: Math.max(0, targetTop), behavior: smooth ? 'smooth' : 'auto' });

        isUserScrolled = false;

        wlmStepJumpBtn.classList.remove('visible');

      }

    };



    wlmStepFlowBar.addEventListener('scroll', () => {

      if (!currentActiveStepNum || !stepsMap.has(currentActiveStepNum)) return;

      const activeObj = stepsMap.get(currentActiveStepNum);

      const targetTop = activeObj.elem.offsetTop - (wlmStepFlowBar.clientHeight / 2) + (activeObj.elem.clientHeight / 2);

      if (Math.abs(wlmStepFlowBar.scrollTop - targetTop) > 30) {

        isUserScrolled = true;

        wlmStepJumpBtn.classList.add('visible');

      } else {

        isUserScrolled = false;

        wlmStepJumpBtn.classList.remove('visible');

      }

    });



    wlmStepJumpBtn.addEventListener('click', () => {

      if (currentActiveStepNum) setStepActive(currentActiveStepNum, true);

    });



    // SQL Database Tabs & Modes Wiring

    sqlSuiteWindow.querySelectorAll('.xp-sql-db-btn').forEach(b => {

      b.addEventListener('click', () => {

        sqlSuiteWindow.querySelectorAll('.xp-sql-db-btn').forEach(btn => btn.classList.remove('active'));

        b.classList.add('active');

        loadSqlGraph(b.dataset.db);

      });

    });



    const modeBtnGreater = sqlSuiteWindow.querySelector('#modeBtnGreater');

    const modeBtnEntries = sqlSuiteWindow.querySelector('#modeBtnEntries');

    modeBtnGreater.addEventListener('click', () => {

      modeBtnGreater.classList.add('active');

      modeBtnEntries.classList.remove('active');

      sqlActiveMode = 'greater';

      loadSqlGraph(sqlActiveDatabase);

    });

    modeBtnEntries.addEventListener('click', () => {

      modeBtnEntries.classList.add('active');

      modeBtnGreater.classList.remove('active');

      sqlActiveMode = 'entries';

      loadSqlGraph(sqlActiveDatabase);

    });



    sqlSuiteWindow.querySelector('#crumbHome').addEventListener('click', () => {

      modeBtnGreater.click();

    });



    sqlSuiteWindow.querySelector('#sqlResetCamBtn').addEventListener('click', () => {

      sqlCamYaw = 0.3;

      sqlCamPitch = 0.2;

      sqlCamZoom = 1.0;

    });



    const autoRotBtn = sqlSuiteWindow.querySelector('#sqlAutoRotateBtn');

    autoRotBtn.addEventListener('click', () => {

      sqlAutoRotate = !sqlAutoRotate;

      autoRotBtn.textContent = `🔄 Auto-Rotate: ${sqlAutoRotate ? 'ON' : 'OFF'}`;

    });



    // Explorer Toolbar & Menubar Wiring

    explorerWindow.querySelector('#expBackBtn').addEventListener('click', () => {

      if (window.explorerHistory && window.explorerHistoryIdx > 0) {

        window.explorerHistoryIdx--;

        loadExplorerDirectory(window.explorerHistory[window.explorerHistoryIdx], false);

      }

    });

    explorerWindow.querySelector('#expFwdBtn').addEventListener('click', () => {

      if (window.explorerHistory && window.explorerHistoryIdx < window.explorerHistory.length - 1) {

        window.explorerHistoryIdx++;

        loadExplorerDirectory(window.explorerHistory[window.explorerHistoryIdx], false);

      }

    });

    explorerWindow.querySelector('#expUpBtn').addEventListener('click', () => {

      if (!currentExplorerSubpath) return;

      const parts = currentExplorerSubpath.replace(/\\/g, '/').split('/').filter(Boolean);

      parts.pop();

      loadExplorerDirectory(parts.join('/'));

    });

    explorerWindow.querySelector('#expGoBtn').addEventListener('click', () => {

      const val = explorerWindow.querySelector('#expAddressInput').value.trim();

      loadExplorerDirectory(val);

    });

    explorerWindow.querySelector('#expSearchBtn').addEventListener('click', () => {

      const addr = explorerWindow.querySelector('#expAddressInput');

      if (addr) addr.focus();

    });

    explorerWindow.querySelector('#expFoldersBtn').addEventListener('click', () => {

      const pane = explorerWindow.querySelector('.xp-explorer-task-pane');

      if (pane) pane.style.display = (pane.style.display === 'none') ? 'flex' : 'none';

    });



    explorerWindow.querySelector('#expRefreshTask').addEventListener('click', () => {

      loadExplorerDirectory(currentExplorerSubpath);

    });

    explorerWindow.querySelector('#expNewDocTask').addEventListener('click', () => {

      openFileInNotepad('');

    });

    explorerWindow.querySelector('#expRootPlace').addEventListener('click', () => {

      loadExplorerDirectory('');

    });

    explorerWindow.querySelector('#expAssetsPlace').addEventListener('click', () => {

      loadExplorerDirectory('Assets');

    });

    explorerWindow.querySelector('#expRuntimePlace').addEventListener('click', () => {

      loadExplorerDirectory('iba_runtime');

    });



    // Explorer Menubar Handlers

    const expActClose = explorerWindow.querySelector('#expActClose');

    if (expActClose) expActClose.addEventListener('click', () => { explorerWindow.style.display = 'none'; });

    const expActRefresh = explorerWindow.querySelector('#expActRefresh');

    if (expActRefresh) expActRefresh.addEventListener('click', () => { loadExplorerDirectory(currentExplorerSubpath); });

    const expActMapDrive = explorerWindow.querySelector('#expActMapDrive');

    if (expActMapDrive) expActMapDrive.addEventListener('click', () => { loadExplorerDirectory('Z:/'); });

    const expActNewDoc = explorerWindow.querySelector('#expActNewDoc');

    if (expActNewDoc) expActNewDoc.addEventListener('click', () => { openFileInNotepad(''); });



    // View Modes Switching

    const viewModes = ['Thumbnails', 'Tiles', 'Icons', 'List', 'Details'];

    viewModes.forEach(vm => {

      const el = explorerWindow.querySelector(`#expView${vm}`);

      if (el) {

        el.addEventListener('click', () => {

          window.currentExplorerViewMode = vm.toLowerCase();

          const grid = document.getElementById('expFileGrid');

          if (grid) {

            grid.className = 'xp-explorer-file-view ' + window.currentExplorerViewMode;

          }

          loadExplorerDirectory(currentExplorerSubpath);

        });

      }

    });



    // Desktop Context Menu Item Wiring

    desktopContextMenu.querySelector('#ctxDesktopProps').addEventListener('click', () => {

      desktopContextMenu.style.display = 'none';

      if (window.XPApps && window.XPApps.openDisplayProperties) {

        window.XPApps.openDisplayProperties();

      }

    });

    desktopContextMenu.querySelector('#ctxDesktopRefresh').addEventListener('click', () => {

      desktopContextMenu.style.display = 'none';

      if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();

      loadExplorerDirectory(currentExplorerSubpath);

    });

    desktopContextMenu.querySelector('#ctxNewNotepad').addEventListener('click', () => {

      desktopContextMenu.style.display = 'none';

      openFileInNotepad('');

    });



    // Sandbox Refresh Button

    sandboxWindow.querySelector('#refreshAuditBtn').addEventListener('click', loadSandboxStatus);



    // Mount 3D Beige Oldschool PC Tower into Sidebar

    const sidebarBeigePcCont = document.getElementById('sidebarBeigePcContainer');

    if (sidebarBeigePcCont && window.XPCore && window.XPCore.BeigePC) {

      window.XPCore.BeigePC.render(sidebarBeigePcCont);

    }



    return {

      desktop,

      workstation,

      thoughtContainer,

      headHoloWindow,

      wlmMainWindow,

      wlmWindow,

      explorerWindow,

      sqlSuiteWindow,

      sandboxWindow,

      cmdWindow,

      notepadWindow,

      renderWlmContactList,

      switchWlmChatToPersona,

      addStepToWlm,

      setStepActive

    };

  }



  // -------------------------------------------------------------------------

  // 3D Hologram Canvas over Robot Head

  // -------------------------------------------------------------------------

  let holoAnimId = null;

  function startHeadHoloAnimation() {

    const canvas = document.getElementById('holoCanvas');

    if (!canvas) return;

    const ctx = canvas.getContext('2d');

    let angle = 0;



    if (holoAnimId) cancelAnimationFrame(holoAnimId);



    const render = () => {

      ctx.clearRect(0, 0, canvas.width, canvas.height);

      const cx = canvas.width / 2;

      const cy = canvas.height / 2;



      // Draw glowing orbit grid

      ctx.strokeStyle = 'rgba(0, 229, 255, 0.2)';

      ctx.lineWidth = 1;

      ctx.beginPath();

      ctx.ellipse(cx, cy, 90, 35, 0, 0, Math.PI * 2);

      ctx.stroke();



      angle += 0.03;

      const nodes = [

        { label: 'control', color: '#38bdf8', a: angle },

        { label: 'codemap', color: '#4ade80', a: angle + Math.PI * 0.66 },

        { label: 'memory', color: '#c084fc', a: angle + Math.PI * 1.33 }

      ];



      nodes.forEach(n => {

        const x = cx + Math.cos(n.a) * 85;

        const y = cy + Math.sin(n.a) * 32;

        const scale = (Math.sin(n.a) + 2) / 3;



        ctx.fillStyle = n.color;

        ctx.shadowColor = n.color;

        ctx.shadowBlur = 8 * scale;

        ctx.beginPath();

        ctx.arc(x, y, 5 * scale, 0, Math.PI * 2);

        ctx.fill();



        ctx.shadowBlur = 0;

        ctx.fillStyle = '#ffffff';

        ctx.font = `${Math.floor(9 * scale)}px monospace`;

        ctx.fillText(n.label, x - 15, y - 8);

      });



      // Center Core

      ctx.fillStyle = '#76b900';

      ctx.shadowColor = '#76b900';

      ctx.shadowBlur = 10;

      ctx.beginPath();

      ctx.arc(cx, cy, 7, 0, Math.PI * 2);

      ctx.fill();

      ctx.shadowBlur = 0;



      const holoWin = document.getElementById('unifiedHeadHoloWindow');

      if (holoWin && holoWin.style.display !== 'none') {

        holoAnimId = requestAnimationFrame(render);

      }

    };

    render();

  }



  // -------------------------------------------------------------------------

  // File Explorer ("My Computer") Implementation with Fallback

  // -------------------------------------------------------------------------

  let currentExplorerSubpath = '';



  async function loadExplorerDirectory(subpath = '', recordHistory = true) {

    window.loadExplorerDirectory = loadExplorerDirectory;

    window.openFileInNotepad = openFileInNotepad;

    currentExplorerSubpath = subpath;



    if (!window.explorerHistory) window.explorerHistory = [];

    if (window.explorerHistoryIdx === undefined) window.explorerHistoryIdx = -1;

    if (!window.currentExplorerViewMode) window.currentExplorerViewMode = 'tiles';



    if (recordHistory && window.explorerHistory[window.explorerHistoryIdx] !== subpath) {

      window.explorerHistory = window.explorerHistory.slice(0, window.explorerHistoryIdx + 1);

      window.explorerHistory.push(subpath);

      window.explorerHistoryIdx = window.explorerHistory.length - 1;

    }



    const grid = document.getElementById('expFileGrid');

    const addr = document.getElementById('expAddressInput');

    const statusBar = document.getElementById('expStatusBar');

    const titleText = document.getElementById('explorerTitleText');

    if (!grid) return;



    grid.className = 'xp-explorer-file-view ' + window.currentExplorerViewMode;

    grid.innerHTML = '<div style="padding:16px; color:#64748b; font-size:11px;">Loading Explorer items...</div>';



    let data = null;

    const activeWs = workspaceSelect ? workspaceSelect.value : '';



    try {

      const params = new URLSearchParams({ subpath, workspace: activeWs });

      const resp = await fetch(`/api/iba/fs/browse?${params}`);

      if (resp.ok) {

        data = await resp.json();

      }

    } catch (e) {}



    if (!data) {

      grid.innerHTML = '<div style="padding:16px; color:#dc2626; font-size:11px;">Could not access drive path.</div>';

      return;

    }



    const currPath = data.current_path || subpath || 'My Computer';

    if (addr) addr.value = currPath;

    if (titleText) titleText.innerHTML = `<img src="/static/xp_assets/icons/shell32_4.png" class="xp-title-icon"> <span>${escapeHtml(currPath)} - Windows Explorer</span>`;



    grid.innerHTML = '';



    // 1. Root Drives View (My Computer)

    if (data.mode === 'drives' || currPath === 'My Computer') {

      const drivesWrap = document.createElement('div');

      drivesWrap.className = 'xp-drives-container';

      drivesWrap.style.width = '100%';



      // Group 1: Hard Disk Drives

      const hddSec = document.createElement('div');

      hddSec.innerHTML = '<div class="xp-drive-category-title">Hard Disk Drives</div><div class="xp-drive-grid" id="hddDriveGrid"></div>';

      drivesWrap.appendChild(hddSec);

      const hddGrid = hddSec.querySelector('#hddDriveGrid');



      // Group 2: Network Drives

      const netSec = document.createElement('div');

      netSec.innerHTML = '<div class="xp-drive-category-title">Network Drives</div><div class="xp-drive-grid" id="netDriveGrid"></div>';

      drivesWrap.appendChild(netSec);

      const netGrid = netSec.querySelector('#netDriveGrid');



      // Group 3: Removable Storage

      const remSec = document.createElement('div');

      remSec.innerHTML = '<div class="xp-drive-category-title">Devices with Removable Storage</div><div class="xp-drive-grid" id="remDriveGrid"></div>';

      drivesWrap.appendChild(remSec);

      const remGrid = remSec.querySelector('#remDriveGrid');



      (data.items || []).forEach(drv => {

        const card = document.createElement('div');

        card.className = 'xp-drive-card';

        let iconHtml = '<img src="/static/xp_assets/icons/shell32_8.png" width="32" height="32" alt="HDD">';

        let targetGrid = hddGrid;



        if (drv.drive_type === 'network') {

          iconHtml = '<img src="/static/xp_assets/icons/shell32_9.png" width="32" height="32" alt="Network Drive">';

          targetGrid = netGrid;

        } else if (drv.drive_type === 'floppy') {

          iconHtml = '<img src="/static/xp_assets/icons/shell32_6.png" width="32" height="32" alt="Floppy">';

          targetGrid = remGrid;

        } else if (drv.drive_type === 'cdrom') {

          iconHtml = '<img src="/static/xp_assets/icons/shell32_11.png" width="32" height="32" alt="CD-ROM">';

          targetGrid = remGrid;

        } else if (drv.drive_type === 'project') {

          iconHtml = '<img src="/static/xp_assets/icons/shell32_4.png" width="32" height="32" alt="Folder">';

          targetGrid = hddGrid;

        }



        card.innerHTML = `

          <div class="xp-drive-icon" style="font-size:24px;">${iconHtml}</div>

          <div class="xp-drive-info">

            <div class="xp-drive-name">${escapeHtml(drv.name)}</div>

            <div class="xp-drive-subtext">${escapeHtml(drv.free || '')}</div>

          </div>

        `;



        card.addEventListener('dblclick', () => {

          if (drv.path) loadExplorerDirectory(drv.path);

        });



        card.addEventListener('click', () => {

          drivesWrap.querySelectorAll('.xp-drive-card').forEach(c => c.classList.remove('selected'));

          card.classList.add('selected');

          const details = document.getElementById('expDetailsPane');

          if (details) {

            details.innerHTML = `

              <div style="font-weight:700; color:#1e293b;">${escapeHtml(drv.name)}</div>

              <div style="color:#64748b; font-size:10px; margin-top:3px;">Type: ${drv.drive_type.toUpperCase()}</div>

              <div style="color:#64748b; font-size:10px;">${escapeHtml(drv.free || '')}</div>

            `;

          }

        });



        if (targetGrid) targetGrid.appendChild(card);

      });



      grid.appendChild(drivesWrap);

      if (statusBar) statusBar.textContent = `${(data.items || []).length} drive(s) mounted (System, Project, Sandbox VM)`;

      return;

    }



    // 2. Normal Directory Browse (C:, D:, Z:)

    if (window.currentExplorerViewMode === 'details') {

      const hdr = document.createElement('div');

      hdr.className = 'xp-details-header';

      hdr.innerHTML = '<div>Name</div><div>Size</div><div>Type</div><div>Date Modified</div>';

      grid.appendChild(hdr);

    }



    // Up Row / Parent

    if (data.parent_path !== null && data.parent_path !== undefined) {

      const upItem = document.createElement('div');

      upItem.className = 'xp-file-item';

      if (window.currentExplorerViewMode === 'details') {

        upItem.innerHTML = '<div><img src="/static/xp_assets/icons/shell32_4.png" width="16" height="16"> .. (Up)</div><div></div><div>Parent Folder</div><div></div>';

      } else {

        upItem.innerHTML = `

          <div class="xp-file-icon"><img src="/static/xp_assets/icons/shell32_4.png" width="16" height="16"></div>

          <div class="xp-file-name">.. (Up)</div>

        `;

      }

      upItem.addEventListener('dblclick', () => {

        loadExplorerDirectory(data.parent_path);

      });

      grid.appendChild(upItem);

    }



    (data.items || []).forEach(f => {

      const item = document.createElement('div');

      item.className = 'xp-file-item';

      let icon = f.is_dir 

        ? '<img src="/static/xp_assets/icons/shell32_4.png" width="16" height="16" alt="Folder">' 

        : '<img src="/static/xp_assets/icons/shell32_1.png" width="16" height="16" alt="File">';



      if (!f.is_dir) {

        if (f.ext === '.exe') icon = '<img src="/static/xp_assets/icons/shell32_2.png" width="16" height="16" alt="App">';

        else if (f.ext === '.dll' || f.ext === '.sys') icon = '<img src="/static/xp_assets/icons/shell32_154.png" width="16" height="16" alt="DLL">';

        else if (f.ext === '.wav' || f.ext === '.mp3') icon = '<img src="/static/xp_assets/icons/mmsys_100.png" width="16" height="16" alt="Audio">';

        else if (f.ext === '.bmp' || f.ext === '.png' || f.ext === '.jpg') icon = '<img src="/static/xp_assets/icons/mspaint_1.png" width="16" height="16" alt="Image">';

        else if (f.ext === '.sqlite' || f.ext === '.db') icon = '<img src="/static/xp_assets/icons/icon_103.png" width="16" height="16" alt="Database">';

        else if (f.ext === '.txt' || f.ext === '.log' || f.ext === '.md' || f.ext === '.ini') icon = '<img src="/static/xp_assets/icons/notepad_2.png" width="16" height="16" alt="Doc">';

      }



      const szStr = f.is_dir ? '' : `${(f.size / 1024).toFixed(1)} KB`;

      const typeStr = f.is_dir ? 'File Folder' : (f.ext ? `${f.ext.toUpperCase().replace('.', '')} File` : 'File');

      const modStr = f.modified || '11/04/2004 12:00 PM';



      if (window.currentExplorerViewMode === 'details') {

        item.innerHTML = `

          <div><span style="margin-right:6px;">${icon}</span>${escapeHtml(f.name)}</div>

          <div>${szStr}</div>

          <div>${typeStr}</div>

          <div>${modStr}</div>

        `;

      } else {

        item.innerHTML = `

          <div class="xp-file-icon">${icon}</div>

          <div class="xp-file-name" title="${escapeHtml(f.name)}">${escapeHtml(f.name)}</div>

        `;

      }



      item.addEventListener('dblclick', () => {

        if (f.is_dir) {

          loadExplorerDirectory(f.path);

        } else if (f.name && (f.name.toLowerCase() === 'setup.exe' || f.name.toLowerCase() === 'autorun.inf')) {

          if (window.XPApps && window.XPApps.openAutorun) window.XPApps.openAutorun();

        } else if (f.name && f.name.toLowerCase() === 'pinball.exe') {

          if (window.XPGames && window.XPGames.openPinball) window.XPGames.openPinball();

        } else if (f.name && f.name.toLowerCase() === 'winmine.exe') {

          if (window.XPGames && window.XPGames.openMinesweeper) window.XPGames.openMinesweeper();

        } else {

          openFileInNotepad(f.path);

        }

      });



      item.addEventListener('click', () => {

        grid.querySelectorAll('.xp-file-item').forEach(i => i.classList.remove('selected'));

        item.classList.add('selected');

        const details = document.getElementById('expDetailsPane');

        if (details) {

          details.innerHTML = `

            <div style="font-weight:700; color:#1e293b;">${escapeHtml(f.name)}</div>

            <div style="color:#64748b; font-size:10px; margin-top:2px;">Size: ${szStr || 'Folder'}</div>

            <div style="color:#64748b; font-size:10px;">Path: ${escapeHtml(f.path)}</div>

          `;

        }

      });



      grid.appendChild(item);

    });



    if (statusBar) {

      const count = (data.items || []).length;

      const modeLabel = data.mode === 'virtual_c' ? 'Windows XP C:' : (data.mode === 'virtual_z' ? 'Sandbox VM Z:' : 'Project Volume D:');

      statusBar.textContent = `${count} object(s) in ${modeLabel}`;

    }

  }



  async function openFileInNotepad(relPath) {

    const notepadWindow = document.getElementById('unifiedNotepadWindow');

    const notepadTitle = document.getElementById('notepadTitleText');

    const notepadBody = document.getElementById('unifiedNotepadBody');

    const taskBtn = document.getElementById('taskBtnNotepad');



    if (notepadTitle) notepadTitle.innerHTML = `<img src="/static/xp_assets/icons/notepad_2.png" class="xp-title-icon"> <span>${escapeHtml(relPath)} - Notepad</span>`;

    if (notepadBody) notepadBody.value = 'Loading file contents...';



    if (notepadWindow) {

      notepadWindow.classList.remove('minimized');

      notepadWindow.style.display = 'flex';

      notepadWindow.style.zIndex = 45;

    }



    // Executable simulation

    if (relPath.endsWith('.exe')) {

      if (notepadBody) {

        notepadBody.value = `[Process Execution Simulated]\nImage: ${relPath}\nStatus: RUNNING in background subsystem.\nSubsystem: Win32 GUI / Console`;

      }

      return;

    }



    try {

      const activeWs = workspaceSelect ? workspaceSelect.value : '';

      const params = new URLSearchParams({ path: relPath, workspace: activeWs });

      const resp = await fetch(`/api/iba/fs/read?${params}`);

      if (resp.ok) {

        const data = await resp.json();

        if (notepadBody) notepadBody.value = data.content || '';

      } else {

        if (notepadBody) notepadBody.value = `Error loading ${relPath}: Binary or restricted file.`;

      }

    } catch (e) {

      if (notepadBody) notepadBody.value = `Error: ${e}`;

    }

  }



  async function loadSqlGraph(database = 'all', concept = null) {

    if (window.SQLAuthority && window.SQLAuthority.loadSqlGraph) {

      return window.SQLAuthority.loadSqlGraph(database, concept);

    }

    sqlActiveDatabase = database;

    const crumbActive = document.getElementById('crumbActive');

    const statusBar = document.getElementById('sqlStatusBar');



    if (crumbActive) {

      crumbActive.textContent = concept ? `Concept: ${concept}` : (database === 'all' ? 'Federated Cognitive Mesh' : database);

    }



    let data = null;

    try {

      const activeWs = workspaceSelect ? workspaceSelect.value : '';

      const params = new URLSearchParams({ database, workspace: activeWs });

      if (concept) params.append('concept', concept);



      const resp = await fetch(`/api/iba/sql/graph?${params}`);

      if (resp.ok) {

        data = await resp.json();

      }

    } catch (err) {}



    // Fallback Graph: 4 Greater Concepts with cross-database relational connections

    if (!data || !data.nodes) {

      data = {

        database: database,

        nodes: [

          { id: 'concept_tasks', label: 'Tasks & Goal Intent', type: 'concept', database: 'control.sqlite', table: 'tasks', x: -1.2, y: -0.8, z: 0.2, color: '#38bdf8', summary: 'Active goal intent, execution states & persistent operational constraints', related_entries: [

            { label: 'Inspect SQLV3RelationalTable.cs', target_id: 'receipt_read_file', target_db: 'control.sqlite', relation: 'Execution Receipt' },

            { label: 'SQLV3RelationalTable.cs', target_id: 'script_sql_relational', target_db: 'codemap.sqlite', relation: 'Code Mental Model' }

          ]},

          { id: 'concept_receipts', label: 'Execution Receipts', type: 'concept', database: 'control.sqlite', table: 'tool_receipts', x: 1.2, y: -0.7, z: -0.3, color: '#fb923c', summary: 'Cryptographic SHA-256 hashes of tool executions and verified results', related_entries: [

            { label: 'Tasks & Goal Intent', target_id: 'concept_tasks', target_db: 'control.sqlite', relation: 'Parent Goal' },

            { label: 'Self-Improvement Proposals', target_id: 'concept_proposals', target_db: 'higher_memory.sqlite', relation: 'Reflected Optimization' }

          ]},

          { id: 'concept_codemap', label: 'Codebase Cognition Map', type: 'concept', database: 'codemap.sqlite', table: 'script_knowledge', x: -0.9, y: 1.0, z: -0.5, color: '#4ade80', summary: 'Incremental semantic models of project scripts with code update tracking', related_entries: [

            { label: 'Assets/SQLV3RelationalTable.cs', target_id: 'script_sql_relational', target_db: 'codemap.sqlite', relation: 'Script AST Node' },

            { label: 'Inspect SQLV3RelationalTable.cs', target_id: 'receipt_read_file', target_db: 'control.sqlite', relation: 'Verified Action' }

          ]},

          { id: 'concept_proposals', label: 'Self-Improvement Proposals', type: 'concept', database: 'higher_memory.sqlite', table: 'improvement_proposals', x: 1.0, y: 0.9, z: 0.6, color: '#c084fc', summary: 'Autonomous memory notes for system efficiency & workflow optimizations', related_entries: [

            { label: 'Batch SQL Pre-allocation Proposal', target_id: 'proposal_batch_sql', target_db: 'higher_memory.sqlite', relation: 'Memory Proposal' },

            { label: 'Tasks & Goal Intent', target_id: 'concept_tasks', target_db: 'control.sqlite', relation: 'Origin Task' }

          ]},

          { id: 'script_sql_relational', label: 'SQLV3RelationalTable.cs', type: 'entry', database: 'codemap.sqlite', table: 'script_knowledge', x: -0.4, y: 0.4, z: -0.2, color: '#4ade80', summary: 'Relational table structure for radiance caching in Unity shader runtime', related_entries: [

            { label: 'Codebase Cognition Map', target_id: 'concept_codemap', target_db: 'codemap.sqlite', relation: 'Parent Concept' },

            { label: 'Inspect SQLV3RelationalTable.cs', target_id: 'receipt_read_file', target_db: 'control.sqlite', relation: 'Spawned Receipt' }

          ]},

          { id: 'receipt_read_file', label: 'read_file Receipt (b30058fb)', type: 'entry', database: 'control.sqlite', table: 'tool_receipts', x: 0.3, y: -0.3, z: 0.1, color: '#fb923c', summary: 'read_file Assets/.../SQLV3RelationalTable.cs offset 240000 limit 3000 verified', related_entries: [

            { label: 'SQLV3RelationalTable.cs', target_id: 'script_sql_relational', target_db: 'codemap.sqlite', relation: 'Target Script' },

            { label: 'Execution Receipts', target_id: 'concept_receipts', target_db: 'control.sqlite', relation: 'Parent Concept' }

          ]}

        ],

        links: [

          { source: 'concept_tasks', target: 'concept_receipts', weight: 2, color: 'rgba(56, 189, 248, 0.4)' },

          { source: 'concept_tasks', target: 'concept_codemap', weight: 2, color: 'rgba(74, 222, 128, 0.4)' },

          { source: 'concept_receipts', target: 'concept_proposals', weight: 2, color: 'rgba(192, 132, 252, 0.4)' },

          { source: 'concept_codemap', target: 'script_sql_relational', weight: 1.5, color: 'rgba(74, 222, 128, 0.6)' },

          { source: 'script_sql_relational', target: 'receipt_read_file', weight: 1.5, color: 'rgba(251, 146, 60, 0.6)' },

          { source: 'receipt_read_file', target: 'concept_receipts', weight: 1.5, color: 'rgba(251, 146, 60, 0.6)' }

        ]

      };

    }



    sqlGraphData = data;

    if (statusBar) {

      statusBar.textContent = `Loaded ${data.nodes ? data.nodes.length : 0} nodes • ${data.links ? data.links.length : 0} relational links`;

    }



    startSql3dRenderer();

  }



  function startSql3dRenderer() {

    const canvas = document.getElementById('sql3dCanvas');

    if (!canvas) return;

    const ctx = canvas.getContext('2d');



    if (sql3dAnimId) cancelAnimationFrame(sql3dAnimId);



    // Mouse Controls for 3D Camera

    let isDragging = false;

    let lastX = 0, lastY = 0;



    canvas.onmousedown = (e) => {

      isDragging = true;

      lastX = e.clientX;

      lastY = e.clientY;

    };

    window.addEventListener('mouseup', () => { isDragging = false; });

    window.addEventListener('mousemove', (e) => {

      if (!isDragging) return;

      const dx = e.clientX - lastX;

      const dy = e.clientY - lastY;

      lastX = e.clientX;

      lastY = e.clientY;

      sqlCamYaw += dx * 0.008;

      sqlCamPitch = Math.max(-1.2, Math.min(1.2, sqlCamPitch + dy * 0.008));

    });

    canvas.onwheel = (e) => {

      e.preventDefault();

      sqlCamZoom = Math.max(0.4, Math.min(2.5, sqlCamZoom - e.deltaY * 0.001));

    };



    // Canvas click to select node

    canvas.onclick = (e) => {

      const rect = canvas.getBoundingClientRect();

      const clickX = e.clientX - rect.left;

      const clickY = e.clientY - rect.top;



      if (!sqlGraphData || !sqlGraphData.nodes) return;

      let closest = null;

      let minDist = 22;



      sqlGraphData.nodes.forEach(node => {

        if (node._screenX !== undefined && node._screenY !== undefined) {

          const d = Math.hypot(node._screenX - clickX, node._screenY - clickY);

          if (d < minDist) {

            minDist = d;

            closest = node;

          }

        }

      });



      if (closest) {

        selectSqlNode(closest);

      }

    };



    const render = () => {

      ctx.clearRect(0, 0, canvas.width, canvas.height);

      const cx = canvas.width / 2;

      const cy = canvas.height / 2;



      if (sqlAutoRotate && !isDragging) {

        sqlCamYaw += 0.005;

      }



      // Draw faint background grid

      ctx.strokeStyle = 'rgba(30, 41, 59, 0.4)';

      ctx.lineWidth = 1;

      for (let r = 50; r <= 200; r += 50) {

        ctx.beginPath();

        ctx.arc(cx, cy, r * sqlCamZoom, 0, Math.PI * 2);

        ctx.stroke();

      }



      if (!sqlGraphData || !sqlGraphData.nodes) {

        const sqlWin = document.getElementById('unifiedSqlSuiteWindow');

        if (sqlWin && !sqlWin.classList.contains('minimized')) {

          sql3dAnimId = requestAnimationFrame(render);

        }

        return;

      }



      // Project 3D nodes

      const cosY = Math.cos(sqlCamYaw);

      const sinY = Math.sin(sqlCamYaw);

      const cosP = Math.cos(sqlCamPitch);

      const sinP = Math.sin(sqlCamPitch);



      sqlGraphData.nodes.forEach(node => {

        let x = (node.x || 0) * 130 * sqlCamZoom;

        let y = (node.y || 0) * 90 * sqlCamZoom;

        let z = (node.z || 0) * 110 * sqlCamZoom;



        // Yaw rotation

        const x1 = x * cosY - z * sinY;

        const z1 = x * sinY + z * cosY;



        // Pitch rotation

        const y2 = y * cosP - z1 * sinP;

        const z2 = y * sinP + z1 * cosP;



        const fov = 350;

        const scale = fov / (fov + z2 + 180);

        node._screenX = cx + x1 * scale;

        node._screenY = cy + y2 * scale;

        node._scale = scale;

        node._depth = z2;

      });



      // Draw Links

      if (sqlGraphData.links) {

        sqlGraphData.links.forEach(l => {

          const s = sqlGraphData.nodes.find(n => n.id === l.source);

          const t = sqlGraphData.nodes.find(n => n.id === l.target);

          if (s && t && s._screenX && t._screenX) {

            ctx.strokeStyle = l.color || 'rgba(56, 189, 248, 0.35)';

            ctx.lineWidth = (l.weight || 1.2) * s._scale;

            ctx.beginPath();

            ctx.moveTo(s._screenX, s._screenY);

            ctx.lineTo(t._screenX, t._screenY);

            ctx.stroke();

          }

        });

      }



      // Sort nodes by depth for proper z-rendering

      const sorted = [...sqlGraphData.nodes].sort((a, b) => b._depth - a._depth);



      sorted.forEach(node => {

        const sx = node._screenX;

        const sy = node._screenY;

        const sc = Math.max(0.4, node._scale);

        const radius = (node.type === 'concept' ? 14 : 7) * sc;

        const isSelected = sqlSelectedNode && sqlSelectedNode.id === node.id;



        // Glow

        ctx.fillStyle = node.color || '#38bdf8';

        ctx.shadowColor = isSelected ? '#ffffff' : (node.color || '#38bdf8');

        ctx.shadowBlur = (isSelected ? 16 : 8) * sc;

        ctx.beginPath();

        ctx.arc(sx, sy, radius, 0, Math.PI * 2);

        ctx.fill();

        ctx.shadowBlur = 0;



        if (isSelected) {

          ctx.strokeStyle = '#ffffff';

          ctx.lineWidth = 2;

          ctx.beginPath();

          ctx.arc(sx, sy, radius + 3, 0, Math.PI * 2);

          ctx.stroke();

        }



        // Label

        ctx.fillStyle = isSelected ? '#ffffff' : '#cbd5e1';

        ctx.font = `${Math.floor((node.type === 'concept' ? 11 : 9.5) * sc)}px sans-serif`;

        ctx.textAlign = 'center';

        ctx.fillText(node.label || node.id, sx, sy + radius + 11 * sc);

      });



      const sqlWin = document.getElementById('unifiedSqlSuiteWindow');

      if (sqlWin && !sqlWin.classList.contains('minimized')) {

        sql3dAnimId = requestAnimationFrame(render);

      }

    };



    render();

  }



  function selectSqlNode(node) {

    sqlSelectedNode = node;

    const title = document.getElementById('sqlInspNodeTitle');

    const badge = document.getElementById('sqlInspTypeBadge');

    const dbTable = document.getElementById('sqlInspDbTable');

    const digest = document.getElementById('sqlInspDigest');

    const relList = document.getElementById('sqlRelatedEntriesList');



    if (title) title.textContent = node.label || node.id;

    if (badge) badge.textContent = (node.type || 'NODE').toUpperCase();

    if (dbTable) dbTable.textContent = `${node.database || 'control.sqlite'} → ${node.table || 'concepts'}`;

    if (digest) digest.textContent = node.summary || node.content || 'Active operational state packet';



    // Populate "Related entries in the same or other databases"

    if (relList) {

      relList.innerHTML = '';

      const related = node.related_entries || [];



      if (related.length === 0) {

        relList.innerHTML = '<span style="font-size:10px; color:#64748b;">No related entries linked.</span>';

      } else {

        related.forEach(rel => {

          const item = document.createElement('div');

          item.className = 'xp-sql-related-item';

          item.innerHTML = `

            <span class="xp-sql-rel-target">🔗 ${escapeHtml(rel.label || rel.target_id)}</span>

            <span class="xp-sql-rel-db">${escapeHtml(rel.target_db || 'SQLite')} • ${escapeHtml(rel.relation || 'Relational Link')}</span>

          `;



          // CRITICAL: Clicking traverses 3D renderer smoothly to that node!

          item.addEventListener('click', () => {

            traverseToRelatedNode(rel);

          });

          relList.appendChild(item);

        });

      }

    }



    // If clicking a Greater Concept node in Greater Concepts mode -> drill down!

    if (node.type === 'concept' && sqlActiveMode === 'greater') {

      sqlActiveMode = 'entries';

      const modeBtnEntries = document.getElementById('modeBtnEntries');

      const modeBtnGreater = document.getElementById('modeBtnGreater');

      if (modeBtnEntries && modeBtnGreater) {

        modeBtnEntries.classList.add('active');

        modeBtnGreater.classList.remove('active');

      }

      loadSqlGraph(node.database || 'all', node.id);

    }

  }



  function traverseToRelatedNode(rel) {

    if (rel.target_db && rel.target_db !== sqlActiveDatabase && sqlActiveDatabase !== 'all') {

      sqlActiveDatabase = rel.target_db;

      document.querySelectorAll('.xp-sql-db-btn').forEach(b => {

        b.classList.toggle('active', b.dataset.db === rel.target_db);

      });

      loadSqlGraph(rel.target_db);

    }



    // Find node and focus camera

    if (sqlGraphData && sqlGraphData.nodes) {

      const targetNode = sqlGraphData.nodes.find(n => n.id === rel.target_id);

      if (targetNode) {

        selectSqlNode(targetNode);

        sqlCamYaw = -Math.atan2(targetNode.z || 0, targetNode.x || 1);

        sqlCamPitch = 0.1;

        sqlCamZoom = 1.3;

      }

    }

  }



  // -------------------------------------------------------------------------

  // Sandbox Security Center Implementation with Fallback

  // -------------------------------------------------------------------------

  async function loadSandboxStatus() {

    const wsDisplay = document.getElementById('sandboxWsRootDisplay');

    const tableBody = document.getElementById('sandboxAuditTableBody');

    if (!wsDisplay || !tableBody) return;



    let data = null;

    const activeWs = workspaceSelect ? workspaceSelect.value : '';



    try {

      const params = new URLSearchParams({ workspace: activeWs });

      const resp = await fetch(`/api/iba/sandbox/status?${params}`);

      if (resp.ok) {

        data = await resp.json();

      }

    } catch (err) {}



    if (!data || !data.success) {

      data = {

        success: true,

        workspace_root: activeWs || 'C:\\Users\\Rackneh\\Documents\\Github\\InferenceLightingLab',

        audit_events: [

          { timestamp: Date.now() / 1000, operation: 'path_jail_verify', target_path: 'InferenceLightingLab', allowed: true },

          { timestamp: (Date.now() / 1000) - 30, operation: 'sensitive_filter_check', target_path: '.env', allowed: false }

        ]

      };

    }



    wsDisplay.textContent = data.workspace_root || 'None';

    const audits = data.audit_events || [];



    if (audits.length === 0) {

      tableBody.innerHTML = '<tr><td colspan="4" style="text-align:center; color:#94a3b8;">No audit events recorded yet.</td></tr>';

    } else {

      tableBody.innerHTML = '';

      audits.forEach(ev => {

        const tr = document.createElement('tr');

        if (!ev.allowed) tr.className = 'blocked';

        const timeStr = ev.timestamp ? new Date(ev.timestamp * 1000).toLocaleTimeString() : '--';

        tr.innerHTML = `

          <td>${escapeHtml(timeStr)}</td>

          <td>${escapeHtml(ev.operation || 'access')}</td>

          <td title="${escapeHtml(ev.target_path || '')}">${escapeHtml((ev.target_path || '').split(/[\\\\/]/).pop() || '--')}</td>

          <td>${ev.allowed ? '<span style="color:#16a34a; font-weight:bold;">ALLOWED</span>' : '<span style="color:#dc2626; font-weight:bold;">BLOCKED</span>'}</td>

        `;

        tableBody.appendChild(tr);

      });

    }

  }



  // -------------------------------------------------------------------------

  // Chat Execution & Streaming Engine

  // -------------------------------------------------------------------------

  async function sendChatMessage(text, imageUris = []) {

    if ((!text && (!imageUris || !imageUris.length)) || isGenerating) return;



    // Get the active session persona name
    let botName = 'IBA';
    let botAvatar = '/static/wlm/wlm/usertiles/usertile_robot.png';
    if (window.WLMClient && window.WLMClient.getActivePersona) {
      const persona = window.WLMClient.getActivePersona();
      if (persona) {
        botName = persona.name || persona.first_name || 'IBA';
        botAvatar = persona.avatar || botAvatar;
      }
    }

    // Append user message to WLM Chat History with optional attached images

    const wlmChatHistory = document.getElementById('wlmChatHistory');

    if (wlmChatHistory) {

      const userRow = document.createElement('div');

      userRow.className = 'wlm-chat-msg-row';

      let imgHtml = '';

      if (imageUris && imageUris.length) {

        imgHtml = imageUris.map(img => `

          <div class="wlm-chat-image-attachment" title="Click to view full image">

            <img src="${img}" onclick="window.open(this.src)" alt="Attachment">

          </div>

        `).join('');

      }

      userRow.innerHTML = `

        <div class="wlm-chat-msg-header">

          <img src="/static/wlm/wlm/usertiles/usertile_flare.png" width="16" height="16" style="border-radius:2px;">

          <span>You say:</span>

          <span class="wlm-chat-msg-time">${new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'})}</span>

        </div>

        <div class="wlm-chat-msg-body">

          ${text ? escapeHtml(text) : ''}

          ${imgHtml}

        </div>

      `;

      wlmChatHistory.appendChild(userRow);

      wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;

    }



    playWlmSound('outgoing.mp3');



    // Robot Workstation starts typing

    const workstation = document.getElementById('unifiedWorkstation');

    if (workstation) workstation.classList.add('is-typing');



    // Reset Thought Bubble

    const thoughtContainer = document.getElementById('unifiedThoughtContainer');

    const thoughtBody = document.getElementById('unifiedThoughtBody');

    const thoughtBadge = document.getElementById('unifiedThoughtBadge');

    if (thoughtContainer) thoughtContainer.classList.add('active');

    if (thoughtBody) thoughtBody.textContent = 'Thinking in recurrent loops...';



    // Prepare Dory Bot reply row in WLM

    let botMsgBody = null;

    if (wlmChatHistory) {

      const botRow = document.createElement('div');

      botRow.className = 'wlm-chat-msg-row';

      botRow.innerHTML = `

        <div class="wlm-chat-msg-header bot">

          <img src="${botAvatar}" width="16" height="16" style="border-radius:2px;">

          <span>${escapeHtml(botName)} says:</span>

          <span class="wlm-chat-msg-time">${new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'})}</span>

        </div>

        <div class="wlm-chat-msg-body bot-answer"><span style="color:#64748b;">Thinking...</span></div>

      `;

      wlmChatHistory.appendChild(botRow);

      botMsgBody = botRow.querySelector('.bot-answer');

      wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;

    }



    // Build messages payload

    const messages = [];

    if (systemPrompt && systemPrompt.value.trim()) {

      messages.push({ role: 'system', content: systemPrompt.value.trim() });

    }

    chatHistory.forEach(m => messages.push({ role: m.role, content: m.content }));

    messages.push({ role: 'user', content: text });

    chatHistory.push({ role: 'user', content: text });



    setGeneratingState(true);

    activeAbortController = new AbortController();



    let fullAnswer = '';

    let fullThought = '';
    let currentThoughtEl = null;
    let needsNewThoughtBlock = false;

    // Parallel IBA executions: peer runners share this stream, so events from
    // multiple tasks interleave. Each task id gets its own column inside the
    // reply bubble so concurrent work renders side by side.
    let parallelWrap = null;
    const taskCols = new Map();
    // Last-activity timestamps per task. When parallel work ends and only one
    // task keeps producing events, the column layout collapses: the finished
    // side-by-side section stays frozen above and the surviving task's new
    // output renders full-width below it. Time-based (not event-count) so a
    // fast reasoning-token burst can't trigger a premature collapse while a
    // peer is mid-tool-call.
    const lastTaskActivity = new Map();
    const noteTaskActivity = (taskId) => {
      const key = taskId || '__single__';
      const now = Date.now();
      lastTaskActivity.set(key, now);
      if (taskCols.size > 1) {
        let solo = true;
        for (const [tid, ts] of lastTaskActivity) {
          if (tid !== key && now - ts < 25000) { solo = false; break; }
        }
        if (solo) {
          taskCols.clear();
          parallelWrap = null;  // old wrap element stays in the DOM, frozen
        }
      }
    };
    const ibaCol = (taskId, taskTitle) => {
      const key = taskId || '__single__';
      let st = taskCols.get(key);
      if (st) {
        if (taskTitle && !st.hasTitle && st.titleEl) { st.titleEl.textContent = taskTitle; st.hasTitle = true; }
        return st;
      }
      if (!botMsgBody) {
        st = { el: null, body: null, titleEl: null, hasTitle: true, thoughtEl: null, colThought: '', needsNewThoughtBlock: false };
        taskCols.set(key, st);
        return st;
      }
      if (!parallelWrap) {
        parallelWrap = document.createElement('div');
        parallelWrap.className = 'wlm-parallel-cols';
        if (botMsgBody) {
          botMsgBody.innerHTML = '';
          botMsgBody.appendChild(parallelWrap);
        }
      }
      const col = document.createElement('div');
      col.className = 'wlm-task-col';
      const shortId = key === '__single__' ? '' : String(key).slice(0, 6);
      col.innerHTML = `
        <div class="wlm-task-col-title" style="display:${taskCols.size ? 'block' : 'none'};">${escapeHtml(taskTitle || ('Task ' + shortId))}</div>
        <div class="wlm-task-col-body"></div>`;
      parallelWrap.appendChild(col);
      st = {
        el: col,
        body: col.querySelector('.wlm-task-col-body'),
        titleEl: col.querySelector('.wlm-task-col-title'),
        hasTitle: !!taskTitle,
        thoughtEl: null,
        colThought: '',
        needsNewThoughtBlock: false,
      };
      taskCols.set(key, st);
      // Reveal titles once a second column proves this is a parallel run.
      if (taskCols.size > 1) {
        taskCols.forEach(s => { if (s.titleEl) s.titleEl.style.display = 'block'; });
      }
      return st;
    };



    try {

      const resp = await fetch('/api/chat/stream', {

        method: 'POST',

        headers: { 'Content-Type': 'application/json' },

        body: JSON.stringify({

          messages: messages,

          temperature: parseFloat(tempRange.value),

          top_p: parseFloat(topPRange.value),

          max_tokens: parseInt(maxTokens.value, 10),

          reasoning_effort: reasoningMode.value,

          use_iba: (ibaModeToggle ? ibaModeToggle.checked : true),

          variable_newconv: (variableNewconvToggle ? variableNewconvToggle.checked : false),

          workspace_path: (workspaceSelect ? workspaceSelect.value : null),

          session_id: (sessionSelect && sessionSelect.value ? sessionSelect.value : (currentActiveSessionId || null)),

          images: (imageUris && imageUris.length) ? imageUris : null

        }),

        signal: activeAbortController.signal

      });



      if (!resp.ok) {

        const err = await resp.json().catch(() => ({ detail: 'Failed request' }));

        if (botMsgBody) botMsgBody.innerHTML = `<span style="color:#dc2626;">Error: ${err.detail || 'Server unavailable'}</span>`;

        setGeneratingState(false);

        if (workstation) workstation.classList.remove('is-typing');

        return;

      }



      const reader = resp.body.getReader();

      const decoder = new TextDecoder('utf-8');

      let buffer = '';



      while (true) {

        const { value, done } = await reader.read();

        if (done) break;



        buffer += decoder.decode(value, { stream: true });

        const lines = buffer.split('\n');

        buffer = lines.pop();



        let currentEvent = 'message';

        for (const line of lines) {

          if (line.startsWith('event: ')) {

            currentEvent = line.substring(7).trim();

          } else if (line.startsWith('data: ')) {

            const rawData = line.substring(6).trim();

            if (!rawData) continue;

            try {

              const data = JSON.parse(rawData);



              if (currentEvent === 'iba_step_start') {

                if (window.unifiedDesktopManager && window.unifiedDesktopManager.addStepToWlm) {

                  window.unifiedDesktopManager.addStepToWlm(data.step_number, data.step_title || 'Planning & Strategy');

                }

              } else if (currentEvent === 'iba_tool_start') {

                // Mark that we need a new thought block after this tool call
                noteTaskActivity(data.task);
                const toolCol = ibaCol(data.task, data.task_title);
                toolCol.needsNewThoughtBlock = true;

                const cmdText = document.getElementById('cmdBodyText');

                if (cmdText) {

                  cmdText.textContent += `\nC:\\DoryLLM> ${data.tool} ${JSON.stringify(data.args || {})}`;

                  cmdText.scrollTop = cmdText.scrollHeight;

                }

                // Also display tool call in WLM chat
                if (wlmChatHistory && toolCol.body) {
                  const toolRow = document.createElement('div');
                  toolRow.className = 'wlm-tool-call-row';
                  toolRow.id = `tool_${data.step || Date.now()}`;
                  toolRow.innerHTML = `
                    <div class="wlm-tool-call-header">
                      <img src="/static/xp_assets/icons/shell32_24.png" width="12" height="12" alt="Tool">
                      <span class="wlm-tool-name">${escapeHtml(data.tool)}</span>
                      <span class="wlm-tool-status" style="margin-left:auto; font-size:10px; color:#f59e0b;">pending</span>
                    </div>
                    <div class="wlm-tool-args"><code>${escapeHtml(JSON.stringify(data.args || {}, null, 2))}</code></div>
                    <div class="wlm-tool-result" style="display:none; margin-top:6px; padding:6px; background:#f0fdf4; border-left:3px solid #16a34a; border-radius:3px; font-size:10px; color:#166534;"></div>
                  `;
                  toolCol.body.appendChild(toolRow);
                  wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;
                }

              } else if (currentEvent === 'iba_tool_result') {

                const cmdText = document.getElementById('cmdBodyText');

                if (cmdText) {

                  cmdText.textContent += `\n[RECEIPT] ${data.tool} -> SUCCESS (Hash: ok)`;

                  cmdText.scrollTop = cmdText.scrollHeight;

                }

                // Also display tool result in WLM chat - find the corresponding tool call row
                noteTaskActivity(data.task);
                const resCol = ibaCol(data.task, data.task_title);
                if (wlmChatHistory && resCol.body) {
                  const toolRows = resCol.body.querySelectorAll('.wlm-tool-call-row');
                  let targetRow = null;
                  
                  // Find the most recent tool call that hasn't been completed
                  for (let i = toolRows.length - 1; i >= 0; i--) {
                    const row = toolRows[i];
                    const statusEl = row.querySelector('.wlm-tool-status');
                    const resultEl = row.querySelector('.wlm-tool-result');
                    if (statusEl && statusEl.textContent === 'pending' && resultEl && resultEl.style.display === 'none') {
                      targetRow = row;
                      break;
                    }
                  }
                  
                  if (targetRow) {
                    const statusEl = targetRow.querySelector('.wlm-tool-status');
                    const resultEl = targetRow.querySelector('.wlm-tool-result');
                    
                    if (statusEl) {
                      statusEl.textContent = 'completed';
                      statusEl.style.color = '#16a34a';
                    }
                    
                    if (resultEl) {
                      resultEl.style.display = 'block';
                      resultEl.innerHTML = `<strong>Result:</strong> ${escapeHtml(JSON.stringify(data.result || {}, null, 2).substring(0, 500))}${JSON.stringify(data.result || {}).length > 500 ? '...' : ''}`;
                    }
                  } else {
                    // Fallback: create a standalone result row if we can't find the tool call
                    const resultRow = document.createElement('div');
                    resultRow.className = 'wlm-tool-result-row';
                    const statusColor = '#16a34a';
                    resultRow.innerHTML = `
                      <div class="wlm-tool-result-header" style="color:${statusColor}">
                        <span>✓ ${escapeHtml(data.tool)} - SUCCESS</span>
                      </div>
                    `;
                    resCol.body.appendChild(resultRow);
                  }
                  wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;
                }

              } else if (currentEvent === 'thought') {

                noteTaskActivity(data.task);
                const thCol = ibaCol(data.task, data.task_title);

                // Create a new thought block if needed (after tool calls)
                if (thCol.needsNewThoughtBlock || !thCol.thoughtEl) {
                  if (thCol.body) {
                    // Close previous thought block if it exists
                    if (thCol.thoughtEl) {
                      thCol.thoughtEl.open = false;
                    }

                    const thoughtEl = document.createElement('details');
                    thoughtEl.className = 'wlm-thought-dropdown';
                    thoughtEl.open = true;
                    thoughtEl.innerHTML = `
                      <summary class="wlm-thought-summary">
                        <span class="wlm-thought-pulse"></span>
                        <img src="/static/xp_assets/icons/shell32_24.png" width="14" height="14" alt="Think">
                        <span class="wlm-thought-title">${botName} Reasoning (0 tokens)</span>
                      </summary>
                      <div class="wlm-thought-body"></div>
                    `;
                    thCol.body.appendChild(thoughtEl);
                    thCol.thoughtEl = thoughtEl;
                    thCol.colThought = '';
                    thCol.needsNewThoughtBlock = false;
                  }
                }

                thCol.colThought += data.content;
                fullThought = thCol.colThought;

                if (thoughtBody) thoughtBody.textContent = fullThought;

                const tokenCnt = thCol.colThought.split(/\s+/).filter(Boolean).length;

                if (thoughtBadge) thoughtBadge.textContent = `${tokenCnt} tokens`;

                if (thoughtContainer) thoughtContainer.scrollTop = thoughtContainer.scrollHeight;

                // Update the current thought element in WLM chat
                if (thCol.thoughtEl) {
                  const bodyEl = thCol.thoughtEl.querySelector('.wlm-thought-body');
                  const titleEl = thCol.thoughtEl.querySelector('.wlm-thought-title');

                  if (bodyEl) {
                    bodyEl.textContent = thCol.colThought;
                    bodyEl.scrollTop = bodyEl.scrollHeight;
                  }

                  if (titleEl) titleEl.textContent = `${botName} Reasoning (${tokenCnt} tokens)`;
                }

                if (wlmChatHistory) wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;

              } else if (currentEvent === 'answer') {

                fullAnswer += data.content;

                if (botMsgBody) {

                  let ansContainer = botMsgBody.querySelector('.wlm-answer-text');

                  if (!ansContainer) {

                    // Close open thought blocks (all parallel columns) when answer begins
                    taskCols.forEach(s => { if (s.thoughtEl) s.thoughtEl.open = false; });
                    if (currentThoughtEl) {
                      currentThoughtEl.open = false;
                    }

                    ansContainer = document.createElement('div');

                    ansContainer.className = 'wlm-answer-text';

                    botMsgBody.appendChild(ansContainer);

                  }

                  renderMarkdown(ansContainer, fullAnswer);

                }

                if (wlmChatHistory) wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;



              } else if (currentEvent === 'iba_wait') {

                // Agent asked a multiple-choice question — render clickable options
                noteTaskActivity(data.task);
                const waitCol = ibaCol(data.task, data.task_title);
                if (waitCol.body && Array.isArray(data.options) && data.options.length) {
                  const waitEl = document.createElement('div');
                  waitEl.className = 'wlm-wait-options';
                  waitEl.style.cssText = 'margin:8px 0;padding:8px;background:#fffbeb;border:1px solid #f59e0b;border-radius:6px;';
                  const qText = document.createElement('div');
                  qText.style.cssText = 'font-size:11px;color:#92400e;margin-bottom:6px;font-weight:600;';
                  qText.textContent = '❓ ' + (data.text || 'The agent needs your input:');
                  waitEl.appendChild(qText);
                  const btnWrap = document.createElement('div');
                  btnWrap.style.cssText = 'display:flex;flex-wrap:wrap;gap:6px;';
                  data.options.forEach((opt, idx) => {
                    const btn = document.createElement('button');
                    btn.type = 'button';
                    btn.textContent = `${idx + 1}. ${opt}`;
                    btn.style.cssText = 'padding:4px 10px;font-size:11px;background:#fff;border:1px solid #d97706;border-radius:4px;cursor:pointer;color:#78350f;';
                    btn.onmouseenter = () => { btn.style.background = '#fef3c7'; };
                    btn.onmouseleave = () => { btn.style.background = '#fff'; };
                    btn.onclick = () => {
                      waitEl.querySelectorAll('button').forEach(b => { b.disabled = true; b.style.opacity = '0.5'; });
                      btn.style.opacity = '1';
                      btn.style.background = '#f59e0b';
                      btn.style.color = '#fff';
                      if (typeof sendChatMessage === 'function') sendChatMessage(String(opt));
                    };
                    btnWrap.appendChild(btn);
                  });
                  waitEl.appendChild(btnWrap);
                  waitCol.body.appendChild(waitEl);
                  wlmChatHistory.scrollTop = wlmChatHistory.scrollHeight;
                }

              } else if (currentEvent === 'done') {

                tokenSpeedStat.textContent = `${data.speed || '--'} tokens/s`;

                if (workstation) workstation.classList.remove('is-typing');

                loadSessions();

                loadSandboxStatus();

              } else if (currentEvent === 'error') {

                if (botMsgBody) botMsgBody.innerHTML += `<div style="color:#dc2626;">[Error: ${data.error}]</div>`;

              }

            } catch (jsonErr) {}

          }

        }

      }



      chatHistory.push({ role: 'assistant', content: fullAnswer });



    } catch (err) {

      if (err.name !== 'AbortError') {

        if (botMsgBody) botMsgBody.innerHTML += `<div style="color:#dc2626;">[Connection failed: ${err.message}]</div>`;

      }

    } finally {

      setGeneratingState(false);

      activeAbortController = null;

      if (workstation) workstation.classList.remove('is-typing');

    }

  }



  // -------------------------------------------------------------------------

  // Existing Settings, Modals, Presets & Polling Logic

  // -------------------------------------------------------------------------

  // Loop Selector

  loopButtons.forEach(btn => {

    btn.addEventListener('click', () => {

      loopButtons.forEach(b => b.classList.remove('active'));

      btn.classList.add('active');

      selectedLoops = parseInt(btn.dataset.loops, 10);

      chipLoops.textContent = `${selectedLoops} Loops`;

    });

  });



  // Slider value displays

  tempRange.addEventListener('input', () => { tempVal.textContent = tempRange.value; });

  topPRange.addEventListener('input', () => { topPVal.textContent = topPRange.value; });

  maxTokens.addEventListener('input', () => { maxTokensVal.textContent = maxTokens.value; });

  cpuThreads.addEventListener('input', () => { threadsVal.textContent = cpuThreads.value; });

  if (gpuLayers) {

    gpuLayers.addEventListener('input', () => {

      gpuLayersVal.textContent = gpuLayers.value == 999 ? '999 (All Layers)' : gpuLayers.value;

    });

  }



  // Sync sidebar engine controls from the live server config

  function syncEngineControls(cfg) {

    if (!cfg) return;

    if (cfg.loops) {

      selectedLoops = parseInt(cfg.loops, 10) || selectedLoops;

      loopButtons.forEach(b => b.classList.toggle('active', parseInt(b.dataset.loops, 10) === selectedLoops));

      if (chipLoops) chipLoops.textContent = `${selectedLoops} Loops`;

    }

    if (cfg.cache_mode && kvCacheMode) {

      kvCacheMode.value = cfg.cache_mode;

      if (chipCache) chipCache.textContent = cfg.cache_mode;

    }

    if (cfg.threads && cpuThreads) {

      cpuThreads.value = String(cfg.threads);

      if (threadsVal) threadsVal.textContent = String(cfg.threads);

    }

    if (cfg.ctx_size && ctxSize) ctxSize.value = String(cfg.ctx_size);

    if (cfg.gpu_layers != null && gpuLayers) {

      gpuLayers.value = String(cfg.gpu_layers);

      if (gpuLayersVal) gpuLayersVal.textContent = cfg.gpu_layers == 999 ? '999 (All Layers)' : String(cfg.gpu_layers);

      if (chipGpu) chipGpu.textContent = cfg.gpu_layers >= 999 ? '🎮 Vulkan RX 580 (8GB)' : `🎮 ${cfg.gpu_layers} Layers`;

    }

    if (cfg.port && serverPortStat) serverPortStat.textContent = `Port: ${cfg.port}`;

  }



  function appendEngineLog(text, type) {

    if (!terminalContainer) return;

    const div = document.createElement('div');

    div.className = `terminal-line ${type || 'info'}`;

    const now = new Date();

    const ts = [now.getHours(), now.getMinutes(), now.getSeconds()].map(n => String(n).padStart(2, '0')).join(':');

    div.innerHTML = `<span class="t-time">[${ts}]</span> ${escapeHtml(text)}`;

    terminalContainer.appendChild(div);

    terminalContainer.scrollTop = terminalContainer.scrollHeight;

  }



  // Apply & Restart Engine — collects sidebar controls and relaunches llama-server

  if (applyEngineBtn) {

    applyEngineBtn.addEventListener('click', async () => {

      applyEngineBtn.disabled = true;

      const originalHtml = applyEngineBtn.innerHTML;

      applyEngineBtn.innerHTML = '<span class="btn-icon">⏳</span> Restarting Engine...';

      statusDot.className = 'status-dot starting';

      statusText.textContent = 'Restarting Engine...';

      appendEngineLog('Restarting Dory engine with new settings...', 'info');

      try {

        const st = await fetch('/api/status').then(r => r.json()).catch(() => ({}));

        const cur = (st && st.config) || {};

        const payload = {

          loops: selectedLoops,

          cache_mode: kvCacheMode ? kvCacheMode.value : 'last_loop',

          threads: parseInt(cpuThreads ? cpuThreads.value : '6', 10) || 6,

          ctx_size: parseInt(ctxSize ? ctxSize.value : '32768', 10) || 32768,

          port: cur.port || 8080,

          host: cur.host || '0.0.0.0',

          api_key: cur.api_key || 'dory-a5118c2b008b1ebe80793733a7aa48ee',

          gpu_layers: gpuLayers ? parseInt(gpuLayers.value, 10) : 999,

        };

        const resp = await fetch('/api/server/start', {

          method: 'POST',

          headers: { 'Content-Type': 'application/json' },

          body: JSON.stringify(payload),

        });

        const data = await resp.json().catch(() => ({}));

        if (!resp.ok) throw new Error(data.detail || 'Engine failed to start');

        syncEngineControls(data.config || payload);

        statusDot.className = 'status-dot running';

        statusText.textContent = 'Engine Active';

        appendEngineLog(`Engine restarted: ${payload.loops} loops, ${payload.cache_mode}, ctx ${payload.ctx_size}, ${payload.threads} threads, ${payload.gpu_layers} GPU layers`, 'success');

      } catch (e) {

        statusDot.className = 'status-dot error';

        statusText.textContent = 'Restart Failed';

        appendEngineLog(`Engine restart failed: ${e.message || e}`, 'error');

      } finally {

        applyEngineBtn.disabled = false;

        applyEngineBtn.innerHTML = originalHtml;

      }

    });

  }



  // IBA Prompt Target Budget — persists per-session context policy in SQLite

  function markIbaBudgetActive(target) {

    ibaCtxButtons.forEach(b => {

      const match = (target === null || target === undefined)

        ? b.dataset.ctx === 'auto'

        : String(target).toLowerCase() === b.dataset.ctx || String(target) === String(parseInt(b.dataset.ctx, 10) * 1024);

      b.classList.toggle('active', match);

    });

  }



  async function syncIbaBudgetButtons() {

    const { ws, sess } = getActiveScope();

    try {

      const params = new URLSearchParams({ workspace: ws, session: sess });

      const resp = await fetch(`/api/iba/policy?${params}`);

      const data = await resp.json();

      markIbaBudgetActive(data.policy ? data.policy.target : null);

    } catch (e) {}

  }



  ibaCtxButtons.forEach(btn => {

    btn.addEventListener('click', async () => {

      ibaCtxButtons.forEach(b => b.classList.remove('active'));

      btn.classList.add('active');

      const { ws, sess } = getActiveScope();

      try {

        const params = new URLSearchParams({ workspace: ws, session: sess });

        const resp = await fetch(`/api/iba/policy?${params}`, {

          method: 'POST',

          headers: { 'Content-Type': 'application/json' },

          body: JSON.stringify({ target: btn.dataset.ctx }),

        });

        const data = await resp.json().catch(() => ({}));

        if (!resp.ok) throw new Error(data.detail || 'Unable to update context policy');

        appendEngineLog(`IBA context target set to ${btn.dataset.ctx} for session ${sess}`, 'info');

      } catch (e) {

        appendEngineLog(`IBA context policy failed: ${e.message || e}`, 'error');

        syncIbaBudgetButtons();

      }

    });

  });



  // IBA Backend switcher — Dory (llama.cpp) vs Gemma (LM Studio), GPU-exclusive

  const ibaBackendButtons = document.querySelectorAll('.iba-backend-btn');

  const ibaBackendStatus = document.getElementById('ibaBackendStatus');



  function renderIbaBackendStatus(data) {

    if (!ibaBackendStatus || !data) return;

    const eff = data.effective;

    if (eff === 'lmstudio') {

      const loaded = (data.lmstudio_loaded || [])[0];

      ibaBackendStatus.innerHTML = loaded

        ? `Active: <b>LM Studio</b> — ${escapeHtml(loaded)} loaded. Unload it in LM Studio before switching back to Dory.`

        : `Active: <b>LM Studio</b> — no model loaded. Load Gemma in LM Studio first.`;

    } else {

      const engine = data.dory_engine === 'RUNNING' ? 'engine running' : 'engine stopped — click ⚡ Apply & Restart';

      ibaBackendStatus.innerHTML = `Active: <b>Dory</b> — ${engine}. Unload Gemma in LM Studio before switching back.`;

    }

  }



  async function syncIbaBackend() {

    try {

      const resp = await fetch('/api/iba/provider');

      const data = await resp.json();

      const sel = data.config && data.config.provider;

      // Highlight the explicit choice; for "auto" highlight whichever is effective.

      const highlight = (sel === 'auto' || !sel) ? data.effective : sel;

      ibaBackendButtons.forEach(b => b.classList.toggle('active', b.dataset.backend === highlight));

      renderIbaBackendStatus(data);

    } catch (e) {}

  }



  ibaBackendButtons.forEach(btn => {

    btn.addEventListener('click', async () => {

      ibaBackendButtons.forEach(b => b.classList.remove('active'));

      btn.classList.add('active');

      try {

        const resp = await fetch('/api/iba/provider', {

          method: 'POST',

          headers: { 'Content-Type': 'application/json' },

          body: JSON.stringify({ provider: btn.dataset.backend }),

        });

        const data = await resp.json().catch(() => ({}));

        if (!resp.ok) throw new Error(data.detail || 'Unable to switch backend');

        appendEngineLog(`IBA backend → ${btn.dataset.backend} (effective: ${data.effective})`, 'info');

        renderIbaBackendStatus(data);

      } catch (e) {

        appendEngineLog(`IBA backend switch failed: ${e.message || e}`, 'error');

        syncIbaBackend();

      }

    });

  });

  syncIbaBackend();



  // Sidebars

  toggleSidebarBtn.addEventListener('click', () => {

    sidebar.classList.toggle('collapsed');

  });

  if (openSessionSidebarBtn) {

    openSessionSidebarBtn.addEventListener('click', () => {

      sessionSidebar.classList.remove('collapsed');

      sessionSidebar.classList.toggle('open');

    });

  }

  if (toggleSessionSidebarBtn) {

    toggleSessionSidebarBtn.addEventListener('click', () => {

      sessionSidebar.classList.toggle('collapsed');

      sessionSidebar.classList.toggle('open');

    });

  }



  // Terminal Drawer

  toggleConsoleBtn.addEventListener('click', () => {

    activityDrawer.classList.toggle('collapsed');

    drawerToggleIcon.textContent = activityDrawer.classList.contains('collapsed') ? '▲' : '▼';

  });

  drawerExpandBtn.addEventListener('click', () => {

    activityDrawer.classList.toggle('collapsed');

    drawerToggleIcon.textContent = activityDrawer.classList.contains('collapsed') ? '▲' : '▼';

  });

  drawerClearBtn.addEventListener('click', () => {

    terminalContainer.innerHTML = '';

  });

  drawerAutoscrollBtn.addEventListener('click', () => {

    autoScrollLogs = !autoScrollLogs;

    drawerAutoscrollBtn.classList.toggle('active', autoScrollLogs);

  });

  drawerCopyBtn.addEventListener('click', () => {

    const text = terminalContainer.innerText;

    navigator.clipboard.writeText(text).then(() => alert('Console logs copied!'));

  });



  // Send & Stop Buttons

  sendBtn.addEventListener('click', () => {

    const text = userInput.value.trim();

    if (!text) return;

    userInput.value = '';

    userInput.style.height = 'auto';

    sendChatMessage(text);

  });

  userInput.addEventListener('keydown', (e) => {

    if (e.key === 'Enter' && !e.shiftKey) {

      e.preventDefault();

      const text = userInput.value.trim();

      if (!text) return;

      userInput.value = '';

      userInput.style.height = 'auto';

      sendChatMessage(text);

    }

  });



  stopBtn.addEventListener('click', () => {

    if (activeAbortController) {

      activeAbortController.abort();

      activeAbortController = null;

    }

    setGeneratingState(false);

  });



  // Allow WLM interactive cards and other XP apps to submit chat messages.
  window.sendChatMessage = sendChatMessage;

  function setGeneratingState(generating) {

    isGenerating = generating;

    if (generating) {

      sendBtn.classList.add('hidden');

      stopBtn.classList.remove('hidden');

    } else {

      sendBtn.classList.remove('hidden');

      stopBtn.classList.add('hidden');

    }

  }



  // -------------------------------------------------------------------------

  // Multi-Project Simulated 3D PC Workstations Subsystem

  // -------------------------------------------------------------------------

  let activeWorkspace = null;

  let isWorkstationBooted = false;

  let cachedWorkspaces = [];



  function bootWorkstation(w) {

    if (!w) return;

    activeWorkspace = w;

    window.currentActiveWorkspace = w;



    // Update Dropdowns

    if (workspaceSelect) workspaceSelect.value = w.path;

    if (sessionWsSelect) sessionWsSelect.value = w.path;



    // Update Top Active Card

    const cardName = document.getElementById('activeProjectCardName');

    const statusPill = document.getElementById('activeProjectStatusPill');

    if (cardName) cardName.textContent = `${w.name} (${w.path})`;

    if (statusPill) {

      statusPill.textContent = 'ONLINE';

      statusPill.className = 'pc-status-pill online';

    }

    if (chipWorkspace) chipWorkspace.textContent = `📁 ${w.name}`;

    if (workspacePathDisplay) workspacePathDisplay.textContent = w.path;

    if (sessionWsPath) sessionWsPath.textContent = w.path;



    // Audio: Mechanical switch clunk & Fan spin

    if (window.XPCore && window.XPCore.Audio) {

      window.XPCore.Audio.playMechanicalClick();

    }



    // Power on active 3D PC tower

    if (window.XPCore && window.XPCore.BeigePC) {

      window.XPCore.BeigePC.setPowerState('activePc', true);

    }



    // Hide Standby Screen

    const standby = document.getElementById('xpStandbyScreen');

    if (standby) {

      standby.style.opacity = '0';

      setTimeout(() => {

        standby.style.display = 'none';

      }, 500);

    }



    isWorkstationBooted = true;



    // Trigger Complete Boot Sequence (POST -> XP Boot -> Setup if fresh / Logon if installed)

    if (window.XPCore && window.XPCore.triggerBootSequence) {

      window.XPCore.triggerBootSequence((finalCfg) => {

        if (window.XPInstaller && window.XPInstaller.markInstalled) {

          window.XPInstaller.markInstalled(w, finalCfg);

        }

        loadSessions();

        loadExplorerDirectory('');

        loadSandboxStatus();

      }, w);

    }

  }



  function renderWorkstationSidebar(workspaces) {

    cachedWorkspaces = workspaces;



    // 1. Populate Dropdowns (Starts Unselected on server start)

    if (workspaceSelect) {

      workspaceSelect.innerHTML = '<option value="" disabled ' + (!activeWorkspace ? 'selected' : '') + '>-- Select a Workstation to Boot --</option>';

      workspaces.forEach(w => {

        const opt = document.createElement('option');

        opt.value = w.path;

        opt.textContent = `${w.name} (${w.path})`;

        if (activeWorkspace && activeWorkspace.path === w.path) opt.selected = true;

        workspaceSelect.appendChild(opt);

      });

    }

    if (sessionWsSelect) {

      sessionWsSelect.innerHTML = '<option value="" disabled ' + (!activeWorkspace ? 'selected' : '') + '>-- Select a Workstation to Boot --</option>';

      workspaces.forEach(w => {

        const opt = document.createElement('option');

        opt.value = w.path;

        opt.textContent = `${w.name} (${w.path})`;

        if (activeWorkspace && activeWorkspace.path === w.path) opt.selected = true;

        sessionWsSelect.appendChild(opt);

      });

    }



    // 2. Render Main Active Project Card

    const cardName = document.getElementById('activeProjectCardName');

    const statusPill = document.getElementById('activeProjectStatusPill');

    const pcContainer = document.getElementById('activeProjectPcContainer');



    if (cardName) {

      cardName.textContent = activeWorkspace ? `${activeWorkspace.name} (${activeWorkspace.path})` : 'No Workstation Selected';

    }

    if (statusPill) {

      statusPill.textContent = isWorkstationBooted ? 'ONLINE' : 'OFFLINE';

      statusPill.className = `pc-status-pill ${isWorkstationBooted ? 'online' : 'offline'}`;

    }



    if (pcContainer && window.XPCore && window.XPCore.BeigePC) {

      pcContainer.innerHTML = window.XPCore.BeigePC.createChassisHtml('activePc', 'large');

      window.XPCore.BeigePC.wireChassis(pcContainer, 'activePc', () => {

        if (!activeWorkspace && workspaces.length > 0) {

          bootWorkstation(workspaces[0]);

        } else if (activeWorkspace) {

          if (!isWorkstationBooted) bootWorkstation(activeWorkspace);

          else window.XPCore.showShutdownDialog();

        }

      }, () => {

        if (activeWorkspace && window.XPCore) {

          window.XPCore.triggerBootSequence(null, activeWorkspace);

        }

      });

      if (isWorkstationBooted) {

        window.XPCore.BeigePC.setPowerState('activePc', true, pcContainer);

      }

    }



    // 3. Render Available Workstations Grid

    const grid = document.getElementById('otherProjectsGrid');

    if (grid && window.XPCore && window.XPCore.BeigePC) {

      grid.innerHTML = '';

      

      // Ensure we display all workspaces (or at least 2 cards matching user mockup)

      let displayList = workspaces.slice();

      if (displayList.length === 1) {

        displayList.push({

          name: 'Demo-Project-XP',

          path: 'C:\\Users\\Rackneh\\Documents\\Github\\Demo-Project-XP',

          description: 'Simulated Secondary XP Project Workstation'

        });

      }



      displayList.forEach((w, idx) => {

        const card = document.createElement('div');

        const isActive = activeWorkspace && activeWorkspace.path === w.path;

        card.className = `other-project-card ${isActive ? 'active' : ''}`;

        card.title = `Click to boot ${w.name}`;

        const pcId = `gridPc_${idx}`;



        card.innerHTML = `

          <div class="other-project-card-title">${escapeHtml(w.name)}</div>

          ${window.XPCore.BeigePC.createChassisHtml(pcId, 'small')}

        `;



        window.XPCore.BeigePC.wireChassis(card, pcId, () => {

          bootWorkstation(w);

        });



        card.addEventListener('click', (e) => {

          if (e.target.closest('button')) return;

          bootWorkstation(w);

        });



        grid.appendChild(card);

      });

    }



    // Wire Standby Power Button

    const standbyBtn = document.getElementById('standbyPowerOnBtn');

    if (standbyBtn) {

      standbyBtn.onclick = () => {

        if (workspaces.length > 0) bootWorkstation(workspaces[0]);

      };

    }

  }



  // Workspaces Loader (Unselected on server start)

  async function loadWorkspaces() {

    try {

      const resp = await fetch('/api/iba/workspaces');

      const data = await resp.json();

      const ws = data.workspaces || [];

      window._cachedWorkspaces = ws;



      // Automatically mark existing projects with sessions/tasks as installed

      if (window.XPInstaller && window.XPInstaller.markInstalled) {

        ws.forEach(w => {

          if (w.stats && w.stats.db_initialized && ((w.stats.sessions_count || 0) > 0 || (w.stats.tasks_count || 0) > 0)) {

            window.XPInstaller.markInstalled(w);

          }

        });

      }



      // Render Workstations UI

      renderWorkstationSidebar(ws);

    } catch (e) {}

  }



  // Wire select changes

  if (sessionWsSelect) {

    sessionWsSelect.addEventListener('change', () => {

      const selectedPath = sessionWsSelect.value;

      const target = cachedWorkspaces.find(w => w.path === selectedPath);

      if (target) bootWorkstation(target);

    });

  }

  if (workspaceSelect) {

    workspaceSelect.addEventListener('change', () => {

      const selectedPath = workspaceSelect.value;

      const target = cachedWorkspaces.find(w => w.path === selectedPath);

      if (target) bootWorkstation(target);

    });

  }



  async function loadSessions() {

    try {

      const activeWs = workspaceSelect ? workspaceSelect.value : '';

      const params = new URLSearchParams({ workspace: activeWs });

      const resp = await fetch(`/api/iba/sessions?${params}`);

      const data = await resp.json();

      const sess = data.sessions || [];



      if (sessionSelect) sessionSelect.innerHTML = '';

      if (sessionCardsList) sessionCardsList.innerHTML = '';



      sess.forEach(s => {

        const opt = document.createElement('option');

        opt.value = s.session_id;

        opt.textContent = `${s.session_id} (${s.task_count || 0} tasks)`;

        if (s.is_active) opt.selected = true;

        if (sessionSelect) sessionSelect.appendChild(opt);



        const card = document.createElement('div');

        card.className = `session-card ${s.is_active ? 'active' : ''}`;

        card.innerHTML = `

          <div style="font-weight:600; font-size:11px;">${escapeHtml(s.session_id)}</div>

          <div style="font-size:10px; color:#64748b;">${s.task_count || 0} tasks recorded</div>

        `;

        card.addEventListener('click', async () => {

          await fetch('/api/iba/sessions/select', {

            method: 'POST',

            headers: {'Content-Type':'application/json'},

            body: JSON.stringify({ session_id: s.session_id })

          });

          loadSessions();

        });

        if (sessionCardsList) sessionCardsList.appendChild(card);

      });



      if (data.active_session && chipSession) {

        chipSession.textContent = `💬 ${data.active_session}`;

      }



      // Sync with Windows Live Messenger Main Contact List & Persona

      if (window.WLMClient && window.WLMClient.renderContactList) {

        window.WLMClient.renderContactList(sess, data.active_session);

        const activeSessObj = sess.find(s => s.session_id === data.active_session);

        if (activeSessObj && activeSessObj.persona && window.WLMClient.switchChatToPersona) {

          window.WLMClient.switchChatToPersona(data.active_session, activeSessObj.persona);

        }

      }

      syncIbaBudgetButtons();



    } catch (e) {}

  }

  // MSN contact-list deletions refresh all other session surfaces.
  window.addEventListener('wlm-sessions-changed', () => { loadSessions(); });

  // Modals Wiring

  function openModal(modal) {
    if (!modal) return;
    modal.classList.add('active');
  }

  function closeModal(modal) {
    if (!modal) return;
    modal.classList.remove('active');
  }

  function getActiveScope() {
    const ws = workspaceSelect ? workspaceSelect.value : (window.currentActiveWorkspace?.path || '');
    let sess = sessionSelect && sessionSelect.value ? sessionSelect.value : '';
    if (!sess && window.WLMClient && window.WLMClient.getActiveSessionId) {
      sess = window.WLMClient.getActiveSessionId();
    }
    if (!sess) sess = window.currentActiveSessionId || 'default';
    return { ws, sess };
  }

  function openNewWorkspaceModal() {

    if (addWorkspaceModal) {

      openModal(addWorkspaceModal);

      if (newWsName) {

        newWsName.value = '';

        newWsName.focus();

      }

      if (newWsPath) newWsPath.value = '';

      if (newWsDesc) newWsDesc.value = '';

    }

  }



  if (openAddWorkspaceModalBtn) {

    openAddWorkspaceModalBtn.addEventListener('click', openNewWorkspaceModal);

  }

  if (sessionAddWsBtn) {

    sessionAddWsBtn.addEventListener('click', openNewWorkspaceModal);

  }

  document.querySelectorAll('[data-close]').forEach(btn => {

    btn.addEventListener('click', () => {

      const mid = btn.dataset.close;

      const m = document.getElementById(mid);

      if (m) closeModal(m);

    });

  });



  if (saveWorkspaceBtn) {

    saveWorkspaceBtn.addEventListener('click', async () => {

      const name = newWsName.value.trim();

      const path = newWsPath.value.trim();

      const initBlank = document.getElementById('newWsInitBlank') ? document.getElementById('newWsInitBlank').checked : true;

      if (!name) return alert('Please enter a Project Name.');



      try {

        saveWorkspaceBtn.disabled = true;

        saveWorkspaceBtn.textContent = 'Creating...';

        const resp = await fetch('/api/iba/workspaces/add', {

          method: 'POST',

          headers: {'Content-Type': 'application/json'},

          body: JSON.stringify({

            name,

            path,

            description: newWsDesc.value.trim(),

            init_blank: initBlank

          })

        });

        const data = await resp.json();

        if (resp.ok) {

          closeModal(addWorkspaceModal);

          const newPath = data.active_workspace || (data.workspace && data.workspace.path);
          await loadWorkspaces();

          const newTarget = (window._cachedWorkspaces || []).find(w => w.path === newPath);
          if (newTarget) bootWorkstation(newTarget);

          if (typeof loadWorkspaceFiles === 'function') {

            loadWorkspaceFiles();

          }

        } else {

          alert('Failed to add workspace: ' + (data.detail || 'Unknown error'));

        }

      } catch (err) {

        alert('Network error adding workspace: ' + err.message);

      } finally {

        saveWorkspaceBtn.disabled = false;

        saveWorkspaceBtn.textContent = 'Save & Switch';

      }

    });

  }



  // -------------------------------------------------------------------------
  // Pinned Rules, Code Map & Task Inspector Modals
  // -------------------------------------------------------------------------

  async function loadPins() {
    if (!pinsListContainer) return;
    const { ws, sess } = getActiveScope();
    pinsListContainer.innerHTML = '<span style="color:var(--text-muted);">Loading pinned constraints...</span>';
    try {
      const params = new URLSearchParams({ workspace: ws, session: sess });
      const resp = await fetch(`/api/iba/policy?${params}`);
      const data = await resp.json();
      const pins = data.pins || [];
      if (!pins.length) {
        pinsListContainer.innerHTML = '<span style="color:var(--text-muted);">No pinned constraints yet.</span>';
        return;
      }
      pinsListContainer.innerHTML = pins.map(p => `
        <div style="display:flex; gap:8px; padding:6px 0; border-bottom:1px solid rgba(255,255,255,0.06);">
          <span style="color:var(--nvidia-green);">📌</span>
          <span style="flex:1; white-space:pre-wrap;">${escapeHtml(p.body || '')}</span>
        </div>
      `).join('');
    } catch (err) {
      pinsListContainer.innerHTML = `<span style="color:#dc2626;">Failed to load pins: ${escapeHtml(err.message)}</span>`;
    }
  }

  async function savePin() {
    if (!newPinInput) return;
    const text = newPinInput.value.trim();
    if (!text) return alert('Please enter a constraint to pin.');
    const { ws, sess } = getActiveScope();
    savePinBtn.disabled = true;
    savePinBtn.textContent = 'Pinning...';
    try {
      const params = new URLSearchParams({ workspace: ws, session: sess });
      const resp = await fetch(`/api/iba/pin?${params}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ constraint: text })
      });
      const data = await resp.json().catch(() => ({}));
      if (!resp.ok) throw new Error(data.detail || 'Unable to pin constraint');
      newPinInput.value = '';
      await loadPins();
    } catch (err) {
      alert('Failed to pin constraint: ' + err.message);
    } finally {
      savePinBtn.disabled = false;
      savePinBtn.textContent = '📌 Pin Constraint to SQLite';
    }
  }

  async function loadCodeMap() {
    if (!codeMapListContainer) return;
    const { ws, sess } = getActiveScope();
    codeMapListContainer.innerHTML = '<span style="color:var(--text-muted);">Loading code map...</span>';
    try {
      const params = new URLSearchParams({ workspace: ws, session: sess });
      const resp = await fetch(`/api/iba/codemap?${params}`);
      const data = await resp.json();
      const entries = data.entries || [];
      if (codeMapCountBadge) codeMapCountBadge.textContent = entries.length;
      if (sessionCodeMapCount) sessionCodeMapCount.textContent = entries.length;
      if (!entries.length) {
        codeMapListContainer.innerHTML = '<span style="color:var(--text-muted);">No code files indexed for this session yet.</span>';
        return;
      }
      codeMapListContainer.innerHTML = entries.map(entry => {
        const symbols = entry.symbols || {};
        const chipValues = []
          .concat((symbols.classes || []).map(x => `class ${x}`))
          .concat((symbols.structs || []).map(x => `struct ${x}`))
          .concat((symbols.methods || []).slice(0, 12).map(x => `${x}()`))
          .slice(0, 24);
        const chips = chipValues.map(x => `<span style="background:rgba(118,185,0,0.12); color:#a3e635; padding:1px 6px; border-radius:8px; font-size:10px;">${escapeHtml(x)}</span>`).join(' ');
        const stale = entry.stale_on_disk ? '<span style="color:#f59e0b; font-size:10px; font-weight:700;">STALE ON DISK</span>' : '';
        return `
          <div style="background:var(--bg-input); border:1px solid var(--border-color); border-radius:6px; padding:10px;">
            <div style="display:flex; justify-content:space-between; gap:10px; align-items:center;">
              <code style="color:var(--cyan-accent); font-size:11px; word-break:break-all;">${escapeHtml(entry.path)}</code>
              ${stale}
            </div>
            <div style="font-size:11px; color:var(--text-secondary); margin:6px 0;">${escapeHtml(entry.summary || 'No summary indexed')}</div>
            <div style="display:flex; flex-wrap:wrap; gap:4px;">${chips || '<span style="color:var(--text-muted); font-size:10px;">No symbols indexed</span>'}</div>
            <div style="font-size:10px; color:var(--text-muted); margin-top:6px;">${entry.total_lines || 0} lines • ${entry.total_chars || 0} chars</div>
          </div>
        `;
      }).join('');
    } catch (err) {
      codeMapListContainer.innerHTML = `<span style="color:#dc2626;">Failed to load code map: ${escapeHtml(err.message)}</span>`;
    }
  }

  async function loadTaskDetails(taskId) {
    if (!taskStepsSection || !stepsListContainer) return;
    const { ws } = getActiveScope();
    taskStepsSection.style.display = 'block';
    if (currentInspectedTaskId) currentInspectedTaskId.textContent = taskId;
    stepsListContainer.innerHTML = '<span style="color:var(--text-muted); font-size:11px;">Loading steps...</span>';
    try {
      const params = new URLSearchParams({ workspace: ws });
      const resp = await fetch(`/api/iba/tasks/${encodeURIComponent(taskId)}?${params}`);
      const data = await resp.json();
      const steps = data.steps || [];
      if (!steps.length) {
        stepsListContainer.innerHTML = '<span style="color:var(--text-muted); font-size:11px;">No steps recorded for this task yet.</span>';
        return;
      }
      stepsListContainer.innerHTML = steps.map((step, idx) => {
        const action = step.action_name ? `${step.action_name}(${JSON.stringify(step.action_args || {})})` : 'Planning';
        const resultText = step.action_result_body || '';
        const stepNumber = idx + 1;
        return `
          <div style="background:var(--bg-input); border:1px solid var(--border-color); border-radius:5px; padding:8px;">
            <div style="display:flex; justify-content:space-between; gap:8px; font-size:11px; font-weight:700;">
              <span>${stepNumber}. ${escapeHtml(step.step_title || action)}</span>
              <span style="color:var(--text-muted);">${escapeHtml(step.action_state || 'completed')}</span>
            </div>
            <div style="font-family:var(--font-mono); font-size:10px; color:var(--cyan-accent); margin-top:4px; word-break:break-all;">${escapeHtml(action)}</div>
            ${resultText ? `<pre style="margin:6px 0 0; max-height:120px; overflow:auto; font-size:10px; background:#070a0f; border-radius:4px; padding:6px;">${escapeHtml(resultText.slice(0, 2000))}</pre>` : ''}
          </div>
        `;
      }).join('');
    } catch (err) {
      stepsListContainer.innerHTML = `<span style="color:#dc2626; font-size:11px;">Failed to load steps: ${escapeHtml(err.message)}</span>`;
    }
  }

  async function loadTasks() {
    if (!tasksTableBody) return;
    const { ws, sess } = getActiveScope();
    tasksTableBody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted);">Loading tasks...</td></tr>';
    try {
      const params = new URLSearchParams({ workspace: ws, session: sess });
      const resp = await fetch(`/api/iba/tasks?${params}`);
      const data = await resp.json();
      const tasks = data.tasks || [];
      if (taskCountBadge) taskCountBadge.textContent = tasks.length;
      if (sessionTaskCount) sessionTaskCount.textContent = tasks.length;
      if (!tasks.length) {
        tasksTableBody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted);">No tasks recorded in this session.</td></tr>';
        return;
      }
      tasksTableBody.innerHTML = '';
      tasks.forEach(task => {
        const tr = document.createElement('tr');
        const created = task.created ? new Date(task.created * 1000).toLocaleString() : '--';
        tr.innerHTML = `
          <td><code style="font-size:10px;">${escapeHtml(task.id.slice(0, 10))}…</code></td>
          <td><span style="font-size:10px; font-weight:700; color:${task.status === 'completed' ? '#16a34a' : '#f59e0b'};">${escapeHtml(task.status || 'ready')}</span></td>
          <td title="${escapeHtml(task.objective_preview || '')}" style="max-width:300px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;">${escapeHtml(task.title || task.objective_preview || 'Untitled task')}</td>
          <td style="font-size:10px; color:var(--text-muted);">${escapeHtml(created)}</td>
          <td><button type="button" class="btn btn-secondary btn-sm" style="padding:2px 6px; font-size:10px;" data-task-id="${escapeHtml(task.id)}">Inspect</button></td>
        `;
        tr.querySelector('[data-task-id]').addEventListener('click', () => loadTaskDetails(task.id));
        tasksTableBody.appendChild(tr);
      });
    } catch (err) {
      tasksTableBody.innerHTML = `<tr><td colspan="5" style="text-align:center; color:#dc2626;">Failed to load tasks: ${escapeHtml(err.message)}</td></tr>`;
    }
  }

  if (openPinModalBtn) openPinModalBtn.addEventListener('click', () => { openModal(pinModal); loadPins(); });
  if (sessionPinRuleBtn) sessionPinRuleBtn.addEventListener('click', () => { openModal(pinModal); loadPins(); });
  if (openCodeMapModalBtn) openCodeMapModalBtn.addEventListener('click', () => { openModal(codeMapModal); loadCodeMap(); });
  if (sessionCodeMapBtn) sessionCodeMapBtn.addEventListener('click', () => { openModal(codeMapModal); loadCodeMap(); });
  if (openInspectorModalBtn) openInspectorModalBtn.addEventListener('click', () => { openModal(inspectorModal); loadTasks(); });
  if (sessionTaskLogBtn) sessionTaskLogBtn.addEventListener('click', () => { openModal(inspectorModal); loadTasks(); });
  if (savePinBtn) savePinBtn.addEventListener('click', savePin);
  if (refreshCodeMapBtn) refreshCodeMapBtn.addEventListener('click', loadCodeMap);
  if (refreshInspectorBtn) refreshInspectorBtn.addEventListener('click', loadTasks);
  if (closeStepDetailsBtn) closeStepDetailsBtn.addEventListener('click', () => { if (taskStepsSection) taskStepsSection.style.display = 'none'; });

  document.querySelectorAll('.modal-backdrop').forEach(modal => {
    modal.addEventListener('click', (e) => {
      if (e.target === modal) closeModal(modal);
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      document.querySelectorAll('.modal-backdrop.active').forEach(modal => closeModal(modal));
    }
  });


  // Server Engine Polling

  let engineControlsSynced = false;

  async function pollStatus() {

    try {

      const resp = await fetch('/api/status');

      const data = await resp.json();

      if (!engineControlsSynced && data.config) {

        engineControlsSynced = true;

        syncEngineControls(data.config);

      }

      if (data.status === 'RUNNING') {

        statusDot.className = 'status-dot running';

        statusText.textContent = 'Engine Active';

      } else {

        statusDot.className = 'status-dot stopped';

        statusText.textContent = data.status || 'Stopped';

      }

    } catch (e) {

      statusDot.className = 'status-dot error';

      statusText.textContent = 'Offline';

    }

  }



  async function pollActivity() {

    try {

      const resp = await fetch(`/api/activity?since=${lastLogId}`);

      const data = await resp.json();

      const act = data.activity || {};

      const isBusy = act.is_busy;



      if (isBusy) {

        activityPulseDot.classList.add('busy');

        drawerPulseDot.classList.add('busy');

        const sp = act.gen_speed > 0 ? act.gen_speed : (act.prompt_speed || 0);

        activityText.textContent = sp > 0 ? `${sp.toFixed(1)} t/s` : 'Active';

      } else {

        activityPulseDot.classList.remove('busy');

        drawerPulseDot.classList.remove('busy');

        activityText.textContent = data.status === 'RUNNING' ? 'Idle' : data.status;

      }



      if (data.logs && data.logs.length > 0) {

        data.logs.forEach(l => {

          const div = document.createElement('div');

          div.className = `terminal-line ${l.type || 'info'}`;

          div.innerHTML = `<span class="t-time">[${escapeHtml(l.timestamp)}]</span> ${escapeHtml(l.text)}`;

          terminalContainer.appendChild(div);

        });

        if (autoScrollLogs) terminalContainer.scrollTop = terminalContainer.scrollHeight;

      }

      if (data.last_id) lastLogId = data.last_id;

    } catch (e) {}

  }



  setInterval(pollStatus, 5000);

  setInterval(pollActivity, 1000);



  // Markdown Render Helper

  function renderMarkdown(elem, text) {

    if (!text) { elem.innerHTML = ''; return; }

    // Get the current bot name for reasoning dropdown
    let botName = 'IBA';
    if (window.WLMClient && window.WLMClient.getActivePersona) {
      const persona = window.WLMClient.getActivePersona();
      if (persona) {
        botName = persona.name || persona.first_name || 'IBA';
      }
    }

    // Transform <think>...</think> into retro Windows XP collapsible details block

    let processed = text;

    const thinkRegex = /<think>([\s\S]*?)(?:<\/think>|$)/gi;

    processed = processed.replace(thinkRegex, (match, thoughtContent) => {

      const tokens = thoughtContent.trim().split(/\s+/).filter(Boolean).length;

      return `<details class="wlm-thought-dropdown" open>

        <summary class="wlm-thought-summary">

          <img src="/static/xp_assets/icons/shell32_24.png" width="14" height="14" alt="Think">

          <span>${botName} Cognitive Reasoning (${tokens} tokens)</span>

        </summary>

        <div class="wlm-thought-body">${escapeHtml(thoughtContent.trim())}</div>

      </details>`;

    });



    if (window.marked) {

      elem.innerHTML = marked.parse(processed);

      elem.querySelectorAll('pre code').forEach(block => {

        if (window.hljs) hljs.highlightElement(block);

      });

    } else {

      elem.innerHTML = processed;

    }

  }



  function escapeHtml(str) {

    return (str || '').toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

  }



  // Mount the Persistent Unified Desktop on page load

  window.unifiedDesktopManager = buildUnifiedXpDesktop();

  loadWorkspaces();

  pollStatus();

  pollActivity();

});



