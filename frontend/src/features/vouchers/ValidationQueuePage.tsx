import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ArrowLeft, ArrowRight, Bell, Calendar, CheckCircle,
  HelpCircle, MessageCircle, Clock, RefreshCw, RotateCw, ShieldCheck,
  Wallet, X, XCircle, ZoomIn,
} from 'lucide-react'
import { type VoucherResponse, type VoucherUrgency, type VoucherDecision } from '@/api/vouchers'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { extractApiError } from '@/lib/extractApiError'
import {
  initialsOf, toneOf, fmtCop,
} from '@/features/customers/lib/customerLook'
import { usePendingVouchers, useValidateVoucher } from './hooks'

/**
 * Cola de validación de comprobantes — split-panel.
 *
 * Replica el mockup `validation.jsx`:
 *  - Header con back link + título serif + count.
 *  - Banner amber si hay urgentes pendientes.
 *  - Panel izq: filter pills + cards con stripe roja si HOY.
 *  - Panel der: avatar grande + 2 cards (info cita + pago esperado) +
 *    mockup visual de la app del banco (Nequi morado, Bancolombia
 *    amarillo, Daviplata rojo) + notas + 3 ActionBtn grandes.
 *  - Atajos de teclado: C confirmar, A aclaración, R rechazar, ↑↓ navegar.
 *  - Decision banner cuando ya decidiste un voucher.
 */
export function ValidationQueuePage() {
  const navigate = useNavigate()
  const { data: vouchers = [], isLoading, error } = usePendingVouchers()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [filter, setFilter] = useState<VoucherUrgency | 'all'>('all')
  const [bannerDismissed, setBannerDismissed] = useState(false)
  // Decisiones recientes (id → tipo) — para mostrar el DecisionBanner
  // arriba del detalle por algunos segundos antes de que el voucher
  // desaparezca de la cola (la mutation invalida y refetcha).
  const [recentDecisions, setRecentDecisions] = useState<Record<string, VoucherDecision>>({})

  // Auto-seleccionar el primero cuando carga — solo en desktop (≥lg, 1024px).
  // En mobile/iPad el detalle se monta como overlay encima de la lista, así
  // que auto-seleccionar al entrar bloquea el acceso a los otros vouchers
  // (justo lo que el usuario reportó: "solo me aparece esto al entrar").
  // En desktop sí auto-seleccionamos porque el detalle vive al lado de la
  // lista y los atajos de teclado (C/A/R) necesitan algo seleccionado.
  useEffect(() => {
    const isDesktop = typeof window !== 'undefined'
      && window.matchMedia('(min-width: 1024px)').matches

    if (vouchers.length > 0 && !selectedId && isDesktop) {
      setSelectedId(vouchers[0].id)
    }
    if (selectedId && !vouchers.find(v => v.id === selectedId)) {
      // El seleccionado ya no existe (fue decidido). En desktop pasamos al
      // siguiente automáticamente; en mobile lo deseleccionamos (el overlay
      // se cierra y volvés a la lista).
      setSelectedId(isDesktop ? vouchers[0]?.id ?? null : null)
    }
  }, [vouchers, selectedId])

  const counts = useMemo(() => ({
    all: vouchers.length,
    urgent: vouchers.filter(v => v.urgency === 'urgent').length,
    tomorrow: vouchers.filter(v => v.urgency === 'tomorrow').length,
    week: vouchers.filter(v => v.urgency === 'week').length,
  }), [vouchers])

  const filtered = useMemo(
    () => filter === 'all' ? vouchers : vouchers.filter(v => v.urgency === filter),
    [vouchers, filter],
  )

  const selected = vouchers.find(v => v.id === selectedId) ?? null
  const urgentCount = counts.urgent

  const validate = useValidateVoucher()

  async function decide(decision: VoucherDecision, notes?: string) {
    if (!selected) return
    const id = selected.id
    setRecentDecisions(prev => ({ ...prev, [id]: decision }))
    try {
      await validate.mutateAsync({ id, decision, notes })
    } catch (e) {
      // Revertir el banner si falló y mostrar el error en alert.
      setRecentDecisions(prev => {
        const next = { ...prev }
        delete next[id]
        return next
      })
      window.alert(extractApiError(e, 'No se pudo registrar la decisión.'))
    }
  }

  // Atajos de teclado — solo si no estamos escribiendo en un input/textarea
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const tag = (document.activeElement as HTMLElement | null)?.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA') return
      const idx = filtered.findIndex(v => v.id === selectedId)
      if (e.key === 'ArrowDown') {
        e.preventDefault()
        const next = filtered[Math.min(filtered.length - 1, idx + 1)]
        if (next) setSelectedId(next.id)
      } else if (e.key === 'ArrowUp') {
        e.preventDefault()
        const prev = filtered[Math.max(0, idx - 1)]
        if (prev) setSelectedId(prev.id)
      } else if (selected && !recentDecisions[selected.id]) {
        if (e.key.toLowerCase() === 'c') decide('Confirm')
        else if (e.key.toLowerCase() === 'r') decide('Reject')
        else if (e.key.toLowerCase() === 'a') decide('RequestClarification')
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtered, selectedId, selected, recentDecisions])

  return (
    <div className="flex h-full min-h-0 bg-warm-50 overflow-hidden">
      <div className="flex-1 min-w-0 flex flex-col overflow-hidden">
        {/* HEADER */}
        <header className="px-5 lg:px-8 pt-5 lg:pt-7 pb-4 flex-shrink-0">
          <button
            type="button"
            onClick={() => navigate('/agenda')}
            className="flex items-center gap-1 text-[12.5px] text-warm-500 hover:text-brand-700 mb-1"
          >
            <ArrowLeft size={13} /> Volver a la agenda
          </button>
          <div className="flex items-baseline gap-3 flex-wrap">
            <h1 className="font-serif text-[32px] lg:text-[40px] leading-[1.05] text-warm-800 tracking-tight">
              Cola de validación
            </h1>
            <span className="text-[13px] text-warm-500">
              {vouchers.length} {vouchers.length === 1 ? 'comprobante' : 'comprobantes'} esperando revisión
            </span>
          </div>
        </header>

        {/* BANNER URGENTE */}
        {urgentCount > 0 && !bannerDismissed && (
          <div className="mx-5 lg:mx-8 mb-4 rounded-xl border border-gold-200 bg-gold-50 px-4 py-3 flex items-center gap-3 flex-shrink-0">
            <div className="w-8 h-8 rounded-lg bg-white flex items-center justify-center text-gold-600 border border-gold-200 flex-shrink-0">
              <Bell size={16} />
            </div>
            <div className="flex-1 text-[13.5px]">
              <span className="font-semibold text-warm-800">
                Tienes {urgentCount} {urgentCount === 1 ? 'pago urgente' : 'pagos urgentes'}
              </span>
              <span className="text-warm-600"> — {urgentCount === 1 ? 'cita' : 'citas'} en menos de 6 horas. Revísalos primero.</span>
            </div>
            <button
              type="button"
              onClick={() => setBannerDismissed(true)}
              className="p-1 text-warm-400 hover:text-warm-700 rounded shrink-0"
              aria-label="Descartar"
            >
              <X size={15} />
            </button>
          </div>
        )}

        {/* SPLIT PANEL */}
        {/* relative para que el detalle pueda hacer overlay absoluto encima
            en <lg sin escaparse de la página. */}
        <div className="flex-1 min-h-0 flex overflow-hidden relative">
          {/* PANEL IZQ — LISTA. En mobile/iPad ocupa el ancho completo
              (max-w-none) y el detalle aparece como overlay encima.
              En desktop (≥lg) la lista queda a 420px y el detalle al lado. */}
          <aside className="w-full lg:max-w-[420px] flex-shrink-0 border-r border-warm-150 bg-warm-50/60 flex flex-col min-h-0">
            {/* Filter pills — flex-wrap para que "Esta semana" caiga abajo
                en vez de cortarse, y gap-2 para más aire entre chips */}
            <div className="px-5 lg:px-6 pt-2 pb-3 flex items-center gap-2 flex-wrap flex-shrink-0">
              {(['all', 'urgent', 'tomorrow', 'week'] as const).map(f => (
                <FilterPill
                  key={f}
                  id={f}
                  active={filter === f}
                  count={counts[f]}
                  onClick={() => setFilter(f)}
                />
              ))}
            </div>

            {/* Cards */}
            <div className="flex-1 overflow-y-auto px-5 lg:px-6 pb-6 space-y-2">
              {isLoading && (
                <div className="text-center py-10 text-[13px] text-warm-500">
                  Cargando comprobantes…
                </div>
              )}
              {error && (
                <div className="text-center py-10 text-[13px] text-terra-500">
                  No se pudo cargar la cola.
                </div>
              )}
              {!isLoading && filtered.length === 0 && (
                <div className="text-center py-10 text-[13px] text-warm-500">
                  Sin resultados para este filtro.
                </div>
              )}
              {filtered.map(v => (
                <VoucherCard
                  key={v.id}
                  voucher={v}
                  selected={selectedId === v.id}
                  decision={recentDecisions[v.id]}
                  onClick={() => setSelectedId(v.id)}
                />
              ))}
            </div>
          </aside>

          {/* PANEL DER — DETALLE.
              - Desktop (≥lg): flujo normal al lado de la lista
              - Mobile/iPad (<lg): overlay absoluto encima de la lista
                cuando hay voucher seleccionado. Sin selección, no se
                monta en mobile (la lista ocupa todo).
              El empty state solo se muestra en desktop porque en mobile
              el detalle se abre on-tap. */}
          {selected ? (
            <>
              {/* Backdrop para cerrar al tocar fuera en mobile */}
              <div
                onClick={() => setSelectedId(null)}
                className="lg:hidden absolute inset-0 bg-warm-900/30 z-20"
                aria-hidden="true"
              />
              <main
                className={cls(
                  'flex flex-col overflow-hidden bg-warm-50 animate-slide',
                  'absolute inset-y-0 right-0 z-30 w-full sm:w-[460px] shadow-panel',
                  'lg:static lg:flex-1 lg:min-w-0 lg:z-auto lg:shadow-none lg:w-auto',
                )}
              >
                <Detail
                  voucher={selected}
                  decision={recentDecisions[selected.id]}
                  onDecide={decide}
                  isPending={validate.isPending}
                  onClose={() => setSelectedId(null)}
                />
              </main>
            </>
          ) : (
            <main className="hidden lg:flex flex-1 min-w-0 flex-col overflow-hidden bg-warm-50">
              <div className="flex-1 flex items-center justify-center">
                <EmptyState />
              </div>
            </main>
          )}
        </div>
      </div>
    </div>
  )
}

// ============================================================
// FilterPill
// ============================================================
function FilterPill({
  id, active, count, onClick,
}: { id: VoucherUrgency | 'all'; active: boolean; count: number; onClick: () => void }) {
  const cfg = FILTER_CFG[id]
  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        // gap-2 más generoso entre dot/label/count para que respire
        'flex items-center gap-2 px-3 py-1.5 rounded-full text-[12.5px] border transition',
        active
          ? 'bg-warm-800 text-warm-50 border-warm-800'
          : 'bg-white text-warm-700 border-warm-200 hover:border-warm-300',
      )}
    >
      {cfg.dot && <span className={cls('w-1.5 h-1.5 rounded-full', cfg.dot)} />}
      <span>{cfg.label}</span>
      <span className={cls(
        'tabular-nums text-[11px]',
        active ? 'text-warm-50/70' : 'text-warm-400',
      )}>
        {count}
      </span>
    </button>
  )
}

const FILTER_CFG: Record<VoucherUrgency | 'all', { label: string; dot: string | null }> = {
  all:      { label: 'Todos',       dot: null },
  urgent:   { label: 'Urgentes',    dot: 'bg-terra-500' },
  tomorrow: { label: 'Mañana',      dot: 'bg-gold-400' },
  week:     { label: 'Esta semana', dot: 'bg-brand-500' },
}

// ============================================================
// URGENCY LOOK
// ============================================================
const URGENCY_LOOK: Record<VoucherUrgency, { label: string; dot: string; pill: string; txt: string }> = {
  urgent:   { label: 'Urgente · Cita HOY', dot: 'bg-terra-500', pill: 'bg-terra-100 text-terra-500 border-terra-300', txt: 'text-terra-500' },
  tomorrow: { label: 'Mañana',             dot: 'bg-gold-400',  pill: 'bg-gold-100 text-gold-600 border-gold-200',    txt: 'text-gold-600' },
  week:     { label: 'Esta semana',        dot: 'bg-brand-500', pill: 'bg-brand-50 text-brand-700 border-brand-100',  txt: 'text-brand-700' },
}

// ============================================================
// VoucherCard (lista izquierda)
// ============================================================
function VoucherCard({
  voucher, selected, decision, onClick,
}: { voucher: VoucherResponse; selected: boolean; decision?: VoucherDecision; onClick: () => void }) {
  const u = URGENCY_LOOK[voucher.urgency]
  const tone = toneOf(voucher.id)
  const decided = !!decision

  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        'w-full text-left rounded-xl border bg-white overflow-hidden transition',
        'hover:border-warm-300 hover:shadow-softer',
        selected ? 'border-brand-500 bg-brand-50/40 ring-1 ring-brand-200' : 'border-warm-150',
        decided && 'opacity-60',
      )}
    >
      {/* Stripe roja para urgent — visualmente impactante para que la
          recepcionista no se le pase */}
      {voucher.urgency === 'urgent' && (
        <div className="bg-terra-100 text-terra-500 px-3 py-1 text-[11px] font-semibold flex items-center gap-1.5">
          <span className="w-1.5 h-1.5 rounded-full bg-terra-500 animate-pulse" />
          Cita HOY {formatTime(voucher.appointmentStartAt)}
        </div>
      )}

      <div className="px-3.5 py-3 flex items-start gap-3">
        <span className={cls('mt-1 w-2 h-2 rounded-full flex-shrink-0', u.dot)} />
        <div className={cls(
          'w-10 h-10 rounded-full flex items-center justify-center font-semibold text-[12.5px] flex-shrink-0',
          tone.bg, tone.fg,
        )}>
          {initialsOf(voucher.customerName)}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-baseline justify-between gap-2">
            <div className="text-[14px] font-semibold text-warm-800 truncate flex items-center gap-1">
              {decision === 'Confirm' && <CheckCircle size={13} className="text-brand-700 -mt-0.5" />}
              {decision === 'Reject' && <XCircle size={13} className="text-terra-500 -mt-0.5" />}
              {decision === 'RequestClarification' && <HelpCircle size={13} className="text-gold-600 -mt-0.5" />}
              {voucher.customerName}
            </div>
            <div className="text-[13px] font-semibold text-warm-800 tabular-nums shrink-0">
              {fmtCop(voucher.reportedAmount)}
            </div>
          </div>
          <div className="text-[12px] text-warm-500 truncate">{voucher.serviceName}</div>
          <div className="mt-1.5 flex items-center justify-between gap-2 text-[11.5px]">
            <span className={cls('font-medium tabular-nums', u.txt)}>
              {formatRelativeAppointment(voucher.appointmentStartAt, voucher.urgency)}
            </span>
            <span className="text-warm-400">{formatReceived(voucher.receivedAt)}</span>
          </div>
        </div>
      </div>
    </button>
  )
}

// ============================================================
// Detail (panel derecho)
// ============================================================
function Detail({
  voucher, decision, onDecide, isPending, onClose,
}: {
  voucher: VoucherResponse
  decision?: VoucherDecision
  onDecide: (d: VoucherDecision, notes?: string) => void
  isPending: boolean
  /** Cerrar el overlay — solo se usa en mobile/iPad donde Detail es overlay. */
  onClose?: () => void
}) {
  const { user } = useAuth()
  const u = URGENCY_LOOK[voucher.urgency]
  const tone = toneOf(voucher.id)
  const [zoom, setZoom] = useState(1)
  const [rot, setRot] = useState(0)
  const [notes, setNotes] = useState('')

  // Reset visual al cambiar voucher
  useEffect(() => {
    setZoom(1); setRot(0); setNotes('')
  }, [voucher.id])

  const depositPercentage = voucher.appointmentTotalServicePrice > 0
    ? Math.round((voucher.appointmentDepositAmount / voucher.appointmentTotalServicePrice) * 100)
    : 0

  return (
    <div className="flex-1 min-w-0 flex flex-col overflow-hidden">
      {/* HEADER STICKY */}
      <div className="px-5 lg:px-8 pt-5 pb-4 border-b border-warm-150 bg-white flex-shrink-0">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3.5 min-w-0">
            <div className={cls(
              'w-12 h-12 rounded-full flex items-center justify-center font-semibold text-[14px] flex-shrink-0',
              tone.bg, tone.fg,
            )}>
              {initialsOf(voucher.customerName)}
            </div>
            <div className="min-w-0">
              <h2 className="font-serif text-[24px] leading-tight text-warm-800 tracking-tight truncate">
                {voucher.customerName}
              </h2>
              <div className="flex items-center gap-2 mt-1 flex-wrap">
                <span className={cls(
                  'text-[10.5px] font-semibold px-2 py-0.5 rounded-md uppercase tracking-wider flex items-center gap-1.5 border',
                  u.pill,
                )}>
                  <span className={cls('w-1.5 h-1.5 rounded-full', u.dot)} />
                  {u.label}
                </span>
                {/* whitespace-nowrap previene que un número como "+57 318 552 3344"
                    quiebre por columna cuando el panel está angosto (iPad
                    overlay), donde antes se veía un dígito por línea. */}
                <span className="text-[12px] text-warm-500 tabular-nums whitespace-nowrap">{voucher.customerPhone}</span>
              </div>
            </div>
          </div>
          {/* Cerrar (mobile/iPad): el panel es overlay, hace falta una salida
              visible. En desktop se oculta porque al cerrar perdés el contexto
              de qué estabas revisando y los atajos de teclado son más rápidos. */}
          {onClose && (
            <button
              type="button"
              onClick={onClose}
              className="lg:hidden p-1.5 -mr-1.5 text-warm-500 hover:text-warm-800 hover:bg-warm-100 rounded-md flex-shrink-0"
              aria-label="Cerrar detalle"
            >
              <X size={18} strokeWidth={1.8} />
            </button>
          )}
          {/* Atajos teclado — desktop only (>=lg). En mobile/tablet no hay teclado. */}
          <div className="hidden lg:flex items-center gap-2 text-[10.5px] text-warm-400">
            <KbdHint k="C" label="Confirmar" />
            <KbdHint k="A" label="Aclaración" />
            <KbdHint k="R" label="Rechazar" />
            <KbdHint k="↑↓" label="Navegar" />
          </div>
        </div>
      </div>

      {/* BODY scrollable */}
      <div className="flex-1 overflow-y-auto px-5 lg:px-8 py-5">
        {decision && <DecisionBanner decision={decision} />}

        <div className="grid lg:grid-cols-2 gap-4">
          {/* Card 1: Información cita */}
          <SectionCard title="Información de la cita" icon={Calendar}>
            <DLRow k="Servicio" v={voucher.serviceName} />
            <DLRow k="Estilista" v={voucher.stylistName} />
            <DLRow k="Fecha" v={formatLongDateTime(voucher.appointmentStartAt)} />
            <DLRow k="Estado" v={
              <span className="text-[11.5px] font-semibold px-1.5 py-0.5 rounded bg-gold-100 text-gold-600 uppercase tracking-wider">
                Pendiente de pago
              </span>
            } />
          </SectionCard>

          {/* Card 2: Pago esperado */}
          <SectionCard title="Pago esperado" icon={Wallet} accent>
            <div className="flex items-baseline justify-between mb-2">
              <span className="text-[11.5px] text-warm-500 uppercase tracking-wider">Monto a verificar</span>
              <span className="text-[10.5px] text-warm-400">
                {depositPercentage}% anticipo de {fmtCop(voucher.appointmentTotalServicePrice)}
              </span>
            </div>
            <div className="font-serif text-[32px] text-warm-800 tabular-nums leading-tight">
              {fmtCop(voucher.appointmentDepositAmount)}
            </div>
            <div className="mt-3 pt-3 border-t border-warm-150 space-y-1.5">
              <DLRow k="Reportado" v={fmtCop(voucher.reportedAmount)} />
              {voucher.bank && <DLRow k="Banco origen" v={voucher.bank} />}
              {voucher.referenceNumber && (
                <DLRow k="Referencia" v={
                  <span className="font-mono text-[11.5px]">{voucher.referenceNumber}</span>
                } />
              )}
            </div>
          </SectionCard>
        </div>

        {/* Comprobante */}
        <div className="mt-5">
          <div className="flex items-center justify-between mb-3">
            <div className="text-[10.5px] uppercase tracking-[0.14em] font-medium text-warm-400 flex items-center gap-2">
              <MessageCircle size={13} /> Comprobante recibido
            </div>
            <div className="flex items-center gap-1.5">
              <IconBtn onClick={() => setZoom(z => Math.min(2, z + 0.25))} title="Acercar">
                <ZoomIn size={14} />
              </IconBtn>
              <IconBtn onClick={() => setRot(r => r + 90)} title="Rotar">
                <RotateCw size={14} />
              </IconBtn>
              <IconBtn onClick={() => { setZoom(1); setRot(0) }} title="Restablecer">
                <RefreshCw size={14} />
              </IconBtn>
            </div>
          </div>

          <div className="rounded-xl border border-warm-200 bg-warm-50/60 p-6 lg:p-8 overflow-hidden">
            <div
              className="origin-center transition-transform duration-200 flex justify-center"
              style={{ transform: `scale(${zoom}) rotate(${rot}deg)` }}
            >
              {voucher.imageUrl
                ? (
                  <img
                    src={voucher.imageUrl}
                    alt="Comprobante"
                    className="max-w-[360px] rounded-xl border border-warm-200 shadow-soft"
                  />
                )
                : <ReceiptMock voucher={voucher} tenantName={user?.tenantName ?? 'Salón'} />}
            </div>
          </div>

          <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-[12px] text-warm-500">
            <span className="flex items-center gap-1.5">
              <MessageCircle size={12} className="text-brand-600" />
              Recibido por WhatsApp desde {voucher.customerPhone} ({voucher.customerName})
            </span>
            <span className="flex items-center gap-1.5">
              <Clock size={12} /> {formatReceived(voucher.receivedAt)}
            </span>
          </div>
        </div>

        {/* Notas internas */}
        <div className="mt-6">
          <label className="block text-[10.5px] uppercase tracking-[0.14em] font-medium text-warm-400 mb-2">
            Notas internas
          </label>
          <textarea
            value={notes}
            onChange={e => setNotes(e.target.value)}
            placeholder="Ej: cliente confirmó por audio que pagó. Se le envió recordatorio."
            className="w-full min-h-[72px] resize-none rounded-lg border border-warm-200 bg-white px-3 py-2.5 text-[13px] text-warm-800 placeholder:text-warm-400 focus:outline-none focus:ring-2 focus:ring-brand-200 focus:border-brand-400"
            disabled={!!decision}
          />
        </div>

        <div className="h-6" />
      </div>

      {/* Footer sticky con 3 acciones */}
      <div className="border-t border-warm-150 bg-white px-5 lg:px-8 py-4 flex-shrink-0">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <ActionBtn
            primary
            disabled={!!decision || isPending}
            onClick={() => onDecide('Confirm', notes || undefined)}
            kbd="C"
            icon={CheckCircle}
            label="Confirmar pago"
            hint="Marca la cita como pagada y notifica a la cliente."
          />
          <ActionBtn
            tone="gold"
            disabled={!!decision || isPending}
            onClick={() => onDecide('RequestClarification', notes || undefined)}
            kbd="A"
            icon={HelpCircle}
            label="Pedir aclaración"
            hint="La cita sigue reservada. Pide al cliente otro comprobante."
          />
          <ActionBtn
            tone="terra"
            disabled={!!decision || isPending}
            onClick={() => onDecide('Reject', notes || undefined)}
            kbd="R"
            icon={XCircle}
            label="Rechazar"
            hint="Anula el voucher y CANCELA la cita. Libera el cupo."
          />
        </div>
      </div>
    </div>
  )
}

// ============================================================
// ReceiptMock — fake screenshot de la app del banco
// ============================================================
// Cuando no hay imagen real (la mayoría de casos hoy), generamos un
// mockup visual creíble basado en los datos del voucher. Replica el
// estilo de Nequi/Bancolombia/Daviplata para que la recepcionista
// tenga el contexto visual aunque no haya foto.
function ReceiptMock({ voucher, tenantName }: { voucher: VoucherResponse; tenantName: string }) {
  const bank = (voucher.bank ?? '').toLowerCase()
  const isNequi = bank.includes('nequi')
  const isDaviplata = bank.includes('daviplata')
  const isDavivienda = bank.includes('davivienda')
  const isBancolombia = bank.includes('bancolombia')

  const accent =
    isNequi ? '#da1e8e' :
    isDaviplata ? '#e2231a' :
    isDavivienda ? '#e2231a' :
    isBancolombia ? '#fdda24' :
    '#0f766e'  // brand default
  const headerBg =
    isNequi ? '#1a1334' :
    isDaviplata ? '#0a1a3b' :
    isDavivienda ? '#e2231a' :
    isBancolombia ? '#fdda24' :
    '#0f766e'
  const headerTxt = isBancolombia ? '#1a1814' : '#ffffff'

  // "Cuenta destino" simulada — última parte del referenceNumber
  // sirve como "•••• 4521" si no hay info real. Si tenemos
  // referenceNumber, usamos sus últimos 4 dígitos. Sino genérico.
  const lastFour = voucher.referenceNumber?.slice(-4) ?? '4521'

  return (
    <div className="rounded-xl overflow-hidden border border-warm-200 bg-white max-w-[360px] w-full shadow-soft">
      {/* phone status bar pretend */}
      <div className="bg-warm-100 px-4 py-1.5 flex items-center justify-between text-[10.5px] text-warm-500 tabular-nums">
        <span>{formatTime(voucher.receivedAt)}</span>
        <span>{voucher.bank ?? 'App'}</span>
        <span>●●●● 84%</span>
      </div>
      <div
        style={{ backgroundColor: headerBg, color: headerTxt }}
        className="px-4 py-3.5 flex items-center justify-between"
      >
        <div className="flex items-center gap-2">
          <div
            className="w-6 h-6 rounded-full bg-white/90 flex items-center justify-center"
            style={{ color: accent }}
          >
            <CheckCircle size={14} />
          </div>
          <div className="text-[13.5px] font-semibold tracking-tight">Transferencia exitosa</div>
        </div>
        <X size={16} />
      </div>
      <div className="px-5 pt-5 pb-4">
        <div className="text-center">
          <div className="text-[10.5px] text-warm-500 uppercase tracking-wider">Monto transferido</div>
          <div className="font-serif text-[34px] text-warm-800 leading-tight tabular-nums mt-1">
            {fmtCop(voucher.reportedAmount)}
          </div>
          <div className="text-[10.5px] text-warm-400 mt-0.5">Comisión $0 · COP</div>
        </div>

        <div className="mt-5 border-t border-dashed border-warm-200 pt-4 space-y-2.5 text-[12px]">
          <RowKV k="Para" v={tenantName} />
          <RowKV k="Cuenta destino" v={`${voucher.bank ?? 'Cuenta'} •••• ${lastFour}`} />
          <RowKV k="Desde" v={voucher.senderName ?? voucher.customerName} />
          <RowKV k="Concepto" v={`Anticipo ${voucher.serviceName}`} />
          <RowKV k="Fecha" v={formatLongDateTime(voucher.receivedAt)} />
          {voucher.referenceNumber && (
            <RowKV k="Referencia" v={voucher.referenceNumber} mono />
          )}
        </div>

        <div
          className="mt-5 rounded-lg p-2.5 text-center text-[11px]"
          style={{ backgroundColor: `${accent}1a`, color: '#46423a' }}
        >
          Comparte tu comprobante
        </div>
      </div>
    </div>
  )
}

function RowKV({ k, v, mono }: { k: string; v: string; mono?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <span className="text-warm-500">{k}</span>
      <span className={cls(
        'text-warm-800 font-medium text-right truncate',
        mono && 'font-mono text-[11.5px]',
      )}>
        {v}
      </span>
    </div>
  )
}

// ============================================================
// DecisionBanner / Cards / Buttons
// ============================================================
function DecisionBanner({ decision }: { decision: VoucherDecision }) {
  const map = {
    Confirm: { c: 'bg-brand-50 border-brand-200 text-brand-800', I: CheckCircle, t: 'Pago confirmado', d: 'La cita quedó marcada como pagada. La cliente recibirá la confirmación.' },
    RequestClarification: { c: 'bg-gold-50 border-gold-200 text-gold-700', I: HelpCircle, t: 'Aclaración solicitada', d: 'La cita sigue reservada. Cliente puede enviar otro comprobante.' },
    Reject: { c: 'bg-terra-100 border-terra-300 text-terra-500', I: XCircle, t: 'Comprobante rechazado · cita cancelada', d: 'El cupo se liberó. Avisa a la cliente que tendrá que volver a agendar.' },
  } as const
  const m = map[decision]
  return (
    <div className={cls('rounded-xl border px-4 py-3 mb-5 flex items-start gap-3', m.c)}>
      <m.I size={18} className="mt-0.5 flex-shrink-0" />
      <div>
        <div className="text-[13.5px] font-semibold">{m.t}</div>
        <div className="text-[12.5px] opacity-80">{m.d}</div>
      </div>
    </div>
  )
}

function SectionCard({
  title, icon: I, children, accent,
}: {
  title: string
  icon: React.ComponentType<{ size?: number; className?: string }>
  children: React.ReactNode
  accent?: boolean
}) {
  return (
    <div className={cls(
      'rounded-xl border p-4',
      accent ? 'border-brand-100 bg-brand-50/40' : 'border-warm-150 bg-white',
    )}>
      <div className="flex items-center gap-2 mb-3">
        <I size={14} className={accent ? 'text-brand-700' : 'text-warm-500'} />
        <div className={cls(
          'text-[10.5px] uppercase tracking-[0.14em] font-medium',
          accent ? 'text-brand-700' : 'text-warm-400',
        )}>
          {title}
        </div>
      </div>
      <div className="space-y-1.5">{children}</div>
    </div>
  )
}

function DLRow({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3 text-[13px]">
      <span className="text-warm-500 flex-shrink-0">{k}</span>
      <span className="text-warm-800 font-medium text-right min-w-0">{v}</span>
    </div>
  )
}

function IconBtn({
  children, onClick, title,
}: { children: React.ReactNode; onClick: () => void; title: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={title}
      className="p-1.5 rounded-md bg-white border border-warm-200 text-warm-600 hover:text-warm-800 hover:border-warm-300"
    >
      {children}
    </button>
  )
}

function KbdHint({ k, label }: { k: string; label: string }) {
  return (
    <span className="flex items-center gap-1">
      <kbd className="border border-warm-200 bg-white text-warm-600 rounded px-1.5 py-0.5 text-[10px] font-medium">
        {k}
      </kbd>
      <span>{label}</span>
    </span>
  )
}

function ActionBtn({
  icon: I, label, hint, onClick, primary, tone, kbd, disabled,
}: {
  icon: React.ComponentType<{ size?: number }>
  label: string
  hint: string
  onClick: () => void
  primary?: boolean
  tone?: 'gold' | 'terra'
  kbd: string
  disabled?: boolean
}) {
  const styles =
    primary    ? 'bg-brand-700 hover:bg-brand-800 text-white border-brand-800' :
    tone === 'gold'  ? 'bg-gold-50 hover:bg-gold-100 text-gold-600 border-gold-200' :
                       'bg-terra-100 hover:bg-terra-100/70 text-terra-500 border-terra-300'
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={cls(
        'group rounded-xl border px-4 py-3 text-left transition-all',
        styles,
        disabled ? 'opacity-40 cursor-not-allowed' : 'hover:shadow-pop hover:-translate-y-[1px]',
      )}
    >
      <div className="flex items-center justify-between mb-1">
        <div className="flex items-center gap-2">
          <I size={17} />
          <span className="text-[14.5px] font-semibold">{label}</span>
        </div>
        <kbd className={cls(
          'text-[10.5px] font-mono px-1.5 py-0.5 rounded',
          primary ? 'bg-white/15 text-white/90' : 'bg-white/70 text-current',
        )}>
          {kbd}
        </kbd>
      </div>
      <div className={cls('text-[11.5px] leading-snug', primary ? 'text-white/80' : 'opacity-80')}>
        {hint}
      </div>
    </button>
  )
}

function EmptyState() {
  return (
    <div className="text-center py-16 max-w-[360px] px-6">
      <div className="mx-auto w-20 h-20 rounded-full bg-brand-50 border border-brand-100 flex items-center justify-center text-brand-700 mb-5">
        <ShieldCheck size={36} strokeWidth={1.5} />
      </div>
      <div className="font-serif text-[26px] text-warm-800 leading-tight">¡Estás al día!</div>
      <div className="text-[13.5px] text-warm-500 mt-2">
        No hay comprobantes pendientes de validar. Cuando lleguen nuevos pagos por WhatsApp, aparecerán aquí.
      </div>
      <a
        href="/agenda"
        className="inline-flex items-center gap-1.5 mt-5 text-[13px] font-medium text-brand-700 hover:text-brand-800"
      >
        <ArrowLeft size={14} /> Volver a la agenda <ArrowRight size={14} className="opacity-0" />
      </a>
    </div>
  )
}

// ============================================================
// Formato fechas
// ============================================================
function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false })
}

function formatLongDateTime(iso: string): string {
  const d = new Date(iso)
  const MESES = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre']
  const DIAS = ['domingo','lunes','martes','miércoles','jueves','viernes','sábado']
  const h = d.getHours()
  const m = d.getMinutes()
  const ampm = h >= 12 ? 'pm' : 'am'
  const hh = ((h + 11) % 12) + 1
  return `${DIAS[d.getDay()]} ${d.getDate()} ${MESES[d.getMonth()]} · ${hh}:${String(m).padStart(2, '0')} ${ampm}`
}

function formatRelativeAppointment(iso: string, urgency: VoucherUrgency): string {
  const d = new Date(iso)
  const time = formatTime(iso)
  if (urgency === 'urgent') return `Hoy · ${time}`
  if (urgency === 'tomorrow') return `Mañana · ${time}`
  const MESES = ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic']
  return `${d.getDate()} ${MESES[d.getMonth()]} · ${time}`
}

function formatReceived(iso: string): string {
  const diffMin = Math.round((Date.now() - new Date(iso).getTime()) / 60000)
  if (diffMin < 1) return 'recién'
  if (diffMin < 60) return `Hace ${diffMin} min`
  const h = Math.floor(diffMin / 60)
  const m = diffMin % 60
  if (h < 24) return m === 0 ? `Hace ${h} h` : `Hace ${h} h ${m} min`
  const days = Math.floor(h / 24)
  return `Hace ${days} ${days === 1 ? 'día' : 'días'}`
}
