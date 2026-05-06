import type { HTMLAttributes, ReactNode } from 'react'
import { cls } from '@/lib/cls'

type Padding = 'none' | 'sm' | 'md' | 'lg'
type Variant = 'default' | 'elevated' | 'flat'

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Espaciado interno. 'none' deja al padre componer (ej. cuando hay header propio) */
  padding?: Padding
  /** Estilo de elevación */
  variant?: Variant
  children?: ReactNode
}

/**
 * Card base de BellaSync.
 *
 *  - default   blanco + border warm-150 + shadow-softer  (cards de listado)
 *  - elevated  blanco + shadow-soft (sin borde)          (cards principales)
 *  - flat      sin fondo ni sombra                       (agrupador semántico)
 */
export function Card({
  padding = 'md',
  variant = 'default',
  className,
  children,
  ...rest
}: CardProps) {
  const paddings: Record<Padding, string> = {
    none: '',
    sm:   'p-3',
    md:   'p-5',
    lg:   'p-7',
  }

  const variants: Record<Variant, string> = {
    default:  'bg-white border border-warm-150 shadow-softer',
    elevated: 'bg-white shadow-soft',
    flat:     'bg-transparent',
  }

  return (
    <div
      {...rest}
      className={cls(
        'rounded-xl',
        paddings[padding],
        variants[variant],
        className,
      )}
    >
      {children}
    </div>
  )
}

/** Subcomponente para títulos dentro de la Card (texto pequeño en uppercase) */
export function CardTitle({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cls('text-[10.5px] uppercase tracking-[0.14em] font-medium text-warm-400 mb-2', className)}>
      {children}
    </div>
  )
}
