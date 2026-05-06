import type { ReactNode } from 'react'

/**
 * Layout para páginas públicas (login, register).
 * Contenido centrado sobre fondo cream + logo BellaSync arriba.
 */
export function PublicLayout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen flex items-center justify-center px-6 py-10 bg-cream">
      <div className="w-full max-w-md">
        <div className="flex items-center justify-center gap-3 mb-8">
          <div className="w-10 h-10 rounded-lg bg-brand-700 text-white flex items-center justify-center font-serif text-[22px] leading-none translate-y-[1px]">
            B
          </div>
          <span className="font-serif text-[28px] tracking-tight text-warm-800 leading-none">
            BellaSync
          </span>
        </div>

        {children}
      </div>
    </div>
  )
}
