import type {
  AssistantReply,
  Budget,
  ChatTurn,
  CompleteTaskResult,
  Expense,
  Household,
  InvitePreview,
  Me,
  NewExpense,
  NewTask,
  NotificationList,
  ParsedExpense,
  TaskItem,
  TaskList,
  Weather,
} from './types'

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

// The session lives in an HttpOnly cookie the page cannot read, so an expired session is only noticed when the
// API answers 401. The session hook registers a listener to switch back to the sign-in screen.
let onSessionExpired: (() => void) | null = null
export function setSessionExpiredHandler(handler: (() => void) | null) {
  onSessionExpired = handler
}

interface RequestOptions extends RequestInit {
  /** Sign-in calls answer 401 for a wrong password; that is not an expired session. */
  authCall?: boolean
}

async function request<T>(path: string, { authCall = false, ...init }: RequestOptions = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body) headers.set('Content-Type', 'application/json')

  let response: Response
  try {
    response = await fetch(path, { ...init, headers, credentials: 'same-origin' })
  } catch {
    throw new ApiError(0, 'Sunucuya ulaşılamadı. İnternet bağlantını kontrol et.')
  }

  if (!response.ok) {
    if (response.status === 401 && !authCall) onSessionExpired?.()
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    throw new ApiError(response.status, problem.detail ?? problem.title ?? 'Bir şeyler ters gitti.', problem.errors)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

const post = <T>(path: string, body?: unknown, options: RequestOptions = {}) =>
  request<T>(path, { ...options, method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })

export const api = {
  // --- Account ---
  me: () => request<Me>('/api/account/me', { authCall: true }),
  register: (body: { email: string; password: string; displayName: string; deviceUserId: string | null }) =>
    post<Me>('/api/account/register', body, { authCall: true }),
  login: (body: { email: string; password: string; deviceUserId: string | null }) =>
    post<Me>('/api/account/login', body, { authCall: true }),
  logout: () => post<void>('/api/account/logout', undefined, { authCall: true }),

  // --- Household ---
  household: () => request<Household | undefined>('/api/households/current'),
  createHousehold: (name: string) => post<Household>('/api/households', { name }),
  previewInvite: (code: string) => request<InvitePreview>(`/api/households/invites/${encodeURIComponent(code)}`),
  joinHousehold: (code: string) => post<Household>('/api/households/join', { code }),
  leaveHousehold: () => post<void>('/api/households/leave'),
  removeMember: (userId: string) => request<Household>(`/api/households/members/${userId}`, { method: 'DELETE' }),
  regenerateInviteCode: () => post<Household>('/api/households/invite-code'),

  // --- Tasks & notifications ---
  tasks: () => request<TaskList>('/api/tasks'),
  createTask: (task: NewTask) => post<TaskItem>('/api/tasks', task),
  completeTask: (id: string, amount: number | null) =>
    post<CompleteTaskResult>(`/api/tasks/${id}/complete`, { amount }),
  deleteTask: (id: string) => request<void>(`/api/tasks/${id}`, { method: 'DELETE' }),
  notifications: () => request<NotificationList>('/api/notifications'),
  markNotificationsRead: () => post<void>('/api/notifications/read'),

  // --- Budget weather ---
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

  // --- AI ---
  parseExpense: (text: string, today: string) => post<ParsedExpense>('/api/expenses/parse', { text, today }),

  askAssistant: (messages: ChatTurn[], today: string) =>
    post<AssistantReply>('/api/assistant/chat', { messages, today }),
}
