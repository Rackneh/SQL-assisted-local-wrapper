/**
 * Windows XP Simulator - Authentic Classic Games Subsystem (xp_games.js)
 * Implements:
 * 1. Klondike Solitaire (sol.exe) using genuine cards.dll 74 card bitmaps (71x96)
 * 2. Minesweeper (winmine.exe) using genuine extracted bitmaps (faces, tiles, LED counters)
 * 3. 3D Pinball Space Cadet (pinball.exe) with canvas table & retro sound effects
 */

window.XPGames = (function() {

  // =========================================================================
  // 1. WINDOWS XP SOLITAIRE (sol.exe)
  // =========================================================================
  let solWin = null;
  let solDeck = [];
  let solWaste = [];
  let solFoundations = [[], [], [], []]; // 4 foundations (Clubs, Diamonds, Hearts, Spades)
  let solTableau = [[], [], [], [], [], [], []]; // 7 columns
  let solCardBackId = 54; // Default classic Castle back
  let solScore = 0;
  let solMoves = 0;
  let solHistory = [];

  // Card mappings for cards.dll:
  // Card 1 = Ace Clubs, 2 = Ace Diamonds, 3 = Ace Hearts, 4 = Ace Spades
  // Card 5 = 2 Clubs, 6 = 2 Diamonds, 7 = 2 Hearts, 8 = 2 Spades
  // ... Card 49 = King Clubs, 50 = King Diamonds, 51 = King Hearts, 52 = King Spades
  const SUITS = ['clubs', 'diamonds', 'hearts', 'spades'];
  const SUIT_SYMBOLS = { clubs: '♣', diamonds: '♦', hearts: '♥', spades: '♠' };
  const SUIT_COLORS = { clubs: 'black', diamonds: 'red', hearts: 'red', spades: 'black' };

  function initSolitaireDeck() {
    const deck = [];
    for (let val = 1; val <= 13; val++) {
      for (let sIdx = 0; sIdx < 4; sIdx++) {
        const id = (val - 1) * 4 + sIdx + 1;
        const suit = SUITS[sIdx];
        deck.push({
          id: id,
          val: val,
          suit: suit,
          color: SUIT_COLORS[suit],
          symbol: SUIT_SYMBOLS[suit],
          faceUp: false,
          imgSrc: `/static/xp_assets/cards/card_${id}.png`
        });
      }
    }
    // Fisher-Yates shuffle
    for (let i = deck.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [deck[i], deck[j]] = [deck[j], deck[i]];
    }
    return deck;
  }

  function startSolitaireGame() {
    solDeck = initSolitaireDeck();
    solWaste = [];
    solFoundations = [[], [], [], []];
    solTableau = [[], [], [], [], [], [], []];
    solScore = 0;
    solMoves = 0;
    solHistory = [];

    // Deal tableau: column i gets i+1 cards, top card is face-up
    for (let col = 0; col < 7; col++) {
      for (let row = 0; row <= col; row++) {
        const card = solDeck.pop();
        card.faceUp = (row === col);
        solTableau[col].push(card);
      }
    }
    renderSolitaireBoard();
  }

  function renderSolitaireBoard() {
    if (!solWin) return;
    const stockEl = solWin.querySelector('#solStockPile');
    const wasteEl = solWin.querySelector('#solWastePile');
    const scoreEl = solWin.querySelector('#solScoreDisplay');

    if (scoreEl) scoreEl.textContent = `Score: ${solScore}  |  Moves: ${solMoves}`;

    // Render Stock
    if (stockEl) {
      stockEl.innerHTML = '';
      if (solDeck.length > 0) {
        const backImg = document.createElement('img');
        backImg.src = `/static/xp_assets/cards/card_${solCardBackId}.png`;
        backImg.className = 'xp-sol-card-img';
        stockEl.appendChild(backImg);
      } else {
        stockEl.innerHTML = '<div class="xp-sol-empty-slot">↺</div>';
      }
    }

    // Render Waste
    if (wasteEl) {
      wasteEl.innerHTML = '';
      if (solWaste.length > 0) {
        const topCard = solWaste[solWaste.length - 1];
        const cardImg = document.createElement('img');
        cardImg.src = topCard.imgSrc;
        cardImg.className = 'xp-sol-card-img';
        cardImg.dataset.source = 'waste';
        cardImg.draggable = true;
        cardImg.ondblclick = () => tryAutoMove(topCard, 'waste');
        wasteEl.appendChild(cardImg);
      }
    }

    // Render 4 Foundations
    for (let f = 0; f < 4; f++) {
      const fEl = solWin.querySelector(`#solFound_${f}`);
      if (!fEl) continue;
      fEl.innerHTML = '';
      const pile = solFoundations[f];
      if (pile.length > 0) {
        const topCard = pile[pile.length - 1];
        const cardImg = document.createElement('img');
        cardImg.src = topCard.imgSrc;
        cardImg.className = 'xp-sol-card-img';
        fEl.appendChild(cardImg);
      } else {
        fEl.innerHTML = `<div class="xp-sol-suit-hint">${SUIT_SYMBOLS[SUITS[f]]}</div>`;
      }
    }

    // Render 7 Tableau Columns
    for (let c = 0; c < 7; c++) {
      const colEl = solWin.querySelector(`#solCol_${c}`);
      if (!colEl) continue;
      colEl.innerHTML = '';
      const pile = solTableau[c];
      pile.forEach((card, idx) => {
        const cardWrapper = document.createElement('div');
        cardWrapper.className = 'xp-tableau-card';
        cardWrapper.style.top = `${idx * 18}px`;

        const img = document.createElement('img');
        img.src = card.faceUp ? card.imgSrc : `/static/xp_assets/cards/card_${solCardBackId}.png`;
        img.className = 'xp-sol-card-img';

        if (card.faceUp) {
          cardWrapper.ondblclick = (e) => {
            e.stopPropagation();
            if (idx === pile.length - 1) tryAutoMove(card, `tableau_${c}`);
          };
          cardWrapper.onclick = (e) => {
            e.stopPropagation();
            handleTableauClick(c, idx);
          };
        }
        cardWrapper.appendChild(img);
        colEl.appendChild(cardWrapper);
      });
    }

    checkSolitaireWin();
  }

  function handleTableauClick(colIdx, cardIdx) {
    const pile = solTableau[colIdx];
    const card = pile[cardIdx];
    if (!card || !card.faceUp) return;

    // Check if card is the top card and can go to foundation
    if (cardIdx === pile.length - 1) {
      if (tryAutoMove(card, `tableau_${colIdx}`)) return;
    }

    // Otherwise check moving to other tableau
    const movingCards = pile.slice(cardIdx);
    for (let targetCol = 0; targetCol < 7; targetCol++) {
      if (targetCol === colIdx) continue;
      const targetPile = solTableau[targetCol];
      if (targetPile.length === 0) {
        if (card.val === 13) { // King on empty
          pile.splice(cardIdx);
          targetPile.push(...movingCards);
          if (pile.length > 0) pile[pile.length - 1].faceUp = true;
          solMoves++;
          solScore += 5;
          renderSolitaireBoard();
          return;
        }
      } else {
        const topTarget = targetPile[targetPile.length - 1];
        if (topTarget.color !== card.color && topTarget.val === card.val + 1) {
          pile.splice(cardIdx);
          targetPile.push(...movingCards);
          if (pile.length > 0) pile[pile.length - 1].faceUp = true;
          solMoves++;
          solScore += 5;
          renderSolitaireBoard();
          return;
        }
      }
    }
  }

  function tryAutoMove(card, source) {
    // Attempt moving to foundation
    for (let f = 0; f < 4; f++) {
      const fPile = solFoundations[f];
      if (fPile.length === 0) {
        if (card.val === 1 && card.suit === SUITS[f]) {
          removeCardFromSource(card, source);
          fPile.push(card);
          solMoves++;
          solScore += 10;
          if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();
          renderSolitaireBoard();
          return true;
        }
      } else {
        const topF = fPile[fPile.length - 1];
        if (topF.suit === card.suit && topF.val + 1 === card.val) {
          removeCardFromSource(card, source);
          fPile.push(card);
          solMoves++;
          solScore += 10;
          if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playClick();
          renderSolitaireBoard();
          return true;
        }
      }
    }
    return false;
  }

  function removeCardFromSource(card, source) {
    if (source === 'waste') {
      solWaste.pop();
    } else if (source.startsWith('tableau_')) {
      const c = parseInt(source.split('_')[1], 10);
      const pile = solTableau[c];
      pile.pop();
      if (pile.length > 0) pile[pile.length - 1].faceUp = true;
    }
  }

  function checkSolitaireWin() {
    const totalFound = solFoundations.reduce((acc, pile) => acc + pile.length, 0);
    if (totalFound === 52) {
      if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playTada();
      setTimeout(() => {
        alert("🎉 Congratulations! You have won Windows XP Solitaire!");
      }, 300);
    }
  }

  function openSolitaire() {
    if (solWin) {
      solWin.style.display = 'flex';
      if (window.XPCore) window.XPCore.bringToFront(solWin);
      return;
    }

    const container = document.getElementById('unifiedXpDesktop') || document.body;
    solWin = document.createElement('div');
    solWin.className = 'xp-window xp-solitaire-window';
    solWin.id = 'unifiedSolitaireWindow';
    solWin.style.left = '90px';
    solWin.style.top = '30px';
    solWin.style.width = '640px';
    solWin.style.height = '480px';
    solWin.style.display = 'flex';
    solWin.style.flexDirection = 'column';

    solWin.innerHTML = `
      <div class="title-bar" id="solTitleHandle">
        <div class="title-bar-text">
          <img src="/static/xp_assets/icons/sol_500.png" class="xp-title-icon" alt="Solitaire">
          <span>Solitaire</span>
        </div>
        <div class="title-bar-controls">
          <button class="xp-caption-btn minimize" id="solMinBtn" title="Minimize"></button>
          <button class="xp-caption-btn maximize" id="solMaxBtn" title="Maximize"></button>
          <button class="xp-caption-btn close" id="solCloseBtn" title="Close"></button>
        </div>
      </div>

      <div class="xp-menubar">
        <div class="xp-menu-item" id="solMenuGame">
          Game
          <div class="xp-dropdown-menu">
            <div class="xp-dropdown-item" id="solDealBtn">Deal (F2)</div>
            <div class="xp-dropdown-item" id="solUndoBtn">Undo</div>
            <div class="xp-dropdown-item" id="solDeckBtn">Deck...</div>
            <div class="xp-dropdown-divider"></div>
            <div class="xp-dropdown-item" id="solExitBtn">Exit</div>
          </div>
        </div>
        <div class="xp-menu-item" id="solMenuHelp">
          Help
          <div class="xp-dropdown-menu">
            <div class="xp-dropdown-item" id="solAboutBtn">About Solitaire</div>
          </div>
        </div>
      </div>

      <div class="xp-sol-board-wrapper" style="flex:1; background:#008000; padding:12px; position:relative; overflow:hidden; user-select:none;">
        <div class="xp-sol-top-row" style="display:flex; justify-content:space-between; margin-bottom:16px;">
          <div style="display:flex; gap:12px;">
            <div id="solStockPile" class="xp-sol-card-slot" style="width:71px; height:96px; cursor:pointer;"></div>
            <div id="solWastePile" class="xp-sol-card-slot" style="width:71px; height:96px;"></div>
          </div>
          <div style="display:flex; gap:12px;">
            <div id="solFound_0" class="xp-sol-card-slot" style="width:71px; height:96px;"></div>
            <div id="solFound_1" class="xp-sol-card-slot" style="width:71px; height:96px;"></div>
            <div id="solFound_2" class="xp-sol-card-slot" style="width:71px; height:96px;"></div>
            <div id="solFound_3" class="xp-sol-card-slot" style="width:71px; height:96px;"></div>
          </div>
        </div>

        <div class="xp-sol-tableau-row" style="display:flex; justify-content:space-between;">
          <div id="solCol_0" class="xp-tableau-col" style="width:71px; min-height:280px; position:relative;"></div>
          <div id="solCol_1" class="xp-tableau-col" style="width:71px; min-height:280px; position:relative;"></div>
          <div id="solCol_2" class="xp-tableau-col" style="width:71px; min-height:280px; position:relative;"></div>
          <div id="solCol_3" class="xp-tableau-col" style="width:71px; min-height:280px; position:relative;"></div>
          <div id="solCol_4" class="xp-tableau-col" style="width:71px; min-height:280px; position:relative;"></div>
          <div id="solCol_5" class="xp-tableau-col" style="width:71px; min-height:280px; position:relative;"></div>
          <div id="solCol_6" class="xp-tableau-col" style="width:71px; min-height:280px; position:relative;"></div>
        </div>
      </div>

      <div class="xp-statusbar" style="display:flex; justify-content:space-between; padding:2px 8px; font-size:11px; background:#ece9d8; border-top:1px solid #716f64;">
        <span id="solScoreDisplay">Score: 0  |  Moves: 0</span>
        <span>Windows XP Solitaire</span>
      </div>
    `;

    container.appendChild(solWin);

    // Wire events
    solWin.querySelector('#solStockPile').onclick = () => {
      if (solDeck.length > 0) {
        const c = solDeck.pop();
        c.faceUp = true;
        solWaste.push(c);
      } else {
        while (solWaste.length > 0) {
          const c = solWaste.pop();
          c.faceUp = false;
          solDeck.push(c);
        }
      }
      solMoves++;
      renderSolitaireBoard();
    };

    solWin.querySelector('#solDealBtn').onclick = () => startSolitaireGame();
    solWin.querySelector('#solCloseBtn').onclick = () => { solWin.style.display = 'none'; };
    solWin.querySelector('#solExitBtn').onclick = () => { solWin.style.display = 'none'; };
    solWin.querySelector('#solAboutBtn').onclick = () => {
      alert("Microsoft Windows XP Solitaire\nClassic 52-Card Klondike Game using authentic cards.dll assets.");
    };

    solWin.querySelector('#solDeckBtn').onclick = () => {
      // Cycle through extracted card backs
      const backs = [53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65];
      const curIdx = backs.indexOf(solCardBackId);
      solCardBackId = backs[(curIdx + 1) % backs.length];
      renderSolitaireBoard();
    };

    if (window.XPCore) {
      window.XPCore.makeDraggable(solWin, solWin.querySelector('#solTitleHandle'));
      window.XPCore.makeResizable(solWin);
      window.XPCore.bringToFront(solWin);
    }

    startSolitaireGame();
  }


  // =========================================================================
  // 2. WINDOWS XP MINESWEEPER (winmine.exe)
  // =========================================================================
  let mineWin = null;
  let mineRows = 9;
  let mineCols = 9;
  let mineCount = 10;
  let mineGrid = [];
  let mineTimer = 0;
  let mineTimerId = null;
  let mineGameOver = false;
  let mineFlagCount = 0;

  function initMinesweeperGame() {
    mineGameOver = false;
    mineFlagCount = 0;
    clearInterval(mineTimerId);
    mineTimer = 0;
    mineTimerId = null;

    mineGrid = [];
    for (let r = 0; r < mineRows; r++) {
      mineGrid[r] = [];
      for (let c = 0; c < mineCols; c++) {
        mineGrid[r][c] = {
          hasMine: false,
          revealed: false,
          flagged: false,
          question: false,
          adj: 0
        };
      }
    }

    // Place mines randomly
    let placed = 0;
    while (placed < mineCount) {
      const rr = Math.floor(Math.random() * mineRows);
      const cc = Math.floor(Math.random() * mineCols);
      if (!mineGrid[rr][cc].hasMine) {
        mineGrid[rr][cc].hasMine = true;
        placed++;
      }
    }

    // Calculate adjacent numbers
    for (let r = 0; r < mineRows; r++) {
      for (let c = 0; c < mineCols; c++) {
        if (mineGrid[r][c].hasMine) continue;
        let count = 0;
        for (let dr = -1; dr <= 1; dr++) {
          for (let dc = -1; dc <= 1; dc++) {
            const nr = r + dr, nc = c + dc;
            if (nr >= 0 && nr < mineRows && nc >= 0 && nc < mineCols && mineGrid[nr][nc].hasMine) {
              count++;
            }
          }
        }
        mineGrid[r][c].adj = count;
      }
    }

    updateMineLeds();
    setSmileyFace('face_smile');
    renderMinesweeperGrid();
  }

  function setSmileyFace(faceName) {
    if (!mineWin) return;
    const btn = mineWin.querySelector('#mineFaceBtn');
    if (btn) btn.innerHTML = `<img src="/static/xp_assets/winmine/${faceName}.png" alt="face" style="width:24px; height:24px; display:block;">`;
  }

  function updateMineLeds() {
    if (!mineWin) return;
    const remaining = Math.max(0, mineCount - mineFlagCount);
    renderLedNumber(mineWin.querySelector('#mineCountLed'), remaining);
    renderLedNumber(mineWin.querySelector('#mineTimerLed'), mineTimer);
  }

  function renderLedNumber(container, num) {
    if (!container) return;
    const str = Math.min(999, Math.max(0, num)).toString().padStart(3, '0');
    container.innerHTML = str.split('').map(ch =>
      `<img src="/static/xp_assets/winmine/digit_${ch}.png" style="width:13px; height:23px; display:inline-block;" alt="${ch}">`
    ).join('');
  }

  function renderMinesweeperGrid() {
    if (!mineWin) return;
    const gridEl = mineWin.querySelector('#mineGridContainer');
    if (!gridEl) return;
    gridEl.innerHTML = '';
    gridEl.style.display = 'grid';
    gridEl.style.gridTemplateColumns = `repeat(${mineCols}, 16px)`;
    gridEl.style.gridTemplateRows = `repeat(${mineRows}, 16px)`;
    gridEl.style.width = `${mineCols * 16}px`;
    gridEl.style.height = `${mineRows * 16}px`;

    for (let r = 0; r < mineRows; r++) {
      for (let c = 0; c < mineCols; c++) {
        const cell = mineGrid[r][c];
        const cellDiv = document.createElement('div');
        cellDiv.className = 'xp-mine-cell';
        cellDiv.style.width = '16px';
        cellDiv.style.height = '16px';
        cellDiv.style.cursor = 'default';

        let imgSrc = '/static/xp_assets/winmine/tile_blank.png';
        if (cell.revealed) {
          if (cell.hasMine) {
            imgSrc = '/static/xp_assets/winmine/tile_bomb_exploded.png';
          } else if (cell.adj > 0) {
            imgSrc = `/static/xp_assets/winmine/tile_${cell.adj}.png`;
          } else {
            imgSrc = '/static/xp_assets/winmine/tile_empty_pressed.png';
          }
        } else if (cell.flagged) {
          imgSrc = '/static/xp_assets/winmine/tile_flag.png';
        } else if (cell.question) {
          imgSrc = '/static/xp_assets/winmine/tile_question.png';
        }

        cellDiv.innerHTML = `<img src="${imgSrc}" style="width:16px; height:16px; display:block;" draggable="false">`;

        // Mouse interaction for shock face
        cellDiv.onmousedown = (e) => {
          if (mineGameOver) return;
          if (e.button === 0 && !cell.revealed && !cell.flagged) {
            setSmileyFace('face_shock');
          }
        };
        cellDiv.onmouseup = () => {
          if (!mineGameOver) setSmileyFace('face_smile');
        };

        cellDiv.onclick = (e) => {
          e.preventDefault();
          if (mineGameOver || cell.flagged) return;
          if (!mineTimerId) {
            mineTimerId = setInterval(() => {
              mineTimer = Math.min(999, mineTimer + 1);
              updateMineLeds();
            }, 1000);
          }
          revealCell(r, c);
        };

        cellDiv.oncontextmenu = (e) => {
          e.preventDefault();
          if (mineGameOver || cell.revealed) return;
          if (!cell.flagged && !cell.question) {
            cell.flagged = true;
            mineFlagCount++;
          } else if (cell.flagged) {
            cell.flagged = false;
            cell.question = true;
            mineFlagCount--;
          } else {
            cell.question = false;
          }
          updateMineLeds();
          renderMinesweeperGrid();
        };

        gridEl.appendChild(cellDiv);
      }
    }
  }

  function revealCell(r, c) {
    const cell = mineGrid[r][c];
    if (cell.revealed || cell.flagged) return;
    cell.revealed = true;

    if (cell.hasMine) {
      mineGameOver = true;
      clearInterval(mineTimerId);
      setSmileyFace('face_dead');
      if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playError();
      // Reveal all mines
      for (let rr = 0; rr < mineRows; rr++) {
        for (let cc = 0; cc < mineCols; cc++) {
          if (mineGrid[rr][cc].hasMine) mineGrid[rr][cc].revealed = true;
        }
      }
      renderMinesweeperGrid();
      return;
    }

    if (cell.adj === 0) {
      // Flood fill reveal
      for (let dr = -1; dr <= 1; dr++) {
        for (let dc = -1; dc <= 1; dc++) {
          const nr = r + dr, nc = c + dc;
          if (nr >= 0 && nr < mineRows && nc >= 0 && nc < mineCols) {
            if (!mineGrid[nr][nc].revealed && !mineGrid[nr][nc].flagged) {
              revealCell(nr, nc);
            }
          }
        }
      }
    }

    // Check win condition
    let unrevealedCount = 0;
    for (let rr = 0; rr < mineRows; rr++) {
      for (let cc = 0; cc < mineCols; cc++) {
        if (!mineGrid[rr][cc].revealed) unrevealedCount++;
      }
    }

    if (unrevealedCount === mineCount) {
      mineGameOver = true;
      clearInterval(mineTimerId);
      setSmileyFace('face_win');
      if (window.XPCore && window.XPCore.Audio) window.XPCore.Audio.playTada();
    }

    renderMinesweeperGrid();
  }

  function openMinesweeper() {
    if (mineWin) {
      mineWin.style.display = 'flex';
      if (window.XPCore) window.XPCore.bringToFront(mineWin);
      return;
    }

    const container = document.getElementById('unifiedXpDesktop') || document.body;
    mineWin = document.createElement('div');
    mineWin.className = 'xp-window xp-minesweeper-window';
    mineWin.id = 'unifiedMinesweeperWindow';
    mineWin.style.left = '200px';
    mineWin.style.top = '70px';
    mineWin.style.display = 'flex';
    mineWin.style.flexDirection = 'column';

    mineWin.innerHTML = `
      <div class="title-bar" id="mineTitleHandle">
        <div class="title-bar-text">
          <img src="/static/xp_assets/icons/winmine_100.png" class="xp-title-icon" alt="Minesweeper">
          <span>Minesweeper</span>
        </div>
        <div class="title-bar-controls">
          <button class="xp-caption-btn minimize" id="mineMinBtn" title="Minimize"></button>
          <button class="xp-caption-btn close" id="mineCloseBtn" title="Close"></button>
        </div>
      </div>

      <div class="xp-menubar">
        <div class="xp-menu-item">
          Game
          <div class="xp-dropdown-menu">
            <div class="xp-dropdown-item" id="mineNewBtn">New (F2)</div>
            <div class="xp-dropdown-divider"></div>
            <div class="xp-dropdown-item" id="mineDiffBeg">Beginner (9x9)</div>
            <div class="xp-dropdown-item" id="mineDiffInt">Intermediate (16x16)</div>
            <div class="xp-dropdown-item" id="mineDiffExp">Expert (30x16)</div>
            <div class="xp-dropdown-divider"></div>
            <div class="xp-dropdown-item" id="mineExitBtn">Exit</div>
          </div>
        </div>
        <div class="xp-menu-item">
          Help
          <div class="xp-dropdown-menu">
            <div class="xp-dropdown-item" id="mineAboutBtn">About Minesweeper</div>
          </div>
        </div>
      </div>

      <div class="xp-mine-outer-frame" style="padding:8px; background:#c0c0c0; border:3px solid; border-color:#fff #808080 #808080 #fff;">
        <!-- Header with counters and face -->
        <div class="xp-mine-header" style="display:flex; justify-content:space-between; align-items:center; padding:4px 6px; margin-bottom:8px; background:#c0c0c0; border:2px solid; border-color:#808080 #fff #fff #808080;">
          <div id="mineCountLed" style="background:#000; border:1px solid #808080; height:23px; display:flex;"></div>
          <button id="mineFaceBtn" style="width:26px; height:26px; padding:0; border:2px solid; border-color:#fff #808080 #808080 #fff; background:#c0c0c0; cursor:pointer; outline:none;"></button>
          <div id="mineTimerLed" style="background:#000; border:1px solid #808080; height:23px; display:flex;"></div>
        </div>

        <!-- Minefield -->
        <div class="xp-mine-grid-frame" style="border:3px solid; border-color:#808080 #fff #fff #808080; display:inline-block;">
          <div id="mineGridContainer"></div>
        </div>
      </div>
    `;

    container.appendChild(mineWin);

    mineWin.querySelector('#mineFaceBtn').onclick = () => initMinesweeperGame();
    mineWin.querySelector('#mineNewBtn').onclick = () => initMinesweeperGame();
    mineWin.querySelector('#mineCloseBtn').onclick = () => { mineWin.style.display = 'none'; };
    mineWin.querySelector('#mineExitBtn').onclick = () => { mineWin.style.display = 'none'; };

    mineWin.querySelector('#mineDiffBeg').onclick = () => {
      mineRows = 9; mineCols = 9; mineCount = 10;
      initMinesweeperGame();
    };
    mineWin.querySelector('#mineDiffInt').onclick = () => {
      mineRows = 16; mineCols = 16; mineCount = 40;
      initMinesweeperGame();
    };
    mineWin.querySelector('#mineDiffExp').onclick = () => {
      mineRows = 16; mineCols = 30; mineCount = 99;
      initMinesweeperGame();
    };
    mineWin.querySelector('#mineAboutBtn').onclick = () => {
      alert("Microsoft Windows XP Minesweeper\nClassic Windows XP Game with genuine extracted sprites.");
    };

    if (window.XPCore) {
      window.XPCore.makeDraggable(mineWin, mineWin.querySelector('#mineTitleHandle'));
      window.XPCore.bringToFront(mineWin);
    }

    initMinesweeperGame();
  }


  // =========================================================================
  // 3. 3D PINBALL SPACE CADET (pinball.exe)
// 3. 3D PINBALL SPACE CADET (pinball.exe)
  // =========================================================================
  let pinballWin = null;
  const pinballAudioCache = {};

  function playPinSound(id) {
    try {
      if (!pinballAudioCache[id]) {
        pinballAudioCache[id] = new Audio(`/static/xp_assets/sounds/pinball/SOUND${id}.wav`);
      }
      const snd = pinballAudioCache[id].cloneNode();
      snd.volume = 0.6;
      snd.play().catch(() => {});
    } catch (e) {}
  }

  function openPinball() {
    if (pinballWin) {
      pinballWin.style.display = 'flex';
      if (window.XPCore) window.XPCore.bringToFront(pinballWin);
      return;
    }

    const container = document.getElementById('unifiedXpDesktop') || document.body;
    pinballWin = document.createElement('div');
    pinballWin.className = 'xp-window xp-pinball-window';
    pinballWin.id = 'unifiedPinballWindow';
    pinballWin.style.left = '120px';
    pinballWin.style.top = '30px';
    pinballWin.style.width = '600px';
    pinballWin.style.height = '520px';
    pinballWin.style.display = 'flex';
    pinballWin.style.flexDirection = 'column';

    pinballWin.innerHTML = `
      <div class="title-bar" id="pinTitleHandle">
        <div class="title-bar-text">
          <img src="/static/xp_assets/icons/pinball_ICON_1.png" class="xp-title-icon" alt="3D Pinball">
          <span>3D Pinball for Windows - Space Cadet</span>
        </div>
        <div class="title-bar-controls">
          <button class="xp-caption-btn minimize" id="pinMinBtn" title="Minimize"></button>
          <button class="xp-caption-btn close" id="pinCloseBtn" title="Close"></button>
        </div>
      </div>

      <div class="xp-menubar">
        <div class="xp-menu-item">
          Game
          <div class="xp-dropdown-menu">
            <div class="xp-dropdown-item" id="pinNewBtn">New Game (F2)</div>
            <div class="xp-dropdown-item" id="pinPauseBtn">Pause / Resume (F3)</div>
            <div class="xp-dropdown-item" id="pinHighScoresBtn">High Scores...</div>
            <div class="xp-dropdown-divider"></div>
            <div class="xp-dropdown-item" id="pinExitBtn">Exit</div>
          </div>
        </div>
        <div class="xp-menu-item">
          Help
          <div class="xp-dropdown-menu">
            <div class="xp-dropdown-item" id="pinAboutBtn">About 3D Pinball</div>
          </div>
        </div>
      </div>

      <div style="flex:1; background:#000000; display:flex; flex-direction:column; align-items:center; justify-content:center; position:relative; overflow:hidden; user-select:none;">
        <canvas id="pinballCanvas" width="560" height="420" style="background:#030712; border:2px solid #1e3a8a; border-radius:4px; box-shadow:0 0 20px rgba(0,100,255,0.35); cursor:default;"></canvas>
        <div style="position:absolute; bottom:8px; display:flex; gap:18px; color:#38bdf8; font-family:'Tahoma', monospace; font-size:11px; text-shadow:0 0 4px #000;">
          <span><strong>SPACE</strong>: Plunger (Hold & Release)</span>
          <span><strong>Z / ◀</strong>: Left Flipper</span>
          <span><strong>/ / ▶</strong>: Right Flipper</span>
          <span><strong>F2</strong>: New Game</span>
        </div>
      </div>
    `;

    container.appendChild(pinballWin);

    pinballWin.querySelector('#pinCloseBtn').onclick = () => { pinballWin.style.display = 'none'; };
    pinballWin.querySelector('#pinExitBtn').onclick = () => { pinballWin.style.display = 'none'; };
    pinballWin.querySelector('#pinAboutBtn').onclick = () => {
      alert("3D Pinball for Windows - Space Cadet\nCinematronics / Maxis / Microsoft\nAuthentic 60-sound FX retro simulation.");
    };

    const canvas = pinballWin.querySelector('#pinballCanvas');
    const ctx = canvas.getContext('2d');

    // Pinball Engine State
    let score = 0;
    let ballsRemaining = 3;
    let ballActive = false;
    let ballX = 525, ballY = 360;
    let ballVx = 0, ballVy = 0;
    let plungerCharge = 0;
    let plungerCharging = false;
    let leftFlipperActive = false;
    let rightFlipperActive = false;
    let isPaused = false;
    let bumperGlow = [0, 0, 0];
    let showSplash = true;

    const splashImg = new Image();
    splashImg.src = '/static/xp_assets/pinball/splash.png';

    // Bumpers definitions
    const bumpers = [
      { x: 230, y: 140, r: 24, score: 500, sound: 4 },
      { x: 330, y: 140, r: 24, score: 500, sound: 5 },
      { x: 280, y: 210, r: 24, score: 1000, sound: 14 }
    ];

    // Hyperspace chute
    const hyperspace = { x: 280, y: 70, r: 30 };

    function resetGame() {
      score = 0;
      ballsRemaining = 3;
      ballActive = false;
      ballX = 525;
      ballY = 360;
      ballVx = 0;
      ballVy = 0;
      showSplash = false;
      playPinSound(1); // Launch sound
    }

    pinballWin.querySelector('#pinNewBtn').onclick = resetGame;
    pinballWin.querySelector('#pinPauseBtn').onclick = () => { isPaused = !isPaused; };
    pinballWin.querySelector('#pinHighScoresBtn').onclick = () => {
      const hs = localStorage.getItem('xp_pinball_highscore') || '250,000';
      alert(`3D Pinball High Scores:\n\n1. Space Cadet Rackneh - ${hs}`);
    };

    // Keyboard controls
    window.addEventListener('keydown', (e) => {
      if (pinballWin.style.display === 'none') return;
      if (e.code === 'Space' && !plungerCharging) {
        e.preventDefault();
        if (showSplash) {
          resetGame();
          return;
        }
        if (!ballActive) plungerCharging = true;
      }
      if (e.code === 'KeyZ' || e.code === 'ArrowLeft') {
        if (!leftFlipperActive) {
          leftFlipperActive = true;
          playPinSound(12);
        }
      }
      if (e.code === 'Slash' || e.code === 'ArrowRight') {
        if (!rightFlipperActive) {
          rightFlipperActive = true;
          playPinSound(12);
        }
      }
      if (e.code === 'F2') {
        e.preventDefault();
        resetGame();
      }
      if (e.code === 'F3') {
        e.preventDefault();
        isPaused = !isPaused;
      }
    });

    window.addEventListener('keyup', (e) => {
      if (pinballWin.style.display === 'none') return;
      if (e.code === 'Space' && plungerCharging) {
        plungerCharging = false;
        if (!ballActive) {
          ballActive = true;
          const power = Math.max(12, Math.min(24, plungerCharge * 0.35));
          ballVy = -power;
          ballVx = (Math.random() - 0.5) * 1.5;
          playPinSound(1); // Plunger launch
        }
        plungerCharge = 0;
      }
      if (e.code === 'KeyZ' || e.code === 'ArrowLeft') {
        leftFlipperActive = false;
      }
      if (e.code === 'Slash' || e.code === 'ArrowRight') {
        rightFlipperActive = false;
      }
    });

    function updatePhysics() {
      if (isPaused || showSplash) return;

      if (plungerCharging) {
        plungerCharge = Math.min(100, plungerCharge + 3);
      }

      if (ballActive) {
        // Gravity
        ballVy += 0.32;

        // Friction
        ballVx *= 0.996;
        ballVy *= 0.996;

        ballX += ballVx;
        ballY += ballVy;

        // Plunger chute wall
        if (ballX > 500 && ballY > 120) {
          if (ballX > 535) { ballX = 535; ballVx = -Math.abs(ballVx) * 0.5; }
          if (ballX < 515 && ballY > 150) { ballX = 515; ballVx = Math.abs(ballVx) * 0.5; }
        }

        // Top arch curvature
        if (ballY < 50) {
          ballY = 50;
          ballVy = Math.abs(ballVy) * 0.7;
          ballVx += (280 - ballX) * 0.03;
        }

        // Outer side walls
        if (ballX < 35) { ballX = 35; ballVx = Math.abs(ballVx) * 0.75; playPinSound(17); }
        if (ballX > 540) { ballX = 540; ballVx = -Math.abs(ballVx) * 0.75; playPinSound(17); }

        // Bumper collisions
        bumpers.forEach((b, idx) => {
          const dx = ballX - b.x;
          const dy = ballY - b.y;
          const dist = Math.hypot(dx, dy);
          if (dist < b.r + 9) {
            // Collision response
            const angle = Math.atan2(dy, dx);
            const kick = 9.5;
            ballVx = Math.cos(angle) * kick;
            ballVy = Math.sin(angle) * kick;
            score += b.score;
            bumperGlow[idx] = 12;
            playPinSound(b.sound);
          }
        });

        // Hyperspace chute entrance
        const hdx = ballX - hyperspace.x;
        const hdy = ballY - hyperspace.y;
        if (Math.hypot(hdx, hdy) < hyperspace.r) {
          score += 2500;
          ballX = 140;
          ballY = 160;
          ballVx = 3;
          ballVy = 4;
          playPinSound(13); // Hyperspace warp
        }

        // Flipper collision detection
        const lfX = 200, lfY = 355;
        const rfX = 340, rfY = 355;

        // Left Flipper zone
        if (ballX >= 180 && ballX <= 265 && ballY >= 340 && ballY <= 380) {
          if (leftFlipperActive) {
            ballVy = -11.5;
            ballVx = 4 + (ballX - 220) * 0.15;
            score += 150;
            playPinSound(18);
          } else {
            // Rest bounce
            ballVy = -Math.abs(ballVy) * 0.4;
            ballVx += 1.2;
          }
        }

        // Right Flipper zone
        if (ballX >= 275 && ballX <= 360 && ballY >= 340 && ballY <= 380) {
          if (rightFlipperActive) {
            ballVy = -11.5;
            ballVx = -4 + (ballX - 320) * 0.15;
            score += 150;
            playPinSound(18);
          } else {
            ballVy = -Math.abs(ballVy) * 0.4;
            ballVx -= 1.2;
          }
        }

        // Slingshots (left & right angled bounce)
        if (ballY > 260 && ballY < 330) {
          if (ballX > 90 && ballX < 140 && ballVx < 0) {
            ballVx = 6.5;
            ballVy -= 3;
            score += 300;
            playPinSound(16);
          }
          if (ballX > 400 && ballX < 450 && ballVx > 0) {
            ballVx = -6.5;
            ballVy -= 3;
            score += 300;
            playPinSound(16);
          }
        }

        // Ball drain (bottom)
        if (ballY > 425) {
          ballActive = false;
          playPinSound(3); // Drain sound
          ballsRemaining--;
          if (ballsRemaining > 0) {
            ballX = 525;
            ballY = 360;
            ballVx = 0;
            ballVy = 0;
          } else {
            // Game Over
            playPinSound(240); // Game Over
            const prevBest = parseInt(localStorage.getItem('xp_pinball_highscore') || '0', 10);
            if (score > prevBest) {
              localStorage.setItem('xp_pinball_highscore', score.toString());
            }
            setTimeout(() => {
              alert(`GAME OVER!\n\nFinal Score: ${score.toLocaleString()}`);
              showSplash = true;
            }, 300);
          }
        }
      }

      // Decay bumper glow
      for (let i = 0; i < 3; i++) {
        if (bumperGlow[i] > 0) bumperGlow[i]--;
      }
    }

    function renderPinballLoop() {
      if (pinballWin.style.display !== 'none') {
        updatePhysics();

        // 1. Clear background
        ctx.fillStyle = '#050814';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        if (showSplash && splashImg.complete) {
          // Draw authentic splash screen
          const sw = 360, sh = 250;
          ctx.drawImage(splashImg, (canvas.width - sw) / 2, 50, sw, sh);
          ctx.fillStyle = '#38bdf8';
          ctx.font = 'bold 16px "Tahoma", sans-serif';
          ctx.textAlign = 'center';
          ctx.fillText("PRESS SPACE OR F2 TO LAUNCH", canvas.width / 2, 340);
          ctx.font = '12px "Tahoma", sans-serif';
          ctx.fillStyle = '#94a3b8';
          ctx.fillText("SPACE: Plunger | Z: Left Flipper | /: Right Flipper", canvas.width / 2, 370);
          ctx.textAlign = 'start';
        } else {
          // Table borders & cosmic background art
          ctx.strokeStyle = '#1e3a8a';
          ctx.lineWidth = 4;
          ctx.strokeRect(18, 18, canvas.width - 36, canvas.height - 36);

          // Table outer arch
          ctx.beginPath();
          ctx.arc(280, 100, 240, Math.PI, 0, false);
          ctx.strokeStyle = '#2563eb';
          ctx.lineWidth = 3;
          ctx.stroke();

          // Plunger chute divider
          ctx.beginPath();
          ctx.moveTo(510, 140);
          ctx.lineTo(510, 400);
          ctx.strokeStyle = '#3b82f6';
          ctx.lineWidth = 2;
          ctx.stroke();

          // Hyperspace chute
          ctx.fillStyle = '#0f172a';
          ctx.beginPath();
          ctx.arc(hyperspace.x, hyperspace.y, hyperspace.r, 0, Math.PI * 2);
          ctx.fill();
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 2;
          ctx.stroke();
          ctx.fillStyle = '#38bdf8';
          ctx.font = 'bold 10px Tahoma';
          ctx.fillText("HYPERSPACE", hyperspace.x - 32, hyperspace.y + 4);

          // Slingshots
          ctx.fillStyle = '#1e293b';
          ctx.strokeStyle = '#f59e0b';
          ctx.lineWidth = 2;
          // Left slingshot
          ctx.beginPath();
          ctx.moveTo(90, 270); ctx.lineTo(135, 330); ctx.lineTo(90, 330); ctx.closePath();
          ctx.fill(); ctx.stroke();
          // Right slingshot
          ctx.beginPath();
          ctx.moveTo(450, 270); ctx.lineTo(405, 330); ctx.lineTo(450, 330); ctx.closePath();
          ctx.fill(); ctx.stroke();

          // Bumpers
          bumpers.forEach((b, idx) => {
            const glow = bumperGlow[idx];
            ctx.fillStyle = glow > 0 ? '#fef08a' : '#ef4444';
            ctx.beginPath();
            ctx.arc(b.x, b.y, b.r, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = glow > 0 ? '#ffffff' : '#fca5a5';
            ctx.lineWidth = glow > 0 ? 4 : 2;
            ctx.stroke();
            // Center ring
            ctx.fillStyle = '#fbbf24';
            ctx.beginPath();
            ctx.arc(b.x, b.y, b.r * 0.45, 0, Math.PI * 2);
            ctx.fill();
          });

          // Flippers
          // Left Flipper
          ctx.save();
          ctx.translate(200, 360);
          ctx.rotate(leftFlipperActive ? -0.45 : 0.25);
          ctx.fillStyle = '#eab308';
          ctx.strokeStyle = '#ca8a04';
          ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.roundRect(0, -6, 62, 12, 6);
          ctx.fill(); ctx.stroke();
          ctx.restore();

          // Right Flipper
          ctx.save();
          ctx.translate(340, 360);
          ctx.rotate(rightFlipperActive ? 0.45 : -0.25);
          ctx.fillStyle = '#eab308';
          ctx.strokeStyle = '#ca8a04';
          ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.roundRect(-62, -6, 62, 12, 6);
          ctx.fill(); ctx.stroke();
          ctx.restore();

          // Ball
          ctx.fillStyle = '#f8fafc';
          ctx.beginPath();
          ctx.arc(ballX, ballY, 8, 0, Math.PI * 2);
          ctx.fill();
          ctx.strokeStyle = '#94a3b8';
          ctx.lineWidth = 1.5;
          ctx.stroke();

          // Plunger power meter
          if (plungerCharging) {
            ctx.fillStyle = '#ef4444';
            ctx.fillRect(520, 400 - (plungerCharge * 0.7), 12, plungerCharge * 0.7);
          }

          // Authentic Retro Digital LED Scoreboard
          ctx.fillStyle = 'rgba(0,0,0,0.7)';
          ctx.fillRect(28, 25, 200, 38);
          ctx.strokeStyle = '#1e3a8a';
          ctx.strokeRect(28, 25, 200, 38);

          ctx.fillStyle = '#38bdf8';
          ctx.font = 'bold 15px "Courier New", monospace';
          ctx.fillText(`SCORE: ${score.toString().padStart(8, '0')}`, 34, 44);
          ctx.font = 'bold 11px Tahoma';
          ctx.fillStyle = '#e2e8f0';
          ctx.fillText(`BALL: ${ballsRemaining}  |  RANK: CADET`, 34, 58);
        }
      }
      requestAnimationFrame(renderPinballLoop);
    }

    renderPinballLoop();

    if (window.XPCore) {
      window.XPCore.makeDraggable(pinballWin, pinballWin.querySelector('#pinTitleHandle'));
      window.XPCore.bringToFront(pinballWin);
    }
  }

  return {
    openSolitaire: openSolitaire,
    openMinesweeper: openMinesweeper,
    openPinball: openPinball
  };

})();
