import { useState } from 'react';
import type { CatalogNode } from './api';
import { buildDownloadUrl } from './api';

function formatSize(size: number | null): string {
  if (size == null) return '';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let n = size;
  let i = 0;
  while (n >= 1024 && i < units.length - 1) {
    n /= 1024;
    i += 1;
  }
  return `${n.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

interface TreeProps {
  root: CatalogNode;
  snapshot: string;
  archive: string;
}

export function Tree({ root, snapshot, archive }: TreeProps) {
  if (!root.children?.length) {
    return <p className="muted">No files in this snapshot.</p>;
  }
  return (
    <ul className="tree">
      {root.children.map((child) => (
        <TreeItem key={child.path} node={child} snapshot={snapshot} archive={archive} />
      ))}
    </ul>
  );
}

function TreeItem({ node, snapshot, archive }: { node: CatalogNode; snapshot: string; archive: string }) {
  const [open, setOpen] = useState(false);

  if (node.isDir) {
    return (
      <li>
        <button type="button" className="row dir" onClick={() => setOpen(!open)} aria-expanded={open}>
          <span className="caret">{open ? '▾' : '▸'}</span>
          <span className="icon" aria-hidden="true">📁</span>
          <span className="name">{node.name}</span>
        </button>
        {open && node.children?.length > 0 && (
          <ul className="tree">
            {node.children.map((child) => (
              <TreeItem key={child.path} node={child} snapshot={snapshot} archive={archive} />
            ))}
          </ul>
        )}
      </li>
    );
  }

  return (
    <li>
      <div className="row file">
        <span className="caret" />
        <span className="icon" aria-hidden="true">📄</span>
        <span className="name">{node.name}</span>
        <span className="size">{formatSize(node.size)}</span>
        {archive ? (
          <a className="download" href={buildDownloadUrl(snapshot, archive, node.path)} download>
            Download
          </a>
        ) : (
          <span className="download disabled" title="Select an archive first">Download</span>
        )}
      </div>
    </li>
  );
}
