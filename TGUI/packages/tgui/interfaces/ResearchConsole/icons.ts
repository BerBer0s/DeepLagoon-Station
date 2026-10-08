import { globalStore } from 'common/redux';
import { useCallback, useEffect, useSyncExternalStore } from 'react';

import { selectBackend, sendAct } from '../../backend';
import type { IconLayer, WireData } from './model';

const MAX_IDS_PER_REQUEST = 100;

// common/redux is untyped JavaScript; this is the part of the store used here.
type Store = {
  getState: () => unknown;
  subscribe: (listener: () => void) => () => void;
};
const getStore = () => globalStore as unknown as Store;

const NO_LAYERS: IconLayer[] = [];
const layersById = new Map<string, IconLayer[]>();
const listenersById = new Map<string, Set<() => void>>();
const requested = new Set<string>();

let pending: string[] = [];
let flushScheduled = false;
let attached = false;
let lastBatch: WireData['iconBatch'];

// The host replaces `iconBatch` on every delivery, so batches are merged here,
// synchronously on each store change, before React can coalesce them.
const onStoreChange = () => {
  const { data } = selectBackend<WireData>(getStore().getState());
  if (data.iconBatch === lastBatch) {
    return;
  }
  lastBatch = data.iconBatch;
  for (const icon of data.iconBatch?.icons ?? []) {
    layersById.set(icon.id, icon.layers);
    listenersById.get(icon.id)?.forEach((listener) => listener());
  }
};

const attach = () => {
  if (!attached) {
    attached = true;
    getStore().subscribe(onStoreChange);
  }
};

const flush = () => {
  flushScheduled = false;
  while (pending.length > 0) {
    const ids = pending.slice(0, MAX_IDS_PER_REQUEST);
    pending = pending.slice(MAX_IDS_PER_REQUEST);
    sendAct('icons', { ids: ids.join(',') });
  }
};

const requestIcon = (id: string) => {
  attach();
  if (requested.has(id)) {
    return;
  }
  requested.add(id);
  pending.push(id);
  if (!flushScheduled) {
    flushScheduled = true;
    setTimeout(flush, 0);
  }
};

/** Returns a technology's icon layers and asks the host for them the first time it is shown. */
export const useTechIcon = (id: string): IconLayer[] => {
  const subscribe = useCallback(
    (listener: () => void) => {
      attach();
      let listeners = listenersById.get(id);
      if (!listeners) {
        listeners = new Set();
        listenersById.set(id, listeners);
      }
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    [id],
  );
  useEffect(() => requestIcon(id), [id]);
  return useSyncExternalStore(
    subscribe,
    () => layersById.get(id) ?? NO_LAYERS,
  );
};
