import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { getAdminStats, getUserWorkStats } from '../api/admin'
import { getMyAnnotatorStats } from '../api/annotations'
import { getMyCollectorStats } from '../api/speakers'
import type { AdminStats, AnnotatorStats, CollectorStats, UserWorkStats } from '../types'

function StatTile({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4">
      <p className="text-sm text-slate-500">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-slate-900">{value}</p>
    </div>
  )
}

function BreakdownList({ title, data }: { title: string; data: Record<string, number> }) {
  const entries = Object.entries(data).sort((a, b) => a[0].localeCompare(b[0]))
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4">
      <p className="mb-3 text-sm font-medium text-slate-900">{title}</p>
      {entries.length === 0 ? (
        <p className="text-sm text-slate-500">No data yet.</p>
      ) : (
        <ul className="space-y-2">
          {entries.map(([key, count]) => (
            <li key={key} className="flex items-center justify-between text-sm">
              <span className="text-slate-600">{key}</span>
              <span className="font-medium text-slate-900">{count}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function UserWorkTable() {
  const [rows, setRows] = useState<UserWorkStats[] | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getUserWorkStats()
      .then((res) => setRows(res.data))
      .finally(() => setLoading(false))
  }, [])

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4">
      <p className="mb-3 text-sm font-medium text-slate-900">Per-user summary</p>
      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : !rows || rows.length === 0 ? (
        <p className="text-sm text-slate-500">No users yet.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[500px] text-left text-sm">
            <thead className="border-b border-slate-200 text-slate-500">
              <tr>
                <th className="py-2 pr-4">Name</th>
                <th className="py-2 pr-4">Location</th>
                <th className="py-2 pr-4">Recordings</th>
                <th className="py-2 pr-4">Speakers</th>
                <th className="py-2 pr-4">Annotations</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.userId} className="border-b border-slate-100 last:border-0">
                  <td className="py-2 pr-4">{r.name ?? '—'}</td>
                  <td className="py-2 pr-4 text-slate-500">{r.location ?? '—'}</td>
                  <td className="py-2 pr-4 font-medium text-slate-900">{r.totalRecordings}</td>
                  <td className="py-2 pr-4 font-medium text-slate-900">{r.totalSpeakers}</td>
                  <td className="py-2 pr-4 font-medium text-slate-900">{r.totalAnnotations}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AdminDashboard() {
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getAdminStats()
      .then((res) => setStats(res.data))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <p className="text-sm text-slate-500">Loading…</p>
  if (!stats) return <p className="text-sm text-slate-500">Couldn't load stats.</p>

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <StatTile label="Total recordings" value={stats.totalRecordings} />
        <StatTile label="Total annotations" value={stats.totalAnnotations} />
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <BreakdownList title="Recordings by gender" data={stats.recordingsByGender} />
        <BreakdownList title="Recordings by age" data={stats.recordingsByAge} />
      </div>
      <UserWorkTable />
    </div>
  )
}

function AnnotatorDashboard() {
  const [stats, setStats] = useState<AnnotatorStats | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getMyAnnotatorStats()
      .then((res) => setStats(res.data))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <p className="text-sm text-slate-500">Loading…</p>
  if (!stats) return <p className="text-sm text-slate-500">Couldn't load stats.</p>

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <StatTile label="Annotations you've made" value={stats.totalAnnotations} />
    </div>
  )
}

function CollectorDashboard() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [stats, setStats] = useState<CollectorStats | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    refresh()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function refresh() {
    setLoading(true)
    getMyCollectorStats({ from: from || undefined, to: to || undefined })
      .then((res) => setStats(res.data))
      .finally(() => setLoading(false))
  }

  function handleFilter(e: React.FormEvent) {
    e.preventDefault()
    refresh()
  }

  function handleClear() {
    setFrom('')
    setTo('')
    setLoading(true)
    getMyCollectorStats()
      .then((res) => setStats(res.data))
      .finally(() => setLoading(false))
  }

  return (
    <div className="space-y-4">
      <form onSubmit={handleFilter} className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col text-sm text-slate-600">
          From
          <input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            className="mt-1 rounded-lg border border-slate-300 px-3 py-2 text-base"
          />
        </label>
        <label className="flex flex-col text-sm text-slate-600">
          To
          <input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="mt-1 rounded-lg border border-slate-300 px-3 py-2 text-base"
          />
        </label>
        <button
          type="submit"
          className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white"
        >
          Filter
        </button>
        {(from || to) && (
          <button
            type="button"
            onClick={handleClear}
            className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700"
          >
            Clear
          </button>
        )}
      </form>

      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : !stats ? (
        <p className="text-sm text-slate-500">Couldn't load stats.</p>
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <StatTile label="Speakers recorded" value={stats.speakersRecorded} />
          <StatTile label="Confirmed recordings" value={stats.confirmed} />
          <StatTile label="Unconfirmed (in progress)" value={stats.unconfirmed} />
        </div>
      )}

      <Link to="/collector/history" className="inline-block text-sm font-medium text-slate-900 underline">
        View all my recordings &rsaquo;
      </Link>
    </div>
  )
}

export default function DashboardPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []

  return (
    <div className="space-y-8">
      <h1 className="text-lg font-semibold text-slate-900">Dashboard</h1>
      {roles.includes('Admin') && <AdminDashboard />}
      {roles.includes('Annotator') && (
        <section>
          {roles.length > 1 && <h2 className="mb-3 text-base font-semibold text-slate-900">Annotator</h2>}
          <AnnotatorDashboard />
        </section>
      )}
      {roles.includes('Collector') && (
        <section>
          {roles.length > 1 && <h2 className="mb-3 text-base font-semibold text-slate-900">Collector</h2>}
          <CollectorDashboard />
        </section>
      )}
    </div>
  )
}
