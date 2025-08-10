namespace V10Sharp.ExtConsole;

public static class ConsoleHelpers
{
    /// <summary>Waits the any key down with sleep.</summary>
    /// <param name="msTimeout">The timeout in milliseconds.</param>
    /// <param name="msSleep">The sleep time between key check in milliseconds.</param>
    /// <param name="worker">Callback for waiting.</param>
    public static void WaitKeySleep(int msTimeout = -1, int msSleep = 5, Func<bool>? worker = null)
    {
        var timeout = DateTime.Now.AddMilliseconds(msTimeout);
        while (msTimeout < 0 || DateTime.Now < timeout)
        {
            if (Console.KeyAvailable)
                break;
            Thread.Sleep(msSleep);
            if (worker != null)
                worker();
        }
    }
}
