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

// ===== Subir voucher (comprobante de anticipo) =====

export interface UploadVoucherRequest {
  file: File
  reportedAmount: number
  bank?: string
  referenceNumber?: string
  senderName?: string
  senderPhone?: string
}

export interface UploadVoucherResponse {
  id: string
  status: string
  reportedAmount: number
}

/**
 * POST /api/PublicBooking/{slug}/appointments/{id}/voucher
 * Multipart: file + metadata. Anonymous (portal público).
 * Devuelve el voucher creado en estado Pending.
 */
export async function uploadPublicVoucher(
  tenantSlug: string,
  appointmentId: string,
  req: UploadVoucherRequest,
): Promise<UploadVoucherResponse> {
  const form = new FormData()
  form.append('file', req.file)
  form.append('reportedAmount', String(req.reportedAmount))
  if (req.bank) form.append('bank', req.bank)
  if (req.referenceNumber) form.append('referenceNumber', req.referenceNumber)
  if (req.senderName) form.append('senderName', req.senderName)
  if (req.senderPhone) form.append('senderPhone', req.senderPhone)

  const { data } = await publicApi.post<UploadVoucherResponse>(
    `/api/PublicBooking/${encodeURIComponent(tenantSlug)}/appointments/${appointmentId}/voucher`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  )
  return data
}
