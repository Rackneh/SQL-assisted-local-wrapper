import re

with open('gui/app.py', 'r', encoding='utf-8') as f:
    code = f.read()

# Update CD Drive name in drives list
old_cd = '{"name": "CD Drive (E:)",'
new_cd = '{"name": "CD Drive (E:) Windows XP Professional SP3",'
if old_cd in code:
    code = code.replace(old_cd, new_cd, 1)
    print('Updated CD Drive name in drives list!')

# Add E: handler right before # 3. Linux Sandbox VM Network Drive (Z:)
target_anchor = '# 3. Linux Sandbox VM Network Drive (Z:)'
e_drive_handler = '''    # 2b. Windows XP CD-ROM Installation Disc (E:)
    if norm.upper().startswith("E:"):
        rel_sub = norm[2:].strip("/").replace("\\\\", "/")
        cd_disc_path = Path(r"D:\\Software\\Windows\\Windows\\Windows.XP.Professional.SP3.x86")
        target_dir = (cd_disc_path / rel_sub) if rel_sub else cd_disc_path
        items = []
        if target_dir.exists() and target_dir.is_dir():
            try:
                for entry in target_dir.iterdir():
                    rel_p = f"E:/{rel_sub}/{entry.name}".replace("//", "/") if rel_sub else f"E:/{entry.name}"
                    items.append({
                        "name": entry.name,
                        "path": rel_p,
                        "is_dir": entry.is_dir(),
                        "size": entry.stat().st_size if entry.is_file() else 0,
                        "ext": entry.suffix.lower() if entry.is_file() else ""
                    })
            except Exception:
                pass
        parent = ("E:/" + "/".join(rel_sub.split("/")[:-1])) if "/" in rel_sub else ("E:" if rel_sub else "My Computer")
        return {
            "mode": "cdrom",
            "workspace_root": "E:/",
            "current_path": f"E:/{rel_sub}" if rel_sub else "E:",
            "full_current_path": f"E:\\\\{rel_sub.replace('/', '\\\\')}" if rel_sub else "E:\\\\",
            "parent_path": parent,
            "items": sorted(items, key=lambda x: (not x["is_dir"], x["name"].lower()))
        }

    '''

if target_anchor in code and '2b. Windows XP CD-ROM Installation Disc' not in code:
    code = code.replace(target_anchor, e_drive_handler + target_anchor, 1)
    print('Added E: CD-ROM drive handler to browse_workspace_files!')

with open('gui/app.py', 'w', encoding='utf-8') as f:
    f.write(code)

print('Saved gui/app.py!')
