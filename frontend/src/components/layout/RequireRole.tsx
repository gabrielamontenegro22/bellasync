import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@/features/auth/useAuth'

type Role = 'SalonAdmin' | 'Receptionist' | 'Stylist' | 'SuperAdmin'

interface RequireRoleProps {
  /** Roles autorizados. Si el rol del user no está acá, redirigimos. */
  roles: Role[]
  /**
   * A dónde mandar al user si no está autorizado.
   * Default: /agenda (home operativa de recepción/stylist), o el panel
   * de SaaS Admin si justo el bloqueado es un SuperAdmin curioseando
   * pantallas de tenant.
   */
  fallback?: string
  children: ReactNode
}

/**
 * Guard de rol. Asume autenticación previa (envolverlo dentro de
 * <ProtectedRoute>). Si el rol del user no está permitido, redirige.
 *
 * Defensa en profundidad: el backend ya bloquea las MUTACIONES con
 * [Authorize(Roles=...)], pero queremos que recepción tampoco VEA
 * pantallas que no le corresponden (reportes financieros, configuración
 * del salón, suscripción, panel SaaS) si por curiosidad escribe la URL.
 */
export function RequireRole({ roles, fallback, children }: RequireRoleProps) {
  const { user } = useAuth()
  const location = useLocation()

  // Si no hay user, <ProtectedRoute> debería haber actuado primero;
  // mandamos a login por las dudas y dejamos `from` para volver post-login.
  if (!user) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (roles.includes(user.role as Role)) {
    return <>{children}</>
  }

  // No autorizado: ruta de escape coherente con el rol real del user.
  const target =
    fallback ?? (user.role === 'SuperAdmin' ? '/saas-admin/subscriptions' : '/agenda')
  return <Navigate to={target} replace />
}
