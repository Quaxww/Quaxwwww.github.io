using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    private static ITelegramBotClient? _botClient;
    private static ReceiverOptions? _receiverOptions;

    static async Task Main()
    {
        // Убедитесь, что токен правильный и бот создан через @BotFather
        _botClient = new TelegramBotClient("8397722379:AAHXWFHDnBH3z6xVTZGW4sp8Sly8MqdcfTw");

        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>(), // Получаем все типы обновлений
            ThrowPendingUpdates = true,
        };

        using var cts = new CancellationTokenSource();

        // Запускаем бота
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: _receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await _botClient.GetMeAsync();
        Console.WriteLine($"Бот {me.FirstName} успешно запущен!");
        Console.WriteLine("Нажмите Ctrl+C для остановки...");

        // Ожидаем бесконечно
        await Task.Delay(-1, cts.Token);
    }

    // Создание главной клавиатуры с кнопкой "Старт"
    private static ReplyKeyboardMarkup GetMainKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("🚀 Старт")
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    // Создание клавиатуры с опциями после нажатия "Старт"
    private static ReplyKeyboardMarkup GetOptionsKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("📊 Информация о заказе"),
                new KeyboardButton("📱 Запустить приложение")
            },
            new[]
            {
                new KeyboardButton("🔙 Назад")
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    // Создание inline кнопки для веб-приложения
    private static InlineKeyboardMarkup GetWebAppInlineKeyboard()
    {
        var webAppInfo = new WebAppInfo { Url = "https://quaxww.github.io/" };
        var webAppButton = InlineKeyboardButton.WithWebApp("✨ Открыть приложение ТМК", webAppInfo);
        return new InlineKeyboardMarkup(webAppButton);
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageAsync(botClient, update.Message, cancellationToken);
                    break;

                case UpdateType.CallbackQuery:
                    Console.WriteLine("Пришел callback query");
                    break;

                default:
                    Console.WriteLine($"Получен неизвестный тип обновления: {update.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в обработчике обновлений: {ex}");
        }
    }

    private static async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message?.Text == null)
            return;

        var chatId = message.Chat.Id;
        var userName = message.From?.FirstName ?? "Неизвестный пользователь";
        var messageText = message.Text;

        Console.WriteLine($"Сообщение от {userName} (ID: {chatId}): {messageText}");

        // Обработка текстовых сообщений и кнопок
        if (messageText.StartsWith('/'))
        {
            // Обработка команд
            switch (messageText.ToLower())
            {
                case "/start":
                    await ShowStartMenu(botClient, chatId, userName, cancellationToken);
                    break;

                case "/run":
                    await LaunchApplication(botClient, chatId, cancellationToken);
                    break;

                case "/help":
                    await ShowHelp(botClient, chatId, cancellationToken);
                    break;

                default:
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Неизвестная команда. Используйте кнопку '🚀 Старт' для начала работы.",
                        replyMarkup: GetMainKeyboard(),
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        else
        {
            // Обработка нажатий на кнопки
            switch (messageText)
            {
                case "🚀 Старт":
                    await ShowOptionsMenu(botClient, chatId, userName, cancellationToken);
                    break;

                case "📊 Информация о заказе":
                    await ShowOrderInfo(botClient, chatId, cancellationToken);
                    break;

                case "📱 Запустить приложение":
                    await LaunchApplication(botClient, chatId, cancellationToken);
                    break;

                case "🔙 Назад":
                    await ShowStartMenu(botClient, chatId, userName, cancellationToken);
                    break;

                default:
                    // Если сообщение не распознано, показываем стартовое меню
                    await ShowStartMenu(botClient, chatId, userName, cancellationToken);
                    break;
            }
        }
    }

    // Показать стартовое меню с кнопкой "Старт"
    private static async Task ShowStartMenu(ITelegramBotClient botClient, long chatId, string userName, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"👋 Привет, {userName}!\n\nДобро пожаловать в сервис ТМК! Нажмите кнопку ниже, чтобы начать работу.",
            replyMarkup: GetMainKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Показать меню опций после нажатия "Старт"
    private static async Task ShowOptionsMenu(ITelegramBotClient botClient, long chatId, string userName, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"🎯 {userName}, выберите нужную опцию:",
            replyMarkup: GetOptionsKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Показать информацию о заказе
    private static async Task ShowOrderInfo(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        // Здесь можно добавить логику для получения реальной информации о заказе
        // Пока что демонстрационная информация

        var orderInfo = @"📊 **Информация о текущем заказе**

🆔 Номер заказа: TMK-20240115-0001
📅 Дата создания: 15.01.2024
💰 Сумма: 150 000 ₽
📦 Статус: В обработке

📋 **Состав заказа:**
• Труба стальная 57x3.5 - 100 м
• Труба стальная 89x4 - 50 м
• Труба стальная 108x4.5 - 25 м

⏳ Ожидаемая дата отгрузки: 20.01.2024";

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: orderInfo,
            parseMode: ParseMode.Markdown,
            replyMarkup: GetOptionsKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Запуск приложения
    private static async Task LaunchApplication(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "📱 **Приложение ТМК**\n\nНажмите кнопку ниже, чтобы открыть веб-приложение:",
            parseMode: ParseMode.Markdown,
            replyMarkup: GetWebAppInlineKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Показать справку
    private static async Task ShowHelp(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var helpText = @"ℹ️ **Помощь по боту ТМК**

**Основные опции:**
• 🚀 Старт - начать работу с ботом
• 📊 Информация о заказе - посмотреть статус текущего заказа
• 📱 Запустить приложение - открыть веб-приложение ТМК

**Команды:**
/start - показать стартовое меню
/run - запустить приложение
/help - показать справку

Для начала работы нажмите кнопку '🚀 Старт'";

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: helpText,
            parseMode: ParseMode.Markdown,
            replyMarkup: GetMainKeyboard(),
            cancellationToken: cancellationToken);
    }

    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine($"Ошибка: {errorMessage}");
        return Task.CompletedTask;
    }
}
