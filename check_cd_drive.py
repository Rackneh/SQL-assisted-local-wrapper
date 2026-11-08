with open('gui/app.py', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

pos = text.find('FICTIONAL_XP_C_DRIVE')
print('FICTIONAL_XP_C_DRIVE pos:', pos)
print(text[pos:pos+1200])

pos_e = text.find('startswith("E:')
print('E: handler pos:', pos_e)
if pos_e != -1:
    print(text[pos_e:pos_e+600])
