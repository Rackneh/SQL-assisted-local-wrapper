with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

pos = 0
while True:
    pos = text.find('workspaceSelect', pos)
    if pos == -1:
        break
    print('--- workspaceSelect ---')
    print(text[max(0, pos-50):min(len(text), pos+200)])
    pos += 15
