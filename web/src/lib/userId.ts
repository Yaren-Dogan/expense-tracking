const STORAGE_KEY = 'expense-tracker.user-id'

// MVP identity (docs/prd.md "Kullanıcı kimliği"): a random id kept on this device.
// Kept in memory as a fallback so the app still works when storage is blocked (private mode).
let memoryId: string | null = null

export function getUserId(): string {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) return stored
    const created = crypto.randomUUID()
    localStorage.setItem(STORAGE_KEY, created)
    return created
  } catch {
    memoryId ??= crypto.randomUUID()
    return memoryId
  }
}
