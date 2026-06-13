
(function (global) {
    let rows = [];
    let aisleAfterColumns = [];
    let reverseNumbering = false;
    let _initialized = false;

    function tierColor(multiplier) {
        if (multiplier >= 2.0) return '#c0392b';
        if (multiplier >= 1.5) return '#e07b00';
        if (multiplier > 1.0) return '#c9a000';
        return '#2e8b57';
    }

    function escHtml(str) {
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function renderRows() {
        const container = document.getElementById('rowCards');
        if (!container) return;
        container.innerHTML = '';
        rows.forEach(function (r, i) {
            const isLast = i === rows.length - 1;
            const card = document.createElement('div');
            card.className = 'card p-3 mb-2';
            card.dataset.rowIdx = String(i);
            card.style.borderLeft = '4px solid ' + tierColor(r.priceMultiplier);
            const hpaCheck = isLast
                ? '<div class="form-check mb-0 mt-3">' +
                    '<input type="checkbox" class="form-check-input" id="hpa_' + i + '"' + (r.hasPassageAfter !== false ? ' checked' : '') + ' data-row="' + i + '" data-field="hasPassageAfter">' +
                    '<label class="form-check-label small text-muted" for="hpa_' + i + '">Passage after</label>' +
                  '</div>'
                : '';
            card.innerHTML =
                '<div class="d-flex flex-wrap gap-2 align-items-center">' +
                  '<div>' +
                    '<div class="text-muted small mb-1">Label</div>' +
                    '<input type="text" class="form-control form-control-sm" value="' + escHtml(r.label) + '" style="width:60px" data-row="' + i + '" data-field="label">' +
                  '</div>' +
                  '<div>' +
                    '<div class="text-muted small mb-1">' + (r.isSofa ? 'Kanapy' : 'Seats') + '</div>' +
                    '<input type="number" class="form-control form-control-sm" value="' + r.seatCount + '" min="1" max="100" style="width:80px" data-row="' + i + '" data-field="seatCount">' +
                  '</div>' +
                  '<div>' +
                    '<div class="text-muted small mb-1">Price ×</div>' +
                    '<input type="number" class="form-control form-control-sm" value="' + r.priceMultiplier + '" min="0.1" max="10" step="0.1" style="width:80px" data-row="' + i + '" data-field="priceMultiplier">' +
                  '</div>' +
                  '<div class="form-check mb-0 mt-3">' +
                    '<input type="checkbox" class="form-check-input" id="esp_' + i + '"' + (r.extraSpacingBefore ? ' checked' : '') + ' data-row="' + i + '" data-field="extraSpacingBefore">' +
                    '<label class="form-check-label small text-muted" for="esp_' + i + '">Gap before</label>' +
                  '</div>' +
                  '<div class="form-check mb-0 mt-3">' +
                    '<input type="checkbox" class="form-check-input" id="sofa_' + i + '"' + (r.isSofa ? ' checked' : '') + ' data-row="' + i + '" data-field="isSofa">' +
                    '<label class="form-check-label small text-muted" for="sofa_' + i + '">Kanapa</label>' +
                  '</div>' +
                  hpaCheck +
                  '<button type="button" class="btn btn-sm btn-outline-danger ms-auto mt-3" data-remove-row="' + i + '"><i class="bi bi-trash"></i></button>' +
                '</div>';
            container.appendChild(card);
        });
    }

    function renderPreview() {
        const preview = document.getElementById('hallPreview');
        if (!preview) return;
        if (rows.length === 0) {
            preview.innerHTML = '<p class="text-muted small text-center py-4">Add rows to see preview</p>';
            scalePreviewToFit();
            return;
        }
        let html = '<div style="display:flex;flex-direction:column;align-items:flex-start;width:max-content;margin:0 auto;">';
        html += '<div class="screen-bar" style="align-self:stretch;max-width:none;margin:0 0 calc(2 * 36px + 1rem) 0;">SCREEN</div>';

        rows.forEach(function (r, idx) {
            const isLast = idx === rows.length - 1;
            const showAisles = !isLast || r.hasPassageAfter !== false;
            const mt = r.extraSpacingBefore ? ' mt-4' : '';
            html += '<div class="seat-row' + mt + '"><span class="row-label">' + escHtml(r.label) + '</span>';

            if (aisleAfterColumns.indexOf(-1) >= 0) {
                html += '<span class="aisle-gap' + (showAisles ? '' : ' aisle-gap--invisible') + '"></span>';
            }

            if (r.isSofa) {
                for (var c = 0; c < r.seatCount; c++) {
                    var dn = reverseNumbering ? (r.seatCount - c) : (c + 1);
                    html += '<button type="button" class="seat-btn sofa"' +
                        ' style="background:' + tierColor(r.priceMultiplier) + ';pointer-events:none;">' +
                        dn + '</button>';
                    if (aisleAfterColumns.indexOf(c * 2) >= 0 || aisleAfterColumns.indexOf(c * 2 + 1) >= 0) {
                        html += '<span class="aisle-gap' + (showAisles ? '' : ' aisle-gap--invisible') + '"></span>';
                    }
                }
            } else {
                for (var c = 0; c < r.seatCount; c++) {
                    var dn = reverseNumbering ? (r.seatCount - c) : (c + 1);
                    html += '<button type="button" class="seat-btn"' +
                        ' style="background:' + tierColor(r.priceMultiplier) + ';pointer-events:none;">' +
                        dn + '</button>';
                    if (aisleAfterColumns.indexOf(c) >= 0) {
                        html += '<span class="aisle-gap' + (showAisles ? '' : ' aisle-gap--invisible') + '"></span>';
                    }
                }
            }

            html += '<span class="row-label">' + escHtml(r.label) + '</span></div>';

            if (isLast && r.hasPassageAfter !== false) {
                html += '<div class="hall-back-aisle"></div>';
            }
        });

        var total = rows.reduce(function (s, r) { return s + r.seatCount; }, 0);
        html += '<div class="text-muted small mt-3" style="align-self:center;">Total: <strong>' + total + '</strong> seats</div>';
        html += '</div>';
        preview.innerHTML = html;
        scalePreviewToFit();
    }

    function scalePreviewToFit() {
        var preview = document.getElementById('hallPreview');
        if (!preview) return;
        preview.style.zoom = '1';
        var natural = preview.scrollWidth;
        var available = preview.parentElement.clientWidth - 32;
        if (natural > 0 && available > 0 && natural > available) {
            preview.style.zoom = String(available / natural);
        }
    }

    function syncCapacity() {
        var cap = rows.reduce(function (s, r) { return s + r.seatCount; }, 0);
        var f = document.getElementById('capacityField');
        if (f) f.value = cap || 1;
    }

    function renderAll() {
        renderRows();
        renderPreview();
        syncCapacity();
    }

    function initHallEditor(initialRows, initialAisles, initialReverseNumbering) {
        rows = initialRows || [];
        aisleAfterColumns = initialAisles || [];
        reverseNumbering = initialReverseNumbering || false;

        if (!_initialized) {
            _initialized = true;

            var aisleInput = document.getElementById('aisleInput');
            if (aisleInput) {
                aisleInput.value = aisleAfterColumns.map(function (n) { return n + 1; }).join(',');
                aisleInput.addEventListener('input', function () {
                    aisleAfterColumns = aisleInput.value
                        .split(',').map(function (s) { return parseInt(s.trim()) - 1; })
                        .filter(function (n) { return !isNaN(n); });
                    renderPreview();
                });
            }

            var revChk = document.getElementById('reverseNumberingChk');
            if (revChk) {
                revChk.checked = reverseNumbering;
                revChk.addEventListener('change', function () {
                    reverseNumbering = revChk.checked;
                    renderPreview();
                });
            }

            var addBtn = document.getElementById('addRowBtn');
            if (addBtn) {
                addBtn.addEventListener('click', function () {
                    var labels = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
                    rows.push({
                        label: labels[rows.length] || String(rows.length + 1),
                        seatCount: 10,
                        extraSpacingBefore: false,
                        priceMultiplier: 1.0,
                        isSofa: false,
                        hasPassageAfter: true
                    });
                    renderAll();
                });
            }

            var rowCards = document.getElementById('rowCards');
            if (rowCards) {
                rowCards.addEventListener('input', function (e) {
                    var el = e.target;
                    if (el.type === 'checkbox') return;
                    var idx = parseInt(el.dataset.row);
                    var field = el.dataset.field;
                    if (isNaN(idx) || !field || !rows[idx]) return;

                    if (field === 'seatCount') {
                        rows[idx][field] = parseInt(el.value) || 1;
                    } else if (field === 'priceMultiplier') {
                        rows[idx][field] = parseFloat(el.value) || 1;
                        var card = el.closest('[data-row-idx]');
                        if (card) card.style.borderLeft = '4px solid ' + tierColor(rows[idx][field]);
                    } else {
                        rows[idx][field] = el.value;
                    }
                    renderPreview();
                    syncCapacity();
                });

                rowCards.addEventListener('change', function (e) {
                    var el = e.target;
                    if (el.type !== 'checkbox') return;
                    var idx = parseInt(el.dataset.row);
                    var field = el.dataset.field;
                    if (isNaN(idx) || !field || !rows[idx]) return;
                    rows[idx][field] = el.checked;
                    renderAll();
                });

                rowCards.addEventListener('click', function (e) {
                    var btn = e.target.closest('[data-remove-row]');
                    if (!btn) return;
                    var idx = parseInt(btn.dataset.removeRow);
                    if (!isNaN(idx)) {
                        rows.splice(idx, 1);
                        renderAll();
                    }
                });
            }

            var form = document.getElementById('hallForm');
            if (form) {
                form.addEventListener('submit', function () {
                    document.getElementById('layoutJson').value = JSON.stringify({
                        rows: rows,
                        aisleAfterColumns: aisleAfterColumns,
                        reverseNumbering: reverseNumbering
                    });
                });
            }

            window.addEventListener('resize', scalePreviewToFit);
        } else {
            var aisleInput = document.getElementById('aisleInput');
            if (aisleInput) aisleInput.value = aisleAfterColumns.map(function (n) { return n + 1; }).join(',');
            var revChk = document.getElementById('reverseNumberingChk');
            if (revChk) revChk.checked = reverseNumbering;
        }

        renderAll();
    }

    global.HallEditor = {};
    global.initHallEditor = initHallEditor;

}(window));
