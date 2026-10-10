# Imported from ru-RU-additional; existing Russian keys are preserved.

cmd-invalid-arg-number-error = Недопустимое число аргументов.

cmd-parse-failure-integer = { $arg } не является допустимым integer.

cmd-parse-failure-float = { $arg } не является допустимым float.

cmd-parse-failure-bool = { $arg } не является допустимым bool.

cmd-parse-failure-uid = { $arg } не является допустимым UID сущности.

cmd-parse-failure-mapid = { $arg } не является допустимым MapId.

cmd-parse-failure-entity-exist = UID { $arg } не соответствует существующей сущности.

cmd-error-file-not-found = Не удалось найти файл: { $file }.

cmd-failure-no-attached-entity = К этой оболочке не привязана никакая сущность.

cmd-help-desc = Выводит общую справку или справку по определённой команде

cmd-help-unknown = Неизвестная команда: { $command }

cmd-help-invalid-args = Недопустимое количество аргументов.

cmd-help-arg-cmdname = [имя команды]

cmd-cvar-desc = Получает или устанавливает CVar.

cmd-cvar-invalid-args = Должно быть представлено ровно один или два аргумента.

cmd-cvar-not-registered = CVar '{ $cvar }' не зарегистрирован. Используйте 'cvar ?' для получения списка всех зарегистрированных CVar-ов.

cmd-cvar-parse-error = Входное значение имеет неправильный формат для типа { $type }

cmd-cvar-compl-list = Список доступных CVar-ов

cmd-list-desc = Выводит список доступных команд с опциональным поисковым фильтром

cmd-list-arg-filter = [фильтр]

cmd-remoteexec-desc = Выполняет команду на стороне сервера

cmd-remoteexec-help =
    Использование: > <command> [arg] [arg] [arg...]
    Выполняет команду на стороне сервера. Это необходимо, если на клиенте имеется команда с таким же именем, так как при простом выполнении команды сначала будет запущена команда на клиенте.

cmd-gc-desc = Запускает GC (Garbage Collector, Сборка мусора)

cmd-gc-failed-parse = Не удалось спарсить аргумент.

cmd-gc-arg-generation = [поколение]

cmd-gcf-desc = Запускает GC, полную, со сжатием 'кучи больших объектов' (LOH-compacting) и всего.

cmd-gc_mode-desc = Изменяет/отображает режим задержки GC

cmd-gc_mode-current = текущий режим задержки gc: { $prevMode }

cmd-gc_mode-possible = возможные режимы:

cmd-gc_mode-unknown = неизвестный режим задержки gc: { $arg }

cmd-gc_mode-attempt = попытка изменения режима задержки gc: { $prevMode } -> { $mode }

cmd-gc_mode-result = полученный режим задержки gc: { $mode }

cmd-gc_mode-arg-type = [тип]

cmd-mem-desc = Выводит информацию об управляемой памяти

cmd-mem-report =
    Размер кучи: { TOSTRING($heapSize, "N0") }
    Всего распределено: { TOSTRING($totalAllocated, "N0") }

cmd-physics-overlay = { $overlay } не является распознанным оверлеем

cmd-lsasm-desc = Выводит список загруженных сборок по контексту загрузки

cmd-lsasm-help = Использование: lsasm

cmd-exec-desc = Исполняет скриптовый файл из записываемых пользовательских данных игры

cmd-dump_net_comps-desc = Выводит таблицу сетевых компонентов.

cmd-dump_net_comps-error-writeable = Регистрация всё ещё доступна для записи, сетевые идентификаторы не были сгенерированы.

cmd-dump_net_comps-header = Регистрации сетевых компонентов:

cmd-dump_event_tables-desc = Выводит таблицы направленных событий для сущности.

cmd-dump_event_tables-missing-arg-entity = Отсутствует аргумент сущности

cmd-dump_event_tables-error-entity = Недопустимая сущность

cmd-monitor-desc = Переключение отладочного монитора в меню F3.

cmd-monitor-invalid-name = Недопустимое имя монитора

cmd-monitor-arg-count = Отсутствует аргумент монитора

cmd-monitor-minus-all-hint = Скрывает все мониторы

cmd-monitor-plus-all-hint = Показывает все мониторы

cmd-set-ambient-light-desc = Позволяет установить эмбиентое освещение для указанной карты, в формате SRGB.

cmd-set-ambient-light-parse = Не удалось спарсить аргументы как байтовые значения цветов.

cmd-savemap-desc = Сериализует карту на диск. Не будет сохранять карту после инициализации, если это не будет сделано принудительно.

cmd-savemap-not-exist = Целевая карта не существует.

cmd-savemap-init-warning = Попытка сохранить карту после инициализации без принудительного сохранения.

cmd-savemap-attempt = Попытка сохранить карту { $mapId } в { $path }.

cmd-savemap-success = Карта успешно сохранена.

cmd-loadmap-desc = Загружает карту с диска в игру.

cmd-loadmap-nullspace = Невозможно загрузить в карту 0.

cmd-loadmap-exists = Карта { $mapId } уже существует.

cmd-loadmap-success = Карта { $mapId } была загружена из { $path }.

cmd-loadmap-error = При загрузке карты из { $path } произошла ошибка.

cmd-flushcookies-desc = Сброс хранилища CEF-cookie на диск

cmd-ldrsc-desc = Предварительно кэширует ресурс.

cmd-rldrsc-desc = Перезагружает ресурсы.

cmd-gridtc-desc = Получить количество плиток в гриде.

cmd-guidump-desc = Дамп дерева интерфейса в /guidump.txt в данные пользователя.

cmd-uitest-desc = Открыть UI окно для тестирования

cmd-uitest2-desc = Открывает UI контрольного тестирования ОС

cmd-uitest2-error-args = Ожидается не более одного аргумента

cmd-uitest2-error-tab = Недопустимая вкладка: '{ $value }'

cmd-setclipboard-desc = Устанавливает системный буфер обмена

cmd-getclipboard-desc = Получает системный буфер обмена

cmd-togglelight-desc = Переключает рендеринг света.

cmd-togglefov-desc = Переключает поле зрения клиента.

cmd-togglehardfov-desc = Включает жёсткое поле зрения клиента. (для отладки space-station-14#2353)

cmd-toggleshadows-desc = Переключение рендеринга теней.

cmd-togglelightbuf-desc = Переключение рендеринга освещения. Сюда входят тени, но не поле зрения.

cmd-chunkinfo-desc = Получает информацию о чанке под курсором мыши.

cmd-rldshader-desc = Перезагружает все шейдеры.

cmd-cldbglyr-desc = Переключение слоёв отладки поле зрения и освещения.

cmd-key-info-desc = Информация о ключе для клавиши.

cmd-bind-desc = Привязывает комбинацию клавиш ввода с командой ввода.

cmd-net-draw-interp-desc = Переключает отладочный рисунок сетевой интерполяции.

cmd-net-watch-ent-desc = Выводит на консоль все сетевые обновления для EntityId.

cmd-net-refresh-desc = Запрашивает полное состояние сервера.

cmd-net-entity-report-desc = Переключает панель отчёта о сетевых сущностях.

cmd-fill-desc = Заполнить консоль для отладки.

cmd-cls-desc = Очищает консоль.

cmd-sendgarbage-desc = Отправляет мусор на сервер.

cmd-loadgrid-desc = Загружает грид из файла на существующую карту.

cmd-loc-desc = Выводит абсолютное местоположение сущности игрока в консоль.

cmd-tpgrid-desc = Телепортирует грид в новое место.

cmd-rmgrid-desc = Удаляет грид с карты. Вы не можете удалить стандартный грид.

cmd-mapinit-desc = Запускает инициализацию карты на карте.

cmd-lsmap-desc = Перечисляет карты.

cmd-lsgrid-desc = Перечисляет гриды.

cmd-addmap-desc = Добавляет в раунд новую пустую карту. Если mapID уже существует, эта команда ничего не сделает.

cmd-rmmap-desc = Удаляет карту из мира. Вы не можете удалить nullspace.

cmd-savegrid-desc = Сериализует грид на диск.

cmd-testbed-desc = Загружает физический испытательный стенд на указаной карте.

cmd-saveconfig-desc = Сохраняет конфигурацию клиента в файл конфигурации.

cmd-addcomp-desc = Добавляет компонент сущности.

cmd-addcompc-desc = Добавляет компонент сущности на клиенте.

cmd-rmcomp-desc = Удаляет компонент у сущности.

cmd-rmcompc-desc = Удаляет компонент у сущности на клиенте.

cmd-addview-desc = Позволяет подписаться на просмотр сущности в целях отладки.

cmd-addviewc-desc = Позволяет подписаться на просмотр сущности в целях отладки.

cmd-removeview-desc = Позволяет отписаться от просмотра сущности в целях отладки.

cmd-loglevel-desc = Изменяет уровень логирования для предоставленного sawmill.

cmd-testlog-desc = Записывает протокол тестов в sawmill.

cmd-vv-desc = Открывает просмотр переменных.

cmd-showvelocities-desc = Отображает угловую и линейную скорости.

cmd-setinputcontext-desc = Устанавливает активный контекст ввода.

cmd-forall-desc = Запускает команду для всех сущностей с данным компонентом.

cmd-delete-desc = Удаляет сущность с указанным ID.

cmd-showtime-desc = Показывает время сервера.

cmd-restart-desc = Корректно перезапускает сервер (не только раунд).

cmd-shutdown-desc = Корректно выключает сервер.

cmd-netaudit-desc = Выводит информацию о безопасности NetMsg.

cmd-tp-desc = Телепортирует игрока в любую точку в раунде.

cmd-tpto-desc = Телепортирует текущего игрока или указанных игроков/сущностей к местоположению первого игрока/сущности.

cmd-tpto-destination-hint = место назначения (uid или имя пользователя)

cmd-tpto-victim-hint = сущность для телепортации (uid или имя пользователя)

cmd-tpto-parse-error = Не удаётся распознать сущность или игрока: { $str }

cmd-listplayers-desc = Перечисляет всех игроков, подключённых в данный момент.

cmd-kick-desc = Кикает подключённого игрока с сервера, отключая его от сети.

cmd-spin-desc = Заставляет сущность вращаться. Сущность по умолчанию является надклассом прикреплённого игрока.

cmd-rldloc-desc = Перезагружает локализацию (клиент и сервер).

cmd-spawn-desc = Создаёт сущность определённого типа.

cmd-cspawn-desc = Спавнит на стороне клиента сущность определённого типа у ваших ног.

cmd-dumpentities-desc = Дамп списка объектов.

cmd-getcomponentregistration-desc = Получает информацию о регистрации компонента.

cmd-showrays-desc = Переключает отладку отображения физических лучей. Необходимо указать целое число для <raylifetime>.

cmd-disconnect-desc = Немедленно отключиться от сервера и вернуться в главное меню.

cmd-entfo-desc = Отображает подробную диагностику сущности.

cmd-fuck-desc = Вызывает исключение

cmd-showpos-desc = Включает отрисовку для всех позиций сущностей в игре.

cmd-sggcell-desc = Перечисляет сущности в ячейке сетки привязки.

cmd-overrideplayername-desc = Изменяет имя, используемое при попытке подключения к серверу.

cmd-showanchored-desc = Показывает закреплённые объекты на определённой плитке.

cmd-dmetamem-desc = Выводит члены типа в формате, подходящем для файла конфигурации песочницы.

cmd-launchauth-desc = Загрузить токены аутентификации из данных лаунчера, чтобы облегчить тестирование работающих серверов.

cmd-lightbb-desc = Переключить отображение световой ограничительной рамки.

cmd-monitorinfo-desc = Информация о мониторах

cmd-setmonitor-desc = Установить монитор

cmd-physics-desc = Показывает наложение отладочной физики. Аргумент определяет наложение.

cmd-hardquit-desc = Мгновенно убивает игровой клиент.

cmd-quit-desc = Корректное завершение работы клиента игры.

cmd-csi-desc = Открывает интерактивную консоль C#.

cmd-scsi-desc = Открывает интерактивную консоль C# на сервере.

cmd-watch-desc = Открывает окно просмотра переменных.

cmd-showspritebb-desc = Переключить отображение границ спрайта

cmd-togglelookup-desc = Показывает/скрывает границы списка сущностей с помощью наложения.

cmd-net_entityreport-desc = Переключает панель отчёта о сетевых сущностях.

cmd-net_refresh-desc = Запрашивает полное состояние сервера.

cmd-net_graph-desc = Переключает панель статистики сети.

cmd-net_watchent-desc = Выводит в консоль все сетевые обновления для EntityId.

cmd-net_draw_interp-desc = Включает отладочную отрисовку сетевой интерполяции.

cmd-vram-desc = Отображает статистику использования видеопамяти игрой.

cmd-showislands-desc = Показывает текущие физические тела, задействованные в каждом physics island.

cmd-showgridnodes-desc = Показывает узлы для разделения сетки.

cmd-profsnap-desc = Сделать снимок профилирования.

cmd-devwindow-desc = Окно разработки

cmd-scene-desc = Немедленно сменяет UI сцены/состояния.

cmd-szr_stats-desc = Сообщить статистику сериализатора.

cmd-hwid-desc = Возвращает текущий HWID (HardWare ID).

cmd-vvread-desc = Получить значение пути с помощью VV (View Variables).

cmd-vvwrite-desc = Изменить значение пути с помощью VV (View Variables).

cmd-vvinvoke-desc = Вызов/запуск пути с аргументами с помощью VV.

cmd-dump_dependency_injectors-desc = Дамп кэша инжектора зависимостей IoCManager.

cmd-dump_dependency_injectors-total-count = Общее количество: { $total }

cmd-dump_netserializer_type_map-desc = Дамп карты типов NetSerializer и хеша сериализатора.

cmd-hub_advertise_now-desc = Немедленно разместить сервер в хабе

cmd-echo-desc = Вывести аргументы в консоль

cmd-vfs_ls-desc = Перечислить содержимое каталогов в VFS.

cmd-vfs_ls-err-args = Нужен ровно 1 аргумент.
