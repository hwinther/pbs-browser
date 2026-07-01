export interface SnapshotInfo {
  id: string;
  backupType: string;
  backupId: string;
  time: string;
  size: number | null;
  comment: string | null;
}

export interface ArchiveInfo {
  filename: string;
  size: number | null;
  cryptMode: string | null;
}

export interface CatalogNode {
  name: string;
  path: string;
  isDir: boolean;
  size: number | null;
  children: CatalogNode[];
}

export interface Identity {
  user: string;
  email: string;
}

async function getJson<T>(url: string): Promise<T> {
  const resp = await fetch(url);
  if (!resp.ok) {
    const text = await resp.text().catch(() => '');
    throw new Error(`${resp.status} ${resp.statusText}${text ? `: ${text}` : ''}`);
  }
  return (await resp.json()) as T;
}

export const api = {
  me: () => getJson<Identity>('/api/me'),
  snapshots: () => getJson<SnapshotInfo[]>('/api/snapshots'),
  files: (snapshot: string) =>
    getJson<ArchiveInfo[]>(`/api/snapshots/files?snapshot=${encodeURIComponent(snapshot)}`),
  catalog: (snapshot: string) =>
    getJson<CatalogNode>(`/api/catalog?snapshot=${encodeURIComponent(snapshot)}`),
};

/** The restore archive name is the pxar/img without the index suffix (root.pxar.didx -> root.pxar). */
export function restoreArchiveName(filename: string): string {
  return filename.replace(/\.(didx|fidx)$/, '');
}

/** Only pxar/img archives can be browsed/restored; blobs and the catalog index cannot. */
export function isRestorableArchive(filename: string): boolean {
  return /\.(pxar|mpxar|ppxar|img)(\.(didx|fidx))?$/.test(filename);
}

export function buildDownloadUrl(snapshot: string, archive: string, path: string): string {
  const query = new URLSearchParams({ snapshot, archive, path });
  return `/api/download?${query.toString()}`;
}
