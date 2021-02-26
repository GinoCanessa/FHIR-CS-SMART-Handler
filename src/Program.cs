using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace smart_handler
{
    class Program
    {
        private const string _uriPrefix = "smartHandler";

        /// <summary>
        /// C# Application to perform FHIR SMART App Launch with a custom URI scheme
        /// </summary>
        /// <param name="configureUriScheme">Flag to ask the application to configure the URI scheme</param>
        /// <param name="launchUrl">URL launched for SMART redirect</param>
        /// <returns></returns>
        public static int Main(
            bool configureUriScheme = false,
            string launchUrl = "")
        {
            if (configureUriScheme)
            {
                if (ConfigureUriScheme())
                {
                    System.Console.WriteLine("Successfully registered URI scheme");
                }
                else
                {
                    System.Console.WriteLine("Failed to configure the URI scheme");
                    return -1;
                }

                return 0;
            }

            if (string.IsNullOrEmpty(launchUrl))
            {
                System.Console.WriteLine($"No launch url present");
            }
            else
            {
                System.Console.WriteLine($"Launched with url: {launchUrl}");
            }


            System.Console.WriteLine("Done, press enter to exit.");
            System.Console.ReadLine();
            return 0;
        }

        /// <summary>
        /// Configure a URI scheme for any supported platform
        /// </summary>
        /// <returns>true for success</returns>
        private static bool ConfigureUriScheme()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ConfigureUriSchemeWindows();
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Configure a URI scheme for Windows, using the registry
        /// </summary>
        /// <returns>true for success</returns>
        private static bool ConfigureUriSchemeWindows()
        {
            string assemblyLocation = Assembly.GetEntryAssembly().Location;

            if (Path.GetExtension(assemblyLocation).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyLocation = Path.ChangeExtension(assemblyLocation, ".exe");

                if (!File.Exists(assemblyLocation))
                {
                    System.Console.WriteLine("Could not find executable, please package as an exe!");
                    return false;
                }
            }

            try
            {
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(_uriPrefix))
                {
                    key.SetValue(string.Empty, "URL:SMART Handler Redirect");
                    key.SetValue("URL Protocol", string.Empty);

                    using (RegistryKey shellKey = key.CreateSubKey("shell"))
                    using (RegistryKey openKey = shellKey.CreateSubKey("open"))
                    using (RegistryKey commandKey = openKey.CreateSubKey("command"))
                    {
                        commandKey.SetValue(string.Empty, $"\"{assemblyLocation}\" --launch-url \"%1\"");
                    }
                }

                return true;
            }
            catch (UnauthorizedAccessException authEx)
            {
                System.Console.WriteLine($"Failed to register the URI scheme: {authEx.Message}");
            }

            return false;
        }
    }
}
