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
  // GitHub Pages serves this project site at
  // https://<owner>.github.io/speech-collector/, so assets must be
  // requested from that subpath rather than the domain root — but Dokploy/
  // Docker deployments serve from the domain root, and nginx.conf there
  // only maps a plain "/assets/" prefix, so this must NOT apply to those
  // builds: doing so unconditionally baked "/speech-collector/" into
  // index.html's asset paths for the Docker image too, 404ing every JS/CSS
  // request (nginx's SPA fallback then served index.html back in place of
  // the missing bundle, i.e. a blank page). Only `npm run deploy`/the
  // gh-pages workflow set GH_PAGES=true. (2026-09-20)
  base: process.env.GH_PAGES === 'true' ? '/speech-collector/' : '/',
  plugins: [react(), tailwindcss()],
  server: {
    host: true,
    https: hasCerts ? { key: fs.readFileSync(keyPath), cert: fs.readFileSync(certPath) } : undefined,
  },
})
