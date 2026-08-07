// El micro frontend "ticket-checkout" se monta como Web Component (ver frontend/nginx/nginx.conf
// y ticket-checkout/src/main.tsx). Este archivo solo le enseña a TypeScript/JSX que el tag
// existe y qué atributos acepta — no hay lógica compartida entre apps, solo este contrato.
// React 19 mueve el namespace JSX dentro del módulo 'react' (React.JSX), ya no es global.
import 'react'

declare module 'react' {
  namespace JSX {
    interface IntrinsicElements {
      'ticket-checkout-app': React.DetailedHTMLProps<React.HTMLAttributes<HTMLElement>, HTMLElement> & {
        'event-id'?: string
        holder?: string
      }
    }
  }
}
