command-list-langs-desc = Список языков на которых вы можете в данный момент разговаривать.
command-list-langs-help = Применение: {$command}

command-saylang-desc = Отправить сообщение на определенном языке. Чтобы выбрать язык, укажите его название или позицию в списке языков.
command-saylang-help = Применение: {$command} <language id> <message>. Пример: {$command} TauCetiBasic "Hello World!". Пример: {$command} 1 "Hello World!"

command-language-select-desc = Выбирает язык на котором в данный момент вы разговаривате. Чтобы выбрать язык, укажите его название или позицию в списке языков.
command-language-select-help = Применение: {$command} <language id>. Пример: {$command} 1. Пример: {$command} TauCetiBasic

command-language-spoken = Говорить:
command-language-understood = Понимать:
command-language-current-entry = {$id}. {$language} - {$name} (current)
command-language-entry = {$id}. {$language} - {$name}

command-language-invalid-number = число языка должно быть между 0 и {$total}, или используйте название языка.
command-language-invalid-language = Язык {$id} не существует или вы не можете на нем разговаривать.

# Toolshed

command-description-language-add = Добавляет новый язык к прикрепленной сущности. Два последних аргумента показывают возможен ли разговор/понимание этого языка. Пример: 'self language:add "Canilunzt" true true'
command-description-language-rm = Убирает язык у прикрепленной сущности. Работает по принципу language:add. Пример: 'self language:rm "TauCetiBasic" true true'.
command-description-language-lsspoken = Выводит список всех языков, на которых вы можете говорить. Пример: 'self language:lsspoken'
command-description-language-lsunderstood = Список всех языков которые вы можете понимать. Пример: 'self language:lssunderstood'

command-description-translator-addlang = Добавляет новый язык к прикрепленной сущности переводчика. See language:add for details.
command-description-translator-rmlang = Убирает язык у прикрепленной сущности переводчика. See language:rm for details.
command-description-translator-addrequired = Добавляет новый нужный язык к прикрепленной сущности переводчика. Пример: 'ent 1234 translator:addrequired "TauCetiBasic"'
command-description-translator-rmrequired = Убирает нужный язык у прикрепленной сущности переводчика. Пример: 'ent 1234 translator:rmrequired "TauCetiBasic"'
command-description-translator-lsspoken = Список всех языков, на которых может говорить прикрепленная сущность переводчик. Пример: 'ent 1234 translator:lsspoken'
command-description-translator-lsunderstood = Список всех языков которые может понимать прикрепленная сущность переводчик. Пример: 'ent 1234 translator:lssunderstood'
command-description-translator-lsrequired = Список всех необходимых языков для прикрепленной сущность переводчика. Пример: 'ent 1234 translator:lsrequired'

command-language-error-this-will-not-work = Это не будет работать.
command-language-error-not-a-translator = Сущность {$entity} не переводчик.
