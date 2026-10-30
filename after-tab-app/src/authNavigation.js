// A navigation can be blocked, or this document can return from the back/forward
// cache with its pending promises and React state intact. Always settle on return.
export function redirectForAuthentication(url) {
  return new Promise((_, reject) => {
    let away = document.visibilityState === 'hidden'
    let settled = false
    const interrupted = 'Logowanie zostało przerwane. Możesz spróbować ponownie.'
    const failed = 'Nie udało się przejść do strony logowania. Możesz ponowić próbę.'

    function cleanup() {
      window.clearTimeout(timer)
      window.removeEventListener('pagehide', onPageHide)
      window.removeEventListener('pageshow', onPageShow)
      window.removeEventListener('focus', onFocus)
      document.removeEventListener('visibilitychange', onVisibility)
    }
    function stop(message) {
      if (settled) return
      settled = true
      cleanup()
      reject(new Error(message))
    }
    function onPageHide() { away = true }
    function onPageShow(event) {
      if (event.persisted || (away && document.visibilityState === 'visible')) stop(interrupted)
    }
    function onVisibility() {
      if (document.visibilityState === 'hidden') away = true
      else if (away) stop(interrupted)
    }
    function onFocus() {
      if (away && document.visibilityState === 'visible') stop(interrupted)
    }
    const timer = window.setTimeout(() => {
      // Do not interrupt a user who is still signing in on the provider's page.
      if (document.visibilityState === 'visible') stop(failed)
    }, 30000)
    window.addEventListener('pagehide', onPageHide)
    window.addEventListener('pageshow', onPageShow)
    window.addEventListener('focus', onFocus)
    document.addEventListener('visibilitychange', onVisibility)
    try { window.location.assign(url) }
    catch { stop(failed) }
  })
}
