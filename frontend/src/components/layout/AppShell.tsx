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
    <div className="min-h-screen flex bg-warm-50">
      <AppSidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      <div className="flex-1 min-w-0 flex flex-col">
        {/* Top bar mobile — solo aparece debajo de lg */}
        <div className="lg:hidden bg-white border-b border-warm-150 px-4 py-3 flex items-center gap-3 sticky top-0 z-20">
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

        {/* Contenido de la pantalla actual */}
        <main className="flex-1 min-w-0 overflow-y-auto">
          {children}
        </main>
      </div>
    </div>
  )
}
