import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// base '/ticket-checkout/' porque Nginx sirve este build bajo esa ruta (ver
// frontend/nginx/nginx.conf). Nombres de fichero SIN hash a propósito: shell necesita
// referenciar "ticket-checkout.js" por una ruta fija, no puede conocer un hash generado en
// build time de un despliegue independiente (ver ADR 0004).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  base: '/ticket-checkout/',
  build: {
    cssCodeSplit: false,
    rollupOptions: {
      output: {
        entryFileNames: 'ticket-checkout.js',
        chunkFileNames: 'ticket-checkout-[name].js',
        assetFileNames: 'ticket-checkout[extname]',
      },
    },
  },
  server: {
    port: 5174,
    proxy: {
      '/api/ticketing': { target: 'http://localhost:5101', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/ticketing/, '') },
      '/api/access-control': { target: 'http://localhost:5102', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/access-control/, '') },
      '/api/checkin': { target: 'http://localhost:5103', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/checkin/, '') },
      '/hubs/tickets': { target: 'http://localhost:5103', ws: true, changeOrigin: true },
      '/hubs/occupancy': { target: 'http://localhost:5102', ws: true, changeOrigin: true },
      '/hubs/pricing': { target: 'http://localhost:5101', ws: true, changeOrigin: true },
    },
  },
})
