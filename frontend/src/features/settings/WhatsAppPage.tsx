import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, MessageCircle, RefreshCw, AlertCircle } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  SettingsHeader, SaveBar, Toggle, inputCls,
} from './_primitives'
import {
  getWhatsAppTemplates,
  listWhatsAppMessages,
  retryWhatsAppMessage,
  updateWhatsAppTemplate,
  type WhatsAppMessageDto,
  type WhatsAppTemplateDto,
} from '@/api/whatsapp'

/**
 * `/configuracion/whatsapp` — Plantillas de mensajes WhatsApp.
 *
 * Persiste al backend (PUT /api/Admin/whatsapp/templates/{kind}). El
 * dispatcher background corre cada 2min y encola recordatorios para
 * las citas en ventana. Mientras no haya cuenta Twilio/Meta conectada,
 * los mensajes se "envían" via NoOpWhatsAppSender (loguean en backend
 * pero no salen). Se ven igual en el panel "Historial reciente" abajo
 * con status=Sent + externalMessageId=noop-... para validar que el
 * flujo entero funciona end-to-end.
 *
 * Variables soportadas (el renderer del backend las reemplaza al despachar):
 *   {nombre}, {fecha}, {hora}, {servicio}, {salon}, {direccion},
 *   {anticipo}, {limite}
 */

const VARS = [
  '{nombre}', '{fecha}', '{hora}', '{servicio}',
  '{salon}', '{direccion}', '{anticipo}', '{limite}',
] as const

const SAMPLE_VALUES: Record<string, string> = {
  '{nombre}': 'María',
  '{fecha}': 'sáb 7 jun',
  '{hora}': '3:00 pm',
  '{servicio}': 'Balayage',
  '{salon}': 'Bella Spa Neiva',
  '{anticipo}': '$80.000',
  '{direccion}': 'Cra 13 #65-22',
  '{limite}': 'las 12:00 pm',
}

function renderPreview(body: string): string {
  let out = body
  for (const [k, v] of Object.entries(SAMPLE_VALUES)) {
    out = out.split(k).join(v)
  }
  return out
}

// ───────────────────────────────────────────────────────────────────────

interface LocalTemplate extends WhatsAppTemplateDto {}

export function WhatsAppPage() {
  const qc = useQueryClient()
  const { data: serverTemplates, isLoading } = useQuery({
    queryKey: ['whatsapp', 'templates'],
    queryFn: getWhatsAppTemplates,
  })

  // Estado local del form (edición optimista). Snapshot del server para
  // detectar dirty y permitir Discard.
  const [tpls, setTpls] = useState<LocalTemplate[]>([])
  const [openKind, setOpenKind] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  // Hidratar cuando llega el server data.
  useEffect(() => {
    if (serverTemplates) {
      setTpls(serverTemplates)
      // Abrir el primero ON por default — la admin típica lo edita.
      if (!openKind) {
        const firstOn = serverTemplates.find(t => t.isEnabled)
        setOpenKind(firstOn?.kind ?? serverTemplates[0]?.kind ?? null)
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [serverTemplates])

  const isDirty = useMemo(() => {
    if (!serverTemplates) return false
    return JSON.stringify(tpls) !== JSON.stringify(serverTemplates)
  }, [tpls, serverTemplates])

  const saveMut = useMutation({
    // Guardamos en serie los que cambiaron — son 5 endpoints distintos
    // (uno por kind) en vez de un bulk update. Volumen es bajo (5 PUT
    // máx) así que vale la simplicidad sobre la optimización.
    mutationFn: async () => {
      if (!serverTemplates) return
      const changed = tpls.filter(t => {
        const orig = serverTemplates.find(s => s.kind === t.kind)
        if (!orig) return true
        return orig.body !== t.body || orig.isEnabled !== t.isEnabled
      })
      for (const t of changed) {
        await updateWhatsAppTemplate(t.kind, t.body, t.isEnabled)
      }
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['whatsapp', 'templates'] })
      qc.invalidateQueries({ queryKey: ['whatsapp', 'messages'] })
      setSaved(true)
    },
  })

  useEffect(() => {
    if (!saved) return
    const t = setTimeout(() => setSaved(false), 3000)
    return () => clearTimeout(t)
  }, [saved])

  const toggleTpl = (kind: string) => {
    setTpls(t => t.map(x => x.kind === kind ? { ...x, isEnabled: !x.isEnabled } : x))
    setSaved(false)
  }
  const editBody = (kind: string, body: string) => {
    setTpls(t => t.map(x => x.kind === kind ? { ...x, body } : x))
    setSaved(false)
  }
  const appendVar = (kind: string, v: string) => {
    setTpls(t => t.map(x => x.kind === kind ? { ...x, body: x.body + ' ' + v } : x))
    setSaved(false)
  }
  const discard = () => {
    if (serverTemplates) setTpls(serverTemplates)
    setSaved(false)
  }

  if (isLoading) {
    return <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">Cargando…</div>
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Notificaciones WhatsApp"
          desc={'Activa los mensajes automáticos y personaliza su texto. Usa variables como {nombre} o {fecha} y se reemplazan solas al enviar.'}
        />

        {/* Banner informativo sobre el sender stub */}
        <NoSenderBanner />

        <div className="space-y-3 py-2">
          {tpls.map(t => {
            const isOpen = openKind === t.kind
            return (
              <div
                key={t.kind}
                className="rounded-xl border border-warm-150 bg-white overflow-hidden"
              >
                {/* HEADER de la card — toggle + título + chevron */}
                <div className="flex items-center gap-3 px-4 py-3.5">
                  <Toggle on={t.isEnabled} onChange={() => toggleTpl(t.kind)} />
                  <button
                    type="button"
                    onClick={() => setOpenKind(isOpen ? null : t.kind)}
                    className="flex-1 text-left min-w-0"
                  >
                    <div className="text-[13.5px] font-medium text-warm-800">{t.title}</div>
                    <div className="text-[11.5px] text-warm-500">{t.description}</div>
                  </button>
                  <button
                    type="button"
                    onClick={() => setOpenKind(isOpen ? null : t.kind)}
                    className="text-warm-400 hover:text-warm-700"
                    aria-label={isOpen ? 'Cerrar' : 'Abrir'}
                  >
                    <ChevronDown
                      size={16}
                      className={cls('transition', isOpen && 'rotate-180')}
                    />
                  </button>
                </div>

                {/* CUERPO expandido — preview + textarea + variables */}
                {isOpen && (
                  <div className="px-4 pb-4 anim-fade">
                    <div className="rounded-lg bg-[#e7f3ec] border border-[#cce5d6] p-3 mb-3">
                      <div className="text-[10px] tracking-[0.14em] uppercase text-[#3d6453] font-medium mb-1.5">
                        Vista previa
                      </div>
                      <div className="text-[12.5px] text-warm-800 leading-relaxed whitespace-pre-wrap">
                        {renderPreview(t.body)}
                      </div>
                    </div>

                    <textarea
                      value={t.body}
                      onChange={(e) => editBody(t.kind, e.target.value)}
                      rows={3}
                      className={cls(inputCls, 'resize-none text-[13px]')}
                    />

                    <div className="flex flex-wrap gap-1.5 mt-2">
                      {VARS.map(v => (
                        <button
                          key={v}
                          type="button"
                          onClick={() => appendVar(t.kind, v)}
                          className="text-[11px] font-mono px-2 py-0.5 rounded-md bg-warm-100 text-warm-600 hover:bg-brand-50 hover:text-brand-700"
                        >
                          {v}
                        </button>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            )
          })}
        </div>

        {/* Historial reciente: muestra que el dispatcher está armando msjs */}
        <RecentMessages />
      </div>

      <SaveBar
        show={isDirty}
        saved={saved}
        saving={saveMut.isPending}
        error={saveMut.error ? extractApiError(saveMut.error, 'No se pudieron guardar las plantillas.') : null}
        onSave={() => saveMut.mutate()}
        onDiscard={discard}
      />
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Banner explicando que el sender está en modo NoOp
// ───────────────────────────────────────────────────────────────────────

function NoSenderBanner() {
  return (
    <div className="mt-4 mb-6 rounded-xl bg-gold-50 border border-gold-200 px-4 py-3.5 flex items-start gap-3">
      <span className="w-7 h-7 rounded-full bg-gold-100 text-gold-700 flex items-center justify-center flex-shrink-0 mt-0.5">
        <AlertCircle size={14} strokeWidth={1.8} />
      </span>
      <div className="min-w-0">
        <div className="text-[12.5px] font-medium text-gold-800">
          Modo simulado — los mensajes aún no salen al WhatsApp real
        </div>
        <div className="text-[11.5px] text-gold-700 mt-0.5 leading-relaxed">
          La cuenta de WhatsApp Business todavía no está conectada. El sistema
          ya arma y guarda los mensajes en los momentos correctos
          (recordatorios 24h y 2h antes), y los podés ver abajo. Cuando
          conectemos Twilio o Meta, los envíos empezarán automáticamente.
        </div>
      </div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Historial reciente de mensajes
// ───────────────────────────────────────────────────────────────────────

function RecentMessages() {
  const qc = useQueryClient()
  const { data: msgs = [], isLoading } = useQuery({
    queryKey: ['whatsapp', 'messages'],
    queryFn: () => listWhatsAppMessages({ take: 20 }),
    refetchInterval: 30_000,  // refresca cada 30s — el dispatcher corre cada 2min
  })

  const retryMut = useMutation({
    mutationFn: (id: string) => retryWhatsAppMessage(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['whatsapp', 'messages'] }),
  })

  if (isLoading) return null

  return (
    <div className="mt-8">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-[13.5px] font-medium text-warm-700 flex items-center gap-2">
          <MessageCircle size={14} className="text-brand-700" />
          Historial reciente
        </h3>
        <span className="text-[11px] text-warm-400">{msgs.length} últimos</span>
      </div>

      {msgs.length === 0 ? (
        <div className="rounded-xl border border-dashed border-warm-200 px-4 py-8 text-center text-[12.5px] text-warm-500">
          Todavía no se han encolado mensajes. Apenas tengas citas en las
          próximas 24h o 2h, el dispatcher armará los recordatorios.
        </div>
      ) : (
        <ul className="rounded-xl border border-warm-150 bg-white overflow-hidden divide-y divide-warm-100">
          {msgs.map(m => (
            <MessageRow
              key={m.id}
              msg={m}
              onRetry={m.status === 'Failed' ? () => retryMut.mutate(m.id) : undefined}
            />
          ))}
        </ul>
      )}
    </div>
  )
}

function MessageRow({ msg, onRetry }: { msg: WhatsAppMessageDto; onRetry?: () => void }) {
  const status = STATUS_LOOK[msg.status] ?? STATUS_LOOK.Queued
  return (
    <li className="px-4 py-3 flex items-start gap-3">
      <span className={cls('w-2 h-2 rounded-full mt-1.5 flex-shrink-0', status.dot)} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-[12.5px] font-medium text-warm-800">{msg.kind}</span>
          <span className="text-[11px] text-warm-400">·</span>
          <span className="text-[11.5px] text-warm-500 tabular-nums whitespace-nowrap">
            {msg.customerPhone}
          </span>
          <span className={cls('text-[10px] uppercase tracking-wider font-medium px-1.5 py-0.5 rounded', status.badge)}>
            {status.label}
          </span>
        </div>
        <div className="text-[12px] text-warm-600 mt-1 line-clamp-2">
          {msg.renderedBody}
        </div>
        {msg.failureReason && (
          <div className="text-[11px] text-terra-500 mt-1 italic">
            ⚠ {msg.failureReason}
          </div>
        )}
        <div className="text-[10.5px] text-warm-400 mt-1 tabular-nums">
          {formatRelative(msg.queuedAt)}
        </div>
      </div>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="text-[11px] text-brand-700 hover:text-brand-800 flex items-center gap-1 px-2 py-1 rounded hover:bg-brand-50 flex-shrink-0"
        >
          <RefreshCw size={11} /> Reintentar
        </button>
      )}
    </li>
  )
}

const STATUS_LOOK: Record<string, { dot: string; badge: string; label: string }> = {
  Queued:    { dot: 'bg-warm-400',   badge: 'bg-warm-100 text-warm-600',   label: 'En cola' },
  Sent:      { dot: 'bg-brand-500',  badge: 'bg-brand-50 text-brand-700',  label: 'Enviado' },
  Failed:    { dot: 'bg-terra-500',  badge: 'bg-terra-100 text-terra-700', label: 'Falló' },
  Cancelled: { dot: 'bg-warm-300',   badge: 'bg-warm-50 text-warm-400',    label: 'Cancelado' },
}

function formatRelative(iso: string): string {
  const ms = Date.now() - new Date(iso).getTime()
  const min = Math.floor(ms / 60_000)
  if (min < 1) return 'ahora mismo'
  if (min < 60) return `hace ${min} min`
  const h = Math.floor(min / 60)
  if (h < 24) return `hace ${h}h`
  const d = Math.floor(h / 24)
  return `hace ${d}d`
}
