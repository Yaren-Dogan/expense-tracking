import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import type { Expense, NewExpense, Weather } from '../api/types'
import { lastDayOfMonth, shiftMonth, todayIso } from '../lib/format'

interface State {
  weather: Weather | null
  expenses: Expense[]
  error: string | null
  /** Which request produced this state; anything else on screen is still loading. */
  requestKey: string | null
  /** The month and date the data on screen belongs to; they lag the requested month until it has loaded. */
  shownMonth: string | null
  shownAsOf: string | null
}

/**
 * Loads everything the home screen shows for the viewed month and reloads it after each change, so the weather is
 * always recomputed server-side. A past month is viewed as of its last day: the weather is the month's close.
 */
export function useDashboard() {
  const [today, setToday] = useState(todayIso)
  const currentMonth = today.slice(0, 7)
  const [viewedMonth, setViewedMonth] = useState(currentMonth)
  const [state, setState] = useState<State>({
    weather: null,
    expenses: [],
    error: null,
    requestKey: null,
    shownMonth: null,
    shownAsOf: null,
  })

  // Future months have nothing to show; clamp if "today" moved back (device clock change) or never caught up.
  const requestedMonth = viewedMonth > currentMonth ? currentMonth : viewedMonth
  const requestedAsOf = requestedMonth === currentMonth ? today : lastDayOfMonth(requestedMonth)

  // Bumping the version refetches; the effect owns the request so a stale response
  // (e.g. from a month the user has already left) can never overwrite a newer one.
  const [version, setVersion] = useState(0)
  const reload = useCallback(() => setVersion((v) => v + 1), [])

  const requestKey = `${requestedAsOf}|${requestedMonth}|${version}`

  useEffect(() => {
    let active = true
    Promise.all([api.weather(requestedAsOf), api.expenses(requestedMonth)])
      .then(([weather, expenses]) => {
        if (active)
          setState({ weather, expenses, error: null, requestKey, shownMonth: requestedMonth, shownAsOf: requestedAsOf })
      })
      .catch((error: unknown) => {
        if (active)
          setState((s) => ({ ...s, requestKey, error: error instanceof Error ? error.message : 'Yüklenemedi.' }))
      })
    return () => {
      active = false
    }
  }, [requestedAsOf, requestedMonth, requestKey])

  // An installed PWA can stay open for days: pick up a new "today" when the user comes back to it.
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState === 'visible') setToday(todayIso())
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [])

  const goToMonth = useCallback(
    (delta: number) => setViewedMonth((m) => {
      const next = shiftMonth(m > currentMonth ? currentMonth : m, delta)
      return next > currentMonth ? currentMonth : next
    }),
    [currentMonth],
  )
  const goToCurrentMonth = useCallback(() => setViewedMonth(currentMonth), [currentMonth])

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
      } catch (error) {
        setState((s) => ({ ...s, error: error instanceof Error ? error.message : 'Silinemedi.' }))
      } finally {
        reload()
      }
    },
    [reload],
  )

  // Everything below acts on what is on screen, so a slow load never mixes one month's data with another's label.
  const month = state.shownMonth ?? requestedMonth
  const asOf = state.shownAsOf ?? requestedAsOf
  const isCurrentMonth = month === currentMonth

  const setBudget = useCallback(
    async (amount: number) => {
      await api.setBudget(month, amount)
      reload()
    },
    [month, reload],
  )

  return {
    weather: state.weather,
    expenses: state.expenses,
    error: state.error,
    loading: state.requestKey !== requestKey,
    today,
    requestedMonth,
    month,
    asOf,
    isCurrentMonth,
    goToMonth,
    goToCurrentMonth,
    reload,
    addExpense,
    deleteExpense,
    setBudget,
  }
}
