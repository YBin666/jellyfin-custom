import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const JELLYFIN_URL = process.env.JELLYFIN_URL || 'http://localhost:8096';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    open: '/?dev=1',
    proxy: {
      '/ShortVideo': { target: JELLYFIN_URL, changeOrigin: true },
      '/Diy': { target: JELLYFIN_URL, changeOrigin: true },
      '/Users': { target: JELLYFIN_URL, changeOrigin: true },
      '/Videos': { target: JELLYFIN_URL, changeOrigin: true },
      '/Items': { target: JELLYFIN_URL, changeOrigin: true },
      '/web': { target: JELLYFIN_URL, changeOrigin: true }
    }
  },
  build: {
    outDir: 'dist',
    assetsDir: '.',
    rollupOptions: {
      input: {
        main: './src/main-prod.jsx'
      },
      output: {
        entryFileNames: 'inject.js',
        assetFileNames: '[name].[ext]',
        format: 'iife'
      }
    },
    sourcemap: false,
    minify: true
  }
});
