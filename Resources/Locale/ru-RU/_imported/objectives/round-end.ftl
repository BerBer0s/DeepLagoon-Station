# Imported from ru-RU-additional; existing Russian keys are preserved.

objectives-round-end-result =
    { $count ->
        [one] Был один { $agent }.
        [few] Было { $count } { $agent }.
       *[other] Было { $count } { $agent }.
    }

objectives-round-end-result-in-custody = { $custody } из { $count } { $agent } были арестованы.

objectives-no-objectives = { $custody }{ $title } – { $agent }.

objectives-with-objectives = { $custody }{ $title } – { $agent } со следующими целями:

objectives-in-custody = [bold][color=red]| АРЕСТОВАН | [/color][/bold]
