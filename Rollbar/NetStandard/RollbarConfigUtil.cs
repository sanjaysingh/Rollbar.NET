﻿namespace Rollbar.NetStandard
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Rollbar.Telemetry;

    /// <summary>
    /// Class RollbarConfigUtil.
    /// </summary>
    public static class RollbarConfigUtil
    {
        /// <summary>
        /// Loads the specified configuration.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns><c>true</c> if configuration was found, <c>false</c> otherwise.</returns>
        public static bool Load(RollbarConfig config)
        {
            // try app.config file:
            if (NetFramework.AppConfigUtil.LoadAppSettings(ref config))
            {
                return true;
            }

#if NETCOREAPP || NETSTANDARD
            // try appsettings.json file:
            if (NetCore.AppSettingsUtil.LoadAppSettings(ref config))
            {
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// Loads the specified configuration.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns><c>true</c> if configuration was found, <c>false</c> otherwise.</returns>
        public static bool Load(TelemetryConfig config)
        {
            // try app.config file:
            if (NetFramework.AppConfigUtil.LoadAppSettings(ref config))
            {
                return true;
            }

#if NETCOREAPP || NETSTANDARD
            // try appsettings.json file:
            if (NetCore.AppSettingsUtil.LoadAppSettings(ref config))
            {
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// Loads the rollbar configuration.
        /// </summary>
        /// <returns>Either IRollbarConfig or null if no configuration file found.</returns>
        public static IRollbarConfig LoadRollbarConfig()
        {
            RollbarConfig config = new RollbarConfig("seedToken");
            if(RollbarConfigUtil.Load(config))
            {
                return config;
            }
            return null;
        }

        /// <summary>
        /// Loads the telemetry configuration.
        /// </summary>
        /// <returns>ITelemetryConfig.</returns>
        /// <returns>Either IRollbarConfig or null if no configuration file found.</returns>
        public static ITelemetryConfig LoadTelemetryConfig()
        {
            TelemetryConfig config = new TelemetryConfig();
            if (RollbarConfigUtil.Load(config))
            {
                return config;
            }
            return null;
        }


#if NETCOREAPP || NETSTANDARD

        /// <summary>
        /// Loads the rollbar configuration.
        /// </summary>
        /// <param name="configFileName">Name of the configuration file.</param>
        /// <param name="configFilePath">The configuration file path.</param>
        /// <returns>Either IRollbarConfig or null if no configuration file found.</returns>
        public static IRollbarConfig LoadRollbarConfig(string configFileName, string configFilePath = null)
        {
            RollbarConfig config = new RollbarConfig("seedToken");

            if (string.IsNullOrWhiteSpace(configFilePath))
            {
                if (!NetCore.AppSettingsUtil.LoadAppSettings(ref config, configFileName))
                {
                    return null;
                }
            }
            else if (!NetCore.AppSettingsUtil.LoadAppSettings(ref config, configFilePath, configFileName))
            {
                return null;
            }

            return config;
        }

        /// <summary>
        /// Loads the telemetry configuration.
        /// </summary>
        /// <param name="configFileName">Name of the configuration file.</param>
        /// <param name="configFilePath">The configuration file path.</param>
        /// <returns>Either IRollbarConfig or null if no configuration file found.</returns>
        public static ITelemetryConfig LoadTelemetryConfig(string configFileName, string configFilePath = null)
        {
            TelemetryConfig config = new TelemetryConfig();

            if (string.IsNullOrWhiteSpace(configFilePath))
            {
                if (!NetCore.AppSettingsUtil.LoadAppSettings(ref config, configFileName))
                {
                    return null;
                }
            }
            else if (!NetCore.AppSettingsUtil.LoadAppSettings(ref config, configFilePath, configFileName))
            {
                return null;
            }

            return config;
        }

#endif

    }
}
