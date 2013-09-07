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

namespace Myrcon_Battlefield3_Example {
    public class Program {

        protected static FrostbiteConnection Connection = null;

        static void Main(string[] args) {

            // You should populate the below details to avoid having to enter them each run.
            Program.Connection = new FrostbiteConnection() {
                // Hostname = "",
                // Port = 27000,
                // PlainTextPassword = ""
            };
            Program.Connection.PacketReceived += new FrostbiteConnection.PacketHandler(Connection_PacketReceived);
            Program.Connection.PacketSent += new FrostbiteConnection.PacketHandler(Connection_PacketSent);
            Program.Connection.Connected += new FrostbiteConnection.EmptyParameterHandler(Connection_Connected);
            Program.Connection.Disconnected += new FrostbiteConnection.EmptyParameterHandler(Connection_Disconnected);

            Program.Connection.Error += new FrostbiteConnection.ErrorHandler(Connection_Error);
            
            while (String.IsNullOrEmpty(Program.Connection.Hostname) == true) {
                Console.Write("Hostname: ");
                Program.Connection.Hostname = Console.ReadLine();
            }

            while (Program.Connection.Port == 0) {
                string portInput = String.Empty;
                ushort port;

                do {
                    Console.Write("Port: ");
                    portInput = Console.ReadLine();
                } while (ushort.TryParse(portInput, out port) == false);

                Program.Connection.Port = port;
            }

            while (String.IsNullOrEmpty(Program.Connection.PlainTextPassword) == true) {
                Console.Write("Password: ");
                Program.Connection.PlainTextPassword = Console.ReadLine();
            }

            Console.WriteLine("Type 'exit' to close the application");
            Console.WriteLine("Try the following commands:");
            Console.WriteLine("\tadmin.help");
            Console.WriteLine("\tadmin.say \"Hello World!\" all");
            Console.WriteLine("\tadmin.eventsEnabled true");
            Console.WriteLine("Attempting connection to {0}:{1}", Program.Connection.Hostname, Program.Connection.Port);
            Program.Connection.Connect();

            while (Program.Connection.Client.Connected == true) {
                String messageInput = Console.ReadLine();

                if (String.Compare(messageInput, "exit", StringComparison.OrdinalIgnoreCase) == 0) {
                    Program.Connection.Shutdown();

                    Environment.Exit(0);
                }
                else {
                    Program.Connection.Command(messageInput.Wordify());
                }
            }

            Console.Write("Press any key to continue..");
            Console.ReadLine();
        }

        static void Connection_Error(FrostbiteConnection sender, Exception e) {
            Console.WriteLine("ERROR: {0}", e.Message);
        }

        static void Connection_Disconnected(FrostbiteConnection sender) {
            Console.WriteLine("Disconnected");
        }

        static void Connection_Connected(FrostbiteConnection sender) {
            Console.WriteLine("Connected");

            sender.Login();
        }

        static void Connection_PacketSent(FrostbiteConnection sender, Packet packet) {
            Console.WriteLine("SENT: {0}", packet);
        }

        static void Connection_PacketReceived(FrostbiteConnection sender, Packet packet) {
            Console.WriteLine("RECV: {0}", packet);
        }
    }
}