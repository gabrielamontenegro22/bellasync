import { useEffect, useMemo, useRef, useState } from 'react'
import { Calendar as CalendarIcon, ChevronLeft, ChevronRight, Clock } from 'lucide-react'
import { cls } from '@/lib/cls'

/**
 * DatePicker custom de BellaSync.
 *
 * ¿Por qué no usar el `<input type="date">` nativo?
 *   1. El popup nativo varía radicalmente entre browsers (Chrome/Safari/Firefox)
 *      y entre OS — algunos muestran fechas en formato US, otros ES, otros con
 *      wheel, otros con calendario. Imposible mantener identidad visual.
 *   2. No combina con el resto del diseño (paleta verde + serif + bordes warm).
 *
 * ¿Por qué no usar `react-day-picker` o `react-datepicker`?
 *   Esa librería pesa 10-20kb gzip, viene con CSS pesado que hay que sobreescribir
 *   y el calendario es simple. ~150 líneas propias dan mejor control + menos bundle.
 *
 * API:
 *   <DatePicker value="2026-06-04" onChange={iso => setDate(iso)} />
 *   value/onChange usan formato ISO YYYY-MM-DD (mismo que el input nativo)
 */

// ────────────────────────────────────────────────────────────────────────────

const DAYS_SHORT = ['L', 'M', 'X', 'J', 'V', 'S', 'D']  // L=Lunes (week starts Monday in CO)
const MONTHS = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
]

// ────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────

/** Parsea "YYYY-MM-DD" → Date local. Devuelve null si vacío/inválido. */
function parseIsoDate(iso: string | null | undefined): Date | null {
  if (!iso) return null
  const [y, m, d] = iso.split('-').map(Number)
  if (!y || !m || !d) return null
  return new Date(y, m - 1, d)
}

/** Date → "YYYY-MM-DD" en hora local (no UTC). */
function formatIsoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** Formato humano para el botón trigger. "jue 4 jun 2026" */
function formatHuman(d: Date): string {
  return new Intl.DateTimeFormat('es-CO', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(d).replace(/\.$/, '')
}

/** True si dos Date caen en el mismo día (ignora hora). */
function isSameDay(a: Date | null, b: Date): boolean {
  if (!a) return false
  return a.getFullYear() === b.getFullYear()
      && a.getMonth() === b.getMonth()
      && a.getDate() === b.getDate()
}

/**
 * Devuelve las 42 celdas (6 semanas × 7 días) que se pintan en el grid del
 * mes dado. Empieza con padding del mes anterior para que la primera celda
 * sea siempre un Lunes, igual que el calendario impreso de Colombia.
 */
function monthCells(year: number, month: number): Array<{ date: Date; inMonth: boolean }> {
  const first = new Date(year, month, 1)
  // Pad inicial: días del mes anterior. DayOfWeek de .toDay() (Sun=0..Sat=6)
  // → restamos para empezar en Lunes.
  const dow = (first.getDay() + 6) % 7  // Sun→6, Mon→0, …, Sat→5
  const cells: Array<{ date: Date; inMonth: boolean }> = []
  for (let i = -dow; i < 42 - dow; i++) {
    const d = new Date(year, month, 1 + i)
    cells.push({ date: d, inMonth: d.getMonth() === month })
  }
  return cells
}

// ────────────────────────────────────────────────────────────────────────────
// Calendar (interno) — el grid de mes que usan tanto DatePicker como DateTimePicker.
// ────────────────────────────────────────────────────────────────────────────

interface CalendarProps {
  selected: Date | null
  onSelect: (d: Date) => void
  /** Fecha mínima permitida (inclusive). */
  min?: Date | null
  /** Fecha máxima permitida (inclusive). */
  max?: Date | null
  /** Mes a mostrar inicialmente (default: selected o hoy). */
  initialMonth?: Date
}

function Calendar({ selected, onSelect, min, max, initialMonth }: CalendarProps) {
  const today = useMemo(() => {
    const t = new Date()
    return new Date(t.getFullYear(), t.getMonth(), t.getDate())
  }, [])

  const [viewMonth, setViewMonth] = useState<Date>(() => {
    const ref = initialMonth ?? selected ?? today
    return new Date(ref.getFullYear(), ref.getMonth(), 1)
  })

  const cells = useMemo(
    () => monthCells(viewMonth.getFullYear(), viewMonth.getMonth()),
    [viewMonth],
  )

  const prevMonth = () => setViewMonth(m => new Date(m.getFullYear(), m.getMonth() - 1, 1))
  const nextMonth = () => setViewMonth(m => new Date(m.getFullYear(), m.getMonth() + 1, 1))

  return (
    <div className="p-3 select-none">
      {/* Header: mes en serif + navegación */}
      <div className="flex items-center justify-between mb-3 px-1">
        <button
          type="button"
          onClick={prevMonth}
          className="p-1.5 rounded-md text-warm-500 hover:bg-warm-100 hover:text-warm-800 transition"
          aria-label="Mes anterior"
        >
          <ChevronLeft size={16} />
        </button>
        <div className="font-serif text-[15px] text-warm-800 tracking-tight">
          <span className="capitalize">{MONTHS[viewMonth.getMonth()]}</span>{' '}
          <span className="text-warm-500 font-normal">{viewMonth.getFullYear()}</span>
        </div>
        <button
          type="button"
          onClick={nextMonth}
          className="p-1.5 rounded-md text-warm-500 hover:bg-warm-100 hover:text-warm-800 transition"
          aria-label="Mes siguiente"
        >
          <ChevronRight size={16} />
        </button>
      </div>

      {/* Encabezado L M X J V S D — uppercase mini */}
      <div className="grid grid-cols-7 mb-1.5">
        {DAYS_SHORT.map((d, i) => (
          <div
            key={i}
            className={cls(
              'text-center text-[10.5px] font-medium tracking-wider uppercase py-1',
              i >= 5 ? 'text-warm-400' : 'text-warm-500',
            )}
          >
            {d}
          </div>
        ))}
      </div>

      {/* Grid 6×7 — días del mes */}
      <div className="grid grid-cols-7 gap-0.5">
        {cells.map(({ date, inMonth }, i) => {
          const isToday = isSameDay(today, date)
          const isSelected = isSameDay(selected, date)
          const disabled =
            (min ? date < min : false) ||
            (max ? date > max : false)

          return (
            <button
              key={i}
              type="button"
              disabled={disabled}
              onClick={() => onSelect(date)}
              className={cls(
                'aspect-square rounded-lg text-[12.5px] tabular-nums',
                'flex items-center justify-center transition',
                // Selected (filled brand) — tiene prioridad sobre todo
                isSelected && 'bg-brand-700 text-white font-medium hover:bg-brand-800',
                // Today (no seleccionado): ring sutil + texto brand
                !isSelected && isToday && 'ring-1 ring-brand-300 text-brand-700 font-medium',
                // Día normal del mes actual
                !isSelected && !isToday && inMonth && 'text-warm-700 hover:bg-warm-100',
                // Día del mes anterior/siguiente — gris claro
                !isSelected && !inMonth && 'text-warm-300 hover:bg-warm-50',
                // Deshabilitado override
                disabled && 'opacity-30 cursor-not-allowed hover:bg-transparent',
              )}
              aria-label={date.toDateString()}
            >
              {date.getDate()}
            </button>
          )
        })}
      </div>
    </div>
  )
}

// ────────────────────────────────────────────────────────────────────────────
// Popover wrapper (close on outside click + ESC) — compartido por ambos pickers.
// ────────────────────────────────────────────────────────────────────────────

function usePopover(onClose: () => void, anchorRef: React.RefObject<HTMLElement | null>) {
  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      const target = e.target as Node
      const popover = document.querySelector('[data-bs-popover="true"]')
      if (popover && popover.contains(target)) return
      if (anchorRef.current && anchorRef.current.contains(target)) return
      onClose()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [onClose, anchorRef])
}

// ────────────────────────────────────────────────────────────────────────────
// DatePicker — solo fecha. Formato YYYY-MM-DD.
// ────────────────────────────────────────────────────────────────────────────

interface DatePickerProps {
  /** "YYYY-MM-DD" o cadena vacía para "sin valor". */
  value: string
  onChange: (iso: string) => void
  /** Texto cuando no hay valor. */
  placeholder?: string
  /** Bloquea fechas anteriores a (inclusive). "today" = hoy, ISO, o Date. */
  min?: string | Date
  /** Bloquea fechas posteriores a (inclusive). */
  max?: string | Date
  /** Tamaño del trigger. */
  size?: 'sm' | 'md'
  /** className extra para el trigger. */
  className?: string
  /** Deshabilitado. */
  disabled?: boolean
  /** Si true, el botón ocupa el ancho disponible. */
  fullWidth?: boolean
}

export function DatePicker({
  value, onChange, placeholder = 'Seleccionar fecha',
  min, max, size = 'md', className, disabled, fullWidth,
}: DatePickerProps) {
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)
  usePopover(() => setOpen(false), anchorRef)

  const date = parseIsoDate(value)
  const minDate = useMemo(() => parseConstraint(min), [min])
  const maxDate = useMemo(() => parseConstraint(max), [max])

  const triggerSize =
    size === 'sm'
      ? 'px-3 py-1.5 text-[12.5px]'
      : 'px-3.5 py-2 text-[13px]'

  return (
    <div className={cls('relative inline-block', fullWidth && 'w-full block')}>
      <button
        ref={anchorRef}
        type="button"
        disabled={disabled}
        onClick={() => setOpen(o => !o)}
        className={cls(
          'inline-flex items-center gap-2 rounded-lg border bg-white',
          'text-warm-800 tabular-nums transition',
          triggerSize,
          fullWidth && 'w-full justify-between',
          open
            ? 'border-brand-500 ring-2 ring-brand-100'
            : 'border-warm-200 hover:border-warm-300',
          disabled && 'opacity-50 cursor-not-allowed',
          className,
        )}
      >
        <CalendarIcon size={14} strokeWidth={1.8} className="text-warm-400 flex-shrink-0" />
        <span className={cls('truncate', !date && 'text-warm-400')}>
          {date ? formatHuman(date) : placeholder}
        </span>
      </button>

      {open && !disabled && (
        <div
          data-bs-popover="true"
          className="absolute z-50 mt-1.5 bg-white rounded-xl border border-warm-150 shadow-pop w-[280px] anim-fade"
        >
          <Calendar
            selected={date}
            onSelect={d => {
              onChange(formatIsoDate(d))
              setOpen(false)
            }}
            min={minDate}
            max={maxDate}
          />
        </div>
      )}
    </div>
  )
}

// ────────────────────────────────────────────────────────────────────────────
// DateTimePicker — fecha + hora. Formato YYYY-MM-DDTHH:mm (datetime-local).
// ────────────────────────────────────────────────────────────────────────────

interface DateTimePickerProps {
  /** "YYYY-MM-DDTHH:mm". */
  value: string
  onChange: (iso: string) => void
  /** Step de minutos del time picker. Default 15 (citas tipo 9:00, 9:15, …). */
  minuteStep?: number
  /** Hora mínima del día (default 0). 24h. */
  minHour?: number
  /** Hora máxima del día (default 23). 24h. */
  maxHour?: number
  /** Bloquea fechas anteriores a. */
  min?: string | Date
  /** Bloquea fechas posteriores a. */
  max?: string | Date
  className?: string
  fullWidth?: boolean
  disabled?: boolean
}

export function DateTimePicker({
  value, onChange,
  minuteStep = 15, minHour = 0, maxHour = 23,
  min, max, className, fullWidth, disabled,
}: DateTimePickerProps) {
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)
  usePopover(() => setOpen(false), anchorRef)

  // Parsear value en parte date (YYYY-MM-DD) + parte time (HH:mm).
  const { datePart, timePart } = useMemo(() => splitLocal(value), [value])
  const dateObj = parseIsoDate(datePart)
  const minDate = useMemo(() => parseConstraint(min), [min])
  const maxDate = useMemo(() => parseConstraint(max), [max])

  const slots = useMemo(() => {
    const out: string[] = []
    for (let h = minHour; h <= maxHour; h++) {
      for (let m = 0; m < 60; m += minuteStep) {
        if (h === maxHour && m > 0) break  // no pasar de maxHour:00
        out.push(`${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`)
      }
    }
    return out
  }, [minuteStep, minHour, maxHour])

  const formatTriggerText = () => {
    if (!dateObj || !timePart) return 'Seleccionar fecha y hora'
    return `${formatHuman(dateObj)} · ${timePart}`
  }

  return (
    <div className={cls('relative inline-block', fullWidth && 'w-full block')}>
      <button
        ref={anchorRef}
        type="button"
        disabled={disabled}
        onClick={() => setOpen(o => !o)}
        className={cls(
          'inline-flex items-center gap-2 rounded-lg border bg-white',
          'px-3.5 py-2 text-[13px] text-warm-800 tabular-nums transition',
          fullWidth && 'w-full justify-between',
          open
            ? 'border-brand-500 ring-2 ring-brand-100'
            : 'border-warm-200 hover:border-warm-300',
          disabled && 'opacity-50 cursor-not-allowed',
          className,
        )}
      >
        <CalendarIcon size={14} strokeWidth={1.8} className="text-warm-400 flex-shrink-0" />
        <span className={cls('truncate', !dateObj && 'text-warm-400')}>
          {formatTriggerText()}
        </span>
      </button>

      {open && !disabled && (
        <div
          data-bs-popover="true"
          className={cls(
            'absolute z-50 mt-1.5 bg-white rounded-xl border border-warm-150 shadow-pop',
            'flex flex-col sm:flex-row anim-fade',
            // En mobile, apila vertical y limita la altura del time picker
            // para que no rebote.
            'w-[280px] sm:w-[380px]',
          )}
        >
          <Calendar
            selected={dateObj}
            onSelect={d => {
              // Mantener el time anterior si había, sino default a primer slot.
              const tp = timePart || slots[0] || '09:00'
              onChange(`${formatIsoDate(d)}T${tp}`)
            }}
            min={minDate}
            max={maxDate}
          />

          {/* Time picker: lista scrolleable de slots */}
          <div className="border-t sm:border-t-0 sm:border-l border-warm-150 p-2 sm:w-[88px]">
            <div className="text-[10px] uppercase tracking-wider text-warm-500 px-2 pb-1.5 flex items-center gap-1">
              <Clock size={10} /> Hora
            </div>
            <div className="max-h-[180px] sm:max-h-[228px] overflow-y-auto space-y-0.5 pr-1">
              {slots.map(slot => {
                const isSelected = timePart === slot
                return (
                  <button
                    key={slot}
                    type="button"
                    onClick={() => {
                      const dp = datePart || formatIsoDate(new Date())
                      onChange(`${dp}T${slot}`)
                      // No cerrar — la admin puede querer ajustar hora varias veces
                    }}
                    className={cls(
                      'w-full px-2.5 py-1 rounded-md text-[12px] tabular-nums text-left',
                      isSelected
                        ? 'bg-brand-700 text-white font-medium'
                        : 'text-warm-700 hover:bg-warm-100',
                    )}
                  >
                    {slot}
                  </button>
                )
              })}
            </div>
            <div className="pt-1.5 mt-1 border-t border-warm-150">
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="w-full px-2.5 py-1.5 rounded-md text-[12px] font-medium bg-brand-50 text-brand-700 hover:bg-brand-100"
              >
                Listo
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// ────────────────────────────────────────────────────────────────────────────
// Helpers extra
// ────────────────────────────────────────────────────────────────────────────

function parseConstraint(c: string | Date | undefined): Date | null {
  if (!c) return null
  if (c === 'today') {
    const t = new Date()
    return new Date(t.getFullYear(), t.getMonth(), t.getDate())
  }
  if (c instanceof Date) return c
  return parseIsoDate(c)
}

function splitLocal(iso: string): { datePart: string; timePart: string } {
  if (!iso) return { datePart: '', timePart: '' }
  const [datePart = '', timePart = ''] = iso.split('T')
  return { datePart, timePart: timePart.slice(0, 5) }  // recortar segundos si vinieran
}
