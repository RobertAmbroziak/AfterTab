const listeners = new Set()
let pending = 0
let requestFailed = false
let browserOffline = typeof navigator !== 'undefined' && !navigator.onLine

function snapshot() {
  return { pending, unavailable: browserOffline || requestFailed }
}
function publish() {
  const state = snapshot()
  listeners.forEach(listener => listener(state))
}

if (typeof window !== 'undefined') {
  window.addEventListener('offline', () => { browserOffline = true; publish() })
  window.addEventListener('online', () => { browserOffline = false; requestFailed = false; publish() })
}

export const NETWORK_ERROR = '__AFTERTAB_NETWORK_ERROR__'

export function subscribeNetworkStatus(listener) {
  listeners.add(listener)
  listener(snapshot())
  return () => listeners.delete(listener)
}

export function isNetworkError(error) {
  return error === NETWORK_ERROR || error?.message === NETWORK_ERROR
}

export async function fetchWithNetworkStatus(url, options) {
  pending += 1
  publish()
  try {
    const response = await fetch(url, options)
    requestFailed = [502, 503, 504].includes(response.status)
    return response
  } catch (error) {
    requestFailed = true
    if (['TypeError', 'TimeoutError', 'AbortError'].includes(error?.name)) {
      throw new Error(NETWORK_ERROR, { cause: error })
    }
    throw error
  } finally {
    pending = Math.max(0, pending - 1)
    publish()
  }
}
