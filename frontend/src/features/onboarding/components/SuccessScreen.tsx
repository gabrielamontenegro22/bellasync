import { Check, MessageCircle, ArrowRight } from 'lucide-react'
import type { WizardData } from '../types'
import { PLANS } from '../data/plans'
import { cls } from '@/lib/cls'

const fmtCOP = (n: number) => '$' + Math.round(n).toLocaleString('es-CO')

interface SuccessScreenProps {
  data: WizardData
  onGoToPanel: () => void
  onRestart: () => void
}

/**
 * Pantalla final del onboarding.
 * Replica fielmente SuccessScreen de onboarding-shell.jsx — animación de check
 * + mensaje WhatsApp + 4 stages del flujo de pago.
 */
export function SuccessScreen({ data, onGoToPanel, onRestart }: SuccessScreenProps) {
  const sel = PLANS.find((p) => p.id === data.plan)

  return (
    <div className="min-h-screen bg-cream flex items-center justify-center px-6 py-12">
      <div className="max-w-xl w-full text-center">

        {/* Check animado con ping slow */}
        <div className="relative w-24 h-24 mx-auto mb-8">
          <div className="absolute inset-0 rounded-full bg-brand-100 animate-ping-slow" />
          <div className="absolute inset-2 rounded-full bg-brand-200/60" />
          <div className="relative w-full h-full rounded-full bg-brand-700 flex items-center justify-center text-white shadow-pop">
            <Check size={44} strokeWidth={3} className="anim-fade" />
          </div>
        </div>

        <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
          Cuenta creada
        </div>
        <h1 className="font-serif text-[44px] lg:text-[56px] leading-[1.02] tracking-tight text-warm-800 mt-3">
          ¡Tu salón está casi listo!
        </h1>
        <p className="text-[15.5px] text-warm-600 leading-relaxed mt-5 max-w-md mx-auto">
          Te enviamos las instrucciones de pago a tu WhatsApp{' '}
          <span className="font-medium text-warm-800 whitespace-nowrap">
            {data.phone || '+57 XXX XXX XXXX'}
          </span>.
        </p>

        {/* Card con flujo de 4 etapas */}
        <div className="mt-8 rounded-2xl bg-white border border-warm-200 p-6 text-left shadow-soft">
          <div className="flex items-start gap-3 mb-5 pb-5 border-b border-warm-150">
            <div className="w-10 h-10 rounded-full bg-brand-700 text-white flex items-center justify-center flex-shrink-0">
              <MessageCircle size={18} />
            </div>
            <div>
              <div className="text-[13.5px] font-medium text-warm-800">
                Mensaje enviado a WhatsApp
              </div>
              <div className="text-[11.5px] text-warm-500 mt-0.5">Hace unos segundos</div>
            </div>
          </div>

          <ol className="space-y-4">
            <Stage
              n={1}
              title="Recibe el mensaje"
              body="En tu WhatsApp encontrarás los datos bancarios y el monto a transferir."
              done
            />
            <Stage
              n={2}
              title="Realiza la transferencia"
              body={`${sel ? fmtCOP(sel.price) : '$0'} a la cuenta de ahorros que recibirás. Bancolombia o Davivienda.`}
              active
            />
            <Stage
              n={3}
              title="Confirmamos en menos de 1 hora hábil"
              body="Validamos el comprobante y activamos tu cuenta automáticamente."
            />
            <Stage
              n={4}
              title="¡Empieza a usar BellaSync!"
              body="Te avisaremos por WhatsApp cuando tu cuenta esté lista."
            />
          </ol>
        </div>

        {/* CTAs */}
        <div className="mt-8 flex flex-col sm:flex-row gap-3 justify-center">
          <button
            type="button"
            onClick={onGoToPanel}
            className="px-6 py-3 rounded-xl bg-brand-700 hover:bg-brand-800 text-white text-[14px] font-medium flex items-center justify-center gap-2 shadow-soft"
          >
            Ir al panel <ArrowRight size={14} />
          </button>
          <button
            type="button"
            onClick={onRestart}
            className="px-6 py-3 rounded-xl border border-warm-200 bg-white hover:bg-warm-50 text-warm-700 text-[14px] font-medium"
          >
            Empezar de nuevo
          </button>
        </div>

        <p className="text-[11.5px] text-warm-500 mt-6">
          ¿No te llegó el mensaje?{' '}
          <a href="#" className="underline hover:text-warm-800">Reenviarlo</a> ·{' '}
          <a href="#" className="underline hover:text-warm-800">Cambiar número</a>
        </p>
      </div>
    </div>
  )
}

interface StageProps {
  n: number
  title: string
  body: string
  done?: boolean
  active?: boolean
}

function Stage({ n, title, body, done, active }: StageProps) {
  return (
    <li className="flex items-start gap-3">
      <div
        className={cls(
          'w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0 text-[11px] font-semibold mt-0.5',
          done
            ? 'bg-brand-700 text-white'
            : active
              ? 'bg-gold-300 text-warm-800 ring-4 ring-gold-100'
              : 'bg-warm-100 text-warm-500',
        )}
      >
        {done ? <Check size={12} strokeWidth={3} /> : n}
      </div>
      <div className="flex-1 min-w-0">
        <div
          className={cls(
            'text-[13.5px] font-medium',
            active ? 'text-warm-900' : 'text-warm-700',
          )}
        >
          {title}
          {active && (
            <span className="ml-2 text-[10px] tracking-[0.14em] uppercase text-gold-600 font-semibold">
              Ahora
            </span>
          )}
        </div>
        <div className="text-[12.5px] text-warm-500 mt-0.5 leading-relaxed">{body}</div>
      </div>
    </li>
  )
}
