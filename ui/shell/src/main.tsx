import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

const remoteScript = document.createElement('script')
remoteScript.type = 'module'
remoteScript.src = '/ticket-checkout/ticket-checkout.js'
document.head.appendChild(remoteScript)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
