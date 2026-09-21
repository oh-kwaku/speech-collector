import { useEffect, useState } from 'react'
import { listRecordings } from '../../api/annotations'
import { syncPhotos } from '../../api/admin'
import AudioPlayer from '../../components/AudioPlayer'
import type { Recording } from '../../types'

export default function RecordingsBrowserPage() {
  const [recordings, setRecordings] = useState<Recording[]>([])
  const [loading, setLoading] = useState(true)
  const [syncing, setSyncing] = useState(false)
  const [syncMessage, setSyncMessage] = useState<string | null>(null)

  useEffect(() => {
    listRecordings()
      .then((res) => setRecordings(res.data))
      .finally(() => setLoading(false))
  }, [])

  async function handleSync() {
    setSyncing(true)
    setSyncMessage(null)
    try {
      const res = await syncPhotos()
      setSyncMessage(`Added ${res.data.added} new photo(s). ${res.data.total} total.`)
    } catch {
      setSyncMessage('Photo sync failed.')
    } finally {
      setSyncing(false)
    }
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-semibold text-slate-900">All recordings</h1>
        <button
          onClick={handleSync}
          disabled={syncing}
          className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 disabled:opacity-50"
        >
          {syncing ? 'Syncing…' : 'Sync photos from S3'}
        </button>
      </div>
      {syncMessage && <p className="mb-4 text-sm text-slate-500">{syncMessage}</p>}

      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : (
        <ul className="space-y-3">
          {recordings.map((r) => (
            <li
              key={r.id}
              className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 sm:flex-row sm:items-center"
            >
              <img src={r.photoUrl} alt="" className="h-24 w-24 rounded-lg object-contain" />
              <div className="flex-1 space-y-1 text-sm">
                <p className="font-medium text-slate-900">Speaker {r.speakerId}</p>
                <p className="text-slate-500">
                  Session {r.sessionId} &middot; {new Date(r.createdAt).toLocaleString()}
                </p>
                <span
                  className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${
                    r.annotationCount >= r.requiredAnnotatorCount
                      ? 'bg-emerald-100 text-emerald-700'
                      : 'bg-amber-100 text-amber-700'
                  }`}
                >
                  {r.requiredAnnotatorCount > 1
                    ? `Annotated ${r.annotationCount}/${r.requiredAnnotatorCount}` // IRR: sampled for double/triple-annotation
                    : r.annotationCount > 0
                      ? 'Annotated'
                      : 'Pending annotation'}
                </span>
              </div>
              <AudioPlayer src={r.audioUrl} className="w-full sm:w-56" />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
