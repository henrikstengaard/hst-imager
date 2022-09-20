namespace Hst.Imager.ConsoleApp
{
    using OperatingSystem = Hst.Core.OperatingSystem;

    public static class User
    {
        public static readonly bool IsAdministrator = OperatingSystem.IsAdministrator();
    }
}