import { afterEach, describe, expect, test, vi } from 'vitest'
import type { SnapshotInfo } from './api'
import {
  api,
  buildDownloadUrl,
  formatVersion,
  groupSnapshotsByHost,
  isRestorableArchive,
  restoreArchiveName,
} from './api'

describe('formatVersion', () => {
  test('adds the short commit when there is one', () => {
    expect(formatVersion({ version: '0.5.2', commit: '896ef5455b6996ac671092d79d066decf264c110' })).toBe(
      '0.5.2 (896ef54)',
    )
  })

  test('is just the version without a commit', () => {
    expect(formatVersion({ version: '0.0.0-local', commit: null })).toBe('0.0.0-local')
  })
})

describe('archive helpers', () => {
  test('restoreArchiveName strips index suffixes', () => {
    expect(restoreArchiveName('root.pxar.didx')).toBe('root.pxar')
    expect(restoreArchiveName('drive-scsi0.img.fidx')).toBe('drive-scsi0.img')
    expect(restoreArchiveName('root.pxar')).toBe('root.pxar')
  })

  test('isRestorableArchive matches pxar/img, not blobs or the catalog', () => {
    expect(isRestorableArchive('root.pxar.didx')).toBe(true)
    expect(isRestorableArchive('drive-scsi0.img.fidx')).toBe(true)
    expect(isRestorableArchive('index.json.blob')).toBe(false)
    expect(isRestorableArchive('catalog.pcat1.didx')).toBe(false)
  })
})

describe('buildDownloadUrl', () => {
  test('encodes all params', () => {
    const url = buildDownloadUrl('host/demo/2026-06-30T00:00:00Z', 'root.pxar', '/etc/hosts')
    expect(url).toContain('snapshot=host%2Fdemo')
    expect(url).toContain('archive=root.pxar')
    expect(url).toContain('path=%2Fetc%2Fhosts')
  })
})

describe('groupSnapshotsByHost', () => {
  const snap = (backupId: string, time: string): SnapshotInfo => ({
    id: `host/${backupId}/${time}`,
    backupType: 'host',
    backupId,
    time,
    size: null,
    comment: null,
  })

  test('groups by type/id and sorts snapshots newest-first', () => {
    const groups = groupSnapshotsByHost([
      snap('grafana', '2026-06-29T00:00:00Z'),
      snap('node-red', '2026-06-30T00:00:00Z'),
      snap('grafana', '2026-07-01T00:00:00Z'),
    ])

    // groups sorted by host name
    expect(groups.map(([h]) => h)).toEqual(['host/grafana', 'host/node-red'])
    // grafana's two snapshots, newest first
    expect(groups[0]?.[1].map((s) => s.time)).toEqual(['2026-07-01T00:00:00Z', '2026-06-29T00:00:00Z'])
  })
})

describe('api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  test('asks for the catalog with both parameters encoded', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(Response.json({ name: '/' })))
    vi.stubGlobal('fetch', fetchMock)

    await expect(api.catalog('host/a b/2026', 'root.pxar')).resolves.toEqual({ name: '/' })
    expect(fetchMock).toHaveBeenCalledWith('/api/catalog?snapshot=host%2Fa+b%2F2026&archive=root.pxar')
  })

  test('an error without a readable body is just the status', async () => {
    const body = {
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
      text: () => Promise.reject(new Error('x')),
    }
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(body)),
    )

    await expect(api.snapshots()).rejects.toThrow(/^503 Service Unavailable$/)
  })
})
