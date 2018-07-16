using System;
using System.IO;

using CockyBathBot;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TelegramBot
{
    public static class Program
    {
        private static JObject settings;

        private static JObject LoadSettings(string fileName)
        {
            FileStream fs = null;
            StreamReader sr = null;

            try
            {
                fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                sr = new StreamReader(fs);
                return JObject.Parse(sr.ReadToEnd());
            }
            catch (Exception ex)
            {
                return null;
            }
            finally
            {
                sr?.Close();
                fs?.Close();
            }

        }

        public static void Main(string[] args)
        {
            try
            {
                settings = LoadSettings("botsettings.json");
                CockyBath bot = new CockyBath(settings["key"].ToString(), Convert.ToInt32(settings["cockyLevel"].ToString()), 
                    settings["proxy"].ToString(), Convert.ToInt32(settings["port"].ToString()));

                Console.Title = bot.Name;

                bot.StartReceiving();
                Console.WriteLine($"Start listening for @{bot.Name}");
                Console.ReadLine();
                bot.StopReceiving();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}