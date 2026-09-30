import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { formatMonth, parseAmount } from '../lib/format'

interface Props {
  open: boolean
  month: string
  current: number | null
  onSave: (amount: number) => Promise<void>
  onClose: () => void
}

export function BudgetDialog({ open, month, current, onSave, onClose }: Props) {
  const ref = useRef<HTMLDialogElement>(null)
  const [value, setValue] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) {
      setValue(current ? String(current).replace('.', ',') : '')
      setError(null)
      dialog.showModal()
    } else if (!open && dialog.open) {
      dialog.close()
    }
  }, [open, current])

  async function submit(event: FormEvent) {
    event.preventDefault()
    const amount = parseAmount(value)
    if (!(amount > 0)) {
      setError('Geçerli bir tutar gir.')
      return
    }
    setSaving(true)
    try {
      await onSave(amount)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? (Object.values(e.fieldErrors)[0]?.[0] ?? e.message) : 'Kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <dialog ref={ref} className="dialog" onClose={onClose} aria-labelledby="budget-title">
      <form onSubmit={submit} noValidate>
        <h2 id="budget-title" className="card-title">{formatMonth(month)} bütçesi</h2>
        <p className="dialog-help">Bu ay toplam ne kadar harcamak istiyorsun? Günlük payın buna göre hesaplanır.</p>
        <label className="amount-field">
          <span className="visually-hidden">Aylık bütçe</span>
          <span className="amount-prefix" aria-hidden="true">₺</span>
          <input
            inputMode="decimal"
            autoFocus
            placeholder="15000"
            value={value}
            onChange={(e) => setValue(e.target.value)}
            aria-invalid={Boolean(error)}
          />
        </label>
        {error && <p className="field-error" role="alert">{error}</p>}
        <div className="dialog-actions">
          <button type="button" className="button button-ghost" onClick={onClose}>
            Vazgeç
          </button>
          <button type="submit" className="button button-primary" disabled={saving}>
            {saving ? 'Kaydediliyor…' : 'Kaydet'}
          </button>
        </div>
      </form>
    </dialog>
  )
}
