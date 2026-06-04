import { useState } from 'react'
import { Mail, Phone, IdCard, Calendar, MoreVertical, Pencil, Pause, Play, Trash2, Check, X, Ghost, Palmtree } from 'lucide-react'
import { cls } from '@/lib/cls'
import type { StylistResponse } from '@/api/stylists'
import { STATUS_META, fmtJoinedDate } from '../types'
import { StylistAvatar } from './StylistAvatar'

interface StylistCardProps {
  stylist: StylistResponse
  onEdit: (s: StylistResponse) => void
  onToggleStatus: (s: StylistResponse) => void
  onDelete: (s: StylistResponse) => void
  onTimeOff: (s: StylistResponse) => void
}

/**
 * Card de un estilista individual. Replica StylistCard del mockup stylists.jsx.
 *
 * Estructura:
 *   - Header: avatar + nombre + role + status badge + menú "..."
 *   - Info de contacto: email, phone, cédula, fecha de joined
 *   - Footer con stats (placeholder hasta tener módulo de citas)
 */
export function StylistCard({ stylist, onEdit, onToggleStatus, onDelete, onTimeOff }: StylistCardProps) {
  const [menuOpen, setMenuOpen] = useState(false)
  const status = STATUS_META[stylist.status]

  // Stats placeholder — vendrán del módulo de Citas en el futuro
  const completed = 0
  const cancelled = 0
  const noshow = 0
  const total = completed + cancelled + noshow

  const isInactive = stylist.status === 'Inactive'
  const toggleLabel = isInactive ? 'Reactivar' : 'Desactivar'
  const ToggleIcon = isInactive ? Play : Pause

  return (
    <div
      className={cls(
        'group bg-white rounded-2xl border border-warm-150 shadow-soft hover:shadow-pop hover:-translate-y-[2px] transition-all overflow-hidden flex flex-col',
        isInactive && 'opacity-75',
      )}
    >
      {/* Header */}
      <div className="px-5 pt-5 pb-4 flex items-start gap-4 relative">
        <StylistAvatar name={stylist.fullName} color={stylist.color} size={64} />

        <div className="flex-1 min-w-0">
          <div className="text-[15px] font-medium text-warm-800 leading-tight truncate">
            {stylist.fullName}
          </div>
          <div className="text-[11.5px] text-warm-500 mt-0.5 truncate">{stylist.role}</div>

          <div className="mt-2.5 inline-flex items-center gap-1.5 text-[10.5px] font-medium">
            <span className={cls('w-1.5 h-1.5 rounded-full', status.dot)} />
            <span className={cls('px-1.5 py-0.5 rounded-md', status.pill)}>
              {status.label}
            </span>
          </div>
        </div>

        {/* Menú "..." */}
        <div className="relative">
          <button
            type="button"
            onClick={() => setMenuOpen((o) => !o)}
            className="w-7 h-7 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center transition"
            aria-label="Más opciones"
          >
            <MoreVertical size={16} />
          </button>
          {menuOpen && (
            <>
              <button
                type="button"
                className="fixed inset-0 z-10 cursor-default"
                onClick={() => setMenuOpen(false)}
                aria-label="Cerrar menú"
              />
              <div className="absolute right-0 top-9 z-20 w-44 bg-white rounded-lg border border-warm-200 shadow-pop py-1 anim-fade">
                <MenuItem
                  icon={<Pencil size={13} />}
                  label="Editar"
                  onClick={() => {
                    setMenuOpen(false)
                    onEdit(stylist)
                  }}
                />
                <MenuItem
                  icon={<Palmtree size={13} />}
                  label="Días libres"
                  onClick={() => {
                    setMenuOpen(false)
                    onTimeOff(stylist)
                  }}
                />
                <MenuItem
                  icon={<ToggleIcon size={13} />}
                  label={toggleLabel}
                  onClick={() => {
                    setMenuOpen(false)
                    onToggleStatus(stylist)
                  }}
                />
                <div className="h-px bg-warm-150 my-1" />
                <MenuItem
                  icon={<Trash2 size={13} />}
                  label="Eliminar"
                  danger
                  onClick={() => {
                    setMenuOpen(false)
                    onDelete(stylist)
                  }}
                />
              </div>
            </>
          )}
        </div>
      </div>

      {/* Contact info. Si no hay ningún campo, mostramos un CTA discreto
          en lugar de un hueco vacío que descuadra la grilla. */}
      <div className="px-5 pb-4 space-y-1.5 text-[12px] text-warm-600">
        {stylist.email && (
          <div className="flex items-center gap-2 truncate">
            <Mail size={13} className="text-warm-400 flex-shrink-0" />
            <span className="truncate">{stylist.email}</span>
          </div>
        )}
        {stylist.phone && (
          <div className="flex items-center gap-2">
            <Phone size={13} className="text-warm-400 flex-shrink-0" />
            <span className="tabular-nums">{stylist.phone}</span>
          </div>
        )}
        {stylist.idNumber && (
          <div className="flex items-center gap-2">
            <IdCard size={13} className="text-warm-400 flex-shrink-0" />
            <span className="tabular-nums text-warm-500">CC {stylist.idNumber}</span>
          </div>
        )}
        {stylist.hireDate && (
          <div className="flex items-center gap-2">
            <Calendar size={13} className="text-warm-400 flex-shrink-0" />
            <span className="text-warm-500">Desde {fmtJoinedDate(stylist.hireDate)}</span>
          </div>
        )}
        {!stylist.email && !stylist.phone && !stylist.idNumber && !stylist.hireDate && (
          <button
            type="button"
            onClick={() => onEdit(stylist)}
            className="text-[11.5px] text-warm-400 italic hover:text-brand-700 transition"
          >
            Sin datos de contacto · agregar
          </button>
        )}
      </div>

      {/* Stats footer */}
      <div className="mt-auto border-t border-warm-150 bg-warm-50/40 px-5 py-4">
        <div className="flex items-baseline justify-between mb-3">
          <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium">
            Citas (últimos 90 días)
          </div>
          <div className="text-[10.5px] tabular-nums text-warm-500">
            <span className="text-warm-800 font-semibold">{total}</span> total
          </div>
        </div>

        {/* Stacked bar */}
        <div className="h-1.5 rounded-full bg-warm-200 overflow-hidden flex">
          {total === 0 ? (
            <div style={{ flex: 1 }} className="bg-warm-200" />
          ) : (
            <>
              <div style={{ width: (completed / total) * 100 + '%' }} className="bg-brand-500" />
              <div style={{ width: (cancelled / total) * 100 + '%' }} className="bg-gold-400" />
              <div style={{ flex: 1 }} className="bg-terra-300/60" />
            </>
          )}
        </div>

        <div className="grid grid-cols-3 gap-3 mt-3.5">
          <MiniStat icon={<Check size={11} strokeWidth={3} />} value={completed} label="Hechas" tone="brand" />
          <MiniStat icon={<X size={11} strokeWidth={3} />} value={cancelled} label="Cancel." tone="gold" />
          <MiniStat icon={<Ghost size={11} />} value={noshow} label="No-show" tone="warm" />
        </div>

        {total === 0 && (
          <p className="mt-3 text-[10.5px] text-warm-400 italic">
            Las métricas aparecerán cuando esté el módulo de Agenda.
          </p>
        )}
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Subcomponentes                                                            */
/* -------------------------------------------------------------------------- */

function MenuItem({
  icon,
  label,
  onClick,
  danger,
}: {
  icon: React.ReactNode
  label: string
  onClick: () => void
  danger?: boolean
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        'w-full px-3 py-2 flex items-center gap-2.5 text-[12.5px] hover:bg-warm-50 transition text-left',
        danger ? 'text-terra-500' : 'text-warm-700',
      )}
    >
      <span className={danger ? 'text-terra-500' : 'text-warm-500'}>{icon}</span>
      {label}
    </button>
  )
}

function MiniStat({
  icon,
  value,
  label,
  tone,
}: {
  icon: React.ReactNode
  value: number
  label: string
  tone: 'brand' | 'gold' | 'warm'
}) {
  const tones = {
    brand: 'text-brand-700',
    gold: 'text-gold-600',
    warm: 'text-warm-500',
  }
  return (
    <div className="flex-1 min-w-0">
      <div className="flex items-center gap-1 text-[10px] tracking-[0.06em] uppercase text-warm-500 whitespace-nowrap">
        <span className={tones[tone]}>{icon}</span>
        {label}
      </div>
      <div className="font-serif text-[22px] leading-none tabular-nums text-warm-800 mt-1">
        {value}
      </div>
    </div>
  )
}
