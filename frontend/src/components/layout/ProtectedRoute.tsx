import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@/features/auth/useAuth'

/**
 * Wrapper que solo deja pasar a usuarias autenticadas.
 * Si no hay sesión, redirige a /login y guarda la ruta de origen
 * en `location.state.from` para volver después del login exitoso.
 */
export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return <>{children}</>
}
