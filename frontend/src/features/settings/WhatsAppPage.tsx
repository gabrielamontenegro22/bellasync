import { useMemo, useState } from 'react'
import { ChevronDown } from 'lucide-react'
import { cls } from '@/lib/cls'
import {
  SettingsHeader, SaveBar, PreviewNotice, Toggle, inputCls,
} from './_primitives'

/**
 * `/configuracion/whatsapp` — plantillas de mensajes automáticos.
 *
 * Por ahora mockup visual sin backend. Cuando hagamos el sprint de
 * integración WhatsApp Business, las plantillas pasan a BD y un
 * BackgroundService se encarga de mandar los recordatorios.
 *
 * Variables soportadas: {nombre}, {fecha}, {hora}, {servicio},
 * {anticipo}, {direccion}, {limite}. La preview los reemplaza con
 * valores de ejemplo en vivo.
 */

interface Template {
  id: string
  title: string
  desc: string
  on: boolean
  body: string
}

const DEFAULT_TEMPLATES: Template[] = [
  {
    id: 'confirm',
    title: 'Confirmación de cita',
    desc: 'Al validar el pago / agendar',
    on: true,
    body: 'Hola {nombre} 💛 Tu cita en Bella Aurora quedó confirmada para el {fecha} a las {hora}. Te esperamos en {direccion}.',
  },
  {
    id: 'remind',
    title: 'Recordatorio 24h antes',
    desc: 'Un día antes de la cita',
    on: true,
    body: 'Hola {nombre}, te recordamos tu cita mañana {fecha} a las {hora} para {servicio}. Responde CONFIRMO para apartar tu cupo.',
  },
  {
    id: 'pending',
    title: 'Anticipo pendiente',
    desc: 'Cuando falta el comprobante',
    on: true,
    body: 'Hola {nombre}, para apartar tu cita de {servicio} envíanos el comprobante del anticipo de {anticipo}. Tienes hasta {limite}.',
  },
  {
    id: 'birthday',
    title: 'Cumpleaños',
    desc: 'El día del cumpleaños de la clienta',
    on: false,
    body: '¡Feliz cumpleaños {nombre}! 🎉 En Bella Aurora queremos consentirte: ven este mes y recibe un 15% en tu servicio favorito.',
  },
]

const VARS = ['{nombre}', '{fecha}', '{hora}', '{servicio}', '{anticipo}', '{direccion}', '{limite}']

const SAMPLE_VALUES: Record<string, string> = {
  '{nombre}': 'María',
  '{fecha}': 'sáb 7 jun',
  '{hora}': '3:00 pm',
  '{servicio}': 'Balayage',
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

export function WhatsAppPage() {
  const [tpls, setTpls] = useState<Template[]>(DEFAULT_TEMPLATES)
  const [openId, setOpenId] = useState<string>('confirm')
  const [saved, setSaved] = useState(false)

  const isDirty = useMemo(
    () => JSON.stringify(tpls) !== JSON.stringify(DEFAULT_TEMPLATES),
    [tpls],
  )

  const toggleTpl = (id: string) => {
    setTpls(t => t.map(x => x.id === id ? { ...x, on: !x.on } : x))
    setSaved(false)
  }
  const editBody = (id: string, body: string) => {
    setTpls(t => t.map(x => x.id === id ? { ...x, body } : x))
    setSaved(false)
  }
  const appendVar = (id: string, v: string) => {
    setTpls(t => t.map(x => x.id === id ? { ...x, body: x.body + ' ' + v } : x))
    setSaved(false)
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <PreviewNotice />
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Notificaciones WhatsApp"
          desc={'Activa los mensajes automáticos y personaliza su texto. Usa variables como {nombre} o {fecha} y se reemplazan solas al enviar.'}
        />

        <div className="space-y-3 py-2">
          {tpls.map(t => {
            const isOpen = openId === t.id
            return (
              <div
                key={t.id}
                className="rounded-xl border border-warm-150 bg-white overflow-hidden"
              >
                {/* HEADER de la card — toggle + título + chevron */}
                <div className="flex items-center gap-3 px-4 py-3.5">
                  <Toggle on={t.on} onChange={() => toggleTpl(t.id)} />
                  <button
                    type="button"
                    onClick={() => setOpenId(isOpen ? '' : t.id)}
                    className="flex-1 text-left min-w-0"
                  >
                    <div className="text-[13.5px] font-medium text-warm-800">{t.title}</div>
                    <div className="text-[11.5px] text-warm-500">{t.desc}</div>
                  </button>
                  <button
                    type="button"
                    onClick={() => setOpenId(isOpen ? '' : t.id)}
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
                      onChange={(e) => editBody(t.id, e.target.value)}
                      rows={3}
                      className={cls(inputCls, 'resize-none text-[13px]')}
                    />

                    <div className="flex flex-wrap gap-1.5 mt-2">
                      {VARS.map(v => (
                        <button
                          key={v}
                          type="button"
                          onClick={() => appendVar(t.id, v)}
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
      </div>

      <SaveBar
        show={isDirty}
        saved={saved}
        onSave={() => setSaved(true)}
        onDiscard={() => { setTpls(DEFAULT_TEMPLATES); setSaved(false) }}
      />
    </div>
  )
}
