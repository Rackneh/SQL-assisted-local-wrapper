with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Add xpIconSetupCd and xpIconSearch into desktop icons
old_desktop_icons_anchor = '<div class="xp-desktop-icon" id="xpIconRecycle" title="Recycle Bin">'
new_desktop_icons = '''<div class="xp-desktop-icon" id="xpIconSetupCd" title="Windows XP Professional CD-ROM (SETUP.EXE)">
        <img src="/static/xp_assets/icons/shell32_11.png" class="xp-desktop-img" alt="XP Setup CD">
        <span class="xp-desktop-icon-label">XP Setup CD</span>
      </div>
      <div class="xp-desktop-icon" id="xpIconSearch" title="Search Companion (Find files with Rover)">
        <img src="/static/xp_assets/icons/search.png" class="xp-desktop-img" alt="Search">
        <span class="xp-desktop-icon-label">Search</span>
      </div>
      <div class="xp-desktop-icon" id="xpIconRecycle" title="Recycle Bin">'''

if old_desktop_icons_anchor in text and 'xpIconSetupCd' not in text:
    text = text.replace(old_desktop_icons_anchor, new_desktop_icons, 1)
    print('Added xpIconSetupCd and xpIconSearch to desktop icons!')

# 2. Wire desktop app icons
old_wire_anchor = "wireDesktopAppIcon('#xpIconWlm', () => {"
new_wire_calls = '''wireDesktopAppIcon('#xpIconSetupCd', () => {
      if (window.XPApps && window.XPApps.openAutorun) window.XPApps.openAutorun();
    });
    wireDesktopAppIcon('#xpIconSearch', () => {
      if (window.XPApps && window.XPApps.openSearchCompanion) window.XPApps.openSearchCompanion();
    });
    wireDesktopAppIcon('#xpIconWlm', () => {'''

if old_wire_anchor in text and '#xpIconSetupCd' not in text:
    text = text.replace(old_wire_anchor, new_wire_calls, 1)
    print('Wired xpIconSetupCd and xpIconSearch!')

# 3. Handle setup.exe / autorun.inf in Explorer double-click
old_exp_dblclick = '''item.addEventListener('dblclick', () => {
        if (f.is_dir) {
          loadExplorerDirectory(f.path);
        } else {
          openFileInNotepad(f.path);
        }
      });'''

new_exp_dblclick = '''item.addEventListener('dblclick', () => {
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
      });'''

if old_exp_dblclick in text:
    text = text.replace(old_exp_dblclick, new_exp_dblclick, 1)
    print('Enhanced Explorer file double-click handling!')

with open('gui/static/app.js', 'w', encoding='utf-8') as f:
    f.write(text)

print('Saved gui/static/app.js!')
