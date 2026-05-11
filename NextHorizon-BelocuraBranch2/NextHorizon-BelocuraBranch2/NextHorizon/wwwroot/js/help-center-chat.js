const chatThread = document.getElementById('chatThread');
const agentInput = document.getElementById('agentInput');
const typingIndicator = document.getElementById('typingIndicator');
const chatInputRow = document.getElementById('chatInputRow');
const supportBody = document.querySelector('.support-body');
const resolveButton = document.getElementById('resolveChatBtn');

let conversationId = null;
let currentCategory = null;
let supportFaqId = null;
let assignedAgentName = null;
let currentConversationStatus = null;
let inactivityTimer = null;
let inactivityResolved = false;
let isLoadingMessages = false;
const sellerId = Number(window.helpCenterSellerId || document.querySelector('.help-center-page')?.dataset?.sellerId || 0);

function loadSupportState() {
  const storedConversationId = localStorage.getItem('supportConversationId');
  const storedCategory = localStorage.getItem('supportConversationCategory');
  const storedFaqId = localStorage.getItem('supportFaqId');

  if (storedConversationId) {
    const id = Number(storedConversationId);
    conversationId = Number.isInteger(id) ? id : null;
  }

  if (storedCategory) {
    currentCategory = storedCategory;
  }

  if (storedFaqId) {
    const id = Number(storedFaqId);
    supportFaqId = Number.isInteger(id) ? id : null;
  }
}

function saveSupportState() {
  if (conversationId) {
    localStorage.setItem('supportConversationId', conversationId.toString());
  } else {
    localStorage.removeItem('supportConversationId');
  }

  if (currentCategory) {
    localStorage.setItem('supportConversationCategory', currentCategory);
  } else {
    localStorage.removeItem('supportConversationCategory');
  }

  if (supportFaqId) {
    localStorage.setItem('supportFaqId', supportFaqId.toString());
  } else {
    localStorage.removeItem('supportFaqId');
  }
}

function clearSupportState() {
  localStorage.removeItem('supportConversationId');
  localStorage.removeItem('supportConversationCategory');
  localStorage.removeItem('supportFaqId');
  conversationId = null;
  currentCategory = null;
  supportFaqId = null;
}

function formatDate(value) {
  const date = value ? new Date(value) : new Date();
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function scrollChatToBottom() {
  if (!supportBody) return;
  supportBody.scrollTop = supportBody.scrollHeight;
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

async function parseResponse(response) {
  const text = await response.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }

  if (!response.ok) {
    const errorMessage = data?.error || data || `${response.status} ${response.statusText}`;
    throw new Error(errorMessage);
  }

  return data;
}

function setResolveVisibility(visible) {
  if (!resolveButton) return;
  resolveButton.classList.toggle('hidden', !visible);
}

function setTyping(visible) {
  if (!typingIndicator) return;
  typingIndicator.style.display = visible ? 'block' : 'none';
}

function setCategoryButtonsEnabled(enabled) {
  document.querySelectorAll('.help-btn, .chat-with-agent-btn').forEach(btn => {
    btn.disabled = !enabled;
    btn.classList.toggle('disabled', !enabled);
  });
}

function setChatEnabled(enabled) {
  if (agentInput) {
    agentInput.disabled = !enabled;
  }

  if (chatInputRow) {
    chatInputRow.classList.toggle('hidden', !enabled);
  }
}

function renderWelcomeState() {
  if (!chatThread) return;
  chatThread.innerHTML = '';
  addMessage('Welcome Seller, choose a category and send your concern to begin.', 'bot');
}

function addMessage(text, type, timestamp = null) {
  if (!chatThread) return;

  const msg = document.createElement('div');
  msg.className = `chat-message ${type}`;
  msg.innerHTML = `<div>${escapeHtml(text)}</div><div class='chat-ts'>${formatDate(timestamp)}</div>`;
  chatThread.appendChild(msg);
  scrollChatToBottom();
}

function renderMessages(messages) {
  if (!chatThread) return;

  chatThread.innerHTML = '';

  if (!messages || messages.length === 0) {
    renderWelcomeState();
    return;
  }

  messages.forEach(message => {
    const type = message.senderRole === 'Seller' ? 'user' : 'bot';
    let text = message.messageText;
    if (message.senderRole === 'Agent' && assignedAgentName) {
      text = `${assignedAgentName}: ${text}`;
    }
    addMessage(text, type, message.createdAt);
  });
}

function resetInactivityTimer(messages) {
  clearTimeout(inactivityTimer);
  inactivityTimer = null;

  if (!supportFaqId || !conversationId || inactivityResolved) {
    return;
  }

  if (!messages || messages.length === 0) {
    return;
  }

  if (currentConversationStatus === 'Resolved' || currentConversationStatus === 'Closed') {
    return;
  }

  const sellerMessages = messages
    .filter(message => message.senderRole === 'Seller')
    .map(message => new Date(message.createdAt).getTime())
    .sort((a, b) => b - a);

  if (sellerMessages.length === 0) {
    return;
  }

  const lastSellerTime = sellerMessages[0];
  const now = Date.now();
  const timeoutMs = 5 * 60 * 1000;
  const remaining = timeoutMs - (now - lastSellerTime);

  if (remaining <= 0) {
    triggerInactivityResolve();
    return;
  }

  inactivityTimer = setTimeout(triggerInactivityResolve, remaining);
}

async function triggerInactivityResolve() {
  if (!conversationId || !supportFaqId || inactivityResolved) {
    return;
  }

  if (currentConversationStatus === 'Resolved' || currentConversationStatus === 'Closed') {
    return;
  }

  try {
    const response = await fetch(`/api/support/${conversationId}/status?status=Resolved&supportFaqId=${supportFaqId}&inactivity=true`, {
      method: 'PUT'
    });

    await parseResponse(response);
    inactivityResolved = true;
    currentConversationStatus = 'Resolved';
    addMessage('The conversation will now be closed due to inactivity. Have a great day!', 'bot');
    setResolveVisibility(false);
    setChatEnabled(false);
    setCategoryButtonsEnabled(true);
    await loadMessages();
    clearSupportState();
  } catch (error) {
    console.error('Error auto-resolving conversation:', error);
  }
}

async function loadMessages() {
  if (!supportFaqId || isLoadingMessages) return;

  isLoadingMessages = true;
  try {
    const response = await fetch(`/api/support/faq/${supportFaqId}/messages`);
    if (!response.ok) {
      throw new Error('Failed to load messages');
    }

    const data = await response.json();
    const messages = data.messages ?? data;
    assignedAgentName = data.agentName ?? null;
    currentConversationStatus = data.status ?? currentConversationStatus;

    if (currentConversationStatus === 'Resolved' || currentConversationStatus === 'Closed') {
      setResolveVisibility(false);
      setChatEnabled(false);
      setCategoryButtonsEnabled(true);
    } else {
      setChatEnabled(true);
      setCategoryButtonsEnabled(false);
    }

    renderMessages(messages);
    resetInactivityTimer(messages);
  } catch (error) {
    console.error('Error loading messages:', error);
  } finally {
    isLoadingMessages = false;
  }
}

async function ensureConversation(firstMessage) {
  if (!sellerId || sellerId <= 0) {
    throw new Error('Seller session is unavailable. Refresh and try again.');
  }

  if (conversationId) {
    return { conversationId, initialMessageSent: false };
  }

  if (!currentCategory) {
    throw new Error('Please choose a category first');
  }

  const params = new URLSearchParams({
    sellerId: sellerId.toString(),
    category: currentCategory,
    question: firstMessage
  });

  const response = await fetch(`/api/support/start?${params.toString()}`, {
    method: 'POST'
  });

  const data = await parseResponse(response);

  conversationId = data.id;
  supportFaqId = data.supportFaqId ?? null;
  saveSupportState();
  setResolveVisibility(true);
  setCategoryButtonsEnabled(false);
  chatInputRow?.classList.remove('hidden');

  return { conversationId, initialMessageSent: true };
}

async function resolveChat() {
  if (!conversationId) {
    alert('No active conversation to resolve');
    return;
  }

  try {
    const response = await fetch(`/api/support/${conversationId}/status?status=Resolved${supportFaqId ? `&supportFaqId=${supportFaqId}` : ''}`, {
      method: 'PUT'
    });

    const data = await parseResponse(response);

    addMessage('Conversation marked as resolved.', 'bot');
    clearSupportState();
    setResolveVisibility(false);
    setCategoryButtonsEnabled(true);
    chatInputRow?.classList.add('hidden');
  } catch (error) {
    console.error('Error resolving conversation:', error);
    alert(`Could not resolve conversation: ${error.message}`);
  }
}

function clearChat() {
  setTyping(false);
  clearSupportState();
  setResolveVisibility(false);
  setCategoryButtonsEnabled(true);
  chatInputRow?.classList.add('hidden');
  if (agentInput) {
    agentInput.value = '';
  }
  renderWelcomeState();
}

async function sendAgentMessage() {
  const text = agentInput?.value.trim() ?? '';
  if (!text) {
    return;
  }

  try {
    const result = await ensureConversation(text);

    if (agentInput) {
      agentInput.value = '';
    }

    if (result.initialMessageSent) {
      await loadMessages();
      return;
    }

    if (!supportFaqId) {
      throw new Error('Support FAQ id is missing for this conversation');
    }

    const response = await fetch('/api/support/message', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        conversationId,
        supportFAQId: supportFaqId,
        senderId: sellerId,
        senderRole: 'Seller',
        messageText: text,
        category: currentCategory
      })
    });

    await parseResponse(response);

    await loadMessages();
  } catch (error) {
    console.error('Error sending message:', error);
    alert(error.message);
  }
}

async function sendTopic(topicText) {
  currentCategory = topicText;
  setResolveVisibility(Boolean(conversationId));
  chatInputRow?.classList.remove('hidden');

  if (!conversationId) {
    renderWelcomeState();
    addMessage(`Category selected: ${topicText}. Send your concern to start the conversation.`, 'bot');
  } else {
    await loadMessages();
  }

  saveSupportState();
  agentInput?.focus();
}

if (agentInput) {
  agentInput.addEventListener('keypress', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      sendAgentMessage();
    }
  });
}

window.addEventListener('load', async () => {
  loadSupportState();

  if (conversationId) {
    try {
      const stateResponse = await fetch(`/api/support/${conversationId}/state`);
      if (!stateResponse.ok) {
        throw new Error('No open conversation found');
      }

      const stateData = await stateResponse.json();
      if (stateData.status === 'Resolved' || stateData.status === 'Closed') {
        clearSupportState();
        renderWelcomeState();
      } else {
        setResolveVisibility(true);
        setCategoryButtonsEnabled(false);
        chatInputRow?.classList.remove('hidden');
        await loadMessages();
      }
    } catch (error) {
      clearSupportState();
      renderWelcomeState();
    }
  } else {
    renderWelcomeState();
  }
});

setInterval(() => {
  loadMessages();
}, 2000);

window.sendTopic = sendTopic;
window.sendAgentMessage = sendAgentMessage;
window.clearChat = clearChat;
window.resolveChat = resolveChat;





