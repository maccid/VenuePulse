import './components/TicketCheckoutElement'

const standaloneRoot = document.getElementById('ticket-checkout-standalone-root')
if (standaloneRoot) {
  standaloneRoot.innerHTML = ''
  const element = document.createElement('ticket-checkout-app')
  element.setAttribute('event-id', new URLSearchParams(location.search).get('eventId') ?? '')
  element.setAttribute('holder', 'Ana (dev)')
  standaloneRoot.appendChild(element)

  window.addEventListener('back-to-catalog', () => {
    const url = new URL(window.location.href)
    url.searchParams.delete('eventId')
    window.location.assign(url.toString())
  })
}
