import { CheckCircle } from 'lucide-react'
import { cls } from '@/lib/cls'

/**
 * Primitivos visuales compartidos por las pantallas de Configuración
 * (Información general, Política de pagos, Comisiones, …). Replican
 * el mockup config-servicios.jsx — eyebrow uppercase gold + título
 * serif gigante + bloques con icono + SaveBar sticky abajo.
 *
 * No tienen lógica; solo presentación. Cada página trae su useQuery /
 * useMutation y conecta los primitivos.
 */

// ───────────────────────────────────────────────────────────────────────
// Header de pantalla
// ───────────────────────────────────────────────────────────────────────

export function SettingsHeader({
  eyebrow,
  title,
  desc,
}: {
  /** Texto pequeño arriba en gold uppercase (ej "Ajustes del salón"). */
  eyebrow: string
  title: string
  desc?: string
}) {
  return (
    <div className="mb-8">
      <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
        {eyebrow}
      </div>
      <h1 className="font-serif text-[40px] lg:text-[46px] leading-[1.02] tracking-tight text-warm-800 mt-1">
        {title}
      </h1>
      {desc && (
        <p className="text-[14px] text-warm-600 leading-relaxed mt-2.5 max-w-2xl">
          {desc}
        </p>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Bloque dentro de la pantalla
// ───────────────────────────────────────────────────────────────────────

export function SettingsBlock({
  icon,
  title,
  children,
  last,
}: {
  icon?: React.ReactNode
  title: string
  children: React.ReactNode
  /** Si es el último bloque, no dibuja el border-b separador. */
  last?: boolean
}) {
  return (
    <div className={cls('py-7', !last && 'border-b border-warm-150')}>
      <div className="flex items-center gap-2 mb-5">
        {icon && <span className="text-warm-400">{icon}</span>}
        <div className="text-[12.5px] font-semibold text-warm-800 tracking-wide">
          {title}
        </div>
      </div>
      <div className="space-y-5">{children}</div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Field con label + hint
// ───────────────────────────────────────────────────────────────────────

export function SettingsField({
  label,
  hint,
  required,
  children,
}: {
  label: string
  hint?: string
  required?: boolean
  children: React.ReactNode
}) {
  return (
    <div>
      <div className="flex items-baseline justify-between gap-3 mb-1.5">
        <label className="text-[12.5px] font-medium text-warm-700">
          {label}
          {required && <span className="text-terra-500"> *</span>}
        </label>
        {hint && <span className="text-[11px] text-warm-400 text-right">{hint}</span>}
      </div>
      {children}
    </div>
  )
}

/** Clase reutilizable para inputs de Configuración. */
export const inputCls =
  'w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition'

// ───────────────────────────────────────────────────────────────────────
// SaveBar sticky abajo de la pantalla
// ───────────────────────────────────────────────────────────────────────

export function SaveBar({
  show,
  saved,
  saving,
  error,
  onSave,
  onDiscard,
}: {
  /** true cuando hay cambios sin guardar */
  show: boolean
  /** true cuando se acaba de guardar — muestra banner verde por unos segundos */
  saved: boolean
  /** true mientras la mutation está pending */
  saving?: boolean
  /** mensaje de error del último intento de guardar */
  error?: string | null
  onSave: () => void
  onDiscard: () => void
}) {
  // Estado verde: "Cambios guardados"
  if (saved) {
    return (
      <div className="sticky bottom-0 left-0 right-0 z-20 bg-brand-50 border-t border-brand-200 px-6 lg:px-10 py-3.5 flex items-center gap-2 anim-fade">
        <CheckCircle size={16} className="text-brand-700" />
        <span className="text-[13px] text-brand-800 font-medium">Cambios guardados</span>
      </div>
    )
  }
  // Estado idle (nada que guardar): no renderiza.
  if (!show && !error) return null
  return (
    <div className="sticky bottom-0 left-0 right-0 z-20 bg-warm-800 px-6 lg:px-10 py-3 flex items-center gap-3 anim-fade">
      <span className="text-[13px] text-warm-100 flex-1">
        {error ? <span className="text-terra-300">{error}</span> : 'Tienes cambios sin guardar'}
      </span>
      <button
        type="button"
        onClick={onDiscard}
        disabled={saving}
        className="px-3.5 py-2 rounded-lg text-[12.5px] font-medium text-warm-200 hover:bg-white/10 disabled:opacity-50"
      >
        Descartar
      </button>
      <button
        type="button"
        onClick={onSave}
        disabled={saving}
        className="px-4 py-2 rounded-lg bg-brand-600 hover:bg-brand-500 text-white text-[12.5px] font-medium flex items-center gap-1.5 disabled:opacity-60"
      >
        <CheckCircle size={14} />
        {saving ? 'Guardando…' : 'Guardar cambios'}
      </button>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// NumberStepper con -/+ — para inputs de tiempos/cantidades
// ───────────────────────────────────────────────────────────────────────

export function NumberStepper({
  value,
  onChange,
  min,
  max,
  step = 1,
  suffix,
}: {
  value: number
  onChange: (v: number) => void
  min: number
  max: number
  step?: number
  /** Texto a la derecha del input (ej "horas", "minutos antes"). */
  suffix?: string
}) {
  const clamp = (v: number) => Math.max(min, Math.min(max, v))
  return (
    <div className="flex items-center gap-2 flex-shrink-0">
      <div className="flex items-center rounded-lg border border-warm-200 bg-white overflow-hidden">
        <button
          type="button"
          onClick={() => onChange(clamp(value - step))}
          className="px-2.5 py-1.5 text-warm-500 hover:bg-warm-50"
        >
          −
        </button>
        <input
          value={value}
          onChange={(e) => {
            const n = Number(e.target.value)
            onChange(Number.isFinite(n) ? clamp(n) : min)
          }}
          inputMode="numeric"
          className="w-14 text-center py-1.5 text-[13.5px] text-warm-800 tabular-nums outline-none bg-white"
        />
        <button
          type="button"
          onClick={() => onChange(clamp(value + step))}
          className="px-2.5 py-1.5 text-warm-500 hover:bg-warm-50"
        >
          +
        </button>
      </div>
      {suffix && <span className="text-[12px] text-warm-500">{suffix}</span>}
    </div>
  )
}

/** Fila típica: label a la izquierda + NumberStepper a la derecha. */
export function NumberRow({
  label,
  suffix,
  value,
  onChange,
  min,
  max,
  step,
}: {
  label: string
  suffix?: string
  value: number
  onChange: (v: number) => void
  min: number
  max: number
  step?: number
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <div className="text-[13.5px] text-warm-800">{label}</div>
      <NumberStepper
        value={value}
        onChange={onChange}
        min={min}
        max={max}
        step={step}
        suffix={suffix}
      />
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Toggle visual
// ───────────────────────────────────────────────────────────────────────

export function Toggle({
  on,
  onChange,
  disabled,
}: {
  on: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      onClick={() => !disabled && onChange(!on)}
      disabled={disabled}
      className={cls(
        'relative w-11 h-6 rounded-full transition flex-shrink-0',
        on ? 'bg-brand-700' : 'bg-warm-200',
        disabled && 'opacity-60 cursor-not-allowed',
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

/** Fila típica: título + descripción + Toggle a la derecha. */
export function ToggleRow({
  title,
  desc,
  on,
  onChange,
  disabled,
}: {
  title: string
  desc?: React.ReactNode
  on: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <div className="flex-1 min-w-0">
        <div className="text-[13.5px] font-medium text-warm-800">{title}</div>
        {desc && <div className="text-[12px] text-warm-500 mt-0.5">{desc}</div>}
      </div>
      <Toggle on={on} onChange={onChange} disabled={disabled} />
    </div>
  )
}
