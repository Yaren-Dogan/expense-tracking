import type { Expense } from '../api/types'
import { formatDayHeading, formatMoney, formatMonth } from '../lib/format'
import { CATEGORY_LABELS } from '../lib/weather'

interface Props {
  month: string
  expenses: Expense[]
  onDelete: (id: string) => void
}

function groupByDay(expenses: Expense[]) {
  const groups = new Map<string, Expense[]>()
  for (const e of expenses) groups.set(e.spentOn, [...(groups.get(e.spentOn) ?? []), e])
  return [...groups.entries()]
}

export function ExpenseList({ month, expenses, onDelete }: Props) {
  return (
    <section className="card" aria-labelledby="list-title">
      <h2 id="list-title" className="card-title">{formatMonth(month)} harcamaları</h2>

      {expenses.length === 0 ? (
        <p className="empty">Bu ay henüz harcama yok. Gökyüzü tertemiz.</p>
      ) : (
        groupByDay(expenses).map(([day, items]) => (
          <div key={day} className="day-group">
            <div className="day-heading">
              <span>{formatDayHeading(day)}</span>
              <span>{formatMoney(items.reduce((sum, e) => sum + e.amount, 0))}</span>
            </div>
            <ul className="expenses">
              {items.map((e) => (
                <li key={e.id} className="expense">
                  <span className="expense-dot" data-category={e.category} aria-hidden="true" />
                  <div className="expense-text">
                    <span className="expense-category">{CATEGORY_LABELS[e.category]}</span>
                    {e.note && <span className="expense-note">{e.note}</span>}
                  </div>
                  <span className="expense-amount">{formatMoney(e.amount)}</span>
                  <button
                    type="button"
                    className="icon-button"
                    aria-label={`${CATEGORY_LABELS[e.category]} ${formatMoney(e.amount)} harcamasını sil`}
                    onClick={() => onDelete(e.id)}
                  >
                    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                      <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                    </svg>
                  </button>
                </li>
              ))}
            </ul>
          </div>
        ))
      )}
    </section>
  )
}
