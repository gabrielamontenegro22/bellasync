import { Check } from 'lucide-react'
import { cls } from '@/lib/cls'
import type { StepInfo } from '../types'

export const STEPS: StepInfo[] = [
  { n: 1, title: 'Crea tu cuenta',         sub: 'Tu acceso al panel' },
  { n: 2, title: 'Información del salón',  sub: 'Datos del negocio' },
  { n: 3, title: 'Horario de atención',    sub: 'Cuándo abren' },
  { n: 4, title: 'Servicios iniciales',    sub: 'Qué ofrecen' },
  { n: 5, title: 'Elige tu plan',          sub: 'Y empezar' },
]

interface StepperProps {
  step: number
  max: number
  onGoTo?: (n: number) => void
}

/**
 * Stepper visual — barra de progreso + lista numerada de pasos.
 * Replica fielmente el Stepper inline de onboarding-shell.jsx.
 */
export function Stepper({ step, max, onGoTo }: StepperProps) {
  const pct = ((step - 1) / (STEPS.length - 1)) * 100

  return (
    <div>
      <div className="flex items-center gap-3 mb-3">
        <div className="flex-1 h-1 rounded-full bg-warm-200 overflow-hidden">
          <div
            className="h-full bg-brand-700 transition-all duration-500"
            style={{ width: pct + '%' }}
          />
        </div>
        <div className="text-[11px] tabular-nums tracking-wider text-warm-500 font-medium">
          {step}/{STEPS.length}
        </div>
      </div>

      <ol className="hidden md:flex items-center gap-1 text-[11.5px]">
        {STEPS.map((s) => {
          const done = s.n < step
          const cur  = s.n === step
          const reachable = s.n <= max
          const clickable = reachable && onGoTo != null

          const Tag = clickable ? 'button' : 'div'

          return (
            <li key={s.n} className="flex-1">
              <Tag
                onClick={clickable ? () => onGoTo!(s.n) : undefined}
                className={cls(
                  'w-full flex items-center gap-2 px-2.5 py-1.5 rounded-md transition text-left',
                  cur
                    ? 'bg-brand-50 text-brand-800'
                    : done
                      ? 'text-warm-700'
                      : 'text-warm-400',
                  clickable && 'hover:bg-warm-100',
                )}
              >
                <span
                  className={cls(
                    'w-5 h-5 rounded-full flex items-center justify-center flex-shrink-0 text-[10px] font-semibold',
                    done
                      ? 'bg-brand-700 text-white'
                      : cur
                        ? 'bg-white border-2 border-brand-700 text-brand-700'
                        : reachable
                          ? 'bg-warm-200 text-warm-600'
                          : 'bg-warm-100 text-warm-400',
                  )}
                >
                  {done ? <Check size={12} strokeWidth={3} /> : s.n}
                </span>
                <span className="font-medium truncate">{s.title}</span>
              </Tag>
            </li>
          )
        })}
      </ol>
    </div>
  )
}
