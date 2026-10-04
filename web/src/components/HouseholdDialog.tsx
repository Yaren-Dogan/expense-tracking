import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, api } from '../api/client'
import type { Household, InvitePreview } from '../api/types'

interface Props {
  open: boolean
  onClose: () => void
  /** Creating, joining or leaving switches the active ledger, so everything on screen must reload. */
  onLedgerChanged: () => Promise<void>
}

type Pending = { kind: 'leave' } | { kind: 'remove'; userId: string; name: string } | null

const errorText = (error: unknown) =>
  error instanceof ApiError
    ? (Object.values(error.fieldErrors)[0]?.[0] ?? error.message)
    : error instanceof Error
      ? error.message
      : 'Bir şeyler ters gitti.'

export function HouseholdDialog({ open, onClose, onLedgerChanged }: Props) {
  const ref = useRef<HTMLDialogElement>(null)
  const [household, setHousehold] = useState<Household | null | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [preview, setPreview] = useState<InvitePreview | null>(null)
  const [pending, setPending] = useState<Pending>(null)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) {
      setHousehold(undefined)
      setError(null)
      setPreview(null)
      setPending(null)
      setCode('')
      setName('')
      dialog.showModal()
      api
        .household()
        .then((h) => setHousehold(h ?? null))
        .catch((e: unknown) => setError(errorText(e)))
    } else if (!open && dialog.open) {
      dialog.close()
    }
  }, [open])

  async function run<T>(action: () => Promise<T>): Promise<T | undefined> {
    setBusy(true)
    setError(null)
    try {
      return await action()
    } catch (e) {
      setError(errorText(e))
      return undefined
    } finally {
      setBusy(false)
    }
  }

  async function create(event: FormEvent) {
    event.preventDefault()
    const created = await run(() => api.createHousehold(name.trim()))
    if (created) {
      setHousehold(created)
      await onLedgerChanged()
    }
  }

  async function lookUp(event: FormEvent) {
    event.preventDefault()
    const found = await run(() => api.previewInvite(code.trim()))
    if (found) setPreview(found)
  }

  async function join() {
    const joined = await run(() => api.joinHousehold(code.trim()))
    if (joined) {
      setHousehold(joined)
      setPreview(null)
      await onLedgerChanged()
    }
  }

  async function confirmPending() {
    if (!pending) return
    if (pending.kind === 'leave') {
      const left = await run(async () => {
        await api.leaveHousehold()
        return true
      })
      if (left) {
        setHousehold(null)
        await onLedgerChanged()
      }
    } else {
      const updated = await run(() => api.removeMember(pending.userId))
      if (updated) setHousehold(updated)
    }
    setPending(null)
  }

  async function regenerate() {
    const updated = await run(() => api.regenerateInviteCode())
    if (updated) setHousehold(updated)
  }

  async function copyCode(value: string) {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      setError('Kopyalanamadı; kodu elle seçip kopyalayabilirsin.')
    }
  }

  return (
    <dialog ref={ref} className="dialog dialog-wide" onClose={onClose} aria-labelledby="household-title">
      <div className="dialog-body">
        <div className="dialog-head">
          <h2 id="household-title" className="card-title">Ortak hane</h2>
          <button type="button" className="icon-button" aria-label="Kapat" onClick={onClose}>
            <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
        </div>

        {household === undefined && !error && <p className="loading">Yükleniyor…</p>}

        {household === null && !preview && (
          <>
            <p className="dialog-help">
              Eşinle ya da ailenle aynı bütçeyi ve harcama listesini paylaşın. Herkes tüm harcamaları görür, sadece
              kendi girdiğini silebilir.
            </p>

            <form className="stack" onSubmit={create}>
              <h3 className="section-title">Hane kur</h3>
              <div className="row">
                <label className="field grow">
                  <span className="visually-hidden">Hane adı</span>
                  <input value={name} maxLength={60} placeholder="Örn. Yılmaz ailesi" onChange={(e) => setName(e.target.value)} />
                </label>
                <button type="submit" className="button button-primary" disabled={busy || !name.trim()}>
                  Kur
                </button>
              </div>
            </form>

            <form className="stack" onSubmit={lookUp}>
              <h3 className="section-title">Davet koduyla katıl</h3>
              <div className="row">
                <label className="field grow">
                  <span className="visually-hidden">Davet kodu</span>
                  <input
                    className="code-input"
                    value={code}
                    maxLength={9}
                    autoCapitalize="characters"
                    autoComplete="off"
                    spellCheck={false}
                    placeholder="ABCD2345"
                    onChange={(e) => setCode(e.target.value.toUpperCase())}
                  />
                </label>
                <button type="submit" className="button button-soft" disabled={busy || !code.trim()}>
                  Devam
                </button>
              </div>
            </form>
          </>
        )}

        {household === null && preview && (
          <div className="stack confirm">
            <h3 className="confirm-title">Aynı haneden misiniz?</h3>
            <p>
              <strong>{preview.householdName}</strong>
              <br />
              <span className="muted">Üyeler: {preview.memberNames.join(', ')}</span>
            </p>
            <p className="dialog-help">
              Katılırsan bundan sonra girdiğin harcamalar ve bütçe hane üyeleriyle paylaşılır. Önceki kişisel
              harcamaların sende kalır, hanede görünmez.
            </p>
            <div className="dialog-actions">
              <button type="button" className="button button-ghost" onClick={() => setPreview(null)}>
                Hayır
              </button>
              <button type="button" className="button button-primary" disabled={busy} onClick={() => void join()}>
                Evet, katıl
              </button>
            </div>
          </div>
        )}

        {household && (
          <>
            <p className="household-name">{household.name}</p>

            <div className="stack">
              <h3 className="section-title">Davet kodu</h3>
              <div className="invite">
                <code className="invite-code">{household.inviteCode}</code>
                <button type="button" className="button button-soft" onClick={() => void copyCode(household.inviteCode)}>
                  {copied ? 'Kopyalandı' : 'Kopyala'}
                </button>
                {household.isOwner && (
                  <button type="button" className="button button-ghost" disabled={busy} onClick={() => void regenerate()}>
                    Yenile
                  </button>
                )}
              </div>
              <p className="hint">Kodu eşine ya da ailene gönder. Yenilersen eski kod çalışmaz.</p>
            </div>

            <div className="stack">
              <h3 className="section-title">Üyeler</h3>
              <ul className="members">
                {household.members.map((m) => (
                  <li key={m.userId} className="member">
                    <span className="member-avatar" aria-hidden="true">
                      {m.displayName.charAt(0).toLocaleUpperCase('tr-TR')}
                    </span>
                    <span className="member-name">
                      {m.displayName}
                      {m.isMe && <span className="muted"> (sen)</span>}
                    </span>
                    {m.role === 'Owner' && <span className="badge">Kurucu</span>}
                    {household.isOwner && !m.isMe && (
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => setPending({ kind: 'remove', userId: m.userId, name: m.displayName })}
                      >
                        Çıkar
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            </div>

            {pending ? (
              <div className="stack confirm">
                <p>
                  {pending.kind === 'leave'
                    ? 'Haneden ayrılırsan kişisel defterine dönersin. Hane harcamaları hanede kalır.'
                    : `${pending.name} haneden çıkarılsın mı? Girdiği harcamalar hanede kalır.`}
                </p>
                <div className="dialog-actions">
                  <button type="button" className="button button-ghost" onClick={() => setPending(null)}>
                    Vazgeç
                  </button>
                  <button type="button" className="button button-danger" disabled={busy} onClick={() => void confirmPending()}>
                    {pending.kind === 'leave' ? 'Ayrıl' : 'Çıkar'}
                  </button>
                </div>
              </div>
            ) : (
              <button type="button" className="button button-ghost button-leave" onClick={() => setPending({ kind: 'leave' })}>
                Haneden ayrıl
              </button>
            )}
          </>
        )}

        {error && <p className="field-error" role="alert">{error}</p>}
      </div>
    </dialog>
  )
}
