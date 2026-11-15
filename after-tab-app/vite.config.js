import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'

// Changing an icon's bytes must also change its manifest URL for installed Chrome PWAs.
function iconUrl(name) {
  const bytes = readFileSync(new URL(`./public/${name}`, import.meta.url))
  const version = createHash('sha256').update(bytes).digest('hex').slice(0, 12)
  return `/${name}?v=${version}`
}

export default defineConfig({
  plugins: [
    {
      name: 'version-pwa-icons',
      transformIndexHtml(html) {
        return html.replaceAll('href="/pwa-192x192.png"', `href="${iconUrl('pwa-192x192.png')}"`)
      },
    },
    react(),
    VitePWA({
      registerType: 'prompt',
      workbox: {
        // Routes use URL hashes; only the app shell may fall back to index.html.
        // Downloads and other public files must retain their actual responses.
        navigateFallbackAllowlist: [/^\/(?:index\.html)?(?:\?|$)/],
        additionalManifestEntries: ['pwa-192x192.png', 'pwa-512x512.png'].flatMap(name => [
          { url: iconUrl(name), revision: null },
          // The app header and login screen use the stable, unversioned URL.
          // Precache it too so a pull-to-refresh while offline keeps the logo visible.
          { url: `/${name}`, revision: createHash('sha256').update(readFileSync(new URL(`./public/${name}`, import.meta.url))).digest('hex').slice(0, 12) },
        ]),
      },
      manifest: {
        id: '/',
        name: 'AfterTab',
        short_name: 'AfterTab',
        description: 'Shared expenses / Rozliczanie wspólnych wyjść',
        lang: 'pl',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        theme_color: '#10191c',
        background_color: '#0c1214',
        icons: [
          {
            src: iconUrl('pwa-192x192.png'),
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: iconUrl('pwa-512x512.png'),
            sizes: '512x512',
            type: 'image/png',
          },
        ],
      },
    }),
  ],
})
