(() => {
    initializeRevenueGraph();
    initializeTopProductsTable();
})();

function initializeRevenueGraph() {
    const yearSelect = document.getElementById('revenueYearSelect');
    const linePath = document.getElementById('revenueLinePath');
    const areaPath = document.getElementById('revenueAreaPath');
    const pointsGroup = document.getElementById('revenuePointGroup');
    const gridGroup = document.getElementById('revenueGridLines');
    const labelsGroup = document.getElementById('revenueLabelsGroup');
    const chartShell = document.querySelector('.revenue-chart-shell[data-chart-data]');
    const snapshotShell = document.querySelector('.analytics-year-snapshot[data-year-snapshot]');
    const yearRevenueValue = document.getElementById('yearRevenueValue');
    const yearOrdersValue = document.getElementById('yearOrdersValue');
    const yearUnitsValue = document.getElementById('yearUnitsValue');
    const yearAovValue = document.getElementById('yearAovValue');

    if (!yearSelect || !linePath || !areaPath || !pointsGroup || !gridGroup || !labelsGroup || !chartShell) {
        return;
    }

    const rawChartData = chartShell.dataset.chartData;
    if (!rawChartData) {
        return;
    }

    let revenueChartData;
    let yearSnapshotData = {};
    try {
        revenueChartData = JSON.parse(rawChartData);
        if (snapshotShell?.dataset.yearSnapshot) {
            yearSnapshotData = JSON.parse(snapshotShell.dataset.yearSnapshot);
        }
    } catch {
        return;
    }

    const width = 1080;
    const height = 320;
    const padding = { top: 18, right: 10, bottom: 16, left: 10 };
    const innerHeight = height - padding.top - padding.bottom;
    const monthCount = 12;
    const horizontalGridLines = 5;

    function sanitizeSeries(series) {
        const values = Array.isArray(series)
            ? series.slice(0, monthCount).map((value) => Number(value) || 0)
            : [];

        while (values.length < monthCount) {
            values.push(0);
        }

        return values;
    }

    function buildGraphFromSeries(series) {
        const values = sanitizeSeries(series);
        const maxValue = Math.max(...values, 1);
        const minValue = Math.min(...values, 0);
        const valueRange = Math.max(maxValue - minValue, 1);

        gridGroup.innerHTML = '';
        for (let i = 0; i < horizontalGridLines; i += 1) {
            const y = padding.top + (i / (horizontalGridLines - 1)) * innerHeight;
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', String(padding.left));
            line.setAttribute('x2', String(width - padding.right));
            line.setAttribute('y1', String(y));
            line.setAttribute('y2', String(y));
            line.setAttribute('class', 'revenue-grid-line');
            gridGroup.appendChild(line);
        }

        const monthSlotWidth = width / monthCount;
        const points = values.map((value, index) => {
            const x = monthSlotWidth * index + monthSlotWidth / 2;
            const y = padding.top + ((maxValue - value) / valueRange) * innerHeight;
            return { x, y, value };
        });

        function buildSmoothPath(pts) {
            if (pts.length === 0) return '';
            if (pts.length === 1) return `M ${pts[0].x.toFixed(2)} ${pts[0].y.toFixed(2)}`;

            const slopes = [];
            const dx = [];
            const dy = [];
            for (let i = 0; i < pts.length - 1; i += 1) {
                dx[i] = pts[i + 1].x - pts[i].x;
                dy[i] = pts[i + 1].y - pts[i].y;
                slopes[i] = dy[i] / dx[i];
            }

            const tangents = new Array(pts.length);
            tangents[0] = slopes[0];
            tangents[pts.length - 1] = slopes[slopes.length - 1];

            for (let i = 1; i < pts.length - 1; i += 1) {
                tangents[i] = slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2;
            }

            const segments = [`M ${pts[0].x.toFixed(2)} ${pts[0].y.toFixed(2)}`];
            for (let i = 0; i < pts.length - 1; i += 1) {
                const cp1x = pts[i].x + dx[i] / 3;
                const cp1y = pts[i].y + (tangents[i] * dx[i]) / 3;
                const cp2x = pts[i + 1].x - dx[i] / 3;
                const cp2y = pts[i + 1].y - (tangents[i + 1] * dx[i]) / 3;
                segments.push(`C ${cp1x.toFixed(2)} ${cp1y.toFixed(2)} ${cp2x.toFixed(2)} ${cp2y.toFixed(2)} ${pts[i + 1].x.toFixed(2)} ${pts[i + 1].y.toFixed(2)}`);
            }

            return segments.join(' ');
        }

        const lineD = buildSmoothPath(points);
        linePath.setAttribute('d', lineD);

        const firstPoint = points[0];
        const lastPoint = points[points.length - 1];
        const bottomY = (padding.top + innerHeight).toFixed(2);
        areaPath.setAttribute('d', `${lineD} L ${lastPoint.x.toFixed(2)} ${bottomY} L ${firstPoint.x.toFixed(2)} ${bottomY} Z`);

        pointsGroup.innerHTML = '';
        labelsGroup.innerHTML = '';

        for (const point of points) {
            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', point.x.toFixed(2));
            circle.setAttribute('cy', point.y.toFixed(2));
            circle.setAttribute('r', '4.6');
            circle.setAttribute('class', 'revenue-point');

            const tooltip = document.createElementNS('http://www.w3.org/2000/svg', 'title');
            tooltip.textContent = `PHP ${Math.round(point.value).toLocaleString()}`;
            circle.appendChild(tooltip);
            pointsGroup.appendChild(circle);

            if (point.value <= 0) {
                continue;
            }

            const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.setAttribute('x', point.x.toFixed(2));
            text.setAttribute('y', Math.max(point.y - 14, padding.top + 10).toFixed(2));
            text.setAttribute('class', 'revenue-value-label');
            text.textContent = formatRevenueLabel(point.value);
            labelsGroup.appendChild(text);
        }
    }

    function formatPeso(value) {
        return `\u20B1${Number(value || 0).toLocaleString('en-PH', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        })}`;
    }

    function updateYearSnapshot(yearKey) {
        const snapshot = yearSnapshotData[yearKey];
        if (!snapshot) {
            return;
        }

        if (yearRevenueValue) yearRevenueValue.textContent = formatPeso(snapshot.revenue);
        if (yearOrdersValue) yearOrdersValue.textContent = Number(snapshot.orders || 0).toLocaleString('en-PH');
        if (yearUnitsValue) yearUnitsValue.textContent = Number(snapshot.units || 0).toLocaleString('en-PH');
        if (yearAovValue) yearAovValue.textContent = formatPeso(snapshot.averageOrderValue);
    }

    function applyGraph(yearKey) {
        const chart = revenueChartData[yearKey];
        if (!chart) {
            return;
        }

        buildGraphFromSeries(Array.isArray(chart.revenue) ? chart.revenue : []);
        updateYearSnapshot(yearKey);
    }

    yearSelect.addEventListener('change', () => {
        applyGraph(yearSelect.value);
    });

    const defaultYear = yearSelect.value || Object.keys(revenueChartData)[0];
    if (defaultYear) {
        applyGraph(defaultYear);
    }
}

function formatRevenueLabel(value) {
    if (value >= 1_000_000) {
        const millions = value / 1_000_000;
        return `\u20B1${millions % 1 === 0 ? millions.toFixed(0) : millions.toFixed(1)}M`;
    }
    if (value >= 1_000) {
        const thousands = value / 1_000;
        return `\u20B1${thousands % 1 === 0 ? thousands.toFixed(0) : thousands.toFixed(1)}K`;
    }

    return `\u20B1${Math.round(value).toLocaleString()}`;
}

function initializeTopProductsTable() {
    const tableWrap = document.querySelector('.analytics-table-wrap[data-products]');
    const tableBody = document.querySelector('[data-analytics-products]');
    const rangeButtons = Array.from(document.querySelectorAll('.analytics-range-toggle button[data-range]'));
    const pageButtons = Array.from(document.querySelectorAll('.analytics-pagination .analytics-page-btn[data-page]'));
    const nextButton = document.querySelector('.analytics-pagination .analytics-next-btn');

    if (!tableWrap || !tableBody || rangeButtons.length === 0 || pageButtons.length === 0 || !nextButton) {
        return;
    }

    const rawProducts = tableWrap.dataset.products;
    if (!rawProducts) {
        return;
    }

    let products;
    try {
        products = JSON.parse(rawProducts);
    } catch {
        return;
    }

    if (!Array.isArray(products)) {
        return;
    }

    const rawProductsByRange = tableWrap.dataset.productsByRange;
    let productsByRange = null;
    if (rawProductsByRange) {
        try {
            const parsed = JSON.parse(rawProductsByRange);
            if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
                productsByRange = parsed;
            }
        } catch {
            productsByRange = null;
        }
    }

    let activeRange = 'TODAY';
    let activePage = 1;
    const fixedPageSlots = pageButtons.length;
    const rowsPerPage = 10;
    let lastPageCount = 1;

    function escapeHtml(value) {
        return String(value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function safeText(value, fallback) {
        const text = String(value ?? '').trim();
        return text.length > 0 ? text : fallback;
    }

    function toNumber(value) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function normalizeProductRow(product) {
        return {
            productName: safeText(product.productName, 'Untitled Product'),
            imageUrl: safeText(product.imageUrl, ''),
            sku: safeText(product.sku, '-'),
            category: safeText(product.category, 'General'),
            unitsSold: Math.max(0, Math.round(toNumber(product.unitsSold))),
            revenueGenerated: Math.max(0, Math.round(toNumber(product.revenueGenerated)))
        };
    }

    function sortRows(rows) {
        return rows.sort((a, b) => {
            if (b.unitsSold !== a.unitsSold) return b.unitsSold - a.unitsSold;
            if (b.revenueGenerated !== a.revenueGenerated) return b.revenueGenerated - a.revenueGenerated;
            return a.productName.localeCompare(b.productName);
        });
    }

    function getRangeRows(rangeKey) {
        if (productsByRange && Object.prototype.hasOwnProperty.call(productsByRange, rangeKey)) {
            const rangeData = productsByRange[rangeKey];
            if (Array.isArray(rangeData)) {
                return sortRows(rangeData.map(normalizeProductRow));
            }
        }

        return [];
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('en-PH', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value);
    }

    function updatePaginationState(pageCount) {
        lastPageCount = Math.max(1, pageCount);
        const pageWindowStart = Math.floor((activePage - 1) / fixedPageSlots) * fixedPageSlots + 1;

        pageButtons.forEach((button, index) => {
            const pageNumber = pageWindowStart + index;
            const isAvailable = pageNumber <= lastPageCount;

            button.textContent = String(pageNumber);
            button.dataset.page = String(pageNumber);
            button.disabled = !isAvailable;
            button.hidden = !isAvailable;
            button.setAttribute('aria-disabled', String(!isAvailable));
            button.classList.toggle('active', isAvailable && pageNumber === activePage);
        });

        const isNextDisabled = activePage >= lastPageCount;
        nextButton.disabled = isNextDisabled;
        nextButton.setAttribute('aria-disabled', String(isNextDisabled));
        nextButton.setAttribute('aria-label', isNextDisabled ? 'Last page reached' : 'Go to next page');
    }

    function renderRows() {
        const rows = getRangeRows(activeRange);
        if (rows.length === 0) {
            tableBody.innerHTML = `
                <tr class="analytics-empty-row">
                    <td colspan="7">
                        <div class="analytics-empty-state">
                            <i class="fas fa-box-open"></i>
                            <p>No product sales found for the selected period.</p>
                        </div>
                    </td>
                </tr>`;
            activePage = 1;
            updatePaginationState(1);
            return;
        }

        const pageCount = Math.max(1, Math.ceil(rows.length / rowsPerPage));
        activePage = Math.min(Math.max(activePage, 1), pageCount);

        const start = (activePage - 1) * rowsPerPage;
        const pageRows = rows.slice(start, start + rowsPerPage);
        const rangeRevenueTotal = Math.max(rows.reduce((sum, row) => sum + row.revenueGenerated, 0), 1);

        tableBody.innerHTML = pageRows.map((row, index) => {
            const rank = start + index + 1;
            const revenueShare = ((row.revenueGenerated / rangeRevenueTotal) * 100).toFixed(2);

            return `
                <tr>
                    <td>${rank}</td>
                    <td>
                        <div class="analytics-product-cell">
                            <img src="${escapeHtml(row.imageUrl)}" alt="${escapeHtml(row.productName)}" onerror="this.style.visibility='hidden'" />
                            <span>${escapeHtml(row.productName)}</span>
                        </div>
                    </td>
                    <td>${escapeHtml(row.sku)}</td>
                    <td>${escapeHtml(row.category)}</td>
                    <td>${row.unitsSold}</td>
                    <td>&#8369;${formatCurrency(row.revenueGenerated)}</td>
                    <td>${revenueShare}%</td>
                </tr>
            `;
        }).join('');

        updatePaginationState(pageCount);
    }

    rangeButtons.forEach((button) => {
        button.addEventListener('click', () => {
            activeRange = button.dataset.range || 'TODAY';
            activePage = 1;

            rangeButtons.forEach((candidate) => {
                candidate.classList.toggle('active', candidate === button);
            });

            renderRows();
        });
    });

    pageButtons.forEach((button) => {
        button.addEventListener('click', () => {
            const requestedPage = Number(button.dataset.page);
            if (!Number.isFinite(requestedPage)) {
                return;
            }

            activePage = Math.min(Math.max(1, requestedPage), lastPageCount);
            renderRows();
        });
    });

    nextButton.addEventListener('click', () => {
        if (activePage >= lastPageCount) {
            return;
        }

        activePage += 1;
        renderRows();
    });

    let initiallyActiveRangeButton = rangeButtons.find((button) => button.classList.contains('active')) || rangeButtons[0];

    if (productsByRange && typeof productsByRange === 'object') {
        const orderedRangeKeys = ['TODAY', '7D', '30D', 'ALL'];
        const firstNonEmptyKey = orderedRangeKeys.find((rangeKey) => {
            const value = productsByRange[rangeKey];
            return Array.isArray(value) && value.length > 0;
        });

        if (firstNonEmptyKey) {
            initiallyActiveRangeButton = rangeButtons.find((button) => button.dataset.range === firstNonEmptyKey) || initiallyActiveRangeButton;
        }
    }

    if (initiallyActiveRangeButton) {
        activeRange = initiallyActiveRangeButton.dataset.range || 'TODAY';
        rangeButtons.forEach((button) => {
            button.classList.toggle('active', button === initiallyActiveRangeButton);
        });
    }

    renderRows();
}
