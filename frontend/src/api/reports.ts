import { api } from './axios'

/**
 * Snapshot de KPIs del período — espejo de ReportsSummaryResponse C#.
 * El frontend pide TODO con una sola call para que el dashboard se arme
 * sin múltiples roundtrips.
 *
 * v2: agrega tasa de no-show, breakdown por método de pago, embudo,
 * daily revenue, ocupación + noShows por estilista, insight dinámico.
 */
export interface ReportsSummary {
  from: string  // "YYYY-MM-DD"
  to: string

  // KPIs
  totalRevenue: number
  appointmentsCount: number
  averageTicket: number
  noShowRate: number          // 0–100
  newCustomersCount: number

  // Deltas (% o pts según corresponda)
  revenueChangePct: number | null
  appointmentsChangePct: number | null
  averageTicketChangePct: number | null
  /** Cambio en puntos (no %): negativo = mejoró. */
  noShowChangePts: number | null
  newCustomersChangePct: number | null

  topServices: TopServiceRow[]
  topStylists: TopStylistRow[]

  dailyRevenue: DailyRevenuePoint[]
  paymentMethodBreakdown: PaymentMethodRow[]

  funnel: FunnelStats

  newCustomerAppointments: number
  returningCustomerAppointments: number

  insightEyebrow: string
  insightText: string | null
}

export interface TopServiceRow {
  serviceId: string
  serviceName: string
  appointmentsCount: number
  revenue: number
}

export interface TopStylistRow {
  stylistId: string
  stylistName: string
  stylistColor: string | null
  appointmentsCount: number
  revenue: number
  occupancyPct: number
  noShowCount: number
}

export interface DailyRevenuePoint {
  date: string  // "YYYY-MM-DD"
  revenue: number
}

export interface PaymentMethodRow {
  method: string  // "Cash" | "Transfer" | "Card" | "Other"
  label: string   // "Efectivo" | "Transferencia" | ...
  revenue: number
  percentage: number
}

export interface FunnelStats {
  requested: number
  confirmed: number
  attended: number
  noShow: number
}

/**
 * GET /api/Reports/summary?from=YYYY-MM-DD&to=YYYY-MM-DD
 */
export async function getReportsSummary(
  from: string,
  to: string,
): Promise<ReportsSummary> {
  const { data } = await api.get<ReportsSummary>('/api/Reports/summary', {
    params: { from, to },
  })
  return data
}
