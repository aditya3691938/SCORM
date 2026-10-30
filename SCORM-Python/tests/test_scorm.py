import os
import sys
import shutil
import tempfile
import unittest

# Ensure parent directory is in sys.path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from scorm_service import (
    process_scorm_package,
    parse_manifest,
    resolve_scorm_asset
)

class TestScormImplementation(unittest.TestCase):

    def setUp(self):
        self.test_dir = tempfile.mkdtemp()
        self.packages_dir = os.path.join(os.path.dirname(__file__), '..', 'packages')
        self.sample_zip = os.path.join(self.packages_dir, 'scorm_test_package.zip')

    def tearDown(self):
        shutil.rmtree(self.test_dir, ignore_errors=True)

    def test_sample_zip_exists(self):
        self.assertTrue(os.path.exists(self.sample_zip), "Sample SCORM test zip must exist.")

    def test_process_scorm_package(self):
        launch_href, err = process_scorm_package(self.sample_zip, "unit_test_pkg", self.test_dir)
        self.assertIsNone(err, f"Processing should succeed with no error, got: {err}")
        self.assertEqual(launch_href, "index.html")

        # Verify extracted files
        pkg_folder = os.path.join(self.test_dir, "scorm", "unit_test_pkg")
        self.assertTrue(os.path.exists(os.path.join(pkg_folder, "imsmanifest.xml")))
        self.assertTrue(os.path.exists(os.path.join(pkg_folder, "index.html")))
        self.assertTrue(os.path.exists(os.path.join(pkg_folder, "res_manifest.json")))

    def test_process_scorm_package_target_folder(self):
        custom_target = os.path.join(self.test_dir, "custom_target_folder")
        launch_href, err = process_scorm_package(self.sample_zip, "direct_pkg", self.test_dir, target_folder=custom_target)
        self.assertIsNone(err)
        self.assertEqual(launch_href, "index.html")
        self.assertTrue(os.path.exists(os.path.join(custom_target, "imsmanifest.xml")))
        self.assertTrue(os.path.exists(os.path.join(custom_target, "index.html")))
        self.assertTrue(os.path.exists(os.path.join(custom_target, "res_manifest.json")))

    def test_parse_manifest(self):
        # Extract first
        process_scorm_package(self.sample_zip, "manifest_test_pkg", self.test_dir)
        manifest_path = os.path.join(self.test_dir, "scorm", "manifest_test_pkg", "imsmanifest.xml")

        info = parse_manifest(manifest_path)
        self.assertEqual(info["schema_version"], "1.2")
        self.assertIn("Safety", info["title"])
        self.assertEqual(info["launch_href"], "index.html")
        self.assertGreaterEqual(len(info["resources"]), 1)

    def test_resolve_scorm_asset(self):
        process_scorm_package(self.sample_zip, "asset_test_pkg", self.test_dir)
        pkg_folder = os.path.join(self.test_dir, "scorm", "asset_test_pkg")

        # 1. Exact match
        resolved = resolve_scorm_asset(pkg_folder, "index.html")
        self.assertEqual(resolved, "index.html")

        # 2. Match with query strings and fragment
        resolved_query = resolve_scorm_asset(pkg_folder, "index.html?timestamp=1690000#slide3")
        self.assertEqual(resolved_query, "index.html")

        # 3. Case-insensitive match
        resolved_case = resolve_scorm_asset(pkg_folder, "INDEX.HTML")
        self.assertIsNotNone(resolved_case)

    def test_rise_360_detection(self):
        # Create a mock Rise 360 package structure
        mock_rise = os.path.join(self.test_dir, "mock_rise")
        os.makedirs(os.path.join(mock_rise, "scormcontent"), exist_ok=True)
        os.makedirs(os.path.join(mock_rise, "scormdriver"), exist_ok=True)

        with open(os.path.join(mock_rise, "scormcontent", "index.html"), "w") as f:
            f.write("<html><body>Rise Content</body></html>")
        with open(os.path.join(mock_rise, "scormdriver", "indexAPI.html"), "w") as f:
            f.write("<html><body>Driver Wrapper</body></html>")

        manifest_content = """<?xml version="1.0"?>
        <manifest identifier="RiseCourse" version="1">
            <resources>
                <resource identifier="r1" type="webcontent" href="scormdriver/indexAPI.html" />
            </resources>
        </manifest>"""
        with open(os.path.join(mock_rise, "imsmanifest.xml"), "w") as f:
            f.write(manifest_content)

        # Make zip
        mock_zip = os.path.join(self.test_dir, "mock_rise.zip")
        shutil.make_archive(os.path.join(self.test_dir, "mock_rise"), 'zip', mock_rise)

        launch_href, err = process_scorm_package(mock_zip, "rise_test", self.test_dir)
        self.assertIsNone(err)
        # Should prefer scormcontent/index.html over driver
        self.assertEqual(launch_href, "scormcontent/index.html")


if __name__ == '__main__':
    unittest.main()

