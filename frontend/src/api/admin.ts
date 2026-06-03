import { api } from './axios'

/** Espejo de SeedDemoDataResponse del backend. */
export interface SeedDemoResponse {
  stylistsCreated: number
  stylistsSkipped: number
  servicesCreated: number
  servicesSkipped: number
  customersCreated: number
  customersSkipped: number
  appointmentsCreated: number
  appointmentsSkipped: number
  targetDate: string
}

/**
 * POST /api/Admin/seed-demo-data
 * Carga datos demo (estilistas/servicios/clientes/citas) en el tenant actual.
 * Idempotente. Solo SalonAdmin.
 *
 * @param date YYYY-MM-DD opcional. Si omite, usa "mañana" (Colombia UTC-5).
 */
export async function seedDemoData(date?: string): Promise<SeedDemoResponse> {
  const { data } = await api.post<SeedDemoResponse>(
    '/api/Admin/seed-demo-data',
    null,
    { params: date ? { date } : {} },
  )
  return data
}
