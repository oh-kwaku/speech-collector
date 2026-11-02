import { useCallback, useEffect, useRef, useState } from 'react'

export type RecorderStatus = 'idle' | 'recording' | 'recorded'

// Output format: 16-bit PCM mono WAV at 16 kHz, the de facto standard input
// for ASR training (Whisper, wav2vec2, etc.). Raise to 48000 to keep the full
// band the mic captures, at ~3x the file size.
export const WAV_SAMPLE_RATE = 16000
export const WAV_MIME_TYPE = 'audio/wav'

// MediaRecorder can't produce WAV, so raw samples are tapped off the mic with
// an AudioWorklet instead. Buffered into ~4096-frame chunks so the main thread
// isn't messaged every 128-frame render quantum. Inlined as a Blob URL so it
// works under any deploy base path (Docker `/`, GitHub Pages `/speech-collector/`).
const WORKLET_SOURCE = `
class PcmCaptureProcessor extends AudioWorkletProcessor {
  constructor() {
    super()
    this.buffer = []
    this.size = 0
    this.port.onmessage = (e) => {
      if (e.data === 'flush') {
        this.send()
        this.port.postMessage('done')
      }
    }
  }
  send() {
    if (this.size === 0) return
    const out = new Float32Array(this.size)
    let offset = 0
    for (const chunk of this.buffer) {
      out.set(chunk, offset)
      offset += chunk.length
    }
    this.port.postMessage(out, [out.buffer])
    this.buffer = []
    this.size = 0
  }
  process(inputs) {
    const channels = inputs[0]
    if (channels && channels.length > 0) {
      // Downmix to mono in case the device ignores channelCount: 1.
      const frame = new Float32Array(channels[0].length)
      for (const ch of channels) for (let i = 0; i < ch.length; i++) frame[i] += ch[i] / channels.length
      this.buffer.push(frame)
      this.size += frame.length
      if (this.size >= 4096) this.send()
    }
    return true
  }
}
registerProcessor('pcm-capture', PcmCaptureProcessor)
`

// Browser defaults for getUserMedia are tuned for video calls: echo
// cancellation, noise suppression and auto gain all audibly degrade speech
// (children's high-pitched voices especially) and aren't wanted in training data.
const INSECURE_CONTEXT_ERROR =
  'Microphone is unavailable: this page must be opened over HTTPS (or on localhost) to record audio.'

const MIC_CONSTRAINTS: MediaTrackConstraints = {
  echoCancellation: false,
  noiseSuppression: false,
  autoGainControl: false,
  channelCount: 1,
}

export function useAudioRecorder() {
  const [status, setStatus] = useState<RecorderStatus>('idle')
  const [audioUrl, setAudioUrl] = useState<string | null>(null)
  const [durationSeconds, setDurationSeconds] = useState(0)
  const [error, setError] = useState<string | null>(null)

  const streamRef = useRef<MediaStream | null>(null)
  const streamPromiseRef = useRef<Promise<MediaStream> | null>(null)
  const contextRef = useRef<AudioContext | null>(null)
  const workletReadyRef = useRef<Promise<void> | null>(null)
  const sourceRef = useRef<MediaStreamAudioSourceNode | null>(null)
  const nodeRef = useRef<AudioWorkletNode | null>(null)
  const chunksRef = useRef<Float32Array[]>([])
  const blobRef = useRef<Blob | null>(null)

  // Acquires (and caches) the mic stream without starting a recording, so the
  // getUserMedia/permission-prompt latency happens while the child is still
  // looking at the photo instead of after they've already started talking.
  const ensureStream = useCallback(() => {
    if (streamRef.current?.active) return Promise.resolve(streamRef.current)
    // navigator.mediaDevices only exists in a secure context (HTTPS or
    // localhost). Reject rather than throw, so callers' .catch handles it
    // instead of the error escaping and unmounting the whole page.
    if (!navigator.mediaDevices?.getUserMedia) {
      return Promise.reject(new Error(INSECURE_CONTEXT_ERROR))
    }
    if (!streamPromiseRef.current) {
      streamPromiseRef.current = navigator.mediaDevices
        .getUserMedia({ audio: MIC_CONSTRAINTS })
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

  // Created lazily on the first Record tap (a user gesture), since iOS Safari
  // keeps an AudioContext created outside one permanently suspended. Runs at
  // the device's native rate; resampling to WAV_SAMPLE_RATE happens on stop.
  const ensureContext = useCallback(async () => {
    if (!contextRef.current || contextRef.current.state === 'closed') {
      contextRef.current = new AudioContext()
      workletReadyRef.current = null
    }
    const ctx = contextRef.current
    if (!workletReadyRef.current) {
      const url = URL.createObjectURL(new Blob([WORKLET_SOURCE], { type: 'application/javascript' }))
      workletReadyRef.current = ctx.audioWorklet.addModule(url).finally(() => URL.revokeObjectURL(url))
    }
    await workletReadyRef.current
    if (ctx.state === 'suspended') await ctx.resume()
    return ctx
  }, [])

  const prepare = useCallback(() => {
    ensureStream().catch((e) => {
      // No mic API at all can't be fixed by tapping Record, so say so now.
      if (e instanceof Error && e.message === INSECURE_CONTEXT_ERROR) setError(INSECURE_CONTEXT_ERROR)
      // Otherwise swallow here — the same error will surface when the user
      // actually taps Record, at which point showing it is actionable.
    })
  }, [ensureStream])

  const start = useCallback(async () => {
    setError(null)
    try {
      const stream = await ensureStream()
      const ctx = await ensureContext()
      chunksRef.current = []
      const source = ctx.createMediaStreamSource(stream)
      const node = new AudioWorkletNode(ctx, 'pcm-capture', { numberOfOutputs: 1 })
      node.port.onmessage = (e) => {
        if (e.data instanceof Float32Array) chunksRef.current.push(e.data)
      }
      // The node writes silence to its output; it's only wired to the
      // destination so every browser keeps pulling it through the graph.
      source.connect(node).connect(ctx.destination)
      sourceRef.current = source
      nodeRef.current = node
      setStatus('recording')
    } catch (e) {
      setError(
        e instanceof Error && e.message === INSECURE_CONTEXT_ERROR
          ? INSECURE_CONTEXT_ERROR
          : 'Microphone access was denied or is unavailable.',
      )
    }
  }, [ensureStream, ensureContext])

  const stop = useCallback(async () => {
    const ctx = contextRef.current
    const source = sourceRef.current
    const node = nodeRef.current
    if (!ctx || !source || !node) return
    sourceRef.current = null
    nodeRef.current = null

    // Drain whatever the worklet still has buffered before tearing it down.
    await new Promise<void>((resolve) => {
      node.port.onmessage = (e) => {
        if (e.data instanceof Float32Array) chunksRef.current.push(e.data)
        else if (e.data === 'done') resolve()
      }
      node.port.postMessage('flush')
    })
    source.disconnect()
    node.disconnect()

    const pcm = await resample(concat(chunksRef.current), ctx.sampleRate, WAV_SAMPLE_RATE)
    chunksRef.current = []
    const blob = encodeWav(pcm, WAV_SAMPLE_RATE)
    blobRef.current = blob
    setAudioUrl(URL.createObjectURL(blob))
    setDurationSeconds(Math.round(pcm.length / WAV_SAMPLE_RATE))
    // Keep the stream (and context) open so the next take on this page can
    // start recording instantly instead of re-requesting the mic and
    // re-incurring this same startup lag.
    setStatus('recorded')
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
    sourceRef.current?.disconnect()
    nodeRef.current?.disconnect()
    sourceRef.current = null
    nodeRef.current = null
    contextRef.current?.close().catch(() => {})
    contextRef.current = null
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
  }, [])

  // Release the mic when the page is left, however that happens.
  useEffect(() => release, [release])

  const getBlob = useCallback(() => blobRef.current, [])

  return { status, audioUrl, durationSeconds, error, prepare, start, stop, reset, getBlob }
}

function concat(chunks: Float32Array[]) {
  const out = new Float32Array(chunks.reduce((n, c) => n + c.length, 0))
  let offset = 0
  for (const c of chunks) {
    out.set(c, offset)
    offset += c.length
  }
  return out
}

// Uses the browser's own resampler (band-limited, so no aliasing) rather than
// naive sample dropping.
async function resample(samples: Float32Array<ArrayBuffer>, fromRate: number, toRate: number) {
  if (fromRate === toRate || samples.length === 0) return samples
  const length = Math.ceil((samples.length * toRate) / fromRate)
  const offline = new OfflineAudioContext(1, length, toRate)
  const buffer = offline.createBuffer(1, samples.length, fromRate)
  buffer.copyToChannel(samples, 0)
  const src = offline.createBufferSource()
  src.buffer = buffer
  src.connect(offline.destination)
  src.start()
  const rendered = await offline.startRendering()
  return rendered.getChannelData(0)
}

// 16-bit PCM mono RIFF/WAVE.
function encodeWav(samples: Float32Array, sampleRate: number) {
  const bytesPerSample = 2
  const dataSize = samples.length * bytesPerSample
  const view = new DataView(new ArrayBuffer(44 + dataSize))
  const writeString = (offset: number, s: string) => {
    for (let i = 0; i < s.length; i++) view.setUint8(offset + i, s.charCodeAt(i))
  }
  writeString(0, 'RIFF')
  view.setUint32(4, 36 + dataSize, true)
  writeString(8, 'WAVE')
  writeString(12, 'fmt ')
  view.setUint32(16, 16, true) // fmt chunk size
  view.setUint16(20, 1, true) // PCM
  view.setUint16(22, 1, true) // mono
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * bytesPerSample, true) // byte rate
  view.setUint16(32, bytesPerSample, true) // block align
  view.setUint16(34, 16, true) // bits per sample
  writeString(36, 'data')
  view.setUint32(40, dataSize, true)
  for (let i = 0, offset = 44; i < samples.length; i++, offset += 2) {
    const s = Math.max(-1, Math.min(1, samples[i]))
    view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7fff, true)
  }
  return new Blob([view], { type: WAV_MIME_TYPE })
}
