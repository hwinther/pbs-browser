import { useEffect, useState } from 'react';
import { api, groupSnapshotsByHost, isRestorableArchive, restoreArchiveName } from './api';
import type { ArchiveInfo, CatalogNode, Identity, SnapshotInfo } from './api';
import { Tree } from './Tree';

export default function App() {
  const [identity, setIdentity] = useState<Identity | null>(null);
  const [snapshots, setSnapshots] = useState<SnapshotInfo[]>([]);
  const [snapshot, setSnapshot] = useState('');
  const [archives, setArchives] = useState<ArchiveInfo[]>([]);
  const [archive, setArchive] = useState('');
  const [tree, setTree] = useState<CatalogNode | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    api.me().then(setIdentity).catch(() => undefined);
    api.snapshots().then(setSnapshots).catch((e: unknown) => setError(String(e)));
  }, []);

  useEffect(() => {
    // Nothing to load without a snapshot; the archive picker + tree render only when one is selected.
    if (!snapshot) return;
    setError('');
    setLoading(true);
    setTree(null);
    setArchives([]);
    setArchive('');

    Promise.all([api.files(snapshot), api.catalog(snapshot)])
      .then(([files, catalog]) => {
        const restorable = files.filter((f) => isRestorableArchive(f.filename));
        setArchives(restorable);
        setArchive(restorable.length ? restoreArchiveName(restorable[0].filename) : '');
        setTree(catalog);
      })
      .catch((e: unknown) => setError(String(e)))
      .finally(() => setLoading(false));
  }, [snapshot]);

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
            <select
              value={archive}
              onChange={(e) => setArchive(e.target.value)}
              disabled={archives.length === 0}
            >
              {archives.map((a) => {
                const name = restoreArchiveName(a.filename);
                return (
                  <option key={name} value={name}>
                    {name}
                  </option>
                );
              })}
            </select>
          </label>
        )}
      </section>

      {error && <p className="error">{error}</p>}
      {loading && <p className="muted">Loading catalog…</p>}
      {snapshot && tree && !loading && <Tree root={tree} snapshot={snapshot} archive={archive} />}
    </div>
  );
}
