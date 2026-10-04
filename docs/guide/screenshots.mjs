// Fills a running app (docker compose up) with a demo household and captures the screenshots the guide uses.
// Every run registers fresh demo accounts, so it never touches real data. Output: img/*.png
import { mkdir } from 'node:fs/promises'
import { chromium } from 'playwright'

const BASE = process.env.GUIDE_URL ?? 'http://localhost:8080'
const OUT = new URL('./img/', import.meta.url)
const stamp = Date.now()
await mkdir(OUT, { recursive: true })

const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {})
const mobile = { viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, locale: 'tr-TR', timezoneId: 'Europe/Istanbul' }
const desktop = { ...mobile, viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1.5 }

const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const today = new Date()
const day = (offset) => iso(new Date(today.getFullYear(), today.getMonth(), today.getDate() + offset))
const thisMonth = iso(today).slice(0, 7)
const lastMonthDate = new Date(today.getFullYear(), today.getMonth() - 1, 1)
const lastMonth = iso(lastMonthDate).slice(0, 7)
const inLastMonth = (d) => `${lastMonth}-${String(d).padStart(2, '0')}`
const shot = (name) => new URL(name, OUT).pathname

async function api(page, method, path, body) {
  const response = await page.request.fetch(BASE + path, { method, data: body })
  if (!response.ok()) throw new Error(`${method} ${path} -> ${response.status()} ${await response.text()}`)
  return response.status() === 204 ? null : response.json()
}

async function account(options, name) {
  const context = await browser.newContext(options)
  const page = await context.newPage()
  await page.goto(BASE)
  const me = await api(page, 'POST', '/api/account/register', {
    email: `${name.toLowerCase()}.${stamp}@ornek.test`, password: 'Parola123', displayName: name,
  })
  return { context, page, me }
}

// --- The sign-in screen, before anyone is signed in ---
{
  const context = await browser.newContext(mobile)
  const page = await context.newPage()
  await page.goto(BASE)
  await page.waitForSelector('role=tab[name="Giriş yap"]')
  await page.click('role=tab[name="Kayıt ol"]')
  await page.screenshot({ path: shot('auth.png') })
  await context.close()
}

// --- Demo household: Yaren (owner) and Ahmet ---
const yaren = await account(desktop, 'Yaren')
const household = await api(yaren.page, 'POST', '/api/households', { name: 'Doğan ailesi' })
const ahmet = await account(mobile, 'Ahmet')

// Ahmet's join flow, captured at the "Aynı haneden misiniz?" question.
await ahmet.page.reload()
await ahmet.page.click('.pill')
await ahmet.page.fill('.code-input', household.inviteCode)
await ahmet.page.click('button:has-text("Devam")')
await ahmet.page.waitForSelector('text=Aynı haneden misiniz?')
await ahmet.page.screenshot({ path: shot('household-confirm.png') })
await ahmet.page.click('button:has-text("Evet, katıl")')
await ahmet.page.waitForSelector('.member:nth-child(2)')
await ahmet.page.locator('dialog[open]').screenshot({ path: shot('household-panel.png') })
await ahmet.page.click('button[aria-label=Kapat]')

// Budgets and expenses for last month and this month.
await api(yaren.page, 'PUT', `/api/budgets/${lastMonth}`, { amount: 30000 })
await api(yaren.page, 'PUT', `/api/budgets/${thisMonth}`, { amount: 45000 })
const add = (who, amount, category, note, spentOn) => api(who.page, 'POST', '/api/expenses', { amount, category, note, spentOn })
for (const [who, amount, category, note, d] of [
  [yaren, 2350, 'Market', 'Migros haftalık', 3], [ahmet, 640, 'Transport', 'İstanbulkart dolum', 5],
  [yaren, 15000, 'Bills', 'Kira', 6], [ahmet, 1250, 'Food', 'Akşam yemeği', 12], [yaren, 890, 'Health', 'Eczane', 15],
  [ahmet, 2100, 'Clothing', 'Mont', 20], [yaren, 1830, 'Market', 'Pazar alışverişi', 24], [ahmet, 450, 'Entertainment', 'Sinema', 27],
]) await add(who, amount, category, note, inLastMonth(d))
const recent = [
  [yaren, 1480, 'Market', 'Migros', -4], [ahmet, 320, 'Transport', 'Taksi', -3], [yaren, 15000, 'Bills', 'Kira', -3],
  [ahmet, 260, 'Food', 'Öğle yemeği', -2], [yaren, 185, 'Food', 'Kahve ve simit', -1], [yaren, 95, 'Food', 'Kahve', 0],
].filter(([, , , , offset]) => day(offset).startsWith(thisMonth))
for (const [who, amount, category, note, offset] of recent) await add(who, amount, category, note, day(offset))

// Tasks: Yaren hands out the bills; Ahmet pays the internet bill.
const task = (who, title, assigneeId, dueOn) => api(who.page, 'POST', '/api/tasks', { title, assigneeId, dueOn, recurrence: 'Monthly', category: 'Bills' })
const internet = await task(yaren, 'İnternet faturası', ahmet.me.id, day(2))
await task(yaren, 'Elektrik faturası', yaren.me.id, day(6))
await task(yaren, 'Doğalgaz faturası', ahmet.me.id, day(1))
await task(ahmet, 'Aidat', yaren.me.id, day(-1))
await api(ahmet.page, 'POST', `/api/tasks/${internet.id}/complete`, { amount: 449.9 })

// --- Desktop dashboard (Yaren) ---
await yaren.page.reload()
await yaren.page.waitForSelector('.hero[data-sky]:not([data-sky=none])')
await yaren.page.waitForSelector('.task')
await yaren.page.screenshot({ path: shot('dashboard-desktop.png') })
await yaren.page.locator('.tasks').screenshot({ path: shot('tasks.png') })
await yaren.page.click('.bell-button')
await yaren.page.waitForSelector('.bell-item')
await yaren.page.locator('.bell-panel').screenshot({ path: shot('bell.png') })
await yaren.page.click('.bell-button')

// Past month: the month's close, and the review agent's write-up of it.
await yaren.page.click('button[aria-label="Önceki ay"]')
await yaren.page.waitForSelector('text=Ay kapanışı')
await yaren.page.waitForSelector('.expense')
await yaren.page.click('.review button:has-text("Değerlendir")')
await yaren.page.waitForSelector('.review-headline')
await yaren.page.screenshot({ path: shot('past-month.png') })
await yaren.page.locator('.review').screenshot({ path: shot('review.png') })

// --- Mobile views (Ahmet) ---
await ahmet.page.reload()
await ahmet.page.waitForSelector('.hero[data-sky]:not([data-sky=none])')
await ahmet.page.screenshot({ path: shot('dashboard-mobile.png') })
await ahmet.page.locator('.add-form').screenshot({ path: shot('add-expense.png') })
await ahmet.page.locator('section:has(#list-title)').screenshot({ path: shot('expense-list.png') })
const open = ahmet.page.locator('.task:has-text("Doğalgaz")')
await open.locator('button:has-text("Ödendi")').click()
await ahmet.page.fill('.pay-form input', '1.120,35')
await ahmet.page.locator('.tasks').screenshot({ path: shot('task-pay.png') })
await ahmet.page.click('.pay-form button:has-text("Vazgeç")')
await ahmet.page.locator('.strip').screenshot({ path: shot('strip.png') })

// Budget planner: a savings goal, the agent's proposal, before it is applied.
await ahmet.page.fill('.plan-form input[inputmode=decimal]', '20.000')
await ahmet.page.selectOption('.plan-form select', '6')
await ahmet.page.fill('.plan-form input[maxlength="100"]', 'Yaz tatili')
await ahmet.page.click('.plan-form button[type=submit]')
await ahmet.page.waitForSelector('.plan')
await ahmet.page.locator('.planner').screenshot({ path: shot('planner.png') })

// --- Adminer: the descriptions stored in the database, shown in the table structure ---
{
  const adminer = process.env.ADMINER_URL ?? 'http://localhost:8081'
  const page = await browser.newPage({ viewport: { width: 1280, height: 640 }, deviceScaleFactor: 1.5 })
  await page.goto(adminer)
  await page.selectOption('select[name="auth[driver]"]', 'pgsql')
  await page.fill('input[name="auth[server]"]', 'postgres')
  await page.fill('input[name="auth[username]"]', 'expense')
  await page.fill('input[name="auth[password]"]', process.env.POSTGRES_PASSWORD ?? 'expense_dev')
  await page.fill('input[name="auth[db]"]', 'expense_tracker')
  await page.click('input[type=submit]')
  await page.waitForLoadState('networkidle')
  await page.goto(`${adminer}/?pgsql=postgres&username=expense&db=expense_tracker&ns=public&table=tasks`)
  await page.screenshot({ path: shot('adminer.png') })
  await page.close()
}

await browser.close()
console.log('Ekran görüntüleri:', OUT.pathname)
