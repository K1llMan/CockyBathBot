using System.Collections.Generic;
using System.Linq;
using System.Timers;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    public class TelegramPoll
    {
        #region Поля

        // Таймер окончания опроса
        private Timer timer;
        private TelegramBotClient bot;
        private List<string> voters;
        private Dictionary<string, List<string>> results;

        #endregion Поля

        #region Свойства

        public User Creator { get; private set; }

        public int MessageId { get; private set; }

        public long ChatId { get; }

        public string Question { get; private set; }

        #endregion Свойства

        #region События

        public class EndPollEventArgs
        {
            public string Message { get; internal set; }
        }

        public delegate void EndPollEventHandler();
        public event EndPollEventHandler End;

        #endregion События

        #region Основные функции

        public void Send(User creator, string question, string[] answers)
        {
            Creator = creator;
            Question = question;

            int i = 0;
            var inlineKeyboard = new InlineKeyboardMarkup(answers.Select(a => InlineKeyboardButton.WithCallbackData(a.Trim())));

            Message msg = bot.SendTextMessageAsync(
                ChatId,
                Question,
                replyMarkup: inlineKeyboard).Result;

            // id сообщения с голосованием
            MessageId = msg.MessageId;

            results = answers.ToDictionary(a => a.Trim(), a => new List<string>());
            voters = new List<string>();
        }

        public bool RegisterAnswer(string answer, User user)
        {
            if (voters.Contains(user.Username))
                return false;

            voters.Add(user.Username);
            results[answer].Add(user.Username);
            return true;
        }

        public void ShowPollResult(Message message = null, bool isEnd = false)
        {
            var outStr = results
                .OrderByDescending(p => p.Value.Count)
                .Select(p =>
                    $"\"{p.Key}\": {(float) p.Value.Count / voters.Count * 100}% ({p.Value.Count}/{voters.Count})" +
                    (p.Value.Count > 0 
                        ? $"\n    {string.Join(", ", p.Value.Select(v => "@" + v))}" 
                        : string.Empty));

            bot.SendTextMessageAsync(
                ChatId,
                (isEnd ? "Голосование завершено! Результаты:\n" : string.Empty) +
                string.Join("\n", outStr),
                replyMarkup: new ReplyKeyboardRemove(),
                replyToMessageId: message?.MessageId ?? -1);
        }

        public void EndPoll()
        {
            timer.Stop();
            timer.Close();

            ShowPollResult(isEnd: true);
            End?.Invoke();
        }

        public TelegramPoll(TelegramBotClient bot, long chatId)
        {
            this.bot = bot;
            ChatId = chatId;

            // Таймер на сутки
            timer = new Timer(24 * 3600 * 1000);
            timer.Elapsed += (s, e) => { EndPoll(); };
            timer.Start();
        }

        #endregion Основные функции
    }
}
