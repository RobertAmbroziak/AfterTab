import { t, formatCount } from './i18n.js'
import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import { invitationLink, openInvitation, showEvent, showEvents } from './navigation'
import { Expenses, PersonAvatar } from './Expenses'
import { Settlement } from './Settlement'
import { money } from './expenseAllocation'
import ParticipantManager from './ParticipantManager'
import { focusAndReveal } from './focusAndReveal'

function ErrorMessage({ error }) { return error ? <p className="auth-error" role="alert">{t(error)}</p> : null }

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
    <section className="workspace invite-card" aria-label={t("Zaproszenie do wydarzenia")}>
      <span className="badge">{t("Zaproszenie")}</span>
      <h2>{preview ? preview.eventName : error ? t("Zaproszenie niedostępne") : t("Sprawdzanie zaproszenia…")}</h2>
      {preview && <>
        <p>{t("Zaprasza:")} <strong>{preview.ownerName}</strong></p>
        {preview.alreadyMember ? <button className="primary-button" onClick={() => showEvent(preview.eventId)}>{t("Otwórz wydarzenie")}</button>
          : !session ? <><p>{t("Zaloguj się, aby dołączyć. Zachowamy to zaproszenie podczas logowania.")}</p><button className="primary-button" onClick={onLogin}>{t("Zaloguj i dołącz")}</button></>
          : session.user.requiresDisplayName ? <p>{t("Uzupełnij najpierw swoje imię powyżej.")}</p>
          : <button className="primary-button" onClick={join} disabled={busy}>{busy ? t("Dołączanie…") : t("Dołącz do wydarzenia")}</button>}
      </>}
      <ErrorMessage error={t(error)} />
      <button className="text-button" onClick={showEvents}>{t("Wróć do listy")}</button>
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
    <h3>{t("Dołącz linkiem lub kodem")}</h3>
    <p>{t("Masz kod lub skopiowany link? Wpisz go tutaj. Kliknięcie otrzymanego linku otwiera zaproszenie bez ręcznego wklejania. Przeglądarka komunikatora może korzystać z osobnej sesji.")}</p>
    <label>{t("Link lub kod zaproszenia")}<input value={input} onChange={e => setInput(e.target.value)} required autoComplete="off" spellCheck={false} /></label>
    <button className="secondary-button">{t("Sprawdź zaproszenie")}</button>
    <ErrorMessage error={t(error)} />
  </form>
}

function CreateEvent({ onCreated, onCancel }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const lock = useRef(false)
  const nameInput = useRef(null)
  useEffect(() => focusAndReveal(nameInput.current), [])
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
    <h2>{t("Utwórz wydarzenie")}</h2>
    <label>{t("Nazwa wydarzenia")}<input ref={nameInput} name="name" required maxLength={150} placeholder={t("Np. Pizza w piątek")} /></label>
    <label>{t("Opis (opcjonalnie)")}<textarea name="description" maxLength={2000} rows={3} /></label>
    <div className="form-actions"><button className="primary-button" disabled={busy}>{busy ? t("Tworzenie…") : t("Utwórz i wygeneruj zaproszenie")}</button><button type="button" className="secondary-button" onClick={onCancel} disabled={busy}>{t("Anuluj")}</button></div>
    <ErrorMessage error={t(error)} />
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
    try { await navigator.clipboard.writeText(link); setMessage(t("Link skopiowany do schowka.")) }
    catch { linkInput.current?.focus(); linkInput.current?.select(); setMessage(t("Zaznaczony link skopiuj ręcznie z pola powyżej.")) }
  }
  async function share() {
    if (!navigator.share) return copy()
    try { await navigator.share({ title: details.event.name, text: t("Dołącz do wydarzenia w AfterTab"), url: link }) }
    catch (reason) { if (reason.name !== 'AbortError') setMessage(t("Nie udało się udostępnić. Skopiuj link do schowka.")) }
  }
  async function manageInvite(revoke) {
    const prompt = revoke ? t("Unieważnić link? Dotychczasowi uczestnicy pozostaną w wydarzeniu.") : t("Wygenerować nowy link? Wszystkie poprzednie linki przestaną działać. Dotychczasowi uczestnicy pozostaną w wydarzeniu.")
    if (lock.current || !window.confirm(prompt)) return
    lock.current = true; setBusy(true); setError(''); setMessage('')
    try {
      const result = await api(`events/${id}/invite`, { method: revoke ? 'DELETE' : 'POST' })
      onToken(id, result?.token || null)
      setMessage(revoke ? t("Link został unieważniony.") : t("Nowy link jest gotowy. Skopiuj go i zachowaj."))
    } catch (reason) { setError(reason.message) }
    finally { lock.current = false; setBusy(false) }
  }
  if (!details) return <section className="workspace"><p>{t(error) || t("Wczytywanie wydarzenia…")}</p><button className="secondary-button" onClick={showEvents}>{t("Moje wydarzenia")}</button></section>
  const eventUser = { ...user, participantId: details.participants.find(person => person.userId === user.id)?.participantId }
  const outgoing = summary?.transfers.filter(row => row.fromParticipantId === eventUser.participantId && !row.acknowledgement?.sent).reduce((sum, row) => sum + row.amountMinor, 0) || 0
  const incoming = summary?.transfers.filter(row => row.toParticipantId === eventUser.participantId && !row.acknowledgement?.received).reduce((sum, row) => sum + row.amountMinor, 0) || 0
  return <section className={`event-page tab-${tab}`} aria-label={t("Szczegóły wydarzenia")}>
    <div className="event-hero">
    <div className="section-heading"><button className="text-button" onClick={showEvents}>{t("← Moje wydarzenia")}</button><button className="text-button" onClick={() => { setError(''); setRefresh(x => x + 1) }}>{t("Odśwież")}</button></div>
    <div className="event-title"><h2>{details.event.name}</h2>{details.event.isOwner && <span className="badge">{t("Twoje wydarzenie")}</span>}</div>
    <p className="event-meta">{t("Autor:")} {details.event.ownerName}{details.event.isOwner ? t(" · Twoje wydarzenie") : ''}</p>
    {details.event.status !== 'Active' && <p className="settlement-warning">{t("Wydarzenie zamknięte — dodawanie pozycji, zmiana podziałów i dołączanie są zablokowane.")}</p>}
    {details.event.description && <p className="event-description">{details.event.description}</p>}
    <h3 className="sr-only">{t("Uczestnicy (")}{details.participants.length})</h3>
    <ul className="participant-list">{details.participants.map(person => <li key={person.participantId}><PersonAvatar person={person} /><span>{person.displayName}</span>{person.isOwner && <span className="badge">{t("Autor")}</span>}{person.isVirtual && <span className="badge virtual-badge">{t("Bez konta")}</span>}</li>)}</ul>
    <div className="event-stats"><div><span>{t("Łączne wydatki")}</span><strong>{summary ? money(summary.totalMinor) : '—'}</strong></div><div><span>{t("Pozycje")}</span><strong>{summary?.expenseCount ?? '—'}</strong></div><div><span>{t("Uczestnicy")}</span><strong>{details.participants.length}</strong></div></div>
    </div>
    {details.event.isOwner && <ParticipantManager eventId={id} participants={details.participants} active={details.event.status === 'Active'} editing={editing} onChanged={() => setRefresh(value => value + 1)} />}
    <div className="personal-balance"><span>{outgoing > 0 ? t("Do oddania") : incoming > 0 ? t("Do otrzymania") : t("Twoje przelewy")}</span><strong>{summary ? money(outgoing > 0 ? outgoing : incoming) : '—'}</strong><small>{summary?.isFinal ? t("Na podstawie zatwierdzonego rozliczenia i Twoich oznaczeń.") : t("Na podstawie bieżącego rozliczenia.")}</small></div>
    <div className="mobile-tabs" role="tablist" aria-label={t("Widok wydarzenia")}><button role="tab" aria-selected={tab === 'expenses'} aria-controls="expenses-panel" id="expenses-tab" onClick={() => setTab('expenses')}>{t("Wydatki")}</button><button role="tab" aria-selected={tab === 'settlement'} aria-controls="settlement-panel" id="settlement-tab" onClick={() => setTab('settlement')}>{t("Rozliczenie")}</button></div>
    <div className="event-columns">
      <div id="expenses-panel" className="expenses-panel" aria-labelledby="expenses-tab"><Expenses ref={expenseActions} key={`${id}-${details.event.status}`} eventId={id} participants={details.participants} user={eventUser} isOwner={details.event.isOwner} refresh={refresh} active={details.event.status === 'Active'} onChanged={() => setRefresh(value => value + 1)} onEditing={setEditing} /></div>
      <div id="settlement-panel" className="settlement-panel" aria-labelledby="settlement-tab"><Settlement eventId={id} user={eventUser} isOwner={details.event.isOwner} refresh={refresh} onData={setSummary} onChanged={() => { setEditing(false); setRefresh(value => value + 1) }} editing={editing} /></div>
    </div>
    {details.event.status === 'Active' && !editing && <button className="primary-button mobile-add" onClick={() => { setTab('expenses'); expenseActions.current?.add(); document.getElementById('expenses-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' }) }}>{t("＋ Dodaj pozycję")}</button>}
    {details.event.isOwner && details.event.status === 'Active' && <details className="event-invite-tools">
      <summary><span>{t("Zaproś znajomych")}</span></summary>
      {link ? <><label>{t("Link zaproszenia")}<input ref={linkInput} value={link} readOnly onFocus={e => e.target.select()} /></label><div className="form-actions"><button className="primary-button" onClick={copy}>{t("Kopiuj link")}</button><button className="secondary-button" onClick={share}>{t("Udostępnij")}</button></div></>
        : <p>{t("Jeśli masz wcześniej skopiowany link, możesz nadal go udostępniać. Nowy link unieważni poprzedni.")}</p>}
      <div className="form-actions"><button className="secondary-button" disabled={busy} onClick={() => manageInvite(false)}>{t("Wygeneruj nowy link")}</button><button className="text-button" disabled={busy} onClick={() => manageInvite(true)}>{t("Unieważnij link")}</button></div>
      <p className="helper-text">{t("Każda osoba z aktywnym linkiem może dołączyć po zalogowaniu. Link nie dopisuje uczestnika bez jego potwierdzenia.")}</p>
      <p role="status">{t(message)}</p>
    </details>}
    <ErrorMessage error={t(error)} />
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
  return <section className="workspace" aria-label={t("Moje wydarzenia")}>
    <div className="section-heading"><h2>{t("Moje wydarzenia")}</h2><button className="text-button" onClick={() => { setError(''); setRefresh(x => x + 1) }}>{t("Odśwież")}</button></div>
    {creating ? <CreateEvent onCancel={() => setCreating(false)} onCreated={result => { onToken(result.details.event.id, result.inviteToken); setCreating(false); setPage(1); showEvent(result.details.event.id) }} />
      : <button className="primary-button" onClick={() => setCreating(true)}>{t("Utwórz wydarzenie")}</button>}
    <ErrorMessage error={t(error)} />
    {!items && !error ? <p>{t("Wczytywanie wydarzeń…")}</p> : items?.length === 0 ? <p className="empty-state">{t("Nie masz jeszcze wydarzeń. Utwórz pierwsze albo dołącz przez zaproszenie.")}</p>
      : <ul className="event-list">{items?.map(event => <li key={event.id} className={event.status === 'Active' ? 'event-open' : 'event-closed'}><button onClick={() => showEvent(event.id)}><strong>{event.name}</strong><span className={`event-status ${event.status === 'Active' ? 'is-open' : 'is-closed'}`}>{event.status === 'Active' ? t("Otwarte") : t("Zamknięte")}</span><span>{event.isOwner ? t("Autor") : t("Uczestnik")} · {event.participantCount} {t("os. ·")} {event.status === 'Active' ? t("Otwarte") : t("Zamknięte")}</span></button></li>)}</ul>}
    {total > 0 && <nav className="pagination" aria-label={t("Strony wydarzeń")}>
      <button className="secondary-button" disabled={loading || page <= 1} onClick={() => changePage(page - 1)}>{t("← Poprzednia")}</button>
      <span role="status">{t("Strona")} {page} {t("z")} {Math.max(1, Math.ceil(total / 10))} · {formatCount(total, 'events')}</span>
      <button className="secondary-button" disabled={loading || page * 10 >= total} onClick={() => changePage(page + 1)}>{t("Następna →")}</button>
    </nav>}
    <PasteInvitation />
  </section>
}
