/* ========================================================
   Calendar Module: Màn Quản lý Loại sự kiện (Categories)
   ======================================================== */
document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    var MAX_NAME_LEN = 100;
    var MAX_DESC_LEN = 500;
    var validNameRegex = /^[\p{L}\p{N}\s\/\-\(\),\.]*$/u;

    // Hàm validate tổng quát cho cả form Thêm và form Sửa
    function validateForm(nameInput, descInput, nameErrorEl, descErrorEl) {
        var isValid = true;
        var nameVal = nameInput ? nameInput.value : '';
        var nameTrimmed = nameVal.trim();
        var descVal = descInput ? descInput.value : '';

        // 1. Kiểm tra Tên loại sự kiện
        if (nameErrorEl && nameInput) {
            var nameSpan = nameErrorEl.querySelector('.category-error-text');
            if (!nameTrimmed) {
                nameInput.classList.add('is-invalid');
                if (nameSpan) nameSpan.textContent = 'Tên loại sự kiện không được để trống.';
                nameErrorEl.classList.remove('d-none');
                isValid = false;
            } else if (nameVal.length > MAX_NAME_LEN) {
                nameInput.classList.add('is-invalid');
                if (nameSpan) nameSpan.textContent = 'Tên loại sự kiện không được vượt quá ' + MAX_NAME_LEN + ' ký tự (hiện có: ' + nameVal.length + ' ký tự).';
                nameErrorEl.classList.remove('d-none');
                isValid = false;
            } else if (!validNameRegex.test(nameVal)) {
                nameInput.classList.add('is-invalid');
                if (nameSpan) nameSpan.textContent = 'Tên loại sự kiện không được chứa ký tự đặc biệt.';
                nameErrorEl.classList.remove('d-none');
                isValid = false;
            } else {
                nameInput.classList.remove('is-invalid');
                nameErrorEl.classList.add('d-none');
            }
        }

        // 2. Kiểm tra Mô tả
        if (descErrorEl && descInput) {
            var descSpan = descErrorEl.querySelector('.category-error-text');
            if (descVal.length > MAX_DESC_LEN) {
                descInput.classList.add('is-invalid');
                if (descSpan) descSpan.textContent = 'Mô tả không được vượt quá ' + MAX_DESC_LEN + ' ký tự (hiện có: ' + descVal.length + ' ký tự).';
                descErrorEl.classList.remove('d-none');
                isValid = false;
            } else {
                descInput.classList.remove('is-invalid');
                descErrorEl.classList.add('d-none');
            }
        }

        return isValid;
    }

    // ========================================================
    // 1. Danh sách Loại sự kiện: Xóa (Categories.cshtml)
    // ========================================================
    document.querySelectorAll('.btn-delete-category').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            var id = this.dataset.id;
            var name = this.dataset.name || '';
            var count = parseInt(this.dataset.count || '0', 10);

            var msg = 'Bạn có chắc chắn muốn xóa loại sự kiện "' + name + '"?';
            if (count > 0) {
                msg += '\n(Hiện có ' + count + ' ca học đang dùng loại này, các ca học này sẽ tự động chuyển về loại khác).';
            }

            var ok = window.showConfirm ? await window.showConfirm(msg) : confirm(msg);
            if (!ok) return;

            try {
                var res = await fetch('/Calendar/Categories/Delete/' + id, { method: 'DELETE' });
                if (res.ok) {
                    if (window.showToast) window.showToast('Đã xóa loại sự kiện "' + name + '"!', 'success');
                    setTimeout(function () { window.location.reload(); }, 600);
                } else {
                    var errData = await res.json().catch(function () { return {}; });
                    if (window.showToast) window.showToast(errData.error || 'Lỗi khi xóa loại sự kiện', 'error');
                }
            } catch (err) {
                if (window.showToast) window.showToast('Lỗi mạng khi xóa', 'error');
            }
        });
    });

    // ========================================================
    // 2. Form Thêm mới Loại sự kiện (CreateCategory.cshtml)
    // ========================================================
    var formCreate = document.getElementById('form-create-category');
    if (formCreate) {
        var nameIn = document.getElementById('category-name');
        var descIn = document.getElementById('category-desc');
        var nameErr = document.getElementById('category-name-error');
        var descErr = document.getElementById('category-desc-error');
        var btnSave = document.getElementById('btn-save-category');

        nameIn?.addEventListener('input', function () { validateForm(nameIn, descIn, nameErr, descErr); });
        descIn?.addEventListener('input', function () { validateForm(nameIn, descIn, nameErr, descErr); });

        formCreate.addEventListener('submit', async function (e) {
            e.preventDefault();
            if (!validateForm(nameIn, descIn, nameErr, descErr)) {
                nameIn?.focus();
                return;
            }

            btnSave.disabled = true;
            btnSave.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang tạo...';

            try {
                var res = await fetch('/Calendar/Categories/Create', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ Name: nameIn.value.trim(), Description: (descIn?.value || '').trim() })
                });

                if (res.ok) {
                    if (window.showToast) window.showToast('Đã thêm loại sự kiện thành công!', 'success');
                    setTimeout(function () { window.location.href = '/Calendar/Categories'; }, 700);
                } else {
                    var errData = await res.json().catch(function () { return {}; });
                    var errorMsg = errData.error || 'Lỗi khi tạo loại sự kiện';
                    if (window.showToast) window.showToast(errorMsg, 'error');
                    if (nameErr) {
                        var span = nameErr.querySelector('.category-error-text');
                        if (span) span.textContent = errorMsg;
                        nameErr.classList.remove('d-none');
                        nameIn.classList.add('is-invalid');
                    }
                }
            } catch (err) {
                if (window.showToast) window.showToast('Lỗi mạng khi tạo loại sự kiện', 'error');
            } finally {
                btnSave.disabled = false;
                btnSave.innerHTML = '<i class="bi bi-check-lg"></i><span>Tạo loại sự kiện</span>';
            }
        });
    }

    // ========================================================
    // 3. Form Chỉnh sửa Loại sự kiện (EditCategory.cshtml)
    // ========================================================
    var formEdit = document.getElementById('form-edit-category');
    if (formEdit) {
        var nameEdit = document.getElementById('edit-category-name');
        var descEdit = document.getElementById('edit-category-desc');
        var nameErrEdit = document.getElementById('edit-category-name-error');
        var descErrEdit = document.getElementById('edit-category-desc-error');
        var btnSaveEdit = document.getElementById('btn-save-edit-category');
        var id = document.getElementById('edit-category-id')?.value;

        nameEdit?.addEventListener('input', function () { validateForm(nameEdit, descEdit, nameErrEdit, descErrEdit); });
        descEdit?.addEventListener('input', function () { validateForm(nameEdit, descEdit, nameErrEdit, descErrEdit); });

        formEdit.addEventListener('submit', async function (e) {
            e.preventDefault();
            if (!validateForm(nameEdit, descEdit, nameErrEdit, descErrEdit)) {
                nameEdit?.focus();
                return;
            }

            btnSaveEdit.disabled = true;
            btnSaveEdit.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang lưu...';

            try {
                var res = await fetch('/Calendar/Categories/Update/' + id, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ Name: nameEdit.value.trim(), Description: (descEdit?.value || '').trim() })
                });

                if (res.ok) {
                    if (window.showToast) window.showToast('Cập nhật loại sự kiện thành công!', 'success');
                    setTimeout(function () { window.location.href = '/Calendar/Categories'; }, 700);
                } else {
                    var errData = await res.json().catch(function () { return {}; });
                    var errorMsg = errData.error || 'Lỗi khi cập nhật loại sự kiện';
                    if (window.showToast) window.showToast(errorMsg, 'error');
                    if (nameErrEdit) {
                        var span = nameErrEdit.querySelector('.category-error-text');
                        if (span) span.textContent = errorMsg;
                        nameErrEdit.classList.remove('d-none');
                        nameEdit.classList.add('is-invalid');
                    }
                }
            } catch (err) {
                if (window.showToast) window.showToast('Lỗi mạng khi cập nhật loại sự kiện', 'error');
            } finally {
                btnSaveEdit.disabled = false;
                btnSaveEdit.innerHTML = '<i class="bi bi-check-lg"></i><span>Lưu thay đổi</span>';
            }
        });
    }
});
