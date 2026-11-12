(() => {
  const input = document.getElementById('new-subject');
  const options = document.getElementById('form-subject-options');
  function addSubject() {
    const name = input.value.trim();
    if (!name) return;
    const existing = [...options.querySelectorAll('input')].find(i => i.value.toLocaleLowerCase('vi') === name.toLocaleLowerCase('vi'));
    if (existing) existing.checked = true;
    else {
      const label = document.createElement('label');
      const checkbox = document.createElement('input');
      checkbox.type = 'checkbox'; checkbox.name = 'Subjects'; checkbox.value = name; checkbox.checked = true;
      label.append(checkbox, document.createTextNode(' ' + name)); options.append(label);
    }
    input.value = '';
  }
  document.getElementById('add-subject').addEventListener('click', addSubject);
  input.addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); addSubject(); } });
})();
