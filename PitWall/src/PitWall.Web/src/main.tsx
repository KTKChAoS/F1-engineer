import React, { useEffect, useRef, useState } from 'react'
import ReactDOM from 'react-dom/client'
import { create } from 'zustand'
import 'uplot/dist/uPlot.min.css'
import './style.css'
import type { CallMessage, CallPreset, CallStatusMessage, ClientRole, DamageInfo, ErrorMessage, EventRecord, FieldPayload, LivePayload, WireMessage } from './contracts.generated'

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

  if (location.pathname.startsWith('/driver'))
    return <DriverView live={live} field={field} calls={calls} connected={connected} send={send} acknowledge={acknowledge} />

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
  const forecast = field?.forecast.filter((sample) => sample.offsetMin <= 60).slice(0, 12) ?? []
  const recentEvents = events.slice(-12).reverse()
  const alerts: { level: 'amber' | 'red'; text: string }[] = []
  tyres?.wearPct.forEach((wear, wheel) => {
    if (wear >= 85) alerts.push({ level: 'red', text: `${['RL', 'RR', 'FL', 'FR'][wheel]} tyre wear ${Math.round(wear)}%` })
    else if (wear >= 70) alerts.push({ level: 'amber', text: `${['RL', 'RR', 'FL', 'FR'][wheel]} tyre wear ${Math.round(wear)}%` })
  })
  if (field && live && live.energy.fuelLaps < field.derived.lapsRemaining + 1) alerts.push({ level: 'amber', text: `Fuel is below race need (${live.energy.fuelLaps.toFixed(1)} laps remaining)` })
  if (damage && Math.max(damage.frontWingL, damage.frontWingR, damage.rearWing, damage.floor, damage.diffuser, damage.sidepod) > 25) alerts.push({ level: 'red', text: 'Aero damage exceeds 25%' })
  if (damage?.drsFault || damage?.ersFault) alerts.push({ level: 'red', text: `Component fault: ${[damage.drsFault && 'DRS', damage.ersFault && 'ERS'].filter(Boolean).join(' and ')}` })
  if (playerCar && playerCar.penaltiesS > 0) alerts.push({ level: 'amber', text: `Penalty recorded (${playerCar.penaltiesS}s)` })
  if (field?.session.safetyCar === 'SC' || field?.session.safetyCar === 'VSC') alerts.push({ level: 'amber', text: `${field.session.safetyCar} deployed` })
  const rainAlert = field?.forecast.find((sample) => sample.offsetMin <= 10 && sample.rainPct > 50)
  if (rainAlert) alerts.push({ level: 'amber', text: `Rain chance ${rainAlert.rainPct}% within ${rainAlert.offsetMin} min` })
  if ((field?.derived.gapBehindUnder1sLaps ?? 0) >= 3) alerts.push({ level: 'amber', text: 'Car behind within 1.0s for 3 laps' })

  return <main className="dashboard">
    <header className="topbar">
      <div><p className="eyebrow">PITWALL · F1 25</p><h1>{field?.session.track || 'Race Engineer Dashboard'}</h1></div>
      <div className="session-summary">{field ? `${field.session.type} · Lap ${field.session.currentLap}/${field.session.totalLaps} · ${Math.ceil(field.session.timeLeftS / 60)} min left · ${field.session.weather} · Track ${field.session.trackTempC}°C · Air ${field.session.airTempC}°C · SC ${field.session.safetyCar}` : 'Waiting for session data'}</div>
      <div className={`connection ${connected && live?.conn.state === 'Receiving' ? 'online' : ''}`}><i />{connected ? live?.conn.state ?? 'connected' : 'offline'}<small>{live?.conn.pps ?? 0} pps</small></div>
    </header>
    {!live && <div className="empty">Start F1 25 telemetry or run <code>dotnet run --project tools/PitWall.Sim</code> to see the dashboard.</div>}
    {alerts.length > 0 && <section className="alert-strip" aria-live="polite">{alerts.map((alert, index) => <div className={`alert-chip alert-${alert.level}`} key={`${alert.text}-${index}`}><b>{alert.level === 'red' ? 'URGENT' : 'ALERT'}</b><span>{alert.text}</span></div>)}</section>}
    <div className="grid">
      <Panel title="Timing Tower" className="tower">
        {cars.length ? <div className="timing-table"><div className="table-head"><span>POS</span><span>DRIVER</span><span>GAP</span><span>LAST LAP</span><span>TYRE</span></div>
          {cars.map((car) => <div className={`timing-row ${car.isPlayer ? 'selected' : ''}`} key={car.index}><b>{car.position}</b><span>{car.name || `Car ${car.index + 1}`}</span><span>{car.gapLeaderMs === 0 ? 'LEADER' : `+${(car.gapLeaderMs / 1000).toFixed(3)}`}</span><span>{formatLap(car.lastLapMs)}</span><span>{car.compound}</span></div>)}</div> : <p className="muted">Waiting for participants</p>}
      </Panel>
      <Panel title="Driver Card" className="driver-card">
        <div className="speed">{Math.round(driver?.speedKph ?? 0)}<small>km/h</small></div>
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
          {[2, 3, 0, 1].map((wheel) => { const wear = tyres?.wearPct[wheel] ?? 0; const lapsLeft = field?.derived.tyreLapsToThreshold?.[wheel]; const wearRate = field?.derived.tyreWearRate?.[wheel]; return <div className="tyre" key={wheel}><b>{['FL', 'FR', 'RL', 'RR'][[2, 3, 0, 1].indexOf(wheel)]}</b><strong>{Math.round(tyres?.surfaceC[wheel] ?? 0)}°</strong><span>{(tyres?.pressurePsi[wheel] ?? 0).toFixed(1)} psi · inner {Math.round(tyres?.innerC[wheel] ?? 0)}°</span><em className={wear >= 85 ? 'wear-red' : wear >= 70 ? 'wear-amber' : ''}>{Math.round(wear)}% · {wearRate == null ? '—' : `${wearRate.toFixed(1)}%/lap`} · {lapsLeft == null ? '—' : `${lapsLeft.toFixed(1)} laps`}</em></div>})}
        </div><p className="muted">Temperatures · pressure · wear</p>
      </Panel>
      <Panel title="Brake Temperatures" className="brake-card">
        <div className="brake-grid">{[2, 3, 0, 1].map((wheel, index) => { const temp = tyres?.brakeC[wheel] ?? 0; return <div className="brake-temp" key={wheel}><span>{['FL', 'FR', 'RL', 'RR'][index]}</span><strong className={temp >= 700 ? 'brake-hot' : temp >= 500 ? 'brake-warm' : ''}>{Math.round(temp)}°C</strong><div className="meter"><i style={{ width: `${Math.min(100, temp / 900 * 100)}%` }} /></div></div>})}</div>
      </Panel>
      <Panel title="Fuel & ERS" className="energy-card">
        <div className="energy-stat"><b>{(energy?.fuelKg ?? 0).toFixed(1)}<small> kg</small></b><span>Fuel remaining</span></div>
        <div className="meter"><i style={{ width: `${Math.min(100, (energy?.fuelLaps ?? 0) * 10)}%` }} /></div>
        <div className="energy-meta"><span>{(energy?.fuelLaps ?? 0).toFixed(1)} laps · mix {energy?.fuelMix ?? '—'}</span><span>{energy?.ersMode ?? '—'}</span></div>
        <div className="energy-details"><span>Burn <b>{field?.derived.fuelBurnPerLap == null ? '—' : `${field.derived.fuelBurnPerLap.toFixed(2)} kg/lap`}</b></span><span>Surplus <b>{field?.derived.fuelSurplusLaps == null ? '—' : `${field.derived.fuelSurplusLaps >= 0 ? '+' : ''}${field.derived.fuelSurplusLaps.toFixed(1)} laps`}</b></span></div>
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
          <div className="weather-current"><strong>{field.session.weather}</strong><span>Track {field.session.trackTempC}°C</span><span>Air {field.session.airTempC}°C</span></div>
          {forecast.length ? <div className="forecast-list">{forecast.map((sample, index) => <div className="forecast-sample" key={`${sample.offsetMin}-${index}`}>
            <b>{sample.offsetMin === 0 ? 'NOW' : `+${sample.offsetMin}m`}</b><span>{sample.weather}</span><span>{sample.trackTempC}° / {sample.airTempC}°</span><em>{sample.rainPct}% rain</em>
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

function DriverView({ live, field, calls, connected, send, acknowledge }: { live?: LivePayload; field?: FieldPayload; calls: CallEntry[]; connected: boolean; send: AppState['send']; acknowledge: AppState['acknowledge'] }) {
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
    <footer className="driver-footer"><span>{field?.session.weather ?? '—'} · Track {field?.session.trackTempC ?? '—'}°C</span><a href="/">Engineer dashboard</a></footer>
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
