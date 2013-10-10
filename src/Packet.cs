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

namespace Battlefield3_Example {

    public class Packet {

        /// <summary>
        /// When this packet was created
        /// </summary>
        public DateTime Stamp { get; set; }

        /// <summary>
        /// The origin of the packet. This is useful when the server sends back responses to packets, we
        /// can say the packet originiated from the client and this is the response.
        /// </summary>
        public PacketOrigin Origin { get; set; }

        /// <summary>
        /// If this is a response or not to a previous packet.
        /// </summary>
        public bool IsResponse { get; set; }

        /// <summary>
        /// A list of words to send to the server or recieved from the server that make up
        /// a frostbite command/event.
        /// </summary>
        public List<String> Words { get; set; }

        /// <summary>
        /// The sequence id for this command/event
        /// </summary>
        public UInt32? SequenceId { get; set; }

        public Packet() {
            this.SequenceId = null;
            this.Words = new List<String>();
            this.Origin = PacketOrigin.None;
            this.IsResponse = false;
            this.Stamp = DateTime.Now;
        }

        public override string ToString() {
            return this.Words.Count > 0 ? String.Join(" ", this.Words.ToArray()) : String.Empty;
        }
    }
}