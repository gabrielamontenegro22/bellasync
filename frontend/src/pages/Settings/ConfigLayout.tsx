import { NavLink, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  Building2,
  CalendarClock,
  Wallet,
  Percent,
  MessageCircle,
  CreditCard,
  ChevronRight,
  Users,
  Shield,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { AppShell } from '@/components/layout/AppShell'
import { SearchablePicker } from '@/components/ui'
import { useAuth, usePermissions } from '@/features/auth/useAuth'

interface ConfigSection {
  to: string
  label: string
  icon: React.ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  hint: string
  /** Si todavía no tiene pantalla — se muestra deshabilitado */
  disabled?: boolean
  /**
   * Predicado que decide si el ítem es visible. Admin siempre lo ve.
   * Si está ausente, default es admin-only.
   */
  visibleFor?: (perms: ReturnType<typeof usePermissions>) => boolean
}

// Solo ajustes "verdaderos" — lo operativo (servicios, estilistas, cola
// de validación) vive como item top-level en el sidebar principal porque
// se usa día a día. Acá quedan cosas que se configuran una vez y rara
// vez se tocan.
const CONFIG_SECTIONS: ConfigSection[] = [
  // Visibles para recepción solo si la admin le dio el permiso específico.
  { to: '/configuracion/general',     label: 'Información general',       icon: Building2,      hint: 'Nombre, dirección, logo',
    visibleFor: (p) => p.isAdmin || p.canEditSalonInfo },
  { to: '/configuracion/horario',     label: 'Horario del salón',          icon: CalendarClock,  hint: 'Días y franjas',
    visibleFor: (p) => p.isAdmin || p.canEditSchedule },
  { to: '/configuracion/pagos',       label: 'Política de pagos',          icon: Wallet,         hint: 'Cupo reservado y anticipación',
    visibleFor: (p) => p.isAdmin || p.canEditPaymentPolicy },
  // Admin-only — son decisiones que recepción no debería tocar nunca:
  //   · Activar/desactivar el módulo de Comisiones (afecta cómo se pagan estilistas)
  //   · WhatsApp templates (mensajes que salen del salón)
  //   · Crear/editar usuarios (incluyendo recepcionistas — sería darse permiso a sí misma)
  //   · Permisos del equipo (idem — recepción NO puede darse permisos)
  //   · Suscripción (plan SaaS, costo)
  { to: '/configuracion/comisiones',  label: 'Comisiones',                 icon: Percent,        hint: 'Activar o desactivar el módulo',
    visibleFor: (p) => p.isAdmin },
  { to: '/configuracion/whatsapp',    label: 'Notificaciones WhatsApp',    icon: MessageCircle,  hint: 'Plantillas y envíos',
    visibleFor: (p) => p.isAdmin },
  { to: '/configuracion/usuarios',    label: 'Usuarios del equipo',        icon: Users,          hint: 'Recepcionistas y administradoras',
    visibleFor: (p) => p.isAdmin },
  { to: '/configuracion/permisos',    label: 'Permisos del equipo',        icon: Shield,         hint: 'Qué puede hacer recepción',
    visibleFor: (p) => p.isAdmin },
  { to: '/configuracion/suscripcion', label: 'Suscripción y facturación',  icon: CreditCard,     hint: 'Plan BellaSync',
    visibleFor: (p) => p.isAdmin },
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
  const perms = usePermissions()
  const location = useLocation()

  // Filtramos las secciones según permisos. Admin las ve todas;
  // recepción solo las que la admin le habilitó.
  const visibleSections = CONFIG_SECTIONS.filter(
    (s) => !s.visibleFor || s.visibleFor(perms),
  )

  // Si la sub-ruta actual NO está dentro de las visibles, redirigimos
  // a la primera visible. Cubre dos casos:
  //   1. Recepción aterrizó en /configuracion (sin subpath) y el
  //      <Route index Navigate to="general"> la tiraba a una sección
  //      que no tiene permiso de ver (típicamente /general).
  //   2. Recepción intenta entrar por URL directa a una sección que
  //      no le corresponde (ej. /configuracion/usuarios).
  // Admin tiene todas visibles, así que para ella esto nunca dispara.
  if (visibleSections.length > 0) {
    const inside = location.pathname === '/configuracion'
      || visibleSections.some((s) => location.pathname.startsWith(s.to))
    if (!inside) {
      return <Navigate to={visibleSections[0].to} replace />
    }
    // Caso raíz exacto /configuracion: si la primera visible NO es
    // /configuracion/general (admin), mandamos a la primera de la lista
    // del user (recepción típicamente verá /configuracion/horario).
    if (location.pathname === '/configuracion'
        && visibleSections[0].to !== '/configuracion/general') {
      return <Navigate to={visibleSections[0].to} replace />
    }
  }

  return (
    <AppShell>
      <div className="flex-1 min-w-0 flex">
        <ConfigSidebar
          tenantName={user?.tenantName ?? 'Salón'}
          sections={visibleSections}
        />

        {/* Columna de contenido: el switcher mobile va ENCIMA del Outlet,
            no como hermano del flex row. Si quedaba al lado, con su
            `w-full` ocupaba todo el ancho y el Outlet se renderizaba con
            0px → la página parecía vacía (este era el bug visible en
            /configuracion/general en iPad). */}
        <div className="flex-1 min-w-0 flex flex-col">
          <MobileSubsectionSwitcher
            current={location.pathname}
            sections={visibleSections}
          />
          <div className="flex-1 min-w-0 min-h-0">
            {visibleSections.length === 0 ? (
              <div className="px-6 lg:px-10 py-16 max-w-xl">
                <div className="rounded-2xl border border-warm-150 bg-warm-50/60 p-6 text-[13px] text-warm-600 leading-relaxed">
                  <strong className="text-warm-800 block mb-1.5">
                    No tenés acceso a ninguna sección de Configuración.
                  </strong>
                  La administradora del salón decide qué podés tocar acá.
                  Pedile que active alguno de los permisos en
                  Configuración → Permisos del equipo (ej. "Puede editar
                  el horario del salón").
                </div>
              </div>
            ) : (
              <Outlet />
            )}
          </div>
        </div>
      </div>
    </AppShell>
  )
}

/* -------------------------------------------------------------------------- */
/*  Sidebar de configuración                                                  */
/* -------------------------------------------------------------------------- */

function ConfigSidebar({
  tenantName, sections,
}: {
  tenantName: string
  sections: ConfigSection[]
}) {
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
        {sections.map((section) => {
          if (section.disabled) {
            return (
              <button
                key={section.to}
                type="button"
                disabled
                title="Próximamente"
                className="w-full text-left flex items-start gap-3 px-3 py-2.5 rounded-lg cursor-not-allowed"
              >
                <section.icon
                  size={17}
                  strokeWidth={1.75}
                  className="mt-0.5 flex-shrink-0 text-warm-300"
                />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <div className="text-[13.5px] leading-tight text-warm-500 font-medium">
                      {section.label}
                    </div>
                    <span className="text-[8.5px] tracking-[0.12em] uppercase font-semibold text-gold-600 bg-gold-50 border border-gold-200 px-1 py-0.5 rounded">
                      Pronto
                    </span>
                  </div>
                  <div className="text-[11.5px] text-warm-400 mt-0.5 truncate">
                    {section.hint}
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

function MobileSubsectionSwitcher({
  current, sections,
}: {
  current: string
  sections: ConfigSection[]
}) {
  const enabled = sections.filter((s) => !s.disabled)
  const navigate = useNavigate()

  // Usamos SearchablePicker (no <select> nativo) por la misma razón que en
  // los date pickers: el dropdown del browser nativo en mobile/iPad rompe
  // la identidad visual (fondo blanco, fila azul de Chrome, fuente del SO).
  // SearchablePicker tiene threshold default 8 → con 6 secciones no muestra
  // input de búsqueda; solo aparece como lista limpia.
  return (
    <div className="lg:hidden bg-white border-b border-warm-150 px-4 py-3 flex-shrink-0">
      <SearchablePicker
        value={current}
        onChange={(v) => navigate(v)}
        options={enabled.map((s) => ({
          value: s.to,
          label: s.label,
          sublabel: s.hint,
        }))}
        placeholder="Seleccionar sección…"
        size="md"
      />
    </div>
  )
}
