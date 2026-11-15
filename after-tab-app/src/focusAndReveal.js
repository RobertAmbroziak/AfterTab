export function focusAndReveal(element) {
  if (!element) return () => {}

  const target = element.closest('[data-focus-scroll]')
    || element.closest('.workspace')
    || element.closest('form')
    || element
  const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
  const viewport = window.matchMedia?.('(max-width: 1050px)').matches ? window.visualViewport : null
  let frame = 0
  let resizeTimer = 0
  let cleanupTimer = 0

  function reveal() {
    window.cancelAnimationFrame(frame)
    frame = window.requestAnimationFrame(() => {
      const header = window.matchMedia?.('(max-width: 1050px)').matches
        ? document.querySelector('.app-header')
        : null
      const headerBottom = header?.getBoundingClientRect().bottom ?? 0
      const targetTop = Math.max(0, window.scrollY + target.getBoundingClientRect().top - headerBottom - 12)
      if (Math.abs(targetTop - window.scrollY) > 2) {
        window.scrollTo({ top: targetTop, behavior: reduceMotion ? 'auto' : 'smooth' })
      }
    })
  }

  function onViewportResize() {
    window.clearTimeout(resizeTimer)
    resizeTimer = window.setTimeout(reveal, 120)
  }

  function stopWatchingViewport() {
    window.clearTimeout(resizeTimer)
    window.clearTimeout(cleanupTimer)
    viewport?.removeEventListener('resize', onViewportResize)
  }

  if (viewport) {
    viewport.addEventListener('resize', onViewportResize)
    cleanupTimer = window.setTimeout(stopWatchingViewport, 1000)
  }

  try {
    element.focus({ preventScroll: true })
  } catch {
    element.focus()
  }

  reveal()

  return () => {
    window.cancelAnimationFrame(frame)
    stopWatchingViewport()
  }
}
