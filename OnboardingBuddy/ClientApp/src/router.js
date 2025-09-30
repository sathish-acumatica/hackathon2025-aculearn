import { createRouter, createWebHistory } from 'vue-router'
import ChatInterface from './components/ChatInterface.vue'
import AdminPanel from './views/AdminPanel.vue'
import { getRouterBasePath } from './utils/pathUtils.js'

const routes = [
  {
    path: '/',
    name: 'Home',
    component: ChatInterface
  },
  {
    path: '/admin',
    name: 'Admin',
    component: AdminPanel
  }
]

const router = createRouter({
  history: createWebHistory(getRouterBasePath()),
  routes
})

export default router