import pefile

dll_path = r'gui\static\xp_assets\syssetup\syssetup.dll'
pe = pefile.PE(dll_path)

strings = {}
for entry in pe.DIRECTORY_ENTRY_RESOURCE.entries:
    if entry.struct.Id == pefile.RESOURCE_TYPE['RT_STRING']:
        for s_entry in entry.directory.entries:
            block_id = s_entry.id
            data_rva = s_entry.directory.entries[0].data.struct.OffsetToData
            data = pe.get_memory_mapped_image()[data_rva:data_rva+s_entry.directory.entries[0].data.struct.Size]
            offset = 0
            for i in range(16):
                if offset >= len(data):
                    break
                length = int.from_bytes(data[offset:offset+2], 'little')
                offset += 2
                if length > 0:
                    str_data = data[offset:offset+length*2].decode('utf-16le', errors='ignore')
                    offset += length * 2
                    str_id = ((block_id - 1) * 16) + i
                    strings[str_id] = str_data

print(f'Total strings: {len(strings)}')
sample_keys = sorted(strings.keys())
for k in sample_keys[:100]:
    if any(term in strings[k].lower() for term in ['product', 'key', 'name', 'password', 'computer', 'setup', 'welcome', 'regional', 'network', 'workgroup', 'licens', 'install']):
        print(f'{k}: {strings[k]}')
