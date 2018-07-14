using System;

namespace CockyBathBot
{
    public class Bath
    {
        public DateTime from;
        public DateTime to;

        public TimeSpan HowMuch()
        {
            return from - DateTime.Now;
        }
    }
}
