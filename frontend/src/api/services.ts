import { api } from './axios'

/**
 * API de Servicios — espeja los endpoints de ServicesController del backend.
 *
 * Por ahora solo exponemos lo que el wizard de onboarding necesita
 * (createService). El CRUD completo se completa en el F4.
 */

export type ServiceCategoryEnum =
  | 'Cabello'
  | 'Unas'        // sin tilde — coincide con el enum del backend
  | 'Estetica'
  | 'Maquillaje'
  | 'Depilacion'
  | 'Otros'

export interface CreateServiceRequest {
  name: string
  description?: string | null
  /** Precio en COP. Validación backend: $10.000 a $500.000 */
  price: number
  /** Duración en minutos */
  durationMinutes: number
  category: ServiceCategoryEnum
}

export interface ServiceResponse {
  id: string
  name: string
  description: string | null
  price: number
  durationMinutes: number
  category: ServiceCategoryEnum
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}

/** POST /api/Services — crea un servicio para el tenant del JWT actual. */
export async function createService(payload: CreateServiceRequest): Promise<ServiceResponse> {
  const { data } = await api.post<ServiceResponse>('/api/Services', payload)
  return data
}

/**
 * Mapea las categorías del wizard (UI español con tildes y "Uñas") al enum
 * del backend (sin tildes, sin Ñ porque el enum C# no las acepta).
 */
const CATEGORY_MAP: Record<string, ServiceCategoryEnum> = {
  'Uñas':       'Unas',
  'Cabello':    'Cabello',
  'Rostro':     'Estetica',  // el mockup llama "Rostro" a lo que el backend llama "Estetica"
  'Maquillaje': 'Maquillaje',
  'Otros':      'Otros',
}

export function mapWizardCategory(uiCategory: string): ServiceCategoryEnum {
  return CATEGORY_MAP[uiCategory] ?? 'Otros'
}
