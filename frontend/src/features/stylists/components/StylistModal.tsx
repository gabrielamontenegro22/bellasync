import { useEffect, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { X, AlertCircle, Trash2, Check, Mail, Phone as PhoneIcon, IdCard } from 'lucide-react'
import { DatePicker } from '@/components/ui'
import { cls } from '@/lib/cls'
import type { StylistResponse } from '@/api/stylists'
import { stylistSchema, defaultStylistForm, type StylistFormData } from '../schemas'
import { TONES, ROLE_SUGGESTIONS, initialsOf } from '../types'
import { StylistAvatar } from './StylistAvatar'
import { useServices } from '@/features/services/hooks'
import { CATEGORY_BY_ID } from '@/features/services/types'
import { extractApiError } from '@/lib/extractApiError'

interface StylistModalProps {
  /** null/undefined = modo "Nuevo". StylistResponse = modo "Editar". */
  initial: StylistResponse | null
  onClose: () => void
  onSave: (data: StylistFormData, originalId?: string) => Promise<void>
  onDelete?: (id: string) => Promise<void>
}

/**
 * Modal de crear/editar estilista.
 * Replica fielmente NewStylistModal del mockup stylists.jsx + AGREGA la sección
 * de asignación M:N de servicios (que el mockup no muestra explícitamente pero
 * el backend espera).
 */
export function StylistModal({ initial, onClose, onSave, onDelete }: StylistModalProps) {
  const isNew = !initial
  const [submitting, setSubmitting] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)

  // Carga la lista de servicios del salón (para el multi-select)
  const servicesQ = useServices(false) // solo activos

  const {
    register,
    handleSubmit,
    control,
    watch,
    formState: { errors },
  } = useForm<StylistFormData>({
    resolver: zodResolver(stylistSchema),
    defaultValues: getInitialValues(initial),
  })

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

  const watchedName = watch('fullName')
  const watchedColor = watch('color')

  const onSubmit = async (data: StylistFormData) => {
    setSubmitting(true)
    setServerError(null)
    try {
      await onSave(data, initial?.id)
      onClose()
    } catch (e) {
      setServerError(extractApiError(e, 'No se pudo guardar el estilista.'))
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async () => {
    if (!initial?.id || !onDelete) return
    if (
      !window.confirm(
        `¿Archivar a ${initial.fullName}?\n\nQuedará oculta del catálogo pero las citas históricas siguen referenciándola.`,
      )
    )
      return
    setSubmitting(true)
    setServerError(null)
    try {
      await onDelete(initial.id)
      onClose()
    } catch (e) {
      setServerError(extractApiError(e, 'No se pudo eliminar el estilista.'))
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center anim-fade">
      <div
        className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm"
        onClick={() => !submitting && onClose()}
      />

      <div className="relative w-full sm:max-w-[600px] bg-white sm:rounded-2xl rounded-t-2xl shadow-pop border border-warm-150 overflow-hidden max-h-[92vh] flex flex-col">
        {/* Header */}
        <div className="px-6 pt-6 pb-4 border-b border-warm-150 flex items-start justify-between gap-3">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium">
              {isNew ? 'Nuevo miembro' : 'Editar miembro'}
            </div>
            <h3 className="font-serif text-[26px] text-warm-800 mt-1 leading-tight">
              {isNew ? 'Agregar estilista' : initial?.fullName || 'Editar estilista'}
            </h3>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center disabled:opacity-50"
            aria-label="Cerrar"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <form
          id="stylist-form"
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

          {/* Avatar preview + selector de tono */}
          <div className="flex items-center gap-4">
            <StylistAvatar
              name={watchedName || initialsOf('? ?')}
              color={watchedColor}
              size={72}
            />
            <div className="flex-1">
              <div className="text-[12px] font-medium text-warm-700 mb-1.5">
                Color de avatar
              </div>
              <Controller
                control={control}
                name="color"
                render={({ field }) => (
                  <div className="flex gap-1.5">
                    {TONES.map((t) => (
                      <button
                        key={t.id}
                        type="button"
                        onClick={() => field.onChange(t.hex)}
                        title={t.id}
                        className={cls(
                          'w-8 h-8 rounded-full transition ring-offset-2 ring-offset-white',
                          t.bg,
                          field.value === t.hex
                            ? 'ring-2 ring-brand-700'
                            : 'hover:scale-110',
                        )}
                      />
                    ))}
                  </div>
                )}
              />
            </div>
          </div>

          {/* Nombre */}
          <Field label="Nombre completo" error={errors.fullName?.message}>
            <Input
              autoFocus
              placeholder="Carolina Rodríguez"
              error={!!errors.fullName}
              {...register('fullName')}
            />
          </Field>

          {/* Rol */}
          <Field label="Cargo" error={errors.role?.message}>
            <Controller
              control={control}
              name="role"
              render={({ field }) => (
                <select
                  {...field}
                  className={inputClass(!!errors.role)}
                >
                  {ROLE_SUGGESTIONS.map((r) => (
                    <option key={r} value={r}>{r}</option>
                  ))}
                </select>
              )}
            />
          </Field>

          {/* Email + Phone */}
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Correo" error={errors.email?.message}>
              <InputWithIcon icon={<Mail size={14} />} error={!!errors.email}>
                <input
                  type="email"
                  placeholder="nombre@bellaspa.com"
                  className="bare-input"
                  {...register('email')}
                />
              </InputWithIcon>
            </Field>
            <Field label="Teléfono / WhatsApp" error={errors.phone?.message}>
              <InputWithIcon icon={<PhoneIcon size={14} />} error={!!errors.phone}>
                <input
                  placeholder="+57 300 123 4567"
                  className="bare-input"
                  {...register('phone')}
                />
              </InputWithIcon>
            </Field>
          </div>

          {/* Cédula + Fecha */}
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Cédula" error={errors.idNumber?.message}>
              <InputWithIcon icon={<IdCard size={14} />} error={!!errors.idNumber}>
                <input
                  placeholder="1.020.554.901"
                  className="bare-input"
                  {...register('idNumber')}
                />
              </InputWithIcon>
            </Field>
            <Field label="Fecha de ingreso" hint="Opcional" error={errors.hireDate?.message}>
              {/* react-hook-form controla el value/onChange a través del Controller
                  porque DatePicker es controlado (no nativo). */}
              <Controller
                control={control}
                name="hireDate"
                render={({ field }) => (
                  <DatePicker
                    value={field.value ?? ''}
                    onChange={field.onChange}
                    placeholder="Sin fecha"
                    fullWidth
                  />
                )}
              />
            </Field>
          </div>

          {/* Status (solo al editar) */}
          {!isNew && (
            <Field label="Estado" error={errors.status?.message}>
              <Controller
                control={control}
                name="status"
                render={({ field }) => (
                  <div className="grid grid-cols-3 gap-2">
                    {(['Active', 'Vacation', 'Inactive'] as const).map((s) => (
                      <button
                        key={s}
                        type="button"
                        onClick={() => field.onChange(s)}
                        className={cls(
                          'px-3 py-2 rounded-lg border text-[12.5px] font-medium transition',
                          field.value === s
                            ? 'bg-brand-50 border-brand-700 text-brand-800 ring-2 ring-brand-700/15'
                            : 'bg-white border-warm-200 text-warm-700 hover:border-warm-300',
                        )}
                      >
                        {s === 'Active' ? 'Activa' : s === 'Vacation' ? 'Vacaciones' : 'Inactiva'}
                      </button>
                    ))}
                  </div>
                )}
              />
            </Field>
          )}

          {/* Servicios M:N */}
          <Field
            label="Servicios que sabe hacer"
            hint={
              servicesQ.isLoading
                ? 'Cargando…'
                : `${watch('serviceIds')?.length ?? 0} de ${servicesQ.data?.length ?? 0} seleccionados`
            }
          >
            {servicesQ.isLoading ? (
              <div className="text-[12.5px] text-warm-500 py-3">Cargando servicios...</div>
            ) : !servicesQ.data || servicesQ.data.length === 0 ? (
              <div className="text-[12.5px] text-warm-500 py-3 px-3 rounded-lg bg-warm-50 border border-warm-150">
                No hay servicios activos en el catálogo. Creá servicios primero en{' '}
                <strong>Configuración → Servicios</strong>.
              </div>
            ) : (
              <Controller
                control={control}
                name="serviceIds"
                render={({ field }) => (
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5 max-h-64 overflow-y-auto p-1 rounded-lg border border-warm-150 bg-warm-50/40">
                    {servicesQ.data!.map((svc) => {
                      const sel = field.value.includes(svc.id)
                      const cat = CATEGORY_BY_ID[svc.category]
                      return (
                        <button
                          key={svc.id}
                          type="button"
                          onClick={() => {
                            const next = sel
                              ? field.value.filter((id) => id !== svc.id)
                              : [...field.value, svc.id]
                            field.onChange(next)
                          }}
                          className={cls(
                            'flex items-center gap-2.5 px-3 py-2 rounded-lg border text-left transition',
                            sel
                              ? 'bg-brand-50 border-brand-700 ring-2 ring-brand-700/15'
                              : 'bg-white border-warm-200 hover:border-warm-300',
                          )}
                        >
                          <span
                            className={cls(
                              'w-5 h-5 rounded-md flex items-center justify-center flex-shrink-0 transition',
                              sel ? 'bg-brand-700 text-white' : 'border-2 border-warm-300',
                            )}
                          >
                            {sel && <Check size={12} strokeWidth={3} />}
                          </span>
                          <div className="flex-1 min-w-0">
                            <div
                              className={cls(
                                'text-[12.5px] truncate',
                                sel ? 'text-brand-800 font-medium' : 'text-warm-800',
                              )}
                            >
                              {svc.name}
                            </div>
                            <div className="text-[10.5px] text-warm-500 truncate">
                              {cat?.label ?? svc.category} · {svc.durationMinutes} min
                            </div>
                          </div>
                        </button>
                      )
                    })}
                  </div>
                )}
              />
            )}
          </Field>
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
              <Trash2 size={13} /> Archivar estilista
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
              form="stylist-form"
              disabled={submitting}
              className="px-5 py-2.5 rounded-lg text-[13.5px] font-medium flex items-center gap-1.5 transition bg-brand-700 hover:bg-brand-800 text-white shadow-soft disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {submitting ? 'Guardando…' : isNew ? 'Agregar al equipo' : 'Guardar cambios'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Helpers internos                                                          */
/* -------------------------------------------------------------------------- */

function getInitialValues(initial: StylistResponse | null): StylistFormData {
  if (!initial) return defaultStylistForm
  return {
    fullName: initial.fullName,
    role: initial.role,
    email: initial.email ?? '',
    phone: initial.phone ?? '',
    idNumber: initial.idNumber ?? '',
    color: initial.color ?? '#d6e6dd',
    hireDate: initial.hireDate ?? '',
    status: initial.status,
    serviceIds: initial.services.map((s) => s.id),
  }
}

function inputClass(error: boolean) {
  return cls(
    'w-full px-3.5 py-2.5 rounded-lg bg-white border text-[14px] text-warm-800 placeholder:text-warm-400 outline-none transition',
    'focus:ring-2 focus:ring-brand-100 focus:border-brand-500',
    error ? 'border-terra-300' : 'border-warm-200',
  )
}

function Field({
  label,
  hint,
  error,
  children,
}: {
  label: string
  hint?: string
  error?: string
  children: React.ReactNode
}) {
  return (
    <div>
      <div className="flex items-baseline justify-between mb-1.5">
        <label className="text-[12.5px] font-medium text-warm-700">{label}</label>
        {hint && !error && <span className="text-[11px] text-warm-400">{hint}</span>}
        {error && <span className="text-[11px] text-terra-500">{error}</span>}
      </div>
      {children}
    </div>
  )
}

function Input(props: React.InputHTMLAttributes<HTMLInputElement> & { error?: boolean }) {
  const { error, className, ...rest } = props
  return <input {...rest} className={cls(inputClass(!!error), className)} />
}

function InputWithIcon({
  icon,
  error,
  children,
}: {
  icon: React.ReactNode
  error?: boolean
  children: React.ReactNode
}) {
  return (
    <div
      className={cls(
        'flex items-center gap-2 px-3 rounded-lg border bg-white transition',
        error ? 'border-terra-300' : 'border-warm-200',
        'focus-within:ring-2 focus-within:ring-brand-100 focus-within:border-brand-500',
      )}
    >
      <span className="text-warm-400 flex-shrink-0">{icon}</span>
      {children}
      <style>{`
        .bare-input {
          flex: 1;
          padding: 0.625rem 0;
          background: transparent;
          outline: none;
          font-size: 14px;
          color: rgb(46 43 37);
          min-width: 0;
        }
        .bare-input::placeholder { color: rgb(168 159 142); }
      `}</style>
    </div>
  )
}
