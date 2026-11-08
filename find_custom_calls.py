with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

pos = 0
while True:
    pos = text.find('openCustomWindow', pos)
    if pos == -1:
        break
    print('--- openCustomWindow call ---')
    print(text[max(0, pos-100):min(len(text), pos+200)])
    pos += len('openCustomWindow')

pos = 0
while True:
    pos = text.find('customWin', pos)
    if pos == -1:
        break
    if 'style.display' in text[pos:pos+50]:
        print('--- customWin.style.display ---')
        print(text[pos:pos+100])
    pos += len('customWin')
