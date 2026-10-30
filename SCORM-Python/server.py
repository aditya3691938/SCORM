#!/usr/bin/env python3
"""
Zero-Dependency SCORM Test Runner (Pure Python Standard Library)
No pip installs needed! Run simply with:
    python3 server.py [port]
Default port is 5050.
"""

import os
import sys
import json
import mimetypes
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
PACKAGES_DIR = os.path.join(BASE_DIR, 'packages')

from scorm_service import resolve_scorm_asset, parse_manifest

class ScormHTTPRequestHandler(BaseHTTPRequestHandler):

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path

        if path == '/' or path == '/index.html':
            self.serve_index()
        elif path.startswith('/static/'):
            rel_file = path[len('/static/'):]
            full_path = os.path.join(BASE_DIR, 'static', rel_file)
            self.serve_static_file(full_path)
        elif path.startswith('/player/'):
            pkg_id = path[len('/player/'):].strip('/')
            self.serve_player(pkg_id)
        elif path.startswith('/manifest/'):
            pkg_id = path[len('/manifest/'):].strip('/')
            self.serve_manifest_json(pkg_id)
        elif path.startswith('/content/'):
            # /content/<pkg_id>/<rel_path>
            parts = path[len('/content/'):].split('/', 1)
            pkg_id = parts[0]
            rel_path = parts[1] if len(parts) > 1 else ''
            self.serve_package_content(pkg_id, rel_path)
        else:
            self.send_error(404, "Page not found")

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path

        if path.startswith('/api/telemetry/'):
            pkg_id = path[len('/api/telemetry/'):].strip('/')
            content_len = int(self.headers.get('Content-Length', 0))
            post_data = self.rfile.read(content_len)
            pkg_dir = os.path.join(PACKAGES_DIR, pkg_id)
            if os.path.exists(pkg_dir):
                state_file = os.path.join(pkg_dir, 'cmi_state.json')
                try:
                    with open(state_file, 'wb') as f:
                        f.write(post_data)
                except Exception:
                    pass
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(b'{"status":"success","saved":true}')
        elif path.startswith('/api/reset/'):
            pkg_id = path[len('/api/reset/'):].strip('/')
            pkg_dir = os.path.join(PACKAGES_DIR, pkg_id)
            state_file = os.path.join(pkg_dir, 'cmi_state.json')
            if os.path.exists(state_file):
                try:
                    os.remove(state_file)
                except Exception:
                    pass
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(b'{"status":"success","reset":true}')
        else:
            self.send_error(404, "Unknown API endpoint")

    def serve_index(self):
        packages = []
        if os.path.exists(PACKAGES_DIR):
            for d in sorted(os.listdir(PACKAGES_DIR)):
                dp = os.path.join(PACKAGES_DIR, d)
                if os.path.isdir(dp):
                    packages.append(d)

        cards = ""
        for p in packages:
            cards += f"""
            <div class="card p-3 mb-3 shadow-sm border">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <h5 class="fw-bold mb-1">{p}</h5>
                        <p class="small text-muted mb-0">Package directory: packages/{p}</p>
                    </div>
                    <div>
                        <a href="/player/{p}" class="btn btn-primary btn-sm rounded-pill px-3 fw-bold">Test Player</a>
                        <a href="/manifest/{p}" target="_blank" class="btn btn-outline-secondary btn-sm rounded-pill px-2">Manifest JSON</a>
                    </div>
                </div>
            </div>
            """

        html = f"""<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>SCORM Standalone Runner (Zero-Dependency)</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
</head>
<body class="bg-light">
    <div class="container py-5" style="max-width: 800px;">
        <div class="card p-4 shadow-sm border-0 mb-4 bg-dark text-white rounded-4">
            <h2 class="fw-bold text-warning mb-2">SCORM Standalone Runner</h2>
            <p class="text-white-50 mb-0">Zero-dependency HTTP server running with Python standard library.</p>
        </div>
        <h4 class="fw-bold mb-3 text-secondary">Available SCORM Packages</h4>
        {cards or '<div class="alert alert-info">No packages found in packages/ folder.</div>'}
    </div>
</body>
</html>"""
        self.send_response(200)
        self.send_header('Content-Type', 'text/html; charset=utf-8')
        self.end_headers()
        self.wfile.write(html.encode('utf-8'))

    def serve_player(self, pkg_id):
        pkg_dir = os.path.join(PACKAGES_DIR, pkg_id)
        if not os.path.exists(pkg_dir):
            self.send_error(404, "Package not found")
            return

        manifest_xml = os.path.join(pkg_dir, 'imsmanifest.xml')
        info = parse_manifest(manifest_xml) if os.path.exists(manifest_xml) else {}
        launch_href = info.get('launch_href') or 'index.html'

        resolved = resolve_scorm_asset(pkg_dir, launch_href)
        if resolved:
            launch_href = resolved

        launch_url = f"/content/{pkg_id}/{launch_href}"

        # Read templates/player.html and replace placeholders for lightweight zero-dependency serving
        tmpl_path = os.path.join(BASE_DIR, 'templates', 'player.html')
        with open(tmpl_path, 'r', encoding='utf-8') as f:
            tmpl = f.read()

        html = tmpl.replace('{{ package_id }}', pkg_id)\
                   .replace('{{ manifest.title or package_id }}', info.get('title') or pkg_id)\
                   .replace('{{ manifest.launch_href }}', launch_href)\
                   .replace('{{ launch_url }}', launch_url)\
                   .replace('{{ initial_state|tojson|safe }}', '{}')

        self.send_response(200)
        self.send_header('Content-Type', 'text/html; charset=utf-8')
        self.end_headers()
        self.wfile.write(html.encode('utf-8'))

    def serve_manifest_json(self, pkg_id):
        pkg_dir = os.path.join(PACKAGES_DIR, pkg_id)
        manifest_xml = os.path.join(pkg_dir, 'imsmanifest.xml')
        info = parse_manifest(manifest_xml) if os.path.exists(manifest_xml) else {"error": "Manifest not found"}
        data = json.dumps(info, indent=2).encode('utf-8')
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(data)

    def serve_package_content(self, pkg_id, rel_path):
        pkg_dir = os.path.join(PACKAGES_DIR, pkg_id)
        if not os.path.exists(pkg_dir):
            self.send_error(404, "Package not found")
            return

        resolved = resolve_scorm_asset(pkg_dir, rel_path)
        if not resolved:
            self.send_error(404, f"File {rel_path} not found in package")
            return

        full_path = os.path.join(pkg_dir, resolved)
        self.serve_static_file(full_path)

    def serve_static_file(self, full_path):
        if not os.path.exists(full_path) or not os.path.isfile(full_path):
            self.send_error(404, "File not found")
            return

        mime_type, _ = mimetypes.guess_type(full_path)
        if not mime_type:
            mime_type = 'application/octet-stream'

        try:
            with open(full_path, 'rb') as f:
                content = f.read()
            self.send_response(200)
            self.send_header('Content-Type', mime_type)
            self.send_header('Content-Length', str(len(content)))
            self.end_headers()
            self.wfile.write(content)
        except Exception as e:
            self.send_error(500, f"Error reading file: {e}")


def run_server(port=5050):
    server_address = ('', port)
    httpd = HTTPServer(server_address, ScormHTTPRequestHandler)
    print("=" * 60)
    print(f"🚀 Zero-Dependency SCORM Runner listening on http://localhost:{port}")
    print("=" * 60)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping server...")
        httpd.server_close()


if __name__ == '__main__':
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 5050
    run_server(port)

