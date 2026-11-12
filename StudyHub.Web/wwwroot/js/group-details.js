(() => {
  const page = document.getElementById('group-details-page');
  if (!page || page.dataset.isMember !== 'true') return;
  const groupId = page.dataset.groupId;
  const messages = document.getElementById('group-chat-messages');
  const connectionStatus = document.getElementById('chat-connection-status');
  const escapeHtml = value => String(value ?? '')
    .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;').replaceAll("'", '&#039;');

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
    row.className = `group-channel-message ${String(message.authorUserId) === page.dataset.currentUserId ? 'mine' : ''}`;
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


  document.getElementById('copy-invite-btn')?.addEventListener('click', async () => {
    const input = document.getElementById('invite-result-url');
    try { await navigator.clipboard.writeText(input.value); window.showToast('Đã sao chép liên kết mời.', 'success'); }
    catch { input.select(); }
  });
  const requestedPanel = new URL(location.href).searchParams.get('panel');
  showPanel([...panels].some(panel => panel.dataset.panelContent === requestedPanel) ? requestedPanel : 'overview');
  scrollChat();
  connectionStatus.textContent = 'Tải lại trang để xem tin mới';
  if (!window.signalR) return;
  const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/study-groups').withAutomaticReconnect().build();
  connection.on('MessageReceived', appendMessage);
  connection.onreconnecting(() => { connectionStatus.textContent = 'Đang kết nối lại...'; });
  connection.onclose(() => { connectionStatus.textContent = 'Tải lại trang để xem tin mới'; });
  const enter = async () => {
    try { await connection.invoke('EnterGroup', groupId); connectionStatus.textContent = 'Trực tuyến'; }
    catch { connectionStatus.textContent = 'Tải lại trang để xem tin mới'; }
  };
  connection.onreconnected(enter);
  connection.start().then(enter).catch(() => { connectionStatus.textContent = 'Tải lại trang để xem tin mới'; });
})();
