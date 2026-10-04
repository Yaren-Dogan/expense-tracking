import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ApiError, api } from '../api/client'
import { CATEGORIES, type Category, type Me, type TaskItem, type TaskList } from '../api/types'
import { daysBetween, formatMoney, formatShortDate, parseAmount, toIsoDate } from '../lib/format'
import { CATEGORY_LABELS } from '../lib/weather'

interface Props {
  me: Me
  today: string
  /** A paid task adds an expense, so the weather and list must reload. */
  onExpenseAdded: () => void
}

interface Person {
  id: string
  name: string
}

const errorText = (error: unknown) =>
  error instanceof ApiError
    ? (Object.values(error.fieldErrors)[0]?.[0] ?? error.message)
    : error instanceof Error
      ? error.message
      : 'Bir şeyler ters gitti.'

function dueLabel(dueOn: string, today: string): { text: string; tone: 'late' | 'soon' | 'later' } {
  const days = daysBetween(today, dueOn)
  if (days < 0) return { text: `${-days} gün gecikti`, tone: 'late' }
  if (days === 0) return { text: 'Bugün', tone: 'soon' }
  if (days === 1) return { text: 'Yarın', tone: 'soon' }
  return { text: `${days} gün`, tone: days <= 3 ? 'soon' : 'later' }
}

const inAWeek = (today: string) => {
  const [y, m, d] = today.split('-').map(Number)
  return toIsoDate(new Date(y, m - 1, d + 7))
}

export function TasksCard({ me, today, onExpenseAdded }: Props) {
  const [tasks, setTasks] = useState<TaskList | null>(null)
  const [people, setPeople] = useState<Person[]>([{ id: me.id, name: me.displayName }])
  const [error, setError] = useState<string | null>(null)
  const [version, setVersion] = useState(0)
  const reload = useCallback(() => setVersion((v) => v + 1), [])

  const [adding, setAdding] = useState(false)
  const [paying, setPaying] = useState<string | null>(null)
  const [showDone, setShowDone] = useState(false)

  useEffect(() => {
    let active = true
    Promise.all([api.tasks(), api.household()])
      .then(([list, household]) => {
        if (!active) return
        setTasks(list)
        setPeople(
          household
            ? household.members.map((m) => ({ id: m.userId, name: m.isMe ? `${m.displayName} (sen)` : m.displayName }))
            : [{ id: me.id, name: `${me.displayName} (sen)` }],
        )
      })
      .catch((e: unknown) => {
        if (active) setError(errorText(e))
      })
    return () => {
      active = false
    }
  }, [me.id, me.displayName, version])

  // Someone else may have assigned or finished a task while the app was in the background.
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState === 'visible') reload()
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [reload])

  async function remove(task: TaskItem) {
    setError(null)
    try {
      await api.deleteTask(task.id)
      reload()
    } catch (e) {
      setError(errorText(e))
    }
  }

  const inHousehold = me.household !== null

  return (
    <section className="card tasks" aria-labelledby="tasks-title">
      <div className="card-head">
        <h2 id="tasks-title" className="card-title">Görevler</h2>
        {!adding && (
          <button type="button" className="link-button" onClick={() => setAdding(true)}>
            + Görev ekle
          </button>
        )}
      </div>

      {adding && (
        <NewTaskForm
          people={people}
          defaultAssignee={me.id}
          defaultDate={inAWeek(today)}
          today={today}
          onCancel={() => setAdding(false)}
          onCreated={() => {
            setAdding(false)
            reload()
          }}
        />
      )}

      {tasks === null && !error && <p className="loading">Yükleniyor…</p>}

      {tasks && tasks.open.length === 0 && !adding && (
        <p className="empty">
          {inHousehold
            ? 'Açık görev yok. Faturaları hane üyelerine dağıtabilirsin; son gün yaklaşınca bildirim gider.'
            : 'Açık görev yok. Fatura ve ödemeleri buraya ekle, son gün yaklaşınca hatırlatalım.'}
        </p>
      )}

      {tasks && tasks.open.length > 0 && (
        <ul className="task-list">
          {tasks.open.map((task) => {
            const due = dueLabel(task.dueOn, today)
            return (
              <li key={task.id} className="task">
                <div className="task-main">
                  <span className="task-avatar" aria-hidden="true">
                    {task.assigneeName.charAt(0).toLocaleUpperCase('tr-TR')}
                  </span>
                  <div className="task-text">
                    <span className="task-title">{task.title}</span>
                    <span className="task-meta">
                      {task.assigneeId === me.id ? 'Sen' : task.assigneeName} · {formatShortDate(task.dueOn)}
                      {task.recurrence === 'Monthly' && ' · her ay'}
                    </span>
                  </div>
                  <span className="due" data-tone={due.tone}>{due.text}</span>
                </div>

                {paying === task.id ? (
                  <PayForm
                    task={task}
                    onCancel={() => setPaying(null)}
                    onDone={(paid) => {
                      setPaying(null)
                      reload()
                      if (paid) onExpenseAdded()
                    }}
                  />
                ) : (
                  (task.canComplete || task.canDelete) && (
                    <div className="task-actions">
                      {task.canComplete && (
                        <button type="button" className="button button-soft button-small" onClick={() => setPaying(task.id)}>
                          {task.category === 'Bills' ? 'Ödendi' : 'Tamamlandı'}
                        </button>
                      )}
                      {task.canDelete && (
                        <button type="button" className="link-button link-muted" onClick={() => void remove(task)}>
                          Sil
                        </button>
                      )}
                    </div>
                  )
                )}
              </li>
            )
          })}
        </ul>
      )}

      {tasks && tasks.done.length > 0 && (
        <div className="done">
          <button type="button" className="link-button link-muted" aria-expanded={showDone} onClick={() => setShowDone((v) => !v)}>
            {showDone ? 'Tamamlananları gizle' : `Tamamlananlar (${tasks.done.length})`}
          </button>
          {showDone && (
            <ul className="task-list task-list-done">
              {tasks.done.map((task) => (
                <li key={task.id} className="task task-done">
                  <span className="task-check" aria-hidden="true">✓</span>
                  <div className="task-text">
                    <span className="task-title">{task.title}</span>
                    <span className="task-meta">
                      {task.completedByName}
                      {task.paidAmount !== null && ` · ${formatMoney(task.paidAmount)}`}
                      {task.completedAt && ` · ${formatShortDate(task.completedAt.slice(0, 10))}`}
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {error && <p className="field-error" role="alert">{error}</p>}
    </section>
  )
}

interface NewTaskFormProps {
  people: Person[]
  defaultAssignee: string
  defaultDate: string
  today: string
  onCancel: () => void
  onCreated: () => void
}

function NewTaskForm({ people, defaultAssignee, defaultDate, today, onCancel, onCreated }: NewTaskFormProps) {
  const [title, setTitle] = useState('')
  const [assigneeId, setAssigneeId] = useState(defaultAssignee)
  const [dueOn, setDueOn] = useState(defaultDate)
  const [monthly, setMonthly] = useState(true)
  const [category, setCategory] = useState<Category>('Bills')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!title.trim()) {
      setErrors({ title: 'Görevin adını yaz (örn. Elektrik faturası).' })
      return
    }
    setSaving(true)
    setErrors({})
    try {
      await api.createTask({ title: title.trim(), assigneeId, dueOn, recurrence: monthly ? 'Monthly' : 'None', category })
      onCreated()
    } catch (e) {
      if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0)
        setErrors(Object.fromEntries(Object.entries(e.fieldErrors).map(([k, v]) => [k, v[0]])))
      else setErrors({ form: errorText(e) })
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="task-form" onSubmit={submit} noValidate>
      <label className="field">
        <span className="field-label">Görev</span>
        <input
          autoFocus
          value={title}
          maxLength={80}
          placeholder="Örn. Elektrik faturası"
          onChange={(e) => setTitle(e.target.value)}
          aria-invalid={Boolean(errors.title)}
        />
        {errors.title && <span className="field-error">{errors.title}</span>}
      </label>
      <div className="row">
        <label className="field grow">
          <span className="field-label">Kim yapacak</span>
          <select value={assigneeId} onChange={(e) => setAssigneeId(e.target.value)}>
            {people.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
          {errors.assigneeId && <span className="field-error">{errors.assigneeId}</span>}
        </label>
        <label className="field">
          <span className="field-label">Son gün</span>
          <input type="date" value={dueOn} min={today} onChange={(e) => setDueOn(e.target.value)} />
        </label>
      </div>
      {errors.dueOn && <p className="field-error">{errors.dueOn}</p>}
      <div className="row">
        <label className="field grow">
          <span className="field-label">Ödenince kaydedilecek kategori</span>
          <select value={category} onChange={(e) => setCategory(e.target.value as Category)}>
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {CATEGORY_LABELS[c]}
              </option>
            ))}
          </select>
        </label>
        <label className="check">
          <input type="checkbox" checked={monthly} onChange={(e) => setMonthly(e.target.checked)} />
          Her ay tekrarla
        </label>
      </div>
      {errors.form && <p className="field-error" role="alert">{errors.form}</p>}
      <div className="dialog-actions">
        <button type="button" className="button button-ghost" onClick={onCancel}>
          Vazgeç
        </button>
        <button type="submit" className="button button-primary" disabled={saving}>
          {saving ? 'Kaydediliyor…' : 'Görevi ver'}
        </button>
      </div>
    </form>
  )
}

interface PayFormProps {
  task: TaskItem
  onCancel: () => void
  onDone: (paid: boolean) => void
}

/** The amount is optional: with it the payment is also recorded as an expense, without it the task is just ticked off. */
function PayForm({ task, onCancel, onDone }: PayFormProps) {
  const [amount, setAmount] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    const value = amount.trim() ? parseAmount(amount) : null
    if (value !== null && !(value > 0)) {
      setError('Geçerli bir tutar gir ya da boş bırak.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      await api.completeTask(task.id, value)
      onDone(value !== null)
    } catch (e) {
      setError(errorText(e))
      setSaving(false)
    }
  }

  return (
    <form className="pay-form" onSubmit={submit} noValidate>
      <label className="amount-field amount-field-small">
        <span className="visually-hidden">Ödenen tutar</span>
        <span className="amount-prefix" aria-hidden="true">₺</span>
        <input
          autoFocus
          inputMode="decimal"
          placeholder="Tutar (isteğe bağlı)"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          aria-invalid={Boolean(error)}
        />
      </label>
      <p className="hint">
        Tutarı yazarsan {CATEGORY_LABELS[task.category].toLocaleLowerCase('tr-TR')} harcaması olarak eklenir.
        {task.recurrence === 'Monthly' && ' Gelecek ayın görevi kendiliğinden açılır.'}
      </p>
      {error && <p className="field-error" role="alert">{error}</p>}
      <div className="dialog-actions">
        <button type="button" className="button button-ghost button-small" onClick={onCancel}>
          Vazgeç
        </button>
        <button type="submit" className="button button-primary button-small" disabled={saving}>
          {saving ? 'Kaydediliyor…' : 'Tamamla'}
        </button>
      </div>
    </form>
  )
}
