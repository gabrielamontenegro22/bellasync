import { cls } from '@/lib/cls'
import { initialsOf, resolveTone } from '../types'

interface StylistAvatarProps {
  /** Nombre completo del estilista (para iniciales) */
  name: string
  /** Color hex del estilista — se mapea al tono más cercano */
  color?: string | null
  /** Tamaño en pixels (cuadrado). Default 64 */
  size?: number
  className?: string
}

/**
 * Avatar circular con iniciales sobre el color del estilista.
 * Replica el componente Avatar del mockup stylists.jsx con su overlay sutil
 * de gradiente para que no se vea totalmente plano.
 */
export function StylistAvatar({ name, color, size = 64, className }: StylistAvatarProps) {
  const tone = resolveTone(color)
  return (
    <div
      className={cls(
        'rounded-full flex items-center justify-center font-serif relative overflow-hidden flex-shrink-0',
        tone.bg,
        tone.fg,
        className,
      )}
      style={{ width: size, height: size, fontSize: size * 0.36 }}
    >
      {/* Soft texture overlay para que no luzca plano */}
      <div
        className="absolute inset-0 opacity-40"
        style={{
          background:
            'radial-gradient(circle at 30% 25%, rgba(255,255,255,.55) 0%, transparent 55%)',
        }}
      />
      <span className="relative">{initialsOf(name || '?')}</span>
    </div>
  )
}
