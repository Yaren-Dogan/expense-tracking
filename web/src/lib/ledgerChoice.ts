export type LedgerChoice = 'household' | 'personal'

const STORAGE_KEY = 'expense-tracker.ledger'

// Which ledger a household member is looking at, kept per device: the phone can stay on the personal ledger while the
// laptop shows the household. In memory when storage is blocked (private mode), so switching still works per session.
let memory: LedgerChoice = 'household'

export function getLedgerChoice(): LedgerChoice {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'personal' ? 'personal' : 'household'
  } catch {
    return memory
  }
}

export function setLedgerChoice(choice: LedgerChoice): void {
  memory = choice
  try {
    localStorage.setItem(STORAGE_KEY, choice)
  } catch {
    // Storage blocked: the in-memory choice is used.
  }
}
