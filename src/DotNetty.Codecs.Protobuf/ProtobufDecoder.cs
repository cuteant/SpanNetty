/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Protobuf
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Google.Protobuf;

    public class ProtobufDecoder : MessageToMessageDecoder<IByteBuffer>
    {
        private readonly MessageParser _messageParser;

        public ProtobufDecoder(MessageParser messageParser)
        {
            if (messageParser is null) { ThrowArgumentNullException(); }

            _messageParser = messageParser;
        }

        public override bool IsSharable => true;

        protected override void Decode(IChannelHandlerContext context, IByteBuffer message, List<object> output)
        {
            Debug.Assert(context != null);
            Debug.Assert(message != null);
            Debug.Assert(output != null);

            int length = message.ReadableBytes;
            if (0u >= (uint)length) { return; }

            Stream inputStream = null;
            try
            {
                CodedInputStream codedInputStream;
                if (message.IsSingleIoBuffer)
                {
                    ArraySegment<byte> bytes = message.GetIoBuffer(message.ReaderIndex, length);
                    codedInputStream = new CodedInputStream(bytes.Array, bytes.Offset, length);
                }
                else
                {
                    inputStream = new ReadOnlyByteBufferStream(message, false);
                    codedInputStream = new CodedInputStream(inputStream);
                }

                //
                // Note that we do not dispose the input stream because there is no input stream attached. 
                // Ideally, it should be disposed. BUT if it is disposed, a null reference exception is 
                // thrown because CodedInputStream flag leaveOpen is set to false for direct byte array reads,
                // when it is disposed the input stream is null.
                // 
                // In this case it is ok because the CodedInputStream does not own the byte data.
                //
                IMessage decoded = _messageParser.ParseFrom(codedInputStream);
                if (decoded is object)
                {
                    output.Add(decoded);
                }
            }
            catch (Exception exception)
            {
                ThrowCodecException(exception);
            }
            finally
            {
                inputStream?.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCodecException(Exception exc)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException(exc);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentNullException()
        {
            throw GetException();
            static ArgumentNullException GetException()
            {
                return new ArgumentNullException("messageParser");
            }
        }
    }
}