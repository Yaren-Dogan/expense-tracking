import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { AppNotification, NotificationKind } from '../api/types'
import { formatAgo } from '../lib/format'

const POLL_MS = 60_000

const ICONS: Record<NotificationKind, string> = {
  TaskAssigned: '📋',
  TaskDueSoon: '⏰',
  TaskOverdue: '⚠️',
  TaskCompleted: '✅',
  MonthReviewReady: '📊',
  BudgetPlanApplied: '🎯',
}

/** In-app notifications. Polled once a minute and whenever the app comes back to the foreground. */
export function NotificationBell() {
  const [items, setItems] = useState<AppNotification[]>([])
  const [unread, setUnread] = useState(0)
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)

  const load = useCallback(async () => {
    try {
      const list = await api.notifications()
      setItems(list.items)
      setUnread(list.unreadCount)
    } catch {
      // The bell is secondary: a failed poll keeps the last list and tries again on the next tick.
    }
  }, [])

  useEffect(() => {
    const first = setTimeout(() => void load(), 0)
    const timer = setInterval(() => void load(), POLL_MS)
    const onVisible = () => {
      if (document.visibilityState === 'visible') void load()
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      clearTimeout(first)
      clearInterval(timer)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [load])

  // Close when clicking outside or pressing Escape.
  useEffect(() => {
    if (!open) return
    const onPointer = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  async function toggle() {
    const opening = !open
    setOpen(opening)
    if (opening && unread > 0) {
      // Unread items stay highlighted while the panel is open; the server marks them read right away.
      setUnread(0)
      try {
        await api.markNotificationsRead()
      } catch {
        void load()
      }
    } else if (!opening) {
      setItems((list) => list.map((n) => ({ ...n, isRead: true })))
    }
  }

  return (
    <div className="bell" ref={rootRef}>
      <button
        type="button"
        className="icon-button bell-button"
        aria-label={unread > 0 ? `Bildirimler, ${unread} okunmamış` : 'Bildirimler'}
        aria-expanded={open}
        onClick={() => void toggle()}
      >
        <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
          <path
            d="M6 16V11a6 6 0 1 1 12 0v5l1.5 2h-15zM10 20a2 2 0 0 0 4 0"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinejoin="round"
            strokeLinecap="round"
          />
        </svg>
        {unread > 0 && <span className="bell-badge">{unread > 9 ? '9+' : unread}</span>}
      </button>

      {open && (
        <div className="bell-panel" role="dialog" aria-label="Bildirimler">
          <p className="bell-title">Bildirimler</p>
          {items.length === 0 ? (
            <p className="empty">Henüz bildirim yok. Sana görev verilince ya da son gün yaklaşınca burada görünür.</p>
          ) : (
            <ul className="bell-list">
              {items.map((n) => (
                <li key={n.id} className="bell-item" data-unread={!n.isRead}>
                  <span aria-hidden="true">{ICONS[n.kind]}</span>
                  <div>
                    <p className="bell-message">{n.message}</p>
                    <p className="bell-time">{formatAgo(n.createdAt)}</p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
