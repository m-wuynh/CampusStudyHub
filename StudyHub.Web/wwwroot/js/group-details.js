(() => {
  const page = document.getElementById('group-details-page');
  if (!page) return;

  const groupId = page.dataset.groupId;
  const isMember = page.dataset.isMember === 'true';
  const isOwner = page.dataset.isOwner === 'true';
  const token = document.querySelector('#group-antiforgery input[name="__RequestVerificationToken"]')?.value;

  const escapeHtml = value => String(value ?? '')
    .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;').replaceAll("'", '&#039;');

  async function requestJson(url, options = {}) {
    const response = await fetch(url, {
      ...options,
      headers: {
        Accept: 'application/json',
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...(options.method && options.method !== 'GET' && token ? { RequestVerificationToken: token } : {}),
        ...options.headers
      }
    });
    const payload = await response.json().catch(() => null);
    if (!response.ok) throw new Error(payload?.message || 'Không thể kết nối tới máy chủ.');
    return payload;
  }

  async function changeMembership(method) {
    try {
      const result = await requestJson(`/api/groups/${encodeURIComponent(groupId)}/join`, { method });
      window.showToast(result.message, result.status === 'Active' ? 'success' : 'info');
      window.setTimeout(() => method === 'DELETE' ? location.assign('/Groups') : location.reload(), 350);
    } catch (error) {
      window.showToast(error.message, 'error');
    }
  }

  document.getElementById('join-group-btn')?.addEventListener('click', event => {
    event.currentTarget.disabled = true;
    changeMembership('POST');
  });
  document.getElementById('withdraw-request-btn')?.addEventListener('click', event => {
    event.currentTarget.disabled = true;
    changeMembership('DELETE');
  });
  document.getElementById('leave-group-btn')?.addEventListener('click', event => {
    if (!window.confirm('Bạn có chắc muốn rời nhóm học tập này?')) return;
    event.currentTarget.disabled = true;
    changeMembership('DELETE');
  });

  if (isOwner) {
    const editModal = document.getElementById('editGroupModal');
    const formatSelect = document.getElementById('edit-group-format');
    const accessSelect = document.getElementById('edit-group-access');
    const subjectPicker = window.createSubjectMultiSelect({
      root: document.getElementById('edit-group-subject-select'),
      maxSelections: 10
    });
    let subjectOptionsLoaded = false;

    formatSelect.value = formatSelect.dataset.currentValue;
    accessSelect.value = accessSelect.dataset.currentValue;

    async function loadEditSubjectOptions() {
      if (subjectOptionsLoaded) return;
      try {
        subjectPicker.setOptions(await requestJson('/api/groups/subjects'));
        subjectOptionsLoaded = true;
      } catch (error) {
        window.showToast(error.message, 'error');
      }
    }

    editModal.addEventListener('show.bs.modal', loadEditSubjectOptions);

    document.getElementById('save-group-changes-btn').addEventListener('click', async event => {
      const button = event.currentTarget;
      const subjects = subjectPicker.getSelectedNames();
      const name = document.getElementById('edit-group-name').value.trim();
      if (!name) return window.showToast('Tên nhóm là bắt buộc.', 'error');
      if (!subjects.length) return window.showToast('Hãy chọn hoặc thêm ít nhất một môn học.', 'error');
      if (subjects.length > 10) return window.showToast('Mỗi nhóm được chọn tối đa 10 môn học.', 'error');

      const access = accessSelect.value;
      const payload = {
        name,
        subjects,
        description: document.getElementById('edit-group-desc').value.trim(),
        goal: document.getElementById('edit-group-goal').value.trim(),
        meetingFormat: formatSelect.value,
        meetingSchedule: document.getElementById('edit-group-schedule').value.trim(),
        rules: document.getElementById('edit-group-rules').value.trim(),
        isPublic: access !== 'InviteOnly',
        joinMode: access,
        maxMembers: document.getElementById('edit-group-max-members').value.trim()
          ? Number(document.getElementById('edit-group-max-members').value)
          : null
      };

      button.disabled = true;
      try {
        await requestJson(`/api/groups/${encodeURIComponent(groupId)}`, {
          method: 'PUT',
          body: JSON.stringify(payload)
        });
        window.showToast('Đã cập nhật nhóm học tập.', 'success');
        window.setTimeout(() => location.reload(), 300);
      } catch (error) {
        button.disabled = false;
        window.showToast(error.message, 'error');
      }
    });
  }

  if (!isMember) return;

  const navButtons = document.querySelectorAll('.group-channel-nav [data-panel]');
  const panels = document.querySelectorAll('[data-panel-content]');
  function showPanel(name) {
    navButtons.forEach(button => button.classList.toggle('active', button.dataset.panel === name));
    panels.forEach(panel => {
      const active = panel.dataset.panelContent === name;
      panel.hidden = !active;
      panel.classList.toggle('active', active);
    });
    if (name === 'chat') scrollChat();
  }
  navButtons.forEach(button => button.addEventListener('click', () => showPanel(button.dataset.panel)));

  document.getElementById('announcement-form')?.addEventListener('submit', async event => {
    event.preventDefault();
    const content = document.getElementById('announcement-content').value.trim();
    if (!content) return;
    const button = event.currentTarget.querySelector('button[type="submit"]');
    button.disabled = true;
    try {
      await requestJson(`/api/groups/${encodeURIComponent(groupId)}/announcements`, {
        method: 'POST',
        body: JSON.stringify({ content, isPinned: document.getElementById('announcement-pinned').checked })
      });
      window.showToast('Đã đăng thông báo.', 'success');
      location.reload();
    } catch (error) {
      button.disabled = false;
      window.showToast(error.message, 'error');
    }
  });

  document.querySelector('.group-member-list')?.addEventListener('click', async event => {
    const button = event.target.closest('.member-action');
    if (!button) return;
    const row = button.closest('[data-member-id]');
    button.disabled = true;
    try {
      const result = await requestJson(`/api/groups/${encodeURIComponent(groupId)}/members/${encodeURIComponent(row.dataset.memberId)}`, {
        method: 'POST', body: JSON.stringify({ action: button.dataset.action })
      });
      window.showToast(result.message, 'success');
      location.reload();
    } catch (error) {
      button.disabled = false;
      window.showToast(error.message, 'error');
    }
  });

  document.getElementById('create-invite-btn')?.addEventListener('click', async event => {
    const button = event.currentTarget;
    button.disabled = true;
    try {
      const invite = await requestJson(`/api/groups/${encodeURIComponent(groupId)}/invites`, {
        method: 'POST', body: JSON.stringify({ expiresInDays: 7, maxUses: 20 })
      });
      const url = `${location.origin}/Groups/Invite/${invite.inviteCode}`;
      document.getElementById('invite-result-url').value = url;
      document.getElementById('invite-result-meta').textContent = `Hết hạn ${new Date(invite.expiresAt).toLocaleString('vi-VN')} · tối đa ${invite.maxUses} lượt`;
      document.getElementById('invite-result').hidden = false;
      window.showToast('Đã tạo lời mời.', 'success');
    } catch (error) {
      window.showToast(error.message, 'error');
    } finally {
      button.disabled = false;
    }
  });

  document.getElementById('copy-invite-btn')?.addEventListener('click', async () => {
    const input = document.getElementById('invite-result-url');
    try {
      await navigator.clipboard.writeText(input.value);
      window.showToast('Đã sao chép liên kết mời.', 'success');
    } catch {
      input.select();
      document.execCommand('copy');
    }
  });

  const messages = document.getElementById('group-chat-messages');
  const chatForm = document.getElementById('group-chat-form');
  const chatInput = document.getElementById('group-chat-input');
  const connectionStatus = document.getElementById('chat-connection-status');
  let connection = null;
  let fallbackTimer = null;

  function formatTime(value) {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : new Intl.DateTimeFormat('vi-VN', {
      day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
    }).format(date);
  }

  function appendMessage(message) {
    if (messages.querySelector(`[data-message-id="${CSS.escape(String(message.id))}"]`)) return;
    document.getElementById('chat-empty-state')?.remove();
    const row = document.createElement('div');
    row.className = `group-channel-message ${message.mine ? 'mine' : ''}`;
    row.dataset.messageId = message.id;
    const avatar = message.authorAvatarUrl
      ? `<img src="${escapeHtml(message.authorAvatarUrl)}" alt="" referrerpolicy="no-referrer">`
      : `<span class="group-avatar">${escapeHtml((message.authorName || '?').trim().charAt(0).toUpperCase())}</span>`;
    row.innerHTML = `${avatar}<div><div><strong>${escapeHtml(message.authorName)}</strong><time>${formatTime(message.sentAt)}</time></div><p>${escapeHtml(message.content).replaceAll('\n', '<br>')}</p></div>`;
    messages.appendChild(row);
    scrollChat();
  }

  function scrollChat() {
    if (messages) messages.scrollTop = messages.scrollHeight;
  }

  async function refreshMessages() {
    try {
      const group = await requestJson(`/api/groups/${encodeURIComponent(groupId)}`);
      group.messages.forEach(appendMessage);
    } catch { /* A reconnect attempt will try again. */ }
  }

  async function connectRealtime() {
    if (!window.signalR) {
      connectionStatus.textContent = 'Chế độ dự phòng';
      fallbackTimer = setInterval(refreshMessages, 10000);
      return;
    }
    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/study-groups')
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();
    connection.on('MessageReceived', appendMessage);
    connection.onreconnecting(() => { connectionStatus.textContent = 'Đang kết nối lại...'; });
    connection.onreconnected(async () => {
      connectionStatus.textContent = 'Trực tuyến';
      await connection.invoke('EnterGroup', groupId);
      await refreshMessages();
    });
    connection.onclose(() => {
      connectionStatus.textContent = 'Mất kết nối';
      fallbackTimer ??= setInterval(refreshMessages, 10000);
    });
    try {
      await connection.start();
      await connection.invoke('EnterGroup', groupId);
      connectionStatus.textContent = 'Trực tuyến';
    } catch {
      connectionStatus.textContent = 'Chế độ dự phòng';
      fallbackTimer = setInterval(refreshMessages, 10000);
    }
  }

  chatForm?.addEventListener('submit', async event => {
    event.preventDefault();
    const content = chatInput.value.trim();
    if (!content) return;
    const button = chatForm.querySelector('button[type="submit"]');
    button.disabled = true;
    try {
      if (connection?.state === signalR.HubConnectionState.Connected) {
        await connection.invoke('SendMessage', groupId, content);
      } else {
        appendMessage(await requestJson(`/api/groups/${encodeURIComponent(groupId)}/messages`, {
          method: 'POST', body: JSON.stringify({ content })
        }));
      }
      chatInput.value = '';
    } catch (error) {
      window.showToast(error.message || 'Không gửi được tin nhắn.', 'error');
    } finally {
      button.disabled = false;
      chatInput.focus();
    }
  });

  chatInput?.addEventListener('keydown', event => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      chatForm.requestSubmit();
    }
  });

  showPanel('overview');
  scrollChat();
  connectRealtime();
})();
