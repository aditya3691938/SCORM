# SCORM Implementation & Testing Studio

Welcome to the **SCORM 1.2 & SCORM 2004 Development and Testing Studio**.

This workspace provides standalone, self-contained development, inspection, testing, and debugging environments for SCORM e-learning courseware packages (Articulate Rise 360, Storyline 360, Adobe Captivate, iSpring, etc.) independent of the full LMS.

---

## 📁 Repository Structure

```
SCORM/
├── README.md                  # Workspace overview & quickstart guide (this file)
├── .gitignore                 # Git rules for Python, .NET, and SCORM session data
│
├── SCORM-Python/              # Python & Flask Studio Implementation
│   ├── app.py                 # Full Flask Studio server (Upload, Manifest Inspector, Live CMI)
│   ├── server.py              # Zero-dependency HTTP test runner (Pure Python Standard Library)
│   ├── scorm_service.py       # Core package processor, XML parser & asset resolver
│   ├── requirements.txt       # Dependencies (Flask)
│   ├── run.sh                 # One-command quickstart runner
│   ├── README.md              # Detailed Python documentation
│   ├── packages/              # SCORM zip packages & extracted modules
│   │   ├── scorm_test_package.zip # Pre-bundled Workplace Safety module
│   │   └── sample_safety/     # Extracted test package ready for immediate playback
│   ├── static/                # Shared player styles and JavaScript SCORM API adapters
│   │   ├── css/player.css     # Responsive layout for player & debugger
│   │   └── js/
│   │       ├── scorm_api_12.js    # SCORM 1.2 Runtime API adapter (window.API)
│   │       ├── scorm_api_2004.js  # SCORM 2004 Runtime API adapter (window.API_1484_11)
│   │       └── debugger.js        # Live CMI visualizer & event logger
│   ├── templates/             # HTML templates (Dashboard & Dual-Pane Player)
│   │   ├── index.html
│   │   └── player.html
│   ├── tests/                 # Automated unit test suite
│   │   └── test_scorm.py
│   └── dotnet_reference/      # Drop-in C# files tailored for AdityaLMS backend
│       ├── IScormService.cs
│       ├── ScormService.cs
│       ├── ScormController.cs
│       └── ScormDtos.cs
│
└── SCORM-DotNet/              # ASP.NET Core 8 MVC Studio Implementation
    ├── SCORM_Implementation.csproj # .NET 8 Web project configuration
    ├── Program.cs             # Application startup & DI registration
    ├── appsettings.json       # App configuration
    ├── nuget.config           # Package management config
    ├── run.sh                 # One-command quickstart runner (.NET SDK auto-detection)
    ├── README.md              # Detailed .NET documentation
    ├── Controllers/           # MVC Controllers
    │   ├── HomeController.cs        # Studio dashboard & upload dropzone
    │   └── ScormPlayerController.cs # Player engine & telemetry API
    ├── Models/
    │   └── ScormModels.cs     # Strongly-typed records and viewmodels
    ├── Services/
    │   ├── IScormService.cs   # SCORM service interface
    │   └── ScormService.cs    # ZIP extractor & XML manifest parser
    ├── Views/                 # Razor Views
    │   ├── Home/Index.cshtml
    │   └── ScormPlayer/Player.cshtml
    ├── wwwroot/               # Static assets (css/player.css, js/scorm_api_*.js, js/debugger.js)
    └── packages/              # Pre-bundled and uploaded SCORM modules
```

---

## ⚡ Quick Start

### 1. Python Studio (`SCORM-Python/`)
Ideal for immediate testing without compiling or when Python is readily available.

```bash
cd SCORM-Python
./run.sh
```
* **Auto-fallback:** If Flask is installed, it boots the complete Studio server on `http://127.0.0.1:5050`. If Flask is not installed, it automatically launches the zero-dependency standard library runner (`server.py`).
* **Run unit tests:**
  ```bash
  python3 tests/test_scorm.py
  ```

---

### 2. .NET 8 / C# Studio (`SCORM-DotNet/`)
Ideal for testing within a .NET environment or validating logic before integrating into **AdityaLMS**.

```bash
cd SCORM-DotNet
./run.sh
```
* Starts the ASP.NET Core 8 MVC application on `http://127.0.0.1:5050`.
* Directly matches `AdityaLMS` interface patterns and BCL utilities (`System.IO.Compression.ZipFile`, `System.Xml.Linq.XDocument`).

---

## 🎯 Key Features Across Both Implementations

| Capability | Details |
|---|---|
| **SCORM 1.2 & 2004 Support** | Dual runtime API adapters (`window.API` and `window.API_1484_11`) injected at parent and child frame levels. |
| **Articulate Rise 360 Fix** | Automatically detects `scormcontent/index.html` payload, bypassing blank popup wrappers (`scormdriver/indexAPI.html`). |
| **Recursive Asset Resolution** | Handles case-insensitivity, query string stripping, and nested subpath fallbacks to eliminate 404s on images, audio, and video. |
| **Dual-Pane Debugger** | Real-time CMI data inspection (`cmi.core.lesson_status`, `score`, `session_time`) and simulation actions (`Pass`, `Fail`, `Complete`, `Reset Attempt`). |
| **AdityaLMS Integration Ready** | Pre-structured service interfaces, controller endpoints, and DTOs ready to copy directly into `AdityaLMS`. |

