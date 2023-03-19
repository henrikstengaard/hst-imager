namespace Hst.Imager.ConsoleApp
{
    using OperatingSystem = Hst.Core.OperatingSystem;

    public static class User
    {
        private static bool? isAdministrator;

        public static bool IsAdministrator
        {
            get
            {
                if (isAdministrator.HasValue)
                {
                    return isAdministrator.Value;
                }
                
                isAdministrator = OperatingSystem.IsAdministrator();

                return isAdministrator.Value;
            }
        }
    }
}