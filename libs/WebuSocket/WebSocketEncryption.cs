using System;
using WebuSocketEncryption.Org.BouncyCastle.Crypto.Tls;
using WebuSocketEncryption.Org.BouncyCastle.Security;
using WebuSocketEncryption.Org.BouncyCastle.Utilities;

namespace WebuSocketCore.Encryption
{
    public class WebuSocketTlsClientProtocol : TlsClientProtocol
    {
        /**
			non-blocking mode constructor of tlsClientProtocol.
		*/
        public WebuSocketTlsClientProtocol() : base(new SecureRandom()) { }

        private byte[] emptyByteBuffer = new byte[0];

        /**
			write byte datas to tls input without creating new partial buffer from exists buffer.
			this method does not require buffer copy for input.
		*/
        public void OfferInputBytes(byte[] bytes, int length)
        {
            /*
				changed for no-new buffer.
			*/
            mInputBuffers.Write(bytes, 0, length);
            base.OfferInput(emptyByteBuffer);
        }
    }

    public class WebuSocketTlsClient : DefaultTlsClient
    {

        internal TlsSession mSession;
        private readonly Action handshakeDone;
        private readonly Action<Exception, string> handleError;

        public WebuSocketTlsClient(Action handshakeDone, Action<Exception, string> handleError)
        {
            this.handshakeDone = handshakeDone;
            this.handleError = handleError;
            this.mSession = null;
        }

        public override TlsSession GetSessionToResume()
        {
            return this.mSession;
        }

        public override void NotifyAlertRaised(byte alertLevel, byte alertDescription, string message, Exception cause)
        {
            if (message != null)
            {
                handleError(null, message);
            }
            if (cause != null)
            {
                handleError(cause, string.Empty);
            }
        }

        public override void NotifyAlertReceived(byte alertLevel, byte alertDescription)
        {
            // Log("TLS client received alert: " + AlertLevel.GetText(alertLevel) + ", " + AlertDescription.GetText(alertDescription));
        }

        public override void NotifyServerVersion(ProtocolVersion serverVersion)
        {
            base.NotifyServerVersion(serverVersion);
        }

        public override TlsAuthentication GetAuthentication()
        {
            return new WebuSocketTlsAuthentication(mContext);
        }


        private class WebuSocketTlsAuthentication : TlsAuthentication
        {
			#pragma warning disable 414
            private readonly TlsContext mContext;
			#pragma warning restore 414
			
            internal WebuSocketTlsAuthentication(TlsContext context)
            {
                this.mContext = context;
            }

            public void NotifyServerCertificate(Certificate serverCertificate)
            {
                // X509CertificateStructure[] chain = serverCertificate.GetCertificateList();
                // Console.WriteLine("TLS client received server certificate chain of length " + chain.Length);
                // for (int i = 0; i != chain.Length; i++) {
                // 	X509CertificateStructure entry = chain[i];
                // 	// TODO Create fingerprint based on certificate signature algorithm digest
                // 	Console.WriteLine("    fingerprint:SHA-256 " + TlsTestUtilities.Fingerprint(entry) + " (" + entry.Subject + ")");
                // }
                // なんもしてない。certが正しいかどうか、チェックしないといけないはず。
            }

            public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
            {
                byte[] certificateTypes = certificateRequest.CertificateTypes;
                if (certificateTypes == null || !Arrays.Contains(certificateTypes, ClientCertificateType.rsa_sign))
                {
                    return null;
                }
                // return TlsTestUtilities.LoadSignerCredentials(mContext, certificateRequest.SupportedSignatureAlgorithms, SignatureAlgorithm.rsa, "x509-client.pem", "x509-client-key.pem");
                return null;
            }
        }

        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();

            TlsSession newSession = mContext.ResumableSession;
            if (newSession != null)
            {
                // byte[] newSessionID = newSession.SessionID;
                // string hex = Hex.ToHexString(newSessionID);

                // if (this.mSession != null && Arrays.AreEqual(this.mSession.SessionID, newSessionID)) {
                // 	Debug.LogError("Resumed session: " + hex);
                // } else {
                // 	Debug.LogError("Established session: " + hex);
                // }

                this.mSession = newSession;
            }

            handshakeDone();
        }
    }
}
