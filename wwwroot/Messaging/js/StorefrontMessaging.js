(function () {
  const DEFAULT_ERROR = 'Could not load messages right now.';
  const AUTH_ERROR = 'Messaging is not available for this session.';
  const NOT_FOUND_ERROR = 'Conversation is not available right now.';
  const IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp'];
  const VIDEO_EXTENSIONS = ['.mp4', '.webm', '.mov'];
  const REQUEST_TIMEOUT = 15000; // 15 seconds timeout
  let csrfTokenPromise = null;

  function withTimeout(promise, timeoutMs) {
    let timeoutId;
    const timeoutPromise = new Promise(function (_, reject) {
      timeoutId = setTimeout(function () {
        reject(new Error('Request timed out. Please check your internet connection.'));
      }, timeoutMs);
    });
    return Promise.race([promise, timeoutPromise]).finally(function () {
      clearTimeout(timeoutId);
    });
  }

  function byId(id) { return id ? document.getElementById(id) : null; }

  function getFileExtension(value) {
    if (typeof value !== 'string' || !value.trim()) return '';
    const sanitized = value.split('#')[0].split('?')[0].trim().toLowerCase();
    const dotIndex = sanitized.lastIndexOf('.');
    return dotIndex >= 0 ? sanitized.slice(dotIndex) : '';
  }

  function getAttachmentKind(value) {
    const extension = getFileExtension(value);
    if (IMAGE_EXTENSIONS.indexOf(extension) >= 0) return 'image';
    if (VIDEO_EXTENSIONS.indexOf(extension) >= 0) return 'video';
    return '';
  }

  function parseServerLocalDate(value) {
    if (!value) return null;
    if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;
    if (typeof value !== 'string') {
      const parsed = new Date(value);
      return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    const trimmed = value.trim();
    if (!trimmed) return null;

    const match = trimmed.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?(Z)?$/);
    if (match) {
      const [, year, month, day, hour, minute, second] = match;
      const parsed = new Date(
        Number(year),
        Number(month) - 1,
        Number(day),
        Number(hour),
        Number(minute),
        Number(second || '0')
      );
      return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    const parsed = new Date(trimmed);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  function formatMessageTime(value) {
    const date = parseServerLocalDate(value);
    if (!date || Number.isNaN(date.getTime())) return '';
    return date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  }

  function parseJson(response) {
    return response.text().then(function (text) {
      if (!text) return null;
      try {
        return JSON.parse(text);
      } catch (_) {
        return text;
      }
    }).catch(function () { return null; });
  }

  function toast(message, type) {
    if (window.toast && typeof window.toast[type || 'info'] === 'function') {
      window.toast[type || 'info'](message);
      return;
    }
    console.warn(message);
  }

  function friendlyError(status, payload) {
    if (status === 401 || status === 403) return AUTH_ERROR;
    if (status === 404) return NOT_FOUND_ERROR;
    if (typeof payload === 'string' && payload.trim()) return payload;
    if (typeof payload?.title === 'string' && payload.title.trim()) return payload.title;
    if (typeof payload?.message === 'string' && payload.message.trim()) return payload.message;
    return DEFAULT_ERROR;
  }

  function normalizeMessages(payload) {
    if (!Array.isArray(payload)) return [];
    return payload
      .map(function (item) {
        return {
          senderUserId: String(item.senderUserId || item.SenderUserId || ''),
          body: typeof (item.body ?? item.Body) === 'string' ? (item.body ?? item.Body) : '',
          sentAt: String(item.sentAt || item.SentAt || ''),
          isDeleted: Boolean(item.isDeleted ?? item.IsDeleted),
          attachmentUrl: typeof (item.attachmentUrl ?? item.AttachmentUrl) === 'string'
            ? (item.attachmentUrl ?? item.AttachmentUrl)
            : '',
        };
      })
      .filter(function (item) { return item.senderUserId; })
      .sort(function (a, b) {
        const left = parseServerLocalDate(a.sentAt);
        const right = parseServerLocalDate(b.sentAt);
        return (left ? left.getTime() : 0) - (right ? right.getTime() : 0);
      });
  }

  async function getCsrfToken() {
    if (!csrfTokenPromise) {
      csrfTokenPromise = fetch('/api/security/csrf-token', {
        method: 'GET',
        credentials: 'same-origin',
      })
        .then(async function (response) {
          if (!response.ok) throw new Error('Unable to initialize secure messaging.');
          const payload = await response.json();
          return typeof payload?.token === 'string' ? payload.token : '';
        })
        .catch(function (error) {
          csrfTokenPromise = null;
          throw error;
        });
    }

    return csrfTokenPromise;
  }

  async function request(url, options) {
    const fetchPromise = fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
    const response = await withTimeout(fetchPromise, REQUEST_TIMEOUT);
    const payload = await parseJson(response);
    if (!response.ok) {
      const error = new Error(friendlyError(response.status, payload));
      error.status = response.status;
      error.payload = payload;
      throw error;
    }
    return payload;
  }

  async function requestWithCsrf(url, options) {
    const csrfToken = await getCsrfToken();
    const headers = Object.assign({}, options?.headers || {});
    if (csrfToken) headers['X-CSRF-TOKEN'] = csrfToken;
    return request(url, Object.assign({}, options || {}, { headers: headers }));
  }

  function buildAttachmentNode(url, kind, classNamePrefix) {
    if (kind === 'image') {
      const image = document.createElement('img');
      image.className = classNamePrefix + '-image';
      image.src = url;
      image.alt = 'Attachment preview';
      image.loading = 'lazy';
      return image;
    }

    if (kind === 'video') {
      const video = document.createElement('video');
      video.className = classNamePrefix + '-video';
      video.src = url;
      video.controls = true;
      video.preload = 'metadata';
      video.playsInline = true;
      return video;
    }

    return null;
  }

  function renderBubble(message, currentUserId) {
    const senderClass = message.senderUserId === currentUserId ? 'buyer' : 'seller';
    const senderLabel = senderClass === 'buyer' ? 'You' : 'Seller';
    const wrapper = document.createElement('div');
    wrapper.className = 'seller-chat-message ' + senderClass;

    const bubble = document.createElement('div');
    bubble.className = 'bubble';

    if (message.isDeleted) {
      bubble.textContent = '[message deleted]';
    } else if (message.body) {
      const body = document.createElement('div');
      body.className = 'bubble-body';
      body.textContent = message.body;
      bubble.appendChild(body);
    }

    if (!message.isDeleted && message.attachmentUrl) {
      const mediaKind = getAttachmentKind(message.attachmentUrl);
      const media = buildAttachmentNode(message.attachmentUrl, mediaKind, 'bubble-attachment');
      if (media) {
        if (mediaKind === 'image') {
          media.alt = 'Message attachment';
        }
        bubble.appendChild(media);
      }

      const link = document.createElement('a');
      link.className = 'bubble-attachment-link';
      link.href = message.attachmentUrl;
      link.target = '_blank';
      link.rel = 'noopener noreferrer';
      link.textContent = 'Open attachment';
      bubble.appendChild(link);
    }

    const meta = document.createElement('div');
    meta.className = 'bubble-meta';
    meta.textContent = senderLabel + ' at ' + formatMessageTime(message.sentAt);
    bubble.appendChild(meta);

    wrapper.appendChild(bubble);
    return wrapper;
  }

  function createStorefrontMessaging(config) {
    const state = {
      sellerUserId: null,
      sellerName: config.defaultSellerName || 'Seller',
      sellerAvatarUrl: config.defaultSellerAvatarUrl || '',
      currentUserId: null,
      conversationId: null,
      messages: [],
      faqs: [],
      headElement: null,
      isLoading: false,
      lastError: '',
      previewObjectUrl: '',
      activeFaqCategory: '',
      faqFabExpanded: false,
      faqPlacementReady: false,
    };

    function modal() { return byId(config.modalId); }
    function thread() { return byId(config.threadId); }
    function input() { return byId(config.inputId); }
    function sendButton() { return byId(config.sendButtonId); }
    function attachmentInput() { return byId(config.attachmentInputId); }
    function attachmentPreview() { return byId(config.attachmentPreviewId); }
    function attachmentStatus() { return byId(config.attachmentStatusId); }
    function clearAttachmentButton() { return byId(config.clearAttachmentButtonId); }
    function titleEl() { return byId(config.titleId); }
    function avatarEl() { return byId(config.avatarId); }
    function headContainer() { return byId(config.headContainerId); }
    function faqPanel() { return byId(config.faqPanelId); }
    function faqTitle() { return byId(config.faqTitleId); }
    function faqStatusNode() { return byId(config.faqStatusId); }
    function faqList() { return byId(config.faqListId); }
    function faqBack() { return byId(config.faqBackId); }
    function faqFab() { return byId(config.faqFabId); }

    function getAttachmentFile() {
      return attachmentInput()?.files?.[0] || null;
    }

    function revokePreviewUrl() {
      if (!state.previewObjectUrl) return;
      URL.revokeObjectURL(state.previewObjectUrl);
      state.previewObjectUrl = '';
    }

    function resizeComposer() {
      const inputNode = input();
      if (!inputNode || inputNode.tagName !== 'TEXTAREA') return;
      inputNode.style.height = 'auto';
      const maxHeight = Number.parseFloat(window.getComputedStyle(inputNode).maxHeight);
      if (Number.isFinite(maxHeight) && maxHeight > 0) {
        inputNode.style.height = Math.min(inputNode.scrollHeight, maxHeight) + 'px';
        inputNode.style.overflowY = inputNode.scrollHeight > maxHeight ? 'auto' : 'hidden';
        return;
      }

      inputNode.style.height = inputNode.scrollHeight + 'px';
      inputNode.style.overflowY = 'hidden';
    }

    function renderAttachmentPreview() {
      const preview = attachmentPreview();
      const file = getAttachmentFile();

      revokePreviewUrl();

      if (!preview) return;

      preview.innerHTML = '';
      preview.hidden = true;

      if (!file) return;

      const mediaKind = getAttachmentKind(file.name);
      if (!mediaKind) return;

      state.previewObjectUrl = URL.createObjectURL(file);
      const media = buildAttachmentNode(state.previewObjectUrl, mediaKind, 'chat-attachment-preview');
      if (!media) return;

      if (mediaKind === 'image') {
        media.alt = 'Selected attachment preview';
      } else if (mediaKind === 'video') {
        media.muted = true;
      }

      preview.appendChild(media);
      preview.hidden = false;
    }

    function updateAttachmentUi() {
      const file = getAttachmentFile();
      const status = attachmentStatus();
      const clearButton = clearAttachmentButton();
      const attachmentNode = attachmentInput();

      if (status) {
        if (file) {
          status.textContent = file.name;
          status.classList.add('is-active');
          status.hidden = false;
        } else {
          status.textContent = '';
          status.classList.remove('is-active');
          status.hidden = true;
        }
      }

      if (clearButton) {
        clearButton.hidden = !file;
        clearButton.disabled = !file || Boolean(attachmentNode?.disabled);
      }

      renderAttachmentPreview();
    }

    function clearAttachment() {
      const inputNode = attachmentInput();
      if (inputNode) inputNode.value = '';
      updateAttachmentUi();
    }

    function setComposer(enabled, placeholder) {
      const inputNode = input();
      const button = sendButton();
      const attachmentNode = attachmentInput();
      const clearButton = clearAttachmentButton();
      if (inputNode) {
        inputNode.disabled = !enabled;
        inputNode.placeholder = placeholder || inputNode.placeholder;
      }
      if (button) button.disabled = !enabled;
      if (attachmentNode) attachmentNode.disabled = !enabled;
      if (clearButton) clearButton.disabled = !enabled || !getAttachmentFile();
    }

    function updateHeader() {
      const title = titleEl();
      if (title) title.textContent = state.sellerName || config.defaultSellerName || 'Seller Chat';
      const avatar = avatarEl();
      if (avatar) {
        avatar.src = state.sellerAvatarUrl || config.defaultSellerAvatarUrl || '';
        avatar.alt = state.sellerName || config.defaultSellerName || 'Seller';
      }
    }

    function applyConversationMeta(payload) {
      if (!payload) return;

      const currentUserId = String(payload.currentUserId || payload.CurrentUserId || '');
      if (currentUserId) {
        state.currentUserId = currentUserId;
      }

      const counterpartyRole = String(payload.counterpartyRole || payload.CounterpartyRole || '').toLowerCase();
      const displayName = String(payload.displayName || payload.DisplayName || '').trim();
      const avatarUrl = String(payload.avatarUrl || payload.AvatarUrl || '').trim();

      if (counterpartyRole === 'seller' && displayName) {
        state.sellerName = displayName;
      }

      if (counterpartyRole === 'seller' && avatarUrl) {
        state.sellerAvatarUrl = avatarUrl;
      }

      updateHeader();
    }

    function renderEmpty(message) {
      const threadNode = thread();
      if (!threadNode) return;
      threadNode.innerHTML = '';
      const empty = document.createElement('div');
      empty.className = 'chat-empty';
      empty.textContent = message;
      threadNode.appendChild(empty);
    }

    function setFaqStatus(message, isError) {
      const node = faqStatusNode();
      if (!node) return;
      if (!message) {
        node.hidden = true;
        node.textContent = '';
        node.classList.remove('error');
        return;
      }
      node.hidden = false;
      node.textContent = message;
      node.classList.toggle('error', Boolean(isError));
    }

    function setFaqPlacementReady(isReady) {
      state.faqPlacementReady = Boolean(isReady);
      const panel = faqPanel();
      if (!panel) return;
      panel.hidden = !state.faqPlacementReady;
      panel.classList.toggle('is-pending', !state.faqPlacementReady);
      if (!state.faqPlacementReady) {
        state.faqFabExpanded = false;
      }
      syncFaqFabState();
    }

    function syncFaqFabState() {
      const panel = faqPanel();
      const fab = faqFab();
      if (!panel || !fab) return;
      const isFabMode = panel.classList.contains('is-fab');
      const isExpanded = isFabMode && state.faqFabExpanded && state.faqPlacementReady;
      fab.hidden = !isFabMode || !state.faqPlacementReady;
      fab.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
      fab.classList.toggle('is-active', isExpanded);
      panel.classList.toggle('is-expanded', isExpanded);
    }

    function positionFaqPanel(showInline) {
      const panel = faqPanel();
      const threadNode = thread();
      const composer = input()?.closest('.seller-chat-input');
      if (!panel || !threadNode || !composer) return;

      if (showInline) {
        if (panel.parentElement !== threadNode) {
          threadNode.insertBefore(panel, threadNode.firstChild);
        }
        panel.classList.add('is-inline');
        panel.classList.remove('is-fab', 'is-expanded');
        state.faqFabExpanded = false;
        return;
      }

      if (panel.parentElement !== composer.parentElement) {
        composer.parentElement.insertBefore(panel, composer);
      }
      panel.classList.add('is-fab');
      panel.classList.remove('is-inline', 'is-expanded');
      state.faqFabExpanded = false;
    }

    function renderFaqButtons(items, cssClass, onClick) {
      const listNode = faqList();
      if (!listNode) return;
      listNode.innerHTML = '';
      items.forEach(function (item) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'seller-chat-faq-btn ' + cssClass;
        button.textContent = item.label;
        button.addEventListener('click', function () {
          onClick(item.value);
        });
        listNode.appendChild(button);
      });
    }

    function renderFaqPanel() {
      const panel = faqPanel();
      const titleNode = faqTitle();
      const back = faqBack();
      const faqs = Array.isArray(state.faqs) ? state.faqs : [];
      
      console.log('renderFaqPanel: Called');
      console.log('renderFaqPanel: FAQ count:', faqs.length);
      console.log('renderFaqPanel: Panel ready:', state.faqPlacementReady);
      console.log('renderFaqPanel: Seller ID:', state.sellerUserId);
      console.log('renderFaqPanel: Full FAQs array:', state.faqs);
      
      if (!panel || !faqList()) {
        console.warn('renderFaqPanel: panel or faqList not found', { panel: !!panel, faqList: !!faqList() });
        return;
      }

      if (!faqs.length) {
        console.warn('renderFaqPanel: No FAQs available. state.faqs =', state.faqs);
        panel.hidden = !state.faqPlacementReady;
        if (titleNode) titleNode.textContent = 'Quick Actions - No quick actions available';
        if (back) back.hidden = true;
        faqList().innerHTML = '';
        setFaqStatus('Make sure to set up quick actions for this seller', false);
        return;
      }

      panel.hidden = !state.faqPlacementReady;
      const groupedFaqs = faqs.reduce(function (accumulator, item) {
        const key = String(item.category || 'General').trim() || 'General';
        const normalizedKey = key.toLowerCase();
        if (!accumulator[normalizedKey]) {
          accumulator[normalizedKey] = { category: key, items: [] };
        }
        accumulator[normalizedKey].items.push(item);
        return accumulator;
      }, {});
      
      console.log('renderFaqPanel: Grouped FAQs:', groupedFaqs);

      const activeCategoryKey = String(state.activeFaqCategory || '').trim().toLowerCase();
      if (!activeCategoryKey || !groupedFaqs[activeCategoryKey]) {
        state.activeFaqCategory = '';
      }

      if (!state.activeFaqCategory) {
        if (titleNode) titleNode.textContent = 'Quick Actions - Select a category';
        if (back) back.hidden = true;
        setFaqStatus('', false);
        renderFaqButtons(
          Object.keys(groupedFaqs).sort().map(function (key) {
            return { label: groupedFaqs[key].category, value: groupedFaqs[key].category };
          }),
          'category',
          function (category) {
            state.activeFaqCategory = category;
            renderFaqPanel();
          });
        return;
      }

      const group = groupedFaqs[activeCategoryKey];
      if (titleNode) titleNode.textContent = group ? group.category : state.activeFaqCategory;
      if (back) back.hidden = false;
      const items = (group ? group.items.slice() : []).sort(function (left, right) {
        return left.question.localeCompare(right.question);
      });
      setFaqStatus(items.length ? (items.length + ' question' + (items.length === 1 ? '' : 's')) : 'No questions in this category.', false);
      renderFaqButtons(items.map(function (faq) {
        return { label: faq.question, value: faq };
      }), 'question', function (faq) {
        clickFaq(faq);
      });
    }

    function scrollThreadToLatest() {
      const threadNode = thread();
      if (!threadNode) return;

      const alignToBottom = function () {
        threadNode.scrollTop = threadNode.scrollHeight;
      };

      alignToBottom();
      window.requestAnimationFrame(alignToBottom);
      window.setTimeout(alignToBottom, 0);
    }

    function renderMessages() {
      const threadNode = thread();
      if (!threadNode) return;
      threadNode.innerHTML = '';

      if (state.lastError) {
        setFaqPlacementReady(false);
        renderEmpty(state.lastError);
        return;
      }
      if (state.isLoading) {
        renderEmpty('Loading messages...');
        positionFaqPanel(true);
        setFaqPlacementReady(true);
        renderFaqPanel();
        return;
      }
      if (!state.messages.length) {
        renderEmpty('No messages yet. Start the conversation.');
        positionFaqPanel(true);
        setFaqPlacementReady(true);
        renderFaqPanel();
        return;
      }

      positionFaqPanel(false);
      setFaqPlacementReady(true);
      renderFaqPanel();

      state.messages.forEach(function (message) {
        const bubble = renderBubble(message, state.currentUserId || '');
        bubble.querySelectorAll('img, video').forEach(function (media) {
          const eventName = media.tagName === 'VIDEO' ? 'loadedmetadata' : 'load';
          media.addEventListener(eventName, scrollThreadToLatest, { once: true });
        });
        threadNode.appendChild(bubble);
      });
      scrollThreadToLatest();
    }

    async function ensureConversation() {
      if (!state.sellerUserId) throw new Error('Chat is not ready for this seller yet.');
      if (state.conversationId) return state.conversationId;

      const payload = await requestWithCsrf('/api/messages/conversations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          contextType: config.contextType || 'general',
          sellerUserId: state.sellerUserId,
          orderId: config.orderId || null,
        }),
      });

      const conversationId = Number.parseInt(String(payload.conversationId || payload.ConversationId), 10);
      if (!Number.isInteger(conversationId) || conversationId <= 0) {
        throw new Error(DEFAULT_ERROR);
      }

      applyConversationMeta(payload);
      state.conversationId = conversationId;
      return conversationId;
    }

    async function resolveConversation() {
      if (!state.sellerUserId) throw new Error('Chat is not ready for this seller yet.');

      const query = new URLSearchParams();
      query.set('contextType', config.contextType || 'general');
      query.set('sellerUserId', state.sellerUserId);
      if (config.orderId) {
        query.set('orderId', String(config.orderId));
      }

      console.log('resolveConversation: Fetching conversation...');
      const payload = await request('/api/messages/conversations/resolve?' + query.toString());
      if (!payload) {
        console.log('resolveConversation: No conversation found');
        state.conversationId = null;
        return null;
      }

      const conversationId = Number.parseInt(String(payload.conversationId || payload.ConversationId), 10);
      if (!Number.isInteger(conversationId) || conversationId <= 0) {
        console.log('resolveConversation: Invalid conversation ID');
        state.conversationId = null;
        return null;
      }

      applyConversationMeta(payload);
      state.conversationId = conversationId;
      console.log('resolveConversation: Success, ID:', conversationId);
      return conversationId;
    }

    async function markRead() {
      if (!state.conversationId) return;
      try {
        await requestWithCsrf('/api/messages/conversations/' + state.conversationId + '/read', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: '{}',
        });
      } catch (error) {
        if (error.status !== 404) console.warn(error);
      }
    }

    async function loadMessages() {
      state.isLoading = true;
      state.lastError = '';
      setFaqPlacementReady(false);
      renderMessages();

      const startTime = Date.now();
      console.log('loadMessages: Starting to load messages...');

      try {
        console.log('loadMessages: Loading FAQs...');
        await loadFaqs();
        console.log('loadMessages: FAQs loaded in', Date.now() - startTime, 'ms');
        
        console.log('loadMessages: Resolving conversation...');
        const conversationId = await resolveConversation();
        console.log('loadMessages: Conversation resolved in', Date.now() - startTime, 'ms. ID:', conversationId);
        
        if (!conversationId) {
          console.log('loadMessages: No conversation found');
          state.messages = [];
          setComposer(true, 'Type a message...');
          return;
        }

        console.log('loadMessages: Fetching messages...');
        const payload = await request('/api/messages/conversations/' + conversationId + '/messages?pageSize=100');
        console.log('loadMessages: Messages fetched in', Date.now() - startTime, 'ms. Count:', payload ? payload.length : 0);
        state.messages = normalizeMessages(payload);
        setComposer(true, 'Type a message...');
        await markRead();
        console.log('loadMessages: Completed successfully in', Date.now() - startTime, 'ms');
      } catch (error) {
        console.error('loadMessages: Error occurred:', error, 'Time elapsed:', Date.now() - startTime, 'ms');
        state.messages = [];
        state.lastError = error.message || DEFAULT_ERROR;
        setComposer(false, 'Messaging unavailable');
        toast(state.lastError, 'info');
      } finally {
        state.isLoading = false;
      }

      renderMessages();
    }

    async function loadFaqs() {
      state.faqs = [];
      if (!state.sellerUserId) {
        console.log('loadFaqs: No seller user ID set. Current state:', state);
        return;
      }

      try {
        const url = '/api/messages/storefront/faqs?sellerUserId=' + encodeURIComponent(state.sellerUserId);
        console.log('loadFaqs: Fetching FAQs from:', url);
        console.log('loadFaqs: Seller user ID:', state.sellerUserId);
        
        const payload = await request(url);
        console.log('loadFaqs: Raw API response:', payload);
        
        if (Array.isArray(payload)) {
          console.log('loadFaqs: Response is array with', payload.length, 'items');
          const mapped = payload.map(function (item, index) {
            const mapped = {
              faqId: Number(item.faqId || item.FaqId || index + 1),
              question: String(item.question || item.Question || ''),
              answer: String(item.answer || item.Answer || ''),
              category: String(item.category || item.Category || 'General')
            };
            console.log('loadFaqs: Mapped item', index, ':', mapped);
            return mapped;
          });
          
          state.faqs = mapped.filter(function (item) { 
            const isValid = item.question && item.question.trim();
            if (!isValid) {
              console.warn('loadFaqs: Filtering out invalid FAQ:', item);
            }
            return isValid;
          });
          
          console.log('loadFaqs: After filtering, valid FAQs count:', state.faqs.length);
          console.log('loadFaqs: Final FAQs array:', state.faqs);
        } else {
          console.warn('loadFaqs: Response is NOT an array. Type:', typeof payload, 'Value:', payload);
        }
      } catch (error) {
        console.error('loadFaqs: Error loading FAQs:', error?.message || error);
        console.error('loadFaqs: Full error:', error);
        state.faqs = [];
      }
    }

    async function send() {
      const inputNode = input();
      if (!inputNode || inputNode.disabled) return;

      const body = inputNode.value.trim();
      const attachment = getAttachmentFile();
      if (!body && !attachment) return;

      const button = sendButton();
      if (button) button.disabled = true;

      try {
        const conversationId = await ensureConversation();
        const form = new FormData();
        if (body) form.append('body', body);
        if (attachment) form.append('attachment', attachment);
        await requestWithCsrf('/api/messages/conversations/' + conversationId + '/messages', {
          method: 'POST',
          body: form,
        });
        inputNode.value = '';
        resizeComposer();
        clearAttachment();
        await loadMessages();
      } catch (error) {
        state.lastError = error.message || DEFAULT_ERROR;
        renderMessages();
        toast(state.lastError, 'info');
      } finally {
        if (button) button.disabled = false;
      }
    }

    async function clickFaq(faq) {
      const inputNode = input();
      if (!faq || !faq.faqId || !state.sellerUserId) return;
      if (inputNode && !inputNode.disabled) {
        inputNode.value = faq.question || '';
        resizeComposer();
      }

      try {
        const payload = await requestWithCsrf('/api/messages/storefront/faqs/click', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            faqId: faq.faqId,
            sellerUserId: state.sellerUserId
          })
        });

        state.currentUserId = String(payload.currentUserId || payload.CurrentUserId || state.currentUserId || '');
        state.conversationId = Number.parseInt(String(payload.conversationId || payload.ConversationId || state.conversationId || 0), 10) || state.conversationId;
        state.messages = normalizeMessages(payload.messages || payload.Messages || []);
        state.lastError = '';
        renderMessages();
        scrollThreadToLatest();
      } catch (error) {
        state.lastError = error.message || DEFAULT_ERROR;
        renderMessages();
        toast(state.lastError, 'info');
      }
    }

    function removeHead() {
      if (state.headElement) {
        state.headElement.remove();
        state.headElement = null;
      }
    }

    function addHead() {
      const container = headContainer();
      if (!container || state.headElement) return;
      const head = document.createElement('div');
      head.className = config.headClassName || 'seller-chat-head';
      head.textContent = config.headTextFactory
        ? config.headTextFactory(state.sellerName)
        : (state.sellerName || 'S').charAt(0).toUpperCase();
      head.addEventListener('click', function () {
        removeHead();
        open();
      });
      container.appendChild(head);
      state.headElement = head;
    }

    function setSeller(meta) {
      const sellerUserId = Number.parseInt(String(meta?.sellerUserId || ''), 10);
      if (!Number.isInteger(sellerUserId) || sellerUserId <= 0) {
        console.warn('setSeller: Invalid seller user ID:', meta?.sellerUserId);
        return;
      }

      const changedSeller = state.sellerUserId !== String(sellerUserId);
      console.log('setSeller: Setting seller. ID:', sellerUserId, 'Changed:', changedSeller, 'Meta:', meta);
      
      state.sellerUserId = String(sellerUserId);
      state.sellerName = meta.sellerName || state.sellerName;
      state.sellerAvatarUrl = meta.sellerAvatarUrl || state.sellerAvatarUrl;
      updateHeader();

      if (changedSeller) {
        console.log('setSeller: Seller changed, resetting state');
        state.conversationId = null;
        state.currentUserId = null;
        state.messages = [];
        state.faqs = [];
        state.activeFaqCategory = '';
        state.faqFabExpanded = false;
        state.faqPlacementReady = false;
        state.lastError = '';
        clearAttachment();
      }
    }

    function open() {
      removeHead();
      updateHeader();
      const node = modal();
      if (node) {
        node.classList.add('active');
        node.style.display = 'block';
      }

      if (!state.sellerUserId) {
        state.lastError = 'Chat is not ready for this seller yet.';
        setComposer(false, 'Chat unavailable');
        renderMessages();
        return;
      }

      setComposer(true, 'Type a message...');
      loadMessages();
      input()?.focus();
    }

    function close() {
      const node = modal();
      if (!node) return;
      node.classList.remove('active');
      node.style.display = 'none';
    }

    function minimize() {
      close();
      addHead();
    }

    updateHeader();
    setComposer(false, 'Chat unavailable');
    attachmentInput()?.addEventListener('change', updateAttachmentUi);
    faqBack()?.addEventListener('click', function () {
      state.activeFaqCategory = '';
      renderFaqPanel();
    });
    faqFab()?.addEventListener('click', function () {
      state.faqFabExpanded = !state.faqFabExpanded;
      syncFaqFabState();
    });
    input()?.addEventListener('input', resizeComposer);
    input()?.addEventListener('keydown', function (event) {
      if (event.key !== 'Enter' || event.shiftKey) return;
      event.preventDefault();
      send();
    });
    clearAttachmentButton()?.addEventListener('click', function () {
      clearAttachment();
      input()?.focus();
    });
    resizeComposer();
    updateAttachmentUi();

    return {
      setSeller: setSeller,
      open: open,
      close: close,
      minimize: minimize,
      send: send,
      refresh: loadMessages,
      refreshFaqs: loadFaqs,
      clickFaq: clickFaq,
    };
  }

  window.createStorefrontMessaging = createStorefrontMessaging;
})();
