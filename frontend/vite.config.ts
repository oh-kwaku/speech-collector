import fs from 'node:fs'
import path from 'node:path'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// Mobile browsers only allow microphone access (getUserMedia) on a secure
// context (HTTPS, or localhost on the same device). LAN testing from a phone
// needs real HTTPS, so we use a locally-trusted cert from mkcert when present
// (see README) and fall back to plain HTTP otherwise.
const certDir = path.resolve(import.meta.dirname, 'certs')
const keyPath = path.join(certDir, 'dev-key.pem')
const certPath = path.join(certDir, 'dev-cert.pem')
const hasCerts = fs.existsSync(keyPath) && fs.existsSync(certPath)

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: true,
    https: hasCerts ? { key: fs.readFileSync(keyPath), cert: fs.readFileSync(certPath) } : undefined,
  },
})
