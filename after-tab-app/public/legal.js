// Both complete translations are in the HTML. Without JavaScript both stay readable.
const key = 'aftertab.language'
function chooseLanguage() {
  const requested = new URLSearchParams(location.search).get('lang')
  if (requested === 'pl' || requested === 'en') return requested
  try { const saved = localStorage.getItem(key); if (saved === 'pl' || saved === 'en') return saved } catch { /* Storage disabled. */ }
  const primary = navigator.languages?.[0] || navigator.language
  return primary ? (primary.toLowerCase().split('-')[0] === 'pl' ? 'pl' : 'en') : 'pl'
}
const language = chooseLanguage()
document.documentElement.lang = language
document.querySelectorAll('article[data-language]').forEach(article => { article.hidden = article.dataset.language !== language })
document.querySelectorAll('[data-pl][data-en]').forEach(element => { element.textContent = element.dataset[language] })
document.querySelectorAll('nav[aria-label]').forEach(nav => { nav.setAttribute('aria-label', language === 'pl' ? 'Informacje i język' : 'Information and language') })
document.querySelectorAll('a[href]').forEach(link => {
  const url = new URL(link.href)
  if (url.origin === location.origin && /\/(privacy|terms|data-deletion)\.html$/.test(url.pathname)) {
    url.searchParams.set('lang', language)
    link.href = url.href
  }
  if (link.hasAttribute('data-select-language')) {
    // Do not overwrite the destination language when updating navigation links.
    const selected = link.dataset.selectLanguage
    link.href = `?lang=${selected}#content-${selected}`
    if (selected === language) link.setAttribute('aria-current', 'true')
    link.addEventListener('click', () => { try { localStorage.setItem(key, selected) } catch { /* Storage disabled. */ } })
  }
})
const heading = document.querySelector(`article[lang="${language}"] h1`)
if (heading) document.title = `${heading.textContent} — AfterTab`
document.querySelector('.skip-link')?.setAttribute('href', `#content-${language}`)
