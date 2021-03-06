﻿#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace UnitTest.Rollbar
{
    using global::Rollbar;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Defines test class RollbarLiveFixtureBase.
    /// Implements the <see cref="System.IDisposable" />
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    /// <remarks>
    /// This is a base abstraction for creating live Rollbar unit tests 
    /// (ones that actually expected to communicate with the Rollbar API).
    /// It allows to set expectations for internal Rollbar events 
    /// (as payload delivery to Rollbar API or any communication or internal errors).
    /// It has built-in verification of actual event counts against the expected ones per type of the events.
    /// </remarks>
    [TestClass]
    [TestCategory(nameof(RollbarLiveFixtureBase))]
    public abstract class RollbarLiveFixtureBase
        : IDisposable
    {
        private RollbarConfig _loggerConfig;

        private readonly List<IRollbar> _disposableRollbarInstances = new List<IRollbar>();

        protected RollbarLiveFixtureBase()
        {
            RollbarQueueController.Instance.InternalEvent += OnRollbarInternalEvent;

            this._loggerConfig =
                new RollbarConfig(RollbarUnitTestSettings.AccessToken) { Environment = RollbarUnitTestSettings.Environment, };
        }

        /// <summary>
        /// Sets the fixture up.
        /// </summary>
        //[TestInitialize]
        public virtual void SetupFixture()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            this.ResetAllExpectedTotals();
            this.ClearAllRollbarInternalEvents();
        }

        /// <summary>
        /// Tears down this fixture.
        /// </summary>
        //[TestCleanup]
        public virtual void TearDownFixture()
        {
            TimeSpan timeout = RollbarQueueController.Instance.GetRecommendedTimeout();
            Thread.Sleep(timeout);
            this.VerifyActualEventsAgainstExpectedTotals();
        }

        private void OnRollbarInternalEvent(object sender, RollbarEventArgs e)
        {
            Console.WriteLine(e.TraceAsString());

            RollbarApiErrorEventArgs apiErrorEvent = e as RollbarApiErrorEventArgs;
            if (apiErrorEvent != null)
            {
                this.ApiErrorEvents.Add(apiErrorEvent);
                return;
            }
            CommunicationEventArgs commEvent = e as CommunicationEventArgs;
            if (commEvent != null)
            {
                this.CommunicationEvents.Add(commEvent);
                return;
            }
            CommunicationErrorEventArgs commErrorEvent = e as CommunicationErrorEventArgs;
            if (commErrorEvent != null)
            {
                this.CommunicationErrorEvents.Add(commErrorEvent);
                return;
            }
            InternalErrorEventArgs internalErrorEvent = e as InternalErrorEventArgs;
            if (internalErrorEvent != null)
            {
                this.InternalSdkErrorEvents.Add(internalErrorEvent);
                return;
            }
        }

        /// <summary>
        /// Gets or sets the expected communication events total.
        /// </summary>
        /// <value>The expected communication events total.</value>
        protected int ExpectedCommunicationEventsTotal { get; set; }

        /// <summary>
        /// Gets or sets the expected communication errors total.
        /// </summary>
        /// <value>The expected communication errors total.</value>
        protected int ExpectedCommunicationErrorsTotal { get; set; }

        /// <summary>
        /// Gets or sets the expected API errors total.
        /// </summary>
        /// <value>The expected API errors total.</value>
        protected int ExpectedApiErrorsTotal { get; set; }

        /// <summary>
        /// Gets or sets the expected internal SDK errors total.
        /// </summary>
        /// <value>The expected internal SDK errors total.</value>
        protected int ExpectedInternalSdkErrorsTotal { get; set; }

        /// <summary>
        /// Resets all expected totals.
        /// </summary>
        protected void ResetAllExpectedTotals()
        {
            this.ExpectedApiErrorsTotal = 0;
            this.ExpectedCommunicationErrorsTotal = 0;
            this.ExpectedCommunicationEventsTotal = 0;
            this.ExpectedInternalSdkErrorsTotal = 0;
        }

        private readonly List<CommunicationEventArgs> CommunicationEvents = new List<CommunicationEventArgs>();
        private readonly List<CommunicationErrorEventArgs> CommunicationErrorEvents = new List<CommunicationErrorEventArgs>();
        private readonly List<RollbarApiErrorEventArgs> ApiErrorEvents = new List<RollbarApiErrorEventArgs>();
        private readonly List<InternalErrorEventArgs> InternalSdkErrorEvents = new List<InternalErrorEventArgs>();

        /// <summary>
        /// Clears all rollbar internal events.
        /// </summary>
        protected void ClearAllRollbarInternalEvents()
        {
            this.CommunicationEvents.Clear();
            this.CommunicationErrorEvents.Clear();
            this.ApiErrorEvents.Clear();
            this.InternalSdkErrorEvents.Clear();
        }

        private void VerifyActualEventsAgainstExpectedTotals()
        {
            Assert.AreEqual(this.ExpectedCommunicationEventsTotal, this.CommunicationEvents.Count, "Actual CommunicationEvents count does not match expectation.");
            Assert.IsTrue(
                // EITHER no errors expected:
                (this.ExpectedCommunicationErrorsTotal == 0 && this.CommunicationErrorEvents.Count == 0)
                // OR at least as many errors as expected (but most likely multiples of that):
                || (this.ExpectedCommunicationErrorsTotal > 0 && this.CommunicationErrorEvents.Count >= this.ExpectedCommunicationErrorsTotal) 
                , "Actual CommunicationErrors count does not match expectation."
                );
            Assert.AreEqual(this.ExpectedApiErrorsTotal, this.ApiErrorEvents.Count, "Actual ApiErrors count does not match expectation.");
            Assert.AreEqual(this.ExpectedInternalSdkErrorsTotal, this.InternalSdkErrorEvents.Count, "Actual InternalSdkErrors count does not match expectation.");
        }

        /// <summary>
        /// Provides the live rollbar configuration.
        /// </summary>
        /// <returns>IRollbarConfig.</returns>
        protected IRollbarConfig ProvideLiveRollbarConfig()
        {
            return this.ProvideLiveRollbarConfig(RollbarUnitTestSettings.AccessToken, RollbarUnitTestSettings.Environment);
        }

        /// <summary>
        /// Provides the live rollbar configuration.
        /// </summary>
        /// <param name="rollbarAccessToken">The rollbar access token.</param>
        /// <param name="rollbarEnvironment">The rollbar environment.</param>
        /// <returns>IRollbarConfig.</returns>
        protected IRollbarConfig ProvideLiveRollbarConfig(string rollbarAccessToken, string rollbarEnvironment)
        {
            if (this._loggerConfig == null)
            {
                this._loggerConfig =
                    new RollbarConfig(rollbarAccessToken) { Environment = rollbarEnvironment, };
            }
            return this._loggerConfig;
        }

        /// <summary>
        /// Provides the disposable rollbar.
        /// </summary>
        /// <returns>IRollbar.</returns>
        protected IRollbar ProvideDisposableRollbar()
        {
            IRollbar rollbar = RollbarFactory.CreateNew(isSingleton: false, rollbarConfig: this.ProvideLiveRollbarConfig());
            this._disposableRollbarInstances.Add(rollbar);
            return rollbar;
        }

        /// <summary>
        /// Provides the shared rollbar.
        /// </summary>
        /// <returns>IRollbar.</returns>
        protected IRollbar ProvideSharedRollbar()
        {
            if (!RollbarLocator.RollbarInstance.Equals(ProvideLiveRollbarConfig()))
            {
                RollbarLocator.RollbarInstance.Configure(ProvideLiveRollbarConfig());
            }
            return RollbarLocator.RollbarInstance;
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
                    TimeSpan timeout = RollbarQueueController.Instance.GetRecommendedTimeout();
                    Thread.Sleep(timeout);
                    RollbarQueueController.Instance.InternalEvent -= OnRollbarInternalEvent;
                    foreach (var rollbar in this._disposableRollbarInstances)
                    {
                        rollbar.Dispose();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RollbarLiveFixtureBase() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}
