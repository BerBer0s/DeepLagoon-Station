// Explicit, reviewable selection of descriptive BlueMoon emotes. No DM is executed.
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const upstream = process.argv[2] || 'C:/old_disk/repos/MOLOT-BlueMoon-Station';
const compared = JSON.parse(execFileSync(process.execPath, [path.join(__dirname, 'compare-emotes.cjs'), upstream]));
const keys = 'blush bow choke cross dance drool frown glare grin groan grimace kiss look nod point pout scowl shake shiver smile smirk smug sniff stare stretch sulk sway tremble twitch twitch_s wave wsmile inhale exhale medic airguitar blink blink3 moan scratch wink grumble eyebrow handshake hug mumble pale raise shrug protect'.split(' ');
const russian = { cross: 'Скрестить руки', grimace: 'Гримасничать', point: 'Указать пальцем',
  pout: 'Надуться', scowl: 'Нахмуриться', smirk: 'Ухмыльнуться', smug: 'Самодовольно ухмыльнуться',
  stretch: 'Потянуться', sulk: 'Опустить руки', sway: 'Покачиваться', inhale: 'Вдохнуть', exhale: 'Выдохнуть',
  grumble: 'Ворчать', protect: 'Сложить руки на груди' };
const english = {
  blush:'blushes.', bow:'bows.', choke:'chokes!', cross:'crosses their arms.', dance:'dances happily.',
  drool:'drools.', frown:'frowns.', glare:'glares.', grin:'grins.', groan:'groans!', grimace:'grimaces.',
  kiss:'blows a kiss.', look:'looks around.', nod:'nods.', point:'points.', pout:'pouts.', scowl:'scowls.',
  shake:'shakes their head.', shiver:'shivers.', smile:'smiles.', smirk:'smirks.', smug:'smirks smugly.',
  sniff:'sniffs.', stare:'stares.', stretch:'stretches their arms.', sulk:'lowers their arms sadly.',
  sway:'sways.', tremble:'trembles in fear!', twitch:'twitches violently.', twitch_s:'twitches.',
  wave:'waves.', wsmile:'smiles weakly.', inhale:'inhales.', exhale:'exhales.', medic:'calls for a medic!',
  airguitar:'plays an imaginary guitar.', blink:'blinks.', blink3:'blinks rapidly.', moan:'moans!',
  scratch:'scratches themselves.', wink:'winks.', grumble:'grumbles under their breath!',
  eyebrow:'raises an eyebrow.', handshake:'shakes their own hands.', hug:'hugs themselves.',
  mumble:'mumbles!', pale:'turns pale for a moment.', raise:'raises their hand.', shrug:'shrugs.',
  protect:'crosses their arms over their chest.',
};
let yaml = '# Descriptive emotes adapted from MOLOT-BlueMoon-Station (AGPL-3.0).\n# Source/license and comparison: TGUI/EMOTES.md, TGUI/UPSTREAM-LICENSE.\n';
let ru = '# BlueMoon descriptive emotes.\n', en = '# BlueMoon descriptive emotes.\n';
const report = [];
// SS13's EMOTE_AUDIBLE describes chat transmission, not the physiological source.
const vocalKeys = new Set('choke groan sniff inhale exhale medic moan grumble mumble protect'.split(' '));
const handKeys = new Set('bow cross dance point stretch wave airguitar scratch handshake hug raise protect'.split(' '));
for (const key of keys) {
  const emote = compared.find(record => record.key === key);
  if (!emote) throw new Error('Missing upstream emote: ' + key);
  const id = 'BlueMoon' + key.split('_').map(part => part[0].toUpperCase() + part.slice(1)).join('');
  if (emote.native && emote.native !== id) throw new Error('Already provided by SS14: ' + key);
  const loc = 'chat-emote-bluemoon-' + key.replaceAll('_', '-');
  const category = vocalKeys.has(key) ? 'Vocal' : handKeys.has(key) ? 'Hands' : 'General';
  yaml += `\n- type: emote\n  id: ${id}\n  name: ${loc}-name\n  category: ${category}\n  chatMessages: [${loc}-message]\n  chatTriggers: [${JSON.stringify(key)}]\n`;
  if (category !== 'General') yaml += `  whitelist:\n    components:\n    - ${category}\n`;
  // Do not advertise facial/limb gestures for machines or inanimate entities.
  else yaml += '  whitelist:\n    components:\n    - HumanoidAppearance\n';
  ru += `${loc}-name = ${russian[key] || emote.name}\n${loc}-message = ${emote.message}\n`;
  en += `${loc}-name = ${key.replaceAll('_', ' ')}\n${loc}-message = ${english[key]}\n`;
  report.push(`| ${key} | ${id} | ${category} |`);
}
for (const [file, text] of [
  ['Resources/Prototypes/_DeepLagoon/Voice/bluemoon_emotes.yml', yaml],
  ['Resources/Locale/ru-RU/_DeepLagoon/bluemoon-emotes.ftl', ru],
  ['Resources/Locale/en-US/_DeepLagoon/bluemoon-emotes.ftl', en],
]) { const target = path.join(root, file); fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, text); }
const existing = compared.filter(emote => emote.native && !emote.native.startsWith('BlueMoon'));
fs.writeFileSync(path.join(root, 'TGUI/EMOTES.md'), `# BlueMoon emote comparison\n\nSource: MOLOT-BlueMoon-Station, AGPL-3.0 (see UPSTREAM-LICENSE).\nCompared common living, carbon and human definitions in code/modules/mob/living/{emote.dm,carbon/emote.dm,carbon/human/emote.dm}. This audit covers literal messages and command keys; species-specific/silicon modules and dynamic DM actions require separate adapters.\n\n105 distinct commands: 24 already have SS14 equivalents; 50 descriptive emotes are added here. Together they cover 74/105 (70%) of the compared set. Synonyms may share a native emote. Text gestures preserve their descriptions; explicit BlueMoon audio bindings are documented in EMOTE-SOUNDS.md. SS13 animations and mechanical effects require separate adapters.\n\nFainting, collapse, surrender, sitting, deathgasp, hacking, syndicate objectives, rock-paper-scissors and species-specific wing/tail/machine actions are excluded because their mechanics cannot be represented by an unrestricted chat message. Duplicate aliases blushh/kiss2 are also excluded.\n\nNative equivalents: ${existing.map(emote => emote.key + ' → ' + emote.native).join(', ')}.\n\n| BlueMoon command | SS14 ID | Category |\n| --- | --- | --- |\n${report.join('\n')}\n\nRun node tools/compare-emotes.cjs [upstream-path] for a read-only refreshed comparison; node tools/import-emotes.cjs [upstream-path] regenerates the explicit selection and localized assets. Server availability/whitelist checks apply to both typed and pinned emotes.\n`);
console.log('Imported ' + keys.length + ' descriptive emotes.');
execFileSync(process.execPath, [path.join(__dirname, 'port-emote-sounds.cjs'), upstream], { stdio: 'inherit' });
