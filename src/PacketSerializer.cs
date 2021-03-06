﻿/* 
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
using System.Text;

namespace Frostbite_Example {
    public class PacketSerializer {

        /// <summary>
        /// The size of the header of a single packet, or the minimum number of bytes
        /// we need before we should even look for a full packet.
        /// </summary>
        public UInt32 PacketHeaderSize { get; protected set; }

        public PacketSerializer() {
            this.PacketHeaderSize = 12;
        }

        /// <summary>
        /// Converts a type Packet to a byte array for transmission.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public byte[] Serialize(Packet packet) {

            // Construct the header uint32
            UInt32 header = packet.SequenceId != null ? (UInt32)packet.SequenceId & 0x3fffffff : 0x3fffffff;

            if (packet.Origin == PacketOrigin.Server) {
                header |= 0x80000000;
            }

            if (packet.IsResponse == true) {
                header |= 0x40000000;
            }

            // Construct the remaining packet headers
            UInt32 packetSize = this.PacketHeaderSize;
            UInt32 wordCount = Convert.ToUInt32(packet.Words.Count);

            // Encode each word (WordLength, Word Bytes, Null Byte)
            byte[] encodedWords = new byte[] { };
            foreach (string word in packet.Words) {

                string convertedWord = word;

                // Truncate words over 64 kbs (though the string is Unicode it gets converted below so this does make sense)
                if (convertedWord.Length > UInt16.MaxValue - 1) {
                    convertedWord = convertedWord.Substring(0, UInt16.MaxValue - 1);
                }

                byte[] appendEncodedWords = new byte[encodedWords.Length + convertedWord.Length + 5];

                encodedWords.CopyTo(appendEncodedWords, 0);

                BitConverter.GetBytes(convertedWord.Length).CopyTo(appendEncodedWords, encodedWords.Length);
                Encoding.GetEncoding(1252).GetBytes(convertedWord + Convert.ToChar(0x00)).CopyTo(appendEncodedWords, encodedWords.Length + 4);

                encodedWords = appendEncodedWords;
            }

            // Get the full size of the packet.
            packetSize += Convert.ToUInt32(encodedWords.Length);

            // Now compile the whole packet.
            byte[] returnPacket = new byte[packetSize];

            BitConverter.GetBytes(header).CopyTo(returnPacket, 0);
            BitConverter.GetBytes(packetSize).CopyTo(returnPacket, 4);
            BitConverter.GetBytes(wordCount).CopyTo(returnPacket, 8);
            encodedWords.CopyTo(returnPacket, this.PacketHeaderSize);

            return returnPacket;
        }

        /// <summary>
        /// Converts a byte array into a Packet. The byte array must match the required length of data exactly.
        /// </summary>
        /// <param name="packetData"></param>
        /// <returns></returns>
        public Packet Deserialize(byte[] packetData) {

            Packet packet = new Packet();

            // The header contains flags specifying the origin, response and the sequence id.
            UInt32 header = BitConverter.ToUInt32(packetData, 0);

            // Unused since packetData has the data in this implementation, but this is where/how you can get the packet size.
            // UInt32 packetSize = BitConverter.ToUInt32(packetData, 4); 

            UInt32 wordsTotal = BitConverter.ToUInt32(packetData, 8);

            packet.Origin = Convert.ToBoolean(header & 0x80000000) == true ? PacketOrigin.Server : PacketOrigin.Client;

            packet.IsResponse = Convert.ToBoolean(header & 0x40000000);
            packet.SequenceId = header & 0x3fffffff;

            int wordOffset = 0;

            for (UInt32 wordCount = 0; wordCount < wordsTotal; wordCount++) {
                UInt32 wordLength = BitConverter.ToUInt32(packetData, (int)this.PacketHeaderSize + wordOffset);

                packet.Words.Add(Encoding.GetEncoding(1252).GetString(packetData, (int)this.PacketHeaderSize + wordOffset + 4, (int)wordLength));

                wordOffset += Convert.ToInt32(wordLength) + 5; // WordLength + WordSize + NullByte
            }

            return packet;
        }

        /// <summary>
        /// Reads the packet size specified in whatever data's header we have.
        /// </summary>
        /// <param name="packetData"></param>
        /// <returns></returns>
        public long ReadPacketSize(byte[] packetData) {
            long length = 0;

            if (packetData.Length >= this.PacketHeaderSize) {
                length = BitConverter.ToUInt32(packetData, 4);
            }

            return length;
        }
    }
}