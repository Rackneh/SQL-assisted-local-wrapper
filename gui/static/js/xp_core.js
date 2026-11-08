/**
 * Windows XP Simulator - Core OS Subsystem (xp_core.js)
 * Implements: Window Manager, Bootup Sequence, Logon Screen, Audio Synthesizer,
 * Themes Engine (Luna Blue, Olive, Silver, Royale, Zune, Classic),
 * Screensaver Subsystem, and Desktop Shell.
 */

window.XPCore = (function() {
  function getDesktopContainer() {
    return document.getElementById("unifiedXpDesktop") || document.getElementById("messagesContainer") || document.querySelector(".chat-workspace") || document.body;
  }
  let audioCtx = null;

  function getAudioContext() {
    if (!audioCtx) {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (AudioCtx) audioCtx = new AudioCtx();
    }
    if (audioCtx && audioCtx.state === 'suspended') {
      audioCtx.resume();
    }
    return audioCtx;
  }

  // -------------------------------------------------------------------------
  // Authentic Windows XP Sound Player (Real WAVs from ISO + Web Audio Fallback)
  // -------------------------------------------------------------------------
  function playWavFile(wavPath, fallbackFn) {
    try {
      const audio = new window.Audio(wavPath);
      audio.volume = 0.65;
      const playPromise = audio.play();
      if (playPromise !== undefined) {
        playPromise.catch(() => {
          if (fallbackFn) fallbackFn();
        });
      }
    } catch (e) {
      if (fallbackFn) fallbackFn();
    }
  }

  const Audio = {
    playStartup() {
      playWavFile('/static/xp_assets/sounds/startup.wav', () => {
        try {
          const ctx = getAudioContext();
          if (!ctx) return;
          const now = ctx.currentTime;
          const notes = [
            { freq: 155.56, start: 0.0, dur: 3.2, gain: 0.18, type: 'triangle' },
            { freq: 233.08, start: 0.35, dur: 2.8, gain: 0.16, type: 'sine' },
            { freq: 207.65, start: 0.70, dur: 2.6, gain: 0.18, type: 'triangle' },
            { freq: 311.13, start: 1.05, dur: 2.4, gain: 0.15, type: 'sine' },
            { freq: 466.16, start: 1.40, dur: 2.5, gain: 0.14, type: 'triangle' },
            { freq: 622.25, start: 1.75, dur: 3.0, gain: 0.20, type: 'sine' },
            { freq: 932.32, start: 1.95, dur: 2.5, gain: 0.08, type: 'sine' }
          ];
          notes.forEach(n => {
            const osc = ctx.createOscillator();
            const g = ctx.createGain();
            osc.type = n.type;
            osc.frequency.setValueAtTime(n.freq, now + n.start);
            g.gain.setValueAtTime(0.0001, now + n.start);
            g.gain.exponentialRampToValueAtTime(n.gain, now + n.start + 0.15);
            g.gain.exponentialRampToValueAtTime(0.0001, now + n.start + n.dur);
            osc.connect(g);
            g.connect(ctx.destination);
            osc.start(now + n.start);
            osc.stop(now + n.start + n.dur);
          });
        } catch (e) {}
      });
    },

    playShutdown() {
      playWavFile('/static/xp_assets/sounds/shutdown.wav', () => {
        try {
          const ctx = getAudioContext();
          if (!ctx) return;
          const now = ctx.currentTime;
          const notes = [
            { freq: 622.25, start: 0.0, dur: 1.8, gain: 0.18, type: 'sine' },
            { freq: 466.16, start: 0.4, dur: 1.8, gain: 0.16, type: 'triangle' },
            { freq: 415.30, start: 0.8, dur: 2.0, gain: 0.16, type: 'sine' },
            { freq: 311.13, start: 1.2, dur: 2.6, gain: 0.20, type: 'triangle' },
            { freq: 155.56, start: 1.4, dur: 3.0, gain: 0.15, type: 'sine' }
          ];
          notes.forEach(n => {
            const osc = ctx.createOscillator();
            const g = ctx.createGain();
            osc.type = n.type;
            osc.frequency.setValueAtTime(n.freq, now + n.start);
            g.gain.setValueAtTime(0.0001, now + n.start);
            g.gain.exponentialRampToValueAtTime(n.gain, now + n.start + 0.1);
            g.gain.exponentialRampToValueAtTime(0.0001, now + n.start + n.dur);
            osc.connect(g);
            g.connect(ctx.destination);
            osc.start(now + n.start);
            osc.stop(now + n.start + n.dur);
          });
        } catch (e) {}
      });
    },

    playLogon() {
      playWavFile('/static/xp_assets/sounds/logon.wav', () => this.playStartup());
    },

    playLogoff() {
      playWavFile('/static/xp_assets/sounds/logoff.wav', () => this.playShutdown());
    },

    playDing() {
      playWavFile('/static/xp_assets/sounds/ding.wav', () => {
        try {
          const ctx = getAudioContext();
          if (!ctx) return;
          const now = ctx.currentTime;
          const osc = ctx.createOscillator();
          const g = ctx.createGain();
          osc.type = 'sine';
          osc.frequency.setValueAtTime(880, now);
          g.gain.setValueAtTime(0.25, now);
          g.gain.exponentialRampToValueAtTime(0.0001, now + 0.6);
          osc.connect(g);
          g.connect(ctx.destination);
          osc.start(now);
          osc.stop(now + 0.6);
        } catch (e) {}
      });
    },

    playError() {
      playWavFile('/static/xp_assets/sounds/error.wav', () => {
        try {
          const ctx = getAudioContext();
          if (!ctx) return;
          const now = ctx.currentTime;
          const osc = ctx.createOscillator();
          const g = ctx.createGain();
          osc.type = 'sawtooth';
          osc.frequency.setValueAtTime(150, now);
          g.gain.setValueAtTime(0.3, now);
          g.gain.exponentialRampToValueAtTime(0.0001, now + 0.35);
          osc.connect(g);
          g.connect(ctx.destination);
          osc.start(now);
          osc.stop(now + 0.35);
        } catch (e) {}
      });
    },

    playTada() {
      playWavFile('/static/xp_assets/sounds/tada.wav', () => this.playStartup());
    },

    playClick() {
      playWavFile('/static/xp_assets/sounds/click.wav', () => {
        try {
          const ctx = getAudioContext();
          if (!ctx) return;
          const now = ctx.currentTime;
          const osc = ctx.createOscillator();
          const g = ctx.createGain();
          osc.type = 'square';
          osc.frequency.setValueAtTime(1200, now);
          osc.frequency.exponentialRampToValueAtTime(400, now + 0.04);
          g.gain.setValueAtTime(0.12, now);
          g.gain.exponentialRampToValueAtTime(0.0001, now + 0.04);
          osc.connect(g);
          g.connect(ctx.destination);
          osc.start(now);
          osc.stop(now + 0.04);
        } catch (e) {}
      });
    },

    playRecycle() {
      playWavFile('/static/xp_assets/sounds/recycle.wav', () => this.playClick());
    },

    playBalloon() {
      playWavFile('/static/xp_assets/sounds/balloon.wav', () => this.playDing());
    },

    playHardwareInsert() {
      playWavFile('/static/xp_assets/sounds/hardware_insert.wav', () => {
        try {
          const ctx = getAudioContext();
          if (!ctx) return;
          const now = ctx.currentTime;
          const osc = ctx.createOscillator();
          const g = ctx.createGain();
          osc.type = 'sine';
          osc.frequency.setValueAtTime(440, now);
          osc.frequency.setValueAtTime(880, now + 0.1);
          g.gain.setValueAtTime(0.2, now);
          g.gain.exponentialRampToValueAtTime(0.0001, now + 0.3);
          osc.connect(g);
          g.connect(ctx.destination);
          osc.start(now);
          osc.stop(now + 0.3);
        } catch (e) {}
      });
    },

    playHardwareRemove() {
      playWavFile('/static/xp_assets/sounds/hardware_remove.wav', () => {
        try {
          const ctx = getAudioContext();
          if (!ctx) return;
          const now = ctx.currentTime;
          const osc = ctx.createOscillator();
          const g = ctx.createGain();
          osc.type = 'sine';
          osc.frequency.setValueAtTime(880, now);
          osc.frequency.setValueAtTime(440, now + 0.1);
          g.gain.setValueAtTime(0.2, now);
          g.gain.exponentialRampToValueAtTime(0.0001, now + 0.3);
          osc.connect(g);
          g.connect(ctx.destination);
          osc.start(now);
          osc.stop(now + 0.3);
        } catch (e) {}
      });
    },

    // Motherboard BIOS POST Beep (Authentic 750Hz 150ms Square Wave)
    playPostBeep() {
      try {
        const ctx = getAudioContext();
        if (!ctx) return;
        const now = ctx.currentTime;
        const osc = ctx.createOscillator();
        const g = ctx.createGain();
        osc.type = 'square';
        osc.frequency.setValueAtTime(750, now);
        g.gain.setValueAtTime(0.25, now);
        g.gain.setValueAtTime(0.25, now + 0.14);
        g.gain.exponentialRampToValueAtTime(0.0001, now + 0.16);
        osc.connect(g);
        g.connect(ctx.destination);
        osc.start(now);
        osc.stop(now + 0.16);
      } catch (e) {}
    },

    // Mechanical Power Switch Tactile Click
    playMechanicalClick() {
      try {
        const ctx = getAudioContext();
        if (!ctx) return;
        const now = ctx.currentTime;
        const osc = ctx.createOscillator();
        const g = ctx.createGain();
        osc.type = 'sawtooth';
        osc.frequency.setValueAtTime(140, now);
        osc.frequency.exponentialRampToValueAtTime(30, now + 0.08);
        g.gain.setValueAtTime(0.35, now);
        g.gain.exponentialRampToValueAtTime(0.0001, now + 0.08);
        osc.connect(g);
        g.connect(ctx.destination);
        osc.start(now);
        osc.stop(now + 0.08);
      } catch (e) {}
    }
  };

  // -------------------------------------------------------------------------
  // Themes Engine (Luna Blue, Olive, Silver, Royale, Zune, Classic)
  // -------------------------------------------------------------------------
  const THEMES = {
    'luna-blue': {
      name: 'Windows XP (Default / Luna Blue)',
      style: 'xp',
      colorScheme: 'blue',
      wallpaper: '/static/img/bliss.jpg',
      previewColor: '#245edb'
    },
    'luna-olive': {
      name: 'Windows XP (Olive Green)',
      style: 'xp',
      colorScheme: 'olive',
      wallpaper: '/static/wlm/wlm/scenes/0004.png',
      previewColor: '#768a4e'
    },
    'luna-silver': {
      name: 'Windows XP (Silver)',
      style: 'xp',
      colorScheme: 'silver',
      wallpaper: '/static/wlm/wlm/scenes/0002.jpg',
      previewColor: '#9999a8'
    },
    'royale': {
      name: 'Windows XP Royale (Energy Blue)',
      style: 'xp',
      colorScheme: 'royale',
      wallpaper: '/static/wlm/wlm/scenes/0007.png',
      previewColor: '#0947a5'
    },
    'zune': {
      name: 'Windows XP Zune (Noir)',
      style: 'xp',
      colorScheme: 'zune',
      wallpaper: '/static/wlm/wlm/scenes/zune_04.jpg',
      previewColor: '#222222'
    },
    'classic': {
      name: 'Windows Classic',
      style: 'classic',
      colorScheme: 'standard',
      wallpaper: '/static/wlm/wlm/background_teal.jpg',
      previewColor: '#0a246a'
    }
  };

  let currentTheme = 'luna-blue';
  let currentWallpaper = '/static/img/bliss.jpg';
  let highestZ = 30;

  function loadSavedTheme() {
    try {
      const saved = localStorage.getItem('xp_theme_config');
      if (saved) {
        const parsed = JSON.parse(saved);
        if (parsed.theme && THEMES[parsed.theme]) currentTheme = parsed.theme;
        if (parsed.wallpaper) currentWallpaper = parsed.wallpaper;
      }
    } catch (e) {}
  }

  function applyTheme(themeId, wallpaperUrl = null) {
    if (!THEMES[themeId]) themeId = 'luna-blue';
    currentTheme = themeId;
    if (wallpaperUrl) currentWallpaper = wallpaperUrl;

    const desktop = document.getElementById('unifiedXpDesktop');
    if (!desktop) return;

    desktop.setAttribute('data-theme', themeId);
    if (currentWallpaper) {
      desktop.style.backgroundImage = `url('${currentWallpaper}')`;
      desktop.style.backgroundSize = 'cover';
      desktop.style.backgroundPosition = 'center';
    }

    try {
      localStorage.setItem('xp_theme_config', JSON.stringify({
        theme: currentTheme,
        wallpaper: currentWallpaper
      }));
    } catch (e) {}
  }

  // -------------------------------------------------------------------------
  // Window Manager: Universal Dragging, Resizing, and Z-Stacking
  // -------------------------------------------------------------------------
  function bringToFront(win, taskBtn = null) {
    if (!win) return;
    highestZ += 2;
    win.style.zIndex = highestZ;

    document.querySelectorAll('.xp-window, .wlm-window').forEach(w => w.classList.remove('active-focus'));
    win.classList.remove('minimized');
    win.classList.add('active-focus');

    if (taskBtn) {
      document.querySelectorAll('.xp-task-btn').forEach(b => b.classList.remove('active'));
      taskBtn.classList.add('active');
    }
  }

  function toggleWindow(win, taskBtn) {
    if (!win) return;
    if (win.classList.contains('minimized') || win.style.display === 'none') {
      win.classList.remove('minimized');
      win.style.display = 'flex';
      bringToFront(win, taskBtn);
    } else {
      if (win.classList.contains('active-focus')) {
        win.classList.add('minimized');
        win.style.display = 'none';
        if (taskBtn) taskBtn.classList.remove('active');
      } else {
        bringToFront(win, taskBtn);
      }
    }
  }

  function makeDraggable(win, handle) {
    if (!win || !handle) return;
    let isDragging = false;
    let startX = 0, startY = 0, initLeft = 0, initTop = 0;

    handle.addEventListener('mousedown', (e) => {
      if (e.target.closest('button') || e.target.closest('input') || e.target.closest('.wlm-window-controls') || e.target.closest('.title-bar-controls')) return;
      isDragging = true;
      startX = e.clientX;
      startY = e.clientY;
      initLeft = win.offsetLeft;
      initTop = win.offsetTop;
      bringToFront(win);
      e.preventDefault();
    });

    window.addEventListener('mousemove', (e) => {
      if (!isDragging) return;
      const dx = e.clientX - startX;
      const dy = e.clientY - startY;
      const desktop = document.getElementById('unifiedXpDesktop');
      const maxW = desktop ? desktop.clientWidth - 40 : 1200;
      const maxH = desktop ? desktop.clientHeight - 40 : 800;

      const newLeft = Math.max(-100, Math.min(maxW, initLeft + dx));
      const newTop = Math.max(0, Math.min(maxH, initTop + dy));

      win.style.left = `${newLeft}px`;
      win.style.top = `${newTop}px`;
      win.style.right = 'auto';
      win.style.bottom = 'auto';
    });

    window.addEventListener('mouseup', () => {
      isDragging = false;
    });

    win.addEventListener('mousedown', () => {
      bringToFront(win);
    });
  }

  function makeResizable(win, minW = 280, minH = 180) {
    if (!win || win.dataset.resizableInit === 'true') return;
    win.dataset.resizableInit = 'true';
    win.style.boxSizing = 'border-box';

    const directions = ['n', 's', 'e', 'w', 'nw', 'ne', 'sw', 'se'];
    directions.forEach(dir => {
      const handle = document.createElement('div');
      handle.className = `xp-resize-handle ${dir}`;
      handle.dataset.dir = dir;
      win.appendChild(handle);

      handle.addEventListener('mousedown', (e) => {
        e.preventDefault();
        e.stopPropagation();
        bringToFront(win);

        const startX = e.clientX;
        const startY = e.clientY;
        const startW = win.offsetWidth;
        const startH = win.offsetHeight;
        const startL = win.offsetLeft;
        const startT = win.offsetTop;

        const onMouseMove = (moveEvent) => {
          const dx = moveEvent.clientX - startX;
          const dy = moveEvent.clientY - startY;

          if (dir.includes('e')) {
            win.style.width = `${Math.max(minW, startW + dx)}px`;
          }
          if (dir.includes('s')) {
            win.style.height = `${Math.max(minH, startH + dy)}px`;
          }
          if (dir.includes('w')) {
            const newW = Math.max(minW, startW - dx);
            if (newW > minW) {
              win.style.width = `${newW}px`;
              win.style.left = `${startL + dx}px`;
            }
          }
          if (dir.includes('n')) {
            const newH = Math.max(minH, startH - dy);
            if (newH > minH) {
              win.style.height = `${newH}px`;
              win.style.top = `${startT + dy}px`;
            }
          }
        };

        const onMouseUp = () => {
          window.removeEventListener('mousemove', onMouseMove);
          window.removeEventListener('mouseup', onMouseUp);
        };

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
      });
    });

    const grip = document.createElement('div');
    grip.className = 'xp-window-resizegrip';
    win.appendChild(grip);
  }

  // -------------------------------------------------------------------------
  // Screensaver Subsystem (3D Pipes, Starfield, Flying Windows, Mystify)
  // -------------------------------------------------------------------------
  let screensaverCanvas = null;
  let screensaverAnimId = null;
  let screensaverMode = 'pipes';
  let screensaverTimeout = null;

  function stopScreensaver() {
    if (screensaverAnimId) {
      cancelAnimationFrame(screensaverAnimId);
      screensaverAnimId = null;
    }
    const overlay = document.getElementById('xpScreensaverOverlay');
    if (overlay) {
      overlay.style.display = 'none';
    }
  }

  function startScreensaver(mode = 'pipes', targetCanvas = null) {
    screensaverMode = mode;
    let canvas = targetCanvas;
    let isPreview = !!targetCanvas;

    if (!canvas) {
      const parentContainer = getDesktopContainer();
      let overlay = document.getElementById('xpScreensaverOverlay');
      if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = 'xpScreensaverOverlay';
        overlay.className = 'xp-screensaver-overlay';
        overlay.innerHTML = '<canvas id="screensaverMainCanvas"></canvas>';
        parentContainer.appendChild(overlay);

        const exitHandler = () => {
          stopScreensaver();
          window.removeEventListener('mousemove', exitHandler);
          window.removeEventListener('keydown', exitHandler);
          window.removeEventListener('mousedown', exitHandler);
        };
        setTimeout(() => {
          window.addEventListener('mousemove', exitHandler);
          window.addEventListener('keydown', exitHandler);
          window.addEventListener('mousedown', exitHandler);
        }, 300);
      }
      overlay.style.display = 'block';
      canvas = overlay.querySelector('canvas');
      canvas.width = parentContainer.clientWidth || window.innerWidth;
      canvas.height = parentContainer.clientHeight || window.innerHeight;
    }

    const ctx = canvas.getContext('2d');
    if (screensaverAnimId) cancelAnimationFrame(screensaverAnimId);

    if (mode === 'starfield') {
      // Hyperspace Starfield
      const numStars = isPreview ? 80 : 300;
      const stars = [];
      for (let i = 0; i < numStars; i++) {
        stars.push({
          x: (Math.random() - 0.5) * canvas.width,
          y: (Math.random() - 0.5) * canvas.height,
          z: Math.random() * canvas.width
        });
      }

      const drawStars = () => {
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        const cx = canvas.width / 2;
        const cy = canvas.height / 2;

        stars.forEach(s => {
          s.z -= 4;
          if (s.z <= 0) {
            s.z = canvas.width;
            s.x = (Math.random() - 0.5) * canvas.width;
            s.y = (Math.random() - 0.5) * canvas.height;
          }
          const k = 200 / s.z;
          const px = cx + s.x * k;
          const py = cy + s.y * k;
          const sz = Math.max(1, (1 - s.z / canvas.width) * 3);

          if (px >= 0 && px <= canvas.width && py >= 0 && py <= canvas.height) {
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(px, py, sz, sz);
          }
        });
        screensaverAnimId = requestAnimationFrame(drawStars);
      };
      drawStars();

    } else if (mode === 'flying_windows') {
      // 3D Flying Windows Flags
      const flags = [];
      const colors = ['#e45b40', '#388e3c', '#0284c7', '#f59e0b'];
      for (let i = 0; i < (isPreview ? 8 : 24); i++) {
        flags.push({
          x: (Math.random() - 0.5) * canvas.width * 1.5,
          y: (Math.random() - 0.5) * canvas.height * 1.5,
          z: Math.random() * 800 + 100,
          rot: Math.random() * Math.PI * 2,
          speed: Math.random() * 3 + 2
        });
      }

      const drawFlags = () => {
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        const cx = canvas.width / 2;
        const cy = canvas.height / 2;

        flags.forEach(f => {
          f.z -= f.speed;
          f.rot += 0.02;
          if (f.z <= 10) {
            f.z = 800;
            f.x = (Math.random() - 0.5) * canvas.width * 1.5;
            f.y = (Math.random() - 0.5) * canvas.height * 1.5;
          }

          const scale = 300 / f.z;
          const px = cx + f.x * scale;
          const py = cy + f.y * scale;
          const sz = 40 * scale;

          if (px >= -50 && px <= canvas.width + 50 && py >= -50 && py <= canvas.height + 50) {
            ctx.save();
            ctx.translate(px, py);
            ctx.rotate(f.rot);
            // Draw 4-color Windows XP flag
            ctx.fillStyle = colors[0];
            ctx.fillRect(-sz/2, -sz/2, sz/2 - 1, sz/2 - 1);
            ctx.fillStyle = colors[1];
            ctx.fillRect(1, -sz/2, sz/2 - 1, sz/2 - 1);
            ctx.fillStyle = colors[2];
            ctx.fillRect(-sz/2, 1, sz/2 - 1, sz/2 - 1);
            ctx.fillStyle = colors[3];
            ctx.fillRect(1, 1, sz/2 - 1, sz/2 - 1);
            ctx.restore();
          }
        });
        screensaverAnimId = requestAnimationFrame(drawFlags);
      };
      drawFlags();

    } else {
      // 3D Pipes Screensaver
      ctx.fillStyle = '#000000';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      const colors = ['#e11d48', '#2563eb', '#16a34a', '#ca8a04', '#9333ea', '#06b6d4'];
      let px = canvas.width / 2;
      let py = canvas.height / 2;
      let dir = 0; // 0: right, 1: down, 2: left, 3: up
      let color = colors[0];
      let stepCount = 0;

      const drawPipes = () => {
        if (stepCount % 60 === 0) {
          dir = Math.floor(Math.random() * 4);
          color = colors[Math.floor(Math.random() * colors.length)];
        }
        if (stepCount % 600 === 0 && !isPreview) {
          ctx.fillStyle = 'rgba(0,0,0,0.85)';
          ctx.fillRect(0, 0, canvas.width, canvas.height);
          px = Math.random() * canvas.width;
          py = Math.random() * canvas.height;
        }

        const step = isPreview ? 2 : 4;
        const width = isPreview ? 4 : 8;

        ctx.fillStyle = color;
        ctx.fillRect(px - width/2, py - width/2, width, width);

        // Highlight
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(px - width/4, py - width/4, width/2, width/2);

        if (dir === 0) px += step;
        else if (dir === 1) py += step;
        else if (dir === 2) px -= step;
        else py -= step;

        if (px < 0) px = canvas.width;
        if (px > canvas.width) px = 0;
        if (py < 0) py = canvas.height;
        if (py > canvas.height) py = 0;

        stepCount++;
        screensaverAnimId = requestAnimationFrame(drawPipes);
      };
      drawPipes();
    }
  }

  // -------------------------------------------------------------------------
  // Bootup Sequence & Logon Screen
  // -------------------------------------------------------------------------
  function triggerBootSequence(onComplete = null, projectInfo = null) {
    const activeWs = projectInfo || window.currentActiveWorkspace || (window._cachedWorkspaces && window._cachedWorkspaces[0]) || null;
    const parentContainer = getDesktopContainer();
    let overlay = document.getElementById('xpBootSequenceOverlay');
    if (!overlay) {
      overlay = document.createElement('div');
      overlay.id = 'xpBootSequenceOverlay';
      overlay.className = 'xp-boot-overlay';
      parentContainer.appendChild(overlay);
    } else {
      overlay.style.display = 'flex';
      overlay.classList.remove('fade-out');
      if (overlay.parentElement !== parentContainer) {
        parentContainer.appendChild(overlay);
      }
    }

    overlay.style.display = 'flex';
    overlay.innerHTML = '';

    // Phase 1: BIOS POST Screen
    overlay.className = 'xp-boot-overlay bios-screen';
    overlay.innerHTML = `
      <div class="bios-header">
        <div style="color:#ffffff; font-weight:700; font-size:14px;">Award Modular BIOS v6.00PG, An Energy Star Ally</div>
        <div style="color:#d4d0c8; font-size:12px; margin-top:4px;">Copyright (C) 1984-2004, Award Software, Inc.</div>
      </div>
      <div class="bios-body" id="biosPostText">
        <br>
        <div>AMD Athlon(tm) 64 X2 Dual Core Processor 3800+</div>
        <div>Memory Testing: <span id="biosMemTest">2097152K</span> OK</div>
        <br>
        <div>Primary Master  : WDC WD400BB-00DEA0 (40GB IDE)</div>
        <div>Primary Slave   : SONY CD-RW CRX210E</div>
        <div>Secondary Master: VIRTUAL JAIL SANDBOX-VM (100GB MAPPED Z:)</div>
        <div>Secondary Slave : NONE</div>
        <br>
        <div style="color:#00ffff;">Detecting IDE devices ... Done.</div>
        <div style="color:#ffffff; margin-top:8px;">ACPI Controller v1.0 Enabled</div>
        <div>Booting from Local Disk (C:)...</div>
      </div>
      <div class="bios-footer">
        <span>Press <b>DEL</b> to enter SETUP, <b>F11</b> for Boot Menu</span>
        <span>11/04/2004-NF-CK804-6A61FE19C-00</span>
      </div>
    `;

    setTimeout(() => {
      // Phase 2: Windows XP Boot Logo Screen
      overlay.className = 'xp-boot-overlay xp-boot-logo-screen';
      overlay.innerHTML = `
        <div class="xp-bootscreen-container">
          <div class="xp-boot-logo-block">
            <img src="/static/xp_assets/boot/bmp_5.png" class="xp-boot-logo-img" alt="Microsoft Windows XP">
            <div class="xp-boot-edition">Professional</div>
          </div>
          <!-- Authentic 3-Segment Glowing Progress Bar -->
          <div class="xp-boot-progress-box">
            <div class="xp-boot-progress-glide">
              <img src="/static/xp_assets/boot/bmp_4.png" style="width:22px; height:9px;" alt="Progress">
            </div>
          </div>
          <div class="xp-boot-copyright">Copyright © Microsoft Corporation</div>
        </div>
      `;

      setTimeout(() => {
        const isInst = window.XPInstaller && window.XPInstaller.isInstalled(activeWs);
        if (!isInst && window.XPInstaller) {
          overlay.style.display = 'none';
          window.XPInstaller.startSetup(onComplete, activeWs);
        } else {
          Audio.playStartup();
          const cfg = window.XPInstaller ? window.XPInstaller.getInstallConfig(activeWs) : null;
          triggerLogonScreen(cfg, onComplete);
        }
      }, 3500);
    }, 2200);
  }

  // -------------------------------------------------------------------------
  // Logon Screen with Credentials Post-it Note
  // -------------------------------------------------------------------------
  function triggerLogonScreen(customCfg = null, onComplete = null) {
    const parentContainer = getDesktopContainer();
    let overlay = document.getElementById('xpBootSequenceOverlay');
    if (!overlay) {
      overlay = document.createElement('div');
      overlay.id = 'xpBootSequenceOverlay';
      overlay.className = 'xp-boot-overlay';
      parentContainer.appendChild(overlay);
    } else {
      overlay.style.display = 'flex';
      overlay.classList.remove('fade-out');
      if (overlay.parentElement !== parentContainer) {
        parentContainer.appendChild(overlay);
      }
    }

    let cfg = customCfg;
    if (!cfg && window.XPInstaller) {
      cfg = window.XPInstaller.getInstallConfig();
    }
    if (!cfg) {
      cfg = {
        userName: 'Rackneh',
        organization: 'Inference Lighting Lab',
        computerName: 'RACKNEH-XP',
        adminPassword: '',
        userTile: '/static/xp_assets/usertiles/dog.bmp'
      };
    }

    overlay.style.display = 'flex';
    overlay.className = 'xp-boot-overlay xp-logon-screen';
    overlay.innerHTML = `
      <div class="xp-logon-top-bar"></div>
      <div class="xp-logon-main-panel">
        <div class="xp-logon-left-brand">
          <div style="margin-bottom:14px;">
            <img src="/static/xp_assets/logon/bmp_100.png" style="width:170px; height:auto; filter:drop-shadow(0 2px 6px rgba(0,0,0,0.45));" alt="Windows XP">
          </div>
          <div class="xp-logon-prompt" style="font-family:'Tahoma', sans-serif; font-size:12px; color:#ffffff; font-weight:700; text-shadow:1px 1px 2px #000;">
            To begin, click your user name
          </div>
        </div>
        <div class="xp-logon-divider"></div>
        <div class="xp-logon-users-col">
          <div class="xp-logon-user-card" id="logonUserPrimary">
            <div class="xp-logon-avatar-box">
              <img src="${cfg.userTile || '/static/xp_assets/usertiles/dog.bmp'}" alt="${cfg.userName}">
            </div>
            <div class="xp-logon-user-info">
              <div class="xp-logon-name">${cfg.userName}</div>
              <div class="xp-logon-sub">Computer: ${cfg.computerName} • Online</div>
              <div class="xp-logon-pw-row" id="logonPwRow" style="display:none; margin-top:4px;">
                <input type="password" id="logonPwInput" placeholder="Type password" style="padding:2px 6px; font-size:11px; border:1px solid #716f64; width:130px; background:#fff; color:#000;">
                <button id="logonSubmitBtn" class="xp-logon-submit-arrow">➔</button>
              </div>
            </div>
          </div>

          <div class="xp-logon-user-card" id="logonUserGuest" style="opacity:0.75;">
            <div class="xp-logon-avatar-box">
              <img src="/static/xp_assets/usertiles/guest.bmp" alt="Guest">
            </div>
            <div class="xp-logon-user-info">
              <div class="xp-logon-name">Guest</div>
              <div class="xp-logon-sub">Guest Account</div>
            </div>
          </div>
        </div>

        <!-- Authentic Yellow Post-it Note with Setup Credentials -->
        <div class="xp-postit-note" id="logonPostitNote">
          <div class="postit-pin">📌</div>
          <div class="postit-tape"></div>
          <div class="postit-header">Setup Credentials</div>
          <div class="postit-content">
            <div><b>User:</b> <span id="postitUser">${cfg.userName}</span></div>
            <div><b>Password:</b> <span id="postitPw">${cfg.adminPassword ? cfg.adminPassword : '(No password set)'}</span></div>
            <div><b>Computer:</b> <span id="postitComp">${cfg.computerName}</span></div>
            <div class="postit-note-tip">Credentials configured during Windows XP Setup.</div>
          </div>
        </div>
      </div>
      <div class="xp-logon-bottom-bar">
        <button class="xp-logon-turnoff-btn" id="logonTurnOffBtn">
          <span class="power-icon">⏻</span>
          <span>Turn off computer</span>
        </button>
        <div style="font-size:11px; color:#ffffff; opacity:0.85;">After you log on, you can add or change accounts in Control Panel.</div>
      </div>
    `;

    const doLogin = () => {
      const userCard = overlay.querySelector('#logonUserPrimary');
      if (userCard) {
        userCard.innerHTML = `
          <div style="padding:16px; color:#ffffff; font-size:14px; font-weight:700;">
            Loading your personal settings...
          </div>
        `;
      }
      Audio.playLogon();
      setTimeout(() => {
        overlay.style.opacity = '0';
        setTimeout(() => {
          overlay.style.display = 'none';
          overlay.style.opacity = '1';
          // Update user identity across simulator
          if (window.XPInstaller) {
            window.XPInstaller.updateSimulatorUserInfo(cfg);
          }
          if (onComplete) onComplete(cfg);
        }, 600);
      }, 1200);
    };

    const card = overlay.querySelector('#logonUserPrimary');
    const pwRow = overlay.querySelector('#logonPwRow');
    const pwInput = overlay.querySelector('#logonPwInput');
    const submitBtn = overlay.querySelector('#logonSubmitBtn');

    card.addEventListener('click', () => {
      if (cfg.adminPassword) {
        if (pwRow.style.display === 'none') {
          pwRow.style.display = 'flex';
          pwInput.value = cfg.adminPassword; // Pre-fill from setup credentials
          pwInput.focus();
        } else {
          doLogin();
        }
      } else {
        doLogin();
      }
    });

    submitBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      if (!cfg.adminPassword || pwInput.value === cfg.adminPassword) {
        doLogin();
      } else {
        Audio.playError();
        alert('The password you typed is incorrect. Please check the post-it note and try again.');
      }
    });

    pwInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        if (!cfg.adminPassword || pwInput.value === cfg.adminPassword) {
          doLogin();
        } else {
          Audio.playError();
          alert('The password you typed is incorrect. Please check the post-it note and try again.');
        }
      }
    });

    overlay.querySelector('#logonUserGuest').addEventListener('click', () => {
      doLogin();
    });

    overlay.querySelector('#logonTurnOffBtn').addEventListener('click', () => {
      showShutdownDialog();
    });
  }

  // -------------------------------------------------------------------------
  // Turn Off Computer Dialog
  // -------------------------------------------------------------------------
  function showShutdownDialog() {
    const parentContainer = getDesktopContainer();
    let dialog = document.getElementById('xpShutdownModal');
    if (!dialog) {
      dialog = document.createElement('div');
      dialog.id = 'xpShutdownModal';
      dialog.className = 'xp-shutdown-backdrop';
      dialog.innerHTML = `
        <div class="xp-shutdown-box">
          <div class="xp-shutdown-header">
            <div class="xp-shutdown-title">
              <span>Windows</span><span style="color:#ea580c; font-size:14px; margin-left:2px;">XP</span>
            </div>
            <div style="font-size:12px; font-weight:700; color:#ffffff;">Turn off computer</div>
          </div>
          <div class="xp-shutdown-body">
            <div class="xp-shutdown-action-btn" id="sdStandbyBtn">
              <img src="/static/xp_assets/icons/xp_btn_standby.png" style="width:40px; height:40px; object-fit:contain; filter:drop-shadow(1px 2px 3px rgba(0,0,0,0.4));" alt="Standby">
              <span>Stand By</span>
            </div>
            <div class="xp-shutdown-action-btn" id="sdTurnoffBtn">
              <img src="/static/xp_assets/icons/xp_btn_turnoff.png" style="width:40px; height:40px; object-fit:contain; filter:drop-shadow(1px 2px 3px rgba(0,0,0,0.4));" alt="Turn Off">
              <span>Turn Off</span>
            </div>
            <div class="xp-shutdown-action-btn" id="sdRestartBtn">
              <img src="/static/xp_assets/icons/xp_btn_restart.png" style="width:40px; height:40px; object-fit:contain; filter:drop-shadow(1px 2px 3px rgba(0,0,0,0.4));" alt="Restart">
              <span>Restart</span>
            </div>
          </div>
          <div class="xp-shutdown-footer">
            <button class="btn btn-secondary btn-sm" id="sdCancelBtn" style="min-width:70px;">Cancel</button>
          </div>
        </div>
      `;
      if (dialog.parentElement !== parentContainer) parentContainer.appendChild(dialog);

      dialog.querySelector('#sdCancelBtn').addEventListener('click', () => {
        dialog.style.display = 'none';
      });

      dialog.querySelector('#sdStandbyBtn').addEventListener('click', () => {
        dialog.style.display = 'none';
        startScreensaver('starfield');
      });

      dialog.querySelector('#sdTurnoffBtn').addEventListener('click', () => {
        dialog.style.display = 'none';
        Audio.playShutdown();
        const overlay = document.getElementById('xpBootSequenceOverlay') || document.createElement('div');
        overlay.id = 'xpBootSequenceOverlay';
        overlay.className = 'xp-boot-overlay';
        overlay.style.display = 'flex';
        overlay.style.background = '#000000';
        overlay.innerHTML = `
          <div style="color:#ea580c; font-family:'Trebuchet MS', Tahoma; font-size:20px; font-weight:700; text-align:center;">
            It is now safe to turn off your computer.
            <div style="margin-top:20px;">
              <button class="btn btn-primary btn-sm" id="sdPowerOnBtn" style="background:#0284c7; color:#fff; font-size:12px;">Power On System</button>
            </div>
          </div>
        `;
        if (overlay.parentElement !== parentContainer) parentContainer.appendChild(overlay);
        overlay.querySelector('#sdPowerOnBtn').addEventListener('click', () => {
          triggerBootSequence();
        });
      });

      dialog.querySelector('#sdRestartBtn').addEventListener('click', () => {
        dialog.style.display = 'none';
        Audio.playShutdown();
        setTimeout(() => {
          triggerBootSequence();
        }, 1200);
      });
    }

    dialog.style.display = 'flex';
  }

  // -------------------------------------------------------------------------
  // 3D Beige Oldschool PC Tower Subsystem (BeigePC)
  // -------------------------------------------------------------------------
  const BeigePC = {
    isPoweredOn: false,
    hddBlinkInterval: null,
    container: null,
    instances: {},

    createChassisHtml(prefix = 'pc', scale = 'large') {
      const chassisHtml = `
        <div class="beige-pc-chassis" id="${prefix}_chassis" title="3D Simulated Windows XP Tower PC. Click the Power button to boot!">
          <div class="chassis-face face-front">
            <!-- 5.25 Inch CD-ROM / DVD Drive Bay -->
            <div class="drive-bay optical-bay">
              <div class="drive-tray"></div>
              <div class="drive-controls">
                <span class="drive-led" id="${prefix}_cdRomLed"></span>
                <button type="button" class="drive-eject-btn" id="${prefix}_cdRomEjectBtn" title="Eject CD-ROM"></button>
              </div>
            </div>

            <!-- 3.5 Inch Floppy Drive Bay -->
            <div class="drive-bay floppy-bay">
              <div class="floppy-slot"></div>
              <div class="floppy-controls">
                <span class="drive-led" id="${prefix}_floppyLed"></span>
                <div class="floppy-eject-pin"></div>
              </div>
            </div>

            <!-- Badges & Brand -->
            <div class="pc-badges">
              <div class="pc-case-badge intel-badge">x86</div>
              <div class="pc-case-badge os-badge">XP</div>
            </div>

            <!-- Front Control Panel (Power & Reset & LEDs) -->
            <div class="pc-power-panel">
              <button type="button" class="pc-power-btn" id="${prefix}_powerBtn" title="Power ON / OFF System">
                <span class="power-sym">⏻</span>
              </button>
              <button type="button" class="pc-reset-btn" id="${prefix}_resetBtn" title="Hardware Reset"></button>
              <div class="pc-leds">
                <div class="led-wrapper">
                  <span class="pc-led power-led" id="${prefix}_powerLed"></span>
                  <small>PWR</small>
                </div>
                <div class="led-wrapper">
                  <span class="pc-led hdd-led" id="${prefix}_hddLed"></span>
                  <small>HDD</small>
                </div>
              </div>
            </div>

            <!-- Lower Ventilation Grills -->
            <div class="pc-intake-grill">
              <span></span><span></span><span></span><span></span><span></span>
            </div>
          </div>

          <!-- 3D Isometric Top and Side Panels -->
          <div class="chassis-face face-top"></div>
          <div class="chassis-face face-side">
            <div class="side-vent-holes"></div>
          </div>
        </div>
      `;

      if (scale === 'small') {
        return `<div class="other-pc-wrapper">${chassisHtml}</div>`;
      }
      return chassisHtml;
    },

    wireChassis(rootEl, prefix = 'pc', onPowerToggle = null, onReset = null) {
      if (!rootEl) return;
      const powerBtn = rootEl.querySelector(`#${prefix}_powerBtn`);
      const resetBtn = rootEl.querySelector(`#${prefix}_resetBtn`);
      const cdBtn = rootEl.querySelector(`#${prefix}_cdRomEjectBtn`);
      const chassis = rootEl.querySelector(`#${prefix}_chassis`);

      if (powerBtn) {
        powerBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          e.preventDefault();
          if (onPowerToggle) onPowerToggle();
          else this.togglePower();
        });
      }

      if (chassis) {
        chassis.addEventListener('click', (e) => {
          if (e.target.closest('button')) return;
          if (onPowerToggle) onPowerToggle();
          else this.togglePower();
        });
      }

      if (resetBtn) {
        resetBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          e.preventDefault();
          Audio.playMechanicalClick();
          if (onReset) onReset();
          else {
            Audio.playPostBeep();
            triggerBootSequence();
          }
        });
      }

      if (cdBtn) {
        cdBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          e.preventDefault();
          Audio.playMechanicalClick();
          const led = rootEl.querySelector(`#${prefix}_cdRomLed`);
          if (led) {
            led.classList.add('active');
            setTimeout(() => led.classList.remove('active'), 800);
          }
        });
      }
    },

    setPowerState(prefix, isOnline, rootEl = document) {
      const pwrLed = rootEl.querySelector(`#${prefix}_powerLed`);
      const hddLed = rootEl.querySelector(`#${prefix}_hddLed`);
      const chassis = rootEl.querySelector(`#${prefix}_chassis`);

      if (this.instances[prefix] && this.instances[prefix].hddInterval) {
        clearInterval(this.instances[prefix].hddInterval);
        this.instances[prefix].hddInterval = null;
      }

      if (isOnline) {
        if (pwrLed) pwrLed.classList.add('power-on');
        if (chassis) chassis.classList.add('pc-running');
        // Intermittent HDD blinking
        const hddInterval = setInterval(() => {
          if (hddLed && Math.random() > 0.35) {
            hddLed.classList.add('active');
            setTimeout(() => {
              if (hddLed) hddLed.classList.remove('active');
            }, 50 + Math.random() * 110);
          }
        }, 220);
        this.instances[prefix] = { isOnline: true, hddInterval };
      } else {
        if (pwrLed) pwrLed.classList.remove('power-on');
        if (hddLed) hddLed.classList.remove('active');
        if (chassis) chassis.classList.remove('pc-running');
        this.instances[prefix] = { isOnline: false, hddInterval: null };
      }
    },

    render(containerEl, projectInfo = null) {
      if (!containerEl) return;
      this.container = containerEl;
      this.isPoweredOn = false;
      containerEl.innerHTML = this.createChassisHtml('activePc', 'large');
      this.wireChassis(containerEl, 'activePc', () => {
        this.togglePower(projectInfo);
      }, () => {
        Audio.playPostBeep();
        triggerBootSequence(null, projectInfo);
      });
    },

    togglePower(projectInfo = null) {
      if (!this.isPoweredOn) {
        this.powerOn(projectInfo);
      } else {
        showShutdownDialog();
      }
    },

    powerOn(projectInfo = null) {
      const activeWs = projectInfo || window.currentActiveWorkspace || (window._cachedWorkspaces && window._cachedWorkspaces[0]) || null;
      if (this.isPoweredOn) return;
      this.isPoweredOn = true;

      Audio.playMechanicalClick();
      this.setPowerState('activePc', true, this.container || document);

      const statusTxt = document.getElementById('activeProjectStatusPill') || (this.container ? this.container.querySelector('#beigePcStatusText') : null);
      if (statusTxt) {
        statusTxt.textContent = 'ONLINE';
        statusTxt.className = 'pc-status-pill online';
      }

      setTimeout(() => {
        Audio.playPostBeep();
        setTimeout(() => {
          triggerBootSequence(null, activeWs);
        }, 500);
      }, 450);
    },

    powerOff() {
      this.isPoweredOn = false;
      this.setPowerState('activePc', false, this.container || document);
      const statusTxt = document.getElementById('activeProjectStatusPill') || (this.container ? this.container.querySelector('#beigePcStatusText') : null);
      if (statusTxt) {
        statusTxt.textContent = 'OFFLINE';
        statusTxt.className = 'pc-status-pill offline';
      }
    }
  };

  return {
    Audio,
    THEMES,
    BeigePC,
    loadSavedTheme,
    applyTheme,
    bringToFront,
    toggleWindow,
    makeDraggable,
    makeResizable,
    startScreensaver,
    stopScreensaver,
    triggerBootSequence,
    triggerLogonScreen,
    showShutdownDialog
  };
})();
