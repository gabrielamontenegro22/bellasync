import { NavLink, useNavigate } from 'react-router-dom'
import {
  Calendar,
  Users,
  Sparkles,
  Scissors,
  Box,
  Wallet,
  Banknote,
  Percent,
  BarChart3,
  Settings,
  LogOut,
  ShieldCheck,
} from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { cls } from '@/lib/cls'
import { useAuth, usePermissions } from '@/features/auth/useAuth'
import { useCommissionsSetting } from '@/features/commissions/useCommissionsSetting'
import { getDashboardSummary } from '@/api/dashboard'
import { getInventorySummary } from '@/api/inventory'

interface AppSidebarProps {
  /** En mobile, el sidebar es un drawer controlado externamente */
  open: boolean
  onClose: () => void
}

interface NavItem {
  to: string
  label: string
  icon: React.ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  badge?: string | null
  /** Si está deshabilitada (módulo todavía no implementado), se muestra en gris y no navega */
  disabled?: boolean
  /**
   * Si true, el item solo aparece para SalonAdmin. Receptionists no lo
   * ven en su sidebar — el backend igual filtra por [Authorize Roles=...],
   * pero acá lo ocultamos para no mostrarle botones que tirarían 403.
   * Default false = todos los roles del salón (admin + recepción) lo ven.
   */
  adminOnly?: boolean
}

/**
 * Items base. El item de "Comisiones" se inyecta condicionalmente
 * abajo dependiendo de Tenant.CommissionsEnabled — algunos salones no
 * usan comisiones (sueldo fijo / alquiler de silla) y no queremos
 * ensuciarles el sidebar.
 *
 * Matriz de visibilidad (combinada con el filtro de abajo):
 *   - Agenda/Clientes/Validación/Caja → siempre ambos roles (operativo).
 *   - Servicios/Estilistas → siempre ambos roles ven el item; la
 *     edición dentro de cada página la controla CanEditServices /
 *     CanEditStylists (la página muestra/oculta botones de crear/editar).
 *   - Reportes → admin siempre; recepción solo con CanViewReports.
 *   - Comisiones → admin siempre (si el módulo está enabled); recepción
 *     solo con CanViewCommissions. Liquidar sigue admin-only en el backend.
 *   - Configuración → admin siempre; recepción solo si tiene al menos
 *     uno de los 3 permisos configurables de subsecciones
 *     (CanEditSchedule / CanEditPaymentPolicy / CanEditSalonInfo).
 *
 * El filtro de abajo replica la lógica del backend (frontend == defensa
 * UI; backend == defensa real). Si recepción burla el sidebar y entra
 * por URL directa, los endpoints devuelven 403 y los guards de ruta
 * (<RequirePermission/>) la mandan a /agenda.
 */
const BASE_NAV_ITEMS: NavItem[] = [
  { to: '/agenda',                  label: 'Agenda',              icon: Calendar                 },
  { to: '/clientes',                label: 'Clientes',            icon: Users                    },
  { to: '/servicios',               label: 'Servicios',           icon: Sparkles                 },
  { to: '/estilistas',              label: 'Estilistas',          icon: Scissors                 },
  { to: '/inventario',              label: 'Inventario',          icon: Box                      },
  { to: '/validacion',              label: 'Validación de pagos', icon: Wallet                   },
  { to: '/caja',                    label: 'Cierre de caja',      icon: Banknote                 },
  { to: '/reportes',                label: 'Reportes',            icon: BarChart3,                  adminOnly: true },
  { to: '/configuracion',           label: 'Configuración',       icon: Settings,                   adminOnly: true },
]

/** Item Comisiones — solo aparece si el módulo está activo Y es admin. */
const COMMISSIONS_ITEM: NavItem = {
  to: '/comisiones', label: 'Comisiones', icon: Percent, adminOnly: true,
}

/**
 * Sidebar principal de la aplicación.
 * Replica fielmente el AppSidebar del mockup config-servicios.jsx + app.jsx.
 *
 * Items deshabilitados: módulos que aún no tienen pantalla (Agenda, Clientes,
 * Inventario, etc.). Al hacer hover muestran "Próximamente". Cuando se
 * implemente el módulo, basta con quitar `disabled: true` del item.
 */
export function AppSidebar({ open, onClose }: AppSidebarProps) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const { data: commissionsSetting } = useCommissionsSetting()
  // Para que recepción vea Reportes/Comisiones SOLO si la admin
  // les dio permiso. Admin siempre los ve.
  const perms = usePermissions()

  // Reusamos el mismo query que el Dashboard para badges. Compartir
  // la queryKey hace que ambos componentes lean el mismo cache — sin
  // queries duplicados ni desincronización.
  // No corremos para SuperAdmin (no tiene tenant ni vouchers).
  const { data: dashboard } = useQuery({
    queryKey: ['dashboard'],
    queryFn: getDashboardSummary,
    refetchInterval: 60_000,
    enabled: user?.role !== 'SuperAdmin' && !!user,
  })

  // Resumen de inventario — para el badge "X stock bajo + agotados" al
  // costado del item Inventario. Refresca cada 90s (no necesita ser
  // tan reactivo como Validación). Shared queryKey con InventoryPage
  // para que ambos lean del mismo cache.
  const { data: inventorySummary } = useQuery({
    queryKey: ['inventorySummary'],
    queryFn: getInventorySummary,
    refetchInterval: 90_000,
    enabled: user?.role !== 'SuperAdmin' && !!user,
  })

  // Pipeline:
  //   1. SuperAdmin (dueño BellaSync) tiene su propio item dedicado
  //   2. Para salones: arranca con BASE_NAV_ITEMS
  //   3. Si el módulo Comisiones está activo, inyecta el item
  //   4. Filtra items adminOnly cuando el user no es SalonAdmin
  //      (Receptionist queda con sidebar reducido: Agenda, Clientes,
  //       Validación, Caja, Servicios y Estilistas para consultar)
  const navItems = (() => {
    if (user?.role === 'SuperAdmin') {
      return [
        {
          to: '/saas-admin/subscriptions',
          label: 'Validación SaaS',
          icon: ShieldCheck,
        } as NavItem,
      ]
    }

    let items: NavItem[] = BASE_NAV_ITEMS
    if (commissionsSetting?.enabled) {
      const idx = items.findIndex(i => i.to === '/reportes')
      items = [...items.slice(0, idx), COMMISSIONS_ITEM, ...items.slice(idx)]
    }

    // Filtros por permisos (admin pasa todo; recepción según toggles):
    //   /reportes      → CanViewReports
    //   /comisiones    → CanViewCommissions
    //   /configuracion → admin only (es agregador; las subpages tienen
    //                    sus propios chequeos por sub-item)
    items = items.filter((item) => {
      if (item.to === '/reportes' && !perms.canViewReports) return false
      if (item.to === '/comisiones' && !perms.canViewCommissions) return false
      // Configuración: necesita ser admin O tener al menos un permiso
      // de edición de algo. Si no tiene ninguno, no le sirve entrar.
      if (item.to === '/configuracion' && !perms.isAdmin
          && !perms.canEditSchedule && !perms.canEditPaymentPolicy
          && !perms.canEditSalonInfo) return false
      // Items legacy con flag adminOnly que no son configurables (ej.
      // Configuración para no-admin si tampoco tiene sub-permisos).
      if (item.adminOnly && !perms.isAdmin) {
        // Excepción: si es uno de los que recién agregamos como
        // configurable, ya filtramos arriba. Para el resto (none por
        // ahora pero defensa a futuro), seguimos restringiendo.
        if (item.to !== '/reportes' && item.to !== '/comisiones'
            && item.to !== '/configuracion') return false
      }
      return true
    })

    // Inyectamos badges desde el dashboard summary. Los hace silenciar
    // cuando el conteo es 0 (no queremos un "(0)" estético).
    items = items.map((item) => {
      if (item.to === '/validacion' && dashboard?.pendingVouchersCount) {
        return { ...item, badge: dashboard.pendingVouchersCount.toString() }
      }
      if (item.to === '/caja' && dashboard?.cashClosingPending) {
        return { ...item, badge: '!' }
      }
      if (item.to === '/inventario' && inventorySummary) {
        // Sumamos stock bajo + agotados — ambos requieren acción de la admin.
        const alerts = inventorySummary.lowStockCount + inventorySummary.outOfStockCount
        if (alerts > 0) return { ...item, badge: alerts.toString() }
      }
      return item
    })

    return items
  })()

  const handleLogout = () => {
    logout()
    navigate('/login', { replace: true })
  }

  const initials = user?.fullName
    ? user.fullName.split(' ').slice(0, 2).map((w) => w[0]?.toUpperCase()).join('')
    : '·'

  return (
    <>
      {/* Backdrop mobile */}
      {open && (
        <div
          onClick={onClose}
          className="lg:hidden fixed inset-0 bg-warm-900/30 z-40 anim-fade"
        />
      )}

      <aside
        className={cls(
          'fixed lg:static z-50 inset-y-0 left-0 w-[260px] flex-shrink-0',
          'bg-white border-r border-warm-150 flex flex-col',
          'transform transition-transform duration-200',
          open ? 'translate-x-0' : '-translate-x-full lg:translate-x-0',
        )}
      >
        {/* Brand */}
        <div className="px-6 pt-6 pb-5 flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-lg bg-brand-700 flex items-center justify-center text-white">
            <span className="font-serif text-[18px] leading-none translate-y-[1px]">B</span>
          </div>
          <div className="font-serif text-[22px] tracking-tight text-warm-800 leading-none">
            BellaSync
          </div>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 pt-2 space-y-0.5 overflow-y-auto">
          {navItems.map((item) => {
            if (item.disabled) {
              return (
                <button
                  key={item.to}
                  type="button"
                  disabled
                  title="Próximamente"
                  className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-[14px] text-warm-400 cursor-not-allowed"
                >
                  <item.icon size={18} strokeWidth={1.75} />
                  <span className="flex-1 text-left">{item.label}</span>
                  <span className="text-[10px] font-medium uppercase tracking-wider text-warm-300">
                    pronto
                  </span>
                </button>
              )
            }

            return (
              <NavLink
                key={item.to}
                to={item.to}
                onClick={onClose}
                className={({ isActive }) =>
                  cls(
                    'w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-[14px] transition',
                    isActive
                      ? 'bg-brand-50 text-brand-800 font-medium'
                      : 'text-warm-600 hover:bg-warm-50 hover:text-warm-800',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      size={18}
                      strokeWidth={isActive ? 2 : 1.75}
                      className={isActive ? 'text-brand-700' : ''}
                    />
                    <span className="flex-1 text-left">{item.label}</span>
                    {item.badge && (
                      <span className="text-[11px] font-semibold px-1.5 py-0.5 rounded-md bg-warm-150 text-warm-600">
                        {item.badge}
                      </span>
                    )}
                  </>
                )}
              </NavLink>
            )
          })}
        </nav>

        {/* Footer con datos del usuario.
            El bloque avatar+nombre es cliqueable y lleva a /mi-cuenta;
            el botón logout queda separado al costado. */}
        <div className="border-t border-warm-150 p-3 m-3 mt-2 rounded-xl bg-warm-50/60">
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => { onClose(); navigate('/mi-cuenta') }}
              title="Mi cuenta"
              className="flex items-center gap-3 flex-1 min-w-0 text-left rounded-lg hover:bg-white px-1 py-1 -m-1 transition"
            >
              <div className="w-9 h-9 rounded-full bg-gold-200 text-gold-600 flex items-center justify-center font-semibold text-[13px] flex-shrink-0">
                {initials}
              </div>
              <div className="flex-1 min-w-0">
                <div className="text-[13px] font-medium text-warm-800 truncate">
                  {user?.fullName || 'Usuario'}
                </div>
                <div className="text-[11.5px] text-warm-500 truncate">
                  {roleLabel(user?.role)}
                  {user?.tenantName ? ` · ${user.tenantName}` : ''}
                </div>
              </div>
            </button>
            <button
              type="button"
              onClick={handleLogout}
              title="Cerrar sesión"
              className="p-1.5 text-warm-400 hover:text-warm-700 rounded-md hover:bg-white flex-shrink-0"
            >
              <LogOut size={16} />
            </button>
          </div>
        </div>
      </aside>
    </>
  )
}

/** Convierte el role del backend ('SalonAdmin' | 'Receptionist' | ...)
 *  a la etiqueta que mostramos al user en español. */
function roleLabel(role: string | undefined): string {
  switch (role) {
    case 'SalonAdmin':   return 'Administradora'
    case 'Receptionist': return 'Recepción'
    case 'Stylist':      return 'Estilista'
    case 'SuperAdmin':   return 'SaaS Admin'
    default:             return role ?? ''
  }
}
