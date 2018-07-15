using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Timers;

using Newtonsoft.Json;

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using TelegramBot;

namespace CockyBathBot
{
    public class CockyBath: CommonBot
    {
        #region Поля

        private Dictionary<long, Bath> baths;

        private CockyLexer lexer;

        // Таймер окончания опроса
        private Timer cockyTimer;

        private int cockyLevel = 40;

        private List<FileInfo> girls;
        private FileSystemWatcher girlsWatcher;

        #endregion Поля

        #region Вспомогательные функции

        private void WatcherHandler(object sender, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);
            if (e.ChangeType == WatcherChangeTypes.Created)
                girls.Add(new FileInfo(e.FullPath));
            if (e.ChangeType == WatcherChangeTypes.Deleted)
                girls.Remove(girls.FirstOrDefault(f => f.Name == fileName));
        }

        private void InitWatchers()
        {
            girlsWatcher = new FileSystemWatcher
            {
                Path = "girls",
                NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName,
                Filter = "*.*",
                IncludeSubdirectories = true
            };

            girlsWatcher.Created += WatcherHandler;
            girlsWatcher.Deleted += WatcherHandler;

            // Включение слежения за директорией
            girlsWatcher.EnableRaisingEvents = true;
        }

        private void InitCockyCooldown()
        {
            // Таймер на час с остыванием дерзости
            cockyTimer = new Timer(3600 * 1000);
            cockyTimer.Elapsed += (s, e) => {
                cockyLevel = Math.Max(cockyLevel - 10, 0);
                cockyTimer.Start();
            };

            cockyTimer.Start();
        }

        private void LoadBaths()
        {
            baths = new Dictionary<long, Bath>();
            if (!Directory.Exists("baths"))
                Directory.CreateDirectory("baths");

            FileInfo[] files = new DirectoryInfo("baths").GetFiles("*.json");
            foreach (FileInfo file in files)
            {
                FileStream fs = null;
                StreamReader sr = null;

                try
                {
                    fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
                    sr = new StreamReader(fs);
                    Bath bath = JsonConvert.DeserializeObject<Bath>(sr.ReadToEnd());

                    baths[Convert.ToInt64(file.Name.Replace(file.Extension, string.Empty))] = bath;
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    sr?.Close();
                    fs?.Close();
                }
            }
        }

        private void SaveBath(Bath bath, long chatId)
        {
            FileStream fs = null;
            StreamWriter sw = null;

            try
            {
                fs = new FileStream($"baths\\{chatId}.json", FileMode.OpenOrCreate, FileAccess.Write);
                sw = new StreamWriter(fs);
                sw.Write(JsonConvert.SerializeObject(bath));
            }
            catch (Exception ex)
            {
            }
            finally
            {
                sw?.Close();
                fs?.Close();
            }
        }

        #endregion Вспомогательные функции

        #region Команды

        [TelegramCommand("/set")]
        [Description("Установить время начала и окончания баньки. Формат: <yyyyMMddHHmmss> <yyyyMMddHHmmss>")]
        public void SetTimer(object sender, Message message)
        {
            try
            {
                ChatMember member = bot.GetChatMemberAsync(message.Chat.Id, message.From.Id).Result;
                if (member.Status != ChatMemberStatus.Administrator && member.Status != ChatMemberStatus.Creator)
                {
                    bot.SendTextMessageAsync(
                        message.Chat.Id,
                        lexer.GetPhrase("notAdmin", cockyLevel, message.From));
                    cockyLevel = Math.Min(cockyLevel + 5, 100);
                    return;
                }

                string[] dates = message.Text.Split(' ');
                Bath bath = new Bath {
                    from = DateTime.ParseExact(dates[0], "yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                    to = DateTime.ParseExact(dates[1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                };

                // Сохраняется в файле, если вдруг бот перезапускается.
                SaveBath(bath, message.Chat.Id);

                baths[message.Chat.Id] = bath;

                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    lexer.GetPhrase("setComplete", cockyLevel, message.From));

                SendSticker(message, "ok");
            }
            catch
            {
                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    lexer.GetPhrase("wrongDate", cockyLevel, message.From));

                SendSticker(message, "yobana");
                cockyLevel = Math.Min(cockyLevel + 5, 100);
            }
        }

        [TelegramCommand("/hello")]
        [Description("Поздороваться с ботом.")]
        public void Hello(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                string.Format(lexer.GetPhrase("hello", cockyLevel, message.From), "@" + message.From.Username));

            SendSticker(message, "hohot");
        }

        [TelegramCommand("/howmuch")]
        [Description("Узнать, сколько до баньки.")]
        public void HowMuch(object sender, Message message)
        {
            if (!baths.ContainsKey(message.Chat.Id) || baths[message.Chat.Id].from == DateTime.MinValue)
            {
                bot.SendTextMessageAsync(
                    message.Chat.Id,
                    lexer.GetPhrase("unknownTime", cockyLevel, message.From));
                return;
            }

            Bath bath = baths[message.Chat.Id];
            TimeSpan time = bath.HowMuch();
            List<string> msg = new List<string>{
                $"До баньки осталось {time.ToString(@"dd\:hh\:mm\:ss")}\n",
                $"В секундах: {(int)time.TotalSeconds}",
                $"В минутах: {(int)time.TotalMinutes}",
                $"В часах: {(int)time.TotalHours}",
                $"В днях: {(int)time.TotalDays}"
            };

            bot.SendTextMessageAsync(
                message.Chat.Id,
                string.Join("\n", msg));
        }

        [TelegramCommand("/boobs")]
        [Description("Заказать свежее мясо.")]
        public async void GetFreshMeat(object sender, Message message)
        {
            try
            {
                if (girls.Count == 0)
                {
                    await bot.SendTextMessageAsync(
                        message.Chat.Id,
                        lexer.GetPhrase("noGirls", cockyLevel, message.From));

                    SendSticker(message, "sad_fag");
                    return;
                }

                FileInfo file = girls[new Random().Next(girls.Count)];
                using (var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                {
                    await bot.SendPhotoAsync(
                        message.Chat.Id,
                        fileStream,
                        string.Format(lexer.GetPhrase("boobsDelivery", cockyLevel, message.From), "@" + message.From.Username));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        [TelegramCommand("/cockylevel")]
        [Description("Проверить уровень дерзости.")]
        public void GetCockyLevel(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                string.Format(lexer.GetPhrase("cockyLevel", cockyLevel, message.From), cockyLevel));
        }

        public override void Start(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                lexer.GetPhrase("start", cockyLevel, message.From));

            SendSticker(message, "absolutly");
        }

        public override void Test(object sender, Message message)
        {
            bot.SendTextMessageAsync(
                message.Chat.Id,
                lexer.GetPhrase("testing", cockyLevel, message.From));
        }

        public override void UnknownCommand(object sender, Message message)
        {
            cockyLevel = Math.Min(cockyLevel + 5, 100);
            bot.SendTextMessageAsync(
                message.Chat.Id,
                lexer.GetPhrase("unknown", cockyLevel, message.From));
        }

        #endregion Команды

        #region Основные функции

        /// <summary>
        /// Набор стикеров - (id, id в телеграме)
        /// </summary>
        public override Dictionary<string, string> GetStickers()
        {
            return new Dictionary<string, string> {
                { "hohot", "CAADAgADGAAD5NdGDj8TYTfHnZ7gAg" },
                { "ok", "CAADAgADBBQAAmOLRgxAXTz7KhSYOwI" },
                { "yobana", "CAADBAADXQEAAnscSQABXa2Sc1jHfB4C" },
                { "absolutly", "CAADAgADMAEAAuL27Qaa1y_1mTBI-gI" },
                { "sad_fag", "CAADBQADiwMAAukKyAOOn6GyPo2mcwI" },
            };
        }

        public CockyBath(string apiKey, string proxyUrl = "", int proxyPort = 0) : base(apiKey, proxyUrl, proxyPort)
        {
            girls = new DirectoryInfo("girls").GetFiles("*", SearchOption.AllDirectories).ToList();
            lexer = new CockyLexer();
            lexer.UpdatePhrases("phrases.json");

            InitWatchers();
            InitCockyCooldown();
            LoadBaths();
        }

        #endregion Основные функции
    }
}
