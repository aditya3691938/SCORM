#!/usr/bin/env python3
"""
SCORM Development & Testing Studio - Standalone Server
Run with: python3 app.py
"""

import os
import sys
import json
import shutil
import mimetypes
from flask import Flask, render_template, request, redirect, url_for, send_from_directory, jsonify, flash, abort

# Import the core standalone SCORM service
from scorm_service import (
    process_scorm_package,
    parse_manifest,
    resolve_scorm_asset
)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
PACKAGES_DIR = os.path.join(BASE_DIR, 'packages')
os.makedirs(PACKAGES_DIR, exist_ok=True)

# Ensure extra MIME types are registered
mimetypes.add_type('application/wasm', '.wasm')
mimetypes.add_type('application/json', '.json')
mimetypes.add_type('font/woff2', '.woff2')
mimetypes.add_type('font/woff', '.woff')

app = Flask(
    __name__,
    template_folder=os.path.join(BASE_DIR, 'templates'),
    static_folder=os.path.join(BASE_DIR, 'static')
)
app.secret_key = 'scorm_standalone_secret_key_development_only'


def get_available_packages():
    """Scan PACKAGES_DIR and list all unzipped / available SCORM packages."""
    packages = []
    if not os.path.exists(PACKAGES_DIR):
        return packages

    for item in sorted(os.listdir(PACKAGES_DIR)):
        item_path = os.path.join(PACKAGES_DIR, item)
        if os.path.isdir(item_path):
            manifest_json_path = os.path.join(item_path, 'res_manifest.json')
            manifest_xml_path = os.path.join(item_path, 'imsmanifest.xml')
            
            manifest_data = {}
            if os.path.exists(manifest_json_path):
                try:
                    with open(manifest_json_path, 'r', encoding='utf-8') as f:
                        manifest_data = json.load(f)
                except Exception:
                    pass

            if not manifest_data and os.path.exists(manifest_xml_path):
                manifest_data = parse_manifest(manifest_xml_path)

            launch_href = manifest_data.get('launch_href')
            if not launch_href:
                for cand in ['scormcontent/index.html', 'index.html', 'story.html', 'index_lms.html', 'launch.html']:
                    if os.path.exists(os.path.join(item_path, cand)):
                        launch_href = cand
                        break

            packages.append({
                'id': item,
                'title': manifest_data.get('title') or item.replace('_', ' ').title(),
                'schema': manifest_data.get('schema', 'SCORM'),
                'schema_version': manifest_data.get('schema_version', '1.2'),
                'launch_href': launch_href or 'index.html',
                'is_rise_360': manifest_data.get('is_rise_360', False)
            })
    return packages


@app.route('/')
def index():
    """Studio Dashboard listing deployed packages and upload interface."""
    packages = get_available_packages()
    return render_template('index.html', packages=packages)


@app.route('/upload', methods=['POST'])
def upload_package():
    """Upload a new SCORM .zip file and process it."""
    file = request.files.get('scorm_file')
    pkg_id = request.form.get('package_id', '').strip()

    if not file or not file.filename:
        flash("No file selected", "danger")
        return redirect(url_for('index'))

    if not file.filename.lower().endswith('.zip'):
        flash("Please upload a valid SCORM (.zip) package.", "danger")
        return redirect(url_for('index'))

    if not pkg_id:
        clean_name = os.path.splitext(file.filename)[0]
        pkg_id = "".join([c if c.isalnum() or c in ['_', '-'] else '_' for c in clean_name])

    # Extract directly into target package directory
    target_dir = os.path.join(PACKAGES_DIR, pkg_id)
    os.makedirs(target_dir, exist_ok=True)

    launch_href, err_msg = process_scorm_package(file, pkg_id, BASE_DIR, target_folder=target_dir)

    if err_msg:
        flash(f"SCORM extraction error: {err_msg}", "danger")
        return redirect(url_for('index'))

    flash(f"Package '{pkg_id}' extracted successfully. Launch href: {launch_href}", "success")
    return redirect(url_for('player', package_id=pkg_id))


@app.route('/player/<package_id>')
def player(package_id):
    """SCORM Runtime Player and live CMI debugger harness."""
    package_dir = os.path.join(PACKAGES_DIR, package_id)
    if not os.path.exists(package_dir):
        abort(404, f"Package '{package_id}' not found.")

    manifest_json_path = os.path.join(package_dir, 'res_manifest.json')
    manifest_data = {}
    if os.path.exists(manifest_json_path):
        try:
            with open(manifest_json_path, 'r', encoding='utf-8') as f:
                manifest_data = json.load(f)
        except Exception:
            pass

    if not manifest_data:
        manifest_xml = os.path.join(package_dir, 'imsmanifest.xml')
        if os.path.exists(manifest_xml):
            manifest_data = parse_manifest(manifest_xml)

    launch_href = manifest_data.get('launch_href') or 'index.html'

    # Check if resolved file exists
    resolved = resolve_scorm_asset(package_dir, launch_href)
    if resolved:
        launch_href = resolved

    launch_url = f"/content/{package_id}/{launch_href}"

    # Load initial state if previously persisted
    state_file = os.path.join(package_dir, 'cmi_state.json')
    initial_state = {}
    if os.path.exists(state_file):
        try:
            with open(state_file, 'r', encoding='utf-8') as f:
                initial_state = json.load(f).get('cmi', {})
        except Exception:
            pass

    return render_template(
        'player.html',
        package_id=package_id,
        manifest=manifest_data,
        launch_url=launch_url,
        initial_state=initial_state
    )


@app.route('/content/<package_id>/', defaults={'filename': ''})
@app.route('/content/<package_id>/<path:filename>')
def serve_content(package_id, filename):
    """
    Serves SCORM package static content (HTML, JS, CSS, Media)
    with the exact nested fallback and resolution algorithm developed in LearningHubV3.
    """
    package_dir = os.path.join(PACKAGES_DIR, package_id)
    if not os.path.exists(package_dir):
        return "<h4 style='color:red;'>Package directory not found.</h4>", 404

    resolved_rel_path = resolve_scorm_asset(package_dir, filename)
    if not resolved_rel_path:
        # Check standard launch files
        for cand in ['scormcontent/index.html', 'index.html', 'story.html', 'index_lms.html']:
            if os.path.exists(os.path.join(package_dir, cand)):
                resolved_rel_path = cand
                break

    if not resolved_rel_path or not os.path.exists(os.path.join(package_dir, resolved_rel_path)):
        return f"File '{filename}' not found in SCORM package.", 404

    target_full_path = os.path.join(package_dir, resolved_rel_path)
    file_dir = os.path.dirname(target_full_path)
    base_file = os.path.basename(target_full_path)

    # Determine mime-type
    mime_type, _ = mimetypes.guess_type(target_full_path)
    return send_from_directory(file_dir, base_file, mimetype=mime_type)


@app.route('/manifest/<package_id>')
def manifest_json(package_id):
    """Returns parsed manifest metadata as JSON for inspection."""
    package_dir = os.path.join(PACKAGES_DIR, package_id)
    if not os.path.exists(package_dir):
        abort(404)

    manifest_json_path = os.path.join(package_dir, 'res_manifest.json')
    if os.path.exists(manifest_json_path):
        with open(manifest_json_path, 'r', encoding='utf-8') as f:
            return jsonify(json.load(f))

    manifest_xml = os.path.join(package_dir, 'imsmanifest.xml')
    if os.path.exists(manifest_xml):
        return jsonify(parse_manifest(manifest_xml))

    return jsonify({"error": "No manifest found for package."}), 404


@app.route('/api/telemetry/<package_id>', methods=['POST'])
def save_telemetry(package_id):
    """Receives and saves SCORM CMI state/progress/score."""
    package_dir = os.path.join(PACKAGES_DIR, package_id)
    if not os.path.exists(package_dir):
        return jsonify({"status": "error", "message": "Package not found"}), 404

    data = request.get_json() or {}
    state_file = os.path.join(package_dir, 'cmi_state.json')

    try:
        with open(state_file, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2)
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

    return jsonify({"status": "success", "package_id": package_id, "recorded": True})


@app.route('/api/state/<package_id>')
def get_state(package_id):
    """Fetches saved CMI state for a package."""
    package_dir = os.path.join(PACKAGES_DIR, package_id)
    state_file = os.path.join(package_dir, 'cmi_state.json')
    if os.path.exists(state_file):
        with open(state_file, 'r', encoding='utf-8') as f:
            return jsonify(json.load(f))
    return jsonify({"cmi": {}})


@app.route('/api/reset/<package_id>', methods=['POST'])
def reset_state(package_id):
    """Resets saved CMI state for fresh attempt testing."""
    package_dir = os.path.join(PACKAGES_DIR, package_id)
    state_file = os.path.join(package_dir, 'cmi_state.json')
    if os.path.exists(state_file):
        try:
            os.remove(state_file)
        except Exception:
            pass
    return jsonify({"status": "success", "message": "State reset."})


if __name__ == '__main__':
    port = 5050
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            pass
    print("=" * 60)
    print(f"🚀 SCORM Implementation & Testing Studio running at:")
    print(f"👉 http://127.0.0.1:{port}")
    print(f"📦 Packages directory: {PACKAGES_DIR}")
    print("=" * 60)
    app.run(host='0.0.0.0', port=port, debug=True)

