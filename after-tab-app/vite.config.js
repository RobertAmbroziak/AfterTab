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
        additionalManifestEntries: ['pwa-192x192.png', 'pwa-512x512.png'].map(name => ({ url: iconUrl(name), revision: null })),
      },
      manifest: {
        id: '/',
        name: 'AfterTab',
        short_name: 'AfterTab',
        description: 'Rozliczanie wspólnych wyjść',
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
