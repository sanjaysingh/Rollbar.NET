﻿namespace Rollbar
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Rollbar.Common;
    using Rollbar.Diagnostics;
    using Rollbar.DTOs;
    using Rollbar.NetFramework;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

#pragma warning disable CS1584 // XML comment has syntactically incorrect cref attribute
#pragma warning disable CS1658 // Warning is overriding an error
    /// <summary>
    /// Models Rollbar client/notifier configuration data.
    /// </summary>
    /// <seealso cref="Rollbar.Common.ReconfigurableBase{Rollbar.RollbarConfig}" />
    /// <seealso cref="Common.ReconfigurableBase{Rollbar.RollbarConfig}" />
    /// <seealso cref="Rollbar.ITraceable" />
    public class RollbarConfig
#pragma warning restore CS1658 // Warning is overriding an error
#pragma warning restore CS1584 // XML comment has syntactically incorrect cref attribute
        : ReconfigurableBase<RollbarConfig, IRollbarConfig>
        , ITraceable
        , IRollbarConfig
        , IEquatable<IRollbarConfig>
    {
        private readonly RollbarLogger _logger;

        private RollbarConfig()
        {
        }

        internal RollbarConfig(RollbarLogger logger)
        {
            this._logger = logger;

            this.SetDefaults();

            // initialize based on application configuration file (if any):
            NetStandard.RollbarConfigUtility.Load(this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RollbarConfig"/> class.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        public RollbarConfig(string accessToken)
        {
            Assumption.AssertNotNullOrWhiteSpace(accessToken, nameof(accessToken));

            this.SetDefaults();

            this.AccessToken = accessToken;

            // initialize based on application configuration file (if any):
            NetStandard.RollbarConfigUtility.Load(this);
        }

        private void SetDefaults()
        {
            // let's set some default values:
            this.Environment = "production";
            this.Enabled = true;
            this.MaxReportsPerMinute = 60;
            this.ReportingQueueDepth = 20;
            this.MaxItems = 0;
            this.CaptureUncaughtExceptions = true;
            this.LogLevel = ErrorLevel.Debug;
            this.ScrubFields = new []
            {
                "passwd",
                "password",
                "secret",
                "confirm_password",
                "password_confirmation",
            };
            this.ScrubWhitelistFields = new string[]
            {
            };
            this.EndPoint = "https://api.rollbar.com/api/1/";
            this.ProxyAddress = null;
            this.ProxyUsername = null;
            this.ProxyPassword = null;
            this.CheckIgnore = null;
            this.Transform = null;
            this.Truncate = null;
            this.Server = null;
            this.Person = null;

            this.PersonDataCollectionPolicies = PersonDataCollectionPolicies.None;
            this.IpAddressCollectionPolicy = IpAddressCollectionPolicy.Collect;
        }

        internal RollbarLogger Logger
        {
            get { return this._logger; }
        }

        /// <summary>
        /// Reconfigures this object similar to the specified one.
        /// </summary>
        /// <param name="likeMe">The pre-configured instance to be cloned in terms of its configuration/settings.</param>
        /// <returns>
        /// Reconfigured instance.
        /// </returns>
        public override RollbarConfig Reconfigure(IRollbarConfig likeMe)
        {
            base.Reconfigure(likeMe);

            var rollbarClient = new RollbarClient(
                this
                , RollbarQueueController.Instance.ProvideHttpClient(this.ProxyAddress, this.ProxyUsername, this.ProxyPassword)
                );

            if (this.Logger != null && this.Logger.Queue != null)
            {
                // reset the queue to use the new RollbarClient:
                this.Logger.Queue.Flush();
                this.Logger.Queue.UpdateClient(rollbarClient);
                this.Logger.Queue.NextDequeueTime = DateTimeOffset.Now;
            }

            return this;
        }

        /// <summary>
        /// Gets the access token.
        /// </summary>
        /// <value>
        /// The access token.
        /// </value>
        public string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the end point.
        /// </summary>
        /// <value>
        /// The end point.
        /// </value>
        public string EndPoint { get; set; }

        /// <summary>
        /// Gets or sets the scrub fields.
        /// </summary>
        /// <value>
        /// The scrub fields.
        /// </value>
        public string[] ScrubFields { get; set; }

        /// <summary>
        /// Gets the scrub white-list fields.
        /// </summary>
        /// <value>
        /// The scrub white-list fields.
        /// </value>
        /// <remarks>
        /// The fields mentioned in this list are guaranteed to be excluded
        /// from the ScrubFields list in cases when the lists overlap.
        /// </remarks>
        public string[] ScrubWhitelistFields { get; set; }

        /// <summary>
        /// Gets or sets the log level.
        /// </summary>
        /// <value>
        /// The log level.
        /// </value>
        public ErrorLevel? LogLevel { get; set; }

        /// <summary>
        /// Gets or sets the enabled.
        /// </summary>
        /// <value>
        /// The enabled.
        /// </value>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Gets or sets the environment.
        /// </summary>
        /// <value>
        /// The environment.
        /// </value>
        public string Environment { get; set; }

        /// <summary>
        /// Gets or sets the check ignore.
        /// </summary>
        /// <value>
        /// The check ignore.
        /// </value>
        public Func<Payload, bool> CheckIgnore { get; set; }

        /// <summary>
        /// Gets or sets the transform.
        /// </summary>
        /// <value>
        /// The transform.
        /// </value>
        public Action<Payload> Transform { get; set; }

        /// <summary>
        /// Gets or sets the truncate.
        /// </summary>
        /// <value>
        /// The truncate.
        /// </value>
        public Action<Payload> Truncate { get; set; }

        /// <summary>
        /// Gets or sets the server.
        /// </summary>
        /// <value>
        /// The server.
        /// </value>
        public Server Server { get; set; }

        /// <summary>
        /// Gets or sets the person.
        /// </summary>
        /// <value>
        /// The person.
        /// </value>
        public Person Person { get;set; }

        /// <summary>
        /// Gets or sets the proxy address.
        /// </summary>
        /// <value>
        /// The proxy address.
        /// </value>
        public string ProxyAddress { get; set; }

        /// <summary>
        /// Gets the proxy username.
        /// </summary>
        /// <value>The proxy username.</value>
        public string ProxyUsername { get; set; }

        /// <summary>
        /// Gets the proxy password.
        /// </summary>
        /// <value>The proxy password.</value>
        public string ProxyPassword { get; set; }

        /// <summary>
        /// Gets or sets the maximum reports per minute.
        /// </summary>
        /// <value>
        /// The maximum reports per minute.
        /// </value>
        public int MaxReportsPerMinute { get; set; }

        /// <summary>
        /// Gets or sets the reporting queue depth.
        /// </summary>
        /// <value>
        /// The reporting queue depth.
        /// </value>
        public int ReportingQueueDepth { get; set; }

        /// <summary>
        /// Gets or sets the maximum items limit.
        /// </summary>
        /// <value>
        /// The maximum items.
        /// </value>
        /// <remarks>
        /// Max number of items to report per page load or per web request.
        /// When this limit is reached, an additional item will be reported stating that the limit was reached.
        /// Like MaxReportsPerMinute, this limit counts uncaught errors and any direct calls to Rollbar.log/debug/info/warning/error/critical().
        /// Default: 0 (no limit)
        /// </remarks>
        public int MaxItems { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to auto-capture uncaught exceptions.
        /// </summary>
        /// <value>
        ///   <c>true</c> if auto-capture uncaught exceptions is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool CaptureUncaughtExceptions { get; set; }

        /// <summary>
        /// Gets or sets the person data collection policies.
        /// </summary>
        /// <value>
        /// The person data collection policies.
        /// </value>
        [JsonConverter(typeof(StringEnumConverter))]
        public PersonDataCollectionPolicies PersonDataCollectionPolicies { get; set; }

        /// <summary>
        /// Gets or sets the IP address collection policy.
        /// </summary>
        /// <value>
        /// The IP address collection policy.
        /// </value>
        [JsonConverter(typeof(StringEnumConverter))]
        public IpAddressCollectionPolicy IpAddressCollectionPolicy { get; set; }

        /// <summary>
        /// Traces as string.
        /// </summary>
        /// <returns>System.String.</returns>
        public string TraceAsString()
        {
            return this.TraceAsString(string.Empty);
        }

        /// <summary>
        /// Traces as a string.
        /// </summary>
        /// <param name="indent">The indent.</param>
        /// <returns>
        /// String rendering of this instance.
        /// </returns>
        public string TraceAsString(string indent)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(indent + this.GetType().Name + ":");
            sb.AppendLine(indent + "  AccessToken: " + this.AccessToken);
            sb.AppendLine(indent + "  EndPoint: " + this.EndPoint);
            sb.AppendLine(indent + "  ScrubFields: " + this.ScrubFields);
            sb.AppendLine(indent + "  ScrubWhitelistFields: " + this.ScrubWhitelistFields);
            sb.AppendLine(indent + "  Enabled: " + this.Enabled);
            sb.AppendLine(indent + "  Environment: " + this.Environment);
            sb.AppendLine(indent + "  Server: " + this.Server);
            sb.AppendLine(indent + "  Person: " + this.Person);
            sb.AppendLine(indent + "  ProxyAddress: " + this.ProxyAddress);
            sb.AppendLine(indent + "  MaxReportsPerMinute: " + this.MaxReportsPerMinute);
            sb.AppendLine(indent + "  ReportingQueueDepth: " + this.ReportingQueueDepth);
            sb.AppendLine(indent + "  MaxItems: " + this.MaxItems);
            sb.AppendLine(indent + "  CaptureUncaughtExceptions: " + this.CaptureUncaughtExceptions);
            sb.AppendLine(indent + "  IpAddressCollectionPolicy: " + this.IpAddressCollectionPolicy);
            sb.AppendLine(indent + "  PersonDataCollectionPolicies: " + this.PersonDataCollectionPolicies);
            return sb.ToString();
        }

        /// <summary>
        /// Gets the fields to scrub.
        /// </summary>
        /// <returns>
        /// Actual fields to be scrubbed based on combining the ScrubFields with the ScrubWhitelistFields.
        /// Basically this.ScrubFields "minus" this.ScrubWhitelistFields.
        /// </returns>
        public virtual IReadOnlyCollection<string> GetFieldsToScrub()
        {
            if (this.ScrubFields == null || this.ScrubFields.Length == 0)
            {
                return new string[0];
            }

            if (this.ScrubWhitelistFields == null || this.ScrubWhitelistFields.Length == 0)
            {
                return this.ScrubFields.ToArray();
            }

            var whitelist = this.ScrubWhitelistFields.ToArray();
            return this.ScrubFields.Where(i => !whitelist.Contains(i)).ToArray();
        }

        /// <summary>
        /// Reconfigures this object similar to the specified one.
        /// </summary>
        /// <param name="likeMe">The pre-configured instance to be cloned in terms of its configuration/settings.</param>
        /// <returns>Reconfigured instance.</returns>
        IRollbarConfig IReconfigurable<IRollbarConfig, IRollbarConfig>.Reconfigure(IRollbarConfig likeMe)
        {
            return this.Reconfigure(likeMe);
        }
    }
}
