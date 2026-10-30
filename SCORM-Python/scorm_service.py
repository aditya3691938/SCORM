"""
SCORM Service - Core Package Processor, Manifest Parser & Asset Resolver
Standalone, modular implementation for SCORM 1.2 and SCORM 2004 packages.
"""

import os
import shutil
import zipfile
import json
import xml.etree.ElementTree as ET
from typing import Tuple, Optional, Dict, Any, List


def strip_xml_namespace(tag: str) -> str:
    """Helper to remove XML namespace from a tag."""
    if '}' in tag:
        return tag.split('}', 1)[1]
    return tag


def parse_manifest(manifest_path: str) -> Dict[str, Any]:
    """
    Parses imsmanifest.xml and extracts comprehensive SCORM metadata:
    - schema & schema version (e.g. SCORM 1.2, CAM 1.3 / 2004)
    - package identifier & version
    - organization title & items / SCOs
    - resources with identifier, type, href, and file lists
    """
    info: Dict[str, Any] = {
        "identifier": "",
        "version": "",
        "schema": "SCORM",
        "schema_version": "1.2",
        "title": "Untitled SCORM Course",
        "organizations": [],
        "resources": [],
        "launch_href": None,
        "is_rise_360": False
    }

    if not os.path.exists(manifest_path):
        return info

    try:
        tree = ET.parse(manifest_path)
        root = tree.getroot()

        # Clean tags of namespaces
        for elem in root.iter():
            elem.tag = strip_xml_namespace(elem.tag)

        info["identifier"] = root.attrib.get("identifier", "")
        info["version"] = root.attrib.get("version", "1.0")

        # Extract schema and version from <metadata>
        schema_elem = root.find(".//metadata/schema")
        if schema_elem is not None and schema_elem.text:
            info["schema"] = schema_elem.text.strip()

        schemaver_elem = root.find(".//metadata/schemaversion")
        if schemaver_elem is not None and schemaver_elem.text:
            info["schema_version"] = schemaver_elem.text.strip()

        # Extract organization & titles
        for org in root.findall(".//organizations/organization"):
            org_title_elem = org.find("./title")
            org_title = org_title_elem.text.strip() if org_title_elem is not None and org_title_elem.text else "Default"
            if info["title"] == "Untitled SCORM Course" and org_title:
                info["title"] = org_title

            items: List[Dict[str, str]] = []
            for item in org.findall(".//item"):
                item_title = item.find("./title")
                items.append({
                    "identifier": item.attrib.get("identifier", ""),
                    "identifierref": item.attrib.get("identifierref", ""),
                    "title": item_title.text.strip() if item_title is not None and item_title.text else "Item"
                })

            info["organizations"].append({
                "identifier": org.attrib.get("identifier", ""),
                "title": org_title,
                "items": items
            })

        # Extract resources
        for res in root.findall(".//resources/resource"):
            res_href = res.attrib.get("href")
            res_type = res.attrib.get("type", "webcontent")
            res_id = res.attrib.get("identifier", "")
            scorm_type = res.attrib.get("scormtype", "sco")

            files = [f.attrib.get("href") for f in res.findall("./file") if f.attrib.get("href")]

            res_data = {
                "identifier": res_id,
                "type": res_type,
                "scorm_type": scorm_type,
                "href": res_href,
                "files": files
            }
            info["resources"].append(res_data)

            # Assign first valid resource href as launch candidate if not yet set
            if not info["launch_href"] and res_href:
                info["launch_href"] = res_href.replace('\\', '/')

    except Exception as e:
        print(f"[SCORM] Warning parsing imsmanifest.xml: {e}")

    return info


def process_scorm_package(
    zip_file_or_path: Any,
    scorm_id_str: str,
    upload_base_folder: str,
    target_folder: Optional[str] = None
) -> Tuple[Optional[str], Optional[str]]:
    """
    Extracts a SCORM zip file into upload_base_folder/scorm/<scorm_id_str>/
    (or target_folder if explicitly specified) and parses imsmanifest.xml
    to locate the launch file (href).
    
    Supports:
    - Path strings, file-like objects (Flask FileStorage, BytesIO)
    - Articulate Rise 360 detection (routes directly to scormcontent/index.html)
    - Fallback heuristic detection (story.html, index_lms.html, launch.html)
    - Generates metadata manifest json cache (res_manifest.json)
    
    Returns:
        (launch_href, error_message)
    """
    if target_folder:
        scorm_folder = target_folder
    else:
        scorm_folder = os.path.join(upload_base_folder, 'scorm', str(scorm_id_str))
    os.makedirs(scorm_folder, exist_ok=True)

    zip_path = os.path.join(scorm_folder, 'package.zip')

    # Handle various input types
    if isinstance(zip_file_or_path, str):
        if os.path.abspath(zip_file_or_path) != os.path.abspath(zip_path) and os.path.exists(zip_file_or_path):
            shutil.copy2(zip_file_or_path, zip_path)
    elif hasattr(zip_file_or_path, 'save'):
        if hasattr(zip_file_or_path, 'seek'):
            zip_file_or_path.seek(0)
        zip_file_or_path.save(zip_path)
        if hasattr(zip_file_or_path, 'seek'):
            zip_file_or_path.seek(0)
    elif hasattr(zip_file_or_path, 'read'):
        if hasattr(zip_file_or_path, 'seek'):
            zip_file_or_path.seek(0)
        with open(zip_path, 'wb') as f:
            f.write(zip_file_or_path.read())
        if hasattr(zip_file_or_path, 'seek'):
            zip_file_or_path.seek(0)

    if not os.path.exists(zip_path):
        return None, "SCORM package file missing or failed to save."

    # Extract archive
    try:
        with zipfile.ZipFile(zip_path, 'r') as zip_ref:
            zip_ref.extractall(scorm_folder)
    except Exception as e:
        return None, f"Failed to extract SCORM zip package: {e}"

    # Search for imsmanifest.xml (case-insensitive & nested search)
    manifest_path = os.path.join(scorm_folder, 'imsmanifest.xml')
    if not os.path.exists(manifest_path):
        for root_dir, _, files in os.walk(scorm_folder):
            for f in files:
                if f.lower() == 'imsmanifest.xml':
                    manifest_path = os.path.join(root_dir, f)
                    break
            if os.path.exists(manifest_path):
                break

    launch_href = None
    manifest_info = {}

    if os.path.exists(manifest_path):
        manifest_info = parse_manifest(manifest_path)
        raw_href = manifest_info.get("launch_href")

        if raw_href:
            # Handle manifest located in a subfolder
            rel_base = os.path.relpath(os.path.dirname(manifest_path), scorm_folder)
            if rel_base and rel_base != '.':
                raw_href = os.path.join(rel_base, raw_href)

            # Special check for Articulate Rise 360 packages:
            # Rise often points to scormdriver/indexAPI.html or blank.html,
            # but modern browsers render scormcontent/index.html far more reliably.
            rise_direct = os.path.join(scorm_folder, 'scormcontent', 'index.html')
            if 'scormdriver' in raw_href.lower() and os.path.exists(rise_direct):
                launch_href = 'scormcontent/index.html'
                manifest_info["is_rise_360"] = True
            else:
                launch_href = raw_href.replace('\\', '/')

    # Secondary check: Direct player check for Articulate Rise 360
    if not launch_href:
        rise_direct = os.path.join(scorm_folder, 'scormcontent', 'index.html')
        if os.path.exists(rise_direct):
            launch_href = 'scormcontent/index.html'
            manifest_info["is_rise_360"] = True

    # Fallback to standard launch candidate filenames
    if not launch_href:
        priority_candidates = ['index.html', 'story.html', 'index_lms.html', 'indexapi.html', 'launch.html', 'default.html']
        for root_dir, _, files in os.walk(scorm_folder):
            lower_files = {f.lower(): f for f in files}
            for candidate in priority_candidates:
                if candidate in lower_files:
                    full_p = os.path.join(root_dir, lower_files[candidate])
                    rel_path = os.path.relpath(full_p, scorm_folder)
                    launch_href = rel_path.replace('\\', '/')
                    break
            if launch_href:
                break

    if not launch_href:
        return None, "Could not identify launch HTML file in SCORM manifest or package directory."

    # Cache metadata for fast lookup without re-parsing XML
    manifest_info["launch_href"] = launch_href
    manifest_info["scorm_id"] = str(scorm_id_str)
    try:
        with open(os.path.join(scorm_folder, 'res_manifest.json'), 'w', encoding='utf-8') as f:
            json.dump(manifest_info, f, indent=2)
    except Exception as e:
        print(f"[SCORM] Notice: could not write res_manifest.json cache: {e}")

    return launch_href, None


def resolve_scorm_asset(scorm_dir: str, requested_filename: str) -> Optional[str]:
    """
    Robust asset resolver for SCORM packages.
    Resolves filenames, handles query string/hash stripping, case-insensitive matches,
    and performs recursive fallback lookups for nested assets (images, css, js, wasm).
    
    Returns:
        Relative path of the resolved file inside scorm_dir, or None if not found.
    """
    if not os.path.exists(scorm_dir):
        return None

    # Strip query strings and fragment identifiers
    clean_filename = requested_filename.split('?')[0].split('#')[0].lstrip('/')
    target_path = os.path.join(scorm_dir, clean_filename)

    # 1. Exact match
    if os.path.exists(target_path) and os.path.isfile(target_path):
        return os.path.relpath(target_path, scorm_dir).replace('\\', '/')

    # 2. Case-insensitive exact relative path match
    clean_norm = os.path.normpath(clean_filename).replace('\\', '/')
    for root, _, files in os.walk(scorm_dir):
        for f in files:
            full_p = os.path.join(root, f)
            rel_p = os.path.relpath(full_p, scorm_dir).replace('\\', '/')
            if rel_p.lower() == clean_norm.lower() or rel_p.lower().endswith('/' + clean_norm.lower()):
                return rel_p

    # 3. Filename-only recursive lookup for deeply nested packages
    base_name = os.path.basename(clean_filename).lower()
    if base_name:
        for root, _, files in os.walk(scorm_dir):
            for f in files:
                if f.lower() == base_name:
                    return os.path.relpath(os.path.join(root, f), scorm_dir).replace('\\', '/')

    # 4. If request is a directory or empty, check for known launch files
    if clean_filename == '' or clean_filename.endswith('/'):
        candidates = ['scormcontent/index.html', 'index.html', 'story.html', 'index_lms.html', 'indexapi.html', 'launch.html']
        for cand in candidates:
            cand_p = os.path.join(scorm_dir, cand)
            if os.path.exists(cand_p):
                return cand

    return None

