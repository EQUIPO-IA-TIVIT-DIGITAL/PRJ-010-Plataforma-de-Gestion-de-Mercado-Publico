import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // E10: vitest para Test Gate por capa frontend (frameworks unit-testing mandatory)
  // vitest usa mismo vite, no necesita config separada
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5001',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    chunkSizeWarningLimit: 700,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom', 'react-router-dom'],
          antd: ['antd', '@ant-design/icons', '@ant-design/x'],
          query: ['@tanstack/react-query'],
          signalr: ['@microsoft/signalr'],
          pdf: ['jspdf', 'jspdf-autotable', 'html2canvas'],
          markdown: ['react-markdown'],
        },
      },
    },
  },
});
