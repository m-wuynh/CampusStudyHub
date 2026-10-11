        document.addEventListener('DOMContentLoaded', function() {
            var calendarEl = document.getElementById('calendar');
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
                    var isCompleted = arg.event.extendedProps.isCompleted;
                    var end = arg.event.end || arg.event.start;
                    var now = new Date();
                    
                    var classes = [];
                    if (isCompleted) {
                        classes.push('event-state-completed');
                    } else if (end < now) {
                        classes.push('event-state-missed');
                    } else {
                        classes.push('event-state-pending');
                    }
                    return classes;
                },
                eventClick: function(info) {
                    var popover = document.getElementById('event-popover');
                    
                    // Populate data
                    document.getElementById('popover-title').innerText = info.event.title;
                    
                    var start = info.event.start;
                    var end = info.event.end;
                    var timeStr = start.toLocaleDateString('vi-VN', { weekday: 'long', day: 'numeric', month: 'long' }) + ' · ';
                    
                    var formatTime = function(date) {
                        var h = date.getHours();
                        var m = date.getMinutes();
                        var ampm = h >= 12 ? 'PM' : 'AM';
                        h = h % 12;
                        h = h ? h : 12; // the hour '0' should be '12'
                        var mStr = m < 10 ? '0' + m : m;
                        return h + ':' + mStr + ampm;
                    };
                    
                    timeStr += formatTime(start) + ' – ' + (end ? formatTime(end) : '');
                    document.getElementById('popover-time').innerText = timeStr;
                    
                    // Set color box based on state
                    var endTime = info.event.end || info.event.start;
                    var nowTime = new Date();
                    var color = '#1a73e8'; // pending (blue)
                    var isMissed = false;
                    if (info.event.extendedProps.isCompleted) {
                        color = '#1e8e3e'; // green
                    } else if (endTime < nowTime) {
                        color = '#d93025'; // red
                        isMissed = true;
                    }
                    document.getElementById('popover-color').style.backgroundColor = color;

                    // Event type label
                    var typeLabels = { 'Exam': 'Kiểm tra / Thi', 'Class': 'Học nhóm', 'Other': 'Bài tập / Nộp bài', 'SelfStudy': 'Tự học' };
                    document.getElementById('popover-type').innerText = typeLabels[type] || 'Tự học';

                    // Description (show/hide)
                    var desc = info.event.extendedProps.description || '';
                    var descRow = document.getElementById('popover-desc-row');
                    if (desc.trim()) {
                        document.getElementById('popover-desc').innerText = desc;
                        descRow.style.display = 'flex';
                    } else {
                        descRow.style.display = 'none';
                    }

                    // Location (show/hide)
                    var loc = info.event.extendedProps.location || '';
                    var locRow = document.getElementById('popover-location-row');
                    if (loc.trim()) {
                        document.getElementById('popover-location').innerText = loc;
                        locRow.style.display = 'flex';
                    } else {
                        locRow.style.display = 'none';
                    }
                    
                    // Position popover
                    var rect = info.el.getBoundingClientRect();
                    var calendarRect = document.getElementById('calendar').getBoundingClientRect();
                    
                    var top = rect.top - calendarRect.top + 20;
                    var left = rect.right - calendarRect.left + 10;
                    
                    // Adjust if it goes off screen
                    if (left + 360 > calendarRect.width) {
                        left = rect.left - calendarRect.left - 370; // show on left
                    }
                    
                    popover.style.top = top + 'px';
                    popover.style.left = left + 'px';
                    popover.classList.remove('d-none');
                    
                    // Fetch Reminders
                    var remRow = document.getElementById('popover-reminders-row');
                    var remList = document.getElementById('popover-reminders-list');
                    remRow.style.setProperty('display', 'none', 'important');
                    remList.innerHTML = '';
                    
                    fetch('/Calendar/GetEventReminders/' + info.event.id)
                        .then(r => r.json())
                        .then(minutes => {
                            if (minutes && minutes.length > 0) {
                                remList.innerHTML = minutes.map(m => {
                                    if (m % 10080 === 0) return (m / 10080) + ' tuần trước';
                                    if (m % 1440 === 0) return (m / 1440) + ' ngày trước';
                                    if (m % 60 === 0) return (m / 60) + ' giờ trước';
                                    return m + ' phút trước';
                                }).join('<br/>');
                                remRow.style.setProperty('display', 'flex', 'important');
                            }
                        });

                    // Set Edit/Delete URLs
                    document.getElementById('btn-edit-event').onclick = function() {
                        window.location.href = '/Calendar/Update/' + info.event.id;
                    };
                    document.getElementById('btn-delete-event').onclick = function() {
                        if (confirm('Bạn có chắc chắn muốn xóa sự kiện này?')) {
                            fetch('/Calendar/DeleteEvent/' + info.event.id, { method: 'DELETE' })
                                .then(res => {
                                    if (res.ok) window.location.reload();
                                    else alert('Lỗi khi xóa!');
                                });
                        }
                    };

                    // Handle Completion Checkbox
                    var isCompleted = info.event.extendedProps.isCompleted;
                    var chkComplete = document.getElementById('popover-is-completed');
                    chkComplete.checked = isCompleted;

                    var titleEl = document.getElementById('popover-title');
                    titleEl.style.textDecoration = isCompleted ? 'line-through' : 'none';
                    titleEl.style.color = isCompleted ? '#70757a' : '#3c4043';

                    // Replace node to clear previous event listeners
                    var newChk = chkComplete.cloneNode(true);
                    chkComplete.parentNode.replaceChild(newChk, chkComplete);
                    
                    if (isMissed) {
                        newChk.disabled = true;
                        newChk.parentElement.style.opacity = '0.5';
                        newChk.parentElement.title = 'Đã quá hạn, không thể đánh dấu hoàn thành';
                    } else {
                        newChk.disabled = false;
                        newChk.parentElement.style.opacity = '1';
                        newChk.parentElement.title = '';
                    }

                    newChk.addEventListener('change', function() {
                        var completed = this.checked;
                        fetch('/Calendar/ToggleEventCompletion/' + info.event.id, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(completed)
                        }).then(res => {
                            if (res.ok) {
                                info.event.setExtendedProp('isCompleted', completed);
                                titleEl.style.textDecoration = completed ? 'line-through' : 'none';
                                titleEl.style.color = completed ? '#70757a' : '#3c4043';
                            } else {
                                alert('Có lỗi xảy ra khi cập nhật!');
                                this.checked = !completed;
                            }
                        });
                    });
                }
            });
            calendar.render();
            
            // Add GMT+00 to top left corner
            setTimeout(() => {
                var axisEl = document.querySelector('.fc-timegrid-axis-cushion');
                if(axisEl) {
                    axisEl.innerHTML = 'GMT+00';
                }
            }, 50);
        });
