import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createSpeaker, getOrCreateActiveSession, listSpeakers } from '../../api/speakers'
import type { Speaker } from '../../types'

export default function SpeakersListPage() {
  const [speakers, setSpeakers] = useState<Speaker[]>([])
  const [loading, setLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [gender, setGender] = useState('Female')
  const [ageYears, setAgeYears] = useState(6)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    listSpeakers()
      .then((res) => setSpeakers(res.data))
      .finally(() => setLoading(false))
  }, [])

  async function goToSpeaker(speakerId: string) {
    const session = await getOrCreateActiveSession(speakerId)
    navigate(`/collector/speakers/${speakerId}/sessions/${session.data.id}`)
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSaving(true)
    try {
      const res = await createSpeaker({ gender, ageYears })
      setShowForm(false)
      setSpeakers((prev) => [res.data, ...prev])
      await goToSpeaker(res.data.id)
    } catch {
      setError('Could not save the speaker. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-semibold text-slate-900">Speakers</h1>
        {!showForm && (
          <button
            onClick={() => setShowForm(true)}
            className="rounded-lg bg-slate-900 px-4 py-3 text-sm font-medium text-white"
          >
            + New speaker
          </button>
        )}
      </div>

      {showForm && (
        <form
          onSubmit={handleSave}
          className="mb-6 space-y-4 rounded-2xl border border-slate-200 bg-white p-4"
        >
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Gender</label>
            <select
              value={gender}
              onChange={(e) => setGender(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-3 text-base"
            >
              <option value="Female">Female</option>
              <option value="Male">Male</option>
              <option value="Other">Other</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Age</label>
            <input
              type="number"
              min={5}
              max={10}
              required
              value={ageYears}
              onChange={(e) => setAgeYears(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 px-3 py-3 text-base"
            />
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setShowForm(false)}
              className="flex-1 rounded-lg border border-slate-300 py-3 font-medium text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={saving}
              className="flex-1 rounded-lg bg-slate-900 py-3 font-medium text-white disabled:opacity-50"
            >
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      )}

      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : speakers.length === 0 ? (
        <p className="text-sm text-slate-500">No speakers yet. Tap "New speaker" to start.</p>
      ) : (
        <ul className="space-y-2">
          {speakers.map((s) => (
            <li key={s.id}>
              <button
                onClick={() => goToSpeaker(s.id)}
                className="flex w-full items-center justify-between rounded-xl border border-slate-200 bg-white px-4 py-4 text-left"
              >
                <span>
                  <span className="block font-medium text-slate-900">Speaker {s.id}</span>
                  <span className="block text-sm text-slate-500">
                    {s.gender}, age {s.ageYears}
                  </span>
                </span>
                <span className="text-slate-400">&rsaquo;</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
