import { CalendarClock, CheckCircle } from 'lucide-react'
import { SettingsHeader, PreviewNotice } from './_primitives'

/**
 * `/configuracion/suscripcion` — info del plan SaaS del salón.
 *
 * Mockup visual: el plan está hardcoded. Cuando hagamos el sprint de
 * billing, esto consume del endpoint del panel SaaS-admin (que aún
 * no existe). Los pagos del historial vendrán de una tabla
 * SubscriptionPayments.
 *
 * No usa SaveBar — es informativo y los botones "Cambiar plan" /
 * "Pagar ahora" abrirán un modal o redirigirán cuando esté el flujo.
 */

const PAYMENTS = [
  { date: '1 jun 2026', plan: 'Profesional', amount: 90000, status: 'Pagado', method: 'Bancolombia' },
  { date: '1 may 2026', plan: 'Profesional', amount: 90000, status: 'Pagado', method: 'Bancolombia' },
  { date: '1 abr 2026', plan: 'Profesional', amount: 90000, status: 'Pagado', method: 'Nequi' },
  { date: '1 mar 2026', plan: 'Básico',      amount: 50000, status: 'Pagado', method: 'Bancolombia' },
]

const fmt = (n: number) => '$' + Math.round(n).toLocaleString('es-CO')

export function SuscripcionPage() {
  return (
    <div className="px-6 lg:px-10 py-8 max-w-3xl">
      <PreviewNotice message="Vista previa del diseño · el plan está hardcoded mientras no exista el panel SaaS-admin." />
      <SettingsHeader
        eyebrow="Ajustes del salón"
        title="Suscripción y facturación"
        desc="Tu plan actual de BellaSync, el próximo cobro y el historial de pagos de tu salón."
      />

      {/* PLAN ACTUAL — card dark con highlight verde */}
      <div className="rounded-2xl bg-warm-800 text-white p-6 relative overflow-hidden">
        <div className="absolute -right-16 -top-16 w-48 h-48 rounded-full bg-brand-700/30 blur-2xl" />
        <div className="relative flex items-start justify-between flex-wrap gap-4">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-300 font-medium">
              Plan actual
            </div>
            <div className="font-serif text-[32px] leading-tight mt-1">Profesional</div>
            <div className="text-[12.5px] text-warm-300 mt-1">
              4–8 estilistas · 500 citas/mes · reportes avanzados
            </div>
          </div>
          <div className="text-right">
            <div className="font-serif text-[34px] tabular-nums leading-none">$90.000</div>
            <div className="text-[11.5px] text-warm-300 mt-1">/ mes</div>
          </div>
        </div>
        <div className="relative mt-5 pt-4 border-t border-white/15 flex items-center justify-between flex-wrap gap-3">
          <div className="text-[12.5px] text-warm-200 flex items-center gap-2">
            <CalendarClock size={14} />
            Próximo cobro:{' '}
            <span className="text-white font-medium">1 jul 2026</span>
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              className="px-3.5 py-2 rounded-lg bg-white/10 hover:bg-white/15 text-white text-[12.5px] font-medium"
            >
              Cambiar plan
            </button>
            <button
              type="button"
              className="px-3.5 py-2 rounded-lg bg-gold-300 hover:bg-gold-200 text-warm-800 text-[12.5px] font-medium"
            >
              Pagar ahora
            </button>
          </div>
        </div>
      </div>

      {/* STATUS */}
      <div className="mt-5 rounded-xl bg-brand-50 border border-brand-200 px-4 py-3 flex items-center gap-2.5">
        <CheckCircle size={16} className="text-brand-700" />
        <span className="text-[12.5px] text-brand-800">
          Tu cuenta está al día. Gracias por confiar en BellaSync 💛
        </span>
      </div>

      {/* HISTORIAL DE PAGOS */}
      <div className="mt-7">
        <div className="text-[12.5px] font-semibold text-warm-800 mb-3">
          Historial de pagos
        </div>
        <div className="rounded-xl border border-warm-150 bg-white overflow-hidden">
          <table className="w-full text-[13px]">
            <thead>
              <tr className="bg-warm-50 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
                <th className="text-left font-medium px-4 py-2.5">Fecha</th>
                <th className="text-left font-medium px-4 py-2.5 hidden sm:table-cell">Plan</th>
                <th className="text-left font-medium px-4 py-2.5 hidden sm:table-cell">Método</th>
                <th className="text-right font-medium px-4 py-2.5">Monto</th>
                <th className="text-right font-medium px-4 py-2.5">Estado</th>
              </tr>
            </thead>
            <tbody>
              {PAYMENTS.map((p, i) => (
                <tr
                  key={i}
                  className="border-b border-warm-100 last:border-0"
                >
                  <td className="px-4 py-3 text-warm-800">{p.date}</td>
                  <td className="px-4 py-3 text-warm-600 hidden sm:table-cell">{p.plan}</td>
                  <td className="px-4 py-3 text-warm-600 hidden sm:table-cell">{p.method}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-warm-800 font-medium">
                    {fmt(p.amount)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className="text-[10.5px] tracking-[0.1em] uppercase font-semibold text-brand-700 bg-brand-50 px-2 py-0.5 rounded-md">
                      {p.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
