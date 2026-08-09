import { StrictMode } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import App from './App.tsx'

/**
 * Micro frontend montado como Web Component en light DOM (sin Shadow DOM, por simplicidad —
 * ver ADR 0004: un único convenio de estilos compartido entre apps en vez de aislar CSS).
 * `shell` lo consume como <ticket-checkout-app event-id="..." holder="..." />.
 */
// El build (`vite.config.ts`, cssCodeSplit: false) emite el CSS como fichero hermano de
// ticket-checkout.js, no lo inyecta por JS. La propia página de ticket-checkout (modo standalone)
// lo enlaza sola vía su index.html, pero cuando este módulo se carga como <script> remoto dentro
// de shell (el flujo real de compra) nadie más lo hace — resultado: el componente pierde toda su
// hoja de estilos (bordes, tokens --tc-*, colores del SVG del plano, que caía a negro) y solo
// "se veía bien" antes por casualidad, heredando los estilos globales de <button>/<input> de shell.
const STYLESHEET_HREF = new URL('./ticket-checkout.css', import.meta.url).href

function ensureStylesheetLoaded() {
  if (document.querySelector(`link[href="${STYLESHEET_HREF}"]`)) return
  const link = document.createElement('link')
  link.rel = 'stylesheet'
  link.href = STYLESHEET_HREF
  document.head.appendChild(link)
}

class TicketCheckoutElement extends HTMLElement {
  static observedAttributes = ['event-id', 'holder']

  private root: Root | null = null

  connectedCallback() {
    ensureStylesheetLoaded()
    this.root = createRoot(this)
    this.render()
  }

  disconnectedCallback() {
    this.root?.unmount()
    this.root = null
  }

  attributeChangedCallback() {
    if (this.root) this.render()
  }

  private render() {
    const eventId = this.getAttribute('event-id')
    const holder = this.getAttribute('holder') ?? 'Invitado'
    if (!eventId || !this.root) return

    this.root.render(
      <StrictMode>
        <App eventId={eventId} holder={holder} />
      </StrictMode>,
    )
  }
}

customElements.define('ticket-checkout-app', TicketCheckoutElement)

// Modo standalone para desarrollo aislado (`npm run dev`, sin depender de shell): usa un id
// propio (NUNCA "root") para no colisionar con el #root de `shell` cuando este bundle se carga
// como script remoto dentro de otra app — si usara "root", este bloque vaciaría y sustituiría
// el DOM ya renderizado de `shell` por un <ticket-checkout-app> vacío (bug real detectado en
// pruebas manuales: pantalla en blanco con event-id="" tras cargar el catálogo).
const standaloneRoot = document.getElementById('ticket-checkout-standalone-root')
if (standaloneRoot) {
  standaloneRoot.innerHTML = ''
  const element = document.createElement('ticket-checkout-app')
  element.setAttribute('event-id', new URLSearchParams(location.search).get('eventId') ?? '')
  element.setAttribute('holder', 'Ana (dev)')
  standaloneRoot.appendChild(element)
}
