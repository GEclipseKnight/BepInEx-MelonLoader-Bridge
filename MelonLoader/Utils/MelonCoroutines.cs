using System;
using System.Collections;

namespace MelonLoader
{
    public class MelonCoroutines
    {
        public static object Start(IEnumerator routine)
        {
            if (SupportModule.Interface == null)
                throw new NotSupportedException("Support module must be initialized before starting coroutines");
            return SupportModule.Interface.StartCoroutine(routine);
        }

        public static void Stop(object coroutineToken)
        {
            if (SupportModule.Interface == null)
                throw new NotSupportedException("Support module must be initialized before starting coroutines");
            SupportModule.Interface.StopCoroutine(coroutineToken);
        }
    }
}
