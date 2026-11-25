(() => {
  const form = document.getElementById('group-form');
  const root = document.getElementById('group-subject-select');
  if (!form || !root) return;
  const subjects = window.createSubjectMultiSelect({ root });
  const errors = [...form.querySelectorAll('[data-valmsg-for]')];
  const controlFor = name => name === 'Subjects'
    ? root.querySelector('[data-subject-toggle]') : form.elements.namedItem(name);

  function setError(error, message) {
    const control = controlFor(error.dataset.valmsgFor);
    error.textContent = message;
    error.classList.toggle('field-validation-error', Boolean(message));
    error.classList.toggle('field-validation-valid', !message);
    control.classList.toggle('input-validation-error', Boolean(message));
    control.setAttribute('aria-invalid', String(Boolean(message)));
    return !message;
  }

  function validate(error) {
    const name = error.dataset.valmsgFor;
    if (name === 'Subjects') {
      const names = subjects.getSelectedNames();
      return setError(error, !names.length ? 'Hãy chọn ít nhất một môn học.'
        : names.some(subject => subject.length > 150) ? 'Tên môn học không được dài quá 150 ký tự.' : '');
    }
    const control = controlFor(name);
    const value = control.value.trim();
    const rules = control.dataset;
    let message = '';
    if (name === 'MaxMembers') {
      const number = Number(value);
      if (control.validity.badInput || value && (!Number.isInteger(number) || number < 2 || number > 500))
        message = 'Giới hạn người phải là số nguyên từ 2 đến 500.';
    } else if (rules.valRequired && !value) {
      message = rules.valRequired;
    } else if (rules.valLengthMax && control.value.length > Number(rules.valLengthMax)) {
      message = rules.valLength;
    } else if (rules.valRegexPattern && value && !new RegExp(`^(?:${rules.valRegexPattern})$`).test(value)) {
      message = rules.valRegex;
    }
    return setError(error, message);
  }

  form.addEventListener('submit', event => {
    const invalid = errors.filter(error => !validate(error));
    if (invalid.length) {
      event.preventDefault();
      controlFor(invalid[0].dataset.valmsgFor).focus();
    }
  });
  function revalidate(event) {
    const error = errors.find(item => item.dataset.valmsgFor === event.target.name);
    if (error?.textContent) validate(error);
  }
  form.addEventListener('input', revalidate);
  form.addEventListener('change', revalidate);
  function revalidateSubjects() {
    const error = errors.find(item => item.dataset.valmsgFor === 'Subjects');
    if (error?.textContent) validate(error);
  }
  root.addEventListener('change', revalidateSubjects);
  root.addEventListener('keydown', event => {
    if (event.key === 'Enter') revalidateSubjects();
  });
  errors.filter(error => error.textContent).forEach(error => {
    setError(error, error.textContent);
  });
})();
