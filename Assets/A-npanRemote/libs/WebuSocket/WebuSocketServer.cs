using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace WebuSocketCore.Server
{
    public class ServerSocketToken
    {
        public SocketState socketState;
        public WebuSocketCloseEnum closeReason;
        public readonly Socket socket;

        public byte[] receiveBuffer;

        public readonly SocketAsyncEventArgs sendArgs;
        public readonly SocketAsyncEventArgs receiveArgs;

        public ServerSocketToken(Socket socket, int bufferLen, SocketAsyncEventArgs sendArgs, SocketAsyncEventArgs receiveArgs)
        {
            this.socket = socket;

            this.receiveBuffer = new byte[bufferLen];

            this.sendArgs = sendArgs;
            this.receiveArgs = receiveArgs;

            this.sendArgs.UserToken = this;
            this.receiveArgs.UserToken = this;

            this.receiveArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
        }

        /*
            default constructor.
        */
        public ServerSocketToken()
        {
            this.socketState = SocketState.EMPTY;
        }
    }

    public class ClientConnection
    {
        public readonly string connectionId;

        public Dictionary<string, string> RequestHeaderDict;
        private readonly ServerSocketToken socketToken;
        private readonly Action<ClientConnection> OnConnected;
        public Action<Queue<ArraySegment<byte>>> OnMessage;
        public Action CloseReceived;

        private readonly int baseReceiveBufferSize;

        private string clientSecret;

        public ClientConnection(string id, int baseReceiveBufferSize, Socket socket, Action<ClientConnection> onConnected)
        {
            this.connectionId = id;
            this.baseReceiveBufferSize = baseReceiveBufferSize;
            this.OnConnected = onConnected;

            var endPoint = socket.LocalEndPoint;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            var sendArgs = new SocketAsyncEventArgs();
            sendArgs.AcceptSocket = socket;
            sendArgs.RemoteEndPoint = endPoint;
            sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);

            var receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.AcceptSocket = socket;
            receiveArgs.RemoteEndPoint = endPoint;
            receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceived);

            socketToken = new ServerSocketToken(socket, baseReceiveBufferSize, sendArgs, receiveArgs);

            // handshake ready状態にセット
            socketToken.socketState = SocketState.WS_HANDSHAKING;

            // clientのhandshakeデータの受け取りを行う
            ReadyReceivingNewData(socketToken);
        }

        private void OnSend(object unused, SocketAsyncEventArgs args)
        {
            var socketError = args.SocketError;
            switch (socketError)
            {
                case SocketError.Success:
                    {
                        // do nothing.
                        break;
                    }
                default:
                    {
                        // if (OnError != null)
                        // {
                        //     var error = new Exception("send error:" + socketError.ToString());
                        //     OnError(WebuSocketErrorEnum.SEND_FAILED, error);
                        // }
                        // Disconnect();
                        break;
                    }
            }
        }

        private object lockObj = new object();


        private byte[] webSocketHandshakeResult;
        private byte[] wsBuffer;
        private int wsBufIndex;
        private int wsBufLength;



        private void OnReceived(object unused, SocketAsyncEventArgs args)
        {
            var token = (ServerSocketToken)args.UserToken;

            if (args.SocketError != SocketError.Success)
            {
                lock (lockObj)
                {
                    switch (token.socketState)
                    {
                        case SocketState.CLOSING:
                        case SocketState.CLOSED:
                            {
                                // already closing, ignore.
                                return;
                            }
                        default:
                            {
                                Debug.LogError("receive error:" + args.SocketError.ToString() + " size:" + args.BytesTransferred);
                                // // show error, then close or continue receiving.
                                // if (OnError != null)
                                // {
                                //     var error = new Exception("receive error:" + args.SocketError.ToString() + " size:" + args.BytesTransferred);
                                //     OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
                                // }
                                // Disconnect();
                                return;
                            }
                    }
                }
            }

            if (args.BytesTransferred == 0)
            {
                Debug.LogError("failed to receive. args.BytesTransferred = 0." + " args.SocketError:" + args.SocketError);
                // if (OnError != null)
                // {
                //     var error = new Exception("failed to receive. args.BytesTransferred = 0." + " args.SocketError:" + args.SocketError);
                //     OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
                // }
                // Disconnect();
                return;
            }

            switch (token.socketState)
            {
                case SocketState.WS_HANDSHAKING:
                    {
                        var receivedData = new byte[args.BytesTransferred];
                        Buffer.BlockCopy(args.Buffer, 0, receivedData, 0, receivedData.Length);


                        var index = 0;
                        var length = args.BytesTransferred;
                        if (webSocketHandshakeResult == null)
                        {
                            webSocketHandshakeResult = new byte[args.BytesTransferred];
                        }
                        else
                        {
                            index = webSocketHandshakeResult.Length;
                            // already hold some bytes, and should expand for holding more decrypted data.
                            Array.Resize(ref webSocketHandshakeResult, webSocketHandshakeResult.Length + length);
                        }
                        Buffer.BlockCopy(args.Buffer, 0, webSocketHandshakeResult, index, length);


                        if (0 < webSocketHandshakeResult.Length)
                        {
                            // clients.Add(new Client(stream, clientRequestHeaders, clientSecret));

                            var lineEndCursor = ReadUpgradeLine(webSocketHandshakeResult, 0, webSocketHandshakeResult.Length);
                            if (lineEndCursor != -1)
                            {
                                var baseStr = Encoding.UTF8.GetString(webSocketHandshakeResult);
                                var lines = baseStr.Replace("\r\n", "\n").Split('\n');

                                var clientRequestHeaders = new Dictionary<string, string>();
                                foreach (var line in lines)
                                {
                                    if (line.Length == 0 || string.IsNullOrEmpty(line))
                                    {
                                        continue;
                                    }

                                    // ignore fixed info.
                                    switch (line)
                                    {
                                        case "GET / HTTP/1.1":
                                        case "Upgrade: websocket":
                                        case "Connection: Upgrade":
                                        case "Sec-WebSocket-Version: 13":
                                            // ignore.
                                            break;
                                        default:
                                            if (line.StartsWith("Host:"))
                                            {
                                                continue;
                                            }

                                            if (line.StartsWith("Sec-WebSocket-Key: "))
                                            {
                                                clientSecret = line.Substring("Sec-WebSocket-Key: ".Length);
                                                continue;
                                            }

                                            // rest is request header parameters.
                                            if (line.Contains(":"))
                                            {
                                                var keyAndVal = line.Split(':');
                                                clientRequestHeaders[keyAndVal[0]] = keyAndVal[1];
                                            }
                                            break;
                                    }
                                }

                                if (string.IsNullOrEmpty(clientSecret))
                                {
                                    Debug.LogError("received connection is not valid.");
                                    return;
                                }

                                // foreach (var a in clientRequestHeaders)
                                // {
                                //     Debug.Log("a:" + a.Key + " v:" + a.Value);
                                // }

                                /*
                                    Sec-WebSocket-Key(key) の末尾の空白を覗いた値を準備
                                    key に固定値 "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" を連結
                                    sha1 を取得
                                    base64 に変換
                                 */

                                var acceptedSecret = WebSocketByteGenerator.GenerateExpectedAcceptedKey(clientSecret);

                                // generate response.
                                var responseStr =
@"HTTP/1.1 101 Switching Protocols
Server: webusocket
Date: " + DateTime.UtcNow +
@"Connection: upgrade
Upgrade: websocket
Sec-WebSocket-Accept: " + acceptedSecret + "\r\n\r\n";
                                var responseBytes = Encoding.UTF8.GetBytes(responseStr);

                                token.socket.BeginSend(
                                    responseBytes,
                                    0,
                                    responseBytes.Length,
                                    SocketFlags.None,
                                    result =>
                                    {
                                        var s = (Socket)result.AsyncState;
                                        var len = s.EndSend(result);

                                        var clientId = Guid.NewGuid().ToString();

                                        token.socketState = SocketState.OPENED;
                                        this.RequestHeaderDict = clientRequestHeaders;

                                        if (OnConnected != null)
                                        {
                                            OnConnected(this);
                                        }

                                        var afterHandshakeDataIndex = lineEndCursor + 1;// after last crlf.


                                        /*
                                            ready buffer data.
                                        */
                                        wsBuffer = new byte[baseReceiveBufferSize];
                                        wsBufIndex = 0;


                                        /*
                                            if end cursor of handshake is not equal to holded data length, received data is already contained.
                                        */
                                        if (webSocketHandshakeResult.Length == afterHandshakeDataIndex)
                                        {
                                            // no extra data exists.

                                            // ready for receiving websocket data.
                                            ReadyReceivingNewData(token);
                                            return;
                                        }
                                        Debug.LogError("なんかよくわからんけどここにいる");
                                        // else
                                        // {
                                        //     wsBufLength = webSocketHandshakeResult.Length - afterHandshakeDataIndex;

                                        //     if (wsBuffer.Length < wsBufLength)
                                        //     {
                                        //         Array.Resize(ref wsBuffer, wsBufLength);
                                        //     }

                                        //     Buffer.BlockCopy(webSocketHandshakeResult, afterHandshakeDataIndex, wsBuffer, 0, wsBufLength);

                                        //     ReadBuffer(token);
                                        // }
                                    },
                                    socketToken.socket
                                );
                                return;
                            }
                        }

                        // continue receiveing websocket handshake data.
                        ReadyReceivingNewData(token);
                        return;
                    }
                case SocketState.OPENED:
                    {
                        var additionalLen = args.BytesTransferred;

                        if (wsBuffer.Length < wsBufIndex + additionalLen)
                        {
                            Array.Resize(ref wsBuffer, wsBufIndex + additionalLen);
                            // resizeイベント発生をどう出すかな〜〜
                        }

                        Buffer.BlockCopy(args.Buffer, 0, wsBuffer, wsBufIndex, additionalLen);
                        wsBufLength = wsBufLength + additionalLen;

                        ReadBuffer(token);
                        return;
                    }
                default:
                    {
                        Debug.LogError("fatal error, could not detect error, receive condition is strange, token.socketState:" + token.socketState);
                        // var error = new Exception("fatal error, could not detect error, receive condition is strange, token.socketState:" + token.socketState);
                        // if (OnError != null) OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
                        // Disconnect();
                        return;
                    }
            }
        }

        public void Send(byte[] data)
        {
            if (socketToken.socketState != SocketState.OPENED)
            {
                WebuSocketErrorEnum ev = WebuSocketErrorEnum.UNKNOWN_ERROR;
                Exception error = null;
                switch (socketToken.socketState)
                {
                    case SocketState.TLS_HANDSHAKING:
                    case SocketState.WS_HANDSHAKING:
                        {
                            ev = WebuSocketErrorEnum.CONNECTING;
                            error = new Exception("send error:" + "not yet connected.");
                            break;
                        }
                    case SocketState.CLOSING:
                    case SocketState.CLOSED:
                        {
                            ev = WebuSocketErrorEnum.ALREADY_DISCONNECTED;
                            error = new Exception("send error:" + "connection was already closed. please create new connection by new WebuSocket().");
                            break;
                        }
                    default:
                        {
                            ev = WebuSocketErrorEnum.CONNECTING;
                            error = new Exception("send error:" + "not yet connected.");
                            break;
                        }
                }
                // if (OnError != null) OnError(ev, error);
                return;
            }

            var payloadBytes = WebSocketByteGenerator.SendBinaryData(data, false);

            {
                try
                {
                    socketToken.socket.BeginSend(
                        payloadBytes,
                        0,
                        payloadBytes.Length,
                        SocketFlags.None,
                        result =>
                        {
                            var s = (Socket)result.AsyncState;
                            var len = s.EndSend(result);
                            if (0 < len)
                            {
                                // do nothing.
                            }
                            else
                            {
                                Debug.LogError("send error:" + "send failed by unknown reason.");
                                // // send failed.
                                // if (OnError != null)
                                // {
                                //     var error = new Exception("send error:" + "send failed by unknown reason.");
                                //     OnError(WebuSocketErrorEnum.SEND_FAILED, error);
                                // }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    Debug.LogError("e:" + e);
                    // if (OnError != null)
                    // {
                    //     OnError(WebuSocketErrorEnum.SEND_FAILED, e);
                    // }
                    // Disconnect();
                }
            }
        }

        public static byte ByteCR = Convert.ToByte('\r');
        public static byte ByteLF = Convert.ToByte('\n');
        private int ReadUpgradeLine(byte[] bytes, int cursor, long length)
        {
            while (cursor < length)
            {
                if (4 < cursor &&
                    bytes[cursor - 3] == ByteCR &&
                    bytes[cursor - 2] == ByteLF &&
                    bytes[cursor - 1] == ByteCR &&
                    bytes[cursor] == ByteLF
                ) return cursor;// end point of linefeed.

                cursor++;
            }

            return -1;
        }

        private void ReadBuffer(ServerSocketToken token)
        {
            var result = ScanBuffer(wsBuffer, wsBufLength);

            // read completed datas.
            if (0 < result.segments.Count)
            {
                OnMessage(result.segments);
            }

            // if the last result index is matched to whole length, receive finished.
            if (result.lastDataTail == wsBufLength)
            {
                wsBufIndex = 0;
                wsBufLength = 0;
                ReadyReceivingNewData(token);
                return;
            }

            // unreadable data still exists in wsBuffer.
            var unreadDataLength = wsBufLength - result.lastDataTail;

            if (result.lastDataTail == 0)
            {
                // no data is read as WS data. 
                // this means the all data in wsBuffer is not enough to read as WS data yet.
                // need more data to add the last of wsBuffer.

                // set wsBufferIndex and wsBufLength to the end of current buffer.
                wsBufIndex = unreadDataLength;
                wsBufLength = unreadDataLength;
            }
            else
            {
                // not all of wsBuffer data is read as WS data.
                // data which is located before alreadyReadDataTail is already read.

                // move rest "unreaded" data to head of wsBuffer.
                Array.Copy(wsBuffer, result.lastDataTail, wsBuffer, 0, unreadDataLength);

                // then set wsBufIndex to 
                wsBufIndex = unreadDataLength;
                wsBufLength = unreadDataLength;
            }

            // should read rest.
            ReadyReceivingNewData(token);
        }

        private Queue<ArraySegment<byte>> receivedDataSegments = new Queue<ArraySegment<byte>>();
        private byte[] continuationBuffer;
        private int continuationBufferIndex = 0;
        private WebuSocketResults ScanBuffer(byte[] encedBuffer, long bufferLength)
        {
            receivedDataSegments.Clear();

            int cursor = 0;
            int lastDataEnd = 0;
            while (cursor < bufferLength)
            {
                // first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
                var opCode = (byte)(encedBuffer[cursor++] & WebSocketByteGenerator.OPFilter);

                // second byte = mask(1), length(7)
                if (bufferLength < cursor) break;

                // ignore mask bit.
                int length = encedBuffer[cursor++] & 0x7f;
                switch (length)
                {
                    case 126:
                        {
                            // next 2 byte is length data.
                            if (bufferLength < cursor + 2) break;

                            length = (
                                (encedBuffer[cursor++] << 8) +
                                (encedBuffer[cursor++])
                            );
                            break;
                        }
                    case 127:
                        {
                            // next 8 byte is length data.
                            if (bufferLength < cursor + 8) break;

                            length = (
                                (encedBuffer[cursor++] << (8 * 7)) +
                                (encedBuffer[cursor++] << (8 * 6)) +
                                (encedBuffer[cursor++] << (8 * 5)) +
                                (encedBuffer[cursor++] << (8 * 4)) +
                                (encedBuffer[cursor++] << (8 * 3)) +
                                (encedBuffer[cursor++] << (8 * 2)) +
                                (encedBuffer[cursor++] << 8) +
                                (encedBuffer[cursor++])
                            );
                            break;
                        }
                    default:
                        {
                            // other.
                            break;
                        }
                }

                // サーバ側はクライアントがセットしたdecKey分の4byteのデータを取り出す。
                if (bufferLength < cursor + 4) break;

                var decKey = new byte[4];
                decKey[0] = encedBuffer[cursor++];
                decKey[1] = encedBuffer[cursor++];
                decKey[2] = encedBuffer[cursor++];
                decKey[3] = encedBuffer[cursor++];

                // read payload data.
                if (bufferLength < cursor + length) break;

                // payload is fully contained!
                switch (opCode)
                {
                    case WebSocketByteGenerator.OP_CONTINUATION:
                        {
                            if (continuationBuffer == null) continuationBuffer = new byte[baseReceiveBufferSize];
                            if (continuationBuffer.Length <= continuationBufferIndex + length) Array.Resize(ref continuationBuffer, continuationBufferIndex + length);

                            // pool data to continuation buffer.
                            WebSocketByteGenerator.Unmasked(ref encedBuffer, decKey, cursor, length);
                            Buffer.BlockCopy(encedBuffer, cursor, continuationBuffer, continuationBufferIndex, length);
                            continuationBufferIndex += length;
                            break;
                        }
                    case WebSocketByteGenerator.OP_TEXT:
                    case WebSocketByteGenerator.OP_BINARY:
                        {
                            if (continuationBufferIndex == 0)
                            {
                                // unmask enced buffer.
                                WebSocketByteGenerator.Unmasked(ref encedBuffer, decKey, cursor, length);
                                receivedDataSegments.Enqueue(new ArraySegment<byte>(encedBuffer, cursor, length));
                            }
                            else
                            {
                                if (continuationBuffer.Length <= continuationBufferIndex + length) Array.Resize(ref continuationBuffer, continuationBufferIndex + length);

                                WebSocketByteGenerator.Unmasked(ref encedBuffer, decKey, cursor, length);
                                Buffer.BlockCopy(encedBuffer, cursor, continuationBuffer, continuationBufferIndex, length);
                                continuationBufferIndex += length;

                                receivedDataSegments.Enqueue(new ArraySegment<byte>(continuationBuffer, 0, continuationBufferIndex));

                                // reset continuationBuffer index.
                                continuationBufferIndex = 0;
                            }
                            break;
                        }
                    case WebSocketByteGenerator.OP_CLOSE:
                        {
                            CloseReceived();
                            break;
                        }
                    case WebSocketByteGenerator.OP_PING:
                        {
                            /*
                                if client sent ping data with application data, open it.
                            */
                            if (0 < length)
                            {
                                var data = new byte[length];
                                WebSocketByteGenerator.Unmasked(ref encedBuffer, decKey, cursor, length);
                                Buffer.BlockCopy(encedBuffer, cursor, data, 0, length);
                                PingReceived(data);
                            }
                            else
                            {
                                PingReceived();
                            }
                            break;
                        }
                    case WebSocketByteGenerator.OP_PONG:
                        {
                            /*
                                if client sent pong with application data, open it.
                            */
                            if (0 < length)
                            {
                                var data = new byte[length];
                                WebSocketByteGenerator.Unmasked(ref encedBuffer, decKey, cursor, length);
                                Buffer.BlockCopy(encedBuffer, cursor, data, 0, length);
                                // PongReceived(data);
                            }
                            else
                            {
                                // PongReceived();
                            }
                            Debug.LogError("pong received, but unable to handle it now.");
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }

                cursor = cursor + length;

                // set end of data.
                lastDataEnd = cursor;
            }

            // finally return payload data indexies.
            return new WebuSocketResults(receivedDataSegments, lastDataEnd);
        }

        private struct WebuSocketResults
        {
            public Queue<ArraySegment<byte>> segments;
            public int lastDataTail;

            public WebuSocketResults(Queue<ArraySegment<byte>> segments, int lastDataTail)
            {
                this.segments = segments;
                this.lastDataTail = lastDataTail;
            }
        }


        private void ReadyReceivingNewData(ServerSocketToken token)
        {
            token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
            if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
        }

        private void PingReceived(byte[] data = null)
        {
            var pongBytes = WebSocketByteGenerator.Pong(data, false);
            Debug.Log("pingがきたのでpongを。");
            {
                try
                {
                    socketToken.socket.BeginSend(
                        pongBytes,
                        0,
                        pongBytes.Length,
                        SocketFlags.None,
                        result =>
                        {
                            var s = (Socket)result.AsyncState;
                            var len = s.EndSend(result);
                            if (0 < len)
                            {
                                // do nothing.
                            }
                            else
                            {
                                Debug.LogError("send error:" + "pong failed by unknown reason.");
                                // // send failed.
                                // if (OnError != null)
                                // {
                                //     var error = new Exception("send error:" + "pong failed by unknown reason.");
                                //     OnError(WebuSocketErrorEnum.PONG_FAILED, error);
                                // }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    Debug.Log("e:" + e);
                    // if (OnError != null)
                    // {
                    //     OnError(WebuSocketErrorEnum.PONG_FAILED, e);
                    // }
                    // Disconnect();
                }
            }

            Debug.Log("ping受けたことを書いてない");
            // if (OnPinged != null) OnPinged();
        }


    }

    public class WebuSocketServer : IDisposable
    {
        private readonly Action<ClientConnection> OnConnected;
        public WebuSocketServer(
            int port,
            Action<ClientConnection> onConnected
        )
        {
            this.OnConnected = onConnected;
            StartServing(port);
        }

        private void StartServing(int port)
        {
            // tcpListenerを使い、tcpClientを受け付ける。が、最終的には内部のsocketを取り出して通信している。networkStreamがクソすぎる。
            var tcpListener = TcpListener.Create(port);
            tcpListener.Start();
            AcceptNewClient(tcpListener);
        }

        private void AcceptNewClient(TcpListener tcpListener)
        {
            tcpListener.AcceptTcpClientAsync().ContinueWith(
                tcpClientTask =>
                {
                    var client = tcpClientTask.Result.Client;
                    StartReading(client);

                    // 再帰で次のクライアントを受け付ける。
                    AcceptNewClient(tcpListener);
                }
            );
        }

        private void StartReading(Socket socket)
        {
            var connection = new ClientConnection(Guid.NewGuid().ToString(), 10240, socket, OnConnected);
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~WebuScoketServer()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}