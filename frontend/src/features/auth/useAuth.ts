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

/**
 * Atajo: ¿el user actual es SalonAdmin?
 * Lo usan páginas/cards compartidas para ocultar botones de CRUD
 * (editar/borrar/crear) que el backend solo acepta de admin.
 *
 * Devuelve false si no hay user (sesión cerrada) o si el rol es otro
 * (Receptionist, Stylist, SuperAdmin). No tira — pensado para inline.
 */
export function useIsAdmin(): boolean {
  const { user } = useAuth()
  return user?.role === 'SalonAdmin'
}
