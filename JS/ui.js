/* GS Analytics — small shared UI helpers (toast messages, CSV export). */
(function (global) {
    'use strict';

    function showToast(message, isError) {
        var el = document.createElement('div');
        el.className = 'gsa-toast' + (isError ? ' gsa-toast-error' : '');
        el.textContent = message;
        document.body.appendChild(el);
        requestAnimationFrame(function () { el.classList.add('gsa-toast-in'); });
        setTimeout(function () {
            el.classList.remove('gsa-toast-in');
            setTimeout(function () { el.remove(); }, 300);
        }, 2400);
    }

    function downloadCsv(filename, rows, columns) {
        var header = columns.map(function (c) { return c.label; }).join(',');
        var lines = rows.map(function (row) {
            return columns.map(function (c) {
                var value = String(row[c.key] === undefined ? '' : row[c.key]);
                if (value.indexOf(',') !== -1 || value.indexOf('"') !== -1) {
                    value = '"' + value.replace(/"/g, '""') + '"';
                }
                return value;
            }).join(',');
        });
        var csv = [header].concat(lines).join('\n');
        var blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        var url = URL.createObjectURL(blob);
        var link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    }

    global.GSUI = { showToast: showToast, downloadCsv: downloadCsv };
})(window);
