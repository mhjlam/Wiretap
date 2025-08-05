namespace Wiretap.Models
{
    public class AppSettings
    {
        public WindowSettings Window { get; set; } = new();
        public ListenerSettings Listeners { get; set; } = new();
        public MessageSettings Messages { get; set; } = new();
    }

    public class WindowSettings
    {
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public int MinWidth { get; set; } = 1280;
        public int MinHeight { get; set; } = 720;
    }

    public class ListenerSettings
    {
        public bool AutoStartDefaultUdp { get; set; } = true;
        public int DefaultUdpPort { get; set; } = 8080;
        public int DefaultTcpPort { get; set; } = 9090;
        public bool StartListenersOnAdd { get; set; } = true;
        public bool ShowConnectionStatus { get; set; } = false;
    }

    public class MessageSettings
    {
        public int MaxMessages { get; set; } = 1000;
        public bool AutoScrollToTop { get; set; } = true;
        public int FlashDurationMs { get; set; } = 200;
        public int FadeDurationMs { get; set; } = 150;
    }
}