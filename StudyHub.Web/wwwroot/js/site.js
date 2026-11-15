/**
 * Study Hub - Vanilla JS Interactivity
 */

document.addEventListener('DOMContentLoaded', () => {

  /* ── 1. Sidebar Toggle (Mobile) ── */
  const body = document.body;
  const hamburgerBtn = document.getElementById('topbar-hamburger-btn');
  const sidebarCloseBtn = document.getElementById('sidebar-close-btn');
  const sidebarBackdrop = document.getElementById('sidebar-backdrop');

  function toggleSidebar() {
    body.classList.toggle('sidebar-open');
  }

  if (hamburgerBtn) hamburgerBtn.addEventListener('click', toggleSidebar);
  if (sidebarCloseBtn) sidebarCloseBtn.addEventListener('click', toggleSidebar);
  if (sidebarBackdrop) sidebarBackdrop.addEventListener('click', toggleSidebar);

  /* ── 2. Active Nav Link Highlighting ── */
  const currentPath = window.location.pathname.toLowerCase();
  const navItems = document.querySelectorAll('.sidebar-nav-item');
  navItems.forEach(item => {
    item.classList.remove('active');
    const href = item.getAttribute('href')?.toLowerCase() || '';
    if (currentPath === href || (currentPath === '/' && href === '/index')) {
      item.classList.add('active');
    }
  });

  /* ── 3. Dropdowns (Quick Add & Notifications) ── */
  function setupDropdown(btnId, menuId) {
    const btn = document.getElementById(btnId);
    const menu = document.getElementById(menuId);
    if (!btn || !menu) return;

    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      // Close other dropdowns
      document.querySelectorAll('.sh-dropdown').forEach(d => {
        if (d.id !== menuId) d.style.display = 'none';
      });
      menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
    });

    menu.addEventListener('click', (e) => e.stopPropagation());
  }

  setupDropdown('topbar-quick-add-btn', 'quick-add-menu');
  // topbar-notifications-btn is handled by Notification JS in _Layout.cshtml to dynamically load notifications

  // Close dropdowns on document click
  document.addEventListener('click', () => {
    document.querySelectorAll('.sh-dropdown').forEach(d => d.style.display = 'none');
  });

  /* ── 4. Toast Notification System ── */
  window.showToast = function (message, type = 'success', duration = 3000) {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `sh-toast`;
    toast.setAttribute('role', type === 'error' ? 'alert' : 'status');

    let icon = '';
    let color = '';
    if (type === 'success') {
      icon = '<i class="bi bi-check-circle-fill"></i>';
      color = 'var(--sh-emerald-600)';
    } else if (type === 'error') {
      icon = '<i class="bi bi-exclamation-triangle-fill"></i>';
      color = 'var(--sh-rose-600)';
    } else if (type === 'warning') {
      icon = '<i class="bi bi-exclamation-circle-fill"></i>';
      color = 'var(--sh-amber-600)';
    } else {
      icon = '<i class="bi bi-info-circle-fill"></i>';
      color = 'var(--sh-indigo-600)';
    }

    toast.innerHTML = `
      <div style="color: ${color}; font-size: 16px; display: flex; align-items: center; flex-shrink: 0;">${icon}</div>
      <div style="flex-grow: 1; min-width: 0;">
        <span class="fw-semibold sh-toast-message" style="font-size: 13px; color: var(--sh-slate-800); line-height: 1.4; display: block;"></span>
      </div>
      <button type="button" class="sh-toast-close" aria-label="Đóng thông báo"><span aria-hidden="true">&times;</span></button>
    `;

    toast.querySelector('.sh-toast-message').textContent = String(message ?? '');
    const closeBtn = toast.querySelector('.sh-toast-close');
    if (closeBtn) {
      closeBtn.onclick = () => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
      };
    }

    container.appendChild(toast);
    
    // Trigger animation
    requestAnimationFrame(() => {
      setTimeout(() => toast.classList.add('show'), 10);
    });

    // Auto remove after 3 seconds
    setTimeout(() => {
      if (toast.parentElement) {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
      }
    }, duration);
  };

  document.querySelectorAll('.js-tempdata-alert').forEach(element => {
    const message = element.dataset.message;
    if (message) window.showToast(message, element.dataset.type || 'info');
    element.remove();
  });

  // Mark notifications as read
  const markAllReadBtn = document.getElementById('mark-all-read-btn');
  if (markAllReadBtn) {
    markAllReadBtn.addEventListener('click', () => {
      document.querySelectorAll('.notif-dot-item').forEach(d => d.style.display = 'none');
      document.querySelectorAll('.notif-item-unread').forEach(item => {
        item.style.background = 'transparent';
      });
      const badge = document.getElementById('notif-count-badge');
      if (badge) badge.style.display = 'none';
      const indicator = document.getElementById('notif-indicator');
      if (indicator) indicator.style.display = 'none';
    });
  }

  /* ── 5. Flashcard Flip ── */
  const flashcardContainer = document.getElementById('active-flashcard');
  const flipBtn = document.getElementById('flip-card-btn');
  
  if (flashcardContainer && flipBtn) {
    flipBtn.addEventListener('click', () => {
      flashcardContainer.classList.toggle('flipped');
    });
  }

  /* ── 6. Event Checklist Toggle ── */
  document.querySelectorAll('.event-check-toggle').forEach(btn => {
    btn.addEventListener('click', function() {
      const eventItem = this.closest('.event-item');
      const icon = this.querySelector('i');
      
      if (eventItem.classList.contains('completed')) {
        eventItem.classList.remove('completed');
        icon.className = 'bi bi-square';
        icon.style.color = 'var(--sh-slate-400)';
        const text = eventItem.querySelector('.fw-semibold');
        if (text) text.classList.remove('text-decoration-line-through');
      } else {
        eventItem.classList.add('completed');
        icon.className = 'bi bi-check-square-fill text-success';
        icon.style.color = '';
        const text = eventItem.querySelector('.fw-semibold');
        if (text) text.classList.add('text-decoration-line-through');
        window.showToast('Đã đánh dấu hoàn thành!', 'success');
      }
    });
  });

  /* ── 8. Login/Register Toggle ── */
  const toggleAuthModeBtn = document.getElementById('toggle-auth-mode-btn');
  if (toggleAuthModeBtn) {
    toggleAuthModeBtn.addEventListener('click', () => {
      const isLogin = document.getElementById('login-form-section').style.display !== 'none';
      
      if (isLogin) {
        document.getElementById('login-form-section').style.display = 'none';
        document.getElementById('register-form-section').style.display = 'block';
        document.getElementById('auth-mode-title').textContent = 'Tạo tài khoản học tập';
        document.getElementById('auth-mode-desc').textContent = 'Tham gia cùng hàng ngàn học sinh THPT khác để tối ưu hóa việc học của bạn';
        document.getElementById('toggle-auth-mode-text').textContent = 'Đã có tài khoản?';
        toggleAuthModeBtn.textContent = 'Đăng nhập ngay';
      } else {
        document.getElementById('login-form-section').style.display = 'block';
        document.getElementById('register-form-section').style.display = 'none';
        document.getElementById('auth-mode-title').textContent = 'Chào mừng bạn trở lại!';
        document.getElementById('auth-mode-desc').textContent = 'Đăng nhập để tiếp tục quản lý ghi chú, flashcard và thời khóa biểu của bạn';
        document.getElementById('toggle-auth-mode-text').textContent = 'Chưa có tài khoản?';
        toggleAuthModeBtn.textContent = 'Đăng ký miễn phí';
      }
    });
  }

  /* ── 9. Password Visibility ── */
  const togglePasswordBtn = document.getElementById('toggle-password-btn');
  if (togglePasswordBtn) {
    togglePasswordBtn.addEventListener('click', () => {
      const input = document.getElementById('login-password-input');
      const icon = document.getElementById('toggle-password-icon');
      if (input.type === 'password') {
        input.type = 'text';
        icon.className = 'bi bi-eye-slash';
      } else {
        input.type = 'password';
        icon.className = 'bi bi-eye';
      }
    });
  }

  /* ── 10. Demo Quick Login ── */
  const demoLoginBtn = document.getElementById('demo-login-quick-btn');
  if (demoLoginBtn) {
    demoLoginBtn.addEventListener('click', () => {
      document.getElementById('login-email-input').value = 'minhanh.nguyen@thpt-hanoi.edu.vn';
      document.getElementById('login-password-input').value = 'StudyHub@2026';
      window.showToast('Đang đăng nhập...', 'info');
      setTimeout(() => {
        document.getElementById('login-form').submit();
      }, 500);
    });
  }

  /* ── 11. Confirm Delete Modal Global Logic ── */
  let confirmationOpen = false;
  window.showConfirm = function(message) {
    const element = document.getElementById('deleteConfirmModal');
    const confirmButton = document.getElementById('confirm-delete-btn');
    if (confirmationOpen) return Promise.resolve(false);
    if (!element || !confirmButton || !window.bootstrap?.Modal) {
      window.showToast('Không mở được hộp xác nhận. Vui lòng tải lại trang.', 'error');
      return Promise.resolve(false);
    }
    confirmationOpen = true;
    document.getElementById('delete-confirm-message').textContent = String(message ?? '');
    const modal = bootstrap.Modal.getOrCreateInstance(element);
    const previousElement = document.querySelector('.modal.show:not(#deleteConfirmModal)');
    const previousModal = previousElement ? bootstrap.Modal.getOrCreateInstance(previousElement) : null;
    const trigger = document.activeElement;
    return new Promise(resolve => {
      let confirmed = false;
      confirmButton.disabled = true;
      element.addEventListener('shown.bs.modal', () => { confirmButton.disabled = false; }, { once: true });
      const accept = () => { confirmed = true; confirmButton.disabled = true; modal.hide(); };
      confirmButton.addEventListener('click', accept);
      element.addEventListener('hidden.bs.modal', () => {
        confirmButton.removeEventListener('click', accept);
        confirmButton.disabled = false;
        confirmationOpen = false;
        if (!confirmed && previousModal) previousModal.show();
        else if (!confirmed && trigger?.isConnected) trigger.focus();
        resolve(confirmed);
      }, { once: true });
      if (previousModal) {
        previousElement.addEventListener('hidden.bs.modal', () => modal.show(), { once: true });
        previousModal.hide();
      } else modal.show();
    });
  };
  window.confirmDelete = itemName => window.showConfirm(`Bạn có chắc muốn xóa ${itemName} không? Hành động này không thể hoàn tác.`);

  const confirmedForms = new WeakSet();
  const pendingForms = new WeakSet();
  document.addEventListener('submit', async event => {
    const form = event.target;
    if (!form.matches('form[data-confirm]')) return;
    if (confirmedForms.has(form)) { confirmedForms.delete(form); return; }
    event.preventDefault();
    event.stopImmediatePropagation();
    if (pendingForms.has(form)) return;
    pendingForms.add(form);
    const submitter = event.submitter;
    try {
      if (await window.showConfirm(form.dataset.confirm) && form.isConnected) {
        confirmedForms.add(form);
        HTMLFormElement.prototype.requestSubmit.call(form, submitter?.isConnected ? submitter : undefined);
      }
    } finally {
      confirmedForms.delete(form);
      pendingForms.delete(form);
    }
  }, true);

  /* ── 12. Logout Modal ── */
  const logoutBtn = document.getElementById('sidebar-logout-btn');
  if (logoutBtn) {
    logoutBtn.addEventListener('click', () => {
      new bootstrap.Modal(document.getElementById('logoutConfirmModal')).show();
    });
  }
  
});
