(() => {
  const view = document.getElementById('groups-view');
  if (!view) return;

  const apiUrl = '/api/groups';
  const token = document.querySelector('#groups-antiforgery input[name="__RequestVerificationToken"]')?.value;
  const searchForm = document.getElementById('group-search-form');
  const searchInput = document.getElementById('group-search-input');
  const clearSearchButton = document.getElementById('clear-group-search');
  const loadingElement = document.getElementById('groups-loading');
  const summaryElement = document.getElementById('groups-summary');
  let activeTabId = 'my-groups';
  let searchTimer;
  let loadSequence = 0;

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

  function accessLabel(group) {
    if (!group.isPublic || group.joinMode === 'InviteOnly') return 'Chỉ qua lời mời';
    return group.joinMode === 'Open' ? 'Tham gia ngay' : 'Cần trưởng nhóm duyệt';
  }

  function createGroupCard(group) {
    const column = document.createElement('div');
    column.className = 'col-12 col-md-6 col-xl-4';
    const isPending = group.membershipStatus === 'Pending';
    const capacity = `${group.memberCount}/${group.maxMembers}`;
    let actions;
    if (group.isMember) {
      actions = `<button type="button" class="open-group-btn btn-primary-sh flex-grow-1 justify-content-center" data-group-id="${escapeHtml(group.id)}"><i class="bi bi-box-arrow-in-right"></i>Vào nhóm</button>
        <button type="button" class="membership-btn btn-secondary-sh" data-group-id="${escapeHtml(group.id)}" data-action="leave" ${group.isOwner ? 'disabled' : ''}>${group.isOwner ? 'Trưởng nhóm' : 'Rời nhóm'}</button>`;
    } else if (isPending) {
      actions = `<button type="button" class="open-group-btn btn-secondary-sh flex-grow-1 justify-content-center" data-group-id="${escapeHtml(group.id)}"><i class="bi bi-eye"></i>Xem</button>
        <button type="button" class="membership-btn btn-secondary-sh" data-group-id="${escapeHtml(group.id)}" data-action="withdraw">Rút yêu cầu</button>`;
    } else {
      const inviteOnly = group.joinMode === 'InviteOnly';
      actions = `<button type="button" class="open-group-btn btn-secondary-sh" data-group-id="${escapeHtml(group.id)}"><i class="bi bi-eye"></i>Xem</button>
        <button type="button" class="membership-btn btn-primary-sh flex-grow-1 justify-content-center" data-group-id="${escapeHtml(group.id)}" data-action="join" ${inviteOnly ? 'disabled' : ''}>${inviteOnly ? 'Cần mã mời' : (group.joinMode === 'Open' ? 'Tham gia ngay' : 'Gửi yêu cầu')}</button>`;
    }

    column.innerHTML = `
      <article class="sh-card group-discord-card p-4 h-100 d-flex flex-column" data-group-id="${escapeHtml(group.id)}">
        <div class="d-flex align-items-center justify-content-between gap-2 mb-3">
          <span class="subject-badge ${escapeHtml(group.subjectCssClass)}">${escapeHtml(group.subject)}</span>
          <span class="group-access-badge"><i class="bi ${group.isPublic ? 'bi-globe2' : 'bi-lock-fill'}"></i>${escapeHtml(accessLabel(group))}</span>
        </div>
        <h3 class="fw-bold mb-1" style="font-size:15px;">${escapeHtml(group.name)}</h3>
        <p class="mb-2 line-clamp-2" style="font-size:11px;color:var(--sh-slate-500);">${escapeHtml(group.description || 'Chưa có mô tả.')}</p>
        ${group.goal ? `<div class="group-card-meta"><i class="bi bi-bullseye"></i><span>${escapeHtml(group.goal)}</span></div>` : ''}
        <div class="group-card-meta"><i class="bi bi-camera-video"></i><span>${escapeHtml(group.meetingFormat)}${group.meetingSchedule ? ` · ${escapeHtml(group.meetingSchedule)}` : ''}</span></div>
        <div class="d-flex align-items-center justify-content-between mt-3 mb-3">
          <span style="font-size:11px;color:var(--sh-slate-500);"><i class="bi bi-people me-1"></i>${capacity} thành viên</span>
          ${isPending ? '<span class="badge text-bg-warning">Đang chờ duyệt</span>' : group.pendingMemberCount > 0 ? `<span class="badge text-bg-warning">${group.pendingMemberCount} yêu cầu mới</span>` : ''}
        </div>
        <div class="d-flex gap-2 mt-auto">${actions}</div>
      </article>`;
    return column;
  }

  function renderSection(id, groups) {
    const section = document.getElementById(id);
    section.querySelector('.row').replaceChildren(...groups.map(createGroupCard));
    section.querySelector('.groups-empty').hidden = groups.length > 0;
  }

  function showTab(id) {
    activeTabId = id;
    document.querySelectorAll('.group-tab').forEach(button => button.classList.toggle('active', button.dataset.tabTarget === id));
    document.querySelectorAll('.group-list-section').forEach(section => { section.hidden = section.id !== id; });
  }

  async function loadGroups() {
    const sequence = ++loadSequence;
    const keyword = searchInput.value.trim();
    loadingElement.hidden = false;
    clearSearchButton.hidden = !keyword;
    try {
      const groups = await requestJson(keyword ? `${apiUrl}?search=${encodeURIComponent(keyword)}` : apiUrl);
      if (sequence !== loadSequence) return;
      const mine = groups.filter(group => group.isMember);
      const pending = groups.filter(group => group.membershipStatus === 'Pending');
      const discover = groups.filter(group => !group.isMember && group.membershipStatus !== 'Pending' && group.isPublic);
      renderSection('my-groups', mine);
      renderSection('pending-groups', pending);
      renderSection('discover-groups', discover);
      const counts = { 'my-groups': mine.length, 'pending-groups': pending.length, 'discover-groups': discover.length };
      document.querySelectorAll('.group-tab').forEach(button => {
        const base = button.dataset.tabTarget === 'my-groups' ? 'Nhóm của tôi' : button.dataset.tabTarget === 'pending-groups' ? 'Đang chờ duyệt' : 'Khám phá';
        button.textContent = `${base} (${counts[button.dataset.tabTarget]})`;
      });
      summaryElement.textContent = `${mine.length} nhóm đang tham gia · ${pending.length} yêu cầu đang chờ`;
      const url = new URL(location.href);
      keyword ? url.searchParams.set('q', keyword) : url.searchParams.delete('q');
      history.replaceState(null, '', url);
    } catch (error) {
      summaryElement.textContent = error.message;
      window.showToast(error.message, 'error');
    } finally {
      if (sequence === loadSequence) loadingElement.hidden = true;
      showTab(activeTabId);
    }
  }

  document.querySelectorAll('.group-tab').forEach(button => button.addEventListener('click', () => showTab(button.dataset.tabTarget)));
  searchForm.addEventListener('submit', event => { event.preventDefault(); clearTimeout(searchTimer); loadGroups(); });
  searchInput.addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(loadGroups, 350); });
  clearSearchButton.addEventListener('click', () => { searchInput.value = ''; loadGroups(); });

  view.addEventListener('click', async event => {
    const openButton = event.target.closest('.open-group-btn');
    if (openButton) return location.assign(`/Groups/${encodeURIComponent(openButton.dataset.groupId)}`);
    const button = event.target.closest('.membership-btn');
    if (!button || button.disabled) return;
    button.disabled = true;
    try {
      const leave = button.dataset.action !== 'join';
      const result = await requestJson(`${apiUrl}/${encodeURIComponent(button.dataset.groupId)}/join`, { method: leave ? 'DELETE' : 'POST' });
      window.showToast(result.message, result.status === 'Active' ? 'success' : 'info');
      await loadGroups();
    } catch (error) {
      button.disabled = false;
      window.showToast(error.message, 'error');
    }
  });

  document.getElementById('create-group-btn').addEventListener('click', async event => {
    const button = event.currentTarget;
    const access = document.getElementById('group-access').value;
    const payload = {
      name: document.getElementById('group-name').value.trim(),
      subject: document.getElementById('group-subject').value,
      description: document.getElementById('group-desc').value.trim(),
      goal: document.getElementById('group-goal').value.trim(),
      meetingFormat: document.getElementById('group-format').value,
      meetingSchedule: document.getElementById('group-schedule').value.trim(),
      contactUrl: document.getElementById('group-contact').value.trim(),
      rules: document.getElementById('group-rules').value.trim(),
      isPublic: access !== 'InviteOnly',
      joinMode: access,
      maxMembers: Number(document.getElementById('group-max-members').value)
    };
    button.disabled = true;
    try {
      const group = await requestJson(apiUrl, { method: 'POST', body: JSON.stringify(payload) });
      location.assign(`/Groups/${encodeURIComponent(group.id)}`);
    } catch (error) {
      button.disabled = false;
      window.showToast(error.message, 'error');
    }
  });

  document.getElementById('open-invite-btn').addEventListener('click', () => {
    const raw = document.getElementById('invite-code-input').value.trim();
    const code = raw.split('/').filter(Boolean).at(-1)?.toLowerCase();
    if (!code || !/^[a-f0-9]{1,64}$/.test(code)) return window.showToast('Mã lời mời không hợp lệ.', 'error');
    location.assign(`/Groups/Invite/${encodeURIComponent(code)}`);
  });

  searchInput.value = view.dataset.initialSearch || '';
  showTab(activeTabId);
  loadGroups();
})();
