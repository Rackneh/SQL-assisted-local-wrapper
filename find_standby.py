with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

pos = 0
while True:
    pos = text.find('xpStandbyScreen', pos)
    if pos == -1:
        break
    print('--- xpStandbyScreen ---')
    print(text[max(0, pos-50):min(len(text), pos+300)])
    pos += 15
