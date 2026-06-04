import { useState } from 'react'
import { Clock, Wallet, Pencil, Copy, Ban, CheckCircle, Percent } from 'lucide-react'
import { cls } from '@/lib/cls'
import type { ServiceResponse } from '@/api/services'
import { CATEGORY_BY_ID, fmtCOP } from '../types'
import { serviceExtrasStorage } from '../storage'
import { useCommissionsSetting } from '@/features/commissions/useCommissionsSetting'

// requiresDeposit y depositPercentage ahora vienen del backend (service.requiresDeposit / service.depositPercentage)
// Solo `assignedStylistIds` queda en localStorage hasta que hagamos F5 (Estilistas)

interface ServiceCardProps {
  service: ServiceResponse
  /**
   * Handlers de CRUD. Si NO se pasan (recepción / stylist viendo el
   * catálogo para crear citas), el footer de acciones no se renderiza.
   * El backend igual rechaza POST/PUT/DELETE de no-admin.
   */
  onEdit?: (service: ServiceResponse) => void
  onDuplicate?: (service: ServiceResponse) => void
  onToggleActive?: (service: ServiceResponse) => void
  /** Lista de estilistas para mostrar avatares (vendrá del API en F5; por ahora vacío) */
  stylists?: Array<{ id: string; name: string; tone?: string }>
}

const TONE_FALLBACK_PALETTE = [
  { bg: 'bg-[#f5dfd8]', fg: 'text-[#8a4a3c]' },
  { bg: 'bg-[#f1e3c1]', fg: 'text-[#7a5b1f]' },
  { bg: 'bg-[#dde6d4]', fg: 'text-[#3f5a37]' },
  { bg: 'bg-[#e8d2d4]', fg: 'text-[#7a3d44]' },
  { bg: 'bg-[#dde7eb]', fg: 'text-[#3e5664]' },
  { bg: 'bg-[#f4dde2]', fg: 'text-[#824354]' },
]

const initialsOf = (name: string) =>
  name.split(' ').slice(0, 2).map((w) => w[0]?.toUpperCase()).join('')

/**
 * Card de un servicio individual.
 * Replica fielmente el ServiceCard del mockup config-servicios.jsx.
 */
export function ServiceCard({
  service,
  onEdit,
  onDuplicate,
  onToggleActive,
  stylists = [],
}: ServiceCardProps) {
  const [extras] = useState(() => serviceExtrasStorage.get(service.id))
  const cat = CATEGORY_BY_ID[service.category]

  // Solo mostramos el badge de comisión cuando el salón tiene activo
  // el módulo. Si no, ensucia la card con info que no se usa.
  const { data: commissionsSetting } = useCommissionsSetting()
  const showCommission = (commissionsSetting?.enabled ?? false) && service.commissionPercentage > 0

  const assignedStylists = extras.assignedStylistIds
    .map((id) => stylists.find((s) => s.id === id))
    .filter((s): s is { id: string; name: string; tone?: string } => Boolean(s))

  const visibleStylists = assignedStylists.slice(0, 3)
  const extraCount = assignedStylists.length - 3

  // requiresDeposit y depositPercentage vienen del backend
  const depositAmount = service.requiresDeposit
    ? (service.price * service.depositPercentage) / 100
    : 0

  return (
    <div
      className={cls(
        'group relative rounded-2xl bg-white border border-warm-150 overflow-hidden flex flex-col transition hover:shadow-soft',
        !service.isActive && 'opacity-75',
      )}
    >
      {/* Photo placeholder con gradient según categoría */}
      <div className="h-32 relative" style={{ background: cat.gradient }}>
        <div className="absolute inset-0 bg-gradient-to-t from-black/30 via-transparent to-black/0" />

        {/* Badge categoría */}
        <div className="absolute top-3 left-3 flex items-center gap-1.5">
          <span
            className={cls(
              'text-[10px] tracking-[0.12em] uppercase font-medium px-2 py-0.5 rounded-md',
              cat.badgeBg,
              cat.badgeFg,
            )}
          >
            {cat.label}
          </span>
        </div>

        {/* Badge estado */}
        <div className="absolute top-3 right-3">
          <span
            className={cls(
              'text-[10px] tracking-[0.12em] uppercase font-medium px-2 py-0.5 rounded-md flex items-center gap-1.5',
              service.isActive
                ? 'bg-white/90 text-brand-800'
                : 'bg-warm-800/80 text-white',
            )}
          >
            <span
              className={cls(
                'w-1.5 h-1.5 rounded-full',
                service.isActive ? 'bg-brand-500' : 'bg-warm-400',
              )}
            />
            {service.isActive ? 'Activo' : 'Inactivo'}
          </span>
        </div>

        {/* Nombre del servicio overlay */}
        <div className="absolute bottom-3 left-3 right-3 flex items-end justify-between gap-2">
          <div className="font-serif text-[20px] text-white leading-tight pr-2 line-clamp-2 drop-shadow">
            {service.name}
          </div>
        </div>
      </div>

      {/* Body */}
      <div className="p-4 flex-1 flex flex-col">
        <div className="flex items-baseline justify-between gap-2">
          <div className="font-serif text-[24px] text-warm-800 tabular-nums leading-none">
            {fmtCOP(service.price)}
          </div>
          <div className="flex items-center gap-2">
            {showCommission && (
              <span
                title={`Comisión al estilista: ${service.commissionPercentage}%`}
                className="inline-flex items-center gap-0.5 text-[11px] font-medium px-1.5 py-0.5 rounded-md bg-brand-50 text-brand-700 tabular-nums"
              >
                <Percent size={10} strokeWidth={2.2} />
                {service.commissionPercentage}
              </span>
            )}
            <div className="text-[12px] text-warm-500 flex items-center gap-1">
              <Clock size={11} />
              {service.durationMinutes} min
            </div>
          </div>
        </div>

        {/* Banda anticipo (datos vienen del backend) */}
        <div
          className={cls(
            'mt-3 px-3 py-2 rounded-lg flex items-center gap-2.5 text-[12px]',
            service.requiresDeposit
              ? 'bg-gold-50 border border-gold-200'
              : 'bg-warm-50 border border-warm-150',
          )}
        >
          <Wallet
            size={13}
            className={service.requiresDeposit ? 'text-gold-600' : 'text-warm-400'}
          />
          <span className={service.requiresDeposit ? 'text-gold-600' : 'text-warm-500'}>
            {service.requiresDeposit ? (
              <>
                Anticipo <strong className="font-semibold">{service.depositPercentage}%</strong>{' '}
                · {fmtCOP(depositAmount)}
              </>
            ) : (
              'Sin anticipo'
            )}
          </span>
        </div>

        {/* Estilistas asignadas (si hay) */}
        {assignedStylists.length > 0 ? (
          <div className="mt-3 flex items-center justify-between gap-2">
            <div className="flex items-center -space-x-1.5">
              {visibleStylists.map((s, i) => {
                const tone = TONE_FALLBACK_PALETTE[i % TONE_FALLBACK_PALETTE.length]
                return (
                  <div
                    key={s.id}
                    title={s.name}
                    className={cls(
                      'w-7 h-7 rounded-full ring-2 ring-white flex items-center justify-center font-serif text-[11px]',
                      tone.bg,
                      tone.fg,
                    )}
                  >
                    {initialsOf(s.name)}
                  </div>
                )
              })}
              {extraCount > 0 && (
                <div className="w-7 h-7 rounded-full ring-2 ring-white bg-warm-100 text-warm-600 flex items-center justify-center text-[10.5px] font-medium">
                  +{extraCount}
                </div>
              )}
            </div>
            <span className="text-[11.5px] text-warm-500">
              {assignedStylists.length} estilistas
            </span>
          </div>
        ) : (
          <div className="mt-3 text-[11.5px] text-warm-400 italic">
            Sin estilistas asignadas
          </div>
        )}

        {/* Footer acciones — solo si hay handlers (admin) */}
        {(onEdit || onDuplicate || onToggleActive) && (
          <div className="mt-4 pt-3 border-t border-warm-150 flex items-center gap-1">
            {onEdit && (
              <button
                type="button"
                onClick={() => onEdit(service)}
                className="flex-1 px-2.5 py-1.5 rounded-md text-[12px] text-warm-700 hover:bg-warm-50 flex items-center justify-center gap-1.5"
              >
                <Pencil size={12} /> Editar
              </button>
            )}
            {onEdit && onDuplicate && <span className="w-px h-4 bg-warm-150" />}
            {onDuplicate && (
              <button
                type="button"
                onClick={() => onDuplicate(service)}
                className="flex-1 px-2.5 py-1.5 rounded-md text-[12px] text-warm-700 hover:bg-warm-50 flex items-center justify-center gap-1.5"
              >
                <Copy size={12} /> Duplicar
              </button>
            )}
            {(onEdit || onDuplicate) && onToggleActive && <span className="w-px h-4 bg-warm-150" />}
            {onToggleActive && (
              <button
                type="button"
                onClick={() => onToggleActive(service)}
                className={cls(
                  'flex-1 px-2.5 py-1.5 rounded-md text-[12px] flex items-center justify-center gap-1.5',
                  service.isActive
                    ? 'text-warm-600 hover:bg-warm-50'
                    : 'text-brand-700 hover:bg-brand-50',
                )}
              >
                {service.isActive ? (
                  <>
                    <Ban size={12} /> Desactivar
                  </>
                ) : (
                  <>
                    <CheckCircle size={12} /> Activar
                  </>
                )}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
