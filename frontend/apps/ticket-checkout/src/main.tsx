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

// Modo standalone para desarrollo aislado (`npm run dev`, sin depender de shell): si existe
// #root en el HTML, monta una instancia de ejemplo directamente.
const standaloneRoot = document.getElementById('root')
if (standaloneRoot) {
  standaloneRoot.innerHTML = ''
  const element = document.createElement('ticket-checkout-app')
  element.setAttribute('event-id', new URLSearchParams(location.search).get('eventId') ?? '')
  element.setAttribute('holder', 'Ana (dev)')
  standaloneRoot.appendChild(element)
}
