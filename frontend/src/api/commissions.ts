import { api } from './axios'

/** Una fila por estilista en el resumen. */
export interface StylistCommissionRow {
  stylistId: string
  stylistName: string
  stylistColor: string | null
  paymentsCount: number
  cobradoTotal: number
  commissionEarned: number
  alreadyPaidInRange: number
  pending: number
}

export interface CommissionsSummary {
  from: string
  to: string
  stylists: StylistCommissionRow[]
  totalEarned: number
  totalPaid: number
  totalPending: number
}

export interface CommissionPayout {
  id: string
  stylistId: string
  stylistName: string
  amount: number
  periodFrom: string
  periodTo: string
  paidAt: string
  paidByUserId: string | null
  notes: string | null
}

export interface CreatePayoutRequest {
  stylistId: string
  amount: number
  periodFrom: string
  periodTo: string
  notes?: string | null
}

/**
 * GET /api/Commissions/summary
 * Sin params = este mes hasta hoy (Colombia).
 */
export async function getCommissionsSummary(
  from?: string,
  to?: string,
): Promise<CommissionsSummary> {
  const { data } = await api.get<CommissionsSummary>('/api/Commissions/summary', {
    params: { from, to },
  })
  return data
}

export async function createCommissionPayout(
  req: CreatePayoutRequest,
): Promise<CommissionPayout> {
  const { data } = await api.post<CommissionPayout>('/api/Commissions/payouts', req)
  return data
}

export async function listCommissionPayouts(
  from?: string,
  to?: string,
): Promise<CommissionPayout[]> {
  const { data } = await api.get<CommissionPayout[]>('/api/Commissions/payouts', {
    params: { from, to },
  })
  return data
}
