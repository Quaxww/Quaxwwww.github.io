using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;

public class OrderData
{
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerCompany { get; set; }
    public string DeliveryAddress { get; set; }
    public string OrderComment { get; set; }
}

public class WebFormServer
{
    private HttpListener listener;
    private string url = "http://localhost:8080/";
    private bool isRunning = false;

    // Событие для уведомления о получении новых данных
    public event Action<OrderData> OnOrderReceived;

    public void Start()
    {
        listener = new HttpListener();
        listener.Prefixes.Add(url);
        
        try
        {
            listener.Start();
            isRunning = true;
            Console.WriteLine($"Сервер запущен на {url}");
            Console.WriteLine("Откройте браузер и перейдите по указанному адресу");
            
            // Запускаем обработку запросов в отдельном потоке
            Thread listenerThread = new Thread(ListenerLoop);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска сервера: {ex.Message}");
        }
    }

    public void Stop()
    {
        isRunning = false;
        listener?.Stop();
        listener?.Close();
        Console.WriteLine("Сервер остановлен");
    }

    private void ListenerLoop()
    {
        while (isRunning)
        {
            try
            {
                var context = listener.GetContext();
                ProcessRequest(context);
            }
            catch (HttpListenerException)
            {
                // Сервер был остановлен
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки запроса: {ex.Message}");
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/submit")
            {
                HandleFormSubmission(request, response);
            }
            else
            {
                ServeHtmlPage(response, "form.html");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            SendError(response, "Internal Server Error");
        }
        finally
        {
            response.Close();
        }
    }

    private void HandleFormSubmission(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = reader.ReadToEnd();
        
        var formData = ParseFormData(body);
        
        var order = new OrderData
        {
            CustomerName = formData.GetValueOrDefault("customerName", ""),
            CustomerPhone = formData.GetValueOrDefault("customerPhone", ""),
            CustomerEmail = formData.GetValueOrDefault("customerEmail", ""),
            CustomerCompany = formData.GetValueOrDefault("customerCompany", ""),
            DeliveryAddress = formData.GetValueOrDefault("deliveryAddress", ""),
            OrderComment = formData.GetValueOrDefault("orderComment", "")
        };

        // Сохраняем данные
        SaveOrderData(order);
        
        // Уведомляем подписчиков
        OnOrderReceived?.Invoke(order);
        
        // Отправляем успешный ответ
        ServeHtmlPage(response, "success.html");
        
        Console.WriteLine("\n=== ПОЛУЧЕН НОВЫЙ ЗАКАЗ ===");
        Console.WriteLine($"ФИО: {order.CustomerName}");
        Console.WriteLine($"Телефон: {order.CustomerPhone}");
        Console.WriteLine($"Email: {order.CustomerEmail}");
        Console.WriteLine($"Компания: {order.CustomerCompany}");
        Console.WriteLine($"Адрес: {order.DeliveryAddress}");
        Console.WriteLine($"Комментарий: {order.OrderComment}");
        Console.WriteLine("============================\n");
    }

    private Dictionary<string, string> ParseFormData(string formData)
    {
        var result = new Dictionary<string, string>();
        var pairs = formData.Split('&');
        
        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=');
            if (keyValue.Length == 2)
            {
                var key = Uri.UnescapeDataString(keyValue[0]);
                var value = Uri.UnescapeDataString(keyValue[1].Replace('+', ' '));
                result[key] = value;
            }
        }
        
        return result;
    }

    private void SaveOrderData(OrderData order)
    {
        // Сохраняем в JSON файл
        var json = JsonSerializer.Serialize(order, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        string fileName = $"order_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        File.WriteAllText(fileName, json, Encoding.UTF8);
        Console.WriteLine($"Данные сохранены в файл: {fileName}");
    }

    private void ServeHtmlPage(HttpListenerResponse response, string htmlFileName)
    {
        try
        {
            if (File.Exists(htmlFileName))
            {
                var html = File.ReadAllText(htmlFileName, Encoding.UTF8);
                var buffer = Encoding.UTF8.GetBytes(html);
                
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                SendError(response, $"HTML файл {htmlFileName} не найден");
            }
        }
        catch (Exception ex)
        {
            SendError(response, $"Ошибка чтения HTML файла: {ex.Message}");
        }
    }

    private void SendError(HttpListenerResponse response, string message)
    {
        var errorHtml = $@"
        <!DOCTYPE html>
        <html>
        <head><title>Ошибка</title></head>
        <body>
            <h1>Ошибка</h1>
            <p>{message}</p>
            <button onclick='window.history.back()'>Назад</button>
        </body>
        </html>";
        
        var buffer = Encoding.UTF8.GetBytes(errorHtml);
        response.StatusCode = 500;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        // Проверяем существование HTML файлов
        if (!File.Exists("form.html"))
        {
            Console.WriteLine("Ошибка: файл form.html не найден!");
            Console.WriteLine("Убедитесь, что form.html находится в той же папке, что и исполняемый файл.");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
            return;
        }

        if (!File.Exists("success.html"))
        {
            Console.WriteLine("Внимание: файл success.html не найден, будет использоваться стандартная страница успеха.");
        }
        
        var server = new WebFormServer();
        
        // Подписываемся на событие получения заказа
        server.OnOrderReceived += order =>
        {
            Console.WriteLine("Заказ передан на обработку...");
            ProcessOrder(order);
        };
        
        server.Start();
        
        Console.WriteLine("\nКоманды:");
        Console.WriteLine("Нажмите 'q' и Enter для выхода");
        Console.WriteLine("Нажмите 'r' и Enter для перезапуска сервера");
        Console.WriteLine("Нажмите Enter для проверки статуса\n");
        
        while (true)
        {
            var input = Console.ReadLine();
            if (input?.ToLower() == "q")
            {
                server.Stop();
                break;
            }
            else if (input?.ToLower() == "r")
            {
                server.Stop();
                Thread.Sleep(1000);
                server = new WebFormServer();
                server.Start();
            }
            else
            {
                Console.WriteLine("Сервер работает...");
            }
        }
        
        Console.WriteLine("Программа завершена. Нажмите любую клавишу...");
        Console.ReadKey();
    }
    
    static void ProcessOrder(OrderData order)
    {
        // Ваша бизнес-логика обработки заказа
        Console.WriteLine("=== НАЧАЛО ОБРАБОТКИ ЗАКАЗА ===");
        
        // Пример обработки
        if (!string.IsNullOrEmpty(order.CustomerCompany))
        {
            Console.WriteLine($"Корпоративный клиент: {order.CustomerCompany}");
        }
        
        // Здесь можно добавить сохранение в базу данных,
        // отправку email, генерацию документов и т.д.
        
        Thread.Sleep(1000); // Имитация обработки
        Console.WriteLine("=== ОБРАБОТКА ЗАКАЗА ЗАВЕРШЕНА ===\n");
    }
}