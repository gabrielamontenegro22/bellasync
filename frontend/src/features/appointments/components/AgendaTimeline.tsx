import { useEffect, useMemo, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { listStylists, type StylistResponse } from '@/api/stylists'
import type { AppointmentResponse, AppointmentStatus } from '@/api/appointments'
import { cls } from '@/lib/cls'
import { initialsOf, toneOf } from '@/features/customers/lib/customerLook'

/**
 * Vista timeline tipo Google Calendar — columnas por estilista, eje vertical
 * de horas. Es la vista principal de la Agenda y replica el layout del
 * mockup `app.jsx`.
 *
 * Layout:
 *   ┌────┬─────────┬─────────┬─────────┬─────────┐
 *   │HORA│ Carolina│ Andrea  │ Lina    │ Juliana │   ← header sticky
 *   ├────┼─────────┼─────────┼─────────┼─────────┤
 *   │ 8 ─│         │         │         │         │
 *   │ 9 ─│ [cita ] │         │ [cita ] │         │
 *   │10 ─│ [cita ] │ [cita ] │         │ [cita ] │
 *   │... │         │         │         │         │
 *   └────┴─────────┴─────────┴─────────┴─────────┘
 *
 * Cada cita es un bloque posicionado absoluto dentro de su columna,
 * con `top` y `height` proporcionales a la hora de inicio y duración.
 */

// Día visible: 8 AM – 9 PM
const DAY_START_MIN = 8 * 60
const DAY_END_MIN = 21 * 60
const PX_PER_MIN = 1.7  // 1 hora = 102 px → tarjetas con más aire visual
const RAIL_WIDTH = 72   // ancho del rail de horas (px)

// Margen alrededor de cada tarjeta de cita dentro de su slot temporal.
// Total = TOP + BOTTOM, da el "respiro" entre citas consecutivas.
const BLOCK_TOP_MARGIN = 4
const BLOCK_BOTTOM_MARGIN = 4

// Las horas del rail (cada hora en punto)
const HOURS = (() => {
  const out: number[] = []
  for (let m = DAY_START_MIN; m <= DAY_END_MIN; m += 60) out.push(m)
  return out
})()

interface AgendaTimelineProps {
  appointments: AppointmentResponse[]
  date: string              // "YYYY-MM-DD" local
  selectedId: string | null
  onSelect: (a: AppointmentResponse) => void
}

export function AgendaTimeline({ appointments, date, selectedId, onSelect }: AgendaTimelineProps) {
  // Estilistas del salón. Solo activos.
  const stylistsQ = useQuery({
    queryKey: ['stylists'],
    queryFn: () => listStylists(),
  })
  const stylists = (stylistsQ.data ?? []).filter(s => s.status !== 'Inactive')

  // Agrupar citas por stylist para acceso O(1) por columna
  const byStylist = useMemo(() => {
    const m: Record<string, AppointmentResponse[]> = {}
    stylists.forEach(s => { m[s.id] = [] })
    appointments.forEach(a => {
      if (m[a.stylistId]) m[a.stylistId].push(a)
    })
    return m
  }, [appointments, stylists])

  // Línea "ahora" si la fecha es hoy
  const today = formatLocalDate(new Date())
  const isToday = date === today
  const now = new Date()
  const nowMin = now.getHours() * 60 + now.getMinutes()
  const showNow = isToday && nowMin >= DAY_START_MIN && nowMin <= DAY_END_MIN
  const nowOffsetPx = (nowMin - DAY_START_MIN) * PX_PER_MIN

  const totalMinutes = DAY_END_MIN - DAY_START_MIN
  const railHeight = totalMinutes * PX_PER_MIN

  // ===== AUTO-SCROLL =====
  // Cuando se carga la timeline (o cambia la fecha), buscamos el primer
  // momento "interesante" y hacemos scroll del contenedor padre para que sea
  // visible. Sin esto, el rango fijo 8am-9pm queda con todas las citas tarde
  // fuera del viewport (el usuario ve 8am cuando llega a las 3pm).
  //
  // Prioridad:
  //   1. Si es hoy → scroll a la hora actual menos 1h de margen
  //   2. Si hay citas → scroll a la primera cita cronológica menos 1h
  //   3. Si no → no scroll (se queda en 8am)
  const wrapperRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!wrapperRef.current) return

    let targetMin: number | null = null
    if (isToday) {
      targetMin = nowMin
    } else if (appointments.length > 0) {
      targetMin = Math.min(...appointments.map(a => isoToLocalMinutes(a.startAt)))
    }
    if (targetMin === null) return

    // Clamp al rango visible y restar 1 hora de margen para contexto
    const clamped = Math.max(DAY_START_MIN, Math.min(DAY_END_MIN, targetMin))
    const offsetWithinTimeline = Math.max(0, (clamped - DAY_START_MIN - 60) * PX_PER_MIN)

    // Buscar el primer ancestro scrollable y hacer scrollTo
    let el: HTMLElement | null = wrapperRef.current
    while (el && el.parentElement) {
      el = el.parentElement
      const style = window.getComputedStyle(el)
      if (style.overflowY === 'auto' || style.overflowY === 'scroll') {
        const wrapperRect = wrapperRef.current.getBoundingClientRect()
        const parentRect = el.getBoundingClientRect()
        // posición top del wrapper relativa al scroll container
        const wrapperOffsetTop = wrapperRect.top - parentRect.top + el.scrollTop
        // +60 para que pase debajo del header sticky del grid
        el.scrollTo({
          top: wrapperOffsetTop + 60 + offsetWithinTimeline,
          behavior: 'auto',
        })
        break
      }
    }
    // intencional: la dependencia es appointments.length + date para no
    // disparar scroll por cada cambio del array (ej: refetch sin cambios)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appointments.length, date, isToday])

  if (stylistsQ.isLoading) {
    return (
      <div className="py-12 text-center text-[13px] text-warm-500">
        Cargando estilistas…
      </div>
    )
  }

  if (stylists.length === 0) {
    return (
      <div className="rounded-2xl border-2 border-dashed border-warm-200 bg-white p-10 text-center">
        <div className="font-serif text-[20px] text-warm-700">Sin estilistas activos</div>
        <div className="text-[13px] text-warm-500 mt-1 max-w-md mx-auto">
          Agrega estilistas en Configuración para poder ver la agenda.
        </div>
      </div>
    )
  }

  const gridCols = `${RAIL_WIDTH}px repeat(${stylists.length}, minmax(220px, 1fr))`

  return (
    <div ref={wrapperRef} className="bg-white border border-warm-150 rounded-xl overflow-hidden shadow-softer">
      {/* Header sticky con la fila de estilistas */}
      <div
        className="grid sticky top-0 z-20 bg-white border-b border-warm-150"
        style={{ gridTemplateColumns: gridCols }}
      >
        <div className="px-3 py-3 text-[11px] uppercase tracking-wider text-warm-400 font-medium bg-warm-50/40 border-r border-warm-150">
          Hora
        </div>
        {stylists.map(s => (
          <StylistHeader
            key={s.id}
            stylist={s}
            count={byStylist[s.id]?.length ?? 0}
          />
        ))}
      </div>

      {/* Body scrollable horizontalmente si hay muchos estilistas */}
      <div className="overflow-x-auto">
        <div
          className="grid relative"
          style={{ gridTemplateColumns: gridCols }}
        >
          {/* rail de horas con fondo sutil + líneas alineadas con columnas */}
          <div
            className="relative bg-warm-50/40 border-r border-warm-150"
            style={{ height: railHeight }}
          >
            {/* mismas líneas horizontales para mantener alineación visual */}
            {HOURS.map(m => (
              <div
                key={`line-${m}`}
                className="absolute left-0 right-0 bg-warm-150 pointer-events-none"
                style={{ top: (m - DAY_START_MIN) * PX_PER_MIN, height: 1 }}
              />
            ))}
            {/* etiquetas de hora */}
            {HOURS.map(m => (
              <div
                key={`label-${m}`}
                className="absolute right-3 -translate-y-1/2 text-[11.5px] text-warm-500 tabular-nums font-medium bg-warm-50/40 px-1"
                style={{ top: (m - DAY_START_MIN) * PX_PER_MIN }}
              >
                {fmtHour12(m)}
              </div>
            ))}
          </div>

          {/* columnas por estilista */}
          {stylists.map(s => (
            <StylistColumn
              key={s.id}
              appts={byStylist[s.id] ?? []}
              onSelect={onSelect}
              selectedId={selectedId}
              railHeight={railHeight}
            />
          ))}

          {/* línea "ahora" — se dibuja por encima de las columnas */}
          {showNow && (
            <div
              className="absolute left-[72px] right-0 pointer-events-none z-10"
              style={{ top: nowOffsetPx }}
            >
              <div className="flex items-center gap-2">
                <div className="text-[10.5px] font-semibold text-brand-700 bg-white px-1.5 py-0.5 rounded border border-brand-200 -ml-3 tabular-nums">
                  {String(now.getHours()).padStart(2, '0')}:{String(now.getMinutes()).padStart(2, '0')}
                </div>
                <div className="flex-1 h-[2px] bg-brand-500/70" />
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// ---------- Stylist column header ----------
function StylistHeader({ stylist, count }: { stylist: StylistResponse; count: number }) {
  const tone = toneOf(stylist.id)
  return (
    <div className="px-4 py-3 border-l border-warm-150 flex items-center gap-3">
      <div className={cls(
        'w-9 h-9 rounded-full flex items-center justify-center font-semibold text-[12.5px] flex-shrink-0',
        tone.bg, tone.fg,
      )}>
        {initialsOf(stylist.fullName)}
      </div>
      <div className="min-w-0 flex-1">
        <div className="text-[13.5px] font-medium text-warm-800 truncate">{stylist.fullName}</div>
        <div className="text-[11.5px] text-warm-500 truncate">{stylist.role || '—'}</div>
      </div>
      <div className="hidden xl:block text-[11px] text-warm-400 tabular-nums">
        {count} {count === 1 ? 'cita' : 'citas'}
      </div>
    </div>
  )
}

// ---------- Stylist column body ----------
function StylistColumn({
  appts, onSelect, selectedId, railHeight,
}: {
  appts: AppointmentResponse[]
  onSelect: (a: AppointmentResponse) => void
  selectedId: string | null
  railHeight: number
}) {
  // Líneas cada 30 min — las "en hora" más visibles, las de "media hora" sutiles.
  // Generamos divs absolutos de 1px (height: 1 + bg) en lugar de border-b para
  // garantizar que el navegador renderice la línea (border en div de altura 0 a
  // veces se colapsa).
  const totalRows = (DAY_END_MIN - DAY_START_MIN) / 30
  return (
    <div className="relative border-l border-warm-150" style={{ height: railHeight }}>
      {/* grid lines horizontales */}
      {Array.from({ length: totalRows + 1 }).map((_, i) => {
        const onHour = i % 2 === 0
        return (
          <div
            key={i}
            className={cls(
              'absolute left-0 right-0 pointer-events-none',
              onHour ? 'bg-warm-150' : 'bg-warm-100',
            )}
            style={{ top: i * 30 * PX_PER_MIN, height: 1 }}
          />
        )
      })}

      {/* bloques de cita */}
      {appts.map(a => (
        <ApptBlock
          key={a.id}
          appointment={a}
          selected={selectedId === a.id}
          onClick={() => onSelect(a)}
        />
      ))}
    </div>
  )
}

// ---------- Bloque de cita ----------
const STATUS_BLOCK: Record<AppointmentStatus, { block: string; dot: string }> = {
  Pending:    { block: 'bg-gold-50 border-gold-200 text-gold-700',     dot: 'bg-gold-400' },
  Confirmed:  { block: 'bg-brand-50 border-brand-200 text-brand-800',  dot: 'bg-brand-500' },
  InProgress: { block: 'bg-gold-100 border-gold-300 text-gold-700',    dot: 'bg-gold-500' },
  Completed:  { block: 'bg-warm-100 border-warm-200 text-warm-700',    dot: 'bg-warm-400' },
  Cancelled:  { block: 'bg-warm-50 border-warm-150 text-warm-400 line-through', dot: 'bg-warm-300' },
  NoShow:     { block: 'bg-terra-100 border-terra-300 text-terra-500', dot: 'bg-terra-500' },
}

function ApptBlock({
  appointment, selected, onClick,
}: {
  appointment: AppointmentResponse
  selected: boolean
  onClick: () => void
}) {
  const startMin = isoToLocalMinutes(appointment.startAt)
  const endMin   = isoToLocalMinutes(appointment.endAt)

  // Clamp dentro del día visible para que no se desborden por arriba/abajo
  const clampedStart = Math.max(startMin, DAY_START_MIN)
  const clampedEnd = Math.min(endMin, DAY_END_MIN)
  if (clampedEnd <= clampedStart) return null

  const top = (clampedStart - DAY_START_MIN) * PX_PER_MIN
  const height = (clampedEnd - clampedStart) * PX_PER_MIN
  const status = STATUS_BLOCK[appointment.status]

  // Altura útil = altura del slot menos el margen top+bottom para crear gap visual
  // entre tarjetas consecutivas (evita que se vean "tocando" verticalmente).
  const usableHeight = height - BLOCK_TOP_MARGIN - BLOCK_BOTTOM_MARGIN
  const isTiny = usableHeight < 38
  const isXL = usableHeight >= 96

  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        // left-2/right-2 = más margen horizontal vs el borde de la columna
        'absolute left-2 right-2 text-left rounded-lg border shadow-softer transition-all',
        isTiny ? 'px-2.5 py-1.5' : 'px-3 py-2',
        'hover:shadow-pop hover:-translate-y-[1px] hover:z-20',
        status.block,
        selected && 'ring-2 ring-brand-700 ring-offset-1 ring-offset-white z-20',
      )}
      style={{
        top: top + BLOCK_TOP_MARGIN,
        height: usableHeight,
        overflow: 'hidden',
      }}
    >
      {isTiny ? (
        <div className="flex items-center gap-1.5 h-full">
          <span className={cls('w-1.5 h-1.5 rounded-full flex-shrink-0', status.dot)} />
          <span className="font-medium text-[11.5px] leading-none truncate flex-1">
            {appointment.customerName}
          </span>
          <span className="text-[10px] opacity-60 leading-none tabular-nums flex-shrink-0">
            {fmtTime12(startMin)}
          </span>
        </div>
      ) : (
        <div className="flex items-start gap-2">
          <span className={cls('mt-1 w-1.5 h-1.5 rounded-full flex-shrink-0', status.dot)} />
          <div className="flex-1 min-w-0">
            <div className="font-medium text-[12.5px] leading-tight truncate">
              {appointment.customerName}
            </div>
            <div className="text-[11px] opacity-80 leading-tight truncate mt-0.5">
              {appointment.serviceName}
            </div>
            {isXL && (
              <div className="text-[10.5px] opacity-70 leading-tight mt-1 tabular-nums">
                {fmtTime12(startMin)} – {fmtTime12(endMin)}
              </div>
            )}
          </div>
        </div>
      )}
    </button>
  )
}

// ---------- Helpers ----------

function isoToLocalMinutes(iso: string): number {
  const d = new Date(iso)
  return d.getHours() * 60 + d.getMinutes()
}

function fmtHour12(min: number): string {
  const h = Math.floor(min / 60)
  const ampm = h >= 12 ? 'pm' : 'am'
  const hh = ((h + 11) % 12) + 1
  return `${hh} ${ampm}`
}

function fmtTime12(min: number): string {
  const h = Math.floor(min / 60)
  const m = min % 60
  const ampm = h >= 12 ? 'pm' : 'am'
  const hh = ((h + 11) % 12) + 1
  return m === 0 ? `${hh} ${ampm}` : `${hh}:${String(m).padStart(2, '0')} ${ampm}`
}

function formatLocalDate(d: Date): string {
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}
