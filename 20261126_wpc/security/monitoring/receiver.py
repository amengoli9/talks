"""Local-only Dependency-Track webhook sink. No outbound messages or dependencies."""
import json
import os
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from email.utils import formatdate
from pathlib import Path
from zipfile import ZipFile

STORE = Path(os.environ.get("NOTIFICATION_STORE", "/data/notifications.jsonl"))
STORE.parent.mkdir(parents=True, exist_ok=True)

class Handler(BaseHTTPRequestHandler):
    def do_HEAD(self):
        self.do_GET(head_only=True)
    def do_GET(self, head_only=False):
        if self.path.startswith("/osv/"):
            base = Path("/osv").resolve()
            target = (base / self.path.removeprefix("/osv/")).resolve()
            if not target.is_relative_to(base):
                self.send_error(404)
                return
            if not target.is_file():
                # OSV requests individual JSON files on incremental updates.
                archive = target.parent / "all.zip"
                if target.suffix != ".json" or not archive.is_file():
                    self.send_error(404)
                    return
                try:
                    with ZipFile(archive) as snapshot:
                        advisory = snapshot.read(target.name)
                except KeyError:
                    self.send_error(404)
                    return
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(advisory)))
                self.end_headers()
                if not head_only:
                    self.wfile.write(advisory)
                return
            self.send_response(200)
            self.send_header("Content-Length", str(target.stat().st_size))
            self.send_header("Last-Modified", formatdate(target.stat().st_mtime, usegmt=True))
            self.end_headers()
            if not head_only: self.wfile.write(target.read_bytes())
            return
        if self.path == "/health":
            self.send_response(200); self.end_headers(); self.wfile.write(b"ok")
        elif self.path == "/notifications":
            self.send_response(200); self.send_header("Content-Type", "application/x-ndjson"); self.end_headers()
            if STORE.exists(): self.wfile.write(STORE.read_bytes())
        else:
            self.send_error(404)
    def do_POST(self):
        if self.path != "/notifications": self.send_error(404); return
        length = int(self.headers.get("Content-Length", 0))
        if length > 2_000_000: self.send_error(413); return
        payload = self.rfile.read(length).decode("utf-8")
        try: body = json.loads(payload)
        except json.JSONDecodeError: body = {"raw": payload}
        event = {"receivedAt": datetime.now(timezone.utc).isoformat(), "body": body}
        with STORE.open("a", encoding="utf-8") as stream: stream.write(json.dumps(event, ensure_ascii=False) + "\n")
        self.send_response(204); self.end_headers()
    def log_message(self, format, *args):
        print("local-webhook:", format % args, flush=True)

ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()

