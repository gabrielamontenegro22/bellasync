import { useEffect, useMemo, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { listStylists, type StylistResponse } from '@/api/stylists'
import type { AppointmentResponse, AppointmentStatus } from '@/api/appointments'
import { cls } from '@/lib/cls'
import { initialsOf, toneOf } from '@/features/customers/lib/customerLook'
import { useSalonHours } from '@/features/settings/useSalonHours'

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

// Rango default cuando no hay horario configurado para el día: 8 AM – 9 PM.
// Cuando SÍ hay horario configurado (lo más común), el timeline se ajusta
// dinámicamente a la apertura/cierre del día para que citas fuera del rango
// default sigan visibles. Ver computeDayRange() abajo.
const DEFAULT_START_MIN = 8 * 60
const DEFAULT_END_MIN = 21 * 60
const PX_PER_MIN = 1.7  // 1 hora = 102 px → tarjetas con más aire visual
const RAIL_WIDTH = 72   // ancho del rail de horas (px)

// Margen alrededor de cada tarjeta de cita dentro de su slot temporal.
// Total = TOP + BOTTOM, da el "respiro" entre citas consecutivas.
const BLOCK_TOP_MARGIN = 4
const BLOCK_BOTTOM_MARGIN = 4

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

  // Horario del salón — para pintar las franjas cerradas. Si no hay
  // configurado (opt-in), no se dibuja nada (= todos los días abiertos).
  const { data: salonHours } = useSalonHours()

  // Agrupar citas por stylist para acceso O(1) por columna
  const byStylist = useMemo(() => {
    const m: Record<string, AppointmentResponse[]> = {}
    stylists.forEach(s => { m[s.id] = [] })
    appointments.forEach(a => {
      if (m[a.stylistId]) m[a.stylistId].push(a)
    })
    return m
  }, [appointments, stylists])

  // Rango visible del timeline. Se calcula dinámicamente para cubrir TANTO:
  //   - El horario configurado del salón para este día (ej: cierra a las 12am)
  //   - El rango de citas que existen (ej: hay cita a las 22:00 aunque el
  //     salón "cierre" a las 21:00 — la cita igual debe verse)
  //   - El default 8am–9pm como fallback / piso mínimo de visibilidad
  // Sin esto, una admin que cambie su horario o cree citas fuera del default
  // ve el timeline cortado y pensa que la cita no existe.
  const { DAY_START_MIN, DAY_END_MIN } = useMemo(
    () => computeDayRange(date, salonHours, appointments),
    [date, salonHours, appointments],
  )

  // Las franjas cerradas se calculan DESPUÉS del rango — para que se clampeen
  // al viewport visible actual.
  const closedBands = useMemo(
    () => computeClosedBands(date, salonHours, DAY_START_MIN, DAY_END_MIN),
    [date, salonHours, DAY_START_MIN, DAY_END_MIN],
  )

  // Las horas del rail (cada hora en punto) — se recalculan si cambia el rango.
  const HOURS = useMemo(() => {
    const out: number[] = []
    for (let m = DAY_START_MIN; m <= DAY_END_MIN; m += 60) out.push(m)
    return out
  }, [DAY_START_MIN, DAY_END_MIN])

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
  // Prioridad (fix 2026-06: antes priorizaba "ahora" para hoy, lo que
  // dejaba a la admin que abría a las 9pm pegada al fondo del timeline
  // sin ver las citas de la mañana):
  //   1. Si hay citas → siempre la primera cita cronológica del día.
  //      Esto matchea el mental model "mostrame el día desde el inicio".
  //   2. Si no hay citas y es hoy → la hora actual (como fallback útil
  //      para "este es el momento en que estamos parados").
  //   3. Si no hay citas y no es hoy → no scroll (queda en 8am).
  const wrapperRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!wrapperRef.current) return

    let targetMin: number | null = null
    if (appointments.length > 0) {
      targetMin = Math.min(...appointments.map(a => isoToLocalMinutes(a.startAt)))
    } else if (isToday) {
      targetMin = nowMin
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
    <>
      {/* MOBILE (<md): lista vertical agrupada por hora. La grid horizontal
          con N columnas por estilista era ilegible en <500px con 3+ estilistas.
          La lista mantiene la info esencial pero priorizando el eje temporal. */}
      <div className="md:hidden">
        <AgendaListMobile
          appointments={appointments}
          stylists={stylists}
          selectedId={selectedId}
          onSelect={onSelect}
        />
      </div>

      {/* DESKTOP/TABLET (≥md): grid timeline original */}
      <div ref={wrapperRef} className="hidden md:block bg-white border border-warm-150 rounded-xl overflow-hidden shadow-softer">
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
              dayStartMin={DAY_START_MIN}
              dayEndMin={DAY_END_MIN}
            />
          ))}

          {/* Bandas grises sobre franjas cerradas del salón (fuera de
              horario, lunch, día/festivo entero). Se dibujan por encima
              del fondo pero por debajo de las citas, abarcando todas
              las columnas de estilistas. */}
          {closedBands.map((band, i) => {
            const top = (band.fromMin - DAY_START_MIN) * PX_PER_MIN
            const height = (band.toMin - band.fromMin) * PX_PER_MIN
            if (height <= 0) return null
            return (
              <div
                key={`closed-${i}`}
                className="absolute left-[72px] right-0 pointer-events-none z-[1] bg-warm-150/40"
                style={{ top: Math.max(0, top), height }}
                title={band.label}
              >
                {/* Etiqueta opcional centrada — solo si la franja es
                    grande (>40px) para no saturar bandas chicas. */}
                {height > 40 && (
                  <div className="absolute inset-0 flex items-center justify-center">
                    <span className="text-[10.5px] tracking-[0.12em] uppercase text-warm-500 font-medium bg-white/70 px-2 py-0.5 rounded">
                      {band.label}
                    </span>
                  </div>
                )}
              </div>
            )
          })}

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
    </>
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
  appts, onSelect, selectedId, railHeight, dayStartMin, dayEndMin,
}: {
  appts: AppointmentResponse[]
  onSelect: (a: AppointmentResponse) => void
  selectedId: string | null
  railHeight: number
  dayStartMin: number
  dayEndMin: number
}) {
  // Líneas cada 30 min — las "en hora" más visibles, las de "media hora" sutiles.
  // Generamos divs absolutos de 1px (height: 1 + bg) en lugar de border-b para
  // garantizar que el navegador renderice la línea (border en div de altura 0 a
  // veces se colapsa).
  const totalRows = (dayEndMin - dayStartMin) / 30
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
          dayStartMin={dayStartMin}
          dayEndMin={dayEndMin}
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
  appointment, selected, onClick, dayStartMin, dayEndMin,
}: {
  appointment: AppointmentResponse
  selected: boolean
  onClick: () => void
  dayStartMin: number
  dayEndMin: number
}) {
  const startMin = isoToLocalMinutes(appointment.startAt)
  const endMin   = isoToLocalMinutes(appointment.endAt)

  // Clamp dentro del día visible para que no se desborden por arriba/abajo
  const clampedStart = Math.max(startMin, dayStartMin)
  const clampedEnd = Math.min(endMin, dayEndMin)
  if (clampedEnd <= clampedStart) return null

  const top = (clampedStart - dayStartMin) * PX_PER_MIN
  const fullHeight = (clampedEnd - clampedStart) * PX_PER_MIN
  const status = STATUS_BLOCK[appointment.status]

  // Las canceladas y no-shows COLAPSAN a una franja fina de 22px en vez de
  // ocupar todo su slot temporal. Sin esto, una cita cancelada de 2h
  // bloquea visualmente el slot completo y cuando se agenda una nueva
  // sobre ese horario, la nueva queda "metida adentro" del bloque viejo
  // (UX terrible: parece que coexisten cuando en realidad la vieja no
  // ocupa el cupo). Con colapso, la cancelada queda como referencia
  // visual ("acá hubo una cita que se canceló") sin estorbar.
  const isInactive = appointment.status === 'Cancelled' || appointment.status === 'NoShow'
  const COLLAPSED_HEIGHT = 22
  const usableHeight = isInactive
    ? COLLAPSED_HEIGHT
    : fullHeight - BLOCK_TOP_MARGIN - BLOCK_BOTTOM_MARGIN

  const isTiny = isInactive || usableHeight < 38
  const isXL = !isInactive && usableHeight >= 96

  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        // left-2/right-2 = más margen horizontal vs el borde de la columna
        'absolute left-2 right-2 text-left rounded-lg border shadow-softer transition-all',
        isTiny ? 'px-2.5 py-1' : 'px-3 py-2',
        'hover:shadow-pop hover:-translate-y-[1px] hover:z-20',
        status.block,
        // Las inactivas van debajo de las activas en z para no robar foco.
        isInactive ? 'z-0 opacity-75' : 'z-10',
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

// ───────────────────────────────────────────────────────────────────────
// Bandas "cerrado" del salón
// ───────────────────────────────────────────────────────────────────────

interface ClosedBand {
  /** Minuto desde medianoche local en que arranca la banda. */
  fromMin: number
  /** Minuto en que termina. */
  toMin: number
  /** Etiqueta para tooltip + para mostrar si la banda es grande. */
  label: string
}

/**
 * Calcula las bandas grises a dibujar sobre el timeline según el horario
 * del salón:
 *  - Antes de la apertura → banda
 *  - Después del cierre → banda
 *  - Lunch break (si activo) → banda
 *  - Día entero cerrado (no hay rango para ese día, o fecha en
 *    SalonClosedDates) → banda full-day
 *
 * Las bandas se clampean al rango visible del timeline (DAY_START_MIN
 * a DAY_END_MIN) para no dibujar fuera del viewport.
 */
function computeClosedBands(
  dateStr: string,
  hours: SalonHoursLike | null | undefined,
  dayStartMin: number,
  dayEndMin: number,
): ClosedBand[] {
  if (!hours) return []  // sin horario configurado → no dibujamos nada

  const bands: ClosedBand[] = []

  // ¿Día cerrado puntual?
  if (hours.closedDates.includes(dateStr)) {
    bands.push({
      fromMin: dayStartMin,
      toMin: dayEndMin,
      label: 'Salón cerrado',
    })
    return bands
  }

  // Día de la semana: convertir YYYY-MM-DD local → Mon=0..Sun=6
  const [y, m, d] = dateStr.split('-').map(Number)
  const jsDate = new Date(y, m - 1, d)
  const jsDow = jsDate.getDay()              // Sun=0..Sat=6
  const dayOfWeek = (jsDow + 6) % 7           // Mon=0..Sun=6

  const dayRange = hours.days[String(dayOfWeek)]
  if (!dayRange) {
    bands.push({
      fromMin: dayStartMin,
      toMin: dayEndMin,
      label: 'Día cerrado',
    })
    return bands
  }

  // Antes de la apertura
  const openMin = dayRange.fromHour * 60
  if (openMin > dayStartMin) {
    bands.push({
      fromMin: dayStartMin,
      toMin: Math.min(openMin, dayEndMin),
      label: 'Cerrado',
    })
  }

  // Después del cierre
  const closeMin = dayRange.toHour * 60
  if (closeMin < dayEndMin) {
    bands.push({
      fromMin: Math.max(closeMin, dayStartMin),
      toMin: dayEndMin,
      label: 'Cerrado',
    })
  }

  // Lunch break
  if (hours.lunchBreakEnabled) {
    const lunchFromMin = hours.lunchBreakFromHour * 60
    const lunchToMin = hours.lunchBreakToHour * 60
    const from = Math.max(dayStartMin, lunchFromMin)
    const to = Math.min(dayEndMin, lunchToMin)
    if (to > from) {
      bands.push({ fromMin: from, toMin: to, label: 'Almuerzo' })
    }
  }

  return bands
}

/**
 * Calcula el rango visible del timeline para un día. Combina 3 fuentes:
 *   1. Default 8am–9pm (mínimo histórico).
 *   2. Horario configurado del salón para ese día de la semana (si existe).
 *      Esto cubre el caso "viernes hasta 12am" o "spa abre desde las 7am".
 *   3. Rango de citas del día — si hay citas fuera del horario del salón
 *      (ej. una cancelada que quedó tarde, o un walk-in fuera de horario)
 *      las queremos visibles igual para no esconder data.
 *
 * El resultado se redondea a horas en punto para que el rail visual sea
 * limpio (no "9:23pm").
 */
function computeDayRange(
  dateStr: string,
  hours: SalonHoursLike | null | undefined,
  appointments: AppointmentResponse[],
): { DAY_START_MIN: number; DAY_END_MIN: number } {
  let startMin = DEFAULT_START_MIN
  let endMin = DEFAULT_END_MIN

  // Expandir según horario del salón para este día de la semana.
  if (hours) {
    const [y, m, d] = dateStr.split('-').map(Number)
    const jsDate = new Date(y, m - 1, d)
    const jsDow = jsDate.getDay()
    const dayOfWeek = (jsDow + 6) % 7
    const dayRange = hours.days[String(dayOfWeek)]
    if (dayRange) {
      startMin = Math.min(startMin, dayRange.fromHour * 60)
      endMin = Math.max(endMin, dayRange.toHour * 60)
    }
  }

  // Expandir según citas — solo las del día actual.
  for (const a of appointments) {
    const startA = isoToLocalMinutes(a.startAt)
    const endA = isoToLocalMinutes(a.endAt)
    if (startA < startMin) startMin = Math.floor(startA / 60) * 60       // redondeo abajo a la hora
    if (endA > endMin) endMin = Math.ceil(endA / 60) * 60                  // redondeo arriba a la hora
  }

  // Tope: 0–24h. Algunas configs raras podrían intentar pasarse.
  startMin = Math.max(0, startMin)
  endMin = Math.min(24 * 60, endMin)

  return { DAY_START_MIN: startMin, DAY_END_MIN: endMin }
}

/** Subset de SalonHoursDto que usa computeClosedBands — evita acoplar
 *  el helper al tipo importado del módulo de admin. */
interface SalonHoursLike {
  days: Record<string, { fromHour: number; toHour: number } | null>
  lunchBreakEnabled: boolean
  lunchBreakFromHour: number
  lunchBreakToHour: number
  closedDates: string[]
}

// ────────────────────────────────────────────────────────────────────────────
// AgendaListMobile — vista de lista vertical agrupada por bloque horario.
// La grid horizontal de N columnas por estilista se vuelve ilegible en <md
// (cada columna queda ~70-100px). Acá priorizamos el eje temporal:
// agrupamos por franja del día (mañana/mediodía/tarde) y mostramos cada
// cita como card compacta con: hora, cliente, servicio, estilista, status.
// ────────────────────────────────────────────────────────────────────────────

function AgendaListMobile({
  appointments, stylists, selectedId, onSelect,
}: {
  appointments: AppointmentResponse[]
  stylists: StylistResponse[]
  selectedId: string | null
  onSelect: (a: AppointmentResponse) => void
}) {
  // Mapa stylistId → stylist para mostrar nombre + tono
  const stylistMap = useMemo(() => {
    const m = new Map<string, StylistResponse>()
    stylists.forEach(s => m.set(s.id, s))
    return m
  }, [stylists])

  // Ordenadas cronológicamente
  const sorted = useMemo(
    () => [...appointments].sort(
      (a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime(),
    ),
    [appointments],
  )

  // Agrupadas por bloque del día. La admin típica piensa en "qué tengo en
  // la mañana" / "qué tengo en la tarde" — no en cada hora suelta.
  const groups = useMemo(() => {
    const morning: AppointmentResponse[] = []   // < 12pm
    const midday: AppointmentResponse[] = []    // 12pm–4pm
    const afternoon: AppointmentResponse[] = [] // ≥ 4pm

    for (const a of sorted) {
      const h = new Date(a.startAt).getHours()
      if (h < 12) morning.push(a)
      else if (h < 16) midday.push(a)
      else afternoon.push(a)
    }
    return [
      { id: 'morning',   label: 'Mañana',     items: morning   },
      { id: 'midday',    label: 'Mediodía',   items: midday    },
      { id: 'afternoon', label: 'Tarde',      items: afternoon },
    ].filter(g => g.items.length > 0)
  }, [sorted])

  if (sorted.length === 0) {
    return (
      <div className="bg-white border border-warm-150 rounded-xl p-10 text-center">
        <div className="font-serif text-[18px] text-warm-700">Sin citas para este día</div>
        <div className="text-[12.5px] text-warm-500 mt-1">
          Tocá <span className="font-medium text-brand-700">+ Nueva cita</span> para agendar.
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {groups.map(group => (
        <section
          key={group.id}
          className="bg-white border border-warm-150 rounded-xl overflow-hidden"
        >
          <div className="px-4 py-2.5 bg-warm-50/60 border-b border-warm-150 flex items-center justify-between">
            <h3 className="text-[11px] uppercase tracking-[0.14em] text-warm-500 font-medium">
              {group.label}
            </h3>
            <span className="text-[11px] text-warm-400 tabular-nums">
              {group.items.length} {group.items.length === 1 ? 'cita' : 'citas'}
            </span>
          </div>
          <ul className="divide-y divide-warm-100">
            {group.items.map(a => {
              const stylist = stylistMap.get(a.stylistId)
              const sTone = stylist ? toneOf(stylist.id) : null
              const status = STATUS_BLOCK[a.status]
              const startMin = isoToLocalMinutes(a.startAt)
              const endMin = isoToLocalMinutes(a.endAt)
              const isSelected = selectedId === a.id
              return (
                <li key={a.id}>
                  <button
                    type="button"
                    onClick={() => onSelect(a)}
                    className={cls(
                      'w-full px-4 py-3 flex items-start gap-3 text-left transition',
                      isSelected ? 'bg-brand-50/60' : 'hover:bg-warm-50',
                    )}
                  >
                    {/* Hora a la izquierda — col fija para alinear */}
                    <div className="w-14 flex-shrink-0 pt-0.5">
                      <div className="text-[13px] font-medium text-warm-800 tabular-nums">
                        {fmtTime12(startMin)}
                      </div>
                      <div className="text-[10.5px] text-warm-400 tabular-nums">
                        {fmtTime12(endMin)}
                      </div>
                    </div>

                    {/* Contenido: cliente + servicio + estilista */}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className={cls('w-1.5 h-1.5 rounded-full flex-shrink-0', status.dot)} />
                        <div className="text-[13.5px] font-medium text-warm-900 truncate">
                          {a.customerName}
                        </div>
                      </div>
                      <div className="text-[12px] text-warm-500 mt-0.5 truncate">
                        {a.serviceName}
                      </div>
                      {stylist && sTone && (
                        <div className="mt-1.5 flex items-center gap-1.5 text-[11px] text-warm-500">
                          <span className={cls(
                            'w-4 h-4 rounded-full flex items-center justify-center text-[9px] font-semibold flex-shrink-0',
                            sTone.bg, sTone.fg,
                          )}>
                            {initialsOf(stylist.fullName)}
                          </span>
                          <span className="truncate">{stylist.fullName}</span>
                        </div>
                      )}
                    </div>
                  </button>
                </li>
              )
            })}
          </ul>
        </section>
      ))}
    </div>
  )
}
