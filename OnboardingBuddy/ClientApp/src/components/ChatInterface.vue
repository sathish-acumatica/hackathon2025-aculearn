<template>
  <div 
    class="chat-container"
    :class="{ 'drag-over': isDragOver }"
  >
    <!-- Drag overlay -->
    <div v-if="isDragOver" class="drag-overlay">
      <div class="drag-content">
        <div class="drag-icon">📎</div>
        <p class="drag-text">Drop files here to attach</p>
      </div>
    </div>
    
    <div class="chat-messages" ref="messagesContainer">
      <div
        v-for="message in messages"
        :key="message.id"
        :class="['message', message.isUser ? 'user-message' : 'assistant-message']"
      >
        <div class="message-content">
          <div v-if="message.isUser" class="message-text">
            {{ message.text }}
          </div>
          <div v-if="message.isUser && message.files && message.files.length" class="user-attached-files">
            <div class="attached-files-label">Attached Files:</div>
            <div class="attached-files-list">
              <div v-for="file in message.files" :key="file.id" class="attached-file-item">
                <span class="file-icon">{{ getFileIcon(file.type) }}</span>
                <span class="file-name">{{ file.name }}</span>
              </div>
            </div>
          </div>
          <div v-else-if="!message.isUser" class="message-text" v-html="message.html || message.text"></div>
          <div class="message-time">{{ formatTime(message.timestamp) }}</div>
        </div>
      </div>
      
      <div v-if="isLoading" class="message assistant-message">
        <div class="message-content">
          <div class="typing-indicator">
            <span></span>
            <span></span>
            <span></span>
          </div>
        </div>
      </div>
    </div>
    
    <div class="chat-input-container">
      <div class="chat-input">
        <textarea
          v-model="newMessage"
          @keyup.enter="sendMessage"
          @paste="handlePaste"
          type="text"
          placeholder="Ask me anything about your onboarding journey..."
          :disabled="isLoading"
          class="message-input"
        />
        <input
          ref="fileInput"
          type="file"
          multiple
          @change="onFileSelected"
          accept=".pdf,.doc,.docx,.txt,.rtf,.odt,.pages,.tex,.md,.markdown,.ppt,.pptx,.odp,.key,.xls,.xlsx,.ods,.numbers,.jpg,.jpeg,.png,.gif,.bmp,.webp,.svg,.tiff,.tif,.ico,.heic,.heif,.mp3,.wav,.flac,.aac,.ogg,.m4a,.wma,.opus,.mp4,.avi,.mov,.wmv,.flv,.webm,.mkv,.m4v,.3gp,.js,.ts,.jsx,.tsx,.py,.java,.c,.cpp,.cs,.php,.rb,.go,.rs,.swift,.kt,.scala,.html,.htm,.css,.scss,.sass,.less,.xml,.yaml,.yml,.toml,.ini,.cfg,.conf,.sql,.sh,.bat,.ps1,.r,.m,.pl,.lua,.dart,.elm,.clj,.hs,.ml,.fs,.vb,.json,.csv,.tsv,.parquet,.avro,.jsonl,.ndjson,.zip,.rar,.7z,.tar,.gz,.bz2,.xz,.epub,.mobi,.azw,.fb2,.djvu,.cbr,.cbz"
          style="display: none"
        />
        <button
          @click="triggerFileUpload"
          :disabled="isLoading"
          class="attach-button"
          title="Attach files"
        >
          📋
        </button>
        <button
          @click="sendMessage"
          :disabled="isLoading || !newMessage.trim()"
          class="send-button"
        >
          <span v-if="isLoading">⏳</span>
          <svg v-else width="18" height="18" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M2.01 21L23 12L2.01 3L2 10L17 12L2 14L2.01 21Z" fill="currentColor"/>
          </svg>
        </button>
      </div>
      
      <div v-if="selectedFiles.length" class="selected-files">
        <div class="selected-files-header">Selected Files:</div>
        <div class="file-list">
          <div v-for="(file, index) in selectedFiles" :key="index" class="file-item">
            <span class="file-icon">{{ getFileIcon(file.type) }}</span>
            <span class="file-name">{{ file.name }}</span>
            <button @click="removeFile(index)" class="remove-file">×</button>
          </div>
        </div>
      </div>
    </div>
    
    <!-- Image Preview Modal -->
    <div v-if="imagePreview.isOpen" class="image-preview-modal" @click="closeImagePreview">
      <div class="image-preview-content" @click.stop>
        <button class="image-preview-close" @click="closeImagePreview">×</button>
        <img :src="imagePreview.src" :alt="imagePreview.alt" class="image-preview-img" />
        <div class="image-preview-caption">{{ imagePreview.alt }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { buildSignalRUrl, buildApiUrl, debugPaths } from '../utils/pathUtils.js'
import { getBrowserSessionId, keepSessionAlive, getSessionInfo } from '../utils/sessionUtils.js'

// Reactive data
const messages = ref([])
const newMessage = ref('')
const isLoading = ref(false)
const isDragOver = ref(false)
const selectedFiles = ref([])
const messagesContainer = ref(null)
const fileInput = ref(null)

// SignalR connection
let connection = null
let sessionId = getBrowserSessionId() // Use persistent browser session

const chatSessionInfo = ref(getSessionInfo())

// Configure marked for better markdown rendering
marked.setOptions({
  breaks: true, // Convert \n to <br>
  gfm: true,    // GitHub Flavored Markdown
  sanitize: false // We'll use DOMPurify for sanitization
})

// Function to process AI response (HTML-first approach like the old successful version)
function processMarkdown(text) {
  try {
    // Check if text already contains HTML tags (AI should send HTML directly)
    if (text.includes('<p>') || text.includes('<h2>') || text.includes('<h3>') || 
        text.includes('<strong>') || text.includes('<em>') || text.includes('<ul>') || text.includes('<li>')) {
      // Text is already HTML, just sanitize it and return
      return DOMPurify.sanitize(text, {
        ALLOWED_TAGS: ['p', 'br', 'strong', 'em', 'u', 'code', 'pre', 'blockquote', 'ul', 'ol', 'li', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'a', 'img', 'table', 'thead', 'tbody', 'tr', 'td', 'th', 'div', 'span'],
        ALLOWED_ATTR: ['href', 'src', 'alt', 'title', 'class', 'width', 'height', 'loading', 'data-*', 'style', 'target'],
        ALLOW_DATA_ATTR: true
      })
    } else {
      // Fallback: Text is plain text or markdown, convert to HTML first
      const html = marked(text)
      // Sanitize the HTML to prevent XSS
      return DOMPurify.sanitize(html, {
        ALLOWED_TAGS: ['p', 'br', 'strong', 'em', 'u', 'code', 'pre', 'blockquote', 'ul', 'ol', 'li', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'a', 'img', 'table', 'thead', 'tbody', 'tr', 'td', 'th', 'div', 'span'],
        ALLOWED_ATTR: ['href', 'src', 'alt', 'title', 'class', 'width', 'height', 'loading', 'data-*', 'style', 'target'],
        ALLOW_DATA_ATTR: true
      })
    }
  } catch (error) {
    console.error('Error processing response:', error)
    return text // Return original text if processing fails
  }
}

// Enhanced function to process images in HTML
function enhanceImages(html) {
  // Create a temporary div to parse the HTML
  const tempDiv = document.createElement('div')
  tempDiv.innerHTML = html
  
  // Find all images and enhance them
  const images = tempDiv.querySelectorAll('img')
  images.forEach((img, index) => {
    // Add lazy loading
    img.setAttribute('loading', 'lazy')
    
    // Add responsive classes
    img.classList.add('chat-image')
    
    // Add click handler for preview (using data attribute)
    img.setAttribute('data-preview', 'true')
    img.setAttribute('data-index', index)
    
    // Add error handling placeholder
    img.setAttribute('onerror', "this.style.display='none'; this.nextElementSibling.style.display='block';")
    
    // Create error fallback
    const errorDiv = document.createElement('div')
    errorDiv.className = 'image-error'
    errorDiv.style.display = 'none'
    errorDiv.innerHTML = `
      <div class="image-error-content">
        <span class="image-error-icon">🖼️</span>
        <span class="image-error-text">Image could not be loaded</span>
        <a href="${img.src}" target="_blank" class="image-error-link">View Original</a>
      </div>
    `
    
    // Insert error div after image
    img.parentNode.insertBefore(errorDiv, img.nextSibling)
  })
  
  return tempDiv.innerHTML
}

// Updated markdown processing with image enhancement
function processMarkdownWithImages(text) {
  console.log('Processing markdown for text:', text.substring(0, 100) + '...')
  const basicHtml = processMarkdown(text)
  console.log('Basic HTML result:', basicHtml.substring(0, 100) + '...')
  const enhanced = enhanceImages(basicHtml)
  console.log('Enhanced HTML result:', enhanced.substring(0, 100) + '...')
  return enhanced
}

// Function to handle image preview clicks
function handleImageClick(event) {
  if (event.target.tagName === 'IMG' && event.target.dataset.preview === 'true') {
    openImagePreview(event.target.src, event.target.alt || 'Image preview')
  }
}

// Image preview modal functionality
const imagePreview = ref({
  isOpen: false,
  src: '',
  alt: ''
})

function openImagePreview(src, alt) {
  imagePreview.value = {
    isOpen: true,
    src,
    alt
  }
}

function closeImagePreview() {
  imagePreview.value.isOpen = false
}

onMounted(() => {
  // Debug paths in development
  if (import.meta.env.DEV) {
    debugPaths()
  }
  
  initializeSignalR()
  setupDragAndDrop()
  
  // Add click listener for image previews
  document.addEventListener('click', handleImageClick)
})

// Cleanup on unmount
onUnmounted(() => {
  document.removeEventListener('click', handleImageClick)
})

function generateSessionId() {
  return 'session_' + Math.random().toString(36).substr(2, 9) + '_' + Date.now()
}

async function initializeSignalR() {
  const hubUrl = buildSignalRUrl('chatHub')
  
  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .build()

  connection.on('ReceiveMessage', (response) => {
    const message = {
      id: generateMessageId(),
      text: response,
      html: processMarkdownWithImages(response),
      isUser: false,
      timestamp: new Date()
    }
    messages.value.push(message)
    isLoading.value = false
    scrollToBottom()
  })

  connection.on('ConversationHistory', (history) => {
    console.log('Received conversation history:', history)
    // Clear existing messages and populate with history
    messages.value = []
    
    history.forEach(msg => {
      const message = {
        id: msg.id,
        text: msg.text,
        html: msg.isUser ? msg.text : processMarkdownWithImages(msg.text),
        isUser: msg.isUser,
        timestamp: new Date(msg.timestamp)
      }
      messages.value.push(message)
    })
    
    scrollToBottom()
  })

  try {
    await connection.start()
    console.log(`SignalR connection started to: ${hubUrl}`)
    
    // Register our browser session ID with the SignalR hub first
    await connection.invoke('RegisterBrowserSession', sessionId)
    console.log(`Registered browser session: ${sessionId}`)
    
    // Add a small delay to allow welcome message to be processed first
    setTimeout(async () => {
      try {
        // Request conversation history for this session
        await connection.invoke('GetConversationHistory')
        console.log('Requested conversation history')
      } catch (err) {
        console.error('Error requesting conversation history:', err)
      }
    }, 100)
  } catch (err) {
    console.error('SignalR connection error:', err)
  }
}

function generateMessageId() {
  return Date.now() + '_' + Math.random().toString(36).substr(2, 9)
}

async function handlePaste(e) {
    console.log('Paste handled');
    const clipboardData = e.clipboardData || window.clipboardData;

    if (!clipboardData || !clipboardData.items) {
        console.log('No clipboard data available');
        return;
    }

    const items = clipboardData.items;
    const files = [];

    // Extract files from clipboard
    for (let i = 0; i < items.length; i++) {
        const item = items[i];
        if (item.kind === 'file') {
            const file = item.getAsFile();
            if (file) {
                // Give pasted images a proper filename if they don't have one
                if (!file.name || file.name === 'blob') {
                    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
                    const extension = file.type.split('/')[1] || 'png';
                    Object.defineProperty(file, 'name', {
                        writable: false,
                        value: `pasted-image-${timestamp}.${extension}`
                    });
                }
                files.push(file);
                console.log('File found in clipboard:', file.name, file.type, file.size);
            }
        }
    }

    // If files were found, add them to the FileUpload component
    if (files.length > 0) {
        console.log('Files found, adding to selected files:', files.length);
        e.preventDefault(); // Prevent default paste behavior

        // Add files directly to selectedFiles instead of using onFileSelected
        selectedFiles.value.push(...files);
        
        console.log('Total selected files now:', selectedFiles.value.length);
    } else {
        console.log('No files found in clipboard');
    }
} 

async function sendMessage() {
  if (!newMessage.value.trim() && !selectedFiles.value.length) return

  const messageText = newMessage.value.trim()
  
  // Keep session alive on user activity
  keepSessionAlive()
  
  // Add user message
  const userMessage = {
    id: generateMessageId(),
    text: messageText || '(File upload)',
    isUser: true,
    timestamp: new Date(),
    files: selectedFiles.value.map(file => ({
      id: Math.random().toString(36),
      name: file.name,
      type: file.type
    }))
  }
  messages.value.push(userMessage)

  // Clear input
  newMessage.value = ''
  isLoading.value = true
  scrollToBottom()

  try {
    if (selectedFiles.value.length > 0) {
      // Send message with files
      await sendMessageWithFiles(messageText)
      selectedFiles.value = []
    } else {
      // Send text message via SignalR
      await connection.invoke('SendMessage', messageText)
    }
  } catch (error) {
    console.error('Error sending message:', error)
    addErrorMessage('Failed to send message. Please try again.')
    isLoading.value = false
  }
}

async function sendMessageWithFiles(message) {
  const apiUrl = buildApiUrl('api/chat/send-with-files')
  
  const formData = new FormData()
  formData.append('message', message || 'Please analyze the attached files.')
  formData.append('sessionId', sessionId)
  
  selectedFiles.value.forEach(file => {
    formData.append('files', file)
  })

  try {
    const response = await fetch(apiUrl, {
      method: 'POST',
      body: formData
    })

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`)
    }

    const data = await response.json()
    
    const assistantMessage = {
      id: generateMessageId(),
      text: data.response,
      html: processMarkdownWithImages(data.response),
      isUser: false,
      timestamp: new Date()
    }
    
    messages.value.push(assistantMessage)
    isLoading.value = false
    scrollToBottom()
  } catch (error) {
    console.error('Error sending message with files:', error)
    addErrorMessage('Failed to process files. Please try again.')
    isLoading.value = false
  }
}

function triggerFileUpload() {
  fileInput.value?.click()
}

function onFileSelected(event) {
  const files = Array.from(event.target.files)
  selectedFiles.value.push(...files)
  
  // Clear the input value to allow selecting the same file again
  event.target.value = ''
}

function removeFile(index) {
  selectedFiles.value.splice(index, 1)
}

function getFileIcon(fileType) {
  if (fileType?.includes('pdf')) return '📄'
  if (fileType?.includes('text')) return '📝'
  if (fileType?.includes('word')) return '📄'
  return '📎'
}

function formatTime(timestamp) {
  return new Date(timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function addErrorMessage(text) {
  const errorMessage = {
    id: generateMessageId(),
    text: text,
    html: `<div class="error-message">${text}</div>`,
    isUser: false,
    timestamp: new Date()
  }
  messages.value.push(errorMessage)
  scrollToBottom()
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
    }
  })
}

function setupDragAndDrop() {
  const container = document.querySelector('.chat-container')
  if (!container) return

  container.addEventListener('dragover', (e) => {
    e.preventDefault()
    isDragOver.value = true
  })

  container.addEventListener('dragleave', (e) => {
    if (!container.contains(e.relatedTarget)) {
      isDragOver.value = false
    }
  })

  container.addEventListener('drop', (e) => {
    e.preventDefault()
    isDragOver.value = false
    
    const files = Array.from(e.dataTransfer.files)
    selectedFiles.value.push(...files)
  })
}
</script>

<style scoped>
.chat-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  position: relative;
  overflow: hidden;
}

.chat-container.drag-over::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(102, 126, 234, 0.3);
  border: 3px dashed rgba(255, 255, 255, 0.8);
  z-index: 1000;
}

.drag-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(102, 126, 234, 0.9);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1001;
  backdrop-filter: blur(10px);
}

.drag-content {
  text-align: center;
  color: white;
}

.drag-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.drag-text {
  font-size: 1.5rem;
  font-weight: 600;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
  background: rgba(255, 255, 255, 0.05);
  min-height: 0;
}

.message {
  margin: 15px 0;
  display: flex;
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.user-message {
  justify-content: flex-end;
}

.assistant-message {
  justify-content: flex-start;
}

.message-content {
  max-width: 80%;
  padding: 15px 20px;
  border-radius: 20px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  backdrop-filter: blur(10px);
}

.user-message .message-content {
  background: linear-gradient(135deg, #ff7b7b 0%, #ff9a56 100%);
  color: white;
  border-bottom-right-radius: 5px;
}

.assistant-message .message-content {
  background: rgba(255, 255, 255, 0.9);
  color: #333;
  border-bottom-left-radius: 5px;
}

.message-text {
  font-size: 16px;
  line-height: 1.5;
  margin-bottom: 8px;
}

/* Formatted content styles for AI responses */
.assistant-message .message-text h1,
.assistant-message .message-text h2,
.assistant-message .message-text h3,
.assistant-message .message-text h4,
.assistant-message .message-text h5,
.assistant-message .message-text h6 {
  margin: 16px 0 8px 0;
  color: #2c3e50;
  font-weight: 600;
}

.assistant-message .message-text h1 { font-size: 1.5em; }
.assistant-message .message-text h2 { font-size: 1.3em; }
.assistant-message .message-text h3 { font-size: 1.2em; }

.assistant-message .message-text p {
  margin: 8px 0;
  line-height: 1.6;
}

.assistant-message .message-text code {
  background: #f8f9fa;
  padding: 2px 6px;
  border-radius: 4px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 0.9em;
  color: #e83e8c;
}

.assistant-message .message-text pre {
  background: #f8f9fa;
  padding: 12px;
  border-radius: 8px;
  overflow-x: auto;
  margin: 12px 0;
  border-left: 4px solid #667eea;
}

.assistant-message .message-text pre code {
  background: none;
  padding: 0;
  color: #333;
}

.assistant-message .message-text blockquote {
  border-left: 4px solid #667eea;
  padding-left: 16px;
  margin: 12px 0;
  font-style: italic;
  color: #666;
}

.assistant-message .message-text ul,
.assistant-message .message-text ol {
  margin: 8px 0;
  padding-left: 20px;
}

.assistant-message .message-text li {
  margin: 4px 0;
}

.assistant-message .message-text strong {
  font-weight: 600;
  color: #2c3e50;
}

.assistant-message .message-text em {
  font-style: italic;
  color: #555;
}

.assistant-message .message-text a {
  color: #667eea;
  text-decoration: none;
  border-bottom: 1px solid transparent;
  transition: border-color 0.3s ease;
}

.assistant-message .message-text a:hover {
  border-bottom-color: #667eea;
}

.assistant-message .message-text table {
  width: 100%;
  border-collapse: collapse;
  margin: 12px 0;
}

.assistant-message .message-text th,
.assistant-message .message-text td {
  border: 1px solid #ddd;
  padding: 8px 12px;
  text-align: left;
}

.assistant-message .message-text th {
  background: #f8f9fa;
  font-weight: 600;
}

.message-time {
  font-size: 12px;
  opacity: 0.7;
  text-align: right;
}

.user-message .message-time {
  color: rgba(255, 255, 255, 0.8);
}

.attached-files-label {
  font-size: 14px;
  font-weight: 600;
  margin: 10px 0 5px;
  color: rgba(255, 255, 255, 0.9);
}

.attached-files-list, .file-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.attached-file-item, .file-item {
  display: flex;
  align-items: center;
  gap: 5px;
  background: rgba(255, 255, 255, 0.2);
  padding: 5px 10px;
  border-radius: 15px;
  font-size: 14px;
}

.file-item {
  background: rgba(102, 126, 234, 0.2);
  color: white;
}

.remove-file {
  background: none;
  border: none;
  color: white;
  cursor: pointer;
  font-size: 16px;
  padding: 0 5px;
}

.typing-indicator {
  display: flex;
  gap: 5px;
  align-items: center;
}

.typing-indicator span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #667eea;
  animation: typing 1.4s infinite;
}

.typing-indicator span:nth-child(2) { animation-delay: 0.2s; }
.typing-indicator span:nth-child(3) { animation-delay: 0.4s; }

@keyframes typing {
  0%, 60%, 100% { transform: translateY(0); }
  30% { transform: translateY(-10px); }
}

.chat-input-container {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(20px);
  border-top: 1px solid rgba(255, 255, 255, 0.2);
  padding: 20px;
  flex-shrink: 0;
}

.chat-input {
  display: flex;
  gap: 10px;
  align-items: center;
}

.message-input {
  flex: 1;
  padding: 15px 20px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 25px;
  background: rgba(255, 255, 255, 0.9);
  font-size: 16px;
  outline: none;
  transition: all 0.3s ease;
}

.message-input:focus {
  border-color: #ff7b7b;
  box-shadow: 0 0 0 3px rgba(255, 123, 123, 0.2);
}

.attach-button, .send-button {
  padding: 15px;
  border: none;
  border-radius: 50%;
  background: linear-gradient(135deg, #ff7b7b 0%, #ff9a56 100%);
  color: white;
  font-size: 18px;
  cursor: pointer;
  transition: all 0.3s ease;
  width: 50px;
  height: 50px;
  display: flex;
  align-items: center;
  justify-content: center;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
  font-weight: bold;
}

.send-button svg {
  transition: transform 0.2s ease;
}

.attach-button:hover, .send-button:hover {
  transform: scale(1.1);
  box-shadow: 0 4px 15px rgba(255, 123, 123, 0.4);
}

.send-button:hover svg {
  transform: translateX(2px);
}

.send-button:disabled {
  background: #ccc;
  cursor: not-allowed;
  transform: none;
}

.send-button:disabled svg {
  transform: none;
}

.selected-files {
  margin-top: 15px;
  padding: 15px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 15px;
}

.selected-files-header {
  color: white;
  font-weight: 600;
  margin-bottom: 10px;
}

.error-message {
  color: #ff4757;
  background: rgba(255, 71, 87, 0.1);
  padding: 10px;
  border-radius: 8px;
  border: 1px solid rgba(255, 71, 87, 0.3);
}

/* Scrollbar styling */
.chat-messages::-webkit-scrollbar {
  width: 8px;
}

.chat-messages::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 4px;
}

.chat-messages::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.3);
  border-radius: 4px;
}

.chat-messages::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.5);
}

/* Image Styles */
.assistant-message .message-text .chat-image {
  max-width: 100%;
  max-height: 400px;
  border-radius: 8px;
  margin: 8px 0;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  cursor: pointer;
  transition: all 0.3s ease;
  display: block;
}

.assistant-message .message-text .chat-image:hover {
  transform: scale(1.02);
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.25);
}

.image-error {
  background: #f8f9fa;
  border: 2px dashed #dee2e6;
  border-radius: 8px;
  padding: 20px;
  text-align: center;
  margin: 8px 0;
}

.image-error-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.image-error-icon {
  font-size: 2rem;
  opacity: 0.5;
}

.image-error-text {
  color: #6c757d;
  font-size: 14px;
}

.image-error-link {
  color: #667eea;
  text-decoration: none;
  font-size: 12px;
  padding: 4px 8px;
  border: 1px solid #667eea;
  border-radius: 4px;
  transition: all 0.3s ease;
}

.image-error-link:hover {
  background: #667eea;
  color: white;
}

/* Image Preview Modal */
.image-preview-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.9);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  animation: fadeIn 0.3s ease;
}

.image-preview-content {
  position: relative;
  max-width: 90vw;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.image-preview-close {
  position: absolute;
  top: -40px;
  right: 0;
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  font-size: 24px;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.3s ease;
}

.image-preview-close:hover {
  background: rgba(255, 255, 255, 0.3);
}

.image-preview-img {
  max-width: 100%;
  max-height: 80vh;
  object-fit: contain;
  border-radius: 8px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
}

.image-preview-caption {
  color: white;
  margin-top: 16px;
  text-align: center;
  font-size: 14px;
  opacity: 0.8;
  max-width: 80%;
}

/* Responsive image styles */
@media (max-width: 768px) {
  .assistant-message .message-text .chat-image {
    max-height: 250px;
  }
  
  .image-preview-content {
    max-width: 95vw;
    max-height: 95vh;
  }
  
  .image-preview-img {
    max-height: 70vh;
  }
}

/* Session info for development */
.session-info-dev {
  background: rgba(0, 0, 0, 0.1);
  padding: 4px 8px;
  border-radius: 4px;
  margin-bottom: 10px;
  text-align: center;
  font-family: monospace;
  color: #666;
  border: 1px dashed #ccc;
}

.session-info-dev small {
  font-size: 11px;
}
</style>