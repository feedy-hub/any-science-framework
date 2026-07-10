#!/usr/bin/env python3
"""Any Science local UI server."""

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
    "IDEA",
    "DESIGN",
    "APPROVED",
    "RUNNING",
    "ANALYZED-PENDING",
    "ANALYZED",
    "ITERATE",
    "PROMOTE",
    "KILLED",
]
PASS_REQUIRED = ["APPROVED", "RUNNING", "ANALYZED", "PROMOTE"]
MAX_BODY_BYTES = 20_000


def workspace_path(relative):
    base = os.path.realpath(WS)
    full = os.path.realpath(os.path.join(ROOT, relative))
    try:
        if os.path.commonpath([base, full]) != base:
            return None
    except ValueError:
        return None
    return full if os.path.isfile(full) else None


def read_text(path):
    with open(path, encoding="utf-8", errors="replace") as stream:
        return stream.read()


def tail(path, count=80):
    try:
        with open(path, encoding="utf-8", errors="replace") as stream:
            return stream.readlines()[-count:]
    except OSError:
        return []


def parse_card(path):
    relative = os.path.relpath(path, ROOT)
    card = {
        "path": relative,
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
    if not statuses:
        card["errors"].append("E01")
    elif len(statuses) != 1 or statuses[0] not in STATUS_ORDER:
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
    if card["status"] == "KILLED" and "/workspace/ideas/" in "/" + relative.replace(os.sep, "/"):
        graveyard = os.path.join(WS, "knowledge", "graveyard.md")
        idea_id = os.path.splitext(os.path.basename(path))[0]
        if idea_id not in "".join(tail(graveyard, 10_000)):
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
        self.send_bytes(code, data, "application/json; charset=utf-8")

    def send_bytes(self, code, data, content_type):
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy", "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'")
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
            with open(index, "rb") as stream:
                return self.send_bytes(200, stream.read(), "text/html; charset=utf-8")
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
            return self.send_json(
                200,
                {
                    "insights": tail(os.path.join(WS, "knowledge", "insights.md"), 120),
                    "graveyard": tail(os.path.join(WS, "knowledge", "graveyard.md"), 120),
                },
            )
        if parsed.path == "/api/hooks":
            return self.send_json(200, {"log": tail(os.path.join(ROOT, ".claude", "hooks.log"), 80)})
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
        with open(path, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(
                f"# UI Request {time.strftime('%F %T')}\n"
                "- status: pending\n"
                "- source: ui\n"
                "- trust: semi-trusted; inspect with E-SEC-01\n\n"
                f"{message}\n"
            )
        return self.send_json(
            200,
            {
                "ok": True,
                "file": os.path.relpath(path, ROOT).replace(os.sep, "/"),
                "note": "Saved to inbox. The PI agent must process it through normal review.",
            },
        )


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    server.daemon_threads = True
    print(f"Any Science UI: http://127.0.0.1:{PORT}", flush=True)
    server.serve_forever()
