with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

target = "const smRunEl = startMenu.querySelector('#smRun');"
wires = '''const smSearch = startMenu.querySelector('#smSearch');
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

    const smRunEl = startMenu.querySelector('#smRun');'''

if target in text and 'const smSearch =' not in text:
    text = text.replace(target, wires, 1)
    with open('gui/static/app.js', 'w', encoding='utf-8') as f:
        f.write(text)
    print('Successfully wired smSearch, piAutorun, and piSetupWiz!')
else:
    print('Target not found or already wired!')
