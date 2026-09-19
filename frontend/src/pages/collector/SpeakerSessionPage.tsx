import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { listSessionRecordings } from '../../api/speakers'
import AudioPlayer from '../../components/AudioPlayer'
import PhotoModal from '../../components/PhotoModal'
import type { Recording } from '../../types'

export default function SpeakerSessionPage() {
  const { speakerId, sessionId } = useParams<{ speakerId: string; sessionId: string }>()
  const [recordings, setRecordings] = useState<Recording[]>([])
  const [loading, setLoading] = useState(true)
  const [viewingPhoto, setViewingPhoto] = useState<string | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    if (!sessionId) return
    refresh()
  }, [sessionId])

  function refresh() {
    if (!sessionId) return
    setLoading(true)
    listSessionRecordings(sessionId)
      .then((res) => setRecordings(res.data))
      .finally(() => setLoading(false))
  }

  return (
    <div>
      <div className="mb-1">
        <Link to="/collector/speakers" className="text-sm text-slate-500">
          &lsaquo; Speakers
        </Link>
      </div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-semibold text-slate-900">Speaker {speakerId}</h1>
        <button
          onClick={() => navigate(`/collector/speakers/${speakerId}/sessions/${sessionId}/record`)}
          className="rounded-lg bg-slate-900 px-4 py-3 text-sm font-medium text-white"
        >
          + New recording
        </button>
      </div>

      <p className="mb-3 text-sm text-slate-500">{recordings.length} recording(s) this session</p>

      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : recordings.length === 0 ? (
        <p className="text-sm text-slate-500">No recordings yet for this session.</p>
      ) : (
        <ul className="space-y-2">
          {recordings.map((r, i) => (
            <li
              key={r.id}
              className="flex items-center justify-between rounded-xl border border-slate-200 bg-white px-4 py-3"
            >
              <span className="font-medium text-slate-900">Recording {i + 1}</span>
              <button
                onClick={() => setViewingPhoto(r.photoUrl)}
                className="text-sm text-slate-700 underline"
              >
                View photo
              </button>
              <AudioPlayer src={r.audioUrl} className="h-8 max-w-[60%]" />
            </li>
          ))}
        </ul>
      )}
      {viewingPhoto && <PhotoModal src={viewingPhoto} onClose={() => setViewingPhoto(null)} />}
    </div>
  )
}
