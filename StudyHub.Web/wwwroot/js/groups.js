(() => {
  const groupsView = document.getElementById('groups-view');
  if (!groupsView) return;

  const apiUrl = '/api/groups';
  const searchForm = document.getElementById('group-search-form');
  const searchInput = document.getElementById('group-search-input');
  const clearSearchButton = document.getElementById('clear-group-search');
  const loadingElement = document.getElementById('groups-loading');
  const summaryElement = document.getElementById('groups-summary');
  let activeTabId = 'my-groups';
  let searchTimer;
  let loadSequence = 0;

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
    const payload = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) throw new Error(payload?.message || 'Không thể kết nối tới máy chủ.');
    return payload;
  }

  function createGroupCard(group) {
    const column = document.createElement('div');
    column.className = 'col-12 col-sm-6 col-xl-4';
    column.innerHTML = `
      <article class="sh-card p-4 h-100 d-flex flex-column" data-group-card data-group-id="${escapeHtml(group.id)}">
        <div class="d-flex align-items-center gap-2 mb-2">
          <span class="subject-badge ${escapeHtml(group.subjectCssClass)}">${escapeHtml(group.subject)}</span>
          <span style="font-size:10px;color:var(--sh-slate-400);">${group.memberCount} thành viên</span>
        </div>
        <h3 class="fw-bold mb-1" style="font-size:14px;">${escapeHtml(group.name)}</h3>
        <p class="mb-3" style="font-size:11px;color:var(--sh-slate-500);flex-grow:1;">${escapeHtml(group.description || 'Chưa có mô tả.')}</p>
        <div class="d-flex gap-2 mt-auto">
          ${group.isMember ? `
            <button type="button" class="open-group-btn btn-primary-sh flex-grow-1 justify-content-center" data-group-id="${escapeHtml(group.id)}">
              <i class="bi bi-arrow-right-circle"></i>Vào nhóm
            </button>
            <button type="button" class="join-group-btn btn-secondary-sh" data-group-id="${escapeHtml(group.id)}" data-member="true" ${group.isOwner ? 'disabled' : ''}>
              ${group.isOwner ? 'Trưởng nhóm' : 'Rời nhóm'}
            </button>` : `
            <button type="button" class="join-group-btn btn-primary-sh flex-grow-1 justify-content-center" data-group-id="${escapeHtml(group.id)}" data-member="false">
              Tham gia
            </button>`}
        </div>
      </article>`;
    return column;
  }

  function renderSection(sectionId, groups) {
    const section = document.getElementById(sectionId);
    const row = section.querySelector('.row');
    const emptyState = section.querySelector('.groups-empty');
    row.replaceChildren(...groups.map(createGroupCard));
    emptyState.hidden = groups.length > 0;
  }

  function showTab(tabId) {
    activeTabId = tabId;
    for (const button of document.querySelectorAll('.group-tab')) {
      const isActive = button.dataset.tabTarget === tabId;
      button.classList.toggle('active', isActive);
      button.classList.toggle('fw-bold', isActive);
      button.classList.toggle('fw-semibold', !isActive);
      button.style.color = isActive ? 'var(--sh-indigo-600)' : 'var(--sh-slate-500)';
      button.style.borderBottom = isActive ? '2px solid var(--sh-indigo-600)' : '0';
    }
    document.getElementById('my-groups').hidden = tabId !== 'my-groups';
    document.getElementById('discover-groups').hidden = tabId !== 'discover-groups';
  }

  function updateAddressBar(keyword) {
    const url = new URL(window.location.href);
    if (keyword) url.searchParams.set('q', keyword);
    else url.searchParams.delete('q');
    window.history.replaceState(null, '', url);
  }

  async function loadGroups() {
    const sequence = ++loadSequence;
    const keyword = searchInput.value.trim();
    const endpoint = keyword ? `${apiUrl}?search=${encodeURIComponent(keyword)}` : apiUrl;
    loadingElement.hidden = false;
    summaryElement.textContent = keyword ? `Đang tìm nhóm phù hợp với “${keyword}”...` : 'Đang tải dữ liệu nhóm...';
    clearSearchButton.hidden = !keyword;

    try {
      const groups = await requestJson(endpoint);
      if (sequence !== loadSequence) return;
      const memberGroups = groups.filter(group => group.isMember);
      const discoverGroups = groups.filter(group => !group.isMember && group.isPublic);
      renderSection('my-groups', memberGroups);
      renderSection('discover-groups', discoverGroups);
      document.getElementById('my-groups-tab').textContent = `Nhóm của tôi (${memberGroups.length})`;
      document.getElementById('discover-groups-tab').textContent = `Khám phá nhóm (${discoverGroups.length})`;
      const resultCount = memberGroups.length + discoverGroups.length;
      summaryElement.textContent = keyword
        ? `${resultCount} kết quả cho “${keyword}” · Dữ liệu từ SQL Server`
        : `${memberGroups.length} nhóm đang tham gia · Dữ liệu từ SQL Server`;
      updateAddressBar(keyword);
    } catch (error) {
      if (sequence !== loadSequence) return;
      renderSection('my-groups', []);
      renderSection('discover-groups', []);
      summaryElement.textContent = error.message;
      window.showToast(error.message, 'error');
    } finally {
      if (sequence === loadSequence) loadingElement.hidden = true;
      showTab(activeTabId);
    }
  }

  document.querySelectorAll('.group-tab').forEach(button => {
    button.addEventListener('click', () => showTab(button.dataset.tabTarget));
  });

  searchForm.addEventListener('submit', event => {
    event.preventDefault();
    clearTimeout(searchTimer);
    loadGroups();
  });
  searchInput.addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = window.setTimeout(loadGroups, 350);
  });
  clearSearchButton.addEventListener('click', () => {
    searchInput.value = '';
    searchInput.focus();
    loadGroups();
  });

  groupsView.addEventListener('click', async event => {
    const openButton = event.target.closest('.open-group-btn');
    if (openButton) {
      window.location.assign(`/Groups/${encodeURIComponent(openButton.dataset.groupId)}`);
      return;
    }

    const joinButton = event.target.closest('.join-group-btn');
    if (!joinButton || joinButton.disabled) return;
    joinButton.disabled = true;
    try {
      const isMember = joinButton.dataset.member === 'true';
      await requestJson(`${apiUrl}/${encodeURIComponent(joinButton.dataset.groupId)}/join`, {
        method: isMember ? 'DELETE' : 'POST'
      });
      window.showToast(isMember ? 'Bạn đã rời nhóm.' : 'Tham gia nhóm thành công!', isMember ? 'info' : 'success');
      await loadGroups();
    } catch (error) {
      joinButton.disabled = false;
      window.showToast(error.message, 'error');
    }
  });

  const createButton = document.getElementById('create-group-btn');
  createButton.addEventListener('click', async () => {
    const nameInput = document.getElementById('group-name');
    const subjectInput = document.getElementById('group-subject');
    const descriptionInput = document.getElementById('group-desc');
    const publicInput = document.getElementById('group-public');
    const name = nameInput.value.trim();
    if (!name) {
      window.showToast('Bạn hãy nhập tên nhóm.', 'error');
      nameInput.focus();
      return;
    }

    createButton.disabled = true;
    try {
      await requestJson(apiUrl, {
        method: 'POST',
        body: JSON.stringify({ name, subject: subjectInput.value, description: descriptionInput.value, isPublic: publicInput.checked })
      });
      bootstrap.Modal.getInstance(document.getElementById('newGroupModal'))?.hide();
      nameInput.value = '';
      descriptionInput.value = '';
      searchInput.value = '';
      showTab('my-groups');
      await loadGroups();
      window.showToast('Đã tạo nhóm học tập thành công!', 'success');
    } catch (error) {
      window.showToast(error.message, 'error');
    } finally {
      createButton.disabled = false;
    }
  });

  searchInput.value = groupsView.dataset.initialSearch || searchInput.value;
  showTab(activeTabId);
  loadGroups();
})();
