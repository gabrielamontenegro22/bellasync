import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  Building2,
  Sparkles,
  Scissors,
  CalendarClock,
  Wallet,
  MessageCircle,
  CreditCard,
  ChevronRight,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { AppShell } from '@/components/layout/AppShell'
import { useAuth } from '@/features/auth/useAuth'

interface ConfigSection {
  to: string
  label: string
  icon: React.ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  hint: string
  /** Si todavía no tiene pantalla — se muestra deshabilitado */
  disabled?: boolean
}

const CONFIG_SECTIONS: ConfigSection[] = [
  { to: '/configuracion/general',     label: 'Información general',       icon: Building2,      hint: 'Nombre, dirección, logo',     disabled: true },
  { to: '/configuracion/servicios',   label: 'Servicios',                  icon: Sparkles,       hint: 'Catálogo y precios' },
  { to: '/configuracion/estilistas',  label: 'Estilistas',                 icon: Scissors,       hint: 'Equipo y asignaciones' },
  { to: '/configuracion/horario',     label: 'Horario del salón',          icon: CalendarClock,  hint: 'Días y franjas',              disabled: true },
  { to: '/configuracion/pagos',       label: 'Política de pagos',          icon: Wallet,         hint: 'Anticipos y métodos',         disabled: true },
  { to: '/configuracion/whatsapp',    label: 'Notificaciones WhatsApp',    icon: MessageCircle,  hint: 'Plantillas y envíos',         disabled: true },
  { to: '/configuracion/suscripcion', label: 'Suscripción y facturación',  icon: CreditCard,     hint: 'Plan BellaSync',              disabled: true },
]

/**
 * Layout específico de la sección "Configuración".
 * Renderiza dentro de <AppShell>:
 *   - ConfigSidebar (lista de subsecciones)
 *   - <Outlet /> donde React Router monta la subsección actual
 *
 * Replica el ConfigSidebar del mockup config-servicios.jsx.
 */
export function ConfigLayout() {
  const { user } = useAuth()
  const location = useLocation()

  return (
    <AppShell>
      <div className="flex-1 min-w-0 flex">
        <ConfigSidebar tenantName={user?.tenantName ?? 'Salón'} />

        {/* Mobile: select para cambiar de subsección */}
        <MobileSubsectionSwitcher current={location.pathname} />

        <div className="flex-1 min-w-0">
          <Outlet />
        </div>
      </div>
    </AppShell>
  )
}

/* -------------------------------------------------------------------------- */
/*  Sidebar de configuración                                                  */
/* -------------------------------------------------------------------------- */

function ConfigSidebar({ tenantName }: { tenantName: string }) {
  return (
    <aside className="hidden lg:flex w-[260px] flex-shrink-0 flex-col border-r border-warm-150 bg-white">
      <div className="px-6 pt-7 pb-4">
        <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-400 font-medium truncate">
          {tenantName}
        </div>
        <div className="font-serif text-[26px] text-warm-800 leading-tight mt-1">
          Configuración
        </div>
      </div>

      <nav className="flex-1 px-3 pb-5 space-y-0.5 overflow-y-auto">
        {CONFIG_SECTIONS.map((section) => {
          if (section.disabled) {
            return (
              <button
                key={section.to}
                type="button"
                disabled
                title="Próximamente"
                className="w-full text-left flex items-start gap-3 px-3 py-2.5 rounded-lg cursor-not-allowed opacity-60"
              >
                <section.icon size={17} strokeWidth={1.75} className="mt-0.5 flex-shrink-0 text-warm-400" />
                <div className="flex-1 min-w-0">
                  <div className="text-[13.5px] leading-tight text-warm-500 font-medium">
                    {section.label}
                  </div>
                  <div className="text-[11.5px] text-warm-400 mt-0.5 truncate">
                    {section.hint} · pronto
                  </div>
                </div>
              </button>
            )
          }

          return (
            <NavLink
              key={section.to}
              to={section.to}
              className={({ isActive }) =>
                cls(
                  'w-full text-left flex items-start gap-3 px-3 py-2.5 rounded-lg transition',
                  isActive ? 'bg-brand-50' : 'hover:bg-warm-50',
                )
              }
            >
              {({ isActive }) => (
                <>
                  <section.icon
                    size={17}
                    strokeWidth={isActive ? 2 : 1.75}
                    className={cls('mt-0.5 flex-shrink-0', isActive ? 'text-brand-700' : 'text-warm-400')}
                  />
                  <div className="flex-1 min-w-0">
                    <div
                      className={cls(
                        'text-[13.5px] leading-tight',
                        isActive ? 'text-brand-800 font-semibold' : 'text-warm-800 font-medium',
                      )}
                    >
                      {section.label}
                    </div>
                    <div className="text-[11.5px] text-warm-500 mt-0.5 truncate">
                      {section.hint}
                    </div>
                  </div>
                  {isActive && <ChevronRight size={14} className="text-brand-700 mt-1.5" />}
                </>
              )}
            </NavLink>
          )
        })}
      </nav>
    </aside>
  )
}

/* -------------------------------------------------------------------------- */
/*  Switcher de subsección en mobile (select dropdown)                        */
/* -------------------------------------------------------------------------- */

function MobileSubsectionSwitcher({ current }: { current: string }) {
  const enabled = CONFIG_SECTIONS.filter((s) => !s.disabled)

  return (
    <div className="lg:hidden bg-white border-b border-warm-150 px-4 py-3 w-full">
      <select
        value={current}
        onChange={(e) => {
          window.location.assign(e.target.value)
        }}
        className="w-full px-3 py-2 rounded-md border border-warm-200 bg-white text-[13.5px] text-warm-800"
      >
        {enabled.map((s) => (
          <option key={s.to} value={s.to}>
            {s.label}
          </option>
        ))}
      </select>
    </div>
  )
}
