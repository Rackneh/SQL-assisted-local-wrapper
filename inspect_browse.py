with open('gui/app.py', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

pos = text.find('# 2. Fictional Windows XP C: Drive')
print(text[pos+1000:pos+3000])
