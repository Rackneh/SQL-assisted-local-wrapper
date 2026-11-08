with open('gui/app.py', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

pos = text.find('/api/iba/fs/read')
print(text[pos:pos+1500])
