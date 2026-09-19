import { useEffect, useState } from 'react'
import { listMyRecordings } from '../../api/speakers'
import AudioPlayer from '../../components/AudioPlayer'
import PhotoModal from '../../components/PhotoModal'
import type { Recording } from '../../types'

export default function HistoryPage() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [recordings, setRecordings] = useState<Recording[]>([])
  const [loading, setLoading] = useState(true)
  const [viewingPhoto, setViewingPhoto] = useState<string | null>(null)

  useEffect(() => {
    refresh()
  }, [])

  function refresh() {
    setLoading(true)
    listMyRecordings({ from: from || undefined, to: to || undefined })
      .then((res) => setRecordings(res.data))
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
    listMyRecordings()
      .then((res) => setRecordings(res.data))
      .finally(() => setLoading(false))
  }

  return (
    <div>
      <h1 className="mb-4 text-lg font-semibold text-slate-900">My recording history</h1>

      <form onSubmit={handleFilter} className="mb-4 flex flex-wrap items-end gap-3">
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

      <p className="mb-3 text-sm text-slate-500">{recordings.length} recording(s)</p>

      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : recordings.length === 0 ? (
        <p className="text-sm text-slate-500">No recordings found for this range.</p>
      ) : (
        <ul className="space-y-2">
          {recordings.map((r) => (
            <li
              key={r.id}
              className="flex flex-col gap-2 rounded-xl border border-slate-200 bg-white px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
            >
              <div className="text-sm">
                <p className="font-medium text-slate-900">Speaker {r.speakerId}</p>
                <p className="text-slate-500">{new Date(r.createdAt).toLocaleString()}</p>
                <button
                  onClick={() => setViewingPhoto(r.photoUrl)}
                  className="text-slate-700 underline"
                >
                  View photo
                </button>
              </div>
              <AudioPlayer src={r.audioUrl} className="h-8 max-w-full sm:max-w-[60%]" />
            </li>
          ))}
        </ul>
      )}
      {viewingPhoto && <PhotoModal src={viewingPhoto} onClose={() => setViewingPhoto(null)} />}
    </div>
  )
}
