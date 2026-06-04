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

/* -------------------------------------------------------------------------- */
/*  Mi cuenta — endpoints autenticados                                         */
/* -------------------------------------------------------------------------- */

/** Snapshot del propio user. Devuelto por GET /api/Auth/me. */
export interface MyProfileResponse {
  id: string
  email: string
  fullName: string
  /** "SalonAdmin" | "Receptionist" | "Stylist" | "SuperAdmin" */
  role: string
  /** Null si es SuperAdmin (no pertenece a ningún salón). */
  tenantName: string | null
  createdAt: string
  lastLoginAt: string | null
}

/** GET /api/Auth/me — devuelve el perfil del user logueado actual. */
export async function getMyProfile(): Promise<MyProfileResponse> {
  const { data } = await api.get<MyProfileResponse>('/api/Auth/me')
  return data
}

/** PUT /api/Auth/me — actualiza el nombre completo. */
export async function updateMyProfile(payload: { fullName: string }): Promise<MyProfileResponse> {
  const { data } = await api.put<MyProfileResponse>('/api/Auth/me', payload)
  return data
}

/**
 * POST /api/Auth/change-password — cambia la contraseña del user logueado.
 * Side effect: revoca los refresh tokens en otros dispositivos. La sesión
 * actual sigue viva hasta que expire el access token (~15min) y el próximo
 * /refresh falle → ahí la UI fuerza re-login con la pass nueva.
 */
export async function changeMyPassword(payload: {
  currentPassword: string
  newPassword: string
}): Promise<void> {
  await api.post('/api/Auth/change-password', payload)
}
