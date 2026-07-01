using System;
using System.Collections.Generic;
using System.Text;

namespace Project.Sensors.Network
{
    public readonly struct OscDecodedMessage
    {
        public readonly string Address;
        public readonly float[] Values;

        public OscDecodedMessage(string address, float[] values)
        {
            Address = address;
            Values = values;
        }
    }

    public static class OscMessageDecoder
    {
        private static readonly bool RequiresByteReversal = BitConverter.IsLittleEndian;

        public static bool TryDecodePacket(byte[] packet, out OscDecodedMessage[] messages)
        {
            messages = Array.Empty<OscDecodedMessage>();

            if (packet == null || packet.Length < 4)
            {
                return false;
            }

            var decodedMessages = new List<OscDecodedMessage>();

            if (!TryDecodeElement(packet, decodedMessages) || decodedMessages.Count == 0)
            {
                return false;
            }

            messages = decodedMessages.ToArray();
            return true;
        }

        public static bool TryDecode(byte[] packet, out string oscAddress, out float[] values)
        {
            oscAddress = string.Empty;
            values = Array.Empty<float>();

            if (packet == null || packet.Length < 4)
            {
                return false;
            }

            int index = 0;

            try
            {
                oscAddress = ReadOscString(packet, ref index);
                if (string.IsNullOrEmpty(oscAddress) || oscAddress[0] != '/')
                {
                    return false;
                }

                string typeTags = ReadOscString(packet, ref index);
                if (string.IsNullOrEmpty(typeTags) || typeTags[0] != ',')
                {
                    return false;
                }

                var tempValues = new List<float>(typeTags.Length - 1);

                for (int i = 1; i < typeTags.Length; i++)
                {
                    char tag = typeTags[i];
                    if (tag == 'f')
                    {
                        if (index + 4 > packet.Length)
                        {
                            return false;
                        }

                        float val = ReadFloat32(packet, index);
                        tempValues.Add(val);
                        index += 4;
                    }
                    else if (tag == 'i')
                    {
                        if (index + 4 > packet.Length)
                        {
                            return false;
                        }

                        int val = ReadInt32(packet, index);
                        tempValues.Add((float)val);
                        index += 4;
                    }
                    else if (tag == 'd')
                    {
                        if (index + 8 > packet.Length)
                        {
                            return false;
                        }

                        double val = ReadFloat64(packet, index);
                        tempValues.Add((float)val);
                        index += 8;
                    }
                    else
                    {
                        return false;
                    }
                }

                values = tempValues.ToArray();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryDecodeElement(byte[] packet, List<OscDecodedMessage> messages)
        {
            if (IsBundle(packet))
            {
                return TryDecodeBundle(packet, messages);
            }

            if (!TryDecode(packet, out string address, out float[] values))
            {
                return false;
            }

            messages.Add(new OscDecodedMessage(address, values));
            return true;
        }

        private static bool TryDecodeBundle(byte[] packet, List<OscDecodedMessage> messages)
        {
            if (packet.Length < 16)
            {
                return false;
            }

            int index = 16;

            while (index < packet.Length)
            {
                if (index + 4 > packet.Length)
                {
                    return false;
                }

                int elementSize = ReadInt32(packet, index);
                index += 4;

                if (elementSize <= 0 || index + elementSize > packet.Length)
                {
                    return false;
                }

                var element = new byte[elementSize];
                Buffer.BlockCopy(packet, index, element, 0, elementSize);
                index += elementSize;

                if (!TryDecodeElement(element, messages))
                {
                    return false;
                }
            }

            return index == packet.Length;
        }

        private static bool IsBundle(byte[] packet)
        {
            return packet.Length >= 8 &&
                   packet[0] == (byte)'#' &&
                   packet[1] == (byte)'b' &&
                   packet[2] == (byte)'u' &&
                   packet[3] == (byte)'n' &&
                   packet[4] == (byte)'d' &&
                   packet[5] == (byte)'l' &&
                   packet[6] == (byte)'e' &&
                   packet[7] == 0;
        }

        private static string ReadOscString(byte[] data, ref int index)
        {
            if (index < 0 || index >= data.Length)
            {
                throw new FormatException("OSC string starts outside the packet.");
            }

            int start = index;
            while (index < data.Length && data[index] != 0)
            {
                index++;
            }

            if (index >= data.Length)
            {
                throw new FormatException("OSC string is not null-terminated.");
            }

            string result = Encoding.UTF8.GetString(data, start, index - start);

            index = (index + 4) & ~3;

            if (index > data.Length)
            {
                throw new FormatException("OSC string padding exceeds the packet.");
            }

            return result;
        }

        private static float ReadFloat32(byte[] data, int index)
        {
            if (RequiresByteReversal)
            {
                byte[] reversed = new byte[4] { data[index + 3], data[index + 2], data[index + 1], data[index + 0] };
                return BitConverter.ToSingle(reversed, 0);
            }
            return BitConverter.ToSingle(data, index);
        }

        private static int ReadInt32(byte[] data, int index)
        {
            if (RequiresByteReversal)
            {
                byte[] reversed = new byte[4] { data[index + 3], data[index + 2], data[index + 1], data[index + 0] };
                return BitConverter.ToInt32(reversed, 0);
            }
            return BitConverter.ToInt32(data, index);
        }

        private static double ReadFloat64(byte[] data, int index)
        {
            if (RequiresByteReversal)
            {
                byte[] reversed = new byte[8]
                {
                    data[index + 7], data[index + 6], data[index + 5], data[index + 4],
                    data[index + 3], data[index + 2], data[index + 1], data[index + 0]
                };

                return BitConverter.ToDouble(reversed, 0);
            }

            return BitConverter.ToDouble(data, index);
        }
    }
}
