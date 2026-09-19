import { useEffect, useState } from 'react'
import { inviteUser, listUsers, updateUserProfile } from '../../api/admin'
import type { AdminUser, Role } from '../../types'

const ALL_ROLES: Role[] = ['Collector', 'Annotator', 'Admin']

// Admin is exclusive: picking it clears any other role, and picking a non-Admin
// role while Admin is set drops Admin in favor of the new selection.
function toggleRole(current: Role[], role: Role): Role[] {
  if (role === 'Admin') return current.includes('Admin') ? [] : ['Admin']
  if (current.includes('Admin')) return [role]
  return current.includes(role) ? current.filter((r) => r !== role) : [...current, role]
}

function RoleCheckboxes({ value, onChange }: { value: Role[]; onChange: (roles: Role[]) => void }) {
  return (
    <div className="flex flex-wrap gap-3">
      {ALL_ROLES.map((r) => (
        <label key={r} className="flex items-center gap-1.5 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={value.includes(r)}
            onChange={() => onChange(toggleRole(value, r))}
          />
          {r}
        </label>
      ))}
    </div>
  )
}

interface EditDraft {
  name: string
  phoneNumber: string
  location: string
  roles: Role[]
}

function toDraft(u: AdminUser): EditDraft {
  return { name: u.name ?? '', phoneNumber: u.phoneNumber ?? '', location: u.location ?? '', roles: u.roles }
}

export default function UsersPage() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)

  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [location, setLocation] = useState('')
  const [roles, setRoles] = useState<Role[]>(['Collector'])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [editingId, setEditingId] = useState<string | null>(null)
  const [draft, setDraft] = useState<EditDraft>({ name: '', phoneNumber: '', location: '', roles: [] })
  const [savingEdit, setSavingEdit] = useState(false)

  useEffect(() => {
    refresh()
  }, [])

  function refresh() {
    setLoading(true)
    listUsers()
      .then((res) => setUsers(res.data))
      .finally(() => setLoading(false))
  }

  async function handleInvite(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!email.trim() && !phoneNumber.trim()) {
      setError('An email or phone number is required.')
      return
    }
    if (roles.length === 0) {
      setError('At least one role is required.')
      return
    }
    setBusy(true)
    try {
      await inviteUser({
        name: name.trim() || undefined,
        email: email.trim() || undefined,
        phoneNumber: phoneNumber.trim() || undefined,
        location: location.trim() || undefined,
        roles,
      })
      setName('')
      setEmail('')
      setPhoneNumber('')
      setLocation('')
      setRoles(['Collector'])
      refresh()
    } catch {
      setError('Could not send the invite.')
    } finally {
      setBusy(false)
    }
  }

  function startEdit(u: AdminUser) {
    setEditingId(u.id)
    setDraft(toDraft(u))
  }

  function cancelEdit() {
    setEditingId(null)
  }

  async function saveEdit(userId: string) {
    if (draft.roles.length === 0) return
    setSavingEdit(true)
    try {
      const updated = await updateUserProfile(userId, {
        name: draft.name.trim() || undefined,
        phoneNumber: draft.phoneNumber.trim() || undefined,
        location: draft.location.trim() || undefined,
        roles: draft.roles,
      })
      setUsers((prev) => prev.map((u) => (u.id === userId ? updated.data : u)))
      setEditingId(null)
    } finally {
      setSavingEdit(false)
    }
  }

  return (
    <div>
      <h1 className="mb-4 text-lg font-semibold text-slate-900">Users</h1>

      <form
        onSubmit={handleInvite}
        className="mb-6 flex flex-wrap items-end gap-3 rounded-2xl border border-slate-200 bg-white p-4"
      >
        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Name</label>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="rounded-lg border border-slate-300 px-3 py-2"
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Email</label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="rounded-lg border border-slate-300 px-3 py-2"
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Phone number</label>
          <input
            value={phoneNumber}
            onChange={(e) => setPhoneNumber(e.target.value)}
            className="rounded-lg border border-slate-300 px-3 py-2"
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Location</label>
          <input
            value={location}
            onChange={(e) => setLocation(e.target.value)}
            className="rounded-lg border border-slate-300 px-3 py-2"
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-slate-700">Roles</label>
          <RoleCheckboxes value={roles} onChange={setRoles} />
        </div>
        <button
          type="submit"
          disabled={busy}
          className="rounded-lg bg-slate-900 px-4 py-2 font-medium text-white disabled:opacity-50"
        >
          {busy ? 'Inviting…' : 'Send invite'}
        </button>
        {error && <p className="w-full text-sm text-red-600">{error}</p>}
        <p className="w-full text-xs text-slate-400">
          At least one of email or phone number is required (used to deliver the OTP code).
        </p>
      </form>

      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="w-full min-w-[800px] text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-slate-500">
              <tr>
                <th className="px-4 py-2">Name</th>
                <th className="px-4 py-2">Contact</th>
                <th className="px-4 py-2">Location</th>
                <th className="px-4 py-2">Roles</th>
                <th className="px-4 py-2">Status</th>
                <th className="px-4 py-2">Invited</th>
                <th className="px-4 py-2"></th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) =>
                editingId === u.id ? (
                  <tr key={u.id} className="border-b border-slate-100 last:border-0">
                    <td className="px-4 py-2">
                      <input
                        value={draft.name}
                        onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))}
                        className="w-full rounded-lg border border-slate-300 px-2 py-1"
                      />
                    </td>
                    <td className="px-4 py-2 text-xs text-slate-500">
                      {u.email ?? '—'}
                      <input
                        value={draft.phoneNumber}
                        onChange={(e) => setDraft((d) => ({ ...d, phoneNumber: e.target.value }))}
                        placeholder="Phone number"
                        className="mt-1 w-full rounded-lg border border-slate-300 px-2 py-1"
                      />
                    </td>
                    <td className="px-4 py-2">
                      <input
                        value={draft.location}
                        onChange={(e) => setDraft((d) => ({ ...d, location: e.target.value }))}
                        className="w-full rounded-lg border border-slate-300 px-2 py-1"
                      />
                    </td>
                    <td className="px-4 py-3">
                      <RoleCheckboxes
                        value={draft.roles}
                        onChange={(r) => setDraft((d) => ({ ...d, roles: r }))}
                      />
                    </td>
                    <td className="px-4 py-3">{u.status}</td>
                    <td className="px-4 py-3 text-slate-500">
                      {new Date(u.createdAt).toLocaleDateString()}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <button
                        onClick={() => saveEdit(u.id)}
                        disabled={savingEdit}
                        className="mr-2 rounded-lg bg-slate-900 px-3 py-1 text-xs font-medium text-white disabled:opacity-50"
                      >
                        Save
                      </button>
                      <button
                        onClick={cancelEdit}
                        disabled={savingEdit}
                        className="rounded-lg border border-slate-300 px-3 py-1 text-xs font-medium text-slate-700"
                      >
                        Cancel
                      </button>
                    </td>
                  </tr>
                ) : (
                  <tr key={u.id} className="border-b border-slate-100 last:border-0">
                    <td className="px-4 py-3">{u.name ?? '—'}</td>
                    <td className="px-4 py-3 text-xs text-slate-500">
                      {u.email && <div>{u.email}</div>}
                      {u.phoneNumber && <div>{u.phoneNumber}</div>}
                      {!u.email && !u.phoneNumber && '—'}
                    </td>
                    <td className="px-4 py-3">{u.location ?? '—'}</td>
                    <td className="px-4 py-3">{u.roles.join(', ')}</td>
                    <td className="px-4 py-3">{u.status}</td>
                    <td className="px-4 py-3 text-slate-500">
                      {new Date(u.createdAt).toLocaleDateString()}
                    </td>
                    <td className="px-4 py-3">
                      <button
                        onClick={() => startEdit(u)}
                        className="rounded-lg border border-slate-300 px-3 py-1 text-xs font-medium text-slate-700"
                      >
                        Edit
                      </button>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
