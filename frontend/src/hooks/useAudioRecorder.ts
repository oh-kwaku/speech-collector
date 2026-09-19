import { useCallback, useRef, useState } from 'react'

export type RecorderStatus = 'idle' | 'recording' | 'recorded'

export function useAudioRecorder() {
  const [status, setStatus] = useState<RecorderStatus>('idle')
  const [audioUrl, setAudioUrl] = useState<string | null>(null)
  const [durationSeconds, setDurationSeconds] = useState(0)
  const [error, setError] = useState<string | null>(null)

  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const streamRef = useRef<MediaStream | null>(null)
  const startedAtRef = useRef<number>(0)
  const blobRef = useRef<Blob | null>(null)

  const start = useCallback(async () => {
    setError(null)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      streamRef.current = stream
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
        streamRef.current?.getTracks().forEach((t) => t.stop())
        setStatus('recorded')
      }
      startedAtRef.current = Date.now()
      recorder.start()
      setStatus('recording')
    } catch {
      setError('Microphone access was denied or is unavailable.')
    }
  }, [])

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

  const getBlob = useCallback(() => blobRef.current, [])

  return { status, audioUrl, durationSeconds, error, start, stop, reset, getBlob }
}
