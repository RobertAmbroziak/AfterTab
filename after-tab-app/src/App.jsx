import { t, getLocale, legalUrl, setLanguage } from './i18n.js'
import { useLanguage } from './useLanguage'
import { useEffect, useRef, useState } from 'react'
import { forgetGuest, hasRememberedGuest, loginAsGuest, logout, restoreSession } from './auth'
import { api } from './api'
import { captureInvitation, readRoute, showEvents } from './navigation'
import { EventWorkspace, Invitation } from './Events'
import NameForm from './NameForm'
import AccountForm from './AccountForm'
import { hasEmailAction } from './auth'
import InstallButton from './InstallButton'
import SessionStatus from './SessionStatus'
import { githubUrl, androidApkUrl } from './distributionLinks'
import './App.css'

export default function App() {
  const language = useLanguage()
  const [session, setSession] = useState(null)
  const [busy, setBusy] = useState(true)
  const [checking, setChecking] = useState(true)
  const [error, setError] = useState('')
  const [remembered, setRemembered] = useState(hasRememberedGuest)
  const [authForm, setAuthForm] = useState(() => hasEmailAction() ? 'account' : null)
  const [editingName, setEditingName] = useState(false)
  const [menu, setMenu] = useState(false)
  const [route, setRoute] = useState(readRoute)
  const [attempt, setAttempt] = useState(0)
  const inFlight = useRef(false)
  useEffect(() => {
    let active = true
    captureInvitation()
    function syncRoute() { captureInvitation(); setRoute(readRoute()); if (hasEmailAction()) setAuthForm('account') }
    function expired() { setSession(null); setError(t("Sesja wygasła. Zaloguj się ponownie.")) }
    window.addEventListener('hashchange', syncRoute)
    window.addEventListener('aftertab:session-expired', expired)
    restoreSession().then(value => { if (active) { setSession(value); setRemembered(hasRememberedGuest()); setRoute(readRoute()) } })
      .catch(reason => { if (active) setError(reason.message) }).finally(() => { if (active) { setBusy(false); setChecking(false) } })
    return () => { active = false; window.removeEventListener('hashchange', syncRoute); window.removeEventListener('aftertab:session-expired', expired) }
  }, [attempt])
  async function handleSession(action) {
    if (inFlight.current) return
    inFlight.current = true; setBusy(true); setChecking(false); setError('')
    try { setSession(await action() ?? null); setRemembered(hasRememberedGuest()); setAuthForm(null) }
    catch (reason) { setError(reason.message) }
    finally { inFlight.current = false; setBusy(false) }
  }
  function startLogin() { setMenu(false); if (busy) return; if (remembered) { setAuthForm(null); handleSession(() => loginAsGuest()) } else setAuthForm('guest') }
  function startAccountLogin() { setAuthForm('account'); setMenu(false) }
  function saveName(displayName) {
    handleSession(async () => { const user = await api('profile/display-name', { method: 'PUT', body: { displayName } }); setEditingName(false); return { ...session, user } })
  }
  function handleForget() {
    if (window.confirm(t("Zapomnieć gościa w tej przeglądarce? Bez połączonego konta utracisz możliwość powrotu do jego wydarzeń. Dane pozostaną w aplikacji."))) handleSession(forgetGuest)
  }
  const needsName = Boolean(session?.user.requiresDisplayName)
  return <main className="app-shell">
    <header className="app-header">
      <a className="brand" aria-label="AfterTab" href="#/" onClick={e => { e.preventDefault(); showEvents() }}><img src="/pwa-192x192.png" alt="" width="38" height="38" /><span>AfterTab</span></a>
      <button className="header-events" aria-label={t("Wydarzenia")} title={t("Wydarzenia")} onClick={() => { showEvents(); setMenu(false) }}><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M7 3v4m10-4v4M3 11h18m-13 5h2m4 0h2" /></svg><span>{t("Wydarzenia")}</span></button>
      <div className="header-install"><InstallButton /></div>
      <div className="language-switch" role="group" aria-label={language === 'pl' ? 'Język aplikacji' : 'App language'}>
        <button type="button" lang="pl" aria-label="Polski" aria-pressed={language === 'pl'} onClick={() => setLanguage('pl')}>PL</button>
        <button type="button" lang="en" aria-label="English" aria-pressed={language === 'en'} onClick={() => setLanguage('en')}>EN</button>
      </div>
      <div className="account-control"><button className="account-button" aria-label={session?.user.displayName || t("Konto")} aria-expanded={menu} aria-controls="account-menu" onClick={() => setMenu(value => !value)}><span className="account-avatar" aria-hidden="true">{session ? [...session.user.displayName][0]?.toLocaleUpperCase(getLocale()) : <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M5 6h14M5 12h14M5 18h14" /></svg>}</span><span>{session?.user.displayName || t("Konto")}</span></button>
        {menu && <section className="account-menu" id="account-menu" aria-label={t("Konto użytkownika")}>
          {session ? <><strong>{session.user.displayName}</strong><p className="helper-text">{session.user.isGuest ? t("Gość zapamiętany w tej przeglądarce") : t("Konto trwałe")}</p>{session.user.isGuest && <button className="text-button" onClick={startAccountLogin}>{t("Zachowaj konto / Zaloguj")}</button>}<button className="text-button" onClick={() => { setEditingName(true); setMenu(false) }}>{t("Zmień imię")}</button><button className="text-button" disabled={busy} onClick={() => { setMenu(false); handleSession(logout) }}>{t("Wyloguj")}</button></>
            : <><button className="primary-button" disabled={busy} onClick={startLogin}>{remembered ? t("WRÓĆ JAKO GOŚĆ") : t("ZALOGUJ JAKO GOŚĆ")}</button><button className="text-button" disabled={busy} onClick={startAccountLogin}>{t("Zaloguj / Utwórz konto")}</button></>}
          {remembered && <button className="text-button danger" disabled={busy} onClick={handleForget}>{t("Zapomnij gościa")}</button>}
        </section>}
      </div>
    </header>
    <div className="page-content">
      {busy && (checking ? <SessionStatus key={attempt} /> : <div className="connection-status" role="status"><span className="spinner" />{t("Łączenie z kontem…")}</div>)}
      {error && <div className="connection-status auth-error" role="alert"><span>{t(error)}</span><button className="secondary-button" disabled={busy} onClick={() => { setError(''); setChecking(true); setBusy(true); setAttempt(value => value + 1) }}>{t("Ponów połączenie")}</button></div>}
      {!session && !busy && route.kind !== 'invite' && <section className="login-card"><img src="/pwa-192x192.png" width="64" height="64" alt="" /><h1>{t("Wspólne wyjścia.")}<br />{t("Proste rozliczenia.")}</h1><p>{t("Zaproś znajomych, dodaj wydatki i zobacz, kto komu oddaje.")}</p><div className="form-actions"><button className="primary-button" disabled={busy} onClick={startLogin}>{remembered ? t("WRÓĆ JAKO GOŚĆ") : t("ZALOGUJ JAKO GOŚĆ")}</button><button className="secondary-button" disabled={busy} onClick={startAccountLogin}>{t("ZALOGUJ / UTWÓRZ KONTO")}</button></div></section>}
      {authForm === 'account' && <AccountForm key={location.hash.startsWith('#account-') ? location.hash : 'account'} guest={Boolean(session?.user.isGuest)} onSession={() => setAttempt(value => value + 1)} onCancel={() => setAuthForm(null)} />}
      {!session && authForm === 'guest' && <section className="workspace"><NameForm title={t("Zaloguj się jako gość")} submitLabel={t("Zapisz imię i wejdź")} onSubmit={name => handleSession(() => loginAsGuest(name))} onCancel={() => setAuthForm(null)} busy={busy} /></section>}
      {session && (needsName || editingName) && <section className="workspace"><NameForm key={`${session.user.id}-${editingName}`} initialName={needsName && session.user.displayName === 'Gość' ? '' : session.user.displayName} title={needsName ? t("Jak masz na imię?") : t("Zmień imię")} onSubmit={saveName} onCancel={needsName ? undefined : () => setEditingName(false)} busy={busy} /></section>}
      {route.kind === 'invite' && <Invitation key={route.token} token={route.token} session={session} onLogin={startLogin} />}
      {session && !needsName && route.kind !== 'invite' && <EventWorkspace key={session.user.id} route={route} user={session.user} />}
    </div>
    <footer className="app-footer" aria-label={t("Informacje o aplikacji")}>
      <a href={legalUrl('privacy')}>{t("Prywatność")}</a>
      <a href={legalUrl('data-deletion')}>{t("Usuwanie danych")}</a>
      <a href={legalUrl('terms')}>{t("Zasady korzystania")}</a>
      <a href="mailto:amb77ro@gmail.com">{t("Kontakt")}</a>
      {githubUrl && <a className="footer-icon-link" href={githubUrl} target="_blank" rel="noopener noreferrer" aria-label={t('Kod źródłowy na GitHub (otwiera nową kartę)')}>
        <svg width="20" height="20" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82A7.65 7.65 0 0 1 8 3.86c.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8Z" /></svg>
        <span>GitHub</span>
      </a>}
      {androidApkUrl && <a className="footer-icon-link" href={androidApkUrl} download aria-label={t('Pobierz aplikację Android (APK)')}>
        <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="m17.6 9.48 1.84-3.18a.38.38 0 0 0-.65-.38l-1.87 3.24a11.55 11.55 0 0 0-9.84 0L5.21 5.92a.38.38 0 1 0-.65.38L6.4 9.48A10.75 10.75 0 0 0 1 18h22a10.75 10.75 0 0 0-5.4-8.52ZM7 15.25a1 1 0 1 1 0-2 1 1 0 0 1 0 2Zm10 0a1 1 0 1 1 0-2 1 1 0 0 1 0 2Z" /></svg>
        <span>{t('Pobierz APK')}</span>
      </a>}
    </footer>
  </main>
}
