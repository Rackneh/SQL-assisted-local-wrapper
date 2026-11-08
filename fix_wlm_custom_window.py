with open('gui/static/style.css', 'r', encoding='utf-8') as f:
    css = f.read()

# Replace .wlm-customization-window in style.css
old_css = '''.wlm-customization-window {
  position: absolute;
  left: 60px;
  top: 40px;
  width: 440px;
  height: 520px;
  background: #ece9d8;
  border: 3px solid #0055ea;
  border-radius: 8px 8px 0 0;
  box-shadow: 0 12px 35px rgba(0, 0, 0, 0.6);
  z-index: 10000;
  display: none;
  flex-direction: column;
  overflow: hidden;
  font-family: 'Tahoma', 'Segoe UI', sans-serif;
  font-size: 11px;
}'''

new_css = '''.wlm-customization-window {
  position: absolute;
  left: 60px;
  top: 40px;
  width: 440px;
  height: 520px;
  background: #ece9d8;
  border: 3px solid #0055ea;
  border-radius: 8px 8px 0 0;
  box-shadow: 0 12px 35px rgba(0, 0, 0, 0.6);
  z-index: 35;
  display: none !important;
  flex-direction: column;
  overflow: hidden;
  font-family: 'Tahoma', 'Segoe UI', sans-serif;
  font-size: 11px;
}

.wlm-customization-window.open {
  display: flex !important;
}'''

if old_css in css:
    css = css.replace(old_css, new_css, 1)
    print('Updated .wlm-customization-window in style.css!')
else:
    # Try normalized newlines
    css_norm = css.replace('\r\n', '\n')
    old_css_norm = old_css.replace('\r\n', '\n')
    new_css_norm = new_css.replace('\r\n', '\n')
    if old_css_norm in css_norm:
        css = css_norm.replace(old_css_norm, new_css_norm, 1)
        print('Updated .wlm-customization-window in style.css (normalized)!')
    else:
        print('old_css not found in style.css')

with open('gui/static/style.css', 'w', encoding='utf-8') as f:
    f.write(css)

# Update app.js
with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    app_js = f.read()

# 1. Ensure customWin starts hidden
old_create = "customWin.id = 'unifiedWlmCustomWindow';"
new_create = "customWin.id = 'unifiedWlmCustomWindow';\n    customWin.style.display = 'none'; // Hidden by default"

if old_create in app_js:
    app_js = app_js.replace(old_create, new_create, 1)
    print('Set customWin.style.display = none in app.js!')

# 2. Update openCustomWindow and closeCustomWindow
old_open_close = '''const openCustomWindow = () => {

      customWin.style.display = 'flex';

      bringToFront(customWin);

    };



    const closeCustomWindow = () => {

      customWin.style.display = 'none';

    };'''

new_open_close = '''const openCustomWindow = () => {

      customWin.classList.add('open');

      customWin.style.display = 'flex';

      bringToFront(customWin);

    };



    const closeCustomWindow = () => {

      customWin.classList.remove('open');

      customWin.style.display = 'none';

    };'''

if old_open_close in app_js:
    app_js = app_js.replace(old_open_close, new_open_close, 1)
    print('Updated openCustomWindow and closeCustomWindow in app.js!')
else:
    # Try with single newlines
    old_oc_single = '''const openCustomWindow = () => {
      customWin.style.display = 'flex';
      bringToFront(customWin);
    };

    const closeCustomWindow = () => {
      customWin.style.display = 'none';
    };'''
    new_oc_single = '''const openCustomWindow = () => {
      customWin.classList.add('open');
      customWin.style.display = 'flex';
      bringToFront(customWin);
    };

    const closeCustomWindow = () => {
      customWin.classList.remove('open');
      customWin.style.display = 'none';
    };'''
    if old_oc_single in app_js:
        app_js = app_js.replace(old_oc_single, new_oc_single, 1)
        print('Updated openCustomWindow and closeCustomWindow (single line)!')
    else:
        print('open_close not matched directly')

with open('gui/static/app.js', 'w', encoding='utf-8') as f:
    f.write(app_js)

print('Saved gui/static/app.js and gui/static/style.css!')
