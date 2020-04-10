using System;

namespace DirectXHost
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            DirectXHost dxHost = new DirectXHost();
            dxHost.Run();
        }
    }
}
