import { useEffect, useState } from 'react'
import { Badge, Button, Card, Input } from '@/components/ui'
import type { CustomerResponse } from '@/api/customers'
import { CustomerModal } from './components/CustomerModal'
import { useCustomers, useDeleteCustomer } from './hooks'

/**
 * CRM de clientes. Lista paginada con búsqueda por nombre/teléfono,
 * botón "Nuevo cliente", y edición/archivado por fila.
 *
 * MVP: solo gestiona los campos básicos. La vista "perfil rico" con tabs
 * (Historial, Fichas, Pagos) llega cuando tengamos esos módulos atrás
 * (ya hay backend para Citas; Fichas/Pagos vendrán después).
 */
export function CustomersPage() {
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [includeInactive, setIncludeInactive] = useState(false)
  const [editing, setEditing] = useState<CustomerResponse | null>(null)
  const [showCreate, setShowCreate] = useState(false)

  // Debounce search input — espera 300ms antes de buscar
  useEffect(() => {
    const t = setTimeout(() => { setDebouncedSearch(search); setPage(1) }, 300)
    return () => clearTimeout(t)
  }, [search])

  const { data, isLoading, error } = useCustomers({
    search: debouncedSearch || undefined,
    page,
    pageSize: 20,
    includeInactive,
  })

  const del = useDeleteCustomer()

  function confirmArchive(c: CustomerResponse) {
    if (window.confirm(`¿Archivar a "${c.fullName}"? Sus citas pasadas se mantienen.`)) {
      del.mutate(c.id)
    }
  }

  return (
    <div className="space-y-4 p-4">
      <Card className="flex flex-wrap items-center gap-2 p-3">
        <h1 className="font-serif text-2xl text-brand-700 mr-auto">Clientes</h1>
        <Input
          placeholder="Buscar por nombre o teléfono…"
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="min-w-64"
        />
        <label className="flex items-center gap-1 text-xs text-warm-600">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={e => { setIncludeInactive(e.target.checked); setPage(1) }}
          />
          Ver archivados
        </label>
        <Button onClick={() => setShowCreate(true)}>+ Nuevo cliente</Button>
      </Card>

      {isLoading && <p className="text-sm text-warm-500">Cargando…</p>}
      {error && <p className="text-sm text-terra-700">No se pudo cargar la lista.</p>}

      {data && data.items.length === 0 && (
        <Card className="p-8 text-center">
          <p className="text-warm-500">
            {debouncedSearch
              ? `Sin resultados para "${debouncedSearch}".`
              : 'No hay clientes todavía.'}
          </p>
          <Button className="mt-3" onClick={() => setShowCreate(true)}>+ Crear el primero</Button>
        </Card>
      )}

      {data && data.items.length > 0 && (
        <>
          <Card className="overflow-x-auto p-0">
            <table className="w-full text-sm">
              <thead className="border-b border-warm-200 bg-warm-50 text-left">
                <tr>
                  <Th>Nombre</Th>
                  <Th>Teléfono</Th>
                  <Th>Email</Th>
                  <Th>Cumpleaños</Th>
                  <Th>Estado</Th>
                  <Th className="text-right">Acciones</Th>
                </tr>
              </thead>
              <tbody>
                {data.items.map(c => (
                  <tr key={c.id} className="border-b border-warm-100 last:border-b-0 hover:bg-warm-50/50">
                    <Td className="font-medium text-warm-900">{c.fullName}</Td>
                    <Td className="font-mono text-xs">{c.phone}</Td>
                    <Td className="text-warm-600">{c.email ?? '—'}</Td>
                    <Td className="text-warm-600">{c.birthday ?? '—'}</Td>
                    <Td>
                      {c.isActive
                        ? <Badge tone="brand">Activo</Badge>
                        : <Badge tone="neutral">Archivado</Badge>}
                      {c.acceptsMarketing && (
                        <Badge tone="gold" className="ml-1">Marketing</Badge>
                      )}
                    </Td>
                    <Td className="text-right">
                      <Button variant="ghost" size="sm" onClick={() => setEditing(c)}>
                        Editar
                      </Button>
                      {c.isActive && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => confirmArchive(c)}
                          disabled={del.isPending}
                        >
                          Archivar
                        </Button>
                      )}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>

          {data.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 text-sm">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={!data.hasPreviousPage}
              >
                ← Anterior
              </Button>
              <span className="text-warm-600">
                Página {data.page} de {data.totalPages} · {data.totalItems} clientes
              </span>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setPage(p => p + 1)}
                disabled={!data.hasNextPage}
              >
                Siguiente →
              </Button>
            </div>
          )}
        </>
      )}

      {showCreate && (
        <CustomerModal customer={null} onClose={() => setShowCreate(false)} />
      )}
      {editing && (
        <CustomerModal customer={editing} onClose={() => setEditing(null)} />
      )}
    </div>
  )
}

function Th({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return (
    <th className={`px-3 py-2 text-xs font-semibold uppercase tracking-wide text-warm-500 ${className}`}>
      {children}
    </th>
  )
}

function Td({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <td className={`px-3 py-2 ${className}`}>{children}</td>
}
