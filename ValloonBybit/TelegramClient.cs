using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/**
 * @author Valloon Present
 * @version 2022-08-05
 */
namespace Valloon.Trading
{
    internal class TelegramClient
    {
        public static TelegramBotClient Client;
        public static User Me { get; set; }
        public static string[] adminNameArray;
        public static string[] chatAdminArray;
        public static string[] chatGuestArray;
        public static Logger logger;

        public static void Init(Config config)
        {
            if (string.IsNullOrWhiteSpace(config.TelegramToken)) return;
            if (Client == null)
            {
                Client = new TelegramBotClient(config.TelegramToken);
                using (var cts = new CancellationTokenSource())
                {
                    // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
                    var receiverOptions = new ReceiverOptions
                    {
                        AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
                    };
                    Client.StartReceiving(
                        updateHandler: HandleUpdateAsync,
                        errorHandler: HandlePollingErrorAsync,
                        receiverOptions: receiverOptions,
                        cancellationToken: cts.Token
                    );
                    Me = Client.GetMeAsync().Result;
                }
            }
            adminNameArray = config.TelegramAdmin?.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            chatAdminArray = config.TelegramChatAdmin?.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            chatGuestArray = config.TelegramChatGuest?.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}", "telegram_log");
            logger.WriteLine($"Telegram connected: username = {Me.Username}");
            logger.WriteLine($"adminArray = {(adminNameArray == null ? "Null" : string.Join(",", adminNameArray))}");
            logger.WriteLine($"listenArray = {(chatAdminArray == null ? "Null" : string.Join(",", chatAdminArray))}");
            logger.WriteLine($"broadcastArray = {(chatGuestArray == null ? "Null" : string.Join(",", chatGuestArray))}");
        }

        static readonly Dictionary<string, string> LastCommand = new Dictionary<string, string>();

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                long chatId;
                int messageId;
                string chatUsername;
                string senderUsername;
                string receivedMessageText;
                // Only process Message updates: https://core.telegram.org/bots/api#message
                if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text && update.Message.Chat.Type == ChatType.Private)
                {
                    // Only process text messages
                    chatId = update.Message.Chat.Id;
                    messageId = update.Message.MessageId;
                    chatUsername = update.Message.Chat.Username;
                    senderUsername = update.Message.From.Username;
                    receivedMessageText = update.Message.Text;
                    logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  \"{receivedMessageText}\" from {senderUsername}. chatId = {chatId}, messageId = {messageId}", ConsoleColor.DarkGray);
                }
                else if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text && (update.Message.Chat.Type == ChatType.Group || update.Message.Chat.Type == ChatType.Supergroup))
                {
                    chatId = update.Message.Chat.Id;
                    messageId = update.Message.MessageId;
                    chatUsername = update.Message.Chat.Username;
                    senderUsername = update.Message.From.Username;
                    receivedMessageText = update.Message.Text;
                    logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  \"{receivedMessageText}\" from {senderUsername}. chatId = {chatId}, messageId = {messageId}", ConsoleColor.DarkGray);
                    if (receivedMessageText[0] == '/' && receivedMessageText.EndsWith($"@{Me.Username}"))
                    {
                        var command = receivedMessageText.Substring(0, receivedMessageText.Length - $"@{Me.Username}".Length);
                        bool isAdmin = adminNameArray != null && adminNameArray.Contains(senderUsername);
                        switch (command)
                        {
                            case "/start":
                                if (isAdmin)
                                {
                                    string replyMessageText = chatId.ToString();
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                break;
                            case "/stop":
                                if (isAdmin)
                                {
                                    string replyMessageText = chatId.ToString();
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                break;
                            case "/now":
                                if (isAdmin || chatAdminArray != null && chatAdminArray.Contains(chatId.ToString()))
                                {
                                    string replyMessageText = PaxgStrategy.LastMessage;
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html);
                                }
                                break;
                        }
                    }

                    return;
                }
                else if (update.Type == UpdateType.CallbackQuery)
                {
                    chatId = update.CallbackQuery.Message.Chat.Id;
                    senderUsername = update.CallbackQuery.From.Username;
                    receivedMessageText = update.CallbackQuery.Data;
                    await botClient.AnswerCallbackQueryAsync(callbackQueryId: update.CallbackQuery.Id, cancellationToken: cancellationToken);
                }
                else
                    return;
                {
                    bool isAdmin = adminNameArray != null && adminNameArray.Contains(senderUsername);
                    if (receivedMessageText[0] == '/')
                    {
                        var command = receivedMessageText;
                        switch (command)
                        {
                            case "/start":
                                //if (isAdmin)
                                {
                                    string replyMessageText = chatId.ToString();
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                break;
                            case "/stop":
                                //if (isAdmin)
                                {
                                    string replyMessageText = chatId.ToString();
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                break;
                            case "/no_buy":
                                {
                                    PaxgStrategy.NoBuyMode = true;
                                    string replyMessageText = "No-Buy Mode";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html, replyMarkup: PaxgStrategy.GetReplyMarkup());
                                }
                                break;
                            case "/no_close":
                                {
                                    PaxgStrategy.NoCloseMode = true;
                                    string replyMessageText = "No-Close Mode";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html, replyMarkup: PaxgStrategy.GetReplyMarkup());
                                }
                                break;
                            case "/no_buy_and_close":
                                {
                                    PaxgStrategy.NoBuyMode = true;
                                    PaxgStrategy.NoCloseMode = true;
                                    string replyMessageText = "No-Buy & No-Close Mode";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html, replyMarkup: PaxgStrategy.GetReplyMarkup());
                                }
                                break;
                            case "/resume_buy":
                                {
                                    PaxgStrategy.NoBuyMode = false;
                                    string replyMessageText = "Buy Resumed";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html);
                                }
                                break;
                            case "/resume_close":
                                {
                                    PaxgStrategy.NoCloseMode = false;
                                    string replyMessageText = "Close Resumed";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html);
                                }
                                break;
                            case "/resume_buy_and_close":
                                {
                                    PaxgStrategy.NoBuyMode = false;
                                    PaxgStrategy.NoCloseMode = false;
                                    string replyMessageText = "Buy & Close Resumed";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html);
                                }
                                break;
                            case "/now":
                                {
                                    string replyMessageText = PaxgStrategy.LastMessage;
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html);
                                }
                                break;
                            case "/get_config":
                                {
                                    string replyMessageText = System.IO.File.ReadAllText("config-paxg.json");
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html);
                                }
                                break;
                            case string x when x.StartsWith("/set_config "):
                                {
                                    string value = x.Substring("/set_config ".Length).Trim();
                                    System.IO.File.WriteAllText("config-paxg.json", value);
                                    //string replyMessageText = value;
                                    //await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken, parseMode: ParseMode.Html);
                                }
                                break;
                            default:
                                {
                                    string replyMessageText = $"Unknown command: {command}";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                LastCommand.Remove(senderUsername);
                                break;
                        }
                    }
                    else if (LastCommand.ContainsKey(senderUsername))
                    {
                        if (receivedMessageText == "exit" || receivedMessageText == "/exit")
                            LastCommand.Remove(senderUsername);
                        else
                            switch (LastCommand[senderUsername])
                            {
                                default:
                                    {
                                        Logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  <ERROR>  Unknown error", ConsoleColor.Red);
                                    }
                                    break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red, false);
                logger.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}");
            }
        }

        static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            string ErrorMessage;
            if (exception is ApiRequestException apiRequestException)
            {
                ErrorMessage = $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}";
            }
            else
            {
                ErrorMessage = exception.InnerException == null ? exception.Message : exception.InnerException.Message;
            }
            if (logger != null) logger.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public static void SendMessageToAdmin(string text, ParseMode? parseMode = default, IReplyMarkup replyMarkup = default)
        {
            if (Client == null || chatAdminArray == null) return;
            try
            {
                int count = 0;
                foreach (var chat in chatAdminArray)
                {
                    if (string.IsNullOrWhiteSpace(chat)) continue;
                    var result = Client.SendTextMessageAsync(chatId: chat, text: text, disableWebPagePreview: true, parseMode: parseMode, replyMarkup: replyMarkup).Result;
                    count++;
                }
                logger.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  Message sent to {count} chats: {text}");
            }
            catch (Exception ex)
            {
                logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red, false);
                logger.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}");
            }
        }

        public static void SendMessageToGuest(string text, ParseMode? parseMode = default, IReplyMarkup replyMarkup = default)
        {
            if (Client == null || chatGuestArray == null) return;
            try
            {
                int count = 0;
                foreach (var chat in chatGuestArray)
                {
                    if (string.IsNullOrWhiteSpace(chat)) continue;
                    var result = Client.SendTextMessageAsync(chatId: chat, text: text, disableWebPagePreview: true, parseMode: parseMode, replyMarkup: replyMarkup).Result;
                    count++;
                }
                logger.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  Message sent to {count} chats: {text}");
            }
            catch (Exception ex)
            {
                logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red, false);
                logger.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}");
            }
        }

    }
}
