(() => {
  const groupsView = document.getElementById('groups-view');
  if (!groupsView) return;

  const apiUrl = '/api/groups';

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
    if (!response.ok) {
      throw new Error(payload?.message || 'Không thể kết nối tới máy chủ.');
    }

    return payload;
  }

  function findGroupCard(groupId) {
    return [...document.querySelectorAll('[data-group-card]')]
      .find(card => card.dataset.groupId === groupId);
  }

  function setMembership(card, group) {
    if (!card) return;

    card.dataset.groupName = group.name;
    card.dataset.groupSubject = group.subject;
    card.dataset.groupSubjectClass = group.subjectCssClass;
    card.dataset.groupMembers = group.memberCount;
    card.dataset.groupDescription = group.description;

    const joinButton = card.querySelector('.join-group-btn');
    const openButton = card.querySelector('.open-group-btn');
    if (joinButton) {
      joinButton.dataset.member = String(group.isMember);
      joinButton.dataset.groupId = group.id;
      joinButton.disabled = group.isOwner;
      joinButton.className = `join-group-btn ${group.isMember ? 'btn-secondary-sh' : 'btn-primary-sh'}${joinButton.classList.contains('flex-grow-1') ? ' flex-grow-1 justify-content-center' : ''}`;
      joinButton.textContent = group.isOwner ? 'Trưởng nhóm' : (group.isMember ? 'Rời nhóm' : 'Tham gia');
    }
    if (openButton) {
      openButton.dataset.groupId = group.id;
      openButton.style.display = group.isMember ? 'inline-flex' : 'none';
    }
  }

  function createGroupCard(group) {
    const column = document.createElement('div');
    column.className = 'col-12 col-sm-6 col-xl-4';
    column.innerHTML = `
      <div class="sh-card p-4 h-100 d-flex flex-column" data-group-card data-group-id="${escapeHtml(group.id)}">
        <div class="d-flex align-items-center gap-2 mb-2">
          <span class="subject-badge ${escapeHtml(group.subjectCssClass)}">${escapeHtml(group.subject)}</span>
          <span class="group-member-count" style="font-size:10px;color:var(--sh-slate-400);">${group.memberCount} thành viên</span>
        </div>
        <h3 class="fw-bold mb-1" style="font-size:14px;">${escapeHtml(group.name)}</h3>
        <p class="mb-3" style="font-size:11px;color:var(--sh-slate-500);flex-grow:1;">${escapeHtml(group.description)}</p>
        <div class="group-card-actions d-flex gap-2 mt-auto">
          <button type="button" class="join-group-btn btn-secondary-sh flex-grow-1 justify-content-center"></button>
          <button type="button" class="open-group-btn btn-primary-sh flex-grow-1 justify-content-center">
            <i class="bi bi-arrow-right-circle"></i>Vào nhóm
          </button>
        </div>
      </div>`;

    const target = group.isMember
      ? document.querySelector('#my-groups > .row')
      : document.querySelector('#discover-groups > .row');
    target?.appendChild(column);
    const card = column.querySelector('[data-group-card]');
    setMembership(card, group);
    return card;
  }

  async function loadGroups() {
    const groups = await requestJson(apiUrl);
    const serverIds = new Set(groups.map(group => group.id));
    document.querySelectorAll('[data-group-card]').forEach(card => {
      if (!serverIds.has(card.dataset.groupId)) {
        (card.closest('[class*="col-"]') || card).remove();
      }
    });

    for (const group of groups) {
      const card = findGroupCard(group.id) || createGroupCard(group);
      setMembership(card, group);
    }

    const memberCount = groups.filter(group => group.isMember).length;
    const discoverCount = groups.filter(group => !group.isMember && group.isPublic).length;
    const summary = document.getElementById('my-groups-summary');
    const memberTab = document.getElementById('my-groups-tab');
    const discoverTab = document.getElementById('discover-groups-tab');
    if (summary) summary.textContent = `${memberCount} nhóm đang tham gia · Dữ liệu từ SQL Server`;
    if (memberTab) memberTab.textContent = `Nhóm của tôi (${memberCount})`;
    if (discoverTab) discoverTab.textContent = `Khám phá nhóm (${discoverCount})`;
  }

  groupsView.addEventListener('click', async event => {
    const joinButton = event.target.closest('.join-group-btn');
    const openButton = event.target.closest('.open-group-btn');

    if (joinButton && !joinButton.disabled) {
      const card = joinButton.closest('[data-group-card]');
      const groupId = card?.dataset.groupId || joinButton.dataset.groupId;
      if (!groupId) return;

      joinButton.disabled = true;
      try {
        const isMember = joinButton.dataset.member === 'true';
        const group = await requestJson(`${apiUrl}/${encodeURIComponent(groupId)}/join`, {
          method: isMember ? 'DELETE' : 'POST'
        });
        setMembership(card, group);
        window.showToast(isMember ? 'Bạn đã rời nhóm.' : 'Tham gia nhóm thành công! Bấm “Vào nhóm” để xem chi tiết.', isMember ? 'info' : 'success');
      } catch (error) {
        window.showToast(error.message, 'error');
      } finally {
        if (!joinButton.textContent.includes('Trưởng nhóm')) joinButton.disabled = false;
      }
      return;
    }

    if (openButton) {
      const groupId = openButton.dataset.groupId || openButton.closest('[data-group-card]')?.dataset.groupId;
      if (!groupId) return;
      window.location.assign(`/Groups/${encodeURIComponent(groupId)}`);
    }
  });

  const createButton = document.getElementById('create-group-btn');
  createButton?.addEventListener('click', async () => {
    const nameInput = document.getElementById('group-name');
    const subjectInput = document.getElementById('group-subject');
    const descriptionInput = document.getElementById('group-desc');
    const publicInput = document.getElementById('group-public');
    const name = nameInput?.value.trim();

    if (!name) {
      window.showToast('Bạn hãy nhập tên nhóm.', 'error');
      nameInput?.focus();
      return;
    }

    createButton.disabled = true;
    try {
      const group = await requestJson(apiUrl, {
        method: 'POST',
        body: JSON.stringify({
          name,
          subject: subjectInput?.value,
          description: descriptionInput?.value,
          isPublic: publicInput?.checked ?? true
        })
      });
      createGroupCard(group);
      bootstrap.Modal.getInstance(document.getElementById('newGroupModal'))?.hide();
      nameInput.value = '';
      descriptionInput.value = '';
      window.showToast('Đã tạo nhóm học tập thành công!', 'success');
    } catch (error) {
      window.showToast(error.message, 'error');
    } finally {
      createButton.disabled = false;
    }
  });

  loadGroups().catch(error => window.showToast(error.message, 'error'));
})();
