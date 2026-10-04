import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { WeatherIcon } from './WeatherIcon'

interface Props {
  notice?: string
  onLogin: (email: string, password: string) => Promise<void>
  onRegister: (email: string, password: string, displayName: string) => Promise<void>
}

type Mode = 'login' | 'register'

export function AuthScreen({ notice, onLogin, onRegister }: Props) {
  const [mode, setMode] = useState<Mode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)

  function switchTo(next: Mode) {
    setMode(next)
    setErrors({})
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setErrors({})
    try {
      if (mode === 'login') await onLogin(email.trim(), password)
      else await onRegister(email.trim(), password, displayName.trim())
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0)
        setErrors(Object.fromEntries(Object.entries(error.fieldErrors).map(([k, v]) => [k, v[0]])))
      else setErrors({ form: error instanceof Error ? error.message : 'Bir şeyler ters gitti.' })
    } finally {
      setBusy(false)
    }
  }

  const isRegister = mode === 'register'

  return (
    <main className="app app-center">
      <section className="card auth" aria-labelledby="auth-title">
        <div className="auth-brand">
          <WeatherIcon condition="PartlyCloudy" size={64} />
          <div>
            <h1 id="auth-title" className="auth-title">Bütçe Hava Durumu</h1>
            <p className="hint">Bütçenin bugünkü havasına bak.</p>
          </div>
        </div>

        <div className="segmented" role="tablist" aria-label="Giriş ya da kayıt">
          <button type="button" role="tab" aria-selected={!isRegister} onClick={() => switchTo('login')}>
            Giriş yap
          </button>
          <button type="button" role="tab" aria-selected={isRegister} onClick={() => switchTo('register')}>
            Kayıt ol
          </button>
        </div>

        {notice && <p className="notice">{notice}</p>}

        <form className="auth-form" onSubmit={submit} noValidate>
          {isRegister && (
            <label className="field">
              <span className="field-label">Adın</span>
              <input
                value={displayName}
                maxLength={40}
                autoComplete="given-name"
                placeholder="Hane üyeleri seni bu adla görür"
                onChange={(e) => setDisplayName(e.target.value)}
                aria-invalid={Boolean(errors.displayName)}
              />
              {errors.displayName && <span className="field-error">{errors.displayName}</span>}
            </label>
          )}
          <label className="field">
            <span className="field-label">E-posta</span>
            <input
              type="email"
              value={email}
              autoComplete="email"
              inputMode="email"
              onChange={(e) => setEmail(e.target.value)}
              aria-invalid={Boolean(errors.email)}
            />
            {errors.email && <span className="field-error">{errors.email}</span>}
          </label>
          <label className="field">
            <span className="field-label">Şifre</span>
            <input
              type="password"
              value={password}
              autoComplete={isRegister ? 'new-password' : 'current-password'}
              onChange={(e) => setPassword(e.target.value)}
              aria-invalid={Boolean(errors.password)}
            />
            {isRegister && !errors.password && <span className="hint">En az 8 karakter, en az bir rakam.</span>}
            {errors.password && <span className="field-error">{errors.password}</span>}
          </label>
          {errors.form && <p className="field-error" role="alert">{errors.form}</p>}
          <button type="submit" className="button button-primary" disabled={busy}>
            {busy ? 'Bekle…' : isRegister ? 'Hesap oluştur' : 'Giriş yap'}
          </button>
        </form>
      </section>
    </main>
  )
}
