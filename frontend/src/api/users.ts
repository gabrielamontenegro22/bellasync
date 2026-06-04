import { api } from './axios'

/**
 * Espejo de UserResponse C#. Lo que ve la admin en
 * Configuración → Usuarios.
 */
export interface User {
  id: string
  email: string
  fullName: string
  /** "SalonAdmin" | "Receptionist" | "Stylist" | "SuperAdmin". */
  role: string
  isActive: boolean
  createdAt: string
  lastLoginAt: string | null
}

export interface CreateUserRequest {
  email: string
  fullName: string
  password: string
  /** "SalonAdmin" o "Receptionist". */
  role: string
}

export interface UpdateUserRequest {
  fullName: string
  role: string
}

export async function listUsers(): Promise<User[]> {
  const { data } = await api.get<User[]>('/api/Users')
  return data
}

export async function createUser(req: CreateUserRequest): Promise<User> {
  const { data } = await api.post<User>('/api/Users', req)
  return data
}

export async function updateUser(id: string, req: UpdateUserRequest): Promise<User> {
  const { data } = await api.put<User>(`/api/Users/${id}`, req)
  return data
}

export async function archiveUser(id: string): Promise<void> {
  await api.post(`/api/Users/${id}/archive`)
}

export async function reactivateUser(id: string): Promise<void> {
  await api.post(`/api/Users/${id}/reactivate`)
}
