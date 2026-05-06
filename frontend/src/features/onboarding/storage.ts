import type { WizardData } from './types'

/**
 * Persistencia local del wizard.
 *
 * Mientras la usuaria avanza por los pasos, el estado se guarda en localStorage
 * para sobrevivir a un refresh del navegador (F5).
 *
 * Cuando el wizard termina exitosamente:
 *  - paso 1 ya se envió al backend (auth)
 *  - paso 4 se intenta enviar al backend (servicios)
 *  - los datos restantes (NIT, dirección, horarios, plan) se guardan
 *    en `bellasync_onboarding_pending` para sincronizar más adelante
 *    cuando el backend tenga endpoints.
 */

const DRAFT_KEY   = 'bellasync_onboarding_draft'
const PENDING_KEY = 'bellasync_onboarding_pending'

function safeParse<T>(raw: string | null): T | null {
  if (!raw) return null
  try { return JSON.parse(raw) as T }
  catch { return null }
}

export const wizardStorage = {
  /** Guarda el draft mientras la usuaria llena el formulario. */
  saveDraft(data: WizardData): void {
    try { localStorage.setItem(DRAFT_KEY, JSON.stringify(data)) }
    catch { /* localStorage lleno o bloqueado — no es crítico */ }
  },

  /** Recupera el draft (si existe) al volver al wizard. */
  loadDraft(): WizardData | null {
    return safeParse<WizardData>(localStorage.getItem(DRAFT_KEY))
  },

  /** Borra el draft cuando el wizard se completa o se reinicia. */
  clearDraft(): void {
    localStorage.removeItem(DRAFT_KEY)
  },

  /**
   * Guarda los datos pendientes de sincronización al backend.
   * Cuando el backend tenga endpoints para Tenant.address/horarios/plan,
   * un hook `useSyncPendingOnboarding()` los toma de acá y los empuja.
   */
  savePending(data: Partial<WizardData>): void {
    try { localStorage.setItem(PENDING_KEY, JSON.stringify(data)) }
    catch { /* idem */ }
  },

  loadPending(): Partial<WizardData> | null {
    return safeParse<Partial<WizardData>>(localStorage.getItem(PENDING_KEY))
  },

  clearPending(): void {
    localStorage.removeItem(PENDING_KEY)
  },
}
