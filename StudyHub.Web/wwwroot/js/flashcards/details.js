document.addEventListener('DOMContentLoaded', function () {

    const container =
        document.getElementById('flashcardDeckDetails');

    if (!container) return;

    const deckId = container.dataset.deckId;

    const editModal =
        document.getElementById('editCardModal');

    editModal?.addEventListener('show.bs.modal', function (event) {

        const button = event.relatedTarget;

        if (!button) return;

        const cardId =
            button.getAttribute('data-card-id');

        document.getElementById('editFrontText').value =
            button.getAttribute('data-front') ?? '';

        document.getElementById('editBackText').value =
            button.getAttribute('data-back') ?? '';

        document.getElementById('editSortOrder').value =
            button.getAttribute('data-sort-order') ?? 0;

        document.getElementById('editCardForm').action =
            `/Flashcards/${deckId}/Cards/${cardId}/Edit`;
    });

    const deleteModal =
        document.getElementById('deleteCardModal');

    deleteModal?.addEventListener('show.bs.modal', function (event) {

        const button = event.relatedTarget;

        if (!button) return;

        const cardId =
            button.getAttribute('data-card-id');

        document.getElementById('deleteCardForm').action =
            `/Flashcards/${deckId}/Cards/${cardId}/Delete`;
    });

});
