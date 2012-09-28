﻿using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;

namespace QuickFix
{
    /// <summary>
    /// TODO merge with SocketInitiatorThread
    /// </summary>
    public class SocketReader
    {
        public const int BUF_SIZE = 4096;
        private byte[] readBuffer_ = new byte[BUF_SIZE];
        private Parser parser_ = new Parser();
        private Session qfSession_ = null;
        private TcpClient tcpClient_;
        private ClientHandlerThread responder_;
        private SslStream sslStream_;

        public SocketReader(TcpClient tcpClient, ClientHandlerThread responder)
            :this(tcpClient, responder, null)
        { }

        public SocketReader(TcpClient tcpClient, ClientHandlerThread responder, SslStream sslStream)
        {
            tcpClient_ = tcpClient;
            responder_ = responder;
            sslStream_ = sslStream;
        }

        /// <summary> FIXME </summary>
        public void Read()
        {
            try
            {
                if (tcpClient_.Client != null && tcpClient_.Client.Poll(1000000, SelectMode.SelectRead)) // one-second timeout
                {
                    int bytesRead = -1;
                    if (sslStream_ != null)                
                        bytesRead = sslStream_.Read(readBuffer_, 0, readBuffer_.Length);    
                    else
                        bytesRead = tcpClient_.Client.Receive(readBuffer_);

                    if (bytesRead < 1)
                        throw new SocketException(System.Convert.ToInt32(SocketError.ConnectionReset));
                    parser_.AddToStream(System.Text.Encoding.UTF8.GetString(readBuffer_, 0, bytesRead));
                }
                else if (null != qfSession_)
                {
                    qfSession_.Next();
                }                    

                ProcessStream();
            }
            catch (MessageParseError e)
            {
                HandleException(qfSession_, e, tcpClient_);
            }
            catch (System.Exception e)
            {
                HandleException(qfSession_, e, tcpClient_);
            }
        }

        public void OnMessageFound(string msg)
        {
            ///Message fixMessage;

            try
            {
                if (null == qfSession_)
                {
                    qfSession_ = Session.LookupSession(Message.GetReverseSessionID(msg));
                    if (null == qfSession_)
                    {
                        string ErrorMsg = "ERROR: Disconnecting; received message for unknown session: " + msg;
                        this.Log(ErrorMsg, new Exception(ErrorMsg));
                        DisconnectClient();
                        return;
                    }
                    else
                    {
                        if (!HandleNewSession(msg))
                            return;
                    }
                }

                try
                {
                    qfSession_.Next(msg);
                }
                catch (System.Exception e)
                {
                    this.Log("Error on Session '" + qfSession_.SessionID + "': " + e.Message, e);
                }
            }
            catch (InvalidMessage e)
            {
                HandleBadMessage(msg, e);
            }
            catch (MessageParseError e)
            {
                HandleBadMessage(msg, e);
            }
        }

        protected void HandleBadMessage(string msg, Exception e)
        {
            try
            {
                if (Fields.MsgType.LOGON.Equals(Message.GetMsgType(msg)))
                {
                    this.Log("ERROR: Invalid LOGON message, disconnecting: " + e.Message, e);
                    DisconnectClient();
                }
                else
                {
                    this.Log("ERROR: Invalid message: " + e.Message, e);
                }
            }
            catch (InvalidMessage)
            { }
        }

        protected bool ReadMessage(out string msg)
        {
            try
            {
                return parser_.ReadFixMessage(out msg);
            }
            catch(MessageParseError e)
            {
                msg = "";
                throw e;
            }
        }

        protected void ProcessStream()
        {
            string msg;
            while (ReadMessage(out msg))
                OnMessageFound(msg);
        }

        protected static void DisconnectClient(TcpClient client, SslStream sslStream)
        {
            if (client.Client != null) client.Client.Close();
            if (sslStream != null) sslStream.Close();
            client.Close();
        }

        protected void DisconnectClient()
        {
            DisconnectClient(tcpClient_, sslStream_);
        }

        protected bool HandleNewSession(string msg)
	    {
		    if(qfSession_.HasResponder)
		    {
                qfSession_.Log.OnIncoming(msg);
                qfSession_.Log.OnEvent("Multiple logons/connections for this session are not allowed (" + tcpClient_.Client.RemoteEndPoint + ")");
			    qfSession_ = null;
                DisconnectClient();
			    return false;
		    }
		    qfSession_.Log.OnEvent(qfSession_.SessionID + " Socket Reader " + GetHashCode() + " accepting session " + qfSession_.SessionID + " from " + tcpClient_.Client.RemoteEndPoint);
            /// FIXME do this here? qfSession_.HeartBtInt = QuickFix.Fields.Converters.IntConverter.Convert(message.GetField(Fields.Tags.HeartBtInt)); /// FIXME
		    qfSession_.Log.OnEvent(qfSession_.SessionID +" Acceptor heartbeat set to " + qfSession_.HeartBtInt + " seconds");
		    qfSession_.SetResponder(responder_);
		    return true;
	    }

        public void HandleException(Session quickFixSession, System.Exception cause, TcpClient client)
        {
            bool disconnectNeeded = true;
            string reason = cause.Message;

            System.Exception realCause = cause;
            /** TODO
            if(cause is FIXMessageDecoder.DecodeError && cause.InnerException != null)
                realCause = cause.getCause();
            */
            if (realCause is System.Net.Sockets.SocketException)
            {
                if (quickFixSession != null && quickFixSession.IsEnabled)
                    reason = "Socket exception (" + client.Client.RemoteEndPoint + "): " + cause.Message;
                else
                    reason = "Socket (" + client.Client.RemoteEndPoint + "): " + cause.Message;
                disconnectNeeded = true;
            }
            /** TODO
            else if(realCause is FIXMessageDecoder.CriticalDecodeError)
            {
                reason = "Critical protocol codec error: " + cause;
                disconnectNeeded = true;
            }
            */
            else if(realCause is MessageParseError)
            {
                reason = "Protocol handler exception: " + cause;
                if (quickFixSession == null)
                    disconnectNeeded = true;
                else
                    disconnectNeeded = false;
            }
            else if (realCause is IOException)
            {
                disconnectNeeded = true;
            }
            else
            {
                reason = cause.ToString();
                disconnectNeeded = false;
            }

            this.Log("SocketReader Error: " + reason, cause);

            if (disconnectNeeded)
            {
                if (null != quickFixSession && quickFixSession.HasResponder)
                    quickFixSession.Disconnect(reason);
                else
                    DisconnectClient(client, sslStream_);
            }
        }

        private void Log(string s)
        {
            responder_.Log(s);
        }

        private void Log(string s, Exception ex)
        {
            responder_.Log(s, ex);
        }
    }
}