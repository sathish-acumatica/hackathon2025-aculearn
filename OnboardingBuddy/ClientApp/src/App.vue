<template>
  <div id="app">
    <nav class="app-nav">
      <div class="nav-brand" @click="handleBrandClick">
        <div class="brand-icon">
          <img src="/onboarding-buddy-icon.svg" alt="AcuBuddy" />
        </div>
        <div class="brand-text">
          <h1>AcuBuddy</h1>
          <p>Your AI-powered onboarding companion</p>
        </div>
      </div>
      <div class="nav-links">
        <router-link to="/" class="nav-link" :class="{ active: $route.path === '/' }">
          💬 Chat
        </router-link>
        <router-link to="/admin" class="nav-link" :class="{ active: $route.path === '/admin' }">
          ⚙️ Admin
        </router-link>
      </div>
    </nav>
    <main class="app-main">
      <router-view />
    </main>
  </div>
</template>

<script setup>
// Main App component

// Handler for clicking the brand/logo to reload page and start new session
function handleBrandClick() {
  // Clear any session storage to ensure a fresh session
  try {
    sessionStorage.clear()
    console.log('Cleared session storage for new session')
  } catch (error) {
    console.warn('Could not clear session storage:', error)
  }
  
  // Force reload the page to start a completely new session
  window.location.reload()
}
</script>

<style>
#app {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  height: 100vh;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  padding: 0;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.app-nav {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-bottom: 2px solid rgba(255, 255, 255, 0.2);
  padding: 0 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  min-height: 80px;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.2);
}

.nav-brand {
  display: flex;
  align-items: center;
  gap: 15px;
  cursor: pointer;
  transition: all 0.3s ease;
  padding: 8px 12px;
  border-radius: 12px;
}

.nav-brand:hover {
  background: rgba(255, 255, 255, 0.1);
  transform: translateY(-1px);
}

.brand-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  animation: bounce 2s infinite;
}

.brand-icon img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

@keyframes bounce {
  0%, 20%, 50%, 80%, 100% { transform: translateY(0); }
  40% { transform: translateY(-8px); }
  60% { transform: translateY(-4px); }
}

.brand-text h1 {
  margin: 0;
  font-size: 28px;
  color: white;
  font-weight: 700;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  line-height: 1.2;
}

.brand-text p {
  margin: 2px 0 0;
  font-size: 14px;
  color: rgba(255, 255, 255, 0.9);
  font-weight: 500;
  line-height: 1.3;
}

.nav-links {
  display: flex;
  gap: 15px;
}

.nav-link {
  text-decoration: none;
  color: rgba(255, 255, 255, 0.9);
  padding: 12px 20px;
  border-radius: 25px;
  font-weight: 600;
  font-size: 16px;
  transition: all 0.3s ease;
  border: 2px solid transparent;
  backdrop-filter: blur(10px);
  white-space: nowrap;
}

.nav-link:hover {
  background: rgba(255, 255, 255, 0.2);
  color: white;
  border-color: rgba(255, 255, 255, 0.3);
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

.nav-link.active {
  background: rgba(255, 255, 255, 0.9);
  color: #667eea;
  border-color: white;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.3);
}

.app-main {
  flex: 1;
  overflow: hidden;
  background: rgba(255, 255, 255, 0.05);
  backdrop-filter: blur(10px);
  min-height: 0;
}

/* Responsive design */
@media (max-width: 768px) {
  .app-nav {
    padding: 0 15px;
    min-height: 70px;
  }
  
  .brand-icon {
    font-size: 2.5rem;
  }
  
  .brand-text h1 {
    font-size: 22px;
  }
  
  .brand-text p {
    font-size: 12px;
  }
  
  .nav-links {
    gap: 10px;
  }
  
  .nav-link {
    padding: 10px 16px;
    font-size: 14px;
  }
}

@media (max-width: 480px) {
  .app-nav {
    flex-direction: column;
    padding: 15px;
    min-height: auto;
    gap: 15px;
  }
  
  .nav-brand {
    gap: 10px;
  }
  
  .brand-text {
    text-align: center;
  }
}
</style>