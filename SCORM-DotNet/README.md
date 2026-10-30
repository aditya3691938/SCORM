# SCORM Implementation & Testing Studio (.NET 8 / C#)

Welcome to the standalone **SCORM 1.2 & SCORM 2004 Development and Testing Studio**.

This project is built **entirely in C# and ASP.NET Core 8 MVC** with **zero Python files or dependencies**. It serves as an isolated testbed and reference implementation for processing, inspecting, launching, and debugging SCORM 1.2 and SCORM 2004 learning modules before or alongside integrating them into **AdityaLMS**.

---

## 📁 Folder Structure

```
SCORM-DotNet/
├── SCORM_Implementation.csproj   # ASP.NET Core 8 Web project configuration
├── Program.cs                     # Application startup, routing, and DI registration
├── appsettings.json               # Environment configuration & listening URLs
├── run.sh                         # Quickstart script (detects .NET SDK & runs server)
├── README.md                      # Complete architecture & usage documentation
│
├── Controllers/
│   ├── HomeController.cs          # Studio dashboard, package upload & manifest inspector
│   └── ScormPlayerController.cs   # Dual-pane test player, streaming asset engine & telemetry API
│
├── Services/
│   ├── IScormService.cs           # SCORM extraction, manifest parsing & asset resolver interface
│   └── ScormService.cs            # High-performance C# ZipFile & XDocument implementation
│
├── Models/
│   └── ScormModels.cs             # Strongly-typed records and viewmodels
│
├── Views/
│   ├── Home/
│   │   └── Index.cshtml           # Modern studio dashboard, upload dropzone & package catalog
│   └── ScormPlayer/
│       └── Player.cshtml          # Dual-pane player with live CMI inspector & LMS simulator
│
├── wwwroot/                       # Static assets served by ASP.NET Core
│   ├── css/
│   │   └── player.css             # Responsive layout styling for player & inspector
│   └── js/
│       ├── scorm_api_12.js        # SCORM 1.2 Runtime API adapter (window.API)
│       ├── scorm_api_2004.js      # SCORM 2004 Runtime API adapter (window.API_1484_11)
│       └── debugger.js            # Live CMI data visualizer, event recorder & simulator
│
└── packages/                      # Storage directory for uploaded & unpacked SCORM modules
    ├── scorm_test_package.zip     # Pre-bundled Workplace Safety Training package
    └── sample_safety/             # Pre-extracted sample module ready for immediate testing
```

---

## 🚀 Running the Studio

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed on your machine.
  - **macOS** (Homebrew): `brew install --cask dotnet-sdk`
  - **Ubuntu / Debian**: `sudo apt install dotnet-sdk-8.0`
  - **Windows**: Use the official .NET 8 installer.

### Option 1: Using the Quickstart Script
```bash
cd "SCORM-DotNet"
./run.sh
```
The script detects the .NET 8 SDK and launches the studio at **`http://127.0.0.1:5050`**.

### Option 2: Using the `dotnet` CLI directly
```bash
cd "SCORM-DotNet"
dotnet run --urls "http://127.0.0.1:5050"
```

### Option 3: Running via Docker (No local .NET installation needed)
```bash
docker run --rm -it \
  -p 5050:5050 \
  -v "$(pwd)":/app \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet run --urls "http://0.0.0.0:5050"
```

---

## 🧪 Testing SCORM Packages Independently

### 1. Launching the Pre-Loaded Test Package
A complete interactive module (**Workplace Safety Training**) is pre-bundled:
1. Open **`http://127.0.0.1:5050`** in any modern web browser.
2. Click **"Launch Player"** on the `sample_safety` card.
3. Progress through the courseware slides, answer quiz questions, and watch real-time CMI telemetry stream in the right-hand inspection pane.

### 2. Uploading Custom SCORM Packages
- Drag and drop or browse any `.zip` SCORM package (from Articulate Rise 360, Storyline 360, Adobe Captivate, or iSpring).
- The ASP.NET Core service:
  1. Extracts the archive safely into `packages/{packageId}/`.
  2. Parses `imsmanifest.xml` via LINQ to XML (`XDocument`), automatically handling namespace stripping.
  3. Detects the launch entry file (with automatic detection and direct routing for **Articulate Rise 360** `scormcontent/index.html`).
  4. Generates and caches `res_manifest.json`.
  5. Redirects to the test player.

### 3. Dual-Pane Inspection & LMS Simulator
- **Live Event Log**: Captures every API call (`LMSInitialize`, `LMSGetValue`, `LMSSetValue`, `LMSCommit`, `LMSFinish`, `GetLastError`) with exact timestamps and return values.
- **CMI Data Model Table**: Displays live values for `cmi.core.lesson_status`, `cmi.core.score.raw`, `cmi.core.lesson_location`, `cmi.suspend_data`, and session time.
- **Simulation Actions**:
  - `Simulate Passed (100%)`
  - `Simulate Completed (85%)`
  - `Simulate Failed (45%)`
  - `Reset Attempt / CMI`
  - `Export CMI State to JSON`

---

## 🧩 Key Architecture Details (.NET 8)

### 1. Zero External NuGet Dependencies
The core implementation relies exclusively on the standard .NET 8 BCL:
- `System.IO.Compression.ZipFile`: Fast, streaming ZIP package decompression.
- `System.Xml.Linq.XDocument`: XML manifest parsing with local-name LINQ queries that handle all ADL namespace variations (`http://www.imsproject.org/xsd/imscp_rootv1p1p2`, `http://www.adlnet.org/xsd/adlcp_rootv1p2`, etc.).
- `Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider`: Accurate MIME type resolution for SCORM assets (HTML, JS, CSS, WASM, SVG, MP4, MP3, WOFF2).

### 2. Articulate Rise 360 Direct Route Handling
Articulate Rise courses frequently register `scormdriver/indexAPI.html` as the manifest launch file, which opens blank pages or popup windows in embedded LMS environments. `ScormService.cs` automatically detects the actual courseware payload at `scormcontent/index.html` and launches it directly for clean responsive embedded playback.

### 3. Recursive Asset Resolution
SCORM packages frequently contain nested relative references that can cause HTTP 404s on complex multimedia courseware. The asset resolver (`ScormService.ResolveScormAsset`):
- Strips query string cache-busters and URL fragment identifiers.
- Performs case-insensitive matching for platforms with differing filename case sensitivity.
- Recursively locates deeply nested images, scripts, stylesheets, fonts, and media files.

### 4. Seamless AdityaLMS Alignment
All interfaces, models, and service logic in `SCORM-DotNet` match the `AdityaLMS` backend architecture (`AdityaLMS.Core` and `AdityaLMS.Infrastructure`). You can test changes here and port them directly into `AdityaLMS/backend/`.
