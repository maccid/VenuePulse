import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  base: '/venue-ops/',
  server: {
    port: 5175,
    proxy: {
      '/api/access-control': { target: 'http://localhost:5102', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/access-control/, '/api') },
      '/api/checkin': { target: 'http://localhost:5103', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/checkin/, '/api') },
      '/hubs/occupancy': { target: 'http://localhost:5102', ws: true, changeOrigin: true },
      '/hubs/tickets': { target: 'http://localhost:5103', ws: true, changeOrigin: true },
    },
  },
})
