import { useEffect, useState, type FormEvent } from 'react'
import { ApiError, api } from '../api/client'
import type { BudgetPlan, CutLevel } from '../api/types'
import { formatMoney, formatMonth, parseAmount } from '../lib/format'
import { CATEGORY_LABELS } from '../lib/weather'

interface Props {
  /** Applying a plan sets budgets; the dashboard reloads in case one of them is on screen. */
  onApplied: () => void
}

const LEVELS: Record<CutLevel, string> = { Light: 'az', Medium: 'orta', Strong: 'çok' }
const RULES = 'kurallar'

const errorText = (error: unknown) =>
  error instanceof ApiError
    ? (Object.values(error.fieldErrors)[0]?.[0] ?? error.message)
    : error instanceof Error
      ? error.message
      : 'Bir şeyler ters gitti.'

/** The budget planner agent: a savings goal in, monthly budgets and category targets out; applied only on approval. */
export function PlannerCard({ onApplied }: Props) {
  const [plan, setPlan] = useState<BudgetPlan | null | undefined>(undefined)
  const [editing, setEditing] = useState(false)
  const [amount, setAmount] = useState('')
  const [months, setMonths] = useState('4')
  const [purpose, setPurpose] = useState('')
  const [busy, setBusy] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api
      .latestPlan()
      .then((p) => {
        if (active) setPlan(p ?? null)
      })
      .catch(() => {
        if (active) setPlan(null)
      })
    return () => {
      active = false
    }
  }, [])

  async function create(event: FormEvent) {
    event.preventDefault()
    const goal = parseAmount(amount)
    if (!(goal > 0)) {
      setError('Biriktirmek istediğin tutarı yaz (örn. 20.000).')
      return
    }
    setBusy(true)
    setError(null)
    try {
      setPlan(await api.createPlan(goal, Number(months), purpose.trim() || null))
      setEditing(false)
      setConfirming(false)
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  async function apply() {
    if (!plan) return
    setBusy(true)
    setError(null)
    try {
      setPlan(await api.applyPlan(plan.id))
      setConfirming(false)
      onApplied()
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  if (plan === undefined) return null
  const showForm = editing || plan === null

  return (
    <section className="card planner" aria-labelledby="planner-title">
      <div className="card-head">
        <h2 id="planner-title" className="card-title">Birikim planlayıcı</h2>
        {!showForm && (
          <button type="button" className="link-button" onClick={() => setEditing(true)}>
            Yeni plan
          </button>
        )}
      </div>

      {showForm && (
        <form className="plan-form" onSubmit={create} noValidate>
          <p className="hint">
            Hedefini yaz; ajan son aylarının harcamalarına bakıp hangi kalemlerden kısabileceğini ve aylık bütçeni önerir.
            Sen onaylamadan hiçbir şey değişmez.
          </p>
          <div className="row">
            <label className="field grow">
              <span className="field-label">Biriktirilecek tutar</span>
              <input inputMode="decimal" placeholder="20.000" value={amount} onChange={(e) => setAmount(e.target.value)} />
            </label>
            <label className="field">
              <span className="field-label">Kaç ayda</span>
              <select value={months} onChange={(e) => setMonths(e.target.value)}>
                {Array.from({ length: 24 }, (_, i) => i + 1).map((m) => (
                  <option key={m} value={m}>
                    {m} ay
                  </option>
                ))}
              </select>
            </label>
          </div>
          <label className="field">
            <span className="field-label">Ne için (isteğe bağlı)</span>
            <input maxLength={100} placeholder="Örn. yaz tatili" value={purpose} onChange={(e) => setPurpose(e.target.value)} />
          </label>
          <div className="dialog-actions">
            {plan && (
              <button type="button" className="button button-ghost" onClick={() => setEditing(false)}>
                Vazgeç
              </button>
            )}
            <button type="submit" className="button button-primary" disabled={busy}>
              {busy ? 'Plan hazırlanıyor…' : 'Plan yap'}
            </button>
          </div>
        </form>
      )}

      {plan && !showForm && (
        <div className="plan">
          <p className="plan-goal">
            {plan.purpose ? <b>{plan.purpose}: </b> : null}
            {formatMoney(plan.goalAmount)}, {plan.months} ayda
          </p>
          {plan.isExtended && (
            <p className="notice">
              Bu hedef {plan.requestedMonths} ayda harcamaların yarısından fazlasını kısmayı gerektiriyor. Plan {plan.months} aya
              uzatıldı.
            </p>
          )}
          <dl className="forecast-stats">
            <div>
              <dt>Şu an aylık</dt>
              <dd>{formatMoney(plan.baselineMonthly)}</dd>
            </div>
            <div>
              <dt>Aylık birikim</dt>
              <dd>{formatMoney(plan.monthlySaving)}</dd>
            </div>
            <div>
              <dt>Yeni bütçe</dt>
              <dd>{formatMoney(plan.monthlyBudget)}</dd>
            </div>
          </dl>
          <p className="hint">
            {formatMonth(plan.startMonth)} – {formatMonth(plan.endMonth)} arası her ay.
          </p>

          <ul className="cuts">
            {plan.cuts.map((c) => (
              <li key={c.category} className="cut">
                <div className="cut-head">
                  <span className="expense-dot" data-category={c.category} aria-hidden="true" />
                  <b>{CATEGORY_LABELS[c.category]}</b>
                  <span className="badge-soft">{LEVELS[c.level]} kıs</span>
                  <span className="cut-amounts">
                    {formatMoney(c.currentMonthly)} → <b>{formatMoney(c.targetMonthly)}</b>
                  </span>
                </div>
                <p className="cut-reason">{c.reason}</p>
              </li>
            ))}
          </ul>
          {plan.advice && (
            <p className="review-tip">
              <span aria-hidden="true">💡</span> {plan.advice}
            </p>
          )}
          <p className="hint">
            {plan.generatedBy === RULES
              ? 'Kategorileri kurallar seçti (yapay zekâ kullanılamadı).'
              : `Kategorileri planlayıcı ajan seçti (${plan.generatedBy}); tutarları kod hesapladı.`}
          </p>

          {plan.appliedAt ? (
            <p className="applied">
              ✓ {plan.appliedByName} uyguladı. Bu aylardaki bütçe {formatMoney(plan.monthlyBudget)}.
            </p>
          ) : confirming ? (
            <div className="confirm stack">
              <p>
                {formatMonth(plan.startMonth)} – {formatMonth(plan.endMonth)} arasındaki {plan.months} ayın bütçesi{' '}
                {formatMoney(plan.monthlyBudget)} olarak ayarlanacak. Hanedeysen herkes için geçerli.
              </p>
              <div className="dialog-actions">
                <button type="button" className="button button-ghost" onClick={() => setConfirming(false)}>
                  Vazgeç
                </button>
                <button type="button" className="button button-primary" disabled={busy} onClick={() => void apply()}>
                  Onayla
                </button>
              </div>
            </div>
          ) : (
            <button type="button" className="button button-primary" onClick={() => setConfirming(true)}>
              Planı uygula
            </button>
          )}
        </div>
      )}

      {error && <p className="field-error" role="alert">{error}</p>}
    </section>
  )
}
