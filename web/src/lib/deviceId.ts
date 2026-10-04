const STORAGE_KEY = 'expense-tracker.user-id'

// Before accounts existed, data was tied to a random id kept on this device (docs/prd-accounts.md
// "Cihazdaki eski veriyi taşıma"). The id is sent once on register/login so the server can move that data
// into the account, then forgotten. A new id is never created.

export function getLegacyDeviceId(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

export function forgetLegacyDeviceId(): void {
  try {
    localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Storage blocked (private mode): there was nothing to forget.
  }
}
