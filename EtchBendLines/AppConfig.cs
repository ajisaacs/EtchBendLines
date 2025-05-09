using System;
using System.Configuration;

namespace EtchBendLines
{
    public static class AppConfig
    {
        public static double GetDouble(string key)
        {
            if (!double.TryParse(ConfigurationManager.AppSettings[key], out var value))
                throw new Exception($"Failed to convert AppSetting[\"{key}\"] to double.");

            return value;
        }
    }
}
