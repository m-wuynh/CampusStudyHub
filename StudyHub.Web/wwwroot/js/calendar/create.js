/* ========================================================
   Calendar Module: Màn Thêm ca học (Create)
   Chứa toàn bộ JS cho form tạo sự kiện / lịch học mới
   ======================================================== */
document.addEventListener('DOMContentLoaded', function() {
    'use strict';

    // 1. Tự động tải danh sách loại sự kiện từ DB vào dropdown
    fetch('/Calendar/GetCategories')
        .then(function(r) { return r.json(); })
        .then(function(cats) {
            if (!cats || !cats.length) return;
            var sel = document.getElementById('event-category');
            if (!sel) return;
            var currentVal = sel.value;
            sel.innerHTML = cats.map(function(c) {
                return '<option value="' + c.name + '">' + c.name + '</option>';
            }).join('');
            if (currentVal) sel.value = currentVal;
        })
        .catch(function(err) {
            console.error('Lỗi khi tải loại sự kiện:', err);
        });

    // 2. Tự động ẩn lỗi khi người dùng gõ tên sự kiện
    var titleInput = document.getElementById('event-title');
    if (titleInput) {
        titleInput.addEventListener('input', function() {
            var titleError = document.getElementById('event-title-error');
            if (this.value.trim().length > 0) {
                if (titleError) titleError.classList.add('d-none');
                this.classList.remove('border-danger');
            }
        });
    }
});

// Hàm thêm dòng nhắc nhở
window.addNotification = function(val, unit, channel) {
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

// Hàm submit tạo ca học
window.submitEvent = async function() {
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

    var selectedStart = new Date(date + 'T' + start);
    if (selectedStart < new Date(Date.now() - 60000)) {
        if (window.showToast) window.showToast('Thời gian bắt đầu không thể ở trong quá khứ', 'error');
        return;
    }

    if (start.length === 5) start += ":00";
    if (end.length === 5) end += ":00";

    var reminders = [];
    document.querySelectorAll('#notifications-container .notif-row-purple').forEach(function(row) {
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
        var res = await fetch('/Calendar/CreateEvent', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (res.ok) {
            if (window.showToast) window.showToast('Đã thêm sự kiện vào lịch thành công!', 'success');
            setTimeout(function() { window.location.href = '/Calendar'; }, 800);
        } else {
            var errData = await res.json().catch(function() { return null; });
            var msg = (errData && errData.error) ? errData.error : 'Lỗi khi thêm sự kiện';
            if (window.showToast) window.showToast(msg, 'error');
        }
    } catch (err) {
        console.error('Lỗi mạng:', err);
        if (window.showToast) window.showToast('Lỗi mạng khi tạo sự kiện', 'error');
    }
};
