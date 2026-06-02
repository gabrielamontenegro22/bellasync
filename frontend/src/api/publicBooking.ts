import axios from 'axios'

/**
 * Cliente HTTP para endpoints PÚBLICOS (sin auth). Usa una instancia
 * separada de axios para NO heredar el interceptor de Authorization
 * (que mete el JWT) — el portal anónimo no debe mandar token.
 */

const baseURL = import.meta.env.VITE_API_BASE_URL ?? ''

const publicApi = axios.create({
  baseURL,
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})

export interface PublicBookingRequest {
  stylistId: string
  serviceId: string
  startAtUtc: string
  clientName: string
  clientPhone: string
  clientEmail?: string
}

export interface PublicBookingResponse {
  appointmentId: string
  startAt: string
  serviceName: string
  stylistName: string
  priceSnapshot: number
  status: 'Pending' | 'Confirmed'
  requiresDeposit: boolean
  depositAmount: number
  holdExpiresAt: string | null
}

/** POST /api/PublicBooking/{tenantSlug} */
export async function publicBook(
  tenantSlug: string,
  req: PublicBookingRequest,
): Promise<PublicBookingResponse> {
  const { data } = await publicApi.post<PublicBookingResponse>(
    `/api/PublicBooking/${encodeURIComponent(tenantSlug)}`,
    req,
  )
  return data
}

// ===== Catálogo público (anónimo) =====

export interface PublicService {
  id: string
  name: string
  description: string | null
  category: string
  durationMinutes: number
  price: number
  color: string | null
  requiresDeposit: boolean
  depositPercentage: number
  depositAmount: number
}

export interface PublicStylist {
  id: string
  fullName: string
  role: string
  color: string | null
  serviceIds: string[]
}

/** GET /api/PublicBooking/{slug}/services — lista servicios activos del salón. */
export async function getPublicServices(tenantSlug: string): Promise<PublicService[]> {
  const { data } = await publicApi.get<PublicService[]>(
    `/api/PublicBooking/${encodeURIComponent(tenantSlug)}/services`,
  )
  return data
}

/** GET /api/PublicBooking/{slug}/stylists — lista estilistas disponibles del salón. */
export async function getPublicStylists(tenantSlug: string): Promise<PublicStylist[]> {
  const { data } = await publicApi.get<PublicStylist[]>(
    `/api/PublicBooking/${encodeURIComponent(tenantSlug)}/stylists`,
  )
  return data
}
