import { useEffect, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Banknote, Plus, X } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import { registerExpense, type ExpenseResponse } from '@/api/expenses'
import type { PaymentMethod } from '@/api/payments'
import { PaymentMethodPicker } from '@/features/payments/components/PaymentMethodPicker'
import { usePermissions } from '@/features/auth/useAuth'

interface Props {
  open: boolean
  onClose: () => void
  /** Callback opcional al guardar (típico: cerrar el modal). */
  onCreated?: (e: ExpenseResponse) => void
}

/**
 * Modal "Registrar egreso" para `/caja`. La admin escribe concepto
 * + monto + método (default Efectivo, que es ~95% de los casos).
 *
 * Reglas UX:
 *  - Concept es required (textarea de 2 líneas para que entren cosas
 *    como "Compra tintes Wella (proveedor)").
 *  - Amount es required > 0.
 *  - Method default Cash. Cambiarlo es para los casos raros
 *    (transferencia al proveedor desde la cuenta del salón).
 *  - Anti doble-click con useRef lock — mismo patrón que
 *    RegisterPaymentModal después de aquel bug.
 *  - Al confirmar exitoso, invalida las queries de cash y expenses
 *    para que se vea inmediatamente en la pantalla.
 */
export function RegisterExpenseModal({ open, onClose, onCreated }: Props) {
  const qc = useQueryClient()
  // Hook centralizado: admin obtiene cap=null (sin tope), recepción
  // obtiene el cap configurado por la admin. Polling de 20s en useAuth
  // refresca los permisos sin que recepción tenga que hacer F5.
  const { isAdmin, expenseCap } = usePermissions()
  const [concept, setConcept] = useState('')
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [provider, setProvider] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const submittingRef = useRef(false)

  // Reset cada vez que se reabre.
  useEffect(() => {
    if (open) {
      setConcept('')
      setAmount('')
      setMethod('Cash')
      setProvider(null)
      setError(null)
      submittingRef.current = false
    }
  }, [open])

  const mut = useMutation({
    mutationFn: registerExpense,
    onSuccess: (created) => {
      submittingRef.current = false
      qc.invalidateQueries({ queryKey: ['cash'] })
      qc.invalidateQueries({ queryKey: ['expenses'] })
      onCreated?.(created)
      onClose()
    },
    onError: (err) => {
      submittingRef.current = false
      setError(extractApiError(err, 'No se pudo registrar el egreso.'))
    },
  })

  if (!open) return null

  const amountNum = parseInt(amount.replace(/[^0-9]/g, '')) || 0
  const conceptOk = concept.trim().length > 0
  const amountOk = amountNum > 0
  const providerOk = method !== 'Transfer' || !!provider

  // Cap configurable por tenant (lo lee la admin desde /configuracion/permisos).
  // - null = sin cap, recepción puede registrar lo que sea.
  // - 0    = recepción no puede registrar nada.
  // - X    = tope; sobre eso requiere admin.
  // Admin no tiene cap NUNCA.
  const cap = expenseCap
  const receptionBlocked = !isAdmin && cap === 0
  const overReceptionCap = !isAdmin && cap != null && cap > 0 && amountNum > cap
  const canSubmit = conceptOk && amountOk && providerOk
    && !receptionBlocked && !overReceptionCap && !mut.isPending

  const handleSubmit = () => {
    if (!canSubmit || submittingRef.current) return
    submittingRef.current = true
    setError(null)
    mut.mutate({
      concept: concept.trim(),
      amount: amountNum,
      method,
      provider,
    })
  }

  return (
    // Mobile: bottom sheet (items-end + sin padding lateral + rounded-t).
    // Desktop (≥sm): centrado con padding y rounded completo.
    // El panel scrollea internamente para que el footer siempre quede visible.
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:justify-center sm:p-4"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm anim-fade" />
      <div
        className="relative w-full sm:max-w-md max-h-[92vh] sm:max-h-[88vh] bg-white rounded-t-2xl sm:rounded-2xl shadow-pop overflow-hidden anim-fade flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-6 pt-6 pb-4 border-b border-warm-150 flex items-start justify-between flex-shrink-0">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium">
              Salida de caja
            </div>
            <h3 className="font-serif text-[26px] text-warm-800 mt-1 leading-tight">
              Registrar egreso
            </h3>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center"
            aria-label="Cerrar"
          >
            <X size={18} strokeWidth={2} />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4 overflow-y-auto flex-1">
          {/* Concepto */}
          <div>
            <label className="text-[12.5px] font-medium text-warm-700 block mb-1.5">
              ¿En qué se gastó?
              <span className="text-terra-500 ml-1">*</span>
            </label>
            <textarea
              value={concept}
              onChange={(e) => setConcept(e.target.value)}
              rows={2}
              placeholder="Ej: Compra tintes Wella (proveedor)"
              autoFocus
              className={cls(
                'w-full px-3 py-2 rounded-lg bg-white border border-warm-200',
                'text-[13.5px] text-warm-800 placeholder:text-warm-400',
                'focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none resize-none',
              )}
            />
          </div>

          {/* Monto */}
          <div>
            <label className="text-[12.5px] font-medium text-warm-700 block mb-1.5">
              Monto
              <span className="text-terra-500 ml-1">*</span>
            </label>
            <div className="relative">
              <span className="absolute left-3.5 top-1/2 -translate-y-1/2 text-warm-400 text-[15px]">
                $
              </span>
              <input
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                inputMode="numeric"
                placeholder="0"
                className={cls(
                  'w-full pl-7 pr-3.5 py-2.5 rounded-lg bg-white border border-warm-200',
                  'text-[15px] text-warm-800 tabular-nums focus:border-brand-500',
                  'focus:ring-2 focus:ring-brand-100 outline-none',
                )}
              />
            </div>
          </div>

          {/* Método */}
          <div>
            <label className="text-[12.5px] font-medium text-warm-700 block mb-1.5">
              ¿Cómo se pagó?
            </label>
            <PaymentMethodPicker
              method={method}
              provider={provider}
              onChange={(m, p) => {
                setMethod(m)
                setProvider(p)
              }}
              hideOther
            />
            {method === 'Cash' ? (
              <p className="text-[11px] text-warm-500 mt-1.5 flex items-center gap-1">
                <Banknote size={11} strokeWidth={1.8} />
                Se descuenta del esperado en el arqueo del cierre.
              </p>
            ) : (
              <p className="text-[11px] text-warm-500 mt-1.5">
                Queda en el registro pero no afecta el arqueo de
                efectivo (solo los pagos en efectivo afectan).
              </p>
            )}
          </div>

          {/* Bloqueo total: la admin configuró cap=0 → recepción no
              puede registrar nada de egresos en este salón. */}
          {receptionBlocked && (
            <div className="rounded-lg bg-gold-50 ring-1 ring-gold-200 px-3 py-2 text-[12px] text-gold-700">
              ⚠️ La administradora del salón configuró que solo ella puede registrar egresos.
            </div>
          )}

          {/* Aviso de cap para recepción — preventivo, antes del submit.
              Se muestra apenas el monto supera el tope configurado por la
              admin para este salón. La admin no ve este aviso. */}
          {overReceptionCap && cap != null && (
            <div className="rounded-lg bg-gold-50 ring-1 ring-gold-200 px-3 py-2 text-[12px] text-gold-700">
              ⚠️ Egresos sobre ${cap.toLocaleString('es-CO')} requieren autorización de la administradora del salón.
            </div>
          )}

          {error && (
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {error}
            </div>
          )}
        </div>

        <div className="px-6 py-4 bg-warm-50 border-t border-warm-150 flex items-center justify-end gap-2 flex-shrink-0">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2.5 rounded-lg text-[13px] text-warm-700 hover:bg-warm-150"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={!canSubmit}
            className={cls(
              'px-5 py-2.5 rounded-lg text-[13px] font-medium flex items-center gap-2 transition',
              canSubmit
                ? 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft'
                : 'bg-warm-200 text-warm-400 cursor-not-allowed',
            )}
          >
            <Plus size={14} strokeWidth={2.2} />
            {mut.isPending ? 'Guardando…' : 'Registrar egreso'}
          </button>
        </div>
      </div>
    </div>
  )
}

