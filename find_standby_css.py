with open('gui/static/style.css', 'r', encoding='utf-8') as f:
    text = f.read()

pos = text.find('.xp-standby-screen')
print(text[pos:pos+400])
