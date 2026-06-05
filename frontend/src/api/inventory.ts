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

export type ProductTone = 'Rose' | 'Amber' | 'Sand' | 'Olive' | 'Wine' | 'Mist'
export type ProductStatus = 'ok' | 'warn' | 'low' | 'out'
export type MovementKind = 'Inflow' | 'Outflow' | 'Adjustment'

/**
 * Categoría custom del tenant. Cada salón crea las suyas desde
 * /inventario → "Gestionar categorías". El tono define el color visual
 * del avatar del producto.
 */
export interface ProductCategory {
  id: string
  name: string
  tone: ProductTone
  isActive: boolean
  activeProductsCount: number
}

export interface Product {
  id: string
  name: string
  brand: string
  categoryId: string
  categoryName: string
  unit: string
  stock: number
  minStock: number
  cost: number
  /** Heredado de la categoría — sirve para colorear el avatar en la tabla. */
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
  categoryId: string
  unit: string
  minStock: number
  cost: number
}

export interface UpdateProductRequest extends CreateProductRequest {}

export interface RegisterMovementRequest {
  productId: string
  kind: MovementKind
  qty: number
  reason: string
  notes?: string | null
}

export interface CreateCategoryRequest {
  name: string
  tone: ProductTone
}

export interface UpdateCategoryRequest {
  name: string
  tone: ProductTone
}

/* -------------------------------------------------------------------------- */
/*  Endpoints                                                                 */
/* -------------------------------------------------------------------------- */

export interface ListProductsParams {
  /** undefined = sin filtro de categoría. Pasar id de ProductCategory para restringir. */
  categoryId?: string
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

/* -------------------------------------------------------------------------- */
/*  Categorías (CRUD custom por tenant)                                       */
/* -------------------------------------------------------------------------- */

export async function listCategories(includeArchived = false): Promise<ProductCategory[]> {
  const { data } = await api.get<ProductCategory[]>('/api/Inventory/categories', {
    params: { includeArchived },
  })
  return data
}

export async function createCategory(req: CreateCategoryRequest): Promise<ProductCategory> {
  const { data } = await api.post<ProductCategory>('/api/Inventory/categories', req)
  return data
}

export async function updateCategory(id: string, req: UpdateCategoryRequest): Promise<ProductCategory> {
  const { data } = await api.put<ProductCategory>(`/api/Inventory/categories/${id}`, req)
  return data
}

export async function archiveCategory(id: string): Promise<void> {
  await api.post(`/api/Inventory/categories/${id}/archive`)
}

export async function reactivateCategory(id: string): Promise<void> {
  await api.post(`/api/Inventory/categories/${id}/reactivate`)
}
