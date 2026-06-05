import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { usePermissions } from '@/features/auth/useAuth'

type Perm =
  | 'canCancelWithMoney'
  | 'canCloseCash'
  | 'canEditStylists'
  | 'canEditServices'
  | 'canEditInventory'
  | 'canViewReports'
  | 'canViewCommissions'
  | 'canEditSchedule'
  | 'canEditPaymentPolicy'
  | 'canEditSalonInfo'

interface RequirePermissionProps {
  /** Permiso requerido para entrar a la ruta. */
  permission: Perm
  /**
   * A dónde redirigir si no autorizada. Default /agenda (la home
   * operativa). El backend de todas formas devolverá 403 si recepción
   * intenta el endpoint sin permiso, así que este guard es UX/defensa.
   */
  fallback?: string
  children: ReactNode
}

/**
 * Guard de ruta basado en los permisos configurables del tenant
 * (Tenant.ReceptionCanXxx). Hermanito de <RequireRole> pero más
 * granular — en vez de "rol SalonAdmin", chequea capability específica.
 *
 * Admin siempre pasa (usePermissions le da true en todo). Recepción
 * pasa si la admin activó el toggle desde /configuracion/permisos.
 *
 * Mientras carga, redirigimos a /agenda en vez de mostrar un loader —
 * un flicker breve es mejor que mostrar una página por 200ms y después
 * sacarla. La query tiene staleTime 5min así que la mayoría de las
 * veces ya está en cache.
 */
export function RequirePermission({ permission, fallback, children }: RequirePermissionProps) {
  const perms = usePermissions()

  if (perms.isLoading) return <Navigate to={fallback ?? '/agenda'} replace />

  const allowed = perms[permission] as boolean
  if (!allowed) return <Navigate to={fallback ?? '/agenda'} replace />

  return <>{children}</>
}
