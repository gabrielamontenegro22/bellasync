import { Mail, Search, Eye, ArrowRight, Sparkles } from 'lucide-react'
import { Button, Input, Card, CardTitle, Badge } from '@/components/ui'

/**
 * Pantalla showcase del Bloque F2.
 *
 * Renderiza todos los componentes base + tokens de la paleta para que vos
 * (Gabriela) puedas validar visualmente que cada token coincide con los
 * mockups de Claude Design.
 *
 * Esta pantalla SE REEMPLAZA en el F3 por el AppRouter real.
 */
function App() {
  return (
    <div className="min-h-screen px-6 py-10 lg:px-12">
      <div className="max-w-5xl mx-auto space-y-10">

        {/* ---------- Header ---------- */}
        <header>
          <div className="flex items-center gap-3 mb-2">
            <div className="w-9 h-9 rounded-lg bg-brand-700 text-white flex items-center justify-center font-serif text-[20px] leading-none translate-y-[1px]">
              B
            </div>
            <span className="font-serif text-[28px] tracking-tight text-warm-800 leading-none">
              BellaSync
            </span>
          </div>
          <p className="text-[12.5px] uppercase tracking-[0.18em] text-gold-600 font-medium">
            Bloque F2 · Design System
          </p>
          <h1 className="font-serif text-[44px] text-warm-800 leading-tight mt-2">
            Showcase de componentes
          </h1>
          <p className="text-[14px] text-warm-600 mt-2 max-w-xl">
            Verificá que cada componente y cada color coincide con los mockups antes de pasar al F3.
          </p>
        </header>

        {/* ---------- Botones ---------- */}
        <Section title="Botones" subtitle="4 variantes · 3 tamaños · estados loading y disabled">
          <Card>
            <div className="flex flex-wrap items-center gap-3 mb-5">
              <Button variant="primary">Primary</Button>
              <Button variant="secondary">Secondary</Button>
              <Button variant="ghost">Ghost</Button>
              <Button variant="danger">Danger</Button>
            </div>
            <div className="flex flex-wrap items-center gap-3 mb-5">
              <Button size="sm">Small</Button>
              <Button size="md">Medium</Button>
              <Button size="lg">Large</Button>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <Button leftIcon={<Sparkles size={16} />}>Con icono</Button>
              <Button rightIcon={<ArrowRight size={16} />} variant="secondary">Continuar</Button>
              <Button loading>Cargando…</Button>
              <Button disabled>Disabled</Button>
            </div>
          </Card>
        </Section>

        {/* ---------- Inputs ---------- */}
        <Section title="Inputs" subtitle="Label, hint, error, iconos, disabled">
          <Card>
            <div className="grid md:grid-cols-2 gap-5">
              <Input
                label="Nombre del salón"
                placeholder="Ej. Bella Aurora"
                hint="Este nombre se mostrará a tus clientes."
              />
              <Input
                label="Correo electrónico"
                type="email"
                placeholder="tu@correo.com"
                leftIcon={<Mail size={16} />}
              />
              <Input
                label="Buscar"
                placeholder="Buscar cliente…"
                leftIcon={<Search size={16} />}
                rightIcon={<kbd className="text-[10.5px] text-warm-400 border border-warm-200 px-1.5 py-0.5 rounded font-sans">⌘K</kbd>}
              />
              <Input
                label="Contraseña"
                type="password"
                placeholder="••••••••"
                error="La contraseña debe tener al menos 8 caracteres."
              />
              <Input
                label="Teléfono"
                placeholder="300 123 4567"
                disabled
                value="—"
              />
              <Input
                label="Visible"
                placeholder="Sin label visible aparte"
                rightIcon={<Eye size={16} />}
              />
            </div>
          </Card>
        </Section>

        {/* ---------- Cards ---------- */}
        <Section title="Cards" subtitle="3 variantes de elevación">
          <div className="grid md:grid-cols-3 gap-4">
            <Card variant="default">
              <CardTitle>Default</CardTitle>
              <p className="text-[13.5px] text-warm-700">
                Fondo blanco, borde <code className="font-mono text-[12px] bg-warm-100 px-1 rounded">warm-150</code>, sombra <code className="font-mono text-[12px] bg-warm-100 px-1 rounded">softer</code>.
              </p>
            </Card>
            <Card variant="elevated">
              <CardTitle>Elevated</CardTitle>
              <p className="text-[13.5px] text-warm-700">
                Sin borde, con sombra <code className="font-mono text-[12px] bg-warm-100 px-1 rounded">soft</code>. Usado para cards principales.
              </p>
            </Card>
            <Card variant="flat">
              <CardTitle>Flat</CardTitle>
              <p className="text-[13.5px] text-warm-700">
                Sin fondo ni sombra. Solo organiza contenido.
              </p>
            </Card>
          </div>
        </Section>

        {/* ---------- Badges ---------- */}
        <Section title="Badges" subtitle="4 tonos · con o sin punto · estilo tag mayúsculas">
          <Card>
            <div className="flex flex-wrap items-center gap-3 mb-5">
              <Badge tone="brand">Confirmada</Badge>
              <Badge tone="gold">Pendiente</Badge>
              <Badge tone="terra">No-show</Badge>
              <Badge tone="neutral">Reagendada</Badge>
            </div>
            <div className="flex flex-wrap items-center gap-3 mb-5">
              <Badge tone="brand" withDot>Confirmada</Badge>
              <Badge tone="gold" withDot>Pendiente</Badge>
              <Badge tone="terra" withDot>No-show</Badge>
              <Badge tone="neutral" withDot>Reagendada</Badge>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <Badge tone="brand" uppercase={false}>Sin uppercase</Badge>
              <Badge tone="gold" uppercase={false}>Modo natural</Badge>
            </div>
          </Card>
        </Section>

        {/* ---------- Tipografía ---------- */}
        <Section title="Tipografía" subtitle="3 familias cargadas desde Google Fonts">
          <Card>
            <div className="space-y-4">
              <div>
                <p className="text-[10.5px] uppercase tracking-[0.14em] text-warm-400 mb-1">Inter — sans (UI)</p>
                <p className="font-sans text-[16px] text-warm-800">The quick brown fox jumps over the lazy dog · 0123456789</p>
              </div>
              <div>
                <p className="text-[10.5px] uppercase tracking-[0.14em] text-warm-400 mb-1">Cormorant Garamond — serif (editorial)</p>
                <p className="font-serif text-[28px] text-warm-800 leading-tight">The quick brown fox jumps over the lazy dog</p>
              </div>
              <div>
                <p className="text-[10.5px] uppercase tracking-[0.14em] text-warm-400 mb-1">JetBrains Mono — mono (datos / kbd)</p>
                <p className="font-mono text-[13.5px] text-warm-800">const total = 185_000; // COP</p>
              </div>
            </div>
          </Card>
        </Section>

        {/* ---------- Paleta de colores ---------- */}
        <Section title="Paleta" subtitle="Cada hex coincide con el tailwind.config inline del mockup">
          <Card padding="none">
            <div className="p-5 space-y-5">
              <Swatches name="cream"  shades={[['cream', '#fbf8f2']]} />
              <Swatches name="brand"  shades={[
                ['50','#f1f8f6'],['100','#dcefe9'],['200','#b9ddd2'],['300','#8ec5b6'],['400','#5fa595'],
                ['500','#3f8a7a'],['600','#2a7064'],['700','#0f766e'],['800','#0d5e57'],['900','#0a4842'],
              ]} />
              <Swatches name="gold" shades={[
                ['50','#fbf7ee'],['100','#f3ead0'],['200','#e6d5a3'],['300','#d6bc78'],['400','#c9a86a'],['500','#b39052'],['600','#8f7341'],
              ]} />
              <Swatches name="warm" shades={[
                ['50','#faf8f5'],['100','#f4f1ec'],['150','#ece7df'],['200','#e3ddd2'],['250','#d8d1c4'],['300','#cbc3b4'],
                ['400','#a89f8e'],['500','#80796a'],['600','#5f5a4f'],['700','#46423a'],['800','#2e2b25'],['900','#1a1814'],
              ]} />
              <Swatches name="terra" shades={[
                ['100','#f6e6e0'],['300','#e2a89a'],['500','#b96f5b'],
              ]} />
            </div>
          </Card>
        </Section>

        <footer className="pt-6 border-t border-warm-150 text-[12px] text-warm-500 text-center">
          F2 cerrado cuando cada componente y cada color coincide con los mockups · próximo: F3 Auth
        </footer>
      </div>
    </div>
  )
}

/* ---------- helpers de la propia showcase ---------- */
function Section({
  title,
  subtitle,
  children,
}: {
  title: string
  subtitle?: string
  children: React.ReactNode
}) {
  return (
    <section>
      <div className="mb-3">
        <h2 className="font-serif text-[24px] text-warm-800 leading-tight">{title}</h2>
        {subtitle && <p className="text-[12.5px] text-warm-500 mt-0.5">{subtitle}</p>}
      </div>
      {children}
    </section>
  )
}

function Swatches({ name, shades }: { name: string; shades: Array<[string, string]> }) {
  return (
    <div>
      <p className="text-[11.5px] uppercase tracking-[0.14em] text-warm-500 font-medium mb-2">{name}</p>
      <div className="flex flex-wrap gap-2">
        {shades.map(([shade, hex]) => (
          <div key={shade} className="flex flex-col items-start min-w-[68px]">
            <div
              className="w-full h-10 rounded-md border border-warm-150"
              style={{ background: hex }}
              aria-label={`${name}-${shade}`}
            />
            <span className="text-[10.5px] text-warm-600 mt-1 font-mono leading-none">{shade}</span>
            <span className="text-[10px] text-warm-400 mt-0.5 font-mono leading-none">{hex}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

export default App
