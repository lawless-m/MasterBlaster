namespace MasterBlaster.Explore;

public static class ExploreHtml
{
    public const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>MasterBlaster Explorer</title>
<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: 'Segoe UI', system-ui, sans-serif; background: #1a1a2e; color: #e0e0e0; height: 100vh; display: flex; flex-direction: column; }
header { background: #16213e; padding: 10px 20px; display: flex; align-items: center; gap: 16px; border-bottom: 1px solid #0f3460; }
header h1 { font-size: 18px; color: #e94560; }
#status-badge { padding: 3px 10px; border-radius: 12px; font-size: 12px; font-weight: 600; }
.connected { background: #0a3d2a; color: #4ade80; }
.disconnected { background: #3d0a0a; color: #f87171; }

.main { display: flex; flex: 1; overflow: hidden; }
.screenshot-panel { flex: 1; display: flex; flex-direction: column; padding: 12px; min-width: 0; }
.screenshot-wrap { flex: 1; position: relative; background: #0f0f23; border-radius: 6px; overflow: hidden; display: flex; align-items: center; justify-content: center; }
.screenshot-wrap img { max-width: 100%; max-height: 100%; object-fit: contain; cursor: crosshair; }
#coords { position: absolute; bottom: 8px; left: 8px; background: rgba(0,0,0,0.7); color: #aaa; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-family: monospace; }
.screenshot-bar { display: flex; gap: 8px; margin-top: 8px; }

.sidebar { width: 380px; min-width: 380px; background: #16213e; border-left: 1px solid #0f3460; display: flex; flex-direction: column; overflow: hidden; }
.sidebar section { padding: 12px; border-bottom: 1px solid #0f3460; }
.sidebar section h2 { font-size: 13px; text-transform: uppercase; color: #888; margin-bottom: 8px; letter-spacing: 0.5px; }

.btn-row { display: flex; gap: 6px; flex-wrap: wrap; }
button { background: #0f3460; color: #e0e0e0; border: 1px solid #1a4a7a; padding: 6px 12px; border-radius: 4px; cursor: pointer; font-size: 13px; }
button:hover { background: #1a4a7a; }
button.primary { background: #e94560; border-color: #e94560; color: #fff; }
button.primary:hover { background: #c93550; }
button:disabled { opacity: 0.5; cursor: not-allowed; }

input[type="text"], textarea { background: #0f0f23; color: #e0e0e0; border: 1px solid #333; border-radius: 4px; padding: 6px 10px; font-size: 13px; width: 100%; font-family: inherit; }
input[type="text"]:focus, textarea:focus { outline: none; border-color: #e94560; }

.input-row { display: flex; gap: 6px; }
.input-row input { flex: 1; }

.chat-messages { max-height: 200px; overflow-y: auto; font-size: 13px; margin-bottom: 8px; }
.chat-msg { margin-bottom: 8px; padding: 6px 8px; border-radius: 4px; }
.chat-msg.user { background: #0f3460; }
.chat-msg.assistant { background: #1a2744; border-left: 3px solid #e94560; white-space: pre-wrap; }

.mbl-output { background: #0f0f23; border: 1px solid #333; border-radius: 4px; padding: 8px; font-family: 'Cascadia Code', 'Consolas', monospace; font-size: 12px; white-space: pre-wrap; max-height: 200px; overflow-y: auto; margin-top: 8px; }

.history-panel { border-top: 1px solid #0f3460; max-height: 150px; overflow-y: auto; padding: 8px 20px; font-size: 12px; font-family: monospace; }
.history-panel div { padding: 2px 0; color: #888; }
.history-panel span.type { color: #e94560; font-weight: 600; }

.spinner { display: none; width: 16px; height: 16px; border: 2px solid #555; border-top-color: #e94560; border-radius: 50%; animation: spin 0.6s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.loading .spinner { display: inline-block; }
</style>
</head>
<body>
<header>
  <h1>MasterBlaster Explorer</h1>
  <span id="status-badge" class="disconnected">Checking...</span>
  <span style="flex:1"></span>
  <button onclick="refreshScreenshot()">Refresh</button>
</header>

<div class="main">
  <div class="screenshot-panel">
    <div class="screenshot-wrap">
      <img id="screenshot" src="" alt="RDP Screenshot" onclick="onScreenshotClick(event)" onmousemove="onScreenshotMove(event)">
      <div id="coords"></div>
    </div>
  </div>

  <div class="sidebar">
    <section>
      <h2>Actions</h2>
      <div class="input-row" style="margin-bottom:6px">
        <input type="text" id="type-input" placeholder="Text to type..." onkeydown="if(event.key==='Enter')doType()">
        <button onclick="doType()">Type</button>
      </div>
      <div class="input-row" style="margin-bottom:6px">
        <input type="text" id="key-input" placeholder="Key combo (e.g. Ctrl+S)" onkeydown="if(event.key==='Enter')doKeyCombo()">
        <button onclick="doKeyCombo()">Key</button>
      </div>
      <div class="btn-row">
        <button onclick="setClickMode('doubleclick')">Dbl Click</button>
        <button onclick="setClickMode('rightclick')">Rt Click</button>
        <button id="click-mode-label" style="background:transparent;border:none;color:#888;cursor:default;font-size:12px">Mode: click</button>
      </div>
    </section>

    <section style="flex:1;display:flex;flex-direction:column;overflow:hidden">
      <h2>Claude Chat</h2>
      <div class="chat-messages" id="chat-messages"></div>
      <div class="input-row">
        <input type="text" id="chat-input" placeholder="Ask Claude about the screen..." onkeydown="if(event.key==='Enter')doChat()">
        <button class="primary" onclick="doChat()" id="chat-btn">Ask</button>
        <div class="spinner" id="chat-spinner"></div>
      </div>
    </section>

    <section>
      <h2>MBL Generator</h2>
      <div class="input-row">
        <input type="text" id="mbl-name" placeholder="Task name..." value="My Task">
        <button class="primary" onclick="doSuggestMbl()" id="mbl-btn">Generate</button>
        <div class="spinner" id="mbl-spinner"></div>
      </div>
      <div class="mbl-output" id="mbl-output" style="display:none"></div>
      <button id="mbl-copy" style="display:none;margin-top:6px" onclick="copyMbl()">Copy MBL</button>
    </section>
  </div>
</div>

<div class="history-panel" id="history-panel"></div>

<script>
let rdpWidth = 1920, rdpHeight = 1080;
let clickMode = 'click';

async function refreshScreenshot() {
  const img = document.getElementById('screenshot');
  img.src = '/api/screenshot?' + Date.now();
}

async function checkStatus() {
  try {
    const r = await fetch('/api/status');
    const d = await r.json();
    const badge = document.getElementById('status-badge');
    rdpWidth = d.rdpWidth; rdpHeight = d.rdpHeight;
    if (d.rdpConnected) {
      badge.textContent = 'Connected';
      badge.className = 'connected';
    } else {
      badge.textContent = 'Disconnected';
      badge.className = 'disconnected';
    }
  } catch(e) {
    document.getElementById('status-badge').textContent = 'Error';
    document.getElementById('status-badge').className = 'disconnected';
  }
}

function mapCoords(event) {
  const img = document.getElementById('screenshot');
  const rect = img.getBoundingClientRect();
  const scaleX = rdpWidth / img.naturalWidth;
  const scaleY = rdpHeight / img.naturalHeight;

  const displayAspect = rect.width / rect.height;
  const imageAspect = img.naturalWidth / img.naturalHeight;

  let renderW, renderH, offsetX, offsetY;
  if (displayAspect > imageAspect) {
    renderH = rect.height;
    renderW = rect.height * imageAspect;
    offsetX = (rect.width - renderW) / 2;
    offsetY = 0;
  } else {
    renderW = rect.width;
    renderH = rect.width / imageAspect;
    offsetX = 0;
    offsetY = (rect.height - renderH) / 2;
  }

  const imgX = ((event.clientX - rect.left - offsetX) / renderW) * img.naturalWidth;
  const imgY = ((event.clientY - rect.top - offsetY) / renderH) * img.naturalHeight;

  return { x: Math.round(imgX * scaleX), y: Math.round(imgY * scaleY) };
}

function onScreenshotMove(event) {
  const c = mapCoords(event);
  document.getElementById('coords').textContent = c.x + ', ' + c.y;
}

async function onScreenshotClick(event) {
  const c = mapCoords(event);
  if (c.x < 0 || c.y < 0 || c.x > rdpWidth || c.y > rdpHeight) return;

  const endpoint = '/api/' + clickMode;
  await fetch(endpoint, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({x: c.x, y: c.y})
  });

  addHistory(clickMode, '(' + c.x + ', ' + c.y + ')');

  if (clickMode !== 'click') {
    clickMode = 'click';
    document.getElementById('click-mode-label').textContent = 'Mode: click';
  }

  setTimeout(refreshScreenshot, 500);
}

function setClickMode(mode) {
  clickMode = mode;
  document.getElementById('click-mode-label').textContent = 'Mode: ' + mode;
}

async function doType() {
  const input = document.getElementById('type-input');
  const text = input.value.trim();
  if (!text) return;
  await fetch('/api/type', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({text})
  });
  addHistory('type', text);
  input.value = '';
  setTimeout(refreshScreenshot, 500);
}

async function doKeyCombo() {
  const input = document.getElementById('key-input');
  const combo = input.value.trim();
  if (!combo) return;
  await fetch('/api/keycombo', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({combo})
  });
  addHistory('keycombo', combo);
  input.value = '';
  setTimeout(refreshScreenshot, 500);
}

async function doChat() {
  const input = document.getElementById('chat-input');
  const prompt = input.value.trim();
  if (!prompt) return;
  input.value = '';

  const msgs = document.getElementById('chat-messages');
  msgs.innerHTML += '<div class="chat-msg user">' + escHtml(prompt) + '</div>';

  document.getElementById('chat-btn').disabled = true;
  document.getElementById('chat-spinner').style.display = 'inline-block';

  try {
    const r = await fetch('/api/chat', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({prompt})
    });
    const d = await r.json();
    msgs.innerHTML += '<div class="chat-msg assistant">' + escHtml(d.response) + '</div>';
  } catch(e) {
    msgs.innerHTML += '<div class="chat-msg assistant" style="color:#f87171">Error: ' + escHtml(e.message) + '</div>';
  }
  document.getElementById('chat-btn').disabled = false;
  document.getElementById('chat-spinner').style.display = 'none';
  msgs.scrollTop = msgs.scrollHeight;
}

async function doSuggestMbl() {
  const taskName = document.getElementById('mbl-name').value.trim() || 'My Task';
  document.getElementById('mbl-btn').disabled = true;
  document.getElementById('mbl-spinner').style.display = 'inline-block';

  try {
    const r = await fetch('/api/suggest-mbl', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({taskName})
    });
    const d = await r.json();
    const out = document.getElementById('mbl-output');
    out.textContent = d.mbl;
    out.style.display = 'block';
    document.getElementById('mbl-copy').style.display = 'block';
  } catch(e) {
    document.getElementById('mbl-output').textContent = 'Error: ' + e.message;
    document.getElementById('mbl-output').style.display = 'block';
  }
  document.getElementById('mbl-btn').disabled = false;
  document.getElementById('mbl-spinner').style.display = 'none';
}

function copyMbl() {
  const text = document.getElementById('mbl-output').textContent;
  navigator.clipboard.writeText(text);
}

function addHistory(type, detail) {
  const panel = document.getElementById('history-panel');
  const time = new Date().toLocaleTimeString();
  panel.innerHTML += '<div>' + time + ' <span class="type">[' + type + ']</span> ' + escHtml(detail) + '</div>';
  panel.scrollTop = panel.scrollHeight;
}

function escHtml(s) {
  const d = document.createElement('div');
  d.textContent = s;
  return d.innerHTML;
}

checkStatus();
refreshScreenshot();
setInterval(checkStatus, 5000);
</script>
</body>
</html>
""";
}
