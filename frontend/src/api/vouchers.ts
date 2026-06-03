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
