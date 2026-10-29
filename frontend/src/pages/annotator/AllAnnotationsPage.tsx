import { useEffect, useState } from 'react'
import { isAxiosError } from 'axios'
import { deleteAnnotation, listAnnotations } from '../../api/annotations'
import AudioPlayer from '../../components/AudioPlayer'
import PhotoModal from '../../components/PhotoModal'
import { useAuth } from '../../auth/AuthContext'
import type { Annotation } from '../../types'

export default function AllAnnotationsPage() {
  const { user } = useAuth()
  const [annotations, setAnnotations] = useState<Annotation[]>([])
  const [loading, setLoading] = useState(true)
  const [viewingPhoto, setViewingPhoto] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const isAdmin = user?.roles.includes('Admin') ?? false

  useEffect(() => {
    listAnnotations()
      .then((res) => setAnnotations(res.data))
      .finally(() => setLoading(false))
  }, [])

  async function handleDelete(a: Annotation) {
    if (
      !window.confirm(
        `Delete this annotation for speaker ${a.speakerId}? The recording is kept and goes back into the annotation queue.`,
      )
    )
      return
    setDeleteError(null)
    setDeletingId(a.id)
    try {
      await deleteAnnotation(a.id)
      setAnnotations((prev) => prev.filter((x) => x.id !== a.id))
    } catch (err) {
      const msg = isAxiosError(err) && typeof err.response?.data === 'string' ? err.response.data : null
      setDeleteError(msg ?? 'Could not delete the annotation.')
    } finally {
      setDeletingId(null)
    }
  }

  if (loading) return <p className="text-sm text-slate-500">Loading…</p>

  return (
    <div>
      <h1 className="mb-4 text-lg font-semibold text-slate-900">All annotations</h1>
      {deleteError && <p className="mb-4 text-sm text-red-600">{deleteError}</p>}
      {annotations.length === 0 ? (
        <p className="text-sm text-slate-500">No annotations yet.</p>
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="w-full min-w-[700px] text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-slate-500">
              <tr>
                <th className="px-4 py-2">Speaker</th>
                <th className="px-4 py-2">Photo</th>
                <th className="px-4 py-2">Audio</th>
                <th className="px-4 py-2">Annotation</th>
                {isAdmin && <th className="px-4 py-2">Collector</th>}
                {isAdmin && <th className="px-4 py-2">Annotator</th>}
                <th className="px-4 py-2">Last updated</th>
                {isAdmin && <th className="px-4 py-2" />}
              </tr>
            </thead>
            <tbody>
              {annotations.map((a) => (
                <tr key={a.id} className="border-b border-slate-100 last:border-0">
                  <td className="px-4 py-3 text-xs text-slate-500">{a.speakerId}</td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => setViewingPhoto(a.photoUrl)}
                      className="text-sm font-medium text-slate-700 underline"
                    >
                      View photo
                    </button>
                  </td>
                  <td className="px-4 py-3">
                    <AudioPlayer src={a.audioUrl} className="h-8 w-56" />
                  </td>
                  <td className="px-4 py-3">{a.text}</td>
                  {isAdmin && (
                    <td className="px-4 py-3 text-slate-500">
                      {a.collectorEmail ?? a.collectorPhoneNumber ?? a.collectorUserId}
                    </td>
                  )}
                  {isAdmin && (
                    <td className="px-4 py-3 text-slate-500">
                      {a.annotatorEmail ?? a.annotatorPhoneNumber ?? a.annotatorUserId}
                    </td>
                  )}
                  <td className="px-4 py-3 text-slate-500">
                    {new Date(a.updatedAt).toLocaleString()}
                  </td>
                  {isAdmin && (
                    <td className="px-4 py-3">
                      <button
                        onClick={() => handleDelete(a)}
                        disabled={deletingId === a.id}
                        className="rounded-lg border border-red-300 px-3 py-1 text-xs font-medium text-red-700 disabled:opacity-50"
                      >
                        {deletingId === a.id ? 'Deleting…' : 'Delete'}
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {viewingPhoto && <PhotoModal src={viewingPhoto} onClose={() => setViewingPhoto(null)} />}
    </div>
  )
}
