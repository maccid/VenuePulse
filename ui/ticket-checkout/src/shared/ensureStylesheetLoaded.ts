const STYLESHEET_HREF = new URL('./ticket-checkout.css', import.meta.url).href

export function ensureStylesheetLoaded(): Promise<void> {
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
