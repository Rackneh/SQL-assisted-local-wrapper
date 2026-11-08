with open('gui/static/js/xp_apps.js', 'r', encoding='utf-8') as f:
    text = f.read()

pos = text.find('function openDisplayProperties')
pos_end = text.find('function openTaskManager')
print(text[pos:pos+2000])
