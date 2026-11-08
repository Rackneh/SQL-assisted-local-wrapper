with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Add smSearch above smRun
target_search = '''<div class="xp-start-item" id="smRun"'''
replacement_search = '''<div class="xp-start-item" id="smSearch" style="display:flex; align-items:center; gap:6px;"><img src="/static/xp_assets/icons/search.png" width="18" height="18"> Search</div>\n\n          <div class="xp-start-item" id="smRun"'''

if target_search in text and 'id="smSearch"' not in text:
    text = text.replace(target_search, replacement_search, 1)
    print('Added smSearch to Start Menu HTML!')

# 2. Add piAutorun and piSetupWiz to smProgramsFlyout
target_pi = '''<div class="xp-prog-item" id="piExplorer">'''
replacement_pi = '''<div class="xp-prog-item" id="piAutorun"><img src="/static/xp_assets/icons/shell32_11.png" width="16" height="16"> <span>Windows XP Setup CD</span></div>\n\n              <div class="xp-prog-item" id="piSetupWiz"><img src="/static/xp_assets/icons/shell32_22.png" width="16" height="16"> <span>Reinstall / Setup Wizard</span></div>\n\n              <div class="xp-prog-item" id="piExplorer">'''

if target_pi in text and 'id="piAutorun"' not in text:
    text = text.replace(target_pi, replacement_pi, 1)
    print('Added piAutorun and piSetupWiz to All Programs!')

# 3. Wire smSearch, piAutorun, piSetupWiz
wire_target = "const smRun = startMenu.querySelector('#smRun');"
wire_replacement = '''const smSearch = startMenu.querySelector('#smSearch');

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



    const smRun = startMenu.querySelector('#smRun');'''

if wire_target in text and 'const smSearch =' not in text:
    text = text.replace(wire_target, wire_replacement, 1)
    print('Wired smSearch, piAutorun, and piSetupWiz in Start Menu!')

with open('gui/static/app.js', 'w', encoding='utf-8') as f:
    f.write(text)

print('Saved gui/static/app.js!')
