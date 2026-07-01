import { describe, expect, test } from 'vitest';
import type { SnapshotInfo } from './api';
import {
  buildDownloadUrl,
  groupSnapshotsByHost,
  isRestorableArchive,
  restoreArchiveName,
} from './api';

describe('archive helpers', () => {
  test('restoreArchiveName strips index suffixes', () => {
    expect(restoreArchiveName('root.pxar.didx')).toBe('root.pxar');
    expect(restoreArchiveName('drive-scsi0.img.fidx')).toBe('drive-scsi0.img');
    expect(restoreArchiveName('root.pxar')).toBe('root.pxar');
  });

  test('isRestorableArchive matches pxar/img, not blobs or the catalog', () => {
    expect(isRestorableArchive('root.pxar.didx')).toBe(true);
    expect(isRestorableArchive('drive-scsi0.img.fidx')).toBe(true);
    expect(isRestorableArchive('index.json.blob')).toBe(false);
    expect(isRestorableArchive('catalog.pcat1.didx')).toBe(false);
  });
});

describe('buildDownloadUrl', () => {
  test('encodes all params', () => {
    const url = buildDownloadUrl('host/demo/2026-06-30T00:00:00Z', 'root.pxar', '/etc/hosts');
    expect(url).toContain('snapshot=host%2Fdemo');
    expect(url).toContain('archive=root.pxar');
    expect(url).toContain('path=%2Fetc%2Fhosts');
  });
});

describe('groupSnapshotsByHost', () => {
  const snap = (backupId: string, time: string): SnapshotInfo => ({
    id: `host/${backupId}/${time}`,
    backupType: 'host',
    backupId,
    time,
    size: null,
    comment: null,
  });

  test('groups by type/id and sorts snapshots newest-first', () => {
    const groups = groupSnapshotsByHost([
      snap('grafana', '2026-06-29T00:00:00Z'),
      snap('node-red', '2026-06-30T00:00:00Z'),
      snap('grafana', '2026-07-01T00:00:00Z'),
    ]);

    // groups sorted by host name
    expect(groups.map(([h]) => h)).toEqual(['host/grafana', 'host/node-red']);
    // grafana's two snapshots, newest first
    expect(groups[0][1].map((s) => s.time)).toEqual([
      '2026-07-01T00:00:00Z',
      '2026-06-29T00:00:00Z',
    ]);
  });
});
