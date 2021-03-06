﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Prime.MessagingServer.Data;

namespace Prime.SocketServer.Transport
{
    class ReceiveState
    {
        private const int DefaultBufferSize = 1024;

        /// <summary>
        /// Creates new object with specified client and buffer which size is default.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="provider"></param>
        /// <param name="bufferSize"></param>
        public ReceiveState(IdentifiedClient client, SocketServiceProvider provider, int bufferSize = DefaultBufferSize)
        {
            IdentifiedClient = client;
            Buffer = new byte[bufferSize];
            ServiceProvider = provider;
        }

        public IdentifiedClient IdentifiedClient { get; }
        public byte[] Buffer { get; }
        public SocketServiceProvider ServiceProvider { get; }
        public UInt32? ExpectedMessageSize { get; set; }
    }
}
