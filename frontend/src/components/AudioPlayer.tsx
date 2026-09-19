// Speaker audio is ML training data and must not be casually downloadable from
// the UI: hides the native download affordance and blocks the right-click
// "Save audio as…" menu. This only removes browser UI shortcuts — it cannot stop
// someone from extracting the response bytes at the network level, so it is not
// a substitute for the short-lived pre-signed URLs and role-based access control
// that actually gate who can request the audio in the first place.
export default function AudioPlayer({ src, className }: { src: string; className?: string }) {
  return (
    <audio
      controls
      controlsList="nodownload noremoteplayback"
      onContextMenu={(e) => e.preventDefault()}
      src={src}
      className={className}
    />
  )
}
