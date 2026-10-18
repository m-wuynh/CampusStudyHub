document.addEventListener('DOMContentLoaded', function() {
    var calendarEl = document.getElementById('calendar');
    if (!calendarEl) return;

    var calendar = new FullCalendar.Calendar(calendarEl, {
        locale: 'en',
        initialView: 'timeGridWeek',
        firstDay: 0, // 0 = Sunday, 1 = Monday
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
        events: '/Calendar/GetEvents',
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
            return classes;
        },
        eventClick: function(info) {
            var popover = document.getElementById('event-popover');
            if (!popover) return;
            
            // Populate data
            var titleEl = document.getElementById('popover-title');
            if (titleEl) titleEl.innerText = info.event.title;
            
            var start = info.event.start;
            var end = info.event.end;
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
            var props = info.event.extendedProps || {};
            var isCompleted = !!props.isCompleted;
            var type = props.type || 'SelfStudy';
            var endTime = info.event.end || info.event.start;
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

            function updateCompletionUI(completed) {
                var curMissed = !completed && endTime && (new Date(endTime) < new Date());
                var curColor = completed ? '#1e8e3e' : (curMissed ? '#d93025' : '#1a73e8');
                if (colorBox) colorBox.style.backgroundColor = curColor;
                if (titleEl) {
                    titleEl.style.textDecoration = completed ? 'line-through' : 'none';
                    titleEl.style.color = completed ? '#70757a' : '#3c4043';
                }

                if (chkComplete) {
                    chkComplete.checked = completed;
                    if (curMissed) {
                        chkComplete.disabled = true;
                        chkComplete.parentElement.style.opacity = '0.5';
                        chkComplete.parentElement.title = 'Đã quá hạn, không thể đánh dấu hoàn thành';
                    } else {
                        chkComplete.disabled = false;
                        chkComplete.parentElement.style.opacity = '1';
                        chkComplete.parentElement.title = '';
                    }
                }

                if (toggleBtn && toggleIcon && toggleText) {
                    if (completed) {
                        toggleBtn.className = "btn btn-sm btn-success text-white border-0 py-1 px-2 d-flex align-items-center gap-1 me-auto";
                        toggleIcon.className = "bi bi-check-circle-fill";
                        toggleText.innerText = "Đã hoàn thành";
                        toggleBtn.disabled = false;
                        toggleBtn.style.opacity = '1';
                        toggleBtn.style.cursor = 'pointer';
                        toggleBtn.title = 'Nhấn để hủy hoàn thành';
                    } else if (curMissed) {
                        toggleBtn.className = "btn btn-sm btn-outline-danger border py-1 px-2 d-flex align-items-center gap-1 me-auto";
                        toggleIcon.className = "bi bi-exclamation-triangle-fill";
                        toggleText.innerText = "Bỏ lỡ (Missed)";
                        toggleBtn.disabled = true;
                        toggleBtn.style.opacity = '0.65';
                        toggleBtn.style.cursor = 'not-allowed';
                        toggleBtn.title = 'Đã quá hạn, không thể đánh dấu hoàn thành';
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
                var curMissed = !info.event.extendedProps.isCompleted && endTime && (new Date(endTime) < new Date());
                if (curMissed) {
                    alert('Ca học đã quá hạn và được tính là Bỏ lỡ (Missed), không thể cập nhật.');
                    return;
                }
                fetch('/Calendar/ToggleCompletion/' + info.event.id, { method: 'POST' })
                    .then(r => r.json())
                    .then(data => {
                        if (data.ok) {
                            info.event.setExtendedProp('isCompleted', data.isCompleted);
                            updateCompletionUI(data.isCompleted);
                            calendar.refetchEvents();
                        } else {
                            alert("Lỗi: " + (data.error || "Không thể cập nhật"));
                        }
                    })
                    .catch(() => alert("Lỗi kết nối máy chủ"));
            }

            if (toggleBtn) {
                toggleBtn.onclick = function(e) {
                    e.preventDefault();
                    doToggle();
                };
            }

            if (chkComplete) {
                var newChk = chkComplete.cloneNode(true);
                chkComplete.parentNode.replaceChild(newChk, chkComplete);
                chkComplete = newChk;
                chkComplete.onclick = function(e) {
                    e.preventDefault();
                    doToggle();
                };
            }

            // Position popover
            var rect = info.el.getBoundingClientRect();
            var calendarRect = calendarEl.getBoundingClientRect();
            
            var top = rect.top - calendarRect.top + 20;
            var left = rect.right - calendarRect.left + 10;
            
            if (left + 360 > calendarRect.width) {
                left = rect.left - calendarRect.left - 370;
            }
            if (left < 0) left = 10;
            
            popover.style.top = Math.max(10, top) + 'px';
            popover.style.left = left + 'px';
            popover.classList.remove('d-none');
            
            // Fetch Reminders
            var remRow = document.getElementById('popover-reminders-row');
            var remList = document.getElementById('popover-reminders-list');
            if (remRow && remList) {
                remRow.style.setProperty('display', 'none', 'important');
                remList.innerHTML = '';
                
                fetch('/Calendar/GetEventReminders/' + info.event.id)
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

            // Set Edit/Delete URLs
            var editBtn = document.getElementById('btn-edit-event');
            if (editBtn) {
                editBtn.onclick = function() {
                    window.location.href = '/Calendar/Update/' + info.event.id;
                };
            }
            var delBtn = document.getElementById('btn-delete-event');
            if (delBtn) {
                delBtn.onclick = function() {
                    if (confirm('Bạn có chắc chắn muốn xóa sự kiện này?')) {
                        fetch('/Calendar/DeleteEvent/' + info.event.id, { method: 'DELETE' })
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
    });

    calendar.render();

    // Close popover when clicking outside
    document.addEventListener('click', function(e) {
        var popover = document.getElementById('event-popover');
        if (!popover || popover.classList.contains('d-none')) return;
        if (!popover.contains(e.target) && !e.target.closest('.fc-event')) {
            popover.classList.add('d-none');
        }
    });
    
    // Add GMT+00 to top left corner
    setTimeout(() => {
        var axisEl = document.querySelector('.fc-timegrid-axis-cushion');
        if(axisEl) {
            axisEl.innerHTML = 'GMT+00';
        }
    }, 50);
});
