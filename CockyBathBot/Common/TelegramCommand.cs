using System;

namespace TelegramBot
{
    public class TelegramCommand: Attribute
    {
        public string Command;

        public TelegramCommand(string command)
        {
            Command = command;
        }
    }
}
