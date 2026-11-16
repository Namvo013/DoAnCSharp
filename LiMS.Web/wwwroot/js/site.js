window.libraryUiReady = true;

const menuButton = document.querySelector("[data-mobile-menu]");
const sidebar = document.getElementById("library-sidebar");

function closeMobileMenu() {
    if (!menuButton || !sidebar) return;
    sidebar.classList.remove("open");
    document.body.classList.remove("mobile-nav-open");
    menuButton.setAttribute("aria-expanded", "false");
    menuButton.setAttribute("aria-label", "Mở điều hướng");
}

function markFormSubmitting(form) {
    if (form.dataset.submitting === "true") return;
    form.dataset.submitting = "true";
    form.setAttribute("aria-busy", "true");

    form.querySelectorAll('button[type="submit"], button:not([type]), input[type="submit"]').forEach((button) => {
        if (button.disabled) return;
        button.dataset.loadingButton = "true";
        button.classList.add("form-submitting");
        button.disabled = true;

        if (button instanceof HTMLInputElement) {
            button.dataset.previousLabel = button.value;
            button.value = "Đang xử lý...";
        } else {
            button.dataset.previousLabel = button.textContent;
            button.textContent = "Đang xử lý...";
        }
    });
}

function resetSubmittingForms() {
    document.querySelectorAll('form[data-submitting="true"]').forEach((form) => {
        form.removeAttribute("aria-busy");
        delete form.dataset.submitting;
        form.querySelectorAll('[data-loading-button="true"]').forEach((button) => {
            button.disabled = false;
            button.classList.remove("form-submitting");
            if (button instanceof HTMLInputElement) {
                button.value = button.dataset.previousLabel || "";
            } else {
                button.textContent = button.dataset.previousLabel || "";
            }
            delete button.dataset.loadingButton;
            delete button.dataset.previousLabel;
        });
    });
}

if (menuButton && sidebar) {
    menuButton.addEventListener("click", () => {
        const isOpen = sidebar.classList.toggle("open");
        document.body.classList.toggle("mobile-nav-open", isOpen);
        menuButton.setAttribute("aria-expanded", String(isOpen));
        menuButton.setAttribute("aria-label", isOpen ? "Đóng điều hướng" : "Mở điều hướng");
    });

    sidebar.querySelectorAll("a, button").forEach((item) => {
        item.addEventListener("click", closeMobileMenu);
    });

    document.addEventListener("click", (event) => {
        if (!sidebar.classList.contains("open")) return;
        if (sidebar.contains(event.target) || menuButton.contains(event.target)) return;
        closeMobileMenu();
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") closeMobileMenu();
    });
}

const toastRegion = document.getElementById("toast-region");

if (toastRegion) {
    document.querySelectorAll(
        '.form-message.success[role="status"], .form-message.warning[role="status"], ' +
        '.form-message.error[role="alert"], .success-message[data-toast]'
    ).forEach((message) => {
        if (!message.textContent.trim()) return;

        message.classList.add("toast");
        toastRegion.appendChild(message);

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "toast-close";
        closeButton.textContent = "×";
        closeButton.setAttribute("aria-label", "Đóng thông báo");
        closeButton.addEventListener("click", () => message.remove());
        message.appendChild(closeButton);

        window.setTimeout(() => message.remove(), 8000);
    });
}

const searchForm = document.querySelector("[data-live-search]");
const searchInput = searchForm?.querySelector("[data-live-search-input]");
const bookCards = [...document.querySelectorAll("[data-book-card]")];
const resultCount = document.querySelector("[data-result-count]");
const liveEmptyState = document.querySelector("[data-live-empty]");

if (searchInput && bookCards.length > 0) {
    const filterBooks = () => {
        const query = searchInput.value.trim().toLocaleLowerCase();
        let visibleCount = 0;

        bookCards.forEach((card) => {
            const matches = card.textContent.toLocaleLowerCase().includes(query);
            card.hidden = !matches;
            if (matches) visibleCount++;
        });

        if (resultCount) resultCount.textContent = String(visibleCount);
        if (liveEmptyState) liveEmptyState.hidden = visibleCount !== 0;
    };

    searchInput.addEventListener("input", filterBooks);
    filterBooks();
}

const confirmDialog = document.getElementById("confirm-dialog");
const confirmMessage = confirmDialog?.querySelector("[data-confirm-message]");
const cancelButton = confirmDialog?.querySelector("[data-confirm-cancel]");
const acceptButton = confirmDialog?.querySelector("[data-confirm-accept]");
let pendingForm = null;

document.addEventListener("submit", (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;

    if (form.dataset.confirm && form.dataset.confirmed !== "true") {
        if (!confirmDialog || !confirmMessage || typeof confirmDialog.showModal !== "function") {
            event.preventDefault();
            if (window.confirm(form.dataset.confirm)) {
                form.dataset.confirmed = "true";
                form.requestSubmit();
            }
            return;
        }

        event.preventDefault();
        if (confirmDialog.open) return;
        pendingForm = form;
        confirmMessage.textContent = form.dataset.confirm;
        confirmDialog.returnValue = "";
        confirmDialog.showModal();
        return;
    }

    delete form.dataset.confirmed;
    if (form.method.toLowerCase() !== "post" && !form.hasAttribute("data-live-search")) return;
    markFormSubmitting(form);
});

cancelButton?.addEventListener("click", () => confirmDialog.close("cancel"));
acceptButton?.addEventListener("click", () => confirmDialog.close("confirm"));

confirmDialog?.addEventListener("click", (event) => {
    if (event.target === confirmDialog) confirmDialog.close("cancel");
});

confirmDialog?.addEventListener("close", () => {
    const form = pendingForm;
    pendingForm = null;

    if (confirmDialog.returnValue !== "confirm" || !form) return;
    form.dataset.confirmed = "true";
    form.requestSubmit();
});

window.addEventListener("pageshow", resetSubmittingForms);
