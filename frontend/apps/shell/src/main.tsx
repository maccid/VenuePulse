import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

// Se inyecta en tiempo de ejecución (no declarado en index.html) para que Vite no intente
// resolverlo como dependencia local del proyecto: es el bundle de OTRO despliegue
// independiente, servido en /ticket-checkout/ por Nginx (ver ADR 0004).
const remoteScript = document.createElement('script')
remoteScript.type = 'module'
remoteScript.src = '/ticket-checkout/ticket-checkout.js'
document.head.appendChild(remoteScript)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
