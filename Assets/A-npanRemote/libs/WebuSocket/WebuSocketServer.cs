using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace WebuSocketCore.Server
{
    public class Client
    {
        public readonly NetworkStream stream;
        public readonly Dictionary<string, string> connectedRequestHeaders;
        public readonly string clientKey;

        public Client(NetworkStream stream, Dictionary<string, string> connectedRequestHeaders, string clientKey)
        {
            this.stream = stream;
            this.connectedRequestHeaders = connectedRequestHeaders;
            this.clientKey = clientKey;
        }
    }

    public class WebuSocketServer : IDisposable
    {
        private readonly Action<Dictionary<string, string>> OnConnected;
        private List<Client> clients = new List<Client>();

        public WebuSocketServer(
            int port,
            Action<Dictionary<string, string>> OnConnected = null,
            Action<Queue<ArraySegment<byte>>> OnMessage = null,
            Action<Queue<ArraySegment<string>>> OnStringMessage = null,
            Action OnPinged = null,
            Action<WebuSocketCloseEnum> OnClosed = null,
            Action<WebuSocketErrorEnum, Exception> OnError = null)
        {
            // localhost、connectedとreceived(byteとstring)と 、disconnectedとerrorを作りたい。
            this.OnConnected = OnConnected;

            StartServing(port);
        }

        private ServerSocketToken socketToken;

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

        private ServerSocketToken serverSocketToken;

        private void StartServing(int port)
        {
            // tcpListenerを使い、tcpClientを受け付ける。が、最終的には内部のsocketを取り出して通信している。networkStreamがクソすぎる。
            var tcpListener = TcpListener.Create(port);
            tcpListener.Start();
            AcceptNewClient(tcpListener);
        }

        private void AcceptNewClient(TcpListener tcpListener)
        {
            Debug.Log("接続待ち開始");
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

            serverSocketToken = new ServerSocketToken(socket, 1024, sendArgs, receiveArgs);
            serverSocketToken.socketState = SocketState.WS_HANDSHAKING;

            ReadyReceivingNewData(serverSocketToken);
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

                                var clientSecret = string.Empty;
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

                                Debug.Log("clientSecret:" + clientSecret);
                                foreach (var a in clientRequestHeaders)
                                {
                                    Debug.Log("a:" + a.Key + " v:" + a.Value);
                                }

                                /*
                                    Sec-WebSocket-Key(key) の末尾の空白を覗いた値を準備
                                    key に固定値 "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" を連結
                                    sha1 を取得
                                    base64 に変換
                                 */

                                var acceptedSecret = WebSocketByteGenerator.GenerateExpectedAcceptedKey(clientSecret);

                                // なんかレスポンスが必要なはず。
                                var responseStr =
@"HTTP/1.1 101 Switching Protocols
Server: webusocket
Date: " + DateTime.UtcNow +
@"Connection: upgrade
Upgrade: websocket
Sec-WebSocket-Accept: " + acceptedSecret + "\r\n\r\n";
                                var responseBytes = Encoding.UTF8.GetBytes(responseStr);
                                var result = token.socket.Send(responseBytes);

                                if (responseStr.Length != result)
                                {
                                    Debug.LogError("返答を入力できなかった");
                                    return;
                                }

                                Debug.Log("このclientを受け入れる。 result:" + result + " この辺に、このソケットからデータがきたらどうするか、このソケットにデータを送るにはどうするか、とかが必要なはず。");
                                // clients.Add(new Client());

                                token.socketState = SocketState.OPENED;
                                if (OnConnected != null)
                                {
                                    OnConnected(new Dictionary<string, string>());
                                }

                                var afterHandshakeDataIndex = lineEndCursor + 1;// after last crlf.


                                /*
                                    ready buffer data.
                                */
                                wsBuffer = new byte[1024];
                                wsBufIndex = 0;


                                /*
                                    if end cursor of handshake is not equal to holded data length, received data is already contained.
                                */
                                if (webSocketHandshakeResult.Length == afterHandshakeDataIndex)
                                {
                                    // no extra data exists.

                                    // ready for receiving websocket data.
                                    ReadyReceivingNewData(token);
                                }
                                else
                                {
                                    wsBufLength = webSocketHandshakeResult.Length - afterHandshakeDataIndex;

                                    if (wsBuffer.Length < wsBufLength)
                                    {
                                        Array.Resize(ref wsBuffer, wsBufLength);
                                    }

                                    Buffer.BlockCopy(webSocketHandshakeResult, afterHandshakeDataIndex, wsBuffer, 0, wsBufLength);

                                    ReadBuffer(token);
                                }
                                return;
                            }
                        }

                        // continue receiveing websocket handshake data.
                        ReadyReceivingNewData(token);
                        return;
                    }
                case SocketState.OPENED:
                    {
                        {
                            var additionalLen = args.BytesTransferred;

                            if (wsBuffer.Length < wsBufIndex + additionalLen)
                            {
                                Array.Resize(ref wsBuffer, wsBufIndex + additionalLen);
                                // resizeイベント発生をどう出すかな〜〜
                            }

                            Buffer.BlockCopy(args.Buffer, 0, wsBuffer, wsBufIndex, additionalLen);
                            wsBufLength = wsBufLength + additionalLen;
                        }

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

            var payloadBytes = WebSocketByteGenerator.SendBinaryData(data);

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
            Debug.Log("ReadBuffer!");
        }


        private void ReadyReceivingNewData(ServerSocketToken token)
        {
            token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
            if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
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