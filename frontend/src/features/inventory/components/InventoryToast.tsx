import { useEffect } from 'react'
import { CheckCircle2 } from 'lucide-react'

interface Props {
  message: string | null
  onClear: () => void
  /** ms hasta auto-cerrar. Default 2.6s, mismo que el mockup. */
  duration?: number
}

/**
 * Toast de confirmación para /inventario. Mismo look del mockup:
 * pill oscura abajo-centro con check verde + texto. Auto-cierra a los
 * 2.6 segundos para no estorbar.
 *
 * Uso desde el page padre:
 *   const [toast, setToast] = useState<string | null>(null)
 *   <InventoryToast message={toast} onClear={() => setToast(null)} />
 *   // ...
 *   onSuccess: () => { setToast('Entrada registrada · Wella'); refresh() }
 */
export function InventoryToast({ message, onClear, duration = 2600 }: Props) {
  useEffect(() => {
    if (!message) return
    const t = setTimeout(onClear, duration)
    return () => clearTimeout(t)
  }, [message, onClear, duration])

  if (!message) return null

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 flex items-center gap-2.5 bg-warm-800 text-white rounded-full pl-3 pr-5 py-2.5 shadow-pop anim-pop max-w-[90vw]">
      <span className="w-6 h-6 rounded-full bg-brand-500 flex items-center justify-center flex-shrink-0">
        <CheckCircle2 size={14}/>
      </span>
      <span className="text-[13px] font-medium truncate">{message}</span>
    </div>
  )
}
