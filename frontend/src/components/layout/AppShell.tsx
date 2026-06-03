import { useState, type ReactNode } from 'react'
import { Menu } from 'lucide-react'
import { AppSidebar } from './AppSidebar'

interface AppShellProps {
  children: ReactNode
}

/**
 * Wrapper estándar de las pantallas internas (post-login).
 *
 * Layout:
 *   ┌──────────┬──────────────────────────────┐
 *   │          │                              │
 *   │  AppSide │       children               │
 *   │   bar    │     (la pantalla actual)     │
 *   │          │                              │
 *   └──────────┴──────────────────────────────┘
 *
 * En mobile el sidebar se convierte en drawer; un botón hamburguesa lo abre.
 */
export function AppShell({ children }: AppShellProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false)

  return (
    // h-screen (no min-h-screen): altura EXACTA del viewport. Necesario
    // para que páginas como /agenda con layout fixed (header sticky +
    // timeline scrollable + panel detalle con footer pegado) funcionen
    // bien. Otras páginas que tengan contenido más largo van a scrollear
    // dentro de <main overflow-y-auto>.
    <div className="h-screen flex bg-warm-50 overflow-hidden">
      <AppSidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      <div className="flex-1 min-w-0 flex flex-col min-h-0">
        {/* Top bar mobile — solo aparece debajo de lg */}
        <div className="lg:hidden bg-white border-b border-warm-150 px-4 py-3 flex items-center gap-3 flex-shrink-0">
          <button
            type="button"
            onClick={() => setSidebarOpen(true)}
            className="p-1.5 text-warm-700 hover:bg-warm-100 rounded-md"
            aria-label="Abrir menú"
          >
            <Menu size={20} />
          </button>
          <div className="font-serif text-[18px] text-warm-800">BellaSync</div>
        </div>

        {/* Contenido de la pantalla actual.
            min-h-0 + flex-1 → respeta la altura del padre y deja que el
            children maneje su propio overflow interno. */}
        <main className="flex-1 min-w-0 min-h-0 overflow-y-auto">
          {children}
        </main>
      </div>
    </div>
  )
}
