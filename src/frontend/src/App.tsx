import React, { useMemo, useState } from 'react'

type ConversionResponse = {
  id: string
  from: string
  to: string
  amount: number
  convertedAmount: number
  rate: number
  providerDate: string
  serverTimestamp: string
}

type ProblemDetails = {
  type?: string
  title?: string
  status?: number
  detail?: string
}

type AuditResponse = {
  id: string
  from: string
  to: string
  amount: number
  convertedAmount: number
  rate: number
  providerDate: string
  serverTimestamp: string
}

function getApiBaseUrl(): string {
  const anyWindow = window as unknown as { __VITE_API_URL__?: string }
  const raw = anyWindow.__VITE_API_URL__
  const normalized = (raw ?? '').trim()
  return normalized.replace(/\/+$/, '')
}

function joinApiUrl(baseUrl: string, path: string): string {
  const base = baseUrl.length ? baseUrl : ''
  const p = path.startsWith('/') ? path : `/${path}`
  return `${base}${p}`
}

export default function App() {
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), [])

  const [from, setFrom] = useState('USD')
  const [to, setTo] = useState('EUR')
  const [amount, setAmount] = useState('100.00')

  const [conversion, setConversion] = useState<ConversionResponse | null>(null)
  const [audit, setAudit] = useState<AuditResponse | null>(null)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [loading, setLoading] = useState(false)

  async function convert() {
    setLoading(true)
    setProblem(null)
    setAudit(null)
    try {
      const resp = await fetch(
        joinApiUrl(apiBaseUrl, `/api/currency/convert?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&amount=${encodeURIComponent(amount)}`),
      )
      const data = (await resp.json()) as ConversionResponse | ProblemDetails
      if (!resp.ok) {
        setProblem(data as ProblemDetails)
        setConversion(null)
        return
      }
      setConversion(data as ConversionResponse)
    } catch {
      setProblem({ title: 'Request failed', status: 0, detail: 'Unable to reach the backend.' })
      setConversion(null)
    } finally {
      setLoading(false)
    }
  }

  async function fetchAudit(id: string) {
    setLoading(true)
    setProblem(null)
    try {
      const resp = await fetch(joinApiUrl(apiBaseUrl, `/api/currency/audit/${encodeURIComponent(id)}`))
      const data = (await resp.json()) as AuditResponse | ProblemDetails
      if (!resp.ok) {
        setProblem(data as ProblemDetails)
        setAudit(null)
        return
      }
      setAudit(data as AuditResponse)
    } catch {
      setProblem({ title: 'Request failed', status: 0, detail: 'Unable to fetch audit record.' })
      setAudit(null)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="page">
      <h1>Real-Time Currency Conversion</h1>

      <div className="card">
        <div className="row">
          <label>
            From
            <input value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label>
            To
            <input value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
        </div>
        <label>
          Amount
          <input value={amount} onChange={(e) => setAmount(e.target.value)} />
        </label>
        <button onClick={convert} disabled={loading}>
          Convert
        </button>
      </div>

      {loading ? <p>Working…</p> : null}

      {problem ? (
        <div className="card error">
          <h2>{problem.title ?? 'Error'}</h2>
          {problem.status ? <p>Status: {problem.status}</p> : null}
          {problem.detail ? <p>{problem.detail}</p> : null}
        </div>
      ) : null}

      {conversion ? (
        <div className="card">
          <h2>Conversion</h2>
          <dl>
            <div>
              <dt>From</dt>
              <dd>{conversion.from}</dd>
            </div>
            <div>
              <dt>To</dt>
              <dd>{conversion.to}</dd>
            </div>
            <div>
              <dt>Amount</dt>
              <dd>{conversion.amount}</dd>
            </div>
            <div>
              <dt>Rate</dt>
              <dd>{conversion.rate}</dd>
            </div>
            <div>
              <dt>Converted Amount</dt>
              <dd>{conversion.convertedAmount}</dd>
            </div>
            <div>
              <dt>Provider Date</dt>
              <dd>{conversion.providerDate}</dd>
            </div>
            <div>
              <dt>Server Timestamp</dt>
              <dd>{conversion.serverTimestamp}</dd>
            </div>
            <div>
              <dt>Audit Id</dt>
              <dd>{conversion.id}</dd>
            </div>
          </dl>
          <button onClick={() => fetchAudit(conversion.id)}>Fetch Audit</button>
        </div>
      ) : null}

      {audit ? (
        <div className="card">
          <h2>Audit Record</h2>
          <dl>
            <div>
              <dt>From</dt>
              <dd>{audit.from}</dd>
            </div>
            <div>
              <dt>To</dt>
              <dd>{audit.to}</dd>
            </div>
            <div>
              <dt>Amount</dt>
              <dd>{audit.amount}</dd>
            </div>
            <div>
              <dt>Rate</dt>
              <dd>{audit.rate}</dd>
            </div>
            <div>
              <dt>Converted Amount</dt>
              <dd>{audit.convertedAmount}</dd>
            </div>
            <div>
              <dt>Provider Date</dt>
              <dd>{audit.providerDate}</dd>
            </div>
            <div>
              <dt>Server Timestamp (UTC)</dt>
              <dd>{audit.serverTimestamp}</dd>
            </div>
          </dl>
        </div>
      ) : null}
    </div>
  )
}
