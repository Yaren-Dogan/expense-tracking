import { useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Expense } from '../api/types'
import { formatDayHeading, formatMoney, formatMonth } from '../lib/format'
import { CATEGORY_LABELS } from '../lib/weather'

interface Props {
  month: string
  expenses: Expense[]
  meId: string
  /** In a household everyone sees everything, so each row says who entered it. */
  showAuthor: boolean
  onDelete: (id: string) => void
}

function groupByDay(expenses: Expense[]) {
  const groups = new Map<string, Expense[]>()
  for (const e of expenses) groups.set(e.spentOn, [...(groups.get(e.spentOn) ?? []), e])
  return [...groups.entries()]
}

export function ExpenseList({ month, expenses, meId, showAuthor, onDelete }: Props) {
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)

  async function exportCsv() {
    setExporting(true)
    setExportError(null)
    try {
      await api.exportExpenses(month)
    } catch (e) {
      setExportError(e instanceof ApiError ? e.message : 'Dosya indirilemedi.')
    } finally {
      setExporting(false)
    }
  }

  return (
    <section className="card" aria-labelledby="list-title">
      <div className="card-head">
        <h2 id="list-title" className="card-title">{formatMonth(month)} harcamaları</h2>
        {expenses.length > 0 && (
          <button type="button" className="link-button" onClick={() => void exportCsv()} disabled={exporting}
            aria-label={`${formatMonth(month)} harcamalarını Excel için CSV olarak indir`}>
            {exporting ? 'Hazırlanıyor…' : 'CSV indir'}
          </button>
        )}
      </div>
      {exportError && <p className="notice" role="alert">{exportError}</p>}

      {expenses.length === 0 ? (
        <p className="empty">Bu ayda harcama yok. Gökyüzü tertemiz.</p>
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
                    {(e.note || showAuthor) && (
                      <span className="expense-note">
                        {showAuthor && <span className="expense-author">{e.createdBy === meId ? 'Sen' : (e.createdByName ?? 'Eski üye')}</span>}
                        {showAuthor && e.note && ' · '}
                        {e.note}
                      </span>
                    )}
                  </div>
                  <span className="expense-amount">{formatMoney(e.amount)}</span>
                  {/* Members can delete only what they entered (docs/prd-accounts.md); the server enforces it too. */}
                  {e.createdBy === meId ? (
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
                  ) : (
                    <span className="icon-placeholder" aria-hidden="true" />
                  )}
                </li>
              ))}
            </ul>
          </div>
        ))
      )}
    </section>
  )
}
