import { api } from './axios'

/**
 * API client de Estilistas — espeja StylistsController del backend.
 * Endpoints implementados:
 *  - GET    /api/Stylists                → listStylists()
 *  - GET    /api/Stylists/{id}           → getStylist()
 *  - POST   /api/Stylists                → createStylist()
 *  - PUT    /api/Stylists/{id}           → updateStylist()
 *  - DELETE /api/Stylists/{id}           → deleteStylist()
 */

/** Estado del estilista. Coincide con el enum StylistStatus del backend. */
export type StylistStatusEnum = 'Active' | 'Vacation' | 'Inactive'

/** Servicio asignado a un estilista (versión simplificada en StylistResponse). */
export interface StylistAssignedService {
  id: string
  name: string
  category: string
  durationMinutes: number
  price: number
}

/** DTO para crear un estilista nuevo. NO incluye Status (siempre arranca Active). */
export interface CreateStylistRequest {
  fullName: string
  role: string
  email?: string | null
  phone?: string | null
  idNumber?: string | null
  color?: string | null
  hireDate?: string | null  // ISO date "YYYY-MM-DD"
  serviceIds: string[]
}

/** DTO para editar un estilista. Permite cambiar Status. */
export interface UpdateStylistRequest extends CreateStylistRequest {
  status: StylistStatusEnum
}

/** Respuesta del backend para un estilista. */
export interface StylistResponse {
  id: string
  fullName: string
  role: string
  email: string | null
  phone: string | null
  idNumber: string | null
  color: string | null
  hireDate: string | null  // ISO date
  status: StylistStatusEnum
  userId: string | null
  services: StylistAssignedService[]
  createdAt: string
  updatedAt: string | null
}

/* -------------------------------------------------------------------------- */
/*  Funciones                                                                 */
/* -------------------------------------------------------------------------- */

/**
 * GET /api/Stylists
 * Por defecto solo trae los no-inactivos. Pasar `includeInactive=true` para incluirlos.
 */
export async function listStylists(includeInactive = false): Promise<StylistResponse[]> {
  const { data } = await api.get<StylistResponse[]>('/api/Stylists', {
    params: { includeInactive },
  })
  return data
}

/** GET /api/Stylists/{id} */
export async function getStylist(id: string): Promise<StylistResponse> {
  const { data } = await api.get<StylistResponse>(`/api/Stylists/${id}`)
  return data
}

/** POST /api/Stylists */
export async function createStylist(payload: CreateStylistRequest): Promise<StylistResponse> {
  const { data } = await api.post<StylistResponse>('/api/Stylists', payload)
  return data
}

/** PUT /api/Stylists/{id} */
export async function updateStylist(
  id: string,
  payload: UpdateStylistRequest,
): Promise<StylistResponse> {
  const { data } = await api.put<StylistResponse>(`/api/Stylists/${id}`, payload)
  return data
}

/**
 * DELETE /api/Stylists/{id} — soft delete (marca Status=Inactive).
 * Idempotente: si ya estaba inactivo, no falla.
 */
export async function deleteStylist(id: string): Promise<void> {
  await api.delete(`/api/Stylists/${id}`)
}

/* -------------------------------------------------------------------------- */
/*  Vacaciones / días libres (StylistTimeOff)                                  */
/* -------------------------------------------------------------------------- */

export interface StylistTimeOff {
  id: string
  stylistId: string
  fromDate: string  // "YYYY-MM-DD"
  toDate: string
  reason: string | null
  isPast: boolean
  createdAt: string
}

export interface AddTimeOffRequest {
  fromDate: string  // "YYYY-MM-DD"
  toDate: string
  reason?: string | null
}

export interface AffectedAppointment {
  appointmentId: string
  customerName: string
  customerPhone: string | null
  serviceName: string
  startAt: string  // ISO UTC
  endAt: string
  status: string
}

export async function listStylistTimeOffs(stylistId: string): Promise<StylistTimeOff[]> {
  const { data } = await api.get<StylistTimeOff[]>(
    `/api/Stylists/${stylistId}/time-off`,
  )
  return data
}

export async function addStylistTimeOff(
  stylistId: string,
  req: AddTimeOffRequest,
): Promise<StylistTimeOff> {
  const { data } = await api.post<StylistTimeOff>(
    `/api/Stylists/${stylistId}/time-off`,
    req,
  )
  return data
}

export async function removeStylistTimeOff(timeOffId: string): Promise<void> {
  await api.delete(`/api/Stylists/time-off/${timeOffId}`)
}

export async function getAffectedAppointments(
  stylistId: string,
  fromDate: string,
  toDate: string,
): Promise<AffectedAppointment[]> {
  const { data } = await api.get<AffectedAppointment[]>(
    `/api/Stylists/${stylistId}/affected-appointments`,
    { params: { from: fromDate, to: toDate } },
  )
  return data
}
