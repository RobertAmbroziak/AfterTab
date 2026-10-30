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
import './App.css'

export default function App() {
  const [session, setSession] = useState(null)
  const [busy, setBusy] = useState(true)
  const [checking, setChecking] = useState(true)
  const [error, setError] = useState('')
  const [remembered, setRemembered] = useState(hasRememberedGuest)
  const [guestForm, setGuestForm] = useState(false)
  const [accountForm, setAccountForm] = useState(hasEmailAction)
  const [editingName, setEditingName] = useState(false)
  const [menu, setMenu] = useState(false)
  const [route, setRoute] = useState(readRoute)
  const [attempt, setAttempt] = useState(0)
  const inFlight = useRef(false)
  useEffect(() => {
    let active = true
    captureInvitation()
    function syncRoute() { captureInvitation(); setRoute(readRoute()); if (hasEmailAction()) setAccountForm(true) }
    function expired() { setSession(null); setError('Sesja wygasła. Zaloguj się ponownie.') }
    window.addEventListener('hashchange', syncRoute)
    window.addEventListener('aftertab:session-expired', expired)
    restoreSession().then(value => { if (active) { setSession(value); setRemembered(hasRememberedGuest()); setRoute(readRoute()) } })
      .catch(reason => { if (active) setError(reason.message) }).finally(() => { if (active) { setBusy(false); setChecking(false) } })
    return () => { active = false; window.removeEventListener('hashchange', syncRoute); window.removeEventListener('aftertab:session-expired', expired) }
  }, [attempt])
  async function handleSession(action) {
    if (inFlight.current) return
    inFlight.current = true; setBusy(true); setChecking(false); setError('')
    try { setSession(await action() ?? null); setRemembered(hasRememberedGuest()); setGuestForm(false) }
    catch (reason) { setError(reason.message) }
    finally { inFlight.current = false; setBusy(false) }
  }
  function startLogin() { setMenu(false); if (busy) return; if (remembered) handleSession(() => loginAsGuest()); else setGuestForm(true) }
  function saveName(displayName) {
    handleSession(async () => { const user = await api('profile/display-name', { method: 'PUT', body: { displayName } }); setEditingName(false); return { ...session, user } })
  }
  function handleForget() {
    if (window.confirm('Zapomnieć gościa w tej przeglądarce? Bez połączonego konta utracisz możliwość powrotu do jego wydarzeń. Dane pozostaną w aplikacji.')) handleSession(forgetGuest)
  }
  const needsName = Boolean(session?.user.requiresDisplayName)
  return <main className="app-shell">
    <header className="app-header">
      <a className="brand" href="#/" onClick={e => { e.preventDefault(); showEvents() }}><img src="/pwa-192x192.png" alt="" width="38" height="38" /><span>AfterTab</span></a>
      <button className="header-events" aria-label="Wydarzenia" title="Wydarzenia" onClick={() => { showEvents(); setMenu(false) }}><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="3" /><path d="M7 3v4m10-4v4M3 11h18m-13 5h2m4 0h2" /></svg><span>Wydarzenia</span></button>
      <div className="header-install"><InstallButton /></div>
      <div className="account-control"><button className="account-button" aria-expanded={menu} aria-controls="account-menu" onClick={() => setMenu(value => !value)}><span className="account-avatar">{session ? [...session.user.displayName][0]?.toLocaleUpperCase('pl') : '☰'}</span><span>{session?.user.displayName || 'Konto'}</span><span aria-hidden="true">⌄</span></button>
        {menu && <section className="account-menu" id="account-menu" aria-label="Konto użytkownika">
          {session ? <><strong>{session.user.displayName}</strong><p className="helper-text">{session.user.isGuest ? 'Gość zapamiętany w tej przeglądarce' : 'Konto trwałe'}</p>{session.user.isGuest && <button className="text-button" onClick={() => { setAccountForm(true); setMenu(false) }}>Zachowaj konto / Zaloguj</button>}<button className="text-button" onClick={() => { setEditingName(true); setMenu(false) }}>Zmień imię</button><button className="text-button" disabled={busy} onClick={() => { setMenu(false); handleSession(logout) }}>Wyloguj</button></>
            : <><button className="primary-button" disabled={busy} onClick={startLogin}>{remembered ? 'WRÓĆ JAKO GOŚĆ' : 'ZALOGUJ JAKO GOŚĆ'}</button><button className="text-button" disabled={busy} onClick={() => { setAccountForm(true); setMenu(false) }}>Zaloguj / Utwórz konto</button></>}
          {remembered && <button className="text-button danger" disabled={busy} onClick={handleForget}>Zapomnij gościa</button>}
        </section>}
      </div>
    </header>
    <div className="page-content">
      {busy && (checking ? <SessionStatus key={attempt} /> : <div className="connection-status" role="status"><span className="spinner" />Łączenie z kontem…</div>)}
      {error && <div className="connection-status auth-error" role="alert"><span>{error}</span><button className="secondary-button" disabled={busy} onClick={() => { setError(''); setChecking(true); setBusy(true); setAttempt(value => value + 1) }}>Ponów połączenie</button></div>}
      {!session && !busy && route.kind !== 'invite' && <section className="login-card"><img src="/pwa-192x192.png" width="64" height="64" alt="" /><h1>Wspólne wyjścia.<br />Proste rozliczenia.</h1><p>Zaproś znajomych, dodaj wydatki i zobacz, kto komu oddaje.</p><div className="form-actions"><button className="primary-button" disabled={busy} onClick={startLogin}>{remembered ? 'WRÓĆ JAKO GOŚĆ' : 'ZALOGUJ JAKO GOŚĆ'}</button><button className="secondary-button" disabled={busy} onClick={() => { setAccountForm(true); setGuestForm(false) }}>ZALOGUJ / UTWÓRZ KONTO</button></div></section>}
      {accountForm && <AccountForm key={location.hash.startsWith('#account-') ? location.hash : 'account'} guest={Boolean(session?.user.isGuest)} onSession={() => setAttempt(value => value + 1)} onCancel={() => setAccountForm(false)} />}
      {!session && guestForm && <section className="workspace"><NameForm title="Zaloguj się jako gość" submitLabel="Zapisz imię i wejdź" onSubmit={name => handleSession(() => loginAsGuest(name))} onCancel={() => setGuestForm(false)} busy={busy} /></section>}
      {session && (needsName || editingName) && <section className="workspace"><NameForm key={`${session.user.id}-${editingName}`} initialName={needsName && session.user.displayName === 'Gość' ? '' : session.user.displayName} title={needsName ? 'Jak masz na imię?' : 'Zmień imię'} onSubmit={saveName} onCancel={needsName ? undefined : () => setEditingName(false)} busy={busy} /></section>}
      {route.kind === 'invite' && <Invitation key={route.token} token={route.token} session={session} onLogin={startLogin} />}
      {session && !needsName && route.kind !== 'invite' && <EventWorkspace key={session.user.id} route={route} user={session.user} />}
    </div>
    <footer className="app-footer" aria-label="Informacje o aplikacji">
      <a href="/privacy.html">Prywatność</a>
      <a href="/data-deletion.html">Usuwanie danych</a>
      <a href="/terms.html">Zasady korzystania</a>
      <a href="mailto:amb77ro@gmail.com">Kontakt</a>
    </footer>
  </main>
}
