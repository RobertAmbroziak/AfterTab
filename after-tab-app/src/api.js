import { t, getLanguage } from './i18n.js'
import { fetchSessionResponse } from './sessionCheck'
import { fetchWithNetworkStatus, NETWORK_ERROR } from './networkStatus'

export async function api(path, { method = 'GET', body } = {}) {
  const configuredBase = import.meta.env.VITE_API_BASE_URL
  const base = (configuredBase ?? (import.meta.env.DEV ? 'https://localhost:7180' : '')).replace(/\/$/, '')
  const token = localStorage.getItem('aftertab.session')
  let response
  try {
    const fetchResponse = path === 'auth/account/providers' && method === 'GET'
      ? (url, options) => fetchSessionResponse(url, options, { fetchImpl: fetchWithNetworkStatus })
      : fetchWithNetworkStatus
    response = await fetchResponse(`${base}/api/${path}`, {
      signal: AbortSignal.timeout(25000),
      method, cache: 'no-store',
      headers: { 'Accept-Language': getLanguage(), ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) },
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    })
  } catch (error) {
    if (error.message === NETWORK_ERROR) throw error
    if (error.name === 'TimeoutError' || error.name === 'AbortError') throw new Error(t("Serwer nie odpowiedział w ciągu 25 sekund. Odśwież dane przed ponownym zapisem — operacja mogła już się zakończyć."), { cause: error })
    throw new Error(t("Nie można połączyć się z serwerem. Spróbuj ponownie."), { cause: error })
  }
  if (response.status === 401) {
    localStorage.removeItem('aftertab.session')
    window.dispatchEvent(new Event('aftertab:session-expired'))
    throw new Error(t("Sesja wygasła. Zaloguj się ponownie — zaproszenie pozostanie zachowane."))
  }
  if ([502, 503, 504].includes(response.status)) throw new Error(NETWORK_ERROR)
  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new Error(response.status === 429 ? t("Zbyt wiele prób. Poczekaj minutę.")
      : (problem?.title && !problem.title.startsWith('One or more validation errors') ? problem.title : null) || t("Nie udało się wykonać operacji. Spróbuj ponownie."))
  }
  return response.status === 204 ? null : response.json()
}
