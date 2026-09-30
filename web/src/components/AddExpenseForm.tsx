import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { CATEGORIES, type Category, type NewExpense } from '../api/types'
import { parseAmount } from '../lib/format'
import { CATEGORY_LABELS } from '../lib/weather'

interface Props {
  today: string
  onAdd: (expense: NewExpense, idempotencyKey: string) => Promise<void>
}

const NOTE_MAX = 200

export function AddExpenseForm({ today, onAdd }: Props) {
  const [amount, setAmount] = useState('')
  const [category, setCategory] = useState<Category>('Food')
  const [note, setNote] = useState('')
  const [spentOn, setSpentOn] = useState(today)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  // One key per intended expense: a double tap or a retry after a timeout reuses it, so the server
  // stores the expense once. A new key is minted only after a successful save.
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())

  async function submit(event: FormEvent) {
    event.preventDefault()
    const value = parseAmount(amount)
    if (!(value > 0)) {
      setErrors({ amount: 'Geçerli bir tutar gir (örn. 85,50).' })
      return
    }

    setSaving(true)
    setErrors({})
    try {
      await onAdd({ amount: value, category, note: note.trim() || null, spentOn }, idempotencyKey)
      setAmount('')
      setNote('')
      setSpentOn(today)
      setIdempotencyKey(crypto.randomUUID())
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setErrors(Object.fromEntries(Object.entries(error.fieldErrors).map(([k, v]) => [k, v[0]])))
      } else {
        setErrors({ form: error instanceof Error ? error.message : 'Kaydedilemedi.' })
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="card add-form" onSubmit={submit} noValidate>
      <h2 className="card-title">Harcama ekle</h2>

      <label className="amount-field">
        <span className="visually-hidden">Tutar</span>
        <span className="amount-prefix" aria-hidden="true">₺</span>
        <input
          inputMode="decimal"
          autoComplete="off"
          placeholder="0,00"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          aria-invalid={Boolean(errors.amount)}
          aria-describedby={errors.amount ? 'amount-error' : undefined}
        />
      </label>
      {errors.amount && <p id="amount-error" className="field-error">{errors.amount}</p>}

      <fieldset className="chips">
        <legend className="visually-hidden">Kategori</legend>
        {CATEGORIES.map((c) => (
          <label key={c} className="chip" data-selected={c === category}>
            <input
              type="radio"
              name="category"
              value={c}
              checked={c === category}
              onChange={() => setCategory(c)}
            />
            {CATEGORY_LABELS[c]}
          </label>
        ))}
      </fieldset>
      {errors.category && <p className="field-error">{errors.category}</p>}

      <div className="row">
        <label className="field grow">
          <span className="field-label">Not</span>
          <input
            value={note}
            maxLength={NOTE_MAX}
            placeholder="Örn. öğle yemeği"
            onChange={(e) => setNote(e.target.value)}
          />
        </label>
        <label className="field">
          <span className="field-label">Tarih</span>
          <input type="date" value={spentOn} max={today} onChange={(e) => setSpentOn(e.target.value)} />
        </label>
      </div>
      {errors.note && <p className="field-error">{errors.note}</p>}
      {errors.spentOn && <p className="field-error">{errors.spentOn}</p>}
      {errors.form && <p className="field-error" role="alert">{errors.form}</p>}

      <button type="submit" className="button button-primary" disabled={saving}>
        {saving ? 'Kaydediliyor…' : 'Ekle'}
      </button>
    </form>
  )
}
