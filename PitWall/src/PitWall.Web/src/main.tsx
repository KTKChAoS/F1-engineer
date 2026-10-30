import React, { useEffect, useRef, useState } from 'react'
import ReactDOM from 'react-dom/client'
import { create } from 'zustand'
import uPlot from 'uplot'
import 'uplot/dist/uPlot.min.css'
import './style.css'
import type { CallMessage, CallPreset, CallStatusMessage, CarSetupInfo, ClientRole, DamageInfo, ErrorMessage, EventRecord, FieldPayload, LivePayload, WireMessage } from './contracts.generated'

type CallEntry = { call: CallMessage; status: string }
type Message = WireMessage<LivePayload | FieldPayload | EventRecord | CallMessage | CallStatusMessage | ErrorMessage>
type AppState = { live?: LivePayload; field?: FieldPayload; events: EventRecord[]; calls: CallEntry[]; callError?: string; connected: boolean; send: (type: string, payload: unknown) => void; setConnected: (value: boolean) => void; setSend: (send: AppState['send']) => void; clearCallError: () => void; acknowledge: (callId: string) => void; update: (message: Message) => void }

const useAppStore = create<AppState>((set) => ({
  connected: false,
  events: [],
  calls: [],
  send: () => {},
  clearCallError: () => set({ callError: undefined }),
  setConnected: (connected) => set({ connected }),
  setSend: (send) => set({ send }),
  acknowledge: (callId) => set((state) => ({ calls: state.calls.map((entry) => entry.call.callId === callId ? { ...entry, status: 'acknowledged' } : entry) })),
  update: (message) => {
    if (message.type === 'live') set({ live: message.payload as LivePayload })
    else if (message.type === 'field') {
      const field = message.payload as FieldPayload
      set((state) => ({ field, events: state.field && state.field.session.uid !== field.session.uid ? [] : state.events }))
    }
    else if (message.type === 'event') {
      const event = message.payload as EventRecord
      set((state) => state.events.some((item) => item.id === event.id)
        ? {}
        : { events: [...state.events, event].slice(-50) })
    }
    else if (message.type === 'call') {
      const call = message.payload as CallMessage
      set((state) => ({ calls: [...state.calls.filter((entry) => entry.call.callId !== call.callId), { call, status: 'delivered' }].slice(-50) }))
      const timeout = call.preset === 'BoxThisLap' || call.preset === 'BoxNextLap' ? 20_000 : 10_000
      window.setTimeout(() => set((state) => ({ calls: state.calls.map((entry) => entry.call.callId === call.callId && entry.status !== 'acknowledged' ? { ...entry, status: 'expired' } : entry) })), timeout)
    }
    else if (message.type === 'callStatus') {
      const update = message.payload as CallStatusMessage
      set((state) => ({ calls: state.calls.map((entry) => entry.call.callId === update.callId ? { ...entry, status: String(update.status).toLowerCase() } : entry) }))
    }
    else if (message.type === 'error') set({ callError: (message.payload as ErrorMessage).message })
  },
}))

const presets: { preset: CallPreset; text: string }[] = [
  { preset: 'BoxThisLap', text: 'Box this lap.' }, { preset: 'BoxNextLap', text: 'Box next lap.' },
  { preset: 'StayOut', text: 'Stay out.' }, { preset: 'PushNow', text: 'Push now.' },
  { preset: 'ManageTyres', text: 'Manage your tyres.' }, { preset: 'SaveFuel', text: 'Save fuel.' },
  { preset: 'MaxErs', text: 'Use maximum ERS.' }, { preset: 'CarBehindClosing', text: 'Car behind is closing.' },
  { preset: 'GoodLap', text: 'Good lap.' },
]

type LayoutMode = 'full' | 'race'
type AppSettings = {
  units: 'metric' | 'imperial'
  theme: 'dark' | 'light'
  colorBlind: boolean
  layout: LayoutMode
  alertDisplay: 'banner' | 'toast'
  alerts: { tyreAmber: number; tyreRed: number; tyreMinC: number; tyreMaxC: number; fuelMargin: number; aeroDamage: number; rainPct: number; gapSeconds: number; gapLaps: number }
  savedLayouts: { name: string; mode: LayoutMode }[]
}
const settingsKey = 'pitwall.settings.v1'
const defaultSettings: AppSettings = {
  units: 'metric', theme: 'dark', colorBlind: false, layout: 'full', alertDisplay: 'banner',
  alerts: { tyreAmber: 70, tyreRed: 85, tyreMinC: 60, tyreMaxC: 110, fuelMargin: 1, aeroDamage: 25, rainPct: 50, gapSeconds: 1, gapLaps: 3 },
  savedLayouts: [{ name: 'Full Dashboard', mode: 'full' }, { name: 'Race Focus', mode: 'race' }],
}

function readSettings(): AppSettings {
  try {
    const saved = JSON.parse(localStorage.getItem(settingsKey) ?? '{}') as Partial<AppSettings>
    return { ...defaultSettings, ...saved, alerts: { ...defaultSettings.alerts, ...saved.alerts }, savedLayouts: saved.savedLayouts?.length ? saved.savedLayouts : defaultSettings.savedLayouts }
  } catch { return defaultSettings }
}

function formatTemperature(celsius: number, units: AppSettings['units']) {
  return units === 'imperial' ? `${Math.round(celsius * 9 / 5 + 32)}°F` : `${Math.round(celsius)}°C`
}
function formatFuel(kg: number, units: AppSettings['units']) {
  return units === 'imperial' ? `${(kg * 2.20462).toFixed(1)} lb` : `${kg.toFixed(1)} kg`
}

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
  const { live, field, events, calls, callError, clearCallError, connected, setConnected, setSend, send, acknowledge, update } = useAppStore()
  const [settings] = useState(readSettings)

  useEffect(() => {
    const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:'
    const socket = new WebSocket(`${scheme}//${location.host}/ws`)
    let seq = 0
    const sendMessage = (type: string, payload: unknown) => {
      if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type, seq: ++seq, t: Date.now(), payload }))
    }
    setSend(sendMessage)
    socket.onopen = () => {
      setConnected(true)
      const role: ClientRole = location.pathname.startsWith('/driver') ? 'Driver' : 'Engineer'
      sendMessage('hello', { role, name: role === 'Driver' ? 'Driver' : 'Engineer', clientVersion: '0.1.0' })
    }
    socket.onclose = () => { setConnected(false); setSend(() => {}) }
    socket.onerror = () => setConnected(false)
    socket.onmessage = (event) => {
      try { update(JSON.parse(event.data) as Message) } catch { /* Ignore malformed server frames. */ }
    }
    return () => { setSend(() => {}); socket.close() }
  }, [setConnected, setSend, update])

  useEffect(() => {
    document.body.dataset.theme = settings.theme
    document.body.dataset.colorBlind = String(settings.colorBlind)
    document.body.dataset.layout = settings.layout
    document.body.dataset.alertDisplay = settings.alertDisplay
  }, [settings])

  if (location.pathname.startsWith('/driver'))
    return <DriverView live={live} field={field} calls={calls} connected={connected} send={send} acknowledge={acknowledge} units={settings.units} />
  if (location.pathname.startsWith('/setup'))
    return <SetupView track={field?.session.track} setup={field?.setup} />
  if (location.pathname.startsWith('/settings'))
    return <SettingsView />

  const sendCall = (preset: CallPreset, text: string) => send('call', { callId: crypto.randomUUID(), preset, text } satisfies CallMessage)

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
  const chartLaps = field?.laps.slice(-20) ?? []
  const allLaps = field?.laps ?? []
  const forecast = field?.forecast.filter((sample) => sample.offsetMin <= 60).slice(0, 12) ?? []
  const recentEvents = events.slice(-12).reverse()
  const trackPath = field?.trackPath ?? []
  const playerIndex = cars.find((car) => car.isPlayer)?.index ?? 0
  const positionHistory = field?.positionHistory.filter((lap) => lap.positions[playerIndex] > 0) ?? []
  const validLaps = allLaps.filter((lap) => lap.valid && lap.lapMs > 0)
  const bestLap = validLaps.length ? Math.min(...validLaps.map((lap) => lap.lapMs)) : 0
  const averageLap = validLaps.length ? validLaps.reduce((sum, lap) => sum + lap.lapMs, 0) / validLaps.length : 0
  const bestSector = (sector: 's1Ms' | 's2Ms' | 's3Ms') => { const values = validLaps.map((lap) => lap[sector]).filter((value) => value > 0); return values.length ? Math.min(...values) : 0 }

  const downloadLapCsv = () => {
    const csvCell = (value: string | number | boolean) => `"${String(value).replaceAll('"', '""')}"`
    const rows = [
      ['track', field?.session.track ?? '', 'session', field?.session.type ?? ''],
      ['lap', 'lap_ms', 'sector1_ms', 'sector2_ms', 'sector3_ms', 'valid', 'compound', 'fuel_kg', 'wear_rl', 'wear_rr', 'wear_fl', 'wear_fr'],
      ...allLaps.map((lap) => [lap.lapNo, lap.lapMs, lap.s1Ms, lap.s2Ms, lap.s3Ms, lap.valid, lap.compound, lap.fuelKg, ...lap.wearPct]),
    ]
    const csv = rows.map((row) => row.map((value) => csvCell(value)).join(',')).join('\r\n')
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
    const link = document.createElement('a')
    link.href = url
    link.download = `pitwall-${(field?.session.track || 'session').toLowerCase().replaceAll(/[^a-z0-9]+/g, '-')}-laps.csv`
    link.click()
    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  }
  const alerts: { level: 'amber' | 'red'; text: string }[] = []
  tyres?.wearPct.forEach((wear, wheel) => {
    if (wear >= settings.alerts.tyreRed) alerts.push({ level: 'red', text: `${['RL', 'RR', 'FL', 'FR'][wheel]} tyre wear ${Math.round(wear)}%` })
    else if (wear >= settings.alerts.tyreAmber) alerts.push({ level: 'amber', text: `${['RL', 'RR', 'FL', 'FR'][wheel]} tyre wear ${Math.round(wear)}%` })
    const temp = tyres.surfaceC[wheel]
    if (temp > 0 && (temp < settings.alerts.tyreMinC || temp > settings.alerts.tyreMaxC)) alerts.push({ level: 'amber', text: `${['RL', 'RR', 'FL', 'FR'][wheel]} tyre temperature ${formatTemperature(temp, settings.units)}` })
  })
  if (field && live && live.energy.fuelLaps < field.derived.lapsRemaining + settings.alerts.fuelMargin) alerts.push({ level: 'amber', text: `Fuel is below race need (${live.energy.fuelLaps.toFixed(1)} laps remaining)` })
  if (damage && Math.max(damage.frontWingL, damage.frontWingR, damage.rearWing, damage.floor, damage.diffuser, damage.sidepod) > settings.alerts.aeroDamage) alerts.push({ level: 'red', text: `Aero damage exceeds ${settings.alerts.aeroDamage}%` })
  if (damage?.drsFault || damage?.ersFault) alerts.push({ level: 'red', text: `Component fault: ${[damage.drsFault && 'DRS', damage.ersFault && 'ERS'].filter(Boolean).join(' and ')}` })
  if (playerCar && playerCar.penaltiesS > 0) alerts.push({ level: 'amber', text: `Penalty recorded (${playerCar.penaltiesS}s)` })
  if (field?.session.safetyCar === 'SC' || field?.session.safetyCar === 'VSC') alerts.push({ level: 'amber', text: `${field.session.safetyCar} deployed` })
  const rainAlert = field?.forecast.find((sample) => sample.offsetMin <= 10 && sample.rainPct > settings.alerts.rainPct)
  if (rainAlert) alerts.push({ level: 'amber', text: `Rain chance ${rainAlert.rainPct}% within ${rainAlert.offsetMin} min` })
  let closeGapLaps = 0
  let latestGapLap: number | undefined
  for (const sample of [...(field?.gapHistory ?? [])].reverse()) {
    if (sample.behindMs == null || sample.behindMs >= settings.alerts.gapSeconds * 1000 || (latestGapLap != null && sample.lapNo !== latestGapLap - 1)) break
    closeGapLaps++
    latestGapLap = sample.lapNo
  }
  if (closeGapLaps >= settings.alerts.gapLaps) alerts.push({ level: 'amber', text: `Car behind within ${settings.alerts.gapSeconds.toFixed(1)}s for ${settings.alerts.gapLaps} laps` })

  return <main className="dashboard">
    <header className="topbar">
      <div><p className="eyebrow">PITWALL · F1 25</p><h1>{field?.session.track || 'Race Engineer Dashboard'}</h1></div>
      <div className="session-summary">{field ? `${field.session.type} · Lap ${field.session.currentLap}/${field.session.totalLaps} · ${Math.ceil(field.session.timeLeftS / 60)} min left · ${field.session.weather} · Track ${formatTemperature(field.session.trackTempC, settings.units)} · Air ${formatTemperature(field.session.airTempC, settings.units)} · SC ${field.session.safetyCar}` : 'Waiting for session data'}</div>
      <div className={`connection ${connected && live?.conn.state === 'Receiving' ? 'online' : ''}`}><i />{connected ? live?.conn.state ?? 'connected' : 'offline'}<small>{live?.conn.pps ?? 0} pps</small></div>
      <a className="setup-link" href="/setup">Setup viewer ↗</a>
      <a className="setup-link" href="/settings">Settings</a>
    </header>
    {!live && <div className="empty">Start F1 25 telemetry or run <code>dotnet run --project tools/PitWall.Sim</code> to see the dashboard.</div>}
    {alerts.length > 0 && <section className={`alert-strip alert-${settings.alertDisplay}`} aria-live="polite">{alerts.map((alert, index) => <div className={`alert-chip alert-${alert.level}`} key={`${alert.text}-${index}`}><b>{alert.level === 'red' ? 'URGENT' : 'ALERT'}</b><span>{alert.text}</span></div>)}</section>}
    <div className="grid">
      <Panel title="Timing Tower" className="tower">
        {cars.length ? <div className="timing-table"><div className="table-head"><span>POS</span><span>DRIVER</span><span>GAP</span><span>LAST LAP</span><span>TYRE</span></div>
          {cars.map((car) => <div className={`timing-row ${car.isPlayer ? 'selected' : ''}`} key={car.index}><b>{car.position}</b><span>{car.name || `Car ${car.index + 1}`}</span><span>{car.gapLeaderMs === 0 ? 'LEADER' : `+${(car.gapLeaderMs / 1000).toFixed(3)}`}</span><span>{formatLap(car.lastLapMs)}</span><span>{car.compound}</span></div>)}</div> : <p className="muted">Waiting for participants</p>}
      </Panel>
      <Panel title="Track Map · Live Trace" className="track-map-card">
        <TrackMap points={trackPath} />
      </Panel>
      <Panel title="Driver Card" className="driver-card">
        <div className="speed">{settings.units === 'imperial' ? Math.round((driver?.speedKph ?? 0) * 0.621371) : Math.round(driver?.speedKph ?? 0)}<small>{settings.units === 'imperial' ? 'mph' : 'km/h'}</small></div>
        <div className="gearline"><strong>{driver?.gear ?? '—'}</strong><span>{Math.round(driver?.rpm ?? 0)} rpm</span><span>DRS {driver?.drs ?? '—'}</span></div>
        <div className="meter rpm"><i style={{ width: `${Math.min(100, (driver?.rpm ?? 0) / Math.max(1, driver?.maxRpm ?? 1) * 100)}%` }} /></div>
        <div className="bar-label"><span>Throttle</span><b>{Math.round((driver?.throttle ?? 0) * 100)}%</b></div><div className="meter"><i style={{ width: `${(driver?.throttle ?? 0) * 100}%` }} /></div>
        <div className="bar-label"><span>Brake</span><b>{Math.round((driver?.brake ?? 0) * 100)}%</b></div><div className="meter brake"><i style={{ width: `${(driver?.brake ?? 0) * 100}%` }} /></div>
        <div className="lapline">P{player?.position ?? '—'} · Lap {player?.lap ?? '—'} · Sector {player?.sector ?? '—'} · Steer {Math.round((driver?.steer ?? 0) * 100)}% <span>Last {formatLap(player?.lastLapMs)}</span></div>
      </Panel>
      <Panel title="Gaps" className="gap-card">
        <div className="gap-pair">
          <div><span>AHEAD</span><strong>{player?.position === 1 ? 'LEADER' : formatGap(player?.gapAheadMs)}</strong><em className={gapAheadTrend.className}>{gapAheadTrend.text}</em></div>
          <div><span>BEHIND</span><strong>{formatGap(player?.gapBehindMs)}</strong><em className={gapBehindTrend.className}>{gapBehindTrend.text}</em></div>
        </div>
      </Panel>
      <Panel title={`Tyre Diagram · ${playerCar?.compound ?? '—'} · age ${playerCar?.tyreAgeLaps ?? '—'} laps`} className="tyre-card">
        <div className="tyres">
          {[2, 3, 0, 1].map((wheel) => { const wear = tyres?.wearPct[wheel] ?? 0; const lapsLeft = field?.derived.tyreLapsToThreshold?.[wheel]; const wearRate = field?.derived.tyreWearRate?.[wheel]; return <div className="tyre" key={wheel}><b>{['FL', 'FR', 'RL', 'RR'][[2, 3, 0, 1].indexOf(wheel)]}</b><strong>{formatTemperature(tyres?.surfaceC[wheel] ?? 0, settings.units)}</strong><span>{(tyres?.pressurePsi[wheel] ?? 0).toFixed(1)} psi · inner {formatTemperature(tyres?.innerC[wheel] ?? 0, settings.units)}</span><em className={wear >= settings.alerts.tyreRed ? 'wear-red' : wear >= settings.alerts.tyreAmber ? 'wear-amber' : ''}>{Math.round(wear)}% · {wearRate == null ? '—' : `${wearRate.toFixed(1)}%/lap`} · {lapsLeft == null ? '—' : `${lapsLeft.toFixed(1)} laps`}</em></div>})}
        </div><p className="muted">Temperatures · pressure · wear</p>
      </Panel>
      <Panel title="Brake Temperatures" className="brake-card">
        <div className="brake-grid">{[2, 3, 0, 1].map((wheel, index) => { const temp = tyres?.brakeC[wheel] ?? 0; return <div className="brake-temp" key={wheel}><span>{['FL', 'FR', 'RL', 'RR'][index]}</span><strong className={temp >= 700 ? 'brake-hot' : temp >= 500 ? 'brake-warm' : ''}>{formatTemperature(temp, settings.units)}</strong><div className="meter"><i style={{ width: `${Math.min(100, temp / 900 * 100)}%` }} /></div></div>})}</div>
      </Panel>
      <Panel title="Strategy & Tyre Inventory" className="strategy-card">
        <div className="strategy-pit-window"><span>Pit window</span><b>{field?.session.pitWindow.ideal ? `Lap ${field.session.pitWindow.ideal}–${field.session.pitWindow.latest}` : 'Not provided'}</b><span>Est. pit loss {(field?.derived.pitLossS ?? 22).toFixed(0)}s</span></div>
        <div className="stint-list">{field?.strategy.stints.map((stint, index) => <div key={`${stint.startLap}-${index}`}><b>{stint.compound}</b><span>Laps {stint.startLap}–{stint.endLap}</span></div>)}</div>
        <div className="set-list">{field?.strategy.tyreSets.filter((set) => set.available || set.fitted).slice(0, 8).map((set, index) => <div key={`${set.compound}-${index}`} className={set.fitted ? 'set-fitted' : ''}><b>{set.compound}{set.fitted ? ' · fitted' : ''}</b><span>{Math.round(set.wearPct)}% wear · {set.lifeLeft.toFixed(0)} laps left</span></div>)}</div>
        <p className={field?.derived.undercutHint ? 'strategy-hint' : 'muted'}>{field?.derived.undercutHint ? 'Undercut hint: you are close to the car ahead and your tyres are older.' : 'No undercut hint detected.'}</p>
      </Panel>
      <Panel title="Lap Times · Last 20" className="chart-card">
        {chartLaps.length ? <LinePlot invert xValues={chartLaps.map((lap) => lap.lapNo)} series={[{ name: 'Lap time (s)', color: '#4dd9c1', values: chartLaps.map((lap) => lap.lapMs / 1000) }]} /> : <p className="muted">Lap times appear as laps are completed.</p>}
      </Panel>
      <Panel title="Tyre Wear · By Wheel" className="chart-card">
        {chartLaps.length ? <LinePlot xValues={chartLaps.map((lap) => lap.lapNo)} series={[0, 1, 2, 3].map((wheel) => ({ name: ['RL', 'RR', 'FL', 'FR'][wheel], color: ['#ffad62', '#f17862', '#58d5bb', '#8d9cff'][wheel], values: chartLaps.map((lap) => lap.wearPct[wheel] ?? 0) }))} /> : <p className="muted">Wear history appears as laps are completed.</p>}
      </Panel>
      <Panel title="Position History" className="chart-card">
        {positionHistory.length ? <LinePlot invert xValues={positionHistory.map((lap) => lap.lapNo)} series={[{ name: 'Position', color: '#c79cff', values: positionHistory.map((lap) => lap.positions[playerIndex]) }]} /> : <p className="muted">Waiting for Lap Positions telemetry.</p>}
      </Panel>
      <Panel title="Session Summary & Export" className="summary-card">
        <div className="summary-grid"><div><span>Completed</span><b>{allLaps.length} laps</b></div><div><span>Valid</span><b>{validLaps.length} laps</b></div><div><span>Best lap</span><b>{formatLap(bestLap)}</b></div><div><span>Average</span><b>{formatLap(averageLap)}</b></div><div><span>Best sectors</span><b>{formatLap(bestSector('s1Ms'))} · {formatLap(bestSector('s2Ms'))} · {formatLap(bestSector('s3Ms'))}</b></div><div><span>Pit stops / penalties</span><b>{playerCar?.stops ?? 0} / {playerCar?.penaltiesS ?? 0}s</b></div><div><span>Tyre stints</span><b>{field?.strategy.stints.length ?? 0}</b></div></div>
        <button className="export-button" onClick={downloadLapCsv} disabled={!allLaps.length}>Export lap data as CSV</button>
      </Panel>
      <Panel title="Fuel & ERS" className="energy-card">
        <div className="energy-stat"><b>{formatFuel(energy?.fuelKg ?? 0, settings.units)}</b><span>Fuel remaining</span></div>
        <div className="meter"><i style={{ width: `${Math.min(100, (energy?.fuelLaps ?? 0) * 10)}%` }} /></div>
        <div className="energy-meta"><span>{(energy?.fuelLaps ?? 0).toFixed(1)} laps · mix {energy?.fuelMix ?? '—'}</span><span>{energy?.ersMode ?? '—'}</span></div>
        <div className="energy-details"><span>Burn <b>{field?.derived.fuelBurnPerLap == null ? '—' : `${formatFuel(field.derived.fuelBurnPerLap, settings.units)}/lap`}</b></span><span>Surplus <b>{field?.derived.fuelSurplusLaps == null ? '—' : `${field.derived.fuelSurplusLaps >= 0 ? '+' : ''}${field.derived.fuelSurplusLaps.toFixed(1)} laps`}</b></span></div>
        <div className="energy-details"><span>Pit loss <b>{field?.derived.pitLossS.toFixed(0) ?? '—'} s est.</b></span><span>Undercut <b>{field?.derived.undercutHint ? 'Opportunity' : 'No hint'}</b></span></div>
        <div className="bar-label"><span>ERS store</span><b>{Math.round(energy?.ersPct ?? 0)}%</b></div><div className="meter ers"><i style={{ width: `${energy?.ersPct ?? 0}%` }} /></div>
        <div className="energy-meta"><span>Harvested {(energy?.ersHarvestedMJ ?? 0).toFixed(1)} MJ</span><span>Deployed {(energy?.ersDeployedMJ ?? 0).toFixed(1)} MJ</span></div>
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
      <Panel title="Weather · 60 min forecast" className="weather-card">
        {field ? <>
          <div className="weather-current"><strong>{field.session.weather}</strong><span>Track {formatTemperature(field.session.trackTempC, settings.units)}</span><span>Air {formatTemperature(field.session.airTempC, settings.units)}</span></div>
          {forecast.length ? <div className="forecast-list">{forecast.map((sample, index) => <div className="forecast-sample" key={`${sample.offsetMin}-${index}`}>
            <b>{sample.offsetMin === 0 ? 'NOW' : `+${sample.offsetMin}m`}</b><span>{sample.weather}</span><span>{formatTemperature(sample.trackTempC, settings.units)} / {formatTemperature(sample.airTempC, settings.units)}</span><em>{sample.rainPct}% rain</em>
          </div>)}</div> : <p className="muted">No forecast samples received yet.</p>}
        </> : <p className="muted">Waiting for session weather</p>}
      </Panel>
      <Panel title="Events" className="events-card">
        {recentEvents.length ? <div className="event-list">{recentEvents.map((event) => <div className="event-row" key={event.id}>
          <b>{event.code}</b><span>{event.text || event.code}</span><em>Lap {event.lap || '—'}</em>
        </div>)}</div> : <p className="muted">Session events will appear here.</p>}
      </Panel>
      <Panel title="Race Radio · /driver" className="calls-card">
        {callError && <div className="call-error" role="status">{callError}<button onClick={clearCallError} aria-label="Dismiss call error">×</button></div>}
        <div className="preset-grid">{presets.map(({ preset, text }) => <button key={preset} onClick={() => sendCall(preset, text)}>{text}</button>)}</div>
        <form className="free-call" onSubmit={(event) => { event.preventDefault(); const data = new FormData(event.currentTarget); const text = String(data.get('call') ?? '').trim(); if (text) { sendCall('FreeText', text); event.currentTarget.reset() } }}>
          <input name="call" maxLength={120} placeholder="Custom call…" aria-label="Custom call" />
          <button type="submit">Send</button>
        </form>
        <div className="call-status-list">{calls.slice(-5).reverse().map(({ call, status }) => <div key={call.callId}><span>{call.text}</span><b className={`status-${status}`}>{status}</b></div>)}</div>
      </Panel>
      <Panel title="Lap Table · Last 10" className="lap-table-card">
        {lapHistory.length ? <div className="lap-table-wrap"><table className="lap-table"><thead><tr><th>LAP</th><th>TIME</th><th>S1</th><th>S2</th><th>S3</th><th>VALID</th><th>TYRE</th></tr></thead><tbody>
          {lapHistory.map((lap) => <tr key={lap.lapNo}><td>{lap.lapNo}</td><td>{formatLap(lap.lapMs)}</td><td>{formatLap(lap.s1Ms)}</td><td>{formatLap(lap.s2Ms)}</td><td>{formatLap(lap.s3Ms)}</td><td className={lap.valid ? 'valid-lap' : 'invalid-lap'}>{lap.valid ? '✓' : 'INVALID'}</td><td>{lap.compound}</td></tr>)}
        </tbody></table></div> : <p className="muted">Lap history appears as laps are completed.</p>}
      </Panel>
    </div>
  </main>
}

function DriverView({ live, field, calls, connected, send, acknowledge, units }: { live?: LivePayload; field?: FieldPayload; calls: CallEntry[]; connected: boolean; send: AppState['send']; acknowledge: AppState['acknowledge']; units: AppSettings['units'] }) {
  const player = live?.player
  const activeCalls = calls.filter((entry) => entry.status !== 'acknowledged').slice(-3).reverse()
  const [alertsEnabled, setAlertsEnabled] = useState(false)
  const [speechEnabled, setSpeechEnabled] = useState(false)
  const [wakeLockActive, setWakeLockActive] = useState(false)
  const audioRef = useRef<AudioContext | null>(null)
  const wakeLockRef = useRef<{ release: () => Promise<void>; addEventListener?: (type: string, listener: () => void) => void } | null>(null)
  const seenCallIds = useRef(new Set<string>())

  const requestWakeLock = async () => {
    const wakeLock = (navigator as Navigator & { wakeLock?: { request: (type: 'screen') => Promise<{ release: () => Promise<void> }> } }).wakeLock
    if (!wakeLock || document.visibilityState !== 'visible') return
    try {
      wakeLockRef.current = await wakeLock.request('screen')
      setWakeLockActive(true)
      wakeLockRef.current.addEventListener?.('release', () => setWakeLockActive(false))
    } catch { setWakeLockActive(false) }
  }

  const enableDriverAlerts = async () => {
    try {
      const audio = new AudioContext()
      await audio.resume()
      audioRef.current = audio
      setAlertsEnabled(true)
    } catch { setAlertsEnabled(false) }
    await requestWakeLock()
  }

  useEffect(() => {
    const onVisibility = () => { if (document.visibilityState === 'visible' && alertsEnabled) void requestWakeLock() }
    document.addEventListener('visibilitychange', onVisibility)
    return () => {
      document.removeEventListener('visibilitychange', onVisibility)
      void wakeLockRef.current?.release()
      void audioRef.current?.close()
    }
  }, [alertsEnabled])

  useEffect(() => {
    for (const { call } of activeCalls) {
      if (seenCallIds.current.has(call.callId)) continue
      seenCallIds.current.add(call.callId)
      if (navigator.vibrate) navigator.vibrate([180, 90, 180])
      const audio = audioRef.current
      if (alertsEnabled && audio) {
        const start = audio.currentTime
        for (const [index, frequency] of [880, 660].entries()) {
          const oscillator = audio.createOscillator()
          const gain = audio.createGain()
          oscillator.frequency.value = frequency
          gain.gain.setValueAtTime(0.0001, start + index * 0.2)
          gain.gain.exponentialRampToValueAtTime(0.15, start + index * 0.2 + 0.015)
          gain.gain.exponentialRampToValueAtTime(0.0001, start + index * 0.2 + 0.16)
          oscillator.connect(gain).connect(audio.destination)
          oscillator.start(start + index * 0.2)
          oscillator.stop(start + index * 0.2 + 0.17)
        }
      }
      if (speechEnabled && 'speechSynthesis' in window) {
        window.speechSynthesis.cancel()
        window.speechSynthesis.speak(new SpeechSynthesisUtterance(call.text))
      }
    }
  }, [activeCalls, alertsEnabled, speechEnabled])

  return <main className="driver-view">
    <header className="driver-header"><div><p className="eyebrow">PITWALL · RACE RADIO</p><h1>{field?.session.track || 'Driver View'}</h1></div><div className={`driver-connection ${connected ? 'online' : ''}`}>{connected ? 'CONNECTED' : 'OFFLINE'}</div></header>
    <div className="driver-controls"><button onClick={() => void enableDriverAlerts()}>{alertsEnabled ? 'ALERT TONE ENABLED' : 'ENABLE SOUND & KEEP AWAKE'}</button><label><input type="checkbox" checked={speechEnabled} onChange={(event) => setSpeechEnabled(event.target.checked)} /> Speak calls</label><span>{wakeLockActive ? 'Screen awake' : alertsEnabled ? 'Screen lock unavailable' : ''}</span></div>
    <section className="driver-stats">
      <div><span>POSITION</span><strong>P{player?.position ?? '—'}</strong></div>
      <div><span>LAP</span><strong>{player?.lap ?? '—'}<small> / {field?.session.totalLaps ?? '—'}</small></strong></div>
      <div><span>LAST LAP</span><strong>{formatLap(player?.lastLapMs)}</strong></div>
      <div><span>GAP AHEAD</span><strong>{player?.position === 1 ? 'LEADER' : formatGap(player?.gapAheadMs)}</strong></div>
      <div><span>GAP BEHIND</span><strong>{formatGap(player?.gapBehindMs)}</strong></div>
      <div><span>TYRES</span><strong>{field?.cars.find((car) => car.isPlayer)?.compound ?? '—'}<small> · age {field?.cars.find((car) => car.isPlayer)?.tyreAgeLaps ?? '—'}</small></strong></div>
      <div><span>FUEL</span><strong>{(live?.energy.fuelLaps ?? 0).toFixed(1)}<small> laps</small></strong></div>
    </section>
    <section className="radio-call-area"><h2>ENGINEER CALLS</h2>
      {activeCalls.length ? activeCalls.map(({ call }) => <article className="driver-call" key={call.callId}><p>{call.text}</p><button onClick={() => { send('callAck', { callId: call.callId }); acknowledge(call.callId) }}>GOT IT</button></article>) : <div className="no-call">{connected ? 'No active calls' : 'Waiting for connection'}</div>}
    </section>
    <footer className="driver-footer"><span>{field?.session.weather ?? '—'} · Track {field ? formatTemperature(field.session.trackTempC, units) : '—'}</span><a href="/">Engineer dashboard</a></footer>
  </main>
}

function TrackMap({ points }: { points: { x: number; z: number }[] }) {
  if (points.length < 2) return <p className="muted">Track trace appears as motion telemetry arrives.</p>
  const canvasRef = useRef<HTMLCanvasElement>(null)
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const draw = () => {
      const rect = canvas.getBoundingClientRect()
      if (rect.width === 0 || rect.height === 0) return
      const dpr = window.devicePixelRatio || 1
      canvas.width = Math.round(rect.width * dpr)
      canvas.height = Math.round(rect.height * dpr)
      const context = canvas.getContext('2d')
      if (!context) return
      context.setTransform(dpr, 0, 0, dpr, 0, 0)
      context.clearRect(0, 0, rect.width, rect.height)

      const minX = Math.min(...points.map((point) => point.x)), maxX = Math.max(...points.map((point) => point.x))
      const minZ = Math.min(...points.map((point) => point.z)), maxZ = Math.max(...points.map((point) => point.z))
      const spanX = maxX - minX || 1, spanZ = maxZ - minZ || 1
      const padding = 20
      const scale = Math.min((rect.width - padding * 2) / spanX, (rect.height - padding * 2) / spanZ)
      const offsetX = (rect.width - spanX * scale) / 2, offsetY = (rect.height - spanZ * scale) / 2
      const mapPoint = (point: { x: number; z: number }) => ({ x: offsetX + (point.x - minX) * scale, y: rect.height - offsetY - (point.z - minZ) * scale })

      context.beginPath()
      points.forEach((point, index) => {
        const mapped = mapPoint(point)
        if (index === 0) context.moveTo(mapped.x, mapped.y)
        else context.lineTo(mapped.x, mapped.y)
      })
      context.lineCap = 'round'
      context.lineJoin = 'round'
      context.strokeStyle = 'rgba(50, 191, 169, .2)'
      context.lineWidth = 13
      context.stroke()
      context.strokeStyle = '#52e2c4'
      context.lineWidth = 3
      context.stroke()

      const current = mapPoint(points[points.length - 1])
      context.beginPath()
      context.arc(current.x, current.y, 7, 0, Math.PI * 2)
      context.fillStyle = '#fff'
      context.fill()
      context.lineWidth = 3
      context.strokeStyle = '#45d9b8'
      context.stroke()
    }
    draw()
    const observer = new ResizeObserver(draw)
    observer.observe(canvas)
    return () => observer.disconnect()
  }, [points])
  return <canvas ref={canvasRef} className="track-map" role="img" aria-label="Live track outline with current car location" />
}

function LinePlot({ series, invert = false, xValues: suppliedXValues }: { series: { name: string; color: string; values: number[] }[]; invert?: boolean; xValues?: number[] }) {
  const containerRef = useRef<HTMLDivElement>(null)
  const plotRef = useRef<uPlot | null>(null)
  const seriesKey = series.map((item) => `${item.name}:${item.color}`).join('|')
  const sampleCount = Math.max(0, ...series.map((item) => item.values.length))
  const xValues = suppliedXValues ?? Array.from({ length: sampleCount }, (_, index) => index + 1)
  const plotData: uPlot.AlignedData = [xValues, ...series.map((item) => item.values.map((value) => Number.isFinite(value) ? value : null))]
  const dataKey = JSON.stringify(plotData)
  const lastDataKey = useRef('')

  useEffect(() => {
    const container = containerRef.current
    if (!container) return
    const makeOptions = (width: number): uPlot.Options => ({
      width,
      height: 190,
      class: 'pitwall-uplot',
      scales: { y: { dir: invert ? -1 : 1 } },
      axes: [
        { stroke: '#8292a7', grid: { stroke: '#253345', width: 1 }, ticks: { stroke: '#253345', width: 1 }, incrs: [1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000], values: (_plot, values) => values.map((value) => `Lap ${Math.round(value)}`) },
        { stroke: '#8292a7', grid: { stroke: '#253345', width: 1 }, ticks: { stroke: '#253345', width: 1 } },
      ],
      series: [{}, ...series.map((item) => ({ label: item.name, stroke: item.color, width: 2, points: { show: false } }))],
      legend: { show: false },
      cursor: { drag: { x: false, y: false } },
    })
    const width = Math.max(1, container.clientWidth)
    plotRef.current = new uPlot(makeOptions(width), plotData, container)
    lastDataKey.current = dataKey
    const observer = new ResizeObserver(() => {
      if (plotRef.current && container.clientWidth > 0) plotRef.current.setSize({ width: container.clientWidth, height: 190 })
    })
    observer.observe(container)
    return () => {
      observer.disconnect()
      plotRef.current?.destroy()
      plotRef.current = null
    }
  // The plot is recreated only when its series structure or scale direction changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seriesKey, invert])

  useEffect(() => {
    if (plotRef.current && lastDataKey.current !== dataKey) {
      plotRef.current.setData(plotData)
      lastDataKey.current = dataKey
    }
  }, [dataKey])

  return <>
    <div ref={containerRef} className="line-plot" role="img" aria-label={series.map((item) => item.name).join(', ')} />
    <div className="chart-legend">{series.map((item) => <span key={item.name}><i style={{ background: item.color }} />{item.name}</span>)}</div>
  </>
}

function SetupView({ track, setup }: { track?: string; setup?: CarSetupInfo }) {
  const pressures = setup?.tyrePressures ?? []
  const groups = [
    { title: 'Aero', rows: [['Front wing', setup?.frontWing], ['Rear wing', setup?.rearWing]] },
    { title: 'Differential', rows: [['On throttle', setup?.onThrottle], ['Off throttle', setup?.offThrottle], ['Engine braking', setup?.engineBraking]] },
    { title: 'Suspension', rows: [['Front suspension', setup?.frontSuspension], ['Rear suspension', setup?.rearSuspension], ['Front anti-roll bar', setup?.frontAntiRollBar], ['Rear anti-roll bar', setup?.rearAntiRollBar], ['Front ride height', setup?.frontSuspensionHeight], ['Rear ride height', setup?.rearSuspensionHeight]] },
    { title: 'Geometry', rows: [['Front camber', setup?.frontCamber], ['Rear camber', setup?.rearCamber], ['Front toe', setup?.frontToe], ['Rear toe', setup?.rearToe]] },
    { title: 'Brakes & Load', rows: [['Brake pressure', setup?.brakePressure == null ? undefined : `${setup.brakePressure}%`], ['Brake bias', setup?.brakeBias == null ? undefined : `${setup.brakeBias}%`], ['Ballast', setup?.ballast], ['Fuel load', setup?.fuelLoad == null ? undefined : `${setup.fuelLoad.toFixed(1)} kg`]] },
    { title: 'Tyre Pressures', rows: [['Front left', pressures[2]], ['Front right', pressures[3]], ['Rear left', pressures[0]], ['Rear right', pressures[1]]] },
  ] as const
  return <main className="dashboard setup-page">
    <header className="topbar"><div><p className="eyebrow">PITWALL · SETUP</p><h1>{track || 'Car Setup Viewer'}</h1></div><a className="setup-link" href="/">← Dashboard</a></header>
    {!setup ? <div className="empty">Waiting for the F1 25 Car Setups packet. Setup telemetry is available in sessions where the game sends it.</div> : <div className="setup-grid">{groups.map((group) => <Panel key={group.title} title={group.title}>{group.rows.map(([label, value]) => <div className="setup-row" key={label}><span>{label}</span><b>{typeof value === 'number' ? value.toFixed(1) : value ?? '—'}</b></div>)}</Panel>)}</div>}
  </main>
}

function SettingsView() {
  const [settings, setSettings] = useState(readSettings)
  const [layoutName, setLayoutName] = useState('')
  const [activeSavedLayout, setActiveSavedLayout] = useState('')
  const patchSettings = <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => setSettings((current) => ({ ...current, [key]: value }))
  const patchAlert = (key: keyof AppSettings['alerts'], value: number) => setSettings((current) => ({ ...current, alerts: { ...current.alerts, [key]: value } }))
  const save = () => { localStorage.setItem(settingsKey, JSON.stringify(settings)); location.href = '/' }
  const saveLayout = () => {
    const name = layoutName.trim()
    if (!name) return
    const savedLayouts = [...settings.savedLayouts.filter((layout) => layout.name !== name), { name, mode: settings.layout }]
    setSettings((current) => ({ ...current, savedLayouts }))
    setActiveSavedLayout(name)
    setLayoutName('')
  }
  const applyLayout = (name: string) => {
    setActiveSavedLayout(name)
    const layout = settings.savedLayouts.find((item) => item.name === name)
    if (layout) patchSettings('layout', layout.mode)
  }
  const deleteLayout = (name: string) => setSettings((current) => ({ ...current, savedLayouts: current.savedLayouts.filter((item) => item.name !== name) }))
  const numberField = (label: string, key: keyof AppSettings['alerts'], min: number, max: number, step = 1) => <label className="settings-field" key={key}><span>{label}</span><input type="number" min={min} max={max} step={step} value={settings.alerts[key]} onChange={(event) => patchAlert(key, Number(event.target.value))} /></label>
  return <main className="dashboard settings-page">
    <header className="topbar"><div><p className="eyebrow">PITWALL · PREFERENCES</p><h1>Dashboard Settings</h1></div><a className="setup-link" href="/">← Dashboard</a></header>
    <div className="settings-grid">
      <Panel title="Display">
        <label className="settings-field"><span>Units</span><select value={settings.units} onChange={(event) => patchSettings('units', event.target.value as AppSettings['units'])}><option value="metric">Metric</option><option value="imperial">Imperial</option></select></label>
        <label className="settings-field"><span>Theme</span><select value={settings.theme} onChange={(event) => patchSettings('theme', event.target.value as AppSettings['theme'])}><option value="dark">Dark</option><option value="light">Light</option></select></label>
        <label className="settings-field"><span>Dashboard layout</span><select value={settings.layout} onChange={(event) => patchSettings('layout', event.target.value as LayoutMode)}><option value="full">Full</option><option value="race">Race focus</option></select></label>
        <label className="settings-check"><input type="checkbox" checked={settings.colorBlind} onChange={(event) => patchSettings('colorBlind', event.target.checked)} /> Use colour-blind palette</label>
        <label className="settings-field"><span>Alert presentation</span><select value={settings.alertDisplay} onChange={(event) => patchSettings('alertDisplay', event.target.value as AppSettings['alertDisplay'])}><option value="banner">Dashboard banner</option><option value="toast">Toast stack</option></select></label>
      </Panel>
      <Panel title="Alert Thresholds">
        {numberField('Tyre wear amber (%)', 'tyreAmber', 0, 100)}
        {numberField('Tyre wear red (%)', 'tyreRed', 0, 100)}
        {numberField('Tyre surface minimum (°C)', 'tyreMinC', -20, 200)}
        {numberField('Tyre surface maximum (°C)', 'tyreMaxC', -20, 200)}
        {numberField('Fuel reserve margin (laps)', 'fuelMargin', 0, 10, 0.5)}
        {numberField('Aero damage alert (%)', 'aeroDamage', 0, 100)}
        {numberField('Rain alert chance (%)', 'rainPct', 0, 100)}
        {numberField('Close gap threshold (seconds)', 'gapSeconds', 0.1, 10, 0.1)}
        {numberField('Close gap duration (laps)', 'gapLaps', 1, 10)}
      </Panel>
      <Panel title="Saved Layouts" className="saved-layouts-card">
        <div className="saved-layout-form"><input aria-label="Layout name" placeholder="Name this layout" value={layoutName} onChange={(event) => setLayoutName(event.target.value)} /><button onClick={saveLayout}>Save current layout</button></div>
        <div className="saved-layout-list">{settings.savedLayouts.map((layout) => <div key={layout.name}><span>{layout.name} · {layout.mode}</span><button onClick={() => applyLayout(layout.name)}>Apply</button><button className="layout-delete" onClick={() => deleteLayout(layout.name)}>Delete</button></div>)}</div>
        {activeSavedLayout && <p className="muted">Selected: {activeSavedLayout}</p>}
      </Panel>
    </div>
    <button className="export-button settings-save" onClick={save}>Save settings</button>
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
