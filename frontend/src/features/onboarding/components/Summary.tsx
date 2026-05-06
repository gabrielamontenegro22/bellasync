import type { ReactNode } from 'react'
import { MessageCircle } from 'lucide-react'
import type { WizardData } from '../types'
import { PLANS } from '../data/plans'

const fmtCOP = (n: number) => '$' + Math.round(n).toLocaleString('es-CO')

interface SummaryProps {
  data: WizardData
}

/**
 * Sidebar dark con resumen vivo del salón.
 * Replica el componente Summary de onboarding-shell.jsx.
 */
export function Summary({ data }: SummaryProps) {
  const onCount  = Object.values(data.servicesOn).filter(Boolean).length
  const openDays = Object.values(data.hours).filter(Boolean).length
  const sel = PLANS.find((p) => p.id === data.plan)

  return (
    <aside className="bg-warm-800 text-white rounded-2xl p-7 sticky top-6">
      {/* brand */}
      <div className="flex items-center gap-2.5 mb-8">
        <div className="w-8 h-8 rounded-lg bg-brand-500 flex items-center justify-center text-white">
          <span className="font-serif text-[18px] leading-none translate-y-[1px]">B</span>
        </div>
        <div className="font-serif text-[20px] tracking-tight leading-none">BellaSync</div>
      </div>

      <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-300 font-medium mb-3">
        Tu salón
      </div>

      {data.salonName ? (
        <div className="font-serif text-[28px] leading-tight">{data.salonName}</div>
      ) : (
        <div className="font-serif text-[28px] leading-tight text-white/30">Tu salón aquí</div>
      )}
      {data.city && <div className="text-[12.5px] text-warm-300 mt-1">{data.city}</div>}

      <div className="mt-6 space-y-3 text-[12.5px]">
        {data.ownerName && <SumRow k="Propietaria" v={data.ownerName} />}
        {data.email     && <SumRow k="Correo"      v={data.email} />}
        {data.phone     && <SumRow k="WhatsApp"    v={data.phone} />}
        {openDays > 0   && <SumRow k="Días abiertos" v={`${openDays} días`} />}
        {onCount > 0    && <SumRow k="Servicios" v={`${onCount} activos`} />}
        {sel && (
          <SumRow
            k="Plan"
            v={<span className="text-gold-300">{sel.name} · {fmtCOP(sel.price)}/mes</span>}
          />
        )}
      </div>

      <div className="mt-8 pt-6 border-t border-white/10">
        <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-300 font-medium mb-2">
          ¿Necesitas ayuda?
        </div>
        <a href="#" className="flex items-center gap-2 text-[13px] text-white hover:text-gold-200">
          <MessageCircle size={14} className="text-brand-300" />
          +57 300 123 4567
        </a>
        <p className="text-[11.5px] text-warm-400 mt-1.5">
          Te respondemos en menos de 5 minutos en horario hábil.
        </p>
      </div>
    </aside>
  )
}

function SumRow({ k, v }: { k: string; v: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <span className="text-warm-400 text-[11px] uppercase tracking-wider whitespace-nowrap">
        {k}
      </span>
      <span className="text-white text-right truncate">{v}</span>
    </div>
  )
}
