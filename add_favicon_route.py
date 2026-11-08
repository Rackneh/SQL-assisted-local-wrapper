with open('gui/app.py', 'r', encoding='utf-8') as f:
    code = f.read()

target = 'app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")'
fav_code = '''
@app.get("/favicon.ico", include_in_schema=False)
async def get_favicon():
    fav = os.path.join(STATIC_DIR, "favicon.ico")
    if os.path.exists(fav):
        return FileResponse(fav, media_type="image/x-icon")
    raise HTTPException(status_code=404, detail="Favicon not found")
'''

if target in code and 'get_favicon' not in code:
    code = code.replace(target, target + '\n' + fav_code, 1)
    with open('gui/app.py', 'w', encoding='utf-8') as f:
        f.write(code)
    print('Successfully added /favicon.ico route!')
else:
    print('Target not found or already added')
