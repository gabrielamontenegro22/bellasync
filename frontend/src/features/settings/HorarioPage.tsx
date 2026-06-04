import { useMemo, useState } from 'react'
import {
  Calendar, CalendarClock, Clock, AlertCircle, ChevronDown, Plus, X,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import {
  SettingsHeader, SettingsBlock, SaveBar, PreviewNotice, ToggleRow, Toggle,
} from './_primitives'

/**
 * `/configuracion/horario` — días y franjas de atención del salón.
 *
 * Por ahora es mockup visual: estado local, "Guardar" cambia el banner
 * verde pero nada se persiste. Cuando hagamos el sprint de backend,
 * los días-franjas se mueven a la entidad Tenant (o nueva entidad
 * SalonHours) y la agenda los lee para validar slots disponibles.
 */
type Range = [number, number]
type DaysMap = Record<string, Range | null>

const DIAS = [
  { id: 'lun', label: 'Lunes' },
  { id: 'mar', label: 'Martes' },
  { id: 'mie', label: 'Miércoles' },
  { id: 'jue', label: 'Jueves' },
  { id: 'vie', label: 'Viernes' },
  { id: 'sab', label: 'Sábado' },
  { id: 'dom', label: 'Domingo' },
] as const

const HOUR_OPTS = Array.from({ length: 25 }, (_, i) => i)

const PRESETS: Record<string, { label: string; days: DaysMap }> = {
  lunsab: {
    label: 'Lun–Sáb · 9am–7pm',
    days: { lun: [9,19], mar: [9,19], mie: [9,19], jue: [9,19], vie: [9,19], sab: [9,19], dom: null },
  },
  mardom: {
    label: 'Mar–Dom · 10am–8pm',
    days: { lun: null, mar: [10,20], mie: [10,20], jue: [10,20], vie: [10,20], sab: [10,20], dom: [10,20] },
  },
  lundom: {
    label: 'Lun–Dom · 8am–8pm',
    days: { lun: [8,20], mar: [8,20], mie: [8,20], jue: [8,20], vie: [8,20], sab: [8,20], dom: [8,20] },
  },
}

type FormShape = {
  preset: string
  days: DaysMap
  lunchOn: boolean
  lunch: Range
  holidaysOff: boolean
  closedDates: string[]
}

/**
 * closedDates se almacena en ISO YYYY-MM-DD para:
 *  - Sortear cronológicamente sin parsear strings ambiguos.
 *  - Comparar duplicados con string equality directo.
 *  - Cuando hagamos backend, el formato ya es el correcto para
 *    DateOnly de C#.
 * Se renderiza al usuario con Intl.DateTimeFormat en español.
 */
const INITIAL: FormShape = {
  preset: 'lunsab',
  days: PRESETS.lunsab.days,
  lunchOn: true,
  lunch: [13, 14],
  holidaysOff: true,
  closedDates: ['2026-12-24', '2026-12-25'],
}

/** Hoy en formato YYYY-MM-DD local (para min del date picker). */
function todayISO(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** "Vie 25 dic 2026" — para mostrar las fechas en chips. */
function formatHumanDate(iso: string): string {
  // Parseamos manualmente para evitar el problema de zona horaria con
  // new Date(iso) que en algunos browsers asume UTC y muestra un día menos.
  const [y, m, d] = iso.split('-').map(Number)
  if (!y || !m || !d) return iso
  const date = new Date(y, m - 1, d)
  return new Intl.DateTimeFormat('es-CO', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(date).replace(/\.$/, '')
}

export function HorarioPage() {
  const [form, setForm] = useState<FormShape>(INITIAL)
  const [saved, setSaved] = useState(false)
  const [newDate, setNewDate] = useState('')

  const isDirty = useMemo(
    () => JSON.stringify(form) !== JSON.stringify(INITIAL),
    [form],
  )

  const openCount = useMemo(
    () => Object.values(form.days).filter(Boolean).length,
    [form.days],
  )

  const set = <K extends keyof FormShape>(k: K, v: FormShape[K]) => {
    setForm(f => ({ ...f, [k]: v }))
    setSaved(false)
  }

  const applyPreset = (k: string) => {
    setForm(f => ({ ...f, preset: k, days: PRESETS[k].days }))
    setSaved(false)
  }

  const toggleDay = (d: string) => {
    setForm(f => ({
      ...f,
      preset: 'custom',
      days: { ...f.days, [d]: f.days[d] ? null : [9, 19] },
    }))
    setSaved(false)
  }

  const setRange = (d: string, idx: 0 | 1, value: number) => {
    setForm(f => {
      const r = ([...(f.days[d] ?? [9, 19])] as Range)
      r[idx] = value
      // Forzamos start <= end mínimo +1h.
      if (idx === 0 && r[0] >= r[1]) r[1] = Math.min(24, r[0] + 1)
      if (idx === 1 && r[1] <= r[0]) r[0] = Math.max(0, r[1] - 1)
      return { ...f, preset: 'custom', days: { ...f.days, [d]: r } }
    })
    setSaved(false)
  }

  const addClosedDate = () => {
    const v = newDate.trim()
    if (!v) return
    // Evitar duplicados — si la admin hace click dos veces sin querer.
    if (form.closedDates.includes(v)) {
      setNewDate('')
      return
    }
    // Ordenar cronológicamente para que los chips se vean en orden real.
    const next = [...form.closedDates, v].sort()
    set('closedDates', next)
    setNewDate('')
  }

  const sortedClosedDates = useMemo(
    () => [...form.closedDates].sort(),
    [form.closedDates],
  )

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <PreviewNotice />
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Horario de atención"
          desc="Define los días y franjas en que tu salón recibe citas. La agenda solo permitirá reservar dentro de estos horarios."
        />

        {/* PRESETS */}
        <SettingsBlock icon={<CalendarClock size={16} />} title="Plantillas rápidas">
          <div className="grid sm:grid-cols-2 gap-2.5">
            {Object.entries(PRESETS).map(([k, p]) => {
              const active = form.preset === k
              return (
                <button
                  key={k}
                  type="button"
                  onClick={() => applyPreset(k)}
                  className={cls(
                    'px-3.5 py-3 rounded-xl border text-left transition',
                    active
                      ? 'border-brand-500 bg-brand-50/60 ring-2 ring-brand-100'
                      : 'border-warm-200 bg-white hover:border-warm-300',
                  )}
                >
                  <div className="text-[12.5px] font-medium text-warm-800">{p.label}</div>
                  <div className="text-[11px] text-warm-500 mt-0.5">
                    {Object.values(p.days).filter(Boolean).length} días abiertos
                  </div>
                </button>
              )
            })}
            <div
              className={cls(
                'px-3.5 py-3 rounded-xl border text-left',
                form.preset === 'custom'
                  ? 'border-brand-500 bg-brand-50/60 ring-2 ring-brand-100'
                  : 'border-warm-200 bg-white',
              )}
            >
              <div className="text-[12.5px] font-medium text-warm-800">Personalizado</div>
              <div className="text-[11px] text-warm-500 mt-0.5">Ajusta cada día abajo</div>
            </div>
          </div>
        </SettingsBlock>

        {/* DÍAS DE LA SEMANA */}
        <SettingsBlock
          icon={<Calendar size={16} />}
          title={`Días de la semana · ${openCount} abiertos`}
        >
          <div className="rounded-xl border border-warm-150 divide-y divide-warm-150 overflow-hidden">
            {DIAS.map(d => {
              const r = form.days[d.id]
              const open = !!r
              return (
                <div key={d.id} className="flex items-center gap-3 px-4 py-3 bg-white">
                  <Toggle on={open} onChange={() => toggleDay(d.id)} />
                  <div className="w-24 text-[13.5px] font-medium text-warm-800">{d.label}</div>
                  {open && r ? (
                    <div className="flex items-center gap-2 flex-1">
                      <HourSelect value={r[0]} onChange={(v) => setRange(d.id, 0, v)} />
                      <span className="text-warm-400 text-[12px]">a</span>
                      <HourSelect value={r[1]} onChange={(v) => setRange(d.id, 1, v)} />
                      <span className="text-[11px] text-warm-500 ml-auto tabular-nums">
                        {r[1] - r[0]}h
                      </span>
                    </div>
                  ) : (
                    <span className="text-[12.5px] text-warm-400 flex-1">Cerrado</span>
                  )}
                </div>
              )
            })}
          </div>
        </SettingsBlock>

        {/* DESCANSO DE ALMUERZO */}
        <SettingsBlock icon={<Clock size={16} />} title="Descanso de almuerzo">
          <ToggleRow
            title="Bloquear un horario de almuerzo"
            desc="La agenda no permitirá reservar en esta franja todos los días."
            on={form.lunchOn}
            onChange={(v) => set('lunchOn', v)}
          />
          {form.lunchOn && (
            <div className="flex items-center gap-2 anim-fade">
              <span className="text-[12.5px] text-warm-600">De</span>
              <HourSelect
                value={form.lunch[0]}
                onChange={(v) => set('lunch', [v, form.lunch[1]] as Range)}
              />
              <span className="text-warm-400 text-[12px]">a</span>
              <HourSelect
                value={form.lunch[1]}
                onChange={(v) => set('lunch', [form.lunch[0], v] as Range)}
              />
            </div>
          )}
        </SettingsBlock>

        {/* FESTIVOS Y CIERRES */}
        <SettingsBlock
          icon={<AlertCircle size={16} />}
          title="Festivos y cierres especiales"
          last
        >
          <ToggleRow
            title="Cerrar automáticamente en festivos de Colombia"
            desc="Año Nuevo, Semana Santa, Independencia y demás festivos nacionales."
            on={form.holidaysOff}
            onChange={(v) => set('holidaysOff', v)}
          />
          <div>
            <div className="text-[12.5px] font-medium text-warm-700 mb-2">
              Días cerrados puntuales
            </div>
            <div className="flex flex-wrap gap-2 mb-2.5">
              {sortedClosedDates.length === 0 ? (
                <span className="text-[11.5px] text-warm-400 italic">
                  Ninguno aún — elegí una fecha abajo.
                </span>
              ) : (
                sortedClosedDates.map(d => (
                  <span
                    key={d}
                    className="inline-flex items-center gap-1.5 text-[12px] px-2.5 py-1 rounded-full bg-warm-100 text-warm-700 capitalize"
                  >
                    {formatHumanDate(d)}
                    <button
                      type="button"
                      onClick={() =>
                        set('closedDates', form.closedDates.filter(x => x !== d))
                      }
                      className="text-warm-400 hover:text-terra-500"
                      aria-label={`Quitar ${formatHumanDate(d)}`}
                    >
                      <X size={12} />
                    </button>
                  </span>
                ))
              )}
            </div>
            <div className="flex gap-2">
              <div className="flex-1 flex items-center rounded-lg border border-warm-200 bg-white overflow-hidden focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <span className="pl-3 pr-2 text-warm-400 flex-shrink-0">
                  <Calendar size={14} strokeWidth={1.8} />
                </span>
                <input
                  type="date"
                  value={newDate}
                  min={todayISO()}
                  onChange={(e) => setNewDate(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault()
                      addClosedDate()
                    }
                  }}
                  className="flex-1 py-2 pr-3 text-[13px] text-warm-800 tabular-nums outline-none bg-transparent"
                />
              </div>
              <button
                type="button"
                onClick={addClosedDate}
                disabled={!newDate}
                className={cls(
                  'px-3.5 py-2 rounded-lg text-[12.5px] font-medium flex items-center gap-1.5 transition',
                  newDate
                    ? 'bg-brand-700 hover:bg-brand-800 text-white'
                    : 'bg-warm-100 text-warm-400 cursor-not-allowed',
                )}
              >
                <Plus size={13} /> Agregar
              </button>
            </div>
            <p className="text-[11px] text-warm-400 italic mt-2">
              Click en el calendario para elegir el día. No se pueden agregar fechas
              pasadas.
            </p>
          </div>
        </SettingsBlock>
      </div>

      <SaveBar
        show={isDirty}
        saved={saved}
        onSave={() => setSaved(true)}
        onDiscard={() => { setForm(INITIAL); setSaved(false) }}
      />
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────

function HourSelect({
  value, onChange,
}: {
  value: number
  onChange: (v: number) => void
}) {
  return (
    <div className="relative">
      <select
        value={value}
        onChange={(e) => onChange(parseInt(e.target.value, 10))}
        className="appearance-none pl-2.5 pr-7 py-1.5 rounded-md border border-warm-200 bg-white text-[12.5px] text-warm-800 tabular-nums focus:border-brand-500 outline-none"
      >
        {HOUR_OPTS.map(h => (
          <option key={h} value={h}>
            {String(h).padStart(2, '0')}:00
          </option>
        ))}
      </select>
      <ChevronDown
        size={12}
        className="absolute right-2 top-1/2 -translate-y-1/2 text-warm-400 pointer-events-none"
      />
    </div>
  )
}
