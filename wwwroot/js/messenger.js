(function () {
    const config = window.sellerMessengerConfig || window.messengerPageConfig;
    const root = document.querySelector("[data-seller-messenger-root]") || document.querySelector("[data-messenger-root]");

    if (!config || !root) {
        return;
    }

    const configuredRole = String(config.messagingRole || config.role || "").toLowerCase();
    const role = configuredRole === "consumer" ? "consumer" : "seller";
    const pollIntervalMs = 5000;
    let csrfTokenPromise = null;

    const elements = {
        list: root.querySelector("[data-conversations-list]"),
        search: root.querySelector("[data-conversation-search]"),
        noResults: root.querySelector("[data-no-results]"),
        status: root.querySelector("[data-messenger-status]"),
        title: root.querySelector("[data-chat-title]"),
        subtitle: root.querySelector("[data-chat-subtitle]"),
        avatar: root.querySelector("[data-chat-avatar]"),
        messages: root.querySelector("[data-messages-container]"),
        input: root.querySelector("[data-message-input]"),
        attachmentInput: root.querySelector("[data-message-attachment-input]"),
        attachmentButton: root.querySelector("[data-attach-message]"),
        attachmentPreview: root.querySelector("[data-composer-attachment-preview]"),
        send: root.querySelector("[data-send-message]"),
        refresh: root.querySelector("[data-refresh-conversations]")
    };

    const state = {
        conversations: [],
        activeConversationId: null,
        currentUserId: String(config.actorUserId || config.currentUserId || ""),
        search: "",
        selectedAttachment: null,
        selectedAttachmentPreviewUrl: null,
        sharedProductDetailsByProductId: {},
        sharedProductDetailsRequestsByProductId: {},
        isRefreshingConversations: false,
        pollHandle: null
    };

    // FAQ quick actions initializer (shows bolt FAB and quick action buttons)
    (function initFaqQuickActions() {
        try {
            const panel = root.querySelector('[data-faq-panel]');
            if (!panel) return;

            const listNode = root.querySelector('[data-faq-list]');
            const fab = root.querySelector('[data-faq-fab]');
            const titleNode = root.querySelector('[data-faq-title]');
            const statusNode = root.querySelector('[data-faq-status]');
            const backBtn = root.querySelector('[data-faq-back]');

            function setFaqStatus(message) {
                if (!statusNode) return;
                const text = String(message || '').trim();
                statusNode.textContent = text;
                statusNode.hidden = !text;
            }

            function renderFaqButtons(items) {
                if (!listNode) return;
                listNode.innerHTML = '';
                (items || []).forEach(function (item) {
                    const btn = document.createElement('button');
                    btn.type = 'button';
                    btn.className = 'faq-quick-action-btn question';
                    btn.textContent = item.label || item.question || item.title || 'Quick Action';
                    btn.addEventListener('click', function () {
                        if (elements && elements.input) {
                            elements.input.value = item.value || item.answer || item.question || '';
                            elements.input.focus();
                        }
                    });
                    listNode.appendChild(btn);
                });
            }

            async function loadFaqs() {
                const sellerId = config.sellerId || config.sellerUserId || config.seller?.id || null;

                try {
                    setFaqStatus('Loading quick actions...');
                    const faqUrl = sellerId
                        ? '/api/messages/storefront/faqs?sellerUserId=' + encodeURIComponent(sellerId)
                        : '/api/messages/storefront/faqs';
                    const resp = await fetch(faqUrl, { credentials: 'same-origin' });
                    if (!resp.ok) throw new Error('Unable to load FAQs');
                    const payload = await resp.json();
                    const items = Array.isArray(payload) ? payload : (payload && payload.faqs) ? payload.faqs : [];
                    if (!items.length) {
                        setFaqStatus('No quick actions available');
                        panel.hidden = false;
                        panel.classList.remove('faq-quick-actions--pending');
                        if (fab) fab.hidden = false;
                        return;
                    }

                    renderFaqButtons(items);
                    setFaqStatus('');
                    panel.hidden = false;
                    panel.classList.remove('faq-quick-actions--pending');
                    if (fab) fab.hidden = false;
                } catch (err) {
                    setFaqStatus('Unable to load quick actions');
                    panel.hidden = false;
                    panel.classList.remove('faq-quick-actions--pending');
                    if (fab) fab.hidden = true;
                    console.warn('FAQ load failed', err);
                }
            }

            if (fab) {
                fab.hidden = false;
                fab.addEventListener('click', function () {
                    const isExpanded = fab.getAttribute('aria-expanded') === 'true';
                    fab.setAttribute('aria-expanded', String(!isExpanded));
                    panel.classList.toggle('faq-quick-actions--fab-expanded', !isExpanded);
                    fab.classList.toggle('is-active', !isExpanded);
                });
            }

            if (backBtn) backBtn.addEventListener('click', function () {
                if (listNode) listNode.scrollTop = 0;
            });

            // load on init
            loadFaqs();
        } catch (e) {
            console.warn('initFaqQuickActions error', e);
        }
    })();

    function buildUrl(path, query) {
        const url = new URL(path, window.location.origin);
        Object.keys(query || {}).forEach(function (key) {
            const value = query[key];
            if (value === null || value === undefined || value === "") {
                return;
            }

            url.searchParams.set(key, String(value));
        });
        return url.toString();
    }

    function parseResponse(response) {
        return response.text().then(function (text) {
            if (!text) {
                return null;
            }

            try {
                return JSON.parse(text);
            } catch (_) {
                return text;
            }
        });
    }

    function normalizeConversation(item) {
        return {
            conversationId: Number(item.conversationId || item.ConversationId || 0),
            currentUserId: String(item.currentUserId || item.CurrentUserId || ""),
            displayName: String(item.displayName || item.DisplayName || ""),
            displaySubtitle: String(item.displaySubtitle || item.DisplaySubtitle || ""),
            avatarUrl: String(item.avatarUrl || item.AvatarUrl || ""),
            contextLabel: String(item.contextLabel || item.ContextLabel || ""),
            counterpartyId: String(item.counterpartyId || item.CounterpartyId || ""),
            canReply: item.canReply !== undefined ? Boolean(item.canReply) : Boolean(item.CanReply),
            orderId: item.orderId || item.OrderId || null,
            lastMessagePreview: String(item.lastMessagePreview || item.LastMessagePreview || ""),
            unreadCount: Number(item.unreadCount || item.UnreadCount || 0),
            updatedAt: item.updatedAt || item.UpdatedAt || item.lastMessageAt || item.LastMessageAt || null
        };
    }

    function normalizeMessage(item) {
        return {
            messageId: String(item.messageId || item.MessageId || ""),
            senderUserId: String(item.senderUserId || item.SenderUserId || ""),
            body: String(item.body || item.Body || ""),
            attachmentUrl: String(item.attachmentUrl || item.AttachmentUrl || ""),
            sentAt: item.sentAt || item.SentAt || null,
            isDeleted: Boolean(item.isDeleted || item.IsDeleted)
        };
    }

    function setStatus(message, isError) {
        if (!elements.status) {
            return;
        }

        const text = String(message || "").trim();
        elements.status.textContent = text;
        elements.status.hidden = !text;
        elements.status.classList.toggle("is-error", Boolean(text) && Boolean(isError));
    }

    function setComposerEnabled(enabled) {
        const isEnabled = Boolean(enabled);
        elements.input.disabled = !isEnabled;
        elements.attachmentButton.disabled = !isEnabled;
        elements.attachmentInput.disabled = !isEnabled;
        elements.send.disabled = !isEnabled;
    }

    function initialsFromName(name) {
        const parts = String(name || "")
            .split(/\s+/)
            .map(function (value) { return value.trim(); })
            .filter(Boolean);

        if (parts.length === 0) {
            return "--";
        }

        return parts.slice(0, 2).map(function (part) {
            return part.charAt(0).toUpperCase();
        }).join("");
    }

    function formatTime(value) {
        const date = parseServerDate(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        return date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
    }

    function formatDateTime(value) {
        const date = parseServerDate(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        return date.toLocaleString([], { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" });
    }

    function parseServerDate(value) {
        if (!value) {
            return new Date("");
        }

        if (value instanceof Date) {
            return value;
        }

        const source = String(value).trim();
        if (!source) {
            return new Date("");
        }

        return new Date(source);
    }

    function attachmentExtension(value) {
        const source = String(value || "").split("#")[0].split("?")[0];
        const lastSegment = source.split("/").pop() || "";
        const lastDot = lastSegment.lastIndexOf(".");
        return lastDot >= 0 ? lastSegment.slice(lastDot + 1).toLowerCase() : "";
    }

    function attachmentKind(value) {
        const source = String(value || "").toLowerCase();

        // Binary-backed chat attachments are served from the API without a file extension.
        if (source.includes("/api/messages/messages/") && source.includes("/attachment")) {
            return "image";
        }

        const extension = attachmentExtension(value);
        if (["jpg", "jpeg", "png", "webp"].includes(extension)) {
            return "image";
        }

        if (["mp4", "webm", "mov"].includes(extension)) {
            return "video";
        }

        return "file";
    }

    function canUseAttachment(file) {
        if (!file) {
            return "";
        }

        const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
        const allowedExtensions = ["jpg", "jpeg", "png", "webp"];
        const extension = attachmentExtension(file.name || "");
        const type = String(file.type || "").toLowerCase();

        if (Number(file.size || 0) > 5 * 1024 * 1024) {
            return "Attachment must be 5MB or smaller.";
        }

        if (!allowedTypes.includes(type) && !allowedExtensions.includes(extension)) {
            return "Attachment must be a jpg, jpeg, png, or webp image.";
        }

        return "";
    }

    function formatBytes(bytes) {
        const value = Number(bytes || 0);
        if (value <= 0) {
            return "";
        }

        if (value < 1024 * 1024) {
            return Math.max(1, Math.round(value / 1024)) + " KB";
        }

        return (value / (1024 * 1024)).toFixed(1) + " MB";
    }

    function stockLabel(stock) {
        const quantity = Number(stock || 0);
        if (quantity <= 0) {
            return "Out of stock";
        }

        if (quantity === 1) {
            return "1 stock left";
        }

        return quantity + " stocks";
    }

    function absoluteUrlOrEmpty(value) {
        const raw = String(value || "").trim();
        if (!raw) {
            return "";
        }

        try {
            return new URL(raw, window.location.origin).toString();
        } catch (_) {
            return "";
        }
    }

    function fallbackProductImageUrl() {
        return "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 96 96'%3E%3Crect width='96' height='96' rx='18' fill='%23f3f4f6'/%3E%3Cpath d='M30 62l12-15 10 11 8-9 14 13' fill='none' stroke='%2394a3b8' stroke-width='6' stroke-linecap='round' stroke-linejoin='round'/%3E%3Ccircle cx='38' cy='36' r='6' fill='%23cbd5e1'/%3E%3C/svg%3E";
    }

    function normalizeSharedProductImageUrl(value) {
        const raw = String(value || "").trim();
        if (!raw) {
            return "";
        }

        const legacyVariantMatch = raw.match(/\/api\/Products\/variant-image\/(\d+)(?:[/?#]|$)/i);
        if (legacyVariantMatch) {
            return new URL("/ProductImage/Variant/" + legacyVariantMatch[1], window.location.origin).toString();
        }

        const currentVariantMatch = raw.match(/\/ProductImage\/Variant\/(\d+)(?:[/?#]|$)/i);
        if (currentVariantMatch) {
            return new URL("/ProductImage/Variant/" + currentVariantMatch[1], window.location.origin).toString();
        }

        return absoluteUrlOrEmpty(raw);
    }

    function buildSellerProductUrl(productId) {
        const resolvedId = Number(productId || 0);
        if (resolvedId <= 0) {
            return "";
        }

        return new URL("/Seller/ViewProduct?id=" + resolvedId + "&state=active", window.location.origin).toString();
    }

    function normalizeSharedProductUrl(value, productId) {
        const raw = String(value || "").trim();
        if (!raw) {
            return buildSellerProductUrl(productId);
        }

        let parsed;
        try {
            parsed = new URL(raw, window.location.origin);
        } catch (_) {
            return buildSellerProductUrl(productId);
        }

        const isLegacyConsumerRoute = /^\/Home\/Product$/i.test(parsed.pathname);
        if (isLegacyConsumerRoute && Number(productId || 0) > 0) {
            return buildSellerProductUrl(productId);
        }

        return parsed.toString();
    }

    function sharedProductCacheKey(productId) {
        const resolvedId = Number(productId || 0);
        return resolvedId > 0 ? String(resolvedId) : "";
    }

    function fetchSharedProductDetails(productId) {
        const cacheKey = sharedProductCacheKey(productId);
        if (!cacheKey) {
            return Promise.resolve(null);
        }

        const cached = state.sharedProductDetailsByProductId[cacheKey];
        if (cached) {
            return Promise.resolve(cached);
        }

        const inFlight = state.sharedProductDetailsRequestsByProductId[cacheKey];
        if (inFlight) {
            return inFlight;
        }

        const requestPromise = fetch("/seller/products/" + cacheKey + "/shared-card", {
            credentials: "same-origin"
        }).then(function (response) {
            if (!response.ok) {
                throw new Error("Unable to load live product details.");
            }

            return response.json();
        }).then(function (payload) {
            const resolved = payload ? {
                productId: Number(payload.productId || cacheKey),
                productName: String(payload.productName || "").trim() || "Product",
                imageUrl: normalizeSharedProductImageUrl(payload.imageUrl),
                price: Number.isFinite(Number(payload.price)) ? Number(payload.price) : null,
                stock: Number.isFinite(Number(payload.stock)) ? Number(payload.stock) : 0,
                productUrl: normalizeSharedProductUrl(payload.productUrl, payload.productId || cacheKey)
            } : null;
            state.sharedProductDetailsByProductId[cacheKey] = resolved;
            return resolved;
        }).catch(function () {
            return null;
        }).finally(function () {
            delete state.sharedProductDetailsRequestsByProductId[cacheKey];
        });

        state.sharedProductDetailsRequestsByProductId[cacheKey] = requestPromise;
        return requestPromise;
    }

    function createProductImageElement(product, className) {
        const image = document.createElement("img");
        image.className = className;
        image.alt = String(product && product.productName ? product.productName : "Product");
        image.loading = "lazy";
        const normalizedImageUrl = normalizeSharedProductImageUrl(product && product.imageUrl);
        image.src = normalizedImageUrl || fallbackProductImageUrl();
        image.addEventListener("error", function () {
            image.src = fallbackProductImageUrl();
        }, { once: true });

        return image;
    }

    function formatProductPrice(value) {
        const amount = Number(value);
        if (!Number.isFinite(amount)) {
            return "Price unavailable";
        }

        return new Intl.NumberFormat(undefined, {
            style: "currency",
            currency: "PHP",
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount);
    }

    function parseSharedProductMessage(body) {
        const text = String(body || "").trim();
        if (!text) {
            return null;
        }

        if (text.startsWith("[[product-share]]")) {
            const lines = text.split(/\r?\n/).slice(1);
            const fields = {};

            lines.forEach(function (line) {
                const separator = line.indexOf("=");
                if (separator <= 0) {
                    return;
                }

                const key = line.slice(0, separator).trim().toLowerCase();
                const value = line.slice(separator + 1).trim();
                if (!key) {
                    return;
                }

                try {
                    fields[key] = decodeURIComponent(value);
                } catch (_) {
                    fields[key] = value;
                }
            });

            const productId = Number(fields.id || 0);
            const productName = String(fields.name || "").trim();
            if (productId <= 0 && !productName) {
                return null;
            }

            const parsedPrice = fields.price !== undefined && fields.price !== "" ? Number(fields.price) : null;
            const parsedStock = fields.stock !== undefined && fields.stock !== "" ? Number(fields.stock) : 0;

            return {
                productId: productId,
                productName: productName || "Product",
                imageUrl: normalizeSharedProductImageUrl(fields.image),
                price: Number.isFinite(parsedPrice) ? parsedPrice : null,
                stock: Number.isFinite(parsedStock) ? parsedStock : 0,
                productUrl: normalizeSharedProductUrl(fields.url, productId),
                isSharedProduct: true
            };
        }

        const legacyMatch = text.match(/^Product:\s*(.+?)(?:\r?\n(https?:\/\/\S+|\/\S+))?$/i);
        if (!legacyMatch) {
            return null;
        }

        return {
            productId: 0,
            productName: String(legacyMatch[1] || "").trim() || "Product",
            imageUrl: "",
            price: null,
            stock: 0,
            productUrl: normalizeSharedProductUrl(legacyMatch[2] || "", 0),
            isSharedProduct: true
        };
    }

    function sharedProductPreviewText(body) {
        const product = parseSharedProductMessage(body);
        return product ? ("Shared product: " + product.productName) : String(body || "");
    }

    function applySharedProductDetails(card, product) {
        if (!card || !product) {
            return;
        }

        const image = card.querySelector(".shared-product-card-image");
        if (image && product.imageUrl) {
            image.src = product.imageUrl;
        }

        const price = card.querySelector(".shared-product-card-price");
        if (price) {
            price.textContent = formatProductPrice(product.price);
        }

        const stock = card.querySelector(".shared-product-card-stock");
        if (stock) {
            stock.textContent = stockLabel(product.stock);
        }

        const name = card.querySelector(".shared-product-card-name");
        if (name && product.productName) {
            name.textContent = product.productName;
        }

        const href = normalizeSharedProductUrl(product.productUrl, product.productId);
        if (card.tagName === "A") {
            card.href = href || "#";
        }

        const cta = card.querySelector(".shared-product-card-cta");
        if (cta) {
            cta.textContent = href ? "View product" : "Product shared";
        }
    }

    function renderSharedProductCard(product) {
        const href = normalizeSharedProductUrl(product && product.productUrl, product && product.productId) || "#";
        const card = document.createElement(href === "#" ? "div" : "a");
        card.className = "shared-product-card";

        if (href !== "#") {
            card.href = href;
            card.target = "_blank";
            card.rel = "noopener noreferrer";
        }

        const media = document.createElement("span");
        media.className = "shared-product-card-media";
        media.appendChild(createProductImageElement(product, "shared-product-card-image"));

        const content = document.createElement("span");
        content.className = "shared-product-card-content";

        const badge = document.createElement("span");
        badge.className = "shared-product-card-badge";
        badge.textContent = "Shared product";

        const name = document.createElement("span");
        name.className = "shared-product-card-name";
        name.textContent = String(product && product.productName ? product.productName : "Product");

        const meta = document.createElement("span");
        meta.className = "shared-product-card-meta";

        const price = document.createElement("span");
        price.className = "shared-product-card-price";
        price.textContent = formatProductPrice(product && product.price);

        const stock = document.createElement("span");
        stock.className = "shared-product-card-stock";
        stock.textContent = Number(product && product.productId || 0) > 0
            ? "Loading stock..."
            : stockLabel(product && product.stock);

        const cta = document.createElement("span");
        cta.className = "shared-product-card-cta";
        cta.textContent = href === "#" ? "Product shared" : "View product";

        meta.appendChild(price);
        meta.appendChild(stock);
        content.appendChild(badge);
        content.appendChild(name);
        content.appendChild(meta);
        content.appendChild(cta);

        card.appendChild(media);
        card.appendChild(content);

        if (Number(product && product.productId || 0) > 0) {
            fetchSharedProductDetails(product.productId).then(function (liveProduct) {
                if (!liveProduct) {
                    stock.textContent = stockLabel(product && product.stock);
                    return;
                }

                applySharedProductDetails(card, liveProduct);
            });
        }

        return card;
    }

    function activeConversation() {
        return state.conversations.find(function (item) {
            return item.conversationId === state.activeConversationId;
        }) || null;
    }

    function conversationSignature(conversation) {
        if (!conversation) {
            return "";
        }

        return [
            conversation.conversationId,
            conversation.updatedAt || "",
            conversation.lastMessagePreview || "",
            conversation.unreadCount || 0
        ].join("|");
    }

    function syncConversationHeader(conversation) {
        if (!conversation) {
            elements.title.textContent = "Conversation";
            elements.subtitle.textContent = "Select a conversation to start messaging.";
            elements.avatar.textContent = "--";
            setComposerEnabled(false);
            return;
        }

        elements.title.textContent = conversation.displayName || "Conversation";
        elements.subtitle.textContent = conversation.displaySubtitle || conversation.contextLabel || "General inquiry";
        elements.avatar.textContent = initialsFromName(conversation.displayName);

        if (conversation.currentUserId) {
            state.currentUserId = conversation.currentUserId;
        }

        setComposerEnabled(conversation.canReply !== false);
    }

    function filteredConversations() {
        const query = state.search.trim().toLowerCase();
        if (!query) {
            return state.conversations;
        }

        return state.conversations.filter(function (conversation) {
            return [
                conversation.displayName,
                conversation.displaySubtitle,
                conversation.contextLabel,
                conversation.lastMessagePreview,
                conversation.orderId ? "order " + conversation.orderId : ""
            ].join(" ").toLowerCase().includes(query);
        });
    }

    function renderConversationList() {
        if (!elements.list || !elements.noResults) {
            return;
        }

        const conversations = filteredConversations();
        elements.list.innerHTML = "";
        elements.noResults.hidden = conversations.length > 0;

        conversations.forEach(function (conversation) {
            const item = document.createElement("button");
            item.type = "button";
            item.className = "conversation-item" + (conversation.conversationId === state.activeConversationId ? " active" : "");

            const avatar = document.createElement("div");
            avatar.className = "conversation-avatar";
            avatar.innerHTML = '<div class="avatar-placeholder">' + initialsFromName(conversation.displayName) + "</div>";

            const info = document.createElement("div");
            info.className = "conversation-info";

            const name = document.createElement("div");
            name.className = "conversation-name";
            name.textContent = conversation.displayName || "Conversation";

            const meta = document.createElement("div");
            meta.className = "conversation-meta";

            const preview = document.createElement("span");
            preview.className = "last-message";
            preview.textContent = sharedProductPreviewText(conversation.lastMessagePreview) || "No messages yet.";
            meta.appendChild(preview);

            if (conversation.unreadCount > 0) {
                const unread = document.createElement("span");
                unread.className = "unread-badge";
                unread.textContent = String(conversation.unreadCount);
                meta.appendChild(unread);
            }

            const time = document.createElement("div");
            time.className = "conversation-time";
            time.textContent = formatDateTime(conversation.updatedAt);

            info.appendChild(name);
            info.appendChild(meta);
            info.appendChild(time);
            item.appendChild(avatar);
            item.appendChild(info);

            item.addEventListener("click", function () {
                if (conversation.conversationId === state.activeConversationId) {
                    return;
                }

                state.activeConversationId = conversation.conversationId;
                renderConversationList();
                syncConversationHeader(conversation);
                loadMessages(true);
            });

            elements.list.appendChild(item);
        });
    }

    function renderEmptyMessages(message) {
        elements.messages.innerHTML = "";
        const empty = document.createElement("div");
        empty.className = "empty-state";
        empty.textContent = message || "";
        elements.messages.appendChild(empty);
    }

    function scrollMessagesToBottom() {
        window.requestAnimationFrame(function () {
            elements.messages.scrollTop = elements.messages.scrollHeight;
        });
    }

    function ensureImageViewer() {
        if (state.imageViewer) {
            return state.imageViewer;
        }

        const viewer = document.createElement("div");
        viewer.className = "messenger-image-viewer";
        viewer.hidden = true;
        viewer.innerHTML = [
            '<button type="button" class="messenger-image-viewer__backdrop" data-image-viewer-close aria-label="Close image viewer"></button>',
            '<div class="messenger-image-viewer__dialog" role="dialog" aria-modal="true" aria-label="Image viewer">',
            '  <button type="button" class="messenger-image-viewer__close" data-image-viewer-close aria-label="Close image viewer">&times;</button>',
            '  <img class="messenger-image-viewer__image" alt="Message attachment preview">',
            '</div>'
        ].join("");

        viewer.querySelectorAll("[data-image-viewer-close]").forEach(function (button) {
            button.addEventListener("click", closeImageViewer);
        });

        const image = viewer.querySelector(".messenger-image-viewer__image");
        const dialog = viewer.querySelector(".messenger-image-viewer__dialog");
        dialog.addEventListener("click", function (event) {
            event.stopPropagation();
        });

        document.body.appendChild(viewer);
        state.imageViewer = viewer;
        state.imageViewerFrame = dialog;
        state.imageViewerImage = image;
        return viewer;
    }

    function openImageViewer(url) {
        const viewer = ensureImageViewer();
        state.imageViewerImage.src = url;
        viewer.hidden = false;
        document.body.classList.add("messenger-image-viewer-open");
    }

    function closeImageViewer() {
        if (!state.imageViewer) {
            return;
        }

        state.imageViewer.hidden = true;
        if (state.imageViewerImage) {
            state.imageViewerImage.removeAttribute("src");
        }
        document.body.classList.remove("messenger-image-viewer-open");
    }
    function createAttachmentPreview(url) {
        const kind = attachmentKind(url);
        const wrapper = document.createElement("div");
        wrapper.className = "message-attachment";

        if (kind === "image") {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "attachment-preview-button";
            button.addEventListener("click", function () {
                openImageViewer(url);
            });

            const image = document.createElement("img");
            image.className = "attachment-preview attachment-image";
            image.src = url;
            image.alt = "Message attachment";
            button.appendChild(image);
            wrapper.appendChild(button);
            return wrapper;
        }

        if (kind === "video") {
            const video = document.createElement("video");
            video.className = "attachment-preview attachment-video";
            video.src = url;
            video.controls = true;
            video.preload = "metadata";
            wrapper.appendChild(video);
            return wrapper;
        }

        const link = document.createElement("a");
        link.className = "attachment-link";
        link.href = url;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        link.textContent = "Open attachment";
        wrapper.appendChild(link);
        return wrapper;
    }

    function renderMessages(messages) {
        elements.messages.innerHTML = "";

        if (!messages.length) {
            renderEmptyMessages("No messages yet.");
            return;
        }

        messages.forEach(function (message) {
            const isSent = message.senderUserId === state.currentUserId;
            const sharedProduct = message.isDeleted ? null : parseSharedProductMessage(message.body);
            const wrapper = document.createElement("div");
            wrapper.className = "message " + (isSent ? "sent" : "received");

            const content = document.createElement("div");
            content.className = "message-content";
            if (sharedProduct) {
                content.classList.add("message-content--product-share");
            }

            if (message.isDeleted || (message.body && !sharedProduct)) {
                const text = document.createElement("p");
                text.textContent = message.isDeleted ? "[message deleted]" : message.body;
                content.appendChild(text);
            }

            if (sharedProduct) {
                content.appendChild(renderSharedProductCard(sharedProduct));
            }

            if (message.attachmentUrl) {
                content.appendChild(createAttachmentPreview(message.attachmentUrl));
            }

            const time = document.createElement("span");
            time.className = "message-time";
            time.textContent = formatTime(message.sentAt);

            wrapper.appendChild(content);
            wrapper.appendChild(time);
            elements.messages.appendChild(wrapper);
        });

        scrollMessagesToBottom();
    }

    function buildHeaders(includeJson) {
        const headers = {};
        if (includeJson) {
            headers["Content-Type"] = "application/json";
        }
        return headers;
    }

    function request(path, options, query) {
        return fetch(buildUrl(path, query), Object.assign({
            credentials: "same-origin"
        }, options || {})).then(function (response) {
            return parseResponse(response).then(function (payload) {
                if (response.ok) {
                    return payload;
                }

                const message = typeof payload === "string"
                    ? payload
                    : payload && payload.title
                        ? payload.title
                        : "Messaging request failed.";
                throw new Error(message);
            });
        });
    }

    function getCsrfToken() {
        if (!csrfTokenPromise) {
            csrfTokenPromise = request("/api/security/csrf-token", { headers: buildHeaders(false) })
                .then(function (payload) {
                    return payload && payload.token ? payload.token : "";
                })
                .catch(function (error) {
                    csrfTokenPromise = null;
                    throw error;
                });
        }

        return csrfTokenPromise;
    }

    function requestWithCsrf(path, options, query) {
        return getCsrfToken().then(function (token) {
            const headers = Object.assign({}, options && options.headers ? options.headers : {});
            if (token) {
                headers["X-CSRF-TOKEN"] = token;
            }

            return request(path, Object.assign({}, options || {}, { headers: headers }), query);
        });
    }

    function chooseInitialConversationId() {
        if (!state.conversations.length) {
            return null;
        }

        const byConversationId = Number(config.initialConversationId || 0);
        if (byConversationId > 0 && state.conversations.some(function (item) { return item.conversationId === byConversationId; })) {
            return byConversationId;
        }

        const byOrderId = Number(config.orderId || 0);
        if (byOrderId > 0) {
            const matchByOrder = state.conversations.find(function (item) {
                return Number(item.orderId || 0) === byOrderId;
            });
            if (matchByOrder) {
                return matchByOrder.conversationId;
            }
        }

        const byConsumerId = String(config.consumerId || "").trim();
        if (byConsumerId) {
            const matchByConsumer = state.conversations.find(function (item) {
                return item.counterpartyId === byConsumerId;
            });
            if (matchByConsumer) {
                return matchByConsumer.conversationId;
            }
        }

        return state.conversations[0].conversationId;
    }

    function listConversations() {
        return request("/api/messages/conversations", { headers: buildHeaders(false) }, {
            role: role,
            page: 1,
            pageSize: 100
        }).then(function (payload) {
            const items = payload && Array.isArray(payload.items) ? payload.items : [];
            return items.map(normalizeConversation);
        });
    }

    function ensureConfiguredConversation() {
        const sellerId = String(config.sellerId || config.sellerUserId || "").trim();
        if (role !== "consumer" || !sellerId) {
            return Promise.resolve();
        }

        return requestWithCsrf("/api/messages/conversations", {
            method: "POST",
            headers: buildHeaders(true),
            body: JSON.stringify({
                contextType: "general",
                sellerUserId: sellerId
            })
        }, { role: role }).then(function (conversation) {
            const conversationId = Number(conversation && (conversation.conversationId || conversation.ConversationId) || 0);
            if (conversationId > 0) {
                config.initialConversationId = conversationId;
                state.activeConversationId = conversationId;
            }
        }).catch(function (error) {
            setStatus(error.message || "Failed to open seller conversation.", true);
        });
    }

    function listMessages(conversationId) {
        return request("/api/messages/conversations/" + conversationId + "/messages", { headers: buildHeaders(false) }, {
            role: role,
            pageSize: 100
        }).then(function (payload) {
            return Array.isArray(payload) ? payload.map(normalizeMessage) : [];
        });
    }

    function markConversationRead(conversationId) {
        return requestWithCsrf("/api/messages/conversations/" + conversationId + "/read", {
            method: "POST",
            headers: buildHeaders(true),
            body: "{}"
        }, { role: role });
    }

    function refreshConversations(forceMessages) {
        if (state.isRefreshingConversations) {
            return Promise.resolve();
        }

        state.isRefreshingConversations = true;
        const previousActiveConversationId = state.activeConversationId;
        const previousActiveSignature = state.activeConversationSignature;

        return listConversations()
            .then(function (conversations) {
                state.conversations = conversations;

                if (!conversations.length) {
                    state.activeConversationId = null;
                    state.activeConversationSignature = "";
                    renderConversationList();
                    syncConversationHeader(null);
                    renderEmptyMessages("No conversations yet.");
                    setStatus("", false);
                    return;
                }

                if (!state.activeConversationId || !conversations.some(function (item) { return item.conversationId === state.activeConversationId; })) {
                    state.activeConversationId = chooseInitialConversationId();
                }

                const currentActiveConversation = activeConversation();
                const currentActiveSignature = conversationSignature(currentActiveConversation);
                const shouldReloadMessages = forceMessages !== false
                    || previousActiveConversationId !== state.activeConversationId
                    || previousActiveSignature !== currentActiveSignature;

                state.activeConversationSignature = currentActiveSignature;

                renderConversationList();
                syncConversationHeader(currentActiveConversation);

                if (shouldReloadMessages) {
                    return loadMessages(forceMessages !== false);
                }

                setStatus("", false);
                return null;
            })
            .catch(function (error) {
                setStatus(error.message || "Failed to load conversations.", true);
                if (!state.conversations.length) {
                    syncConversationHeader(null);
                    renderEmptyMessages("Failed to load conversations.");
                }
            })
            .finally(function () {
                state.isRefreshingConversations = false;
            });
    }

    function loadMessages(markReadAfterLoad) {
        const conversation = activeConversation();
        if (!conversation) {
            syncConversationHeader(null);
            renderEmptyMessages("No conversation selected.");
            return Promise.resolve();
        }

        syncConversationHeader(conversation);

        return listMessages(conversation.conversationId)
            .then(function (messages) {
                renderMessages(messages);
                state.activeConversationSignature = conversationSignature(conversation);
                setStatus("", false);

                if (markReadAfterLoad && conversation.unreadCount > 0) {
                    return markConversationRead(conversation.conversationId).then(function () {
                        conversation.unreadCount = 0;
                        renderConversationList();
                    });
                }

                return null;
            })
            .catch(function (error) {
                setStatus(error.message || "Failed to load messages.", true);
                renderEmptyMessages("Failed to load messages.");
            });
    }

    function clearComposerAttachment() {
        if (state.selectedAttachmentPreviewUrl) {
            URL.revokeObjectURL(state.selectedAttachmentPreviewUrl);
        }

        state.selectedAttachment = null;
        state.selectedAttachmentPreviewUrl = null;
        elements.attachmentInput.value = "";
        elements.attachmentPreview.innerHTML = "";
        elements.attachmentPreview.hidden = true;
    }

    function renderComposerAttachmentPreview() {
        elements.attachmentPreview.innerHTML = "";

        if (!state.selectedAttachment) {
            elements.attachmentPreview.hidden = true;
            return;
        }

        const card = document.createElement("div");
        card.className = "composer-attachment-card";

        const kind = attachmentKind(state.selectedAttachment.name || "");
        if ((kind === "image" || kind === "video") && state.selectedAttachmentPreviewUrl) {
            if (kind === "image") {
                const preview = document.createElement("img");
                preview.className = "composer-attachment-media";
                preview.src = state.selectedAttachmentPreviewUrl;
                preview.alt = state.selectedAttachment.name || "Selected attachment";
                card.appendChild(preview);
            } else {
                const preview = document.createElement("video");
                preview.className = "composer-attachment-media";
                preview.src = state.selectedAttachmentPreviewUrl;
                preview.muted = true;
                preview.playsInline = true;
                preview.preload = "metadata";
                card.appendChild(preview);
            }
        } else {
            const icon = document.createElement("div");
            icon.className = "composer-attachment-file-icon";
            icon.textContent = "FILE";
            card.appendChild(icon);
        }

        const meta = document.createElement("div");
        meta.className = "composer-attachment-meta";

        const name = document.createElement("div");
        name.className = "composer-attachment-name";
        name.textContent = state.selectedAttachment.name || "Attachment";

        const details = document.createElement("div");
        details.className = "composer-attachment-details";
        details.textContent = formatBytes(state.selectedAttachment.size);

        meta.appendChild(name);
        meta.appendChild(details);
        card.appendChild(meta);

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "composer-attachment-remove";
        remove.setAttribute("aria-label", "Remove attachment");
        remove.textContent = "×";
        remove.addEventListener("click", clearComposerAttachment);
        card.appendChild(remove);

        elements.attachmentPreview.appendChild(card);
        elements.attachmentPreview.hidden = false;
    }

    function sendMessage() {
        const conversation = activeConversation();
        if (!conversation || elements.send.disabled) {
            return Promise.resolve();
        }

        const body = elements.input.value.trim();
        const attachment = state.selectedAttachment;

        if (!body && !attachment) {
            return Promise.resolve();
        }

        const attachmentError = canUseAttachment(attachment);
        if (attachmentError) {
            setStatus(attachmentError, true);
            return Promise.resolve();
        }

        const formData = new FormData();
        formData.append("body", body);
        if (attachment) {
            formData.append("attachment", attachment);
        }

        setComposerEnabled(false);

        return requestWithCsrf("/api/messages/conversations/" + conversation.conversationId + "/messages", {
            method: "POST",
            headers: buildHeaders(false),
            body: formData
        }, { role: role })
            .then(function () {
                elements.input.value = "";
                clearComposerAttachment();
                return refreshConversations(true);
            })
            .catch(function (error) {
                setStatus(error.message || "Failed to send message.", true);
            })
            .finally(function () {
                const active = activeConversation();
                setComposerEnabled(Boolean(active) && active.canReply !== false);
            });
    }

    function startPolling() {
        if (state.pollHandle) {
            window.clearInterval(state.pollHandle);
        }

        state.pollHandle = window.setInterval(function () {
            if (document.hidden) {
                return;
            }

            refreshConversations(false);
        }, pollIntervalMs);
    }

    if (elements.search) {
        elements.search.addEventListener("input", function (event) {
            state.search = event.target.value || "";
            renderConversationList();
        });
    }

    elements.attachmentButton.addEventListener("click", function () {
        if (!elements.attachmentButton.disabled) {
            elements.attachmentInput.click();
        }
    });

    elements.attachmentInput.addEventListener("change", function (event) {
        const file = event.target.files && event.target.files.length ? event.target.files[0] : null;
        if (!file) {
            clearComposerAttachment();
            return;
        }

        const attachmentError = canUseAttachment(file);
        if (attachmentError) {
            clearComposerAttachment();
            setStatus(attachmentError, true);
            return;
        }

        clearComposerAttachment();
        state.selectedAttachment = file;
        state.selectedAttachmentPreviewUrl = URL.createObjectURL(file);
        renderComposerAttachmentPreview();
        setStatus("", false);
    });

    elements.input.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            sendMessage();
        }
    });

    elements.send.addEventListener("click", function () {
        sendMessage();
    });

    elements.refresh.addEventListener("click", function () {
        refreshConversations(true);
    });

    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) {
            refreshConversations(false);
        }
    });

    setComposerEnabled(false);
    renderEmptyMessages("");
    ensureConfiguredConversation().finally(function () {
        refreshConversations(true);
        startPolling();
    });
})();







