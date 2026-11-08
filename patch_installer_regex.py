import re

with open('gui/static/js/xp_installer.js', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. Replace features array
feat_pattern = r'const features = \[\s*\{ title: "An exciting new look".*?\];'
new_features = '''const features = [
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

if re.search(feat_pattern, code, re.DOTALL):
    code = re.sub(feat_pattern, new_features, code, flags=re.DOTALL)
    print('Updated features array successfully!')
else:
    print('Could not find feat_pattern')

# 2. Update OOBE step 2
oobe_pattern = r'(else if \(oobeStep === 2\) \{.*?)(setupContainer\.innerHTML = `.*?)(setupContainer\.querySelector\(\'#btnBackOOBE2\'\)\.onclick = \(\) => \{)'

new_oobe_content = '''setupContainer.innerHTML = `
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
              </div>
              <div class="xp-oobe-bottom">
                <button class="btn btn-secondary btn-sm" id="btnBackOOBE2">&lt; Back</button>
                <button class="xp-oobe-btn-finish" id="btnNextOOBE2">Finish &gt;</button>
              </div>
            </div>
          `;

          const avModal = setupContainer.querySelector('#oobeAvatarModal');
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
          '''

m = re.search(oobe_pattern, code, re.DOTALL)
if m:
    code = code[:m.start(2)] + new_oobe_content + '\n' + code[m.start(3):]
    print('Updated OOBE step 2 successfully!')
else:
    print('Could not find oobe_pattern')

with open('gui/static/js/xp_installer.js', 'w', encoding='utf-8') as f:
    f.write(code)

print('Saved xp_installer.js!')
