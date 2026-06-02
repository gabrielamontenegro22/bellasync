import { api } from './axios'

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
