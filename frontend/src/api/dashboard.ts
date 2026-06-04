import { api } from './axios'

/**
 * Snapshot que arma el home tras login + alimenta los badges del sidebar.
 * Una sola call cubre las dos necesidades.
 */
export interface DashboardSummary {
  today: string              // "YYYY-MM-DD"
  todayAppointmentsCount: number
  todayCompletedCount: number
  todayPendingCount: number
  todayRevenue: number

  nextAppointment: NextAppointmentDto | null

  weekAppointmentsCount: number
  weekRevenue: number

  /** Vouchers Pending — sirve para el badge "Validación (N)" del sidebar. */
  pendingVouchersCount: number

  /** True si hubo pagos hoy pero la caja todavía no se cerró. */
  cashClosingPending: boolean
}

export interface NextAppointmentDto {
  id: string
  customerName: string
  serviceName: string
  stylistName: string
  stylistColor: string | null
  startAt: string  // ISO
  endAt: string
  status: string
}

export async function getDashboardSummary(): Promise<DashboardSummary> {
  const { data } = await api.get<DashboardSummary>('/api/Dashboard/summary')
  return data
}
