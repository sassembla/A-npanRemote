using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace WebuSocketCore
{
    public static class WebSocketByteGenerator
    {
        // #0                   1                   2                   3
        // #0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        // #+-+-+-+-+-------+-+-------------+-------------------------------+
        // #|F|R|R|R| opcode|M| Payload len |    Extended payload length    |
        // #|I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
        // #|N|V|V|V|       |S|             |   (if payload len==126/127)   |
        // #| |1|2|3|       |K|             |                               |
        // #+-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
        // #|     Extended payload length continued, if payload len == 127  |
        // #+ - - - - - - - - - - - - - - - +-------------------------------+
        // #|                               | Masking-key, if MASK set to 1 |
        // #+-------------------------------+-------------------------------+
        // #| Masking-key (continued)       |          Payload Data         |
        // #+-------------------------------- - - - - - - - - - - - - - - - +
        // #:                     Payload Data continued ...                :
        // #+ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
        // #|                     Payload Data continued ...                |

        public const byte OP_CONTINUATION = 0x0;
        public const byte OP_TEXT = 0x1;// 0001
        public const byte OP_BINARY = 0x2;// 0010
        public const byte OP_CLOSE = 0x8;// 1000
        public const byte OP_PING = 0x9;// 1001
        public const byte OP_PONG = 0xA;// 1010

        public const byte OPFilter = 0xF;// 1111

        public static byte[] Ping(byte[] data = null, bool isClient = true)
        {
            if (data == null) data = new byte[0];
            return WSDataFrame(1, 0, 0, 0, OP_PING, isClient, data);
        }

        public static byte[] Pong(byte[] data = null, bool isClient = true)
        {
            if (data == null) data = new byte[0];
            return WSDataFrame(1, 0, 0, 0, OP_PONG, isClient, data);
        }

        public static byte[] SendTextData(byte[] data, bool isClient = true)
        {
            return WSDataFrame(1, 0, 0, 0, OP_TEXT, isClient, data);
        }
        public static byte[] SendBinaryData(byte[] data, bool isClient = true)
        {
            return WSDataFrame(1, 0, 0, 0, OP_BINARY, isClient, data);
        }

        public static byte[] CloseData(bool isClient = true)
        {
            return WSDataFrame(1, 0, 0, 0, OP_CLOSE, isClient, new byte[0]);
        }

        private static byte[] WSDataFrame(
            byte fin,
            byte rsv1,
            byte rsv2,
            byte rsv3,
            byte opCode,
            bool maskRequired,
            byte[] data)
        {
            uint length = (uint)(data.Length);

            byte dataLength7bit = 0;
            UInt16 dataLength16bit = 0;
            UInt64 dataLength64bit = 0;

            if (length < 126)
            {
                dataLength7bit = (byte)length;
            }
            else if (65535 < length)
            {
                dataLength7bit = 127;
                dataLength64bit = length;
            }
            else
            {// 126 ~ 65535
                dataLength7bit = 126;
                dataLength16bit = (UInt16)length;
            }

            /*
				ready data stream structure for send.
			*/
            using (var dataStream = new MemoryStream())
            {
                dataStream.WriteByte((byte)((fin << 7) | (rsv1 << 6) | (rsv2 << 5) | (rsv3 << 4) | opCode));
                var mask = maskRequired ? 1 : 0;
                dataStream.WriteByte((byte)((mask << 7) | dataLength7bit));

                // 126 ~ 65535.
                if (0 < dataLength16bit)
                {
                    var intBytes = new byte[2];
                    intBytes[0] = (byte)(dataLength16bit >> 8);
                    intBytes[1] = (byte)dataLength16bit;

                    // dataLength16 to 2bytes.
                    dataStream.Write(intBytes, 0, intBytes.Length);
                }

                // 65536 ~.
                if (0 < dataLength64bit)
                {
                    var intBytes = new byte[8];
                    intBytes[0] = (byte)(dataLength64bit >> (8 * 7));
                    intBytes[1] = (byte)(dataLength64bit >> (8 * 6));
                    intBytes[2] = (byte)(dataLength64bit >> (8 * 5));
                    intBytes[3] = (byte)(dataLength64bit >> (8 * 4));
                    intBytes[4] = (byte)(dataLength64bit >> (8 * 3));
                    intBytes[5] = (byte)(dataLength64bit >> (8 * 2));
                    intBytes[6] = (byte)(dataLength64bit >> 8);
                    intBytes[7] = (byte)dataLength64bit;

                    // dataLength64 to 8bytes.
                    dataStream.Write(intBytes, 0, intBytes.Length);
                }

                if (maskRequired)
                {
                    // client should mask control frame.
                    var maskKey = NewMaskKey();

                    // insert mask key bytes.
                    dataStream.Write(maskKey, 0, maskKey.Length);

                    // mask data.
                    var maskedData = Masked(data, maskKey);
                    dataStream.Write(maskedData, 0, maskedData.Length);
                }
                else
                {
                    // server must not mask data.
                    dataStream.Write(data, 0, data.Length);
                }

                return dataStream.ToArray();
            }
        }

        /**
            クライアント側はランダムに生成したマスクキーを元に、与えられたデータをマスク処理する。            
         */
        private static byte[] Masked(byte[] data, byte[] maskKey)
        {
            for (var i = 0; i < data.Length; i++) data[i] ^= maskKey[i % 4];
            return data;
        }

        /**
            サーバ側はデータのマスクを解除する。
         */
        public static void Unmasked(ref byte[] data, byte[] maskKey, int index, int length)
        {
            for (var i = index; i < index + length; i++) data[i] ^= maskKey[(i - index) % 4];
        }

        /**
			get message detail from data.
			no copy emitted. only read data then return there indexies of messages.
		*/
        public static List<OpCodeAndPayloadIndex> GetIndexies(byte[] data)
        {
            var opCodeAndPayloadIndexies = new List<OpCodeAndPayloadIndex>();

            uint cursor = 0;
            while (cursor < data.Length)
            {

                // first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
                var opCode = (byte)(data[cursor++] & OPFilter);

                // second byte = mask(1), length(7)
                /*
					mask of data from server is definitely zero(0).
					ignore reading mask bit.
				*/
                uint length = (uint)data[cursor++];
                switch (length)
                {
                    case 126:
                        {
                            // next 2 byte is length data.
                            length = (uint)(
                                (data[cursor++] << 8) +
                                (data[cursor++])
                            );
                            break;
                        }
                    case 127:
                        {
                            // next 8 byte is length data.
                            length = (uint)(
                                (data[cursor++] << (8 * 7)) +
                                (data[cursor++] << (8 * 6)) +
                                (data[cursor++] << (8 * 5)) +
                                (data[cursor++] << (8 * 4)) +
                                (data[cursor++] << (8 * 3)) +
                                (data[cursor++] << (8 * 2)) +
                                (data[cursor++] << 8) +
                                (data[cursor++])
                            );
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }


                /*
					shortage of payload length.
					the whole payload datas of this message is not yet read from socket.
					
					break indexing then store the rest = header of fragment data and half of payload.
				*/
                if ((data.Length - cursor) < length) break;

                if (length != 0)
                {
                    var payload = new byte[length];
                    Array.Copy(data, cursor, payload, 0, payload.Length);
                }

                opCodeAndPayloadIndexies.Add(new OpCodeAndPayloadIndex(opCode, cursor, length));

                cursor = cursor + length;
            }

            return opCodeAndPayloadIndexies;
        }

        public struct OpCodeAndPayloadIndex
        {
            public readonly byte opCode;
            public readonly uint start;
            public readonly uint length;
            public OpCodeAndPayloadIndex(byte opCode, uint start, uint length)
            {
                this.opCode = opCode;
                this.start = start;
                this.length = length;
            }
        }

        private static RNGCryptoServiceProvider randomGen = new RNGCryptoServiceProvider();

        public static string GenerateExpectedAcceptedKey(string baseStr)
        {
            var concat = (baseStr.TrimEnd() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
            var sha1d = new SHA1CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(concat));
            return Convert.ToBase64String(sha1d);
        }

        public static string GeneratePrivateBase64Key()
        {
            var src = new byte[16];
            randomGen.GetBytes(src);
            return Convert.ToBase64String(src);
        }

        public static byte[] NewMaskKey()
        {
            var maskingKeyBytes = new byte[4];
            randomGen.GetBytes(maskingKeyBytes);
            return maskingKeyBytes;
        }

    }
}