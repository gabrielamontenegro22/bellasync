import { NavLink, useNavigate } from 'react-router-dom'
import {
  Calendar,
  Users,
  Sparkles,
  Scissors,
  Box,
  Wallet,
  Banknote,
  BarChart3,
  Settings,
  LogOut,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'

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
}

const NAV_ITEMS: NavItem[] = [
  { to: '/agenda',                  label: 'Agenda',              icon: Calendar                 },
  { to: '/clientes',                label: 'Clientes',            icon: Users                    },
  { to: '/servicios',               label: 'Servicios',           icon: Sparkles                 },
  { to: '/estilistas',              label: 'Estilistas',          icon: Scissors                 },
  { to: '/inventario',              label: 'Inventario',          icon: Box,       disabled: true  },
  { to: '/validacion',              label: 'Validación de pagos', icon: Wallet                   },
  { to: '/caja',                    label: 'Cierre de caja',      icon: Banknote                 },
  { to: '/reportes',                label: 'Reportes',            icon: BarChart3, disabled: true  },
  { to: '/configuracion',           label: 'Configuración',       icon: Settings                 },
]

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
          {NAV_ITEMS.map((item) => {
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

        {/* Footer con datos del usuario */}
        <div className="border-t border-warm-150 p-3 m-3 mt-2 rounded-xl bg-warm-50/60">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-full bg-gold-200 text-gold-600 flex items-center justify-center font-semibold text-[13px]">
              {initials}
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-[13px] font-medium text-warm-800 truncate">
                {user?.fullName || 'Usuario'}
              </div>
              <div className="text-[11.5px] text-warm-500 truncate">
                {user?.role === 'SalonAdmin' ? 'Administradora' : user?.role}
                {user?.tenantName ? ` · ${user.tenantName}` : ''}
              </div>
            </div>
            <button
              type="button"
              onClick={handleLogout}
              title="Cerrar sesión"
              className="p-1.5 text-warm-400 hover:text-warm-700 rounded-md hover:bg-white"
            >
              <LogOut size={16} />
            </button>
          </div>
        </div>
      </aside>
    </>
  )
}
