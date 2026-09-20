(() => {
  const page = document.getElementById('group-details-page');
  if (!page) return;

  const groupId = page.dataset.groupId;
  const launcher = document.getElementById('group-chat-launcher');
  const chatbox = document.getElementById('group-chatbox');
  const closeButton = document.getElementById('group-chat-close');
  const messagesContainer = document.getElementById('group-chatbox-messages');
  const form = document.getElementById('group-chatbox-form');
  const input = document.getElementById('group-chatbox-input');
  let pollTimer = null;

  function escapeHtml(value) {
    return String(value ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  async function requestJson(url, options = {}) {
    const response = await fetch(url, {
      ...options,
      headers: {
        Accept: 'application/json',
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...options.headers
      }
    });
    const payload = await response.json().catch(() => null);
    if (!response.ok) throw new Error(payload?.message || 'Không thể kết nối tới máy chủ.');
    return payload;
  }

  function formatTime(value) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return new Intl.DateTimeFormat('vi-VN', {
      day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
    }).format(date);
  }

  function renderMessages(messages) {
    if (!messages?.length) {
      messagesContainer.innerHTML = '<div class="group-chat-empty"><i class="bi bi-chat-dots"></i><p>Chưa có tin nhắn. Hãy bắt đầu cuộc trò chuyện!</p></div>';
      return;
    }

    messagesContainer.innerHTML = messages.map(message => `
      <div class="group-chat-row ${message.mine ? 'mine' : ''}">
        <div class="group-chat-bubble">
          ${message.mine ? '' : `<strong>${escapeHtml(message.authorName)}</strong>`}
          <div>${escapeHtml(message.content).replaceAll('\n', '<br>')}</div>
          <time>${formatTime(message.sentAt)}</time>
        </div>
      </div>`).join('');
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
  }

  async function loadMessages(showError = false) {
    try {
      const group = await requestJson(`/api/groups/${encodeURIComponent(groupId)}`);
      renderMessages(group.messages);
    } catch (error) {
      if (showError) window.showToast(error.message, 'error');
    }
  }

  function openChatbox() {
    chatbox.hidden = false;
    launcher.setAttribute('aria-expanded', 'true');
    launcher.classList.add('is-open');
    loadMessages(true);
    clearInterval(pollTimer);
    pollTimer = setInterval(() => loadMessages(false), 4000);
    window.setTimeout(() => input.focus(), 50);
  }

  function closeChatbox() {
    chatbox.hidden = true;
    launcher.setAttribute('aria-expanded', 'false');
    launcher.classList.remove('is-open');
    clearInterval(pollTimer);
    pollTimer = null;
    launcher.focus();
  }

  launcher.addEventListener('click', () => chatbox.hidden ? openChatbox() : closeChatbox());
  closeButton.addEventListener('click', closeChatbox);

  form.addEventListener('submit', async event => {
    event.preventDefault();
    const content = input.value.trim();
    if (!content) return;

    const submitButton = form.querySelector('button[type="submit"]');
    submitButton.disabled = true;
    try {
      await requestJson(`/api/groups/${encodeURIComponent(groupId)}/messages`, {
        method: 'POST',
        body: JSON.stringify({ content })
      });
      input.value = '';
      await loadMessages(true);
    } catch (error) {
      window.showToast(error.message, 'error');
    } finally {
      submitButton.disabled = false;
      input.focus();
    }
  });

  input.addEventListener('keydown', event => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      form.requestSubmit();
    }
  });

  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && !chatbox.hidden) closeChatbox();
  });
})();
