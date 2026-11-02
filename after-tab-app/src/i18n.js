import { english } from './translations.js'

export const languageKey = 'aftertab.language'
export function detectLanguage(saved, languages = []) {
  if (saved === 'pl' || saved === 'en') return saved
  const primary = languages.find(value => typeof value === 'string' && value.length)
  return primary ? (primary.toLowerCase().split('-')[0] === 'pl' ? 'pl' : 'en') : 'pl'
}
function readLanguage() {
  let saved
  try { saved = globalThis.localStorage?.getItem(languageKey) } catch { /* Storage can be disabled. */ }
  return detectLanguage(saved, typeof window === 'undefined' ? [] : navigator.languages || [navigator.language])
}
let language = readLanguage()
const listeners = new Set()
const polish = new Map(Object.entries(english).map(([pl, en]) => [en, pl]))
export function getLanguage() { return language }
export function getLocale() { return language === 'pl' ? 'pl-PL' : 'en-GB' }
export function subscribeLanguage(listener) { listeners.add(listener); return () => listeners.delete(listener) }
export function setLanguage(value) {
  if (!['pl', 'en'].includes(value)) return
  language = value
  try { globalThis.localStorage?.setItem(languageKey, value) } catch { /* Keep the selection for this visit. */ }
  if (typeof document !== 'undefined') document.documentElement.lang = value
  listeners.forEach(listener => listener())
}
export function t(source, values = {}) {
  const key = polish.get(source) || source
  const message = language === 'en' ? english[key] || source : key
  return typeof message === 'string' ? message.replace(/\{(\w+)\}/g, (match, name) => Object.hasOwn(values, name) ? String(values[name]) : match) : message
}
export function legalUrl(page) { return `/${page}.html?lang=${language}` }
export function formatCount(value, kind) {
  const forms = {
    expenses: { pl: { one: 'pozycja', few: 'pozycje', many: 'pozycji', other: 'pozycji' }, en: { one: 'expense', other: 'expenses' } },
    events: { pl: { one: 'wydarzenie', few: 'wydarzenia', many: 'wydarzeń', other: 'wydarzeń' }, en: { one: 'event', other: 'events' } },
  }[kind][language]
  return `${value.toLocaleString(getLocale())} ${forms[new Intl.PluralRules(language).select(value)] || forms.other}`
}
if (typeof document !== 'undefined') document.documentElement.lang = language
if (typeof window !== 'undefined') window.addEventListener('storage', event => {
  if (event.key !== languageKey) return
  language = readLanguage()
  document.documentElement.lang = language
  listeners.forEach(listener => listener())
})
