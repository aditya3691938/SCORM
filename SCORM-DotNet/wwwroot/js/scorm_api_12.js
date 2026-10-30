/**
 * SCORM 1.2 Runtime Environment (RTE) API Implementation
 * Implements standard window.API object per ADL SCORM 1.2 Specification.
 */

(function(global) {
    'use strict';

    function Scorm12API(options) {
        options = options || {};
        this.packageId = options.packageId || 'default';
        this.telemetryEndpoint = options.telemetryEndpoint || '/api/telemetry/' + this.packageId;
        this.onEvent = options.onEvent || function() {};
        this.onCommit = options.onCommit || function() {};

        this.initialized = false;
        this.terminated = false;
        this.lastError = "0";

        // Default CMI 1.2 Data Model
        this.cmi = {
            "cmi.core._children": "student_id,student_name,lesson_location,credit,lesson_status,entry,score,total_time,lesson_mode,exit,session_time",
            "cmi.core.student_id": options.studentId || "10001",
            "cmi.core.student_name": options.studentName || "Learner, Sample",
            "cmi.core.lesson_location": "",
            "cmi.core.credit": "credit",
            "cmi.core.lesson_status": "not attempted",
            "cmi.core.entry": "ab-initio",
            "cmi.core.score._children": "raw,min,max",
            "cmi.core.score.raw": "",
            "cmi.core.score.min": "0",
            "cmi.core.score.max": "100",
            "cmi.core.total_time": "0000:00:00.00",
            "cmi.core.lesson_mode": "normal",
            "cmi.core.exit": "",
            "cmi.core.session_time": "0000:00:00.00",
            "cmi.suspend_data": "",
            "cmi.launch_data": "",
            "cmi.comments": "",
            "cmi.comments_from_lms": ""
        };

        // Merge initial state if provided
        if (options.initialState) {
            for (var key in options.initialState) {
                if (options.initialState.hasOwnProperty(key)) {
                    this.cmi[key] = String(options.initialState[key]);
                }
            }
        }

        this.errorStrings = {
            "0": "No error",
            "101": "General Exception",
            "201": "Invalid argument error",
            "202": "Element cannot have children",
            "203": "Element not an array - cannot have count",
            "301": "Not initialized",
            "401": "Not implemented error",
            "402": "Invalid set value, element is a keyword",
            "403": "Element is read only",
            "404": "Element is write only",
            "405": "Incorrect Data Type"
        };
    }

    Scorm12API.prototype.logEvent = function(method, element, value, result) {
        var eventData = {
            version: '1.2',
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

    Scorm12API.prototype.LMSInitialize = function(parameter) {
        if (this.initialized) {
            this.lastError = "101";
            this.logEvent("LMSInitialize", parameter, "", "false");
            return "false";
        }
        this.initialized = true;
        this.terminated = false;
        this.lastError = "0";

        if (this.cmi["cmi.core.lesson_status"] === "not attempted") {
            this.cmi["cmi.core.lesson_status"] = "incomplete";
        }

        this.logEvent("LMSInitialize", parameter, "", "true");
        return "true";
    };

    Scorm12API.prototype.LMSFinish = function(parameter) {
        if (!this.initialized || this.terminated) {
            this.lastError = "301";
            this.logEvent("LMSFinish", parameter, "", "false");
            return "false";
        }

        this.LMSCommit("");
        this.initialized = false;
        this.terminated = true;
        this.lastError = "0";
        this.logEvent("LMSFinish", parameter, "", "true");
        return "true";
    };

    Scorm12API.prototype.LMSGetValue = function(element) {
        if (!this.initialized) {
            this.lastError = "301";
            this.logEvent("LMSGetValue", element, "", "");
            return "";
        }

        var val = this.cmi[element];
        if (val === undefined) {
            // Check for array length or children
            if (element.endsWith("._count")) {
                this.lastError = "0";
                this.logEvent("LMSGetValue", element, "", "0");
                return "0";
            }
            this.lastError = "201";
            this.logEvent("LMSGetValue", element, "", "");
            return "";
        }

        this.lastError = "0";
        this.logEvent("LMSGetValue", element, "", String(val));
        return String(val);
    };

    Scorm12API.prototype.LMSSetValue = function(element, value) {
        if (!this.initialized) {
            this.lastError = "301";
            this.logEvent("LMSSetValue", element, value, "false");
            return "false";
        }

        var readOnlyElements = [
            "cmi.core._children",
            "cmi.core.student_id",
            "cmi.core.student_name",
            "cmi.core.credit",
            "cmi.core.entry",
            "cmi.core.total_time",
            "cmi.core.lesson_mode"
        ];

        if (readOnlyElements.indexOf(element) !== -1) {
            this.lastError = "403";
            this.logEvent("LMSSetValue", element, value, "false");
            return "false";
        }

        this.cmi[element] = String(value);
        this.lastError = "0";
        this.logEvent("LMSSetValue", element, value, "true");
        return "true";
    };

    Scorm12API.prototype.LMSCommit = function(parameter) {
        if (!this.initialized) {
            this.lastError = "301";
            this.logEvent("LMSCommit", parameter, "", "false");
            return "false";
        }

        var self = this;
        var payload = {
            package_id: this.packageId,
            version: "1.2",
            status: this.cmi["cmi.core.lesson_status"],
            score: this.cmi["cmi.core.score.raw"],
            location: this.cmi["cmi.core.lesson_location"],
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
                console.warn("[SCORM 1.2] Telemetry sync error:", err);
            });
        }

        this.lastError = "0";
        this.logEvent("LMSCommit", parameter, "", "true");
        return "true";
    };

    Scorm12API.prototype.LMSGetLastError = function() {
        return String(this.lastError);
    };

    Scorm12API.prototype.GetLastError = function() {
        return this.LMSGetLastError();
    };

    Scorm12API.prototype.LMSGetErrorString = function(errorCode) {
        return this.errorStrings[String(errorCode)] || "Unknown error";
    };

    Scorm12API.prototype.GetErrorString = function(errorCode) {
        return this.LMSGetErrorString(errorCode);
    };

    Scorm12API.prototype.LMSGetDiagnostic = function(errorCode) {
        return "SCORM 1.2 Diagnostic for code " + errorCode + ": " + this.LMSGetErrorString(errorCode);
    };

    Scorm12API.prototype.GetDiagnostic = function(errorCode) {
        return this.LMSGetDiagnostic(errorCode);
    };

    global.Scorm12API = Scorm12API;

})(window);

