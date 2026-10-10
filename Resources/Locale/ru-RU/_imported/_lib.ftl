# Imported from ru-RU-additional; existing Russian keys are preserved.

zzzz-fmt-pressure =
    { TOSTRING($divided, "F1") } { $places ->
        [0] кПа
        [1] МПа
        [2] ГПа
        [3] ТПа
        [4] ППа
       *[5] ???
    }

zzzz-fmt-power-watts =
    { TOSTRING($divided, "F1") } { $places ->
        [0] Вт
        [1] кВт
        [2] МВт
        [3] ГВт
        [4] ТВт
       *[5] ???
    }

zzzz-fmt-power-joules =
    { TOSTRING($divided, "F1") } { $places ->
        [0] Дж
        [1] кДж
        [2] МДж
        [3] ГДж
        [4] ТДж
       *[5] ???
    }

zzzz-fmt-energy-watt-hours =
    { TOSTRING($divided, "F1") } { $places ->
        [0] Вт·ч
        [1] кВт·ч
        [2] МВт·ч
        [3] ГВт·ч
        [4] ТВт·ч
       *[5] ???
    }

zzzz-fmt-playtime = { $hours }ч { $minutes }м
