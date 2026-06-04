import { api } from './axios'

/**
 * Snapshot de KPIs del período — espejo de ReportsSummaryResponse C#.
 * El frontend pide TODO con una sola call para que el dashboard se arme
 * sin múltiples roundtrips.
 */
export interface ReportsSummary {
  from: string  // "YYYY-MM-DD"
  to: string

  totalRevenue: number
  appointmentsCount: number
  averageTicket: number
  newCustomersCount: number

  /** % vs período inmediatamente anterior. null si el anterior fue 0. */
  revenueChangePct: number | null

  topServices: TopServiceRow[]
  topStylists: TopStylistRow[]

  weeklyRevenue: WeeklyRevenuePoint[]

  newCustomerAppointments: number
  returningCustomerAppointments: number
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
}

export interface WeeklyRevenuePoint {
  weekStart: string  // "YYYY-MM-DD" (lunes)
  revenue: number
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
