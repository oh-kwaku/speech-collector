import { useEffect, useState } from 'react'
import { getIrrReport, setCanonicalAnnotation } from '../../api/annotations'
import AudioPlayer from '../../components/AudioPlayer'
import PhotoModal from '../../components/PhotoModal'
import type { IrrRecording } from '../../types'

function agreementBadgeClass(score: number) {
  if (score >= 0.85) return 'bg-emerald-100 text-emerald-700'
  if (score >= 0.5) return 'bg-amber-100 text-amber-700'
  return 'bg-rose-100 text-rose-700'
}

// IRR: admin-only view of recordings that have been independently annotated
// by 2+ annotators, so agreement can be checked and, where raters disagree,
// an Admin/adjudicator can pick which annotation is used for the
// training-data export. This is the one place raw text from multiple
// annotators on the same recording is shown side by side - everywhere else
// (the queue) an annotator only ever sees a recording they haven't
// personally annotated yet, never another annotator's text.
export default function IrrReportPage() {
  const [recordings, setRecordings] = useState<IrrRecording[]>([])
  const [loading, setLoading] = useState(true)
  const [viewingPhoto, setViewingPhoto] = useState<string | null>(null)
  const [savingId, setSavingId] = useState<string | null>(null)

  useEffect(() => {
    refresh()
  }, [])

  function refresh() {
    setLoading(true)
    getIrrReport()
      .then((res) => setRecordings(res.data))
      .finally(() => setLoading(false))
  }

  async function handleMarkCanonical(recordingId: string, annotationId: string) {
    setSavingId(annotationId)
    try {
      await setCanonicalAnnotation(recordingId, annotationId)
      setRecordings((prev) =>
        prev.map((r) =>
          r.recordingId !== recordingId
            ? r
            : { ...r, annotations: r.annotations.map((a) => ({ ...a, isCanonical: a.annotationId === annotationId })) },
        ),
      )
    } finally {
      setSavingId(null)
    }
  }

  if (loading) return <p className="text-sm text-slate-500">Loading…</p>

  return (
    <div>
      <h1 className="mb-2 text-lg font-semibold text-slate-900">Inter-rater reliability</h1>
      <p className="mb-6 text-sm text-slate-500">
        Recordings annotated by 2 or more independent annotators, sorted by lowest agreement first
        so the ones most needing a decision surface at the top. Pick which annotation is used for
        the training-data export where raters disagree.
      </p>

      {recordings.length === 0 ? (
        <p className="text-sm text-slate-500">
          No recordings have multiple annotations yet. This fills in as annotators work through
          the queue - a sample of recordings collects several independent annotations before
          dropping out.
        </p>
      ) : (
        <ul className="space-y-4">
          {recordings.map((r) => (
            <li key={r.recordingId} className="rounded-2xl border border-slate-200 bg-white p-4">
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm text-slate-500">
                  Speaker {r.speakerId} &middot;{' '}
                  <button onClick={() => setViewingPhoto(r.photoUrl)} className="underline">
                    View photo
                  </button>
                </p>
                <span
                  className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${agreementBadgeClass(r.agreementScore)}`}
                >
                  {Math.round(r.agreementScore * 100)}% agreement
                </span>
              </div>
              <AudioPlayer src={r.audioUrl} className="mb-3 w-full" />
              <div className="grid gap-3 sm:grid-cols-2">
                {r.annotations.map((a) => (
                  <div
                    key={a.annotationId}
                    className={`rounded-lg border p-3 ${a.isCanonical ? 'border-slate-900 bg-slate-50' : 'border-slate-100 bg-slate-50'}`}
                  >
                    <div className="mb-1 flex items-center justify-between gap-2">
                      <p className="text-xs font-medium text-slate-500">
                        {a.annotatorEmail ?? a.annotatorPhoneNumber ?? a.annotatorUserId}
                      </p>
                      {a.isCanonical ? (
                        <span className="rounded-full bg-slate-900 px-2 py-0.5 text-[10px] font-medium text-white">
                          Used for export
                        </span>
                      ) : (
                        <button
                          onClick={() => handleMarkCanonical(r.recordingId, a.annotationId)}
                          disabled={savingId === a.annotationId}
                          className="text-[10px] font-medium text-slate-500 underline disabled:opacity-50"
                        >
                          {savingId === a.annotationId ? 'Saving…' : 'Use for export'}
                        </button>
                      )}
                    </div>
                    <p className="text-sm text-slate-900">{a.text}</p>
                  </div>
                ))}
              </div>
            </li>
          ))}
        </ul>
      )}
      {viewingPhoto && <PhotoModal src={viewingPhoto} onClose={() => setViewingPhoto(null)} />}
    </div>
  )
}
