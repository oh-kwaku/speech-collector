import { useState } from 'react'
import { api } from '../../api/client'

export default function ExportPage() {
  const [busy, setBusy] = useState<'csv' | 'xlsx' | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleExport(format: 'csv' | 'xlsx') {
    setBusy(format)
    setError(null)
    try {
      const res = await api.get('/admin/export', {
        params: { format },
        responseType: 'blob',
      })
      const url = URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = url
      a.download = `recordings-export.${format}`
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
      <div className="flex gap-3">
        <button
          onClick={() => handleExport('csv')}
          disabled={busy !== null}
          className="rounded-lg bg-slate-900 px-4 py-3 font-medium text-white disabled:opacity-50"
        >
          {busy === 'csv' ? 'Preparing…' : 'Download CSV'}
        </button>
        <button
          onClick={() => handleExport('xlsx')}
          disabled={busy !== null}
          className="rounded-lg border border-slate-300 px-4 py-3 font-medium text-slate-700 disabled:opacity-50"
        >
          {busy === 'xlsx' ? 'Preparing…' : 'Download Excel'}
        </button>
      </div>
    </div>
  )
}
