import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import axios from 'axios'
import {
  confirmRecording,
  getNextPhoto,
  getUploadUrl,
  listSessionRecordings,
} from '../../api/speakers'
import { useAudioRecorder } from '../../hooks/useAudioRecorder'
import AudioPlayer from '../../components/AudioPlayer'
import type { Photo } from '../../types'

type Phase = 'loading' | 'ready' | 'saving' | 'saved' | 'error'

export default function RecordingCapturePage() {
  const { speakerId, sessionId } = useParams<{ speakerId: string; sessionId: string }>()
  const navigate = useNavigate()
  const recorder = useAudioRecorder()

  const [photo, setPhoto] = useState<Photo | null>(null)
  const [count, setCount] = useState(0)
  const [phase, setPhase] = useState<Phase>('loading')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!sessionId) return
    loadCount()
    loadNextPhoto()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId])

  function loadCount() {
    if (!sessionId) return
    listSessionRecordings(sessionId).then((res) => setCount(res.data.length))
  }

  function loadNextPhoto() {
    if (!sessionId) return
    setPhase('loading')
    recorder.reset()
    getNextPhoto(sessionId)
      .then((res) => {
        setPhoto(res.data)
        setPhase('ready')
      })
      .catch(() => {
        setErrorMessage('Could not load the next photo.')
        setPhase('error')
      })
  }

  async function handleSave() {
    if (!sessionId || !photo) return
    const blob = recorder.getBlob()
    if (!blob) return
    setPhase('saving')
    setErrorMessage(null)
    try {
      const { data } = await getUploadUrl(sessionId, photo.id)
      // Must match the Content-Type the upload URL was signed with exactly (S3/MinIO
      // reject a mismatch with 403), not the browser's actual MediaRecorder mime type
      // (e.g. "audio/webm;codecs=opus"), which varies by browser.
      await axios.put(data.uploadUrl, blob, {
        headers: { 'Content-Type': 'audio/webm' },
      })
      await confirmRecording(data.recordingId, {
        s3Key: data.s3Key,
        durationSeconds: recorder.durationSeconds,
      })
      setCount((c) => c + 1)
      setPhase('saved')
    } catch {
      setErrorMessage('Could not save the recording. Please try again.')
      setPhase('ready')
    }
  }

  function handleRetake() {
    recorder.reset()
  }

  function handleCancel() {
    navigate(`/collector/speakers/${speakerId}/sessions/${sessionId}`)
  }

  return (
    <div className="mx-auto max-w-md">
      <div className="mb-4 flex items-center justify-between">
        <button onClick={handleCancel} className="text-sm text-slate-500">
          &lsaquo; Cancel
        </button>
        <span className="rounded-full bg-slate-900 px-3 py-1 text-sm font-medium text-white">
          Recording {count + (phase === 'saved' ? 0 : 1)}
        </span>
      </div>

      {phase === 'loading' && <p className="text-center text-sm text-slate-500">Loading photo…</p>}

      {phase === 'error' && (
        <div className="text-center">
          <p className="mb-3 text-sm text-red-600">{errorMessage}</p>
          <button onClick={loadNextPhoto} className="rounded-lg bg-slate-900 px-4 py-2 text-white">
            Retry
          </button>
        </div>
      )}

      {photo && (phase === 'ready' || phase === 'saving' || phase === 'saved') && (
        <>
          <div className="mb-4 overflow-hidden rounded-2xl border border-slate-200 bg-white">
            <img src={photo.url} alt="Describe what you see" className="aspect-square w-full object-contain" />
          </div>

          {errorMessage && <p className="mb-3 text-sm text-red-600">{errorMessage}</p>}
          {recorder.error && <p className="mb-3 text-sm text-red-600">{recorder.error}</p>}

          {phase === 'saved' ? (
            <button
              onClick={loadNextPhoto}
              className="w-full rounded-lg bg-emerald-600 py-4 text-lg font-semibold text-white"
            >
              Next photo &rsaquo;
            </button>
          ) : recorder.status === 'idle' ? (
            <div className="flex gap-2">
              <button
                onClick={handleCancel}
                className="flex-1 rounded-lg border border-slate-300 py-4 font-medium text-slate-700"
              >
                Cancel
              </button>
              <button
                onClick={recorder.start}
                className="flex-[2] rounded-lg bg-red-600 py-4 text-lg font-semibold text-white"
              >
                ● Record
              </button>
            </div>
          ) : recorder.status === 'recording' ? (
            <button
              onClick={recorder.stop}
              className="w-full animate-pulse rounded-lg bg-slate-900 py-4 text-lg font-semibold text-white"
            >
              ■ Stop
            </button>
          ) : (
            <div className="space-y-2">
              {recorder.audioUrl && (
                <AudioPlayer src={recorder.audioUrl} className="w-full" />
              )}
              <div className="flex gap-2">
                <button
                  onClick={handleRetake}
                  disabled={phase === 'saving'}
                  className="flex-1 rounded-lg border border-slate-300 py-4 font-medium text-slate-700 disabled:opacity-50"
                >
                  Retake
                </button>
                <button
                  onClick={handleSave}
                  disabled={phase === 'saving'}
                  className="flex-1 rounded-lg bg-slate-900 py-4 font-medium text-white disabled:opacity-50"
                >
                  {phase === 'saving' ? 'Saving…' : 'Save'}
                </button>
              </div>
              <button onClick={handleCancel} className="w-full py-2 text-sm text-slate-500">
                Cancel
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
