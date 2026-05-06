import type { ReactNode } from 'react'
import { ArrowLeft, ArrowRight } from 'lucide-react'
import { cls } from '@/lib/cls'

interface NavButtonsProps {
  onBack: () => void
  onNext: () => void
  valid: boolean
  loading?: boolean
  nextLabel?: string
  nextIcon?: ReactNode
}

/**
 * Botones "Atrás / Continuar" del wizard.
 * Replica NavButtons de onboarding-steps.jsx.
 */
export function NavButtons({
  onBack,
  onNext,
  valid,
  loading = false,
  nextLabel = 'Continuar',
  nextIcon,
}: NavButtonsProps) {
  const disabled = !valid || loading
  return (
    <div className="pt-2 flex items-center gap-3">
      <button
        type="button"
        onClick={onBack}
        disabled={loading}
        className="px-4 py-3 rounded-xl border border-warm-200 bg-white hover:bg-warm-50 text-warm-700 text-[13.5px] font-medium flex items-center gap-2 disabled:opacity-50"
      >
        <ArrowLeft size={14} />
        Atrás
      </button>

      <button
        type="button"
        onClick={onNext}
        disabled={disabled}
        className={cls(
          'flex-1 px-5 py-3 rounded-xl text-[14px] font-medium flex items-center justify-center gap-2 transition',
          disabled
            ? 'bg-warm-150 text-warm-400 cursor-not-allowed'
            : 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft',
        )}
      >
        {loading ? 'Procesando…' : nextLabel}
        {!loading && (nextIcon ?? <ArrowRight size={14} />)}
      </button>
    </div>
  )
}
