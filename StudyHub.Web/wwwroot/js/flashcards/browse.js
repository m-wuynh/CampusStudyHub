const cardsElement = document.getElementById('flashcardData');

const cards = JSON.parse(
    cardsElement?.textContent ?? '[]'
);

let currentIndex = 0;
let isFlipped = false;


const flashcard =
    document.getElementById('flashcard');

const frontText =
    document.getElementById('frontText');

const backText =
    document.getElementById('backText');

const previousCardButton =
    document.getElementById('previousCardButton');

const nextCardButton =
    document.getElementById('nextCardButton');

const flipCardButton =
    document.getElementById('flipCardButton');

const flipButtonText =
    document.getElementById('flipButtonText');

const currentCardNumber =
    document.getElementById('currentCardNumber');

const studyContainer =
    document.getElementById('studyContainer');

const studyCompleted =
    document.getElementById('studyCompleted');

const finishStudyButton =
    document.getElementById('finishStudyButton');

const studyAgainButton =
    document.getElementById('studyAgainButton');


function getCardProperty(card, pascalName, camelName) {
    return card[pascalName] ?? card[camelName];
}


function renderCard() {

    if (!cards || cards.length === 0) {
        return;
    }

    const card = cards[currentIndex];

    frontText.textContent =
        getCardProperty(
            card,
            'FrontText',
            'frontText'
        ) ?? '';

    backText.textContent =
        getCardProperty(
            card,
            'BackText',
            'backText'
        ) ?? '';

    currentCardNumber.textContent =
        currentIndex + 1;


    // Mỗi lần đổi card sẽ quay về Front.
    isFlipped = false;

    flashcard.classList.remove('is-flipped');

    flipButtonText.textContent =
        'Hiện đáp án';


    // Không vượt quá biên deck.
    previousCardButton.disabled =
        currentIndex === 0;

    nextCardButton.disabled =
        currentIndex === cards.length - 1;
}


function flipCard() {

    isFlipped = !isFlipped;

    flashcard.classList.toggle(
        'is-flipped',
        isFlipped
    );

    flipButtonText.textContent =
        isFlipped
            ? 'Xem câu hỏi'
            : 'Hiện đáp án';
}


previousCardButton?.addEventListener(
    'click',
    function () {

        if (currentIndex <= 0) {
            return;
        }

        currentIndex--;

        renderCard();
    });


nextCardButton?.addEventListener(
    'click',
    function () {

        if (currentIndex >= cards.length - 1) {
            return;
        }

        currentIndex++;

        renderCard();
    });


flipCardButton?.addEventListener(
    'click',
    flipCard
);



// Cho phép click trực tiếp lên card để lật.
flashcard?.addEventListener('click', flipCard);
finishStudyButton?.addEventListener(
    'click',
    function () {

        studyContainer.classList.add('d-none');
        studyCompleted.classList.remove('d-none');
    }
);

studyAgainButton?.addEventListener(
    'click',
    function () {
        currentIndex = 0;
        isFlipped = false;

        studyCompleted.classList.add('d-none');
        studyContainer.classList.remove('d-none');

        renderCard();
    }
);
renderCard();
