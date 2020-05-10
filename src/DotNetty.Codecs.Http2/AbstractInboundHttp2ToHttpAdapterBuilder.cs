// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A skeletal builder implementation of <see cref="InboundHttp2ToHttpAdapter"/> and its subtypes.
    /// </summary>
    public abstract class AbstractInboundHttp2ToHttpAdapterBuilder<TAdapter, TBuilder>
        where TAdapter : InboundHttp2ToHttpAdapter
        where TBuilder : AbstractInboundHttp2ToHttpAdapterBuilder<TAdapter, TBuilder>
    {
        private readonly IHttp2Connection connection;
        private int maxContentLength;
        private bool validateHttpHeaders;
        private bool propagateSettings;

        /// <summary>
        /// Creates a new <see cref="InboundHttp2ToHttpAdapter"/> builder for the specified <see cref="IHttp2Connection"/>.
        /// </summary>
        /// <param name="connection">the object which will provide connection notification events
        /// for the current connection.</param>
        protected AbstractInboundHttp2ToHttpAdapterBuilder(IHttp2Connection connection)
        {
            if (connection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }
            this.connection = connection;
        }

        [MethodImpl(InlineMethod.Value)]
        protected TBuilder Self() => (TBuilder)this;

        /// <summary>
        /// Gets the <see cref="IHttp2Connection"/>.
        /// </summary>
        public IHttp2Connection Connection => this.connection;

        /// <summary>
        /// Gets or sets the maximum length of the message content.
        /// <para>
        /// If the length of the message content, exceeds this value, a <see cref="TooLongFrameException"/> will be raised
        /// </para>
        /// </summary>
        public int MaxContentLength
        {
            get => this.maxContentLength;
            set => this.maxContentLength = value;
        }

        /// <summary>
        /// Specifies whether validation of HTTP headers should be performed.
        /// </summary>
        public bool IsValidateHttpHeaders
        {
            get => this.validateHttpHeaders;
            set => this.validateHttpHeaders = value;
        }

        /// <summary>
        /// Specifies whether a read settings frame should be propagated along the channel pipeline.
        /// </summary>
        /// <remarks>if <c>true</c> read settings will be passed along the pipeline. This can be useful
        /// to clients that need hold off sending data until they have received the settings.</remarks>
        public bool IsPropagateSettings
        {
            get => this.propagateSettings;
            set => this.propagateSettings = value;
        }

        /// <summary>
        /// Builds/creates a new <see cref="InboundHttp2ToHttpAdapter"/> instance using this builder's current settings.
        /// </summary>
        /// <returns></returns>
        public virtual TAdapter Build()
        {
            TAdapter instance = null;
            try
            {
                instance = Build(this.connection, this.maxContentLength,
                    this.validateHttpHeaders, this.propagateSettings);
            }
            catch (Exception t)
            {
                ThrowHelper.ThrowInvalidOperationException_FailedToCreateInboundHttp2ToHttpAdapter(t);
            }
            this.connection.AddListener(instance);
            return instance;
        }

        /// <summary>
        /// Creates a new <see cref="InboundHttp2ToHttpAdapter"/> with the specified properties.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="maxContentLength"></param>
        /// <param name="validateHttpHeaders"></param>
        /// <param name="propagateSettings"></param>
        /// <returns></returns>
        protected abstract TAdapter Build(IHttp2Connection connection, int maxContentLength,
            bool validateHttpHeaders, bool propagateSettings);
    }
}
