# BlueMoon emote comparison

Source: MOLOT-BlueMoon-Station, AGPL-3.0 (see UPSTREAM-LICENSE).
Compared common living, carbon and human definitions in code/modules/mob/living/{emote.dm,carbon/emote.dm,carbon/human/emote.dm}. This audit covers literal messages and command keys; species-specific/silicon modules and dynamic DM actions require separate adapters.

105 distinct commands: 24 already have SS14 equivalents; 50 descriptive emotes are added here. Together they cover 74/105 (70%) of the compared set. Synonyms may share a native emote. Text gestures preserve their descriptions; explicit BlueMoon audio bindings are documented in EMOTE-SOUNDS.md. SS13 animations and mechanical effects require separate adapters.

Fainting, collapse, surrender, sitting, deathgasp, hacking, syndicate objectives, rock-paper-scissors and species-specific wing/tail/machine actions are excluded because their mechanics cannot be represented by an unrestricted chat message. Duplicate aliases blushh/kiss2 are also excluded.

Native equivalents: chuckle → Laugh, cough → Cough, gasp → Gasp, giggle → Laugh, jump → Jump, laugh → Laugh, chitter → Chitter, scream → Scream, sigh → Sigh, sneeze → Sneeze, snore → Snore, whimper → Whimper, yawn → Yawn, beep → HarpyBeep, bubble → Bubble, clap → ClapSingle, screech → Scream, salute → Salute, chime → Chime, squeak → Squeak, shriek → Scream, cry → Crying, buzz → Buzz, ping → Ping.

| BlueMoon command | SS14 ID | Category |
| --- | --- | --- |
| blush | BlueMoonBlush | General |
| bow | BlueMoonBow | Hands |
| choke | BlueMoonChoke | Vocal |
| cross | BlueMoonCross | Hands |
| dance | BlueMoonDance | Hands |
| drool | BlueMoonDrool | General |
| frown | BlueMoonFrown | General |
| glare | BlueMoonGlare | General |
| grin | BlueMoonGrin | General |
| groan | BlueMoonGroan | Vocal |
| grimace | BlueMoonGrimace | General |
| kiss | BlueMoonKiss | General |
| look | BlueMoonLook | General |
| nod | BlueMoonNod | General |
| point | BlueMoonPoint | Hands |
| pout | BlueMoonPout | General |
| scowl | BlueMoonScowl | General |
| shake | BlueMoonShake | General |
| shiver | BlueMoonShiver | General |
| smile | BlueMoonSmile | General |
| smirk | BlueMoonSmirk | General |
| smug | BlueMoonSmug | General |
| sniff | BlueMoonSniff | Vocal |
| stare | BlueMoonStare | General |
| stretch | BlueMoonStretch | Hands |
| sulk | BlueMoonSulk | General |
| sway | BlueMoonSway | General |
| tremble | BlueMoonTremble | General |
| twitch | BlueMoonTwitch | General |
| twitch_s | BlueMoonTwitchS | General |
| wave | BlueMoonWave | Hands |
| wsmile | BlueMoonWsmile | General |
| inhale | BlueMoonInhale | Vocal |
| exhale | BlueMoonExhale | Vocal |
| medic | BlueMoonMedic | Vocal |
| airguitar | BlueMoonAirguitar | Hands |
| blink | BlueMoonBlink | General |
| blink3 | BlueMoonBlink3 | General |
| moan | BlueMoonMoan | Vocal |
| scratch | BlueMoonScratch | Hands |
| wink | BlueMoonWink | General |
| grumble | BlueMoonGrumble | Vocal |
| eyebrow | BlueMoonEyebrow | General |
| handshake | BlueMoonHandshake | Hands |
| hug | BlueMoonHug | Hands |
| mumble | BlueMoonMumble | Vocal |
| pale | BlueMoonPale | General |
| raise | BlueMoonRaise | Hands |
| shrug | BlueMoonShrug | General |
| protect | BlueMoonProtect | Vocal |

Run node tools/compare-emotes.cjs [upstream-path] for a read-only refreshed comparison; node tools/import-emotes.cjs [upstream-path] regenerates the explicit selection and localized assets. Server availability/whitelist checks apply to both typed and pinned emotes.
