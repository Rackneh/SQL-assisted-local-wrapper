with open('gui/static/js/xp_apps.js', 'r', encoding='utf-8') as f:
    text = f.read()

target = "val === 'msnmsgr' || val === 'wlm' || val === 'messenger'"
new_code = '''val === 'msnmsgr' || val === 'wlm' || val === 'messenger') {

          const wlmWin = document.getElementById('unifiedWlmMainWindow');

          if (wlmWin) { wlmWin.style.display = 'flex'; wlmWin.classList.remove('minimized'); window.XPCore.bringToFront(wlmWin); }

        } else if (val === 'setup' || val === 'autorun' || val === 'setup.exe') {

          openAutorun();

        } else if (val === 'syssetup' || val === 'install' || val === 'oobe') {

          openSetupWizard();

        } else if (val === 'search' || val === 'find' || val === 'rover') {

          openSearchCompanion();'''

# Find the if block for msnmsgr
pos = text.find(target)
if pos != -1:
    pos_end = text.find('}', pos)
    if pos_end != -1 and 'openAutorun()' not in text[pos:pos+500]:
        text = text[:pos] + new_code + text[pos_end+1:]
        with open('gui/static/js/xp_apps.js', 'w', encoding='utf-8') as f:
            f.write(text)
        print('Added setup, oobe, and search commands to Run dialog!')
    else:
        print('Already present or pos_end not found')
else:
    print('Target not found')
