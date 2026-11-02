/**
 * Grades Module - Wizard (grades-wizard.js)
 * Manages the step-by-step gradebook creation wizard and date validations.
 */

function wizardGoToStep(step) {
    if (step === 2) {
        // Validate step 1
        const nameInput = document.querySelector('#wizard-step-1 input[name="Name"]');
        if (!nameInput.value.trim()) {
            nameInput.reportValidity();
            return;
        }

        const fromYear = parseInt(document.getElementById('wizardFromYear').value) || 0;
        const toYear = parseInt(document.getElementById('wizardToYear').value) || 0;
        const existingGradeBooks = (window.GradesState && window.GradesState.existingGradeBooks) ? window.GradesState.existingGradeBooks : [];
        const exists = existingGradeBooks && existingGradeBooks.some(gb => (gb.fromYear || gb.FromYear) === fromYear && (gb.toYear || gb.ToYear) === toYear);
        if (exists) {
            if (window.showToast) {
                window.showToast('Sổ điểm này đã tồn tại', 'error');
            } else {
                alert('Sổ điểm này đã tồn tại');
            }
            return;
        }
        
        const numCols = parseInt(document.getElementById('wizard-column-count').value) || 0;
        const numSubjs = parseInt(document.getElementById('wizard-subject-count').value) || 0;
        
        let colsHtml = '';
        for (let i = 0; i < numCols; i++) {
            colsHtml += `
                <div class="row g-2 mb-2">
                    <div class="col-8">
                        <input type="text" name="Columns[${i}].Name" class="sh-input" placeholder="Tên cột ${i+1}" required maxlength="100">
                    </div>
                    <div class="col-4">
                        <input type="number" name="Columns[${i}].Weight" class="sh-input" placeholder="Hệ số" value="1" step="0.1" min="0.1" required>
                    </div>
                </div>
            `;
        }
        document.getElementById('wizard-columns-container').innerHTML = colsHtml;
        
        let subjsHtml = '';
        for (let i = 0; i < numSubjs; i++) {
            subjsHtml += `
                <div class="mb-2">
                    <input type="text" name="SubjectNames[${i}]" class="sh-input" placeholder="Tên môn học ${i+1}" required maxlength="150">
                </div>
            `;
        }
        document.getElementById('wizard-subjects-container').innerHTML = subjsHtml;

        document.getElementById('wizard-step-1').style.display = 'none';
        document.getElementById('btn-wizard-next').style.display = 'none';
        
        document.getElementById('wizard-step-2').style.display = 'block';
        document.getElementById('btn-wizard-back').style.display = 'block';
        document.getElementById('btn-wizard-submit').style.display = 'block';
    } else {
        document.getElementById('wizard-step-2').style.display = 'none';
        document.getElementById('btn-wizard-back').style.display = 'none';
        document.getElementById('btn-wizard-submit').style.display = 'none';
        
        document.getElementById('wizard-step-1').style.display = 'block';
        document.getElementById('btn-wizard-next').style.display = 'block';
    }
}
