document.addEventListener('DOMContentLoaded', function () {

    const editModal = document.getElementById('editSetModal');

    editModal?.addEventListener('show.bs.modal', function (event) {
        const button = event.relatedTarget;

        const deckId = button.getAttribute('data-deck-id');
        const title = button.getAttribute('data-title') ?? '';
        const description =
            button.getAttribute('data-description') ?? '';
        const subjectId = button.getAttribute('data-subject-id') ?? '';

        const form =
            document.getElementById('editDeckForm');

        document.getElementById('editDeckSubjectId').value = subjectId;
        document.getElementById('editDeckTitle').value =
            title;

        document.getElementById('editDeckDescription').value =
            description;

        form.action =
            `/Flashcards/${deckId}/Edit`;
    });


    const deleteModal =
        document.getElementById('deleteSetModal');

    deleteModal?.addEventListener('show.bs.modal', function (event) {
        const button = event.relatedTarget;

        const deckId =
            button.getAttribute('data-deck-id');

        const title =
            button.getAttribute('data-title') ?? '';

        document.getElementById('deleteDeckTitle')
            .textContent = title;

        document.getElementById('deleteDeckForm')
            .action = `/Flashcards/${deckId}/Delete`;
    });

});
