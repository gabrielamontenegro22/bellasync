import { api } from './axios'

/**
 * API client de Servicios — espeja ServicesController del backend.
 * Endpoints implementados:
 *  - GET    /api/Services            → listServices()
 *  - GET    /api/Services/{id}       → getService()
 *  - POST   /api/Services            → createService()
 *  - PUT    /api/Services/{id}       → updateService()
 *  - DELETE /api/Services/{id}       → deleteService()
 */

export type ServiceCategoryEnum =
  | 'Cabello'
  | 'Unas'        // sin tilde — coincide con el enum C# del backend
  | 'Estetica'
  | 'Maquillaje'
  | 'Depilacion'
  | 'Otros'

/** DTO para crear un servicio nuevo. */
export interface CreateServiceRequest {
  name: string
  description?: string | null
  category: ServiceCategoryEnum
  /** Duración en minutos (1-480) */
  durationMinutes: number
  /** Precio en COP (10.000 a 500.000) */
  price: number
  /** Comisión al estilista (0-100) */
  commissionPercentage: number
  /** Color hex #RRGGBB para identificarlo en la agenda. Opcional. */
  color?: string | null
  /** Si requiere anticipo para confirmar la cita. */
  requiresDeposit: boolean
  /** Porcentaje del precio cobrado como anticipo (0-100). */
  depositPercentage: number
}

/** DTO para editar un servicio existente. Permite además cambiar isActive. */
export interface UpdateServiceRequest extends CreateServiceRequest {
  isActive: boolean
}

/** Respuesta del backend para un servicio. */
export interface ServiceResponse {
  id: string
  name: string
  description: string | null
  category: ServiceCategoryEnum
  durationMinutes: number
  price: number
  commissionPercentage: number
  color: string | null
  isActive: boolean
  requiresDeposit: boolean
  depositPercentage: number
  createdAt: string
  updatedAt: string | null
}

/* -------------------------------------------------------------------------- */
/*  Funciones                                                                 */
/* -------------------------------------------------------------------------- */

/**
 * GET /api/Services
 * Por defecto solo trae activos. Pasar `includeInactive=true` para ver archivados.
 */
export async function listServices(includeInactive = false): Promise<ServiceResponse[]> {
  const { data } = await api.get<ServiceResponse[]>('/api/Services', {
    params: { includeInactive },
  })
  return data
}

/** GET /api/Services/{id} */
export async function getService(id: string): Promise<ServiceResponse> {
  const { data } = await api.get<ServiceResponse>(`/api/Services/${id}`)
  return data
}

/** POST /api/Services */
export async function createService(payload: CreateServiceRequest): Promise<ServiceResponse> {
  const { data } = await api.post<ServiceResponse>('/api/Services', payload)
  return data
}

/** PUT /api/Services/{id} */
export async function updateService(
  id: string,
  payload: UpdateServiceRequest,
): Promise<ServiceResponse> {
  const { data } = await api.put<ServiceResponse>(`/api/Services/${id}`, payload)
  return data
}

/**
 * DELETE /api/Services/{id} — soft delete (marca isActive=false).
 * Idempotente: si ya estaba archivado, no falla.
 */
export async function deleteService(id: string): Promise<void> {
  await api.delete(`/api/Services/${id}`)
}

/* -------------------------------------------------------------------------- */
/*  Helpers de compatibilidad con el wizard                                   */
/* -------------------------------------------------------------------------- */

/**
 * Mapea las categorías "amigables" del wizard a los valores exactos del enum backend.
 *  - "Uñas"   → "Unas"
 *  - "Rostro" → "Estetica"
 */
const CATEGORY_MAP: Record<string, ServiceCategoryEnum> = {
  'Uñas':       'Unas',
  'Cabello':    'Cabello',
  'Rostro':     'Estetica',
  'Maquillaje': 'Maquillaje',
  'Otros':      'Otros',
}

export function mapWizardCategory(uiCategory: string): ServiceCategoryEnum {
  return CATEGORY_MAP[uiCategory] ?? 'Otros'
}
