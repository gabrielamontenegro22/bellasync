import { useState } from 'react'
import { Check, Plus } from 'lucide-react'
import type { WizardData, CustomService, ServiceCategory } from '../types'
import { NavButtons } from '../components/NavButtons'
import { SUGGESTED_SERVICES, SERVICE_CATEGORIES_ORDER } from '../data/suggestedServices'
import { cls } from '@/lib/cls'

const fmtCOP = (n: number) => '$' + Math.round(n).toLocaleString('es-CO')

interface Step4Props {
  data: WizardData
  set: (patch: Partial<WizardData>) => void
  onNext: () => void
  onBack: () => void
}

/** Paso 4 — selección de servicios iniciales. */
export function Step4Services({ data, set, onNext, onBack }: Step4Props) {
  const [showAdd, setShowAdd] = useState(false)
  const [draft, setDraft] = useState({ name: '', price: '', dur: '' })

  const all = [...SUGGESTED_SERVICES, ...data.customServices]
  const grouped = SERVICE_CATEGORIES_ORDER
    .map((cat) => ({ cat, items: all.filter((s) => s.cat === cat) }))
    .filter((g) => g.items.length > 0)

  const onCount = Object.values(data.servicesOn).filter(Boolean).length

  const toggleSrv = (id: string) => {
    set({ servicesOn: { ...data.servicesOn, [id]: !data.servicesOn[id] } })
  }

  const updateField = (id: string, key: 'price' | 'dur', val: number) => {
    set({
      servicesData: {
        ...data.servicesData,
        [id]: { ...data.servicesData[id], [key]: val },
      },
    })
  }

  const addCustom = () => {
    const price = parseInt(draft.price, 10)
    const dur   = parseInt(draft.dur, 10)
    if (!draft.name.trim() || !Number.isFinite(price) || !Number.isFinite(dur)) return

    const id: string = 'custom_' + Date.now()
    const newSrv: CustomService = {
      id,
      name: draft.name.trim(),
      cat: 'Otros' as ServiceCategory,
      price,
      dur,
      emoji: '⭐',
    }
    set({
      customServices: [...data.customServices, newSrv],
      servicesOn:     { ...data.servicesOn,   [id]: true },
      servicesData:   { ...data.servicesData, [id]: { price, dur } },
    })
    setDraft({ name: '', price: '', dur: '' })
    setShowAdd(false)
  }

  return (
    <div className="space-y-5">
      {/* Banner contador */}
      <div className="flex items-center justify-between bg-brand-50/60 border border-brand-200 rounded-lg px-4 py-2.5">
        <div className="text-[12.5px] text-brand-800">
          <span className="font-semibold tabular-nums">{onCount}</span> servicios seleccionados
        </div>
        <span className="text-[11px] text-warm-600">
          No te preocupes, podrás agregar más después
        </span>
      </div>

      {/* Servicios agrupados por categoría */}
      {grouped.map((g) => (
        <div key={g.cat}>
          <div className="text-[10.5px] tracking-[0.16em] uppercase text-warm-500 font-medium mb-2">
            {g.cat}
          </div>
          <div className="space-y-1.5">
            {g.items.map((s) => {
              const on = !!data.servicesOn[s.id]
              const sd = data.servicesData[s.id] || { price: s.price, dur: s.dur }
              return (
                <div
                  key={s.id}
                  className={cls(
                    'rounded-lg border transition overflow-hidden',
                    on
                      ? 'border-brand-300 bg-white'
                      : 'border-warm-200 bg-white hover:border-warm-300',
                  )}
                >
                  <button
                    type="button"
                    onClick={() => toggleSrv(s.id)}
                    className="w-full px-3.5 py-3 flex items-center gap-3 text-left"
                  >
                    <span
                      className={cls(
                        'w-5 h-5 rounded-md flex items-center justify-center flex-shrink-0 transition',
                        on ? 'bg-brand-700 text-white' : 'border-2 border-warm-300',
                      )}
                    >
                      {on && <Check size={12} strokeWidth={3} />}
                    </span>
                    <span className="text-[16px]">{s.emoji}</span>
                    <span className="flex-1 text-[13.5px] font-medium text-warm-800">
                      {s.name}
                    </span>
                    {!on && (
                      <span className="text-[11.5px] text-warm-500 tabular-nums">
                        {fmtCOP(s.price)} · {s.dur}min
                      </span>
                    )}
                  </button>

                  {on && (
                    <div className="px-3.5 pb-3 pt-0 grid grid-cols-2 gap-2 anim-fade">
                      <label className="block">
                        <div className="text-[11px] text-warm-500 mb-1">Precio (COP)</div>
                        <input
                          type="number"
                          value={sd.price}
                          onChange={(e) => updateField(s.id, 'price', parseInt(e.target.value, 10) || 0)}
                          className="w-full px-2.5 py-1.5 rounded-md border border-warm-200 bg-white text-[12.5px] text-warm-800 tabular-nums focus:border-brand-500 outline-none"
                        />
                      </label>
                      <label className="block">
                        <div className="text-[11px] text-warm-500 mb-1">Duración (min)</div>
                        <input
                          type="number"
                          value={sd.dur}
                          onChange={(e) => updateField(s.id, 'dur', parseInt(e.target.value, 10) || 0)}
                          className="w-full px-2.5 py-1.5 rounded-md border border-warm-200 bg-white text-[12.5px] text-warm-800 tabular-nums focus:border-brand-500 outline-none"
                        />
                      </label>
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      ))}

      {/* Agregar custom */}
      {showAdd ? (
        <div className="rounded-lg border-2 border-dashed border-brand-300 bg-brand-50/30 p-4 anim-fade">
          <div className="text-[12.5px] font-medium text-warm-800 mb-3">Servicio personalizado</div>
          <div className="space-y-2.5">
            <input
              value={draft.name}
              onChange={(e) => setDraft({ ...draft, name: e.target.value })}
              placeholder="Nombre del servicio"
              className="w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition"
            />
            <div className="grid grid-cols-2 gap-2.5">
              <input
                type="number"
                value={draft.price}
                onChange={(e) => setDraft({ ...draft, price: e.target.value })}
                placeholder="Precio (COP)"
                className="w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition"
              />
              <input
                type="number"
                value={draft.dur}
                onChange={(e) => setDraft({ ...draft, dur: e.target.value })}
                placeholder="Duración (min)"
                className="w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition"
              />
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={addCustom}
                className="flex-1 px-4 py-2 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[12.5px] font-medium"
              >
                Agregar
              </button>
              <button
                type="button"
                onClick={() => setShowAdd(false)}
                className="px-4 py-2 rounded-lg border border-warm-200 text-warm-700 text-[12.5px] font-medium"
              >
                Cancelar
              </button>
            </div>
          </div>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => setShowAdd(true)}
          className="w-full px-4 py-3 rounded-lg border-2 border-dashed border-warm-250 hover:border-brand-300 text-warm-600 hover:text-brand-700 text-[13px] font-medium flex items-center justify-center gap-2 transition"
        >
          <Plus size={14} /> Agregar servicio personalizado
        </button>
      )}

      <NavButtons onBack={onBack} onNext={onNext} valid={onCount > 0} />
    </div>
  )
}
