import { forwardRef, useId } from 'react'
import type { InputHTMLAttributes, ReactNode } from 'react'
import { cls } from '@/lib/cls'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  /** Etiqueta visible arriba del input */
  label?: string
  /** Texto de ayuda gris debajo (cuando no hay error) */
  hint?: string
  /** Mensaje de error — al setearlo cambia el borde a terra y reemplaza al hint */
  error?: string
  /** Icono dentro del input, a la izquierda */
  leftIcon?: ReactNode
  /** Icono dentro del input, a la derecha (ej. botón mostrar/ocultar password) */
  rightIcon?: ReactNode
}

/**
 * Input base de BellaSync.
 * Estilo extraído del mockup: borde warm-200, focus ring brand-200, error en terra-500.
 */
export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, hint, error, leftIcon, rightIcon, id, className, ...rest },
  ref,
) {
  const autoId = useId()
  const inputId = id ?? autoId
  const hasError = Boolean(error)

  return (
    <div className="w-full">
      {label && (
        <label
          htmlFor={inputId}
          className="block text-[12.5px] font-medium text-warm-700 mb-1.5"
        >
          {label}
        </label>
      )}

      <div className="relative">
        {leftIcon && (
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-warm-400 pointer-events-none">
            {leftIcon}
          </span>
        )}

        <input
          {...rest}
          id={inputId}
          ref={ref}
          aria-invalid={hasError || undefined}
          aria-describedby={hint || error ? `${inputId}-help` : undefined}
          className={cls(
            'w-full rounded-lg bg-white text-[13.5px] text-warm-800 placeholder:text-warm-400',
            'border transition focus:outline-none focus:ring-2',
            leftIcon  ? 'pl-9'  : 'pl-3',
            rightIcon ? 'pr-9' : 'pr-3',
            'py-2.5',
            hasError
              ? 'border-terra-300 focus:ring-terra-300/40 focus:border-terra-500'
              : 'border-warm-200 focus:ring-brand-200 focus:border-brand-400',
            rest.disabled && 'bg-warm-50 cursor-not-allowed opacity-70',
            className,
          )}
        />

        {rightIcon && (
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-warm-400">
            {rightIcon}
          </span>
        )}
      </div>

      {(hint || error) && (
        <p
          id={`${inputId}-help`}
          className={cls(
            'mt-1.5 text-[11.5px]',
            hasError ? 'text-terra-500' : 'text-warm-500',
          )}
        >
          {error ?? hint}
        </p>
      )}
    </div>
  )
})
