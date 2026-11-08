with open('gui/static/style.css', 'r', encoding='utf-8') as f:
    text = f.read()

pos = text.find('.xp-window {')
print(text[pos:pos+500])

pos2 = 0
while True:
    pos2 = text.find('unifiedWlmCustomWindow', pos2)
    if pos2 == -1:
        break
    print('--- unifiedWlmCustomWindow in style.css ---')
    print(text[pos2-50:pos2+200])
    pos2 += 22
