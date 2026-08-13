import type { ReactNode } from 'react'

interface Props {
  title: string
  onClose: () => void
  size?: 'small' | 'large'
  children: ReactNode
}

export default function Modal({ title, onClose, size = 'small', children }: Props) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-[rgba(16,24,40,0.45)] p-6"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={onClose}
    >
      <div
        className={`flex max-h-[calc(100vh-128px)] w-full flex-col overflow-hidden rounded-lg bg-surface shadow-md ${size === 'large' ? 'max-w-[680px] h-[min(calc(100vh-128px),900px)]' : 'max-w-[440px]'}`}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="flex items-center justify-between gap-4 border-b border-border px-6 py-4">
          <h2 className="m-0">{title}</h2>
          <button
            type="button"
            className="rounded-md bg-transparent px-2.5 py-0.5 text-[24px] leading-[1.4] text-text-muted hover:bg-accent-bg hover:text-text"
            onClick={onClose}
            aria-label="Cerrar"
          >
            ×
          </button>
        </header>
        <div className="overflow-y-auto p-6">{children}</div>
      </div>
    </div>
  )
}
