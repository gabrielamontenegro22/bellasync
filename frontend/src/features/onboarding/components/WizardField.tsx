import type { ReactNode } from 'react'
import { AlertCircle } from 'lucide-react'

interface WizardFieldProps {
  label: string
  hint?: string | null
  error?: string | null
  optional?: boolean
  children: ReactNode
}

/**
 * Field primitive del wizard — replica el `Field` inline del mockup
 * (data.jsx → Field). Etiqueta arriba, hint abajo, error a la derecha.
 */
export function WizardField({ label, hint, error, optional, children }: WizardFieldProps) {
  return (
    <div>
      <div className="flex items-baseline justify-between mb-1.5">
        <label className="text-[12.5px] font-medium text-warm-700">
          {label}
          {optional && <span className="text-warm-400 font-normal"> · opcional</span>}
        </label>
        {error && (
          <span className="text-[11px] text-terra-500 flex items-center gap-1">
            <AlertCircle size={12} />
            {error}
          </span>
        )}
      </div>
      {children}
      {hint && !error && (
        <div className="text-[11.5px] text-warm-500 mt-1">{hint}</div>
      )}
    </div>
  )
}

/**
 * Input plano usado dentro del wizard (sin label, lo pone el WizardField).
 * Replica el estilo exacto del Input inline del mockup.
 */
export function WizardInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  const { className = '', ...rest } = props
  return (
    <input
      {...rest}
      className={
        'w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 ' +
        'text-[14px] text-warm-800 placeholder:text-warm-400 ' +
        'focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition ' +
        className
      }
    />
  )
}

/** Select estilizado igual al WizardInput. */
export function WizardSelect(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  const { className = '', children, ...rest } = props
  return (
    <select
      {...rest}
      className={
        'w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 ' +
        'text-[14px] text-warm-800 ' +
        'focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition ' +
        className
      }
    >
      {children}
    </select>
  )
}
