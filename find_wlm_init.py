with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

pos1 = text.find("wlmMainWindow = document.createElement('div')")
if pos1 == -1:
    pos1 = text.find('unifiedWlmMainWindow')
print('wlmMainWindow:', text[pos1:pos1+400])

pos2 = text.find("wlmWindow = document.createElement('div')")
if pos2 == -1:
    pos2 = text.find('unifiedWlmWindow')
print('wlmWindow:', text[pos2:pos2+400])
