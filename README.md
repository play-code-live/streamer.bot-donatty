# Donatty + Streamer.bot = ♥️

[![GitHub All Releases](https://img.shields.io/github/downloads/play-code-live/streamer.bot-donatty/total.svg)](https://github.com/play-code-live/streamer.bot-donatty/releases) [![GitHub release](https://img.shields.io/github/release/play-code-live/streamer.bot-donatty.svg)](https://github.com/play-code-live/streamer.bot-donatty/releases)

Данный модуль является набором действий и комманд для интеграции с сервисом Donatty.

## Установка

> В разработке. Появится в скором времени

## Обновление

> В разработке. Появится в скором времени

## Структура

Весь список команд можно поделить на две условные группы:

### Служебные действия

Все служебные действия размещены в группе **Donatty Integration** и имеют префикс `--Donatty`. Все они плотно связаны друг с другом и не могут быть переименованны без нарушения работоспособности.

> **Важно!** Не переименовывайте служебные действия! Они имеют высокую зависимость друг от друга

* `--Donatty Code` - Содержит основной код интеграции, который вызывается посредством **Call C# Method** в Streamer.bot
* `--Donatty Authorization` - Основная логика авторизации в сервисе. Вызывается вводом команды `!donatty_connect`
* `--Donatty Enable Background Watcher` - Активирует действия `Init` и `Background Watcher` для работы интеграции и автоматического запуска.
* `--Donatty Background Watcher` - Фоновое действие, отслеживающее появление новых донатов. Всегда должно находиться в очереди. Для ручной постановки в очередь, введите команду `!donatty_start`
* `--Donatty Autostart` - Обеспечивает безопасный запуск кода отслеживания донатов
* `--Donatty Init` - Автоматически запускает код отслеживания донатов на запуске streamer.bot

### Действия-обработчики

#### Обработчики доната

Действия обработчики редактируются пользователем, для достижения желаемого эффекта реакции на донат. Однако, они имеют строго заданный шаблон именования.

Вся основная обработка донатов выполняется в действии `DonattyHandler_Default`. Из него вы можете развести логику в зависимости от диапазонов сумм. Однако, для точной суммы, есть другой подход...

Если вы хотите обработать донат на конкретную сумму, вам необходимо создать действие `DonattyHandler_SUM`, где вместо `SUM` вы указываете желаемое число.

#### Финализирующий обработчик доната

Если у вас заведено множество обработчиков для конкретных сумм, и в конце каждого приходится выполнять дополнительные операции или вызывать Action'ы - вы можете реализовать общую логику в специальном экшене `DonattyHandler_After`.

`DonattyHandler_After` выполняется в самом конце после любого другого обработчика доната, вне зависимости от того, был ли это `Default` обработчик или обработчик конкретной суммы.

## Аргументы

### Донат

Каждое обработанное событие доната сопровождается следующими аргументами:

| Аргумент                    | Описание                                                                                               |
| --------------------------- | ------------------------------------------------------------------------------------------------------ |
| `donatty.donation.username` | Имя пользователя. Не может быть пустым. В случаях анонимной поддержки подставляется значение Anonymous |
| `donatty.donation.message`  | Сообщение доната. Может быть пустым                                                                    |
| `donatty.donation.amount`   | Сумма поддержки в валюте зрителя                                                                       |
| `donatty.donation.currency` | Выбранная для поддержки валюта                                                                         |

## Автор

**play_code** <info@play-code.info>

https://twitch.tv/play_code
