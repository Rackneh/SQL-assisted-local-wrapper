with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

target = '''loadExplorerDirectory(f.path);

        } else {

          openFileInNotepad(f.path);'''

replacement = '''loadExplorerDirectory(f.path);

        } else if (f.name && (f.name.toLowerCase() === 'setup.exe' || f.name.toLowerCase() === 'autorun.inf')) {

          if (window.XPApps && window.XPApps.openAutorun) window.XPApps.openAutorun();

        } else if (f.name && f.name.toLowerCase() === 'pinball.exe') {

          if (window.XPGames && window.XPGames.openPinball) window.XPGames.openPinball();

        } else if (f.name && f.name.toLowerCase() === 'winmine.exe') {

          if (window.XPGames && window.XPGames.openMinesweeper) window.XPGames.openMinesweeper();

        } else {

          openFileInNotepad(f.path);'''

if target in text:
    text = text.replace(target, replacement, 1)
    with open('gui/static/app.js', 'w', encoding='utf-8') as f:
        f.write(text)
    print('Replaced dblclick in Explorer successfully!')
else:
    print('Target not found for dblclick')
