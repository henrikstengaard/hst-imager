namespace Hst.Imager.ConsoleApp
{
    using OperatingSystem = Core.OperatingSystem;

    public static class User
    {
        public static readonly bool IsAdministrator = OperatingSystem.IsAdministrator();
    }
}