/* 
 * Copyright (C) 2013 Myrcon Pty. Ltd. / Geoff "Phogue" Green
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to 
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in 
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 * IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Battlefield3_Example {
    public class FrostbiteConnection {

        /// <summary>
        /// The client connected to the end point.
        /// </summary>
        public TcpClient Client { get; private set; }

        /// <summary>
        /// The open stream to read data from.
        /// </summary>
        protected NetworkStream Stream { get; set; }

        /// <summary>
        /// The end point hostname to connect to
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// The end point port to connect to
        /// </summary>
        public ushort Port { get; set; }

        /// <summary>
        /// The password to authenticate with. We should be using the hashed method in all
        /// production code, but for this example to keep it ultra simple we're using the
        /// command that does not need prior requests for data.
        /// </summary>
        public String PlainTextPassword { get; set; }

        /// <summary>
        /// Our byte buffer, used to store data if we don't get a complete packet.
        /// </summary>
        protected byte[] Buffer;

        /// <summary>
        /// The data that was last read from our stream.
        /// </summary>
        protected byte[] ReadData;

        /// <summary>
        /// The serializer used to pass through packets to be sent or received.
        /// </summary>
        protected PacketSerializer PacketSerializer { get; set; }

        /// <summary>
        /// Stores the last sequence number used when issuing a command to the server.
        /// </summary>
        protected UInt32 SequenceNumber { get; set; }

        /// <summary>
        /// Lock used when aquiring a sequence #
        /// </summary>
        protected readonly Object AcquireSequenceNumberLock = new Object();

        #region Events

        public delegate void ErrorHandler(FrostbiteConnection sender, Exception e);
        public delegate void EmptyParameterHandler(FrostbiteConnection sender);
        public delegate void PacketHandler(FrostbiteConnection sender, Packet packet);

        /// <summary>
        /// Fired whenever an event occurs on the connection (exception). This is a general
        /// catch all for this example.
        /// </summary>
        public event ErrorHandler Error;

        /// <summary>
        /// Fires the Error event, provided there are some listeners attached.
        /// </summary>
        /// <param name="e"></param>
        protected void OnError(Exception e) {
            var handler = this.Error;

            if (handler != null) {
                handler(this, e);
            }
        }

        /// <summary>
        /// Fired once a connection has been established to the server. This does not mean we
        /// are yet authenticated, just that we have a stream and can send/recv packets
        /// </summary>
        public event EmptyParameterHandler Connected;

        /// <summary>
        /// Fires the Connected event, provided there are some listeners attached.
        /// </summary>
        protected void OnConnected() {
            var handler = this.Connected;

            if (handler != null) {
                handler(this);
            }
        }

        /// <summary>
        /// Fired whenever the client or server forcably closes the current open connection.
        /// </summary>
        public event EmptyParameterHandler Disconnected;

        /// <summary>
        /// Fires the Disconnected event, provided there are some listeners attached.
        /// </summary>
        protected void OnDisconnected() {
            var handler = this.Disconnected;

            if (handler != null) {
                handler(this);
            }
        }

        /// <summary>
        /// Fired whenever a complete packet is recieved from the server.
        /// </summary>
        public event PacketHandler PacketReceived;

        /// <summary>
        /// Fires the PacketReceived event, provided there are some listeners attached.
        /// </summary>
        protected void OnPacketReceived(Packet packet) {
            var handler = this.PacketReceived;

            if (handler != null) {
                handler(this, packet);
            }
        }

        /// <summary>
        /// Fired after a packet has been sent to the server.
        /// </summary>
        public event PacketHandler PacketSent;

        /// <summary>
        /// Fires the PacketSent event, provided there are some listeners attached.
        /// </summary>
        protected void OnPacketSent(Packet packet) {
            var handler = this.PacketSent;

            if (handler != null) {
                handler(this, packet);
            }
        }

        #endregion

        public FrostbiteConnection() {
            this.Client = new TcpClient();
            this.Hostname = String.Empty;

            this.Buffer = new byte[0];
            this.ReadData = new byte[1024];

            this.PacketSerializer = new PacketSerializer();

            this.SequenceNumber = 0;
        }

        /// <summary>
        /// Fetches a new sequence number, ensuring that it is not a duplicate for this connection.
        /// </summary>
        /// <returns></returns>
        public UInt32 AcquireSequenceNumber() {
            lock (this.AcquireSequenceNumberLock) {
                return ++this.SequenceNumber;
            }
        }

        /// <summary>
        /// Synchronously connect to a server, then begin asynchronous reading of data from the server.
        /// </summary>
        /// <returns>Returns true if a connection was successfully established, false otherwise.</returns>
        public bool Connect() {
            bool connected = false;

            try {
                this.Client.Connect(this.Hostname, this.Port);

                if (this.Client.Connected == true) {
                    this.Stream = this.Client.GetStream();

                    this.Stream.BeginRead(this.ReadData, 0, this.ReadData.Length, this.ReceiveCallback, null);

                    this.OnConnected();

                    connected = true;
                }
            }
            catch (Exception e) {
                this.OnError(e);

                this.Shutdown();
            }

            return connected;
        }

        /// <summary>
        /// You shouldn't use the plain text password login. We use it here to keep this entire example simple.
        /// 
        /// Use the login.hashed method instead. I'm making a case for the plain text password login to be removed
        /// from the protocol in battlefield 4.
        /// </summary>
        public void Login() {
            this.Command("login.plainText", this.PlainTextPassword);
        }

        /// <summary>
        /// Send a list of words to the server as a command.
        /// </summary>
        /// <param name="words">The words to build a packet from to then send to the server.</param>
        public void Command(List<String> words) {
            this.Send(new Packet() {
                Origin = PacketOrigin.Client,
                IsResponse = false,
                SequenceId = this.AcquireSequenceNumber(),
                Words = words
            });
        }

        /// <summary>
        /// Send an array of words as a command to the server. This is a proxy for Command(List words)
        /// </summary>
        /// <param name="words">The words to build a packet from to then send to the server.</param>
        public void Command(params String[] words) {
            this.Command(new List<String>(words));
        }

        /// <summary>
        /// Respond to a given packet with a list of words. This will ensure the origin stays the same,
        /// the response flag is set to true and the sequence id will match. This method should be used
        /// to respond to server initiated events.
        /// </summary>
        /// <param name="packet">The packet to respond to</param>
        /// <param name="words">The words to send as a reply to the packet</param>
        public void Respond(Packet packet, List<String> words) {
            this.Send(new Packet() {
                Origin = packet.Origin,
                IsResponse = true,
                SequenceId = packet.SequenceId,
                Words = words
            });
        }

        /// <summary>
        /// Respond to a given packet with a list of words. This is a proxy for Respond(List words);
        /// </summary>
        /// <param name="packet">The packet to respond to</param>
        /// <param name="words">The words to send as a reply to the packet</param>
        public void Respond(Packet packet, params String[] words) {
            this.Respond(packet, new List<String>(words));
        }

        /// <summary>
        /// Sends a packet to the server, provided a connection is established.
        /// </summary>
        /// <param name="packet">The packet to send to the server</param>
        /// <returns>Returns true if the packet was successfully sent, false otherwise.</returns>
        private bool Send(Packet packet) {
            bool sent = false;

            try {
                if (this.Client.Connected == true && this.Stream != null) {
                    // 1. Encode the packet to byte[] for sending
                    byte[] data = this.PacketSerializer.Serialize(packet);

                    // 2. Write it to the stream
                    this.Stream.Write(data, 0, data.Length);

                    // 3. Alert the application a packet has been successfully sent.
                    this.OnPacketSent(packet);

                    sent = true;
                }
            }
            catch (Exception e) {
                this.OnError(e);

                this.Shutdown();
            }

            return sent;
        }

        /// <summary>
        /// Processes packets, responding to server side initiated events with an "OK" packet if required.
        /// </summary>
        /// <param name="packet">The packet that has been receieved and requires dispatching</param>
        protected void ProcessRecievedPacket(Packet packet) {
            this.OnPacketReceived(packet);

            // If this originated from the server then make sure we send back an OK message with
            // the same sequence id. If we don't do this then the server may disconnect us.
            if (packet.Origin == PacketOrigin.Server && packet.IsResponse == false) {
                this.Respond(packet, "OK");
            }
        }

        /// <summary>
        /// Callback for BeginRead of the stream for the connection to the server. 
        /// </summary>
        private void ReceiveCallback(IAsyncResult ar) {

            if (this.Client.Connected == true) {
                try {
                    int bytesRead = this.Stream.EndRead(ar);

                    if (bytesRead > 0) {

                        // 1. Resize our buffer to store the additional data just read
                        Array.Resize(ref this.Buffer, this.Buffer.Length + bytesRead);

                        // 2. Append the data just read onto the buffer.
                        Array.Copy(this.ReadData, 0, this.Buffer, this.Buffer.Length - bytesRead, bytesRead);

                        // 3. Find the size of the first packet on the buffer
                        long packetSize = this.PacketSerializer.ReadPacketSize(this.Buffer);

                        // WHILE we have enough data AND that data at least covers the header of the packet
                        while (this.Buffer.Length >= packetSize && this.Buffer.Length > this.PacketSerializer.PacketHeaderSize) {

                            // 4. Create a packet by removing the first X bytes from the buffer
                            Packet packet = this.PacketSerializer.Deserialize(this.Buffer.Take((int)packetSize).ToArray());

                            // 5. Alert application to new packet.
                            this.ProcessRecievedPacket(packet);

                            // 6. Remove all bytes from the buffer that were used to create the packet in step 4.
                            this.Buffer = this.Buffer.Skip((int)packetSize).ToArray();

                            // 7. Find out if we've got enough for another packet.
                            packetSize = this.PacketSerializer.ReadPacketSize(this.Buffer);
                        }

                        // Now wait for more data to read
                        this.Stream.BeginRead(this.ReadData, 0, this.ReadData.Length, this.ReceiveCallback, null);
                    }
                    else if (bytesRead == 0) {
                        // Nothing was read, time to shut down the connection.
                        this.Shutdown();
                    }
                }
                catch (Exception e) {
                    this.OnError(e);

                    this.Shutdown();
                }
            }
        }

        /// <summary>
        /// Shuts down an open connection to a server.
        /// </summary>
        public void Shutdown() {
            if (this.Client != null && this.Client.Connected == true) {
                this.Client.Close();

                this.OnDisconnected();
            }
        }
    }
}