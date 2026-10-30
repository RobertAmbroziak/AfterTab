import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import { invitationLink, openInvitation, showEvent, showEvents } from './navigation'
import { Expenses, PersonAvatar } from './Expenses'
import { Settlement } from './Settlement'
import { money } from './expenseAllocation'
import ParticipantManager from './ParticipantManager'

function ErrorMessage({ error }) { return error ? <p className="auth-error" role="alert">{error}</p> : null }

export function Invitation({ token, session, onLogin }) {
  const [preview, setPreview] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const lock = useRef(false)
  useEffect(() => {
    let active = true
    api('invites/preview', { method: 'POST', body: { token } })
      .then(data => { if (active) setPreview(data) })
      .catch(reason => { if (active) setError(reason.message) })
    return () => { active = false }
  }, [token, session?.user.id])
  async function join() {
    if (lock.current) return
    lock.current = true; setBusy(true); setError('')
    try {
      const details = await api('invites/join', { method: 'POST', body: { token } })
      showEvent(details.event.id)
    } catch (reason) { setError(reason.message) }
    finally { lock.current = false; setBusy(false) }
  }
  return (
    <section className="workspace invite-card" aria-label="Zaproszenie do wydarzenia">
      <span className="badge">Zaproszenie</span>
      <h2>{preview ? preview.eventName : error ? 'Zaproszenie niedostępne' : 'Sprawdzanie zaproszenia…'}</h2>
      {preview && <>
        <p>Zaprasza: <strong>{preview.ownerName}</strong></p>
        {preview.alreadyMember ? <button className="primary-button" onClick={() => showEvent(preview.eventId)}>Otwórz wydarzenie</button>
          : !session ? <><p>Zaloguj się, aby dołączyć. Zachowamy to zaproszenie podczas logowania.</p><button className="primary-button" onClick={onLogin}>Zaloguj i dołącz</button></>
          : session.user.requiresDisplayName ? <p>Uzupełnij najpierw swoje imię powyżej.</p>
          : <button className="primary-button" onClick={join} disabled={busy}>{busy ? 'Dołączanie…' : 'Dołącz do wydarzenia'}</button>}
      </>}
      <ErrorMessage error={error} />
      <button className="text-button" onClick={showEvents}>Wróć do listy</button>
    </section>
  )
}

function PasteInvitation() {
  const [input, setInput] = useState('')
  const [error, setError] = useState('')
  function submit(e) {
    e.preventDefault()
    try { openInvitation(input); setError('') } catch (reason) { setError(reason.message) }
  }
  return <form className="app-form paste-invitation" onSubmit={submit}>
    <h3>Dołącz linkiem lub kodem</h3>
    <p>Masz kod lub skopiowany link? Wpisz go tutaj. Kliknięcie otrzymanego linku otwiera zaproszenie bez ręcznego wklejania. Przeglądarka komunikatora może korzystać z osobnej sesji.</p>
    <label>Link lub kod zaproszenia<input value={input} onChange={e => setInput(e.target.value)} required autoComplete="off" spellCheck={false} /></label>
    <button className="secondary-button">Sprawdź zaproszenie</button>
    <ErrorMessage error={error} />
  </form>
}

function CreateEvent({ onCreated, onCancel }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const lock = useRef(false)
  async function submit(event) {
    event.preventDefault()
    if (lock.current) return
    const fields = new FormData(event.currentTarget)
    lock.current = true; setBusy(true); setError('')
    try {
      const result = await api('events', { method: 'POST', body: { name: fields.get('name').trim(), description: fields.get('description').trim() } })
      onCreated(result)
    } catch (reason) { setError(reason.message) }
    finally { lock.current = false; setBusy(false) }
  }
  return <form className="app-form" onSubmit={submit}>
    <h2>Utwórz wydarzenie</h2>
    <label>Nazwa wydarzenia<input name="name" required maxLength={150} placeholder="Np. Pizza w piątek" autoFocus /></label>
    <label>Opis (opcjonalnie)<textarea name="description" maxLength={2000} rows={3} /></label>
    <div className="form-actions"><button className="primary-button" disabled={busy}>{busy ? 'Tworzenie…' : 'Utwórz i wygeneruj zaproszenie'}</button><button type="button" className="secondary-button" onClick={onCancel} disabled={busy}>Anuluj</button></div>
    <ErrorMessage error={error} />
  </form>
}

function EventDetails({ id, knownToken, onToken, user }) {
  const [details, setDetails] = useState(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [refresh, setRefresh] = useState(0)
  const [editing, setEditing] = useState(false)
  const [summary, setSummary] = useState(null)
  const [tab, setTab] = useState('expenses')
  const expenseActions = useRef(null)
  const linkInput = useRef(null)
  const lock = useRef(false)
  useEffect(() => {
    let active = true
    api(`events/${id}`).then(value => { if (active) setDetails(value) }).catch(reason => { if (active) setError(reason.message) })
    return () => { active = false }
  }, [id, refresh, user.displayName])
  useEffect(() => {
    const update = () => { if (document.visibilityState === 'visible') setRefresh(value => value + 1) }
    const timer = window.setInterval(update, 15000)
    window.addEventListener('focus', update)
    document.addEventListener('visibilitychange', update)
    return () => { window.clearInterval(timer); window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update) }
  }, [])
  const link = knownToken ? invitationLink(knownToken) : ''
  async function copy() {
    try { await navigator.clipboard.writeText(link); setMessage('Link skopiowany do schowka.') }
    catch { linkInput.current?.focus(); linkInput.current?.select(); setMessage('Zaznaczony link skopiuj ręcznie z pola powyżej.') }
  }
  async function share() {
    if (!navigator.share) return copy()
    try { await navigator.share({ title: details.event.name, text: 'Dołącz do wydarzenia w AfterTab', url: link }) }
    catch (reason) { if (reason.name !== 'AbortError') setMessage('Nie udało się udostępnić. Skopiuj link do schowka.') }
  }
  async function manageInvite(revoke) {
    const prompt = revoke ? 'Unieważnić link? Dotychczasowi uczestnicy pozostaną w wydarzeniu.' : 'Wygenerować nowy link? Wszystkie poprzednie linki przestaną działać. Dotychczasowi uczestnicy pozostaną w wydarzeniu.'
    if (lock.current || !window.confirm(prompt)) return
    lock.current = true; setBusy(true); setError(''); setMessage('')
    try {
      const result = await api(`events/${id}/invite`, { method: revoke ? 'DELETE' : 'POST' })
      onToken(id, result?.token || null)
      setMessage(revoke ? 'Link został unieważniony.' : 'Nowy link jest gotowy. Skopiuj go i zachowaj.')
    } catch (reason) { setError(reason.message) }
    finally { lock.current = false; setBusy(false) }
  }
  if (!details) return <section className="workspace"><p>{error || 'Wczytywanie wydarzenia…'}</p><button className="secondary-button" onClick={showEvents}>Moje wydarzenia</button></section>
  const eventUser = { ...user, participantId: details.participants.find(person => person.userId === user.id)?.participantId }
  const outgoing = summary?.transfers.filter(row => row.fromParticipantId === eventUser.participantId && !row.acknowledgement?.sent).reduce((sum, row) => sum + row.amountMinor, 0) || 0
  const incoming = summary?.transfers.filter(row => row.toParticipantId === eventUser.participantId && !row.acknowledgement?.received).reduce((sum, row) => sum + row.amountMinor, 0) || 0
  return <section className={`event-page tab-${tab}`} aria-label="Szczegóły wydarzenia">
    <div className="event-hero">
    <div className="section-heading"><button className="text-button" onClick={showEvents}>← Moje wydarzenia</button><button className="text-button" onClick={() => { setError(''); setRefresh(x => x + 1) }}>Odśwież</button></div>
    <div className="event-title"><h2>{details.event.name}</h2>{details.event.isOwner && <span className="badge">Twoje wydarzenie</span>}</div>
    <p className="event-meta">Autor: {details.event.ownerName}{details.event.isOwner ? ' · Twoje wydarzenie' : ''}</p>
    {details.event.status !== 'Active' && <p className="settlement-warning">Wydarzenie zamknięte — dodawanie pozycji, zmiana podziałów i dołączanie są zablokowane.</p>}
    {details.event.description && <p className="event-description">{details.event.description}</p>}
    <h3 className="sr-only">Uczestnicy ({details.participants.length})</h3>
    <ul className="participant-list">{details.participants.map(person => <li key={person.participantId}><PersonAvatar person={person} /><span>{person.displayName}</span>{person.isOwner && <span className="badge">Autor</span>}{person.isVirtual && <span className="badge virtual-badge">Bez konta</span>}</li>)}</ul>
    <div className="event-stats"><div><span>Łączne wydatki</span><strong>{summary ? money(summary.totalMinor) : '—'}</strong></div><div><span>Pozycje</span><strong>{summary?.expenseCount ?? '—'}</strong></div><div><span>Uczestnicy</span><strong>{details.participants.length}</strong></div></div>
    </div>
    {details.event.isOwner && <ParticipantManager eventId={id} participants={details.participants} active={details.event.status === 'Active'} editing={editing} onChanged={() => setRefresh(value => value + 1)} />}
    <div className="personal-balance"><span>{outgoing > 0 ? 'Do oddania' : incoming > 0 ? 'Do otrzymania' : 'Twoje przelewy'}</span><strong>{summary ? money(outgoing > 0 ? outgoing : incoming) : '—'}</strong><small>{summary?.isFinal ? 'Na podstawie zatwierdzonego rozliczenia i Twoich oznaczeń.' : 'Na podstawie bieżącego rozliczenia.'}</small></div>
    <div className="mobile-tabs" role="tablist" aria-label="Widok wydarzenia"><button role="tab" aria-selected={tab === 'expenses'} aria-controls="expenses-panel" id="expenses-tab" onClick={() => setTab('expenses')}>Wydatki</button><button role="tab" aria-selected={tab === 'settlement'} aria-controls="settlement-panel" id="settlement-tab" onClick={() => setTab('settlement')}>Rozliczenie</button></div>
    <div className="event-columns">
      <div id="expenses-panel" className="expenses-panel" aria-labelledby="expenses-tab"><Expenses ref={expenseActions} key={`${id}-${details.event.status}`} eventId={id} participants={details.participants} user={eventUser} isOwner={details.event.isOwner} refresh={refresh} active={details.event.status === 'Active'} onChanged={() => setRefresh(value => value + 1)} onEditing={setEditing} /></div>
      <div id="settlement-panel" className="settlement-panel" aria-labelledby="settlement-tab"><Settlement eventId={id} user={eventUser} isOwner={details.event.isOwner} refresh={refresh} onData={setSummary} onChanged={() => { setEditing(false); setRefresh(value => value + 1) }} editing={editing} /></div>
    </div>
    {details.event.status === 'Active' && !editing && <button className="primary-button mobile-add" onClick={() => { setTab('expenses'); expenseActions.current?.add(); document.getElementById('expenses-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' }) }}>＋ Dodaj pozycję</button>}
    {details.event.isOwner && details.event.status === 'Active' && <details className="event-invite-tools">
      <summary>Zaproś znajomych</summary>
      {link ? <><label>Link zaproszenia<input ref={linkInput} value={link} readOnly onFocus={e => e.target.select()} /></label><div className="form-actions"><button className="primary-button" onClick={copy}>Kopiuj link</button><button className="secondary-button" onClick={share}>Udostępnij</button></div></>
        : <p>Jeśli masz wcześniej skopiowany link, możesz nadal go udostępniać. Nowy link unieważni poprzedni.</p>}
      <div className="form-actions"><button className="secondary-button" disabled={busy} onClick={() => manageInvite(false)}>Wygeneruj nowy link</button><button className="text-button" disabled={busy} onClick={() => manageInvite(true)}>Unieważnij link</button></div>
      <p className="helper-text">Każda osoba z aktywnym linkiem może dołączyć po zalogowaniu. Link nie dopisuje uczestnika bez jego potwierdzenia.</p>
      <p role="status">{message}</p>
    </details>}
    <ErrorMessage error={error} />
  </section>
}

export function EventWorkspace({ route, user }) {
  const [items, setItems] = useState(null)
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [creating, setCreating] = useState(false)
  const [tokens, setTokens] = useState({})
  const [refresh, setRefresh] = useState(0)
  useEffect(() => {
    let active = true
    api(`events?page=${page}`).then(value => {
      if (active) {
        const last = Math.max(1, Math.ceil(value.totalCount / 10))
        if (page > last) { setPage(last); return }
        setItems(value.items); setTotal(value.totalCount)
      }
    }).catch(reason => { if (active) setError(reason.message) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [page, refresh, route.kind, user.displayName])
  function changePage(value) { setPage(value); setLoading(true); setError(''); setItems(null) }
  function onToken(id, token) { setTokens(previous => ({ ...previous, [id]: token })) }
  if (route.kind === 'event') return <EventDetails key={route.id} id={route.id} knownToken={tokens[route.id]} onToken={onToken} user={user} />
  return <section className="workspace" aria-label="Moje wydarzenia">
    <div className="section-heading"><h2>Moje wydarzenia</h2><button className="text-button" onClick={() => { setError(''); setRefresh(x => x + 1) }}>Odśwież</button></div>
    {creating ? <CreateEvent onCancel={() => setCreating(false)} onCreated={result => { onToken(result.details.event.id, result.inviteToken); setCreating(false); setPage(1); showEvent(result.details.event.id) }} />
      : <button className="primary-button" onClick={() => setCreating(true)}>Utwórz wydarzenie</button>}
    <ErrorMessage error={error} />
    {!items && !error ? <p>Wczytywanie wydarzeń…</p> : items?.length === 0 ? <p className="empty-state">Nie masz jeszcze wydarzeń. Utwórz pierwsze albo dołącz przez zaproszenie.</p>
      : <ul className="event-list">{items?.map(event => <li key={event.id} className={event.status === 'Active' ? 'event-open' : 'event-closed'}><button onClick={() => showEvent(event.id)}><strong>{event.name}</strong><span className={`event-status ${event.status === 'Active' ? 'is-open' : 'is-closed'}`}>{event.status === 'Active' ? 'Otwarte' : 'Zamknięte'}</span><span>{event.isOwner ? 'Autor' : 'Uczestnik'} · {event.participantCount} os. · {event.status === 'Active' ? 'Otwarte' : 'Zamknięte'}</span></button></li>)}</ul>}
    {total > 0 && <nav className="pagination" aria-label="Strony wydarzeń">
      <button className="secondary-button" disabled={loading || page <= 1} onClick={() => changePage(page - 1)}>← Poprzednia</button>
      <span role="status">Strona {page} z {Math.max(1, Math.ceil(total / 10))} · {total} wydarzeń</span>
      <button className="secondary-button" disabled={loading || page * 10 >= total} onClick={() => changePage(page + 1)}>Następna →</button>
    </nav>}
    <PasteInvitation />
  </section>
}
