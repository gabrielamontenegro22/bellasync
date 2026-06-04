import { api } from './axios'

/**
 * Espejo de SubscriptionResponse C#. Una sola call trae todo lo que la
 * pantalla /configuracion/suscripcion necesita: plan actual, fechas,
 * historial, catálogo de planes para el modal "Cambiar plan" y la
 * próxima factura pendiente para el botón "Pagar ahora".
 */
export interface Subscription {
  planCode: string
  planName: string
  planTagline: string
  monthlyPrice: number
  features: string[]

  /** "Trial" | "Active" | "PastDue" | "Cancelled". */
  status: string

  startedAt: string             // ISO UTC
  currentPeriodEnd: string      // ISO UTC
  trialEndsAt: string | null
  cancelledAt: string | null

  /** Días hasta el próximo cobro (negativo = vencido). */
  daysUntilNextCharge: number

  /** True si Trial y faltan ≤3 días. */
  trialEndingSoon: boolean

  availablePlans: PlanOption[]
  invoices: InvoiceRow[]
  nextDueInvoice: InvoiceRow | null
}

export interface PlanOption {
  code: string
  name: string
  tagline: string
  monthlyPrice: number
  features: string[]
  isHighlighted: boolean
  isCurrent: boolean
}

export interface InvoiceRow {
  id: string
  planCode: string
  planName: string
  amount: number
  periodStart: string  // ISO UTC
  periodEnd: string
  dueDate: string
  issuedAt: string
  /** "Pending" | "Paid" | "Failed" | "Waived". */
  status: string
  paidAt: string | null
  paymentMethod: string | null
  reference: string | null
  note: string | null
}

// ───────────────────────────────────────────────────────────────────────
// Endpoints
// ───────────────────────────────────────────────────────────────────────

export async function getSubscription(): Promise<Subscription> {
  const { data } = await api.get<Subscription>('/api/Subscription')
  return data
}

export async function changePlan(planCode: string): Promise<Subscription> {
  const { data } = await api.post<Subscription>(
    '/api/Subscription/change-plan',
    { planCode },
  )
  return data
}

export interface PayInvoiceRequest {
  paymentMethod: string
  reference?: string | null
}

export async function payInvoice(
  invoiceId: string,
  req: PayInvoiceRequest,
): Promise<Subscription> {
  const { data } = await api.post<Subscription>(
    `/api/Subscription/invoices/${invoiceId}/pay`,
    req,
  )
  return data
}

/**
 * "Pagar suscripción ahora" inteligente:
 *   - Si hay una factura Pending, la paga.
 *   - Si no hay, emite una para el período actual y la paga atómicamente.
 *
 * El frontend siempre llama a este endpoint sin preocuparse por el estado
 * actual — funciona para Trial (primera activación), Active (renovación),
 * PastDue (regularización).
 */
export async function paySubscription(req: PayInvoiceRequest): Promise<Subscription> {
  const { data } = await api.post<Subscription>('/api/Subscription/pay', req)
  return data
}
