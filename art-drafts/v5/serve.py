"""Local review server with byte ranges so MP4 pause, replay and seeking work."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import re
import sys

class ReviewHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Accept-Ranges", "bytes")
        super().end_headers()

    def send_head(self):
        self.remaining = None
        path = Path(self.translate_path(self.path))
        request = self.headers.get("Range")
        if not request or not path.is_file():
            return super().send_head()
        size = path.stat().st_size
        match = re.fullmatch(r"bytes=(\d*)-(\d*)", request)
        if not match or not any(match.groups()):
            self.send_error(416, "Unsupported byte range")
            return None
        left, right = match.groups()
        start = int(left) if left else max(0, size - int(right))
        end = min(size - 1, int(right)) if left and right else size - 1
        if start >= size or end < start:
            self.send_response(416)
            self.send_header("Content-Range", f"bytes */{size}")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return None
        source = path.open("rb")
        source.seek(start)
        self.remaining = end - start + 1
        self.send_response(206)
        self.send_header("Content-Type", self.guess_type(str(path)))
        self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.send_header("Content-Length", str(self.remaining))
        self.end_headers()
        return source

    def copyfile(self, source, destination):
        if self.remaining is None:
            return super().copyfile(source, destination)
        while self.remaining:
            data = source.read(min(65536, self.remaining))
            if not data:
                break
            destination.write(data)
            self.remaining -= len(data)

if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 4321
    root = Path(__file__).resolve().parents[2]
    print(f"Review: http://localhost:{port}/art-drafts/v5/", flush=True)
    ThreadingHTTPServer(("127.0.0.1", port), partial(ReviewHandler, directory=str(root))).serve_forever()
