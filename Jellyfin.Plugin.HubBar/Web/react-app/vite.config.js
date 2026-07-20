import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: './dist',
    assetsDir: 'assets',
    rollupOptions: {
      output: {
        entryFileNames: 'main-[hash].js',
        chunkFileNames: 'chunk-[hash].js',
        assetFileNames: 'asset-[hash].[ext]'
      }
    }
  },
  server: {
    port: 5174,
    proxy: {
      '/HubBar': {
        target: 'http://localhost:8096',
        changeOrigin: true
      },
      '/ShortVideo': {
        target: 'http://localhost:8096',
        changeOrigin: true
      },
      '/Users': {
        target: 'http://localhost:8096',
        changeOrigin: true
      },
      '/Items': {
        target: 'http://localhost:8096',
        changeOrigin: true
      },
      '/Videos': {
        target: 'http://localhost:8096',
        changeOrigin: true
      }
    }
  }
})