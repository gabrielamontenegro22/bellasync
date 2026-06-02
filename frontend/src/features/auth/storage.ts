import type { AuthResponse, AuthenticatedUser } from '@/types/auth'

/**
 * Wrapper de localStorage para datos de autenticación.
 *
 * COOKIE-BASED REFRESH: el refresh token NO se guarda en localStorage;
 * vive solo en una cookie HttpOnly inalcanzable desde JavaScript (mitiga XSS).
 * Solo el access token corto (15-30 min) queda en localStorage.
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

  /**
   * Persiste el access token + datos del usuario tras login/register.
   * El refresh token llega en la cookie HttpOnly automáticamente — NO lo
   * tocamos desde JavaScript.
   */
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

  /**
   * Actualiza solo el access token después de un refresh exitoso.
   * El nuevo refresh token llegó en la cookie HttpOnly automáticamente.
   */
  updateAccessToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token)
  },

  clear(): void {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    // La cookie HttpOnly NO se puede borrar desde JS. Para borrarla
    // de forma confiable hay que llamar a serverLogout() de api/axios.ts.
  },
}
