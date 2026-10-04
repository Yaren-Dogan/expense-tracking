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
