const key = `notesv3-reader-position:${location.pathname.toLowerCase()}`;
const saved = sessionStorage.getItem(key);

document.querySelector("[data-reader-book-link]")?.addEventListener("click", () => {
    sessionStorage.setItem(key, JSON.stringify({
        scrollY: window.scrollY,
        notesOpen: document.getElementById("notesv3-note-toggle").checked
    }));
});

if (saved) {
    sessionStorage.removeItem(key);
    try {
        const state = JSON.parse(saved);
        document.getElementById("notesv3-note-toggle").checked = state.notesOpen;
        const restore = () => window.scrollTo(0, state.scrollY);
        requestAnimationFrame(restore);
        window.addEventListener("load", restore, { once: true });
    } catch {
        // Dữ liệu của tab cũ không còn hợp lệ.
    }
}
