import type { WizardData, DayId, HoursPresetId } from '../types'
import { NavButtons } from '../components/NavButtons'
import { HOURS_PRESETS, DAYS } from '../data/hoursPresets'
import { cls } from '@/lib/cls'

interface Step3Props {
  data: WizardData
  set: (patch: Partial<WizardData>) => void
  onNext: () => void
  onBack: () => void
}

/** Paso 3 — horarios de atención. Presets rápidos + edición fina por día. */
export function Step3Hours({ data, set, onNext, onBack }: Step3Props) {
  const HOURS_OPTS = Array.from({ length: 25 }, (_, i) => i)

  const applyPreset = (k: Exclude<HoursPresetId, 'custom'>) => {
    set({ hours: HOURS_PRESETS[k].days, hoursPreset: k })
  }

  const toggleDay = (d: DayId) => {
    const cur = data.hours[d]
    const next = { ...data.hours, [d]: cur ? null : ([9, 19] as [number, number]) }
    set({ hours: next, hoursPreset: 'custom' })
  }

  const setRange = (d: DayId, idx: 0 | 1, val: number) => {
    const range: [number, number] = [...(data.hours[d] || [9, 19])] as [number, number]
    range[idx] = val
    set({ hours: { ...data.hours, [d]: range }, hoursPreset: 'custom' })
  }

  const valid = Object.values(data.hours).some(Boolean)

  return (
    <div className="space-y-6">
      {/* Presets */}
      <div>
        <div className="text-[12.5px] font-medium text-warm-700 mb-2.5">Plantillas rápidas</div>
        <div className="grid sm:grid-cols-3 gap-2.5">
          {(Object.entries(HOURS_PRESETS) as Array<[Exclude<HoursPresetId,'custom'>, typeof HOURS_PRESETS.classic]>).map(([k, p]) => (
            <button
              key={k}
              type="button"
              onClick={() => applyPreset(k)}
              className={cls(
                'px-3.5 py-3 rounded-xl border text-left transition',
                data.hoursPreset === k
                  ? 'border-brand-500 bg-brand-50/60 ring-2 ring-brand-100'
                  : 'border-warm-200 bg-white hover:border-warm-300',
              )}
            >
              <div className="text-[12.5px] font-medium text-warm-800">{p.label}</div>
              <div className="text-[11px] text-warm-500 mt-0.5">
                {Object.values(p.days).filter(Boolean).length} días abiertos
              </div>
            </button>
          ))}
          <button
            type="button"
            onClick={() => set({ hoursPreset: 'custom' })}
            className={cls(
              'px-3.5 py-3 rounded-xl border text-left transition',
              data.hoursPreset === 'custom'
                ? 'border-brand-500 bg-brand-50/60 ring-2 ring-brand-100'
                : 'border-warm-200 bg-white hover:border-warm-300',
            )}
          >
            <div className="text-[12.5px] font-medium text-warm-800">Personalizado</div>
            <div className="text-[11px] text-warm-500 mt-0.5">Ajusta cada día</div>
          </button>
        </div>
      </div>

      {/* Día por día */}
      <div className="rounded-xl bg-white border border-warm-200 divide-y divide-warm-150">
        {DAYS.map((d) => {
          const range = data.hours[d.id]
          const open = !!range
          return (
            <div key={d.id} className="flex items-center gap-3 px-4 py-3">
              <button
                type="button"
                onClick={() => toggleDay(d.id)}
                className={cls(
                  'relative w-10 h-6 rounded-full transition flex-shrink-0',
                  open ? 'bg-brand-700' : 'bg-warm-200',
                )}
                aria-label={`Toggle ${d.label}`}
              >
                <span
                  className={cls(
                    'absolute top-0.5 w-5 h-5 bg-white rounded-full shadow-sm transition-all',
                    open ? 'left-[18px]' : 'left-0.5',
                  )}
                />
              </button>

              <div className="w-24 text-[13.5px] font-medium text-warm-800">{d.label}</div>

              {open && range ? (
                <div className="flex items-center gap-2 flex-1">
                  <select
                    value={range[0]}
                    onChange={(e) => setRange(d.id, 0, parseInt(e.target.value, 10))}
                    className="px-2.5 py-1.5 rounded-md border border-warm-200 bg-white text-[12.5px] text-warm-800 tabular-nums focus:border-brand-500 outline-none"
                  >
                    {HOURS_OPTS.map((h) => (
                      <option key={h} value={h}>{String(h).padStart(2, '0')}:00</option>
                    ))}
                  </select>
                  <span className="text-warm-400 text-[12px]">a</span>
                  <select
                    value={range[1]}
                    onChange={(e) => setRange(d.id, 1, parseInt(e.target.value, 10))}
                    className="px-2.5 py-1.5 rounded-md border border-warm-200 bg-white text-[12.5px] text-warm-800 tabular-nums focus:border-brand-500 outline-none"
                  >
                    {HOURS_OPTS.map((h) => (
                      <option key={h} value={h}>{String(h).padStart(2, '0')}:00</option>
                    ))}
                  </select>
                  <span className="text-[11px] text-warm-500 ml-auto tabular-nums">
                    {range[1] - range[0]}h
                  </span>
                </div>
              ) : (
                <span className="text-[12.5px] text-warm-400 flex-1">Cerrado</span>
              )}
            </div>
          )
        })}
      </div>

      <NavButtons onBack={onBack} onNext={onNext} valid={valid} />
    </div>
  )
}
