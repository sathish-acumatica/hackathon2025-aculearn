import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Plugin to inject base tag for virtual application deployment (production only)
function injectBaseTagForProduction() {
  return {
    name: 'inject-base-tag-production',
    transformIndexHtml: {
      order: 'pre',
      handler(html, ctx) {
        // Only inject in production builds for virtual application support
        if (ctx.server) {
          // Development mode - no injection needed
          return html
        }
        
        // Production mode - inject dynamic base tag script
        const baseScript = `
<script>
  (function() {
    // Get the current path and determine the base for the virtual application
    const path = window.location.pathname;
    let basePath = './';
    
    // If we're not at the root, extract the virtual application base
    if (path !== '/' && path !== '') {
      const segments = path.split('/').filter(segment => segment !== '');
      if (segments.length > 0) {
        // Check if first segment looks like a virtual app (not a common route)
        const firstSegment = segments[0];
        const commonRoutes = ['admin', 'chat', 'login', 'dashboard', 'api', 'assets'];
        
        if (!commonRoutes.includes(firstSegment.toLowerCase())) {
          // This is likely a virtual application
          basePath = '/' + firstSegment + '/';
        }
      }
    }
    
    // Create and inject the base tag
    const baseTag = document.createElement('base');
    baseTag.href = basePath;
    document.head.insertBefore(baseTag, document.head.firstChild);
    
    // Debug info
    console.log('Virtual App Detection:', {
      currentPath: path,
      basePath: basePath,
      url: window.location.href
    });
  })();
</script>`
        
        // Inject the script right after the opening <head> tag
        return html.replace('<head>', '<head>' + baseScript)
      }
    }
  }
}

export default defineConfig({
  plugins: [vue(), injectBaseTagForProduction()],
  base: './', // Use relative paths for assets
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        secure: false
      },
      '/chatHub': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        secure: false,
        ws: true
      }
    }
  },
  build: {
    outDir: '../wwwroot',
    assetsDir: 'assets',
    emptyOutDir: true, // Clean the output directory before build
    rollupOptions: {
      output: {
        // Asset file naming
        assetFileNames: 'assets/[name]-[hash].[ext]',
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js'
      }
    },
    // Additional settings
    assetsInlineLimit: 0 // Don't inline any assets as data URLs
  }
})