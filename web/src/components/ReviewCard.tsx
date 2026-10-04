import { useEffect, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { MonthReview } from '../api/types'
import { formatMonth } from '../lib/format'

interface Props {
  /** The finished month to show (yyyy-MM). */
  month: string
  /** On the current month's screen only an existing review of last month is shown, without the "write" button. */
  offerToCreate: boolean
}

const RULES = 'kurallar'

const errorText = (error: unknown) =>
  error instanceof ApiError ? error.message : error instanceof Error ? error.message : 'Bir şeyler ters gitti.'

/** The month review agent's output: headline, the facts it chose (computed in code) and a tip. */
export function ReviewCard({ month, offerToCreate }: Props) {
  const [review, setReview] = useState<MonthReview | null | undefined>(undefined)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api
      .review(month)
      .then((r) => {
        if (active) setReview(r)
      })
      .catch((e: unknown) => {
        if (active) setError(errorText(e))
      })
    return () => {
      active = false
    }
  }, [month])

  async function generate(refresh: boolean) {
    setBusy(true)
    setError(null)
    try {
      setReview(await api.createReview(month, refresh))
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  if (review === undefined && !error) return null
  if (review === null && !offerToCreate) return null

  return (
    <section className="card review" aria-labelledby="review-title">
      <div className="card-head">
        <h2 id="review-title" className="card-title">{formatMonth(month)} değerlendirmesi</h2>
        {review && (
          <button type="button" className="link-button link-muted" disabled={busy} onClick={() => void generate(true)}>
            {busy ? 'Yazılıyor…' : 'Yenile'}
          </button>
        )}
      </div>

      {review ? (
        <>
          <p className="review-headline">{review.headline}</p>
          <ul className="review-points">
            {review.highlights.map((h) => (
              <li key={h}>{h}</li>
            ))}
          </ul>
          {review.tip && (
            <p className="review-tip">
              <span aria-hidden="true">💡</span> {review.tip}
            </p>
          )}
          <p className="hint">
            {review.generatedBy === RULES
              ? 'Kurallarla hazırlandı (yapay zekâ kullanılamadı).'
              : `Değerlendirme ajanı hazırladı (${review.generatedBy}). Rakamları kod hesapladı.`}
          </p>
        </>
      ) : (
        <>
          <p className="empty">Bu ayın değerlendirmesi henüz yok. Ajan ayı inceleyip öne çıkanları ve bir tavsiye yazar.</p>
          <button type="button" className="button button-soft" disabled={busy} onClick={() => void generate(false)}>
            {busy ? 'İnceleniyor…' : 'Değerlendir'}
          </button>
        </>
      )}

      {error && <p className="field-error" role="alert">{error}</p>}
    </section>
  )
}
