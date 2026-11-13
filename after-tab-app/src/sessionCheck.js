import { NETWORK_ERROR, fetchWithNetworkStatus } from './networkStatus'
export const SESSION_CHECK_MS = 25000
export const SESSION_TIMEOUT_MESSAGE = NETWORK_ERROR

// Retry only explicitly selected reads and the restore preparation. Never retry
// passwords, one-use exchanges or expense writes after an ambiguous response.
export async function fetchSessionResponse(url, options = {}, {
  deadline = Date.now() + SESSION_CHECK_MS,
  retry = true,
  fetchImpl = fetchWithNetworkStatus,
  now = Date.now,
  sleep = ms => new Promise(resolve => setTimeout(resolve, ms)),
  timeoutSignal = ms => AbortSignal.timeout(ms),
} = {}) {
  while (true) {
    const remaining = deadline - now()
    if (remaining <= 0) throw new Error(SESSION_TIMEOUT_MESSAGE)
    let response
    try {
      response = await fetchImpl(url, { ...options, signal: timeoutSignal(Math.max(1, Math.ceil(Math.min(remaining, retry ? 25000 : SESSION_CHECK_MS)))) })
    } catch (error) {
      const retryable = error.message === NETWORK_ERROR || ['TypeError', 'TimeoutError', 'AbortError'].includes(error.name)
      if (!retry || !retryable) {
        if (now() >= deadline || error.name === 'TimeoutError') throw new Error(SESSION_TIMEOUT_MESSAGE, { cause: error })
        throw error
      }
    }
    if (response && (!retry || ![408, 500, 502, 503, 504].includes(response.status))) return response
    // Discard failed response bodies before the next attempt.
    if (response?.body) await response.body.cancel().catch(() => {})
    const wait = Math.min(2000, deadline - now())
    if (wait <= 0) throw new Error(SESSION_TIMEOUT_MESSAGE)
    await sleep(wait)
  }
}
