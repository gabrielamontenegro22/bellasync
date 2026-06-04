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

/** Espejo de TenantPaymentPolicyResponse del backend. */
export interface PaymentPolicy {
  holdDurationHours: number
  holdMinBeforeAppointmentMinutes: number
  minAdvanceMinutes: number
}

/** GET /api/Admin/payment-policy */
export async function getPaymentPolicy(): Promise<PaymentPolicy> {
  const { data } = await api.get<PaymentPolicy>('/api/Admin/payment-policy')
  return data
}

/** PUT /api/Admin/payment-policy */
export async function updatePaymentPolicy(req: PaymentPolicy): Promise<PaymentPolicy> {
  const { data } = await api.put<PaymentPolicy>('/api/Admin/payment-policy', req)
  return data
}

/** Espejo de CommissionsSettingResponse. */
export interface CommissionsSetting {
  enabled: boolean
}

/** GET /api/Admin/commissions-setting */
export async function getCommissionsSetting(): Promise<CommissionsSetting> {
  const { data } = await api.get<CommissionsSetting>('/api/Admin/commissions-setting')
  return data
}

/** PUT /api/Admin/commissions-setting */
export async function updateCommissionsSetting(enabled: boolean): Promise<CommissionsSetting> {
  const { data } = await api.put<CommissionsSetting>('/api/Admin/commissions-setting', { enabled })
  return data
}

/** Info pública/contacto del salón (espejo TenantInfoResponse). */
export interface TenantInfo {
  name: string
  slug: string
  address: string | null
  phone: string | null
  contactEmail: string | null
  logoUrl: string | null
  instagramHandle: string | null
  description: string | null
}

export interface UpdateTenantInfoRequest {
  name: string
  address?: string | null
  phone?: string | null
  contactEmail?: string | null
  logoUrl?: string | null
  instagramHandle?: string | null
  description?: string | null
}

export async function getTenantInfo(): Promise<TenantInfo> {
  const { data } = await api.get<TenantInfo>('/api/Admin/tenant-info')
  return data
}

export async function updateTenantInfo(req: UpdateTenantInfoRequest): Promise<TenantInfo> {
  const { data } = await api.put<TenantInfo>('/api/Admin/tenant-info', req)
  return data
}

/**
 * Sube el logo del salón. Multipart con campo "file". El backend valida
 * tipo (jpg/png/webp/heic) y tamaño (max 5MB), guarda el archivo,
 * actualiza Tenant.LogoUrl y devuelve la URL final ("/uploads/logos/...").
 */
export async function uploadTenantLogo(file: File): Promise<{ logoUrl: string }> {
  const form = new FormData()
  form.append('file', file)
  const { data } = await api.post<{ logoUrl: string }>(
    '/api/Admin/tenant/logo',
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  )
  return data
}

/**
 * Horario completo del salón. days es un dict 0-6 (Lunes..Domingo)
 * donde la clave numérica viene como string en JSON ("0", "1", …).
 * El frontend normaliza al consumirlo.
 */
export interface SalonHoursDto {
  days: Record<string, { fromHour: number; toHour: number } | null>
  lunchBreakEnabled: boolean
  lunchBreakFromHour: number
  lunchBreakToHour: number
  isHolidaysClosed: boolean
  closedDates: string[]  // YYYY-MM-DD
}

export async function getSalonHours(): Promise<SalonHoursDto> {
  const { data } = await api.get<SalonHoursDto>('/api/Admin/salon-hours')
  return data
}

export async function updateSalonHours(req: SalonHoursDto): Promise<SalonHoursDto> {
  const { data } = await api.put<SalonHoursDto>('/api/Admin/salon-hours', req)
  return data
}
