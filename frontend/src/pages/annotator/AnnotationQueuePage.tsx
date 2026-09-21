import { useEffect, useState } from 'react'
import { createAnnotation, listAnnotationQueue } from '../../api/annotations'
import AudioPlayer from '../../components/AudioPlayer'
import type { RecordingQueueEntry } from '../../types'

export default function AnnotationQueuePage() {
  const [recordings, setRecordings] = useState<RecordingQueueEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const [savingId, setSavingId] = useState<string | null>(null)

  useEffect(() => {
    refresh()
  }, [])

  function refresh() {
    setLoading(true)
    // IRR: this only ever returns recordings the current user hasn't
    // annotated yet, so their view of the count/target never leaks another
    // annotator's text - they stay blind while writing their own.
    listAnnotationQueue()
      .then((res) => setRecordings(res.data))
      .finally(() => setLoading(false))
  }

  async function handleSave(recordingId: string) {
    const text = (drafts[recordingId] ?? '').trim()
    if (!text) return
    setSavingId(recordingId)
    try {
      await createAnnotation(recordingId, text)
      setRecordings((prev) => prev.filter((r) => r.recordingId !== recordingId))
    } finally {
      setSavingId(null)
    }
  }

  if (loading) return <p className="text-sm text-slate-500">Loading…</p>

  return (
    <div>
      <h1 className="mb-4 text-lg font-semibold text-slate-900">To annotate</h1>
      {recordings.length === 0 ? (
        <p className="text-sm text-slate-500">Nothing left to annotate. 🎉</p>
      ) : (
        <ul className="space-y-4">
          {recordings.map((r) => (
            <li key={r.recordingId} className="rounded-2xl border border-slate-200 bg-white p-4">
              <div className="mb-3 flex flex-col gap-3 sm:flex-row">
                <img
                  src={r.photoUrl}
                  alt="Recording prompt"
                  className="h-40 w-full rounded-lg object-contain sm:w-40"
                />
                <div className="flex-1 space-y-2">
                  <AudioPlayer src={r.audioUrl} className="w-full" />
                  <p className="text-xs text-slate-400">
                    Speaker {r.speakerId} &middot; {new Date(r.createdAt).toLocaleString()}
                  </p>
                  {r.annotationCount > 0 && (
                    <p className="text-xs font-medium text-amber-600">
                      {r.annotationCount} of {r.targetAnnotatorCount} annotators done &middot; your
                      annotation will be independent (you won&apos;t see theirs)
                    </p>
                  )}
                </div>
              </div>
              <textarea
                value={drafts[r.recordingId] ?? ''}
                onChange={(e) => setDrafts((d) => ({ ...d, [r.recordingId]: e.target.value }))}
                placeholder="Write what the child said…"
                rows={3}
                className="mb-2 w-full rounded-lg border border-slate-300 px-3 py-2 text-base"
              />
              <button
                onClick={() => handleSave(r.recordingId)}
                disabled={savingId === r.recordingId || !(drafts[r.recordingId] ?? '').trim()}
                className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
              >
                {savingId === r.recordingId ? 'Saving…' : 'Save annotation'}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
