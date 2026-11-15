import { fireEvent, render, screen } from '@testing-library/react'
import { expect, test } from 'vitest'
import { Tree } from './Tree'
import type { CatalogNode } from './api'

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
}

test('expands a directory and exposes a download link for the file', () => {
  render(<Tree root={root} snapshot="host/demo/2026-06-30T00:00:00Z" archive="root.pxar" />)

  // directory is shown; its child file is hidden until expanded
  expect(screen.getByText('etc')).toBeInTheDocument()
  expect(screen.queryByText('hosts')).not.toBeInTheDocument()

  fireEvent.click(screen.getByText('etc'))

  expect(screen.getByText('hosts')).toBeInTheDocument()
  const link = screen.getByRole('link', { name: 'Download' })
  expect(link).toHaveAttribute('href', expect.stringContaining('path=%2Fetc%2Fhosts'))
  expect(link).toHaveAttribute('href', expect.stringContaining('archive=root.pxar'))
})

test('renders a disabled download when no archive is selected', () => {
  render(<Tree root={root} snapshot="host/demo/2026-06-30T00:00:00Z" archive="" />)
  fireEvent.click(screen.getByText('etc'))
  expect(screen.queryByRole('link', { name: 'Download' })).not.toBeInTheDocument()
})

test('says so when the archive has no files', () => {
  render(<Tree root={{ ...root, children: [] }} snapshot="s" archive="root.pxar" />)
  expect(screen.getByText('No files in this archive.')).toBeInTheDocument()
})

test('sizes files in binary units and collapses a directory again', () => {
  const file = (name: string, size: number | null): CatalogNode => ({
    name,
    path: `/${name}`,
    isDir: false,
    size,
    children: [],
  })
  const big: CatalogNode = {
    ...root,
    children: [
      file('tiny', 512),
      file('kilo', 1536),
      file('mega', 5 * 1024 ** 2),
      file('peta', 3 * 1024 ** 5),
      file('unknown', null),
      { name: 'empty', path: '/empty', isDir: true, size: null, children: [] },
    ],
  }
  render(<Tree root={big} snapshot="s" archive="root.pxar" />)

  expect(screen.getByText('512 B')).toBeInTheDocument()
  expect(screen.getByText('1.5 KB')).toBeInTheDocument()
  expect(screen.getByText('5.0 MB')).toBeInTheDocument()
  expect(screen.getByText('3072.0 TB')).toBeInTheDocument()

  const dir = screen.getByRole('button', { name: /empty/ })
  fireEvent.click(dir)
  expect(dir).toHaveAttribute('aria-expanded', 'true')
  fireEvent.click(dir)
  expect(dir).toHaveAttribute('aria-expanded', 'false')
})
