import { describe, expect, test } from 'vitest';
import { buildDownloadUrl, isRestorableArchive, restoreArchiveName } from './api';

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
