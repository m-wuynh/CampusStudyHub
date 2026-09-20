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
  setupDropdown('topbar-notifications-btn', 'notifications-menu');

  // Close dropdowns on document click
  document.addEventListener('click', () => {
    document.querySelectorAll('.sh-dropdown').forEach(d => d.style.display = 'none');
  });

  /* ── 4. Toast Notification System ── */
  window.showToast = function (message, type = 'success') {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `sh-toast`;

    let icon = '';
    let color = '';
    if (type === 'success') {
      icon = '<i class="bi bi-check-circle-fill fs-5"></i>';
      color = 'var(--sh-emerald-600)';
    } else if (type === 'error') {
      icon = '<i class="bi bi-x-circle-fill fs-5"></i>';
      color = 'var(--sh-rose-600)';
    } else if (type === 'info') {
      icon = '<i class="bi bi-info-circle-fill fs-5"></i>';
      color = 'var(--sh-indigo-600)';
    }

    toast.innerHTML = `
      <div style="color: ${color}; display: flex; align-items: center;">${icon}</div>
      <div style="flex-grow: 1;">
        <p class="mb-0 fw-semibold" style="font-size: 13px; color: var(--sh-slate-900);">${message}</p>
      </div>
      <button type="button" class="btn-close" style="font-size: 10px;" onclick="this.parentElement.remove()"></button>
    `;

    container.appendChild(toast);
    
    // Trigger animation
    setTimeout(() => toast.classList.add('show'), 10);

    // Auto remove
    setTimeout(() => {
      toast.classList.remove('show');
      setTimeout(() => toast.remove(), 300);
    }, 4000);
  };

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

  /* ── 7. Legacy study-group demo (the Groups page now uses /js/groups.js) ── */
  if (!document.getElementById('groups-view')) {
  const joinedGroupsKey = 'studyhub-web-joined-groups';
  const groupChatKey = 'studyhub-web-group-chat-v1';
  const detailModalElement = document.getElementById('groupDetailModal');
  let activeGroupId = 'math-advanced';

  function readJsonStorage(key, fallback) {
    try {
      const value = JSON.parse(localStorage.getItem(key));
      return value ?? fallback;
    } catch {
      return fallback;
    }
  }

  function saveJsonStorage(key, value) {
    try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* Storage may be disabled. */ }
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function getChats() {
    return readJsonStorage(groupChatKey, {
      'math-advanced': [
        { id: 'demo-1', author: 'Trần Văn Bình', content: 'Mọi người đã làm xong đề số 1 chưa?', sentAt: '2026-09-20T18:30:00', mine: false },
        { id: 'demo-2', author: 'Nguyễn Minh Anh', content: 'Mình làm xong phần đầu rồi, tối nay mình gửi lời giải nhé!', sentAt: '2026-09-20T18:34:00', mine: true }
      ]
    });
  }

  function formatChatTime(value) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return new Intl.DateTimeFormat('vi-VN', {
      day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
    }).format(date);
  }

  function renderGroupChat() {
    const container = document.getElementById('group-chat-messages');
    if (!container) return;
    const messages = getChats()[activeGroupId] || [];
    if (!messages.length) {
      container.innerHTML = '<div class="group-chat-empty"><i class="bi bi-chat-dots"></i><p>Chưa có tin nhắn. Hãy bắt đầu cuộc trò chuyện!</p></div>';
      return;
    }
    container.innerHTML = messages.map(message => `
      <div class="group-chat-row ${message.mine ? 'mine' : ''}">
        <div class="group-chat-bubble">
          ${message.mine ? '' : `<strong>${escapeHtml(message.author)}</strong>`}
          <div>${escapeHtml(message.content).replaceAll('\n', '<br>')}</div>
          <time>${formatChatTime(message.sentAt)}</time>
        </div>
      </div>`).join('');
    container.scrollTop = container.scrollHeight;
  }

  function updateGroupDetail(card) {
    if (!card || !detailModalElement) return;
    activeGroupId = card.dataset.groupId || 'math-advanced';
    const title = document.getElementById('groupDetailModalLabel');
    const subject = document.getElementById('groupDetailSubject');
    const meta = document.getElementById('groupDetailMeta');
    const description = document.getElementById('groupDetailDescription');
    if (title) title.textContent = card.dataset.groupName || 'Nhóm học tập';
    if (subject) {
      subject.textContent = card.dataset.groupSubject || 'Học tập';
      subject.className = `subject-badge ${card.dataset.groupSubjectClass || 'subject-toan'}`;
    }
    if (meta) meta.textContent = `${card.dataset.groupMembers || '1'} thành viên · Bạn đã tham gia`;
    if (description) description.textContent = card.dataset.groupDescription || '';
    renderGroupChat();
  }

  function setDiscoverMembership(card, isMember) {
    if (!card) return;
    const joinButton = card.querySelector('.join-group-btn');
    const openButton = card.querySelector('.open-group-btn');
    if (joinButton) {
      joinButton.setAttribute('data-member', String(isMember));
      joinButton.className = `join-group-btn ${isMember ? 'btn-secondary-sh' : 'btn-primary-sh'} flex-grow-1 justify-content-center`;
      joinButton.textContent = isMember ? 'Đã tham gia' : 'Tham gia';
    }
    if (openButton) openButton.style.display = isMember ? 'inline-flex' : 'none';
  }

  // Keep the join flow working while an older Razor view is still loaded by a debug session.
  document.querySelectorAll('#discover-groups .sh-card').forEach((card, index) => {
    const title = card.querySelector('h3')?.textContent?.trim() || `Nhóm học ${index + 1}`;
    const subjectBadge = card.querySelector('.subject-badge');
    const description = card.querySelector('p')?.textContent?.trim() || '';
    const memberText = card.querySelector('.d-flex.align-items-center.gap-2 span:last-child')?.textContent || '1';
    if (!card.hasAttribute('data-group-card')) card.setAttribute('data-group-card', '');
    if (!card.dataset.groupId) card.dataset.groupId = `discover-group-${index + 1}`;
    if (!card.dataset.groupName) card.dataset.groupName = title;
    if (!card.dataset.groupSubject) card.dataset.groupSubject = subjectBadge?.textContent?.trim() || 'Học tập';
    if (!card.dataset.groupSubjectClass) {
      card.dataset.groupSubjectClass = [...(subjectBadge?.classList || [])].find(name => name.startsWith('subject-')) || 'subject-toan';
    }
    if (!card.dataset.groupMembers) card.dataset.groupMembers = memberText.match(/\d+/)?.[0] || '1';
    if (!card.dataset.groupDescription) card.dataset.groupDescription = description;

    const joinButton = card.querySelector('.join-group-btn');
    if (joinButton && !card.querySelector('.open-group-btn')) {
      const actions = document.createElement('div');
      actions.className = 'group-card-actions d-flex gap-2 mt-auto';
      joinButton.parentNode.insertBefore(actions, joinButton);
      joinButton.classList.remove('w-100');
      joinButton.classList.add('flex-grow-1', 'justify-content-center');
      joinButton.dataset.groupId = card.dataset.groupId;
      actions.appendChild(joinButton);
      const openButton = document.createElement('button');
      openButton.type = 'button';
      openButton.className = 'open-group-btn btn-secondary-sh flex-grow-1 justify-content-center';
      openButton.style.display = 'none';
      openButton.textContent = 'Vào nhóm';
      actions.appendChild(openButton);
    }
  });

  const storedJoinedGroups = readJsonStorage(joinedGroupsKey, []);
  const joinedGroupIds = new Set(Array.isArray(storedJoinedGroups) ? storedJoinedGroups : []);
  document.querySelectorAll('[data-group-card]').forEach(card => {
    setDiscoverMembership(card, joinedGroupIds.has(card.dataset.groupId));
  });

  document.querySelectorAll('.join-group-btn').forEach(btn => {
    btn.addEventListener('click', function() {
      const card = this.closest('[data-group-card]');
      const isMember = this.getAttribute('data-member') === 'true';
      const nextMemberState = !isMember;

      if (card) {
        setDiscoverMembership(card, nextMemberState);
        if (nextMemberState) joinedGroupIds.add(card.dataset.groupId);
        else joinedGroupIds.delete(card.dataset.groupId);
        saveJsonStorage(joinedGroupsKey, [...joinedGroupIds]);
      } else {
        this.setAttribute('data-member', String(nextMemberState));
        this.className = `join-group-btn ${nextMemberState ? 'btn-secondary-sh' : 'btn-primary-sh'}`;
        this.textContent = nextMemberState ? 'Đã tham gia' : 'Tham gia';
      }

      window.showToast(nextMemberState ? 'Tham gia nhóm thành công! Bạn có thể bấm “Vào nhóm”.' : 'Bạn đã rời nhóm', nextMemberState ? 'success' : 'info');
    });
  });

  document.querySelectorAll('.open-group-btn').forEach(btn => {
    btn.addEventListener('click', function() {
      const card = this.closest('[data-group-card]');
      if (!card || !joinedGroupIds.has(card.dataset.groupId)) {
        window.showToast('Bạn cần tham gia nhóm trước khi xem chi tiết.', 'error');
        return;
      }
      updateGroupDetail(card);
      bootstrap.Modal.getOrCreateInstance(detailModalElement).show();
    });
  });

  if (detailModalElement) {
    detailModalElement.addEventListener('show.bs.modal', () => renderGroupChat());
  }

  const groupChatForm = document.getElementById('group-chat-form');
  const groupChatInput = document.getElementById('group-chat-input');
  if (groupChatForm && groupChatInput) {
    groupChatForm.addEventListener('submit', event => {
      event.preventDefault();
      const content = groupChatInput.value.trim();
      if (!content) return;
      const chats = getChats();
      chats[activeGroupId] = [...(chats[activeGroupId] || []), {
        id: `msg-${Date.now()}`,
        author: 'Nguyễn Minh Anh',
        content,
        sentAt: new Date().toISOString(),
        mine: true
      }];
      saveJsonStorage(groupChatKey, chats);
      groupChatInput.value = '';
      renderGroupChat();
    });
    groupChatInput.addEventListener('keydown', event => {
      if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        groupChatForm.requestSubmit();
      }
    });
  }

  window.addEventListener('storage', event => {
    if (event.key === groupChatKey) renderGroupChat();
  });

  }

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
  window.confirmDelete = function(itemName) {
    const modal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
    document.getElementById('delete-confirm-message').innerHTML = `Bạn có chắc muốn xóa <strong>${itemName}</strong> không? Hành động này không thể hoàn tác.`;
    
    document.getElementById('confirm-delete-btn').onclick = function() {
      modal.hide();
      window.showToast(`Đã xóa ${itemName} thành công`, 'success');
    };
    
    modal.show();
  };

  /* ── 12. Logout Modal ── */
  const logoutBtn = document.getElementById('sidebar-logout-btn');
  if (logoutBtn) {
    logoutBtn.addEventListener('click', () => {
      new bootstrap.Modal(document.getElementById('logoutConfirmModal')).show();
    });
  }
  
  const confirmLogoutBtn = document.getElementById('confirm-logout-btn');
  if (confirmLogoutBtn) {
    confirmLogoutBtn.addEventListener('click', () => {
      window.location.href = '/Login';
    });
  }
});
