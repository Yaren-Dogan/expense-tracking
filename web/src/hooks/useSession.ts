import { useCallback, useEffect, useState } from 'react'
import { ApiError, api, setSessionExpiredHandler } from '../api/client'
import type { Me } from '../api/types'
import { forgetLegacyDeviceId, getLegacyDeviceId } from '../lib/deviceId'

export type Session =
  | { status: 'loading' }
  | { status: 'signedOut'; notice?: string }
  | { status: 'signedIn'; me: Me; claimedExpenses: number }
  | { status: 'offline'; message: string }

/** Who is signed in. The cookie is HttpOnly, so the only way to know is to ask the API. */
export function useSession() {
  const [session, setSession] = useState<Session>({ status: 'loading' })

  // Bumping the attempt re-asks the API (the "Tekrar dene" button when the server was unreachable).
  const [attempt, setAttempt] = useState(0)
  const retry = useCallback(() => setAttempt((a) => a + 1), [])

  useEffect(() => {
    let active = true
    api
      .me()
      .then((me) => {
        if (active) setSession({ status: 'signedIn', me, claimedExpenses: 0 })
      })
      .catch((error: unknown) => {
        if (!active) return
        if (error instanceof ApiError && error.status === 401) setSession({ status: 'signedOut' })
        else setSession({ status: 'offline', message: error instanceof Error ? error.message : 'Yüklenemedi.' })
      })
    return () => {
      active = false
    }
  }, [attempt])

  useEffect(() => {
    setSessionExpiredHandler(() => setSession({ status: 'signedOut', notice: 'Oturumun sona erdi, tekrar giriş yap.' }))
    return () => setSessionExpiredHandler(null)
  }, [])

  const signedIn = useCallback((me: Me) => {
    // The server has moved this device's old data into the account; the id has no further use.
    forgetLegacyDeviceId()
    setSession({ status: 'signedIn', me, claimedExpenses: me.claimedExpenses })
  }, [])

  const login = useCallback(
    async (email: string, password: string) =>
      signedIn(await api.login({ email, password, deviceUserId: getLegacyDeviceId() })),
    [signedIn],
  )

  const register = useCallback(
    async (email: string, password: string, displayName: string) =>
      signedIn(await api.register({ email, password, displayName, deviceUserId: getLegacyDeviceId() })),
    [signedIn],
  )

  const logout = useCallback(async () => {
    try {
      await api.logout()
    } finally {
      setSession({ status: 'signedOut' })
    }
  }, [])

  /** Re-reads the user after a household change, which switches the active ledger. */
  const refresh = useCallback(async () => {
    const me = await api.me()
    setSession({ status: 'signedIn', me, claimedExpenses: 0 })
  }, [])

  return { session, login, register, logout, refresh, retry }
}
