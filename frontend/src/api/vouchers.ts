import { api } from './axios'

export type VoucherStatus = 'Pending' | 'Validated' | 'Rejected' | 'NeedsClarification'
export type VoucherUrgency = 'urgent' | 'tomorrow' | 'week'
export type VoucherDecision = 'Confirm' | 'Reject' | 'RequestClarification'

export interface VoucherResponse {
  id: string
  appointmentId: string
  customerName: string
  customerPhone: string
  serviceName: string
  stylistName: string
  appointmentStartAt: string
  appointmentDepositAmount: number
  /** Precio total del servicio (priceSnapshot). Para mostrar "X% de $Y". */
  appointmentTotalServicePrice: number
  reportedAmount: number
  bank: string | null
  referenceNumber: string | null
  senderName: string | null
  senderPhone: string | null
  imageUrl: string | null
  receivedAt: string
  status: VoucherStatus
  urgency: VoucherUrgency
  decidedAt: string | null
  /** Nombre del user que validó/rechazó. Para mostrar "por X" en la cola. */
  decidedByUserName: string | null
  decisionNotes: string | null
}

export interface CreateVoucherRequest {
  appointmentId: string
  reportedAmount: number
  bank?: string
  referenceNumber?: string
  senderName?: string
  senderPhone?: string
  imageUrl?: string
}

/** GET /api/Vouchers/pending */
export async function listPendingVouchers(): Promise<VoucherResponse[]> {
  const { data } = await api.get<VoucherResponse[]>('/api/Vouchers/pending')
  return data
}

/** POST /api/Vouchers (recepción crea manualmente / o webhook futuro) */
export async function createVoucher(req: CreateVoucherRequest): Promise<VoucherResponse> {
  const { data } = await api.post<VoucherResponse>('/api/Vouchers', req)
  return data
}

/** POST /api/Vouchers/{id}/validate con decisión */
export async function validateVoucher(
  id: string,
  decision: VoucherDecision,
  notes?: string,
): Promise<VoucherResponse> {
  const { data } = await api.post<VoucherResponse>(`/api/Vouchers/${id}/validate`, {
    decision,
    notes,
  })
  return data
}

/* -------------------------------------------------------------------------- */
/*  Refunds pendientes (anticipos a devolver/aplicar después de cancelaciones)*/
/* -------------------------------------------------------------------------- */

export type RefundDecision = 'Refunded' | 'CreditPending'

/** Espejo de PendingRefundResponse del backend. */
export interface PendingRefund {
  voucherId: string
  appointmentId: string
  customerName: string
  customerPhone: string
  serviceName: string
  stylistName: string
  /** Fecha+hora original de la cita cancelada (ISO). */
  appointmentStartAt: string
  /** Monto del anticipo a devolver / aplicar. */
  amount: number
  /** Banco que reportó la cliente al pagar. Para identificar la transferencia. */
  bank: string | null
  /** Cuándo se canceló la cita (= cuándo se decidió el refund). */
  cancelledAt: string
  /** Motivo de la cancelación que escribió el operador. */
  cancellationReason: string | null
  /** "Refunded" o "CreditPending". Forfeited no aparece acá. */
  decision: RefundDecision
}

/**
 * GET /api/Vouchers/pending-refunds
 * Devoluciones de anticipo todavía sin resolver.
 */
export async function listPendingRefunds(): Promise<PendingRefund[]> {
  const { data } = await api.get<PendingRefund[]>('/api/Vouchers/pending-refunds')
  return data
}

/**
 * POST /api/Vouchers/{id}/mark-refunded
 * Admin marca el refund como resuelto (ya hizo la transferencia bancaria).
 */
export async function markRefundResolved(voucherId: string): Promise<PendingRefund> {
  const { data } = await api.post<PendingRefund>(
    `/api/Vouchers/${voucherId}/mark-refunded`,
  )
  return data
}
