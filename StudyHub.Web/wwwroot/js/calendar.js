document.addEventListener('DOMContentLoaded', function() {
    var calendarEl = document.getElementById('calendar');
    if (!calendarEl) return;

    var currentSearchQuery = '';

    var calendar = new FullCalendar.Calendar(calendarEl, {
        locale: 'en',
        initialView: 'timeGridWeek',
        firstDay: 0, // 0 = Sunday, 1 = Monday
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
        dayHeaderContent: function(arg) {
            var dayNames = ['CN', 'THỨ 2', 'THỨ 3', 'THỨ 4', 'THỨ 5', 'THỨ 6', 'THỨ 7'];
            var dayName = dayNames[arg.date.getDay()];
            var dateNum = arg.date.getDate();
            
            var html = '<div>' + dayName + '</div>' + 
                       '<div class="sh-date-num">' + dateNum + '</div>';
            return { html: html };
        },
        events: document.body.dataset.guest === 'true' ? [] : '/Calendar/GetEvents',
        eventClassNames: function(arg) {
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

            return classes;
        },
        eventClick: function(info) {
            showEventPopover(info.event, info.el);
        },
   
        eventsSet: function(events) {
            updateWeeklyProgress(events);
        }
    });

    calendar.render();

    // ========================================================
    // Dynamic Weekly Study Progress & Stats Calculation
    // ========================================================
    function updateWeeklyProgress(events) {
        if (!events) events = calendar.getEvents();
        var total = events.length;
        var completed = 0;
        var missed = 0;
        var pending = 0;
        var now = new Date();

        events.forEach(function(e) {
            var isComp = !!e.extendedProps?.isCompleted;
            var end = e.end || e.start;
            if (isComp) {
                completed++;
            } else if (end && new Date(end) < now) {
                missed++;
            } else {
                pending++;
            }
        });

        var percent = total > 0 ? Math.round((completed / total) * 100) : 0;

        var barEl = document.getElementById('calendar-progress-bar');
        var percentEl = document.getElementById('calendar-progress-percent');
        var completedEl = document.getElementById('stat-completed-badge');
        var pendingEl = document.getElementById('stat-pending-badge');
        var missedEl = document.getElementById('stat-missed-badge');
        if (barEl) {
            barEl.style.width = percent + '%';
        }

        if (percentEl) {
            percentEl.innerText = percent + '% (' + completed + '/' + total + ' ca)';
        }

        if (completedEl) {
            completedEl.innerHTML = '<i class="bi bi-check2 me-1 fs-6"></i><span>' + completed + ' hoàn thành</span>';
        }
        if (pendingEl) {
            pendingEl.innerHTML = '<i class="bi bi-clock me-1"></i><span>' + pending + ' sắp tới</span>';
        }
        if (missedEl) {
            missedEl.innerHTML = '<i class="bi bi-exclamation-triangle me-1"></i><span>' + missed + ' quá hạn</span>';
        }
    }


    // ========================================================
    // Reusable Event Popover Function
    // ========================================================
    function showEventPopover(eventApi, targetEl) {
        var popover = document.getElementById('event-popover');
        if (!popover) return;

        // Populate data
        var titleEl = document.getElementById('popover-title');
        if (titleEl) titleEl.innerText = eventApi.title;

        var start = eventApi.start;
        var end = eventApi.end;
        var timeStr = start ? start.toLocaleDateString('vi-VN', { weekday: 'long', day: 'numeric', month: 'long' }) + ' · ' : '';

        var formatTime = function(date) {
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

        // Event properties
        var props = eventApi.extendedProps || {};
        var isCompleted = !!props.isCompleted;
        var type = props.type || 'SelfStudy';
        var endTime = eventApi.end || eventApi.start;
        var colorBox = document.getElementById('popover-color');

        // Event type label
        var typeLabels = { 'Exam': 'Kiểm tra / Thi', 'Class': 'Học nhóm', 'Other': 'Bài tập / Nộp bài', 'SelfStudy': 'Tự học' };
        var typeEl = document.getElementById('popover-type');
        if (typeEl) typeEl.innerText = typeLabels[type] || 'Tự học';

        // Description (show/hide)
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

        // Location (show/hide)
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

        // Completion UI elements
        var toggleBtn = document.getElementById('btn-toggle-complete');
        var toggleIcon = document.getElementById('popover-complete-icon');
        var toggleText = document.getElementById('popover-complete-text');
        var chkComplete = document.getElementById('popover-is-completed');
        var chkRow = document.getElementById('popover-completion-row') || (chkComplete ? chkComplete.closest('.px-3') : null);

        var isPast = endTime && (new Date(endTime) < new Date());

        // Edit button control: Quá thời hạn hoặc đã complete/miss -> CHỈ ĐƯỢC XÓA, KHÔNG EDIT
        var editBtn = document.getElementById('btn-edit-event');
        function updateEditButton(completed) {
            if (editBtn) {
                if (isPast || completed) {
                    editBtn.style.setProperty('display', 'none', 'important');
                    editBtn.onclick = null;
                } else {
                    editBtn.style.removeProperty('display');
                    editBtn.onclick = function() {
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

            // Đồng bộ trạng thái nút Edit
            updateEditButton(completed);

            // Nếu quá hạn: ẩn đi ô tick hoàn thành ở dưới
            if (chkRow) {
                if (isPast) {
                    chkRow.style.setProperty('display', 'none', 'important');
                } else {
                    chkRow.style.removeProperty('display');
                }
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

        var activeTogglePromise = null;
        var desiredState = null;

        function doToggle() {
            if (isPast) {
                alert('Ca học đã kết thúc, không thể thay đổi trạng thái.');
                return;
            }

            var currentVal = desiredState !== null ? desiredState : !!eventApi.extendedProps.isCompleted;
            desiredState = !currentVal;

            // 1. Phản hồi giao diện tức thì 0ms (Optimistic UI - triệt tiêu delay)
            eventApi.setExtendedProp('isCompleted', desiredState);
            updateCompletionUI(desiredState);
            updateWeeklyProgress();

            if (targetEl) {
                targetEl.classList.remove('event-state-completed', 'event-state-pending', 'event-state-missed');
                targetEl.classList.add(desiredState ? 'event-state-completed' : 'event-state-pending');
            }

            // 2. Đồng bộ an toàn với server, gửi tuần tự tránh nghẽn/lỗi DbContext khi click dồn dập
            syncToggleWithServer();
        }

        function syncToggleWithServer() {
            if (activeTogglePromise) {
                // Đang có request gửi dở, khi xong sẽ tự động kiểm tra desiredState và gửi tiếp
                return;
            }

            var stateToSend = desiredState;
            var targetEventId = eventApi.id;
            var originalState = !stateToSend;

            activeTogglePromise = fetch('/Calendar/ToggleEventCompletion/' + targetEventId, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(stateToSend)
            })
            .then(function(res) {
                if (!res.ok) {
                    return res.json().catch(function() { return {}; }).then(function(err) {
                        throw new Error(err.error || 'Cập nhật thất bại');
                    });
                }
            })
            .catch(function(err) {
                console.error('Toggle error:', err);
                // Khôi phục lại trạng thái cũ nếu lỗi mạng / lỗi server
                eventApi.setExtendedProp('isCompleted', originalState);
                updateCompletionUI(originalState);
                updateWeeklyProgress();
                if (targetEl) {
                    targetEl.classList.remove('event-state-completed', 'event-state-pending', 'event-state-missed');
                    targetEl.classList.add(originalState ? 'event-state-completed' : 'event-state-pending');
                }
                alert(err.message || 'Lỗi khi cập nhật trạng thái');
                desiredState = null;
            })
            .finally(function() {
                activeTogglePromise = null;
                // Nếu người dùng click thêm lần nữa trong lúc request đang gửi, tiếp tục gửi trạng thái mới nhất
                if (desiredState !== null && desiredState !== stateToSend) {
                    syncToggleWithServer();
                } else {
                    desiredState = null;
                }
            });
        }

        if (toggleBtn) {
            if (isPast) {
                toggleBtn.onclick = function(e) { e.preventDefault(); };
            } else {
                toggleBtn.onclick = function(e) {
                    e.preventDefault();
                    doToggle();
                };
            }
        }

        if (chkComplete) {
            if (isPast) {
                chkComplete.disabled = true;
                chkComplete.onclick = function(e) { e.preventDefault(); };
            } else {
                chkComplete.disabled = false;
                chkComplete.onclick = function(e) {
                    e.preventDefault();
                    doToggle();
                };
            }
        }

        // Position popover
        var calendarRect = calendarEl.getBoundingClientRect();
        var top = 80;
        var left = 40;

        if (targetEl && targetEl.getBoundingClientRect) {
            var rect = targetEl.getBoundingClientRect();
            top = rect.top - calendarRect.top + 20;
            left = rect.right - calendarRect.left + 10;

            if (left + 360 > calendarRect.width) {
                left = rect.left - calendarRect.left - 370;
            }
            if (left < 0) left = 10;
        } else {
            top = 100;
            left = Math.max(10, (calendarRect.width - 360) / 2);
        }

        popover.style.top = Math.max(10, top) + 'px';
        popover.style.left = left + 'px';
        popover.classList.remove('d-none');

        // Fetch Reminders
        var remRow = document.getElementById('popover-reminders-row');
        var remList = document.getElementById('popover-reminders-list');
        if (remRow && remList) {
            remRow.style.setProperty('display', 'none', 'important');
            remList.innerHTML = '';

            fetch('/Calendar/GetEventReminders/' + eventApi.id)
                .then(r => r.json())
                .then(reminders => {
                    if (reminders && reminders.length > 0) {
                        remList.innerHTML = reminders.map(item => {
                            var m = typeof item === 'object' ? item.minutes : item;
                            var ch = typeof item === 'object' ? (item.channel || '').toLowerCase() : '';
                            var text = '';
                            if (m === 0) text = 'Vào lúc bắt đầu sự kiện';
                            else if (m % 10080 === 0) text = (m / 10080) + ' tuần trước';
                            else if (m % 1440 === 0) text = (m / 1440) + ' ngày trước';
                            else if (m % 60 === 0) text = (m / 60) + ' giờ trước';
                            else text = m + ' phút trước';

                            if (ch === 'email' || ch === 'gmail') {
                                text += ' ( gmail )';
                            }
                            return '<div>' + text + '</div>';
                        }).join('');
                        remRow.style.setProperty('display', 'flex', 'important');
                    }
                })
                .catch(() => {});
        }

        // Delete URL
        var delBtn = document.getElementById('btn-delete-event');
        if (delBtn) {
            delBtn.onclick = function() {
                if (confirm('Bạn có chắc chắn muốn xóa sự kiện này?')) {
                    fetch('/Calendar/DeleteEvent/' + eventApi.id, { method: 'DELETE' })
                        .then(res => {
                            if (res.ok) {
                                popover.classList.add('d-none');
                                calendar.refetchEvents();
                            }
                            else alert('Lỗi khi xóa!');
                        });
                }
            };
        }
    }

    // ========================================================
    // Mount & Initialize Search Bar into Toolbar Center
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

            // Trigger calendar to re-apply filter classes (event-search-match / event-search-dimmed)
            calendar.render();

            if (!q) {
                if (searchClear) searchClear.classList.add('d-none');
                if (searchDropdown) searchDropdown.classList.add('d-none');
                return;
            }

            if (searchClear) searchClear.classList.remove('d-none');
            if (searchDropdown) searchDropdown.classList.remove('d-none');

            // Show loading state
            if (resultsList) {
                resultsList.innerHTML = `
                    <div class="p-3 text-center text-muted small">
                        <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                        <span>Đang tìm kiếm...</span>
                    </div>`;
            }

            if (document.body.dataset.guest === 'true') {
                renderSearchResults([], q);
                return;
            }
            fetch('/Calendar/SearchEvents?q=' + encodeURIComponent(q))
                .then(r => r.json())
                .then(data => {
                    renderSearchResults(data, q);
                })
                .catch(err => {
                    console.error('Search error:', err);
                    if (resultsList) {
                        resultsList.innerHTML = `<div class="p-3 text-center text-danger small">Lỗi khi tìm kiếm sự kiện</div>`;
                    }
                });
        }

        function renderSearchResults(items, query) {
            if (!resultsList || !searchDropdown) return;

            if (!items || items.length === 0) {
                if (resultsCount) resultsCount.innerText = 'Không có kết quả';
                resultsList.innerHTML = `
                    <div class="p-4 text-center text-muted">
                        <i class="bi bi-search fs-3 d-block mb-2 text-secondary opacity-50"></i>
                        <span class="small">Không tìm thấy sự kiện nào khớp với "<strong>${escapeHtml(query)}</strong>"</span>
                    </div>`;
                return;
            }

            if (resultsCount) resultsCount.innerText = `Tìm thấy ${items.length} sự kiện`;

            var typeConfig = {
                'Exam':      { name: 'Kiểm tra', color: '#dc3545', icon: 'bi-alarm' },
                'Class':     { name: 'Học nhóm', color: '#6f42c1', icon: 'bi-people' },
                'Other':     { name: 'Bài tập',  color: '#fd7e14', icon: 'bi-journal-check' },
                'SelfStudy': { name: 'Tự học',   color: '#0d6efd', icon: 'bi-book' }
            };

            var html = items.map(function(item) {
                var isCompleted = item.isCompleted;
                var isPast = item.isPast;
                var dotColor = isCompleted ? '#1e8e3e' : (isPast ? '#d93025' : '#1a73e8');

                var badgeClass = isCompleted 
                    ? 'bg-success-subtle text-success border border-success-subtle'
                    : (isPast ? 'bg-danger-subtle text-danger border border-danger-subtle' : 'bg-primary-subtle text-primary border border-primary-subtle');

                var cat = typeConfig[item.eventType] || typeConfig['SelfStudy'];

                return `
                    <div class="search-result-item d-flex align-items-start gap-2" data-event-id="${item.id}" data-start="${item.start}">
                        <div class="result-dot" style="background-color: ${dotColor};"></div>
                        <div class="flex-grow-1 min-w-0">
                            <div class="d-flex align-items-center justify-content-between gap-2">
                                <span class="fw-semibold text-truncate text-dark" style="font-size: 13.5px;">${escapeHtml(item.title)}</span>
                                <span class="badge ${badgeClass}" style="font-size: 10px; font-weight: 500;">${item.statusText}</span>
                            </div>
                            <div class="d-flex flex-wrap align-items-center gap-2 text-muted mt-1" style="font-size: 11.5px;">
                                <span><i class="bi bi-calendar-event me-1"></i>${item.dateFormatted}</span>
                                <span><i class="bi bi-clock me-1"></i>${item.startTimeStr} – ${item.endTimeStr}</span>
                                <span class="badge bg-light text-secondary border px-1.5 py-0.5" style="font-size: 10px;">
                                    <i class="bi ${cat.icon} me-1" style="color: ${cat.color};"></i>${cat.name}
                                </span>
                            </div>
                            ${item.location ? `
                                <div class="text-truncate text-muted mt-1" style="font-size: 11.5px;">
                                    <i class="bi bi-geo-alt me-1 text-danger"></i>${escapeHtml(item.location)}
                                </div>
                            ` : ''}
                        </div>
                    </div>
                `;
            }).join('');

            resultsList.innerHTML = html;

            // Bind click handlers to each result item
            resultsList.querySelectorAll('.search-result-item').forEach(function(el) {
                el.addEventListener('click', function() {
                    var eventId = el.getAttribute('data-event-id');
                    var startIso = el.getAttribute('data-start');
                    
                    // Close dropdown
                    searchDropdown.classList.add('d-none');

                    if (startIso) {
                        var targetDate = new Date(startIso);
                        calendar.gotoDate(targetDate);
                        
                        // Scroll to event hour
                        var hours = targetDate.getHours();
                        var scrollHour = Math.max(0, hours - 1);
                        var scrollStr = (scrollHour < 10 ? '0' + scrollHour : scrollHour) + ':00:00';
                        calendar.scrollToTime(scrollStr);
                    }

                    // After view updates, locate the event and show popover
                    setTimeout(function() {
                        var targetEvent = calendar.getEventById(eventId);
                        if (targetEvent) {
                            // Find DOM element
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
                                setTimeout(function() {
                                    targetDom.classList.remove('event-pulse');
                                }, 3000);
                            }

                            showEventPopover(targetEvent, targetDom);
                        }
                    }, 200);
                });
            });
        }

        // Input listener with debounce
        searchInput.addEventListener('input', function(e) {
            clearTimeout(debounceTimer);
            var val = e.target.value;
            debounceTimer = setTimeout(function() {
                performSearch(val);
            }, 250);
        });

        // Focus listener: re-show dropdown if there is text
        searchInput.addEventListener('focus', function() {
            if (this.value.trim().length > 0 && resultsList && resultsList.children.length > 0) {
                searchDropdown.classList.remove('d-none');
            }
        });

        // Clear button
        if (searchClear) {
            searchClear.addEventListener('click', function() {
                searchInput.value = '';
                searchInput.focus();
                performSearch('');
            });
        }

        // Escape key to close
        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                if (searchDropdown) searchDropdown.classList.add('d-none');
                searchInput.blur();
            }
        });

        // Close dropdown when clicking outside
        document.addEventListener('click', function(e) {
            if (searchDropdown && !searchDropdown.contains(e.target) && !searchInput.contains(e.target)) {
                searchDropdown.classList.add('d-none');
            }
        });
    }

    initSearchBar();

    function escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Close popover when clicking outside
    document.addEventListener('click', function(e) {
        var popover = document.getElementById('event-popover');
        if (!popover || popover.classList.contains('d-none')) return;
        if (!popover.contains(e.target) && !e.target.closest('.fc-event') && !e.target.closest('.search-result-item')) {
            popover.classList.add('d-none');
        }
    });

    // Add GMT+00 to top left corner
    setTimeout(() => {
        var axisEl = document.querySelector('.fc-timegrid-axis-cushion');
        if (axisEl) {
            axisEl.innerHTML = 'GMT+00';
        }
    }, 50);
});

