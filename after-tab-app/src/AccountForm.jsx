import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import { loginAccount, loginExternal } from './auth'

function emailAction() {
  const params = new URLSearchParams(location.hash.slice(1))
  if (params.has('account-confirm')) return { mode: 'confirm', token: params.get('account-confirm') }
  if (params.has('account-reset')) return { mode: 'reset', token: params.get('account-reset') }
  return null
}


export default function AccountForm({ guest = false, onSession, onCancel }) {
  const [initial] = useState(emailAction)
  const [mode, setMode] = useState(initial?.mode || (guest ? 'register' : 'login'))
  const [providers, setProviders] = useState({})
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const lock = useRef(false)
  useEffect(() => {
    let active = true
    api('auth/account/providers').then(value => { if (active) setProviders(value) }).catch(() => {})
    return () => { active = false }
  }, [])
  function change(value) {
    setMode(value); setError(''); setMessage('')
    if (initial) history.replaceState(null, '', location.pathname + location.search)
  }
  async function run(action) {
    if (lock.current) return
    lock.current = true; setBusy(true); setError(''); setMessage('')
    try { await action() } catch (reason) { setError(reason.message) }
    finally { lock.current = false; setBusy(false) }
  }
  function submit(event) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    run(async () => {
      const email = data.get('email')?.trim()
      const password = data.get('password')
      if (mode === 'login') { await loginAccount(email, password); return }
      const path = mode === 'register' ? 'register' : mode === 'forgot' ? 'forgot-password' : mode === 'resend' ? 'resend-confirmation' : 'complete-email'
      const body = mode === 'register' ? { email, password, displayName: data.get('displayName').trim() }
        : mode === 'confirm' || mode === 'reset' ? { token: initial?.token, password: mode === 'reset' ? password : null } : { email }
      const result = await api(`auth/account/${path}`, { method: 'POST', body })
      setMessage(result.message)
      if (mode === 'register') onSession?.()
      if (mode === 'confirm' || mode === 'reset') {
        history.replaceState(null, '', location.pathname + location.search)
        setMode('login')
      }
    })
  }
  const titles = { login: 'Zaloguj się', register: guest ? 'Zachowaj swoje konto i wydarzenia' : 'Utwórz konto', forgot: 'Odzyskaj hasło', resend: 'Wyślij potwierdzenie ponownie', confirm: 'Potwierdź adres e-mail', reset: 'Ustaw nowe hasło' }
  return <section className="workspace account-form-panel" aria-label="Logowanie i rejestracja">
    <h2>{titles[mode]}</h2>
    {guest && <p>Rejestracja nowego konta zachowa Twoje wydarzenia. Logowanie do istniejącego konta otworzy wydarzenia tamtego konta.</p>}
    {(mode === 'login' || mode === 'register') && <div className="social-login" role="group" aria-label="Zaloguj przez zewnętrzne konto">
      <button className="social-login-button social-google" aria-label="Kontynuuj z Google" disabled={busy || !providers.google} title={providers.google ? 'Kontynuuj z Google' : 'Logowanie Google jest obecnie niedostępne'} onClick={() => run(() => loginExternal('Google'))}>
        <svg viewBox="0 0 48 48" width="24" height="24" aria-hidden="true"><path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5Z"/><path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6C44.4 38.03 46.98 31.87 46.98 24.55Z"/><path fill="#FBBC05" d="M10.53 28.59A14.41 14.41 0 0 1 9.75 24c0-1.59.27-3.13.76-4.59l-7.98-6.19A23.87 23.87 0 0 0 0 24c0 3.87.93 7.53 2.56 10.78l7.97-6.19Z"/><path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.91-5.8l-7.73-6c-2.15 1.45-4.92 2.3-8.18 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48Z"/></svg>
      </button>
      <button className="social-login-button social-facebook" aria-label="Kontynuuj z Facebook" disabled={busy || !providers.facebook} title={providers.facebook ? 'Kontynuuj z Facebook' : 'Logowanie Facebook jest obecnie niedostępne'} onClick={() => run(() => loginExternal('Facebook'))}>
        <svg viewBox="0 0 24 24" width="32" height="32" aria-hidden="true"><path fill="currentColor" d="M24 12.073C24 5.405 18.627 0 12 0S0 5.405 0 12.073C0 18.1 4.388 23.095 10.125 24v-8.437H7.078v-3.49h3.047v-2.66c0-3.025 1.792-4.697 4.533-4.697 1.312 0 2.686.236 2.686.236v2.972H15.83c-1.491 0-1.956.931-1.956 1.887v2.262h3.328l-.532 3.49h-2.796V24C19.612 23.095 24 18.1 24 12.073Z"/></svg>
      </button>
    </div>}
    <form className="app-form" onSubmit={submit}>
      {mode === 'register' && <label>Imię<input name="displayName" required maxLength={100} autoComplete="given-name" /></label>}
      {!['confirm', 'reset'].includes(mode) && <label>E-mail<input name="email" type="email" required maxLength={254} autoComplete="username" /></label>}
      {['login', 'register', 'reset'].includes(mode) && <label>Hasło<input aria-label="Hasło" name="password" type="password" required minLength={mode === 'login' ? undefined : 12} maxLength={128} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} /><small>{mode !== 'login' && 'Od 12 do 128 znaków.'}</small></label>}
      {mode === 'confirm' && <p>Kliknij, aby potwierdzić adres. Link jest jednorazowy.</p>}
      {mode === 'register' && <p className="helper-text">Przed utworzeniem konta zapoznaj się z <a href="/terms.html" target="_blank" rel="noopener noreferrer">zasadami korzystania</a> i <a href="/privacy.html" target="_blank" rel="noopener noreferrer">polityką prywatności</a>.</p>}
      <p className="helper-text">Logowanie jest zapamiętywane w tej przeglądarce do wylogowania lub wygaśnięcia sesji (maksymalnie rok).</p>
      <button className="primary-button" disabled={busy}>{busy ? 'Proszę czekać…' : mode === 'login' ? 'Zaloguj' : mode === 'register' ? 'Utwórz konto' : mode === 'confirm' ? 'Potwierdź e-mail' : mode === 'reset' ? 'Zapisz hasło' : 'Wyślij wiadomość'}</button>
    </form>
    {error && <p className="auth-error" role="alert">{error}</p>}
    {message && <p role="status">{message}</p>}
    <div className="form-actions">
      {mode !== 'login' && <button className="text-button" disabled={busy} onClick={() => change('login')}>Mam konto — zaloguj</button>}
      {mode !== 'register' && <button className="text-button" disabled={busy} onClick={() => change('register')}>Utwórz konto</button>}
      {mode === 'login' && <><button className="text-button" disabled={busy} onClick={() => change('forgot')}>Nie pamiętam hasła</button><button className="text-button" disabled={busy} onClick={() => change('resend')}>Wyślij potwierdzenie e-mail</button></>}
      <button className="text-button" disabled={busy} onClick={() => { if (initial) history.replaceState(null, '', location.pathname + location.search); onCancel() }}>Zamknij</button>
    </div>
  </section>
}
