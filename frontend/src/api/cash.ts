import { api } from './axios'
import type { PaymentResponse, PaymentMethod } from './payments'

export interface MethodBreakdownItem {
  method: PaymentMethod
  count: number
  total: number
}

export interface DailyCashSummary {
  date: string  // YYYY-MM-DD
  totalAmount: number
  totalTips: number
  paymentCount: number
  byMethod: MethodBreakdownItem[]
  payments: PaymentResponse[]
}

/**
 * GET /api/Cash/daily-summary?date=YYYY-MM-DD
 * Resumen de caja del día (zona Colombia). Sin date = hoy.
 */
export async function getDailyCashSummary(date?: string): Promise<DailyCashSummary> {
  const { data } = await api.get<DailyCashSummary>('/api/Cash/daily-summary', {
    params: date ? { date } : {},
  })
  return data
}
