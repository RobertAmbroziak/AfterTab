import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { detectLanguage, formatCount, getLanguage, getLocale, legalUrl, setLanguage, subscribeLanguage, t } from '../src/i18n.js'
import { english } from '../src/translations.js'
import { money, parseHundredths } from '../src/expenseAllocation.js'

test('explicit choice takes priority; primary browser language selects PL or EN with a PL fallback', () => {
  assert.equal(detectLanguage('en', ['pl-PL']), 'en')
  assert.equal(detectLanguage('pl', ['en-US']), 'pl')
  assert.equal(detectLanguage(null, ['pl-PL', 'en-GB']), 'pl')
  assert.equal(detectLanguage(null, ['en-US', 'pl-PL']), 'en')
  assert.equal(detectLanguage('invalid', ['de-DE', 'pl']), 'en')
  assert.equal(detectLanguage(null, []), 'pl')
})

test('language changes update messages and legal links, preserve placeholders and keep PLN arithmetic', () => {
  const initial = getLanguage()
  let notifications = 0
  const unsubscribe = subscribeLanguage(() => notifications++)
  try {
    setLanguage('en')
    assert.equal(getLocale(), 'en-GB')
    assert.equal(t('Zaloguj i dołącz'), 'Sign in and join')
    assert.equal(t('Wyrównaj na {v0}', { v0: 'Magda <&>' }), 'Assign remainder to Magda <&>')
    assert.equal(t('Podaj prawidłowy adres e-mail.'), 'Enter a valid email address.')
    assert.equal(legalUrl('privacy'), '/privacy.html?lang=en')
    assert.match(money(6250), /PLN/)
    assert.equal(formatCount(1, 'expenses'), '1 expense')
    assert.equal(formatCount(2, 'expenses'), '2 expenses')
    assert.equal(parseHundredths('62,50'), 6250)
    assert.equal(parseHundredths('62.50'), 6250)
    const savedMessage = t('Sesja wygasła. Zaloguj się ponownie.')
    setLanguage('pl')
    assert.equal(t(savedMessage), 'Sesja wygasła. Zaloguj się ponownie.')
    assert.equal(legalUrl('data-deletion'), '/data-deletion.html?lang=pl')
    assert.match(money(6250), /62,50/)
    assert.equal(formatCount(1, 'expenses'), '1 pozycja')
    assert.equal(formatCount(2, 'expenses'), '2 pozycje')
    assert.equal(formatCount(5, 'expenses'), '5 pozycji')
    assert.equal(formatCount(12, 'events'), '12 wydarzeń')
    assert.equal(formatCount(22, 'events'), '22 wydarzenia')
    assert.equal(notifications, 2)
    setLanguage('invalid')
    assert.equal(getLanguage(), 'pl')
  } finally { unsubscribe(); setLanguage(initial) }
})

test('every English message keeps the source interpolation parameters', () => {
  for (const [source, translation] of Object.entries(english)) {
    assert.equal(typeof translation, 'string', source)
    assert(translation.trim(), source)
    const placeholders = value => [...value.matchAll(/\{(\w+)\}/g)].map(match => match[1]).sort()
    assert.deepEqual(placeholders(translation), placeholders(source), source)
  }
})

test('public legal pages retain both full static articles and all existing destinations', () => {
  for (const page of ['privacy', 'terms', 'data-deletion']) {
    const html = readFileSync(new URL(`../public/${page}.html`, import.meta.url), 'utf8')
    assert.match(html, /<article lang="pl" data-language="pl"/)
    assert.match(html, /<article lang="en" data-language="en"/)
    assert.match(html, /data-select-language="pl"/)
    assert.match(html, /data-select-language="en"/)
    assert.match(html, /amb77ro@gmail.com/)
    for (const destination of ['privacy', 'terms', 'data-deletion']) assert(html.includes(`/${destination}.html`))
  }
})

test('known Polish domain validation errors have English translations', () => {
  const sourceRoot = new URL('../../after-tab-api/src/', import.meta.url)
  function visit(directory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      if (['bin', 'obj', 'Migrations'].includes(entry.name)) continue
      const url = new URL(entry.name + (entry.isDirectory() ? '/' : ''), directory)
      if (entry.isDirectory()) visit(url)
      else if (entry.name.endsWith('.cs')) {
        const code = readFileSync(url, 'utf8')
        for (const match of code.matchAll(/(?:AppException\(ErrorKind\.\w+,|throw Invalid\()\s*"([^"\n]+)"/g)) {
          assert(Object.hasOwn(english, match[1]), `Missing English validation message: ${match[1]}`)
        }
      }
    }
  }
  visit(sourceRoot)
})
