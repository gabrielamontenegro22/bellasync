import { useContext } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AuthContext } from './AuthContext'
import { getReceptionPermissions } from '@/api/admin'

/**
 * Hook para consumir el contexto de auth.
 * Tira error explícito si se usa fuera del <AuthProvider>.
 */
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  return ctx
}

/**
 * Atajo: ¿el user actual es SalonAdmin?
 * Devuelve false si no hay user o si el rol es otro.
 * Pensado para inline en componentes que necesitan saber "admin sí/no".
 */
export function useIsAdmin(): boolean {
  const { user } = useAuth()
  return user?.role === 'SalonAdmin'
}

/**
 * Hook unificado de permisos. Combina rol + settings del tenant
 * (cargados vía /api/Admin/reception-permissions) en una sola
 * respuesta booleana por capacidad.
 *
 * Para admin → todos los permisos devuelven true sin necesidad de
 * leer la BD (atajo: la admin nunca está restringida).
 *
 * Para recepción → consulta los toggles del tenant. Mientras carga
 * devolvemos los defaults conservadores (todo OFF excepto cancelar
 * con plata) para evitar flashes de UI permitida que luego se oculte.
 *
 * Los componentes lo usan así:
 *   const { canEditStylists } = usePermissions()
 *   if (canEditStylists) { ... muestra botón ... }
 *
 * NOTA: este hook hace un query — usalo en componentes "página", no
 * en componentes de lista que se renderizan N veces.
 */
export function usePermissions() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'SalonAdmin'

  // Política de cache pensada para que recepción vea cambios de permisos
  // CASI inmediato (sin que admin tenga que avisarle "refrescá"):
  //
  // - staleTime 0       → siempre considerar stale; refetch al montar.
  // - refetchInterval 20s → polling en background cuando hay query
  //                        montado (sidebar siempre lo está).
  // - refetchOnWindowFocus (default true) → si cambia de tab y vuelve,
  //                        refetch automático.
  // - refetchOnMount 'always' → cada vez que un componente nuevo monta
  //                        usePermissions, refetch.
  //
  // Resultado: admin guarda → recepción ve el cambio en máximo 20s sin
  // hacer nada, o instantáneo si navega/cambia de tab.
  //
  // enabled: !isAdmin → la admin no necesita el query (tiene todo true).
  const permsQ = useQuery({
    queryKey: ['receptionPermissions'],
    queryFn: getReceptionPermissions,
    enabled: !isAdmin && !!user,
    staleTime: 0,
    refetchInterval: 20_000,
    refetchOnMount: 'always',
  })

  // Admin: todo true. Recepción: lo que diga el tenant (o defaults
  // conservadores mientras carga). El cap se devuelve también para
  // los componentes que necesitan validar montos en runtime.
  if (isAdmin) {
    return {
      isAdmin: true,
      expenseCap: null as number | null,  // sin cap para admin
      canCancelWithMoney: true,
      canCloseCash: true,
      canEditStylists: true,
      canEditServices: true,
      canEditInventory: true,
      canViewReports: true,
      canViewCommissions: true,
      canEditSchedule: true,
      canEditPaymentPolicy: true,
      canEditSalonInfo: true,
      isLoading: false,
    }
  }

  const p = permsQ.data
  return {
    isAdmin: false,
    expenseCap: p?.expenseCapCop ?? 100_000,
    canCancelWithMoney: p?.canCancelWithMoney ?? true,
    canCloseCash: p?.canCloseCash ?? false,
    canEditStylists: p?.canEditStylists ?? false,
    canEditServices: p?.canEditServices ?? false,
    canEditInventory: p?.canEditInventory ?? false,
    canViewReports: p?.canViewReports ?? false,
    canViewCommissions: p?.canViewCommissions ?? false,
    canEditSchedule: p?.canEditSchedule ?? false,
    canEditPaymentPolicy: p?.canEditPaymentPolicy ?? false,
    canEditSalonInfo: p?.canEditSalonInfo ?? false,
    isLoading: permsQ.isLoading,
  }
}
