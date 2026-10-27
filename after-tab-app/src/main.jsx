import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { registerSW } from 'virtual:pwa-register'
import './index.css'
import App from './App.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

const updateSW = registerSW({
  onNeedRefresh() {
    const confirmed = window.confirm(
      'Dostępna jest nowa wersja AfterTab. Zaktualizować teraz?'
    )

    if (confirmed) {
      updateSW(true)
    }
  },
  onOfflineReady() {
    console.log('AfterTab jest gotowy do działania offline.')
  },
})
