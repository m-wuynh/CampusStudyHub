/* ========================================================
   Calendar Module: Màn Lịch chính (Index)
   Chứa toàn bộ JS cho màn Xem Thời khóa biểu & Kế hoạch
   ======================================================== */
document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    var calendarEl = document.getElementById('calendar');
    if (!calendarEl) return;

    var currentSearchQuery = '';
    var currentCategoryFilter = 'all';
    var activeCopiedEvent = null;
    var activeTogglePromise = null;
    var desiredState = null;

    // ========================================================
    // 1. Khởi tạo FullCalendar
    // ========================================================
    var calendar = new FullCalendar.Calendar(calendarEl, {
        locale: 'en',
        initialView: 'timeGridWeek',
        firstDay: 0, // 0 = Sunday
        height: 'auto',
        headerToolbar: {
            left: 'today prev,next title',
            center: '',
            right: 'timeGridWeek,timeGridDay'
        },
        buttonText: {
            today: 'Hôm nay',
            week: 'Tuần',
            day: 'Ngày'
        },
        allDaySlot: false,
        slotMinTime: '00:00:00',
        slotMaxTime: '24:00:00',
        slotDuration: '01:00:00',
        slotLabelInterval: '01:00',
        scrollTime: '07:00:00',
        eventMinHeight: 30,
        slotLabelFormat: {
            hour: 'numeric',
            meridiem: 'short',
            omitZeroMinute: true
        },
        dayHeaderContent: function (arg) {
            var dayNames = ['CN', 'THỨ 2', 'THỨ 3', 'THỨ 4', 'THỨ 5', 'THỨ 6', 'THỨ 7'];
            var dayName = dayNames[arg.date.getDay()];
            var dateNum = arg.date.getDate();
            var html = '<div>' + dayName + '</div>' +
                '<div class="sh-date-num">' + dateNum + '</div>';
            return { html: html };
        },
        events: document.body.dataset.guest === 'true' ? [] : '/Calendar/GetEvents',
        eventClassNames: function (arg) {
            var isCompleted = !!arg.event.extendedProps.isCompleted;
            var end = arg.event.end || arg.event.start;
            var now = new Date();

            var classes = [];
            if (isCompleted) {
                classes.push('event-state-completed');
            } else if (end && new Date(end) < now) {
                classes.push('event-state-missed');
            } else {
                classes.push('event-state-pending');
            }

            // Live search filter on calendar view
            if (currentSearchQuery) {
                var q = currentSearchQuery.toLowerCase();
                var title = (arg.event.title || '').toLowerCase();
                var loc = (arg.event.extendedProps?.location || '').toLowerCase();
                var desc = (arg.event.extendedProps?.description || '').toLowerCase();
                if (title.includes(q) || loc.includes(q) || desc.includes(q)) {
                    classes.push('event-search-match');
                } else {
                    classes.push('event-search-dimmed');
                }
            }

            // Category filter on calendar view
            if (currentCategoryFilter && currentCategoryFilter !== 'all') {
                var evType = (arg.event.extendedProps?.type || '').toLowerCase();
                var targetFilter = currentCategoryFilter.toLowerCase();
                if (evType !== targetFilter) {
                    classes.push('event-category-hidden');
                }
            }

            return classes;
        },
        /* [TẠM ẨN NÚT SAO CHÉP TRÊN Ô SỰ KIỆN]
        eventDidMount: function(info) {
            if (!info.el.querySelector('.fc-event-copy-btn')) {
                var copyBtn = document.createElement('button');
                copyBtn.className = 'fc-event-copy-btn';
                copyBtn.type = 'button';
                copyBtn.title = 'Sao chép sự kiện';
                copyBtn.innerHTML = '<i class="bi bi-copy"></i>';
                copyBtn.addEventListener('click', function(e) {
                    e.preventDefault();
                    e.stopPropagation();
                    startCopyEvent(info.event);
                });
                info.el.appendChild(copyBtn);
            }
        },
        */
        eventClick: function (info) {
            showEventPopover(info.event, info.el);
        },
        dateClick: function (info) {
            handleCalendarDateClick(info);
        },
        eventsSet: function (events) {
            updateWeeklyProgress(events);
        }
    });

calendar.render();
window.calendarInstance = calendar;

// ========================================================
// 2. Tính toán tiến độ học tập tuần & Thống kê
// ========================================================
function updateWeeklyProgress() {
    var view = calendar ? calendar.view : null;
    var viewDate = view && view.currentStart ? view.currentStart : new Date();
    var dateStr = viewDate.toISOString().split('T')[0];
    var category = currentCategoryFilter || 'all';

    fetch('/Calendar/GetWeeklyProgress?date=' + dateStr + '&category=' + encodeURIComponent(category))
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data) return;
            var barEl = document.getElementById('calendar-progress-bar');
            var percentEl = document.getElementById('calendar-progress-percent');
            var completedEl = document.getElementById('stat-completed-badge');
            var pendingEl = document.getElementById('stat-pending-badge');
            var missedEl = document.getElementById('stat-missed-badge');

            if (barEl) barEl.style.width = data.completionPct + '%';
            if (percentEl) percentEl.innerText = data.completionPct + '% (' + data.completedEvents + '/' + data.totalEvents + ' ca)';
            if (completedEl) completedEl.innerHTML = '<i class="bi bi-check2 me-1 fs-6"></i><span>' + data.completedEvents + ' hoàn thành</span>';
            if (pendingEl) pendingEl.innerHTML = '<i class="bi bi-clock me-1"></i><span>' + data.upcomingEvents + ' sắp tới</span>';
            if (missedEl) missedEl.innerHTML = '<i class="bi bi-exclamation-triangle me-1"></i><span>' + data.missedEvents + ' quá hạn</span>';
        })
        .catch(function (err) {
            console.error('Lỗi khi tải tiến độ từ server:', err);
        });
}

// ========================================================
// 3. Popover chi tiết sự kiện & Optimistic UI
// ========================================================
function showEventPopover(eventApi, targetEl) {
    var popover = document.getElementById('event-popover');
    if (!popover || !calendarEl) return;

    var titleEl = document.getElementById('popover-title');
    if (titleEl) titleEl.innerText = eventApi.title;

    var start = eventApi.start;
    var end = eventApi.end;
    var timeStr = start ? start.toLocaleDateString('vi-VN', { weekday: 'long', day: 'numeric', month: 'long' }) + ' · ' : '';

    var formatTime = function (date) {
        if (!date) return '';
        var h = date.getHours();
        var m = date.getMinutes();
        var ampm = h >= 12 ? 'PM' : 'AM';
        h = h % 12;
        h = h ? h : 12;
        var mStr = m < 10 ? '0' + m : m;
        return h + ':' + mStr + ampm;
    };

    if (start) {
        timeStr += formatTime(start) + ' – ' + (end ? formatTime(end) : '');
    }
    var timeEl = document.getElementById('popover-time');
    if (timeEl) timeEl.innerText = timeStr;

    var props = eventApi.extendedProps || {};
    var isCompleted = !!props.isCompleted;
    var type = props.type || 'Tự học';
    var endTime = eventApi.end || eventApi.start;
    var colorBox = document.getElementById('popover-color');

    var typeEl = document.getElementById('popover-type');
    if (typeEl) typeEl.innerText = props.categoryName || type || 'Tự học';

    var desc = props.description || '';
    var descRow = document.getElementById('popover-desc-row');
    var descEl = document.getElementById('popover-desc');
    if (descRow && descEl) {
        if (desc.trim()) {
            descEl.innerText = desc;
            descRow.style.setProperty('display', 'flex', 'important');
        } else {
            descRow.style.setProperty('display', 'none', 'important');
        }
    }

    var loc = props.location || '';
    var locRow = document.getElementById('popover-location-row');
    var locEl = document.getElementById('popover-location');
    if (locRow && locEl) {
        if (loc.trim()) {
            locEl.innerText = loc;
            locRow.style.setProperty('display', 'flex', 'important');
        } else {
            locRow.style.setProperty('display', 'none', 'important');
        }
    }

    var toggleBtn = document.getElementById('btn-toggle-complete');
    var toggleIcon = document.getElementById('popover-complete-icon');
    var toggleText = document.getElementById('popover-complete-text');
    var chkComplete = document.getElementById('popover-is-completed');
    var chkRow = document.getElementById('popover-completion-row') || (chkComplete ? chkComplete.closest('.px-3') : null);

    var isPast = endTime && (new Date(endTime) < new Date());

    var editBtn = document.getElementById('btn-edit-event');
    function updateEditButton(completed) {
        if (editBtn) {
            if (isPast || completed) {
                editBtn.style.setProperty('display', 'none', 'important');
                editBtn.onclick = null;
            } else {
                editBtn.style.removeProperty('display');
                editBtn.onclick = function () {
                    window.location.href = '/Calendar/Update/' + eventApi.id;
                };
            }
        }
    }

    function updateCompletionUI(completed) {
        var curMissed = !completed && isPast;
        var curColor = completed ? '#1e8e3e' : (curMissed ? '#d93025' : '#1a73e8');
        if (colorBox) colorBox.style.backgroundColor = curColor;
        if (titleEl) {
            titleEl.style.textDecoration = completed ? 'line-through' : 'none';
            titleEl.style.color = completed ? '#70757a' : '#3c4043';
        }

        updateEditButton(completed);

        if (chkRow) {
            if (isPast) chkRow.style.setProperty('display', 'none', 'important');
            else chkRow.style.removeProperty('display');
        }

        if (chkComplete) {
            chkComplete.checked = completed;
            chkComplete.disabled = isPast;
        }

        if (toggleBtn && toggleIcon && toggleText) {
            if (completed) {
                toggleBtn.className = "btn btn-sm btn-success text-white border-0 py-1 px-2 d-flex align-items-center gap-1 me-auto";
                toggleIcon.className = "bi bi-check-circle-fill";
                toggleText.innerText = "Đã hoàn thành";
                if (isPast) {
                    toggleBtn.disabled = true;
                    toggleBtn.style.opacity = '1';
                    toggleBtn.style.cursor = 'default';
                    toggleBtn.title = 'Ca học đã kết thúc, giữ nguyên trạng thái hoàn thành';
                } else {
                    toggleBtn.disabled = false;
                    toggleBtn.style.opacity = '1';
                    toggleBtn.style.cursor = 'pointer';
                    toggleBtn.title = 'Nhấn để hủy hoàn thành';
                }
            } else if (curMissed) {
                toggleBtn.className = "btn btn-sm btn-outline-danger border py-1 px-2 d-flex align-items-center gap-1 me-auto";
                toggleIcon.className = "bi bi-exclamation-triangle-fill";
                toggleText.innerText = "Bỏ lỡ (Missed)";
                toggleBtn.disabled = true;
                toggleBtn.style.opacity = '0.75';
                toggleBtn.style.cursor = 'not-allowed';
                toggleBtn.title = 'Ca học đã quá hạn, không thể đánh dấu hoàn thành';
            } else {
                toggleBtn.className = "btn btn-sm btn-outline-secondary border py-1 px-2 d-flex align-items-center gap-1 me-auto";
                toggleIcon.className = "bi bi-circle";
                toggleText.innerText = "Chưa hoàn thành";
                toggleBtn.disabled = false;
                toggleBtn.style.opacity = '1';
                toggleBtn.style.cursor = 'pointer';
                toggleBtn.title = 'Nhấn để đánh dấu hoàn thành';
            }
        }
    }

    updateCompletionUI(isCompleted);

    function doToggle() {
        if (isPast) {
            if (window.showToast) window.showToast('Ca học đã kết thúc, không thể thay đổi trạng thái.', 'error');
            return;
        }

        var currentVal = desiredState !== null ? desiredState : !!eventApi.extendedProps.isCompleted;
        desiredState = !currentVal;

        // 1. Phản hồi giao diện tức thì 0ms (Optimistic UI)
        eventApi.setExtendedProp('isCompleted', desiredState);
        updateCompletionUI(desiredState);
        updateWeeklyProgress();

        if (targetEl) {
            targetEl.classList.remove('event-state-completed', 'event-state-pending', 'event-state-missed');
            targetEl.classList.add(desiredState ? 'event-state-completed' : 'event-state-pending');
        }

        // 2. Đồng bộ tuần tự với server
        syncToggleWithServer();
    }

    function syncToggleWithServer() {
        if (activeTogglePromise) return;

        var stateToSend = desiredState;
        var targetEventId = eventApi.id;
        var originalState = !stateToSend;

        activeTogglePromise = fetch('/Calendar/ToggleEventCompletion/' + targetEventId, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(stateToSend)
        })
            .then(function (res) {
                if (!res.ok) {
                    return res.json().catch(function () { return {}; }).then(function (err) {
                        throw new Error(err.error || 'Cập nhật thất bại');
                    });
                }
            })
            .catch(function (err) {
                console.error('Toggle error:', err);
                eventApi.setExtendedProp('isCompleted', originalState);
                updateCompletionUI(originalState);
                updateWeeklyProgress();
                if (targetEl) {
                    targetEl.classList.remove('event-state-completed', 'event-state-pending', 'event-state-missed');
                    targetEl.classList.add(originalState ? 'event-state-completed' : 'event-state-pending');
                }
                if (window.showToast) window.showToast(err.message || 'Lỗi khi cập nhật trạng thái', 'error');
                desiredState = null;
            })
            .finally(function () {
                activeTogglePromise = null;
                if (desiredState !== null && desiredState !== stateToSend) {
                    syncToggleWithServer();
                } else {
                    desiredState = null;
                }
            });
    }

    if (toggleBtn) {
        toggleBtn.onclick = function (e) {
            e.preventDefault();
            if (!isPast) doToggle();
        };
    }

    if (chkComplete) {
        chkComplete.onclick = function (e) {
            e.preventDefault();
            if (!isPast) doToggle();
        };
    }

    // Định vị Popover
    var calendarRect = calendarEl.getBoundingClientRect();
    var top = 80;
    var left = 40;

    if (targetEl && targetEl.getBoundingClientRect) {
        var rect = targetEl.getBoundingClientRect();
        top = rect.top - calendarRect.top + 10;
        left = rect.right - calendarRect.left + 10;

        if (left + 370 > calendarRect.width) {
            left = rect.left - calendarRect.left - 370;
        }
        if (left < 10) left = 10;
    } else {
        top = 100;
        left = Math.max(10, (calendarRect.width - 360) / 2);
    }

    popover.style.left = Math.max(10, left) + 'px';
    popover.style.top = Math.max(10, top) + 'px';
    popover.classList.remove('d-none');

    function adjustPopoverVertical() {
        var popoverHeight = popover.offsetHeight || 280;
        var calHeight = calendarEl.offsetHeight || calendarRect.height;
        var maxTop = calHeight - popoverHeight - 15;

        if (top > maxTop) {
            if (targetEl && targetEl.getBoundingClientRect) {
                var r = targetEl.getBoundingClientRect();
                var flippedTop = (r.bottom - calendarRect.top) - popoverHeight;
                top = Math.max(10, Math.min(flippedTop, maxTop));
            } else {
                top = Math.max(10, maxTop);
            }
        } else if (top < 10) {
            top = 10;
        }
        popover.style.top = top + 'px';
    }

    adjustPopoverVertical();

    // Tải danh sách Reminders
    var remRow = document.getElementById('popover-reminders-row');
    var remList = document.getElementById('popover-reminders-list');
    if (remRow && remList) {
        remRow.style.setProperty('display', 'none', 'important');
        remList.innerHTML = '';

        fetch('/Calendar/GetEventReminders/' + eventApi.id)
            .then(function (r) { return r.json(); })
            .then(function (reminders) {
                if (reminders && reminders.length > 0) {
                    remList.innerHTML = reminders.map(function (item) {
                        var m = typeof item === 'object' ? item.minutes : item;
                        var ch = typeof item === 'object' ? (item.channel || '').toLowerCase() : '';
                        var text = '';
                        if (m === 0) text = 'Vào lúc bắt đầu sự kiện';
                        else if (m % 10080 === 0) text = (m / 10080) + ' tuần trước';
                        else if (m % 1440 === 0) text = (m / 1440) + ' ngày trước';
                        else if (m % 60 === 0) text = (m / 60) + ' giờ trước';
                        else text = m + ' phút trước';

                        if (ch === 'email' || ch === 'gmail') text += ' ( gmail )';
                        return '<div>' + text + '</div>';
                    }).join('');
                    remRow.style.setProperty('display', 'flex', 'important');
                    adjustPopoverVertical();
                }
            })
            .catch(function () { });
    }

    /* [TẠM ẨN NÚT SAO CHÉP TRONG POPOVER]
    var copyBtn = document.getElementById('btn-copy-event');
    if (copyBtn) {
        copyBtn.onclick = function (e) {
            e.preventDefault();
            e.stopPropagation();
            startCopyEvent(eventApi);
        };
    }
    */

    // Nút Xóa sự kiện
    var delBtn = document.getElementById('btn-delete-event');
    if (delBtn) {
        delBtn.onclick = async function () {
            var confirmed = window.showConfirm ? await window.showConfirm('Bạn có chắc chắn muốn xóa sự kiện này?') : confirm('Bạn có chắc chắn muốn xóa sự kiện này?');
            if (confirmed) {
                fetch('/Calendar/DeleteEvent/' + eventApi.id, { method: 'DELETE' })
                    .then(function (res) {
                        if (res.ok) {
                            popover.classList.add('d-none');
                            calendar.refetchEvents();
                            if (window.showToast) window.showToast('Đã xóa sự kiện thành công!', 'success');
                        } else {
                            if (window.showToast) window.showToast('Lỗi khi xóa sự kiện!', 'error');
                        }
                    })
                    .catch(function () {
                        if (window.showToast) window.showToast('Lỗi mạng khi xóa sự kiện!', 'error');
                    });
            }
        };
    }
}

// ========================================================
// 4. Thanh tìm kiếm Live Search trên toolbar
// ========================================================
function initSearchBar() {
    var searchInput = document.getElementById('calendar-search-input');
    var searchClear = document.getElementById('calendar-search-clear');
    var searchDropdown = document.getElementById('calendar-search-dropdown');
    var resultsList = document.getElementById('calendar-search-results-list');
    var resultsCount = document.getElementById('search-results-count');

    if (!searchInput) return;

    var debounceTimer = null;

    function performSearch(query) {
        var q = query.trim();
        currentSearchQuery = q;
        calendar.render();

        if (!q) {
            if (searchClear) searchClear.classList.add('d-none');
            if (searchDropdown) searchDropdown.classList.add('d-none');
            return;
        }

        if (searchClear) searchClear.classList.remove('d-none');
        if (searchDropdown) searchDropdown.classList.remove('d-none');

        if (resultsList) {
            resultsList.innerHTML = '\
                    <div class="p-3 text-center text-muted small">\
                        <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>\
                        <span>Đang tìm kiếm...</span>\
                    </div>';
        }

        if (document.body.dataset.guest === 'true') {
            renderSearchResults([], q);
            return;
        }

        fetch('/Calendar/SearchEvents?q=' + encodeURIComponent(q))
            .then(function (r) { return r.json(); })
            .then(function (data) { renderSearchResults(data, q); })
            .catch(function (err) {
                console.error('Search error:', err);
                if (resultsList) resultsList.innerHTML = '<div class="p-3 text-center text-danger small">Lỗi khi tìm kiếm sự kiện</div>';
            });
    }

    function renderSearchResults(items, query) {
        if (!resultsList || !searchDropdown) return;

        if (!items || items.length === 0) {
            if (resultsCount) resultsCount.innerText = 'Không có kết quả';
            resultsList.innerHTML = '\
                    <div class="p-4 text-center text-muted">\
                        <i class="bi bi-search fs-3 d-block mb-2 text-secondary opacity-50"></i>\
                        <span class="small">Không tìm thấy sự kiện nào khớp với "<strong>' + escapeHtml(query) + '</strong>"</span>\
                    </div>';
            return;
        }

        if (resultsCount) resultsCount.innerText = 'Tìm thấy ' + items.length + ' sự kiện';

        var typeConfig = {
            'Kiểm tra / Thi': { name: 'Kiểm tra', color: '#dc3545', icon: 'bi-alarm' },
            'Học nhóm': { name: 'Học nhóm', color: '#6f42c1', icon: 'bi-people' },
            'Bài tập / Nộp bài': { name: 'Bài tập', color: '#fd7e14', icon: 'bi-journal-check' },
            'Tự học': { name: 'Tự học', color: '#0d6efd', icon: 'bi-book' }
        };

        var html = items.map(function (item) {
            var isCompleted = item.isCompleted;
            var isPast = item.isPast;
            var dotColor = isCompleted ? '#1e8e3e' : (isPast ? '#d93025' : '#1a73e8');

            var badgeClass = isCompleted
                ? 'bg-success-subtle text-success border border-success-subtle'
                : (isPast ? 'bg-danger-subtle text-danger border border-danger-subtle' : 'bg-primary-subtle text-primary border border-primary-subtle');

            var cat = typeConfig[item.eventType] || { name: item.eventType || 'Tự học', color: '#6c757d', icon: 'bi-bookmark' };
            var locHtml = item.location ? '\
                    <div class="text-truncate text-muted mt-1 calendar-search-loc">\
                        <i class="bi bi-geo-alt me-1 text-danger"></i>' + escapeHtml(item.location) + '\
                    </div>' : '';

            return '\
                    <div class="search-result-item d-flex align-items-start gap-2" data-event-id="' + item.id + '" data-start="' + (item.start || '') + '">\
                        <div class="result-dot" style="background-color: ' + dotColor + ';"></div>\
                        <div class="flex-grow-1 min-w-0">\
                            <div class="d-flex align-items-center justify-content-between gap-2">\
                                <span class="fw-semibold text-truncate text-dark calendar-search-item-title">' + escapeHtml(item.title) + '</span>\
                                <span class="badge ' + badgeClass + ' calendar-search-item-badge">' + (item.statusText || '') + '</span>\
                            </div>\
                            <div class="d-flex flex-wrap align-items-center gap-2 text-muted mt-1 calendar-search-item-meta">\
                                <span><i class="bi bi-calendar-event me-1"></i>' + (item.dateFormatted || '') + '</span>\
                                <span><i class="bi bi-clock me-1"></i>' + (item.startTimeStr || '') + ' – ' + (item.endTimeStr || '') + '</span>\
                                <span class="badge bg-light text-secondary border px-1.5 py-0.5 calendar-search-type-badge">\
                                    <i class="bi ' + cat.icon + ' me-1" style="color: ' + cat.color + ';"></i>' + cat.name + '\
                                </span>\
                            </div>' + locHtml + '\
                        </div>\
                    </div>';
        }).join('');

        resultsList.innerHTML = html;

        resultsList.querySelectorAll('.search-result-item').forEach(function (el) {
            el.addEventListener('click', function () {
                var eventId = el.getAttribute('data-event-id');
                var startIso = el.getAttribute('data-start');
                searchDropdown.classList.add('d-none');

                if (startIso) {
                    var targetDate = new Date(startIso);
                    calendar.gotoDate(targetDate);
                    var hours = targetDate.getHours();
                    var scrollHour = Math.max(0, hours - 1);
                    var scrollStr = (scrollHour < 10 ? '0' + scrollHour : scrollHour) + ':00:00';
                    calendar.scrollToTime(scrollStr);
                }

                setTimeout(function () {
                    var targetEvent = calendar.getEventById(eventId);
                    if (targetEvent) {
                        var allEls = calendarEl.querySelectorAll('.fc-event');
                        var targetDom = null;
                        for (var i = 0; i < allEls.length; i++) {
                            if (allEls[i].innerText.includes(targetEvent.title)) {
                                targetDom = allEls[i];
                                break;
                            }
                        }
                        if (targetDom) {
                            targetDom.classList.add('event-pulse');
                            setTimeout(function () { targetDom.classList.remove('event-pulse'); }, 3000);
                        }
                        showEventPopover(targetEvent, targetDom);
                    }
                }, 200);
            });
        });
    }

    searchInput.addEventListener('input', function (e) {
        clearTimeout(debounceTimer);
        var val = e.target.value;
        debounceTimer = setTimeout(function () { performSearch(val); }, 250);
    });

    searchInput.addEventListener('focus', function () {
        if (this.value.trim().length > 0 && resultsList && resultsList.children.length > 0) {
            searchDropdown.classList.remove('d-none');
        }
    });

    if (searchClear) {
        searchClear.addEventListener('click', function () {
            searchInput.value = '';
            searchInput.focus();
            performSearch('');
        });
    }

    searchInput.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            if (searchDropdown) searchDropdown.classList.add('d-none');
            searchInput.blur();
        }
    });

    document.addEventListener('click', function (e) {
        if (searchDropdown && !searchDropdown.contains(e.target) && !searchInput.contains(e.target)) {
            searchDropdown.classList.add('d-none');
        }
    });
}

// ========================================================
// 5. Bộ nút lọc nhanh (Filter Chips)
// ========================================================
function initFilterChips() {
    var container = document.getElementById('calendar-filter-chips');
    if (!container) return;

    fetch('/Calendar/GetCategories')
        .then(function (r) { return r.json(); })
        .then(function (categories) {
            if (!categories || !categories.length) return;

            categories.forEach(function (cat) {
                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'btn-filter-chip';
                btn.dataset.type = cat.name;
                btn.innerText = cat.name;
                container.appendChild(btn);
            });

            container.querySelectorAll('.btn-filter-chip').forEach(function (chip) {
                chip.addEventListener('click', function () {
                    container.querySelectorAll('.btn-filter-chip').forEach(function (c) {
                        c.classList.remove('is-active');
                    });
                    this.classList.add('is-active');

                    currentCategoryFilter = this.dataset.type || 'all';
                    calendar.refetchEvents();
                    updateWeeklyProgress();
                });
            });
        })
        .catch(function (err) {
            console.error('Failed to load filter categories:', err);
        });
}

// ========================================================
// 6. Sao chép và Dán sự kiện (Copy & Paste)
// ========================================================
function normalizeEventType(type) {
    var t = type ? type.toString().trim() : '';
    return t || 'Tự học';
}

function startCopyEvent(eventApi) {
    if (!eventApi) return;

    var start = eventApi.start;
    var end = eventApi.end || (start ? new Date(start.getTime() + 3600000) : new Date());
    var durationMs = end.getTime() - start.getTime();
    if (durationMs <= 0 || isNaN(durationMs)) durationMs = 3600000;

    var rawType = eventApi.extendedProps?.type || 'Tự học';

    activeCopiedEvent = {
        title: eventApi.title || 'Sự kiện sao chép',
        durationMs: durationMs,
        eventType: normalizeEventType(rawType),
        location: eventApi.extendedProps?.location || '',
        description: eventApi.extendedProps?.description || '',
        reminders: []
    };

    // Đóng popover nếu đang mở
    var popover = document.getElementById('event-popover');
    if (popover) popover.classList.add('d-none');

    // Tải danh sách reminders của sự kiện gốc
    fetch('/Calendar/GetEventReminders/' + eventApi.id)
        .then(function (r) { return r.json(); })
        .then(function (rems) {
            if (rems && rems.length) {
                activeCopiedEvent.reminders = rems.map(function (r) {
                    return {
                        minutes: typeof r === 'object' ? r.minutes : r,
                        channel: typeof r === 'object' ? (r.channel || 'InApp') : 'InApp'
                    };
                });
            }
        })
        .catch(function () { });

    enablePasteMode();
}

function enablePasteMode() {
    if (!activeCopiedEvent) return;

    document.body.classList.add('calendar-copying-active');

    var pasteBar = document.getElementById('calendar-paste-bar');
    var barTitle = document.getElementById('paste-bar-event-title');
    if (barTitle) barTitle.innerText = activeCopiedEvent.title;
    if (pasteBar) pasteBar.classList.remove('d-none');

    var follower = document.getElementById('calendar-cursor-follower');
    var followerTitle = document.getElementById('follower-event-title');
    if (followerTitle) followerTitle.innerText = activeCopiedEvent.title;
    if (follower) follower.style.display = 'flex';

    if (window.showToast) {
        window.showToast('Đã sao chép "' + activeCopiedEvent.title + '". Nhấp ô thời gian trên lịch để Dán (Esc để hủy)', 'info');
    }
}

function disablePasteMode() {
    activeCopiedEvent = null;
    document.body.classList.remove('calendar-copying-active');

    var pasteBar = document.getElementById('calendar-paste-bar');
    if (pasteBar) pasteBar.classList.add('d-none');

    var follower = document.getElementById('calendar-cursor-follower');
    if (follower) follower.style.display = 'none';
}

function handleCalendarDateClick(info) {
    if (!activeCopiedEvent) return;

    var clickedTime = info.date;
    var now = new Date();

    // Kiểm tra không được dán vào quá khứ (cho phép sai số 60s đối với slot hiện tại)
    if (clickedTime < new Date(now.getTime() - 60000)) {
        if (window.showToast) {
            window.showToast('Không thể dán sự kiện vào thời gian trong quá khứ!', 'error');
        }
        return;
    }

    var durationMs = activeCopiedEvent.durationMs || 3600000;
    var newStart = clickedTime;
    var newEnd = new Date(newStart.getTime() + durationMs);

    var pad = function (n) { return n < 10 ? '0' + n : n; };
    var dateStr = newStart.getFullYear() + '-' + pad(newStart.getMonth() + 1) + '-' + pad(newStart.getDate());
    var startStr = pad(newStart.getHours()) + ':' + pad(newStart.getMinutes()) + ':00';
    var endStr = pad(newEnd.getHours()) + ':' + pad(newEnd.getMinutes()) + ':00';

    // Nếu thời gian kết thúc sang ngày hôm sau, giới hạn ở cuối ngày 23:59:59
    if (newEnd.getDate() !== newStart.getDate()) {
        endStr = '23:59:59';
    }

    var payload = {
        title: activeCopiedEvent.title,
        eventType: activeCopiedEvent.eventType,
        date: dateStr,
        startTime: startStr,
        endTime: endStr,
        location: activeCopiedEvent.location || '',
        description: activeCopiedEvent.description || '',
        reminders: activeCopiedEvent.reminders || []
    };

    savePastedEvent(payload);
}

function savePastedEvent(payload) {
    fetch('/Calendar/CreateEvent', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    })
        .then(function (res) {
            if (res.ok) {
                return res.json();
            } else {
                return res.json().then(function (err) {
                    var errorMsg = (err && (err.inner || err.error)) || 'Lỗi khi dán sự kiện';
                    console.error('Lỗi server khi dán sự kiện:', err);
                    throw new Error(errorMsg);
                });
            }
        })
        .then(function (createdEvent) {
            if (window.showToast) {
                window.showToast('Đã dán sự kiện "' + payload.title + '" thành công! Nhấp vào sự kiện để chỉnh sửa nếu cần.', 'success');
            }
            disablePasteMode();
            calendar.refetchEvents();
        })
        .catch(function (err) {
            console.error('Lỗi khi dán sự kiện:', err);
            if (window.showToast) {
                window.showToast(err.message || 'Lỗi mạng khi dán sự kiện', 'error');
            }
        });
}

function initCopyPasteHandlers() {
    // Di chuyển cursor follower theo chuột
    document.addEventListener('mousemove', function (e) {
        if (!activeCopiedEvent) return;
        var follower = document.getElementById('calendar-cursor-follower');
        if (follower && follower.style.display !== 'none') {
            follower.style.left = e.clientX + 'px';
            follower.style.top = e.clientY + 'px';
        }
    });

    // Phím Escape để hủy dán
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && activeCopiedEvent) {
            disablePasteMode();
            if (window.showToast) window.showToast('Đã hủy sao chép', 'info');
        }
    });

    // Nút Hủy trên banner
    var btnCancel = document.getElementById('btn-cancel-paste');
    if (btnCancel) {
        btnCancel.addEventListener('click', function () {
            disablePasteMode();
            if (window.showToast) window.showToast('Đã hủy sao chép', 'info');
        });
    }
}

// ========================================================
// 7. Khởi chạy và các sự kiện chung
// ========================================================
initSearchBar();
initFilterChips();
// initCopyPasteHandlers();

function escapeHtml(text) {
    if (!text) return '';
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Đóng popover khi nhấp ra ngoài
document.addEventListener('click', function (e) {
    var popover = document.getElementById('event-popover');
    if (!popover || popover.classList.contains('d-none')) return;
    if (!popover.contains(e.target) && !e.target.closest('.fc-event') && !e.target.closest('.search-result-item')) {
        popover.classList.add('d-none');
    }
});

// Thêm nhãn GMT+00 vào góc trên bên trái
setTimeout(function () {
    var axisEl = document.querySelector('.fc-timegrid-axis-cushion');
    if (axisEl) axisEl.innerHTML = 'GMT+00';
}, 50);
});
