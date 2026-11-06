import { useEffect, useState } from 'react'
import { api, formatVersion, groupSnapshotsByHost, isRestorableArchive, restoreArchiveName } from './api'
import type { ArchiveInfo, BuildInfo, CatalogNode, Identity, SnapshotInfo } from './api'
import { Tree } from './Tree'

export default function App() {
  const [identity, setIdentity] = useState<Identity | null>(null)
  const [buildInfo, setBuildInfo] = useState<BuildInfo | null>(null)
  const [snapshots, setSnapshots] = useState<SnapshotInfo[]>([])
  const [snapshot, setSnapshot] = useState('')
  const [archives, setArchives] = useState<ArchiveInfo[]>([])
  const [archive, setArchive] = useState('')
  // The snapshot that `archives` was loaded for.
  const [archivesOf, setArchivesOf] = useState('')
  const [tree, setTree] = useState<CatalogNode | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    api
      .me()
      .then(setIdentity)
      .catch(() => undefined)
    api
      .version()
      .then(setBuildInfo)
      .catch(() => undefined)
    api
      .snapshots()
      .then(setSnapshots)
      .catch((e: unknown) => setError(String(e)))
  }, [])

  useEffect(() => {
    // Nothing to load without a snapshot; the archive picker + tree render only when one is selected.
    if (!snapshot) return
    let stale = false
    setError('')
    setLoading(true)
    setTree(null)
    setArchives([])
    setArchive('')
    setArchivesOf('')

    api
      .files(snapshot)
      .then((files) => {
        if (stale) return
        const restorable = files.filter((f) => isRestorableArchive(f.filename))
        const first = restorable[0]
        setArchives(restorable)
        setArchive(first ? restoreArchiveName(first.filename) : '')
        setArchivesOf(snapshot)
        // With an archive selected, the catalog effect below takes over the loading state.
        if (!first) setLoading(false)
      })
      .catch((e: unknown) => {
        if (stale) return
        setError(String(e))
        setLoading(false)
      })
    return () => {
      stale = true
    }
  }, [snapshot])

  useEffect(() => {
    // Each archive has its own tree. Wait until the archive list is this snapshot's, so a switch of
    // snapshot never asks for the previous snapshot's archive.
    if (!archive || archivesOf !== snapshot) return
    let stale = false
    setError('')
    setLoading(true)
    setTree(null)

    api
      .catalog(snapshot, archive)
      .then((catalog) => {
        if (!stale) setTree(catalog)
      })
      .catch((e: unknown) => {
        if (!stale) setError(String(e))
      })
      .finally(() => {
        if (!stale) setLoading(false)
      })
    return () => {
      stale = true
    }
  }, [snapshot, archive, archivesOf])

  return (
    <div className="app">
      <header>
        <h1>PBS Browser</h1>
        <span className="user">{identity?.user ? `Signed in as ${identity.user}` : ''}</span>
      </header>

      <section className="controls">
        <label>
          Snapshot
          <select value={snapshot} onChange={(e) => setSnapshot(e.target.value)}>
            <option value="">— select a snapshot —</option>
            {groupSnapshotsByHost(snapshots).map(([host, snaps]) => (
              <optgroup key={host} label={host}>
                {snaps.map((s) => (
                  <option key={s.id} value={s.id}>
                    {new Date(s.time).toLocaleString()}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </label>

        {snapshot && (
          <label>
            Archive
            <select value={archive} onChange={(e) => setArchive(e.target.value)} disabled={archives.length === 0}>
              {archives.map((a) => {
                const name = restoreArchiveName(a.filename)
                return (
                  <option key={name} value={name}>
                    {name}
                  </option>
                )
              })}
            </select>
          </label>
        )}
      </section>

      {error && <p className="error">{error}</p>}
      {loading && <p className="muted">Loading catalog…</p>}
      {snapshot && !loading && archivesOf === snapshot && archives.length === 0 && (
        <p className="muted">No browsable archives in this snapshot.</p>
      )}
      {snapshot && tree && !loading && <Tree root={tree} snapshot={snapshot} archive={archive} />}

      {buildInfo && (
        <footer className="version muted" title={buildInfo.commit ?? undefined}>
          pbs-browser {formatVersion(buildInfo)}
        </footer>
      )}
    </div>
  )
}
