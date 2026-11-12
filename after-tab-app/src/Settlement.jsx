import { t, getLocale, formatCount } from './i18n.js'
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
    if (!window.confirm(close ? t("Zamknąć wydarzenie i zatwierdzić rozliczenie zapisanych pozycji? Dodawanie i zmiana podziałów zostaną zablokowane.") : t("Otworzyć wydarzenie ponownie? Rozliczenie stanie się podglądem i może się zmienić. Jeśli ktoś już wykonał przelew, uwzględnij go przed kolejnym rozliczeniem."))) return
    lock.current = true; setBusy(true); setError('')
    try {
      const result = await api(`events/${eventId}/${close ? 'close' : 'reopen'}`, { method: 'POST' })
      setData(result); onChanged()
    } catch (reason) { setError(reason.message); onChanged() }
    finally { lock.current = false; setBusy(false) }
  }
  return <section className="settlement" aria-label={t("Podsumowanie rozliczenia")}>
    <div className="section-heading"><h3>{t("Rozliczenie")}</h3>{data && <span className="badge">{data.isFinal ? t("Zatwierdzone · wersja {v0}", { v0: data.version }) : t("Podgląd")}</span>}</div>
    {!data && !error && <p>{t("Obliczanie rozliczenia…")}</p>}
    {data && <>
      <p>{formatCount(data.expenseCount, 'expenses')} · {t("Razem")}: <strong>{money(data.totalMinor)}</strong></p>
      <p className="helper-text">{t("Rozliczamy wskazanych płacących. Wzajemne długi są kompensowane, aby uprościć przelewy.")}</p>
      {data.unassignedMinor > 0 && <div className="settlement-warning" role="status"><strong>{t("Do przypisania „Inni”:")} {money(data.unassignedMinor)}</strong><p>{t("Tabela obejmuje tylko przypisane udziały. Przed zamknięciem autorzy poniższych pozycji muszą poprawić ich podział lub użyć „Wyrównaj”.")}</p><ul>{data.unassignedExpenses.map(item => <li key={item.expenseId}>{item.name} — {item.authorName}: {money(item.amountMinor)}</li>)}</ul></div>}
      <h4>{t("Kto komu oddaje")}</h4>
      {data.transfers.length > 0 ? <ul className="transfer-list">{data.transfers.map(row => <li key={`${row.fromParticipantId}-${row.toParticipantId}`} className={row.fromParticipantId === user.participantId || row.toParticipantId === user.participantId ? 'own-transfer' : ''}><div className="transfer-line"><span><small>{row.fromParticipantId === user.participantId ? t("Do oddania") : row.toParticipantId === user.participantId ? t("Do otrzymania") : t("Przelew")}</small>{row.fromName} → {row.toName}</span><strong>{money(row.amountMinor)}</strong></div>{row.acknowledgement && <div className="payment-flags"><label><input type="checkbox" aria-label={t("Wysłane: {v0} do {v1}", { v0: row.fromName, v1: row.toName })} checked={row.acknowledgement.sent} disabled={busy || !row.acknowledgement.canMarkSent} onChange={event => mark(row, 'Sent', event.target.checked)} />{t("Wysłane")}</label><label><input type="checkbox" aria-label={t("Odebrane: {v0} do {v1}", { v0: row.fromName, v1: row.toName })} checked={row.acknowledgement.received} disabled={busy || !row.acknowledgement.canMarkReceived} onChange={event => mark(row, 'Received', event.target.checked)} />{t("Odebrane")}</label><small>{t("Widoczne tylko dla stron tego przelewu.")}</small></div>}</li>)}</ul>
        : <p>{data.unassignedMinor > 0 ? t("Na razie brak przelewów między wskazanymi osobami. Pozostaje kwota „Inni”.") : data.expenseCount === 0 ? t("Dodaj pierwszą pozycję, aby zobaczyć rozliczenie.") : t("Wszystkie salda są równe zero — nikt nikomu nie musi oddawać.")}</p>}
      <details className="balance-details"><summary><span>{t("Salda uczestników")}</span></summary><ul className="allocation-list">{data.balances.map(person => <li key={person.participantId}><span>{person.displayName}<small>{t("Zapłacono:")} {money(person.paidMinor)} {t("· Koszt:")} {money(person.costMinor)}{person.unassignedMinor > 0 ? t(" · Od „Inni”: {v0}", { v0: money(person.unassignedMinor) }) : ''}</small></span><strong>{person.netMinor > 0 ? '+' : ''}{money(person.netMinor)}</strong></li>)}</ul><p className="helper-text">{t("Plus oznacza kwotę do otrzymania, minus — do oddania. Kwoty „Inni” są pokazane osobno.")}</p></details>
      {data.isFinal ? <p className="helper-text">{t("Wydarzenie zamknięte")}{data.closedAtUtc ? ` ${new Date(data.closedAtUtc).toLocaleString(getLocale())}` : ''}{t(". Pozycje i podziały są zablokowane. Autor wydarzenia może je ponownie otworzyć.")}</p> : <p className="helper-text">{t("Podgląd aktualizuje się po zapisie, po „Odśwież” i co 15 sekund na widocznym ekranie. Ostateczny wynik zatwierdza autor wydarzenia.")}</p>}
      <p className="helper-text">{t("Po zamknięciu nadawca może oznaczyć „Wysłane”, a odbiorca „Odebrane”. Oznaczenia są prywatne dla obu stron; nie są potwierdzeniem bankowym.")}</p>
      {data.previousPayments?.length > 0 && <details className="settlement-warning"><summary><span>{t("Twoje oznaczenia poprzednich wersji")}</span></summary><p>{t("Wydarzenie ponownie otwarto. Nowy plan nie odejmuje automatycznie wcześniejszych płatności. Sprawdź historię, zanim wykonasz kolejny przelew.")}</p><ul>{data.previousPayments.map((row, index) => <li key={index}>{t("Wersja")} {row.version}: {row.fromName} → {row.toName}, {money(row.amountMinor)} · {row.sent ? t("wysłane") : t("nieoznaczone jako wysłane")} · {row.received ? t('odebrane') : t("nieoznaczone jako odebrane")}</li>)}</ul></details>}
      {isOwner && <div className="form-actions"><button className="primary-button" disabled={busy || (!data.isFinal && (data.unassignedMinor > 0 || editing)) || data.status === 'Archived'} onClick={changeStatus}>{busy ? t("Zapisywanie…") : data.isFinal ? t("Otwórz ponownie") : t("Zamknij i rozlicz")}</button>{editing && !data.isFinal && <span className="helper-text">{t("Najpierw zatwierdź lub anuluj otwarty formularz pozycji.")}</span>}</div>}
    </>}
    {error && <p className="auth-error" role="alert">{t(error)}</p>}
  </section>
}
