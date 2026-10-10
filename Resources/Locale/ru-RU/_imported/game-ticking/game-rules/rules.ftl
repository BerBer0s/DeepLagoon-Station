# Imported from ru-RU-additional; existing Russian keys are preserved.

rule-restarting-in-seconds =
    Перезапуск через { $seconds } { $seconds ->
        [one] секунду
        [few] секунды
       *[other] секунд
    }.

rule-time-has-run-out = Время вышло!

rule-respawn-in-seconds =
    Возрождение через { $second } { $second ->
        [one] секунду
        [few] секунды
       *[other] секунд
    }...
