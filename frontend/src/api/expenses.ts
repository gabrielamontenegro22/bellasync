import { api } from './axios'
import type { PaymentMethod } from './payments'

/**
 * Egreso (gasto) del día del salón. Plata que SALE — opuesto del Payment.
 * Tipos espejo de BellaSync.Application.Features.Expenses.Dtos.
 */
export interface ExpenseResponse {
  id: string
  concept: string
  amount: number
  method: PaymentMethod | string
  /** Banco/billetera/marca cuando aplica. */
  provider: string | null
  registeredByUserId: string | null
  /** Nombre del user que registró el egreso (para auditoría en /caja). */
  registeredByUserName: string | null
  registeredAt: string  // ISO
}

export interface RegisterExpenseRequest {
  concept: string
  amount: number
  /** Default 'Cash' si no se manda (caso típico). */
  method?: PaymentMethod
  /** Banco/billetera/marca. Obligatorio si method=Transfer. */
  provider?: string | null
}

/**
 * POST /api/Expenses
 * Registra un egreso del día.
 */
export async function registerExpense(
  req: RegisterExpenseRequest,
): Promise<ExpenseResponse> {
  const { data } = await api.post<ExpenseResponse>('/api/Expenses', {
    method: 'Cash',
    ...req,
  })
  return data
}

/**
 * GET /api/Expenses?date=YYYY-MM-DD
 * Lista los egresos de un día. Sin date = hoy.
 */
export async function getDailyExpenses(date?: string): Promise<ExpenseResponse[]> {
  const { data } = await api.get<ExpenseResponse[]>('/api/Expenses', {
    params: date ? { date } : {},
  })
  return data
}
