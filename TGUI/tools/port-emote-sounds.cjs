// Explicit mappings copied from BlueMoon's emote DM, not approximated SS14 sounds.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const upstream = process.argv[2] || 'C:/old_disk/repos/MOLOT-BlueMoon-Station';
const series = (stem, count) => Array.from({ length: count }, (_, i) => stem + (i + 1) + '.ogg');
const medic = fs.readFileSync(path.join(upstream, 'code/modules/mob/living/emote.dm'), 'utf8')
  .split('/datum/emote/sound/human/medic/run_emote')[1].split('/datum/emote/')[0];
const mapping = {
  BlueMoonChoke: { maleSound: series('sound/voice/gasp_male', 7), femaleSound: series('sound/voice/gasp_female', 7) },
  BlueMoonGroan: { sound: ['sound/voice/roar.ogg'] },
  BlueMoonSniff: { maleSound: ['sound/voice/sniff_m1.ogg'], femaleSound: ['sound/voice/sniff_f1.ogg'] },
  BlueMoonMoan: { maleSound: series('modular_sand/sound/interactions/final_m', 3), femaleSound: series('modular_sand/sound/interactions/moan_f', 7) },
  BlueMoonProtect: { sound: ['sound/voice/emperorprotects.ogg'] },
  // Preserve duplicate entries: BlueMoon uses them as weights in pick().
  BlueMoonMedic: { sound: [...medic.matchAll(/'([^']+\.ogg)'/g)].map(match => match[1]) },
};
let collections = '# Exact BlueMoon emote audio selections; source mapping: TGUI/EMOTE-SOUNDS.md.\n';
const hashes = new Map();
const prototype = path.join(root, 'Resources/Prototypes/_DeepLagoon/Voice/bluemoon_emotes.yml');
let yaml = fs.readFileSync(prototype, 'utf8').replace(/^  (sound|maleSound|femaleSound):\r?\n(?:    [^\r\n]*\r?\n)*/gm, '');
for (const [id, variants] of Object.entries(mapping)) {
  let fields = '';
  for (const [field, files] of Object.entries(variants)) {
    if (!files.length) throw new Error('Empty source mapping: ' + id);
    const paths = files.map(file => {
      const source = path.resolve(upstream, file);
      if (!source.startsWith(path.resolve(upstream) + path.sep)) throw new Error('Invalid source path');
      const bytes = fs.readFileSync(source);
      const target = path.join(root, 'Resources/Audio/_DeepLagoon/BlueMoonEmotes', file);
      fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, bytes);
      hashes.set(file, crypto.createHash('sha256').update(bytes).digest('hex'));
      return '/Audio/_DeepLagoon/BlueMoonEmotes/' + file;
    });
    const collection = id + field[0].toUpperCase() + field.slice(1);
    fields += `  ${field}:\n`;
    if (paths.length === 1) fields += `    path: ${paths[0]}\n`;
    else {
      fields += `    collection: ${collection}\n`;
      collections += `\n- type: soundCollection\n  id: ${collection}\n  files:\n${paths.map(file => '  - ' + file).join('\n')}\n`;
    }
    // BlueMoon's playsound(..., 50, TRUE), adapted to SS14's dB/variation API.
    fields += '    params:\n      volume: -6\n      variation: 0.125\n';
  }
  yaml = yaml.replace(new RegExp('(^  id: ' + id + '\\r?\\n)', 'm'), '$1' + fields);
}
fs.writeFileSync(prototype, yaml);
fs.copyFileSync(path.join(upstream, 'sound/voice/attribution.txt'),
  path.join(root, 'Resources/Audio/_DeepLagoon/BlueMoonEmotes/sound/voice/attribution.txt'));
fs.writeFileSync(path.join(root, 'Resources/Prototypes/_DeepLagoon/Voice/bluemoon_sounds.yml'), collections);
fs.writeFileSync(path.join(root, 'Resources/Audio/_DeepLagoon/BlueMoonEmotes/SOURCE.txt'),
  'Copied byte-for-byte from MOLOT-BlueMoon-Station. Source repository license: TGUI/UPSTREAM-LICENSE.\n' +
  'Mappings: code/modules/mob/living/emote.dm, code/modules/mob/living/carbon/emote.dm, code/__SANDCODE/DEFINES/lewd.dm.\n\n' +
  [...hashes].map(([file, hash]) => hash + '  ' + file).join('\n') + '\n');
fs.writeFileSync(path.join(root, 'TGUI/EMOTE-SOUNDS.md'), '# BlueMoon emote sounds\n\n' +
  'Exact source files and random selections are copied, including male/female variants and weighted medic calls. SS14 plays them positionally on the server through its normal emote event. Volume/pitch variation use SS14 equivalents.\n\n' +
  '| Emote | BlueMoon source audio |\n| --- | --- |\n' + Object.entries(mapping).map(([id, variants]) =>
    '| ' + id + ' | ' + Object.entries(variants).map(([variant, files]) => variant + ': ' + files.join(', ')).join('; ') + ' |').join('\n') + '\n\n' +
  'Other imported gestures have no sound assignment in these BlueMoon definitions; no artificial audio is attached to them. Existing SS14 emotes retain their native voice/species sounds. Source hashes are in Resources/Audio/_DeepLagoon/BlueMoonEmotes/SOURCE.txt.\n');
console.log('Ported exact sound bindings for ' + Object.keys(mapping).length + ' emotes, ' + hashes.size + ' unique audio files.');
