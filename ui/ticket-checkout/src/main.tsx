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

// Devuelve una promesa que se resuelve cuando el CSS ya está aplicado (o falló al cargar, para
// no bloquear el render eternamente). Evita el flash de estilos sin aplicar (FOUC) que pintaba
// los SVGs del plano/icono a su tamaño por defecto (300×150) hasta que la hoja llegaba: el icono
// del mapa "salía gigante" un instante. Antes solo se enlazaba el <link> y se renderizaba al
// momento, sin esperar a que el navegador hubiera descargado y aplicado la hoja.
function ensureStylesheetLoaded(): Promise<void> {
  const existing = document.querySelector<HTMLLinkElement>(`link[href="${STYLESHEET_HREF}"]`)
  if (existing) {
    return new Promise((resolve) => {
      if (existing.sheet) {
        resolve()
        return
      }
      existing.addEventListener('load', () => resolve(), { once: true })
      existing.addEventListener('error', () => resolve(), { once: true })
    })
  }

  return new Promise((resolve) => {
    const link = document.createElement('link')
    link.rel = 'stylesheet'
    link.href = STYLESHEET_HREF
    link.addEventListener('load', () => resolve(), { once: true })
    link.addEventListener('error', () => resolve(), { once: true })
    document.head.appendChild(link)
  })
}

class TicketCheckoutElement extends HTMLElement {
  static observedAttributes = ['event-id', 'holder']

  private root: Root | null = null

  async connectedCallback() {
    await ensureStylesheetLoaded()
    // Guard: si entre el await y el render el elemento se desconectó y reconectó (o StrictMode
    // remontó), no crear un segundo root sobre el mismo nodo.
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

  // En modo standalone no hay shell que escuche 'back-to-catalog' (lo hace CheckoutPanel allí),
  // así que se recarga sin el eventId — el equivalente dev de "volver al catálogo".
  window.addEventListener('back-to-catalog', () => {
    const url = new URL(window.location.href)
    url.searchParams.delete('eventId')
    window.location.assign(url.toString())
  })
}
