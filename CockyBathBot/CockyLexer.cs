using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json.Linq;

using Telegram.Bot.Types;

namespace CockyBathBot
{
    public class CockyLexer
    {
        #region Поля

        private Dictionary<string, Dictionary<string, Dictionary<int, string[]>>> phrases;
        private FileSystemWatcher watcher;

        #endregion Поля

        #region Вспомогательные функции

        private void WatcherHandler(object sender, FileSystemEventArgs e)
        {
            UpdatePhrases(e.FullPath);
        }

        private void InitWatchers(string fileName)
        {
            if (watcher != null)
                return;

            watcher = new FileSystemWatcher
            {
                Path = AppDomain.CurrentDomain.BaseDirectory,
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite,
                Filter = fileName,
                IncludeSubdirectories = true
            };

            watcher.Changed += WatcherHandler;

            // Включение слежения за директорией
            watcher.EnableRaisingEvents = true;
        }

        #endregion Вспомогательные функции

        #region Основные функции

        public string GetPhrase(string type, int cockyLevel, User user)
        {
            try
            {
                if (phrases.Count == 0 || !phrases.ContainsKey(type))
                    return string.Empty;

                var group = phrases[type];
                string[] strings = group
                    .Where(p => p.Key == "common" || user != null && (p.Key == user.Username || p.Key == Convert.ToString(user.Id)))
                    .SelectMany(p => {
                        KeyValuePair<int, string[]> strs = p.Value.FirstOrDefault(c => c.Key <= cockyLevel);
                        return strs.Value ?? new string[] { };
                    })
                    .ToArray();

                if (strings.Length == 0)
                    return string.Empty;

                return strings[new Random().Next(strings.Length)];
            }
            catch (Exception ex)
            {
                return "Вот это ошибка о_О";
            }
        }

        public void UpdatePhrases(string fileName)
        {
            InitWatchers(fileName);

            FileStream fs = null;
            StreamReader sr = null;

            try
            {
                fs = new FileStream(fileName, FileMode.Open);
                sr = new StreamReader(fs);
                JObject strings = JObject.Parse(sr.ReadToEnd());

                var fullPhrase = new Dictionary<string, Dictionary<string, Dictionary<int, string[]>>>();
                foreach (JProperty strGroup in strings.Properties())
                {
                    Dictionary<string, Dictionary<int, string[]>> group = new Dictionary<string, Dictionary<int, string[]>>();
                    foreach (JProperty strUser in ((JObject)strGroup.Value).Properties())
                    {
                        Dictionary<int, string[]> cockyLevels = new Dictionary<int, string[]>();
                        foreach (JProperty strLevel in ((JObject)strUser.Value).Properties())
                            cockyLevels.Add(Convert.ToInt32(strLevel.Name), strLevel.Value.Children().Select(v => v.ToString()).ToArray());

                        group.Add(strUser.Name, cockyLevels);
                    }

                    fullPhrase.Add(strGroup.Name, group);
                }

                phrases = fullPhrase;
            }
            catch(Exception ex)
            { 
            }
            finally
            {
                sr?.Close();
                fs?.Close();
            }
        }

        public CockyLexer()
        {
            phrases = new Dictionary<string, Dictionary<string, Dictionary<int, string[]>>>();
        }

        #endregion Основные функции
    }
}
