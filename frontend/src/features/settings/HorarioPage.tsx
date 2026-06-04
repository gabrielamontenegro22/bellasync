import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Calendar, CalendarClock, Clock, AlertCircle, ChevronDown, Plus, X,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  SettingsHeader, SettingsBlock, SaveBar, ToggleRow, Toggle,
} from './_primitives'
import { updateSalonHours, type SalonHoursDto } from '@/api/admin'
import { useSalonHours } from './useSalonHours'

/**
 * `/configuracion/horario` — días y franjas de atención del salón.
 * Conectado al backend: GET/PUT /api/Admin/salon-hours.
 *
 * El backend es replace-all (manda todo el horario en cada update),
 * así que el form de acá también lo es: hay un único objeto FormShape
 * que representa el estado completo, y "Guardar" lo manda entero.
 *
 * Los presets son UX puro — no se persisten. Después de cargar, si
 * el day map matchea alguna plantilla conocida, la pintamos como
 * activa; sino mostramos "Personalizado".
 */

// ───────────────────────────────────────────────────────────────────────
// Tipos locales y constantes
// ───────────────────────────────────────────────────────────────────────

type Range = [number, number]
type DaysMap = Record<number, Range | null>

const DIAS = [
  { id: 0, label: 'Lunes' },
  { id: 1, label: 'Martes' },
  { id: 2, label: 'Miércoles' },
  { id: 3, label: 'Jueves' },
  { id: 4, label: 'Viernes' },
  { id: 5, label: 'Sábado' },
  { id: 6, label: 'Domingo' },
] as const

const HOUR_OPTS = Array.from({ length: 25 }, (_, i) => i)

const PRESETS: Record<string, { label: string; days: DaysMap }> = {
  lunsab: {
    label: 'Lun–Sáb · 9am–7pm',
    days: { 0:[9,19], 1:[9,19], 2:[9,19], 3:[9,19], 4:[9,19], 5:[9,19], 6: null },
  },
  mardom: {
    label: 'Mar–Dom · 10am–8pm',
    days: { 0: null, 1:[10,20], 2:[10,20], 3:[10,20], 4:[10,20], 5:[10,20], 6:[10,20] },
  },
  lundom: {
    label: 'Lun–Dom · 8am–8pm',
    days: { 0:[8,20], 1:[8,20], 2:[8,20], 3:[8,20], 4:[8,20], 5:[8,20], 6:[8,20] },
  },
}

type FormShape = {
  days: DaysMap
  lunchOn: boolean
  lunch: Range
  holidaysOff: boolean
  closedDates: string[]  // YYYY-MM-DD
}

// Estado vacío al iniciar sin data del backend — todos los días cerrados.
const EMPTY_FORM: FormShape = {
  days: { 0: null, 1: null, 2: null, 3: null, 4: null, 5: null, 6: null },
  lunchOn: false,
  lunch: [13, 14],
  holidaysOff: false,
  closedDates: [],
}

// ───────────────────────────────────────────────────────────────────────
// Helpers
// ───────────────────────────────────────────────────────────────────────

function todayISO(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatHumanDate(iso: string): string {
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

/** Convierte el DTO del backend al FormShape local. */
function dtoToForm(dto: SalonHoursDto): FormShape {
  const days: DaysMap = {} as DaysMap
  for (let d = 0; d < 7; d++) {
    const r = dto.days[String(d)]
    days[d] = r ? [r.fromHour, r.toHour] : null
  }
  return {
    days,
    lunchOn: dto.lunchBreakEnabled,
    lunch: [dto.lunchBreakFromHour, dto.lunchBreakToHour],
    holidaysOff: dto.isHolidaysClosed,
    closedDates: [...dto.closedDates].sort(),
  }
}

/** Convierte el FormShape al DTO que espera el backend. */
function formToDto(f: FormShape): SalonHoursDto {
  const days: SalonHoursDto['days'] = {}
  for (let d = 0; d < 7; d++) {
    const r = f.days[d]
    days[String(d)] = r ? { fromHour: r[0], toHour: r[1] } : null
  }
  return {
    days,
    lunchBreakEnabled: f.lunchOn,
    lunchBreakFromHour: f.lunch[0],
    lunchBreakToHour: f.lunch[1],
    isHolidaysClosed: f.holidaysOff,
    closedDates: f.closedDates,
  }
}

/** Compara la day-map con cada preset; devuelve el id matcheante o 'custom'. */
function detectPreset(days: DaysMap): string {
  for (const [k, p] of Object.entries(PRESETS)) {
    let match = true
    for (let d = 0; d < 7; d++) {
      const a = days[d]
      const b = p.days[d]
      if (a === null && b === null) continue
      if (a === null || b === null) { match = false; break }
      if (a[0] !== b[0] || a[1] !== b[1]) { match = false; break }
    }
    if (match) return k
  }
  return 'custom'
}

// ───────────────────────────────────────────────────────────────────────

export function HorarioPage() {
  const qc = useQueryClient()
  const { data: serverData, isLoading } = useSalonHours()

  // Snapshot del estado del servidor — sirve como "valor inicial" para
  // detectar dirty y para Discard.
  const serverForm: FormShape = useMemo(
    () => (serverData ? dtoToForm(serverData) : EMPTY_FORM),
    [serverData],
  )

  const [form, setForm] = useState<FormShape>(EMPTY_FORM)
  useEffect(() => { setForm(serverForm) }, [serverForm])

  const [saved, setSaved] = useState(false)
  const [newDate, setNewDate] = useState('')

  const mut = useMutation({
    mutationFn: (req: SalonHoursDto) => updateSalonHours(req),
    onSuccess: (r) => {
      qc.setQueryData(['salonHours'], r)
      setSaved(true)
    },
    onError: () => { /* el error lo muestra el SaveBar abajo */ },
  })

  useEffect(() => {
    if (!saved) return
    const t = setTimeout(() => setSaved(false), 3000)
    return () => clearTimeout(t)
  }, [saved])

  const isDirty = useMemo(
    () => JSON.stringify(form) !== JSON.stringify(serverForm),
    [form, serverForm],
  )

  const preset = useMemo(() => detectPreset(form.days), [form.days])
  const openCount = useMemo(
    () => Object.values(form.days).filter(Boolean).length,
    [form.days],
  )

  const set = <K extends keyof FormShape>(k: K, v: FormShape[K]) => {
    setForm(f => ({ ...f, [k]: v }))
    setSaved(false)
  }

  const applyPreset = (k: string) => {
    setForm(f => ({ ...f, days: { ...PRESETS[k].days } }))
    setSaved(false)
  }

  const toggleDay = (d: number) => {
    setForm(f => ({
      ...f,
      days: { ...f.days, [d]: f.days[d] ? null : [9, 19] },
    }))
    setSaved(false)
  }

  const setRange = (d: number, idx: 0 | 1, value: number) => {
    setForm(f => {
      const r = ([...(f.days[d] ?? [9, 19])] as Range)
      r[idx] = value
      if (idx === 0 && r[0] >= r[1]) r[1] = Math.min(24, r[0] + 1)
      if (idx === 1 && r[1] <= r[0]) r[0] = Math.max(0, r[1] - 1)
      return { ...f, days: { ...f.days, [d]: r } }
    })
    setSaved(false)
  }

  const addClosedDate = () => {
    const v = newDate.trim()
    if (!v) return
    if (form.closedDates.includes(v)) {
      setNewDate('')
      return
    }
    const next = [...form.closedDates, v].sort()
    set('closedDates', next)
    setNewDate('')
  }

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">Cargando…</div>
    )
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Horario de atención"
          desc="Define los días y franjas en que tu salón recibe citas. La agenda solo permitirá reservar dentro de estos horarios."
        />

        {/* PRESETS */}
        <SettingsBlock icon={<CalendarClock size={16} />} title="Plantillas rápidas">
          <div className="grid sm:grid-cols-2 gap-2.5">
            {Object.entries(PRESETS).map(([k, p]) => {
              const active = preset === k
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
                preset === 'custom'
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
                onChange={(v) => set('lunch', [v, Math.max(form.lunch[1], v + 1)] as Range)}
              />
              <span className="text-warm-400 text-[12px]">a</span>
              <HourSelect
                value={form.lunch[1]}
                onChange={(v) => set('lunch', [Math.min(form.lunch[0], v - 1), v] as Range)}
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
              {form.closedDates.length === 0 ? (
                <span className="text-[11.5px] text-warm-400 italic">
                  Ninguno aún — elegí una fecha abajo.
                </span>
              ) : (
                form.closedDates.map(d => (
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
        saving={mut.isPending}
        error={mut.error ? extractApiError(mut.error, 'No se pudo guardar el horario.') : null}
        onSave={() => mut.mutate(formToDto(form))}
        onDiscard={() => { setForm(serverForm); setSaved(false) }}
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
