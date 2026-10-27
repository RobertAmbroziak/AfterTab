import { captureInvitation } from './navigation'

const tokenKey = 'aftertab.session'
const rememberedKey = 'aftertab.guestRemembered'
const flowKey = 'aftertab.guestFlow'
const configuredBase = import.meta.env.VITE_API_BASE_URL
const apiBase = (configuredBase ?? (import.meta.env.DEV ? 'https://localhost:7180' : '')).replace(/\/$/, '')
let restorePromise = null
let navigating = false

export function getToken() {
  return localStorage.getItem(tokenKey)
}

export function hasRememberedGuest() {
  // Only a UI hint. The durable authentication secret is an HttpOnly cookie on the API host.
  return localStorage.getItem(rememberedKey) === 'yes'
}

export function clearToken() {
  localStorage.removeItem(tokenKey)
}

async function request(path, options = {}) {
  const token = getToken()
  let response
  try {
    response = await fetch(`${apiBase}/api/auth/${path}`, {
      ...options,
      signal: AbortSignal.timeout(25000),
      cache: 'no-store',
      headers: { ...options.headers, ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    })
  } catch (error) {
    if (error.name === 'TimeoutError' || error.name === 'AbortError') throw new Error('Serwer potrzebuje więcej czasu na uruchomienie. Ponów próbę — Twoje konto pozostaje zapamiętane.', { cause: error })
    throw new Error('Nie można połączyć się z serwerem. Sprawdź połączenie i spróbuj ponownie.', { cause: error })
  }
  if (response.status === 401) {
    clearToken()
    return null
  }
  if (!response.ok) {
    throw new Error(response.status === 429
      ? 'Zbyt wiele prób. Poczekaj minutę i spróbuj ponownie.'
      : 'Nie udało się obsłużyć sesji. Spróbuj ponownie.')
  }
  return response.status === 204 ? null : response.json()
}

function randomSecret() {
  return Array.from(crypto.getRandomValues(new Uint8Array(32)), x => x.toString(16).padStart(2, '0')).join('').toUpperCase()
}

async function beginFlow(action, displayName) {
  if (navigating) return new Promise(() => {})
  navigating = true
  try {
    captureInvitation()
    const verifier = randomSecret()
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
    const challenge = Array.from(new Uint8Array(digest), x => x.toString(16).padStart(2, '0')).join('').toUpperCase()
    const state = randomSecret()
    sessionStorage.setItem(flowKey, JSON.stringify({ state, verifier }))
    const prepared = await request('guest/prepare', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ challenge, state, returnOrigin: location.origin, action, displayName }),
    })
    // A top-level visit allows Safari to use the secure first-party cookie.
    // Neither a session token nor a remembered-device secret is placed in the URL.
    location.assign(`${apiBase}/api/auth/guest/continue?request=${encodeURIComponent(prepared.request)}#verifier=${encodeURIComponent(verifier)}`)
    return await new Promise(() => {})
  } catch (error) {
    navigating = false
    sessionStorage.removeItem(flowKey)
    throw error
  }
}

async function loadSession() {
  if (location.hash.startsWith('#aftertab-code=')) {
    const callback = new URLSearchParams(location.hash.slice(1))
    const pending = JSON.parse(sessionStorage.getItem(flowKey) || 'null')
    sessionStorage.removeItem(flowKey)
    history.replaceState(null, '', location.pathname + location.search)
    if (!pending || pending.state !== callback.get('state')) {
      throw new Error('Próba logowania wygasła lub pochodzi z innej karty. Zaloguj się ponownie.')
    }
    const result = await request('guest/exchange', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: callback.get('aftertab-code'), verifier: pending.verifier }),
    })
    if (result.session) localStorage.setItem(tokenKey, result.session.token)
    else clearToken()
    if (result.remembered) localStorage.setItem(rememberedKey, 'yes')
    else localStorage.removeItem(rememberedKey)
    return result.session
  }
  const profile = getToken() ? await request('me') : null
  // Adopt accounts from the previous release while their original session is valid.
  if (profile?.user.isGuest && !hasRememberedGuest()) return beginFlow('login')
  return profile
}

export function restoreSession() {
  // React StrictMode can mount the welcome screen twice; exchange the one-use code once.
  if (!restorePromise) restorePromise = loadSession().finally(() => { restorePromise = null })
  return restorePromise
}

export async function loginAsGuest(displayName) {
  const existing = await restoreSession()
  return existing || beginFlow('login', displayName)
}

export async function logout() {
  if (!hasRememberedGuest() && getToken()) return beginFlow('logout')
  await request('logout', { method: 'POST' })
  clearToken()
}

export function forgetGuest() {
  return beginFlow('forget')
}
