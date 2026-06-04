import { useEffect, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { cls } from '@/lib/cls'

type Size = 'sm' | 'md' | 'lg' | 'xl'

interface ModalProps {
  /** Texto del header. Si no querés header, pasá `null` y armá el tuyo en children. */
  title?: ReactNode
  /** Se invoca al click en backdrop, en el botón de cerrar y al ESC. */
  onClose: () => void
  /** Ancho máximo en desktop. Default 'md' (max-w-lg / 512px). */
  size?: Size
  /**
   * Si true, el modal NO sube como sheet desde abajo en mobile —
   * queda centrado. Útil para confirmaciones cortas. Default false.
   */
  centeredOnMobile?: boolean
  children: ReactNode
}

/**
 * Modal primitivo compartido. Resuelve dos problemas que aparecían
 * cuando cada feature montaba su propio modal a mano:
 *
 *  1. **Mobile**: el patrón viejo (`fixed inset-0 flex items-center
 *     justify-center p-4`) dejaba modales largos cortados sin scroll
 *     interno. Acá el panel siempre tiene `max-h-[90vh]` + overflow
 *     interno, y en `<sm` por default se pega al fondo de la pantalla
 *     (bottom sheet) con esquinas superiores redondeadas — gesto
 *     conocido en apps mobile.
 *  2. **Boilerplate**: cerrar al ESC, cerrar al click-fuera, evitar
 *     scroll del body — todo eso se duplicaba en 7 modales. Acá
 *     se centraliza.
 *
 * Ejemplo:
 *   <Modal title="Nueva cita" onClose={close} size="lg">
 *     <FormFields/>
 *     <ModalFooter>...</ModalFooter>
 *   </Modal>
 *
 * Si necesitás un header custom (badge, eyebrow, etc.) podés pasar
 * `title={null}` y armar tu propio header como primer hijo.
 */
export function Modal({
  title,
  onClose,
  size = 'md',
  centeredOnMobile = false,
  children,
}: ModalProps) {
  // Cerrar al ESC. Solo escuchamos una vez por mount.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  // Evitar que el body scrollee detrás del modal. Importante en mobile
  // donde el body sino "se cuela" por debajo del sheet.
  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  const sizes: Record<Size, string> = {
    sm: 'sm:max-w-sm',
    md: 'sm:max-w-lg',
    lg: 'sm:max-w-2xl',
    xl: 'sm:max-w-4xl',
  }

  // Layout del backdrop:
  //   - centeredOnMobile=false (default): bottom sheet en <sm (items-end),
  //     centrado en ≥sm (sm:items-center). Padding lateral solo en ≥sm
  //     porque el sheet va flush al edge en mobile.
  //   - centeredOnMobile=true: siempre centrado con padding 16px.
  const wrapperLayout = centeredOnMobile
    ? 'items-center justify-center p-4'
    : 'items-end justify-center sm:items-center sm:justify-center sm:p-4'

  // Layout del panel:
  //   - Mobile sheet: w-full + rounded-t-2xl (esquinas inferiores rectas),
  //     max-h-[92vh] para dejar ver un poco del backdrop arriba.
  //   - Desktop: w-full + max-w-X + rounded-2xl entero, max-h-[88vh].
  //   - centeredOnMobile fuerza rounded-2xl y max-h-[88vh] siempre.
  const panelLayout = centeredOnMobile
    ? cls('w-full max-w-[calc(100%-32px)] rounded-2xl max-h-[88vh]', sizes[size])
    : cls(
        'w-full rounded-t-2xl max-h-[92vh]',
        'sm:rounded-2xl sm:max-h-[88vh]',
        sizes[size],
      )

  return (
    <div
      className={cls(
        'fixed inset-0 z-50 flex bg-warm-900/40 backdrop-blur-[2px]',
        'anim-fade',
        wrapperLayout,
      )}
      onClick={onClose}
    >
      <div
        className={cls(
          'bg-white shadow-pop flex flex-col overflow-hidden',
          panelLayout,
        )}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
      >
        {title !== undefined && title !== null && (
          <header className="flex-shrink-0 flex items-center justify-between px-5 py-4 border-b border-warm-150">
            <h2 className="font-serif text-[19px] sm:text-[20px] text-brand-700 truncate pr-3">
              {title}
            </h2>
            <button
              type="button"
              onClick={onClose}
              className="p-1.5 -mr-1.5 text-warm-400 hover:text-warm-700 hover:bg-warm-100 rounded-md transition flex-shrink-0"
              aria-label="Cerrar"
            >
              <X size={18} strokeWidth={1.8} />
            </button>
          </header>
        )}

        {/* Body: scrolleable. Padding lateral consistente con header. */}
        <div className="flex-1 overflow-y-auto px-5 py-4">
          {children}
        </div>
      </div>
    </div>
  )
}

/**
 * Footer estándar para acciones de un Modal. Stickea al fondo del
 * sheet en mobile, y separa visualmente con border-top.
 *
 * Uso típico:
 *   <ModalFooter>
 *     <Button variant="secondary" onClick={close} fullWidth>Cancelar</Button>
 *     <Button onClick={save} fullWidth>Guardar</Button>
 *   </ModalFooter>
 *
 * Si pasás `error`, se muestra arriba de los botones en terra.
 */
export function ModalFooter({
  children,
  error,
}: {
  children: ReactNode
  error?: string | null
}) {
  return (
    <div className="-mx-5 -mb-4 mt-5 px-5 pt-3 pb-4 border-t border-warm-150 bg-warm-50/40">
      {error && (
        <p className="mb-2.5 rounded-md bg-terra-100 px-3 py-2 text-[12.5px] text-terra-700">
          {error}
        </p>
      )}
      <div className="flex gap-2">
        {children}
      </div>
    </div>
  )
}
