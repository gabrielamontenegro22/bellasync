import type { AuthResponse, AuthenticatedUser } from '@/types/auth'

/**
 * Wrapper de localStorage para datos de autenticación.
 * Centralizamos las keys en constantes para evitar typos repartidos.
 */

const TOKEN_KEY         = 'bellasync_token'
const REFRESH_TOKEN_KEY = 'bellasync_refresh_token'
const USER_KEY          = 'bellasync_user'

function safeParse<T>(raw: string | null): T | null {
  if (!raw) return null
  try { return JSON.parse(raw) as T }
  catch { return null }
}

export const authStorage = {
  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  },

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY)
  },

  getUser(): AuthenticatedUser | null {
    return safeParse<AuthenticatedUser>(localStorage.getItem(USER_KEY))
  },

  /** Persiste la respuesta de auth: access + refresh tokens + datos del usuario. */
  save(auth: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, auth.token)
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)
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

  /**
   * Actualiza solo los tokens después de un refresh exitoso, sin tocar
   * la información del usuario (no cambia entre refreshes).
   */
  updateTokens(token: string, refreshToken: string): void {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
  },

  clear(): void {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  },
}
