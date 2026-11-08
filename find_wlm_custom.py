with open('gui/static/app.js', 'r', encoding='utf-8') as f:
    text = f.read()

pos = text.find('sceneGrid')
print(text[pos+500:pos+2000])
