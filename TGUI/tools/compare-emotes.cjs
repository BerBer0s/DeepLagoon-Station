// Read-only comparison against a local BlueMoon checkout; never evaluates DM code.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const upstream = process.argv[2] || 'C:/old_disk/repos/MOLOT-BlueMoon-Station';
const files = ['code/modules/mob/living/emote.dm', 'code/modules/mob/living/carbon/emote.dm',
  'code/modules/mob/living/carbon/human/emote.dm'];
const records = new Map();
for (const file of files) {
  const text = fs.readFileSync(path.join(upstream, file), 'utf8');
  for (const match of text.matchAll(/^\/datum\/emote[^\r\n]*(?:\r?\n(?!\/datum\/emote)[^\r\n]*)*/gm)) {
    const header = match[0].split('\n')[0].trim();
    if (header.includes('(')) continue;
    const properties = {};
    for (const property of match[0].matchAll(/^\t(name|key|key_third_person|message|emote_type|restraint_check)\s*=\s*([^\r\n]+)/gm))
      properties[property[1]] = property[2].replace(/\s*\/\/.*$/, '').trim();
    records.set(header, { source: file, ...properties });
  }
}
function inherit(header) {
  const parent = header.slice(0, header.lastIndexOf('/'));
  return { ...(records.has(parent) ? inherit(parent) : {}), ...records.get(header) };
}
function quoted(value) { return /^"(?:[^"\\]|\\.)*"$/.test(value || '') ? JSON.parse(value) : null; }
function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => entry.isDirectory()
    ? walk(path.join(dir, entry.name)) : [path.join(dir, entry.name)]);
}
const existing = new Map();
for (const file of walk(path.join(root, 'Resources/Prototypes')).filter(file => /\.yml$/.test(file))) {
  const text = fs.readFileSync(file, 'utf8');
  for (const doc of text.split(/(?=^- type: )/m)) {
    if (!/^- type: emote\s*$/m.test(doc)) continue;
    const id = /^  id: ([^\s#]+)/m.exec(doc)?.[1];
    if (!id) continue;
    existing.set(id.toLowerCase(), id);
    const triggers = /  chatTriggers:[ \t]*\r?\n((?: {2,4}- [^\r\n]+\r?\n?)*)/.exec(doc)?.[1] || '';
    for (const trigger of triggers.matchAll(/ {2,4}- ([^\r\n#]+)/g)) existing.set(trigger[1].trim().replace(/^["']|["']$/g, '').toLowerCase(), id);
    const inline = /  chatTriggers: \[([^\]]*)\]/.exec(doc)?.[1] || '';
    for (const trigger of inline.split(',')) if (trigger.trim()) existing.set(trigger.trim().replace(/^["']|["']$/g, '').toLowerCase(), id);
  }
}
const results = new Map();
for (const header of records.keys()) {
  const record = inherit(header);
  const key = quoted(record.key);
  const message = quoted(record.message);
  if (!key || !message) continue;
  const name = quoted(record.name) || key;
  const native = existing.get(key.toLowerCase());
  const previous = results.get(key);
  // Prefer a named human specialization over an inherited base definition.
  if (previous && previous.name !== key && name === key) continue;
  results.set(key, { key, name, message, native: native || null, source: record.source,
    vocal: record.emote_type === 'EMOTE_AUDIBLE', hands: record.restraint_check === 'TRUE' });
}
process.stdout.write(JSON.stringify([...results.values()], null, 2));
