/* ========================================================
   Calendar Module: Màn Cập nhật ca học (Update)
   Chứa toàn bộ JS cho form chỉnh sửa sự kiện / lịch học
   ======================================================== */
document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    var eventIdEl = document.getElementById('event-id');
    var eventId = eventIdEl ? eventIdEl.value : window.location.pathname.split('/').pop();
    var sel = document.getElementById('event-category');
    var initialType = sel ? sel.getAttribute('data-initial-type') || sel.value : '';

    // 1. Tải danh sách loại sự kiện từ DB vào dropdown
    fetch('/Calendar/GetCategories')
        .then(function (r) { return r.json(); })
        .then(function (cats) {
            if (!cats || !cats.length) return;
            if (!sel) return;
            sel.innerHTML = cats.map(function (c) {
                var isSel = (c.name === initialType);
                return '<option value="' + c.name + '"' + (isSel ? ' selected' : '') + '>' + c.name + '</option>';
            }).join('');
        })
        .catch(function (err) {
            console.error('Lỗi khi tải loại sự kiện:', err);
        });

    // 2. Nạp các nhắc nhở đã lưu từ server
    if (eventId) {
        fetch('/Calendar/GetEventReminders/' + eventId)
            .then(function (r) { return r.json(); })
            .then(function (reminders) {
                if (!reminders || !reminders.length) return;
                reminders.forEach(function (item) {
                    var m = typeof item === 'object' ? item.minutes : item;
                    var ch = typeof item === 'object' ? item.channel : 'notification';
                    var unit = 'minutes';
                    var val = m;

                    if (val > 0) {
                        if (val % 10080 === 0) {
                            unit = 'weeks';
                            val /= 10080;
                        } else if (val % 1440 === 0) {
                            unit = 'days';
                            val /= 1440;
                        } else if (val % 60 === 0) {
                            unit = 'hours';
                            val /= 60;
                        }
                    }
                    window.addNotification(val, unit, ch);
                });
            })
            .catch(function (err) {
                console.error('Lỗi khi tải thông báo:', err);
            });
    }

    // 3. Ẩn lỗi khi người dùng nhập tên sự kiện
    var titleInput = document.getElementById('event-title');
    if (titleInput) {
        titleInput.addEventListener('input', function () {
            var titleError = document.getElementById('event-title-error');
            if (this.value.trim().length > 0) {
                if (titleError) titleError.classList.add('d-none');
                this.classList.remove('border-danger');
            }
        });
    }
});

// Hàm thêm dòng thông báo nhắc nhở
window.addNotification = function (val, unit, channel) {
    if (val === undefined) val = 10;
    if (unit === undefined) unit = 'minutes';
    if (channel === undefined) channel = 'notification';

    var container = document.getElementById('notifications-container');
    if (!container) return;

    var row = document.createElement('div');
    row.className = 'notif-row-purple d-flex align-items-center gap-2 flex-wrap mb-1';
    row.innerHTML = '\
        <i class="bi bi-bell-fill notif-bell-icon"></i>\
        <select class="notif-select-purple">\
            <option value="notification"' + (channel === 'notification' ? ' selected' : '') + '>Thông báo hệ thống</option>\
            <option value="email"' + (channel === 'email' ? ' selected' : '') + '>Email</option>\
        </select>\
        <span class="text-muted small">Trước:</span>\
        <input type="number" min="0" class="notif-input-purple" value="' + val + '">\
        <select class="notif-select-purple">\
            <option value="minutes"' + (unit === 'minutes' ? ' selected' : '') + '>phút</option>\
            <option value="hours"' + (unit === 'hours' ? ' selected' : '') + '>giờ</option>\
            <option value="days"' + (unit === 'days' ? ' selected' : '') + '>ngày</option>\
            <option value="weeks"' + (unit === 'weeks' ? ' selected' : '') + '>tuần</option>\
        </select>\
        <button type="button" class="btn btn-sm btn-link text-danger p-0 ms-auto" title="Xóa thông báo" onclick="this.closest(\'.notif-row-purple\').remove()">\
            <i class="bi bi-x-circle-fill notif-remove-icon"></i>\
        </button>';
    container.appendChild(row);
};

// Hàm submit cập nhật ca học
window.submitEvent = async function () {
    var eventIdEl = document.getElementById('event-id');
    var eventId = eventIdEl ? eventIdEl.value : window.location.pathname.split('/').pop();

    var titleInput = document.getElementById('event-title');
    var titleError = document.getElementById('event-title-error');
    var title = titleInput ? titleInput.value : '';
    var date = document.getElementById('event-date')?.value;
    var start = document.getElementById('event-start')?.value;
    var end = document.getElementById('event-end')?.value;
    var category = document.getElementById('event-category')?.value;
    var loc = document.getElementById('event-location')?.value || '';
    var desc = document.getElementById('event-description')?.value || '';

    if (!title || !title.trim()) {
        if (titleError) titleError.classList.remove('d-none');
        if (titleInput) {
            titleInput.classList.add('border-danger');
            titleInput.focus();
        }
        return;
    } else {
        if (titleError) titleError.classList.add('d-none');
        if (titleInput) titleInput.classList.remove('border-danger');
    }

    if (!date || !start || !end || !category) {
        if (window.showToast) window.showToast('Vui lòng điền đủ thông tin thời gian và loại sự kiện', 'error');
        return;
    }

    if (end <= start) {
        if (window.showToast) window.showToast('Giờ kết thúc (' + end + ') phải sau giờ bắt đầu (' + start + ') trong cùng ngày.', 'error');
        return;
    }

    var selectedEnd = new Date(date + 'T' + end);
    if (selectedEnd < new Date(Date.now() - 60000)) {
        if (window.showToast) window.showToast('Thời gian kết thúc ca học không thể ở trong quá khứ', 'error');
        return;
    }

    if (start.length === 5) start += ":00";
    if (end.length === 5) end += ":00";

    var reminders = [];
    document.querySelectorAll('#notifications-container .notif-row-purple').forEach(function (row) {
        var inputs = row.querySelectorAll('input, select');
        if (inputs.length >= 3) {
            var channel = inputs[0].value;
            var val = parseInt(inputs[1].value, 10);
            var unit = inputs[2].value;
            if (!isNaN(val) && val >= 0) {
                if (val > 0) {
                    if (unit === 'hours') val *= 60;
                    else if (unit === 'days') val *= 1440;
                    else if (unit === 'weeks') val *= 10080;
                }
                reminders.push({ Channel: channel, Minutes: val });
            }
        }
    });

    var data = {
        Title: title.trim(),
        Date: date,
        StartTime: start,
        EndTime: end,
        EventType: category,
        Location: loc,
        Description: desc,
        Reminders: reminders
    };

    try {
        var res = await fetch('/Calendar/Update/' + eventId, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (res.ok) {
            if (window.showToast) window.showToast('Đã cập nhật sự kiện thành công!', 'success');

            // Xóa cache thông báo cũ
            try {
                var toasted = JSON.parse(localStorage.getItem('sh_notif_toasted') || '[]');
                toasted = toasted.filter(function (id) { return id != eventId && !String(id).startsWith(eventId + '_'); });
                localStorage.setItem('sh_notif_toasted', JSON.stringify(toasted));

                var reads = JSON.parse(localStorage.getItem('sh_notif_read') || '[]');
                reads = reads.filter(function (id) { return id != eventId && !String(id).startsWith(eventId + '_'); });
                localStorage.setItem('sh_notif_read', JSON.stringify(reads));
            } catch (e) { }

            setTimeout(function () { window.location.href = '/Calendar'; }, 800);
        } else {
            var errData = await res.json().catch(function () { return null; });
            var msg = (errData && errData.error) ? errData.error : 'Lỗi khi cập nhật sự kiện';
            if (window.showToast) window.showToast(msg, 'error');
        }
    } catch (err) {
        console.error('Lỗi mạng:', err);
        if (window.showToast) window.showToast('Lỗi mạng khi cập nhật sự kiện', 'error');
    }
};
