using System;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MelonIDAttribute : Attribute
    {
        public string ID { get; internal set; }

        public MelonIDAttribute(string id)
            => ID = id;
        public MelonIDAttribute(int id)
            => ID = id.ToString();
    }
}
