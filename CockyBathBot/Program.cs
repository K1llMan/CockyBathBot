using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CockyBathBot;

using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    public static class Program
    {
        private static readonly TelegramBotClient Bot = new TelegramBotClient("659678780:AAESk5vXWl3nRV3na33Xlu03ewBzuMDp-tU");

        public static void Main(string[] args)
        {
            CockyBath bot = new CockyBath("659678780:AAESk5vXWl3nRV3na33Xlu03ewBzuMDp-tU", "192.169.140.74", 27371);

            Console.Title = bot.Name;

            bot.StartReceiving();
            Console.WriteLine($"Start listening for @{bot.Name}");
            Console.ReadLine();
            bot.StopReceiving();
        }
    }
}