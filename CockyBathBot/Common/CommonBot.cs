using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

using MihaZupan;

using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    public class CommonBot
    {
        #region Поля

        protected TelegramBotClient bot;
        private User user;

        public delegate void CommandHandler(object sender, Message messageEventArgs);

        // Список доступных команд
        private Dictionary<string, CommandHandler> commandList;

        // Список команд с описанием для помощи
        private List<string> help;

        // Набор стикеров
        private Dictionary<string, string> stickers;

        // Голосования
        protected List<TelegramPoll> polls;

        #endregion Поля

        #region Свойства

        public string Name
        {
            get
            {
               return user == null ? string.Empty : user.Username;
            }
        }

        #endregion Свойства

        #region Вспомогательные функции

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text)
                return;

            string command = message.Text.Split(' ').First().Replace("@" + user.Username, string.Empty).ToLower();
            if (commandList.ContainsKey(command))
            {
                message.Text = message.Text.Replace(command, string.Empty).Trim();
                commandList[command](sender, message);
            }
            else
                // Отвечаем только на цитаты своих сообщений
                if(message.Text.StartsWith("/") || 
                    message.Text.Contains(user.Username) || 
                    message.ReplyToMessage != null && message.ReplyToMessage.From == user)
                    UnknownCommand(sender, message);
        }

        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            // Обработка голосования
            TelegramPoll poll = polls.FirstOrDefault(p => p.ChatId == callbackQuery.Message.Chat.Id && 
                p.MessageId == callbackQuery.Message.MessageId);

            if (poll != null)
            {
                bool isVoted = poll.RegisterAnswer(callbackQuery.Data, callbackQuery.From);
                await bot.AnswerCallbackQueryAsync(
                    callbackQuery.Id,
                    isVoted ? $"Голос за {callbackQuery.Data}!" : "Ваш голос уже учтён!");
                return;
            }

            try
            {
                await bot.AnswerCallbackQueryAsync(
                    callbackQuery.Id,
                    $"Received {callbackQuery.Data}");
            }
            catch (Exception ex)
            {
            }
        }

        private async void BotOnInlineQueryReceived(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {
            Console.WriteLine($"Received inline query from: {inlineQueryEventArgs.InlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                new InlineQueryResultLocation(
                    id: "1",
                    latitude: 40.7058316f,
                    longitude: -74.2581888f,
                    title: "New York")   // displayed result
                    {
                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 40.7058316f,
                            longitude: -74.2581888f)    // message if result is selected
                    },

                new InlineQueryResultLocation(
                    id: "2",
                    latitude: 13.1449577f,
                    longitude: 52.507629f,
                    title: "Berlin") // displayed result
                    {

                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 13.1449577f,
                            longitude: 52.507629f)   // message if result is selected
                    }
            };

            await bot.AnswerInlineQueryAsync(
                inlineQueryEventArgs.InlineQuery.Id,
                results,
                isPersonal: true,
                cacheTime: 0);
        }

        private void BotOnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResultEventArgs.ChosenInlineResult.ResultId}");
        }

        private void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message);
        }

        private void GetCommandsList()
        {
            commandList = new Dictionary<string, CommandHandler>();
            help = new List<string>();

            var methods = GetType()
                .GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(TelegramCommand), true).Length > 0);

            foreach (MethodInfo method in methods)
            {
                string command = ((TelegramCommand)method.GetCustomAttributes(typeof(TelegramCommand)).First()).Command;
                if (commandList.ContainsKey(command))
                    continue;

                string description = ((DescriptionAttribute)method.GetCustomAttributes(typeof(DescriptionAttribute)).FirstOrDefault())?.Description;

                commandList.Add(command, (CommandHandler)method.CreateDelegate(typeof(CommandHandler), this));
                help.Add($"{command} {description}");
            }
        }

        protected void SendSticker(Message message, string name)
        {
            if (!stickers.ContainsKey(name))
                return;

            bot.SendStickerAsync(
                message.Chat.Id,
                new InputOnlineFile(stickers[name]));
        }

        #endregion Вспомогательные функции

        #region Основные функции

        public CommonBot(string apiKey, string proxyUrl = "", int proxyPort = 0)
        {
            HttpToSocks5Proxy proxy = null;
            if (!string.IsNullOrEmpty(proxyUrl) && proxyPort != 0)
                proxy = new HttpToSocks5Proxy(proxyUrl, proxyPort) {
                    // Allows you to use proxies that are only allowing connections to Telegram
                    ResolveHostnamesLocally = true
                };

            bot = new TelegramBotClient(apiKey, proxy);
            user = bot.GetMeAsync().Result;
            polls = new List<TelegramPoll>();

            GetCommandsList();
            stickers = GetStickers();

            // Обработчики сообщений
            if (bot == null)
                return;

            bot.OnMessage += BotOnMessageReceived;
            bot.OnMessageEdited += BotOnMessageReceived;
            bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            bot.OnInlineQuery += BotOnInlineQueryReceived;
            bot.OnInlineResultChosen += BotOnChosenInlineResultReceived;
            bot.OnReceiveError += BotOnReceiveError;
        }

        public void StartReceiving()
        {
            bot?.StartReceiving(Array.Empty<UpdateType>());
        }

        public void StopReceiving()
        {
            bot?.StopReceiving();
        }

        public virtual Dictionary<string, string> GetStickers()
        {
            return new Dictionary<string, string>();
        }

        #region Команды

        [TelegramCommand("/start")]
        public virtual void Start(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                "Бот запущен!");
        }

        [TelegramCommand("/help")]
        [Description("Помощь по командам.")]
        public virtual void GetHelp(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                string.Join("\n", help));
        }

        [TelegramCommand("/test")]
        [Description("Тест бота.")]
        public virtual void Test(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                "Testing success");
        }

        [TelegramCommand("/poll")]
        [Description("Новое голосование. Команда: <вопрос>: <вариант 1>;<вариант 2>[;<вариант 3>]")]
        public virtual void CreatePool(object sender, Message message)
        {
            string[] data = message.Text.Split(':');
            string question = data[0].Trim();
            string[] answers = data[1].Split(';');

            if (string.IsNullOrEmpty(question) || answers.Length < 2)
            {
                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    "Неправильный формат команды.");
                return;
            }

            TelegramPoll poll = new TelegramPoll(bot, message.Chat.Id);
            poll.Send(message.From, question, answers);
            polls.Add(poll);

            // Удаление голосования по завершению
            poll.End += () => { polls.Remove(poll); };
        }

        [TelegramCommand("/pollresult")]
        [Description("Текущие результаты голосования.")]
        public virtual void GetPoolResult(object sender, Message message)
        {
            if (polls.Count == 0)
            {
                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    "Нет доступных голосований.",
                    replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            // Пользователь не указал голосование
            if (message.Text.Split(' ').Length < 2)
            {
                ReplyKeyboardMarkup requestReplyKeyboard = new ReplyKeyboardMarkup(polls.Select(p => new KeyboardButton($"/pollresult {p.Question}")), true, true);

                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    "Результат какого голосования показать?",
                    replyMarkup: requestReplyKeyboard);
            }
            else
            {
                // Проверка голосований
                TelegramPoll poll = polls.FirstOrDefault(p => p.Question == message.Text);
                poll?.ShowPollResult(message);
            }
        }

        [TelegramCommand("/endpoll")]
        [Description("Завершить голосование.")]
        public virtual void StopPool(object sender, Message message)
        {
            if (polls.Count == 0)
            {
                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    "Нет доступных голосований.",
                    replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            // Пользователь не указал голосование
            if (message.Text.Split(' ').Length < 2)
            {
                ReplyKeyboardMarkup requestReplyKeyboard = new ReplyKeyboardMarkup(polls.Select(p => new KeyboardButton($"/endpoll {p.Question}")), true, true);

                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    "Какое голосование остановить?",
                    replyMarkup: requestReplyKeyboard);
            }
            else
            {
                // Проверка голосований
                TelegramPoll poll = polls.FirstOrDefault(p => p.Question == message.Text);
                if (poll == null)
                    return;

                if (poll.Creator != message.From)
                {
                    bot.SendTextMessageAsync(
                        message.Chat.Id,
                        "Голосование может остановить только создатель.",
                        replyMarkup: new ReplyKeyboardRemove());
                    return;
                }

                poll.EndPoll();
            }
        }

        public virtual void UnknownCommand(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                "Unknown command");
        }

        #endregion Команды

        #endregion Основные функции
    }
}
