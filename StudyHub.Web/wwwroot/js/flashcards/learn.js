document.addEventListener('DOMContentLoaded', function () {

    const form =
        document.getElementById('learnAnswerForm');

    if (!form) return;

    const buttons = Array.from(
        document.querySelectorAll('.learn-option')
    );

    const selectedAnswerInput =
        document.getElementById('selectedAnswer');

    const feedbackMessage =
        document.getElementById('feedbackMessage');

    const feedbackPanel =
        document.getElementById('learnFeedback');

    const nextButton =
        document.getElementById('nextQuestionButton');

    const progressText =
        document.getElementById('sessionProgress');

    const progressBar =
        document.getElementById('learnProgressBar');

    let submitting = false;

    function markButton(button, cssClass) {
        button.classList.remove(
            'btn-outline-secondary',
            'btn-success',
            'btn-danger'
        );

        button.classList.add(cssClass);
    }

    function showFeedback(message, success) {
        feedbackMessage.className =
            success
                ? 'alert alert-success mt-3'
                : 'alert alert-danger mt-3';

        feedbackMessage.textContent = message;
    }

    function showNextButton(text) {
        const buttonText =
            document.getElementById('nextQuestionButtonText');

        if (buttonText) {
            buttonText.textContent = text;
        }

        feedbackPanel.classList.remove('d-none');
    }

    function normalize(text) {
        return (text ?? '').trim().toLocaleLowerCase();
    }

    function updateProgress(data) {
        if (progressText) {
            progressText.textContent =
                `Đã học: ${data.learnedCount} / ${data.totalCards}` +
                ` | New: ${data.newCount}` +
                ` | Learning: ${data.learningCount}`;
        }

        if (progressBar && data.totalCards > 0) {
            const percent =
                100 * data.learnedCount / data.totalCards;

            progressBar.style.width = `${percent}%`;

            progressBar.parentElement?.setAttribute(
                'aria-valuenow',
                String(data.learnedCount)
            );
        }
    }

    buttons.forEach(button => {
        button.addEventListener('click', async function () {

            if (submitting) return;

            const answer = button.dataset.answer;

            if (!answer) return;

            submitting = true;

            selectedAnswerInput.value = answer;

            buttons.forEach(b => b.disabled = true);

            feedbackMessage.textContent = '';
            feedbackMessage.className = '';

            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                let data = {};

                try {
                    data = await response.json();
                }
                catch {
                    // Response có thể không phải JSON.
                }

                if (!response.ok) {
                    showFeedback(
                        data.message ??
                        'Không thể xử lý câu trả lời. Hãy tải lại trang.',
                        false
                    );

                    showNextButton('Tải lại phiên học');
                    return;
                }

                if (data.isCorrect) {
                    markButton(button, 'btn-success');

                    showFeedback(
                        'Chính xác!',
                        true
                    );
                }
                else {
                    markButton(button, 'btn-danger');

                    const correctButton = buttons.find(
                        b => normalize(b.dataset.answer) ===
                            normalize(data.correctAnswer)
                    );

                    if (correctButton) {
                        markButton(correctButton, 'btn-success');
                    }

                    showFeedback(
                        `Chưa chính xác. Đáp án đúng: ${data.correctAnswer}`,
                        false
                    );
                }

                updateProgress(data);

                showNextButton(
                    data.isCompleted
                        ? 'Xem hoàn thành'
                        : 'Câu tiếp theo'
                );
            }
            catch {
                showFeedback(
                    'Không xác định được kết quả gửi đáp án. ' +
                    'Hãy tải lại phiên học.',
                    false
                );

                showNextButton('Tải lại phiên học');
            }
        });
    });

    nextButton.addEventListener('click', function () {
        // Đang ở URL có SessionId,
        // nên refresh không tạo session mới.
        window.location.reload();
    });
});
