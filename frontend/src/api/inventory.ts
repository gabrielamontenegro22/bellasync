import { api } from './axios'

/**
 * Tipos del módulo Inventario. Espejo de los DTOs del backend
 * (ProductResponse, ProductMovementResponse, InventorySummaryResponse).
 *
 * Convenciones:
 *  - status lo calcula el backend ("ok" | "warn" | "low" | "out").
 *  - category/tone son enums serializados como string ("Hair", "Rose", etc.).
 *  - lastInAt es ISO string o null si nunca tuvo entradas.
 */

export type ProductCategory = 'Hair' | 'Nails' | 'Hairremoval' | 'Spa' | 'Accessories'
export type ProductTone = 'Rose' | 'Amber' | 'Sand' | 'Olive' | 'Wine' | 'Mist'
export type ProductStatus = 'ok' | 'warn' | 'low' | 'out'
export type MovementKind = 'Inflow' | 'Outflow' | 'Adjustment'

export interface Product {
  id: string
  name: string
  brand: string
  category: ProductCategory
  unit: string
  stock: number
  minStock: number
  cost: number
  tone: ProductTone
  lastInAt: string | null
  isActive: boolean
  createdAt: string
  status: ProductStatus
}

export interface ProductMovement {
  id: string
  productId: string
  kind: MovementKind
  qty: number
  stockBefore: number
  stockAfter: number
  reason: string
  notes: string | null
  registeredByUserId: string | null
  registeredByUserName: string | null
  registeredAt: string
}

export interface InventorySummary {
  totalProducts: number
  totalValueCop: number
  okCount: number
  lowStockCount: number
  outOfStockCount: number
}

export interface CreateProductRequest {
  name: string
  brand: string
  category: ProductCategory
  unit: string
  minStock: number
  cost: number
  tone?: ProductTone
}

export interface UpdateProductRequest extends CreateProductRequest {}

export interface RegisterMovementRequest {
  productId: string
  kind: MovementKind
  qty: number
  reason: string
  notes?: string | null
}

/* -------------------------------------------------------------------------- */
/*  Endpoints                                                                 */
/* -------------------------------------------------------------------------- */

export interface ListProductsParams {
  category?: string  // 'all' | ProductCategory minúsculas? backend acepta case-insensitive
  status?: 'all' | ProductStatus | 'ok' | 'low' | 'out'
  query?: string
  includeArchived?: boolean
}

export async function listProducts(params: ListProductsParams = {}): Promise<Product[]> {
  const { data } = await api.get<Product[]>('/api/Inventory', { params })
  return data
}

export async function getInventorySummary(): Promise<InventorySummary> {
  const { data } = await api.get<InventorySummary>('/api/Inventory/summary')
  return data
}

export async function listMovements(productId: string): Promise<ProductMovement[]> {
  const { data } = await api.get<ProductMovement[]>(`/api/Inventory/${productId}/movements`)
  return data
}

export async function createProduct(req: CreateProductRequest): Promise<Product> {
  const { data } = await api.post<Product>('/api/Inventory', req)
  return data
}

export async function updateProduct(id: string, req: UpdateProductRequest): Promise<Product> {
  const { data } = await api.put<Product>(`/api/Inventory/${id}`, req)
  return data
}

export async function archiveProduct(id: string): Promise<void> {
  await api.post(`/api/Inventory/${id}/archive`)
}

export async function reactivateProduct(id: string): Promise<void> {
  await api.post(`/api/Inventory/${id}/reactivate`)
}

export async function registerMovement(req: RegisterMovementRequest): Promise<ProductMovement> {
  const { data } = await api.post<ProductMovement>('/api/Inventory/movements', req)
  return data
}
