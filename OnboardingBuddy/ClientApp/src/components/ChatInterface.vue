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
        <input
          v-model="newMessage"
          @keyup.enter="sendMessage"
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
          accept=".pdf,.txt,.doc,.docx"
          style="display: none"
        />
        <button
          @click="triggerFileUpload"
          :disabled="isLoading"
          class="attach-button"
          title="Attach files"
        >
          📎
        </button>
        <button
          @click="sendMessage"
          :disabled="isLoading || !newMessage.trim()"
          class="send-button"
        >
          <span v-if="isLoading">⏳</span>
          <span v-else>🚀</span>
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
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue'
import * as signalR from '@microsoft/signalr'
import { buildSignalRUrl, buildApiUrl, debugPaths } from '../utils/pathUtils.js'

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
let sessionId = generateSessionId()

onMounted(() => {
  // Debug paths in development
  if (import.meta.env.DEV) {
    debugPaths()
  }
  
  initializeSignalR()
  setupDragAndDrop()
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
      html: response,
      isUser: false,
      timestamp: new Date()
    }
    messages.value.push(message)
    isLoading.value = false
    scrollToBottom()
  })

  try {
    await connection.start()
    console.log(`SignalR connection started to: ${hubUrl}`)
  } catch (err) {
    console.error('SignalR connection error:', err)
  }
}

function generateMessageId() {
  return Date.now() + '_' + Math.random().toString(36).substr(2, 9)
}

async function sendMessage() {
  if (!newMessage.value.trim() && !selectedFiles.value.length) return

  const messageText = newMessage.value.trim()
  
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
      html: data.response,
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
}

.attach-button:hover, .send-button:hover {
  transform: scale(1.1);
  box-shadow: 0 4px 15px rgba(255, 123, 123, 0.4);
}

.send-button:disabled {
  background: #ccc;
  cursor: not-allowed;
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
</style>