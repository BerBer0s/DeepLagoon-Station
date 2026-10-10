# Imported from ru-RU-additional; existing Russian keys are preserved.

ammonia-smell = Что-то резко попахивает!!

rotting-rotting = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { GENDER($target) ->
        [male] гниёт
        [female] гниёт
        [epicene] гниют
       *[neuter] гниёт
    }![/color]

rotting-bloated = [color=orangered]{ CAPITALIZE(SUBJECT($target)) } { GENDER($target) ->
        [male] вздулся
        [female] вздулась
        [epicene] вздулись
       *[neuter] вздулось
    }![/color]

rotting-extremely-bloated = [color=red]{ CAPITALIZE(SUBJECT($target)) } сильно { GENDER($target) ->
        [male] вздулся
        [female] вздулась
        [epicene] вздулись
       *[neuter] вздулось
    }![/color]
