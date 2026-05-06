import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { cls } from '@/lib/cls'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size    = 'sm' | 'md' | 'lg'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  /** Icono opcional a la izquierda del texto */
  leftIcon?: ReactNode
  /** Icono opcional a la derecha del texto */
  rightIcon?: ReactNode
  /** Estado de carga: deshabilita y muestra un spinner. */
  loading?: boolean
  /** Ocupa todo el ancho disponible */
  fullWidth?: boolean
}

/**
 * Botón base de BellaSync.
 *
 * Variantes:
 *  - primary   verde brand-700 sólido  (acción principal)
 *  - secondary blanco con borde warm   (acción secundaria)
 *  - ghost     transparente con hover  (acciones discretas)
 *  - danger    terra-500 sólido        (acciones destructivas)
 */
export function Button({
  variant = 'primary',
  size = 'md',
  leftIcon,
  rightIcon,
  loading = false,
  fullWidth = false,
  disabled,
  className,
  children,
  ...rest
}: ButtonProps) {
  const isDisabled = disabled || loading

  const base = 'inline-flex items-center justify-center gap-2 font-medium rounded-lg transition select-none disabled:cursor-not-allowed disabled:opacity-60'

  const sizes: Record<Size, string> = {
    sm: 'text-[12.5px] px-3 py-1.5',
    md: 'text-[13.5px] px-4 py-2.5',
    lg: 'text-[14.5px] px-5 py-3',
  }

  const variants: Record<Variant, string> = {
    primary:   'bg-brand-700 text-white hover:bg-brand-800 active:bg-brand-900 shadow-soft',
    secondary: 'bg-white text-warm-700 border border-warm-200 hover:border-warm-300 hover:text-warm-800',
    ghost:     'bg-transparent text-warm-600 hover:text-warm-800 hover:bg-warm-100',
    danger:    'bg-terra-500 text-white hover:bg-terra-500/90 shadow-soft',
  }

  return (
    <button
      {...rest}
      disabled={isDisabled}
      className={cls(
        base,
        sizes[size],
        variants[variant],
        fullWidth && 'w-full',
        className,
      )}
    >
      {loading ? <Spinner /> : leftIcon}
      {children}
      {!loading && rightIcon}
    </button>
  )
}

function Spinner() {
  return (
    <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeOpacity="0.25" strokeWidth="3" />
      <path d="M22 12a10 10 0 0 1-10 10" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </svg>
  )
}
