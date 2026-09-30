const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 2 })
const moneyWhole = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 })

export const formatMoney = (value: number) => money.format(value)

/** For tight spaces such as the 7-day strip. */
export const formatMoneyShort = (value: number) => moneyWhole.format(value)

/** Local calendar date as yyyy-MM-dd; toISOString() would give the UTC date and be wrong near midnight. */
export function toIsoDate(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

export const todayIso = () => toIsoDate(new Date())

/** Parses yyyy-MM-dd as a local date (new Date('2026-09-30') would be UTC midnight). */
export function fromIsoDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d)
}

export const formatWeekday = (iso: string) =>
  fromIsoDate(iso).toLocaleDateString('tr-TR', { weekday: 'short' })

export const formatDayHeading = (iso: string) =>
  fromIsoDate(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', weekday: 'long' })

export const formatMonth = (month: string) =>
  fromIsoDate(`${month}-01`).toLocaleDateString('tr-TR', { month: 'long', year: 'numeric' })

/**
 * Parses amounts the way Turkish users type them: "85,50", "85.50", "1.250,75" and "15.000" (thousands).
 * Returns NaN for anything else.
 */
export function parseAmount(input: string): number {
  let normalized = input.replace(/\s/g, '')
  if (normalized.includes(',')) normalized = normalized.replace(/\./g, '').replace(',', '.')
  else if (/^\d{1,3}(\.\d{3})+$/.test(normalized)) normalized = normalized.replace(/\./g, '')
  return /^\d+(\.\d{1,2})?$/.test(normalized) ? Number(normalized) : Number.NaN
}
