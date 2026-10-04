import type { Me } from '../api/types'
import { formatMonth } from '../lib/format'
import { NotificationBell } from './NotificationBell'

interface Props {
  me: Me
  month: string
  isCurrentMonth: boolean
  onPreviousMonth: () => void
  onNextMonth: () => void
  onCurrentMonth: () => void
  onOpenHousehold: () => void
  onLogout: () => void
}

const Chevron = ({ flip }: { flip?: boolean }) => (
  <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" style={flip ? { transform: 'scaleX(-1)' } : undefined}>
    <path d="M15 5l-7 7 7 7" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
)

export function TopBar(props: Props) {
  const { me, month, isCurrentMonth } = props

  return (
    <header className="topbar">
      <nav className="month-switcher" aria-label="Ay seçimi">
        <button type="button" className="icon-button" aria-label="Önceki ay" onClick={props.onPreviousMonth}>
          <Chevron />
        </button>
        <span className="month-label" aria-live="polite">{formatMonth(month)}</span>
        <button
          type="button"
          className="icon-button"
          aria-label="Sonraki ay"
          disabled={isCurrentMonth}
          onClick={props.onNextMonth}
        >
          <Chevron flip />
        </button>
        {!isCurrentMonth && (
          <button type="button" className="link-button" onClick={props.onCurrentMonth}>
            Bu aya dön
          </button>
        )}
      </nav>

      <div className="topbar-actions">
        <NotificationBell />
        <button type="button" className="pill" onClick={props.onOpenHousehold}>
          <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
            <path
              d="M4 11l8-6 8 6v8a1 1 0 0 1-1 1h-4v-5h-6v5H5a1 1 0 0 1-1-1z"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinejoin="round"
            />
          </svg>
          {me.household ? me.household.name : 'Kişisel'}
        </button>
        <span className="topbar-user" title={me.email}>{me.displayName}</span>
        <button type="button" className="link-button" onClick={props.onLogout}>
          Çıkış
        </button>
      </div>
    </header>
  )
}
