// Explicit mappings copied from upstream emote DM, not approximated SS14 sounds.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const upstream = process.argv[2];
if (!upstream) throw new Error('Pass the source repository path as the first argument.');
const series = (stem, count) => Array.from({ length: count }, (_, i) => stem + (i + 1) + '.ogg');
const medic = fs.readFileSync(path.join(upstream, 'code/modules/mob/living/emote.dm'), 'utf8')
  .split('/datum/emote/sound/human/medic/run_emote')[1].split('/datum/emote/')[0];
const mapping = {
  ExtendedChoke: { maleSound: series('sound/voice/gasp_male', 7), femaleSound: series('sound/voice/gasp_female', 7) },
  ExtendedGroan: { sound: ['sound/voice/roar.ogg'] },
  ExtendedSniff: { maleSound: ['sound/voice/sniff_m1.ogg'], femaleSound: ['sound/voice/sniff_f1.ogg'] },
  ExtendedMoan: { maleSound: series('modular_sand/sound/interactions/final_m', 3), femaleSound: series('modular_sand/sound/interactions/moan_f', 7) },
  ExtendedProtect: { sound: ['sound/voice/emperorprotects.ogg'] },
  // Preserve duplicate entries: the source uses them as weights in pick().
  ExtendedMedic: { sound: [...medic.matchAll(/'([^']+\.ogg)'/g)].map(match => match[1]) },
};
let collections = '# Exact Extended emote audio selections; source hashes: Resources/Audio/_DeepLagoon/Emotes/SOURCE.txt.\n';
const hashes = new Map();
const prototype = path.join(root, 'Resources/Prototypes/_DeepLagoon/Voice/extended_emotes.yml');
let yaml = fs.readFileSync(prototype, 'utf8').replace(/^  (sound|maleSound|femaleSound):\r?\n(?:    [^\r\n]*\r?\n)*/gm, '');
for (const [id, variants] of Object.entries(mapping)) {
  let fields = '';
  for (const [field, files] of Object.entries(variants)) {
    if (!files.length) throw new Error('Empty source mapping: ' + id);
    const paths = files.map(file => {
      const source = path.resolve(upstream, file);
      if (!source.startsWith(path.resolve(upstream) + path.sep)) throw new Error('Invalid source path');
      const bytes = fs.readFileSync(source);
      const target = path.join(root, 'Resources/Audio/_DeepLagoon/Emotes', file);
      fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, bytes);
      hashes.set(file, crypto.createHash('sha256').update(bytes).digest('hex'));
      return '/Audio/_DeepLagoon/Emotes/' + file;
    });
    const collection = id + field[0].toUpperCase() + field.slice(1);
    fields += `  ${field}:\n`;
    if (paths.length === 1) fields += `    path: ${paths[0]}\n`;
    else {
      fields += `    collection: ${collection}\n`;
      collections += `\n- type: soundCollection\n  id: ${collection}\n  files:\n${paths.map(file => '  - ' + file).join('\n')}\n`;
    }
    // Upstream playsound(..., 50, TRUE), adapted to SS14's dB/variation API.
    fields += '    params:\n      volume: -6\n      variation: 0.125\n';
  }
  yaml = yaml.replace(new RegExp('(^  id: ' + id + '\\r?\\n)', 'm'), '$1' + fields);
}
fs.writeFileSync(prototype, yaml);
fs.copyFileSync(path.join(upstream, 'sound/voice/attribution.txt'),
  path.join(root, 'Resources/Audio/_DeepLagoon/Emotes/sound/voice/attribution.txt'));
fs.writeFileSync(path.join(root, 'Resources/Prototypes/_DeepLagoon/Voice/extended_sounds.yml'), collections);
fs.writeFileSync(path.join(root, 'Resources/Audio/_DeepLagoon/Emotes/SOURCE.txt'),
  'Copied byte-for-byte from upstream SS13 source. Source repository license: TGUI/UPSTREAM-LICENSE.\n' +
  'Mappings: code/modules/mob/living/emote.dm, code/modules/mob/living/carbon/emote.dm, code/__SANDCODE/DEFINES/lewd.dm.\n\n' +
  [...hashes].map(([file, hash]) => hash + '  ' + file).join('\n') + '\n');
console.log('Ported exact sound bindings for ' + Object.keys(mapping).length + ' emotes, ' + hashes.size + ' unique audio files.');
