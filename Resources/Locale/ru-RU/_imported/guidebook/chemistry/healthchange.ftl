# Imported from ru-RU-additional; existing Russian keys are preserved.

health-change-display =
    { $deltasign ->
        [-1] [color=green]{ NATURALFIXED($amount, 2) }[/color] ед. { $kind }
       *[1] [color=red]{ NATURALFIXED($amount, 2) }[/color] ед. { $kind }
    }
