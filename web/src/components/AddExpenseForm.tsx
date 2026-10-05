import { useState, type FormEvent } from 'react'
import { ApiError, api } from '../api/client'
import { CATEGORIES, type Category, type NewExpense } from '../api/types'
import { parseAmount } from '../lib/format'
import { CATEGORY_LABELS } from '../lib/weather'

interface Props {
  today: string
  /** Today, or the last day of the past month being viewed. */
  defaultDate: string
  /** Which ledger the expense goes to, shown so a household member never adds to the wrong one. */
  ledgerLabel: string
  onAdd: (expense: NewExpense, idempotencyKey: string) => Promise<void>
}

const NOTE_MAX = 200

export function AddExpenseForm({ today, defaultDate, ledgerLabel, onAdd }: Props) {
  const [amount, setAmount] = useState('')
  const [category, setCategory] = useState<Category>('Food')
  const [note, setNote] = useState('')
  const [spentOn, setSpentOn] = useState(defaultDate)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  // One key per intended expense: a double tap or a retry after a timeout reuses it, so the server
  // stores the expense once. A new key is minted only after a successful save.
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
  const [sentence, setSentence] = useState('')
  const [parsing, setParsing] = useState(false)
  const [suggested, setSuggested] = useState(false)

  // The model only suggests: it fills the form and the user still presses "Ekle" (human in the loop).
  async function understand(event: FormEvent) {
    event.preventDefault()
    if (!sentence.trim()) return
    setParsing(true)
    setErrors({})
    try {
      const parsed = await api.parseExpense(sentence.trim(), today)
      setAmount(String(parsed.amount).replace('.', ','))
      setCategory(parsed.category)
      setNote(parsed.note ?? '')
      setSpentOn(parsed.spentOn)
      setSuggested(true)
    } catch (error) {
      setErrors({ sentence: error instanceof Error ? error.message : 'Anlayamadım.' })
    } finally {
      setParsing(false)
    }
  }

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
      setSpentOn(defaultDate)
      setSentence('')
      setSuggested(false)
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
    <section className="card add-form">
      <div className="card-head">
        <h2 className="card-title">Harcama ekle</h2>
        <span className="ledger-tag">{ledgerLabel}</span>
      </div>

      <form className="quick-entry" onSubmit={understand}>
        <label className="visually-hidden" htmlFor="quick-entry">Cümleyle harcama ekle</label>
        <input
          id="quick-entry"
          value={sentence}
          maxLength={300}
          placeholder="Yaz gitsin: dün akşam kahveye 85 lira"
          onChange={(e) => setSentence(e.target.value)}
          aria-describedby="quick-entry-help"
        />
        <button type="submit" className="button button-soft" disabled={parsing || !sentence.trim()}>
          {parsing ? 'Düşünüyor…' : 'Anla'}
        </button>
      </form>
      {errors.sentence ? (
        <p className="field-error" role="alert">{errors.sentence}</p>
      ) : (
        <p id="quick-entry-help" className="hint">
          {suggested ? 'Yapay zekâ formu doldurdu. Kontrol et ve Ekle’ye bas.' : 'Yapay zekâ cümleni forma çevirir, kaydetmeden önce sen onaylarsın.'}
        </p>
      )}

      <form className="add-form-fields" onSubmit={submit} noValidate>
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
    </section>
  )
}
