# Imported from ru-RU-additional; existing Russian keys are preserved.

zzzz-subject-pronoun =
    { GENDER($ent) ->
        [male] он
        [female] она
        [epicene] они
       *[neuter] оно
    }

zzzz-object-pronoun =
    { GENDER($ent) ->
        [male] его
        [female] её
        [epicene] их
       *[neuter] его
    }

zzzz-dat-object =
    { GENDER($ent) ->
        [male] ему
        [female] ей
        [epicene] им
       *[neuter] ему
    }

zzzz-possessive-pronoun =
    { GENDER($ent) ->
        [male] его
        [female] её
        [epicene] их
       *[neuter] его
    }

zzzz-possessive-adjective =
    { GENDER($ent) ->
        [male] его
        [female] её
        [epicene] их
       *[neuter] его
    }

zzzz-reflexive-pronoun =
    { GENDER($ent) ->
        [male] сам
        [female] сама
        [epicene] сами
       *[neuter] сам
    }

zzzz-conjugate-have =
    { GENDER($ent) ->
        [epicene] имеют
       *[other] имеет
    }
