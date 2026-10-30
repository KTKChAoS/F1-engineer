import React from 'react'
import ReactDOM from 'react-dom/client'
import { create } from 'zustand'
import 'uplot/dist/uPlot.min.css'
import './style.css'

type ConnectionState = 'waiting' | 'receiving' | 'stale'
type AppState = { connection: ConnectionState; setConnection: (connection: ConnectionState) => void }

const useAppStore = create<AppState>((set) => ({
  connection: 'waiting',
  setConnection: (connection) => set({ connection }),
}))

function App() {
  const connection = useAppStore((state) => state.connection)

  return (
    <main className="min-h-screen bg-slate-950 p-8 text-slate-100">
      <section className="mx-auto max-w-5xl">
        <p className="text-sm font-semibold tracking-[0.3em] text-cyan-400">PITWALL</p>
        <h1 className="mt-3 text-3xl font-bold">F1 25 Race Engineer</h1>
        <p className="mt-3 text-slate-400">Web dashboard scaffold · telemetry connection pending</p>
        <div className="mt-8 rounded-xl border border-slate-800 bg-slate-900 p-5">
          <p className="text-sm uppercase tracking-wide text-slate-400">Connection</p>
          <p className="mt-2 text-xl font-semibold">{connection}</p>
        </div>
      </section>
    </main>
  )
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode><App /></React.StrictMode>,
)
