import { useState, useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Percent, Check, X } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import { updateCommissionsSetting } from '@/api/admin'
import { useCommissionsSetting } from '@/features/commissions/useCommissionsSetting'

/**
 * Configuración → Comisiones.
 *
 * Toggle simple: activar/desactivar el módulo. Cuando está OFF, el
 * item "Comisiones" no aparece en el sidebar y los formularios de
 * Servicios esconden el campo % de comisión. Cuando está ON, todo
 * el flujo se enciende.
 *
 * No tiene "modos" intermedios: o lo usás o no lo usás. El % de
 * comisión por servicio se edita desde el form de cada servicio
 * (no acá).
 */
export function CommissionsSettingPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useCommissionsSetting()
  const [error, setError] = useState<string | null>(null)
  const [savedRecently, setSavedRecently] = useState(false)

  const mut = useMutation({
    mutationFn: updateCommissionsSetting,
    onSuccess: (r) => {
      // Invalidamos para que el sidebar y cualquier otro consumidor
      // re-renderice con el nuevo valor inmediatamente.
      qc.setQueryData(['commissionsSetting'], r)
      setSavedRecently(true)
      setError(null)
    },
    onError: (e) => setError(extractApiError(e, 'No se pudo actualizar.')),
  })

  // Mensaje de éxito desaparece a los 3s.
  useEffect(() => {
    if (!savedRecently) return
    const t = setTimeout(() => setSavedRecently(false), 3000)
    return () => clearTimeout(t)
  }, [savedRecently])

  const enabled = data?.enabled ?? false

  return (
    <div className="px-6 lg:px-10 py-8 max-w-2xl">
      <div className="mb-6">
        <div className="text-[10.5px] tracking-[0.2em] uppercase text-gold-600 font-medium">
          Ajustes del salón
        </div>
        <h1 className="font-serif text-[32px] lg:text-[38px] leading-[1.1] text-warm-800 mt-1">
          Comisiones de estilistas
        </h1>
        <p className="text-[13.5px] text-warm-500 mt-2 max-w-xl leading-relaxed">
          Activá este módulo si tu salón paga a las estilistas un % por
          servicio. Si pagás sueldo fijo o alquiler de silla, dejalo
          apagado y BellaSync no te molesta con esto.
        </p>
      </div>

      {isLoading ? (
        <div className="text-[13px] text-warm-500">Cargando…</div>
      ) : (
        <div className="bg-white border border-warm-150 rounded-2xl shadow-soft p-5 space-y-5">
          {/* Toggle visual grande */}
          <div className="flex items-start gap-4">
            <span className={cls(
              'w-11 h-11 rounded-xl flex items-center justify-center flex-shrink-0',
              enabled ? 'bg-brand-50 text-brand-700' : 'bg-warm-100 text-warm-500',
            )}>
              <Percent size={20} strokeWidth={1.8} />
            </span>
            <div className="flex-1 min-w-0">
              <div className="text-[14.5px] font-medium text-warm-800">
                Llevar registro de comisiones
              </div>
              <div className="text-[12.5px] text-warm-500 mt-0.5">
                {enabled
                  ? 'Activado: la pantalla /comisiones aparece en el sidebar y el formulario de Servicios pide un % por cada servicio.'
                  : 'Apagado: nada relacionado a comisiones aparece en la app. Podés activarlo cuando quieras.'}
              </div>
            </div>

            {/* Switch */}
            <button
              type="button"
              onClick={() => mut.mutate(!enabled)}
              disabled={mut.isPending}
              role="switch"
              aria-checked={enabled}
              className={cls(
                'relative w-12 h-7 rounded-full transition flex-shrink-0',
                enabled ? 'bg-brand-700' : 'bg-warm-300',
                mut.isPending && 'opacity-60 cursor-wait',
              )}
            >
              <span
                className={cls(
                  'absolute top-0.5 w-6 h-6 rounded-full bg-white shadow-sm transition',
                  enabled ? 'left-[22px]' : 'left-0.5',
                )}
              />
            </button>
          </div>

          {/* Detalle de lo que activa/desactiva */}
          <div className="pt-4 border-t border-warm-150">
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium mb-3">
              Qué incluye
            </div>
            <ul className="space-y-2 text-[13px] text-warm-700">
              <ItemRow
                enabled={enabled}
                text={<>Item <strong>Comisiones</strong> en el sidebar — pantalla con resumen por estilista del período.</>}
              />
              <ItemRow
                enabled={enabled}
                text={<>Campo <strong>% de comisión</strong> en el formulario de cada servicio (Balayage 40%, Manicure 30%, etc.).</>}
              />
              <ItemRow
                enabled={enabled}
                text={<>Botón <strong>"Liquidar"</strong> para marcar lo que ya le pagaste a cada estilista, con historial.</>}
              />
            </ul>
            <p className="text-[11.5px] text-warm-500 italic mt-3">
              Los datos históricos se conservan: si apagás y volvés a prender, todo aparece intacto.
            </p>
          </div>

          {error && (
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {error}
            </div>
          )}
          {savedRecently && !error && (
            <div className="rounded-lg bg-brand-50 ring-1 ring-brand-200 px-3 py-2 text-[12.5px] text-brand-800">
              ✓ Guardado.
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function ItemRow({ enabled, text }: { enabled: boolean; text: React.ReactNode }) {
  return (
    <li className="flex items-start gap-2">
      <span className={cls(
        'w-4 h-4 rounded-full flex items-center justify-center flex-shrink-0 mt-0.5',
        enabled ? 'bg-brand-100 text-brand-700' : 'bg-warm-150 text-warm-400',
      )}>
        {enabled ? <Check size={11} strokeWidth={3} /> : <X size={11} strokeWidth={3} />}
      </span>
      <span className={enabled ? 'text-warm-700' : 'text-warm-400'}>
        {text}
      </span>
    </li>
  )
}
