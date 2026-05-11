document.addEventListener("DOMContentLoaded", () => {
    const page = document.querySelector("[data-help-view='assistant']");
    if (!page) {
        return;
    }

    const config = {
        botGreeting: "Hi. Select a topic above or ask a question.",
        botNoMatch: "No FAQ matches found. Choose a category, quick action, or refine your question.",
        agentGreeting: "Your Live Agent conversation is active.",
        agentSelectCategoryMessage: "Choose a help category first to start a Live Agent conversation.",
        agentWaitingStatus: "Waiting for an available agent",
        agentAssignedStatus: "Agent assigned",
        agentWaitingReply: "An agent will assist you shortly.",
        agentQueuedHint: "An agent will assist you shortly. Your messages will stay in this queue.",
        agentAssignedHint: "An agent has already been assigned to this conversation.",
        agentCategoryLockedHint: "Your Live Agent conversation is locked to the selected category until it is resolved or cleared.",
        quickActionsError: "Quick actions are unavailable right now.",
        sessionCreateError: "Unable to start the Live Agent conversation right now.",
        sessionQuestionError: "Unable to save your Live Agent question right now.",
        sessionResolveError: "Unable to resolve the current Live Agent conversation right now.",
        botTypingDelay: 500,
        agentTypingDelay: 1000,
        liveAgentPollIntervalMs: 3000,
        maxSearchSuggestions: 3,
        maxCategorySuggestions: 5,
        quickActionLimit: 6,
        chatScrollBottomThreshold: 48,
        defaultInputPlaceholder: "Type your message here...",
        categoryRequiredPlaceholder: "Choose a category first...",
        agentInputPlaceholder: "Describe your concern for the selected category...",
    };
    const parseServerDate = (value) => {
        if (!value) {
            return null;
        }

        if (value instanceof Date) {
            return Number.isNaN(value.getTime()) ? null : value;
        }

        if (typeof value === "string") {
            const trimmed = value.trim();
            if (!trimmed) {
                return null;
            }

            const serverLocalMatch = trimmed.match(
                /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?$/
            );

            if (serverLocalMatch) {
                const [, year, month, day, hour, minute, second] = serverLocalMatch;
                const parsed = new Date(
                    Number(year),
                    Number(month) - 1,
                    Number(day),
                    Number(hour),
                    Number(minute),
                    Number(second || "0")
                );

                return Number.isNaN(parsed.getTime()) ? null : parsed;
            }

            const parsed = new Date(trimmed);
            return Number.isNaN(parsed.getTime()) ? null : parsed;
        }

        const parsed = new Date(value);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    };

    const state = {
        currentMode: "bot",
        conversations: {
            bot: [],
            agent: [],
        },
        quickActionCategories: [],
        agentAvailability: "queued",
        agentEndedNotice: null,
        agentTransaction: {
            selectedCategorySlug: "",
            selectedCategoryTitle: "",
            sessionId: null,
            createdAt: null,
            supportFaqId: null,
            firstQuestionCaptured: false,
            hasAssignedAgent: false,
            assignedAgentName: "",
            isOfflineSession: false,
            isStartingSession: false,
            isSendingMessage: false,
        },
        csrfTokenPromise: null,
        isTyping: false,
        liveAgentPollHandle: null,
        isPollingLiveAgentSession: false,
        liveAgentSessionSignature: "",
    };

    const elements = {
        chatBox: page.querySelector("#helpChatBox"),
        chatInput: page.querySelector("#chatInput"),
        sendBtn: page.querySelector("#sendBtn"),
        clearBtn: page.querySelector("#clearChatBtn"),
        resolveBtn: page.querySelector("#resolveChatBtn"),
        modeBtns: page.querySelectorAll(".mode-btn"),
        modeIcon: page.querySelector("#chatModeIcon"),
        modeLabel: page.querySelector("#chatModeLabel"),
        statusDot: page.querySelector(".status-dot"),
        statusText: page.querySelector(".status-text"),
        chatHint: page.querySelector("#chatHint"),
        quickSolutions: page.querySelector("#quickSolutions"),
        quickToggle: page.querySelector("#toggleQuickSolutions"),
        quickActions: page.querySelector("[data-help-quick-actions]"),
    };

    const escapeHtml = (value) => {
        if (typeof value !== "string") {
            return "";
        }

        return value
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    };

    const fetchJson = async (url, options) => {
        const response = await fetch(url, options);
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
                : typeof payload === "string" && payload.trim()
                    ? payload
                : response.status === 401
                    ? "You need to sign in before starting a Live Agent conversation."
                    : "Unable to load help content right now.";
            throw new Error(message);
        }

        return payload;
    };

    const ensureCsrfToken = async () => {
        if (!state.csrfTokenPromise) {
            state.csrfTokenPromise = fetchJson("/api/security/csrf-token").then((result) => result.token);
        }

        return state.csrfTokenPromise;
    };

    const scrollToBottom = () => {
        if (elements.chatBox) {
            elements.chatBox.scrollTop = elements.chatBox.scrollHeight;
        }
    };

    const isChatNearBottom = () => {
        if (!elements.chatBox) {
            return true;
        }

        const remainingScroll = elements.chatBox.scrollHeight - elements.chatBox.scrollTop - elements.chatBox.clientHeight;
        return remainingScroll <= config.chatScrollBottomThreshold;
    };

    const hasSelectedAgentCategory = () => !!state.agentTransaction.selectedCategorySlug;
    const hasAssignedAgent = () => !!state.agentTransaction.hasAssignedAgent;

    const syncQuickActions = () => {
        elements.quickActions?.querySelectorAll(".quick-solution-btn").forEach((button) => {
            const slug = button.dataset.categorySlug || "";
            const isSelected = state.currentMode === "agent"
                && hasSelectedAgentCategory()
                && slug === state.agentTransaction.selectedCategorySlug;
            const isDisabled = state.currentMode === "agent"
                && hasSelectedAgentCategory()
                && slug !== state.agentTransaction.selectedCategorySlug;

            button.classList.toggle("is-selected", isSelected);
            button.disabled = isDisabled;
            button.setAttribute("aria-disabled", isDisabled.toString());
        });
    };

    const syncAgentComposerState = () => {
        if (!elements.chatInput || !elements.sendBtn || !elements.resolveBtn) {
            return;
        }

        const isAgent = state.currentMode === "agent";
        const hasCategory = hasSelectedAgentCategory();

        if (!isAgent) {
            elements.chatInput.disabled = false;
            elements.chatInput.placeholder = config.defaultInputPlaceholder;
            elements.sendBtn.disabled = false;
            elements.resolveBtn.hidden = true;
            elements.resolveBtn.disabled = true;
            return;
        }

        elements.resolveBtn.hidden = false;
        elements.resolveBtn.disabled = !state.agentTransaction.sessionId;
        elements.chatInput.disabled = !hasCategory;
        elements.chatInput.placeholder = hasCategory
            ? config.agentInputPlaceholder
            : config.categoryRequiredPlaceholder;
        elements.sendBtn.disabled = !hasCategory;
    };

    const stopLiveAgentPolling = () => {
        if (state.liveAgentPollHandle) {
            window.clearInterval(state.liveAgentPollHandle);
            state.liveAgentPollHandle = null;
        }

        state.isPollingLiveAgentSession = false;
    };

    const resetAgentTransaction = () => {
        stopLiveAgentPolling();
        state.liveAgentSessionSignature = "";
        state.agentTransaction = {
            selectedCategorySlug: "",
            selectedCategoryTitle: "",
            sessionId: null,
            createdAt: null,
            supportFaqId: null,
            firstQuestionCaptured: false,
            hasAssignedAgent: false,
            assignedAgentName: "",
            isOfflineSession: false,
            isStartingSession: false,
            isSendingMessage: false,
        };
        syncQuickActions();
        syncAgentComposerState();
    };

    const createSuggestionMarkup = (label, items, emptyMessage) => {
        if (!Array.isArray(items) || !items.length) {
            return `<div class="chat-no-match">${escapeHtml(emptyMessage)}</div>`;
        }

        const buttons = items
            .map((item) => {
                const question = item.question || "";
                const answer = item.answer || "";
                const categoryTitle = item.categoryTitle || "";
                return `
                    <button
                        type="button"
                        class="chat-result-btn"
                        data-chat-question="${escapeHtml(question)}"
                        data-chat-answer="${escapeHtml(answer)}">
                        <span class="chat-result-question">${escapeHtml(question)}</span>
                        ${categoryTitle ? `<span class="chat-result-meta">${escapeHtml(categoryTitle)}</span>` : ""}
                    </button>`;
            })
            .join("");

        return `
            <div class="chat-results">
                <div class="chat-results-label">${escapeHtml(label)}</div>
                <div class="chat-results-actions">${buttons}</div>
            </div>`;
    };

    const getGreeting = (mode) => {
        if (mode === "agent") {
            if (!hasSelectedAgentCategory()) {
                return config.agentSelectCategoryMessage;
            }

            return hasAssignedAgent()
                ? config.agentGreeting
                : config.agentWaitingReply;
        }

        return config.botGreeting;
    };

    const formatChatTimestamp = (timestamp, actionLabel) => {
        if (!timestamp) {
            return actionLabel;
        }

        const date = parseServerDate(timestamp);
        if (!date) {
            return actionLabel;
        }

        const now = new Date();
        const timeLabel = new Intl.DateTimeFormat(undefined, {
            hour: "numeric",
            minute: "2-digit",
        }).format(date);
        const currentDayKey = new Intl.DateTimeFormat("en-CA", {
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
        }).format(now);
        const messageDayKey = new Intl.DateTimeFormat("en-CA", {
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
        }).format(date);
        const isSameDay = messageDayKey === currentDayKey;

        if (isSameDay) {
            return `${actionLabel} at ${timeLabel}`;
        }

        const dateLabel = new Intl.DateTimeFormat(undefined, {
            month: "short",
            day: "numeric",
        }).format(date);

        return `${actionLabel} on ${dateLabel} at ${timeLabel}`;
    };

    const formatConversationStartedLabel = (timestamp) =>
        formatChatTimestamp(timestamp, "Conversation started");

    const createEndedConversationNoticeMarkup = (mode) => {
        if (mode !== "agent" || state.agentTransaction.sessionId || !state.agentEndedNotice) {
            return "";
        }

        const notice = state.agentEndedNotice;
        const noticeTitle = notice.endedReason === "Inactive"
            ? "Your previous Live Agent conversation ended after 5 minutes of inactivity."
            : "Your previous Live Agent conversation was resolved.";
        const noticeMeta = `${notice.categoryTitle || "Live Agent"} - ${formatChatTimestamp(notice.endedAt, "Ended")}`;

        return `
            <div class="chat-session-notice">
                <div class="chat-session-notice-title">${escapeHtml(noticeTitle)}</div>
                <div class="chat-session-notice-meta">${escapeHtml(noticeMeta)}</div>
            </div>`;
    };

    const createConversationStartedMarkup = (mode) => {
        if (mode !== "agent" || !state.agentTransaction.sessionId || !state.agentTransaction.createdAt) {
            return "";
        }

        const label = formatConversationStartedLabel(state.agentTransaction.createdAt);
        if (!label) {
            return "";
        }

        return `<div class="chat-conversation-start"><span>${escapeHtml(label)}</span></div>`;
    };

    const createAssignedAgentSpielMarkup = (mode, messages) => {
        if (mode !== "agent" || !state.agentTransaction.sessionId || !hasAssignedAgent()) {
            return "";
        }

        const assignedAgentName = (state.agentTransaction.assignedAgentName || "").trim();
        const spielText = assignedAgentName
            ? `Hi! I'm ${assignedAgentName}, and I'll be assisting you today. I'm here to help.`
            : "Hi! I'm one of the support agents, and I'll be assisting you today. I'm here to help.";
        const hasTranscriptDuplicate = Array.isArray(messages)
            && messages.some((message) => !message.html
                && message.type === "agent"
                && (message.text || "").trim() === spielText);

        if (hasTranscriptDuplicate) {
            return "";
        }

        return `<div class="chat-message agent">${escapeHtml(spielText)}</div>`;
    };

    const renderConversation = (mode, options = {}) => {
        if (!elements.chatBox) {
            return;
        }

        const messages = state.conversations[mode];
        const shouldStickToBottom = options.forceScroll ?? isChatNearBottom();
        const previousScrollTop = elements.chatBox.scrollTop;
        const endedNoticeMarkup = createEndedConversationNoticeMarkup(mode);
        const conversationStartedMarkup = createConversationStartedMarkup(mode);
        const assignedAgentSpielMarkup = createAssignedAgentSpielMarkup(mode, messages);
        if (!messages.length) {
            const greeting = getGreeting(mode);
            elements.chatBox.innerHTML = `${endedNoticeMarkup}${conversationStartedMarkup}${assignedAgentSpielMarkup}<div class="chat-message ${mode}">${escapeHtml(greeting)}</div>`;
            if (shouldStickToBottom) {
                scrollToBottom();
            } else {
                elements.chatBox.scrollTop = previousScrollTop;
            }
            return;
        }

        elements.chatBox.innerHTML = endedNoticeMarkup + conversationStartedMarkup + assignedAgentSpielMarkup + messages
            .map((message) => {
                if (message.html) {
                    return `<div class="chat-message ${message.type}">${message.html}</div>`;
                }

                return `<div class="chat-message ${message.type}">${escapeHtml(message.text || "")}</div>`;
            })
            .join("");
        if (shouldStickToBottom) {
            scrollToBottom();
        } else {
            elements.chatBox.scrollTop = previousScrollTop;
        }
    };

    const addTextMessage = (mode, type, text) => {
        state.conversations[mode].push({ type, text });
        if (mode !== state.currentMode || !elements.chatBox) {
            return;
        }

        const node = document.createElement("div");
        node.className = `chat-message ${type}`;
        node.textContent = text;
        elements.chatBox.appendChild(node);
        scrollToBottom();
    };

    const addHtmlMessage = (mode, type, html) => {
        state.conversations[mode].push({ type, html });
        if (mode !== state.currentMode || !elements.chatBox) {
            return;
        }

        const node = document.createElement("div");
        node.className = `chat-message ${type}`;
        node.innerHTML = html;
        elements.chatBox.appendChild(node);
        scrollToBottom();
    };

    const setEndedLiveAgentNotice = (notice) => {
        state.agentEndedNotice = notice || null;
    };

    const removeLastTextMessage = (mode, type, text) => {
        const messages = state.conversations[mode];
        for (let index = messages.length - 1; index >= 0; index -= 1) {
            const message = messages[index];
            if (message.type === type && message.text === text) {
                messages.splice(index, 1);
                break;
            }
        }

        if (mode === state.currentMode) {
            renderConversation(mode);
        }
    };

    const mapLiveAgentMessageType = (senderRole) => (
        (senderRole || "").toLowerCase() === "consumer" ? "user" : "agent"
    );

    const buildLiveAgentSessionSignature = (session) => {
        if (!session || !session.sessionId) {
            return "";
        }

        const messages = Array.isArray(session.messages) ? session.messages : [];
        const lastMessage = messages.length ? messages[messages.length - 1] : null;

        return JSON.stringify({
            sessionId: session.sessionId,
            updatedAt: session.updatedAt || "",
            hasAssignedAgent: !!session.hasAssignedAgent,
            assignedAgentName: session.assignedAgentName || "",
            lastMessageId: lastMessage?.messageId || 0,
            lastMessageCreatedAt: lastMessage?.createdAt || "",
            lastMessageText: lastMessage?.messageText || "",
            messageCount: messages.length,
        });
    };

    const setAgentConversationFromTranscript = (messages) => {
        state.conversations.agent = Array.isArray(messages)
            ? messages.map((message) => ({
                type: mapLiveAgentMessageType(message.senderRole),
                text: message.messageText || "",
            }))
            : [];
    };

    const appendAgentTranscriptMessages = (messages, echoedUserText) => {
        let skippedEcho = false;

        (Array.isArray(messages) ? messages : []).forEach((message) => {
            const type = mapLiveAgentMessageType(message.senderRole);
            const text = message.messageText || "";

            if (!skippedEcho && type === "user" && text === echoedUserText) {
                skippedEcho = true;
                return;
            }

            addTextMessage("agent", type, text);
        });
    };

    const applyLiveAgentSession = (session) => {
        setEndedLiveAgentNotice(null);
        state.agentTransaction.selectedCategorySlug = session.categorySlug || "";
        state.agentTransaction.selectedCategoryTitle = session.categoryTitle || "";
        state.agentTransaction.sessionId = session.sessionId || null;
        state.agentTransaction.createdAt = session.createdAt || null;
        state.agentTransaction.supportFaqId = session.supportFaqId || null;
        state.agentTransaction.firstQuestionCaptured = !!session.firstQuestionCaptured;
        state.agentTransaction.hasAssignedAgent = !!session.hasAssignedAgent;
        state.agentTransaction.assignedAgentName = session.assignedAgentName || "";
        state.agentTransaction.isOfflineSession = !!session.isOfflineSession;
        state.agentTransaction.isStartingSession = false;
        state.agentTransaction.isSendingMessage = false;
        state.liveAgentSessionSignature = buildLiveAgentSessionSignature(session);
        setAgentConversationFromTranscript(session.messages);
        syncQuickActions();
        syncAgentComposerState();
    };

    const showTypingIndicator = (mode = state.currentMode) => {
        if (state.isTyping || !elements.chatBox) {
            return null;
        }

        state.isTyping = true;
        const indicator = document.createElement("div");
        indicator.className = `chat-message ${mode === "agent" ? "agent" : "bot"} loading`;
        indicator.innerHTML = "<div class=\"typing\"><span></span><span></span><span></span></div>";
        elements.chatBox.appendChild(indicator);
        scrollToBottom();
        return indicator;
    };

    const hideTypingIndicator = (indicator) => {
        if (indicator && indicator.parentNode) {
            indicator.remove();
        }

        state.isTyping = false;
    };

    const updateModeUi = (mode) => {
        elements.modeBtns.forEach((button) => {
            button.classList.toggle("active", button.dataset.mode === mode);
        });

        if (mode === "agent") {
            const assignedAgent = hasAssignedAgent();

            elements.modeIcon.className = "fas fa-headset";
            elements.modeLabel.textContent = "Live Agent";
            elements.statusDot.classList.remove("online");
            elements.statusDot.classList.add("waiting");
            elements.statusText.textContent = assignedAgent
                ? config.agentAssignedStatus
                : config.agentWaitingStatus;
        elements.statusText.classList.add("agent-waiting");
        elements.chatHint.textContent = !hasSelectedAgentCategory()
            ? config.agentSelectCategoryMessage
                : assignedAgent
                    ? config.agentAssignedHint
                    : config.agentCategoryLockedHint;
        syncAgentComposerState();
        syncQuickActions();
        return;
        }

        elements.modeIcon.className = "fas fa-robot";
        elements.modeLabel.textContent = "Quick Help Chat";
        elements.statusDot.classList.add("online");
        elements.statusDot.classList.remove("waiting");
        elements.statusText.textContent = "Bot ready to help";
        elements.statusText.classList.remove("agent-waiting");
        elements.chatHint.textContent = "Tip: Click a category or question for instant help.";
        syncAgentComposerState();
        syncQuickActions();
    };

    const switchMode = (mode) => {
        if (state.currentMode === mode) {
            return;
        }

        state.currentMode = mode;
        updateModeUi(mode);
        renderConversation(mode);
        elements.chatInput?.focus();
    };

    const renderQuickActions = (categories) => {
        if (!elements.quickActions) {
            return;
        }

        const items = Array.isArray(categories) ? categories.slice(0, config.quickActionLimit) : [];
        state.quickActionCategories = items;
        if (!items.length) {
            elements.quickActions.innerHTML = `<div class="help-empty-state">${escapeHtml(config.quickActionsError)}</div>`;
            return;
        }

        elements.quickActions.innerHTML = "";
        items.forEach((category, index) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = `quick-solution-btn${index === 0 ? " primary" : ""}`;
            button.dataset.categorySlug = category.slug || "";
            button.innerHTML = `<span>${escapeHtml(category.title || "")}</span>`;
            elements.quickActions.appendChild(button);
        });

        syncQuickActions();
    };

    const loadQuickActions = async () => {
        if (!elements.quickActions) {
            return;
        }

        try {
            const categories = await fetchJson("/api/help/categories");
            renderQuickActions(categories);
        } catch (error) {
            elements.quickActions.innerHTML = `<div class="help-error-state">${escapeHtml(error.message)}</div>`;
        }
    };

    const getCategoryDetail = async (slug) => {
        if (!slug) {
            return null;
        }

        return fetchJson(`/api/help/categories/${encodeURIComponent(slug)}`);
    };

    const createOfflineLiveAgentSession = ({ slug, title }) => {
        const timestamp = new Date().toISOString();

        return {
            sessionId: `offline-${Date.now()}`,
            supportFaqId: 0,
            categorySlug: slug,
            categoryTitle: title,
            createdAt: timestamp,
            updatedAt: timestamp,
            firstQuestionCaptured: false,
            hasAssignedAgent: false,
            assignedAgentName: "",
            isOfflineSession: true,
            messages: [
                {
                    messageId: 0,
                    conversationId: 0,
                    senderId: 0,
                    senderRole: "Agent",
                    messageText: config.agentQueuedHint,
                    createdAt: timestamp,
                },
            ],
        };
    };

    const selectAssistantCategory = async (categorySlug) => {
        if (!categorySlug) {
            return null;
        }

        const token = await ensureCsrfToken();
        return fetchJson("/api/help/assistant/category", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": token,
            },
            body: JSON.stringify({ categorySlug }),
        });
    };

    const captureAssistantQuestion = async (message) => {
        if (!message) {
            return null;
        }

        const token = await ensureCsrfToken();
        return fetchJson("/api/help/assistant/question", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": token,
            },
            body: JSON.stringify({ message }),
        });
    };

    const resolveAssistantSupportFaq = async () => {
        const token = await ensureCsrfToken();
        return fetchJson("/api/help/assistant/resolve", {
            method: "POST",
            headers: {
                "X-CSRF-TOKEN": token,
            },
        });
    };

    const createLiveAgentSession = async (categorySlug) => {
        const token = await ensureCsrfToken();
        const payload = {
            categorySlug,
        };

        return fetchJson("/api/help/live-agent/sessions", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": token,
            },
            body: JSON.stringify(payload),
        });
    };

    const getCurrentLiveAgentSession = async () => fetchJson("/api/help/live-agent/sessions/current");
    const getLastEndedLiveAgentNotice = async () => fetchJson("/api/help/live-agent/sessions/last-ended");

    const appendLiveAgentMessage = async (sessionId, message) => {
        const token = await ensureCsrfToken();
        return fetchJson(`/api/help/live-agent/sessions/${encodeURIComponent(sessionId)}/messages`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": token,
            },
            body: JSON.stringify({ message }),
        });
    };

    const restoreLastEndedLiveAgentNotice = async () => {
        try {
            const notice = await getLastEndedLiveAgentNotice();
            setEndedLiveAgentNotice(notice);
            return !!notice;
        } catch (error) {
            setEndedLiveAgentNotice(null);
            return false;
        }
    };

    const resolveLiveAgentSession = async (sessionId) => {
        const token = await ensureCsrfToken();
        return fetchJson(`/api/help/live-agent/sessions/${encodeURIComponent(sessionId)}/resolve`, {
            method: "POST",
            headers: {
                "X-CSRF-TOKEN": token,
            },
        });
    };

    const startLiveAgentPolling = () => {
        if (state.liveAgentPollHandle || !state.agentTransaction.sessionId) {
            return;
        }

        state.liveAgentPollHandle = window.setInterval(() => {
            void pollCurrentLiveAgentSession();
        }, config.liveAgentPollIntervalMs);
    };

    const syncAgentSessionEndedFromPolling = async () => {
        resetAgentTransaction();
        state.conversations.agent = [];
        await restoreLastEndedLiveAgentNotice();
        updateModeUi(state.currentMode);
        if (state.currentMode === "agent") {
            renderConversation("agent", { forceScroll: true });
        }
    };

    const pollCurrentLiveAgentSession = async () => {
        if (state.isPollingLiveAgentSession || !state.agentTransaction.sessionId || state.agentTransaction.isSendingMessage) {
            return;
        }

        const requestedSessionId = state.agentTransaction.sessionId;
        state.isPollingLiveAgentSession = true;

        try {
            const session = await getCurrentLiveAgentSession();
            if (state.agentTransaction.sessionId !== requestedSessionId) {
                return;
            }

            if (!session || !session.sessionId) {
                await syncAgentSessionEndedFromPolling();
                return;
            }

            const nextSignature = buildLiveAgentSessionSignature(session);
            if (!nextSignature || nextSignature === state.liveAgentSessionSignature) {
                return;
            }

            const shouldStickToBottom = state.currentMode === "agent" ? isChatNearBottom() : false;
            applyLiveAgentSession(session);
            updateModeUi(state.currentMode);
            if (state.currentMode === "agent") {
                renderConversation("agent", { forceScroll: shouldStickToBottom });
            }
        } catch (error) {
            if (window.console && typeof window.console.warn === "function") {
                window.console.warn("Unable to refresh the current Live Agent session.", error);
            }
        } finally {
            state.isPollingLiveAgentSession = false;
        }
    };

    const selectFaq = (question, answer, mode = state.currentMode) => {
        if (!question || !answer) {
            return;
        }

        if (mode !== state.currentMode) {
            switchMode(mode);
        }

        addTextMessage(mode, "user", question);
        addTextMessage(mode, mode === "agent" ? "agent" : "bot", answer);
    };

    const handleBotSearch = async (text) => {
        const indicator = showTypingIndicator("bot");

        try {
            const results = await fetchJson(`/api/help/search?query=${encodeURIComponent(text)}`);
            window.setTimeout(() => {
                hideTypingIndicator(indicator);

                const matches = Array.isArray(results)
                    ? results
                        .slice(0, config.maxSearchSuggestions)
                        .map((result) => ({
                            question: result.question,
                            answer: result.answer,
                            categoryTitle: result.categoryTitle,
                        }))
                    : [];

                const html = createSuggestionMarkup(
                    "Select the FAQ that matches your question.",
                    matches,
                    config.botNoMatch);

                addHtmlMessage("bot", "bot", html);
            }, config.botTypingDelay);
        } catch (error) {
            window.setTimeout(() => {
                hideTypingIndicator(indicator);
                addTextMessage("bot", "bot", error.message);
            }, config.botTypingDelay);
        }
    };

    const setSelectedAgentCategory = async (detail) => {
        const slug = detail?.slug || "";
        const title = detail?.title || "";
        if (!slug || !title) {
            return false;
        }

        if (hasSelectedAgentCategory()) {
            return state.agentTransaction.selectedCategorySlug === slug;
        }

        if (state.agentTransaction.isStartingSession) {
            return false;
        }

        state.agentTransaction.isStartingSession = true;

        try {
            const session = await createLiveAgentSession(slug);
            applyLiveAgentSession(session);
            if (!session.isOfflineSession) {
                startLiveAgentPolling();
            }
            return (session.categorySlug || slug) === slug;
        } catch (error) {
            const fallbackSession = createOfflineLiveAgentSession({ slug, title });
            applyLiveAgentSession(fallbackSession);
            return true;
        } finally {
            state.agentTransaction.isStartingSession = false;
            syncQuickActions();
            syncAgentComposerState();
        }
    };

    const handleCategorySelection = async (detail, mode = state.currentMode) => {
        const title = detail?.title || "";
        const slug = detail?.slug || "";
        const faqs = Array.isArray(detail?.faqs)
            ? detail.faqs
                .slice(0, config.maxCategorySuggestions)
                .map((faq) => ({
                    question: faq.question,
                    answer: faq.answer,
                    categoryTitle: title,
                }))
            : [];

        if (!title) {
            return;
        }

        if (mode !== state.currentMode) {
            switchMode(mode);
        }

        try {
            await selectAssistantCategory(slug);
        } catch (error) {
            addTextMessage(mode, mode === "agent" ? "agent" : "bot", error.message || config.quickActionsError);
            return;
        }

        if (mode === "agent") {
            const accepted = await setSelectedAgentCategory({ slug, title });
            if (!accepted) {
                return;
            }
        }

        addTextMessage(mode, "user", title);
        addHtmlMessage(
            mode,
            mode === "agent" ? "agent" : "bot",
            createSuggestionMarkup(
                `Select a ${title} FAQ.`,
                faqs,
                config.botNoMatch)
        );
    };

    const sendMessage = async () => {
        const text = elements.chatInput?.value.trim();
        if (!text) {
            return;
        }

        const mode = state.currentMode;
        if (mode === "agent" && !hasSelectedAgentCategory()) {
            addTextMessage("agent", "agent", config.agentSelectCategoryMessage);
            syncAgentComposerState();
            return;
        }

        if (mode === "agent" && state.agentTransaction.isSendingMessage) {
            return;
        }

        addTextMessage(mode, "user", text);
        elements.chatInput.value = "";

        try {
            await captureAssistantQuestion(text);
        } catch {
        }

        if (mode === "agent") {
            if (!state.agentTransaction.sessionId) {
                removeLastTextMessage("agent", "user", text);
                addTextMessage("agent", "agent", config.sessionCreateError);
                return;
            }

            if (state.agentTransaction.isOfflineSession) {
                state.agentTransaction.firstQuestionCaptured = true;
                addTextMessage("agent", "agent", config.agentWaitingReply);
                updateModeUi("agent");
                syncAgentComposerState();
                return;
            }

            state.agentTransaction.isSendingMessage = true;
            const indicator = showTypingIndicator("agent");

            try {
                const response = await appendLiveAgentMessage(state.agentTransaction.sessionId, text);

                state.agentTransaction.firstQuestionCaptured = !!response.firstQuestionCaptured;
                state.agentTransaction.hasAssignedAgent = !!response.hasAssignedAgent;
                state.agentTransaction.assignedAgentName = response.assignedAgentName || "";
                state.agentTransaction.supportFaqId = response.supportFaqId || state.agentTransaction.supportFaqId;
                state.liveAgentSessionSignature = buildLiveAgentSessionSignature({
                    sessionId: response.sessionId || state.agentTransaction.sessionId,
                    updatedAt: response.updatedAt,
                    hasAssignedAgent: response.hasAssignedAgent,
                    assignedAgentName: response.assignedAgentName,
                    messages: response.messages,
                });
                appendAgentTranscriptMessages(response.messages, text);
                updateModeUi("agent");
                syncAgentComposerState();
            } catch (error) {
                const restoredEndedNotice = await restoreLastEndedLiveAgentNotice();
                if (restoredEndedNotice) {
                    resetAgentTransaction();
                    state.conversations.agent = [];
                    updateModeUi("agent");
                    renderConversation("agent");
                    return;
                }

                removeLastTextMessage("agent", "user", text);
                addTextMessage("agent", "agent", error.message || config.sessionQuestionError);
            } finally {
                hideTypingIndicator(indicator);
                state.agentTransaction.isSendingMessage = false;
            }

            return;
        }

        handleBotSearch(text);
    };

    const clearConversation = async () => {
        if (state.currentMode === "agent" && state.agentTransaction.sessionId) {
            try {
                await resolveLiveAgentSession(state.agentTransaction.sessionId);
            } catch (error) {
                addTextMessage("agent", "agent", error.message || config.sessionResolveError);
                return;
            }

            resetAgentTransaction();
            await restoreLastEndedLiveAgentNotice();
        }

        if (state.currentMode === "agent" && !state.agentTransaction.sessionId) {
            resetAgentTransaction();
        }

        state.conversations[state.currentMode] = [];
        updateModeUi(state.currentMode);
        renderConversation(state.currentMode);
    };

    const resolveConversation = async () => {
        if (!state.agentTransaction.sessionId) {
            return;
        }

        try {
            await resolveAssistantSupportFaq();
        } catch (error) {
            addTextMessage("agent", "agent", error.message || config.sessionResolveError);
            return;
        }

        if (state.agentTransaction.isOfflineSession) {
            resetAgentTransaction();
            state.conversations.agent = [];
            updateModeUi("agent");
            renderConversation("agent");
            return;
        }

        try {
            await resolveLiveAgentSession(state.agentTransaction.sessionId);
        } catch (error) {
            addTextMessage("agent", "agent", error.message || config.sessionResolveError);
            return;
        }

        resetAgentTransaction();
        state.conversations.agent = [];
        await restoreLastEndedLiveAgentNotice();
        updateModeUi("agent");
        renderConversation("agent");
    };

    const restoreActiveLiveAgentSession = async () => {
        try {
            const session = await getCurrentLiveAgentSession();
            if (!session) {
                return false;
            }

            applyLiveAgentSession(session);
            startLiveAgentPolling();
            state.currentMode = "agent";
            updateModeUi("agent");
            renderConversation("agent");
            return true;
        } catch (error) {
            if (window.console && typeof window.console.warn === "function") {
                window.console.warn("Unable to restore the current Live Agent session.", error);
            }

            return false;
        }
    };

    const ensureAgentCategorySelection = async () => {
        if (state.currentMode !== "agent" || hasSelectedAgentCategory() || state.agentTransaction.isStartingSession) {
            return;
        }

        const firstCategory = Array.isArray(state.quickActionCategories) && state.quickActionCategories.length
            ? state.quickActionCategories[0]
            : null;
        if (!firstCategory?.slug) {
            return;
        }

        try {
            const detail = await getCategoryDetail(firstCategory.slug);
            await handleCategorySelection(detail || firstCategory, "agent");
        } catch {
        }
    };

    const bindEvents = () => {
        elements.modeBtns.forEach((button) => {
            button.addEventListener("click", () => {
                const mode = button.dataset.mode || "bot";
                switchMode(mode);
                if (mode === "agent") {
                    void ensureAgentCategorySelection();
                }
            });
        });

        elements.clearBtn?.addEventListener("click", async () => {
            await clearConversation();
        });

        elements.resolveBtn?.addEventListener("click", async () => {
            await resolveConversation();
        });

        elements.sendBtn?.addEventListener("click", async () => {
            await sendMessage();
        });

        elements.chatInput?.addEventListener("keydown", async (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                await sendMessage();
            }
        });

        elements.chatInput?.addEventListener("input", () => {
            syncAgentComposerState();
        });

        elements.quickActions?.addEventListener("click", (event) => {
            const button = event.target.closest(".quick-solution-btn");
            if (!button || button.disabled) {
                return;
            }

            const slug = button.dataset.categorySlug || "";
            if (!slug) {
                return;
            }

            void (async () => {
                try {
                    const detail = await getCategoryDetail(slug);
                    await handleCategorySelection(detail || {}, state.currentMode);
                } catch (error) {
                    addTextMessage(state.currentMode, state.currentMode === "agent" ? "agent" : "bot", error.message || config.quickActionsError);
                }
            })();
        });

        elements.quickToggle?.addEventListener("click", () => {
            if (!elements.quickSolutions) {
                return;
            }

            elements.quickSolutions.classList.toggle("collapsed");
            const collapsed = elements.quickSolutions.classList.contains("collapsed");
            elements.quickToggle.innerHTML = collapsed
                ? "<span>Show</span><i class=\"fas fa-chevron-down\"></i>"
                : "<span>Hide</span><i class=\"fas fa-chevron-up\"></i>";
        });

        elements.chatBox?.addEventListener("click", (event) => {
            const button = event.target.closest(".chat-result-btn");
            if (!button) {
                return;
            }

            selectFaq(button.dataset.chatQuestion || "", button.dataset.chatAnswer || "", state.currentMode);
        });

        window.addEventListener("beforeunload", stopLiveAgentPolling);
    };

    const initialize = async () => {
        bindEvents();
        updateModeUi("bot");
        renderConversation("bot");
        await loadQuickActions();

        const hasActiveAgentSession = await restoreActiveLiveAgentSession();
        if (!hasActiveAgentSession) {
            await restoreLastEndedLiveAgentNotice();
        }

    };

    initialize();
});
