import { api } from './axios'
import type { AppointmentResponse } from './appointments'

/** Tipos espejo de BellaSync.Application.Features.Customers.Dtos.CustomerResponse. */
export interface CustomerResponse {
  id: string
  fullName: string
  phone: string
  email: string | null
  birthday: string | null
  documentNumber: string | null
  address: string | null
  notes: string | null
  acceptsMarketing: boolean
  isActive: boolean
  createdAt: string
  updatedAt: string | null

  // Stats derivados (proyectados desde Appointments en el backend)
  /** Cantidad de citas Completed. */
  visits: number
  /** Última cita Completed (ISO 8601 UTC) o null. */
  lastVisitAt: string | null
  /** Próxima cita Pending/Confirmed futura (ISO 8601 UTC) o null. */
  nextVisitAt: string | null
  /** Estilista con más citas Completed o null. */
  preferredStylistName: string | null
  /** "VIP" | "Frecuente" | "Nuevo" | "Inactivo". Derivado en el backend. */
  tag: 'VIP' | 'Frecuente' | 'Nuevo' | 'Inactivo'
}

export interface PaginatedCustomers {
  items: CustomerResponse[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

/**
 * GET /api/Customers?search=&page=&pageSize=&includeInactive=
 * Búsqueda por nombre o teléfono (case-insensitive).
 */
export async function listCustomers(params: {
  search?: string
  page?: number
  pageSize?: number
  includeInactive?: boolean
} = {}): Promise<PaginatedCustomers> {
  const { data } = await api.get<PaginatedCustomers>('/api/Customers', { params })
  return data
}

/** Body del POST /api/Customers — espejo de CreateCustomerRequest del backend. */
export interface CreateCustomerRequest {
  fullName: string
  phone: string
  email?: string
  birthday?: string  // YYYY-MM-DD
  documentNumber?: string
  address?: string
  notes?: string
  acceptsMarketing?: boolean
}

/** POST /api/Customers — crea un cliente. */
export async function createCustomer(req: CreateCustomerRequest): Promise<CustomerResponse> {
  const { data } = await api.post<CustomerResponse>('/api/Customers', req)
  return data
}

/**
 * Body del PUT /api/Customers/{id}. Mismo shape que CreateCustomerRequest
 * + isActive (permite reactivar un cliente archivado).
 */
export interface UpdateCustomerRequest extends CreateCustomerRequest {
  isActive: boolean
}

/** GET /api/Customers/{id} */
export async function getCustomer(id: string): Promise<CustomerResponse> {
  const { data } = await api.get<CustomerResponse>(`/api/Customers/${id}`)
  return data
}

/** PUT /api/Customers/{id} */
export async function updateCustomer(id: string, req: UpdateCustomerRequest): Promise<CustomerResponse> {
  const { data } = await api.put<CustomerResponse>(`/api/Customers/${id}`, req)
  return data
}

/** DELETE /api/Customers/{id} — soft delete (marca isActive=false). */
export async function deleteCustomer(id: string): Promise<void> {
  await api.delete(`/api/Customers/${id}`)
}

/**
 * GET /api/Customers/{id}/appointments — historial completo de citas
 * (pasadas + futuras) ordenado desc por StartAt.
 */
export async function getCustomerAppointments(id: string): Promise<AppointmentResponse[]> {
  const { data } = await api.get<AppointmentResponse[]>(`/api/Customers/${id}/appointments`)
  return data
}
