import { useContext } from 'react'
import { AuthContext } from './AuthContext'

/**
 * Hook para consumir el contexto de auth.
 * Tira error explícito si se usa fuera del <AuthProvider>.
 */
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  return ctx
}
