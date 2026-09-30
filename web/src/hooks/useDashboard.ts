import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import type { Expense, NewExpense, Weather } from '../api/types'
import { todayIso } from '../lib/format'

interface State {
  weather: Weather | null
  expenses: Expense[]
  error: string | null
  loading: boolean
}

/** Loads everything the home screen shows and reloads it after each change, so the weather is always recomputed server-side. */
export function useDashboard() {
  const [today, setToday] = useState(todayIso)
  const [state, setState] = useState<State>({ weather: null, expenses: [], error: null, loading: true })
  const month = today.slice(0, 7)

  // Bumping the version refetches; the effect owns the request so a stale response
  // (e.g. from before "today" changed) can never overwrite a newer one.
  const [version, setVersion] = useState(0)
  const reload = useCallback(() => setVersion((v) => v + 1), [])

  useEffect(() => {
    let active = true
    Promise.all([api.weather(today), api.expenses(month)])
      .then(([weather, expenses]) => {
        if (active) setState({ weather, expenses, error: null, loading: false })
      })
      .catch((error: unknown) => {
        if (active)
          setState((s) => ({ ...s, loading: false, error: error instanceof Error ? error.message : 'Yüklenemedi.' }))
      })
    return () => {
      active = false
    }
  }, [today, month, version])

  // An installed PWA can stay open for days: pick up a new "today" when the user comes back to it.
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState === 'visible') setToday(todayIso())
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [])

  const addExpense = useCallback(
    async (expense: NewExpense, key: string) => {
      await api.addExpense(expense, key)
      reload()
    },
    [reload],
  )

  const deleteExpense = useCallback(
    async (id: string) => {
      setState((s) => ({ ...s, expenses: s.expenses.filter((e) => e.id !== id) }))
      try {
        await api.deleteExpense(id)
      } finally {
        reload()
      }
    },
    [reload],
  )

  const setBudget = useCallback(
    async (amount: number) => {
      await api.setBudget(month, amount)
      reload()
    },
    [month, reload],
  )

  return { ...state, today, month, reload, addExpense, deleteExpense, setBudget }
}
