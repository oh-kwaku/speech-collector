import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { requestOtp, verifyOtp } from '../api/auth'
import { useAuth } from '../auth/AuthContext'
import type { OtpChannel } from '../types'

export default function LoginPage() {
  const [channel, setChannel] = useState<OtpChannel>('Email')
  const [identifier, setIdentifier] = useState('')
  const [code, setCode] = useState('')
  const [step, setStep] = useState<'request' | 'verify'>('request')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const { signIn } = useAuth()
  const navigate = useNavigate()

  async function handleRequest(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await requestOtp(identifier.trim(), channel)
      setStep('verify')
    } catch {
      setError('Could not send a code. Check the address/number and try again.')
    } finally {
      setBusy(false)
    }
  }

  async function handleVerify(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const res = await verifyOtp(identifier.trim(), channel, code.trim())
      const { user, ...tokens } = res.data
      signIn(tokens, user)
      navigate('/', { replace: true })
    } catch {
      setError('That code is invalid or expired.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-sm">
        <h1 className="mb-1 text-xl font-semibold text-slate-900">Sign in</h1>
        <p className="mb-6 text-sm text-slate-500">
          {step === 'request'
            ? 'Enter your email or phone number to receive a one-time code.'
            : `Enter the code sent to ${identifier}.`}
        </p>

        {step === 'request' ? (
          <form onSubmit={handleRequest} className="space-y-4">
            <div className="flex gap-2 rounded-lg bg-slate-100 p-1 text-sm">
              {(['Email', 'Sms'] as OtpChannel[]).map((c) => (
                <button
                  type="button"
                  key={c}
                  onClick={() => setChannel(c)}
                  className={`flex-1 rounded-md py-2 font-medium transition ${
                    channel === c ? 'bg-white shadow-sm text-slate-900' : 'text-slate-500'
                  }`}
                >
                  {c === 'Email' ? 'Email' : 'Phone'}
                </button>
              ))}
            </div>
            <input
              type={channel === 'Email' ? 'email' : 'tel'}
              required
              placeholder={channel === 'Email' ? 'you@example.com' : '+1 555 123 4567'}
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-3 text-base outline-none focus:border-slate-500"
            />
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button
              type="submit"
              disabled={busy}
              className="w-full rounded-lg bg-slate-900 py-3 font-medium text-white disabled:opacity-50"
            >
              {busy ? 'Sending…' : 'Send code'}
            </button>
          </form>
        ) : (
          <form onSubmit={handleVerify} className="space-y-4">
            <input
              type="text"
              inputMode="numeric"
              required
              placeholder="6-digit code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-3 text-center text-lg tracking-widest outline-none focus:border-slate-500"
            />
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button
              type="submit"
              disabled={busy}
              className="w-full rounded-lg bg-slate-900 py-3 font-medium text-white disabled:opacity-50"
            >
              {busy ? 'Verifying…' : 'Verify & sign in'}
            </button>
            <button
              type="button"
              onClick={() => setStep('request')}
              className="w-full text-sm text-slate-500"
            >
              Use a different email/phone
            </button>
          </form>
        )}
      </div>
    </div>
  )
}
