import { useMemo, useState } from 'react'
import {
  Calendar, CalendarClock, Clock, AlertCircle, ChevronDown, Plus, X,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import {
  SettingsHeader, SettingsBlock, SaveBar, PreviewNotice, ToggleRow, Toggle,
  inputCls,
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

const INITIAL: FormShape = {
  preset: 'lunsab',
  days: PRESETS.lunsab.days,
  lunchOn: true,
  lunch: [13, 14],
  holidaysOff: true,
  closedDates: ['24 dic 2026', '25 dic 2026'],
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
    set('closedDates', [...form.closedDates, v])
    setNewDate('')
  }

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
              {form.closedDates.length === 0 ? (
                <span className="text-[11.5px] text-warm-400 italic">Ninguno aún.</span>
              ) : (
                form.closedDates.map(d => (
                  <span
                    key={d}
                    className="inline-flex items-center gap-1.5 text-[12px] px-2.5 py-1 rounded-full bg-warm-100 text-warm-700"
                  >
                    {d}
                    <button
                      type="button"
                      onClick={() =>
                        set('closedDates', form.closedDates.filter(x => x !== d))
                      }
                      className="text-warm-400 hover:text-terra-500"
                      aria-label={`Quitar ${d}`}
                    >
                      <X size={12} />
                    </button>
                  </span>
                ))
              )}
            </div>
            <div className="flex gap-2">
              <input
                value={newDate}
                onChange={(e) => setNewDate(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    addClosedDate()
                  }
                }}
                placeholder="Ej. 1 ene 2027"
                className={cls(inputCls, 'py-2 text-[13px] flex-1')}
              />
              <button
                type="button"
                onClick={addClosedDate}
                className="px-3.5 py-2 rounded-lg bg-warm-100 hover:bg-warm-150 text-warm-700 text-[12.5px] font-medium flex items-center gap-1.5"
              >
                <Plus size={13} /> Agregar
              </button>
            </div>
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
