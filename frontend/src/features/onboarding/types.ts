/**
 * Tipos del wizard de onboarding.
 * Espejo del defaultState de onboarding-shell.jsx.
 */

export type DayId = 'mon' | 'tue' | 'wed' | 'thu' | 'fri' | 'sat' | 'sun'

/** Rango horario [horaInicio, horaFin]. null = día cerrado. */
export type DayRange = [number, number] | null

export type HoursPresetId = 'classic' | 'martes_dom' | 'lun_dom' | 'custom'

export type PlanId = 'basico' | 'pro' | 'premium'

export type ServiceCategory = 'Uñas' | 'Cabello' | 'Rostro' | 'Maquillaje' | 'Otros'

export interface SuggestedService {
  id: string
  name: string
  cat: ServiceCategory
  price: number
  dur: number
  emoji: string
  defOn: boolean
}

export interface CustomService {
  id: string
  name: string
  cat: ServiceCategory
  price: number
  dur: number
  emoji: string
}

export interface ServiceFieldData {
  price: number
  dur: number
}

export interface Plan {
  id: PlanId
  name: string
  price: number
  sub: string
  features: string[]
  recommended?: boolean
}

/**
 * Estado completo del wizard. Coincide 1:1 con defaultState() del mockup.
 * Se persiste en localStorage para sobrevivir un refresh del navegador.
 */
export interface WizardData {
  // step 1 — cuenta
  ownerName: string
  email: string
  password: string

  // step 2 — salón
  salonName: string
  nit: string
  address: string
  city: string
  phone: string
  logoData: string | null  // data URL
  logoName: string

  // step 3 — horarios
  hoursPreset: HoursPresetId
  hours: Record<DayId, DayRange>

  // step 4 — servicios
  servicesOn: Record<string, boolean>
  servicesData: Record<string, ServiceFieldData>
  customServices: CustomService[]

  // step 5 — plan
  plan: PlanId
}

export interface StepInfo {
  n: number
  title: string
  sub: string
}
