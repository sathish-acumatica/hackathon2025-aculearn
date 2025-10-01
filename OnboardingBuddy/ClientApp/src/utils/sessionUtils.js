// Session management utilities for persistent browser sessions
// Ensures sessions persist across page navigation and component remounts

const SESSION_STORAGE_KEY = 'onboarding_buddy_session_id'
const SESSION_TIMEOUT_MINUTES = 60

// Generate a unique session ID
function generateSessionId() {
  return 'session_' + Math.random().toString(36).substr(2, 9) + '_' + Date.now()
}

// Get or create a persistent session ID for this browser
export function getBrowserSessionId() {
  try {
    // First check if we have an existing session
    const stored = sessionStorage.getItem(SESSION_STORAGE_KEY)
    if (stored) {
      const sessionData = JSON.parse(stored)
      const ageMinutes = (Date.now() - sessionData.timestamp) / (1000 * 60)
      
      // If session is still valid (within timeout), use it
      if (ageMinutes < SESSION_TIMEOUT_MINUTES) {
        console.log('Using existing browser session:', sessionData.sessionId, `(age: ${ageMinutes.toFixed(1)} min)`)
        return sessionData.sessionId
      } else {
        console.log('Session expired, creating new one')
      }
    }
    
    // Create a new session if no valid existing session
    const newSessionId = generateSessionId()
    const sessionData = {
      sessionId: newSessionId,
      timestamp: Date.now(),
      created: new Date().toISOString()
    }
    
    sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(sessionData))
    console.log('Created new browser session:', newSessionId)
    return newSessionId
    
  } catch (error) {
    console.warn('Session storage not available, using temporary session:', error)
    // Fallback for browsers without session storage
    return generateSessionId()
  }
}

// Clear the current session (for logout, etc.)
export function clearBrowserSession() {
  try {
    sessionStorage.removeItem(SESSION_STORAGE_KEY)
    console.log('Browser session cleared')
  } catch (error) {
    console.warn('Could not clear session storage:', error)
  }
}

// Force create a new session (for explicit session reset)
export function createNewBrowserSession() {
  try {
    const newSessionId = generateSessionId()
    const sessionData = {
      sessionId: newSessionId,
      timestamp: Date.now(),
      created: new Date().toISOString()
    }
    
    sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(sessionData))
    console.log('Force created new browser session:', newSessionId)
    return newSessionId
    
  } catch (error) {
    console.warn('Session storage not available, using temporary session:', error)
    return generateSessionId()
  }
}

// Get session info for debugging
export function getSessionInfo() {
  try {
    const stored = sessionStorage.getItem(SESSION_STORAGE_KEY)
    if (stored) {
      const sessionData = JSON.parse(stored)
      const ageMinutes = (Date.now() - sessionData.timestamp) / (1000 * 60)
      return {
        ...sessionData,
        ageMinutes: Math.round(ageMinutes * 100) / 100,
        isValid: ageMinutes < SESSION_TIMEOUT_MINUTES
      }
    }
    return null
  } catch (error) {
    console.warn('Could not get session info:', error)
    return null
  }
}

// Keep session alive (call this on user activity)
export function keepSessionAlive() {
  try {
    const stored = sessionStorage.getItem(SESSION_STORAGE_KEY)
    if (stored) {
      const sessionData = JSON.parse(stored)
      sessionData.timestamp = Date.now()
      sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(sessionData))
    }
  } catch (error) {
    console.warn('Could not update session timestamp:', error)
  }
}