import type { ReactNode } from 'react'

interface Props {
  title: string
  onClose: () => void
  // 'large' se usa para el alta de evento: el diálogo ocupa de arriba abajo lo mismo que el
  // cuadro de detalle, 'small' para las confirmaciones del ciclo de vida.
  size?: 'small' | 'large'
  children: ReactNode
}

export default function Modal({ title, onClose, size = 'small', children }: Props) {
  return (
    <div className={`modal modal--${size}`} role="dialog" aria-modal="true" aria-label={title} onClick={onClose}>
      <div className="modal__dialog" onClick={(e) => e.stopPropagation()}>
        <header className="modal__header">
          <h2>{title}</h2>
          <button type="button" className="modal__close" onClick={onClose} aria-label="Cerrar">
            ×
          </button>
        </header>
        <div className="modal__body">{children}</div>
      </div>
    </div>
  )
}
