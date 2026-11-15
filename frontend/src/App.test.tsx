import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest'
import App from './App'
import type { ArchiveInfo, CatalogNode, SnapshotInfo } from './api'

const GRAFANA = 'host/grafana/2026-07-01T04:30:00Z'
const NODE_RED = 'host/node-red/2026-06-30T04:00:00Z'

const snapshots: SnapshotInfo[] = [
  { id: GRAFANA, backupType: 'host', backupId: 'grafana', time: '2026-07-01T04:30:00Z', size: 1, comment: null },
  { id: NODE_RED, backupType: 'host', backupId: 'node-red', time: '2026-06-30T04:00:00Z', size: 1, comment: null },
]

const archives: ArchiveInfo[] = [
  { filename: 'catalog.pcat1.didx', size: 1, cryptMode: 'encrypt' },
  { filename: 'config.pxar.didx', size: 1, cryptMode: 'encrypt' },
  { filename: 'data.pxar.didx', size: 1, cryptMode: 'encrypt' },
  { filename: 'index.json.blob', size: 1, cryptMode: 'sign-only' },
]

const tree = (file: string): CatalogNode => ({
  name: '/',
  path: '/',
  isDir: true,
  size: null,
  children: [{ name: file, path: `/${file}`, isDir: false, size: 10, children: [] }],
})

type Route = (url: URL) => Response | Promise<Response>

const json = (body: unknown) => Response.json(body)

/** Answers fetch from `routes` by path; anything unrouted is a 404. */
function stubApi(routes: Record<string, Route>) {
  const fetchMock = vi.fn((input: RequestInfo | URL) => {
    const url = new URL(String(input), 'http://pbs.test')
    const route = routes[url.pathname]
    return Promise.resolve(route ? route(url) : new Response('', { status: 404, statusText: 'Not Found' }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const defaultRoutes: Record<string, Route> = {
  '/api/me': () => json({ user: 'hcw', email: 'hcw@wsh.no' }),
  '/api/version': () => json({ version: '0.5.2', commit: '896ef5455b6996ac671092d79d066decf264c110' }),
  '/api/snapshots': () => json(snapshots),
  '/api/snapshots/files': () => json(archives),
  '/api/catalog': (url) => json(tree(`${url.searchParams.get('archive')}.txt`)),
}

const snapshotSelect = () => screen.getByRole('combobox', { name: 'Snapshot' })
const archiveSelect = () => screen.getByRole('combobox', { name: 'Archive' })

describe('App', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  test('shows who is signed in, the version and the snapshots grouped by host', async () => {
    stubApi(defaultRoutes)
    render(<App />)

    expect(await screen.findByText('Signed in as hcw')).toBeInTheDocument()
    expect(await screen.findByText('pbs-browser 0.5.2 (896ef54)')).toHaveAttribute(
      'title',
      '896ef5455b6996ac671092d79d066decf264c110',
    )
    expect(await screen.findByRole('group', { name: 'host/grafana' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'host/node-red' })).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Archive' })).not.toBeInTheDocument()
  })

  test('selecting a snapshot loads its first browsable archive, and the archive picker switches trees', async () => {
    const fetchMock = stubApi(defaultRoutes)
    render(<App />)
    await screen.findByRole('group', { name: 'host/grafana' })

    fireEvent.change(snapshotSelect(), { target: { value: GRAFANA } })

    expect(await screen.findByText('config.pxar.txt')).toBeInTheDocument()
    expect(
      within(archiveSelect())
        .getAllByRole('option')
        .map((o) => o.textContent),
    ).toEqual(['config.pxar', 'data.pxar'])

    fireEvent.change(archiveSelect(), { target: { value: 'data.pxar' } })

    expect(await screen.findByText('data.pxar.txt')).toBeInTheDocument()
    expect(screen.queryByText('config.pxar.txt')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Download' })).toHaveAttribute(
      'href',
      expect.stringContaining('archive=data.pxar'),
    )
    const catalogCalls = fetchMock.mock.calls.map(([u]) => String(u)).filter((u) => u.startsWith('/api/catalog'))
    expect(catalogCalls).toHaveLength(2)
  })

  test('says so when a snapshot has no browsable archives', async () => {
    stubApi({ ...defaultRoutes, '/api/snapshots/files': () => json([archives[3]]) })
    render(<App />)
    await screen.findByRole('group', { name: 'host/grafana' })

    fireEvent.change(snapshotSelect(), { target: { value: NODE_RED } })

    expect(await screen.findByText('No browsable archives in this snapshot.')).toBeInTheDocument()
    expect(archiveSelect()).toBeDisabled()
  })

  test('shows the error when the snapshots cannot be listed, and no identity or version', async () => {
    stubApi({
      '/api/snapshots': () => new Response('pbs down', { status: 502, statusText: 'Bad Gateway' }),
    })
    render(<App />)

    expect(await screen.findByText('Error: 502 Bad Gateway: pbs down')).toBeInTheDocument()
    expect(screen.queryByText(/Signed in as/)).not.toBeInTheDocument()
    expect(screen.queryByText(/pbs-browser \d/)).not.toBeInTheDocument()
  })

  test('shows the error when the archives cannot be listed', async () => {
    stubApi({ ...defaultRoutes, '/api/snapshots/files': () => new Response('', { status: 500, statusText: 'Oops' }) })
    render(<App />)
    await screen.findByRole('group', { name: 'host/grafana' })

    fireEvent.change(snapshotSelect(), { target: { value: GRAFANA } })

    expect(await screen.findByText('Error: 500 Oops')).toBeInTheDocument()
    expect(screen.queryByText('Loading catalog…')).not.toBeInTheDocument()
  })

  test('shows the error when the catalog cannot be read', async () => {
    stubApi({
      ...defaultRoutes,
      '/api/catalog': () => new Response('bad key', { status: 500, statusText: 'Internal Server Error' }),
    })
    render(<App />)
    await screen.findByRole('group', { name: 'host/grafana' })

    fireEvent.change(snapshotSelect(), { target: { value: GRAFANA } })

    expect(await screen.findByText('Error: 500 Internal Server Error: bad key')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Download' })).not.toBeInTheDocument()
  })

  test('a slow answer for the previous snapshot never replaces the current one', async () => {
    const pending: { release?: (r: Response) => void } = {}
    stubApi({
      ...defaultRoutes,
      '/api/snapshots/files': (url) =>
        url.searchParams.get('snapshot') === GRAFANA
          ? new Promise<Response>((resolve) => {
              pending.release = resolve
            })
          : json([archives[2]]),
    })
    render(<App />)
    await screen.findByRole('group', { name: 'host/grafana' })

    fireEvent.change(snapshotSelect(), { target: { value: GRAFANA } })
    fireEvent.change(snapshotSelect(), { target: { value: NODE_RED } })
    expect(await screen.findByText('data.pxar.txt')).toBeInTheDocument()

    pending.release?.(json([archives[1]]))
    await new Promise((r) => setTimeout(r, 0))

    expect(
      within(archiveSelect())
        .getAllByRole('option')
        .map((o) => o.textContent),
    ).toEqual(['data.pxar'])
    expect(screen.queryByText('config.pxar.txt')).not.toBeInTheDocument()
  })
})
