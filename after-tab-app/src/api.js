export async function api(path, { method = 'GET', body } = {}) {
  const configuredBase = import.meta.env.VITE_API_BASE_URL
  const base = (configuredBase ?? (import.meta.env.DEV ? 'https://localhost:7180' : '')).replace(/\/$/, '')
  const token = localStorage.getItem('aftertab.session')
  let response
  try {
    response = await fetch(`${base}/api/${path}`, {
      signal: AbortSignal.timeout(25000),
      method, cache: 'no-store',
      headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) },
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    })
  } catch (error) {
    if (error.name === 'TimeoutError' || error.name === 'AbortError') throw new Error('Serwer nie odpowiedział w ciągu 25 sekund. Odśwież dane przed ponownym zapisem — operacja mogła już się zakończyć.', { cause: error })
    throw new Error('Nie można połączyć się z serwerem. Spróbuj ponownie.', { cause: error })
  }
  if (response.status === 401) {
    localStorage.removeItem('aftertab.session')
    window.dispatchEvent(new Event('aftertab:session-expired'))
    throw new Error('Sesja wygasła. Zaloguj się ponownie — zaproszenie pozostanie zachowane.')
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new Error(response.status === 429 ? 'Zbyt wiele prób. Poczekaj minutę.'
      : problem?.title || 'Nie udało się wykonać operacji. Spróbuj ponownie.')
  }
  return response.status === 204 ? null : response.json()
}
