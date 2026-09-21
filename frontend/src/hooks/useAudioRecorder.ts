import { useCallback, useEffect, useRef, useState } from 'react'

export type RecorderStatus = 'idle' | 'recording' | 'recorded'

export function useAudioRecorder() {
  const [status, setStatus] = useState<RecorderStatus>('idle')
  const [audioUrl, setAudioUrl] = useState<string | null>(null)
  const [durationSeconds, setDurationSeconds] = useState(0)
  const [error, setError] = useState<string | null>(null)

  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const streamRef = useRef<MediaStream | null>(null)
  const streamPromiseRef = useRef<Promise<MediaStream> | null>(null)
  const startedAtRef = useRef<number>(0)
  const blobRef = useRef<Blob | null>(null)

  // Acquires (and caches) the mic stream without starting a recording, so the
  // getUserMedia/permission-prompt latency happens while the child is still
  // looking at the photo instead of after they've already started talking.
  const ensureStream = useCallback(() => {
    if (streamRef.current?.active) return Promise.resolve(streamRef.current)
    if (!streamPromiseRef.current) {
      streamPromiseRef.current = navigator.mediaDevices
        .getUserMedia({ audio: true })
        .then((stream) => {
          streamRef.current = stream
          return stream
        })
        .finally(() => {
          streamPromiseRef.current = null
        })
    }
    return streamPromiseRef.current
  }, [])

  const prepare = useCallback(() => {
    ensureStream().catch(() => {
      // Swallow here — the same error will surface when the user actually
      // taps Record, at which point showing it is actionable.
    })
  }, [ensureStream])

  const start = useCallback(async () => {
    setError(null)
    try {
      const stream = await ensureStream()
      chunksRef.current = []
      const recorder = new MediaRecorder(stream)
      mediaRecorderRef.current = recorder
      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunksRef.current.push(e.data)
      }
      recorder.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' })
        blobRef.current = blob
        setAudioUrl(URL.createObjectURL(blob))
        setDurationSeconds(Math.round((Date.now() - startedAtRef.current) / 1000))
        // Keep the stream open (don't stop its tracks) so the next take on
        // this page can start recording instantly instead of re-requesting
        // the mic and re-incurring this same startup lag.
        setStatus('recorded')
      }
      startedAtRef.current = Date.now()
      recorder.start()
      setStatus('recording')
    } catch {
      setError('Microphone access was denied or is unavailable.')
    }
  }, [ensureStream])

  const stop = useCallback(() => {
    mediaRecorderRef.current?.stop()
  }, [])

  const reset = useCallback(() => {
    if (audioUrl) URL.revokeObjectURL(audioUrl)
    blobRef.current = null
    chunksRef.current = []
    setAudioUrl(null)
    setDurationSeconds(0)
    setStatus('idle')
  }, [audioUrl])

  const release = useCallback(() => {
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
  }, [])

  // Release the mic when the page is left, however that happens.
  useEffect(() => release, [release])

  const getBlob = useCallback(() => blobRef.current, [])

  return { status, audioUrl, durationSeconds, error, prepare, start, stop, reset, getBlob }
}
