import { fireEvent, render, screen } from '@testing-library/react';
import { expect, test } from 'vitest';
import { Tree } from './Tree';
import type { CatalogNode } from './api';

const root: CatalogNode = {
  name: '/',
  path: '/',
  isDir: true,
  size: null,
  children: [
    {
      name: 'etc',
      path: '/etc',
      isDir: true,
      size: null,
      children: [{ name: 'hosts', path: '/etc/hosts', isDir: false, size: 158, children: [] }],
    },
  ],
};

test('expands a directory and exposes a download link for the file', () => {
  render(<Tree root={root} snapshot="host/demo/2026-06-30T00:00:00Z" archive="root.pxar" />);

  // directory is shown; its child file is hidden until expanded
  expect(screen.getByText('etc')).toBeInTheDocument();
  expect(screen.queryByText('hosts')).not.toBeInTheDocument();

  fireEvent.click(screen.getByText('etc'));

  expect(screen.getByText('hosts')).toBeInTheDocument();
  const link = screen.getByRole('link', { name: 'Download' });
  expect(link.getAttribute('href')).toContain('path=%2Fetc%2Fhosts');
  expect(link.getAttribute('href')).toContain('archive=root.pxar');
});

test('renders a disabled download when no archive is selected', () => {
  render(<Tree root={root} snapshot="host/demo/2026-06-30T00:00:00Z" archive="" />);
  fireEvent.click(screen.getByText('etc'));
  expect(screen.queryByRole('link', { name: 'Download' })).not.toBeInTheDocument();
});
