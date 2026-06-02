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
