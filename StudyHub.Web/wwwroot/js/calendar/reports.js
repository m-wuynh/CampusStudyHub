/**
 * StudyHub - Calendar Reports & Smart Insights (reports.js)
 */
document.addEventListener('DOMContentLoaded', function () {
    const tabButtons = document.querySelectorAll('.reports-tab-btn');
    const contentArea = document.getElementById('reports-content-area');
    const dateRangeEl = document.getElementById('reports-date-range');

    // KPI Elements
    const kpiTotalEl = document.getElementById('kpi-total-events');
    const kpiAvgDayEl = document.getElementById('kpi-avg-day');
    const kpiCompletionPctEl = document.getElementById('kpi-completion-pct');
    const kpiCompletedEventsEl = document.getElementById('kpi-completed-events');
    const kpiTotalEventsSubEl = document.getElementById('kpi-total-events-sub');
    const kpiProgressFillEl = document.getElementById('kpi-progress-fill');
    const kpiMissedEventsEl = document.getElementById('kpi-missed-events');
    const kpiMissedSubEl = document.getElementById('kpi-missed-sub');

    // Section B: Category Stats Table Body
    const catTbody = document.getElementById('category-stats-tbody');

    // Smart Insight Elements
    const insightBadgeEl = document.getElementById('insight-badge');
    const insightTitleEl = document.getElementById('insight-title');
    const insightMessageEl = document.getElementById('insight-message');
    const insightRecEl = document.getElementById('insight-rec');

    // Initial progress fill animation for KPI
    if (kpiProgressFillEl && kpiProgressFillEl.dataset.initialPct) {
        setTimeout(function () {
            kpiProgressFillEl.style.width = kpiProgressFillEl.dataset.initialPct + '%';
        }, 100);
    }

    // Initial progress fill animation for Table Rows
    document.querySelectorAll('.reports-table-prog-fill').forEach(function (el) {
        if (el.dataset.pct) {
            setTimeout(function () {
                el.style.width = el.dataset.pct + '%';
            }, 150);
        }
    });

    tabButtons.forEach(function (btn) {
        btn.addEventListener('click', function () {
            const range = this.dataset.range;
            if (!range || this.classList.contains('active')) return;

            // Update tab UI
            tabButtons.forEach(function (b) { b.classList.remove('active'); });
            this.classList.add('active');

            // Fetch reports data via AJAX
            loadReportsData(range);
        });
    });

    async function loadReportsData(range) {
        if (contentArea) contentArea.classList.add('reports-loading');

        try {
            const response = await fetch('/Calendar/GetReportsData?range=' + encodeURIComponent(range), {
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                throw new Error('Không thể tải dữ liệu báo cáo');
            }

            const data = await response.json();
            renderReportsData(data);
        } catch (err) {
            console.error('Lỗi khi tải báo cáo:', err);
        } finally {
            if (contentArea) contentArea.classList.remove('reports-loading');
        }
    }

    function renderReportsData(data) {
        if (!data) return;

        // Date range
        if (dateRangeEl) {
            dateRangeEl.textContent = data.dateRangeText || '';
        }

        // Section A: KPIs
        if (kpiTotalEl) kpiTotalEl.textContent = data.totalEvents;
        if (kpiAvgDayEl) kpiAvgDayEl.textContent = data.avgPerDay;
        if (kpiCompletionPctEl) kpiCompletionPctEl.textContent = data.completionPct;
        if (kpiCompletedEventsEl) kpiCompletedEventsEl.textContent = data.completedEvents;
        if (kpiTotalEventsSubEl) kpiTotalEventsSubEl.textContent = data.totalEvents;

        if (kpiProgressFillEl) {
            kpiProgressFillEl.style.width = data.completionPct + '%';
        }

        if (kpiMissedEventsEl) kpiMissedEventsEl.textContent = data.missedEvents;
        if (kpiMissedSubEl) {
            if (data.missedEvents > 0) {
                kpiMissedSubEl.textContent = 'Nhắc nhở học sinh cần bổ sung lại kiến thức';
                kpiMissedSubEl.className = 'kpi-card-sub kpi-card-sub-rose';
            } else {
                kpiMissedSubEl.textContent = 'Tuyệt vời! Không có ca quá hạn nào';
                kpiMissedSubEl.className = 'kpi-card-sub';
            }
        }

        // Section B: Category Breakdown Table
        if (catTbody) {
            if (!data.categoryStats || data.categoryStats.length === 0) {
                catTbody.innerHTML = '<tr><td colspan="6" class="text-center py-4 text-muted"><i class="bi bi-inbox me-1"></i> Chưa có dữ liệu loại sự kiện.</td></tr>';
            } else {
                let html = '';
                data.categoryStats.forEach(function (cat) {
                    html += `
                    <tr>
                        <td>
                            <div class="d-flex align-items-center gap-2">
                                <span class="reports-table-dot"></span>
                                <span class="fw-semibold text-dark">${cat.categoryName}</span>
                            </div>
                        </td>
                        <td class="text-center">
                            <span class="fw-bold text-dark">${cat.totalEvents}</span>
                            <span class="text-muted small">ca</span>
                        </td>
                        <td class="text-center">
                            <span class="badge bg-light text-secondary border">${cat.percentageOfTotal}%</span>
                        </td>
                        <td>
                            <div class="reports-table-prog-wrap">
                                <div class="d-flex justify-content-between align-items-center mb-1">
                                    <span class="small text-muted">
                                        <strong class="text-dark">${cat.completedEvents}</strong>/${cat.totalEvents} ca
                                    </span>
                                    <span class="small fw-bold text-primary">${cat.completionPct}%</span>
                                </div>
                                <div class="reports-table-prog-track">
                                    <div class="reports-table-prog-fill" data-pct="${cat.completionPct}"></div>
                                </div>
                            </div>
                        </td>
                        <td class="text-center">
                            ${cat.missedEvents > 0
                                ? `<span class="badge bg-danger-subtle text-danger fw-semibold"><i class="bi bi-exclamation-triangle-fill me-1"></i>${cat.missedEvents} ca</span>`
                                : '<span class="text-muted small">—</span>'
                            }
                        </td>
                        <td class="text-center">
                            <span class="badge category-stat-badge category-badge-${cat.statusBadgeType}">
                                ${cat.statusBadge}
                            </span>
                        </td>
                    </tr>`;
                });
                catTbody.innerHTML = html;

                setTimeout(function () {
                    catTbody.querySelectorAll('.reports-table-prog-fill').forEach(function (el) {
                        if (el.dataset.pct) el.style.width = el.dataset.pct + '%';
                    });
                }, 50);
            }
        }

        // Section C: Smart Insight
        if (insightBadgeEl) {
            insightBadgeEl.textContent = data.insightBadge || 'Đang cập nhật';
            insightBadgeEl.className = 'badge insight-badge insight-badge-' + (data.insightBadgeType || 'primary');
        }

        if (insightTitleEl) {
            insightTitleEl.textContent = data.insightTitle || '';
        }

        if (insightMessageEl) {
            insightMessageEl.textContent = data.insightMessage || '';
        }

        if (insightRecEl) {
            insightRecEl.textContent = data.insightRecommendation || '';
        }
    }
});
