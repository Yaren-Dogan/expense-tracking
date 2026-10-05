// Mirrors the API contracts in src/ExpenseTracker.Application (see docs/prd.md "Endpoint'ler").

export const CATEGORIES = [
  'Market',
  'Food',
  'Transport',
  'Bills',
  'Entertainment',
  'Health',
  'Clothing',
  'Other',
] as const

export type Category = (typeof CATEGORIES)[number]

export type WeatherCondition = 'Sunny' | 'PartlyCloudy' | 'Cloudy' | 'Rainy' | 'Stormy'

export interface Expense {
  id: string
  amount: number
  category: Category
  note: string | null
  spentOn: string // yyyy-MM-dd
  createdAt: string
  createdBy: string
  createdByName: string | null
}

export interface NewExpense {
  amount: number
  category: Category
  note: string | null
  spentOn: string
}

export interface Budget {
  month: string // yyyy-MM
  amount: number
}

export interface TodayWeather {
  condition: WeatherCondition
  dailyAllowance: number
  spentToday: number
  safeToSpend: number
  ratio: number | null
}

export interface DayWeather {
  date: string
  condition: WeatherCondition | null
  spent: number
}

export interface MonthForecast {
  condition: WeatherCondition
  projectedSpend: number
  difference: number
}

export interface Weather {
  date: string
  month: string
  budget: number | null
  spentThisMonth: number
  remainingBudget: number | null
  today: TodayWeather | null
  lastSevenDays: DayWeather[]
  forecast: MonthForecast | null
}

export interface ParsedExpense {
  amount: number
  category: Category
  note: string | null
  spentOn: string
}

export interface ChatTurn {
  role: 'user' | 'assistant'
  content: string
}

export interface AssistantReply {
  reply: string
  toolsUsed: string[]
}

export interface HouseholdSummary {
  id: string
  name: string
}

export interface Me {
  id: string
  email: string
  displayName: string
  household: HouseholdSummary | null
  /** The ledger on screen: the household's, or the personal one (always personal without a household). */
  ledger: 'household' | 'personal'
  claimedExpenses: number
}

export interface HouseholdMember {
  userId: string
  displayName: string
  role: 'Owner' | 'Member'
  joinedAt: string
  isMe: boolean
}

export interface Household {
  id: string
  name: string
  inviteCode: string
  isOwner: boolean
  members: HouseholdMember[]
}

export interface InvitePreview {
  householdName: string
  memberNames: string[]
}

export type TaskRecurrence = 'None' | 'Monthly'

export interface TaskItem {
  id: string
  title: string
  category: Category
  assigneeId: string
  assigneeName: string
  createdById: string
  createdByName: string
  dueOn: string
  recurrence: TaskRecurrence
  completedAt: string | null
  completedById: string | null
  completedByName: string | null
  paidAmount: number | null
  canComplete: boolean
  canDelete: boolean
}

export interface TaskList {
  open: TaskItem[]
  done: TaskItem[]
}

export interface NewTask {
  title: string
  assigneeId: string
  dueOn: string
  recurrence: TaskRecurrence
  category: Category
}

export interface CompleteTaskResult {
  completed: TaskItem
  next: TaskItem | null
}

export type NotificationKind =
  | 'TaskAssigned'
  | 'TaskDueSoon'
  | 'TaskOverdue'
  | 'TaskCompleted'
  | 'MonthReviewReady'
  | 'BudgetPlanApplied'

export interface AppNotification {
  id: string
  kind: NotificationKind
  message: string
  subjectId: string
  createdAt: string
  isRead: boolean
}

export interface NotificationList {
  unreadCount: number
  items: AppNotification[]
}

export interface MonthReview {
  id: string
  month: string
  headline: string
  highlights: string[]
  tip: string
  generatedBy: string // model name, or "kurallar"
  generatedAt: string
}

export type CutLevel = 'Light' | 'Medium' | 'Strong'

export interface PlanCut {
  category: Category
  level: CutLevel
  currentMonthly: number
  targetMonthly: number
  reason: string
}

export interface BudgetPlan {
  id: string
  purpose: string
  goalAmount: number
  requestedMonths: number
  months: number
  isExtended: boolean
  startMonth: string
  endMonth: string
  baselineMonthly: number
  monthlySaving: number
  monthlyBudget: number
  cuts: PlanCut[]
  advice: string
  generatedBy: string
  createdByName: string
  createdAt: string
  appliedAt: string | null
  appliedByName: string | null
}
