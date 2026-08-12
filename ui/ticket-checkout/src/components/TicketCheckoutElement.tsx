import { StrictMode } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import App from '../App.tsx'
import { ensureStylesheetLoaded } from '../shared/ensureStylesheetLoaded'

export class TicketCheckoutElement extends HTMLElement {
  static observedAttributes = ['event-id', 'holder']

  private root: Root | null = null

  async connectedCallback() {
    await ensureStylesheetLoaded()
    if (this.root) return
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
