<template>
  <div class="admin-panel">
    <div class="admin-header">
      <div class="header-content">
        <div class="admin-icon">⚙️</div>
        <div class="header-text">
          <h1>OnboardingBuddy Admin</h1>
          <p>Manage training materials and content</p>
        </div>
      </div>
      <button @click="showCreateModal = true" class="btn-create">
        ➕ Add New Material
      </button>
    </div>

    <!-- Filter Bar -->
    <div class="filter-section">
      <div class="search-container">
        <input 
          v-model="searchTerm" 
          type="text" 
          placeholder="Search materials..." 
          class="search-input"
        />
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
          </div>
        </div>
      </div>
    </div>

    <!-- Create/Edit Modal -->
    <div v-if="showCreateModal || showEditModal" class="modal-overlay" @click="closeModal">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h2>{{ showEditModal ? '✏️ Edit Material' : '➕ Create New Material' }}</h2>
          <button @click="closeModal" class="btn-close">×</button>
        </div>
        
        <form @submit.prevent="saveMaterial" class="modal-form">
          <div class="form-group">
            <label for="title">Title *</label>
            <input
              id="title"
              v-model="currentMaterial.title"
              type="text"
              required
              maxlength="200"
              placeholder="Enter material title"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label for="category">Category *</label>
            <input
              id="category"
              v-model="currentMaterial.category"
              type="text"
              required
              maxlength="100"
              placeholder="e.g., Training Materials, System Prompts"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label for="content">Content</label>
            <textarea
              id="content"
              v-model="currentMaterial.content"
              rows="10"
              placeholder="Enter the material content (HTML supported)"
              class="form-textarea"
            ></textarea>
          </div>

          <div class="form-group">
            <label for="internalNotes">Internal Notes</label>
            <textarea
              id="internalNotes"
              v-model="currentMaterial.internalNotes"
              rows="3"
              placeholder="Internal notes (not visible to AI)"
              class="form-textarea"
            ></textarea>
          </div>

          <div class="form-group">
            <label class="checkbox-label">
              <input
                type="checkbox"
                v-model="currentMaterial.isActive"
              />
              <span>Active (visible to AI)</span>
            </label>
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
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'

// Reactive data
const materials = ref([])
const searchTerm = ref('')
const selectedCategory = ref('')
const showActiveOnly = ref(false)
const loading = ref(false)
const saving = ref(false)
const showCreateModal = ref(false)
const showEditModal = ref(false)

const currentMaterial = ref({
  title: '',
  category: '',
  content: '',
  internalNotes: '',
  isActive: true
})

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
  loadMaterials()
})

async function loadMaterials() {
  loading.value = true
  try {
    const response = await fetch('/api/trainingmaterials')
    if (response.ok) {
      materials.value = await response.json()
    }
  } catch (error) {
    console.error('Error loading materials:', error)
  }
  loading.value = false
}

function editMaterial(material) {
  currentMaterial.value = { ...material }
  showEditModal.value = true
}

async function deleteMaterial(id) {
  if (!confirm('Are you sure you want to delete this material?')) return

  try {
    const response = await fetch(`/api/trainingmaterials/${id}`, {
      method: 'DELETE'
    })
    
    if (response.ok) {
      materials.value = materials.value.filter(m => m.id !== id)
    }
  } catch (error) {
    console.error('Error deleting material:', error)
  }
}

async function saveMaterial() {
  saving.value = true
  
  try {
    const url = showEditModal.value 
      ? `/api/trainingmaterials/${currentMaterial.value.id}`
      : '/api/trainingmaterials'
    
    const method = showEditModal.value ? 'PUT' : 'POST'
    
    const response = await fetch(url, {
      method,
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(currentMaterial.value)
    })
    
    if (response.ok) {
      const savedMaterial = await response.json()
      
      if (showEditModal.value) {
        const index = materials.value.findIndex(m => m.id === savedMaterial.id)
        if (index !== -1) {
          materials.value[index] = savedMaterial
        }
      } else {
        materials.value.push(savedMaterial)
      }
      
      closeModal()
    }
  } catch (error) {
    console.error('Error saving material:', error)
  }
  
  saving.value = false
}

function closeModal() {
  showCreateModal.value = false
  showEditModal.value = false
  currentMaterial.value = {
    title: '',
    category: '',
    content: '',
    internalNotes: '',
    isActive: true
  }
}

function truncateContent(content, maxLength = 200) {
  if (!content) return ''
  const stripped = content.replace(/<[^>]*>/g, '')
  return stripped.length > maxLength ? stripped.substring(0, maxLength) + '...' : stripped
}

function formatDate(dateString) {
  return new Date(dateString).toLocaleDateString()
}
</script>

<style scoped>
.admin-panel {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 20px;
}

.admin-header {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(20px);
  border-radius: 20px;
  padding: 30px;
  margin-bottom: 30px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
}

.header-content {
  display: flex;
  align-items: center;
  gap: 20px;
}

.admin-icon {
  font-size: 3rem;
  animation: rotate 3s linear infinite;
}

@keyframes rotate {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.header-text h1 {
  margin: 0;
  color: white;
  font-size: 2.5rem;
  font-weight: 700;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
}

.header-text p {
  margin: 5px 0 0;
  color: rgba(255, 255, 255, 0.9);
  font-size: 1.2rem;
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
}

.btn-create:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(255, 123, 123, 0.4);
}

.filter-section {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(20px);
  border-radius: 20px;
  padding: 25px;
  margin-bottom: 30px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 20px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
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

.materials-container {
  background: rgba(255, 255, 255, 0.05);
  backdrop-filter: blur(10px);
  border-radius: 20px;
  padding: 30px;
  min-height: 400px;
}

.loading-state, .empty-state {
  text-align: center;
  padding: 60px 20px;
  color: white;
}

.loading-spinner, .empty-icon {
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

.btn-edit, .btn-delete {
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
  width: 90%;
  max-width: 600px;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.modal-header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 25px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-radius: 20px 20px 0 0;
}

.modal-header h2 {
  margin: 0;
  font-size: 1.5rem;
}

.btn-close {
  background: none;
  border: none;
  color: white;
  font-size: 24px;
  cursor: pointer;
  padding: 5px;
  border-radius: 50%;
  width: 35px;
  height: 35px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-close:hover {
  background: rgba(255, 255, 255, 0.2);
}

.modal-form {
  padding: 30px;
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

.form-input, .form-textarea {
  width: 100%;
  padding: 12px 15px;
  border: 2px solid #e2e8f0;
  border-radius: 10px;
  font-size: 16px;
  outline: none;
  transition: all 0.3s ease;
}

.form-input:focus, .form-textarea:focus {
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

.btn-cancel, .btn-save {
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
</style>