document.addEventListener("DOMContentLoaded", () => {
    const page = document.querySelector("[data-help-view]");
    if (!page) {
        return;
    }

    const view = page.getAttribute("data-help-view");
    let csrfTokenPromise;
    let homeCategoryPage = 0;
    let homeVisibleCount = 4;
    let homeVisibleCategories = [];

    const homeCarousel = view === "index"
        ? {
            viewport: page.querySelector("[data-help-category-viewport]"),
            grid: page.querySelector("[data-help-category-grid]"),
            prev: page.querySelector("[data-help-category-prev]"),
            next: page.querySelector("[data-help-category-next]"),
        }
        : null;

    const fetchJson = async (url, options) => {
        const response = await fetch(url, options);
        let payload = null;

        if (response.status !== 204) {
            const contentType = response.headers.get("content-type") || "";
            if (contentType.includes("application/json")) {
                payload = await response.json();
            } else {
                const text = await response.text();
                payload = text ? { message: text } : null;
            }
        }

        if (!response.ok) {
            const message =
                payload && typeof payload === "object" && "message" in payload && payload.message
                    ? payload.message
                    : response.status === 401
                        ? "You need to sign in before sending a support ticket."
                        : "Unable to load help content right now.";
            throw new Error(message);
        }

        return payload;
    };

    const ensureCsrfToken = async () => {
        if (!csrfTokenPromise) {
            csrfTokenPromise = fetchJson("/api/security/csrf-token").then((result) => result.token);
        }

        return csrfTokenPromise;
    };

    const wireAccordion = (triggerSelector, itemSelector, root) => {
        (root || document).querySelectorAll(triggerSelector).forEach((trigger) => {
            const item = trigger.closest(itemSelector);
            if (!item) {
                return;
            }

            const parent = item.parentElement;

            const toggle = () => {
                const isActive = item.classList.contains("active");

                if (parent) {
                    parent.querySelectorAll(`:scope > ${itemSelector}`).forEach((sibling) => {
                        sibling.classList.remove("active");
                        const siblingTrigger = sibling.querySelector(triggerSelector);
                        if (siblingTrigger) {
                            siblingTrigger.setAttribute("aria-expanded", "false");
                        }
                    });
                }

                item.classList.toggle("active", !isActive);
                trigger.setAttribute("aria-expanded", (!isActive).toString());
            };

            if (!trigger.hasAttribute("role")) {
                trigger.setAttribute("role", "button");
            }

            if (!trigger.hasAttribute("tabindex")) {
                trigger.setAttribute("tabindex", "0");
            }

            trigger.setAttribute("aria-expanded", item.classList.contains("active").toString());
            trigger.addEventListener("click", toggle);
            trigger.addEventListener("keydown", (event) => {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    toggle();
                }
            });
        });
    };

    const setTopicHeader = (data) => {
        const title = page.querySelector("[data-help-topic-title]");
        const description = page.querySelector("[data-help-topic-description]");
        const icon = page.querySelector("[data-help-topic-icon]");

        if (title) {
            title.textContent = data.title;
        }

        if (description) {
            description.textContent = data.description;
        }

        if (icon) {
            icon.className = data.iconKey;
        }
    };

    const renderFaqItems = (container, faqs, itemClass, questionClass) => {
        container.innerHTML = "";

        if (!faqs || faqs.length === 0) {
            const empty = document.createElement("div");
            empty.className = "help-empty-state";
            empty.textContent = "No FAQs are available yet for this topic.";
            container.appendChild(empty);
            return;
        }

        faqs.forEach((faq, index) => {
            const item = document.createElement("div");
            item.className = itemClass;

            const question = document.createElement("div");
            question.className = questionClass;

            if (questionClass === "faq-question") {
                const number = document.createElement("span");
                number.className = "faq-number";
                number.textContent = String(index + 1);
                question.appendChild(number);
            }

            const label = document.createElement("span");
            label.textContent = faq.question;
            question.appendChild(label);

            const icon = document.createElement("span");
            icon.className = questionClass === "faq-question" ? "arrow" : "";
            icon.setAttribute("aria-hidden", "true");
            icon.textContent = questionClass === "faq-question" ? ">" : "";
            if (questionClass !== "faq-question") {
                const chevron = document.createElement("i");
                chevron.className = "fas fa-chevron-down";
                icon.appendChild(chevron);
            }
            question.appendChild(icon);

            const answer = document.createElement("div");
            answer.className = itemClass === "faq-item" ? "faq-answer" : "help-faq-answer";
            answer.textContent = faq.answer;

            item.appendChild(question);
            item.appendChild(answer);
            container.appendChild(item);
        });

        wireAccordion(`.${questionClass}`, `.${itemClass}`, container);
    };

    const renderTopicFaqs = (faqs) => {
        const container = page.querySelector("[data-help-topic-faqs]");
        if (!container) {
            return;
        }

        renderFaqItems(container, faqs, "help-faq-item", "help-faq-question");
    };

    const setHomeCategoryMessage = (message, className) => {
        if (!homeCarousel?.grid) {
            return;
        }

        homeVisibleCategories = [];
        homeCategoryPage = 0;
        homeCarousel.grid.innerHTML = `<div class="${className}">${message}</div>`;
        window.requestAnimationFrame(updateHomeCarouselLayout);
    };

    const getHomeVisibleCount = () => {
        const width = window.innerWidth;
        if (width <= 640) {
            return 1;
        }

        if (width <= 900) {
            return 2;
        }

        return 4;
    };

    const getHomeCarouselGap = () => {
        if (!homeCarousel?.grid) {
            return 10;
        }

        const styles = window.getComputedStyle(homeCarousel.grid);
        const gap = Number.parseFloat(styles.columnGap || styles.gap || "10");
        return Number.isFinite(gap) ? gap : 10;
    };

    const getHomeMaxPage = () => {
        if (!homeVisibleCategories.length) {
            return 0;
        }

        return Math.max(0, Math.ceil(homeVisibleCategories.length / homeVisibleCount) - 1);
    };

    const syncHomeCarouselControls = () => {
        const maxPage = getHomeMaxPage();
        const canSlide = homeVisibleCategories.length > homeVisibleCount;

        if (homeCarousel?.prev) {
            homeCarousel.prev.disabled = !canSlide || homeCategoryPage <= 0;
        }

        if (homeCarousel?.next) {
            homeCarousel.next.disabled = !canSlide || homeCategoryPage >= maxPage;
        }
    };

    const updateHomeCarouselPosition = () => {
        if (!homeCarousel?.grid || !homeCarousel.viewport) {
            return;
        }

        const viewportWidth = homeCarousel.viewport.clientWidth;
        if (!viewportWidth) {
            homeCarousel.grid.style.transform = "translateX(0px)";
            syncHomeCarouselControls();
            return;
        }

        const maxOffset = Math.max(0, homeCarousel.grid.scrollWidth - viewportWidth);
        const requestedOffset = homeCategoryPage * viewportWidth;
        const offset = Math.min(requestedOffset, maxOffset);
        homeCarousel.grid.style.transform = `translateX(-${offset}px)`;
        syncHomeCarouselControls();
    };

    function updateHomeCarouselLayout() {
        if (!homeCarousel?.grid || !homeCarousel.viewport) {
            return;
        }

        homeVisibleCount = getHomeVisibleCount();
        homeCategoryPage = Math.min(homeCategoryPage, getHomeMaxPage());

        const cards = Array.from(homeCarousel.grid.querySelectorAll(".help-card"));
        if (!cards.length) {
            homeCarousel.grid.style.transform = "translateX(0px)";
            syncHomeCarouselControls();
            return;
        }

        const viewportWidth = homeCarousel.viewport.clientWidth;
        const gap = getHomeCarouselGap();
        const cardWidth = Math.max(0, (viewportWidth - (gap * (homeVisibleCount - 1))) / homeVisibleCount);

        cards.forEach((card) => {
            card.style.flex = `0 0 ${cardWidth}px`;
        });

        updateHomeCarouselPosition();
    }

    const renderHomeCategories = (categories) => {
        if (!homeCarousel?.grid) {
            return;
        }

        homeVisibleCategories = categories;
        homeCategoryPage = 0;
        homeCarousel.grid.innerHTML = "";

        categories.forEach((category) => {
            const card = document.createElement("article");
            card.className = "help-card";

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
            link.textContent = "View topic";

            card.appendChild(iconWrap);
            card.appendChild(content);
            card.appendChild(link);
            homeCarousel.grid.appendChild(card);
        });

        window.requestAnimationFrame(updateHomeCarouselLayout);
    };

    const renderFeaturedFaqs = (faqs) => {
        const container = page.querySelector("[data-help-featured-faqs]");
        if (!container) {
            return;
        }

        renderFaqItems(container, faqs, "faq-item", "faq-question");
    };

    const renderSearchResults = (results, query) => {
        const wrapper = page.querySelector("[data-help-search-container]");
        const list = page.querySelector("[data-help-search-results]");
        const meta = page.querySelector("[data-help-search-meta]");
        const empty = page.querySelector("[data-help-search-empty]");
        if (!wrapper || !list || !meta || !empty) {
            return;
        }

        if (!query) {
            wrapper.hidden = true;
            list.innerHTML = "";
            empty.hidden = true;
            meta.textContent = "";
            return;
        }

        wrapper.hidden = false;
        list.innerHTML = "";
        empty.hidden = results.length > 0;
        meta.textContent = `${results.length} match${results.length === 1 ? "" : "es"} for "${query}"`;

        results.forEach((result, index) => {
            const item = document.createElement("div");
            item.className = "faq-item";

            const question = document.createElement("div");
            question.className = "faq-question";
            question.setAttribute("role", "button");
            question.setAttribute("tabindex", "0");

            const number = document.createElement("span");
            number.className = "faq-number";
            number.textContent = String(index + 1);

            const label = document.createElement("span");
            label.textContent = `${result.question} (${result.categoryTitle})`;

            const arrow = document.createElement("span");
            arrow.className = "arrow";
            arrow.setAttribute("aria-hidden", "true");
            arrow.textContent = ">";

            question.appendChild(number);
            question.appendChild(label);
            question.appendChild(arrow);

            const answer = document.createElement("div");
            answer.className = "faq-answer";

            const body = document.createElement("div");
            body.textContent = result.answer;

            const link = document.createElement("a");
            link.className = "help-search-link";
            link.href = `/Help/${encodeURIComponent(result.categorySlug)}`;
            link.textContent = `Open ${result.categoryTitle}`;

            answer.appendChild(body);
            answer.appendChild(link);
            item.appendChild(question);
            item.appendChild(answer);
            list.appendChild(item);
        });

        wireAccordion(".faq-question", ".faq-item", list);
    };

    const loadIndexPage = async () => {
        try {
            const home = await fetchJson("/api/help/home");
            const categories = home.categories || [];
            if (categories.length) {
                renderHomeCategories(categories);
            } else {
                setHomeCategoryMessage("No help topics are available right now.", "help-empty-state");
            }
            renderFeaturedFaqs(home.featuredFaqs || []);
        } catch (error) {
            const featured = page.querySelector("[data-help-featured-faqs]");
            setHomeCategoryMessage(error.message, "help-error-state");
            if (featured) {
                featured.innerHTML = `<div class="help-error-state">${error.message}</div>`;
            }
        }

        homeCarousel?.prev?.addEventListener("click", () => {
            if (homeCategoryPage <= 0) {
                return;
            }

            homeCategoryPage -= 1;
            updateHomeCarouselPosition();
        });

        homeCarousel?.next?.addEventListener("click", () => {
            const maxPage = getHomeMaxPage();
            if (homeCategoryPage >= maxPage) {
                return;
            }

            homeCategoryPage += 1;
            updateHomeCarouselPosition();
        });

        window.addEventListener("resize", updateHomeCarouselLayout);

        const input = page.querySelector("[data-help-search-input]");
        if (!input) {
            return;
        }

        let searchTimer;
        input.addEventListener("input", () => {
            const query = input.value.trim();
            window.clearTimeout(searchTimer);

            if (!query) {
                renderSearchResults([], "");
                return;
            }

            searchTimer = window.setTimeout(async () => {
                try {
                    const results = await fetchJson(`/api/help/search?query=${encodeURIComponent(query)}`);
                    renderSearchResults(results || [], query);
                } catch (error) {
                    renderSearchResults([], query);
                    const meta = page.querySelector("[data-help-search-meta]");
                    if (meta) {
                        meta.textContent = error.message;
                    }
                }
            }, 220);
        });
    };

    const loadTopicPage = async () => {
        const slug = page.getAttribute("data-help-slug");
        if (!slug) {
            return;
        }

        try {
            const topic = await fetchJson(`/api/help/categories/${encodeURIComponent(slug)}`);
            setTopicHeader(topic);
            renderTopicFaqs(topic.faqs || []);
        } catch (error) {
            const container = page.querySelector("[data-help-topic-faqs]");
            if (container) {
                container.innerHTML = `<div class="help-error-state">${error.message}</div>`;
            }
        }
    };

    const populateTicketCategories = (categories, selectedSlug) => {
        const select = page.querySelector("[data-help-ticket-category]");
        if (!select) {
            return;
        }

        categories.forEach((category) => {
            const option = document.createElement("option");
            option.value = category.slug;
            option.textContent = category.title;
            option.selected = category.slug === selectedSlug;
            select.appendChild(option);
        });
    };

    const renderContactChannels = (channels) => {
        const container = page.querySelector("[data-help-contact-channels]");
        if (!container) {
            return;
        }

        container.innerHTML = "";

        channels.forEach((channel) => {
            const card = document.createElement("article");
            card.className = "help-contact-card";

            const label = document.createElement("h3");
            label.textContent = channel.label;

            const value = channel.actionHref
                ? document.createElement("a")
                : document.createElement("div");

            value.className = "help-contact-card-value";
            value.textContent = channel.displayText;

            if (channel.actionHref) {
                value.href = channel.actionHref;
            }

            const meta = document.createElement("p");
            meta.className = "help-contact-card-meta";
            meta.textContent = channel.channelType;

            card.appendChild(label);
            card.appendChild(value);
            card.appendChild(meta);
            container.appendChild(card);
        });
    };

    const setTicketMessage = (message, isError) => {
        const element = page.querySelector("[data-help-ticket-message]");
        if (!element) {
            return;
        }

        element.hidden = false;
        element.className = `help-ticket-message ${isError ? "is-error" : "is-success"}`;
        element.textContent = message;
    };

    const clearTicketMessage = () => {
        const element = page.querySelector("[data-help-ticket-message]");
        if (!element) {
            return;
        }

        element.hidden = true;
        element.className = "help-ticket-message";
        element.textContent = "";
    };

    const wireTicketForm = () => {
        const form = page.querySelector("[data-help-ticket-form]");
        if (!form) {
            return;
        }

        form.addEventListener("submit", async (event) => {
            event.preventDefault();
            clearTicketMessage();

            const submitButton = form.querySelector("button[type='submit']");
            if (submitButton) {
                submitButton.disabled = true;
                submitButton.textContent = "Sending...";
            }

            try {
                const token = await ensureCsrfToken();
                const formData = new FormData(form);
                const payload = {
                    categorySlug: (formData.get("categorySlug") || "").toString() || null,
                    subject: (formData.get("subject") || "").toString(),
                    body: (formData.get("body") || "").toString(),
                };

                const result = await fetchJson("/api/help/tickets", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "X-CSRF-TOKEN": token,
                    },
                    body: JSON.stringify(payload),
                });

                form.reset();
                setTicketMessage(`Ticket ${result.referenceCode} was created successfully.`, false);
            } catch (error) {
                setTicketMessage(error.message, true);
            } finally {
                if (submitButton) {
                    submitButton.disabled = false;
                    submitButton.textContent = "Send ticket";
                }
            }
        });
    };

    const loadContactPage = async () => {
        const slug = page.getAttribute("data-help-slug") || "contact";

        try {
            const [topic, contact, categories] = await Promise.all([
                fetchJson(`/api/help/categories/${encodeURIComponent(slug)}`),
                fetchJson("/api/help/contact"),
                fetchJson("/api/help/categories"),
            ]);

            setTopicHeader(topic);
            renderTopicFaqs(topic.faqs || []);
            renderContactChannels(contact.channels || []);
            populateTicketCategories(categories || [], slug);
            wireTicketForm();
        } catch (error) {
            const faqContainer = page.querySelector("[data-help-topic-faqs]");
            const contactContainer = page.querySelector("[data-help-contact-channels]");
            if (faqContainer) {
                faqContainer.innerHTML = `<div class="help-error-state">${error.message}</div>`;
            }
            if (contactContainer) {
                contactContainer.innerHTML = `<div class="help-error-state">${error.message}</div>`;
            }
        }
    };

    if (view === "index") {
        loadIndexPage();
        return;
    }

    if (view === "contact") {
        loadContactPage();
        return;
    }

    loadTopicPage();
});
