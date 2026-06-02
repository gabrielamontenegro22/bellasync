import { api } from './axios'
import type { AuthResponse, LoginRequest, RegisterSalonRequest, RefreshTokenRequest } from '@/types/auth'

/**
 * Funciones que llaman a los endpoints de autenticación del backend.
 * Tipadas a partir de los DTOs del backend.
 */

/** POST /api/Auth/register-salon — crea Tenant + User admin en una operación. */
export async function registerSalon(payload: RegisterSalonRequest): Promise<AuthResponse> {
  const { data } = await api.post<AuthResponse>('/api/Auth/register-salon', payload)
  return data
}

/** POST /api/Auth/login — devuelve un JWT válido. */
export async function login(payload: LoginRequest): Promise<AuthResponse> {
  const { data } = await api.post<AuthResponse>('/api/Auth/login', payload)
  return data
}

/**
 * POST /api/Auth/refresh — intercambia un refresh token por un nuevo access+refresh.
 *
 * El interceptor de axios lo llama automáticamente cuando un request falla
 * con 401 y hay refresh token. NO debería llamarse manualmente desde la UI.
 */
export async function refreshAccessToken(payload: RefreshTokenRequest): Promise<AuthResponse> {
  const { data } = await api.post<AuthResponse>('/api/Auth/refresh', payload)
  return data
}

/**
 * POST /api/Auth/forgot-password — solicita un enlace de reseteo por email.
 *
 * El backend SIEMPRE responde 200 OK aunque el email no exista (para no
 * revelar qué emails están registrados). Por eso esta función nunca lanza
 * errores de "email no encontrado" — solo errores de red o validación.
 */
export async function forgotPassword(payload: { email: string }): Promise<void> {
  await api.post('/api/Auth/forgot-password', payload)
}

/**
 * POST /api/Auth/reset-password — guarda la nueva contraseña usando un token
 * recibido por email. Después del éxito el usuario debe loguearse.
 *
 * Lanza error si el token es inválido, expiró o ya fue usado (400).
 */
export async function resetPassword(payload: {
  token: string
  newPassword: string
}): Promise<void> {
  await api.post('/api/Auth/reset-password', payload)
}
