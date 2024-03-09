using System;

namespace DiscordOverlay
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            DiscordOverlay dxHost = new DiscordOverlay();
            dxHost.Run().Wait();
        }
    }
}
