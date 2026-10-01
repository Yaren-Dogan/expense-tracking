import { useState } from 'react'
import { AddExpenseForm } from './components/AddExpenseForm'
import { AssistantPanel } from './components/AssistantPanel'
import { BudgetDialog } from './components/BudgetDialog'
import { ExpenseList } from './components/ExpenseList'
import { ForecastStrip } from './components/ForecastStrip'
import { MonthForecastCard } from './components/MonthForecastCard'
import { WeatherHero } from './components/WeatherHero'
import { useDashboard } from './hooks/useDashboard'

export default function App() {
  const dashboard = useDashboard()
  const [budgetOpen, setBudgetOpen] = useState(false)
  const { weather } = dashboard

  if (!weather) {
    return (
      <main className="app app-center">
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
      </main>
    )
  }

  return (
    <main className="app">
      <WeatherHero weather={weather} onEditBudget={() => setBudgetOpen(true)} />

      {dashboard.error && (
        <p className="banner" role="alert">
          {dashboard.error}
        </p>
      )}

      <div className="layout">
        <div className="column">
          <AddExpenseForm today={dashboard.today} onAdd={dashboard.addExpense} />
          <ForecastStrip days={weather.lastSevenDays} today={dashboard.today} />
          <MonthForecastCard weather={weather} />
        </div>
        <div className="column">
          <AssistantPanel today={dashboard.today} />
          <ExpenseList
            month={dashboard.month}
            expenses={dashboard.expenses}
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
