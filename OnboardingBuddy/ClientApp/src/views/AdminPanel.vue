<template>
  <div class="admin-panel">
    <!-- Filter Bar -->
    <div class="filter-section">
      <div class="search-container">
        <input v-model="searchTerm" type="text" placeholder="Search materials..." class="search-input" />
        <div class="search-icon">🔍</div>
      </div>
      <div class="filter-controls">
        <select v-model="selectedCategory" class="category-select">
          <option value="">All Categories</option>
          <option v-for="category in categories" :key="category" :value="category">
            {{ category }}
          </option>
        </select>
        <label class="status-toggle">
          <input type="checkbox" v-model="showActiveOnly" />
          <span>Active Only</span>
        </label>
        <button @click="openCreateModal" class="btn-create">
          ➕ Add New Material
        </button>
      </div>
    </div>

    <!-- Materials Grid -->
    <div class="materials-container">
      <div v-if="loading" class="loading-state">
        <div class="loading-spinner">⏳</div>
        <p>Loading materials...</p>
      </div>

      <div v-else-if="filteredMaterials.length === 0" class="empty-state">
        <div class="empty-icon">📚</div>
        <h3>No materials found</h3>
        <p>Create your first training material to get started!</p>
      </div>

      <div v-else class="materials-grid">
        <div v-for="material in filteredMaterials" :key="material.id" class="material-card">
          <div class="card-header">
            <h3 class="material-title">{{ material.title }}</h3>
            <div class="card-actions">
              <button @click="editMaterial(material)" class="btn-edit" title="Edit">✏️</button>
              <button @click="deleteMaterial(material.id)" class="btn-delete" title="Delete">🗑️</button>
            </div>
          </div>

          <div class="card-body">
            <div class="material-category">
              <span class="category-badge">{{ material.category }}</span>
              <span class="status-badge" :class="{ active: material.isActive, inactive: !material.isActive }">
                {{ material.isActive ? '✅ Active' : '❌ Inactive' }}
              </span>
            </div>

            <div class="material-content" v-html="truncateContent(material.content)"></div>

            <div class="material-meta">
              <div class="date-info">
                📅 {{ formatDate(material.createdAt) }}
                <span v-if="material.updatedAt">(Updated: {{ formatDate(material.updatedAt) }})</span>
              </div>
            </div>

            <div v-if="material.internalNotes" class="internal-notes">
              <strong>📝 Notes:</strong> {{ material.internalNotes }}
            </div>

            <div v-if="material.attachments?.length" class="attachments-section-card">
              <div class="attachments-header">
                <strong>📎 Attachments ({{ material.attachments.length }})</strong>
              </div>
              <div class="attachment-list-card">
                <div v-for="attachment in material.attachments" :key="attachment.id" class="attachment-item-display">
                  <div class="attachment-info-display">
                    <span class="attachment-icon">📄</span>
                    <div class="attachment-details">
                      <span class="attachment-name-display">{{ attachment.originalFileName }}</span>
                      <span v-if="attachment.fileSizeBytes" class="attachment-size-display">
                        ({{ formatFileSize(attachment.fileSizeBytes) }})
                      </span>
                    </div>
                  </div>
                  <a :href="buildApiUrl(`api/fileupload/${attachment.id}/download`)" 
                     target="_blank" 
                     class="download-btn-card" 
                     title="Download file">
                    ⬇️ Download
                  </a>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create/Edit Modal -->
    <div v-if="showCreateModal || showEditModal" class="modal-overlay" :class="{ 'maximized': isMaximized }"
      @click="closeModal">
      <div class="modal-content" :class="{ 'maximized': isMaximized }" @click.stop>
        <div class="modal-header">
          <h2>{{ showEditModal ? '✏️ Edit Material' : '➕ Create New Material' }}</h2>
          <div class="header-actions">
            <button @click="toggleMaximize" class="btn-maximize" :title="isMaximized ? 'Restore' : 'Maximize'">
              {{ isMaximized ? '🗗' : '🗖' }}
            </button>
            <button @click="closeModal" class="btn-close">×</button>
          </div>
        </div>

        <form @submit.prevent="saveMaterial" class="modal-form">
          <div class="form-content">
            <div class="form-group">
              <label for="title">Title *</label>
              <input id="title" v-model="currentMaterial.title" type="text" required maxlength="200"
                placeholder="Enter material title" class="form-input" />
            </div>

            <div class="form-group">
              <label for="category">Category *</label>
              <input id="category" v-model="currentMaterial.category" type="text" required maxlength="100"
                placeholder="e.g., Training Materials, System Prompts" class="form-input" />
            </div>

            <div class="form-group">
              <div class="content-label-row">
                <label for="content">Content</label>
                <div class="content-actions">
                  <button type="button" class="btn-secondary" @click="openHtmlPasteModal" title="Paste raw HTML">📥 Paste HTML</button>
                  <button type="button" class="btn-secondary" @click="toggleSourceEditor" :title="showSourceEditor ? 'Hide HTML source editor' : 'Show & edit HTML source'">{{ showSourceEditor ? '🔧 Hide Source' : '🧪 Edit Source' }}</button>
                </div>
              </div>
              <QuillEditor ref="editorRef" v-model:content="currentMaterial.content" :options="editorOptions" content-type="html"
                class="rich-editor" />
              <p class="paste-hint">Paste formatted content directly (Word/HTML). Use "Paste HTML" for raw snippets.</p>
              <div v-if="showSourceEditor" class="source-editor-wrapper">
                <div class="source-editor-header">
                  <span>HTML Source (sanitized on apply)</span>
                  <div class="source-editor-actions">
                    <button type="button" class="btn-outline-sm" @click="refreshSourceFromEditor" title="Reload from current editor">↻ Reload</button>
                    <button type="button" class="btn-outline-sm" @click="applySourceHtml" :disabled="!sourceHtml.trim()" title="Apply HTML to editor">✅ Apply</button>
                    <button type="button" class="btn-outline-sm" @click="toggleSourceEditor" title="Close source editor">✖</button>
                  </div>
                </div>
                <textarea v-model="sourceHtml" class="source-textarea" spellcheck="false" placeholder="<p>Your HTML...</p>"></textarea>
                <div class="source-meta-row">
                  <span class="char-count">{{ sourceHtml.length }} chars</span>
                  <span v-if="sourceApplyMessage" class="apply-msg">{{ sourceApplyMessage }}</span>
                </div>
              </div>
            </div>

            <div class="form-group">
              <label for="internalNotes">Internal Notes</label>
              <textarea id="internalNotes" v-model="currentMaterial.internalNotes" rows="3"
                placeholder="Internal notes (not visible to AI)" class="form-textarea"></textarea>
            </div>

            <!-- File Attachments Section -->
            <div class="form-group">
              <label for="attachments">Attachments</label>
              <div class="attachments-section">
                <!-- Existing Attachments (for edit mode) -->
                <div v-if="showEditModal && currentMaterial.attachments?.length" class="existing-attachments">
                  <h4>Existing Attachments:</h4>
                  <div v-for="attachment in currentMaterial.attachments" :key="attachment.id" class="attachment-item">
                    <div class="attachment-info">
                      <span class="attachment-name">📎 {{ attachment.originalFileName }}</span>
                      <span class="attachment-size">({{ formatFileSize(attachment.fileSizeBytes) }})</span>
                      <span v-if="attachment.description" class="attachment-description">- {{ attachment.description }}</span>
                      <a :href="buildApiUrl(`api/fileupload/${attachment.id}/download`)" 
                         target="_blank" 
                         class="download-link-small" 
                         title="Download file">⬇️</a>
                    </div>
                    <button type="button" @click="removeAttachment(attachment.id)" class="btn-remove-attachment">🗑️</button>
                  </div>
                </div>

                <!-- New File Upload -->
                <div class="file-upload-section">
                  <input 
                    type="file" 
                    id="attachments" 
                    @change="handleFileSelect" 
                    multiple 
                    accept=".pdf,.doc,.docx,.txt,.rtf,.odt,.pages,.tex,.md,.markdown,.ppt,.pptx,.odp,.key,.xls,.xlsx,.ods,.numbers,.jpg,.jpeg,.png,.gif,.bmp,.webp,.svg,.tiff,.tif,.ico,.heic,.heif,.mp3,.wav,.flac,.aac,.ogg,.m4a,.wma,.opus,.mp4,.avi,.mov,.wmv,.flv,.webm,.mkv,.m4v,.3gp,.js,.ts,.jsx,.tsx,.py,.java,.c,.cpp,.cs,.php,.rb,.go,.rs,.swift,.kt,.scala,.html,.htm,.css,.scss,.sass,.less,.xml,.yaml,.yml,.toml,.ini,.cfg,.conf,.sql,.sh,.bat,.ps1,.r,.m,.pl,.lua,.dart,.elm,.clj,.hs,.ml,.fs,.vb,.json,.csv,.tsv,.parquet,.avro,.jsonl,.ndjson,.zip,.rar,.7z,.tar,.gz,.bz2,.xz,.epub,.mobi,.azw,.fb2,.djvu,.cbr,.cbz"
                    class="file-input"
                  />
                  <label for="attachments" class="file-upload-label">
                    <span class="upload-icon">📁</span>
                    Choose Files (PDF, DOC, TXT, Images)
                  </label>
                </div>

                <!-- Selected Files Preview -->
                <div v-if="selectedFiles.length" class="selected-files-preview">
                  <h4>New Files to Upload:</h4>
                  <div v-for="(file, index) in selectedFiles" :key="index" class="selected-file-item">
                    <div class="file-info">
                      <span class="file-name">📄 {{ file.name }}</span>
                      <span class="file-size">({{ formatFileSize(file.size) }})</span>
                    </div>
                    <div class="file-description">
                      <input 
                        v-model="fileDescriptions[index]" 
                        type="text" 
                        placeholder="Description (optional)"
                        class="description-input"
                      />
                    </div>
                    <button type="button" @click="removeSelectedFile(index)" class="btn-remove-file">❌</button>
                  </div>
                </div>
              </div>
            </div>

            <div class="form-group">
              <label class="checkbox-label">
                <input type="checkbox" v-model="currentMaterial.isActive" />
                <span>Active (visible to AI)</span>
              </label>
            </div>
          </div>

          <div class="form-actions">
            <button type="button" @click="closeModal" class="btn-cancel">Cancel</button>
            <button type="submit" :disabled="saving" class="btn-save">
              {{ saving ? '⏳ Saving...' : '💾 Save' }}
            </button>
          </div>
        </form>
      </div>
    </div>
    <!-- Raw HTML Paste Modal -->
    <div v-if="showHtmlPasteModal" class="html-paste-modal-overlay" @click.self="cancelHtmlPaste">
      <div class="html-paste-modal">
        <div class="html-paste-header">
          <h3>📥 Paste Raw HTML</h3>
          <button type="button" class="btn-close" @click="cancelHtmlPaste" title="Close">×</button>
        </div>
        <div class="html-paste-body">
          <p>Paste cleaned or exported HTML here. It will be sanitized before insertion.</p>
          <textarea v-model="rawHtmlInput" placeholder="<div>Example...</div>"></textarea>
          <div class="flex-row space-between" style="margin-top:6px;font-size:12px;">
            <span>{{ rawHtmlInput.length }} / {{ MAX_HTML_PASTE_CHARS }}</span>
            <span v-if="rawHtmlInput.length > MAX_HTML_PASTE_CHARS" style="color:#dc2626">Too large</span>
          </div>
          <div v-if="htmlPasteError" class="error-text">{{ htmlPasteError }}</div>
        </div>
        <div class="html-paste-footer">
          <button type="button" class="btn-outline" @click="cancelHtmlPaste">Cancel</button>
          <button type="button" class="btn-primary" :disabled="!rawHtmlInput.trim() || rawHtmlInput.length > MAX_HTML_PASTE_CHARS" @click="insertRawHtml">Insert</button>
        </div>
      </div>
    </div>
  </div>
  
  <!-- Toast Notification -->
  <div v-if="toast.show" :class="['admin-toast', `toast-${toast.type}`]">
    {{ toast.message }}
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { QuillEditor } from '@vueup/vue-quill'
import '@vueup/vue-quill/dist/vue-quill.snow.css'
import { buildApiUrl, debugPaths } from '../utils/pathUtils.js'
import DOMPurify from 'dompurify'

// Reactive data
const materials = ref([])
const searchTerm = ref('')
const selectedCategory = ref('')
const showActiveOnly = ref(false)
const loading = ref(false)
const saving = ref(false)
const showCreateModal = ref(false)
const showEditModal = ref(false)
const isMaximized = ref(false)

const currentMaterial = ref({
  title: '',
  category: '',
  content: '',
  internalNotes: '',
  isActive: true,
  attachments: []
})

// Rich HTML paste support refs
const editorRef = ref(null)
const showHtmlPasteModal = ref(false)
const rawHtmlInput = ref('')
const htmlPasteError = ref('')
const MAX_HTML_PASTE_CHARS = 50000
// Source editor
const showSourceEditor = ref(false)
const sourceHtml = ref('')
const sourceApplyMessage = ref('')

// Toast notification system
const toast = ref({
  show: false,
  message: '',
  type: 'success' // 'success', 'error', 'warning', 'info'
})

// File handling
const selectedFiles = ref([])
const fileDescriptions = ref([])
const fileInput = ref(null)

// Quill editor options
const editorOptions = {
  theme: 'snow',
  modules: {
    toolbar: [
      ['bold', 'italic', 'underline', 'strike'],
      ['blockquote', 'code-block'],
      [{ 'header': 1 }, { 'header': 2 }],
      [{ 'list': 'ordered' }, { 'list': 'bullet' }],
      [{ 'script': 'sub' }, { 'script': 'super' }],
      [{ 'indent': '-1' }, { 'indent': '+1' }],
      [{ 'direction': 'rtl' }],
      [{ 'size': ['small', false, 'large', 'huge'] }],
      [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
      [{ 'color': [] }, { 'background': [] }],
      [{ 'font': [] }],
      [{ 'align': [] }],
      ['link', 'image'],
      ['clean']
    ]
  },
  placeholder: 'Enter the material content (rich text supported)...'
}

// Computed properties
const categories = computed(() => {
  const cats = [...new Set(materials.value.map(m => m.category))]
  return cats.sort()
})

const filteredMaterials = computed(() => {
  let filtered = materials.value

  if (searchTerm.value) {
    const term = searchTerm.value.toLowerCase()
    filtered = filtered.filter(m =>
      m.title.toLowerCase().includes(term) ||
      m.category.toLowerCase().includes(term) ||
      m.content.toLowerCase().includes(term)
    )
  }

  if (selectedCategory.value) {
    filtered = filtered.filter(m => m.category === selectedCategory.value)
  }

  if (showActiveOnly.value) {
    filtered = filtered.filter(m => m.isActive)
  }

  return filtered.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
})

// Methods
onMounted(() => {
  // Debug paths in development
  if (import.meta.env.DEV) {
    debugPaths()
  }

  loadMaterials()

  // Attach enhanced paste handler once editor mounts
  setTimeout(() => {
    const quillEl = editorRef.value?.$el?.querySelector?.('.ql-editor')
    if (quillEl) {
      quillEl.addEventListener('paste', (e) => {
        if (!e.clipboardData) return
        const html = e.clipboardData.getData('text/html')
        if (html) {
          const cleaned = sanitizeHtml(html)
          e.preventDefault()
          const quill = editorRef.value?.getQuill?.()
          if (quill) {
            const range = quill.getSelection(true)
            const index = range ? range.index : quill.getLength() - 1
            quill.clipboard.dangerouslyPasteHTML(index, cleaned, 'user')
            currentMaterial.value.content = quill.root.innerHTML
          }
        }
      })
    }
  }, 300)
})

// Toast notification function
function showToast(message, type = 'success') {
  toast.value.message = message
  toast.value.type = type
  toast.value.show = true
  
  // Auto-hide after 3 seconds
  setTimeout(() => {
    toast.value.show = false
  }, 3000)
}

async function loadMaterials() {
  loading.value = true
  try {
    const apiUrl = buildApiUrl('api/trainingmaterials')
    console.log('Loading materials from:', apiUrl)

    const response = await fetch(apiUrl)
    if (response.ok) {
      materials.value = await response.json()
    } else {
      console.error('Failed to load materials:', response.status, response.statusText)
    }
  } catch (error) {
    console.error('Error loading materials:', error)
  }
  loading.value = false
}

function editMaterial(material) {
  currentMaterial.value = { ...material }
  showEditModal.value = true
  // If source editor is visible, sync it with the newly loaded material content
  if (showSourceEditor.value) {
    sourceHtml.value = currentMaterial.value.content || ''
    sourceApplyMessage.value = ''
  }
}

async function deleteMaterial(id) {
  if (!confirm('Are you sure you want to delete this material?')) return

  try {
    const apiUrl = buildApiUrl(`api/trainingmaterials/${id}`)
    console.log('Deleting material:', apiUrl)

    const response = await fetch(apiUrl, {
      method: 'DELETE'
    })

    if (response.ok) {
      // Show success toast
      showToast('Training material deleted successfully!', 'success')
      materials.value = materials.value.filter(m => m.id !== id)
    } else {
      showToast('Failed to delete training material. Please try again.', 'error')
      console.error('Failed to delete material:', response.status, response.statusText)
    }
  } catch (error) {
    showToast('An error occurred while deleting the training material.', 'error')
    console.error('Error deleting material:', error)
  }
}

// File handling functions
function handleFileSelect(event) {
  const files = Array.from(event.target.files)
  selectedFiles.value = [...selectedFiles.value, ...files]
  
  // Initialize descriptions for new files
  const newDescriptions = new Array(files.length).fill('')
  fileDescriptions.value = [...fileDescriptions.value, ...newDescriptions]
}

function removeSelectedFile(index) {
  selectedFiles.value.splice(index, 1)
  fileDescriptions.value.splice(index, 1)
}

async function removeAttachment(attachmentId) {
  if (!confirm('Are you sure you want to remove this attachment?')) return

  try {
    const apiUrl = buildApiUrl(`api/trainingmaterials/${currentMaterial.value.id}/attachments/${attachmentId}`)
    const response = await fetch(apiUrl, {
      method: 'DELETE'
    })

    if (response.ok) {
      currentMaterial.value.attachments = currentMaterial.value.attachments.filter(a => a.id !== attachmentId)
    } else {
      console.error('Failed to remove attachment:', response.status, response.statusText)
    }
  } catch (error) {
    console.error('Error removing attachment:', error)
  }
}

function formatFileSize(bytes) {
  if (bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

function clearFileSelection() {
  selectedFiles.value = []
  fileDescriptions.value = []
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}

async function saveMaterial() {
  saving.value = true

  try {
    // Sanitize content before persisting, since we later render with v-html
    if (currentMaterial.value.content) {
      currentMaterial.value.content = sanitizeHtml(currentMaterial.value.content)
    }
    // Check if we have files to upload
    const hasFiles = selectedFiles.value.length > 0

    if (hasFiles) {
      // Use the with-attachments endpoint
      const endpoint = showEditModal.value
        ? `api/trainingmaterials/${currentMaterial.value.id}/with-attachments`
        : 'api/trainingmaterials/with-attachments'

      const apiUrl = buildApiUrl(endpoint)
      const method = showEditModal.value ? 'PUT' : 'POST'

      console.log('Saving material with files:', method, apiUrl)

      // Create FormData for file upload
      const formData = new FormData()
      formData.append('title', currentMaterial.value.title)
      formData.append('category', currentMaterial.value.category)
      formData.append('content', currentMaterial.value.content)
      formData.append('internalNotes', currentMaterial.value.internalNotes)
      formData.append('isActive', currentMaterial.value.isActive.toString())

      // Add files and descriptions
      selectedFiles.value.forEach((file, index) => {
        formData.append('files', file)
        // Always append a description, even if empty, to maintain index alignment
        const description = fileDescriptions.value[index] || ''
        formData.append('attachmentDescriptions', description)
      })

      const response = await fetch(apiUrl, {
        method,
        body: formData
      })

      if (response.ok) {
        const savedMaterial = await response.json()
        console.log('Material saved with attachments:', savedMaterial)
        
        // Show success toast
        showToast(isEditMode.value ? `Training material "${savedMaterial.title}" updated successfully!` : `Training material "${savedMaterial.title}" created successfully!`, 'success')
        
        // Reload materials to get updated attachment info
        await loadMaterials()
        closeModal()
      } else {
        console.error('Failed to save material with attachments:', response.status, response.statusText)
      }
    } else {
      // Use the regular endpoint without files
      const endpoint = showEditModal.value
        ? `api/trainingmaterials/${currentMaterial.value.id}`
        : 'api/trainingmaterials'

      const apiUrl = buildApiUrl(endpoint)
      const method = showEditModal.value ? 'PUT' : 'POST'

      console.log('Saving material:', method, apiUrl)

      const response = await fetch(apiUrl, {
        method,
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(currentMaterial.value)
      })

      if (response.ok) {
        const savedMaterial = await response.json()

        // Show success toast
        showToast(showEditModal.value ? `Training material "${savedMaterial.title}" updated successfully!` : `Training material "${savedMaterial.title}" created successfully!`, 'success')

        if (showEditModal.value) {
          const index = materials.value.findIndex(m => m.id === savedMaterial.id)
          if (index !== -1) {
            materials.value[index] = savedMaterial
          }
        } else {
          materials.value.push(savedMaterial)
        }

        closeModal()
      } else {
        console.error('Failed to save material:', response.status, response.statusText)
      }
    }
  } catch (error) {
    console.error('Error saving material:', error)
  }

  saving.value = false
}

function closeModal() {
  showCreateModal.value = false
  showEditModal.value = false
  isMaximized.value = false
  currentMaterial.value = {
    title: '',
    category: '',
    content: '',
    internalNotes: '',
    isActive: true,
    attachments: []
  }
  clearFileSelection()
  // Reset source editor state to avoid showing stale previous content
  showSourceEditor.value = false
  sourceHtml.value = ''
  sourceApplyMessage.value = ''
}

function toggleMaximize() {
  isMaximized.value = !isMaximized.value
}

function truncateContent(content, maxLength = 200) {
  if (!content) return ''
  const stripped = content.replace(/<[^>]*>/g, '')
  return stripped.length > maxLength ? stripped.substring(0, maxLength) + '...' : stripped
}

function formatDate(dateString) {
  return new Date(dateString).toLocaleDateString()
}

// ---------- HTML Paste & Sanitization Helpers ----------
function openHtmlPasteModal() {
  rawHtmlInput.value = ''
  htmlPasteError.value = ''
  showHtmlPasteModal.value = true
}

function cancelHtmlPaste() {
  showHtmlPasteModal.value = false
  rawHtmlInput.value = ''
  htmlPasteError.value = ''
}

function sanitizeHtml(html) {
  let clean = DOMPurify.sanitize(html, {
    USE_PROFILES: { html: true },
    ALLOWED_ATTR: ['href','target','rel','src','alt','title','class','style','width','height','colspan','rowspan','align'],
    ALLOWED_TAGS: [
      'p','div','br','span','strong','em','u','s','blockquote','code','pre','ul','ol','li','h1','h2','h3','h4','h5','h6',
      'table','thead','tbody','tr','th','td','img','a'
    ]
  })
  clean = scrubWordArtifacts(clean)
  return clean
}

function scrubWordArtifacts(html) {
  if (!html) return ''
  // Remove comments & MSO conditionals
  html = html.replace(/<!--([\s\S]*?)-->/g, '')
  html = html.replace(/<!\[if !supportLists\]>[\s\S]*?<!\[endif\]>/gi, '')
  const parser = new DOMParser()
  const doc = parser.parseFromString(`<div id="__root">${html}</div>`, 'text/html')
  const root = doc.getElementById('__root')
  if (!root) return html
  const blockTags = new Set(['P','DIV','H1','H2','H3','H4','H5','H6','LI','UL','OL','BLOCKQUOTE','PRE','TABLE','TR','TD','TH'])
  const walker = doc.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null)
  const toRemove = []
  while (walker.nextNode()) {
    const el = walker.currentNode
    const tag = el.tagName
    if (['STYLE','SCRIPT','META','LINK','XML'].includes(tag) || /^(O:P|W:|V:)/i.test(tag)) { toRemove.push(el); continue }
    if (el.hasAttribute && el.hasAttribute('class')) {
      const filtered = el.getAttribute('class').split(/\s+/).filter(c => !/^Mso/.test(c))
      if (filtered.length) {
        el.setAttribute('class', filtered.join(' '))
      } else {
        el.removeAttribute('class')
      }
    }
    if (el.hasAttribute && el.hasAttribute('style')) {
      const style = el.getAttribute('style') || ''
      let allowed = ''
      if (blockTags.has(tag)) {
        const align = style.match(/text-align\s*:\s*(left|right|center|justify)/i)
        if (align) allowed += `text-align:${align[1].toLowerCase()};`
      }
      if (['IMG','TABLE','TD','TH'].includes(tag)) {
        const w = style.match(/width\s*:\s*([0-9]+)(px|%)/i)
        const h = style.match(/height\s*:\s*([0-9]+)(px|%)/i)
        if (w) allowed += `width:${w[1]}${w[2]};`
        if (h) allowed += `height:${h[1]}${h[2]};`
      }
      if (allowed) {
        el.setAttribute('style', allowed)
      } else {
        el.removeAttribute('style')
      }
    }
    if (tag === 'SPAN' && !el.attributes.length) {
      el.replaceWith(doc.createTextNode(el.textContent))
    }
  }
  toRemove.forEach(n => n.remove())
  let output = root.innerHTML
  output = output.replace(/(&nbsp;){2,}/g, '&nbsp; ')
  output = output.replace(/\n{2,}/g, '\n')
  return output
}

function insertRawHtml() {
  htmlPasteError.value = ''
  if (!rawHtmlInput.value.trim()) {
    htmlPasteError.value = 'No HTML provided.'
    return
  }
  if (rawHtmlInput.value.length > MAX_HTML_PASTE_CHARS) {
    htmlPasteError.value = `HTML too large (> ${MAX_HTML_PASTE_CHARS} chars).`
    return
  }
  const quill = editorRef.value?.getQuill?.()
  if (!quill) {
    htmlPasteError.value = 'Editor not ready.'
    return
  }
  const clean = sanitizeHtml(rawHtmlInput.value)
  quill.clipboard.dangerouslyPasteHTML(quill.getLength() - 1, clean, 'user')
  currentMaterial.value.content = quill.root.innerHTML
  showHtmlPasteModal.value = false
  rawHtmlInput.value = ''
}

// -------- Source Editor Functions --------
function toggleSourceEditor() {
  sourceApplyMessage.value = ''
  if (!showSourceEditor.value) {
    // opening: load current sanitized content
    sourceHtml.value = currentMaterial.value.content || ''
  }
  showSourceEditor.value = !showSourceEditor.value
}

function openCreateModal() {
  // Prepare fresh material
  currentMaterial.value = {
    title: '',
    category: '',
    content: '',
    internalNotes: '',
    isActive: true,
    attachments: []
  }
  showCreateModal.value = true
  // If source editor currently visible from previous session, refresh it to blank
  if (showSourceEditor.value) {
    sourceHtml.value = ''
    sourceApplyMessage.value = ''
  }
}

function refreshSourceFromEditor() {
  sourceApplyMessage.value = ''
  sourceHtml.value = currentMaterial.value.content || ''
}

function applySourceHtml() {
  sourceApplyMessage.value = ''
  const html = sourceHtml.value
  if (!html.trim()) {
    sourceApplyMessage.value = 'Nothing to apply.'
    return
  }
  const sanitized = sanitizeHtml(html)
  const quill = editorRef.value?.getQuill?.()
  if (quill) {
    // Replace entire contents
    quill.setContents([])
    quill.clipboard.dangerouslyPasteHTML(0, sanitized, 'user')
    currentMaterial.value.content = quill.root.innerHTML
    sourceApplyMessage.value = 'Applied.'
    // subtle auto-hide after a delay could be added
  } else {
    sourceApplyMessage.value = 'Editor not ready.'
  }
}
</script>

<style scoped>
.admin-panel {
  height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 20px;
  overflow: hidden;
}

.filter-section {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(20px);
  border-radius: 20px;
  padding: 25px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 20px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
  flex-shrink: 0;
}

.search-container {
  position: relative;
  flex: 1;
  max-width: 400px;
}

.search-input {
  width: 100%;
  padding: 15px 50px 15px 20px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 25px;
  background: rgba(255, 255, 255, 0.9);
  font-size: 16px;
  outline: none;
  transition: all 0.3s ease;
}

.search-input:focus {
  border-color: #ff7b7b;
  box-shadow: 0 0 0 3px rgba(255, 123, 123, 0.2);
}

.search-icon {
  position: absolute;
  right: 15px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 18px;
  color: #666;
}

.filter-controls {
  display: flex;
  align-items: center;
  gap: 20px;
}

.category-select {
  padding: 12px 15px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 15px;
  background: rgba(255, 255, 255, 0.9);
  font-size: 14px;
  outline: none;
}

.status-toggle {
  display: flex;
  align-items: center;
  gap: 8px;
  color: white;
  font-weight: 600;
  cursor: pointer;
}

.status-toggle input[type="checkbox"] {
  width: 18px;
  height: 18px;
}

.btn-create {
  background: linear-gradient(135deg, #ff7b7b 0%, #ff9a56 100%);
  color: white;
  border: none;
  padding: 15px 25px;
  border-radius: 25px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s ease;
  box-shadow: 0 4px 15px rgba(255, 123, 123, 0.3);
  white-space: nowrap;
}

.btn-create:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(255, 123, 123, 0.4);
}

.materials-container {
  background: rgba(255, 255, 255, 0.05);
  backdrop-filter: blur(10px);
  border-radius: 20px;
  padding: 30px;
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.loading-state,
.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: white;
}

.loading-spinner,
.empty-icon {
  font-size: 4rem;
  margin-bottom: 20px;
}

.empty-state h3 {
  font-size: 1.5rem;
  margin-bottom: 10px;
}

.materials-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
  gap: 25px;
}

.material-card {
  background: rgba(255, 255, 255, 0.9);
  border-radius: 20px;
  overflow: hidden;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
  transition: all 0.3s ease;
}

.material-card:hover {
  transform: translateY(-5px);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.15);
}

.card-header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.material-title {
  margin: 0;
  font-size: 1.3rem;
  font-weight: 600;
}

.card-actions {
  display: flex;
  gap: 10px;
}

.btn-edit,
.btn-delete {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  padding: 8px 12px;
  border-radius: 10px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.3s ease;
}

.btn-edit:hover {
  background: rgba(255, 255, 255, 0.3);
}

.btn-delete:hover {
  background: rgba(255, 71, 87, 0.8);
}

.card-body {
  padding: 20px;
}

.material-category {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 15px;
}

.category-badge {
  background: #667eea;
  color: white;
  padding: 6px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 600;
}

.status-badge {
  padding: 4px 8px;
  border-radius: 10px;
  font-size: 12px;
  font-weight: 600;
}

.status-badge.active {
  background: rgba(34, 197, 94, 0.2);
  color: #059669;
}

.status-badge.inactive {
  background: rgba(239, 68, 68, 0.2);
  color: #dc2626;
}

.material-content {
  line-height: 1.6;
  margin-bottom: 15px;
  color: #333;
}

.material-meta {
  font-size: 12px;
  color: #666;
  margin-bottom: 10px;
}

.internal-notes {
  background: rgba(102, 126, 234, 0.1);
  padding: 10px;
  border-radius: 10px;
  font-size: 14px;
  color: #4c1d95;
}

/* Modal Styles */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(5px);
}

.modal-content {
  background: white;
  border-radius: 20px;
  width: 95%;
  max-width: 900px;
  height: 85vh;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  overflow: hidden;
}

.modal-header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 20px 25px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-radius: 20px 20px 0 0;
  flex-shrink: 0;
}

.modal-header h2 {
  margin: 0;
  font-size: 1.5rem;
  flex: 1;
}

/* Header actions container */
.header-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

.btn-maximize,
.btn-close {
  background: rgba(255, 255, 255, 0.1);
  border: none;
  color: white;
  font-size: 20px;
  cursor: pointer;
  padding: 8px;
  border-radius: 8px;
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s ease;
}

.btn-maximize:hover,
.btn-close:hover {
  background: rgba(255, 255, 255, 0.2);
}

.btn-close {
  font-size: 24px;
}

.modal-form {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.form-content {
  flex: 1;
  padding: 25px 30px;
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: #cbd5e0 #f7fafc;
}

.form-content::-webkit-scrollbar {
  width: 8px;
}

.form-content::-webkit-scrollbar-track {
  background: #f7fafc;
}

.form-content::-webkit-scrollbar-thumb {
  background-color: #cbd5e0;
  border-radius: 4px;
}

.form-content::-webkit-scrollbar-thumb:hover {
  background-color: #a0aec0;
}

.form-actions {
  padding: 20px 30px;
  border-top: 1px solid #e2e8f0;
  display: flex;
  justify-content: flex-end;
  gap: 15px;
  background: #f8fafc;
  flex-shrink: 0;
}

.form-group {
  margin-bottom: 25px;
}

.form-group label {
  display: block;
  margin-bottom: 8px;
  font-weight: 600;
  color: #333;
}

.form-input,
.form-textarea {
  width: 100%;
  padding: 12px 15px;
  border: 2px solid #e2e8f0;
  border-radius: 10px;
  font-size: 16px;
  outline: none;
  transition: all 0.3s ease;
}

.form-input:focus,
.form-textarea:focus {
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.form-textarea {
  resize: vertical;
  min-height: 100px;
}

.checkbox-label {
  display: flex !important;
  align-items: center;
  gap: 10px;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
}

.form-actions {
  display: flex;
  gap: 15px;
  justify-content: flex-end;
  margin-top: 30px;
}

.btn-cancel,
.btn-save {
  padding: 12px 25px;
  border: none;
  border-radius: 10px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-cancel {
  background: #e2e8f0;
  color: #4a5568;
}

.btn-cancel:hover {
  background: #cbd5e0;
}

.btn-save {
  background: linear-gradient(135deg, #ff7b7b 0%, #ff9a56 100%);
  color: white;
}

.btn-save:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 15px rgba(255, 123, 123, 0.3);
}

.btn-save:disabled {
  background: #cbd5e0;
  cursor: not-allowed;
  transform: none;
}

/* Content actions */
.content-label-row { display:flex; justify-content:space-between; align-items:center; gap:12px; }
.content-actions { display:flex; gap:8px; }
.btn-secondary { background:#e2e8f0; color:#374151; border:none; padding:6px 12px; border-radius:6px; cursor:pointer; font-size:12px; font-weight:600; transition:background .2s; }
.btn-secondary:hover { background:#cbd5e0; }
.paste-hint { font-size:12px; color:#6b7280; margin-top:6px; }

/* HTML Paste Modal */
.html-paste-modal-overlay { position:fixed; inset:0; background:rgba(0,0,0,0.55); display:flex; justify-content:center; align-items:center; z-index:1500; backdrop-filter:blur(4px); }
.html-paste-modal { background:#ffffff; width:min(900px,95%); max-height:85vh; border-radius:16px; display:flex; flex-direction:column; box-shadow:0 10px 40px rgba(0,0,0,0.25); }
.html-paste-header { padding:16px 20px; background:linear-gradient(135deg,#667eea 0%, #764ba2 100%); color:white; display:flex; justify-content:space-between; align-items:center; border-radius:16px 16px 0 0; }
.html-paste-body { padding:18px 22px; overflow-y:auto; }
.html-paste-body textarea { width:100%; min-height:300px; border:1px solid #d1d5db; border-radius:8px; padding:12px; font-family:ui-monospace, monospace; font-size:13px; line-height:1.4; resize:vertical; }
.html-paste-footer { padding:14px 20px; border-top:1px solid #e5e7eb; display:flex; gap:12px; justify-content:flex-end; background:#f8fafc; border-radius:0 0 16px 16px; }
.error-text { color:#dc2626; font-size:12px; margin-top:6px; }
.btn-primary { background:linear-gradient(135deg,#667eea 0%, #764ba2 100%); color:white; border:none; padding:8px 16px; border-radius:8px; cursor:pointer; font-size:14px; font-weight:600; }
.btn-primary:hover { opacity:.9; }
.btn-outline { background:white; color:#374151; border:1px solid #d1d5db; padding:8px 16px; border-radius:8px; cursor:pointer; font-size:14px; font-weight:600; }
.btn-outline:hover { background:#f3f4f6; }

/* Source HTML Editor */
.source-editor-wrapper { margin-top:14px; border:1px solid #d1d5db; border-radius:8px; background:#f9fafb; padding:10px 12px; display:flex; flex-direction:column; gap:8px; }
.source-editor-header { display:flex; justify-content:space-between; align-items:center; font-size:13px; font-weight:600; color:#374151; }
.source-editor-actions { display:flex; gap:6px; }
.btn-outline-sm { background:white; border:1px solid #d1d5db; padding:4px 10px; font-size:11px; border-radius:6px; cursor:pointer; font-weight:600; color:#374151; }
.btn-outline-sm:hover { background:#eef2f7; }
.source-textarea { width:100%; min-height:180px; font-family:ui-monospace, monospace; font-size:12px; line-height:1.4; border:1px solid #cbd5e0; border-radius:6px; padding:8px 10px; background:white; resize:vertical; }
.source-textarea:focus { outline:none; border-color:#667eea; box-shadow:0 0 0 2px rgba(102,126,234,0.25); }
.source-meta-row { display:flex; justify-content:space-between; font-size:11px; color:#6b7280; }
.apply-msg { color:#059669; }
.char-count { font-variant-numeric: tabular-nums; }

/* Responsive design */
@media (max-width: 768px) {
  .filter-section {
    flex-direction: column;
    gap: 15px;
  }

  .filter-controls {
    flex-wrap: wrap;
    gap: 15px;
  }

  .materials-grid {
    grid-template-columns: 1fr;
  }
}

/* Rich Text Editor Styles */
.rich-editor {
  border: 2px solid #e2e8f0;
  border-radius: 8px;
  overflow: hidden;
  background: white;
}

.rich-editor :deep(.ql-toolbar) {
  border: none;
  border-bottom: 1px solid #e2e8f0;
  background: #f8fafc;
  padding: 12px;
}

.rich-editor :deep(.ql-container) {
  border: none;
  font-size: 14px;
  min-height: 200px;
  font-family: inherit;
}

.rich-editor :deep(.ql-editor) {
  min-height: 200px;
  padding: 15px;
  line-height: 1.6;
}

/* Maximized editor takes full available space */
.modal-content.maximized .rich-editor :deep(.ql-editor) {
  min-height: calc(100vh - 320px);
  /* Adjust based on header, toolbar, and buttons */
}

/* Ensure editor container fills available space in maximized mode */
.modal-content.maximized .form-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.modal-content.maximized .rich-editor {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.modal-content.maximized .rich-editor :deep(.ql-container) {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.rich-editor :deep(.ql-editor.ql-blank::before) {
  color: #a0aec0;
  font-style: normal;
  font-size: 14px;
}

.form-actions {
  padding: 20px 30px;
  border-top: 1px solid #e2e8f0;
  display: flex;
  justify-content: flex-end;
  gap: 15px;
  background: #f8fafc;
  flex-shrink: 0;
}

.btn-cancel,
.btn-save {
  padding: 12px 24px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s ease;
  min-width: 100px;
}

.btn-cancel {
  background: #e2e8f0;
  color: #4a5568;
}

.btn-cancel:hover {
  background: #cbd5e0;
}

.btn-save {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
}

.btn-save:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.3);
}

.btn-save:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Maximized modal styles */
.modal-overlay.maximized {
  padding: 0;
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1001;
}

.modal-content.maximized {
  width: 100%;
  height: 100%;
  max-width: none;
  max-height: none;
  border-radius: 0;
  display: flex;
  flex-direction: column;
  margin: 0;
}

.modal-content.maximized .modal-form {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-height: 0;
}

.modal-content.maximized .rich-editor {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.modal-content.maximized .rich-editor :deep(.ql-container) {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.modal-content.maximized .rich-editor :deep(.ql-editor) {
  flex: 1;
  min-height: auto;
}

/* Form layout adjustments for maximized view */
.modal-content.maximized .form-group:has(.rich-editor) {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.modal-content.maximized .form-group:has(.rich-editor) label {
  flex-shrink: 0;
}

/* File Upload Styles */
.attachments-section {
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 16px;
  background-color: #f9fafb;
}

.existing-attachments {
  margin-bottom: 16px;
}

.existing-attachments h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: #374151;
}

.attachment-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  margin-bottom: 8px;
}

.attachment-info {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
}

.attachment-name {
  font-weight: 500;
  color: #374151;
}

.attachment-size {
  color: #6b7280;
  font-size: 12px;
}

.attachment-description {
  color: #6b7280;
  font-style: italic;
}

.btn-remove-attachment {
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.btn-remove-attachment:hover {
  background-color: #fee2e2;
}

.file-upload-section {
  margin-bottom: 16px;
}

.file-input {
  display: none;
}

.file-upload-label {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%);
  color: white;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.2s;
  border: none;
}

.file-upload-label:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.3);
}

.upload-icon {
  font-size: 16px;
}

.selected-files-preview {
  margin-top: 16px;
}

.selected-files-preview h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: #374151;
}

.selected-file-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  margin-bottom: 8px;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
}

.file-name {
  font-weight: 500;
  color: #374151;
}

.file-size {
  color: #6b7280;
  font-size: 12px;
}

.file-description {
  flex: 1;
}

.description-input {
  width: 100%;
  padding: 6px 8px;
  border: 1px solid #d1d5db;
  border-radius: 4px;
  font-size: 12px;
}

.btn-remove-file {
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.btn-remove-file:hover {
  background-color: #fee2e2;
}

/* Attachment info in cards */
.attachments-info {
  margin-top: 12px;
  padding: 8px 12px;
  background-color: #f3f4f6;
  border-radius: 6px;
  font-size: 13px;
}

.attachment-count {
  color: #6b7280;
  margin-left: 8px;
}

.attachment-list {
  margin-top: 6px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.attachment-item-card {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 6px;
  background-color: #e5e7eb;
  border-radius: 4px;
  font-size: 11px;
  color: #374151;
}

/* New enhanced attachment display styles */
.attachments-section-card {
  margin-top: 16px;
  padding: 12px;
  background-color: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  border-left: 4px solid #3b82f6;
}

.attachments-header {
  margin-bottom: 8px;
  font-size: 14px;
  color: #1f2937;
  font-weight: 600;
}

.attachment-list-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.attachment-item-display {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.attachment-item-display:hover {
  border-color: #3b82f6;
  box-shadow: 0 2px 4px rgba(59, 130, 246, 0.1);
}

.attachment-info-display {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
}

.attachment-icon {
  font-size: 16px;
  opacity: 0.7;
}

.attachment-details {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.attachment-name-display {
  font-weight: 500;
  color: #374151;
  font-size: 13px;
}

.attachment-size-display {
  color: #6b7280;
  font-size: 11px;
}

.download-btn-card {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  background: linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%);
  color: white;
  text-decoration: none;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.download-btn-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.3);
  color: white;
}

.attachment-name {
  display: inline-block;
}

.download-link {
  text-decoration: none;
  font-size: 12px;
  opacity: 0.7;
  transition: opacity 0.2s;
}

.download-link:hover {
  opacity: 1;
}

.download-link-small {
  text-decoration: none;
  font-size: 12px;
  margin-left: 8px;
  opacity: 0.7;
  transition: opacity 0.2s;
}

.download-link-small:hover {
  opacity: 1;
}

/* Admin Toast Notifications */
.admin-toast {
  position: fixed;
  top: 20px;
  right: 20px;
  padding: 12px 20px;
  border-radius: 8px;
  color: white;
  font-weight: 500;
  z-index: 10000;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  backdrop-filter: blur(10px);
  animation: slideInAdmin 0.3s ease-out;
  max-width: 400px;
}

.toast-success {
  background: linear-gradient(135deg, #28a745 0%, #1e7e34 100%);
}

.toast-error {
  background: linear-gradient(135deg, #dc3545 0%, #c82333 100%);
}

.toast-warning {
  background: linear-gradient(135deg, #ffc107 0%, #e0a800 100%);
  color: #212529;
}

.toast-info {
  background: linear-gradient(135deg, #17a2b8 0%, #138496 100%);
}

@keyframes slideInAdmin {
  from {
    transform: translateX(100%);
    opacity: 0;
  }
  to {
    transform: translateX(0);
    opacity: 1;
  }
}
</style>