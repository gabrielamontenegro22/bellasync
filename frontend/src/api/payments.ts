import { api } from './axios'

/**
 * Tipos espejo de BellaSync.Application.Features.Payments.Dtos.
 *
 * Recordatorio: PaymentVoucher (anticipo online que se valida) y Payment
 * (pago en sitio al terminar) son entidades distintas. Acá solo el segundo.
 */

/**
 * 3 categorías top + escape "Other". El detalle del banco/billetera/marca
 * vive aparte en `provider` (string libre dentro de una lista predefinida
 * por el frontend; el backend solo valida que si method=Transfer entonces
 * provider debe venir).
 */
export type PaymentMethod = 'Cash' | 'Transfer' | 'Card' | 'Other'

export interface PaymentResponse {
  id: string
  appointmentId: string
  method: PaymentMethod
  /** Banco ("Bancolombia"), billetera ("Nequi") o marca ("Visa"). null para Cash. */
  provider: string | null
  amount: number
  tip: number
  total: number  // amount + tip (conveniencia del DTO)
  reference: string | null
  registeredByUserId: string | null
  /** Nombre del user que cobró ("María González"). Null si fue automático. */
  registeredByUserName: string | null
  registeredAt: string  // ISO
  // snapshot mínimo del contexto de la cita
  customerName: string
  serviceName: string
  stylistName: string
  appointmentStartAt: string
}

export interface RegisterPaymentRequest {
  method: PaymentMethod
  /** Obligatorio si method=Transfer; opcional para Card; null para Cash. */
  provider?: string | null
  amount: number
  tip: number
  reference?: string | null
}

/**
 * POST /api/Appointments/{appointmentId}/payments
 * Registra un pago para una cita InProgress o Completed.
 */
export async function registerPayment(
  appointmentId: string,
  req: RegisterPaymentRequest,
): Promise<PaymentResponse> {
  const { data } = await api.post<PaymentResponse>(
    `/api/Appointments/${appointmentId}/payments`,
    req,
  )
  return data
}

/**
 * GET /api/Customers/{customerId}/payments
 * Historial completo de pagos del cliente — para el tab "Pagos" del CRM.
 */
export async function getCustomerPayments(customerId: string): Promise<PaymentResponse[]> {
  const { data } = await api.get<PaymentResponse[]>(`/api/Customers/${customerId}/payments`)
  return data
}
