(() => {
  const tabs = document.querySelectorAll('.group-tab');
  const sections = document.querySelectorAll('.group-list-section');
  function showTab(id) {
    tabs.forEach(button => button.classList.toggle('active', button.dataset.tabTarget === id));
    sections.forEach(section => { section.hidden = section.id !== id; });
  }
  tabs.forEach(button => button.addEventListener('click', () => showTab(button.dataset.tabTarget)));
  showTab('my-groups');
  document.getElementById('open-invite-btn')?.addEventListener('click', () => {
    const raw = document.getElementById('invite-code-input').value.trim();
    const code = raw.split('/').filter(Boolean).at(-1)?.toLowerCase();
    if (!code || !/^[a-f0-9]{1,64}$/.test(code)) return window.showToast('Mã lời mời không hợp lệ.', 'error');
    location.assign('/Groups/Invite/' + encodeURIComponent(code));
  });
})();
