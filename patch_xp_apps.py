with open('gui/static/js/xp_apps.js', 'r', encoding='utf-8') as f:
    code = f.read()

# Check if openAutorun is already in xp_apps.js
if 'function openAutorun' not in code:
    autorun_and_search_code = '''
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
        alert("Microsoft Windows XP Compatibility Check\\n\\nAll components passed compatibility inspection.\\n\\n• Processor: Intel Pentium / Core compatible (Passed)\\n• Memory: 3.25 GB RAM detected (Passed)\\n• Video: DirectDraw / DirectX 9.0c hardware acceleration (Passed)\\n• Disk: 24.6 GB available space (Passed)\\n\\nYour computer is 100% ready for Windows XP.");
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
'''
    pos_exp = code.rfind('return {')
    code = code[:pos_exp] + autorun_and_search_code + '\n' + code[pos_exp:]
    # Add to exports
    code = code.replace('openDorySettings\n  };', 'openDorySettings,\n    openAutorun,\n    openSearchCompanion,\n    openSetupWizard\n  };')
    with open('gui/static/js/xp_apps.js', 'w', encoding='utf-8') as f:
        f.write(code)
    print('Updated xp_apps.js with openAutorun, openSearchCompanion, and openSetupWizard!')
else:
    print('Already present in xp_apps.js')
