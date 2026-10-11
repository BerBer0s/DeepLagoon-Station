import { useEffect, useRef, useState } from 'react';
import { useBackend } from '../backend';
import { cellsBetween, changeCount, copyItems, inside, stamp, validateDraft, makeTemplate, projectTemplate, insertTemplate, makePatch, itemsAtPoint, drawOrder, editorKey } from './gridEditorModel.mjs';
import './GridEditor.scss';

const categories = [['tile', 'Полы'], ['wall', 'Стены'], ['entity', 'Объекты'], ['decal', 'Декали'], ['all', 'Все'], ['favorites', '★ Избранное'], ['templates', 'Шаблоны']];
const tools = [['pan', 'Перемещение · H'], ['select', 'Выбор · V'], ['brush', 'Кисть · B'], ['line', 'Линия · L'], ['rect', 'Прямоугольник · U'], ['erase', 'Ластик · E'], ['pick', 'Пипетка · I']];
const Sprite = ({ images = [], color }) => <>{images.map((image, index) => color ?
  <i key={index} className="GridEditor__tinted" style={{ backgroundColor: color, maskImage: `url("${image.url}")` }} /> :
  <img key={index} src={image.url} alt="" draggable={false} />)}</>;

export const GridEditor = () => {
  const { data, act } = useBackend();
  const [draft, setDraft] = useState(data.items || []);
  const [undo, setUndo] = useState([]), [redo, setRedo] = useState([]);
  const [selected, setSelected] = useState([]);
  const [brush, setBrush] = useState(null), [tool, setTool] = useState('select');
  const [category, setCategory] = useState('tile'), [query, setQuery] = useState('');
  const [zoom, setZoom] = useState(1), [gridLines, setGridLines] = useState(true);
  const [layers, setLayers] = useState({ tile: true, entity: true, decal: true });
  const [rotation, setRotation] = useState(0), [color, setColor] = useState('#ffffff');
  const [z, setZ] = useState(0), [cleanable, setCleanable] = useState(false), [replace, setReplace] = useState(true);
  const [hover, setHover] = useState(null), [previewCells, setPreviewCells] = useState([]);
  const [before, setBefore] = useState(false), [pending, setPending] = useState(false), [confirmClose, setConfirmClose] = useState(false);
  const [clipboard, setClipboard] = useState([]);
  const [templateBrush, setTemplateBrush] = useState(null), [templateName, setTemplateName] = useState('');
  const viewport = useRef(null), canvas = useRef(null), gesture = useRef(null), current = useRef(draft);
  const counter = useRef(0), visualCache = useRef(new Map());
  const upload = useRef(null);
  const space = useRef(false);
  const shiftHeld = useRef(false), alphaMasks = useRef(new Map()), [pointer, setPointer] = useState(null);
  const [shift, setShift] = useState(false), [panning, setPanning] = useState(false);
  const [catalogRows, setCatalogRows] = useState([]), [catalogScroll, setCatalogScroll] = useState(0);
  const catalogView = useRef(null), requestedPages = useRef(new Set()), catalogRequest = useRef(null);
  const catalogStart = Math.max(0, Math.floor(catalogScroll / 64) - 2);
  const catalogEnd = Math.min(data.catalogCount || 0, catalogStart + 20);
  const area = { x: data.x || 0, y: data.y || 0, width: data.width || 16, height: data.height || 16 };
  const cellSize = 32 * zoom;
  const changes = changeCount(data.items || [], draft);
  const errors = validateDraft(draft, area) || (changes > 2048 ? 'Примените не более 2048 изменений за один раз' : '');
  const locked = before || pending || data.downloading;
  const displayed = before ? data.items || [] : draft;
  for (const entry of [...(data.visuals || []), ...(data.libraryVisuals || []), ...(data.catalog || [])])
    visualCache.current.set(`${entry.kind}:${entry.id}`, { ...visualCache.current.get(`${entry.kind}:${entry.id}`), ...entry });
  const visual = (item) => visualCache.current.get(`${item.kind}:${item.prototype}`);
  useEffect(() => {
    for (const entry of visualCache.current.values()) for (const { url } of entry.images || []) {
      if (!url || alphaMasks.current.has(url)) continue;
      alphaMasks.current.set(url, null);
      const image = new Image();
      image.onload = () => {
        try { const buffer = document.createElement('canvas'); buffer.width = image.width; buffer.height = image.height;
          const context = buffer.getContext('2d'); context.drawImage(image, 0, 0);
          alphaMasks.current.set(url, { width: image.width, height: image.height, bytes: context.getImageData(0, 0, image.width, image.height).data });
        } catch { /* Missing masks fall back to the visible sprite bounds. */ }
      };
      image.src = url;
    }
  }, [data.visuals, data.catalog, data.libraryVisuals]);
  const opaque = (images, u, v) => !images.length || images.some(({ url }) => {
    const mask = alphaMasks.current.get(url); if (!mask) return true;
    const scale = Math.max(mask.width, mask.height), x = (u - .5) * scale + mask.width / 2, y = (v - .5) * scale + mask.height / 2;
    return x >= 0 && y >= 0 && x < mask.width && y < mask.height && mask.bytes[(Math.floor(y) * mask.width + Math.floor(x)) * 4 + 3] > 16;
  });
  const pointAt = event => { const rect = canvas.current.getBoundingClientRect(); return { x: area.x + (event.clientX - rect.left) / cellSize, y: area.y + area.height - (event.clientY - rect.top) / cellSize }; };
  const underPointer = point => itemsAtPoint(current.current, point, layers, visual, opaque);
  const nextId = () => `new_${Date.now()}_${counter.current++}`;
  const update = (next) => { current.current = next; setDraft(next); };
  const remember = (previous = current.current) => { setUndo((history) => [...history.slice(-49), copyItems(previous)]); setRedo([]); };
  const change = (next) => { if (JSON.stringify(next) === JSON.stringify(current.current)) return; remember(); update(next); };
  const search = (page = 0) => act('search', { category, query, page });
  useEffect(() => {
    setUndo([]); setRedo([]); setSelected([]);
    update(copyItems(data.items || [])); setPending(false); setBefore(false);
    gesture.current = null; setPreviewCells([]); upload.current = null;
  }, [data.token]);
  useEffect(() => { setPending(false); }, [data.resultVersion]);
  useEffect(() => {
    const transfer = upload.current;
    if (transfer && data.uploadId === transfer.id && data.uploadNext < transfer.chunks.length)
      act('applyChunk', { id: transfer.id, index: data.uploadNext, items: transfer.chunks[data.uploadNext] });
  }, [data.uploadId, data.uploadNext]);
  const startTransfer = (items, purpose = 'apply', name = '') => {
    const id = `${Date.now()}_${counter.current++}`;
    const chunks = [];
    for (let i = 0; i < items.length; i += 16) chunks.push(items.slice(i, i + 16));
    if (!chunks.length) chunks.push([]);
    upload.current = { id, chunks }; setPending(true);
    act('applyStart', { id, count: items.length, chunks: chunks.length, purpose, name });
  };
  const apply = () => {
    const baseline = new Set((data.items || []).map(i => i.id));
    const normalized = current.current.map(i => i.kind !== 'tile' && !baseline.has(i.id) && !i.id.startsWith('new_') ? { ...i, id: nextId() } : i);
    update(normalized);
    startTransfer(makePatch(data.items || [], normalized));
  };
  useEffect(() => {
    requestedPages.current.clear(); catalogRequest.current = 0; setCatalogRows([]); setCatalogScroll(0);
    if (catalogView.current) catalogView.current.scrollTop = 0;
    const timer = setTimeout(() => { requestedPages.current.add(0); search(); }, 180);
    return () => clearTimeout(timer);
  }, [category, query, JSON.stringify(data.favorites)]);
  useEffect(() => {
    if (data.catalogCategory !== category || data.catalogQuery !== query) return;
    if (catalogRequest.current === data.catalogPage) catalogRequest.current = null;
    setCatalogRows(previous => { const next = [...previous]; (data.catalog || []).forEach((entry, index) => { next[(data.catalogPage || 0) * 48 + index] = entry; }); return next; });
  }, [data.catalog, data.catalogPage, data.catalogCategory, data.catalogQuery]);
  useEffect(() => {
    if (catalogRequest.current !== null || category === 'templates' || data.catalogCategory !== category || data.catalogQuery !== query) return;
    for (let page = Math.floor(catalogStart / 48); page <= Math.floor(Math.max(0, catalogEnd - 1) / 48); page++)
      if (!requestedPages.current.has(page)) { requestedPages.current.add(page); catalogRequest.current = page; search(page); break; }
  }, [catalogStart, catalogEnd, category, query, data.catalogCategory, data.catalogQuery, data.catalog]);
  const at = (event) => {
    const rect = canvas.current.getBoundingClientRect();
    return { x: area.x + Math.floor((event.clientX - rect.left) / cellSize),
      y: area.y + area.height - 1 - Math.floor((event.clientY - rect.top) / cellSize) };
  };
  const layerKind = category === 'tile' ? 'tile' : category === 'decal' ? 'decal' : 'entity';
  const topAt = (cell, anyLayer = false) => [...current.current].reverse().sort((a, b) =>
    ({ entity: 3, decal: 2, tile: 1 }[b.kind] - { entity: 3, decal: 2, tile: 1 }[a.kind])).find((item) => layers[item.kind] &&
    (anyLayer || ['all', 'templates', 'favorites'].includes(category) || item.kind === layerKind) && (anyLayer || category !== 'wall' || visual(item)?.category === 'wall') &&
    Math.floor(item.x) === cell.x && Math.floor(item.y) === cell.y);
  const options = { rotation, color, z, cleanable, replace, isWall: (id) => visualCache.current.get(`entity:${id}`)?.category === 'wall' };
  const paint = (cells) => update(stamp(current.current, cells.filter((cell) => inside(cell, area)), brush, options, nextId));
  const erase = (cell) => {
    const item = topAt(cell);
    if (item) update(current.current.filter((i) => i.id !== item.id));
  };
  const down = (event) => {
    if (event.button === 1 || event.button === 2 || event.button === 0 && (space.current || tool === 'pan')) {
      event.preventDefault(); act('focus'); canvas.current.focus(); setPanning(true);
      gesture.current = { pan: true, px: event.clientX, py: event.clientY, left: viewport.current.scrollLeft, top: viewport.current.scrollTop };
      canvas.current.setPointerCapture(event.pointerId); return;
    }
    if (locked || event.button !== 0) return;
    event.preventDefault(); act('focus'); canvas.current.focus();
    const cell = at(event); if (!inside(cell, area)) return;
    canvas.current.setPointerCapture(event.pointerId);
    const heldShift = event.shiftKey || shiftHeld.current;
    const item = underPointer(pointAt(event))[0];
    if (heldShift) {
      if (item) { setSelected(ids => ids.includes(item.id) ? ids.filter(id => id !== item.id) : [...ids, item.id]); setTool('select'); }
      return;
    }
    if (tool === 'pick') {
      if (item) {
        const definition = visual(item);
        setBrush({ ...definition, id: item.prototype, kind: item.kind, name: definition?.name || item.prototype,
          category: definition?.category || item.kind, anchored: item.anchored });
        setRotation(item.rotation); setColor(item.color.slice(0, 7)); setZ(item.z); setCleanable(item.cleanable); setTool('brush');
        setTemplateBrush(null);
      }
      return;
    }
    if (tool === 'select') {
      if (!item) {
        const originalSelected = event.shiftKey ? selected : [];
        setSelected(originalSelected); gesture.current = { selectArea: true, start: cell, originalSelected }; return;
      }
      const ids = event.shiftKey ? selected.includes(item.id) ? selected.filter((id) => id !== item.id) : [...selected, item.id] :
        selected.includes(item.id) ? selected : [item.id];
      setSelected(ids);
      if (item.kind === 'decal') { setColor(item.color.slice(0, 7)); setZ(item.z); setCleanable(item.cleanable); }
      // Tiles are painted rather than dragged; entity/decal moves retain fractional offsets.
      gesture.current = { start: cell, original: copyItems(current.current), ids: ids.filter((id) => current.current.find((i) => i.id === id)?.kind !== 'tile') };
      return;
    }
    if (templateBrush && tool === 'brush') {
      const projected = projectTemplate(templateBrush.items, cell, rotation / 90);
      if (projected.some((item) => !inside(item, area))) return;
      change(insertTemplate(current.current, templateBrush.items, cell, rotation / 90, nextId)); return;
    }
    if (tool !== 'erase' && !brush) return;
    gesture.current = { start: cell, last: cell, original: copyItems(current.current), drawing: true };
    if (tool === 'brush') paint([cell]);
    if (tool === 'erase') erase(cell);
    if (tool === 'line' || tool === 'rect') setPreviewCells([cell]);
  };
  const move = (event) => {
    const active = gesture.current;
    if (active?.pan) {
      viewport.current.scrollLeft = active.left - (event.clientX - active.px);
      viewport.current.scrollTop = active.top - (event.clientY - active.py); return;
    }
    const cell = at(event); setHover(inside(cell, area) ? cell : null); setPointer(pointAt(event));
    if (locked || !active) return;
    const bounded = { x: Math.max(area.x, Math.min(area.x + area.width - 1, cell.x)), y: Math.max(area.y, Math.min(area.y + area.height - 1, cell.y)) };
    if (active.selectArea) {
      const cells = cellsBetween(active.start, bounded, true);
      const keys = new Set(cells.map((cell) => `${cell.x}:${cell.y}`));
      setPreviewCells(cells);
      setSelected([...new Set([...active.originalSelected, ...current.current.filter((item) => layers[item.kind] &&
        (['all', 'templates', 'favorites'].includes(category) || item.kind === layerKind) && (category !== 'wall' || visual(item)?.category === 'wall') &&
        keys.has(`${Math.floor(item.x)}:${Math.floor(item.y)}`)).map((item) => item.id)])]);
    } else if (active.ids) update(active.original.map((item) => active.ids.includes(item.id) ?
      { ...item, x: item.x + bounded.x - active.start.x, y: item.y + bounded.y - active.start.y } : item));
    else if (active.drawing) {
      if (tool === 'brush') paint(cellsBetween(active.last, bounded));
      if (tool === 'erase') cellsBetween(active.last, bounded).forEach(erase);
      if (tool === 'line' || tool === 'rect') setPreviewCells(cellsBetween(active.start, bounded, tool === 'rect'));
      active.last = bounded;
    }
  };
  const up = (event, cancel = false) => {
    const active = gesture.current; if (!active) return;
    if (active.selectArea) {
      if (cancel) setSelected(active.originalSelected); else move(event);
    } else if (!active.pan) {
      if (cancel) update(active.original);
      else {
        move(event);
        if (active.drawing && (tool === 'line' || tool === 'rect')) paint(cellsBetween(active.start, active.last, tool === 'rect'));
        if (JSON.stringify(current.current) !== JSON.stringify(active.original)) remember(active.original);
      }
    }
    gesture.current = null; setPreviewCells([]); setPanning(false);
    if (canvas.current.hasPointerCapture(event.pointerId)) canvas.current.releasePointerCapture(event.pointerId);
  };
  const step = (forward) => {
    const history = forward ? redo : undo; if (!history.length || locked) return;
    const next = copyItems(history[history.length - 1]);
    const previous = copyItems(current.current);
    if (forward) { setUndo((h) => [...h, previous]); setRedo(history.slice(0, -1)); }
    else { setRedo((h) => [...h, previous]); setUndo(history.slice(0, -1)); }
    update(next); setSelected([]);
  };
  const remove = () => { change(current.current.filter((i) => !selected.includes(i.id))); setSelected([]); };
  const rotate = () => {
    if (selected.length && tool === 'select') change(current.current.map((i) => selected.includes(i.id) && i.kind !== 'tile' ? { ...i, rotation: (i.rotation + 90) % 360 } : i));
    else setRotation((r) => (r + 90) % 360);
  };
  const paste = () => {
    if (!clipboard.length) return;
    const origin = { x: Math.min(...clipboard.map((i) => Math.floor(i.x))), y: Math.min(...clipboard.map((i) => Math.floor(i.y))) };
    const target = hover || { x: origin.x + 1, y: origin.y + 1 };
    const added = projectTemplate(clipboard, target);
    if (added.some((i) => !inside(i, area))) return;
    change(insertTemplate(current.current, clipboard, target, 0, nextId)); setSelected([]);
  };
  useEffect(() => {
    const key = (event) => {
      if (event.target?.closest('input:not([type=checkbox]),textarea,select,[contenteditable=true]')) return;
      if (event.code === 'Space') { event.preventDefault(); space.current = true; setPanning(true); return; }
      if (editorKey(event) === 'shift') { shiftHeld.current = true; setShift(true); }
      if (locked || gesture.current) return;
      const key = editorKey(event);
      if (event.ctrlKey && key === 'z') { event.preventDefault(); step(event.shiftKey); }
      else if (event.ctrlKey && key === 'y') { event.preventDefault(); step(true); }
      else if (event.ctrlKey && key === 'c') { event.preventDefault(); setClipboard(copyItems(current.current.filter((i) => selected.includes(i.id)))); }
      else if (event.ctrlKey && key === 'v') { event.preventDefault(); paste(); }
      else if (key === 'delete') { event.preventDefault(); remove(); }
      else if (key === 'r') { event.preventDefault(); rotate(); }
      else if (key === 'escape') { setSelected([]); setTool('select'); setBrush(null); setTemplateBrush(null); }
      else if (!event.ctrlKey && !event.altKey) {
        const shortcuts = { h: 'pan', v: 'select', b: 'brush', l: 'line', u: 'rect', e: 'erase', i: 'pick' };
        if (shortcuts[key]) { event.preventDefault(); setTool(shortcuts[key]); }
      }
    };
    const release = event => { if (event.code === 'Space') { space.current = false; setPanning(false); } if (editorKey(event) === 'shift') { shiftHeld.current = false; setShift(false); } };
    const blur = () => { space.current = false; shiftHeld.current = false; setPanning(false); setShift(false); };
    const nativeKey = event => { const input = { ...event.detail, target: document.activeElement, preventDefault() {} }; if (input.down) key(input); else release(input); };
    document.addEventListener('keydown', key, true); document.addEventListener('keyup', release, true); window.addEventListener('blur', blur); window.addEventListener('grid-editor-key', nativeKey);
    return () => { document.removeEventListener('keydown', key, true); document.removeEventListener('keyup', release, true); window.removeEventListener('blur', blur); window.removeEventListener('grid-editor-key', nativeKey); };
  }, [draft, selected, locked, tool, undo, redo, hover, clipboard]);
  const setScale = (value) => setZoom(Math.max(0.1, Math.min(4, value)));
  const fit = () => setScale(Math.min((viewport.current.clientWidth - 32) / (area.width * 32), (viewport.current.clientHeight - 32) / (area.height * 32)));
  useEffect(() => { const frame = requestAnimationFrame(fit); return () => cancelAnimationFrame(frame); }, [data.grid, data.x, data.y, data.width, data.height]);
  const wheel = (event) => {
    event.preventDefault();
    const rect = canvas.current.getBoundingClientRect();
    const px = event.clientX - rect.left, py = event.clientY - rect.top;
    const next = Math.max(0.1, Math.min(4, zoom * (event.deltaY < 0 ? 1.15 : 1 / 1.15)));
    setZoom(next);
    requestAnimationFrame(() => { viewport.current.scrollLeft += px * (next / zoom - 1); viewport.current.scrollTop += py * (next / zoom - 1); });
  };
  useEffect(() => {
    const view = viewport.current;
    view.addEventListener('wheel', wheel, { passive: false });
    return () => view.removeEventListener('wheel', wheel);
  }, [zoom]);
  const pointedItems = pointer ? underPointer(pointer) : [];
  const hoveredItem = (shift || ['pick', 'select'].includes(tool)) ? pointedItems[0] : null;
  const one = draft.find((i) => selected.includes(i.id));
  const patchSelected = (patch) => change(current.current.map((i) => selected.includes(i.id) && i.kind !== 'tile' ? { ...i, ...patch } : i));
  const favorite = (kind, id) => (data.favorites || []).some((f) => f.kind === kind && f.id === id);
  const templatePreview = templateBrush && hover && tool === 'brush' ? projectTemplate(templateBrush.items, hover, rotation / 90) : [];
  const close = () => changes ? setConfirmClose(true) : act('close');
  const region = (dx, dy) => {
    const target = { x: area.x + dx * Math.max(8, area.width - 8), y: area.y + dy * Math.max(8, area.height - 8) };
    if (changes) setConfirmClose(target); else act('region', target);
  };
  useEffect(() => { if (data.closeRequested) close(); }, [data.closeRequested]);
  return <div className="GridEditor" onPointerDownCapture={() => act('focus')}
    onFocusCapture={e => act('editing', { active: !!e.target.closest('input:not([type=checkbox]),textarea,select,[contenteditable=true]') })}
    onBlurCapture={e => act('editing', { active: !!e.relatedTarget?.closest('input:not([type=checkbox]),textarea,select,[contenteditable=true]') })}>
    <header><strong>Редактор grid <small>#{data.grid} · область {area.x}, {area.y} / {area.width}×{area.height}</small></strong><span>{pending ? 'Применение к grid…' : before ? 'Исходный grid' : `${changes} изменений в черновике`}</span><button aria-label="Закрыть" onClick={close} disabled={pending}>×</button></header>
    <div className="GridEditor__body">
      <aside className="GridEditor__catalog">
        <nav>{categories.map(([id, name]) => <button key={id} className={category === id ? 'active' : ''} onClick={() => setCategory(id)}>{name}</button>)}</nav>
        <input aria-label="Поиск прототипов" placeholder="Название или ID прототипа…" value={query} onFocus={() => act('focus')} onChange={(e) => setQuery(e.target.value)} />
        <div ref={catalogView} className="GridEditor__entries" onScroll={e => setCatalogScroll(e.currentTarget.scrollTop)}>{category === 'templates' ? (data.templates || []).filter((t) => t.name.toLowerCase().includes(query.toLowerCase())).map((entry) =>
          <div key={entry.id} className="GridEditor__entry"><button disabled={locked} className={templateBrush?.id === entry.id ? 'active' : ''}
            onClick={() => { setTemplateBrush(entry); setBrush(null); setTool('brush'); setSelected([]); setRotation(0); }}><span>{entry.name}<small>{entry.items.length} элементов · нажмите место на плане</small></span></button>
            <button aria-label={`Удалить шаблон ${entry.name}`} disabled={locked} onClick={() => setConfirmClose({ deleteTemplate: entry.id, name: entry.name })}>×</button></div>) : <div style={{ height: (data.catalogCount || 0) * 64, position: 'relative' }}>{Array.from({ length: Math.max(0, catalogEnd - catalogStart) }, (_, offset) => { const index = catalogStart + offset; const entry = catalogRows[index]; return entry ? <div style={{ position: 'absolute', top: index * 64, height: 64, width: '100%' }} key={`${entry.kind}:${entry.id}`} className="GridEditor__entry"><button disabled={locked} title={entry.id}
          className={brush?.id === entry.id && brush?.kind === entry.kind ? 'active' : ''} onClick={() => { setBrush(entry); setTemplateBrush(null); setTool('brush'); setSelected([]); }}>
          <span className="GridEditor__sprite"><Sprite images={entry.images} /></span><span>{entry.name}<small>{entry.id}</small></span></button>
          <button aria-label={`${favorite(entry.kind, entry.id) ? 'Убрать из' : 'Добавить в'} избранное ${entry.name}`} disabled={locked}
            onClick={() => act('favorite', { kind: entry.kind, id: entry.id })}>{favorite(entry.kind, entry.id) ? '★' : '☆'}</button></div> : <div key={index} style={{ position: 'absolute', top: index * 64 }}>Загрузка…</div>; })}</div>}</div>
        <div className="GridEditor__pages">{data.catalogCount || 0} элементов · прокрутите каталог</div>
      </aside>
      <main>
        <div className="GridEditor__toolbar">{tools.map(([id, name]) => <button key={id} disabled={locked || !!templateBrush && ['line', 'rect'].includes(id)} className={tool === id ? 'active' : ''} onClick={() => setTool(id)}>{name}</button>)}</div>
        <div className="GridEditor__toolbar"><button disabled={!undo.length || locked} onClick={() => step(false)}>↶</button><button disabled={!redo.length || locked} onClick={() => step(true)}>↷</button>
          <button disabled={pending} className={before ? 'active' : ''} onClick={() => setBefore(!before)}>До / После</button>
          <button onClick={() => setScale(zoom / 1.25)}>−</button><button onClick={() => setScale(1)}>{Math.round(zoom * 100)}%</button><button onClick={() => setScale(zoom * 1.25)}>+</button><button onClick={fit}>Вписать</button>
          <label><input type="checkbox" checked={gridLines} onChange={(e) => setGridLines(e.target.checked)} />Сетка</label>
        </div>
        <div ref={viewport} className="GridEditor__viewport" onContextMenu={(e) => e.preventDefault()}>
          <div ref={canvas} className={`GridEditor__canvas ${gridLines ? 'grid' : ''} ${panning ? 'panning' : ''} tool-${tool}`} tabIndex={0} aria-label="План grid"
            style={{ width: area.width * cellSize, height: area.height * cellSize, '--cell': `${cellSize}px` }}
            onPointerDown={down} onPointerMove={move} onPointerUp={(e) => up(e)} onPointerCancel={(e) => up(e, true)} onPointerLeave={() => { if (!gesture.current) setHover(null); }}>
            {['tile', 'decal', 'entity'].flatMap((kind) => layers[kind] ? displayed.filter((item) => item.kind === kind).map((item) => {
              const px = item.x - (kind === 'entity' ? .5 : 0), py = item.y - (kind === 'entity' ? .5 : 0);
              return <div key={item.id} className={`GridEditor__item ${kind} ${selected.includes(item.id) ? 'selected' : ''} ${hoveredItem?.id === item.id ? 'hovered' : ''}`} title={`${item.prototype} (${item.x}, ${item.y})`}
                style={{ left: (px - area.x) * cellSize, top: (area.y + area.height - 1 - py) * cellSize,
                  width: cellSize, height: cellSize, zIndex: drawOrder(item, visual(item)) }}>
                <span style={{ transform: `rotate(${-item.rotation}deg)`, opacity: kind === 'decal' ? 0.85 : 1 }}><Sprite images={visual(item)?.images} color={kind === 'decal' ? item.color : undefined} /></span>
              </div>;
            }) : [])}
            {(previewCells.length ? previewCells : hover ? [hover] : []).map((cell) => <div key={`${cell.x}:${cell.y}`} className={`GridEditor__cursor ${tool === 'erase' ? 'erase' : ''}`}
              style={{ left: (cell.x - area.x) * cellSize, top: (area.y + area.height - 1 - cell.y) * cellSize, width: cellSize, height: cellSize }}>
              {brush && ['brush', 'line', 'rect'].includes(tool) && <span style={{ transform: `rotate(${-rotation}deg)` }}><Sprite images={brush.images} color={brush.kind === 'decal' ? color : undefined} /></span>}</div>)}
            {templatePreview.map((item, index) => <div key={`template_${index}`} className="GridEditor__cursor"
              style={{ left: (item.x - (item.kind === 'entity' ? .5 : 0) - area.x) * cellSize,
                top: (area.y + area.height - 1 - item.y + (item.kind === 'entity' ? .5 : 0)) * cellSize, width: cellSize, height: cellSize }}>
              <span style={{ transform: `rotate(${-item.rotation}deg)` }}><Sprite images={visual(item)?.images} color={item.kind === 'decal' ? item.color : undefined} /></span></div>)}
          </div>
        </div>
        <div className="GridEditor__status">{hover ? `X ${hover.x} · Y ${hover.y}` : 'Координаты grid'} · Space + ЛКМ / СКМ: перемещение · Shift + ЛКМ: выбор · Колесо: масштаб</div>
      </main>
      <aside className="GridEditor__properties">
        {!!pointedItems.length && <><strong>Под курсором</strong>{pointedItems.slice(0, 8).map(item => <button key={item.id} disabled={locked} onPointerDown={e => e.preventDefault()} onClick={() => {
          if (tool === 'pick') { const definition = visual(item); setBrush({ ...definition, id: item.prototype, kind: item.kind, anchored: item.anchored }); setRotation(item.rotation); setColor(item.color.slice(0, 7)); setZ(item.z); setCleanable(item.cleanable); setTemplateBrush(null); setTool('brush'); }
          else { setSelected([item.id]); setTool('select'); }
        }}>{visual(item)?.name || item.prototype}</button>)}</>}
        <strong>Слои</strong>{[['tile', 'Полы'], ['entity', 'Объекты и стены'], ['decal', 'Декали']].map(([id, name]) => <label key={id}><input type="checkbox" checked={layers[id]} onChange={(e) => setLayers({ ...layers, [id]: e.target.checked })} />{name}</label>)}
        <strong>Область grid</strong><div className="GridEditor__regions">
          {[[-1, 0, '←', 'Область левее'], [0, 1, '↑', 'Область выше'], [0, -1, '↓', 'Область ниже'], [1, 0, '→', 'Область правее']].map(([dx, dy, label, name]) =>
            <button key={name} aria-label={name} disabled={pending} onClick={() => region(dx, dy)}>{label}</button>)}
        </div>
        <strong>{selected.length ? `Выбрано: ${selected.length}` : 'Кисть'}</strong><p>{one?.prototype || templateBrush?.name || brush?.name || 'Выберите элемент каталога'}</p>
        <button disabled={locked} onClick={rotate}>Повернуть · R ({tool === 'select' && one ? one.rotation : rotation}°)</button>
        <label><input type="checkbox" checked={replace} onChange={(e) => setReplace(e.target.checked)} />Заменять стены</label>
        {(brush?.kind === 'decal' || one?.kind === 'decal') && <>
          <label>Цвет <input aria-label="Цвет декали" type="color" value={color} disabled={locked} onChange={(e) => { setColor(e.target.value); if (one?.kind === 'decal') patchSelected({ color: e.target.value }); }} /></label>
          <label>Слой декали <input aria-label="Слой декали" type="number" min={-100} max={100} value={z} disabled={locked} onChange={(e) => { const value = Math.max(-100, Math.min(100, Number(e.target.value))); setZ(value); if (one?.kind === 'decal') patchSelected({ z: value }); }} /></label>
          <label><input type="checkbox" checked={cleanable} disabled={locked} onChange={(e) => { setCleanable(e.target.checked); if (one?.kind === 'decal') patchSelected({ cleanable: e.target.checked }); }} />Можно смыть</label>
        </>}
        {one?.kind === 'entity' && <label><input type="checkbox" disabled={locked} checked={one.anchored} onChange={(e) => patchSelected({ anchored: e.target.checked })} />Закреплён</label>}
        {one && one.kind !== 'tile' && selected.length === 1 && ['x', 'y'].map((axis) => <label key={axis}>{axis.toUpperCase()}
          <input type="number" aria-label={`Координата ${axis.toUpperCase()}`} step={0.125} value={one[axis]} disabled={locked}
            onChange={(e) => { const value = Number(e.target.value); if (Number.isFinite(value)) patchSelected({ [axis]: value }); }} /></label>)}
        <button disabled={locked || !selected.length} onClick={remove}>Удалить · Delete</button>
        <button disabled={locked || !selected.length} onClick={() => setClipboard(copyItems(draft.filter((i) => selected.includes(i.id))))}>Копировать · Ctrl+C</button>
        <button disabled={locked || !clipboard.length} onClick={paste}>Вставить · Ctrl+V</button>
        <strong>Свой шаблон</strong><input aria-label="Имя шаблона" placeholder="Например: каюта 5×4" value={templateName} maxLength={64} onFocus={() => act('focus')} onChange={(e) => setTemplateName(e.target.value)} />
        <button disabled={locked || !selected.length || selected.length > 512 || !templateName.trim()} onClick={() =>
          startTransfer(makeTemplate(draft.filter((i) => selected.includes(i.id))), 'template', templateName.trim())}>Сохранить выделение</button>
        <small>Шаблоны и избранное сохраняются на этом компьютере.</small>
        <small>Рамка с пустой клетки: выделение.<br />Shift + ЛКМ: объект под подсветкой.<br />Ctrl+Z / Ctrl+Shift+Z: история.<br />Esc: сброс инструмента.<br />Правки отправляются кнопкой «Применить».</small>
      </aside>
    </div>
    <footer><span className={errors || data.success === false ? 'error' : ''} role="status">{errors || (data.success === false && data.status) || (changes ? 'Черновик · нажмите «Применить», чтобы изменить grid' : data.status) || (data.initialized ? 'Карта инициализирована · для экспорта откройте исходник в mapping' : 'Изменения отправляются кнопкой «Применить»')}</span>
      <button disabled={locked || !!changes || data.initialized} onClick={() => act('download', { scope: 'grid' })} title="Скачать весь grid, включая область за пределами плана">Скачать grid</button>
      <button disabled={locked || !!changes || data.initialized} onClick={() => act('download', { scope: 'map' })} title="Скачать всю карту в .yml">Скачать карту</button>
      <button disabled={locked || !!changes} onClick={() => act('fixAtmos')} title="Восстановить стартовые газы после изменения площади grid">Атмосфера</button>
      <button disabled={pending || !changes} onClick={() => { change(copyItems(data.items || [])); setSelected([]); }}>Сбросить</button>
      <button className="primary" disabled={locked || !changes || !!errors || changes > 2048} onClick={apply}>{pending ? 'Применение…' : 'Применить'}</button></footer>
    {confirmClose && <div className="GridEditor__veil"><div role="dialog" aria-label="Подтверждение редактора"><strong>{confirmClose.deleteTemplate ? `Удалить шаблон «${confirmClose.name}»?` : 'Отказаться от черновика?'}</strong><p>{confirmClose.deleteTemplate ? 'Постройки на карте сохранятся.' : `В черновике ${changes} изменений.`}</p><button onClick={() => setConfirmClose(false)}>Продолжить</button><button onClick={() => {
      if (confirmClose === true) act('close'); else if (confirmClose.deleteTemplate) { act('deleteTemplate', { id: confirmClose.deleteTemplate }); setConfirmClose(false); }
      else { act('region', confirmClose); setConfirmClose(false); }
    }}>{confirmClose.deleteTemplate ? 'Удалить шаблон' : confirmClose === true ? 'Отказаться и закрыть' : 'Отказаться и перейти'}</button></div></div>}
  </div>;
};
