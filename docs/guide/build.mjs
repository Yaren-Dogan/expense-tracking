// Builds the user guide PDF from kilavuz.html.
//   {{schema}}    <- schema.json (npm run schema): tables and the Turkish descriptions stored in the database
//   {{changelog}} <- changelog.json, newest first; its first entry is the guide's version
// Output: ../Butce-Hava-Durumu-Kilavuz.pdf
import { readFile, writeFile } from 'node:fs/promises'
import { chromium } from 'playwright'

const here = (name) => new URL(name, import.meta.url)
const escape = (text) =>
  String(text ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c])

const months = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık']
const trDate = (iso) => {
  const [y, m, d] = iso.split('-').map(Number)
  return `${d} ${months[m - 1]} ${y}`
}

const changelog = JSON.parse(await readFile(here('changelog.json'), 'utf8'))
const schema = JSON.parse(await readFile(here('schema.json'), 'utf8'))
const template = await readFile(here('kilavuz.html'), 'utf8')


const groupTitle = (name) =>
  name.startsWith('AspNet') ? 'Kimlik (ASP.NET Core Identity)' : name.startsWith('__') ? 'Sistem' : 'Uygulama'

// PostgreSQL's long type names, shortened to the aliases it also accepts.
const shortType = (type) =>
  type.replace('timestamp with time zone', 'timestamptz').replace(/character varying/g, 'varchar')

function schemaHtml() {
  let html = ''
  let group = ''
  for (const table of schema) {
    const g = groupTitle(table.name)
    if (g !== group) {
      group = g
      html += `<h3 class="schema-group">${escape(g)} tabloları</h3>`
    }
    const rows = table.columns
      .map(
        (c) => `<tr>
          <td><code>${escape(c.name)}</code>${c.pk ? ' <span class="pk">PK</span>' : ''}</td>
          <td><code class="type">${escape(shortType(c.type))}</code></td>
          <td class="center">${c.nullable ? 'evet' : ''}</td>
          <td>${escape(c.comment)}</td>
        </tr>`,
      )
      .join('')
    const indexes = (table.indexes ?? [])
      .map((i) => `<li>${i.unique ? '<b>unique</b> ' : ''}<code>${escape(i.definition)}</code></li>`)
      .join('')
    const fks = (table.foreign_keys ?? [])
      .map(
        (f) =>
          `<li><code>${escape(f.columns)}</code> → <code>${escape(f.references)}</code>${f.onDelete === 'cascade' ? ' (silinince birlikte silinir)' : ''}</li>`,
      )
      .join('')
    html += `<section class="db-table">
      <h4><code>${escape(table.name)}</code></h4>
      <p class="db-comment">${escape(table.comment)}</p>
      <table class="columns">
        <thead><tr><th style="width:24%">Kolon</th><th style="width:18%">Tip</th><th style="width:8%">Boş</th><th>Açıklama</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
      ${indexes || fks ? `<div class="db-extra">${indexes ? `<div><span class="label">Index'ler</span><ul>${indexes}</ul></div>` : ''}${fks ? `<div><span class="label">İlişkiler</span><ul>${fks}</ul></div>` : ''}</div>` : ''}
    </section>`
  }
  return html
}

const changelogHtml = () =>
  changelog
    .map(
      (e) => `<div class="release">
        <div class="release-head"><span class="badge">v${escape(e.version)}</span><b>${escape(e.title)}</b><span class="muted">${trDate(e.date)}</span></div>
        <ul>${e.items.map((i) => `<li>${escape(i)}</li>`).join('')}</ul>
      </div>`,
    )
    .join('')

const latest = changelog[0]
const tableCount = schema.length
const columnCount = schema.reduce((n, t) => n + t.columns.length, 0)

let html = template
  .replaceAll('{{version}}', escape(latest.version))
  .replaceAll('{{date}}', trDate(latest.date))
  .replaceAll('{{tableCount}}', String(tableCount))
  .replaceAll('{{columnCount}}', String(columnCount))
  .replace('{{schema}}', schemaHtml())
  .replace('{{changelog}}', changelogHtml())

// Number the chapters and sections and build the table of contents from them.
let chapter = 0
let section = 0
const toc = []
html = html.replace(/<h([23])([^>]*)>([\s\S]*?)<\/h\1>/g, (match, level, attrs, inner) => {
  if (attrs.includes('data-nonumber')) return match
  if (level === '2') {
    chapter++
    section = 0
  } else {
    if (attrs.includes('schema-group')) return match
    section++
  }
  const number = level === '2' ? `${chapter}` : `${chapter}.${section}`
  // Headings that are link targets keep their own id; the rest get one from their number.
  const own = attrs.match(/\sid="([^"]+)"/)
  const id = own ? own[1] : `s${number.replace('.', '-')}`
  toc.push({ level, number, id, title: inner.replace(/<[^>]+>/g, '') })
  return `<h${level}${attrs}${own ? '' : ` id="${id}"`}><span class="num">${number}</span> ${inner}</h${level}>`
})
const tocHtml = toc
  .map((t) => `<li class="toc-${t.level}"><a href="#${t.id}"><span class="num">${t.number}</span> ${escape(t.title)}</a></li>`)
  .join('')
html = html.replace('{{toc}}', `<ul class="toc">${tocHtml}</ul>`)

const staged = here('.build.html')
await writeFile(staged, html)

const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {})
const page = await browser.newPage()
await page.goto(staged.href, { waitUntil: 'networkidle' })
await page.evaluate(() => document.fonts.ready)
const output = here('../Butce-Hava-Durumu-Kilavuz.pdf')
await page.pdf({
  path: output.pathname,
  format: 'A4',
  printBackground: true,
  margin: { top: '16mm', bottom: '18mm', left: '16mm', right: '16mm' },
  displayHeaderFooter: true,
  headerTemplate: '<span></span>',
  footerTemplate: `<div style="width:100%;font-size:8px;color:#8b93a3;padding:0 16mm;display:flex;justify-content:space-between;font-family:sans-serif">
      <span>Bütçe Hava Durumu · Kılavuz v${escape(latest.version)}</span>
      <span><span class="pageNumber"></span> / <span class="totalPages"></span></span></div>`,
})
await browser.close()
console.log('PDF:', output.pathname)
