﻿[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UnitTest.Rollbar")]

namespace Rollbar
{
    using Rollbar.Diagnostics;
    using Rollbar.DTOs;
    using Rollbar.Telemetry;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements disposable implementation of IRollbar.
    /// 
    /// All the logging methods implemented in async "fire-and-forget" fashion.
    /// Hence, the payload is not yet delivered to the Rollbar API service when
    /// the methods return.
    /// 
    /// </summary>
    /// <seealso cref="Rollbar.IRollbar" />
    /// <seealso cref="System.IDisposable" />
    internal class RollbarLogger
        : IRollbar
        , IDisposable
    {
        private static readonly Task completedTask = // for more recent .NET implementations it would be: Task.CompletedTask;
            Task.Factory.StartNew(state => { }, "EnqueueAsyncShortcut");

        private readonly object _syncRoot = new object();

        private readonly IRollbarConfig _config;
        private readonly PayloadQueue _payloadQueue;
        private readonly ConcurrentQueue<Action> _pendingTasks = new ConcurrentQueue<Action>();
        private readonly ManualResetEvent _pendingTasksWaitEvent = new ManualResetEvent(false);
        private readonly int _pendingTasksWorkersCount = 2;

        /// <summary>
        /// Occurs when a Rollbar internal event happens.
        /// </summary>
        public event EventHandler<RollbarEventArgs> InternalEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="RollbarLogger"/> class.
        /// </summary>
        /// <param name="isSingleton">if set to <c>true</c> [is singleton].</param>
        internal RollbarLogger(bool isSingleton)
            : this(isSingleton, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RollbarLogger"/> class.
        /// </summary>
        /// <param name="isSingleton">if set to <c>true</c> [is singleton].</param>
        /// <param name="rollbarConfig">The rollbar configuration.</param>
        internal RollbarLogger(bool isSingleton, IRollbarConfig rollbarConfig)
        {
            if (!TelemetryCollector.Instance.IsAutocollecting)
            {
                TelemetryCollector.Instance.StartAutocollection();
            }

            this.IsSingleton = isSingleton;
            if (rollbarConfig != null)
            {
                this._config = rollbarConfig;
            }
            else
            {
                this._config = new RollbarConfig(this);
            }
            var rollbarClient = new RollbarClient(
                this._config
                , RollbarQueueController.Instance.ProvideHttpClient(this._config.ProxyAddress, this._config.ProxyUsername, this._config.ProxyPassword)
                );
            this._payloadQueue = new PayloadQueue(this, rollbarClient);
            RollbarQueueController.Instance.Register(this._payloadQueue);
            
            for (int i = 0; i < _pendingTasksWorkersCount; i++)
                new Task(ProcessPendingTasks).Start();
        }

        /// <summary>
        /// Gets a value indicating whether this instance is singleton.
        /// </summary>
        /// <value><c>true</c> if this instance is singleton; otherwise, <c>false</c>.</value>
        internal bool IsSingleton { get; private set; }

        /// <summary>
        /// Gets the queue.
        /// </summary>
        /// <value>The queue.</value>
        internal PayloadQueue Queue
        {
            get { return this._payloadQueue; }
        }

        #region IRollbar

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>The logger.</value>
        public IAsyncLogger Logger => this;

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>The configuration.</value>
        public IRollbarConfig Config
        {
            get { return this._config; }
        }

        /// <summary>
        /// Configures the using specified settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns>IRollbar.</returns>
        public IRollbar Configure(IRollbarConfig settings)
        {
            this._config.Reconfigure(settings);

            return this;
        }

        /// <summary>
        /// Configures using the specified access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>IRollbar.</returns>
        public IRollbar Configure(string accessToken)
        {
            return this.Configure(new RollbarConfig(accessToken));
        }

        #endregion IRollbar

        #region IAsyncLogger

        /// <summary>
        /// Returns blocking/synchronous implementation of this ILogger.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>ILogger.</returns>
        public ILogger AsBlockingLogger(TimeSpan timeout)
        {
            return new RollbarLoggerBlockingWrapper(this, timeout);
        }

        /// <summary>
        /// Logs using the specified level.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        public Task Log(ErrorLevel level, object obj)
        {
            return this.Log(level, obj, null);
        }

        /// <summary>
        /// Logs the specified object as using critical level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        public Task Critical(object obj)
        {
            return this.Critical(obj, null);
        }

        /// <summary>
        /// Logs the specified object as using error level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        public Task Error(object obj)
        {
            return this.Error(obj, null);
        }

        /// <summary>
        /// Logs the specified object as using warning level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        public Task Warning(object obj)
        {
            return this.Warning(obj, null);
        }

        /// <summary>
        /// Logs the specified object as using informational level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        public Task Info(object obj)
        {
            return this.Info(obj, null);
        }

        /// <summary>
        /// Logs the specified object as using debug level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        public Task Debug(object obj)
        {
            return this.Debug(obj, null);
        }

        /// <summary>
        /// Logs the specified Rollbar Data DTO.
        /// </summary>
        /// <param name="rollbarData">The Rollbar Data DTO.</param>
        /// <returns>Task.</returns>
        public Task Log(DTOs.Data rollbarData)
        {
            return this.EnqueueAsync(rollbarData, rollbarData.Level.HasValue ? rollbarData.Level.Value : ErrorLevel.Debug, null);
        }

        /// <summary>
        /// Logs using the specified level.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        public Task Log(ErrorLevel level, object obj, IDictionary<string, object> custom)
        {
            return this.EnqueueAsync(obj, level, custom);
        }


        /// <summary>
        /// Logs the specified object as using critical level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        public Task Critical(object obj, IDictionary<string, object> custom)
        {
            return this.EnqueueAsync(obj, ErrorLevel.Critical, custom);
        }

        /// <summary>
        /// Logs the specified object as using error level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        public Task Error(object obj, IDictionary<string, object> custom)
        {
            return this.EnqueueAsync(obj, ErrorLevel.Error, custom);
        }

        /// <summary>
        /// Logs the specified object as using warning level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        public Task Warning(object obj, IDictionary<string, object> custom)
        {
            return this.EnqueueAsync(obj, ErrorLevel.Warning, custom);
        }

        /// <summary>
        /// Logs the specified object as using informational level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        public Task Info(object obj, IDictionary<string, object> custom)
        {
            return this.EnqueueAsync(obj, ErrorLevel.Info, custom);
        }

        /// <summary>
        /// Logs the specified object as using debug level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        public Task Debug(object obj, IDictionary<string, object> custom)
        {
            return this.EnqueueAsync(obj, ErrorLevel.Debug, custom);
        }

        #endregion IAsyncLogger

        #region IRollbar explicitly

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>The configuration.</value>
        IRollbarConfig IRollbar.Config { get { return this.Config; } }

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>The logger.</value>
        IAsyncLogger IRollbar.Logger { get { return this; } }

        /// <summary>
        /// Configures the using specified settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns>IRollbar.</returns>
        IRollbar IRollbar.Configure(IRollbarConfig settings)
        {
            return this.Configure(settings);
        }

        /// <summary>
        /// Configures using the specified access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>IRollbar.</returns>
        IRollbar IRollbar.Configure(string accessToken)
        {
            return this.Configure(accessToken);
        }

        /// <summary>
        /// Occurs when a Rollbar internal event happens.
        /// </summary>
        event EventHandler<RollbarEventArgs> IRollbar.InternalEvent
        {
            add
            {
                this.InternalEvent += value;
            }

            remove
            {
                this.InternalEvent -= value;
            }
        }

        #endregion IRollbar explicitly

        #region IAsyncLogger explicitly

        /// <summary>
        /// Returns as the blocking logger.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>ILogger.</returns>
        ILogger IAsyncLogger.AsBlockingLogger(TimeSpan timeout)
        {
            return this.AsBlockingLogger(timeout);
        }

        /// <summary>
        /// Logs the specified Rollbar Data DTO.
        /// </summary>
        /// <param name="rollbarData">The Rollbar Data DTO.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Log(Data rollbarData)
        {
            return this.Log(rollbarData);
        }

        /// <summary>
        /// Logs using the specified level.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Log(ErrorLevel level, object obj)
        {
            return this.Log(level, obj);
        }

        /// <summary>
        /// Logs the specified object as using critical level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Critical(object obj)
        {
            return this.Critical(obj);
        }

        /// <summary>
        /// Logs the specified object as using error level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Error(object obj)
        {
            return this.Error(obj);
        }

        /// <summary>
        /// Logs the specified object as using warning level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Warning(object obj)
        {
            return this.Warning(obj);
        }

        /// <summary>
        /// Logs the specified object as using informational level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Info(object obj)
        {
            return this.Info(obj);
        }

        /// <summary>
        /// Logs the specified object as using debug level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Debug(object obj)
        {
            return this.Debug(obj);
        }

        /// <summary>
        /// Logs using the specified level.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Log(ErrorLevel level, object obj, IDictionary<string, object> custom)
        {
            return this.Log(level, obj, custom);
        }


        /// <summary>
        /// Logs the specified object as using critical level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Critical(object obj, IDictionary<string, object> custom)
        {
            return this.Critical(obj, custom);
        }

        /// <summary>
        /// Logs the specified object as using error level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Error(object obj, IDictionary<string, object> custom)
        {
            return this.Error(obj, custom);
        }

        /// <summary>
        /// Logs the specified object as using warning level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Warning(object obj, IDictionary<string, object> custom)
        {
            return this.Warning(obj, custom);
        }

        /// <summary>
        /// Logs the specified object as using informational level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Info(object obj, IDictionary<string, object> custom)
        {
            return this.Info(obj, custom);
        }

        /// <summary>
        /// Logs the specified object as using debug level.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="custom">The custom.</param>
        /// <returns>Task.</returns>
        Task IAsyncLogger.Debug(object obj, IDictionary<string, object> custom)
        {
            return this.Debug(obj, custom);
        }

        #endregion IAsyncLogger explicitly 

        #region IDisposable explicitly

        void IDisposable.Dispose()
        {
            this.Dispose();
        }

        #endregion IDisposable explicitly

        internal Task EnqueueAsync(
            object dataObject,
            ErrorLevel level,
            IDictionary<string, object> custom,
            TimeSpan? timeout = null,
            SemaphoreSlim signal = null
            )
        {
            DateTime utcTimestamp = DateTime.UtcNow;

            if (this.Config.LogLevel.HasValue && level < this.Config.LogLevel.Value)
            {
                // nice shortcut:
                return completedTask;
            }

            DateTime? timeoutAt = null;
            if (timeout.HasValue)
            {
                timeoutAt = DateTime.Now.Add(timeout.Value);
            }

            // we are taking here a fire-and-forget approach:
            Action task = () => Enqueue(utcTimestamp, dataObject, level, custom, timeoutAt, signal);
            lock (this._pendingTasks)
            {
                if (this.Config.ReportingQueueDepth == this._pendingTasks.Count)
                {
                    this._pendingTasks.TryDequeue(out var _);
                }
                this._pendingTasks.Enqueue(task);
                this._pendingTasksWaitEvent.Set();
            }
            return Task.FromResult(true);
        }

        private void ProcessPendingTasks()
        {
            while (!disposedValue)
            {
                this._pendingTasksWaitEvent.WaitOne();
                if (this._pendingTasks.TryDequeue(out var task))
                {
                    try
                    {
                        task();
                    }
                    catch (System.Exception ex)
                    {
                        OnRollbarEvent(new InternalErrorEventArgs(this, null, ex, "While performing Enqueue(...)..."));
                        System.Diagnostics.Trace.TraceError(ex.ToString());
                    }
                }
                else
                {
                    this._pendingTasksWaitEvent.Reset();
                }
            }
        }

        private void Enqueue(
            DateTime utcTimestamp,
            object dataObject,
            ErrorLevel level,
            IDictionary<string, object> custom,
            DateTime? timeoutAt = null,
            SemaphoreSlim signal = null
            )
        {
            lock (this._syncRoot)
            {
                var data = RollbarUtility.PackageAsPayloadData(utcTimestamp, this.Config, level, dataObject, custom);
                var payload = new Payload(this._config.AccessToken, data, timeoutAt, signal);
                DoSend(payload);
            }
        }

        private void DoSend(Payload payload)
        {
            //lock (this._syncRoot)
            {
                // here is the last chance to decide if we need to actually send this payload
                // based on the current config settings:
                if (string.IsNullOrWhiteSpace(this._config.AccessToken)
                    || this._config.Enabled == false
                    || (this._config.LogLevel.HasValue && payload.Data.Level < this._config.LogLevel.Value)
                    )
                {
                    return;
                }

                if (TelemetryCollector.Instance.Config.TelemetryEnabled)
                {
                    payload.Data.Body.Telemetry =
                        TelemetryCollector.Instance.GetQueueContent();
                }

                if (this._config.Server != null)
                {
                    payload.Data.Server = this._config.Server;
                }

                try
                {
                    if (this._config.CheckIgnore != null
                        && this._config.CheckIgnore.Invoke(payload)
                        )
                    {
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    OnRollbarEvent(new InternalErrorEventArgs(this, payload, ex, "While  check-ignoring a payload..."));
                }

                try
                {
                    this._config.Transform?.Invoke(payload);
                }
                catch (System.Exception ex)
                {
                    OnRollbarEvent(new InternalErrorEventArgs(this, payload, ex, "While  transforming a payload..."));
                }

                try
                {
                    this._config.Truncate?.Invoke(payload);
                }
                catch (System.Exception ex)
                {
                    OnRollbarEvent(new InternalErrorEventArgs(this, payload, ex, "While  truncating a payload..."));
                }

                this._payloadQueue.Enqueue(payload);

                return;
            }
        }

        internal virtual void OnRollbarEvent(RollbarEventArgs e)
        {
            EventHandler<RollbarEventArgs> handler = InternalEvent;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    this._payloadQueue.Release();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RollbarLogger() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <remarks>
        /// This code added to correctly implement the disposable pattern.
        /// </remarks>
        public void Dispose()
        {
            // RollbarLogger type supports both paradigms: singleton-like (via RollbarLocator) and
            // multiple disposable instances (via RollbarFactory).
            // Here we want to make sure that the singleton instance is never disposed:
            Assumption.AssertTrue(!this.IsSingleton, nameof(this.IsSingleton));

            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support

    }
}
