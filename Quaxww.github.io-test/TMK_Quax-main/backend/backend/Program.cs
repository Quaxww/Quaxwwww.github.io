using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ConsoleApp1.Database;
using ConsoleApp1.Database.Entities;
using Microsoft.EntityFrameworkCore;

class Program
{
    private static ITelegramBotClient? _botClient;
    private static ReceiverOptions? _receiverOptions;
    private static Dictionary<long, string> _userRoles = new Dictionary<long, string>(); // chatId -> role
    private static Dictionary<long, bool> _managerAuthenticated = new Dictionary<long, bool>(); // chatId -> isAuthenticated
    private const string MANAGER_PASSWORD = "manager123"; // Пароль для менеджера

    static async Task Main()
    {
        // Инициализация базы данных
        await InitializeDatabaseAsync();

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

    // Инициализация базы данных
    static async Task InitializeDatabaseAsync()
    {
        using var context = new Context();

        // Создание базы данных и таблиц
        await context.Database.EnsureCreatedAsync();

        // Добавление тестовых данных, если база пустая
        if (!context.Users.Any())
        {
            var user1 = new User { Name = "Иван Иванов" };
            var user2 = new User { Name = "Петр Петров" };

            context.Users.AddRange(user1, user2);
            await context.SaveChangesAsync();
            Console.WriteLine("Тестовые данные добавлены.");
        }
    }

    // Главное меню выбора роли
    private static ReplyKeyboardMarkup GetRoleSelectionKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("👤 Заказчик"),
                new KeyboardButton("👨‍💼 Менеджер")
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    // Меню для заказчика
    private static ReplyKeyboardMarkup GetCustomerKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("🌐 Перейти на сайт"),
                new KeyboardButton("📊 Отследить заказ")
            },
            new[]
            {
                new KeyboardButton("🔙 Назад к выбору роли")
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    // Меню для менеджера (до аутентификации)
    private static ReplyKeyboardMarkup GetManagerAuthKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("🔐 Войти как менеджер")
            },
            new[]
            {
                new KeyboardButton("🔙 Назад к выбору роли")
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    // Меню для менеджера (после аутентификации)
    private static ReplyKeyboardMarkup GetManagerKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("📋 Активные заказы"),
                new KeyboardButton("📊 Статистика")
            },
            new[]
            {
                new KeyboardButton("🔙 Назад к выбору роли")
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    // Inline кнопка для веб-сайта
    private static InlineKeyboardMarkup GetWebsiteInlineKeyboard()
    {
        var webAppInfo = new WebAppInfo { Url = "https://quaxww.github.io/" };
        var webAppButton = InlineKeyboardButton.WithWebApp("🌐 Открыть сайт ТМК", webAppInfo);
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

        // Обработка команд
        if (messageText.StartsWith('/'))
        {
            await HandleCommandAsync(botClient, chatId, userName, messageText, cancellationToken);
            return;
        }

        // Определяем текущую роль пользователя
        if (!_userRoles.ContainsKey(chatId))
        {
            // Показываем выбор роли для новых пользователей
            await ShowRoleSelection(botClient, chatId, userName, cancellationToken);
            return;
        }

        var userRole = _userRoles[chatId];

        // Обработка в зависимости от роли
        if (userRole == "customer")
        {
            await HandleCustomerMessageAsync(botClient, chatId, userName, messageText, cancellationToken);
        }
        else if (userRole == "manager")
        {
            await HandleManagerMessageAsync(botClient, chatId, userName, messageText, cancellationToken);
        }
    }

    private static async Task HandleCommandAsync(ITelegramBotClient botClient, long chatId, string userName, string command, CancellationToken cancellationToken)
    {
        switch (command.ToLower())
        {
            case "/start":
                _userRoles.Remove(chatId); // Сбрасываем роль при старте
                _managerAuthenticated.Remove(chatId); // Сбрасываем аутентификацию
                await ShowRoleSelection(botClient, chatId, userName, cancellationToken);
                break;

            case "/help":
                await ShowHelp(botClient, chatId, cancellationToken);
                break;

            default:
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Неизвестная команда. Используйте /start для начала работы.",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private static async Task HandleCustomerMessageAsync(ITelegramBotClient botClient, long chatId, string userName, string messageText, CancellationToken cancellationToken)
    {
        switch (messageText)
        {
            case "🌐 Перейти на сайт":
                await ShowWebsite(botClient, chatId, cancellationToken);
                break;

            case "📊 Отследить заказ":
                await TrackOrder(botClient, chatId, cancellationToken);
                break;

            case "🔙 Назад к выбору роли":
                _userRoles.Remove(chatId);
                await ShowRoleSelection(botClient, chatId, userName, cancellationToken);
                break;

            default:
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Используйте кнопки ниже для навигации:",
                    replyMarkup: GetCustomerKeyboard(),
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private static async Task HandleManagerMessageAsync(ITelegramBotClient botClient, long chatId, string userName, string messageText, CancellationToken cancellationToken)
    {
        // Проверяем аутентификацию менеджера
        if (!_managerAuthenticated.ContainsKey(chatId) || !_managerAuthenticated[chatId])
        {
            if (messageText == "🔐 Войти как менеджер")
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "🔐 Введите пароль для доступа к панели менеджера:",
                    cancellationToken: cancellationToken);
                return;
            }

            // Проверка пароля
            if (messageText == MANAGER_PASSWORD)
            {
                _managerAuthenticated[chatId] = true;
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "✅ Успешный вход! Добро пожаловать в панель менеджера.",
                    replyMarkup: GetManagerKeyboard(),
                    cancellationToken: cancellationToken);
            }
            else if (!string.IsNullOrEmpty(messageText))
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "❌ Неверный пароль. Попробуйте снова или вернитесь назад.",
                    replyMarkup: GetManagerAuthKeyboard(),
                    cancellationToken: cancellationToken);
            }
            return;
        }

        // Обработка команд аутентифицированного менеджера
        switch (messageText)
        {
            case "📋 Активные заказы":
                await ShowActiveOrders(botClient, chatId, cancellationToken);
                break;

            case "📊 Статистика":
                await ShowStatistics(botClient, chatId, cancellationToken);
                break;

            case "🔙 Назад к выбору роли":
                _userRoles.Remove(chatId);
                _managerAuthenticated.Remove(chatId);
                await ShowRoleSelection(botClient, chatId, userName, cancellationToken);
                break;

            default:
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Используйте кнопки ниже для навигации:",
                    replyMarkup: GetManagerKeyboard(),
                    cancellationToken: cancellationToken);
                break;
        }
    }

    // Показать выбор роли
    private static async Task ShowRoleSelection(ITelegramBotClient botClient, long chatId, string userName, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"👋 Привет, {userName}!\n\nВыберите вашу роль:",
            replyMarkup: GetRoleSelectionKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Обработка выбора роли
    public static async Task HandleRoleSelection(ITelegramBotClient botClient, long chatId, string userName, string role, CancellationToken cancellationToken)
    {
        _userRoles[chatId] = role;

        if (role == "customer")
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"👤 Добро пожаловать, {userName}!\n\nВы вошли как заказчик. Вы можете перейти на сайт или отследить свой заказ.",
                replyMarkup: GetCustomerKeyboard(),
                cancellationToken: cancellationToken);
        }
        else if (role == "manager")
        {
            _managerAuthenticated[chatId] = false;
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"👨‍💼 Добро пожаловать, {userName}!\n\nДля доступа к панели менеджера требуется аутентификация.",
                replyMarkup: GetManagerAuthKeyboard(),
                cancellationToken: cancellationToken);
        }
    }

    // Показать сайт
    private static async Task ShowWebsite(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "🌐 **Сайт ТМК**\n\nНажмите кнопку ниже, чтобы открыть сайт:",
            parseMode: ParseMode.Markdown,
            replyMarkup: GetWebsiteInlineKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Отслеживание заказа
    private static async Task TrackOrder(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        // Здесь можно добавить логику для получения реальной информации о заказе
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
            replyMarkup: GetCustomerKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Показать активные заказы (для менеджера)
    private static async Task ShowActiveOrders(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        using var context = new Context();
        var users = await context.Users.ToListAsync();

        var ordersInfo = "📋 **Активные заказы**\n\n";

        // Демонстрационные данные - в реальном приложении здесь будет запрос к БД заказов
        var demoOrders = new[]
        {
            new { Id = "TMK-20240115-0001", Customer = "Иван Иванов", Amount = "150 000 ₽", Status = "В обработке" },
            new { Id = "TMK-20240116-0002", Customer = "Петр Петров", Amount = "85 000 ₽", Status = "В производстве" },
            new { Id = "TMK-20240117-0003", Customer = "Сергей Сидоров", Amount = "210 000 ₽", Status = "Готов к отгрузке" }
        };

        foreach (var order in demoOrders)
        {
            ordersInfo += $"🆔 **{order.Id}**\n";
            ordersInfo += $"👤 Клиент: {order.Customer}\n";
            ordersInfo += $"💰 Сумма: {order.Amount}\n";
            ordersInfo += $"📦 Статус: {order.Status}\n\n";
        }

        ordersInfo += $"Всего активных заказов: {demoOrders.Length}";

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: ordersInfo,
            parseMode: ParseMode.Markdown,
            replyMarkup: GetManagerKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Показать статистику (для менеджера)
    private static async Task ShowStatistics(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var statistics = @"📊 **Статистика за январь 2024**

💰 Общая выручка: 2 450 000 ₽
📦 Заказов выполнено: 28
👥 Активных клиентов: 15
⭐ Средняя оценка: 4.8/5

📈 **Топ товаров:**
1. Труба стальная 57x3.5 - 45%
2. Труба стальная 89x4 - 30%
3. Труба стальная 108x4.5 - 25%";

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: statistics,
            parseMode: ParseMode.Markdown,
            replyMarkup: GetManagerKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Показать справку
    private static async Task ShowHelp(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var helpText = @"ℹ️ **Помощь по боту ТМК**

**Основные команды:**
/start - начать работу с ботом
/help - показать справку

**Для заказчиков:**
• 🌐 Перейти на сайт - открыть сайт ТМК
• 📊 Отследить заказ - посмотреть статус заказа

**Для менеджеров:**
• 🔐 Войти как менеджер - доступ к панели управления
• 📋 Активные заказы - просмотр текущих заказов
• 📊 Статистика - аналитика продаж

Для начала работы используйте /start";

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: helpText,
            parseMode: ParseMode.Markdown,
            replyMarkup: GetRoleSelectionKeyboard(),
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
