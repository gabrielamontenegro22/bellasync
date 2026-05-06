import type { AuthResponse, AuthenticatedUser } from '@/types/auth'

/**
 * Wrapper de localStorage para datos de autenticación.
 * Centralizamos las keys en constantes para evitar typos repartidos.
 */

const TOKEN_KEY = 'bellasync_token'
const USER_KEY  = 'bellasync_user'

function safeParse<T>(raw: string | null): T | null {
  if (!raw) return null
  try { return JSON.parse(raw) as T }
  catch { return null }
}

export const authStorage = {
  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  },

  getUser(): AuthenticatedUser | null {
    return safeParse<AuthenticatedUser>(localStorage.getItem(USER_KEY))
  },

  /** Persiste la respuesta de auth: token + datos básicos del usuario. */
  save(auth: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, auth.token)
    const user: AuthenticatedUser = {
      userId:     auth.userId,
      email:      auth.email,
      fullName:   auth.fullName,
      role:       auth.role,
      tenantId:   auth.tenantId,
      tenantName: auth.tenantName,
      tenantSlug: auth.tenantSlug,
    }
    localStorage.setItem(USER_KEY, JSON.stringify(user))
  },

  clear(): void {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  },
}
