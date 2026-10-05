using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ClinicAppointmentSystem.Services;

// Target: Telegram.Bot 22.x (long polling). Method names may differ slightly in other versions.

// Implement this against your DbContext once the models are known.
public interface IBookingService
{
    Task<bool> IsLinkedAsync(long chatId);
    Task<bool> LinkByPhoneAsync(long chatId, string phone);
    Task<List<(int Id, string Name)>> GetDoctorsAsync();
    Task<List<TimeOnly>> GetFreeSlotsAsync(int doctorId, DateOnly date);
    Task<bool> BookAsync(long chatId, int doctorId, DateOnly date, TimeOnly time);
}

public enum Step { None, AwaitingContact, AwaitingDoctor, AwaitingDate, AwaitingSlot, AwaitingConfirm }

public class Session
{
    public Step Step { get; set; } = Step.None;
    public int DoctorId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
}

public class TelegramBotService : BackgroundService
{
    private readonly TelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TelegramBotService> _log;
    private static readonly ConcurrentDictionary<long, Session> Sessions = new();

    public TelegramBotService(IConfiguration cfg, IServiceScopeFactory scopes, ILogger<TelegramBotService> log)
    {
        var token = cfg["Telegram:BotToken"]
            ?? throw new InvalidOperationException("Telegram:BotToken is missing.");
        _bot = new TelegramBotClient(token);
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _bot.DeleteWebhook(dropPendingUpdates: true, cancellationToken: ct);
        _bot.StartReceiving(OnUpdate, OnError, new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
        }, ct);
    }

    private Task OnError(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
    {
        _log.LogError(ex, "Telegram error from {Source}", source);
        return Task.CompletedTask;
    }

    private async Task OnUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        long chatId;
        string? text = null, data = null;
        Contact? contact = null;
        long? fromId = null;

        if (update.Message is { } m)
        {
            chatId = m.Chat.Id; text = m.Text; contact = m.Contact; fromId = m.From?.Id;
        }
        else if (update.CallbackQuery is { } cq && cq.Message is { } cm)
        {
            chatId = cm.Chat.Id; data = cq.Data;
            await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        }
        else return;

        using var scope = _scopes.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var s = Sessions.GetOrAdd(chatId, _ => new Session());

        // Start / restart
        if (text == "/start" || s.Step == Step.None)
        {
            if (!await svc.IsLinkedAsync(chatId))
            {
                s.Step = Step.AwaitingContact;
                await bot.SendMessage(chatId, "Welcome. Please share your phone number to link your account.",
                    replyMarkup: new ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Share my number"))
                    { ResizeKeyboard = true, OneTimeKeyboard = true }, cancellationToken: ct);
                return;
            }
            await ShowDoctors(bot, svc, chatId, s, ct);
            return;
        }

        switch (s.Step)
        {
            case Step.AwaitingContact:
                // Accept only the sender's own contact (prevents linking someone else's number)
                if (contact is null || contact.UserId != fromId)
                {
                    await bot.SendMessage(chatId, "Please use the button to share your own number.", cancellationToken: ct);
                    return;
                }
                if (await svc.LinkByPhoneAsync(chatId, contact.PhoneNumber))
                {
                    await bot.SendMessage(chatId, "Account linked.", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    await ShowDoctors(bot, svc, chatId, s, ct);
                }
                else
                {
                    await bot.SendMessage(chatId, "No patient found with this number. Please register on the website first.", cancellationToken: ct);
                    s.Step = Step.None;
                }
                break;

            case Step.AwaitingDoctor when data?.StartsWith("doc:") == true:
                s.DoctorId = int.Parse(data[4..]);
                s.Step = Step.AwaitingDate;
                await bot.SendMessage(chatId, "Send the date (yyyy-MM-dd):", cancellationToken: ct);
                break;

            case Step.AwaitingDate:
                if (!DateOnly.TryParse(text, out var date) || date < DateOnly.FromDateTime(DateTime.Today))
                {
                    await bot.SendMessage(chatId, "Invalid or past date. Use yyyy-MM-dd.", cancellationToken: ct);
                    return;
                }
                var slots = await svc.GetFreeSlotsAsync(s.DoctorId, date);
                if (slots.Count == 0)
                {
                    await bot.SendMessage(chatId, "No free slots on this date. Send another date:", cancellationToken: ct);
                    return;
                }
                s.Date = date;
                s.Step = Step.AwaitingSlot;
                var rows = slots.Select(t => new[] { InlineKeyboardButton.WithCallbackData(t.ToString("HH:mm"), $"slot:{t:HH\\:mm}") });
                await bot.SendMessage(chatId, "Choose a time:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
                break;

            case Step.AwaitingSlot when data?.StartsWith("slot:") == true:
                s.Time = TimeOnly.Parse(data[5..]);
                s.Step = Step.AwaitingConfirm;
                await bot.SendMessage(chatId, $"Confirm booking on {s.Date:yyyy-MM-dd} at {s.Time:HH:mm}?",
                    replyMarkup: new InlineKeyboardMarkup([[
                        InlineKeyboardButton.WithCallbackData("Confirm", "yes"),
                        InlineKeyboardButton.WithCallbackData("Cancel", "no")]]),
                    cancellationToken: ct);
                break;

            case Step.AwaitingConfirm when data == "yes":
                var ok = await svc.BookAsync(chatId, s.DoctorId, s.Date, s.Time);
                await bot.SendMessage(chatId, ok ? "Your appointment is booked." : "That slot was just taken. Please try again with /start.", cancellationToken: ct);
                s.Step = Step.None;
                break;

            case Step.AwaitingConfirm when data == "no":
                await bot.SendMessage(chatId, "Cancelled. Send /start to begin again.", cancellationToken: ct);
                s.Step = Step.None;
                break;
        }
    }

    private static async Task ShowDoctors(ITelegramBotClient bot, IBookingService svc, long chatId, Session s, CancellationToken ct)
    {
        var doctors = await svc.GetDoctorsAsync();
        s.Step = Step.AwaitingDoctor;
        var rows = doctors.Select(d => new[] { InlineKeyboardButton.WithCallbackData(d.Name, $"doc:{d.Id}") });
        await bot.SendMessage(chatId, "Choose a doctor:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }
}
