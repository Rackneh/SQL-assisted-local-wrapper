with open('gui/app.py', 'r', encoding='utf-8') as f:
    code = f.read()

target = 'app.mount("/static", StaticFiles(directory="gui/static"), name="static")'
route_code = '''
@app.get("/favicon.ico", include_in_schema=False)
async def get_favicon():
    fav = Path("gui/static/favicon.ico")
    if fav.exists():
        return FileResponse(str(fav), media_type="image/x-icon")
    raise HTTPException(status_code=404, detail="Favicon not found")
'''

if target in code and 'get_favicon' not in code:
    code = code.replace(target, target + '\n' + route_code, 1)
    with open('gui/app.py', 'w', encoding='utf-8') as f:
        f.write(code)
    print('Added /favicon.ico route to gui/app.py!')
else:
    print('Already present or target not found')
