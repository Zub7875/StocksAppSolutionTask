using Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;


namespace StocksAppSolution
{   
    public class Program  
    {
        #region MainMethod

        static void Main(string[] args)
        {
            string serverAddress = "127.0.0.1"; 
            int port = 3000; 

            try
            {
                // Create a TCP client and connect to the server
                TcpClient tcpClient = new TcpClient(serverAddress, port);
                Console.WriteLine("Connected to the server.");

                // Get the network stream
                NetworkStream stream = tcpClient.GetStream();

                // Prepare the request payload (1 byte for callType: 1 for Stream All Packets)
                byte[] requestPayload = new byte[1]; // 1 byte for callType
                requestPayload[0] = 1; // "1" means Stream All Packets

                // Send the request to the server
                stream.Write(requestPayload, 0, requestPayload.Length);
                Console.WriteLine("Request sent to server to stream all packets.");

                // List to store parsed packet data
                List<PacketData> packetDataList = new List<PacketData>();

                // Track the sequence numbers
                HashSet<int> receivedSequences = new HashSet<int>();
                int lastSequence = -1;

                // Buffer to store the incoming data
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    int offset = 0;

                    while (offset < bytesRead)
                    {
                        // Read each packet (each packet is 14 bytes: Symbol(4), BuySellIndicator(1), Quantity(4), Price(4), Sequence(4))
                        PacketData packet = new PacketData
                        {
                            Symbol = Encoding.ASCII.GetString(buffer, offset, 4).Trim(), // 4 bytes for Symbol
                            BuySellIndicator = (char)buffer[offset + 4], // 1 byte for Buy/Sell Indicator
                            Quantity = BitConverter.ToInt32(buffer, offset + 5), // 4 bytes for Quantity
                            Price = BitConverter.ToInt32(buffer, offset + 9), // 4 bytes for Price
                            Sequence = BitConverter.ToInt32(buffer, offset + 13) // 4 bytes for Sequence
                        };

                        packetDataList.Add(packet);
                        receivedSequences.Add(packet.Sequence);
                        lastSequence = packet.Sequence;

                        offset += 17; // Move to the next packet (each packet is 17 bytes long)
                    }
                }

                // Identify missing sequences
                Console.WriteLine("Identifying missing sequences...");
                List<int> missingSequences = new List<int>();

                for (int i = 1; i <= lastSequence; i++)
                {
                    if (!receivedSequences.Contains(i))
                    {
                        missingSequences.Add(i);
                    }
                }

                // Request the missing packets
                if (missingSequences.Count > 0)
                {
                    Console.WriteLine("Missing sequences detected. Requesting them...");
                    foreach (int seq in missingSequences)
                    {
                        // Prepare the resend packet request payload (2 for Resend Packet, with the sequence number)
                        byte[] resendPayload = new byte[2];
                        resendPayload[0] = 2; // CallType 2 for Resend Packet
                        resendPayload[1] = (byte)seq; // Sequence number to be resent

                        // Send the resend request
                        stream.Write(resendPayload, 0, resendPayload.Length);
                        Console.WriteLine($"Requesting missing packet with sequence: {seq}");

                        // Wait for the server response for this specific packet
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            int offset = 0;

                            while (offset < bytesRead)
                            {
                                PacketData packet = new PacketData
                                {
                                    Symbol = Encoding.ASCII.GetString(buffer, offset, 4).Trim(),
                                    BuySellIndicator = (char)buffer[offset + 4],
                                    Quantity = BitConverter.ToInt32(buffer, offset + 5),
                                    Price = BitConverter.ToInt32(buffer, offset + 9),
                                    Sequence = BitConverter.ToInt32(buffer, offset + 13)
                                };

                                packetDataList.Add(packet);
                                offset += 17;
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No missing sequences detected.");
                }

                // Serialize the list of packet data to JSON
                string json = JsonSerializer.Serialize(packetDataList, new JsonSerializerOptions { WriteIndented = true });

                // Save the JSON to a file
                File.WriteAllText("output.json", json);
                Console.WriteLine("Data saved to output.json");

                // Close the stream and connection
                stream.Close();
                tcpClient.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        #endregion

    }
}
