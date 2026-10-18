(() => {
  const escapeHtml = value => String(value ?? '')
    .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;').replaceAll("'", '&#039;');

  const identityKey = value => String(value ?? '').trim().toLocaleLowerCase('vi');
  const searchKey = value => identityKey(value)
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replaceAll('đ', 'd');

  window.createSubjectMultiSelect = ({ root, maxSelections = 10 }) => {
    if (!root) throw new Error('Không tìm thấy bộ chọn môn học.');

    const toggle = root.querySelector('[data-subject-toggle]');
    const panel = root.querySelector('[data-subject-panel]');
    const search = root.querySelector('[data-subject-search]');
    const container = root.querySelector('[data-subject-options]');
    const summary = root.querySelector('[data-subject-summary]');
    const hint = root.querySelector('[data-subject-hint]');
    const options = new Map();
    const selected = new Set();
    const idPrefix = root.id || `subject-select-${crypto.randomUUID()}`;

    const notify = message => window.showToast?.(message, 'error');

    function addOption(option) {
      const name = String(option?.name ?? '').trim();
      if (!name) return;
      const key = identityKey(name);
      const existing = options.get(key);
      if (existing) {
        if (!option.isCustom) existing.isCustom = false;
        return;
      }
      options.set(key, { name, isCustom: Boolean(option.isCustom) });
    }

    container.querySelectorAll('[data-subject-initial]').forEach(label => {
      const input = label.querySelector('input');
      if (!input?.value) return;
      addOption({
        name: input.value,
        isCustom: label.dataset.subjectCustom === 'true'
      });
      if (input.checked) selected.add(identityKey(input.value));
    });

    function sortedOptions() {
      return [...options.entries()].sort((left, right) =>
        left[1].name.localeCompare(right[1].name, 'vi'));
    }

    function selectedNames() {
      return [...selected]
        .map(key => options.get(key)?.name)
        .filter(Boolean);
    }

    function updateSummary() {
      const names = selectedNames();
      summary.classList.toggle('is-placeholder', names.length === 0);
      if (!names.length) summary.textContent = 'Chọn môn học';
      else if (names.length <= 2) summary.textContent = names.join(', ');
      else summary.textContent = `${names.slice(0, 2).join(', ')} +${names.length - 2}`;
    }

    function render() {
      const query = search.value.trim();
      const normalizedQuery = searchKey(query);
      const matches = sortedOptions().filter(([, option]) =>
        !normalizedQuery || searchKey(option.name).includes(normalizedQuery));

      if (!matches.length) {
        container.innerHTML = `<div class="subject-multiselect-empty">
          <i class="bi bi-search"></i>
          <span>${query ? `Không tìm thấy môn “${escapeHtml(query)}”` : 'Chưa có môn học gợi ý'}</span>
        </div>`;
      } else {
        container.innerHTML = matches.map(([key, option], index) => `
          <label class="group-subject-option" for="${idPrefix}-option-${index}">
            <input id="${idPrefix}-option-${index}" type="checkbox"
                   value="${escapeHtml(option.name)}" data-subject-key="${escapeHtml(key)}"
                   ${selected.has(key) ? 'checked' : ''} />
            <span>${escapeHtml(option.name)}</span>
            ${option.isCustom ? '<small>Cá nhân</small>' : ''}
          </label>`).join('');
      }

      if (query) {
        const exact = options.get(identityKey(query));
        hint.innerHTML = exact
          ? `Nhấn <kbd>Enter</kbd> để tích môn “${escapeHtml(exact.name)}”.`
          : `Nhấn <kbd>Enter</kbd> để thêm “${escapeHtml(query)}” làm môn mới.`;
      } else {
        hint.textContent = `Đã chọn ${selected.size}/${maxSelections} môn. Nhập tên để tìm kiếm.`;
      }
      updateSummary();
    }

    function selectFromSearch() {
      const name = search.value.trim();
      if (!name) return;
      if (name.length > 150) {
        notify('Tên môn học tối đa 150 ký tự.');
        return;
      }

      const key = identityKey(name);
      if (!selected.has(key) && selected.size >= maxSelections) {
        notify(`Mỗi nhóm được chọn tối đa ${maxSelections} môn học.`);
        return;
      }
      if (!options.has(key)) addOption({ name, isCustom: true });
      selected.add(key);
      search.value = '';
      render();
      search.focus();
    }

    function setOpen(open) {
      panel.hidden = !open;
      toggle.setAttribute('aria-expanded', String(open));
      root.classList.toggle('is-open', open);
      if (open) window.setTimeout(() => search.focus(), 0);
    }

    toggle.addEventListener('click', () => setOpen(panel.hidden));
    search.addEventListener('input', render);
    search.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        selectFromSearch();
      } else if (event.key === 'Escape') {
        event.preventDefault();
        setOpen(false);
        toggle.focus();
      }
    });

    container.addEventListener('change', event => {
      const checkbox = event.target.closest('input[type="checkbox"]');
      if (!checkbox) return;
      const key = checkbox.dataset.subjectKey;
      if (checkbox.checked) {
        if (!selected.has(key) && selected.size >= maxSelections) {
          checkbox.checked = false;
          notify(`Mỗi nhóm được chọn tối đa ${maxSelections} môn học.`);
          return;
        }
        selected.add(key);
      } else {
        selected.delete(key);
      }
      render();
    });

    document.addEventListener('click', event => {
      if (!panel.hidden && !root.contains(event.target)) setOpen(false);
    });

    render();

    return {
      getSelectedNames: selectedNames,
      setOptions(newOptions) {
        newOptions.forEach(addOption);
        render();
      },
      focus() {
        setOpen(true);
      }
    };
  };
})();
