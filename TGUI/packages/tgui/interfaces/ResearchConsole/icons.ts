import { globalStore } from 'common/redux';
import { useCallback, useSyncExternalStore } from 'react';

import { selectBackend, sendAct } from '../../backend';
import type { IconLayer, WireData } from './model';

const MAX_IDS_PER_REQUEST = 100;
const VISIBLE_MARGIN = '200px';

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
    const ids = pending.splice(0, MAX_IDS_PER_REQUEST);
    sendAct('icons', { ids: ids.join(',') });
  }
};

const requestIcon = (id: string) => {
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

let observer: IntersectionObserver | undefined;
const idByElement = new WeakMap<Element, string>();

const getObserver = () => {
  observer ??= new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        const id = entry.isIntersecting && idByElement.get(entry.target);
        if (id) {
          observer?.unobserve(entry.target);
          requestIcon(id);
        }
      }
    },
    { rootMargin: VISIBLE_MARGIN },
  );
  return observer;
};

/** Ref callback: asks the host for a technology's icon once its element is near the viewport. */
export const useIconRequest = (id: string) =>
  useCallback(
    (element: HTMLElement | null) => {
      if (!element) {
        return;
      }
      attach();
      if (layersById.has(id)) {
        return;
      }
      idByElement.set(element, id);
      getObserver().observe(element);
      return () => getObserver().unobserve(element);
    },
    [id],
  );

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
  return useSyncExternalStore(
    subscribe,
    () => layersById.get(id) ?? NO_LAYERS,
  );
};
