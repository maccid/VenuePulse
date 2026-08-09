import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// En producción, Nginx hace de reverse proxy (ver frontend/nginx/nginx.conf).
// En desarrollo (`npm run dev`), este proxy apunta directo a los servicios en localhost
// para poder trabajar en la app sin levantar todo docker-compose.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api/ticketing': { target: 'http://localhost:5101', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/ticketing/, '/api') },
      '/api/access-control': { target: 'http://localhost:5102', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/access-control/, '/api') },
      '/api/checkin': { target: 'http://localhost:5103', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/checkin/, '/api') },
      '/hubs/occupancy': { target: 'http://localhost:5102', ws: true, changeOrigin: true },
      '/hubs/tickets': { target: 'http://localhost:5103', ws: true, changeOrigin: true },
    },
  },
})
