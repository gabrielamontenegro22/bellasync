import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Percent, Check, X } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import { updateCommissionsSetting } from '@/api/admin'
import { useCommissionsSetting } from '@/features/commissions/useCommissionsSetting'
import { SettingsHeader, SettingsBlock, ToggleRow } from './_primitives'

/**
 * `/configuracion/comisiones` — toggle global del módulo de comisiones.
 *
 * Lógica nuestra: el % se define por SERVICIO (Service.CommissionPercentage),
 * no por estilista como en el mockup. Por eso acá solo está el switch
 * principal — la edición del % vive en el form de cada servicio.
 *
 * Diseño basado en config-servicios.jsx (ComisionesView). Estructura:
 * eyebrow + título serif + descripción + un Block único con el toggle
 * + sección "Qué incluye" con lista de check/X que cambia color según
 * el estado.
 */
export function CommissionsSettingPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useCommissionsSetting()
  const [error, setError] = useState<string | null>(null)
  const [savedRecently, setSavedRecently] = useState(false)

  const mut = useMutation({
    mutationFn: updateCommissionsSetting,
    onSuccess: (r) => {
      qc.setQueryData(['commissionsSetting'], r)
      setSavedRecently(true)
      setError(null)
    },
    onError: (e) => setError(extractApiError(e, 'No se pudo actualizar.')),
  })

  useEffect(() => {
    if (!savedRecently) return
    const t = setTimeout(() => setSavedRecently(false), 3000)
    return () => clearTimeout(t)
  }, [savedRecently])

  const enabled = data?.enabled ?? false

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">Cargando…</div>
    )
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Comisiones"
          desc="Activá este módulo si tu salón paga a las estilistas un % por servicio. Si pagás sueldo fijo o alquiler de silla, dejalo apagado y BellaSync no te molesta con esto."
        />

        {/* TOGGLE */}
        <SettingsBlock icon={<Percent size={16} />} title="Estado del módulo">
          <ToggleRow
            title="Activar comisiones"
            desc={
              enabled
                ? 'Activado: la pantalla /comisiones aparece en el sidebar y el formulario de Servicios pide un % por cada servicio.'
                : 'Apagado: nada relacionado a comisiones aparece en la app. Podés activarlo cuando quieras.'
            }
            on={enabled}
            onChange={(v) => mut.mutate(v)}
            disabled={mut.isPending}
          />
          {error && (
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {error}
            </div>
          )}
          {savedRecently && !error && (
            <div className="rounded-lg bg-brand-50 ring-1 ring-brand-200 px-3 py-2 text-[12.5px] text-brand-800 flex items-center gap-1.5">
              <Check size={13} strokeWidth={2.4} /> Guardado.
            </div>
          )}
        </SettingsBlock>

        {/* QUÉ INCLUYE */}
        <SettingsBlock title="Qué incluye" last>
          <ul className="space-y-2.5 text-[13px] text-warm-700">
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
          <p className="text-[11.5px] text-warm-500 italic">
            Los datos históricos se conservan: si apagás y volvés a prender, todo aparece intacto.
          </p>
        </SettingsBlock>
      </div>
    </div>
  )
}

function ItemRow({ enabled, text }: { enabled: boolean; text: React.ReactNode }) {
  return (
    <li className="flex items-start gap-2">
      <span
        className={cls(
          'w-4 h-4 rounded-full flex items-center justify-center flex-shrink-0 mt-0.5',
          enabled ? 'bg-brand-100 text-brand-700' : 'bg-warm-150 text-warm-400',
        )}
      >
        {enabled ? <Check size={11} strokeWidth={3} /> : <X size={11} strokeWidth={3} />}
      </span>
      <span className={enabled ? 'text-warm-700' : 'text-warm-400'}>{text}</span>
    </li>
  )
}
