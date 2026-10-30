/**
 * Live SCORM RTE Debugger & CMI Inspector
 * Visualizes real-time SCORM API method calls, tracks CMI state changes,
 * and enables manual simulation of LMS events.
 */

(function(global) {
    'use strict';

    function ScormDebugger(options) {
        options = options || {};
        this.logContainer = document.getElementById(options.logContainerId || 'scorm-log-list');
        this.cmiTableBody = document.getElementById(options.cmiTableBodyId || 'cmi-state-tbody');
        this.statusBadge = document.getElementById(options.statusBadgeId || 'scorm-status-badge');
        this.scoreBadge = document.getElementById(options.scoreBadgeId || 'scorm-score-badge');
        this.callCounter = document.getElementById(options.callCounterId || 'scorm-call-counter');

        this.filterType = 'all';
        this.events = [];
        this.cmiState = {};
        this.callCount = 0;

        this.initControls();
    }

    ScormDebugger.prototype.initControls = function() {
        var self = this;
        var filterBtns = document.querySelectorAll('[data-scorm-filter]');
        filterBtns.forEach(function(btn) {
            btn.addEventListener('click', function() {
                filterBtns.forEach(function(b) { b.classList.remove('active'); });
                btn.classList.add('active');
                self.filterType = btn.getAttribute('data-scorm-filter');
                self.renderLogs();
            });
        });

        var clearBtn = document.getElementById('btn-clear-scorm-logs');
        if (clearBtn) {
            clearBtn.addEventListener('click', function() {
                self.events = [];
                self.callCount = 0;
                if (self.callCounter) self.callCounter.textContent = '0 calls';
                self.renderLogs();
            });
        }

        var exportBtn = document.getElementById('btn-export-cmi-json');
        if (exportBtn) {
            exportBtn.addEventListener('click', function() {
                var dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(self.cmiState, null, 2));
                var dlAnchor = document.createElement('a');
                dlAnchor.setAttribute("href", dataStr);
                dlAnchor.setAttribute("download", "scorm_cmi_state.json");
                dlAnchor.click();
            });
        }
    };

    ScormDebugger.prototype.handleEvent = function(eventData) {
        this.events.unshift(eventData);
        this.callCount++;
        if (this.callCounter) {
            this.callCounter.textContent = this.callCount + ' call' + (this.callCount === 1 ? '' : 's');
        }

        if (eventData.cmiSnapshot) {
            this.cmiState = eventData.cmiSnapshot;
            this.updateCmiUI();
        }

        this.renderLogs();
    };

    ScormDebugger.prototype.renderLogs = function() {
        if (!this.logContainer) return;

        var filtered = this.events.filter(function(ev) {
            if (this.filterType === 'all') return true;
            if (this.filterType === 'set' && ev.method.toLowerCase().indexOf('set') !== -1) return true;
            if (this.filterType === 'get' && ev.method.toLowerCase().indexOf('get') !== -1) return true;
            if (this.filterType === 'commit' && ev.method.toLowerCase().indexOf('commit') !== -1) return true;
            if (this.filterType === 'status' && (ev.element.indexOf('status') !== -1 || ev.element.indexOf('score') !== -1)) return true;
            return false;
        }.bind(this));

        if (filtered.length === 0) {
            this.logContainer.innerHTML = '<div class="text-muted text-center py-4 small">No SCORM API events recorded yet. Launch the module to start interaction.</div>';
            return;
        }

        var html = '';
        filtered.slice(0, 80).forEach(function(ev) {
            var badgeClass = 'bg-secondary';
            var methodLower = ev.method.toLowerCase();
            if (methodLower.indexOf('init') !== -1) badgeClass = 'bg-primary';
            else if (methodLower.indexOf('finish') !== -1 || methodLower.indexOf('terminate') !== -1) badgeClass = 'bg-dark';
            else if (methodLower.indexOf('set') !== -1) badgeClass = 'bg-warning text-dark';
            else if (methodLower.indexOf('get') !== -1) badgeClass = 'bg-info text-dark';
            else if (methodLower.indexOf('commit') !== -1) badgeClass = 'bg-success';

            var isError = ev.result === 'false' || (ev.error && ev.error !== '0');
            var errorTag = isError ? '<span class="badge bg-danger ms-1">Err ' + ev.error + '</span>' : '';

            html += '<div class="scorm-log-item py-1 px-2 mb-1 border-bottom d-flex align-items-center justify-content-between font-monospace small ' + (isError ? 'bg-danger-subtle' : '') + '">';
            html += '  <div class="d-flex align-items-center text-truncate">';
            html += '    <span class="text-muted me-2" style="font-size:0.75rem;">' + ev.timestamp + '</span>';
            html += '    <span class="badge ' + badgeClass + ' me-2">' + ev.method + '</span>';
            if (ev.element) {
                html += '    <strong class="text-primary-emphasis me-1 text-truncate" style="max-width:240px;">' + ev.element + '</strong>';
            }
            if (ev.value !== '') {
                html += '    <span class="text-secondary me-1">=</span>';
                html += '    <span class="text-success fw-bold text-truncate" style="max-width:180px;">"' + ev.value + '"</span>';
            }
            html += '  </div>';
            html += '  <div class="d-flex align-items-center ms-2 flex-shrink-0">';
            html += '    <span class="badge ' + (ev.result === 'true' ? 'bg-light text-dark border' : 'bg-secondary') + '">' + ev.result + '</span>';
            html += errorTag;
            html += '  </div>';
            html += '</div>';
        });

        this.logContainer.innerHTML = html;
    };

    ScormDebugger.prototype.updateCmiUI = function() {
        // Status resolution
        var status = this.cmiState['cmi.core.lesson_status'] || this.cmiState['cmi.completion_status'] || 'not attempted';
        if (this.statusBadge) {
            this.statusBadge.textContent = status.toUpperCase();
            this.statusBadge.className = 'badge px-3 py-2 fs-6 ' + (
                status === 'completed' || status === 'passed' ? 'bg-success' :
                status === 'failed' ? 'bg-danger' :
                status === 'incomplete' ? 'bg-warning text-dark' : 'bg-secondary'
            );
        }

        // Score resolution
        var rawScore = this.cmiState['cmi.core.score.raw'] || this.cmiState['cmi.score.raw'] || '-';
        if (this.scoreBadge) {
            this.scoreBadge.textContent = rawScore !== '-' && rawScore !== '' ? rawScore + '%' : 'N/A';
        }

        // CMI State Table
        if (this.cmiTableBody) {
            var rows = '';
            var keys = Object.keys(this.cmiState).sort();
            keys.forEach(function(k) {
                var v = this.cmiState[k];
                if (typeof v === 'object') v = JSON.stringify(v);
                rows += '<tr>';
                rows += '  <td class="font-monospace small text-primary fw-semibold">' + k + '</td>';
                rows += '  <td class="font-monospace small text-dark">' + (v !== '' ? v : '<em class="text-muted">(empty)</em>') + '</td>';
                rows += '</tr>';
            }.bind(this));
            this.cmiTableBody.innerHTML = rows;
        }
    };

    global.ScormDebugger = ScormDebugger;

})(window);

