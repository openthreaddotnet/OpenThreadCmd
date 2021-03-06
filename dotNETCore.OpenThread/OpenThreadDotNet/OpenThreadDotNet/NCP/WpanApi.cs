using System;
using System.Collections;
using System.Threading;

#if (NANOFRAMEWORK_1_0)
using nanoFramework.OpenThread.Spinel;

namespace nanoFramework.OpenThread.NCP
{ 

#else

using dotNETCore.OpenThread.Spinel;

namespace dotNETCore.OpenThread.NCP
{
#endif

    internal class WpanApi
    {
        private const byte SpinelHeaderFlag = 0x80;
        private IStream stream;
        private Hdlc hdlcInterface;
        private SpinelEncoder mEncoder = new SpinelEncoder();
        private Queue waitingQueue = new Queue();
        private bool isSyncFrameExpecting = false;
        private AutoResetEvent receivedPacketWaitHandle = new AutoResetEvent(false);

        static object rxLocker = new object();
        static object txLocker = new object();

        internal event FrameReceivedEventHandler FrameDataReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="WpanApi"/> class.
        /// </summary>
        /// <param name="stream"></param>
        internal WpanApi(IStream stream)
        {
            this.stream = stream;
            this.hdlcInterface = new Hdlc(this.stream);
            this.stream.SerialDataReceived += new SerialDataReceivedEventHandler(StreamDataReceived);
        }

        /// <summary>
        ///
        /// </summary>
        internal void Open()
        {
            stream.Open();
        }

        internal void DoReset()
        {
            Transact(SpinelCommands.CMD_RESET);
        }

        internal uint DoLastStatus()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_LAST_STATUS);

            try
            {
                return (uint)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Interface type format violation");
            }
        }

        internal uint[] DoProtocolVersion()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_PROTOCOL_VERSION);

            try
            {               
                return (uint[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Protocol version format violation");
            }
        }

        internal string DoNCPVersion()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_NCP_VERSION);

            try
            {
                return frameData.Response.ToString();
            }
            catch
            {
                throw new SpinelProtocolExceptions("Protocol ncp version format violation");
            }
        }

        internal string DoVendor()
        {
            FrameData frameData= PropertyGetValue(SpinelProperties.PROP_VENDOR_ID);

            try
            {
                return frameData.Response.ToString();
            }
            catch
            {
                throw new SpinelProtocolExceptions("Vendor id format violation");
            }
        }


        internal uint DoInterfaceType()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_INTERFACE_TYPE);

            try
            {
                return (uint) frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Interface type format violation");
            }
        }

        internal Capabilities[] DoCaps()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_CAPS);

            try
            {
                Capabilities[] caps = (Capabilities[])frameData.Response;
                return caps;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Caps format violation");
            }
        }

        internal string DoNetworkName()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_NAME);

            try
            {
                return frameData.Response.ToString();
            }
            catch
            {
                throw new SpinelProtocolExceptions("Network name format violation");
            }
        }

        internal bool DoNetworkName(string networkName)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_NAME, networkName, "U");

            if (frameData != null && frameData.Response.ToString() == networkName)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal byte DoNetRole()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_ROLE );

            try
            {
                return (byte)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Role id format violation");
            }
        }

        internal bool DoNetRole(byte role)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_ROLE, role, "C");

            if (frameData != null && (byte)(frameData.Response) == role)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal byte DoPowerState()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_MCU_POWER_STATE);

            try
            {
                return (byte)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Power state format violation");
            }
        }

        internal bool DoPowerState(byte powerstate)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_MCU_POWER_STATE, powerstate, "C");

            if (frameData != null && (byte)(frameData.Response) == powerstate)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal byte DoChannel()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_PHY_CHAN);

            try
            {
                return (byte)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Channel number format violation");
            }
        }

        internal bool DoChannel(byte channel)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.PROP_PHY_CHAN, channel, "C");

            if (frameData != null && ((byte)frameData.Response == channel))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal byte[] DoChannels()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_PHY_CHAN_SUPPORTED);

            try
            {
                return (byte[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Supported channels format violation");
            }
        }

        internal byte[] DoChannelsMask()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_MAC_SCAN_MASK);

            try
            {
                return (byte[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Channels mask format violation");
            }
        }

        internal bool DoChannelsMask(byte[] channels)
        {        
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_MAC_SCAN_MASK, channels, "D");

            if (frameData != null && Utilities.ByteArrayCompare((byte[])frameData.Response, channels))                
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal ushort DoPanId()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_MAC_15_4_PANID);

            try
            {
                return (ushort)(frameData.Response);
            }
            catch
            {
                throw new SpinelProtocolExceptions("Pan id format violation");
            }
        }

        internal bool DoPanId(ushort panId)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_MAC_15_4_PANID, panId, "S");

            if (frameData != null && (ushort)(frameData.Response) == panId)
            {
                return true;
            }
            else
            {
                return false;
            }          
        }

        internal byte[] DoXpanId()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_XPANID);

            try
            {
                return (byte[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("XPan id format violation");
            }
        }

        internal bool DoXpanId(byte[] xpanId)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_XPANID, xpanId, "D");

            if (frameData != null && Utilities.ByteArrayCompare((byte[])frameData.Response , xpanId))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        
        internal SpinelIPv6Address[] DoIPAddresses()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_IPV6_ADDRESS_TABLE);

            try
            {
                return (SpinelIPv6Address[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("IP addesss format violation");
            }
        }

        internal SpinelIPv6Address DoIPLinkLocal64()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_IPV6_LL_ADDR);

            try
            {
                return (SpinelIPv6Address)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("IP addesss format violation");
            }
        }

        internal SpinelEUI64 DoExtendedAddress()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_MAC_15_4_LADDR);

            try
            {
                return (SpinelEUI64)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("IP addesss format violation");
            }
        }
     
        internal SpinelEUI64 DoPhysicalAddress()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.PROP_HWADDR);

            try
            {
                return (SpinelEUI64)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("IP addesss format violation");
            }
        }



        internal SpinelIPv6Address DoIPMeshLocal64()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_IPV6_ML_ADDR);

            try
            {
                return (SpinelIPv6Address)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("IP addesss format violation");
            }
        }

        internal bool DoInterfaceConfig()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_IF_UP);
            try
            {
                return (bool)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("XPan id format violation");
            }
        }

        internal bool DoInterfaceConfig(bool interfaceState)
        {            
            FrameData frameData;

            if (interfaceState)
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_IF_UP, 1, "b");
            }
            else
            {
                frameData =  PropertySetValue(SpinelProperties.SPINEL_PROP_NET_IF_UP, 0, "b");
            }

            if (frameData != null && (bool)(frameData.Response) == interfaceState)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal bool DoThread()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_STACK_UP );
            try
            {
                return (bool)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Stack up format violation");
            }
        }

        internal bool DoThread(bool threadState)
        {
            FrameData frameData;

            if (threadState)
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_STACK_UP , 1, "b");
            }
            else
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_STACK_UP , 0, "b");
            }

            if (frameData != null && (bool)(frameData.Response) == threadState)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal byte[] DoMasterkey()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_KEY);

            try
            {
                return (byte[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("XPan id format violation");
            }
        }

        internal bool DoMasterkey(byte[] masterKey)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_KEY, masterKey, "D");

            if (frameData != null && Utilities.ByteArrayCompare((byte[])frameData.Response , masterKey))
            {               
                return true;
            }
            else
            {
                return false;
            }
        }

        internal uint DoPartitionId()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_PARTITION_ID);
            try
            {
                return (uint)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Partition id format violation");
            }
        }

        internal void DoScan(byte ScanState)
        {                       
            PropertySetValue(SpinelProperties.SPINEL_PROP_MAC_SCAN_STATE, ScanState, "C");                      
        }
       
        internal bool DoProperty_NET_REQUIRE_JOIN_EXISTING(bool State)
        {
            FrameData frameData;

            if (State)
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_REQUIRE_JOIN_EXISTING, 1, "b");
            }
            else
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_REQUIRE_JOIN_EXISTING, 0, "b");
            }

            if (frameData != null && (bool)(frameData.Response) == State)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal void DoSendData(byte[] frame, bool waitResponse=true)
        {
            byte[] dataCombined = mEncoder.EncodeDataWithLength(frame);

           PropertySetValue(SpinelProperties.PROP_STREAM_NET, dataCombined, "dD", 129, waitResponse);                
        }


        internal void DoCountersReset()
        {
            PropertySetValue(SpinelProperties.SPINEL_PROP_CNTR_RESET, 1 , "C");
        }

        internal ushort[] DoCountersMessageBuffer()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_MSG_BUFFER_COUNTERS);

            try
            {
                return (ushort[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Buffer counters format violation");
            }
        }

        //**********************************************************************
        //
        // Spinel NET Properties
        //
        //**********************************************************************

        /// <summary>
        /// Network Is Saved (Is Commissioned)
        /// </summary>
        /// <returns>true if there is a network state stored/saved.</returns>
        internal bool GetNetSaved()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_SAVED);
            try
            {
                return (bool)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Spinel Net format violation");
            }
        }

        /// <summary>
        /// Network Interface Status
        /// </summary>
        /// <returns>Returns true if interface up and false if interface down</returns>
        internal bool GetNetIfUp()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_IF_UP);
            try
            {
                return (bool)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Spinel Net format violation");
            }
        }

        /// <summary>
        /// Network interface up/down status. Write true to bring interface up and false to bring interface down.     
        /// </summary>
        /// <param name="NetworkInterfaceStatus"></param>
        /// <returns></returns>
        internal bool SetNetIfUp(bool NetworkInterfaceStatus)
        {
            FrameData frameData;

            if (NetworkInterfaceStatus)
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_IF_UP, 1, "b");
            }
            else
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_IF_UP, 0, "b");
            }

            if (frameData != null && (bool)(frameData.Response) == NetworkInterfaceStatus)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal bool GetNetStackUp()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_STACK_UP);
            try
            {
                return (bool)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Spinel stack up format violation");
            }
        }

        internal bool SetNetStackUp(bool ThreadStackStatus)
        {
            FrameData frameData;

            if (ThreadStackStatus)
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_STACK_UP, 1, "b");
            }
            else
            {
                frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_STACK_UP, 0, "b");
            }

            if (frameData != null && (bool)(frameData.Response) == ThreadStackStatus)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal SpinelNetRole GetNetRole()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_ROLE);

            try
            {
                return (SpinelNetRole)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Role id format violation");
            }
        }

        internal bool SetNetRole(SpinelNetRole ThreadDeviceRole)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_ROLE, ((byte)ThreadDeviceRole), "C");

            if (frameData != null && (SpinelNetRole)(frameData.Response) == ThreadDeviceRole)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal string GetNetNetworkName()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_NAME);

            try
            {
                return frameData.Response.ToString();
            }
            catch
            {
                throw new SpinelProtocolExceptions("Network name format violation");
            }
        }

        internal bool SetNetNetworkName(string ThreadNetworkName)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_NAME, ThreadNetworkName, "U");

            if (frameData != null && frameData.Response.ToString() == ThreadNetworkName)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal byte[] GetNetXPANId()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_XPANID);

            try
            {
                return (byte[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("XPan id format violation");
            }
        }

        internal bool SetNetXPANId(byte[] ThreadNetworkExtendedPANId)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_XPANID, ThreadNetworkExtendedPANId, "D");

            if (frameData != null && Utilities.ByteArrayCompare((byte[])frameData.Response, ThreadNetworkExtendedPANId))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal byte[] GetNetNetworkKey()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_KEY);

            try
            {
                return (byte[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Spinel network key format violation.");
            }
        }

        internal bool SetNetNetworkKey(byte[] ThreadNetworkKey)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_NETWORK_KEY, ThreadNetworkKey, "D");

            if (frameData != null && Utilities.ByteArrayCompare((byte[])frameData.Response, ThreadNetworkKey))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
       
        internal uint GetNetKeySequenceCounter()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_KEY_SEQUENCE_COUNTER);

            try
            {
                return (uint)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Spinel Key Sequence Counter format violation.");
            }
        }

        internal bool SetNetKeySequenceCounter(uint ThreadNetworkKeySequenceCounter)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_KEY_SEQUENCE_COUNTER, ThreadNetworkKeySequenceCounter, "L");

            if (frameData != null && ((uint)frameData.Response==ThreadNetworkKeySequenceCounter))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal uint GetNetPartitionId()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_PARTITION_ID);

            try
            {
                return (uint)frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Spinel Key Sequence Counter format violation.");
            }
        }

        internal bool SetNetPartitionId(uint ThreadNetworkPartitionId)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_PARTITION_ID, ThreadNetworkPartitionId, "L");

            if (frameData != null && ((uint)frameData.Response == ThreadNetworkPartitionId))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal bool GetNetRequireJoinExisting()
        {
            //    SPINEL_PROP_NET_REQUIRE_JOIN_EXISTING
            throw new NotImplementedException();
        }

        internal  bool SetNetRequireJoinExisting(bool RequireJoinExisting)
        {
            //    SPINEL_PROP_NET_REQUIRE_JOIN_EXISTING
            throw new NotImplementedException();
        }

        internal uint GetNetKeySwitchGuardtime()
        {
            //     SPINEL_PROP_NET_KEY_SWITCH_GUARDTIME
            throw new NotImplementedException();
        }

        internal bool SetNetKeySwitchGuardtime(uint ThreadNetworkKeySwitchGuardTime)
        {
            //    SPINEL_PROP_NET_REQUIRE_JOIN_EXISTING
            throw new NotImplementedException();
        }

        internal byte[] GetNetNetworkPSKC()
        {
            FrameData frameData = PropertyGetValue(SpinelProperties.SPINEL_PROP_NET_PSKC);

            try
            {
                return (byte[])frameData.Response;
            }
            catch
            {
                throw new SpinelProtocolExceptions("Spinel network pskc format violation.");
            }
        }

        internal bool SetNetNetworkPSKC(byte[] ThreadNetworkPSKc)
        {
            FrameData frameData = PropertySetValue(SpinelProperties.SPINEL_PROP_NET_PSKC, ThreadNetworkPSKc, "D");

            if (frameData != null && Utilities.ByteArrayCompare((byte[])frameData.Response, ThreadNetworkPSKc))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //**********************************************************************
        //
        //      Spinel NET Properties
        //
        //**********************************************************************




        //**********************************************************************
        //
        //      Spinel Thread Properties
        //
        //**********************************************************************


        internal void Transact(int commandId, byte[] payload, byte tID = SpinelCommands.HEADER_DEFAULT)
        {
            byte[] packet = EncodePacket(commandId,tID,payload);
            StreamTx(packet);
        }

        internal void Transact(int commandId, byte tID = SpinelCommands.HEADER_DEFAULT)
        {
            Transact(commandId, null, tID);
        }

        internal byte[] EncodePacket(int commandId, byte tid = SpinelCommands.HEADER_DEFAULT, params byte[] payload)
        {
            byte[] tidBytes = new byte[1] { tid };
            byte[] commandBytes = mEncoder.EncodeValue(commandId);
            byte[] packet = new byte[commandBytes.Length + tidBytes.Length + (payload == null?0:payload.Length) ];

            if (payload != null)
            {
                packet = Utilities.CombineArrays(tidBytes, commandBytes, payload);
            }
            else
            {
                packet = Utilities.CombineArrays(tidBytes, commandBytes);
            }

            return packet;
        }


        private void StreamDataReceived()
        {
            lock (rxLocker)
            {
                StreamRX();
            }

            receivedPacketWaitHandle.Set();

            if (isSyncFrameExpecting)
            {
                return;
            }

            while (waitingQueue.Count != 0)
            {
               
                FrameData frameData = waitingQueue.Dequeue() as FrameData;

                FrameDataReceived(frameData);
            }

          //  receivedPacketWaitHandle.Reset();
        }

        private object PropertyChangeValue(int commandId, int propertyId, byte[] propertyValue, string propertyFormat = "B", byte tid = SpinelCommands.HEADER_DEFAULT, bool waitResponse = true)
        {
            FrameData responseFrame = null;
            isSyncFrameExpecting = true;
            byte[] payload = mEncoder.EncodeValue(propertyId);

            if (propertyFormat != null)
            {
                payload = Utilities.CombineArrays(payload, propertyValue);
            }

            int uid = Utilities.GetUID(propertyId, tid);

            lock (txLocker)
            {
                Transact(commandId, payload, tid);
            }

            if (!waitResponse)
            {
                isSyncFrameExpecting = false;
                return null;
            }

            receivedPacketWaitHandle.Reset();

            if (!receivedPacketWaitHandle.WaitOne(155000, false))
            {
                throw new SpinelProtocolExceptions("Timeout for sync packet " + commandId);
            }

            if (waitingQueue.Count > 0)
            {
                while (waitingQueue.Count != 0)
                {
                    FrameData frameData = waitingQueue.Dequeue() as FrameData;

                    if (frameData.UID == uid)
                    {
                        responseFrame = frameData;
                        isSyncFrameExpecting = false;
                    }
                    else
                    {
                       FrameDataReceived(frameData);
                    }
                }
            }
            else
            {
                throw new SpinelProtocolExceptions("No response packet for command" + commandId);
            }

            return responseFrame;
        }

        private void StreamTx(byte[] packet)
        {
            hdlcInterface.Write(packet);
        }

        /// <summary>
        /// 
        /// </summary>
        private void StreamRX(int timeout = 0)
        {
            DateTime start = DateTime.UtcNow;
            
            bool dataPooled = false;

            while (true)
            {
                TimeSpan elapsed = DateTime.UtcNow - start;

                if (timeout != 0)
                {
                    if (elapsed.Seconds > timeout)
                    {
                        break;
                    }
                }

                if (stream.IsDataAvailable)
                {
                    byte[] frameDecoded = hdlcInterface.Read();
                    ParseRX(frameDecoded);
                    dataPooled = true;
                }
                else
                {
                  //  Console.WriteLine("Serial data not available. Data pooled :" + dataPooled.ToString() );
                }

                if (!stream.IsDataAvailable && dataPooled)
                {
                    break;
                }
            }
        }

        private void ParseRX(byte[] frameIn)
        {

            SpinelDecoder mDecoder = new SpinelDecoder();
            object ncpResponse=null;
            mDecoder.Init(frameIn);

            byte header = mDecoder.FrameHeader;

            if ((SpinelHeaderFlag & header) != SpinelHeaderFlag)
            {
                throw new SpinelFormatException("Header parsing error.");
            }

            uint command = mDecoder.FrameCommand;
            uint properyId = mDecoder.FramePropertyId;

            if (properyId == SpinelProperties.SPINEL_PROP_THREAD_CHILD_TABLE)
            {
                if (command == SpinelCommands.RSP_PROP_VALUE_INSERTED || command == SpinelCommands.RSP_PROP_VALUE_REMOVED)
                {
                    return;
                }
            }

            object tempObj = null;

            switch (properyId)
            {
                case SpinelProperties.PROP_NCP_VERSION:
                    ncpResponse = mDecoder.ReadUtf8();
                    break;

                case SpinelProperties.PROP_LAST_STATUS:
                    ncpResponse = mDecoder.ReadUintPacked();
                    break;

                case SpinelProperties.PROP_INTERFACE_TYPE:
                    ncpResponse = mDecoder.ReadUintPacked();
                    break;

                case SpinelProperties.PROP_VENDOR_ID:
                    ncpResponse = mDecoder.ReadUintPacked();
                    break;

                case SpinelProperties.SPINEL_PROP_NET_NETWORK_NAME:

                    ncpResponse = mDecoder.ReadUtf8();
                    break;

                case SpinelProperties.SPINEL_PROP_MAC_SCAN_STATE:
                    ncpResponse = mDecoder.ReadUint8();
                    break;

                case SpinelProperties.SPINEL_PROP_MAC_SCAN_MASK:                   
                    tempObj = mDecoder.ReadFields("A(C)");

                    if (tempObj != null)
                    {
                        ArrayList channels = (ArrayList)tempObj;
                        ncpResponse = (byte[])channels.ToArray(typeof(byte));
                    }

                    break;

                case SpinelProperties.SPINEL_PROP_MAC_SCAN_PERIOD:
                    ncpResponse = mDecoder.ReadUint16();
                    break;

                case SpinelProperties.SPINEL_PROP_MAC_SCAN_BEACON:
                    ncpResponse = mDecoder.ReadFields("Cct(ESSC)t(iCUdd)");
                    break;

                case SpinelProperties.SPINEL_PROP_MAC_ENERGY_SCAN_RESULT:
                    ncpResponse = mDecoder.ReadFields("Cc");
                    break;
                    
                case SpinelProperties.PROP_PROTOCOL_VERSION:

                    tempObj = mDecoder.ReadFields("ii");

                    if (tempObj != null)
                    {
                        ArrayList protocol = (ArrayList)tempObj;
                        ncpResponse = (uint[])protocol.ToArray(typeof(uint));
                    }

                    break;

                case SpinelProperties.PROP_CAPS:

                    tempObj = mDecoder.ReadFields("A(i)");

                    if (tempObj != null)
                    {
                        ArrayList caps = (ArrayList)tempObj;
                        Capabilities[] capsArray = new Capabilities[caps.Count];
                        int index = 0;

                        foreach (var capsValue in caps)
                        {
                            capsArray[index] = (Capabilities)(uint)(capsValue);
                            index++;
                        }

                        ncpResponse = capsArray;
                    }

                    break;

                case SpinelProperties.SPINEL_PROP_MSG_BUFFER_COUNTERS:
                  
                    tempObj = mDecoder.ReadFields("SSSSSSSSSSSSSSSS");
                    
                    if (tempObj != null)
                    {
                        ArrayList counters = (ArrayList)tempObj;
                        ncpResponse = (ushort[])counters.ToArray(typeof(ushort));
                    }

                    break;

                case SpinelProperties.PROP_PHY_CHAN:
                    ncpResponse = mDecoder.ReadUint8();
                    break;

                case SpinelProperties.PROP_PHY_CHAN_SUPPORTED:
                    tempObj = mDecoder.ReadFields("A(C)");

                    if (tempObj != null)
                    {
                        ArrayList channels = (ArrayList)tempObj;
                        ncpResponse = (byte[])channels.ToArray(typeof(byte));
                    }

                    break;

                case SpinelProperties.SPINEL_PROP_IPV6_ADDRESS_TABLE:

                    tempObj = mDecoder.ReadFields("A(t(6CLL))");
                    ArrayList ipAddresses = new ArrayList();

                    if (tempObj != null)
                    {
                        ArrayList addressArray = tempObj as ArrayList;

                        foreach (ArrayList addrInfo in addressArray)
                        {
                            object[] ipProps = addrInfo.ToArray();
                            SpinelIPv6Address ipaddr = ipProps[0] as SpinelIPv6Address;                           
                            ipAddresses.Add(ipaddr);
                        }
                    }

                    if (ipAddresses.Count > 0)
                    {
                        ncpResponse = ipAddresses.ToArray(typeof(SpinelIPv6Address));
                    }

                    break;

                case SpinelProperties.SPINEL_PROP_NET_IF_UP:
                    ncpResponse = mDecoder.ReadBool();
                    break;

                case SpinelProperties.SPINEL_PROP_NET_STACK_UP :
                    ncpResponse = mDecoder.ReadBool();
                    break;

                case SpinelProperties.SPINEL_PROP_NET_REQUIRE_JOIN_EXISTING:
                    ncpResponse = mDecoder.ReadBool();
                    break;
                    
                case SpinelProperties.SPINEL_PROP_MAC_15_4_PANID:
                    ncpResponse = mDecoder.ReadUint16();
                    break;

                case SpinelProperties.SPINEL_PROP_NET_XPANID:
                    ncpResponse = mDecoder.ReadData();
                    break;

                case SpinelProperties.SPINEL_PROP_NET_ROLE :
                    ncpResponse = mDecoder.ReadUint8();
                    break;

                case SpinelProperties.SPINEL_PROP_MCU_POWER_STATE:
                    ncpResponse = mDecoder.ReadUint8();
                    break;
                  
                case SpinelProperties.SPINEL_PROP_NET_NETWORK_KEY:
                    ncpResponse = mDecoder.ReadData();
                    break;
                case SpinelProperties.PROP_STREAM_NET:                    
                    tempObj = mDecoder.ReadFields("dD");
                    if (tempObj != null)
                    {
                        ArrayList responseArray = tempObj as ArrayList;
                        ncpResponse = responseArray[0];
                    }                        
                    break;            

                case SpinelProperties.SPINEL_PROP_IPV6_LL_ADDR:
                    SpinelIPv6Address ipaddrLL = mDecoder.ReadIp6Address();                    
                    ncpResponse = ipaddrLL;
                    break;

                case SpinelProperties.SPINEL_PROP_IPV6_ML_ADDR:
                    SpinelIPv6Address ipaddrML = mDecoder.ReadIp6Address();                   
                    ncpResponse = ipaddrML;
                    break;

                case SpinelProperties.SPINEL_PROP_MAC_15_4_LADDR:
                    SpinelEUI64 spinelEUI64 = mDecoder.ReadEui64();
                    ncpResponse = spinelEUI64;
                    break;

                case SpinelProperties.PROP_HWADDR:
                    SpinelEUI64 hwaddr = mDecoder.ReadEui64();
                    ncpResponse = hwaddr;
                    break;
                   
                    //case SpinelProperties.SPINEL_PROP_IPV6_ML_PREFIX:
                    //    ncpResponse = mDecoder.ReadFields("6C");
                    //    break;
            }

            FrameData frameData = new FrameData(mDecoder.FramePropertyId, mDecoder.FrameHeader, mDecoder.GetFrameLoad(),  ncpResponse);

            waitingQueue.Enqueue(frameData);
        }

        private FrameData PropertyGetValue(int propertyId, byte tid = SpinelCommands.HEADER_DEFAULT)
        {
            return PropertyChangeValue(SpinelCommands.CMD_PROP_VALUE_GET, propertyId, null, null, tid) as FrameData;
        }

        private FrameData PropertySetValue(int propertyId, ushort propertyValue, string propertyFormat = "B", byte tid = SpinelCommands.HEADER_DEFAULT)
        {
            byte[] propertyValueArray = mEncoder.EncodeValue(propertyValue, propertyFormat);

            return PropertySetValue(propertyId, propertyValueArray, propertyFormat, tid);
        }

        private FrameData PropertySetValue(int propertyId, byte propertyValue, string propertyFormat = "B", byte tid = SpinelCommands.HEADER_DEFAULT)
        {
            byte[] propertyValueArray = mEncoder.EncodeValue(propertyValue, propertyFormat);

            return PropertySetValue(propertyId, propertyValueArray, propertyFormat, tid);
        }

        private FrameData PropertySetValue(int propertyId, string propertyValue, string propertyFormat = "B", byte tid = SpinelCommands.HEADER_DEFAULT)
        {
            byte[] propertyValueArray = mEncoder.EncodeValue(propertyValue, propertyFormat);

            return PropertySetValue(propertyId, propertyValueArray, propertyFormat, tid);
        }

        private FrameData PropertySetValue(int propertyId, uint propertyValue, string propertyFormat = "L", byte tid = SpinelCommands.HEADER_DEFAULT)
        {
            byte[] propertyValueArray = mEncoder.EncodeValue(propertyValue, propertyFormat);

            return PropertySetValue(propertyId, propertyValueArray, propertyFormat, tid);
        }

        private FrameData PropertySetValue(int propertyId, byte[] propertyValue, string propertyFormat = "B", byte tid = SpinelCommands.HEADER_DEFAULT, bool waitResponse = true)
        {
            return PropertyChangeValue(SpinelCommands.CMD_PROP_VALUE_SET, propertyId, propertyValue, propertyFormat, tid, waitResponse) as FrameData;
        }       
    }
}
