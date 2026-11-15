using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MelonAuthorColorAttribute : Attribute
    {
        public ConsoleColor Color { get; internal set; }

        public MelonAuthorColorAttribute() { Color = MelonLogger.DefaultTextColor; }
        public MelonAuthorColorAttribute(ConsoleColor color) { Color = ((color == ConsoleColor.Black) ? MelonLogger.DefaultMelonColor : color); }
    }
}
