import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const NAV_BY_ROLE: Record<string, { to: string; label: string }[]> = {
  Collector: [
    { to: '/collector/speakers', label: 'Speakers' },
    { to: '/collector/history', label: 'History' },
    { to: '/dashboard', label: 'Dashboard' },
  ],
  Annotator: [
    { to: '/annotator/queue', label: 'To annotate' },
    { to: '/annotator/all', label: 'All annotations' },
    { to: '/dashboard', label: 'Dashboard' },
  ],
  Admin: [
    { to: '/admin/users', label: 'Users' },
    { to: '/admin/recordings', label: 'Recordings' },
    { to: '/admin/export', label: 'Export' },
    { to: '/admin/irr', label: 'IRR' },
    { to: '/annotator/queue', label: 'To annotate' },
    { to: '/annotator/all', label: 'All annotations' },
    { to: '/dashboard', label: 'Dashboard' },
  ],
}

export default function AppShell() {
  const { user, signOut } = useAuth()
  const location = useLocation()
  const [menuOpen, setMenuOpen] = useState(false)

  // A user can hold multiple roles (except Admin, which is exclusive) — show the
  // union of each role's menu items so they can reach everything they need to do.
  const links = user
    ? user.roles
        .flatMap((r) => NAV_BY_ROLE[r] ?? [])
        .filter((link, i, all) => all.findIndex((l) => l.to === link.to) === i)
    : []

  useEffect(() => {
    setMenuOpen(false)
  }, [location.pathname])

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `whitespace-nowrap rounded-md px-3 py-2 text-sm font-medium ${
      isActive ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'
    }`

  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-4 px-4 py-3">
          <span className="font-semibold text-slate-900">Speech Collector</span>
          <nav className="hidden flex-1 gap-1 overflow-x-auto md:flex">
            {links.map((l) => (
              <NavLink key={l.to} to={l.to} className={navLinkClass}>
                {l.label}
              </NavLink>
            ))}
          </nav>
          <button
            onClick={signOut}
            className="hidden rounded-md px-3 py-2 text-sm font-medium text-slate-500 hover:bg-slate-100 md:block"
          >
            Sign out
          </button>
          <button
            onClick={() => setMenuOpen((open) => !open)}
            aria-label={menuOpen ? 'Close menu' : 'Open menu'}
            aria-expanded={menuOpen}
            className="flex items-center justify-center rounded-md p-2 text-slate-600 hover:bg-slate-100 md:hidden"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth={2}
              strokeLinecap="round"
              strokeLinejoin="round"
              className="h-6 w-6"
            >
              {menuOpen ? <path d="M6 6l12 12M18 6l-12 12" /> : <path d="M4 6h16M4 12h16M4 18h16" />}
            </svg>
          </button>
        </div>
        {menuOpen && (
          <nav className="flex flex-col gap-1 border-t border-slate-200 bg-white px-4 py-3 md:hidden">
            {links.map((l) => (
              <NavLink key={l.to} to={l.to} className={navLinkClass}>
                {l.label}
              </NavLink>
            ))}
            <button
              onClick={signOut}
              className="mt-1 rounded-md px-3 py-2 text-left text-sm font-medium text-slate-500 hover:bg-slate-100"
            >
              Sign out
            </button>
          </nav>
        )}
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
