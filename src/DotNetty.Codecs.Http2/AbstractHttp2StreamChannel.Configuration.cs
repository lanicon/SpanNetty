﻿/*
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
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    partial class AbstractHttp2StreamChannel
    {
        /// <summary>
        /// <see cref="IChannelConfiguration"/> so that the high and low writebuffer watermarks can reflect the outbound flow control
        /// window, without having to create a new <c>WriteBufferWaterMark</c> object whenever the flow control window
        /// changes.
        /// </summary>
        sealed class Http2StreamChannelConfiguration : DefaultChannelConfiguration
        {
            public Http2StreamChannelConfiguration(AbstractHttp2StreamChannel channel)
                : base(channel)
            {
            }

            public override IMessageSizeEstimator MessageSizeEstimator
            {
                get => FlowControlledFrameSizeEstimator.Instance;
                set => throw new NotSupportedException();
            }
        }
    }
}
