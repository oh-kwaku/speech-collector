import { useState } from 'react'
import { api } from '../../api/client'

type Busy = `${'main' | 'irr'}-${'csv' | 'xlsx'}` | null

export default function ExportPage() {
  const [busy, setBusy] = useState<Busy>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleExport(kind: 'main' | 'irr', format: 'csv' | 'xlsx') {
    setBusy(`${kind}-${format}`)
    setError(null)
    try {
      const res = await api.get(kind === 'main' ? '/admin/export' : '/admin/export/irr', {
        params: { format },
        responseType: 'blob',
      })
      const url = URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = url
      a.download = `${kind === 'main' ? 'recordings' : 'irr'}-export.${format}`
      document.body.appendChild(a)
      a.click()
      a.remove()
      URL.revokeObjectURL(url)
    } catch {
      setError('Export failed. Please try again.')
    } finally {
      setBusy(null)
    }
  }

  return (
    <div>
      <h1 className="mb-2 text-lg font-semibold text-slate-900">Export metadata</h1>
      <p className="mb-6 text-sm text-slate-500">
        Download all recording metadata: photoId, sessionId, userId, speakerId,
        annotation, created_at, speaker_gender, speaker_age.
      </p>
      {error && <p className="mb-4 text-sm text-red-600">{error}</p>}
      <div className="mb-8 flex gap-3">
        <button
          onClick={() => handleExport('main', 'csv')}
          disabled={busy !== null}
          className="rounded-lg bg-slate-900 px-4 py-3 font-medium text-white disabled:opacity-50"
        >
          {busy === 'main-csv' ? 'Preparing…' : 'Download CSV'}
        </button>
        <button
          onClick={() => handleExport('main', 'xlsx')}
          disabled={busy !== null}
          className="rounded-lg border border-slate-300 px-4 py-3 font-medium text-slate-700 disabled:opacity-50"
        >
          {busy === 'main-xlsx' ? 'Preparing…' : 'Download Excel'}
        </button>
      </div>

      <h2 className="mb-2 text-base font-semibold text-slate-900">IRR data</h2>
      <p className="mb-4 text-sm text-slate-500">
        One row per (recording, annotator) pair, for recordings with 2 or more independent
        annotations - use this to compute agreement/kappa outside the app. Recordings with a
        single annotation aren't included here (see the main export above for those).
      </p>
      <div className="flex gap-3">
        <button
          onClick={() => handleExport('irr', 'csv')}
          disabled={busy !== null}
          className="rounded-lg bg-slate-900 px-4 py-3 font-medium text-white disabled:opacity-50"
        >
          {busy === 'irr-csv' ? 'Preparing…' : 'Download IRR CSV'}
        </button>
        <button
          onClick={() => handleExport('irr', 'xlsx')}
          disabled={busy !== null}
          className="rounded-lg border border-slate-300 px-4 py-3 font-medium text-slate-700 disabled:opacity-50"
        >
          {busy === 'irr-xlsx' ? 'Preparing…' : 'Download IRR Excel'}
        </button>
      </div>
    </div>
  )
}
