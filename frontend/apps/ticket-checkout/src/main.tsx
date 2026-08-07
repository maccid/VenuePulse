import { StrictMode } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import App from './App.tsx'

/**
 * Micro frontend montado como Web Component en light DOM (sin Shadow DOM, por simplicidad —
 * ver ADR 0004: un único convenio de estilos compartido entre apps en vez de aislar CSS).
 * `shell` lo consume como <ticket-checkout-app event-id="..." holder="..." />.
 */
class TicketCheckoutElement extends HTMLElement {
  static observedAttributes = ['event-id', 'holder']

  private root: Root | null = null

  connectedCallback() {
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
