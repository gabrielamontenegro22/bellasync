import { api } from './axios'

/**
 * Tipos espejo de los DTOs del backend (BellaSync.Application.Features.Appointments).
 */

export type AppointmentStatus =
  | 'Pending' | 'Confirmed' | 'InProgress' | 'Completed' | 'Cancelled' | 'NoShow'

export type AppointmentDepositStatus =
  | 'NotRequired' | 'AwaitingPayment' | 'Validated'

export type AppointmentChannel = 'Reception' | 'PublicPortal'

export interface AppointmentResponse {
  id: string
  customerId: string
  customerName: string
  customerPhone: string
  stylistId: string
  stylistName: string
  stylistColor: string | null
  serviceId: string
  serviceName: string
  serviceCategory: string
  durationMinutes: number
  serviceColor: string | null
  startAt: string   // ISO
  endAt: string
  priceSnapshot: number
  depositPercentage: number
  depositAmount: number
  status: AppointmentStatus
  depositStatus: AppointmentDepositStatus
  channel: AppointmentChannel
  holdExpiresAt: string | null
  notes: string | null
  cancelledAt: string | null
  cancellationReason: string | null
  startedAt: string | null
  completedAt: string | null
  createdAt: string
  updatedAt: string | null
}

export interface AgendaMetrics {
  total: number
  pendingValidation: number
  confirmed: number
  noShow: number
}

export interface AgendaResponse {
  date: string  // YYYY-MM-DD
  metrics: AgendaMetrics
  appointments: AppointmentResponse[]
}

export interface CreateAppointmentRequest {
  customerId: string
  stylistId: string
  serviceId: string
  startAtUtc: string  // ISO
  notes?: string | null
}

/** GET /api/Appointments?date=YYYY-MM-DD[&stylistId=guid] */
export async function getAgenda(date: string, stylistId?: string): Promise<AgendaResponse> {
  const params: Record<string, string> = { date }
  if (stylistId) params.stylistId = stylistId
  const { data } = await api.get<AgendaResponse>('/api/Appointments', { params })
  return data
}

export async function getAppointment(id: string): Promise<AppointmentResponse> {
  const { data } = await api.get<AppointmentResponse>(`/api/Appointments/${id}`)
  return data
}

export async function createAppointment(req: CreateAppointmentRequest): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>('/api/Appointments', req)
  return data
}

export async function confirmAppointment(id: string): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>(`/api/Appointments/${id}/confirm`)
  return data
}

export async function cancelAppointment(id: string, reason?: string): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>(`/api/Appointments/${id}/cancel`, { reason })
  return data
}

export async function startAppointment(id: string): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>(`/api/Appointments/${id}/start`)
  return data
}

export async function completeAppointment(id: string): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>(`/api/Appointments/${id}/complete`)
  return data
}

export async function markNoShow(id: string): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>(`/api/Appointments/${id}/no-show`)
  return data
}
