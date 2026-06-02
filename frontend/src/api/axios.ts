import axios, { AxiosError, AxiosHeaders, type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios'
import { authStorage } from '@/features/auth/storage'

/**
 * Cliente HTTP único para BellaSync API.
 *
 * COOKIE-BASED REFRESH:
 *  - El refresh token vive en una cookie HttpOnly (set por el backend).
 *  - withCredentials: true asegura que la cookie viaja en cada request.
 *  - En dev: Vite proxy /api → :5059 → mismo origen lógico, cookie funciona.
 *  - En prod: frontend y API en mismo site → SameSite=Lax + Secure HTTPS.
 *
 *  - baseURL vacío: usamos paths relativos (/api/...) — Vite proxy en dev,
 *    reverse proxy / mismo dominio en prod.
 *  - Interceptor de request: inyecta `Authorization: Bearer <jwt>` si hay sesión.
 *  - Interceptor de response: si llega 401 fuera de los flujos de auth, intenta
 *    refrescar (sin body — la cookie va sola). Si funciona, reintenta el
 *    request original. Si falla, limpia sesión y dispara AUTH_LOGOUT_EVENT.
 */

const baseURL = import.meta.env.VITE_API_BASE_URL ?? ''

export const api = axios.create({
  baseURL,
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
  // Crítico para que la cookie HttpOnly viaje en cada request.
  withCredentials: true,
})

// ---------- request interceptor ----------
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = authStorage.getToken()
  if (token) {
    if (!config.headers) {
      config.headers = new AxiosHeaders()
    }
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  return config
})

// ---------- response interceptor con auto-refresh ----------
export const AUTH_LOGOUT_EVENT = 'bellasync:auth:logout'

interface RetryableConfig extends AxiosRequestConfig {
  _retry?: boolean
}

let refreshInFlight: Promise<string | null> | null = null

function isAuthEndpoint(url: string | undefined): boolean {
  if (!url) return false
  return url.includes('/Auth/login')
      || url.includes('/Auth/register-salon')
      || url.includes('/Auth/refresh')
      || url.includes('/Auth/forgot-password')
      || url.includes('/Auth/reset-password')
      || url.includes('/Auth/logout')
}

async function performRefresh(): Promise<string | null> {
  try {
    // Sin body: el backend lee la cookie HttpOnly automáticamente.
    // Call directo (sin pasar por la instancia `api`) para no recursar
    // en este mismo interceptor si el refresh también da 401.
    const resp = await axios.post<{ token: string }>(
      `${baseURL}/api/Auth/refresh`,
      {},
      {
        headers: { 'Content-Type': 'application/json' },
        timeout: 15_000,
        withCredentials: true,
      },
    )
    authStorage.updateAccessToken(resp.data.token)
    return resp.data.token
  } catch {
    return null
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const status = error.response?.status
    const original = error.config as RetryableConfig | undefined
    const url = original?.url ?? ''

    if (status !== 401 || isAuthEndpoint(url)) {
      return Promise.reject(error)
    }

    if (original?._retry) {
      authStorage.clear()
      window.dispatchEvent(new Event(AUTH_LOGOUT_EVENT))
      return Promise.reject(error)
    }

    refreshInFlight ??= performRefresh().finally(() => { refreshInFlight = null })
    const newToken = await refreshInFlight

    if (!newToken || !original) {
      authStorage.clear()
      window.dispatchEvent(new Event(AUTH_LOGOUT_EVENT))
      return Promise.reject(error)
    }

    original._retry = true
    const headers = new AxiosHeaders(original.headers as AxiosHeaders)
    headers.set('Authorization', `Bearer ${newToken}`)
    original.headers = headers
    return api(original)
  },
)

/**
 * Logout server-side: revoca el refresh token actual y borra la cookie HttpOnly.
 * Llamar desde el AuthContext antes de authStorage.clear() local.
 */
export async function serverLogout(): Promise<void> {
  try {
    await api.post('/api/Auth/logout', {})
  } catch {
    // Best-effort: si falla, la cookie expirará por sí sola y el próximo
    // refresh fallido disparará el logout local.
  }
}
