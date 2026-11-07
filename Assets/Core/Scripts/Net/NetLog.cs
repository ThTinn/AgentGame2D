using System;

namespace Game.Net
{
    /// <summary>Log dùng chung cho client và server. Unity gắn sink vào Debug.Log trong NetworkRunner.</summary>
    public static class NetLog
    {
        public static Action<string> InfoSink = Console.WriteLine;
        public static Action<string> WarnSink = s => Console.WriteLine("[WARN] " + s);
        public static Action<string> ErrorSink = s => Console.Error.WriteLine("[ERROR] " + s);

        public static void Info(string text) => InfoSink?.Invoke(text);
        public static void Warn(string text) => WarnSink?.Invoke(text);
        public static void Error(string text) => ErrorSink?.Invoke(text);
    }
}
