/**
 * SQL Authority Studio 3D - Multi-Database Cognitive Engine (sql_authority.js)
 * Implements: Multi-Database 3D Cognitive Mesh, Spatial Orbital Layer Planes,
 * Cross-Database Relational Traversal, and Interactive Inspector.
 */

window.SQLAuthority = (function() {
  let sqlGraphData = null;
  let sqlActiveDatabase = 'all';
  let sqlViewMode = 'federated'; // 'federated' or 'nodes'
  let sqlSelectedNode = null;
  let sql3dAnimId = null;

  // 3D Camera State
  let sqlCamYaw = 0.5;
  let sqlCamPitch = 0.25;
  let sqlCamZoom = 1.0;
  let sqlCamTargetY = 0;
  let sqlCurrentY = 0;
  let sqlAutoRotate = true;

  // Database Layers Specification
  const DB_LAYERS = {
    'control.sqlite': { y: -1.2, color: '#38bdf8', label: 'control.sqlite (Intent Control)' },
    'domain-code.sqlite': { y: -0.4, color: '#4ade80', label: 'domain-code.sqlite (Code Projections)' },
    'codemap.sqlite': { y: -0.4, color: '#4ade80', label: 'codemap.sqlite (Code Projections)' },
    'domain-higher.sqlite': { y: 0.4, color: '#fb923c', label: 'domain-higher.sqlite (Cognitive Memory)' },
    'higher_memory.sqlite': { y: 0.4, color: '#fb923c', label: 'higher_memory.sqlite (Cognitive Memory)' },
    'domain-math.sqlite': { y: 1.2, color: '#c084fc', label: 'domain-math.sqlite (Shader Projections)' },
    'domain-lower.sqlite': { y: 1.8, color: '#f472b6', label: 'domain-lower.sqlite (Execution Traces)' }
  };

  async function loadSqlGraph(database = 'all', concept = null) {
    const dbAliases = {
      'codemap.sqlite': 'domain-code.sqlite',
      'higher_memory.sqlite': 'domain-higher.sqlite'
    };
    const canonicalDb = dbAliases[database] || database;
    sqlActiveDatabase = database;
    const crumbActive = document.getElementById('crumbActive');
    const statusBar = document.getElementById('sqlStatusBar');

    if (crumbActive) {
      crumbActive.textContent = concept ? `Concept: ${concept}` : (database === 'all' ? 'Federated Cognitive Mesh (All Databases)' : database);
    }

    // Set camera target Y plane based on database
    if (database !== 'all' && DB_LAYERS[database]) {
      sqlCamTargetY = -DB_LAYERS[database].y * 90;
    } else if (canonicalDb !== 'all' && DB_LAYERS[canonicalDb]) {
      sqlCamTargetY = -DB_LAYERS[canonicalDb].y * 90;
    } else {
      sqlCamTargetY = 0;
    }

    let data = null;
    try {
      const activeWs = window.workspaceSelect ? window.workspaceSelect.value : '';
      const params = new URLSearchParams({ database, workspace: activeWs });
      if (concept) params.append('concept', concept);

      const resp = await fetch(`/api/iba/sql/graph?${params}`);
      if (resp.ok) {
        data = await resp.json();
      }
    } catch (err) {}

    // Fallback Multi-Database Graph if offline
    if (!data || !data.nodes || !data.nodes.length) {
      data = {
        database: database,
        nodes: [
          { id: 'concept_control', name: 'Intent Control & Orchestration', label: 'Intent Control', database: 'control.sqlite', table: 'catalog', color: '#38bdf8', x: 0, y: -1.2, z: 0, type: 'concept', payload: { engine: 'IBA O(1) Memory', status: 'active' }, related: [{ id: 'concept_code', name: 'Code Architecture', database: 'domain-code.sqlite', table: 'catalog', relation: 'executes_code' }] },
          { id: 'concept_code', name: 'Code Architecture & Shaders', label: 'Code & Shaders', database: 'domain-code.sqlite', table: 'catalog', color: '#4ade80', x: 0, y: -0.4, z: 0, type: 'concept', payload: { shaders: 'RadianceLiveV3.compute', language: 'HLSL/C#' }, related: [{ id: 'concept_higher', name: 'Higher Cognitive Memory', database: 'domain-higher.sqlite', table: 'catalog', relation: 'patterns' }] },
          { id: 'concept_higher', name: 'Higher Cognitive Memory & Patterns', label: 'Higher Memory', database: 'domain-higher.sqlite', table: 'catalog', color: '#fb923c', x: 0, y: 0.4, z: 0, type: 'concept', payload: { loop_depth: '2 Loops Balanced', kv_cache: 'last_loop' }, related: [{ id: 'concept_math', name: 'Shader Formulae', database: 'domain-math.sqlite', table: 'catalog', relation: 'verifies' }] },
          { id: 'concept_math', name: 'Mathematical Invariants & Bounds', label: 'Math & Bounds', database: 'domain-math.sqlite', table: 'catalog', color: '#c084fc', x: 0, y: 1.2, z: 0, type: 'concept', payload: { dispatch_indirect: 'Structured Buffer', kernel: 9 }, related: [{ id: 'concept_control', name: 'Intent Control', database: 'control.sqlite', table: 'catalog', relation: 'invariants' }] },
          // Layer Entries
          { id: 'task_001', name: 'Task: Compute Shader Bounds Fix', label: 'Shader Bounds Fix', database: 'control.sqlite', table: 'task', pk: 'task_001', concept: 'concept_control', x: 1.2, y: -1.2, z: 0.4, color: '#38bdf8', type: 'entry', payload: { objective: 'Fix kernel index 9 bounds in SQLRadianceLiveV3', status: 'ready' }, related: [{ id: 'proj_shader_cs', name: 'SQLRadianceLiveV3.cs', database: 'domain-code.sqlite', table: 'projection', relation: 'modifies' }] },
          { id: 'proj_shader_cs', name: 'SQLRadianceLiveV3.cs', label: 'SQLRadianceLiveV3.cs', database: 'domain-code.sqlite', table: 'projection', pk: 'doc_cs_01', concept: 'concept_code', x: 1.3, y: -0.4, z: 0.5, color: '#4ade80', type: 'entry', payload: { source: 'Assets/InferenceLighting/Runtime/SQLRadianceLiveV3.cs', rev: 4 }, related: [{ id: 'task_001', name: 'Shader Bounds Fix', database: 'control.sqlite', table: 'task', relation: 'parent_task' }, { id: 'proj_rule_depth', name: 'Recurrent Depth Bounds', database: 'domain-higher.sqlite', table: 'projection', relation: 'governed_by' }] },
          { id: 'proj_rule_depth', name: 'Recurrent Depth Bounds', label: 'Recurrent Bounds', database: 'domain-higher.sqlite', table: 'projection', pk: 'doc_rule_01', concept: 'concept_higher', x: 1.2, y: 0.4, z: 0.3, color: '#fb923c', type: 'entry', payload: { pattern: 'O(1) Context Isolation per loop step' }, related: [{ id: 'proj_shader_cs', name: 'SQLRadianceLiveV3.cs', database: 'domain-code.sqlite', table: 'projection', relation: 'applies_to' }] }
        ],
        links: [
          { source: 'concept_control', target: 'concept_code', weight: 2.5, color: 'rgba(56, 189, 248, 0.5)' },
          { source: 'concept_code', target: 'concept_higher', weight: 2.5, color: 'rgba(74, 222, 128, 0.5)' },
          { source: 'concept_higher', target: 'concept_math', weight: 2.5, color: 'rgba(251, 146, 60, 0.5)' },
          { source: 'task_001', target: 'proj_shader_cs', weight: 1.5, color: 'rgba(74, 222, 128, 0.7)' },
          { source: 'proj_shader_cs', target: 'proj_rule_depth', weight: 1.5, color: 'rgba(251, 146, 60, 0.7)' }
        ]
      };
    }

    sqlGraphData = data;
    if (statusBar) {
      statusBar.textContent = `Loaded ${data.nodes ? data.nodes.length : 0} nodes across databases • ${data.links ? data.links.length : 0} relational links`;
    }

    renderDatabaseButtons(data.databases || []);
    startSql3dRenderer();
  }

  function renderDatabaseButtons(databases) {
    const bar = document.getElementById('sqlDbTabsBar') || document.querySelector('.xp-sql-db-tabs');
    if (!bar) return;
    bar.innerHTML = '';

    const createBtn = (id, label, icon, color, active) => {
      const btn = document.createElement('button');
      btn.className = `btn btn-xs ${active ? 'btn-primary' : 'btn-secondary'}`;
      btn.style.fontSize = '10.5px';
      btn.style.display = 'inline-flex';
      btn.style.alignItems = 'center';
      btn.style.gap = '4px';
      if (active) {
        btn.style.background = '#0284c7';
        btn.style.color = '#ffffff';
        btn.style.fontWeight = '700';
      }
      btn.innerHTML = `<span>${icon}</span><span>${label}</span>`;
      btn.addEventListener('click', () => {
        loadSqlGraph(id);
      });
      return btn;
    };

    bar.appendChild(createBtn('all', 'Federated View', '🌐', '#0284c7', sqlActiveDatabase === 'all'));

    const defaultList = [
      { name: 'control.sqlite', label: 'control.sqlite', icon: '🗄️', color: '#38bdf8' },
      { name: 'domain-code.sqlite', label: 'domain-code.sqlite', icon: '🗺️', color: '#4ade80' },
      { name: 'domain-higher.sqlite', label: 'domain-higher.sqlite', icon: '🧠', color: '#fb923c' },
      { name: 'domain-math.sqlite', label: 'domain-math.sqlite', icon: '📐', color: '#c084fc' }
    ];

    const list = databases.length ? databases : defaultList;
    list.forEach(db => {
      const isAct = sqlActiveDatabase === db.name;
      const icon = db.name.includes('code') ? '🗺️' : (db.name.includes('higher') ? '🧠' : (db.name.includes('math') ? '📐' : '🗄️'));
      bar.appendChild(createBtn(db.name, db.name, icon, db.color, isAct));
    });
  }

  function startSql3dRenderer() {
    const canvas = document.getElementById('sql3dCanvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    if (sql3dAnimId) cancelAnimationFrame(sql3dAnimId);

    // Mouse Controls
    let isDragging = false;
    let lastX = 0, lastY = 0;

    canvas.onmousedown = (e) => {
      isDragging = true;
      lastX = e.clientX;
      lastY = e.clientY;
    };
    window.addEventListener('mouseup', () => { isDragging = false; });
    window.addEventListener('mousemove', (e) => {
      if (!isDragging) return;
      const dx = e.clientX - lastX;
      const dy = e.clientY - lastY;
      lastX = e.clientX;
      lastY = e.clientY;
      sqlCamYaw += dx * 0.008;
      sqlCamPitch = Math.max(-1.2, Math.min(1.2, sqlCamPitch + dy * 0.008));
    });
    canvas.onwheel = (e) => {
      e.preventDefault();
      sqlCamZoom = Math.max(0.4, Math.min(2.5, sqlCamZoom - e.deltaY * 0.001));
    };

    // Canvas click node selection
    canvas.onclick = (e) => {
      const rect = canvas.getBoundingClientRect();
      const clickX = e.clientX - rect.left;
      const clickY = e.clientY - rect.top;

      if (!sqlGraphData || !sqlGraphData.nodes) return;
      let closest = null;
      let minDist = 24;

      sqlGraphData.nodes.forEach(node => {
        if (node._screenX !== undefined && node._screenY !== undefined) {
          const d = Math.hypot(node._screenX - clickX, node._screenY - clickY);
          if (d < minDist) {
            minDist = d;
            closest = node;
          }
        }
      });

      if (closest) {
        selectSqlNode(closest);
      }
    };

    const render = () => {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      const cx = canvas.width / 2;
      const cy = canvas.height / 2;

      // Smooth camera Y transition
      sqlCurrentY += (sqlCamTargetY - sqlCurrentY) * 0.08;

      if (sqlAutoRotate && !isDragging) {
        sqlCamYaw += 0.004;
      }

      // Draw 3D Database Orbital Planes / Rings
      const cosY = Math.cos(sqlCamYaw);
      const sinY = Math.sin(sqlCamYaw);
      const cosP = Math.cos(sqlCamPitch);
      const sinP = Math.sin(sqlCamPitch);

      // Render Spatial Planes for each database layer
      Object.keys(DB_LAYERS).forEach(dbName => {
        const layer = DB_LAYERS[dbName];
        const isLayerActive = sqlActiveDatabase === 'all' || sqlActiveDatabase === dbName;
        const planeY = (layer.y * 90 + sqlCurrentY) * sqlCamZoom;

        // Project plane center in 3D
        const y2 = planeY * cosP;
        const z2 = planeY * sinP;
        const fov = 350;
        const scale = fov / (fov + z2 + 180);
        const screenY = cy + y2 * scale;

        if (screenY > 10 && screenY < canvas.height - 10) {
          ctx.save();
          ctx.strokeStyle = isLayerActive ? layer.color : 'rgba(71, 85, 105, 0.2)';
          ctx.lineWidth = isLayerActive ? 1.5 : 0.8;
          ctx.setLineDash([4, 4]);
          ctx.beginPath();
          ctx.ellipse(cx, screenY, 180 * sqlCamZoom * scale, 45 * sqlCamZoom * scale, 0, 0, Math.PI * 2);
          ctx.stroke();

          // Database plane label tag
          ctx.fillStyle = isLayerActive ? layer.color : 'rgba(148, 163, 184, 0.5)';
          ctx.font = '10px Tahoma, sans-serif';
          ctx.fillText(`⬡ ${dbName}`, cx - 170 * sqlCamZoom * scale, screenY - 6);
          ctx.restore();
        }
      });

      if (!sqlGraphData || !sqlGraphData.nodes) {
        const sqlWin = document.getElementById('unifiedSqlSuiteWindow');
        if (sqlWin && !sqlWin.classList.contains('minimized')) {
          sql3dAnimId = requestAnimationFrame(render);
        }
        return;
      }

      // Project Nodes in 3D Space
      sqlGraphData.nodes.forEach(node => {
        let x = (node.x || 0) * 140 * sqlCamZoom;
        let y = ((node.y || 0) * 90 + sqlCurrentY) * sqlCamZoom;
        let z = (node.z || 0) * 120 * sqlCamZoom;

        // Yaw
        const x1 = x * cosY - z * sinY;
        const z1 = x * sinY + z * cosY;

        // Pitch
        const y2 = y * cosP - z1 * sinP;
        const z2 = y * sinP + z1 * cosP;

        const fov = 350;
        const scale = fov / (fov + z2 + 180);
        node._screenX = cx + x1 * scale;
        node._screenY = cy + y2 * scale;
        node._scale = scale;
        node._depth = z2;
      });

      // Draw Relational Links
      if (sqlGraphData.links) {
        sqlGraphData.links.forEach(l => {
          const s = sqlGraphData.nodes.find(n => n.id === l.source);
          const t = sqlGraphData.nodes.find(n => n.id === l.target);
          if (s && t && s._screenX && t._screenX) {
            const isHighlighted = (sqlSelectedNode && (sqlSelectedNode.id === s.id || sqlSelectedNode.id === t.id));
            ctx.strokeStyle = isHighlighted ? '#ffffff' : (l.color || 'rgba(56, 189, 248, 0.35)');
            ctx.lineWidth = (isHighlighted ? 2.5 : (l.weight || 1.2)) * s._scale;
            ctx.beginPath();
            ctx.moveTo(s._screenX, s._screenY);
            ctx.lineTo(t._screenX, t._screenY);
            ctx.stroke();
          }
        });
      }

      // Sort nodes by depth for correct painter's order
      const sorted = [...sqlGraphData.nodes].sort((a, b) => b._depth - a._depth);

      sorted.forEach(node => {
        const sx = node._screenX;
        const sy = node._screenY;
        const sc = Math.max(0.4, node._scale);
        const radius = (node.type === 'concept' ? 14 : 7) * sc;
        const isSelected = sqlSelectedNode && sqlSelectedNode.id === node.id;
        const isDbMatch = sqlActiveDatabase === 'all' || sqlActiveDatabase === node.database || (sqlActiveDatabase === 'codemap.sqlite' && node.database === 'domain-code.sqlite') || (sqlActiveDatabase === 'higher_memory.sqlite' && node.database === 'domain-higher.sqlite');

        // Outer Glow
        ctx.beginPath();
        ctx.arc(sx, sy, radius + (isSelected ? 6 : 2), 0, Math.PI * 2);
        ctx.fillStyle = isSelected ? 'rgba(255, 255, 255, 0.9)' : (isDbMatch ? (node.color || '#38bdf8') : 'rgba(71, 85, 105, 0.25)');
        ctx.globalAlpha = isDbMatch ? 0.4 : 0.15;
        ctx.fill();
        ctx.globalAlpha = 1.0;

        // Core Node
        ctx.beginPath();
        ctx.arc(sx, sy, radius, 0, Math.PI * 2);
        ctx.fillStyle = isDbMatch ? (node.color || '#38bdf8') : '#475569';
        ctx.fill();
        ctx.lineWidth = isSelected ? 2 : 1;
        ctx.strokeStyle = '#ffffff';
        ctx.stroke();

        // Node Label
        if (sc > 0.45 && isDbMatch) {
          ctx.fillStyle = isSelected ? '#ffffff' : '#cbd5e1';
          ctx.font = `${Math.round((node.type === 'concept' ? 11 : 9.5) * sc)}px Tahoma, sans-serif`;
          ctx.textAlign = 'center';
          ctx.fillText(node.label || node.name, sx, sy + radius + 11 * sc);
        }
      });

      const sqlWin = document.getElementById('unifiedSqlSuiteWindow');
      if (sqlWin && !sqlWin.classList.contains('minimized')) {
        sql3dAnimId = requestAnimationFrame(render);
      }
    };

    render();
  }

  function selectSqlNode(node) {
    sqlSelectedNode = node;
    const inspectorName = document.getElementById('sqlInspectorName');
    const inspectorDb = document.getElementById('sqlInspectorDb');
    const inspectorDigest = document.getElementById('sqlInspectorDigest');
    const inspectorRelated = document.getElementById('sqlInspectorRelated');
    const inspectorTypeBadge = document.getElementById('sqlInspectorTypeBadge');

    if (inspectorName) inspectorName.textContent = node.name || node.label || 'Selected Node';
    if (inspectorTypeBadge) {
      inspectorTypeBadge.textContent = (node.type || 'entry').toUpperCase();
      inspectorTypeBadge.className = `badge ${node.type === 'concept' ? 'badge-primary' : 'badge-info'}`;
    }
    if (inspectorDb) {
      inspectorDb.textContent = `${node.database} > ${node.table} (PK: ${node.pk || node.id})`;
    }
    if (inspectorDigest) {
      inspectorDigest.innerHTML = `<pre style="font-size:10px; color:#cbd5e1; max-height:140px; overflow-y:auto; margin:0;">${JSON.stringify(node.payload || {}, null, 2)}</pre>`;
    }

    // Render Clickable Related Entries in Same or Other Databases
    if (inspectorRelated) {
      inspectorRelated.innerHTML = '';
      const related = node.related || [];
      if (!related.length) {
        inspectorRelated.innerHTML = '<div style="color:#64748b; font-size:10px;">No related cross-database entries linked.</div>';
      } else {
        related.forEach(rel => {
          const chip = document.createElement('div');
          chip.className = 'wlm-question-chip';
          chip.style.display = 'flex';
          chip.style.alignItems = 'center';
          chip.style.justifyContent = 'space-between';
          chip.style.gap = '6px';
          chip.style.padding = '4px 8px';
          chip.style.margin = '3px 0';
          chip.style.cursor = 'pointer';
          chip.style.background = 'rgba(255, 255, 255, 0.05)';
          chip.style.border = '1px solid rgba(56, 189, 248, 0.3)';
          chip.style.borderRadius = '3px';

          chip.innerHTML = `
            <span>🔗 <strong>${rel.database}</strong> &gt; ${rel.name}</span>
            <span style="color:#38bdf8; font-size:9.5px;">[${rel.relation}] ➔</span>
          `;

          // Clicking related entry navigates to that node in 3D!
          chip.addEventListener('click', () => {
            navigateToNode(rel.id);
          });

          inspectorRelated.appendChild(chip);
        });
      }
    }
  }

  function navigateToNode(nodeId) {
    if (!sqlGraphData || !sqlGraphData.nodes) return;
    const targetNode = sqlGraphData.nodes.find(n => n.id === nodeId);
    if (targetNode) {
      selectSqlNode(targetNode);
      // Smooth camera pan to target's layer
      if (targetNode.database && DB_LAYERS[targetNode.database]) {
        sqlCamTargetY = -DB_LAYERS[targetNode.database].y * 90;
      }
      // Calculate yaw angle to face the node
      const angle = Math.atan2(targetNode.z || 0, targetNode.x || 0);
      sqlCamYaw = -angle + Math.PI / 2;
    }
  }

  function resetView() {
    sqlCamYaw = 0.5;
    sqlCamPitch = 0.25;
    sqlCamZoom = 1.0;
    sqlCamTargetY = 0;
  }

  function toggleAutoRotate() {
    sqlAutoRotate = !sqlAutoRotate;
    return sqlAutoRotate;
  }

  return {
    loadSqlGraph,
    selectSqlNode,
    navigateToNode,
    resetView,
    toggleAutoRotate,
    startSql3dRenderer
  };
})();
