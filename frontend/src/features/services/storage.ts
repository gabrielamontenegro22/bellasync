/**
 * Persistencia local de los campos del mockup que NO existen todavía en el backend.
 *
 * Ya migrados al backend:
 *  - requiresDeposit ✅ (ahora es Service.RequiresDeposit)
 *  - depositPercentage ✅ (ahora es Service.DepositPercentage)
 *
 * Pendientes en backend:
 *  - assignedStylistIds — no es necesario migrar; la relación M:N existe en el
 *    backend desde el lado de Stylist (StylistsController acepta serviceIds).
 *    Cuando hagamos F5 (pantalla de Estilistas), eliminamos completamente
 *    este storage y derivamos los estilistas asignados a un servicio
 *    haciendo GET /api/Stylists y filtrando por service.id.
 *
 * Estructura en localStorage:
 *   bellasync_service_extras = {
 *     [serviceId]: { assignedStylistIds: string[] }
 *   }
 */

const KEY = 'bellasync_service_extras'

export interface ServiceExtras {
  assignedStylistIds: string[]
}

const DEFAULT_EXTRAS: ServiceExtras = {
  assignedStylistIds: [],
}

type ExtrasMap = Record<string, ServiceExtras>

function readAll(): ExtrasMap {
  try {
    const raw = localStorage.getItem(KEY)
    return raw ? (JSON.parse(raw) as ExtrasMap) : {}
  } catch {
    return {}
  }
}

function writeAll(map: ExtrasMap): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(map))
  } catch {
    /* localStorage lleno o bloqueado — no es crítico */
  }
}

export const serviceExtrasStorage = {
  /** Lee los extras de un servicio (devuelve defaults si no existen). */
  get(serviceId: string): ServiceExtras {
    const all = readAll()
    return all[serviceId] ?? { ...DEFAULT_EXTRAS }
  },

  /** Guarda/actualiza los extras de un servicio. */
  save(serviceId: string, extras: ServiceExtras): void {
    const all = readAll()
    all[serviceId] = extras
    writeAll(all)
  },

  /** Borra los extras de un servicio (al eliminarlo). */
  remove(serviceId: string): void {
    const all = readAll()
    delete all[serviceId]
    writeAll(all)
  },

  /** Útil al inicio: lee todos de una. */
  getAll(): ExtrasMap {
    return readAll()
  },
}
