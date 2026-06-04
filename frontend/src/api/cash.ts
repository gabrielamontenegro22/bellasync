import { api } from './axios'
import type { ExpenseResponse } from './expenses'
import type { PaymentResponse, PaymentMethod } from './payments'

export interface ProviderBreakdownItem {
  /** "Bancolombia" / "Nequi" / "Visa" / null si no se especificó. */
  provider: string | null
  count: number
  total: number
}

export interface MethodBreakdownItem {
  /** "Cash" / "Transfer" / "Card" / "Other". */
  method: PaymentMethod | string
  count: number
  total: number
  /** Sub-desglose para cruzar con cada extracto bancario por separado. */
  byProvider: ProviderBreakdownItem[]
}

export interface DailyCashSummary {
  date: string  // YYYY-MM-DD
  totalAmount: number
  totalTips: number
  paymentCount: number
  byMethod: MethodBreakdownItem[]
  payments: PaymentResponse[]
  totalExpenses: number
  cashExpenses: number
  expenses: ExpenseResponse[]
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
