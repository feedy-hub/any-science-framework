#!/bin/bash
# Any Science UI extension - run inside an Any Science workspace.
set -euo pipefail

require_workspace() {
  [ -f CLAUDE.md ] && [ -f PROTOCOL.md ] && [ -f .claude/settings.json ] && [ -x scripts/validate.sh ] || {
    echo "ERROR: run this script inside an Any Science workspace."
    exit 1
  }
}

backup_if_exists() {
  local path=$1
  if [ -e "$path" ]; then
    local stamp
    stamp=$(date +%Y%m%d-%H%M%S)
    mkdir -p "$(dirname "$path")"
    cp -a "$path" "$path.bak.$stamp"
    echo "backup: $path -> $path.bak.$stamp"
  fi
}

write_file() {
  local path=$1
  backup_if_exists "$path"
  mkdir -p "$(dirname "$path")"
  cat > "$path"
}

require_workspace
mkdir -p ui/static workspace/inbox scripts .claude/agents .claude/commands

write_file ui/UI_SPEC.md <<'EOF'
# UI_SPEC.md - Any Science UI Extension

## Architecture Rules

1. The UI is a read-only projection of the workspace. It must not write cards, results, or knowledge files.
2. The only write API is `POST /api/inbox`, which writes semi-trusted requests into `workspace/inbox/`.
3. The backend uses only the Python standard library. The frontend is a single local HTML file with no CDN and no npm build step.
4. The server binds only to `127.0.0.1`. Use SSH port forwarding for remote access.
5. Invalid cards must be shown with protocol error codes instead of silently skipped.

## API

| Endpoint | Method | Purpose | Writes |
|---|---|---|---|
| `/api/overview` | GET | Parse cards and board state | No |
| `/api/detail?path=` | GET | Read one workspace file plus results | No |
| `/api/knowledge` | GET | Read insights and graveyard tails | No |
| `/api/hooks` | GET | Read hook log tail | No |
| `/api/inbox` | POST | Write a semi-trusted request to inbox | `workspace/inbox/` only |

## Safety Checklist

- Keep `os.path.commonpath` path checks for all path parameters.
- Keep Host validation on all requests.
- Keep Origin validation on POST requests.
- Keep the server bound to `127.0.0.1`.
- Keep all UI-originated requests semi-trusted and subject to E-SEC-01.
EOF

write_file ui/server.py <<'EOF'
#!/usr/bin/env python3
"""Any Science local UI server.

Read-only workspace projection plus a single semi-trusted inbox write channel.
"""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse
import json
import os
import re
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WS = os.path.join(ROOT, "workspace")
PORT = int(os.environ.get("ANY_SCIENCE_UI_PORT", "8321"))
ALLOWED_HOSTS = {f"127.0.0.1:{PORT}", f"localhost:{PORT}"}
ALLOWED_ORIGINS = {f"http://{host}" for host in ALLOWED_HOSTS}
STATUS_ORDER = [
    "IDEA", "DESIGN", "APPROVED", "RUNNING", "ANALYZED-PENDING",
    "ANALYZED", "ITERATE", "PROMOTE", "KILLED",
]
PASS_REQUIRED = ["APPROVED", "RUNNING", "ANALYZED", "PROMOTE"]
MAX_BODY_BYTES = 20_000


def workspace_path(rel):
    base = os.path.realpath(WS)
    full = os.path.realpath(os.path.join(ROOT, rel))
    try:
        if os.path.commonpath([base, full]) != base:
            return None
    except ValueError:
        return None
    return full if os.path.isfile(full) else None


def read_text(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read()


def tail(path, n=80):
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return f.readlines()[-n:]
    except OSError:
        return []


def parse_card(path):
    rel = os.path.relpath(path, ROOT)
    card = {
        "path": rel,
        "title": os.path.basename(path),
        "status": None,
        "review": None,
        "errors": [],
    }
    try:
        text = read_text(path)
    except OSError as exc:
        card["errors"].append(f"READ:{exc}")
        return card

    title = re.search(r"^#\s+(.+)$", text, re.M)
    if title:
        card["title"] = title.group(1).strip()

    statuses = re.findall(r"^-\s*status:\s*(\S+)", text, re.M)
    if len(statuses) == 0:
        card["errors"].append("E01")
    elif len(statuses) != 1:
        card["errors"].append("E02")
    elif statuses[0] not in STATUS_ORDER:
        card["errors"].append("E02")
    else:
        card["status"] = statuses[0]

    reviews = re.findall(r"^REVIEW:\s*(\S+)", text, re.M)
    if len(reviews) != 1:
        card["errors"].append("E03")
    elif not text.rstrip("\n").splitlines()[-1].startswith("REVIEW:"):
        card["errors"].append("E03")
    elif reviews[0] not in ("PENDING", "PASS", "REVISE"):
        card["errors"].append("E02")
    else:
        card["review"] = reviews[0]

    if card["status"] in PASS_REQUIRED and card["review"] != "PASS":
        card["errors"].append("E04")
    if card["status"] == "KILLED" and "/workspace/ideas/" in "/" + rel.replace(os.sep, "/"):
        graveyard = os.path.join(WS, "knowledge", "graveyard.md")
        idea_id = os.path.splitext(os.path.basename(path))[0]
        if idea_id not in "".join(tail(graveyard, 10000)):
            card["errors"].append("E04")

    return card


def overview():
    cards = []
    ideas = os.path.join(WS, "ideas")
    if os.path.isdir(ideas):
        for name in sorted(os.listdir(ideas)):
            if name.startswith("IDEA-") and name.endswith(".md"):
                card = parse_card(os.path.join(ideas, name))
                card["kind"] = "idea"
                cards.append(card)

    experiments = os.path.join(WS, "experiments")
    if os.path.isdir(experiments):
        for name in sorted(os.listdir(experiments)):
            path = os.path.join(experiments, name, "card.md")
            if os.path.isfile(path):
                card = parse_card(path)
                card["kind"] = "experiment"
                card["exp_id"] = name
                cards.append(card)

    return {"status_order": STATUS_ORDER, "cards": cards}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_args):
        return

    def send_json(self, code, body):
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def send_bytes(self, code, data, content_type):
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def host_ok(self):
        return self.headers.get("Host", "") in ALLOWED_HOSTS

    def origin_ok(self):
        origin = self.headers.get("Origin")
        return origin is None or origin in ALLOWED_ORIGINS

    def do_GET(self):
        if not self.host_ok():
            return self.send_json(403, {"error": "Host validation failed"})

        parsed = urlparse(self.path)
        query = parse_qs(parsed.query)
        if parsed.path == "/":
            index = os.path.join(ROOT, "ui", "static", "index.html")
            if not os.path.isfile(index):
                return self.send_json(500, {"error": "ui/static/index.html missing"})
            return self.send_bytes(200, open(index, "rb").read(), "text/html; charset=utf-8")
        if parsed.path == "/api/overview":
            return self.send_json(200, overview())
        if parsed.path == "/api/detail":
            path = workspace_path(query.get("path", [""])[0])
            if not path:
                return self.send_json(403, {"error": "path is outside workspace or missing"})
            result = {"content": read_text(path)}
            results_dir = os.path.join(os.path.dirname(path), "results")
            if os.path.isdir(results_dir):
                metrics = os.path.join(results_dir, "metrics.json")
                if os.path.isfile(metrics):
                    try:
                        result["metrics"] = json.loads(read_text(metrics))
                    except json.JSONDecodeError:
                        result["metrics_error"] = "E06"
                result["run_log_tail"] = tail(os.path.join(results_dir, "run.log"), 40)
            return self.send_json(200, result)
        if parsed.path == "/api/knowledge":
            return self.send_json(200, {
                "insights": tail(os.path.join(WS, "knowledge", "insights.md"), 120),
                "graveyard": tail(os.path.join(WS, "knowledge", "graveyard.md"), 120),
            })
        if parsed.path == "/api/hooks":
            return self.send_json(200, {
                "log": tail(os.path.join(ROOT, ".claude", "hooks.log"), 80),
            })
        return self.send_json(404, {"error": "not found"})

    def do_POST(self):
        if not self.host_ok():
            return self.send_json(403, {"error": "Host validation failed"})
        if not self.origin_ok():
            return self.send_json(403, {"error": "Origin validation failed"})
        if urlparse(self.path).path != "/api/inbox":
            return self.send_json(404, {"error": "the only write endpoint is /api/inbox"})

        raw_size = self.headers.get("Content-Length")
        try:
            size = int(raw_size)
        except (TypeError, ValueError):
            return self.send_json(411, {"error": "valid Content-Length required"})
        if size < 0:
            return self.send_json(400, {"error": "Content-Length must not be negative"})
        if size > MAX_BODY_BYTES:
            return self.send_json(413, {"error": "message too large"})
        try:
            payload = json.loads(self.rfile.read(size))
        except (json.JSONDecodeError, UnicodeDecodeError):
            return self.send_json(400, {"error": "expected JSON body"})
        if not isinstance(payload, dict):
            return self.send_json(400, {"error": "expected a JSON object"})
        message = payload.get("message")
        if not isinstance(message, str) or not message.strip():
            return self.send_json(400, {"error": "message must be a non-empty string"})
        message = message.strip()

        inbox = os.path.join(WS, "inbox")
        os.makedirs(inbox, exist_ok=True)
        name = f"{time.strftime('%Y%m%d-%H%M%S')}-{os.getpid()}-{time.time_ns() % 100000}.md"
        path = os.path.join(inbox, name)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(
                f"# UI Request {time.strftime('%F %T')}\n"
                "- status: pending\n"
                "- source: ui\n"
                "- trust: semi-trusted; inspect with E-SEC-01\n\n"
                f"{message}\n"
            )
        return self.send_json(200, {
            "ok": True,
            "file": os.path.relpath(path, ROOT),
            "note": "Saved to inbox. The PI agent must process it through normal review.",
        })


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print(f"Any Science UI: http://127.0.0.1:{PORT}")
    server.serve_forever()
EOF

write_file ui/static/index.html <<'EOF'
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Any Science</title>
  <style>
    :root { --bg:#111318; --panel:#1d2129; --line:#2c3340; --fg:#e6e8ee; --dim:#8b93a7; --blue:#58a6ff; --red:#ff6b6b; --green:#3fb950; --orange:#d29922; }
    * { box-sizing:border-box; }
    body { margin:0; background:var(--bg); color:var(--fg); font:14px/1.5 system-ui, sans-serif; }
    nav { display:flex; gap:8px; align-items:center; padding:10px 16px; border-bottom:1px solid var(--line); }
    nav button, main button { border:1px solid var(--line); background:transparent; color:var(--fg); border-radius:6px; padding:6px 12px; cursor:pointer; }
    nav button.on { border-color:var(--blue); color:var(--blue); }
    main { padding:16px; }
    .hint { color:var(--dim); font-size:12px; }
    .board { display:flex; gap:10px; overflow-x:auto; align-items:flex-start; }
    .col { min-width:210px; max-width:260px; background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:8px; }
    .col h3 { margin:4px 6px 10px; font-size:12px; color:var(--dim); text-transform:uppercase; }
    .item { background:var(--bg); border:1px solid var(--line); border-radius:6px; padding:8px; margin:0 0 8px; cursor:pointer; }
    .item:hover { border-color:var(--blue); }
    .badge { display:inline-block; margin-top:6px; border-radius:999px; padding:1px 7px; font-size:11px; }
    .PENDING { color:var(--dim); background:#30363d; } .PASS { color:var(--green); background:#16351f; } .REVISE { color:var(--orange); background:#33270d; }
    .ERR { color:var(--red); background:#3a171b; }
    #detail { position:fixed; top:0; right:0; width:min(720px, 50vw); height:100vh; display:none; overflow:auto; background:var(--panel); border-left:1px solid var(--line); padding:16px; }
    pre { white-space:pre-wrap; background:var(--bg); border:1px solid var(--line); border-radius:6px; padding:10px; }
    textarea { width:100%; min-height:110px; color:var(--fg); background:var(--bg); border:1px solid var(--line); border-radius:6px; padding:8px; }
    table { border-collapse:collapse; } td, th { border:1px solid var(--line); padding:4px 8px; }
    #error { display:none; margin:0; padding:8px 16px; color:var(--red); border-bottom:1px solid var(--line); }
    @media (max-width:760px) { nav .hint { display:none; } main { padding:10px; } #detail { width:100vw; } }
  </style>
</head>
<body>
  <nav>
    <button data-tab="board" class="on">看板</button>
    <button data-tab="knowledge">知识库</button>
    <button data-tab="hooks">门禁日志</button>
    <button data-tab="inbox">发指令</button>
    <span class="hint" style="margin-left:auto">只读投影；写入仅进入 inbox</span>
  </nav>
  <p id="error"></p>
  <main>
    <section id="board" class="tab board"></section>
    <section id="knowledge" class="tab" style="display:none"></section>
    <section id="hooks" class="tab" style="display:none"></section>
    <section id="inbox" class="tab" style="display:none">
      <p class="hint">这里的内容会写入 workspace/inbox/，仍需总管按正常流程审查处理。</p>
      <textarea id="message" placeholder="例：请把 IDEA-003 推进到实验设计"></textarea>
      <p><button id="send">提交到 inbox</button> <span id="result" class="hint"></span></p>
    </section>
  </main>
  <aside id="detail"><p style="text-align:right"><button id="close">关闭</button></p><div id="detailBody"></div></aside>
  <script>
    const $ = s => document.querySelector(s);
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    async function api(url, options) {
      try {
        const response = await fetch(url, options);
        const data = await response.json();
        if (!response.ok) throw new Error(data.error || `HTTP ${response.status}`);
        $('#error').style.display = 'none';
        return data;
      } catch (error) {
        $('#error').textContent = `连接失败：${error.message}`;
        $('#error').style.display = 'block';
        throw error;
      }
    }
    document.querySelectorAll('nav button').forEach(button => button.onclick = () => {
      document.querySelectorAll('nav button').forEach(x => x.classList.remove('on'));
      button.classList.add('on');
      document.querySelectorAll('.tab').forEach(x => x.style.display = 'none');
      const tab = $('#' + button.dataset.tab);
      tab.style.display = button.dataset.tab === 'board' ? 'flex' : 'block';
      load(button.dataset.tab).catch(() => {});
    });
    $('#close').onclick = () => $('#detail').style.display = 'none';
    $('#board').addEventListener('click', event => {
      const item = event.target.closest('.item');
      if (item) openDetail(item.dataset.path).catch(() => {});
    });
    function cardHtml(card) {
      const errors = card.errors || [];
      const badge = errors.length
        ? `<span class="badge ERR">${esc(errors.join(','))} -> validate.sh</span>`
        : `<span class="badge ${esc(card.review)}">REVIEW: ${esc(card.review)}</span>`;
      return `<div class="item" data-path="${esc(card.path)}">${esc(card.title)}<br>${badge}</div>`;
    }
    async function load(tab) {
      if (tab === 'board') {
        const data = await api('/api/overview');
        let html = data.status_order.map(status => {
          const cards = data.cards.filter(card => card.status === status);
          return `<div class="col"><h3>${esc(status)} (${cards.length})</h3>${cards.map(cardHtml).join('')}</div>`;
        }).join('');
        const invalid = data.cards.filter(card => (card.errors || []).length || !card.status);
        if (invalid.length) html += `<div class="col"><h3>协议异常</h3>${invalid.map(cardHtml).join('')}</div>`;
        $('#board').innerHTML = html;
      } else if (tab === 'knowledge') {
        const data = await api('/api/knowledge');
        $('#knowledge').innerHTML = `<h3>Insights</h3><pre>${esc(data.insights.join(''))}</pre><h3>Graveyard</h3><pre>${esc(data.graveyard.join(''))}</pre>`;
      } else if (tab === 'hooks') {
        const data = await api('/api/hooks');
        $('#hooks').innerHTML = `<pre>${esc(data.log.join(''))}</pre>`;
      }
    }
    async function openDetail(path) {
      const data = await api('/api/detail?path=' + encodeURIComponent(path));
      let html = `<pre>${esc(data.content || data.error || '')}</pre>`;
      if (data.metrics) {
        html += '<h3>metrics.json</h3><table><tr><th>方法</th><th>指标</th></tr>' +
          Object.entries(data.metrics).filter(([key]) => key !== '_meta').map(([key, value]) =>
            `<tr><td>${esc(key)}</td><td>${esc(Object.entries(value).map(([k,v]) => k + '=' + JSON.stringify(v)).join('  '))}</td></tr>`
          ).join('') + '</table>';
      }
      if (data.metrics_error) html += `<p class="badge ERR">${esc(data.metrics_error)}</p>`;
      if (data.run_log_tail && data.run_log_tail.length) html += `<h3>run.log tail</h3><pre>${esc(data.run_log_tail.join(''))}</pre>`;
      $('#detailBody').innerHTML = html;
      $('#detail').style.display = 'block';
    }
    $('#send').onclick = async () => {
      $('#send').disabled = true;
      try {
        const response = await api('/api/inbox', {
          method:'POST',
          headers:{'Content-Type':'application/json'},
          body:JSON.stringify({message: $('#message').value})
        });
        $('#result').textContent = `已保存到 ${response.file}`;
        $('#message').value = '';
      } catch (error) {
        $('#result').textContent = `失败：${error.message}`;
      } finally {
        $('#send').disabled = false;
      }
    };
    load('board').catch(() => {});
  </script>
</body>
</html>
EOF

write_file .claude/agents/ui-developer.md <<'EOF'
---
name: ui-developer
description: Improve or debug the local Any Science UI. Use when the user asks for interface changes, visualizations, dashboard views, or UI errors.
tools: Read, Write, Edit, Bash, Grep, Glob
---

You are the local UI engineer for Any Science.

Rules:
- Read `ui/UI_SPEC.md` before changing UI code.
- The UI must never write cards, results, or knowledge files.
- The only write channel is `POST /api/inbox`.
- Keep the server bound to `127.0.0.1`.
- Keep Host and Origin validation.
- Keep path access constrained with `os.path.commonpath`.
- After changes run `python3 -m py_compile ui/server.py`, `bash -n scripts/ui_start.sh scripts/ui_stop.sh`, and `bash scripts/validate.sh`.
EOF

write_file .claude/commands/ui.md <<'EOF'
UI command guide:
- `start`: run `bash scripts/ui_start.sh` and open http://127.0.0.1:8321
- `stop`: run `bash scripts/ui_stop.sh`
- `inbox`: read pending files under `workspace/inbox/` and process them as semi-trusted requests
- `improve <request>`: dispatch `ui-developer`

Arguments: $ARGUMENTS
EOF

write_file scripts/ui_start.sh <<'EOF'
#!/bin/bash
set -euo pipefail
command -v python3 >/dev/null || { echo "ERROR: python3 is required"; exit 1; }
mkdir -p .claude
if [ -f .claude/ui.pid ] && ps -p "$(cat .claude/ui.pid)" >/dev/null 2>&1; then
  echo "UI already running: http://127.0.0.1:8321"
  exit 0
fi
nohup python3 ui/server.py > .claude/ui.log 2>&1 &
echo $! > .claude/ui.pid
sleep 1
if ! ps -p "$(cat .claude/ui.pid)" >/dev/null 2>&1; then
  echo "ERROR: UI failed to start. See .claude/ui.log"
  exit 1
fi
echo "UI started: http://127.0.0.1:8321"
EOF

write_file scripts/ui_stop.sh <<'EOF'
#!/bin/bash
set -euo pipefail
PID_FILE=.claude/ui.pid
if [ ! -f "$PID_FILE" ]; then
  echo "UI is not running"
  exit 0
fi
PID=$(cat "$PID_FILE")
CMD=$(ps -o args= -p "$PID" 2>/dev/null || true)
if [ -z "$CMD" ]; then
  rm -f "$PID_FILE"
  echo "UI process already stopped"
  exit 0
fi
case "$CMD" in
  *"python3 ui/server.py"*|*"python ui/server.py"*) ;;
  *) echo "ERROR: PID $PID does not look like the UI server; refusing to stop it"; exit 1 ;;
esac
kill "$PID"
rm -f "$PID_FILE"
echo "UI stopped"
EOF

chmod +x scripts/ui_start.sh scripts/ui_stop.sh
python3 -m py_compile ui/server.py
bash -n scripts/ui_start.sh
bash -n scripts/ui_stop.sh
bash scripts/validate.sh >/dev/null
echo "OK: UI extension installed. Start with: bash scripts/ui_start.sh"
