import { useEffect, useState } from 'react'
import axios from 'axios'
import { useNavigate } from 'react-router-dom'
import { createSpeaker, getOrCreateActiveSession, listSpeakers, updateSpeaker } from '../../api/speakers'
import type { Speaker } from '../../types'

const AGE_OPTIONS = Array.from({ length: 10 }, (_, i) => i + 3)

function GenderAgeFields({
  gender,
  ageYears,
  onGenderChange,
  onAgeChange,
}: {
  gender: string
  ageYears: string
  onGenderChange: (v: string) => void
  onAgeChange: (v: string) => void
}) {
  return (
    <>
      <div>
        <label className="mb-1 block text-sm font-medium text-slate-700">Gender</label>
        <select
          value={gender}
          onChange={(e) => onGenderChange(e.target.value)}
          required
          className="w-full rounded-lg border border-slate-300 px-3 py-3 text-base"
        >
          <option value="" disabled>
            Select gender
          </option>
          <option value="Female">Female</option>
          <option value="Male">Male</option>
        </select>
      </div>
      <div>
        <label className="mb-1 block text-sm font-medium text-slate-700">Age</label>
        <select
          value={ageYears}
          onChange={(e) => onAgeChange(e.target.value)}
          required
          className="w-full rounded-lg border border-slate-300 px-3 py-3 text-base"
        >
          <option value="" disabled>
            Select age
          </option>
          {AGE_OPTIONS.map((age) => (
            <option key={age} value={age}>
              {age}
            </option>
          ))}
        </select>
      </div>
    </>
  )
}

export default function SpeakersListPage() {
  const [speakers, setSpeakers] = useState<Speaker[]>([])
  const [loading, setLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [gender, setGender] = useState('')
  const [ageYears, setAgeYears] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [editingId, setEditingId] = useState<string | null>(null)
  const [editGender, setEditGender] = useState('')
  const [editAgeYears, setEditAgeYears] = useState('')
  const [editSaving, setEditSaving] = useState(false)
  const [editError, setEditError] = useState<string | null>(null)

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

  function openForm() {
    setGender('')
    setAgeYears('')
    setError(null)
    setShowForm(true)
  }

  function closeForm() {
    setShowForm(false)
    setGender('')
    setAgeYears('')
    setError(null)
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault()
    setError(null)

    const age = Number(ageYears)
    if (!gender) {
      setError('Please select a gender.')
      return
    }
    if (!ageYears || Number.isNaN(age) || age < 3 || age > 12) {
      setError('Age must be between 3 and 12.')
      return
    }

    setSaving(true)
    try {
      const res = await createSpeaker({ gender, ageYears: age })
      closeForm()
      setSpeakers((prev) => [res.data, ...prev])
      await goToSpeaker(res.data.id)
    } catch (err) {
      const message =
        (axios.isAxiosError(err) && typeof err.response?.data === 'string' && err.response.data) ||
        'Could not save the speaker. Please try again.'
      setError(message)
    } finally {
      setSaving(false)
    }
  }

  function openEdit(s: Speaker) {
    setEditingId(s.id)
    setEditGender(s.gender)
    setEditAgeYears(String(s.ageYears))
    setEditError(null)
  }

  function closeEdit() {
    setEditingId(null)
    setEditGender('')
    setEditAgeYears('')
    setEditError(null)
  }

  async function handleEditSave(e: React.FormEvent, speakerId: string) {
    e.preventDefault()
    setEditError(null)

    const age = Number(editAgeYears)
    if (!editGender) {
      setEditError('Please select a gender.')
      return
    }
    if (!editAgeYears || Number.isNaN(age) || age < 3 || age > 12) {
      setEditError('Age must be between 3 and 12.')
      return
    }

    setEditSaving(true)
    try {
      const res = await updateSpeaker(speakerId, { gender: editGender, ageYears: age })
      setSpeakers((prev) => prev.map((s) => (s.id === speakerId ? res.data : s)))
      closeEdit()
    } catch (err) {
      const message =
        (axios.isAxiosError(err) && typeof err.response?.data === 'string' && err.response.data) ||
        'Could not save changes. Please try again.'
      setEditError(message)
    } finally {
      setEditSaving(false)
    }
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-semibold text-slate-900">Speakers</h1>
        {!showForm && (
          <button
            onClick={openForm}
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
          <GenderAgeFields
            gender={gender}
            ageYears={ageYears}
            onGenderChange={setGender}
            onAgeChange={setAgeYears}
          />
          {error && <p className="text-sm text-red-600">{error}</p>}
          <div className="flex gap-2">
            <button
              type="button"
              onClick={closeForm}
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
          {speakers.map((s) =>
            editingId === s.id ? (
              <li key={s.id}>
                <form
                  onSubmit={(e) => handleEditSave(e, s.id)}
                  className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4"
                >
                  <p className="text-sm font-medium text-slate-900">Editing Speaker {s.id}</p>
                  <GenderAgeFields
                    gender={editGender}
                    ageYears={editAgeYears}
                    onGenderChange={setEditGender}
                    onAgeChange={setEditAgeYears}
                  />
                  {editError && <p className="text-sm text-red-600">{editError}</p>}
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={closeEdit}
                      className="flex-1 rounded-lg border border-slate-300 py-3 font-medium text-slate-700"
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      disabled={editSaving}
                      className="flex-1 rounded-lg bg-slate-900 py-3 font-medium text-white disabled:opacity-50"
                    >
                      {editSaving ? 'Saving…' : 'Save'}
                    </button>
                  </div>
                </form>
              </li>
            ) : (
              <li
                key={s.id}
                className="flex items-center justify-between rounded-xl border border-slate-200 bg-white px-4 py-4"
              >
                <button onClick={() => goToSpeaker(s.id)} className="flex-1 text-left">
                  <span className="block font-medium text-slate-900">Speaker {s.id}</span>
                  <span className="block text-sm text-slate-500">
                    {s.gender}, age {s.ageYears}
                  </span>
                </button>
                <button
                  onClick={() => openEdit(s)}
                  className="ml-3 shrink-0 rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700"
                >
                  Edit
                </button>
              </li>
            ),
          )}
        </ul>
      )}
    </div>
  )
}
