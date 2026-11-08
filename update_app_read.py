with open('gui/app.py', 'r', encoding='utf-8') as f:
    code = f.read()

target = '# 3. Real Project File on D:'
cd_read = '''    # 2b. Windows XP CD-ROM File Read (E:)
    if clean.upper().startswith("E:"):
        rel = clean[2:].strip("/")
        cd_disc_path = Path(r"D:\\Software\\Windows\\Windows\\Windows.XP.Professional.SP3.x86")
        target_f = cd_disc_path / rel
        if target_f.is_file():
            try:
                content = target_f.read_text(encoding="utf-8", errors="replace")
                return {
                    "path": clean,
                    "size": target_f.stat().st_size,
                    "content": content,
                    "virtual": False,
                    "origin": "Windows XP Professional SP3 Installation Disc"
                }
            except Exception as e:
                raise HTTPException(status_code=500, detail=str(e))
        else:
            raise HTTPException(status_code=404, detail="File not found on CD-ROM")

    '''

if target in code and '2b. Windows XP CD-ROM File Read' not in code:
    code = code.replace(target, cd_read + target, 1)
    with open('gui/app.py', 'w', encoding='utf-8') as f:
        f.write(code)
    print('Added CD-ROM file read support to gui/app.py!')
else:
    print('Already present or target not found.')
