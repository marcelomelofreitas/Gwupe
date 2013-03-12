﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BlitsMe.Communication.P2P.RUDP.Packet.API;
using BlitsMe.Communication.P2P.RUDP.Packet.TCP;
using BlitsMe.Communication.P2P.RUDP.Socket.API;
using BlitsMe.Communication.P2P.RUDP.Socket;
using BlitsMe.Communication.P2P.RUDP.Tunnel.API;
using System.Threading;
using BlitsMe.Communication.P2P.RUDP.Tunnel.Transport;
using log4net;
using BlitsMe.Communication.P2P.Exceptions;

namespace BlitsMe.Communication.P2P.RUDP.Tunnel
{
    public class TcpTransportLayerOne4One : ITcpTransportLayer
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpTransportLayerOne4One));
        private readonly TCPTransport _transport;
        private readonly byte _connectionId;
        // Event Handlers
        private readonly AutoResetEvent _ackEvent = new AutoResetEvent(false); // when ack comes in, this gets raised
        private ushort _sequenceIn = ushort.MaxValue - 50; // sequence number we last received in
        private ushort _sequenceOut = ushort.MaxValue - 50; // sequence number we last sent out
        private readonly Object _sendingLock = new Object(); // lock to make sending thread safe
        private readonly Object _checkEstablishedLock = new Object();
        private bool _isEstablished = false;
        public int AckWaitInterval { get; private set; }

        // Properties
        public IInternalTcpOverUdptSocket socket { get; private set; }
        public int PacketCountReceiveAckValid { get; private set; }
        public int PacketCountReceiveAckInvalid { get; private set; }
        public int PacketCountReceiveDataFirst { get; private set; }
        public int PacketCountReceiveDataResend { get; private set; }
        public int PacketCountTransmitAckFirst { get; private set; }
        public int PacketCountTransmitAckResend { get; private set; }
        public int PacketCountTransmitDataFirst { get; private set; }
        public int PacketCountTransmitDataResend { get; private set; }

        public TcpTransportLayerOne4One(TCPTransport transport, byte connectionId)
        {
            this._transport = transport;
            this._connectionId = connectionId;
            socket = new StandardTcpOverUdptSocket(this);
            AckWaitInterval = 300;
        }

        public void SendData(byte[] data, int timeout)
        {
            lock (_checkEstablishedLock)
            {
                // block if connection is not established
                while (!_isEstablished)
                {
#if(DEBUG)
                    Logger.Debug("Connection [" + _connectionId + "] not yet established, waiting for connection");
#endif
                    Monitor.Wait(_checkEstablishedLock);
#if(DEBUG)
                    Logger.Debug("Connection [" + _connectionId + "] established, continuing to send data");
#endif
                }
            }
            long waitTime = timeout * 10000;
            lock (_sendingLock)
            {
                _sequenceOut++;
                StandardTcpDataPacket packet = new StandardTcpDataPacket(_sequenceOut);
                packet.Data = data;
                packet.ConnectionId = _connectionId;
#if(DEBUG)
                Logger.Debug("Sending packet " + packet.ToString());
#endif
                long startTime = DateTime.Now.Ticks;
                _ackEvent.Reset();
                do
                {
                    if (!_isEstablished)
                    {
#if(DEBUG)
                        Logger.Debug("Connection is down, aborting data send");
#endif
                        throw new ConnectionException("Cannot send data, connection is down");
                    }
                    byte[] sendBytes = packet.GetBytes();
                    _transport.SendData(packet);
                    if (packet.ResendCount > 0) { PacketCountTransmitDataResend++; }
                    else { PacketCountTransmitDataFirst++; }
                    if (DateTime.Now.Ticks - startTime > waitTime)
                    {
#if(DEBUG)
                        Logger.Debug("Data timeout : " + (DateTime.Now.Ticks - startTime));
#endif
                        _sequenceOut--;
                        throw new TimeoutException("Timeout occured while sending data to " + _transport.TransportManager.RemoteIp);
                    }
#if(DEBUG)
                    Logger.Debug("Waiting for ack from " + _transport.TransportManager.RemoteIp + " for packet " + _sequenceOut);
#endif
                    packet.ResendCount++;

                } while (!_ackEvent.WaitOne(AckWaitInterval + (AckWaitInterval * packet.ResendCount)));
                long stopTime = DateTime.Now.Ticks;
                int ackTrip = (int)((stopTime - startTime) / 10000);
                if (ackTrip > AckWaitInterval && (ackTrip - AckWaitInterval) > 20)
                {
                    // ackTrip was more than 50ms longer than our internal, we need to adjust up
                    AckWaitInterval = ((ackTrip - AckWaitInterval) / 2) + AckWaitInterval;
#if(DEBUG)
                    Logger.Debug("Adjusted ack wait interval UP to " + AckWaitInterval + ", trip was " + ackTrip);
#endif
                }
                else if (ackTrip < AckWaitInterval && (AckWaitInterval - ackTrip) > 20)
                {
                    // ackTrip was more than 50ms shorter than our internal, we need to adjust down
                    AckWaitInterval = AckWaitInterval - ((AckWaitInterval - ackTrip) / 2);
#if(DEBUG)
                    Logger.Debug("Adjusted ack wait interval DOWN to " + AckWaitInterval + ", trip was " + ackTrip);
#endif
                }
            }
        }

        public void ProcessDataPacket(ITcpPacket packet)
        {
            if (BasicTcpPacket.compareSequences((ushort)(_sequenceIn + 1), packet.Sequence) == 0)
            {
                // This is exactly what we were expecting
                _sequenceIn++;
                if (packet.ResendCount > 0) { PacketCountReceiveDataResend++; }
                else { PacketCountReceiveDataFirst++; }
                SendAck(packet, true);
                socket.BufferClientData(packet.Data);
            }
            else if (BasicTcpPacket.compareSequences((ushort)(_sequenceIn + 1), packet.Sequence) > 0)
            {
                // This is an old packet, don't process (already did that), just send ack
#if(DEBUG)
                Logger.Debug("Got old Data packet, sending ACK, data already processed.");
#endif
                SendAck(packet, false);
            }
            else
            {
#if(DEBUG)
                Logger.Debug("Got unexpected sequence (expected " + _sequenceIn + ") from packet " + packet);
#endif
            }
        }

        private void SendAck(ITcpPacket packet, bool firstSend)
        {
            StandardAckPacket outPacket = new StandardAckPacket(packet.Sequence);
            outPacket.ConnectionId = _connectionId;
            try
            {
                _transport.SendData(outPacket);
#if DEBUG
                Logger.Debug("Sent ack [" + outPacket + "]");
#endif

                if (firstSend) { PacketCountTransmitAckFirst++; }
                else { PacketCountTransmitAckResend++; }
            }
            catch (Exception e)
            {
                Logger.Error("Failed to send ack to peer for packet " + packet.Sequence + " : " + e.Message);
            }

        }

        public void Close()
        {
            if (_isEstablished)
            {
                _isEstablished = false;
                // close the socket
                socket.Close();
                // close the connection maintained by the transportManager
                _transport.CloseConnection(_connectionId);
            }
        }


        public void ProcessAck(StandardAckPacket packet)
        {
            if (packet.Sequence == _sequenceOut)
            {
                PacketCountReceiveAckValid++;
                _ackEvent.Set();
            }
            else
            {
                PacketCountReceiveAckInvalid++;
#if DEBUG
                Logger.Debug("Dropping unexpected ack : " + packet);
#endif

            }
        }


        public void Open()
        {
            lock (_checkEstablishedLock)
            {
                _isEstablished = true;
                Monitor.Pulse(_checkEstablishedLock);
            }
        }
    }
}