import React, { useEffect } from 'react'
import ReactDOM from 'react-dom/client'
import { create } from 'zustand'
import 'uplot/dist/uPlot.min.css'
import './style.css'
import type { DamageInfo, EventRecord, FieldPayload, LivePayload, WireMessage } from './contracts.generated'

type Message = WireMessage<LivePayload | FieldPayload | EventRecord>
type AppState = { live?: LivePayload; field?: FieldPayload; events: EventRecord[]; connected: boolean; setConnected: (value: boolean) => void; update: (message: Message) => void }

const useAppStore = create<AppState>((set) => ({
  connected: false,
  events: [],
  setConnected: (connected) => set({ connected }),
  update: (message) => {
    if (message.type === 'live') set({ live: message.payload as LivePayload })
    else if (message.type === 'field') set({ field: message.payload as FieldPayload })
    else if (message.type === 'event') set((state) => ({ events: [...state.events, message.payload as EventRecord].slice(-50) }))
  },
}))

function formatLap(ms?: number) {
  if (!ms || ms <= 0) return '—'
  const minutes = Math.floor(ms / 60000)
  const seconds = ((ms % 60000) / 1000).toFixed(3).padStart(6, '0')
  return `${minutes}:${seconds}`
}

function formatGap(ms?: number) {
  if (!ms || ms <= 0) return '—'
  return `${(ms / 1000).toFixed(3)}s`
}

function formatGapTrend(value?: number | null) {
  if (value == null) return { text: 'Need more lap data', className: 'trend-neutral' }
  if (Math.abs(value) < 0.05) return { text: '→ Stable', className: 'trend-neutral' }
  return value < 0
    ? { text: `↓ Closing ${Math.abs(value).toFixed(2)}s/lap`, className: 'trend-closing' }
    : { text: `↑ Opening ${value.toFixed(2)}s/lap`, className: 'trend-opening' }
}

function DamageBar({ label, value }: { label: string; value: number }) {
  const amount = Math.max(0, Math.min(100, value))
  const tone = amount >= 25 ? 'damage-high' : amount >= 10 ? 'damage-mid' : 'damage-low'
  return <div className="damage-item"><div><span>{label}</span><b>{Math.round(amount)}%</b></div><div className={`meter ${tone}`}><i style={{ width: `${amount}%` }} /></div></div>
}

function Panel({ title, children, className = '' }: { title: string; children: React.ReactNode; className?: string }) {
  return <section className={`panel ${className}`}><h2>{title}</h2>{children}</section>
}

function App() {
  const { live, field, connected, setConnected, update } = useAppStore()

  useEffect(() => {
    const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:'
    const socket = new WebSocket(`${scheme}//${location.host}/ws`)
    socket.onopen = () => setConnected(true)
    socket.onclose = () => setConnected(false)
    socket.onerror = () => setConnected(false)
    socket.onmessage = (event) => {
      try { update(JSON.parse(event.data) as Message) } catch { /* Ignore malformed server frames. */ }
    }
    return () => socket.close()
  }, [setConnected, update])

  const player = live?.player
  const driver = live?.driver
  const tyres = live?.tyres
  const energy = live?.energy
  const damage = field?.damage
  const cars = field?.cars.filter((car) => car.position > 0).sort((a, b) => a.position - b.position) ?? []
  const playerCar = cars.find((car) => car.isPlayer)
  const gapAheadTrend = formatGapTrend(field?.derived.gapAheadTrend)
  const gapBehindTrend = formatGapTrend(field?.derived.gapBehindTrend)
  const lapHistory = field?.laps.slice(-10).reverse() ?? []

  return <main className="dashboard">
    <header className="topbar">
      <div><p className="eyebrow">PITWALL · F1 25</p><h1>{field?.session.track || 'Race Engineer Dashboard'}</h1></div>
      <div className="session-summary">{field ? `${field.session.type} · Lap ${field.session.currentLap}/${field.session.totalLaps} · ${field.session.weather} · Track ${field.session.trackTempC}°C` : 'Waiting for session data'}</div>
      <div className={`connection ${connected && live?.conn.state === 'Receiving' ? 'online' : ''}`}><i />{connected ? live?.conn.state ?? 'connected' : 'offline'}<small>{live?.conn.pps ?? 0} pps</small></div>
    </header>
    {!live && <div className="empty">Start F1 25 telemetry or run <code>dotnet run --project tools/PitWall.Sim</code> to see the dashboard.</div>}
    <div className="grid">
      <Panel title="Timing Tower" className="tower">
        {cars.length ? <div className="timing-table"><div className="table-head"><span>POS</span><span>DRIVER</span><span>GAP</span><span>LAST LAP</span><span>TYRE</span></div>
          {cars.map((car) => <div className={`timing-row ${car.isPlayer ? 'selected' : ''}`} key={car.index}><b>{car.position}</b><span>{car.name || `Car ${car.index + 1}`}</span><span>{car.gapLeaderMs === 0 ? 'LEADER' : `+${(car.gapLeaderMs / 1000).toFixed(3)}`}</span><span>{formatLap(car.lastLapMs)}</span><span>{car.compound}</span></div>)}</div> : <p className="muted">Waiting for participants</p>}
      </Panel>
      <Panel title="Driver Card" className="driver-card">
        <div className="speed">{Math.round(driver?.speedKph ?? 0)}<small>km/h</small></div>
        <div className="gearline"><strong>{driver?.gear ?? '—'}</strong><span>{Math.round(driver?.rpm ?? 0)} rpm</span><span>DRS {driver?.drs ?? '—'}</span></div>
        <div className="bar-label"><span>Throttle</span><b>{Math.round((driver?.throttle ?? 0) * 100)}%</b></div><div className="meter"><i style={{ width: `${(driver?.throttle ?? 0) * 100}%` }} /></div>
        <div className="bar-label"><span>Brake</span><b>{Math.round((driver?.brake ?? 0) * 100)}%</b></div><div className="meter brake"><i style={{ width: `${(driver?.brake ?? 0) * 100}%` }} /></div>
        <div className="lapline">P{player?.position ?? '—'} · Lap {player?.lap ?? '—'} · Sector {player?.sector ?? '—'} <span>Last {formatLap(player?.lastLapMs)}</span></div>
      </Panel>
      <Panel title="Gaps" className="gap-card">
        <div className="gap-pair">
          <div><span>AHEAD</span><strong>{player?.position === 1 ? 'LEADER' : formatGap(player?.gapAheadMs)}</strong><em className={gapAheadTrend.className}>{gapAheadTrend.text}</em></div>
          <div><span>BEHIND</span><strong>{formatGap(player?.gapBehindMs)}</strong><em className={gapBehindTrend.className}>{gapBehindTrend.text}</em></div>
        </div>
      </Panel>
      <Panel title={`Tyre Diagram · ${playerCar?.compound ?? '—'} · age ${playerCar?.tyreAgeLaps ?? '—'} laps`} className="tyre-card">
        <div className="tyres">
          {[2, 3, 0, 1].map((wheel) => <div className="tyre" key={wheel}><b>{['FL', 'FR', 'RL', 'RR'][[2, 3, 0, 1].indexOf(wheel)]}</b><strong>{Math.round(tyres?.surfaceC[wheel] ?? 0)}°</strong><span>{(tyres?.pressurePsi[wheel] ?? 0).toFixed(1)} psi</span><em>{Math.round(tyres?.wearPct[wheel] ?? 0)}% wear</em></div>)}
        </div><p className="muted">Temperatures · pressure · wear</p>
      </Panel>
      <Panel title="Fuel & ERS" className="energy-card">
        <div className="energy-stat"><b>{(energy?.fuelKg ?? 0).toFixed(1)}<small> kg</small></b><span>Fuel remaining</span></div>
        <div className="meter"><i style={{ width: `${Math.min(100, (energy?.fuelLaps ?? 0) * 10)}%` }} /></div>
        <div className="energy-meta"><span>{(energy?.fuelLaps ?? 0).toFixed(1)} laps</span><span>{energy?.ersMode ?? '—'}</span></div>
        <div className="bar-label"><span>ERS store</span><b>{Math.round(energy?.ersPct ?? 0)}%</b></div><div className="meter ers"><i style={{ width: `${energy?.ersPct ?? 0}%` }} /></div>
      </Panel>
      <Panel title="Damage" className="damage-card">
        {damage ? <>
          <div className="damage-grid">
            <DamageBar label="Front wing L" value={damage.frontWingL} />
            <DamageBar label="Front wing R" value={damage.frontWingR} />
            <DamageBar label="Rear wing" value={damage.rearWing} />
            <DamageBar label="Floor" value={damage.floor} />
            <DamageBar label="Diffuser" value={damage.diffuser} />
            <DamageBar label="Sidepod" value={damage.sidepod} />
            <DamageBar label="Gearbox" value={damage.gearbox} />
            <DamageBar label="Engine" value={damage.engine} />
          </div>
          <FaultBadges damage={damage} />
        </> : <p className="muted">Waiting for damage data</p>}
      </Panel>
      <Panel title="Lap Table · Last 10" className="lap-table-card">
        {lapHistory.length ? <div className="lap-table-wrap"><table className="lap-table"><thead><tr><th>LAP</th><th>TIME</th><th>S1</th><th>S2</th><th>S3</th><th>VALID</th><th>TYRE</th></tr></thead><tbody>
          {lapHistory.map((lap) => <tr key={lap.lapNo}><td>{lap.lapNo}</td><td>{formatLap(lap.lapMs)}</td><td>{formatLap(lap.s1Ms)}</td><td>{formatLap(lap.s2Ms)}</td><td>{formatLap(lap.s3Ms)}</td><td className={lap.valid ? 'valid-lap' : 'invalid-lap'}>{lap.valid ? '✓' : 'INVALID'}</td><td>{lap.compound}</td></tr>)}
        </tbody></table></div> : <p className="muted">Lap history appears as laps are completed.</p>}
      </Panel>
    </div>
  </main>
}

function FaultBadges({ damage }: { damage: DamageInfo }) {
  const faults = [
    damage.drsFault && 'DRS fault',
    damage.ersFault && 'ERS fault',
    damage.engineBlown && 'Engine blown',
    damage.engineSeized && 'Engine seized',
  ].filter((fault): fault is string => Boolean(fault))
  return <div className="faults">{faults.length ? faults.map((fault) => <span key={fault}>{fault}</span>) : <span className="fault-clear">No component faults</span>}</div>
}

ReactDOM.createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
