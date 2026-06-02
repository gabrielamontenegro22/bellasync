import axios, { AxiosError, AxiosHeaders, type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios'
import { authStorage } from '@/features/auth/storage'

/**
 * Cliente HTTP único para BellaSync API.
 *
 *  - baseURL viene de VITE_API_BASE_URL (frontend/.env)
 *  - Interceptor de request: inyecta `Authorization: Bearer <jwt>` si hay sesión
 *  - Interceptor de response: si llega 401 fuera de los flujos de auth, intenta
 *    refrescar el access token con el refresh token. Si el refresh funciona,
 *    reintenta el request original. Si falla (refresh expirado o revocado),
 *    limpia la sesión y emite el evento de logout para que el AuthContext
 *    redirija al login.
 */

const baseURL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5059'

export const api = axios.create({
  baseURL,
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
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

/** Marker para no reintentar un request más de una vez. */
interface RetryableConfig extends AxiosRequestConfig {
  _retry?: boolean
}

/**
 * Estado del refresh en curso: si llegan N requests en paralelo y todos dan
 * 401 al mismo tiempo, solo lanzamos UN refresh. Los demás esperan a su
 * resolución (compartiendo la misma promesa).
 */
let refreshInFlight: Promise<string | null> | null = null

function isAuthEndpoint(url: string | undefined): boolean {
  if (!url) return false
  return url.includes('/Auth/login')
      || url.includes('/Auth/register-salon')
      || url.includes('/Auth/refresh')
      || url.includes('/Auth/forgot-password')
      || url.includes('/Auth/reset-password')
}

async function performRefresh(): Promise<string | null> {
  const refreshToken = authStorage.getRefreshToken()
  if (!refreshToken) return null

  try {
    // Call directo (sin pasar por la instancia `api` para no recursar
    // en este mismo interceptor si el refresh también da 401).
    const resp = await axios.post<{ token: string; refreshToken: string }>(
      `${baseURL}/api/Auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' }, timeout: 15_000 },
    )
    authStorage.updateTokens(resp.data.token, resp.data.refreshToken)
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

    // 401 en endpoints de auth → no intentamos refresh; los login/register
    // legítimos pueden devolver 401 por credenciales malas.
    if (status !== 401 || isAuthEndpoint(url)) {
      return Promise.reject(error)
    }

    // Ya reintentamos este request una vez — el refresh falló.
    if (original?._retry) {
      authStorage.clear()
      window.dispatchEvent(new Event(AUTH_LOGOUT_EVENT))
      return Promise.reject(error)
    }

    // Coalesce: si ya hay un refresh en curso, esperamos a esa promesa.
    refreshInFlight ??= performRefresh().finally(() => { refreshInFlight = null })
    const newToken = await refreshInFlight

    if (!newToken || !original) {
      authStorage.clear()
      window.dispatchEvent(new Event(AUTH_LOGOUT_EVENT))
      return Promise.reject(error)
    }

    // Reintentar el request original con el access token nuevo
    original._retry = true
    original.headers = new AxiosHeaders(original.headers)
    ;(original.headers as AxiosHeaders).set('Authorization', `Bearer ${newToken}`)
    return api(original)
  },
)
