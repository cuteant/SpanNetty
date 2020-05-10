// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Exception thrown when an HTTP/2 error was encountered.
    /// </summary>
    public class Http2Exception : Exception
    {
        public Http2Exception(Http2Error error)
            : this(error, ShutdownHint.HardShutdown)
        {
        }

        public Http2Exception(Http2Error error, ShutdownHint shutdownHint)
        {
            this.Error = error;
            this.ShutdownHint = shutdownHint;
        }

        public Http2Exception(Http2Error error, string message)
            : this(error, message, ShutdownHint.HardShutdown)
        {
        }

        public Http2Exception(Http2Error error, string message, ShutdownHint shutdownHint)
            : base(message)
        {
            this.Error = error;
            this.ShutdownHint = shutdownHint;
        }

        public Http2Exception(Http2Error error, string message, Exception cause)
            : this(error, message, cause, ShutdownHint.HardShutdown)
        {
        }

        public Http2Exception(Http2Error error, string message, Exception cause, ShutdownHint shutdownHint)
            : base(message, cause)
        {
            this.Error = error;
            this.ShutdownHint = shutdownHint;
        }

        public Http2Error Error { get; }

        public ShutdownHint ShutdownHint { get; }

        /// <summary>
        /// Use if an error has occurred which can not be isolated to a single stream, but instead applies
        /// to the entire connection.
        /// </summary>
        /// <param name="error">The type of error as defined by the HTTP/2 specification.</param>
        /// <param name="fmt">string with the content and format for the additional debug data.</param>
        /// <param name="args">Objects which fit into the format defined by <paramref name="fmt"/>.</param>
        /// <returns>An exception which can be translated into a HTTP/2 error.</returns>
        public static Http2Exception ConnectionError(Http2Error error, string fmt, params object[] args)
        {
            return new Http2Exception(error, args is object && (uint)args.Length > 0u ? string.Format(fmt, args) : fmt);
        }

        /// <summary>
        /// Use if an error has occurred which can not be isolated to a single stream, but instead applies
        /// to the entire connection.
        /// </summary>
        /// <param name="error">The type of error as defined by the HTTP/2 specification.</param>
        /// <param name="cause">The object which caused the error.</param>
        /// <param name="fmt">string with the content and format for the additional debug data.</param>
        /// <param name="args">Objects which fit into the format defined by <paramref name="fmt"/>.</param>
        /// <returns>An exception which can be translated into a HTTP/2 error.</returns>
        public static Http2Exception ConnectionError(Http2Error error, Exception cause, string fmt, params object[] args)
        {
            return new Http2Exception(error, args is object && (uint)args.Length > 0u ? string.Format(fmt, args) : fmt, cause);
        }

        /// <summary>
        /// Use if an error has occurred which can not be isolated to a single stream, but instead applies
        /// to the entire connection.
        /// </summary>
        /// <param name="error">The type of error as defined by the HTTP/2 specification.</param>
        /// <param name="fmt">string with the content and format for the additional debug data.</param>
        /// <param name="args">Objects which fit into the format defined by <paramref name="fmt"/>.</param>
        /// <returns>An exception which can be translated into a HTTP/2 error.</returns>
        public static Http2Exception ClosedStreamError(Http2Error error, string fmt, params object[] args)
        {
            return new ClosedStreamCreationException(error, args is object && (uint)args.Length > 0u ? string.Format(fmt, args) : fmt);
        }

        /// <summary>
        /// Use if an error which can be isolated to a single stream has occurred. If the <paramref name="id"/> is not
        /// <see cref="Http2CodecUtil.ConnectionStreamId"/> then a <see cref="StreamException"/> will be returned.
        /// Otherwise the error is considered a connection error and a <see cref="Http2Exception"/> is returned.
        /// </summary>
        /// <param name="id">The stream id for which the error is isolated to.</param>
        /// <param name="error">The type of error as defined by the HTTP/2 specification.</param>
        /// <param name="fmt">string with the content and format for the additional debug data.</param>
        /// <param name="args">Objects which fit into the format defined by <paramref name="fmt"/>.</param>
        /// <returns>If the <paramref name="id"/> is not
        /// <see cref="Http2CodecUtil.ConnectionStreamId"/> then a <see cref="StreamException"/> will be returned.
        /// Otherwise the error is considered a connection error and a <see cref="Http2Exception"/> is returned.</returns>
        public static Http2Exception StreamError(int id, Http2Error error, string fmt, params object[] args)
        {
            return Http2CodecUtil.ConnectionStreamId == id ?
                    Http2Exception.ConnectionError(error, fmt, args) :
                        new StreamException(id, error, args is object && (uint)args.Length > 0u ? string.Format(fmt, args) : fmt);
        }

        /// <summary>
        /// Use if an error which can be isolated to a single stream has occurred.  If the <paramref name="id"/> is not
        /// <see cref="Http2CodecUtil.ConnectionStreamId"/> then a <see cref="StreamException"/> will be returned.
        /// Otherwise the error is considered a connection error and a <see cref="Http2Exception"/> is returned.
        /// </summary>
        /// <param name="id">The stream id for which the error is isolated to.</param>
        /// <param name="error">The type of error as defined by the HTTP/2 specification.</param>
        /// <param name="cause">The object which caused the error.</param>
        /// <param name="fmt">string with the content and format for the additional debug data.</param>
        /// <param name="args">Objects which fit into the format defined by <paramref name="fmt"/>.</param>
        /// <returns>If the <paramref name="id"/> is not
        /// <see cref="Http2CodecUtil.ConnectionStreamId"/> then a <see cref="StreamException"/> will be returned.
        /// Otherwise the error is considered a connection error and a <see cref="Http2Exception"/> is returned.</returns>
        public static Http2Exception StreamError(int id, Http2Error error, Exception cause, string fmt, params object[] args)
        {
            return Http2CodecUtil.ConnectionStreamId == id ?
                    Http2Exception.ConnectionError(error, cause, fmt, args) :
                        new StreamException(id, error, args is object && (uint)args.Length > 0u ? string.Format(fmt, args) : fmt, cause);
        }

        /// <summary>
        /// A specific stream error resulting from failing to decode headers that exceeds the max header size list.
        /// If the <paramref name="id"/> is not <see cref="Http2CodecUtil.ConnectionStreamId"/> then a
        /// <see cref="StreamException"/> will be returned. Otherwise the error is considered a
        /// connection error and a <see cref="Http2Exception"/> is returned.
        /// </summary>
        /// <param name="id">The stream id for which the error is isolated to.</param>
        /// <param name="error">The type of error as defined by the HTTP/2 specification.</param>
        /// <param name="onDecode">Whether this error was caught while decoding headers</param>
        /// <param name="fmt">string with the content and format for the additional debug data.</param>
        /// <param name="args">Objects which fit into the format defined by <paramref name="fmt"/>.</param>
        /// <returns>If the <paramref name="id"/> is not
        /// <see cref="Http2CodecUtil.ConnectionStreamId"/> then a <see cref="HeaderListSizeException"/>
        /// will be returned. Otherwise the error is considered a connection error and a <see cref="Http2Exception"/> is
        /// returned.</returns>
        public static Http2Exception HeaderListSizeError(int id, Http2Error error, bool onDecode, string fmt, params object[] args)
        {
            return Http2CodecUtil.ConnectionStreamId == id ?
                    Http2Exception.ConnectionError(error, fmt, args) :
                        new HeaderListSizeException(id, error, args is object && (uint)args.Length > 0u ? string.Format(fmt, args) : fmt, onDecode);
        }

        /// <summary>
        /// Check if an exception is isolated to a single stream or the entire connection.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        /// <returns><c>true</c> if <paramref name="e"/> is an instance of <see cref="StreamException"/>.
        /// <c>false</c> otherwise.</returns>
        public static bool IsStreamError(Http2Exception e)
        {
            return e is StreamException;
        }

        /// <summary>
        /// Get the stream id associated with an exception.
        /// </summary>
        /// <param name="e">The exception to get the stream id for.</param>
        /// <returns><see cref="Http2CodecUtil.ConnectionStreamId"/> if <paramref name="e"/> is a connection error.
        /// Otherwise the stream id associated with the stream error.</returns>
        public static int GetStreamId(Http2Exception e)
        {
            return e is StreamException streamException ? streamException.StreamId : Http2CodecUtil.ConnectionStreamId;
        }
    }

    /// <summary>
    /// Used when a stream creation attempt fails but may be because the stream was previously closed.
    /// </summary>
    public class ClosedStreamCreationException : Http2Exception
    {
        public ClosedStreamCreationException(Http2Error error)
            : base(error)
        {
        }

        public ClosedStreamCreationException(Http2Error error, string message)
            : base(error, message)
        {
        }

        public ClosedStreamCreationException(Http2Error error, string message, Exception cause)
            : base(error, message, cause)
        {
        }
    }

    /// <summary>
    /// Represents an exception that can be isolated to a single stream (as opposed to the entire connection).
    /// </summary>
    public class StreamException : Http2Exception
    {
        public StreamException(int streamId, Http2Error error, string message)
            : base(error, message, ShutdownHint.NoShutdown)
        {
            this.StreamId = streamId;
        }

        public StreamException(int streamId, Http2Error error, string message, Exception cause)
            : base(error, message, cause, ShutdownHint.NoShutdown)

        {
            this.StreamId = streamId;
        }

        public int StreamId { get; }
    }

    public class HeaderListSizeException : StreamException
    {
        public HeaderListSizeException(int streamId, Http2Error error, string message, bool decode)
            : base(streamId, error, message)
        {
            this.DuringDecode = decode;
        }

        public bool DuringDecode { get; }
    }

    /// <summary>
    /// Provides the ability to handle multiple stream exceptions with one throw statement.
    /// </summary>
    public class CompositeStreamException : Http2Exception, IEnumerable<StreamException>
    {
        private readonly List<StreamException> exceptions;

        public CompositeStreamException(Http2Error error, int initialCapacity)
            : base(error, ShutdownHint.NoShutdown)
        {

            this.exceptions = new List<StreamException>(initialCapacity);
        }

        public void Add(StreamException e)
        {
            this.exceptions.Add(e);
        }

        public IList<StreamException> Exceptions => this.exceptions;

        public IEnumerator<StreamException> GetEnumerator()
        {
            return this.exceptions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
