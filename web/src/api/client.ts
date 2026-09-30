import { getUserId } from '../lib/userId'
import type { Budget, Expense, NewExpense, Weather } from './types'

/** A failed API call. `fieldErrors` holds RFC 7807 validation errors keyed by camelCase field name. */
export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('X-User-Id', getUserId())
  if (init.body) headers.set('Content-Type', 'application/json')

  let response: Response
  try {
    response = await fetch(path, { ...init, headers })
  } catch {
    throw new ApiError(0, 'Sunucuya ulaşılamadı. İnternet bağlantını kontrol et.')
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    throw new ApiError(response.status, problem.detail ?? problem.title ?? 'Bir şeyler ters gitti.', problem.errors)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

export const api = {
  weather: (date: string) => request<Weather>(`/api/weather?date=${date}`),

  expenses: (month: string) => request<Expense[]>(`/api/expenses?month=${month}`),

  addExpense: (expense: NewExpense, idempotencyKey: string) =>
    request<Expense>('/api/expenses', {
      method: 'POST',
      body: JSON.stringify(expense),
      headers: { 'Idempotency-Key': idempotencyKey },
    }),

  deleteExpense: (id: string) => request<void>(`/api/expenses/${id}`, { method: 'DELETE' }),

  budget: async (month: string): Promise<Budget | null> => {
    try {
      return await request<Budget>(`/api/budgets/${month}`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  },

  setBudget: (month: string, amount: number) =>
    request<Budget>(`/api/budgets/${month}`, { method: 'PUT', body: JSON.stringify({ amount }) }),
}
