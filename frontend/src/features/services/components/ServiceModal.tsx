import { useEffect, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import {
  X,
  CheckCircle,
  Trash2,
  Upload,
  ChevronDown,
  AlertCircle,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import type { ServiceResponse } from '@/api/services'
import { serviceSchema, defaultServiceForm, type ServiceFormData } from '../schemas'
import { CATEGORIES, CATEGORY_BY_ID, fmtCOP } from '../types'
import { serviceExtrasStorage } from '../storage'
import { extractApiError } from '@/lib/extractApiError'

interface ServiceModalProps {
  /** Si es null/undefined → modo "Nuevo". Si es ServiceResponse → modo "Editar" */
  initial: ServiceResponse | null
  onClose: () => void
  onSave: (data: ServiceFormData, originalId?: string) => Promise<void>
  onDelete?: (id: string) => Promise<void>
  /** Si está disponible, se usa para mostrar el grid de estilistas */
  stylists?: Array<{ id: string; name: string }>
}

/**
 * Modal de crear / editar servicio.
 * Replica el ServiceModal del mockup config-servicios.jsx.
 *
 * Persistencia:
 *  - Campos del backend (name, description, category, price, duration,
 *    commission, color, isActive) → API real
 *  - Campos extras (requiresDeposit, depositPercentage, assignedStylistIds)
 *    → localStorage (hasta que el backend los soporte)
 */
export function ServiceModal({
  initial,
  onClose,
  onSave,
  onDelete,
  stylists = [],
}: ServiceModalProps) {
  const isNew = !initial
  const [submitting, setSubmitting] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    formState: { errors },
  } = useForm<ServiceFormData>({
    resolver: zodResolver(serviceSchema),
    defaultValues: getInitialValues(initial),
  })

  // Cuando cambia `initial`, resetear el form
  useEffect(() => {
    setServerError(null)
  }, [initial])

  // Cerrar con Esc
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !submitting) onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose, submitting])

  const watchedCategory = watch('category')
  const watchedPrice = watch('price') || 0
  const watchedRequiresDeposit = watch('requiresDeposit')
  const watchedDepositPct = watch('depositPercentage') || 0
  const watchedDuration = watch('durationMinutes') || 60

  const onSubmit = async (data: ServiceFormData) => {
    setSubmitting(true)
    setServerError(null)
    try {
      await onSave(data, initial?.id)
      onClose()
    } catch (e) {
      setServerError(extractApiError(e, 'No se pudo guardar el servicio.'))
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async () => {
    if (!initial?.id || !onDelete) return
    if (!window.confirm(`¿Estás segura de archivar el servicio "${initial.name}"?\n\nQuedará oculto del catálogo pero las citas históricas seguirán referenciándolo.`))
      return
    setSubmitting(true)
    setServerError(null)
    try {
      await onDelete(initial.id)
      onClose()
    } catch (e) {
      setServerError(extractApiError(e, 'No se pudo eliminar el servicio.'))
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center anim-fade">
      <div className="absolute inset-0 bg-warm-900/40" onClick={() => !submitting && onClose()} />

      <div className="relative w-full sm:max-w-[640px] bg-white sm:rounded-2xl rounded-t-2xl shadow-pop border border-warm-150 overflow-hidden max-h-[92vh] flex flex-col">
        {/* Header */}
        <div className="px-6 pt-6 pb-4 flex items-start justify-between border-b border-warm-150">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-500 font-medium mb-1.5">
              {isNew ? 'Nuevo' : 'Editando'}
            </div>
            <div className="font-serif text-[26px] text-warm-800 leading-tight">
              {isNew ? 'Nuevo servicio' : initial?.name || 'Editar servicio'}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            className="p-1.5 rounded-md hover:bg-warm-100 text-warm-500 disabled:opacity-50"
            aria-label="Cerrar"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body scroll */}
        <form
          id="service-form"
          onSubmit={handleSubmit(onSubmit)}
          className="px-6 py-5 space-y-5 overflow-y-auto flex-1"
          noValidate
        >
          {serverError && (
            <div
              role="alert"
              className="rounded-lg bg-terra-100/50 border border-terra-300/60 px-3.5 py-2.5 text-[12.5px] text-terra-500 flex items-start gap-2"
            >
              <AlertCircle size={13} className="flex-shrink-0 mt-0.5" />
              <span>{serverError}</span>
            </div>
          )}

          {/* Nombre */}
          <Field label="Nombre del servicio" error={errors.name?.message}>
            <input
              {...register('name')}
              placeholder="Ej. Color completo + corte"
              autoFocus
              className={inputClass(!!errors.name)}
            />
          </Field>

          {/* Descripción */}
          <Field label="Descripción" hint="Visible para los clientes al agendar" error={errors.description?.message}>
            <textarea
              {...register('description')}
              rows={2}
              placeholder="Servicio completo de color con tinte profesional, lavado, corte y secado."
              className={cls(inputClass(!!errors.description), 'resize-none text-[13.5px]')}
            />
          </Field>

          {/* Categoría / Precio / Duración */}
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
            <Field label="Categoría" error={errors.category?.message}>
              <div className="relative">
                <select {...register('category')} className={cls(inputClass(!!errors.category), 'appearance-none pr-8')}>
                  {CATEGORIES.map((c) => (
                    <option key={c.id} value={c.id}>{c.label}</option>
                  ))}
                </select>
                <ChevronDown size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-warm-400 pointer-events-none" />
              </div>
            </Field>

            <Field label="Precio (COP)" error={errors.price?.message}>
              <div className={cls(
                'flex items-center gap-1 px-3 py-2.5 rounded-lg border bg-white',
                errors.price ? 'border-terra-300' : 'border-warm-200',
                'focus-within:ring-2 focus-within:ring-brand-700/15 focus-within:border-brand-700',
              )}>
                <span className="text-warm-400 text-[14px]">$</span>
                <input
                  type="number"
                  min={0}
                  step={1000}
                  {...register('price', { valueAsNumber: true })}
                  className="flex-1 outline-none text-[14px] text-warm-800 tabular-nums bg-transparent"
                />
              </div>
            </Field>

            <Field label="Duración (min)" error={errors.durationMinutes?.message}>
              <div className={cls(
                'flex items-center rounded-lg border bg-white overflow-hidden',
                errors.durationMinutes ? 'border-terra-300' : 'border-warm-200',
              )}>
                <button
                  type="button"
                  onClick={() => setValue('durationMinutes', Math.max(15, watchedDuration - 15), { shouldValidate: true })}
                  className="px-3 py-2.5 text-warm-500 hover:bg-warm-50"
                >
                  −
                </button>
                <input
                  type="number"
                  min={1}
                  step={15}
                  {...register('durationMinutes', { valueAsNumber: true })}
                  className="flex-1 outline-none text-[14px] text-warm-800 text-center tabular-nums bg-transparent w-0"
                />
                <button
                  type="button"
                  onClick={() => setValue('durationMinutes', watchedDuration + 15, { shouldValidate: true })}
                  className="px-3 py-2.5 text-warm-500 hover:bg-warm-50"
                >
                  +
                </button>
              </div>
            </Field>
          </div>

          {/* Comisión + color */}
          <div className="grid grid-cols-2 gap-4">
            <Field label="Comisión al estilista (%)" error={errors.commissionPercentage?.message}>
              <input
                type="number"
                min={0}
                max={100}
                step={5}
                {...register('commissionPercentage', { valueAsNumber: true })}
                className={inputClass(!!errors.commissionPercentage)}
              />
            </Field>

            <Field label="Color (hex)" hint="Ej. #0f766e — opcional" error={errors.color?.message}>
              <Controller
                control={control}
                name="color"
                render={({ field }) => (
                  <div className="flex items-center gap-2">
                    <input
                      type="color"
                      value={field.value || '#0f766e'}
                      onChange={(e) => field.onChange(e.target.value)}
                      className="w-10 h-10 rounded-md border border-warm-200 cursor-pointer"
                    />
                    <input
                      type="text"
                      value={field.value ?? ''}
                      onChange={(e) => field.onChange(e.target.value)}
                      placeholder="#0f766e"
                      className={cls(inputClass(!!errors.color), 'flex-1')}
                    />
                  </div>
                )}
              />
            </Field>
          </div>

          {/* Anticipo (LOCAL) */}
          <div className="rounded-xl border border-warm-150 bg-warm-50/40 p-4">
            <div className="flex items-center justify-between gap-3">
              <div>
                <div className="text-[13.5px] font-medium text-warm-800">¿Requiere anticipo?</div>
                <div className="text-[12px] text-warm-500 mt-0.5">
                  El cliente deberá enviar pago parcial para confirmar.
                </div>
              </div>
              <Controller
                control={control}
                name="requiresDeposit"
                render={({ field }) => <Toggle on={field.value} onChange={field.onChange} />}
              />
            </div>

            {watchedRequiresDeposit && (
              <div className="mt-4 pt-4 border-t border-warm-150 anim-fade">
                <div className="flex items-baseline justify-between mb-2">
                  <div className="text-[12px] tracking-[0.12em] uppercase text-warm-500 font-medium">
                    Porcentaje del precio
                  </div>
                  <div className="font-serif text-[22px] text-warm-800 tabular-nums leading-none">
                    {watchedDepositPct}%
                  </div>
                </div>
                <input
                  type="range"
                  min={10}
                  max={100}
                  step={5}
                  {...register('depositPercentage', { valueAsNumber: true })}
                  className="w-full accent-brand-700"
                />
                <div className="flex items-center justify-between mt-1.5 text-[11px] text-warm-400">
                  <span>10%</span>
                  <span className="tabular-nums text-warm-600">
                    ≈ {fmtCOP((watchedPrice * watchedDepositPct) / 100)}
                  </span>
                  <span>100%</span>
                </div>
              </div>
            )}
          </div>

          {/* Estilistas asignadas (LOCAL) */}
          {stylists.length > 0 && (
            <Field
              label="Estilistas habilitadas"
              hint={`${watch('assignedStylistIds')?.length ?? 0} de ${stylists.length} seleccionadas`}
            >
              <Controller
                control={control}
                name="assignedStylistIds"
                render={({ field }) => (
                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                    {stylists.map((s) => {
                      const sel = field.value.includes(s.id)
                      return (
                        <button
                          key={s.id}
                          type="button"
                          onClick={() => {
                            const next = sel
                              ? field.value.filter((id) => id !== s.id)
                              : [...field.value, s.id]
                            field.onChange(next)
                          }}
                          className={cls(
                            'flex items-center gap-2.5 px-3 py-2 rounded-lg border text-left transition',
                            sel
                              ? 'bg-brand-50 border-brand-700 ring-2 ring-brand-700/15'
                              : 'bg-white border-warm-200 hover:border-warm-300',
                          )}
                        >
                          <div className={cls(
                            'w-7 h-7 rounded-full bg-warm-100 text-warm-600 flex items-center justify-center font-serif text-[11px] flex-shrink-0',
                          )}>
                            {s.name.split(' ').slice(0, 2).map((w) => w[0]?.toUpperCase()).join('')}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className={cls('text-[12.5px] truncate', sel ? 'text-brand-800 font-medium' : 'text-warm-800')}>
                              {s.name.split(' ')[0]}
                            </div>
                          </div>
                          {sel && <CheckCircle size={14} className="text-brand-700 flex-shrink-0" />}
                        </button>
                      )
                    })}
                  </div>
                )}
              />
            </Field>
          )}

          {/* Foto placeholder */}
          <Field label="Foto del servicio" hint="Próximamente · placeholder con gradient por categoría">
            <div className="rounded-xl border-2 border-dashed border-warm-200 bg-warm-50/40 p-5 flex items-center gap-4">
              <div
                className="w-16 h-16 rounded-lg flex-shrink-0"
                style={{ background: CATEGORY_BY_ID[watchedCategory]?.gradient }}
              />
              <div className="flex-1 min-w-0">
                <div className="text-[13px] text-warm-700">Sin foto personalizada</div>
                <div className="text-[11.5px] text-warm-500 mt-0.5">
                  Por ahora se usa el gradient de {CATEGORY_BY_ID[watchedCategory]?.label}
                </div>
              </div>
              <button
                type="button"
                disabled
                title="Disponible cuando el backend implemente storage de imágenes"
                className="px-3.5 py-2 rounded-lg border border-warm-200 bg-white text-[12.5px] font-medium text-warm-400 cursor-not-allowed flex items-center gap-1.5"
              >
                <Upload size={13} /> Próximamente
              </button>
            </div>
          </Field>

          {/* Toggle activo */}
          <div className="rounded-xl border border-warm-150 bg-white p-4 flex items-center justify-between">
            <div>
              <div className="text-[13.5px] font-medium text-warm-800">Servicio activo</div>
              <div className="text-[12px] text-warm-500 mt-0.5">
                Cuando está inactivo, no aparece para agendar.
              </div>
            </div>
            <Controller
              control={control}
              name="isActive"
              render={({ field }) => <Toggle on={field.value} onChange={field.onChange} />}
            />
          </div>
        </form>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-warm-150 flex items-center justify-between gap-2 bg-warm-50/50">
          {!isNew && onDelete && (
            <button
              type="button"
              onClick={handleDelete}
              disabled={submitting}
              className="px-3.5 py-2.5 rounded-lg text-terra-500 hover:bg-terra-100/50 text-[13px] font-medium flex items-center gap-1.5 disabled:opacity-50"
            >
              <Trash2 size={13} /> Archivar servicio
            </button>
          )}
          <div className="flex items-center gap-2 ml-auto">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2.5 rounded-lg text-warm-600 hover:bg-warm-100 text-[13.5px] font-medium disabled:opacity-50"
            >
              Cancelar
            </button>
            <button
              type="submit"
              form="service-form"
              disabled={submitting}
              className="px-5 py-2.5 rounded-lg text-[13.5px] font-medium flex items-center gap-1.5 transition bg-brand-700 hover:bg-brand-800 text-white shadow-soft disabled:opacity-60 disabled:cursor-not-allowed"
            >
              <CheckCircle size={14} /> {submitting ? 'Guardando…' : 'Guardar servicio'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                   */
/* -------------------------------------------------------------------------- */

function getInitialValues(initial: ServiceResponse | null): ServiceFormData {
  if (!initial) return defaultServiceForm
  // requiresDeposit y depositPercentage vienen del backend.
  // assignedStylistIds sigue en localStorage hasta F5 (Estilistas).
  const extras = serviceExtrasStorage.get(initial.id)
  return {
    name: initial.name,
    description: initial.description ?? '',
    category: initial.category,
    durationMinutes: initial.durationMinutes,
    price: initial.price,
    commissionPercentage: initial.commissionPercentage,
    color: initial.color ?? '',
    isActive: initial.isActive,
    requiresDeposit: initial.requiresDeposit,
    depositPercentage: initial.depositPercentage,
    assignedStylistIds: extras.assignedStylistIds,
  }
}

function inputClass(error: boolean) {
  return cls(
    'w-full px-3 py-2.5 rounded-lg border bg-white text-[14px] text-warm-800 placeholder:text-warm-400 outline-none transition',
    'focus:ring-2 focus:ring-brand-700/15 focus:border-brand-700',
    error ? 'border-terra-300' : 'border-warm-200',
  )
}

interface FieldProps {
  label: string
  hint?: string
  error?: string
  children: React.ReactNode
}

function Field({ label, hint, error, children }: FieldProps) {
  return (
    <div>
      <div className="flex items-baseline justify-between mb-1.5">
        <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium">
          {label}
        </label>
        {hint && !error && <span className="text-[11px] text-warm-400">{hint}</span>}
      </div>
      {children}
      {error && (
        <div className="text-[11px] text-terra-500 mt-1 flex items-center gap-1">
          <AlertCircle size={11} />
          {error}
        </div>
      )}
    </div>
  )
}

interface ToggleProps {
  on: boolean
  onChange: (v: boolean) => void
}

function Toggle({ on, onChange }: ToggleProps) {
  return (
    <button
      type="button"
      onClick={() => onChange(!on)}
      className={cls(
        'w-11 h-6 rounded-full relative transition flex-shrink-0',
        on ? 'bg-brand-700' : 'bg-warm-200',
      )}
    >
      <span
        className={cls(
          'absolute top-0.5 w-5 h-5 rounded-full bg-white shadow-soft transition-all',
          on ? 'left-[22px]' : 'left-0.5',
        )}
      />
    </button>
  )
}
