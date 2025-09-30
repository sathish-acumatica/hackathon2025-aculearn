// Utility functions for dynamic path configuration
// This works in any subdirectory deployment

// Utility functions for dynamic path configuration
// This works in any subdirectory deployment and development

export function getBasePath() {
  // In development mode, use empty string to leverage Vite proxy
  if (import.meta.env.DEV) {
    return ''
  }
  
  // In production, check if we're in a virtual application
  const path = window.location.pathname
  
  // If we're at the root, return empty string
  if (path === '/' || path === '') {
    return ''
  }
  
  // For paths like /OnboardingBuddy/admin or /SomeApp/, extract the base
  const segments = path.split('/').filter(segment => segment !== '')
  
  if (segments.length === 0) {
    return ''
  }
  
  // Check if this might be a virtual application
  // If the first segment doesn't look like a page route, treat it as the app base
  const firstSegment = segments[0]
  const commonRoutes = ['admin', 'chat', 'login', 'dashboard', 'api', 'assets']
  
  if (commonRoutes.includes(firstSegment.toLowerCase())) {
    // First segment is a route, we're probably at root
    return ''
  }
  
  // For virtual applications, return the base path with leading slash but no trailing slash for API calls
  return `/${firstSegment}`
}

export function getRouterBasePath() {
  // In development mode, use '/' since we're not in a subdirectory
  if (import.meta.env.DEV) {
    return '/'
  }
  
  const path = window.location.pathname
  
  // If we're at the root, return '/'
  if (path === '/' || path === '') {
    return '/'
  }
  
  // For paths like /OnboardingBuddy/ or /SomeApp/, extract the base
  const segments = path.split('/').filter(segment => segment !== '')
  
  if (segments.length === 0) {
    return '/'
  }
  
  // Check if this might be a virtual application
  const firstSegment = segments[0]
  const commonRoutes = ['admin', 'chat', 'login', 'dashboard', 'api', 'assets']
  
  if (commonRoutes.includes(firstSegment.toLowerCase())) {
    // First segment is a route, we're probably at root
    return '/'
  }
  
  // Return the base path with leading and trailing slashes for router
  return `/${firstSegment}/`
}

export function buildApiUrl(endpoint) {
  const basePath = getBasePath()
  // Remove leading slash from endpoint if present
  const cleanEndpoint = endpoint.startsWith('/') ? endpoint.substring(1) : endpoint
  return basePath ? `${basePath}/${cleanEndpoint}` : `/${cleanEndpoint}`
}

export function buildSignalRUrl(hubName) {
  const basePath = getBasePath()
  // Remove leading slash from hubName if present
  const cleanHubName = hubName.startsWith('/') ? hubName.substring(1) : hubName
  return basePath ? `${basePath}/${cleanHubName}` : `/${cleanHubName}`
}

// Debug function to help understand current deployment
export function debugPaths() {
  console.log('=== Path Debug Info ===')
  console.log('Environment:', import.meta.env.DEV ? 'Development' : 'Production')
  console.log('Current URL:', window.location.href)
  console.log('Host:', window.location.host)
  console.log('Pathname:', window.location.pathname)
  console.log('Path Segments:', window.location.pathname.split('/').filter(s => s !== ''))
  console.log('Base Path:', getBasePath())
  console.log('Router Base Path:', getRouterBasePath())
  console.log('Example API URL:', buildApiUrl('api/chat/send'))
  console.log('Example SignalR URL:', buildSignalRUrl('chatHub'))
  console.log('======================')
}

// Auto-debug on load in production to help troubleshoot IP access issues
if (!import.meta.env.DEV && typeof window !== 'undefined') {
  window.addEventListener('load', () => {
    debugPaths()
  })
}