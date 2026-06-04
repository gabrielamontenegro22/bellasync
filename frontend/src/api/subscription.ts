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

  /** Factura reportada esperando validación del SuperAdmin. null si no hay. */
  pendingValidationInvoice: InvoiceRow | null

  /** Razón del último rechazo del SuperAdmin (si la última acción fue rechazo). */
  lastRejectionReason: string | null
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
  /** "Pending" | "Reported" | "Paid" | "Failed" | "Waived". */
  status: string
  paidAt: string | null
  paymentMethod: string | null
  reference: string | null
  note: string | null

  // Reporte (paso intermedio antes de validación)
  reportedAt: string | null
  reportedMethod: string | null
  reportedReference: string | null
  rejectedAt: string | null
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

export interface ReportPaymentRequest {
  paymentMethod: string
  reference?: string | null
}

/**
 * La admin del salón reporta una transferencia que dice haber hecho.
 * El backend emite la factura si no existe + la pone en estado
 * Reported. La suscripción NO se activa hasta que el SuperAdmin
 * de BellaSync valide contra el extracto bancario.
 */
export async function reportPayment(req: ReportPaymentRequest): Promise<Subscription> {
  const { data } = await api.post<Subscription>(
    '/api/Subscription/report-payment',
    req,
  )
  return data
}

/**
 * La admin cancela su suscripción. El backend rechaza si hay un pago
 * Reported pendiente (esperar la decisión del SuperAdmin primero).
 */
export async function cancelSubscription(reason?: string): Promise<Subscription> {
  const { data } = await api.post<Subscription>('/api/Subscription/cancel', { reason })
  return data
}
