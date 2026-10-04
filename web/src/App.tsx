import { useState } from 'react'
import type { Me } from './api/types'
import { AddExpenseForm } from './components/AddExpenseForm'
import { AssistantPanel } from './components/AssistantPanel'
import { AuthScreen } from './components/AuthScreen'
import { BudgetDialog } from './components/BudgetDialog'
import { ExpenseList } from './components/ExpenseList'
import { ForecastStrip } from './components/ForecastStrip'
import { HouseholdDialog } from './components/HouseholdDialog'
import { MonthForecastCard } from './components/MonthForecastCard'
import { TasksCard } from './components/TasksCard'
import { TopBar } from './components/TopBar'
import { WeatherHero } from './components/WeatherHero'
import { useDashboard } from './hooks/useDashboard'
import { useSession } from './hooks/useSession'

export default function App() {
  const { session, login, register, logout, refresh, retry } = useSession()
  // Lives above the dashboard so it stays open while a household change remounts the dashboard.
  const [householdOpen, setHouseholdOpen] = useState(false)

  switch (session.status) {
    case 'loading':
      return (
        <main className="app app-center">
          <p className="loading">Gökyüzüne bakılıyor…</p>
        </main>
      )
    case 'offline':
      return (
        <main className="app app-center">
          <div className="card">
            <p className="field-error" role="alert">{session.message}</p>
            <button type="button" className="button button-primary" onClick={() => void retry()}>
              Tekrar dene
            </button>
          </div>
        </main>
      )
    case 'signedOut':
      return <AuthScreen notice={session.notice} onLogin={login} onRegister={register} />
    case 'signedIn':
      // Keyed by the active ledger: joining or leaving a household starts the dashboard afresh.
      return (
        <>
          <Dashboard
            key={session.me.household?.id ?? session.me.id}
            me={session.me}
            claimedExpenses={session.claimedExpenses}
            onOpenHousehold={() => setHouseholdOpen(true)}
            onLogout={() => {
              setHouseholdOpen(false)
              void logout()
            }}
          />
          <HouseholdDialog open={householdOpen} onClose={() => setHouseholdOpen(false)} onLedgerChanged={refresh} />
        </>
      )
  }
}

interface DashboardProps {
  me: Me
  claimedExpenses: number
  onOpenHousehold: () => void
  onLogout: () => void
}

function Dashboard({ me, claimedExpenses, onOpenHousehold, onLogout }: DashboardProps) {
  const dashboard = useDashboard()
  const [budgetOpen, setBudgetOpen] = useState(false)
  const { weather } = dashboard

  const topBar = (
    <TopBar
      me={me}
      month={dashboard.requestedMonth}
      isCurrentMonth={dashboard.requestedMonth === dashboard.today.slice(0, 7)}
      onPreviousMonth={() => dashboard.goToMonth(-1)}
      onNextMonth={() => dashboard.goToMonth(1)}
      onCurrentMonth={dashboard.goToCurrentMonth}
      onOpenHousehold={onOpenHousehold}
      onLogout={onLogout}
    />
  )

  if (!weather) {
    return (
      <main className="app">
        {topBar}
        <div className="app-center">
          {dashboard.error ? (
            <div className="card">
              <p className="field-error" role="alert">{dashboard.error}</p>
              <button type="button" className="button button-primary" onClick={dashboard.reload}>
                Tekrar dene
              </button>
            </div>
          ) : (
            <p className="loading">Gökyüzüne bakılıyor…</p>
          )}
        </div>
      </main>
    )
  }

  return (
    <main className="app" aria-busy={dashboard.loading}>
      {topBar}
      <WeatherHero weather={weather} isCurrentMonth={dashboard.isCurrentMonth} onEditBudget={() => setBudgetOpen(true)} />

      {claimedExpenses > 0 && (
        <p className="notice notice-inline">Bu cihazda daha önce girdiğin {claimedExpenses} harcama hesabına taşındı.</p>
      )}
      {dashboard.error && (
        <p className="banner" role="alert">
          {dashboard.error}
        </p>
      )}

      <div className="layout">
        <div className="column">
          <AddExpenseForm
            key={dashboard.asOf}
            today={dashboard.today}
            defaultDate={dashboard.asOf}
            onAdd={dashboard.addExpense}
          />
          <ForecastStrip
            days={weather.lastSevenDays}
            today={dashboard.today}
            title={dashboard.isCurrentMonth ? 'Son 7 günün havası' : 'Ayın son 7 günü'}
          />
          {dashboard.isCurrentMonth && <MonthForecastCard weather={weather} />}
        </div>
        <div className="column">
          <TasksCard me={me} today={dashboard.today} onExpenseAdded={dashboard.reload} />
          <AssistantPanel today={dashboard.today} />
          <ExpenseList
            month={dashboard.month}
            expenses={dashboard.expenses}
            meId={me.id}
            showAuthor={me.household !== null}
            onDelete={(id) => void dashboard.deleteExpense(id)}
          />
        </div>
      </div>

      <BudgetDialog
        open={budgetOpen}
        month={dashboard.month}
        current={weather.budget}
        onSave={dashboard.setBudget}
        onClose={() => setBudgetOpen(false)}
      />
    </main>
  )
}
