import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Calendar, CheckCircle, Gift, MessageCircle, AtSign,
  Plus, Pencil, Scissors, Wallet,
} from 'lucide-react'
import type { CustomerResponse } from '@/api/customers'
import type { AppointmentResponse } from '@/api/appointments'
import { cls } from '@/lib/cls'
import { useCustomer, useCustomerAppointments } from '../hooks'
import { useCustomerPayments } from '@/features/payments/hooks'
import { getPaymentBadge } from '@/features/payments/paymentBadge'
import {
  TAG_BADGE, ageFromBday, fmtBday, fmtCop, fmtDateTime,
  fmtMonth, initialsOf, relativeFrom, toneOf, whatsappLink,
} from '../lib/customerLook'

type TabId = 'resumen' | 'historial' | 'ficha' | 'pagos'

const TABS: { id: TabId; label: string }[] = [
  { id: 'resumen',   label: 'Resumen' },
  { id: 'historial', label: 'Historial' },
  { id: 'ficha',     label: 'Ficha técnica' },
  { id: 'pagos',     label: 'Pagos' },
]

interface ClientDetailProps {
  /** Snapshot del cliente capturado al hacer click en la lista. Sirve de
   *  fallback mientras carga la versión fresca del backend. */
  fallback: CustomerResponse
  onEdit: () => void
  onNewAppointment?: () => void
}

/**
 * Panel derecho del CRM: ficha completa del cliente seleccionado.
 *
 * Header grande con avatar pastel + tag + acciones (WhatsApp / Agendar /
 * Editar). Debajo, tabs:
 *  - Resumen: 4 stat cards + card destacada de próxima cita + servicios
 *    favoritos (top 3) + card cumpleaños.
 *  - Historial: timeline vertical con citas.
 *  - Ficha técnica: placeholder hasta que tengamos el módulo.
 *  - Pagos: placeholder hasta que tengamos el módulo.
 *
 * Pide stats frescos por `useCustomer(id)` para que tras editar la ficha
 * o que pasen citas, el panel reflejé lo último. `fallback` evita el
 * flash de pantalla en blanco mientras la query carga la primera vez.
 */
export function ClientDetail({ fallback, onEdit, onNewAppointment }: ClientDetailProps) {
  const [tab, setTab] = useState<TabId>('resumen')
  const navigate = useNavigate()
  const { data: fresh } = useCustomer(fallback.id)
  const client = fresh ?? fallback
  const tone = toneOf(client.id)
  const tag = TAG_BADGE[client.tag]

  const { data: appointments = [], isLoading: loadingAppts } = useCustomerAppointments(client.id)

  const bday = fmtBday(client.birthday)
  const age = ageFromBday(client.birthday)
  const wa = whatsappLink(client.phone)

  return (
    <main className="flex-1 min-w-0 flex flex-col bg-warm-50 overflow-y-auto">
      {/* HEAD */}
      <div className="bg-white border-b border-warm-150 px-6 lg:px-10 py-7">
        <div className="flex items-start gap-5">
          <div className={cls(
            'w-20 h-20 lg:w-24 lg:h-24 rounded-full flex items-center justify-center font-serif text-[34px] flex-shrink-0',
            tone.bg, tone.fg,
          )}>
            {initialsOf(client.fullName)}
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className={cls(
                'text-[10.5px] tracking-[0.18em] uppercase font-medium px-2 py-0.5 rounded-md border',
                tag.bg, tag.fg, tag.border,
              )}>
                Cliente {client.tag.toLowerCase()}
              </span>
              <span className="text-[11px] text-warm-400">·</span>
              <span className="text-[11.5px] text-warm-500">{client.visits} visitas</span>
              <span className="text-[11px] text-warm-400">·</span>
              <span className="text-[11.5px] text-warm-500">
                cliente desde {new Date(client.createdAt).getFullYear()}
              </span>
              {!client.isActive && (
                <>
                  <span className="text-[11px] text-warm-400">·</span>
                  <span className="text-[11.5px] text-terra-500 font-medium">Archivado</span>
                </>
              )}
            </div>
            <h2 className="font-serif text-[36px] lg:text-[44px] leading-[1.02] text-warm-800 tracking-tight mt-2">
              {client.fullName}
            </h2>
            <div className="mt-3 flex items-center gap-x-5 gap-y-1.5 flex-wrap text-[13px] text-warm-600">
              <span className="flex items-center gap-1.5">
                <MessageCircle size={13} className="text-brand-700" />
                <span className="tabular-nums">{client.phone}</span>
              </span>
              {client.email && (
                <span className="flex items-center gap-1.5">
                  <AtSign size={13} className="text-warm-400" />
                  {client.email}
                </span>
              )}
              {bday && (
                <span className="flex items-center gap-1.5">
                  <Gift size={13} className="text-gold-500" />
                  {bday}{age !== null ? ` · ${age} años` : ''}
                </span>
              )}
            </div>
          </div>

          <div className="hidden md:flex flex-col items-stretch gap-2 flex-shrink-0">
            <a
              href={wa}
              target="_blank"
              rel="noreferrer"
              className="px-4 py-2.5 rounded-lg bg-[#25D366] hover:brightness-110 text-white text-[13px] font-medium flex items-center justify-center gap-1.5 shadow-soft"
            >
              <MessageCircle size={14} /> Enviar WhatsApp
            </a>
            {onNewAppointment && client.isActive && (
              <button
                type="button"
                onClick={onNewAppointment}
                className="px-4 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[13px] font-medium flex items-center justify-center gap-1.5"
              >
                <Plus size={14} /> Agendar cita
              </button>
            )}
            <button
              type="button"
              onClick={onEdit}
              className="px-4 py-2 rounded-lg text-warm-600 hover:bg-warm-100 text-[12.5px] flex items-center justify-center gap-1.5"
            >
              <Pencil size={13} /> Editar
            </button>
          </div>
        </div>

        {/* TABS */}
        <div className="mt-6 flex items-center gap-1 border-b border-warm-150 -mb-7 overflow-x-auto">
          {TABS.map(t => (
            <button
              key={t.id}
              type="button"
              onClick={() => setTab(t.id)}
              className={cls(
                'px-4 py-3 text-[13.5px] font-medium border-b-2 -mb-px transition whitespace-nowrap',
                tab === t.id
                  ? 'border-brand-700 text-brand-800'
                  : 'border-transparent text-warm-500 hover:text-warm-700',
              )}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      {/* BODY */}
      <div className="px-6 lg:px-10 py-7">
        {tab === 'resumen' && (
          <ResumenTab
            client={client}
            appointments={appointments}
            loading={loadingAppts}
            onGoToAgenda={iso => {
              const d = new Date(iso).toISOString().slice(0, 10)
              navigate(`/agenda?date=${d}`)
            }}
            onNewAppointment={onNewAppointment}
          />
        )}
        {tab === 'historial' && (
          <HistorialTab appointments={appointments} loading={loadingAppts} />
        )}
        {tab === 'ficha' && <FichaTab client={client} />}
        {tab === 'pagos' && <PagosTab customerId={client.id} />}
      </div>
    </main>
  )
}

// ---------- TAB: RESUMEN ----------
function ResumenTab({
  client,
  appointments,
  loading,
  onGoToAgenda,
  onNewAppointment,
}: {
  client: CustomerResponse
  appointments: AppointmentResponse[]
  loading: boolean
  onGoToAgenda: (isoDate: string) => void
  onNewAppointment?: () => void
}) {
  const completed = useMemo(
    () => appointments.filter(a => a.status === 'Completed'),
    [appointments],
  )
  const upcoming = useMemo(() => {
    const future = appointments.filter(a =>
      (a.status === 'Pending' || a.status === 'Confirmed') &&
      new Date(a.startAt) > new Date()
    )
    return future.sort((a, b) => +new Date(a.startAt) - +new Date(b.startAt))[0]
  }, [appointments])

  const totalSpent = completed.reduce((acc, a) => acc + (a.priceSnapshot ?? 0), 0)

  // top 3 servicios
  const topServices = useMemo(() => {
    const counts: Record<string, number> = {}
    completed.forEach(a => { counts[a.serviceName] = (counts[a.serviceName] ?? 0) + 1 })
    return Object.entries(counts).sort((a, b) => b[1] - a[1]).slice(0, 3)
  }, [completed])

  const bday = fmtBday(client.birthday)
  const age = ageFromBday(client.birthday)

  return (
    <div className="space-y-6 animate-fade">
      {/* stats */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <Stat label="Total visitas" value={client.visits.toString()} sub="completadas" />
        <Stat
          label="Última visita"
          value={client.lastVisitAt ? relativeFrom(client.lastVisitAt) : '—'}
          sub={client.lastVisitAt ? fmtMonth(client.lastVisitAt) : 'sin historial'}
        />
        <Stat label="Total invertido" value={fmtCop(totalSpent)} sub="lifetime" />
        <Stat
          label="Estilista"
          value={client.preferredStylistName?.split(' ')[0] ?? '—'}
          sub={client.preferredStylistName?.split(' ').slice(1).join(' ') || 'sin asignar'}
        />
      </div>

      {/* próxima cita */}
      {loading ? (
        <div className="rounded-2xl border border-warm-150 bg-white p-7 text-center text-[13px] text-warm-500">
          Cargando historial…
        </div>
      ) : upcoming ? (
        <div className="rounded-2xl bg-brand-700 text-white p-6 lg:p-7 relative overflow-hidden">
          <div className="absolute -right-16 -bottom-16 w-56 h-56 rounded-full bg-white/5" />
          <div className="absolute -right-32 -top-20 w-48 h-48 rounded-full bg-white/5" />
          <div className="relative">
            <div className="text-[10.5px] tracking-[0.2em] uppercase text-brand-200 font-medium">
              Próxima cita
            </div>
            <div className="font-serif text-[28px] lg:text-[34px] leading-tight mt-1.5">
              {upcoming.serviceName}
            </div>
            <div className="mt-3 flex items-center gap-x-5 gap-y-1.5 flex-wrap text-[13.5px] text-brand-100">
              <span className="flex items-center gap-1.5">
                <Calendar size={14} /> {fmtDateTime(upcoming.startAt)}
              </span>
              <span className="flex items-center gap-1.5">
                <Scissors size={14} /> Con {upcoming.stylistName}
              </span>
              <span className="flex items-center gap-1.5">
                <Wallet size={14} /> {fmtCop(upcoming.priceSnapshot)}
              </span>
            </div>
            <div className="mt-5 flex items-center gap-2 flex-wrap">
              <button
                type="button"
                onClick={() => onGoToAgenda(upcoming.startAt)}
                className="px-4 py-2 rounded-lg bg-white text-brand-800 text-[13px] font-medium hover:bg-cream"
              >
                Ver en agenda
              </button>
              {onNewAppointment && (
                <button
                  type="button"
                  onClick={onNewAppointment}
                  className="px-4 py-2 rounded-lg border border-white/30 text-white text-[13px] hover:bg-white/10"
                >
                  Agendar otra
                </button>
              )}
            </div>
          </div>
        </div>
      ) : (
        <div className="rounded-2xl border-2 border-dashed border-warm-200 bg-white p-7 text-center">
          <div className="w-12 h-12 rounded-full bg-warm-100 text-warm-500 flex items-center justify-center mx-auto">
            <Calendar size={20} />
          </div>
          <div className="font-serif text-[20px] text-warm-700 mt-3">Sin próxima cita</div>
          <div className="text-[13px] text-warm-500 mt-1">¿Quieres invitarla a volver?</div>
          {onNewAppointment && client.isActive && (
            <button
              type="button"
              onClick={onNewAppointment}
              className="mt-4 px-4 py-2 rounded-lg bg-brand-700 text-white text-[13px] font-medium hover:bg-brand-800"
            >
              Agendar ahora
            </button>
          )}
        </div>
      )}

      {/* dos columnas: top servicios + bday */}
      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 rounded-2xl bg-white border border-warm-150 p-6">
          <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-500 font-medium">
            Servicios favoritos
          </div>
          <div className="mt-3 space-y-3">
            {topServices.length === 0 && (
              <div className="text-[13px] text-warm-500 italic py-2">
                {loading ? 'Cargando…' : 'Aún no hay historial suficiente.'}
              </div>
            )}
            {topServices.map(([name, count], i) => {
              const pct = (count / completed.length) * 100
              return (
                <div key={name}>
                  <div className="flex items-center justify-between mb-1.5">
                    <div className="flex items-center gap-2.5">
                      <span className="font-serif text-[15px] text-warm-400 tabular-nums">
                        0{i + 1}
                      </span>
                      <span className="text-[13.5px] text-warm-800">{name}</span>
                    </div>
                    <span className="text-[12px] text-warm-500 tabular-nums">
                      {count} {count === 1 ? 'vez' : 'veces'}
                    </span>
                  </div>
                  <div className="h-1 rounded-full bg-warm-100 overflow-hidden">
                    <div
                      className="h-full rounded-full bg-gold-400"
                      style={{ width: pct + '%' }}
                    />
                  </div>
                </div>
              )
            })}
          </div>
        </div>

        {bday ? (
          <div className="rounded-2xl bg-gold-50/70 border border-gold-200 p-6">
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium">
              Cumpleaños
            </div>
            <div className="font-serif text-[26px] text-warm-800 leading-tight mt-2">{bday}</div>
            {age !== null && (
              <div className="text-[12.5px] text-warm-600 mt-1">{age} años</div>
            )}
          </div>
        ) : (
          <div className="rounded-2xl bg-warm-50 border border-warm-200 p-6 flex flex-col items-center justify-center text-center">
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium">
              Cumpleaños
            </div>
            <div className="text-[13px] text-warm-500 italic mt-2">No registrado.</div>
          </div>
        )}
      </div>
    </div>
  )
}

// ---------- TAB: HISTORIAL ----------
function HistorialTab({
  appointments,
  loading,
}: {
  appointments: AppointmentResponse[]
  loading: boolean
}) {
  if (loading) {
    return <div className="text-[13px] text-warm-500">Cargando historial…</div>
  }

  if (appointments.length === 0) {
    return (
      <div className="rounded-2xl border-2 border-dashed border-warm-200 bg-white p-10 text-center">
        <Calendar size={28} className="mx-auto text-warm-400" />
        <div className="font-serif text-[20px] text-warm-700 mt-3">Sin historial todavía</div>
        <div className="text-[13px] text-warm-500 mt-1">
          Cuando agendes su primera cita, aparecerá aquí.
        </div>
      </div>
    )
  }

  const now = new Date()

  return (
    <div className="animate-fade">
      <div className="text-[12.5px] text-warm-500 mb-5">
        {appointments.length} citas en total
      </div>
      <ol className="relative">
        <div className="absolute left-[15px] top-2 bottom-2 w-px bg-warm-200" />
        {appointments.map(a => {
          const isUpcoming =
            (a.status === 'Pending' || a.status === 'Confirmed') &&
            new Date(a.startAt) > now
          const isCompleted = a.status === 'Completed'
          const isCancelled = a.status === 'Cancelled' || a.status === 'NoShow'

          return (
            <li key={a.id} className="relative pl-12 pb-5">
              <div className={cls(
                'absolute left-0 top-0.5 w-8 h-8 rounded-full flex items-center justify-center border-2',
                isUpcoming
                  ? 'bg-brand-700 border-brand-700 text-white'
                  : isCancelled
                    ? 'bg-warm-50 border-warm-200 text-warm-400'
                    : 'bg-white border-warm-200 text-warm-500',
              )}>
                {isUpcoming
                  ? <Calendar size={13} />
                  : isCompleted
                    ? <CheckCircle size={13} />
                    : <span className="text-[10px]">✕</span>}
              </div>
              <div className="rounded-xl bg-white border border-warm-150 p-4 hover:shadow-soft transition">
                <div className="flex items-start justify-between gap-3 flex-wrap">
                  <div className="min-w-0">
                    <div className="font-serif text-[18px] text-warm-800 leading-tight">
                      {a.serviceName}
                    </div>
                    <div className="text-[12px] text-warm-500 mt-1 flex items-center gap-x-3 gap-y-0.5 flex-wrap">
                      <span className="flex items-center gap-1">
                        <Calendar size={11} />
                        {isUpcoming ? fmtDateTime(a.startAt) : fmtMonth(a.startAt)}
                      </span>
                      <span className="text-warm-300">·</span>
                      <span>Con {a.stylistName}</span>
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="font-serif text-[18px] text-warm-800 tabular-nums leading-none">
                      {fmtCop(a.priceSnapshot)}
                    </div>
                    <div className={cls(
                      'text-[10.5px] tracking-[0.14em] uppercase font-medium mt-1.5',
                      isUpcoming ? 'text-brand-700'
                        : isCompleted ? 'text-warm-400'
                          : 'text-terra-500',
                    )}>
                      {isUpcoming
                        ? 'Próxima'
                        : isCompleted ? 'Completada'
                          : a.status === 'NoShow' ? 'No-show' : 'Cancelada'}
                    </div>
                  </div>
                </div>
              </div>
            </li>
          )
        })}
      </ol>
    </div>
  )
}

// ---------- TAB: FICHA TÉCNICA ----------
function FichaTab({ client }: { client: CustomerResponse }) {
  return (
    <div className="animate-fade space-y-5">
      <div>
        <h3 className="font-serif text-[24px] text-warm-800 leading-tight">Ficha técnica</h3>
        <div className="text-[12.5px] text-warm-500 mt-0.5">
          Información clínica y de preferencias para el equipo.
        </div>
      </div>

      {/* Por ahora solo mostramos las notas internas del cliente.
          Fórmulas, preferencias detalladas, alergias estructuradas
          llegarán cuando construyamos el módulo Ficha técnica. */}
      <div className="rounded-2xl border border-warm-150 bg-white p-6">
        <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium mb-3">
          Notas internas
        </div>
        {client.notes
          ? <div className="text-[13.5px] text-warm-800 leading-relaxed whitespace-pre-wrap">{client.notes}</div>
          : <div className="text-[13px] text-warm-400 italic">
              Sin notas todavía. Usa el botón Editar para añadir alergias,
              preferencias o cualquier observación útil para el equipo.
            </div>}
      </div>

      <div className="rounded-2xl border-2 border-dashed border-warm-200 bg-warm-50/40 p-6 text-center">
        <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium">
          Próximamente
        </div>
        <div className="font-serif text-[20px] text-warm-700 mt-2">
          Fórmulas y preferencias estructuradas
        </div>
        <div className="text-[13px] text-warm-500 mt-1 max-w-md mx-auto">
          Tipo de cabello/piel, alergias, fórmulas usadas con resultados,
          bebida favorita y conversación. Disponible en próximas versiones.
        </div>
      </div>
    </div>
  )
}

// ---------- TAB: PAGOS ----------
function PagosTab({ customerId }: { customerId: string }) {
  const { data: payments = [], isLoading } = useCustomerPayments(customerId)

  const totalLifetime = payments.reduce((acc, p) => acc + p.total, 0)
  const totalTip = payments.reduce((acc, p) => acc + p.tip, 0)

  return (
    <div className="animate-fade space-y-5">
      {/* Stats arriba — réplica del mockup */}
      <div className="grid sm:grid-cols-3 gap-3">
        <Stat label="Total pagado lifetime" value={fmtCop(totalLifetime)} />
        <Stat label="Pagos registrados"     value={payments.length.toString()} />
        <Stat label="Propinas acumuladas"   value={fmtCop(totalTip)} sub="incluido en total" />
      </div>

      {isLoading ? (
        <div className="rounded-2xl border border-warm-150 bg-white p-10 text-center text-[13px] text-warm-500">
          Cargando pagos…
        </div>
      ) : payments.length === 0 ? (
        <div className="rounded-2xl border-2 border-dashed border-warm-200 bg-warm-50/40 p-10 text-center">
          <Wallet size={28} className="mx-auto text-warm-400" />
          <div className="font-serif text-[22px] text-warm-700 mt-3">Sin pagos registrados</div>
          <div className="text-[13px] text-warm-500 mt-1 max-w-md mx-auto">
            Cuando la cliente termine un servicio y registres el cobro
            desde el agenda, los pagos aparecerán acá.
          </div>
        </div>
      ) : (
        <div className="rounded-2xl bg-white border border-warm-150 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-[13px]">
              <thead>
                <tr className="bg-warm-50/60 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
                  <th className="text-left font-medium pl-6 pr-3 py-3">Fecha</th>
                  <th className="text-left font-medium px-3 py-3">Servicio</th>
                  <th className="text-left font-medium px-3 py-3">Método</th>
                  <th className="text-left font-medium px-3 py-3">Referencia</th>
                  <th className="text-right font-medium pr-6 pl-3 py-3">Monto</th>
                </tr>
              </thead>
              <tbody>
                {payments.map(p => {
                  const badge = getPaymentBadge(p.method, p.provider)
                  return (
                    <tr key={p.id} className="border-b border-warm-100 last:border-0 hover:bg-warm-50/40">
                      <td className="py-3.5 pl-6 pr-3 text-warm-700 tabular-nums">
                        {fmtMonth(p.appointmentStartAt)}
                      </td>
                      <td className="py-3.5 px-3 text-warm-800">{p.serviceName}</td>
                      <td className="py-3.5 px-3">
                        <span className={cls(
                          'text-[11.5px] px-2 py-0.5 rounded-md',
                          badge.className,
                        )}>
                          {badge.label}
                        </span>
                      </td>
                      <td className="py-3.5 px-3 font-mono text-[11.5px] text-warm-500">
                        {p.reference ?? '—'}
                      </td>
                      <td className="py-3.5 pr-6 pl-3 text-right tabular-nums font-medium text-warm-800">
                        {fmtCop(p.total)}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <div className="px-6 py-3.5 border-t border-warm-150 flex items-center justify-between text-[12px] text-warm-500 bg-warm-50/40">
            <span>{payments.length} pagos</span>
            <span className="tabular-nums">
              Total: <strong className="text-warm-800">{fmtCop(totalLifetime)}</strong>
            </span>
          </div>
        </div>
      )}
    </div>
  )
}

// ---------- Componentes auxiliares ----------
function Stat({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div className="rounded-xl bg-white border border-warm-150 p-4">
      <div className="text-[10.5px] tracking-[0.16em] uppercase text-warm-500 font-medium">{label}</div>
      <div className="font-serif text-[26px] lg:text-[28px] leading-none text-warm-800 mt-2 tabular-nums">
        {value}
      </div>
      {sub && <div className="text-[11.5px] text-warm-500 mt-1.5">{sub}</div>}
    </div>
  )
}
