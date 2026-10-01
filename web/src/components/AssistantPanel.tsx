import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api } from '../api/client'
import type { ChatTurn } from '../api/types'

interface Message extends ChatTurn {
  tools?: string[]
  failed?: boolean
}

const SUGGESTIONS = ['Bu ay bütçem nasıl gidiyor?', 'Geçen ay en çok nereye harcadım?', 'Kahveye ne kadar veriyorum?']
const MAX_HISTORY = 20

const TOOL_LABELS: Record<string, string> = {
  get_budget_status: 'bütçe durumu',
  get_spending_by_category: 'kategori toplamları',
  list_expenses: 'harcama listesi',
  search_expenses: 'anlamsal arama',
}

export function AssistantPanel({ today }: { today: string }) {
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState('')
  const [thinking, setThinking] = useState(false)
  const logRef = useRef<HTMLOListElement>(null)

  useEffect(() => {
    logRef.current?.lastElementChild?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  }, [messages, thinking])

  async function ask(question: string) {
    const text = question.trim()
    if (!text || thinking) return

    const history: Message[] = [...messages, { role: 'user', content: text }]
    setMessages(history)
    setInput('')
    setThinking(true)
    try {
      // The server is stateless: send the recent conversation, without failed turns or UI-only fields.
      const turns: ChatTurn[] = history
        .filter((m) => !m.failed)
        .slice(-MAX_HISTORY)
        .map(({ role, content }) => ({ role, content }))
      const reply = await api.askAssistant(turns, today)
      setMessages([...history, { role: 'assistant', content: reply.reply, tools: reply.toolsUsed }])
    } catch (error) {
      const content = error instanceof Error ? error.message : 'Bir sorun oluştu.'
      setMessages([...history, { role: 'assistant', content, failed: true }])
    } finally {
      setThinking(false)
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    void ask(input)
  }

  return (
    <section className="card assistant" aria-labelledby="assistant-title">
      <div>
        <h2 id="assistant-title" className="card-title">Bütçe asistanı</h2>
        <p className="hint">Harcamaların hakkında soru sor. Cevaplar senin verilerinden hesaplanır.</p>
      </div>

      {messages.length > 0 && (
        <ol className="chat" ref={logRef} aria-live="polite">
          {messages.map((m, i) => (
            <li key={i} className={`bubble bubble-${m.role}${m.failed ? ' bubble-failed' : ''}`}>
              <p>{m.content}</p>
              {m.tools && m.tools.length > 0 && (
                <p className="bubble-tools">Kullandığı araçlar: {m.tools.map((t) => TOOL_LABELS[t] ?? t).join(', ')}</p>
              )}
            </li>
          ))}
          {thinking && (
            <li className="bubble bubble-assistant bubble-thinking">
              <p>Verilerine bakıyor…</p>
            </li>
          )}
        </ol>
      )}

      {messages.length === 0 && (
        <div className="suggestions">
          {SUGGESTIONS.map((s) => (
            <button key={s} type="button" className="chip" onClick={() => void ask(s)} disabled={thinking}>
              {s}
            </button>
          ))}
        </div>
      )}

      <form className="quick-entry" onSubmit={submit}>
        <label className="visually-hidden" htmlFor="assistant-input">Asistana sor</label>
        <input
          id="assistant-input"
          value={input}
          maxLength={1000}
          placeholder="Bir şey sor…"
          onChange={(e) => setInput(e.target.value)}
        />
        <button type="submit" className="button button-primary" disabled={thinking || !input.trim()}>
          Sor
        </button>
      </form>
    </section>
  )
}
