import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  base: './', // Ensure all paths are relative
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
    outDir: 'dist',
    assetsDir: 'assets',
    rollupOptions: {
      output: {
        // Ensure all asset paths are relative
        assetFileNames: 'assets/[name]-[hash].[ext]',
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js'
      }
    },
    // Additional settings to ensure relative paths
    assetsInlineLimit: 0 // Don't inline any assets as data URLs
  },
  experimental: {
    // Ensure proper relative path handling
    renderBuiltUrl(filename, { hostType }) {
      // Always return relative paths
      return filename
    }
  }
})