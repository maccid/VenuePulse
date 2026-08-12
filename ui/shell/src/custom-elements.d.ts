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
