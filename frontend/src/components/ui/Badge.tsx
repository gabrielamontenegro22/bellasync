import type { HTMLAttributes, ReactNode } from 'react'
import { cls } from '@/lib/cls'

type Tone = 'brand' | 'gold' | 'terra' | 'neutral'

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: Tone
  /** Punto pequeño a la izquierda (útil para indicar status) */
  withDot?: boolean
  /** Si está en mayúsculas con tracking (estilo "tag" del mockup) */
  uppercase?: boolean
  children?: ReactNode
}

/**
 * Badge / chip / tag de BellaSync.
 * Sigue el estilo del mockup: pill pequeño en color suave.
 */
export function Badge({
  tone = 'brand',
  withDot = false,
  uppercase = true,
  className,
  children,
  ...rest
}: BadgeProps) {
  const tones: Record<Tone, { bg: string; text: string; dot: string }> = {
    brand:   { bg: 'bg-brand-50',  text: 'text-brand-700',  dot: 'bg-brand-500'  },
    gold:    { bg: 'bg-gold-50',   text: 'text-gold-600',   dot: 'bg-gold-400'   },
    terra:   { bg: 'bg-terra-100', text: 'text-terra-500',  dot: 'bg-terra-300'  },
    neutral: { bg: 'bg-warm-100',  text: 'text-warm-600',   dot: 'bg-warm-400'   },
  }
  const t = tones[tone]

  return (
    <span
      {...rest}
      className={cls(
        'inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[11px] font-semibold',
        uppercase && 'uppercase tracking-wider',
        t.bg,
        t.text,
        className,
      )}
    >
      {withDot && <span className={cls('w-1.5 h-1.5 rounded-full', t.dot)} />}
      {children}
    </span>
  )
}
