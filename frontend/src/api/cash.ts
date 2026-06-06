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

/**
 * Item de "Anticipos retenidos por cancelación tardía" — un voucher cuya
 * cita se canceló con decisión Forfeited (No devolver). El salón se quedó
 * con esa plata por política.
 */
export interface ForfeitedItem {
  voucherId: string
  customerName: string
  serviceName: string
  amount: number
  /** Cuándo era la cita cancelada (ISO). */
  appointmentStartAt: string
  /** Cuándo se canceló la cita (ISO). */
  cancelledAt: string
  cancellationReason: string | null
}

export interface DailyCashSummary {
  date: string  // YYYY-MM-DD
  /** Plata REAL que entró hoy (Payments + vouchers externos). Excluye crédito interno. */
  totalAmount: number
  totalTips: number
  /** Cantidad de movimientos visibles (pagos + vouchers externos). */
  paymentCount: number
  /** Sub-total de anticipos validados hoy. */
  validatedDepositsTotal: number
  validatedDepositsCount: number
  /** Crédito interno aplicado hoy (saldo viejo consumido). NO es plata nueva. */
  internalCreditTotal: number
  internalCreditCount: number
  /** Anticipos retenidos hoy por cancelación tardía. Ingreso "ganado" por política. */
  forfeitedTodayTotal: number
  forfeitedTodayCount: number
  forfeitedToday: ForfeitedItem[]
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

// ───────────────────────────────────────────────────────────────────────
// Cierres persistidos
// ───────────────────────────────────────────────────────────────────────

export interface CashClosing {
  id: string
  closedDate: string  // YYYY-MM-DD
  baseAmount: number
  cashSales: number
  cashExpenses: number
  expectedCash: number
  countedCash: number
  /** counted − expected. Negativo=faltó, positivo=sobró, 0=cuadró. */
  diff: number
  diffNote: string | null
  totalAmount: number
  closedAt: string  // ISO
  closedByUserId: string | null
  /** Nombre del user que firmó el cierre (historial). */
  closedByUserName: string | null
}

export interface CreateCashClosingRequest {
  closedDate?: string | null
  baseAmount: number
  countedCash: number
  diffNote?: string | null
}

export async function createCashClosing(
  req: CreateCashClosingRequest,
): Promise<CashClosing> {
  const { data } = await api.post<CashClosing>('/api/Cash/closings', req)
  return data
}

export async function listCashClosings(
  from?: string,
  to?: string,
): Promise<CashClosing[]> {
  const { data } = await api.get<CashClosing[]>('/api/Cash/closings', {
    params: { from, to },
  })
  return data
}

/**
 * Devuelve el cierre del día indicado, o null si todavía no se ha cerrado.
 * Usa 404 del backend como señal de "no existe" (no es un error).
 */
export async function getCashClosingForDate(date: string): Promise<CashClosing | null> {
  try {
    const { data } = await api.get<CashClosing>(`/api/Cash/closings/by-date/${date}`)
    return data
  } catch (e: any) {
    if (e?.response?.status === 404) return null
    throw e
  }
}
