import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // @venue/core es un paquete `file:` con su propia copia de react como peer — sin dedupe, los
  // .tsx del core importarían esa segunda copia y habría doble React en el bundle (hooks rotos
  // en runtime). Dedupe fuerza una sola instancia, la de esta app.
  resolve: {
    dedupe: ['react', 'react-dom'],
  },
  base: '/venue-ops/',
  server: {
    port: 5175,
    proxy: {
      '/api/access-control': { target: 'http://localhost:5102', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/access-control/, '') },
      '/api/checkin': { target: 'http://localhost:5103', changeOrigin: true, rewrite: (p) => p.replace(/^\/api\/checkin/, '') },
      '/hubs/occupancy': { target: 'http://localhost:5102', ws: true, changeOrigin: true },
      '/hubs/tickets': { target: 'http://localhost:5103', ws: true, changeOrigin: true },
    },
  },
})
