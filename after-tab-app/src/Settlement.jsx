import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import { money } from './expenseAllocation'

export function Settlement({ eventId, user, isOwner, refresh, onChanged, onData, editing }) {
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const lock = useRef(false)
  useEffect(() => {
    let current = true
    api(`events/${eventId}/settlement`).then(result => { if (current) { setData(result); onData(result); setError('') } }).catch(reason => { if (current) setError(reason.message) })
    return () => { current = false }
  }, [eventId, refresh, onData])
  async function mark(row, side, value) {
    if (lock.current) return
    lock.current = true; setBusy(true); setError('')
    try { const result = await api(`events/${eventId}/settlement/confirmation`, { method: 'PUT', body: { version: data.version, fromParticipantId: row.fromParticipantId, toParticipantId: row.toParticipantId, amountMinor: row.amountMinor, side, value } }); setData(result); onData(result); onChanged() }
    catch (reason) { setError(reason.message) }
    finally { lock.current = false; setBusy(false) }
  }
  async function changeStatus() {
    if (lock.current) return
    const close = !data.isFinal
    if (!window.confirm(close ? 'Zamknąć wydarzenie i zatwierdzić rozliczenie zapisanych pozycji? Dodawanie i zmiana podziałów zostaną zablokowane.' : 'Otworzyć wydarzenie ponownie? Rozliczenie stanie się podglądem i może się zmienić. Jeśli ktoś już wykonał przelew, uwzględnij go przed kolejnym rozliczeniem.')) return
    lock.current = true; setBusy(true); setError('')
    try {
      const result = await api(`events/${eventId}/${close ? 'close' : 'reopen'}`, { method: 'POST' })
      setData(result); onChanged()
    } catch (reason) { setError(reason.message); onChanged() }
    finally { lock.current = false; setBusy(false) }
  }
  return <section className="settlement" aria-label="Podsumowanie rozliczenia">
    <div className="section-heading"><h3>Rozliczenie</h3>{data && <span className="badge">{data.isFinal ? `Zatwierdzone · wersja ${data.version}` : 'Podgląd'}</span>}</div>
    {!data && !error && <p>Obliczanie rozliczenia…</p>}
    {data && <>
      <p>{data.expenseCount} pozycji · Razem: <strong>{money(data.totalMinor)}</strong></p>
      <p className="helper-text">Rozliczamy wskazanych płacących. Wzajemne długi są kompensowane, aby uprościć przelewy.</p>
      {data.unassignedMinor > 0 && <div className="settlement-warning" role="status"><strong>Do przypisania „Inni”: {money(data.unassignedMinor)}</strong><p>Tabela obejmuje tylko przypisane udziały. Przed zamknięciem autorzy poniższych pozycji muszą poprawić ich podział lub użyć „Wyrównaj”.</p><ul>{data.unassignedExpenses.map(item => <li key={item.expenseId}>{item.name} — {item.authorName}: {money(item.amountMinor)}</li>)}</ul></div>}
      <h4>Kto komu oddaje</h4>
      {data.transfers.length > 0 ? <ul className="transfer-list">{data.transfers.map(row => <li key={`${row.fromParticipantId}-${row.toParticipantId}`} className={row.fromParticipantId === user.participantId || row.toParticipantId === user.participantId ? 'own-transfer' : ''}><div className="transfer-line"><span><small>{row.fromParticipantId === user.participantId ? 'Do oddania' : row.toParticipantId === user.participantId ? 'Do otrzymania' : 'Przelew'}</small>{row.fromName} → {row.toName}</span><strong>{money(row.amountMinor)}</strong></div>{row.acknowledgement && <div className="payment-flags"><label><input type="checkbox" aria-label={`Wysłane: ${row.fromName} do ${row.toName}`} checked={row.acknowledgement.sent} disabled={busy || !row.acknowledgement.canMarkSent} onChange={event => mark(row, 'Sent', event.target.checked)} />Wysłane</label><label><input type="checkbox" aria-label={`Odebrane: ${row.fromName} do ${row.toName}`} checked={row.acknowledgement.received} disabled={busy || !row.acknowledgement.canMarkReceived} onChange={event => mark(row, 'Received', event.target.checked)} />Odebrane</label><small>Widoczne tylko dla stron tego przelewu.</small></div>}</li>)}</ul>
        : <p>{data.unassignedMinor > 0 ? 'Na razie brak przelewów między wskazanymi osobami. Pozostaje kwota „Inni”.' : data.expenseCount === 0 ? 'Dodaj pierwszą pozycję, aby zobaczyć rozliczenie.' : 'Wszystkie salda są równe zero — nikt nikomu nie musi oddawać.'}</p>}
      <details className="balance-details"><summary>Salda uczestników</summary><ul className="allocation-list">{data.balances.map(person => <li key={person.participantId}><span>{person.displayName}<small>Zapłacono: {money(person.paidMinor)} · Koszt: {money(person.costMinor)}{person.unassignedMinor > 0 ? ` · Od „Inni”: ${money(person.unassignedMinor)}` : ''}</small></span><strong>{person.netMinor > 0 ? '+' : ''}{money(person.netMinor)}</strong></li>)}</ul><p className="helper-text">Plus oznacza kwotę do otrzymania, minus — do oddania. Kwoty „Inni” są pokazane osobno.</p></details>
      {data.isFinal ? <p className="helper-text">Wydarzenie zamknięte{data.closedAtUtc ? ` ${new Date(data.closedAtUtc).toLocaleString('pl-PL')}` : ''}. Pozycje i podziały są zablokowane. Autor wydarzenia może je ponownie otworzyć.</p> : <p className="helper-text">Podgląd aktualizuje się po zapisie, po „Odśwież” i co 15 sekund na widocznym ekranie. Ostateczny wynik zatwierdza autor wydarzenia.</p>}
      <p className="helper-text">Po zamknięciu nadawca może oznaczyć „Wysłane”, a odbiorca „Odebrane”. Oznaczenia są prywatne dla obu stron; nie są potwierdzeniem bankowym.</p>
      {data.previousPayments?.length > 0 && <details className="settlement-warning"><summary>Twoje oznaczenia poprzednich wersji</summary><p>Wydarzenie ponownie otwarto. Nowy plan nie odejmuje automatycznie wcześniejszych płatności. Sprawdź historię, zanim wykonasz kolejny przelew.</p><ul>{data.previousPayments.map((row, index) => <li key={index}>Wersja {row.version}: {row.fromName} → {row.toName}, {money(row.amountMinor)} · {row.sent ? 'wysłane' : 'nieoznaczone jako wysłane'} · {row.received ? 'odebrane' : 'nieoznaczone jako odebrane'}</li>)}</ul></details>}
      {isOwner && <div className="form-actions"><button className="primary-button" disabled={busy || (!data.isFinal && (data.unassignedMinor > 0 || editing)) || data.status === 'Archived'} onClick={changeStatus}>{busy ? 'Zapisywanie…' : data.isFinal ? 'Otwórz ponownie' : 'Zamknij i rozlicz'}</button>{editing && !data.isFinal && <span className="helper-text">Najpierw zatwierdź lub anuluj otwarty formularz pozycji.</span>}</div>}
    </>}
    {error && <p className="auth-error" role="alert">{error}</p>}
  </section>
}
