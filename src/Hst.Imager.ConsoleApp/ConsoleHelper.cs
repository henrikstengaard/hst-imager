namespace Hst.Imager.ConsoleApp;

using System;

public static class ConsoleHelper
{
    private static int GetConsoleWindowWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            return 80;
        }
    }

    public static readonly int ConsoleWindowWidth = GetConsoleWindowWidth();
}