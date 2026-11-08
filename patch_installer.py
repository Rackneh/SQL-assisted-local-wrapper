with open('gui/static/js/xp_installer.js', 'r', encoding='utf-8') as f:
    raw = f.read()

has_crlf = '\r\n' in raw
code = raw.replace('\r\n', '\n')

old_features = '''      // Cycle billboard features
      const features = [
        { title: "An exciting new look", text: "Windows XP Professional sports a fresh visual design that makes common tasks easier and puts your most important files within easy reach." },
        { title: "The software you need", text: "Windows XP Professional comes with rich communications tools, Windows Live Messenger integration, and built-in multimedia." },
        { title: "Superior performance and stability", text: "Built on the rock-solid Windows 2000 engine, Windows XP delivers enhanced multitasking and bulletproof memory isolation." }
      ];'''

new_features = '''      // Cycle billboard features extracted directly from WINNTBBA.DLL
      const features = [
        { 
          title: "An exciting new look", 
          text: "Windows XP Professional sports a visual design that combines a sleek look, clean lines, and appealing colors with a task-oriented design and exceptionally streamlined navigation. The redesigned Start menu makes it easier to find important information and to access the programs you use most frequently." 
        },
        { 
          title: "The connected world", 
          text: "Windows XP makes it easy to share hardware, network drives, and files securely across your home or office. With integrated Internet Connection Sharing, Windows Firewall, and remote desktop access, staying connected has never been simpler." 
        },
        { 
          title: "Rock-solid reliability", 
          text: "Designed for maximum dependability and minimal restarts. Built on the proven NT 32-bit kernel architecture, Windows XP delivers bulletproof stability, protected memory architecture, and seamless system restore capabilities." 
        },
        { 
          title: "Advanced security and privacy", 
          text: "Windows XP gives you peace of mind with Encrypting File System (EFS), advanced user permission controls, and automatic updates to protect against security threats." 
        }
      ];'''

if old_features in code:
    code = code.replace(old_features, new_features, 1)
    print('Updated billboard features in xp_installer.js!')
else:
    print('old_features not found!')

old_oobe_step2 = '''          // Step 2: User Accounts Setup
          setupContainer.innerHTML = `
            <div class="xp-oobe-screen">
              <div class="xp-oobe-top">
                <img src="/static/xp_assets/logon/bmp_100.png" style="width:120px; height:auto; margin-right:16px;">
                <h2>Welcome to Microsoft Windows</h2>
              </div>
              <div class="xp-oobe-center">
                <div class="xp-oobe-card" style="max-width:540px;">
                  <h3>Who will use this computer?</h3>
                  <p style="font-size:12px; color:#334155;">Type the name of each person who will use this computer. A personalized user account will be created for each person.</p>
                  <div style="margin-top:14px; display:flex; flex-direction:column; gap:8px;">
                    <div style="display:flex; align-items:center; gap:12px;">
                      <label style="width:90px; font-weight:700; font-size:12px;">Your name:</label>
                      <input type="text" id="oobeUserNameInput" value="${currentConfig.userName}" style="padding:4px 8px; font-size:13px; font-weight:700; width:220px; border:1px solid #7f9db9;">
                    </div>
                    <div style="display:flex; align-items:center; gap:12px;">
                      <label style="width:90px; font-size:12px; color:#64748b;">User 2:</label>
                      <input type="text" id="oobeUser2" value="Dory AI Bot" style="padding:3px 6px; font-size:12px; width:220px; border:1px solid #cbd5e1;">
                    </div>
                    <div style="display:flex; align-items:center; gap:12px;">
                      <label style="width:90px; font-size:12px; color:#64748b;">User 3:</label>
                      <input type="text" id="oobeUser3" placeholder="(Optional)" style="padding:3px 6px; font-size:12px; width:220px; border:1px solid #cbd5e1;">
                    </div>
                  </div>
                </div>
              </div>'''

new_oobe_step2 = '''          // Step 2: User Accounts Setup with User Account Tile Avatar Selector
          setupContainer.innerHTML = `
            <div class="xp-oobe-screen">
              <div class="xp-oobe-top">
                <img src="/static/xp_assets/logon/bmp_100.png" style="width:140px; height:auto; margin-right:16px; filter:drop-shadow(0 2px 6px rgba(0,0,0,0.4));">
                <h2 style="font-size:22px; font-weight:700; text-shadow:1px 1px 3px rgba(0,0,0,0.5);">Welcome to Microsoft Windows</h2>
              </div>
              <div class="xp-oobe-center">
                <div class="xp-oobe-card" style="max-width:580px;">
                  <h3 style="color:#003399; font-size:16px;">Who will use this computer?</h3>
                  <p style="font-size:12px; color:#334155; margin-top:4px;">Type the name of each person who will use this computer. A personalized user account will be created for each person. Click an avatar to choose from the authentic Windows XP account pictures.</p>
                  
                  <div style="margin-top:14px; display:flex; flex-direction:column; gap:12px;">
                    <div style="display:flex; align-items:center; gap:12px;">
                      <div class="xp-logon-avatar-box" id="oobeAvatarPickBtn" title="Click to change account tile" style="width:48px; height:48px; cursor:pointer; border:2px solid #0284c7; border-radius:4px; overflow:hidden; background:#000;">
                        <img id="oobeAvatarImg" src="${currentConfig.userTile || '/static/xp_assets/usertiles/dog.png'}" style="width:100%; height:100%; object-fit:cover;">
                      </div>
                      <div>
                        <div style="font-size:11px; color:#64748b;">Primary User Name:</div>
                        <input type="text" id="oobeUserNameInput" value="${currentConfig.userName}" style="padding:4px 8px; font-size:13px; font-weight:700; width:230px; border:1px solid #7f9db9;">
                      </div>
                    </div>
                    
                    <div style="display:flex; align-items:center; gap:12px;">
                      <div class="xp-logon-avatar-box" style="width:48px; height:48px; border:2px solid #cbd5e1; border-radius:4px; overflow:hidden; background:#000;">
                        <img src="/static/xp_assets/usertiles/duck.png" style="width:100%; height:100%; object-fit:cover;">
                      </div>
                      <div>
                        <div style="font-size:11px; color:#64748b;">User 2 (AI Assistant):</div>
                        <input type="text" id="oobeUser2" value="Dory AI Bot" style="padding:3px 6px; font-size:12px; width:230px; border:1px solid #cbd5e1;">
                      </div>
                    </div>

                    <div style="display:flex; align-items:center; gap:12px;">
                      <div class="xp-logon-avatar-box" style="width:48px; height:48px; border:2px solid #cbd5e1; border-radius:4px; overflow:hidden; background:#000;">
                        <img src="/static/xp_assets/usertiles/chess.png" style="width:100%; height:100%; object-fit:cover;">
                      </div>
                      <div>
                        <div style="font-size:11px; color:#64748b;">User 3 (Optional):</div>
                        <input type="text" id="oobeUser3" placeholder="Guest / Colleague" style="padding:3px 6px; font-size:12px; width:230px; border:1px solid #cbd5e1;">
                      </div>
                    </div>
                  </div>

                  <!-- Inline Avatar Selector Grid Modal -->
                  <div id="oobeAvatarModal" style="display:none; margin-top:14px; padding:10px; background:#f1f5f9; border:1px solid #94a3b8; border-radius:4px;">
                    <div style="font-size:11px; font-weight:700; margin-bottom:8px;">Choose an authentic Windows XP user account tile:</div>
                    <div style="display:grid; grid-template-columns:repeat(8, 1fr); gap:6px; max-height:120px; overflow-y:auto;" id="oobeTileGrid"></div>
                  </div>
                </div>
              </div>'''

if old_oobe_step2 in code:
    code = code.replace(old_oobe_step2, new_oobe_step2, 1)
    print('Updated OOBE step 2 in xp_installer.js!')
else:
    print('old_oobe_step2 not found!')

old_wire = "setupContainer.querySelector('#btnBackOOBE2').onclick = () => {"
new_wire = '''const avModal = setupContainer.querySelector('#oobeAvatarModal');
          const avBtn = setupContainer.querySelector('#oobeAvatarPickBtn');
          if (avBtn && avModal) {
            avBtn.onclick = () => {
              avModal.style.display = avModal.style.display === 'none' ? 'block' : 'none';
              const grid = avModal.querySelector('#oobeTileGrid');
              if (grid && !grid.hasChildNodes()) {
                const sampleTiles = ['dog.png', 'duck.png', 'chess.png', 'car.png', 'guitar.png', 'butterfly.png', 'airplane.png', 'astro.png', 'beach.png', 'cat.png', 'horses.png', 'palmtree.png', 'soccer.png', 'skater.png', 'snwflake.png', 'frog.png'];
                sampleTiles.forEach(tileName => {
                  const tImg = document.createElement('img');
                  tImg.src = '/static/xp_assets/usertiles/' + tileName;
                  tImg.style.cssText = 'width:36px; height:36px; cursor:pointer; border:1px solid #7f9db9; border-radius:2px; object-fit:cover;';
                  tImg.onclick = () => {
                    currentConfig.userTile = '/static/xp_assets/usertiles/' + tileName;
                    setupContainer.querySelector('#oobeAvatarImg').src = currentConfig.userTile;
                    avModal.style.display = 'none';
                  };
                  grid.appendChild(tImg);
                });
              }
            };
          }

          setupContainer.querySelector('#btnBackOOBE2').onclick = () => {'''

if old_wire in code:
    code = code.replace(old_wire, new_wire, 1)
    print('Added avatar picker wiring to xp_installer.js!')
else:
    print('old_wire not found!')

if has_crlf:
    code = code.replace('\n', '\r\n')

with open('gui/static/js/xp_installer.js', 'w', encoding='utf-8') as f:
    f.write(code)

print('Saved normalized xp_installer.js!')
