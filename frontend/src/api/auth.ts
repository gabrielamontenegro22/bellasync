import { api } from './axios'
import type { AuthResponse, LoginRequest, RegisterSalonRequest } from '@/types/auth'

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
