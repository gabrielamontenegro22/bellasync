import { Check, MessageCircle, Star } from 'lucide-react'
import type { WizardData } from '../types'
import { NavButtons } from '../components/NavButtons'
import { PLANS } from '../data/plans'
import { cls } from '@/lib/cls'

const fmtCOP = (n: number) => '$' + Math.round(n).toLocaleString('es-CO')

interface Step5Props {
  data: WizardData
  set: (patch: Partial<WizardData>) => void
  onSubmit: () => void
  onBack: () => void
  loading?: boolean
}

/** Paso 5 — selección de plan + nota sobre cobro por WhatsApp. */
export function Step5Plan({ data, set, onSubmit, onBack, loading }: Step5Props) {
  return (
    <div className="space-y-5">
      <div className="space-y-3">
        {PLANS.map((p) => {
          const selected = data.plan === p.id
          return (
            <button
              key={p.id}
              type="button"
              onClick={() => set({ plan: p.id })}
              className={cls(
                'w-full text-left rounded-2xl border-2 p-5 transition relative',
                selected
                  ? 'border-brand-700 bg-brand-50/40 ring-4 ring-brand-100'
                  : 'border-warm-200 bg-white hover:border-warm-300',
              )}
            >
              {p.recommended && (
                <div className="absolute -top-2.5 right-5 px-2.5 py-0.5 rounded-full bg-gold-300 text-warm-800 text-[9.5px] tracking-[0.14em] uppercase font-semibold flex items-center gap-1">
                  <Star size={10} fill="currentColor" /> Recomendado
                </div>
              )}

              <div className="flex items-start gap-4">
                <span
                  className={cls(
                    'w-5 h-5 rounded-full flex-shrink-0 mt-1 flex items-center justify-center transition',
                    selected ? 'bg-brand-700' : 'border-2 border-warm-300',
                  )}
                >
                  {selected && <span className="w-1.5 h-1.5 rounded-full bg-white" />}
                </span>

                <div className="flex-1 min-w-0">
                  <div className="flex items-baseline justify-between gap-3">
                    <div>
                      <div className="font-serif text-[22px] text-warm-800 leading-tight">
                        {p.name}
                      </div>
                      <div className="text-[11.5px] text-warm-500">{p.sub}</div>
                    </div>
                    <div className="text-right whitespace-nowrap">
                      <span className="font-serif text-[26px] text-warm-800 tabular-nums leading-none">
                        {fmtCOP(p.price)}
                      </span>
                      <div className="text-[11px] text-warm-500">/ mes</div>
                    </div>
                  </div>

                  <ul className="mt-3 grid sm:grid-cols-2 gap-x-3 gap-y-1.5">
                    {p.features.map((f, i) => (
                      <li key={i} className="flex items-start gap-1.5 text-[12px] text-warm-700">
                        <span className="text-brand-700 mt-0.5 flex-shrink-0">
                          <Check size={12} strokeWidth={3} />
                        </span>
                        {f}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </button>
          )
        })}
      </div>

      {/* Nota sobre cobro por WhatsApp */}
      <div className="rounded-xl bg-warm-50 border border-warm-200 p-4 flex gap-3">
        <div className="w-9 h-9 rounded-full bg-brand-700 text-white flex-shrink-0 flex items-center justify-center">
          <MessageCircle size={16} />
        </div>
        <div className="flex-1">
          <div className="text-[13px] font-medium text-warm-800">¿Cómo se hace el cobro?</div>
          <p className="text-[12.5px] text-warm-600 mt-0.5 leading-relaxed">
            Te enviaremos las instrucciones de pago a tu WhatsApp. Una vez recibamos tu transferencia
            (Bancolombia o Davivienda), activaremos tu cuenta. Sin tarjeta de crédito, sin débito automático.
          </p>
        </div>
      </div>

      <NavButtons
        onBack={onBack}
        onNext={onSubmit}
        valid={!!data.plan}
        loading={loading}
        nextLabel="Crear mi salón"
        nextIcon={<Check size={14} strokeWidth={3} />}
      />
    </div>
  )
}
