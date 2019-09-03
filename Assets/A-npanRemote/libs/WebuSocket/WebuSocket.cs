using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace WebuSocketCore
{

    public enum SocketState
    {
        EMPTY,
        CONNECTING,
        TLS_HANDSHAKING,
        TLS_HANDSHAKE_DONE,
        WS_HANDSHAKING,
        OPENED,
        CLOSING,
        CLOSED
    }

    public enum WebuSocketCloseEnum
    {
        CLOSED_FORCIBLY,
        CLOSED_GRACEFULLY,
        CLOSED_BY_SERVER,
        CLOSED_BY_TIMEOUT,
    }

    public enum WebuSocketErrorEnum
    {
        UNKNOWN_ERROR,
        DOMAIN_UNRESOLVED,
        CONNECTION_FAILED,
        TLS_HANDSHAKE_FAILED,
        TLS_ERROR,
        WS_HANDSHAKE_FAILED,
        WS_HANDSHAKE_KEY_UNMATCHED,
        SEND_FAILED,
        PING_FAILED,
        PONG_FAILED,
        RECEIVE_FAILED,
        CONNECTING,
        ALREADY_DISCONNECTED,
    }

    public class WebuSocket
    {
        public const int DEFAULT_TIMEOUT_SEC = 10;

        /**
			create new WebuSocket instance from source WebuSocket instance.
		*/
        public static WebuSocket Reconnect(WebuSocket sourceSocket)
        {
            return new WebuSocket(
                sourceSocket.url,
                sourceSocket.baseReceiveBufferSize,
                sourceSocket.OnConnected,
                sourceSocket.OnMessage,
                sourceSocket.OnPinged,
                sourceSocket.OnClosed,
                sourceSocket.OnError,
                sourceSocket.additionalHeaderParams
            );
        }

        private EndPoint endPoint;

        private const string CRLF = "\r\n";
        private const string WEBSOCKET_VERSION = "13";


        private SocketToken socketToken;

        public readonly string webSocketConnectionId;

        public class SocketToken
        {
            public SocketState socketState;
            public WebuSocketCloseEnum closeReason;
            public readonly Socket socket;

            public byte[] receiveBuffer;

            public readonly SocketAsyncEventArgs connectArgs;
            public readonly SocketAsyncEventArgs sendArgs;
            public readonly SocketAsyncEventArgs receiveArgs;

            public SocketToken(Socket socket, int bufferLen, SocketAsyncEventArgs connectArgs, SocketAsyncEventArgs sendArgs, SocketAsyncEventArgs receiveArgs)
            {
                this.socket = socket;

                this.receiveBuffer = new byte[bufferLen];

                this.connectArgs = connectArgs;
                this.sendArgs = sendArgs;
                this.receiveArgs = receiveArgs;

                this.connectArgs.UserToken = this;
                this.sendArgs.UserToken = this;
                this.receiveArgs.UserToken = this;

                this.receiveArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
            }

            /*
				default constructor.
			*/
            public SocketToken()
            {
                this.socketState = SocketState.EMPTY;
            }
        }

        /*
			WebuSocket basement parameters.
		*/
        public readonly string url;
        public readonly int baseReceiveBufferSize;

        public readonly Action OnConnected;
        public readonly Action OnPinged;
        public readonly Action<Queue<ArraySegment<byte>>> OnMessage;
        public readonly Action<WebuSocketCloseEnum> OnClosed;
        public readonly Action<WebuSocketErrorEnum, Exception> OnError;

        public readonly Dictionary<string, string> additionalHeaderParams;


        /*
			temporary parameters.
		*/
        private readonly string base64Key;

        private readonly bool isWss;
        private readonly Encryption.WebuSocketTlsClientProtocol tlsClientProtocol;

        private readonly byte[] websocketHandshakeRequestBytes;

        public WebuSocket(
            string url,
            int baseReceiveBufferSize,
            Action OnConnected = null,
            Action<Queue<ArraySegment<byte>>> OnMessage = null,
            Action OnPinged = null,
            Action<WebuSocketCloseEnum> OnClosed = null,
            Action<WebuSocketErrorEnum, Exception> OnError = null,
            Dictionary<string, string> additionalHeaderParams = null
        )
        {
            this.webSocketConnectionId = Guid.NewGuid().ToString();

            this.url = url;
            this.baseReceiveBufferSize = baseReceiveBufferSize;

            this.OnConnected = OnConnected;
            this.OnMessage = OnMessage;
            this.OnPinged = OnPinged;
            this.OnClosed = OnClosed;
            this.OnError = OnError;

            this.additionalHeaderParams = additionalHeaderParams;

            this.base64Key = WebSocketByteGenerator.GeneratePrivateBase64Key();

            var uri = new Uri(url);

            var host = uri.Host;
            var scheme = uri.Scheme;
            var port = uri.Port;
            var path = uri.LocalPath;
            var userInfo = uri.UserInfo;

            /*
				set default port.
			*/
            if (port == -1)
            {
                if (scheme == "wss")
                {
                    port = 443;
                }
                else
                {
                    port = 80;
                }
            }

            // check if dns or ip.
            var isDns = (uri.HostNameType == UriHostNameType.Dns);

            // ready tls machine for wss.
            if (scheme == "wss")
            {
                this.isWss = true;
                this.tlsClientProtocol = new Encryption.WebuSocketTlsClientProtocol();
                tlsClientProtocol.Connect(new Encryption.WebuSocketTlsClient(TLSHandshakeDone, TLSHandleError));
            }

            var requestHeaderParams = new Dictionary<string, string> {
                {"Host", isDns ? uri.DnsSafeHost : uri.Authority},
                {"Upgrade", "websocket"},
                {"Connection", "Upgrade"},
                {"Sec-WebSocket-Key", base64Key},
                {"Sec-WebSocket-Version", WEBSOCKET_VERSION}
            };

            /*
				basic auth.
			*/
            if (!string.IsNullOrEmpty(userInfo))
            {
                requestHeaderParams["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userInfo));
            }

            if (additionalHeaderParams != null)
            {
                foreach (var key in additionalHeaderParams.Keys) requestHeaderParams[key] = additionalHeaderParams[key];
            }

            /*
				construct http request bytes data.
			*/
            var requestData = new StringBuilder();
            {
                requestData.AppendFormat("GET {0} HTTP/1.1{1}", path, CRLF);
                foreach (var key in requestHeaderParams.Keys) requestData.AppendFormat("{0}: {1}{2}", key, requestHeaderParams[key], CRLF);
                requestData.Append(CRLF);
            }

            this.websocketHandshakeRequestBytes = Encoding.UTF8.GetBytes(requestData.ToString());

            if (isDns)
            {
                // initialize socketToken with empty state.
                this.socketToken = new SocketToken();

                Dns.BeginGetHostEntry(
                    host,
                    new AsyncCallback(
                        result =>
                        {
                            IPHostEntry addresses = null;

                            try
                            {
                                addresses = Dns.EndGetHostEntry(result);
                            }
                            catch (Exception e)
                            {
                                if (OnError != null)
                                {
                                    OnError(WebuSocketErrorEnum.DOMAIN_UNRESOLVED, e);
                                }
                                Disconnect();
                                return;
                            }

                            if (addresses.AddressList.Length == 0)
                            {
                                if (OnError != null)
                                {
                                    var domainUnresolvedException = new Exception("failed to resolve domain.");
                                    OnError(WebuSocketErrorEnum.DOMAIN_UNRESOLVED, domainUnresolvedException);
                                }
                                Disconnect();
                                return;
                            }

                            // choose valid ip.
                            foreach (IPAddress ipaddress in addresses.AddressList)
                            {
                                if (ipaddress.AddressFamily == AddressFamily.InterNetwork || ipaddress.AddressFamily == AddressFamily.InterNetworkV6)
                                {
                                    this.endPoint = new IPEndPoint(ipaddress, port);
                                    StartConnectAsync();
                                    return;
                                }
                            }

                            if (OnError != null)
                            {
                                var unresolvedAddressFamilyException = new Exception("failed to get valid address family from list.");
                                OnError(WebuSocketErrorEnum.DOMAIN_UNRESOLVED, unresolvedAddressFamilyException);
                            }
                            Disconnect();
                        }
                    ),
                    this
                );
                return;
            }

            // raw ip.
            this.endPoint = new IPEndPoint(IPAddress.Parse(host), port);
            StartConnectAsync();
        }

        private void TLSHandshakeDone()
        {
            switch (socketToken.socketState)
            {
                case SocketState.TLS_HANDSHAKING:
                    {
                        socketToken.socketState = SocketState.TLS_HANDSHAKE_DONE;
                        break;
                    }
                default:
                    {
                        if (OnError != null)
                        {
                            var error = new Exception("tls handshake failed in unexpected state.");
                            OnError(WebuSocketErrorEnum.TLS_HANDSHAKE_FAILED, error);
                        }
                        Disconnect();
                        break;
                    }
            }
        }

        private void TLSHandleError(Exception e, string errorMessage)
        {
            if (OnError != null)
            {
                if (e == null) e = new Exception("tls error:" + errorMessage);
                OnError(WebuSocketErrorEnum.TLS_ERROR, e);
            }
            Disconnect();
        }

        private void StartConnectAsync()
        {
            var clientSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.NoDelay = true;
            clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            var connectArgs = new SocketAsyncEventArgs();
            connectArgs.AcceptSocket = clientSocket;
            connectArgs.RemoteEndPoint = endPoint;
            connectArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnect);

            var sendArgs = new SocketAsyncEventArgs();
            sendArgs.AcceptSocket = clientSocket;
            sendArgs.RemoteEndPoint = endPoint;
            sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);

            var receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.AcceptSocket = clientSocket;
            receiveArgs.RemoteEndPoint = endPoint;
            receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceived);

            socketToken = new SocketToken(clientSocket, baseReceiveBufferSize, connectArgs, sendArgs, receiveArgs);
            socketToken.socketState = SocketState.CONNECTING;

            // start connect.
            if (!clientSocket.ConnectAsync(socketToken.connectArgs)) OnConnect(clientSocket, connectArgs);
        }
        private byte[] webSocketHandshakeResult;
        private void OnConnect(object unused, SocketAsyncEventArgs args)
        {
            var token = (SocketToken)args.UserToken;
            switch (token.socketState)
            {
                case SocketState.CONNECTING:
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            token.socketState = SocketState.CLOSED;

                            if (OnError != null)
                            {
                                var error = new Exception("connect error:" + args.SocketError.ToString());
                                OnError(WebuSocketErrorEnum.CONNECTION_FAILED, error);
                            }
                            return;
                        }

                        if (isWss)
                        {
                            SendTLSHandshake(token);
                            return;
                        }

                        SendWSHandshake(token);
                        return;
                    }
                default:
                    {
                        // unexpected error, should fall this connection.
                        if (OnError != null)
                        {
                            var error = new Exception("unexpcted connection state error.");
                            OnError(WebuSocketErrorEnum.CONNECTION_FAILED, error);
                        }
                        Disconnect();
                        return;
                    }
            }
        }

        private void SendTLSHandshake(SocketToken token)
        {
            token.socketState = SocketState.TLS_HANDSHAKING;

            // ready receive.
            ReadyReceivingNewData(token);

            // first, send clientHello to server.
            // get ClientHello byte data from tlsClientProtocol instance and send it to server.
            var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
            tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

            token.sendArgs.SetBuffer(buffer, 0, buffer.Length);
            if (!token.socket.SendAsync(token.sendArgs)) OnSend(token.socket, token.sendArgs);
        }

        private void SendWSHandshake(SocketToken token)
        {
            token.socketState = SocketState.WS_HANDSHAKING;

            ReadyReceivingNewData(token);

            if (isWss)
            {
                tlsClientProtocol.OfferOutput(websocketHandshakeRequestBytes, 0, websocketHandshakeRequestBytes.Length);

                var count = tlsClientProtocol.GetAvailableOutputBytes();
                var buffer = new byte[count];
                tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

                token.sendArgs.SetBuffer(buffer, 0, buffer.Length);
            }
            else
            {
                token.sendArgs.SetBuffer(websocketHandshakeRequestBytes, 0, websocketHandshakeRequestBytes.Length);
            }

            if (!token.socket.SendAsync(token.sendArgs)) OnSend(token.socket, token.sendArgs);
        }

        private void OnDisconnected(object unused, SocketAsyncEventArgs args)
        {
            var token = (SocketToken)args.UserToken;
            switch (token.socketState)
            {
                case SocketState.CLOSED:
                    {
                        // do nothing.
                        break;
                    }
                default:
                    {
                        lock (lockObj)
                        {
                            token.socketState = SocketState.CLOSED;

                            try
                            {
                                token.socket.Close();
                            }
                            catch
                            {
                                // do nothing.
                            }

                            if (OnClosed != null)
                            {
                                if (token.closeReason != WebuSocketCloseEnum.CLOSED_GRACEFULLY) OnClosed(token.closeReason);
                                else OnClosed(WebuSocketCloseEnum.CLOSED_GRACEFULLY);
                            }
                        }
                        break;
                    }
            }
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
                        Debug.Log("送り出したエラー:" + socketError.ToString());
                        if (OnError != null)
                        {
                            var error = new Exception("send error:" + socketError.ToString());
                            OnError(WebuSocketErrorEnum.SEND_FAILED, error);
                        }
                        Disconnect();
                        break;
                    }
            }
        }

        private object lockObj = new object();


        /*
			buffers.
		*/
        private byte[] wsBuffer;
        private int wsBufIndex;
        private int wsBufLength;


        private void OnReceived(object unused, SocketAsyncEventArgs args)
        {
            var token = (SocketToken)args.UserToken;

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
                                // show error, then close or continue receiving.
                                if (OnError != null)
                                {
                                    var error = new Exception("receive error:" + args.SocketError.ToString() + " size:" + args.BytesTransferred);
                                    OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
                                }
                                Disconnect();
                                return;
                            }
                    }
                }
            }

            if (args.BytesTransferred == 0)
            {
                if (OnError != null)
                {
                    var error = new Exception("failed to receive. args.BytesTransferred = 0." + " args.SocketError:" + args.SocketError);
                    OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
                }
                Disconnect();
                return;
            }

            switch (token.socketState)
            {
                case SocketState.TLS_HANDSHAKING:
                    {
                        // set received data to tlsClientProtocol by "OfferInput" method.
                        // tls handshake phase will progress.
                        tlsClientProtocol.OfferInputBytes(args.Buffer, args.BytesTransferred);

                        // state is changed to TLS_HANDSHAKE_DONE if tls handshake is done inside tlsClientProtocol.
                        if (token.socketState == SocketState.TLS_HANDSHAKE_DONE)
                        {
                            SendWSHandshake(token);
                            return;
                        }

                        /*
                            continue handshaking.
                        */

                        var outputBufferSize = tlsClientProtocol.GetAvailableOutputBytes();

                        if (outputBufferSize == 0)
                        {
                            // ready receive next data.
                            ReadyReceivingNewData(token);
                            return;
                        }

                        // next tls handshake data is ready inside tlsClientProtocol.
                        var buffer = new byte[outputBufferSize];
                        tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

                        // ready receive next data.
                        ReadyReceivingNewData(token);

                        // send.
                        token.sendArgs.SetBuffer(buffer, 0, buffer.Length);
                        if (!token.socket.SendAsync(token.sendArgs)) OnSend(token.socket, token.sendArgs);
                        return;
                    }
                case SocketState.WS_HANDSHAKING:
                    {
                        var receivedData = new byte[args.BytesTransferred];
                        Buffer.BlockCopy(args.Buffer, 0, receivedData, 0, receivedData.Length);

                        if (isWss)
                        {
                            tlsClientProtocol.OfferInput(receivedData);
                            if (0 < tlsClientProtocol.GetAvailableInputBytes())
                            {
                                var index = 0;
                                var length = tlsClientProtocol.GetAvailableInputBytes();
                                if (webSocketHandshakeResult == null)
                                {
                                    webSocketHandshakeResult = new byte[length];
                                }
                                else
                                {
                                    index = webSocketHandshakeResult.Length;
                                    // already hold some bytes, and should expand for holding more decrypted data.
                                    Array.Resize(ref webSocketHandshakeResult, webSocketHandshakeResult.Length + length);
                                }

                                tlsClientProtocol.ReadInput(webSocketHandshakeResult, index, length);
                            }

                            // failed to get tls decrypted data from current receiving data.
                            // continue receiving next data at the end of this case.
                        }
                        else
                        {
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
                        }

                        if (0 < webSocketHandshakeResult.Length)
                        {
                            var lineEndCursor = ReadUpgradeLine(webSocketHandshakeResult, 0, webSocketHandshakeResult.Length);
                            if (lineEndCursor != -1)
                            {
                                try
                                {
                                    var protocolData = new SwitchingProtocolData(Encoding.UTF8.GetString(webSocketHandshakeResult, 0, lineEndCursor));
                                    var expectedKey = WebSocketByteGenerator.GenerateExpectedAcceptedKey(base64Key);
                                    if (protocolData.securityAccept != expectedKey)
                                    {
                                        if (OnError != null)
                                        {
                                            var error = new Exception("WebSocket Key Unmatched.");
                                            OnError(WebuSocketErrorEnum.WS_HANDSHAKE_KEY_UNMATCHED, error);
                                        }
                                        Disconnect();
                                        return;
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (OnError != null)
                                    {
                                        OnError(WebuSocketErrorEnum.WS_HANDSHAKE_FAILED, e);
                                    }
                                    Disconnect();
                                    return;
                                }

                                token.socketState = SocketState.OPENED;
                                if (OnConnected != null) OnConnected();


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
                        if (isWss)
                        {
                            // write input to tls buffer.
                            tlsClientProtocol.OfferInputBytes(args.Buffer, args.BytesTransferred);

                            if (0 < tlsClientProtocol.GetAvailableInputBytes())
                            {
                                var additionalLen = tlsClientProtocol.GetAvailableInputBytes();

                                if (wsBuffer.Length < wsBufIndex + additionalLen)
                                {
                                    Array.Resize(ref wsBuffer, wsBufIndex + additionalLen);
                                    // resizeイベント発生をどう出すかな〜〜
                                }

                                // transfer bytes from tls buffer to wsBuffer.
                                tlsClientProtocol.ReadInput(wsBuffer, wsBufIndex, additionalLen);

                                wsBufLength = wsBufLength + additionalLen;
                            }
                            else
                            {
                                // received incomlete tls bytes, continue.
                                ReadyReceivingNewData(token);
                                return;
                            }
                        }
                        else
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
                        var error = new Exception("fatal error, could not detect error, receive condition is strange, token.socketState:" + token.socketState);
                        if (OnError != null) OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
                        Disconnect();
                        return;
                    }
            }
        }

        /**
			consume buffered data.
		*/
        private void ReadBuffer(SocketToken token)
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


        private void ReadyReceivingNewData(SocketToken token)
        {
            token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
            if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
        }

        public void Disconnect(WebuSocketCloseEnum closeReason = WebuSocketCloseEnum.CLOSED_FORCIBLY)
        {
            lock (lockObj)
            {
                switch (socketToken.socketState)
                {
                    case SocketState.CLOSING:
                    case SocketState.CLOSED:
                        {
                            // do nothing
                            break;
                        }
                    default:
                        {
                            socketToken.socketState = SocketState.CLOSING;
                            if (closeReason != WebuSocketCloseEnum.CLOSED_FORCIBLY) socketToken.closeReason = closeReason;
                            StartCloseAsync();
                            break;
                        }
                }
            }
        }

        private void StartCloseAsync()
        {
            var closeEventArgs = new SocketAsyncEventArgs();
            closeEventArgs.UserToken = socketToken;
            closeEventArgs.AcceptSocket = socketToken.socket;

            var closeData = WebSocketByteGenerator.CloseData();

            if (isWss)
            {
                tlsClientProtocol.OfferOutput(closeData, 0, closeData.Length);

                var count = tlsClientProtocol.GetAvailableOutputBytes();
                var buffer = new byte[count];
                tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

                closeEventArgs.SetBuffer(buffer, 0, buffer.Length);
            }
            else
            {
                closeEventArgs.SetBuffer(closeData, 0, closeData.Length);
            }

            closeEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnDisconnected);

            if (!socketToken.socket.SendAsync(closeEventArgs)) OnDisconnected(socketToken.socket, closeEventArgs);
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


        public class SwitchingProtocolData
        {
            // HTTP/1.1 101 Switching Protocols
            // Server: nginx/1.7.10
            // Date: Sun, 22 May 2016 18:31:47 GMT
            // Connection: upgrade
            // Upgrade: websocket
            // Sec-WebSocket-Accept: C3HoL/ER1LOnEj8yVINdXluouHw=

            public string protocolDesc;
            public string httpResponseCode;
            public string httpMessage;
            public string serverInfo;
            public string date;
            public string connectionType;
            public string upgradeMethod;
            public string securityAccept;

            public SwitchingProtocolData(string source)
            {
                var acceptedResponseHeaderKeyValues = source.Split('\n');
                foreach (var line in acceptedResponseHeaderKeyValues)
                {
                    if (line.StartsWith("HTTP"))
                    {
                        var httpResponseHeaderSplitted = line.Split(' ');
                        this.protocolDesc = httpResponseHeaderSplitted[0];
                        this.httpResponseCode = httpResponseHeaderSplitted[1];
                        this.httpMessage = httpResponseHeaderSplitted[2] + httpResponseHeaderSplitted[3];
                        continue;
                    }

                    if (!line.Contains(": ")) continue;

                    var keyAndValue = line.Replace(": ", ":").Split(':');
                    switch (keyAndValue[0].ToLower())
                    {
                        case "server":
                            {
                                this.serverInfo = keyAndValue[1];
                                break;
                            }
                        case "date":
                            {
                                this.date = keyAndValue[1];
                                break;
                            }
                        case "connection":
                            {
                                this.connectionType = keyAndValue[1];
                                break;
                            }
                        case "upgrade":
                            {
                                this.upgradeMethod = keyAndValue[1];
                                break;
                            }
                        case "sec-websocket-accept":
                            {
                                this.securityAccept = keyAndValue[1].TrimEnd();
                                break;
                            }
                        default:
                            {
                                throw new Exception("invalid key value found. line:" + line);
                            }
                    }
                }
            }
        }

        private Queue<ArraySegment<byte>> receivedDataSegments = new Queue<ArraySegment<byte>>();
        private byte[] continuationBuffer;
        private int continuationBufferIndex;
        private WebuSocketResults ScanBuffer(byte[] buffer, long bufferLength)
        {
            receivedDataSegments.Clear();

            int cursor = 0;
            int lastDataEnd = 0;
            while (cursor < bufferLength)
            {

                // first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
                var opCode = (byte)(buffer[cursor++] & WebSocketByteGenerator.OPFilter);

                // second byte = mask(1), length(7)
                if (bufferLength < cursor) break;

                /*
					mask of data from server is definitely zero(0).
					ignore reading mask bit.
				*/
                int length = buffer[cursor++];
                switch (length)
                {
                    case 126:
                        {
                            // next 2 byte is length data.
                            if (bufferLength < cursor + 2) break;

                            length = (
                                (buffer[cursor++] << 8) +
                                (buffer[cursor++])
                            );
                            break;
                        }
                    case 127:
                        {
                            // next 8 byte is length data.
                            if (bufferLength < cursor + 8) break;

                            length = (
                                (buffer[cursor++] << (8 * 7)) +
                                (buffer[cursor++] << (8 * 6)) +
                                (buffer[cursor++] << (8 * 5)) +
                                (buffer[cursor++] << (8 * 4)) +
                                (buffer[cursor++] << (8 * 3)) +
                                (buffer[cursor++] << (8 * 2)) +
                                (buffer[cursor++] << 8) +
                                (buffer[cursor++])
                            );
                            break;
                        }
                    default:
                        {
                            // other.
                            break;
                        }
                }

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
                            Buffer.BlockCopy(buffer, cursor, continuationBuffer, continuationBufferIndex, length);
                            continuationBufferIndex += length;
                            break;
                        }
                    case WebSocketByteGenerator.OP_TEXT:
                    case WebSocketByteGenerator.OP_BINARY:
                        {
                            if (continuationBufferIndex == 0) receivedDataSegments.Enqueue(new ArraySegment<byte>(buffer, cursor, length));
                            else
                            {
                                if (continuationBuffer.Length <= continuationBufferIndex + length) Array.Resize(ref continuationBuffer, continuationBufferIndex + length);
                                Buffer.BlockCopy(buffer, cursor, continuationBuffer, continuationBufferIndex, length);
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
                                if server sent ping data with application data, open it.
                            */
                            if (0 < length)
                            {
                                var data = new byte[length];
                                Buffer.BlockCopy(buffer, cursor, data, 0, length);
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
                                if server sent pong with application data, open it.
                            */
                            if (0 < length)
                            {
                                var data = new byte[length];
                                Buffer.BlockCopy(buffer, cursor, data, 0, length);
                                PongReceived(data);
                            }
                            else
                            {
                                PongReceived();
                            }
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
        private byte[] sendingPingData;
        private byte[] ignoringPingData = new byte[0];
        private Action _OnPonged;
        public void Ping(Action<int> onPonged, byte[] data = null)
        {
            if (timeoutCheckCoroutine != null)
            {
                // すでに動いていたら、現在動いているping sendedの返答が帰ってくるのを待っている。
                // これをignoreする必要がある。
                // timeoutCheckCoroutineの初期化とpingがセットになっているので、現状sendingPingDataは必ずnullではない。
                // IsConnectedを実行するのが先か、Pingを実行するのが先か、という感じになる。
                ignoringPingData = sendingPingData;
            }

            // force renew timeout-check-coroutine.
            timeoutCheckCoroutine = GenerateTimeoutCheckCoroutine(data, onPonged);

            // update coroutine. first time = send ping data to server.
            UpdateTimeoutCoroutine();
        }

        private void _Ping(Action _onPonged, byte[] data = null)
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
                if (OnError != null) OnError(ev, error);
                return;
            }

            /*
				update onPong handler. this handler will be fired at most once.
			*/
            this._OnPonged = _onPonged;
            var pingBytes = WebSocketByteGenerator.Ping(data);

            if (isWss)
            {
                tlsClientProtocol.OfferOutput(pingBytes, 0, pingBytes.Length);

                var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
                tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

                try
                {
                    socketToken.socket.BeginSend(
                        buffer,
                        0,
                        buffer.Length,
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "ping failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.PING_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.PING_FAILED, e);
                    }
                    Disconnect();
                }
            }
            else
            {
                try
                {
                    socketToken.socket.BeginSend(
                        pingBytes,
                        0,
                        pingBytes.Length,
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "ping failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.PING_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.PING_FAILED, e);
                    }
                    Disconnect();
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
                if (OnError != null) OnError(ev, error);
                return;
            }

            var payloadBytes = WebSocketByteGenerator.SendBinaryData(data);
            // for (var i = 0; i < Math.Min(payloadBytes.Length, 10); i++)
            // {
            //     Debug.Log("send i:" + payloadBytes[i]);
            // }

            if (isWss)
            {
                tlsClientProtocol.OfferOutput(payloadBytes, 0, payloadBytes.Length);

                var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
                tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

                try
                {
                    socketToken.socket.BeginSend(
                        buffer,
                        0,
                        buffer.Length,
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "send failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.SEND_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.SEND_FAILED, e);
                    }
                    Disconnect();
                }
            }
            else
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "send failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.SEND_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    Debug.Log("送り出してる e:" + e);
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.SEND_FAILED, e);
                    }
                    Disconnect();
                }
            }
        }

        public void SendString(string data)
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
                if (OnError != null) OnError(ev, error);
                return;
            }

            var byteData = Encoding.UTF8.GetBytes(data);
            var payloadBytes = WebSocketByteGenerator.SendTextData(byteData);

            if (isWss)
            {
                tlsClientProtocol.OfferOutput(payloadBytes, 0, payloadBytes.Length);

                var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
                tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

                try
                {
                    socketToken.socket.BeginSend(
                        buffer,
                        0,
                        buffer.Length,
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "send failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.SEND_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.SEND_FAILED, e);
                    }
                    Disconnect();
                }
            }
            else
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "send failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.SEND_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.SEND_FAILED, e);
                    }
                    Disconnect();
                }
            }
        }

        private bool IsStateOpened()
        {
            if (socketToken.socketState != SocketState.OPENED) return false;
            return true;
        }

        /**
			CAUTION: this feature expects that Ping with application data will be returned by server as Pong with same application data.
			by https://tools.ietf.org/html/rfc6455#section-5.5.2

			this method is for checking websocket connectivity with timeout sec setting.
			it is good that call this method very constantly. because this check method is very lightweight.

			if you set a timeout of 2 second and call this method every 3 seconds, you can detect the timeout in 3 seconds.
			also if you set a timeout of X second and call this method every Y seconds, you can detect the timeout in Y seconds.

			this method calls Ping internally but interval of Ping is always larger than timeout sec.

			return true if waiting ping in timeout sec.
			return true if ping is returned in timeout sec.
			return true and send new ping if timeout sec passed.
			else, return false.
		*/
        private byte pingCount;
        public bool IsConnected(int newTimeoutSec = DEFAULT_TIMEOUT_SEC)
        {
            if (newTimeoutSec <= 0) return IsStateOpened();

            // state check.
            if (!IsStateOpened()) return false;

            // set new timeout.
            this.timeoutSec = newTimeoutSec;

            /*
				create coroutine for holding timeout checker if this method called faster than Ping.
			*/
            if (timeoutCheckCoroutine == null)
            {
                var data = new byte[] { pingCount };
                timeoutCheckCoroutine = GenerateTimeoutCheckCoroutine(data, _ => { });
            }

            return UpdateTimeoutCoroutine();
        }

        private bool UpdateTimeoutCoroutine()
        {
            // update coroutine.
            timeoutCheckCoroutine.MoveNext();
            var isInTimelimit = timeoutCheckCoroutine.Current;

            if (!isInTimelimit)
            {
                Disconnect(WebuSocketCloseEnum.CLOSED_BY_TIMEOUT);
            }

            return isInTimelimit;
        }
        private int timeoutSec;
        private int rttMilliSec;

        public int RttMilliseconds
        {
            get
            {
                return rttMilliSec;
            }
        }

        private IEnumerator<bool> timeoutCheckCoroutine;

        /**
			このメソッドは2つのルートで使われている。
			・IsConnected()で接続性チェックをするため
			・PingでPingデータを送るため

			ユーザーがPingデータを送付しようとした際、このCoroutineは常に新規作成され、Pingデータを直ちに送付し、その後サーバからPongが帰ってくる。
			そのままCoroutine自体は生き続ける。

			ユーザーがIsConnectedを実行した際、もしPingを打っていない状態だったら、Pingを送る。
			以降、このメソッドを実行するたびに、次のことが起きる。
				実行時、Ping送信から指定時間内だったら、trueを返す。
				実行時、指定時間外でPongが帰ってきていたら、trueを返しつつ、次のPingを送付する。
				実行時、指定時間外でまだPongが帰ってきていなかったら、falseを返し、Disconnectを行う。

			もしユーザーがIsConnected -> Pingの順で呼んだ場合、IsConnectedで送付したPingのPongは無視される。
				(送信済みのidentifyされたbyteが帰ってきても、無視する)
				Ping実行の際、Coroutineは新規作成されるので、以降のIsConnectedで利用される。

			もしユーザーがPing -> IsConnectedの順で呼んだ場合、PingでCoroutineは新規作成され、Ping中になるため、
				IsConnectedはtimeoutまでのあいだ常にtrueを返す。
		*/
        private IEnumerator<bool> GenerateTimeoutCheckCoroutine(byte[] firstPingData, Action<int> firstOnPong)
        {
            var waitingPong = true;
            var currentDate = DateTime.UtcNow;
            var limitTick = (TimeSpan.FromTicks(currentDate.Ticks) + TimeSpan.FromSeconds(timeoutSec)).Ticks;
            sendingPingData = firstPingData;
            _Ping(
                () =>
                {
                    this.rttMilliSec = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - currentDate.Ticks).Milliseconds;
                    waitingPong = false;
                    if (firstOnPong != null) firstOnPong(rttMilliSec);// call only first pong.
                },
                firstPingData
            );

            // return true at first time.
            yield return true;

            // second and later. called by IsConnected method.
            while (true)
            {
                /*
					if state is not OPENED, this connection is already disconnected.
				*/
                var isConnectedSync = IsStateOpened();
                if (!isConnectedSync)
                {
                    yield return false;
                    break;
                }

                /*
					continue if current sec does not reached to timelimit. 
				*/
                if (DateTime.UtcNow.Ticks < limitTick)
                {
                    yield return true;
                    continue;
                }

                /*
					if pong is not returned yet, that is timeout.
					(this method call is at least exceed the timeout.)
					this connection is already disconnected.
				*/
                if (waitingPong)
                {
                    yield return false;
                    break;
                }

                /*
					send next ping and timeout after timelimit.
				*/
                waitingPong = true;
                currentDate = DateTime.UtcNow;
                limitTick = (TimeSpan.FromTicks(currentDate.Ticks) + TimeSpan.FromSeconds(timeoutSec)).Ticks;

                /*
					set new ping data.
					data is 1 ~ byte.MaxValue.
				*/
                var data = new byte[] { ++pingCount };
                if (byte.MaxValue == pingCount)
                {
                    pingCount = 0;
                }

                sendingPingData = data;
                _Ping(
                    () =>
                    {
                        this.rttMilliSec = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - currentDate.Ticks).Milliseconds;
                        waitingPong = false;
                    },
                    data
                );

                yield return true;
                continue;
            }
        }

        private void CloseReceived()
        {
            lock (lockObj)
            {
                switch (socketToken.socketState)
                {
                    case SocketState.OPENED:
                        {
                            socketToken.socketState = SocketState.CLOSED;
                            if (OnClosed != null) OnClosed(WebuSocketCloseEnum.CLOSED_BY_SERVER);
                            Disconnect();
                            break;
                        }
                    default:
                        {

                            break;
                        }
                }
            }
        }

        private void PingReceived(byte[] data = null)
        {
            var pongBytes = WebSocketByteGenerator.Pong(data);

            if (isWss)
            {
                tlsClientProtocol.OfferOutput(pongBytes, 0, pongBytes.Length);

                var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
                tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

                try
                {
                    socketToken.socket.BeginSend(
                        buffer,
                        0,
                        buffer.Length,
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "pong failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.PONG_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.PONG_FAILED, e);
                    }
                    Disconnect();
                }
            }
            else
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
                                // send failed.
                                if (OnError != null)
                                {
                                    var error = new Exception("send error:" + "pong failed by unknown reason.");
                                    OnError(WebuSocketErrorEnum.PONG_FAILED, error);
                                }
                            }
                        },
                        socketToken.socket
                    );
                }
                catch (Exception e)
                {
                    if (OnError != null)
                    {
                        OnError(WebuSocketErrorEnum.PONG_FAILED, e);
                    }
                    Disconnect();
                }
            }

            if (OnPinged != null) OnPinged();
        }

        private void PongReceived(byte[] data = null)
        {
            if (data != null && ignoringPingData.Length == data.Length)
            {
                // check if data is perfectly same.
                for (var i = 0; i < ignoringPingData.Length; i++)
                {
                    /*
						data not matched. call pong once.
					*/
                    if (ignoringPingData[i] != data[i])
                    {
                        if (_OnPonged != null)
                        {
                            _OnPonged();
                            _OnPonged = null;
                        }
                        return;
                    }
                }
            }

            /*
				call pong once.
			*/
            if (_OnPonged != null)
            {
                _OnPonged();
                _OnPonged = null;
            }
        }
    }
}
