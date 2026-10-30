# SCORM Implementation & Independent Testing Studio

Welcome to the standalone **SCORM 1.2 & SCORM 2004 Development and Testing Studio**.

This workspace has been extracted from the LMS so that you can develop, inspect, test, and debug SCORM packages and runtime API integrations **independently** from the full LMS platform.

---

## 📁 Folder Structure

```
SCORM-Python/
├── README.md                 # Complete guide & documentation (this file)
├── run.sh                    # One-command quickstart script (auto-detects python environment)
├── app.py                    # Standalone Flask Studio server (Upload, Manifest Inspector, Live Debugger)
├── server.py                 # Zero-dependency HTTP test runner (runs with standard python3)
├── scorm_service.py          # Core package processor, XML manifest parser & asset resolver
├── requirements.txt          # Python dependencies (Flask)
│
├── packages/                 # Storage for SCORM zip packages & unpacked modules
│   ├── scorm_test_package.zip # Pre-bundled Workplace Safety Training SCORM 1.2/2004 module
│   └── sample_safety/        # Pre-extracted ready-to-test courseware
│
├── static/
│   ├── css/player.css        # Styling for the player, iframe viewport, and debugger
│   └── js/
│       ├── scorm_api_12.js   # SCORM 1.2 Runtime API implementation (window.API)
│       ├── scorm_api_2004.js # SCORM 2004 Runtime API implementation (window.API_1484_11)
│       └── debugger.js       # Live CMI visualizer, telemetry event logger & LMS simulator
│
├── templates/
│   ├── index.html            # Studio dashboard & package manager
│   └── player.html           # SCORM test player harness with live side-by-side debug panel
│
├── tests/
│   └── test_scorm.py         # Automated unit tests (Manifest parser, extraction, Rise 360 detection)
│
└── dotnet_reference/         # C# / ASP.NET Core port ready for AdityaLMS
    ├── IScormService.cs
    ├── ScormService.cs
    ├── ScormController.cs
    └── ScormDtos.cs
```

---

## 🚀 Quick Start (Running the Studio)

### Option 1: Quickstart Script (Recommended)
From the project directory, run:
```bash
./run.sh
```
The script automatically detects the virtual environment (`../LearningHubV3/venv`) or system python, launches the studio, and opens:
👉 **`http://127.0.0.1:5050`**

### Option 2: Running with Python / Flask
```bash
# Using the LearningHub virtualenv:
../LearningHubV3/venv/bin/python3 app.py 5050

# Or using system python:
python3 app.py 5050
```

### Option 3: Zero-Dependency Runner (No Pip / No Flask Required)
If you don't have Flask installed or want a pure standard-library server:
```bash
python3 server.py 5050
```

---

## 🧪 Testing SCORM Packages Independently

### 1. Launching the Pre-Loaded Test Package
A full SCORM 1.2 / 2004 interactive module (**Workplace Safety Training**) is pre-configured:
1. Open `http://127.0.0.1:5050`
2. Click **"Launch Test Package"** or select **"sample_safety"**
3. Navigate slides, answer the interactive safety quiz, and watch the live telemetry stream on the right pane!

### 2. Uploading Custom SCORM Packages
- Drag and drop any `.zip` SCORM package created in **Articulate Rise 360**, **Articulate Storyline**, **Adobe Captivate**, or **iSpring**.
- The server will:
  1. Extract the package safely into `packages/<package_id>/`.
  2. Parse `imsmanifest.xml` and strip namespace variations automatically.
  3. Detect the launch entry file (with special handling for Rise 360 to bypass blank launcher wrappers).
  4. Cache the metadata into `res_manifest.json`.
  5. Redirect straight to the interactive test harness.

### 3. Live CMI Inspector & Telemetry Console
While interacting with the courseware inside the iframe:
- **Real-Time Event Stream**: Every single API method invocation (`LMSInitialize`, `LMSGetValue`, `LMSSetValue`, `LMSCommit`, `LMSFinish`, `GetLastError`) is logged with timestamps, parameter names, passed values, and return values.
- **CMI Data Model Table**: Displays live values for `cmi.core.lesson_status`, `cmi.core.score.raw`, `cmi.core.lesson_location`, `cmi.suspend_data`, and session time.
- **LMS Simulator**:
  - Test course completion triggers (`Simulate Passed (100%)`, `Simulate Completed (85%)`, `Simulate Failed (45%)`).
  - Test fresh attempt resets (`Reset Attempt / CMI`).
  - Export current CMI state to JSON (`scorm_cmi_state.json`).

---

## 🧩 Key Architecture & Implementation Details

### 1. Universal SCORM API Injection
SCORM content packages search for runtime adapters using the standard ADL algorithm `findAPI(window)`. To ensure maximum compatibility across varying iframe sandboxes and nested frames:
- SCORM 1.2 adapter (`window.API`) and SCORM 2004 adapter (`window.API_1484_11`) are attached directly to:
  - `window`
  - `window.parent`
  - `window.top`
- Spec-compliant error codes (`0`, `101`, `201`, `301`, `401`, `403`, etc.) are returned on improper calls.

### 2. Articulate Rise 360 Optimization
Articulate Rise SCORM packages frequently declare `scormdriver/indexAPI.html` as the manifest launch target, which displays a blank page or requires popup windows. 
`scorm_service.py` detects this pattern and automatically routes directly to `scormcontent/index.html` for clean, responsive embedded playback.

### 3. Smart Asset Fallback Resolution
SCORM packages frequently contain nested relative references that can cause HTTP 404s on complex multimedia courseware. The asset resolver (`resolve_scorm_asset`):
- Strips query string cache-busters and URL fragment identifiers.
- Performs case-insensitive matching for platforms with differing filename case sensitivity.
- Recursively locates deeply nested images, scripts, stylesheets, fonts, and WASM files.

---

## 🔬 Running Automated Unit Tests

Run the built-in unit tests to verify package extraction, manifest parsing, and asset resolution:
```bash
python3 -m unittest discover tests
```
Output:
```
.....
----------------------------------------------------------------------
Ran 5 tests in 0.010s

OK
```

---

## ⚡ Porting to .NET / C# (AdityaLMS)

If you are implementing this in **AdityaLMS** (`AdityaLMS.Api` / `AdityaLMS.Infrastructure`), the complete C# implementation is available in `dotnet_reference/`:

| File | Purpose |
|------|---------|
| `dotnet_reference/IScormService.cs` | Interface definition for SCORM extraction and asset resolution |
| `dotnet_reference/ScormService.cs` | C# implementation using `System.IO.Compression.ZipFile` & `XDocument` |
| `dotnet_reference/ScormController.cs` | ASP.NET Core API controller (`/api/scorm/upload`, `/api/scorm/content/*`, `/api/scorm/telemetry`) |
| `dotnet_reference/ScormDtos.cs` | DTO classes for telemetry, scores, and manifest metadata |

To integrate into AdityaLMS:
1. Copy `dotnet_reference/ScormService.cs` into `AdityaLMS.Infrastructure/Services/`.
2. Register the service in `AdityaLMS.Api/Program.cs`:
   ```csharp
   builder.Services.AddScoped<IScormService, ScormService>();
   ```
3. Copy `dotnet_reference/ScormController.cs` into `AdityaLMS.Api/Controllers/`.

