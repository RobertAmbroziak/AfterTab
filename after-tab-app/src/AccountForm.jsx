import { t, legalUrl } from './i18n.js'
import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import { loginAccount, loginExternal } from './auth'
import { focusAndReveal } from './focusAndReveal'
import { isNetworkError } from './networkStatus'

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
  const [showPassword, setShowPassword] = useState(false)
  const lock = useRef(false)
  const nameInput = useRef(null)
  const emailInput = useRef(null)
  useEffect(() => {
    let active = true
    api('auth/account/providers').then(value => { if (active) setProviders(value) }).catch(() => {})
    return () => { active = false }
  }, [])
  useEffect(() => {
    if (mode === 'login' || mode === 'register') return focusAndReveal(nameInput.current || emailInput.current)
  }, [mode])
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
  const titles = { login: t("Zaloguj się"), register: guest ? t("Zachowaj swoje konto i wydarzenia") : t("Utwórz konto"), forgot: t("Odzyskaj hasło"), resend: t("Wyślij potwierdzenie ponownie"), confirm: t("Potwierdź adres e-mail"), reset: t("Ustaw nowe hasło") }
  return <section className="workspace account-form-panel" aria-label={t("Logowanie i rejestracja")}>
    <h2>{titles[mode]}</h2>
    {guest && <p>{t("Rejestracja nowego konta zachowa Twoje wydarzenia. Logowanie do istniejącego konta otworzy wydarzenia tamtego konta.")}</p>}
    {(mode === 'login' || mode === 'register') && <div className="social-login" role="group" aria-label={t("Zaloguj przez zewnętrzne konto")}>
      <button className="social-login-button social-google" aria-label={t("Kontynuuj z Google")} disabled={busy || !providers.google} title={providers.google ? t("Kontynuuj z Google") : t("Logowanie Google jest obecnie niedostępne")} onClick={() => run(() => loginExternal('Google'))}>
        <svg viewBox="0 0 48 48" width="24" height="24" aria-hidden="true"><path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5Z"/><path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6C44.4 38.03 46.98 31.87 46.98 24.55Z"/><path fill="#FBBC05" d="M10.53 28.59A14.41 14.41 0 0 1 9.75 24c0-1.59.27-3.13.76-4.59l-7.98-6.19A23.87 23.87 0 0 0 0 24c0 3.87.93 7.53 2.56 10.78l7.97-6.19Z"/><path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.91-5.8l-7.73-6c-2.15 1.45-4.92 2.3-8.18 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48Z"/></svg>
      </button>
      <button className="social-login-button social-facebook" aria-label={t("Kontynuuj z Facebook")} disabled={busy || !providers.facebook} title={providers.facebook ? t("Kontynuuj z Facebook") : t("Logowanie Facebook jest obecnie niedostępne")} onClick={() => run(() => loginExternal('Facebook'))}>
        <svg viewBox="0 0 24 24" width="32" height="32" aria-hidden="true"><path fill="currentColor" d="M24 12.073C24 5.405 18.627 0 12 0S0 5.405 0 12.073C0 18.1 4.388 23.095 10.125 24v-8.437H7.078v-3.49h3.047v-2.66c0-3.025 1.792-4.697 4.533-4.697 1.312 0 2.686.236 2.686.236v2.972H15.83c-1.491 0-1.956.931-1.956 1.887v2.262h3.328l-.532 3.49h-2.796V24C19.612 23.095 24 18.1 24 12.073Z"/></svg>
      </button>
    </div>}
    <form className="app-form" onSubmit={submit}>
      {mode === 'register' && <label>{t("Imię")}<input ref={nameInput} name="displayName" required maxLength={100} autoComplete="given-name" /></label>}
      {!['confirm', 'reset'].includes(mode) && <label>{t("E-mail")}<input ref={emailInput} name="email" type="email" required maxLength={254} autoComplete="username" /></label>}
      {['login', 'register', 'reset'].includes(mode) && <label>{t("Hasło")}<span className="password-input-wrap"><input aria-label={t("Hasło")} name="password" type={showPassword ? 'text' : 'password'} required minLength={mode === 'login' ? undefined : 8} maxLength={128} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} /><button className="password-visibility-button" type="button" aria-label={t(showPassword ? "Ukryj hasło" : "Pokaż hasło")} aria-pressed={showPassword} title={t(showPassword ? "Ukryj hasło" : "Pokaż hasło")} onClick={() => setShowPassword(value => !value)}><svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{showPassword ? <><path d="M3 3l18 18"/><path d="M10.6 10.6a2 2 0 0 0 2.8 2.8"/><path d="M9.9 5.2A11.8 11.8 0 0 1 12 5c5 0 8.3 4.5 9 7-.3 1.1-1.1 2.5-2.4 3.7M6.2 6.2C3.9 7.7 2.4 10 2 12c.7 2.5 4 7 10 7 1 0 2-.2 2.9-.5"/></> : <><path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></>}</svg></button></span><small>{mode !== 'login' && t("Od 8 do 128 znaków.")}</small></label>}
      {mode === 'confirm' && <p>{t("Kliknij, aby potwierdzić adres. Link jest jednorazowy.")}</p>}
      {mode === 'register' && <p className="helper-text">{t("Przed utworzeniem konta zapoznaj się z")} <a href={legalUrl('terms')} target="_blank" rel="noopener noreferrer">{t("zasadami korzystania")}</a> {t("i")} <a href={legalUrl('privacy')} target="_blank" rel="noopener noreferrer">{t("polityką prywatności")}</a>.</p>}
      <p className="helper-text">{t("Logowanie jest zapamiętywane w tej przeglądarce do wylogowania lub wygaśnięcia sesji (maksymalnie rok).")}</p>
      <button className="primary-button" disabled={busy}>{busy ? t("Proszę czekać…") : mode === 'login' ? t("Zaloguj") : mode === 'register' ? t("Utwórz konto") : mode === 'confirm' ? t("Potwierdź e-mail") : mode === 'reset' ? t("Zapisz hasło") : t("Wyślij wiadomość")}</button>
    </form>
    {error && !isNetworkError(error) && <p className="auth-error" role="alert">{t(error)}</p>}
    {message && <p role="status">{t(message)}</p>}
    <div className="form-actions">
      {mode !== 'login' && <button className="text-button" disabled={busy} onClick={() => change('login')}>{t("Mam konto — zaloguj")}</button>}
      {mode !== 'register' && <button className="text-button" disabled={busy} onClick={() => change('register')}>{t("Utwórz konto")}</button>}
      {mode === 'login' && <><button className="text-button" disabled={busy} onClick={() => change('forgot')}>{t("Nie pamiętam hasła")}</button><button className="text-button" disabled={busy} onClick={() => change('resend')}>{t("Wyślij potwierdzenie e-mail")}</button></>}
      <button className="text-button" disabled={busy} onClick={() => { if (initial) history.replaceState(null, '', location.pathname + location.search); onCancel() }}>{t("Zamknij")}</button>
    </div>
  </section>
}
