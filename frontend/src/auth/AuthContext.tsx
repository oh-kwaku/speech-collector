import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { fetchMe } from '../api/auth'
import { getStoredTokens, storeTokens } from '../api/client'
import type { AuthTokens, AuthUser } from '../types'

interface AuthContextValue {
  user: AuthUser | null
  loading: boolean
  signIn: (tokens: AuthTokens, user: AuthUser) => void
  signOut: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const tokens = getStoredTokens()
    if (!tokens) {
      setLoading(false)
      return
    }
    fetchMe()
      .then((res) => setUser(res.data))
      .catch(() => storeTokens(null))
      .finally(() => setLoading(false))
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      signIn: (tokens, signedInUser) => {
        storeTokens(tokens)
        setUser(signedInUser)
      },
      signOut: () => {
        storeTokens(null)
        setUser(null)
      },
    }),
    [user, loading],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
