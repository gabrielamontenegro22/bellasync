import axios, { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios'
import { authStorage } from '@/features/auth/storage'

/**
 * Cliente HTTP único para BellaSync API.
 *
 *  - baseURL viene de VITE_API_BASE_URL (frontend/.env)
 *  - Interceptor de request: inyecta `Authorization: Bearer <jwt>` si hay sesión
 *  - Interceptor de response: si llega 401 fuera de los flujos de auth,
 *    limpia la sesión y emite un evento global que el AuthContext escucha
 *    para hacer logout automático.
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

// ---------- response interceptor ----------
/**
 * Evento global que el AuthContext escucha para forzar logout
 * cuando el backend rechaza por sesión expirada / token inválido.
 */
export const AUTH_LOGOUT_EVENT = 'bellasync:auth:logout'

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    const status = error.response?.status
    const url = error.config?.url ?? ''
    const isLoginFlow = url.includes('/Auth/login') || url.includes('/Auth/register-salon')

    // 401 fuera de login/register => sesión expirada o token revocado
    if (status === 401 && !isLoginFlow) {
      authStorage.clear()
      window.dispatchEvent(new Event(AUTH_LOGOUT_EVENT))
    }

    return Promise.reject(error)
  },
)
