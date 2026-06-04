import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Users, UserPlus, Edit3, Archive, ArchiveRestore, ShieldCheck, Sparkles,
} from 'lucide-react'
import { Modal, ModalFooter } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { extractApiError } from '@/lib/extractApiError'
import { cls } from '@/lib/cls'
import {
  archiveUser,
  createUser,
  listUsers,
  reactivateUser,
  updateUser,
  type CreateUserRequest,
  type UpdateUserRequest,
  type User,
} from '@/api/users'
import { SettingsHeader, SettingsBlock, inputCls } from './_primitives'

/**
 * `/configuracion/usuarios` — gestión del equipo que loguea en
 * BellaSync. La admin crea/edita/archiva otros SalonAdmins y
 * Receptionists. Los Stylists son entidad separada (no loguean).
 *
 * Restricciones del backend:
 *   - Email único global (no se puede repetir entre tenants)
 *   - No demoteable el último SalonAdmin activo
 *   - No auto-archivable (la admin no puede archivarse sola)
 */

const fmtDate = (iso: string | null) => {
  if (!iso) return 'Nunca'
  return new Date(iso).toLocaleDateString('es-CO', {
    day: 'numeric', month: 'short', year: 'numeric',
  })
}

export function UsuariosPage() {
  const qc = useQueryClient()
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['users'],
    queryFn: listUsers,
  })

  const [createOpen, setCreateOpen] = useState(false)
  const [editingUser, setEditingUser] = useState<User | null>(null)

  const refresh = () => qc.invalidateQueries({ queryKey: ['users'] })

  return (
    <div className="px-6 lg:px-10 py-8 max-w-4xl">
      <SettingsHeader
        eyebrow="Ajustes del salón"
        title="Usuarios del equipo"
        desc="Gestioná las cuentas que pueden entrar a BellaSync. Los estilistas que atienden clientes se manejan desde Estilistas (no necesitan login)."
      />

      <SettingsBlock icon={<Users size={16} />} title="Equipo" last>
        <div className="flex items-center justify-between mb-3">
          <div className="text-[12.5px] text-warm-500">
            {data?.length ?? 0} usuario{data?.length === 1 ? '' : 's'}
          </div>
          <Button
            size="sm"
            onClick={() => setCreateOpen(true)}
            leftIcon={<UserPlus size={14} />}
          >
            Nuevo usuario
          </Button>
        </div>

        {isLoading && <div className="rounded-xl bg-warm-100 h-32 animate-pulse" />}

        {error && (
          <div className="rounded-xl bg-terra-100 border border-terra-200 p-4 text-[13px] text-terra-700">
            {extractApiError(error)}
            <Button size="sm" variant="secondary" className="mt-3" onClick={() => refetch()}>
              Reintentar
            </Button>
          </div>
        )}

        {data && (
          <div className="rounded-xl border border-warm-150 bg-white overflow-hidden">
            {data.length === 0 ? (
              <div className="px-4 py-8 text-center text-[13px] text-warm-500">
                Sin usuarios. Creá el primero.
              </div>
            ) : (
              <table className="w-full text-[13px]">
                <thead>
                  <tr className="bg-warm-50 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
                    <th className="text-left font-medium px-4 py-2.5">Nombre</th>
                    <th className="text-left font-medium px-4 py-2.5 hidden sm:table-cell">Email</th>
                    <th className="text-left font-medium px-4 py-2.5">Rol</th>
                    <th className="text-left font-medium px-4 py-2.5 hidden md:table-cell">Último login</th>
                    <th className="text-right font-medium px-4 py-2.5">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {data.map((u) => (
                    <tr
                      key={u.id}
                      className={cls(
                        'border-b border-warm-100 last:border-0',
                        !u.isActive && 'opacity-60',
                      )}
                    >
                      <td className="px-4 py-3 text-warm-800 font-medium">
                        {u.fullName}
                        {!u.isActive && (
                          <span className="ml-2 text-[10.5px] tracking-[0.1em] uppercase font-semibold text-warm-500 bg-warm-100 px-1.5 py-0.5 rounded">
                            Archivado
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-warm-600 hidden sm:table-cell truncate max-w-[200px]">
                        {u.email}
                      </td>
                      <td className="px-4 py-3">
                        <RoleBadge role={u.role} />
                      </td>
                      <td className="px-4 py-3 text-warm-500 hidden md:table-cell">
                        {fmtDate(u.lastLoginAt)}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-end gap-1">
                          <IconButton
                            onClick={() => setEditingUser(u)}
                            title="Editar"
                            disabled={!u.isActive}
                          >
                            <Edit3 size={14} />
                          </IconButton>
                          {u.isActive ? (
                            <ArchiveButton userId={u.id} onDone={refresh} />
                          ) : (
                            <ReactivateButton userId={u.id} onDone={refresh} />
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}
      </SettingsBlock>

      {createOpen && (
        <CreateUserModal
          onClose={() => setCreateOpen(false)}
          onCreated={() => { setCreateOpen(false); refresh() }}
        />
      )}

      {editingUser && (
        <EditUserModal
          user={editingUser}
          onClose={() => setEditingUser(null)}
          onUpdated={() => { setEditingUser(null); refresh() }}
        />
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────

function RoleBadge({ role }: { role: string }) {
  const map: Record<string, { label: string; icon: typeof ShieldCheck; cls: string }> = {
    SalonAdmin:   { label: 'Administradora', icon: ShieldCheck, cls: 'text-brand-700 bg-brand-50' },
    Receptionist: { label: 'Recepción',      icon: Users,       cls: 'text-warm-700 bg-warm-100' },
    Stylist:      { label: 'Estilista',      icon: Sparkles,    cls: 'text-gold-700 bg-gold-50' },
    SuperAdmin:   { label: 'SaaS Admin',     icon: ShieldCheck, cls: 'text-terra-700 bg-terra-100' },
  }
  const v = map[role] ?? { label: role, icon: Users, cls: 'text-warm-600 bg-warm-100' }
  const Icon = v.icon
  return (
    <span className={cls(
      'inline-flex items-center gap-1 text-[10.5px] tracking-[0.1em] uppercase font-semibold px-2 py-0.5 rounded-md',
      v.cls,
    )}>
      <Icon size={11} />
      {v.label}
    </span>
  )
}

function IconButton({
  children, onClick, title, disabled,
}: {
  children: React.ReactNode
  onClick: () => void
  title: string
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={title}
      disabled={disabled}
      className={cls(
        'p-1.5 rounded-md text-warm-500 hover:text-warm-800 hover:bg-warm-100',
        'transition disabled:opacity-40 disabled:cursor-not-allowed',
      )}
    >
      {children}
    </button>
  )
}

function ArchiveButton({ userId, onDone }: { userId: string; onDone: () => void }) {
  const mut = useMutation({
    mutationFn: () => archiveUser(userId),
    onSuccess: onDone,
    onError: (e) => window.alert(extractApiError(e, 'No se pudo archivar.')),
  })
  return (
    <IconButton
      onClick={() => {
        if (window.confirm('¿Archivar este usuario? No podrá loguear hasta ser reactivado.')) {
          mut.mutate()
        }
      }}
      title="Archivar"
    >
      <Archive size={14} />
    </IconButton>
  )
}

function ReactivateButton({ userId, onDone }: { userId: string; onDone: () => void }) {
  const mut = useMutation({
    mutationFn: () => reactivateUser(userId),
    onSuccess: onDone,
    onError: (e) => window.alert(extractApiError(e, 'No se pudo reactivar.')),
  })
  return (
    <IconButton onClick={() => mut.mutate()} title="Reactivar">
      <ArchiveRestore size={14} />
    </IconButton>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Modal: crear usuario
// ───────────────────────────────────────────────────────────────────────

function CreateUserModal({
  onClose, onCreated,
}: {
  onClose: () => void
  onCreated: () => void
}) {
  const [form, setForm] = useState<CreateUserRequest>({
    email: '',
    fullName: '',
    password: '',
    role: 'Receptionist',
  })
  const [err, setErr] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: () => createUser(form),
    onSuccess: onCreated,
    onError: (e) => setErr(extractApiError(e)),
  })

  const canSubmit =
    form.email.includes('@') &&
    form.fullName.trim().length >= 2 &&
    form.password.length >= 6

  return (
    <Modal title="Nuevo usuario" onClose={onClose} size="md">
      <p className="text-[13px] text-warm-600 mb-4">
        Creá una cuenta para alguien de tu equipo. La persona inicia sesión
        con su email y la contraseña que le pongas.
      </p>

      <div className="space-y-3">
        <Field label="Nombre completo">
          <input
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
            className={inputCls}
            placeholder="Andrea López"
          />
        </Field>
        <Field label="Email">
          <input
            type="email"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            className={inputCls}
            placeholder="andrea@misalon.com"
          />
        </Field>
        <Field label="Contraseña" hint="Mínimo 6 caracteres">
          <input
            type="text"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
            className={inputCls}
            placeholder="Una clave fácil de recordar"
          />
        </Field>
        <Field label="Rol">
          <RolePicker value={form.role} onChange={(role) => setForm({ ...form, role })} />
        </Field>
      </div>

      <ModalFooter error={err}>
        <Button variant="secondary" onClick={onClose} fullWidth disabled={mut.isPending}>
          Cancelar
        </Button>
        <Button
          onClick={() => { setErr(null); mut.mutate() }}
          fullWidth
          loading={mut.isPending}
          disabled={!canSubmit}
        >
          Crear usuario
        </Button>
      </ModalFooter>
    </Modal>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Modal: editar usuario
// ───────────────────────────────────────────────────────────────────────

function EditUserModal({
  user, onClose, onUpdated,
}: {
  user: User
  onClose: () => void
  onUpdated: () => void
}) {
  const [form, setForm] = useState<UpdateUserRequest>({
    fullName: user.fullName,
    role: user.role,
  })
  const [err, setErr] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: () => updateUser(user.id, form),
    onSuccess: onUpdated,
    onError: (e) => setErr(extractApiError(e)),
  })

  return (
    <Modal title="Editar usuario" onClose={onClose} size="md">
      <p className="text-[13px] text-warm-600 mb-4">
        El email <strong>{user.email}</strong> no se puede cambiar. La
        contraseña la cambia el propio usuario desde "Olvidé mi contraseña".
      </p>

      <div className="space-y-3">
        <Field label="Nombre completo">
          <input
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
            className={inputCls}
          />
        </Field>
        <Field label="Rol">
          <RolePicker value={form.role} onChange={(role) => setForm({ ...form, role })} />
        </Field>
      </div>

      <ModalFooter error={err}>
        <Button variant="secondary" onClick={onClose} fullWidth disabled={mut.isPending}>
          Cancelar
        </Button>
        <Button
          onClick={() => { setErr(null); mut.mutate() }}
          fullWidth
          loading={mut.isPending}
        >
          Guardar cambios
        </Button>
      </ModalFooter>
    </Modal>
  )
}

// ───────────────────────────────────────────────────────────────────────

function Field({
  label, hint, children,
}: {
  label: string
  hint?: string
  children: React.ReactNode
}) {
  return (
    <div>
      <div className="flex items-baseline justify-between gap-3 mb-1.5">
        <label className="text-[12.5px] font-medium text-warm-700">{label}</label>
        {hint && <span className="text-[11px] text-warm-400">{hint}</span>}
      </div>
      {children}
    </div>
  )
}

function RolePicker({
  value, onChange,
}: {
  value: string
  onChange: (role: string) => void
}) {
  const options = [
    {
      role: 'Receptionist',
      label: 'Recepción',
      desc: 'Agenda + clientes + cobranza',
      icon: Users,
    },
    {
      role: 'SalonAdmin',
      label: 'Administradora',
      desc: 'Todo: agenda + configuración + reportes',
      icon: ShieldCheck,
    },
  ]
  return (
    <div className="grid grid-cols-1 gap-2">
      {options.map((o) => {
        const selected = value === o.role
        const Icon = o.icon
        return (
          <button
            key={o.role}
            type="button"
            onClick={() => onChange(o.role)}
            className={cls(
              'text-left rounded-lg p-3 border-2 transition flex items-start gap-3',
              selected
                ? 'border-brand-700 bg-brand-50/40'
                : 'border-warm-200 bg-white hover:border-warm-300',
            )}
          >
            <Icon size={18} className={selected ? 'text-brand-700' : 'text-warm-500'} />
            <div className="flex-1 min-w-0">
              <div className="text-[13.5px] font-medium text-warm-800">{o.label}</div>
              <div className="text-[12px] text-warm-500 mt-0.5">{o.desc}</div>
            </div>
          </button>
        )
      })}
    </div>
  )
}
