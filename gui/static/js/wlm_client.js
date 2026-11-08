/**
 * Windows Live Messenger Wave 4 Client Subsystem (wlm_client.js)
 * Implements: 2000s Procedural Personas, Contact List with Graphical Scene Strips,
 * Wave-4 Scenic Chat Header, Winks System (13 authentic winks + video overlay),
 * User Customization Modal, and Agentic Host Permission & Question Cards.
 */

window.WLMClient = (function() {
  let allSessionsData = [];
  let currentActiveSessionId = 'default';
  let attachedImages = [];

  let userProfile = {
    name: 'Rackneh',
    email: 'user@dory-workspace.local (Available)',
    avatar: '/static/xp_assets/usertiles/dog.bmp',
    banner: '/static/wlm/wlm/background_blue.jpg',
    status: 'Available'
  };

  // Synchronize with Windows XP Setup user identity
  try {
    if (window.XPInstaller && window.XPInstaller.getInstallConfig) {
      const cfg = window.XPInstaller.getInstallConfig();
      if (cfg && cfg.userName) {
        userProfile.name = cfg.userName;
        if (cfg.userTile) userProfile.avatar = cfg.userTile;
      }
    }
  } catch (e) {}

  try {
    const saved = localStorage.getItem('wlm_user_profile');
    if (saved) {
      userProfile = Object.assign(userProfile, JSON.parse(saved));
    }
  } catch (e) {}

  function escapeHtml(str) {
    return (str || '').toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function applyUserProfile() {
    const mainHeader = document.getElementById('wlmMainHeaderHandle');
    const mainDp = document.getElementById('wlmCurrentUserDp');
    const mainName = document.querySelector('.wlm-main-user-name');
    const mainPm = document.getElementById('wlmUserPmInput');
    const bottomChatDp = document.getElementById('wlmBottomUserDpImg');

    if (mainHeader && userProfile.banner) {
      mainHeader.style.backgroundImage = `url('${userProfile.banner}')`;
      mainHeader.style.backgroundSize = 'cover';
      mainHeader.style.backgroundPosition = 'center';
    }
    if (mainDp && userProfile.avatar) mainDp.src = userProfile.avatar;
    if (bottomChatDp && userProfile.avatar) bottomChatDp.src = userProfile.avatar;
    if (mainName) {
      mainName.innerHTML = `${escapeHtml(userProfile.name)} <span class="wlm-status-tag" id="wlmCurrentStatusTag">(${escapeHtml(userProfile.status)}) ▾</span>`;
    }
    if (mainPm) mainPm.value = userProfile.email;
  }

  // -------------------------------------------------------------------------
  // Winks Video Overlay System
  // -------------------------------------------------------------------------
  function playWink(winkId, senderName = 'You') {
    const overlay = document.getElementById('wlmWinkOverlay');
    if (!overlay) return;

    const winkExt = winkId === 'fartguy' ? 'webm' : 'mp4';
    const videoUrl = `/static/wlm/wlm/winks/videos/wink_${winkId}.${winkExt}`;

    overlay.innerHTML = `
      <video src="${videoUrl}" autoplay playsinline style="max-width:85%; max-height:85%; filter:drop-shadow(0 12px 25px rgba(0,0,0,0.6)); pointer-events:none;"></video>
    `;
    overlay.style.display = 'flex';

    if (window.XPCore && window.XPCore.Audio) {
      window.XPCore.Audio.playDing();
    }

    const vid = overlay.querySelector('video');
    if (vid) {
      vid.onended = () => {
        overlay.style.display = 'none';
        overlay.innerHTML = '';
      };
      setTimeout(() => {
        overlay.style.display = 'none';
        overlay.innerHTML = '';
      }, 8000);
    }

    // Append system message in chat
    const chatHist = document.getElementById('wlmChatHistory');
    if (chatHist) {
      const row = document.createElement('div');
      row.className = 'wlm-sys-msg-row';
      row.textContent = `${senderName} sent a wink.`;
      chatHist.appendChild(row);
      chatHist.scrollTop = chatHist.scrollHeight;
    }
  }

  // -------------------------------------------------------------------------
  // Contact List with Scene Banner Rows
  // -------------------------------------------------------------------------
  function renderContactList(sessions, activeSessionId) {
    allSessionsData = sessions || [];
    currentActiveSessionId = activeSessionId || 'default';
    window.currentActiveSessionId = currentActiveSessionId;

    const container = document.getElementById('wlmSessionsGroupItems');
    const countLabel = document.getElementById('wlmSessionsCount');
    if (countLabel) countLabel.textContent = `(${allSessionsData.length})`;
    if (!container) return;

    container.innerHTML = '';
    
    // Always render IBA Bot contact first
    const ibaRow = document.createElement('div');
    ibaRow.className = `wlm-contact-banner-row ${currentActiveSessionId === 'default' ? 'active' : ''}`;
    ibaRow.style.backgroundImage = `url('/static/wlm/wlm/background_blue.jpg')`;
    ibaRow.innerHTML = `
      <div class="wlm-contact-banner-overlay"></div>
      <div class="wlm-contact-banner-content">
        <div style="position:relative; width:34px; height:34px; flex-shrink:0;">
          <img src="/static/wlm/wlm/usertiles/usertile_robot.png" class="wlm-contact-dp-mini" alt="IBA">
        </div>
        <div class="wlm-contact-meta-col">
          <div class="wlm-contact-name-line">
            <span>IBA (Agentic System)</span>
            <span class="wlm-contact-status-chip">- Online</span>
          </div>
          <div class="wlm-contact-bio-line">Online • Autonomous Agent System</div>
        </div>
      </div>
    `;
    ibaRow.onclick = () => {
      currentActiveSessionId = 'default';
      switchChatToPersona('default', {
        name: 'IBA (Agentic System)',
        bio: 'Online • Autonomous Agent System',
        avatar: '/static/wlm/wlm/usertiles/usertile_robot.png',
        banner: '/static/wlm/wlm/background_blue.jpg'
      });
      const chatWin = document.getElementById('unifiedWlmWindow');
      if (chatWin) {
        chatWin.classList.remove('minimized');
        chatWin.style.display = 'flex';
        if (window.XPCore) window.XPCore.bringToFront(chatWin);
      }
    };
    container.appendChild(ibaRow);

    // Right-click opens chat too; the default bot session cannot be deleted.
    ibaRow.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      e.stopPropagation();
      showSessionContextMenu(e, {
        session_id: 'default',
        is_active: currentActiveSessionId === 'default'
      }, {
        name: 'IBA (Agentic System)',
        bio: 'Online • Autonomous Agent System',
        avatar: '/static/wlm/wlm/usertiles/usertile_robot.png'
      }, ibaRow);
    });

    allSessionsData.forEach(s => {
      const p = s.persona || {};
      const isActive = s.session_id === currentActiveSessionId;
      const bannerUrl = p.banner || '/static/wlm/wlm/scenes/0001.png';

      const row = document.createElement('div');
      row.className = `wlm-contact-banner-row ${isActive ? 'active' : ''}`;
      row.style.backgroundImage = `url('${bannerUrl}')`;
      row.title = `${p.name || s.session_id} - ${p.bio || ''}`;

      row.innerHTML = `
        <div class="wlm-contact-banner-overlay"></div>
        <div class="wlm-contact-banner-content">
          <div style="position:relative; width:34px; height:34px; flex-shrink:0;">
            <img src="${p.avatar || '/static/wlm/wlm/usertiles/usertile_dog.png'}" class="wlm-contact-dp-mini" alt="DP">
          </div>
          <div class="wlm-contact-meta-col">
            <div class="wlm-contact-name-line">
              <span>${escapeHtml(p.name || s.session_id)}</span>
              <span class="wlm-contact-status-chip">- Available</span>
            </div>
            <div class="wlm-contact-bio-line">${escapeHtml(p.bio || 'Available • Recurrent Loop')}</div>
          </div>
        </div>
      `;

      row.addEventListener('click', async () => {
        try {
          await fetch('/api/iba/sessions/select', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ session_id: s.session_id })
          });
        } catch (e) {}

        currentActiveSessionId = s.session_id;
        switchChatToPersona(s.session_id, p);
        renderContactList(allSessionsData, s.session_id);

        const chatWin = document.getElementById('unifiedWlmWindow');
        const taskBtn = document.getElementById('taskBtnWlm');
        if (chatWin) {
          chatWin.classList.remove('minimized');
          chatWin.style.display = 'flex';
          window.XPCore.bringToFront(chatWin, taskBtn);
        }
      });

      // Right-click: open chat, set active, delete the session
      row.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        showSessionContextMenu(e, s, p, row);
      });

      container.appendChild(row);
    });
  }

  // -------------------------------------------------------------------------
  // Contact List Right-Click Menu (open / activate / delete session)
  // -------------------------------------------------------------------------
  function closeSessionContextMenu() {
    const menu = document.getElementById('wlmSessionCtxMenu');
    if (menu) menu.remove();
  }

  function showSessionContextMenu(e, sessionObj, persona, rowEl) {
    closeSessionContextMenu();

    const menu = document.createElement('div');
    menu.id = 'wlmSessionCtxMenu';
    menu.className = 'wlm-ctx-menu';
    const isDefault = sessionObj.session_id === 'default';
    menu.innerHTML = `
      <div class="wlm-ctx-item" id="wlmCtxOpen" data-bold="1"><strong>Open Chat</strong></div>
      <div class="wlm-ctx-item" id="wlmCtxActivate">${sessionObj.is_active ? '<span style="color:#16a34a;">✓</span> Active Session' : 'Set as Active'}</div>
      ${isDefault ? '' : '<div class="wlm-ctx-sep"></div>'}
      ${isDefault ? '' : `
      <div class="wlm-ctx-item wlm-ctx-col-1" id="wlmCtxDelete">
        <span>Delete Session</span>
      </div>`}
    `;
    document.body.appendChild(menu);

    // Fixed positioning is viewport based: clientX/clientY is exactly right.
    const rect = menu.getBoundingClientRect();
    menu.style.left = `${Math.max(4, Math.min(e.clientX, window.innerWidth - rect.width - 8))}px`;
    menu.style.top = `${Math.max(4, Math.min(e.clientY, window.innerHeight - rect.height - 8))}px`;

    const openChat = () => {
      closeSessionContextMenu();
      rowEl.click();
    };

    const activateSession = async () => {
      closeSessionContextMenu();
      try {
        await fetch('/api/iba/sessions/select', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ session_id: sessionObj.session_id })
        });
      } catch (err) {}
      rowEl.click();
    };

    // Two-step inline confirmation; window.confirm() is not supported in the
    // embedded browser, so the menu item itself flips into "Confirm delete".
    const deleteWithConfirm = () => {
      const item = menu.querySelector('#wlmCtxDelete');
      if (item && item.dataset.armed !== '1') {
        item.dataset.armed = '1';
        item.classList.add('armed');
        item.innerHTML = '<span>⚠ Click again to confirm delete</span>';
        return;
      }
      closeSessionContextMenu();
      deleteSession(sessionObj.session_id);
    };

    menu.querySelector('#wlmCtxOpen').addEventListener('click', openChat);
    menu.querySelector('#wlmCtxActivate').addEventListener('click', activateSession);
    const delBtn = menu.querySelector('#wlmCtxDelete');
    if (delBtn) delBtn.addEventListener('click', deleteWithConfirm);
  }

  async function deleteSession(sessionId) {
    try {
      const resp = await fetch('/api/iba/sessions/delete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ session_id: sessionId })
      });
      if (!resp.ok) {
        const err = await resp.json().catch(() => ({}));
        alertWlm(err.detail || 'Could not delete this session.');
        return;
      }
    } catch (err) {
      alertWlm('Network error deleting session: ' + err);
      return;
    }

    // If the deleted session was active, fall back to the IBA bot session.
    const wasActive = currentActiveSessionId === sessionId;
    try {
      const resp = await fetch('/api/iba/sessions');
      const data = await resp.json();
      const sess = data.sessions || [];
      const freshActive = data.active_session || 'default';
      const activeObj = sess.find(x => x.session_id === freshActive);
      currentActiveSessionId = freshActive;
      window.currentActiveSessionId = freshActive;
      renderContactList(sess, freshActive);
      if (wasActive || freshActive === 'default') {
        switchChatToPersona(freshActive || 'default', activeObj && activeObj.persona ? activeObj.persona : {
          name: 'IBA (Agentic System)',
          bio: 'Online • Autonomous Agent System',
          avatar: '/static/wlm/wlm/usertiles/usertile_robot.png',
          banner: '/static/wlm/wlm/background_blue.jpg'
        });
      }
    } catch (err) {}

    // Notify the host app so workspace chips, selects and cards refresh too.
    window.dispatchEvent(new CustomEvent('wlm-sessions-changed', { detail: { deleted: sessionId } }));
  }

  // window.alert()/confirm()/prompt() are unavailable in the embedded
  // session-review browser; surface failure text inside the chat instead.
  function alertWlm(message) {
    const chatHist = document.getElementById('wlmChatHistory');
    if (!chatHist) return;
    const row = document.createElement('div');
    row.className = 'wlm-sys-msg-row';
    row.textContent = String(message);
    chatHist.appendChild(row);
    chatHist.scrollTop = chatHist.scrollHeight;
  }

  document.addEventListener('click', (e) => {
    // Clicks on menu items bubble here; don't close the menu under them.
    if (e.target && e.target.closest && e.target.closest('#wlmSessionCtxMenu')) return;
    closeSessionContextMenu();
  });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeSessionContextMenu(); });
  window.addEventListener('scroll', closeSessionContextMenu, true);

  // -------------------------------------------------------------------------
  // Chat Header & Session Persona Switch
  // -------------------------------------------------------------------------
  async function switchChatToPersona(sessionId, persona) {
    currentActiveSessionId = sessionId || 'default';
    window.currentActiveSessionId = currentActiveSessionId;
    const header = document.getElementById('wlmChatHeaderHandle');
    const tileImg = document.querySelector('#wlmContactTile img');
    const nameEl = document.querySelector('.wlm-chat-contact-name');
    const pmEl = document.querySelector('.wlm-chat-contact-pm');
    const taskLabel = document.getElementById('taskBtnWlmLabel');

    const sceneBanner = persona.banner || '/static/wlm/wlm/scenes/0001.png';
    if (header) {
      header.style.backgroundImage = `url('${sceneBanner}')`;
      header.style.backgroundSize = 'cover';
      header.style.backgroundPosition = 'center bottom';
    }
    if (persona.avatar && tileImg) tileImg.src = persona.avatar;
    if (nameEl) nameEl.textContent = persona.name || sessionId;
    if (pmEl) pmEl.textContent = persona.bio || 'Online & handling session';
    if (taskLabel) {
      const shortName = persona.first_name || (persona.name ? persona.name.split(' ')[0] : sessionId);
      taskLabel.textContent = `Chat - ${shortName}`;
    }

    // Load session history
    try {
      const resp = await fetch(`/api/iba/sessions/${sessionId}/history`);
      if (resp.ok) {
        const histData = await resp.json();
        const msgs = histData.messages || [];
        const chatHist = document.getElementById('wlmChatHistory');
        if (chatHist) {
          chatHist.innerHTML = '';
          if (msgs.length === 0) {
            const welcomeRow = document.createElement('div');
            welcomeRow.className = 'wlm-chat-msg-row';
            const activePersona = getActivePersona();
            const botName = activePersona?.name || persona.name || sessionId;
            const botAvatar = activePersona?.avatar || persona.avatar || '/static/wlm/wlm/usertiles/usertile_dog.png';
            welcomeRow.innerHTML = `
              <div class="wlm-chat-msg-header bot">
                <img src="${botAvatar}" width="16" height="16" style="border-radius:2px;">
                <span>${escapeHtml(botName)} says:</span>
                <span class="wlm-chat-msg-time">${new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'})}</span>
              </div>
              <div class="wlm-chat-msg-body">
                Hello! I am ${escapeHtml(persona.full_name || persona.name || 'your agent')} for session <strong>${escapeHtml(sessionId)}</strong>.<br>
                ${escapeHtml(persona.bio || '')}<br>
                Commands execute safely in the Linux Sandbox VM (Z:). Host actions will ask your permission!
              </div>
            `;
            chatHist.appendChild(welcomeRow);
          } else {
            const activePersona = getActivePersona();
            msgs.forEach(m => {
              const row = document.createElement('div');
              row.className = 'wlm-chat-msg-row';
              const isBot = m.role === 'assistant';
              const botName = activePersona?.name || persona.name || 'Agent';
              const botAvatar = activePersona?.avatar || persona.avatar || '/static/wlm/wlm/usertiles/usertile_dog.png';
              const reasoning = (m.reasoning || '').trim();
              const reasoningTokens = reasoning ? reasoning.split(/\s+/).filter(Boolean).length : 0;
              const reasoningOpen = document.getElementById('showReasoningInline')?.checked !== false;
              
              // Build content with interleaved reasoning and tool calls
              let contentHtml = '';
              
              // Add reasoning block if it exists
              if (isBot && reasoning) {
                contentHtml += `
                  <details class="wlm-thought-dropdown" ${reasoningOpen ? 'open' : ''}>
                    <summary class="wlm-thought-summary">
                      <img src="/static/xp_assets/icons/shell32_24.png" width="14" height="14" alt="Think">
                      <span class="wlm-thought-title">${escapeHtml(botName)} Reasoning (${reasoningTokens} tokens)</span>
                    </summary>
                    <div class="wlm-thought-body" style="max-height:220px; overflow-y:auto;">${escapeHtml(reasoning)}</div>
                  </details>
                `;
              }
              
              // Add tool calls from steps with results below them.
              // Steps executed by different (parallel) tasks render side by side.
              const stepHtml = (step) => {
                if (!step.action_name) return '';
                const statusColor = step.action_state === 'completed' ? '#16a34a' : (step.action_state === 'failed' ? '#dc2626' : '#f59e0b');
                const statusText = step.action_state === 'completed' ? 'completed' : (step.action_state === 'failed' ? 'failed' : 'pending');

                let resultHtml = '';
                if (step.action_state === 'completed' && step.action_result_body) {
                  try {
                    const result = JSON.parse(step.action_result_body);
                    resultHtml = `
                      <div class="wlm-tool-result" style="margin-top:6px; padding:6px; background:#f0fdf4; border-left:3px solid #16a34a; border-radius:3px; font-size:10px; color:#166534;">
                        <strong>Result:</strong> ${escapeHtml(JSON.stringify(result, null, 2).substring(0, 500))}${JSON.stringify(result, null, 2).length > 500 ? '...' : ''}
                      </div>
                    `;
                  } catch (e) {
                    resultHtml = `
                      <div class="wlm-tool-result" style="margin-top:6px; padding:6px; background:#f0fdf4; border-left:3px solid #16a34a; border-radius:3px; font-size:10px; color:#166534;">
                        <strong>Result:</strong> ${escapeHtml(step.action_result_body.substring(0, 500))}${step.action_result_body.length > 500 ? '...' : ''}
                      </div>
                    `;
                  }
                }

                return `
                  <div class="wlm-tool-call-row" style="margin:4px 0; padding:6px 8px; background:#f8fafc; border-left:3px solid #3b82f6; border-radius:3px; font-size:11px;">
                    <div class="wlm-tool-call-header" style="display:flex; align-items:center; gap:6px; font-weight:600; color:#1e40af; margin-bottom:4px;">
                      <img src="/static/xp_assets/icons/shell32_24.png" width="12" height="12" alt="Tool">
                      <span class="wlm-tool-name">${escapeHtml(step.action_name)}</span>
                      <span style="color:${statusColor}; font-size:10px; margin-left:auto;">${statusText}</span>
                    </div>
                    <div class="wlm-tool-args" style="padding:4px 6px; background:#ffffff; border:1px solid #e2e8f0; border-radius:3px; font-size:10px; color:#475569; max-height:150px; overflow-y:auto; white-space:pre-wrap;">${escapeHtml(JSON.stringify(step.action_args || {}, null, 2))}</div>
                    ${resultHtml}
                  </div>
                `;
              };

              const activeGroups = (m.step_groups || []).filter(g => g.steps.some(s => s.action_name));
              if (isBot && activeGroups.length > 1) {
                contentHtml += '<div class="wlm-parallel-cols">' + activeGroups.map(g => `
                  <div class="wlm-task-col">
                    <div class="wlm-task-col-title">${escapeHtml(g.title || 'Subtask')}</div>
                    <div>${g.steps.map(stepHtml).join('')}</div>
                  </div>
                `).join('') + '</div>';
              } else if (isBot && m.steps && Array.isArray(m.steps)) {
                m.steps.forEach(step => { contentHtml += stepHtml(step); });
              }
              
              // Add content
              contentHtml += escapeHtml(m.content || '');
              
              row.innerHTML = `
                <div class="wlm-chat-msg-header ${isBot ? 'bot' : ''}">
                  <img src="${isBot ? botAvatar : userProfile.avatar}" width="16" height="16" style="border-radius:2px;">
                  <span>${isBot ? escapeHtml(botName) : escapeHtml(userProfile.name || 'You')} says:</span>
                  <span class="wlm-chat-msg-time">${new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'})}</span>
                </div>
                <div class="wlm-chat-msg-body ${isBot ? 'bot-answer' : ''}">${contentHtml}</div>
              `;
              chatHist.appendChild(row);
            });
          }
          chatHist.scrollTop = chatHist.scrollHeight;
        }
      }
    } catch (e) {}
  }

  // -------------------------------------------------------------------------
  // Host Permission & Question Cards
  // -------------------------------------------------------------------------
  function renderHostPermissionCard(perm) {
    const chatHist = document.getElementById('wlmChatHistory');
    if (!chatHist) return;
    const card = document.createElement('div');
    card.className = 'wlm-permission-card';
    card.id = `permCard_${perm.id}`;
    card.innerHTML = `
      <div class="wlm-perm-header">
        <span class="wlm-perm-title"><img src="/static/xp_assets/icons/appwiz_1.png" class="xp-title-icon" alt="Shield"> ${escapeHtml(perm.title || 'Host Execution Permission Required')}</span>
        <span class="wlm-perm-badge">HOST ACCESS</span>
      </div>
      <div class="wlm-perm-desc">${escapeHtml(perm.description || 'The agent needs to execute an action on your host computer (outside Linux Sandbox VM):')}</div>
      <div class="wlm-perm-cmd"><code>${escapeHtml(perm.command || '')}</code></div>
      <div style="font-size:10px; color:#92400e; margin-bottom:8px;">Target: ${escapeHtml(perm.target || 'Host Environment')} • Risk: <strong>${escapeHtml(perm.risk_level || 'medium').toUpperCase()}</strong></div>
      <div class="wlm-perm-actions" id="permActions_${perm.id}">
        <button class="wlm-btn-perm-approve" id="approveBtn_${perm.id}">✓ Approve &amp; Execute on Host</button>
        <button class="wlm-btn-perm-deny" id="denyBtn_${perm.id}">✕ Deny (Run in Sandbox)</button>
      </div>
    `;
    chatHist.appendChild(card);
    chatHist.scrollTop = chatHist.scrollHeight;

    card.querySelector(`#approveBtn_${perm.id}`).addEventListener('click', async () => {
      try {
        await fetch('/api/iba/permission/respond', {
          method: 'POST',
          headers: {'Content-Type': 'application/json'},
          body: JSON.stringify({ request_id: perm.id, approved: true, session_id: currentActiveSessionId })
        });
      } catch (e) {}
      const actions = card.querySelector(`#permActions_${perm.id}`);
      if (actions) actions.innerHTML = `<span style="font-size:11px; font-weight:700; color:#16a34a;">✓ Approved by user at ${new Date().toLocaleTimeString()}</span>`;
    });

    card.querySelector(`#denyBtn_${perm.id}`).addEventListener('click', async () => {
      try {
        await fetch('/api/iba/permission/respond', {
          method: 'POST',
          headers: {'Content-Type': 'application/json'},
          body: JSON.stringify({ request_id: perm.id, approved: false, session_id: currentActiveSessionId })
        });
      } catch (e) {}
      const actions = card.querySelector(`#permActions_${perm.id}`);
      if (actions) actions.innerHTML = `<span style="font-size:11px; font-weight:700; color:#dc2626;">✕ Denied by user at ${new Date().toLocaleTimeString()}</span>`;
    });
  }

  function renderQuestionCard(q) {
    const chatHist = document.getElementById('wlmChatHistory');
    if (!chatHist) return;
    const card = document.createElement('div');
    card.className = 'wlm-question-card';
    const optionsHtml = (q.options || []).map(opt => `
      <button class="wlm-question-chip" data-ans="${escapeHtml(opt)}">${escapeHtml(opt)}</button>
    `).join('');

    card.innerHTML = `
      <div class="wlm-question-title"><img src="/static/xp_assets/icons/shell32_24.png" class="xp-title-icon" alt="Question"> ${escapeHtml(q.title || 'IBA Question for Session')}</div>
      <div class="wlm-question-body">${escapeHtml(q.question || '')}</div>
      <div class="wlm-question-options">${optionsHtml}</div>
    `;
    chatHist.appendChild(card);
    chatHist.scrollTop = chatHist.scrollHeight;

    card.querySelectorAll('.wlm-question-chip').forEach(btn => {
      btn.addEventListener('click', () => {
        const ans = btn.dataset.ans;
        if (window.sendChatMessage) window.sendChatMessage(ans);
        card.querySelectorAll('.wlm-question-chip').forEach(b => b.disabled = true);
        btn.style.background = '#16a34a';
        btn.style.color = '#ffffff';
      });
    });
  }

  function getActiveSessionId() {
    return currentActiveSessionId || 'default';
  }

  function getActivePersona() {
    const sess = allSessionsData.find(s => s.session_id === currentActiveSessionId);
    if (sess && sess.persona) return sess.persona;
    if (currentActiveSessionId === 'default') {
      return {
        name: 'IBA',
        bio: 'Online • Autonomous Agent System',
        avatar: '/static/wlm/wlm/usertiles/usertile_robot.png',
        banner: '/static/wlm/wlm/background_blue.jpg'
      };
    }
    return null;
  }

  return {
    userProfile,
    applyUserProfile,
    playWink,
    renderContactList,
    getActiveSessionId,
    getActivePersona,
    switchChatToPersona,
    renderHostPermissionCard,
    renderQuestionCard,
    deleteSession
  };
})();
