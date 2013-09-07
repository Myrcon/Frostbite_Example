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

namespace Myrcon_Battlefield3_Example {
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
        public string PlainTextPassword { get; set; }

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
        /// Fired once a connection has been established to the server. This does not mean we
        /// are yet authenticated, just that we have a stream and can send/recv packets
        /// </summary>
        public event EmptyParameterHandler Connected;

        /// <summary>
        /// Fired whenever the client or server forcably closes the current open connection.
        /// </summary>
        public event EmptyParameterHandler Disconnected;

        /// <summary>
        /// Fired whenever a complete packet is recieved from the server.
        /// </summary>
        public event PacketHandler PacketReceived;

        /// <summary>
        /// Fired after a packet has been sent to the server.
        /// </summary>
        public event PacketHandler PacketSent;

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
            lock (new object()) {
                return ++this.SequenceNumber;
            }
        }

        public void Connect() {
            try {
                this.Client.Connect(this.Hostname, this.Port);

                if (this.Client.Connected == true) {

                    this.Stream = this.Client.GetStream();

                    this.Stream.BeginRead(this.ReadData, 0, this.ReadData.Length, this.ReceiveCallback, null);

                    if (this.Connected != null) {
                        this.Connected(this);
                    }
                }
            }
            catch (Exception e) {
                this.Error(this, e);
            }
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

        public void Command(List<String> words) {
            this.Send(new Packet() {
                Origin = PacketOrigin.Client,
                IsResponse = false,
                SequenceId = this.AcquireSequenceNumber(),
                Words = words
            });
        }

        public void Command(params String[] words) {
            this.Command(new List<String>(words));
        }

        public void Respond(Packet packet, List<String> words) {
            this.Send(new Packet() {
                Origin = packet.Origin,
                IsResponse = true,
                SequenceId = packet.SequenceId,
                Words = words
            });
        }

        public void Respond(Packet packet, params String[] words) {
            this.Respond(packet, new List<String>(words));
        }

        private void Send(Packet packet) {
            try {
                if (this.Client.Connected == true && this.Stream != null) {
                    // 1. Encode the packet to byte[] for sending
                    byte[] data = this.PacketSerializer.Serialize(packet);

                    // 2. Write it to the stream
                    this.Stream.Write(data, 0, data.Length);

                    // 3. Alert the application a packet has been successfully sent.
                    if (this.PacketSent != null) {
                        this.PacketSent(this, packet);
                    }
                }
            }
            catch (Exception e) {
                this.Error(this, e);
            }
        }

        protected void ProcessRecievedPacket(Packet packet) {
            if (this.PacketReceived != null) {
                this.PacketReceived(this, packet);
            }

            // If this originated from the server then make sure we send back an OK message with
            // the same sequence id.
            if (packet.Origin == PacketOrigin.Server && packet.IsResponse == false) {
                this.Respond(packet, "OK");
            }
        }

        private void ReceiveCallback(IAsyncResult ar) {

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
                    this.Shutdown();
                }
            }
            catch (Exception e) {
                this.Error(this, e);
            }
        }

        public void Shutdown() {
            if (this.Client.Connected == true && this.Disconnected != null) {
                this.Client.Close();

                this.Disconnected(this);
            }
        }
    }
}