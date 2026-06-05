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
  /**
   * Suma de los vouchers Validated (anticipos online validados) para esta
   * cita. Es el dinero que realmente entró al banco por anticipo. Se usa
   * para calcular "lo que falta cobrar en sitio" = priceSnapshot - este.
   */
  validatedDepositAmount: number
  /**
   * Suma de Payment registrados directamente (cobros en sitio). NO incluye
   * anticipos online. El modal de cancelar lo usa para saber si la cita
   * tiene dinero asociado y exigir motivo obligatorio.
   */
  directPaymentsTotal: number
  status: AppointmentStatus
  depositStatus: AppointmentDepositStatus
  channel: AppointmentChannel
  holdExpiresAt: string | null
  notes: string | null
  cancelledAt: string | null
  cancellationReason: string | null
  /** Nombre del user que canceló. Null si fue automático (hold/voucher rechazo). */
  cancelledByUserName: string | null
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
  /**
   * Saltar la regla de "al menos 30 min de anticipación". Pensado para
   * walk-ins. El backend solo lo respeta si el JWT tiene rol SalonAdmin;
   * Receptionist lo manda y queda silenciosamente ignorado.
   */
  bypassAdvanceWindow?: boolean
  /**
   * Vouchers CreditPending del cliente a aplicar como anticipo de esta
   * cita. El backend valida que la suma cubra el anticipo requerido y
   * consume los vouchers FIFO. Si vienen los IDs pero el crédito no
   * alcanza, rechaza con código "credit.insufficient".
   */
  applyCreditFromVoucherIds?: string[]
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

/**
 * Decisión sobre el anticipo cuando se cancela una cita con voucher
 * Validado. Espejo del enum DepositRefundDecision del dominio:
 *  - 'Refunded'      → admin va a devolver la plata (queda en pendientes
 *                      hasta que marque la transferencia como hecha).
 *  - 'CreditPending' → la cliente reagenda y el anticipo se aplica a la
 *                      próxima cita.
 *  - 'Forfeited'     → el salón se queda con el anticipo (cancelación
 *                      tardía, política estricta).
 *
 * Si la admin/recepción NO manda este campo, el backend aplica la regla
 * automática según la ventana del salón (Refunded dentro, Forfeited fuera).
 */
export type DepositRefundDecision = 'Refunded' | 'CreditPending' | 'Forfeited'

export interface CancelAppointmentRequest {
  reason?: string
  /**
   * Override explícito de la decisión de refund. Mandar undefined deja
   * que el backend aplique la regla automática. Recepción solo puede
   * mandarlo si la admin activó `canRefundDeposit` — si no, el backend
   * responde 403.
   */
  depositOverride?: DepositRefundDecision
}

export async function cancelAppointment(
  id: string,
  req: CancelAppointmentRequest = {},
): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>(
    `/api/Appointments/${id}/cancel`,
    req,
  )
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

export interface RescheduleAppointmentRequest {
  newStartAtUtc: string  // ISO
  /** Saltar la regla de 30 min. Backend la silencia si el rol no es SalonAdmin. */
  bypassAdvanceWindow?: boolean
}

/**
 * POST /api/Appointments/{id}/reschedule
 * Cambia el slot de una cita Pending/Confirmed. Mismo stylist/service/customer.
 */
export async function rescheduleAppointment(
  id: string,
  req: RescheduleAppointmentRequest,
): Promise<AppointmentResponse> {
  const { data } = await api.post<AppointmentResponse>(
    `/api/Appointments/${id}/reschedule`,
    req,
  )
  return data
}
