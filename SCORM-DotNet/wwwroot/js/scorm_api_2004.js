/**
 * SCORM 2004 Runtime Environment (RTE) API Implementation
 * Implements standard window.API_1484_11 object per ADL SCORM 2004 Specification.
 */

(function(global) {
    'use strict';

    function Scorm2004API(options) {
        options = options || {};
        this.packageId = options.packageId || 'default';
        this.telemetryEndpoint = options.telemetryEndpoint || '/api/telemetry/' + this.packageId;
        this.onEvent = options.onEvent || function() {};
        this.onCommit = options.onCommit || function() {};

        this.initialized = false;
        this.terminated = false;
        this.lastError = "0";

        // Default CMI 2004 Data Model
        this.cmi = {
            "_version": "1.0",
            "cmi._version": "1.0",
            "cmi.learner_id": options.studentId || "10001",
            "cmi.learner_name": options.studentName || "Learner, Sample",
            "cmi.location": "",
            "cmi.completion_status": "not attempted",
            "cmi.completion_threshold": "1.0",
            "cmi.success_status": "unknown",
            "cmi.credit": "credit",
            "cmi.entry": "ab-initio",
            "cmi.exit": "",
            "cmi.mode": "normal",
            "cmi.score.raw": "",
            "cmi.score.scaled": "",
            "cmi.score.min": "0",
            "cmi.score.max": "100",
            "cmi.session_time": "PT0H0M0S",
            "cmi.total_time": "PT0H0M0S",
            "cmi.suspend_data": "",
            "cmi.launch_data": ""
        };

        if (options.initialState) {
            for (var key in options.initialState) {
                if (options.initialState.hasOwnProperty(key)) {
                    this.cmi[key] = String(options.initialState[key]);
                }
            }
        }

        this.errorStrings = {
            "0": "No Error",
            "101": "General Exception",
            "102": "General Initialization Failure",
            "103": "Already Initialized",
            "104": "Content Instance Terminated",
            "111": "General Termination Failure",
            "112": "Termination Before Initialization",
            "113": "Termination After Termination",
            "122": "Retrieve Data Before Initialization",
            "123": "Retrieve Data After Termination",
            "132": "Store Data Before Initialization",
            "133": "Store Data After Termination",
            "142": "Commit Before Initialization",
            "143": "Commit After Termination",
            "201": "General Argument Error",
            "301": "General Get Failure",
            "351": "General Set Failure",
            "391": "General Commit Failure",
            "401": "Undefined Data Model Element",
            "402": "Unimplemented Data Model Element",
            "403": "Data Model Element Value Not Initialized",
            "404": "Data Model Element Is Read Only",
            "405": "Data Model Element Is Write Only",
            "406": "Data Model Element Type Mismatch",
            "407": "Data Model Element Value Out Of Range"
        };
    }

    Scorm2004API.prototype.logEvent = function(method, element, value, result) {
        var eventData = {
            version: '2004',
            method: method,
            element: element || '',
            value: value !== undefined ? value : '',
            result: result,
            error: this.lastError,
            timestamp: new Date().toLocaleTimeString(),
            cmiSnapshot: JSON.parse(JSON.stringify(this.cmi))
        };
        this.onEvent(eventData);
    };

    Scorm2004API.prototype.Initialize = function(parameter) {
        if (this.initialized) {
            this.lastError = "103";
            this.logEvent("Initialize", parameter, "", "false");
            return "false";
        }
        if (this.terminated) {
            this.lastError = "104";
            this.logEvent("Initialize", parameter, "", "false");
            return "false";
        }
        this.initialized = true;
        this.terminated = false;
        this.lastError = "0";

        if (this.cmi["cmi.completion_status"] === "not attempted" || this.cmi["cmi.completion_status"] === "unknown") {
            this.cmi["cmi.completion_status"] = "incomplete";
        }

        this.logEvent("Initialize", parameter, "", "true");
        return "true";
    };

    Scorm2004API.prototype.Terminate = function(parameter) {
        if (!this.initialized || this.terminated) {
            this.lastError = this.terminated ? "113" : "112";
            this.logEvent("Terminate", parameter, "", "false");
            return "false";
        }

        this.Commit("");
        this.initialized = false;
        this.terminated = true;
        this.lastError = "0";
        this.logEvent("Terminate", parameter, "", "true");
        return "true";
    };

    Scorm2004API.prototype.GetValue = function(element) {
        if (!this.initialized) {
            this.lastError = "122";
            this.logEvent("GetValue", element, "", "");
            return "";
        }

        var val = this.cmi[element];
        if (val === undefined) {
            if (element.endsWith("._count")) {
                this.lastError = "0";
                this.logEvent("GetValue", element, "", "0");
                return "0";
            }
            this.lastError = "401";
            this.logEvent("GetValue", element, "", "");
            return "";
        }

        this.lastError = "0";
        this.logEvent("GetValue", element, "", String(val));
        return String(val);
    };

    Scorm2004API.prototype.SetValue = function(element, value) {
        if (!this.initialized) {
            this.lastError = "132";
            this.logEvent("SetValue", element, value, "false");
            return "false";
        }

        var readOnlyElements = [
            "_version",
            "cmi._version",
            "cmi.learner_id",
            "cmi.learner_name",
            "cmi.credit",
            "cmi.entry",
            "cmi.mode",
            "cmi.total_time"
        ];

        if (readOnlyElements.indexOf(element) !== -1) {
            this.lastError = "404";
            this.logEvent("SetValue", element, value, "false");
            return "false";
        }

        // Auto calculate raw from scaled if set
        if (element === "cmi.score.scaled") {
            var scaledNum = parseFloat(value);
            if (!isNaN(scaledNum)) {
                this.cmi["cmi.score.raw"] = String(Math.round(scaledNum * 100));
            }
        }

        this.cmi[element] = String(value);
        this.lastError = "0";
        this.logEvent("SetValue", element, value, "true");
        return "true";
    };

    Scorm2004API.prototype.Commit = function(parameter) {
        if (!this.initialized) {
            this.lastError = "142";
            this.logEvent("Commit", parameter, "", "false");
            return "false";
        }

        var self = this;
        var payload = {
            package_id: this.packageId,
            version: "2004",
            completion_status: this.cmi["cmi.completion_status"],
            success_status: this.cmi["cmi.success_status"],
            score_raw: this.cmi["cmi.score.raw"],
            score_scaled: this.cmi["cmi.score.scaled"],
            location: this.cmi["cmi.location"],
            suspend_data: this.cmi["cmi.suspend_data"],
            cmi: this.cmi
        };

        if (this.telemetryEndpoint) {
            fetch(this.telemetryEndpoint, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            })
            .then(function(res) { return res.json(); })
            .then(function(data) {
                self.onCommit(data);
            })
            .catch(function(err) {
                console.warn("[SCORM 2004] Telemetry sync error:", err);
            });
        }

        this.lastError = "0";
        this.logEvent("Commit", parameter, "", "true");
        return "true";
    };

    Scorm2004API.prototype.GetLastError = function() {
        return String(this.lastError);
    };

    Scorm2004API.prototype.GetErrorString = function(errorCode) {
        return this.errorStrings[String(errorCode)] || "Unknown error";
    };

    Scorm2004API.prototype.GetDiagnostic = function(errorCode) {
        return "SCORM 2004 Diagnostic for error code " + errorCode + ": " + this.GetErrorString(errorCode);
    };

    global.Scorm2004API = Scorm2004API;

})(window);

