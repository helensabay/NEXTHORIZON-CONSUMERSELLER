document.addEventListener("DOMContentLoaded", () => {
    const page = document.querySelector("[data-help-view='assistant']");
    if (!page) {
        return;
    }

    const elements = {
        search: page.querySelector("[data-help-assistant-search]"),
        viewport: page.querySelector("[data-help-assistant-viewport]"),
        grid: page.querySelector("[data-help-assistant-grid]"),
        prev: page.querySelector("[data-help-assistant-prev]"),
        next: page.querySelector("[data-help-assistant-next]"),
        faqViewport: page.querySelector("[data-help-assistant-faq-viewport]"),
        faqTrack: page.querySelector("[data-help-assistant-faq-track]"),
        faqPrev: page.querySelector("[data-help-assistant-faq-prev]"),
        faqNext: page.querySelector("[data-help-assistant-faq-next]"),
        faqList: page.querySelector("[data-help-assistant-faq-list]"),
        toggleAll: page.querySelector("[data-help-assistant-toggle-all]"),
        chat: page.querySelector("#helpChat"),
    };

    const categoryCache = new Map();
    let categories = [];
    let visibleCategories = [];
    let activeSlug = "";
    let allExpanded = false;
    let currentPage = 0;
    let visibleCount = 4;
    let faqPage = 0;
    let chatViewportFrame = 0;

    const fetchJson = async (url) => {
        const response = await fetch(url, {
            headers: {
                "X-Requested-With": "XMLHttpRequest",
            },
        });

        let payload = null;
        if (response.status !== 204) {
            const contentType = response.headers.get("content-type") || "";
            payload = contentType.includes("application/json")
                ? await response.json()
                : await response.text();
        }

        if (!response.ok) {
            const message = payload && typeof payload === "object" && payload.message
                ? payload.message
                : "Unable to load help content right now.";
            throw new Error(message);
        }

        return payload;
    };

    const setGridMessage = (message, className) => {
        if (!elements.grid) {
            return;
        }

        elements.grid.innerHTML = `<div class="${className}">${message}</div>`;
        scheduleChatViewportSync();
    };

    const setFaqMessage = (message, className) => {
        if (!elements.faqList) {
            return;
        }

        elements.faqList.innerHTML = `<div class="${className}">${message}</div>`;
    };

    const getMinimumChatHeight = () => {
        const width = window.innerWidth;
        if (width <= 640) {
            return 400;
        }

        if (width <= 900) {
            return 470;
        }

        return 560;
    };

    const scheduleChatViewportSync = () => {
        if (chatViewportFrame) {
            return;
        }

        chatViewportFrame = window.requestAnimationFrame(() => {
            chatViewportFrame = 0;

            if (!elements.chat) {
                return;
            }

            const headerHeight = document.querySelector("#main-header")?.getBoundingClientRect().height || 0;
            const pageStyles = window.getComputedStyle(page);
            const pageBottomPadding = Number.parseFloat(pageStyles.paddingBottom || "0") || 0;
            const viewportHeight = window.visualViewport?.height || window.innerHeight;
            const chatTop = elements.chat.getBoundingClientRect().top;
            const topOffset = Math.max(chatTop, headerHeight + 16);
            const availableHeight = Math.floor(viewportHeight - topOffset - Math.max(16, pageBottomPadding));
            const clampedHeight = Math.max(getMinimumChatHeight(), availableHeight);

            page.style.setProperty("--help-chat-height", `${clampedHeight}px`);
        });
    };

    const getVisibleCount = () => {
        const width = window.innerWidth;
        if (width <= 640) {
            return 1;
        }

        if (width <= 900) {
            return 2;
        }

        return 4;
    };

    const getCarouselGap = () => {
        if (!elements.grid) {
            return 10;
        }

        const styles = window.getComputedStyle(elements.grid);
        const gap = Number.parseFloat(styles.columnGap || styles.gap || "10");
        return Number.isFinite(gap) ? gap : 10;
    };

    const getMaxPage = () => {
        if (!visibleCategories.length) {
            return 0;
        }

        return Math.max(0, Math.ceil(visibleCategories.length / visibleCount) - 1);
    };

    const syncCarouselControls = () => {
        const maxPage = getMaxPage();
        const canSlide = visibleCategories.length > visibleCount;

        if (elements.prev) {
            elements.prev.disabled = !canSlide || currentPage <= 0;
        }

        if (elements.next) {
            elements.next.disabled = !canSlide || currentPage >= maxPage;
        }
    };

    const updateCarouselPosition = () => {
        if (!elements.grid || !elements.viewport) {
            return;
        }

        const viewportWidth = elements.viewport.clientWidth;
        if (!viewportWidth) {
            elements.grid.style.transform = "translateX(0px)";
            syncCarouselControls();
            return;
        }

        const maxOffset = Math.max(0, elements.grid.scrollWidth - viewportWidth);
        const requestedOffset = currentPage * viewportWidth;
        const offset = Math.min(requestedOffset, maxOffset);
        elements.grid.style.transform = `translateX(-${offset}px)`;
        syncCarouselControls();
    };

    const updateCarouselLayout = () => {
        if (!elements.grid || !elements.viewport) {
            return;
        }

        visibleCount = getVisibleCount();
        currentPage = Math.min(currentPage, getMaxPage());

        const cards = Array.from(elements.grid.querySelectorAll(".help-card"));
        if (!cards.length) {
            elements.grid.style.transform = "translateX(0px)";
            syncCarouselControls();
            return;
        }

        const viewportWidth = elements.viewport.clientWidth;
        const gap = getCarouselGap();
        const cardWidth = Math.max(0, (viewportWidth - (gap * (visibleCount - 1))) / visibleCount);

        cards.forEach((card) => {
            card.style.flex = `0 0 ${cardWidth}px`;
        });

        updateCarouselPosition();
    };

    const getFaqMaxPage = () => {
        if (!elements.faqViewport || !elements.faqTrack) {
            return 0;
        }

        const viewportWidth = elements.faqViewport.clientWidth;
        if (!viewportWidth) {
            return 0;
        }

        return Math.max(0, Math.ceil(elements.faqTrack.scrollWidth / viewportWidth) - 1);
    };

    const syncFaqCarouselControls = () => {
        const maxPage = getFaqMaxPage();
        const canSlide = maxPage > 0;

        if (elements.faqPrev) {
            elements.faqPrev.disabled = !canSlide || faqPage <= 0;
        }

        if (elements.faqNext) {
            elements.faqNext.disabled = !canSlide || faqPage >= maxPage;
        }
    };

    const updateFaqCarouselPosition = () => {
        if (!elements.faqViewport || !elements.faqTrack) {
            return;
        }

        const viewportWidth = elements.faqViewport.clientWidth;
        if (!viewportWidth) {
            elements.faqTrack.style.transform = "translateX(0px)";
            syncFaqCarouselControls();
            return;
        }

        const maxOffset = Math.max(0, elements.faqTrack.scrollWidth - viewportWidth);
        const requestedOffset = faqPage * viewportWidth;
        const offset = Math.min(requestedOffset, maxOffset);
        elements.faqTrack.style.transform = `translateX(-${offset}px)`;
        syncFaqCarouselControls();
    };

    const ensureActiveFaqChipVisible = (slug) => {
        if (!slug || !elements.faqViewport || !elements.faqTrack) {
            return;
        }

        const chip = elements.faqTrack.querySelector(`[data-faq-category-slug="${slug}"]`);
        if (!chip) {
            return;
        }

        const viewportWidth = elements.faqViewport.clientWidth;
        if (!viewportWidth) {
            return;
        }

        const start = faqPage * viewportWidth;
        const chipStart = chip.offsetLeft;
        const chipEnd = chip.offsetLeft + chip.offsetWidth;
        const end = start + viewportWidth;

        if (chipStart < start) {
            faqPage = Math.max(0, Math.floor(chipStart / viewportWidth));
        } else if (chipEnd > end) {
            faqPage = Math.max(0, Math.floor((chipEnd - 1) / viewportWidth));
        }

        updateFaqCarouselPosition();
    };

    const renderCards = (items) => {
        if (!elements.grid) {
            return;
        }

        visibleCategories = items;
        currentPage = 0;

        if (!items.length) {
            setGridMessage("No help topics matched your search.", "help-empty-state");
            updateCarouselLayout();
            return;
        }

        elements.grid.innerHTML = "";

        items.forEach((category) => {
            const card = document.createElement("article");
            card.className = "help-card";
            card.dataset.categorySlug = category.slug;
            card.dataset.categoryTitle = category.title;
            card.dataset.categoryDescription = category.description;
            card.tabIndex = 0;

            const iconWrap = document.createElement("div");
            iconWrap.className = "help-card-icon";

            const icon = document.createElement("i");
            icon.className = category.iconKey;
            iconWrap.appendChild(icon);

            const content = document.createElement("div");
            content.className = "help-card-content";

            const title = document.createElement("h3");
            title.textContent = category.title;

            const description = document.createElement("p");
            description.className = "help-card-desc";
            description.textContent = category.description;

            content.appendChild(title);
            content.appendChild(description);

            const link = document.createElement("a");
            link.className = "help-card-view";
            link.href = `/Help/${encodeURIComponent(category.slug)}`;
            link.textContent = "View Details";

            card.appendChild(iconWrap);
            card.appendChild(content);
            card.appendChild(link);
            elements.grid.appendChild(card);
        });

        window.requestAnimationFrame(() => {
            updateCarouselLayout();
            scheduleChatViewportSync();
        });
    };

    const setActiveCategory = (slug) => {
        if (!elements.faqTrack) {
            return;
        }

        elements.faqTrack.querySelectorAll("[data-faq-category-slug]").forEach((chip) => {
            chip.classList.toggle("active", chip.dataset.faqCategorySlug === slug);
        });

        ensureActiveFaqChipVisible(slug);
    };

    const renderCategoryOptions = (items) => {
        if (!elements.faqTrack) {
            return;
        }

        elements.faqTrack.innerHTML = "";
        faqPage = 0;

        items.forEach((category, index) => {
            const chip = document.createElement("button");
            chip.type = "button";
            chip.className = "faq-category-chip";
            chip.dataset.faqCategorySlug = category.slug;
            chip.textContent = category.title;
            if (index === 0) {
                chip.classList.add("active");
            }

            elements.faqTrack.appendChild(chip);
        });

        window.requestAnimationFrame(updateFaqCarouselPosition);
    };

    const renderFaqs = (faqs) => {
        if (!elements.faqList) {
            return;
        }

        elements.faqList.innerHTML = "";
        allExpanded = false;
        if (elements.toggleAll) {
            elements.toggleAll.textContent = "Expand All";
        }

        const items = faqs.slice(0, 5);
        if (!items.length) {
            setFaqMessage("No FAQs are available for this topic.", "help-empty-state");
            return;
        }

        items.forEach((faq, index) => {
            const item = document.createElement("div");
            item.className = "faq-item";
            item.dataset.faqQuestion = faq.question;
            item.dataset.faqAnswer = faq.answer;

            const question = document.createElement("div");
            question.className = "faq-question";
            question.setAttribute("role", "button");
            question.setAttribute("tabindex", "0");
            question.setAttribute("aria-expanded", "false");

            const number = document.createElement("span");
            number.className = "faq-number";
            number.textContent = String(index + 1);

            const label = document.createElement("span");
            label.textContent = faq.question;

            const arrow = document.createElement("span");
            arrow.className = "arrow";
            arrow.textContent = ">";
            arrow.setAttribute("aria-hidden", "true");

            question.appendChild(number);
            question.appendChild(label);
            question.appendChild(arrow);

            const answer = document.createElement("div");
            answer.className = "faq-answer";
            answer.textContent = faq.answer;

            item.appendChild(question);
            item.appendChild(answer);
            elements.faqList.appendChild(item);
        });
    };

    const toggleFaq = (item, forceExpand) => {
        const items = elements.faqList?.querySelectorAll(".faq-item") || [];
        const question = item.querySelector(".faq-question");
        const willExpand = typeof forceExpand === "boolean"
            ? forceExpand
            : !item.classList.contains("active");

        if (typeof forceExpand !== "boolean") {
            items.forEach((other) => {
                if (other !== item) {
                    other.classList.remove("active");
                    other.querySelector(".faq-question")?.setAttribute("aria-expanded", "false");
                }
            });
        }

        item.classList.toggle("active", willExpand);
        question?.setAttribute("aria-expanded", willExpand.toString());

        return willExpand;
    };

    const loadCategory = async (slug) => {
        if (!slug) {
            return;
        }

        activeSlug = slug;
        setActiveCategory(slug);
        setFaqMessage("Loading FAQs...", "help-loading-state");

        try {
            let detail = categoryCache.get(slug);
            if (!detail) {
                detail = await fetchJson(`/api/help/categories/${encodeURIComponent(slug)}`);
                categoryCache.set(slug, detail);
            }

            renderFaqs(detail.faqs || []);

        } catch (error) {
            setFaqMessage(error.message, "help-error-state");
        }
    };

    const filterCards = () => {
        const term = elements.search?.value.trim().toLowerCase() || "";
        if (!term) {
            renderCards(categories);
            return;
        }

        const filtered = categories.filter((category) => {
            const title = category.title.toLowerCase();
            const description = category.description.toLowerCase();
            return title.includes(term) || description.includes(term);
        });

        renderCards(filtered);
    };

    const bindEvents = () => {
        elements.search?.addEventListener("input", filterCards);

        elements.prev?.addEventListener("click", () => {
            if (currentPage <= 0) {
                return;
            }

            currentPage -= 1;
            updateCarouselPosition();
        });

        elements.next?.addEventListener("click", () => {
            const maxPage = getMaxPage();
            if (currentPage >= maxPage) {
                return;
            }

            currentPage += 1;
            updateCarouselPosition();
        });

        elements.grid?.addEventListener("click", async (event) => {
            const link = event.target.closest(".help-card-view");
            if (link) {
                return;
            }

            const card = event.target.closest(".help-card");
            if (!card) {
                return;
            }

            await loadCategory(card.dataset.categorySlug || "");
        });

        elements.grid?.addEventListener("keydown", async (event) => {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            const card = event.target.closest(".help-card");
            if (!card) {
                return;
            }

            event.preventDefault();
            await loadCategory(card.dataset.categorySlug || "");
        });

        elements.faqPrev?.addEventListener("click", () => {
            if (faqPage <= 0) {
                return;
            }

            faqPage -= 1;
            updateFaqCarouselPosition();
        });

        elements.faqNext?.addEventListener("click", () => {
            const maxPage = getFaqMaxPage();
            if (faqPage >= maxPage) {
                return;
            }

            faqPage += 1;
            updateFaqCarouselPosition();
        });

        elements.faqTrack?.addEventListener("click", async (event) => {
            const chip = event.target.closest("[data-faq-category-slug]");
            if (!chip) {
                return;
            }

            const slug = chip.dataset.faqCategorySlug || "";
            if (!slug) {
                return;
            }

            await loadCategory(slug);
        });

        elements.faqTrack?.addEventListener("keydown", async (event) => {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            const chip = event.target.closest("[data-faq-category-slug]");
            if (!chip) {
                return;
            }

            event.preventDefault();
            const slug = chip.dataset.faqCategorySlug || "";
            if (!slug) {
                return;
            }

            await loadCategory(slug);
        });

        elements.toggleAll?.addEventListener("click", () => {
            const items = Array.from(elements.faqList?.querySelectorAll(".faq-item") || []);
            if (!items.length) {
                return;
            }

            allExpanded = !allExpanded;
            items.forEach((item) => toggleFaq(item, allExpanded));
            elements.toggleAll.textContent = allExpanded ? "Collapse All" : "Expand All";
        });

        elements.faqList?.addEventListener("click", (event) => {
            const trigger = event.target.closest(".faq-question");
            if (!trigger) {
                return;
            }

            const item = trigger.closest(".faq-item");
            if (!item) {
                return;
            }

            toggleFaq(item);
        });

        elements.faqList?.addEventListener("keydown", (event) => {
            if (event.key !== "Enter" && event.key !== " ") {
                return;
            }

            const trigger = event.target.closest(".faq-question");
            if (!trigger) {
                return;
            }

            event.preventDefault();
            trigger.click();
        });

        window.addEventListener("resize", () => {
            updateCarouselLayout();
            updateFaqCarouselPosition();
            scheduleChatViewportSync();
        });
    };

    const init = async () => {
        bindEvents();
        scheduleChatViewportSync();

        try {
            categories = await fetchJson("/api/help/categories");
            if (!Array.isArray(categories) || !categories.length) {
                setGridMessage("No help topics are available right now.", "help-empty-state");
                setFaqMessage("No FAQs are available right now.", "help-empty-state");
                return;
            }

            renderCards(categories);
            renderCategoryOptions(categories);
            await loadCategory(categories[0].slug);
        } catch (error) {
            setGridMessage(error.message, "help-error-state");
            setFaqMessage(error.message, "help-error-state");
        }
    };

    init();

    window.addEventListener("load", scheduleChatViewportSync, { once: true });
});
