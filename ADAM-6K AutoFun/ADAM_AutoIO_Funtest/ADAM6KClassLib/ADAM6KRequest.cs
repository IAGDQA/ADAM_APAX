using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Collections;
using Model;
using System.Threading;
using Advantech.Adam;
using Advantech.Common;
using System.Net;
using System.Net.Sockets;

namespace Service
{
    /// <summary>
    /// ---------------------- V01 History --------------------------
    /// 20161207    v2001   Add channel enable function
    /// 20161110    v2000   Fix ADAM-6017 command
    /// 20160105    v1000   Add new library for ADAM6K series
    /// ---------------------------------------------------------
    /// </summary>
    public class ADAM6KReqService
    {
        ADAM6K_IO_Model Device;//建立一個DeviceModel給所有Service

        ArrayList BaseListOfIOItems;//建立一個IO Item List

        const int ADAM6KUDP_PORT = 1025;
        const int ADAMTCP_PORT = 502;
        private int m_iCom, m_iAddr;
        private Adam6000Type m_Adam6000Type;
        private AdamSocket adamModbus, adamUDP;
        private string m_szIP;
        private string m_szFwVersion;
        const int m_Adam6000NewerFwVer = 5;
        private int m_DeviceFwVer;
        private int m_iPort;
        private int m_iCount;
        //private int m_iAiTotal, m_iDoTotal;
        private bool[] m_bChEnabled;
        private byte[] m_byRange;
        private ushort[] m_usRange; //for newer version Adam6017

        float[] ai_values;

        bool DevConnFail = false;

        /// <summary>
        /// main
        /// </summary>
        public ADAM6KReqService()
        {
        }

        public bool OpenCOM(string m_szIP)//Adam6000Type type
        {
            adamUDP = new AdamSocket();
            adamUDP.SetTimeout(5000, 3000, 3000); // set timeout for UDP
            if (adamUDP.Connect(m_szIP, ProtocolType.Udp, ADAM6KUDP_PORT))
            {
                if (!adamUDP.Configuration().GetFirmwareVer(out m_szFwVersion))
                {
                    adamUDP.Disconnect();
                    return false;
                }
                else
                {
                    m_DeviceFwVer = int.Parse(m_szFwVersion.Trim().Substring(0, 1));
                }
                UpdateDevUIStatus();
                if (Device != null)
                {
                    adamModbus = new AdamSocket();
                    adamModbus.SetTimeout(5000, 3000, 3000); // set timeout for TCP
                    if (adamModbus.Connect(m_szIP, ProtocolType.Tcp, ADAMTCP_PORT))
                    {
                        Device.Port = ADAMTCP_PORT;
                        Device.SlotNum = 1;
                    }
                    else return false;
                }
                else return false;

                return true;
            }


            return false;
        }

        public void CloseCOM()//Adam6000Type type
        {
            if (adamUDP != null && adamUDP.Connected) { adamUDP.Disconnect(); adamUDP = null; }
            if (adamModbus != null && adamModbus.Connected) adamModbus.Disconnect();
        }

        public string SendCmd(string cmd)//ASCII Cmd send
        {
            string rec_str = "Command Fail!";
            if (!DevConnFail && cmd != "")
            {
                string szCommand, szRecv;
                szCommand = cmd + "\r";
                if (adamUDP.AdamTransaction(szCommand, out szRecv))
                {
                    rec_str = szRecv;
                }
                else
                {
                    rec_str = adamUDP.LastError.ToString();
                }
            }
            return rec_str;
        }

        public string ReceiveCmd()//ASCII Cmd receive
        {
            string rec_str = "";
            if (!DevConnFail)
            {
                //int res = adamUDP.Receive(out rec_str);
            }
            return rec_str;
        }

        #region -- Implement Interface --
        public DeviceModel GetDevice()//DeviceViewModel實作介面
        {
            return Device;
        }

        public List<IOModel> GetListOfIOItems(string stateFilter)
        {
            List<IOModel> list = new List<IOModel>();
            GetIORefreshItems();
            foreach (var item in BaseListOfIOItems)
            {
                IOModel devm = (IOModel)item;
                list.Add(devm);
            }
            return list;
        }

        public string GetDevRng(string _mdname)
        {
            if (Device.AIRng == null) return "";
            List<ValueRange> list = new List<ValueRange>();
            foreach (var typ in Device.AIRng) list.Add(typ);

            ValueRange[] listAr = list.ToArray();
            string reStr = "";
            foreach (var _ls in listAr)
            {
                reStr += _ls.ToString() + "\n";
            }
            return reStr;
        }

        public int GetDllVersion()
        {
            return 2001;//1000
        }

        //20161111 add new
        public object GetRngCodeFromModbus()
        {
            int[] res = new int[1]; int[] temp;
            if (adamModbus.Modbus().ReadInputRegs(201, Device.AiTotal, out temp))
            {
                res = temp;
            }
            return res;
        }
        public object GetValueFromModbus()
        {
            int[] res = new int[1]; int[] temp;
            if (adamModbus.Modbus().ReadInputRegs(1, Device.AiTotal, out temp))
            {
                res = temp;
            }
            return res;
        }

        private void UpdateDevUIStatus()
        {
            BaseListOfIOItems = new System.Collections.ArrayList();
            //string mtype = "";
            //adamCom.Configuration(m_iAddr).GetModuleName(out mtype);
            m_Adam6000Type = GetModuleType(SendCmd("$01M"));
            if (m_Adam6000Type != Adam6000Type.Non)
            {
                Device = new ADAM6K_IO_Model(m_Adam6000Type);
            }
        }

        #region -- IO Service --

        //-- IO List Service --


        public IOModel GetIOConfig(int _id)
        {
            IOModel _RtnInfo = new IOModel();
            //實作IO config api
            //GetIOConfigRequest(_id);
            //Thread.Sleep(100);
            foreach (var item in BaseListOfIOItems)
            {
                var readInfo = (IOModel)item;
                if (readInfo.Id.Equals(_id))
                {
                    _RtnInfo = readInfo;
                    break;
                }
            }
            return _RtnInfo;
        }

        public void UpdateIOConfig(IOModel data)
        {
            UpdateIOConfg(data);
        }

        public void UpdateIOValue(IOModel data)
        {
            UpdateIOVal(data);
        }

        //20170104 add for set all channel mode to default.
        public bool SetDIO_DefaultMod()
        {
            return Set_DefaultMod();
        }


        #endregion //IO Service

        #endregion

        //------------------------------------------------------//
        private void UpdateIOConfg(IOModel data)
        {
            if (data.Id >= (int)DevIDDefine.AI)
            {
                //get ch config status
                var chConfig = UpdateAIConfig(data.Ch);
                //change enable status
                if (chConfig.cEn != data.cEn)
                    SetEnableStatus(data.Ch, data.cEn);
                //change range code
                if (chConfig.cRng != data.cRng)
                    SendCmdToConfig(data.Ch, data.cRng);
            }
            else if(data.Id >= (int)DevIDDefine.DO)
            {
                //get ch config status
                var chConfig = UpdateDOConfig(data.Ch);
                //change mode
                if (chConfig.Md != data.Md || chConfig.Inv != data.Inv || chConfig.Fltr != data.Fltr)
                    SetDIO_ModeConfig(data);//SetDIO_ModeConfig(data.Id.GetValueOrDefault(), data.Md.GetValueOrDefault());
            }
            else
            {
                //get ch config status
                var chConfig = UpdateDIConfig(data.Ch);
                //change mode
                if (chConfig.Md != data.Md || chConfig.Inv != data.Inv || chConfig.Fltr != data.Fltr)
                    SetDIO_ModeConfig(data);//SetDIO_ModeConfig(data.Ch, data.Md.GetValueOrDefault());
            }

            int cnt = 0;
            while (cnt < 10)//delay 1sec
            {
                //adamCom.Configuration(m_iAddr).GetModuleConfig(out m_adamConfig);
                Thread.Sleep(100);
                cnt++;
            }
        }
        private IOModel UpdateAIConfig(int ch)
        {
            var data = GetIO(ch + (int)DevIDDefine.AI);
            return new IOModel()
            {
                Ch = data.Ch,
                Tag = data.Tag,
                cEn = data.En,
                cRng = data.Rng,
                //EnLA = data.EnLA.Value,
                //EnHA = data.EnHA.Value,
                //LAMd = data.LAMd.Value,
                //HAMd = data.HAMd.Value,
            };
        }
        private IOModel UpdateDIConfig(int ch)
        {
            var data = GetIO(ch + (int)DevIDDefine.DI);
            return new IOModel()
            {
                Ch = data.Ch,
                Tag = data.Tag,
                Md = data.Md,
            };
        }
        private IOModel UpdateDOConfig(int ch)
        {
            var data = GetIO(ch + (int)DevIDDefine.DO);
            return new IOModel()
            {
                Ch = data.Ch,
                Tag = data.Tag,
                Md = data.Md,
            };
        }

        private void UpdateIOVal(IOModel data)//for AO module
        {
            if (data.Id >= 60)
            {
                float fValue = OutputByRng(data.Rng, data.Val);//Convert.ToSingle((double)data.Val / 1000);
                //if (Device.ModuleType == "Adam4021")
                //    adamCom.AnalogOutput(m_iAddr).SetCurrentValue((byte)m_adamConfig.Format, fValue);
                //else if (Device.ModuleType == "Adam4024")
                //    adamCom.AnalogOutput(m_iAddr).SetCurrentValue(data.Ch, fValue);
                System.Threading.Thread.Sleep(100);
            }
        }

        private IOModel GetIO(int _id)
        {
            IOModel _RtnInfo = new IOModel();
            foreach (var item in BaseListOfIOItems)
            {
                var readInfo = (IOModel)item;
                if (readInfo.Id.Equals(_id))
                {
                    _RtnInfo = readInfo;
                    break;
                }
            }
            return _RtnInfo;
        }

        private void GetIORefreshItems()
        {
            if (Device.DiTotal > 0)
                UpdateDIUIStatus();
            if (Device.DoTotal > 0)
                UpdateDOUIStatus();
            if (Device.AiTotal > 0)
                UpdateAIUIStatus();
            if (Device.AoTotal > 0)
                UpdateAOUIStatus();
        }
        //------------------------------------------------------------------------//
        private void UpdateDIUIStatus()//Event to update data
        {
            int iDiStart = 1;
            bool[] bDiData, bData;
            int[] bMd, bInv, bFlt;
            //byte[] di_config;
            int m_iDiTotal = Device.DiTotal;

            //if (adamModbus.DigitalInput().GetIOConfig(out di_config))
            //{ }
            if (GetDIO_ModeConfig(out bMd, out bInv, out bFlt))//20161220 add get DI config
            { }

            if (adamModbus.Modbus().ReadCoilStatus(iDiStart, m_iDiTotal, out bDiData))
            {
                bData = new bool[m_iDiTotal];
                Array.Copy(bDiData, 0, bData, 0, m_iDiTotal);
            }

            for (int i = 0; i < m_iDiTotal; ++i)
            {
                IOModel BfMdfIOCh = GetIO(i + (int)DevIDDefine.DI);
                if (BfMdfIOCh.Id != null && BfMdfIOCh.Ch == i)
                {
                    var temp = BfMdfIOCh;
                    temp.Val = Convert.ToInt32(bDiData[i]);
                    temp.Md = bMd[i];
                    temp.Inv = bInv[i];
                    temp.Fltr = bFlt[i];
                    //temp.Md = di_config[i];
                    //temp.Stat = di_values.DIVal[i].Stat;
                    //temp.Cnting = di_values.DIVal[i].Cnting;
                    //temp.OvLch = di_values.DIVal[i].OvLch;

                    int Idx = BaseListOfIOItems.IndexOf(BfMdfIOCh);
                    BaseListOfIOItems.Remove(BfMdfIOCh);
                    BaseListOfIOItems.Insert(Idx, temp);
                }
                else if (BfMdfIOCh.Id == null)
                {
                    IOModel _BfMdfIOCh = new IOModel()
                    {
                        Id = i + (int)DevIDDefine.DI,
                        Ch = i,
                    };
                    BaseListOfIOItems.Add(_BfMdfIOCh);
                    //
                    int _id = _BfMdfIOCh.Id.Value;
                    //GetIOConfigRequest(_id);
                }
            }


        }
        private void UpdateDOUIStatus()//Event to update data
        {
            int iDoStart = 17;
            bool[] bDoData, bData;
            byte[] do_config;
            int m_iDoTotal = Device.DoTotal;

            if (adamModbus.DigitalOutput().GetIOConfig(out do_config))
            { }

            if (adamModbus.Modbus().ReadCoilStatus(iDoStart, m_iDoTotal, out bDoData))
            {
                bData = new bool[m_iDoTotal];
                Array.Copy(bDoData, 0, bData, 0, m_iDoTotal);
            }

            for (int i = 0; i < m_iDoTotal; ++i)
            {
                IOModel BfMdfIOCh = GetIO(i + (int)DevIDDefine.DO);
                if (BfMdfIOCh.Id != null && BfMdfIOCh.Ch == i)
                {
                    var temp = BfMdfIOCh;
                    temp.Val = Convert.ToInt32(bDoData[i]);
                    temp.Md = do_config[i];
                    //temp.Stat = di_values.DIVal[i].Stat;
                    //temp.Cnting = di_values.DIVal[i].Cnting;
                    //temp.OvLch = di_values.DIVal[i].OvLch;

                    int Idx = BaseListOfIOItems.IndexOf(BfMdfIOCh);
                    BaseListOfIOItems.Remove(BfMdfIOCh);
                    BaseListOfIOItems.Insert(Idx, temp);
                }
                else if (BfMdfIOCh.Id == null)
                {
                    IOModel _BfMdfIOCh = new IOModel()
                    {
                        Id = i + (int)DevIDDefine.DO,
                        Ch = i,
                    };
                    BaseListOfIOItems.Add(_BfMdfIOCh);
                    //
                    int _id = _BfMdfIOCh.Id.Value;
                    //GetIOConfigRequest(_id);
                }
            }

        }
        //更新AI所有屬性
        private void UpdateAIUIStatus()//Event to update data
        {
            int iStart = 1, iBurnStart = 121;
            int iIdx; int[] iData;
            int m_iAiTotal = Device.AiTotal;
            float[] fValue = new float[m_iAiTotal];
            bool[] bBurn = new bool[m_iAiTotal]; bBurn.Initialize();
            bool[] bEnabled;

            m_byRange = new byte[m_iAiTotal];
            m_usRange = new ushort[m_iAiTotal];

            if (adamModbus.AnalogInput().GetChannelEnabled(m_iAiTotal, out m_bChEnabled))
            { }

            if (adamModbus.Modbus().ReadInputRegs(iStart, m_iAiTotal, out iData))
            {
                for (iIdx = 0; iIdx < m_iAiTotal; iIdx++)
                {
                    GetRngCode(iIdx);
                    //
                    if (m_DeviceFwVer < m_Adam6000NewerFwVer)
                        fValue[iIdx] = AnalogInput.GetScaledValue(m_Adam6000Type, m_byRange[iIdx], iData[iIdx]);
                    else//for newer version
                        fValue[iIdx] = AnalogInput.GetScaledValue(m_Adam6000Type, m_usRange[iIdx], (ushort)iData[iIdx]); 
                }

                try
                {
                    for (int i = 0; i < m_iAiTotal; ++i)
                    {
                        IOModel BfMdfIOCh = GetIO(i + (int)DevIDDefine.AI);
                        if (BfMdfIOCh.Id != null && BfMdfIOCh.Ch == i)
                        {
                            var temp = BfMdfIOCh;
                            {
                                #region -- Adam6015--
                                if (m_Adam6000Type == Adam6000Type.Adam6015)
                                {
                                    if (adamModbus.Modbus().ReadCoilStatus(iBurnStart, m_iAiTotal, out bBurn)) // read burn out flag
                                    {
                                        if (m_bChEnabled[i])
                                        {
                                            temp.En = m_bChEnabled[i] ? 1 : 0;
                                            temp.Val = (int)(fValue[i] * 1000);
                                            temp.Rng = ReturnFormalRng(m_byRange[i]);
                                            if (bBurn[i])
                                                temp.Val_Eg = "Burn out";
                                            else
                                            {
                                                string szFormat = AnalogInput.GetFloatFormat(m_Adam6000Type, m_byRange[i]);
                                                temp.Val_Eg = fValue[i].ToString(szFormat) + " " + AnalogInput.GetUnitName(m_Adam6000Type, m_byRange[i]);
                                            }
                                        }
                                    }
                                }
                                #endregion
                                else
                                {
                                    temp.En = m_bChEnabled[i] ? 1 : 0;
                                    //填入相對應的屬性數值
                                    if (m_bChEnabled[i])
                                    {   //value is row data.                                        
                                        temp.EgF = fValue[i];
                                        temp.Val = FloatToRowData(i, fValue[i]);// * 1000
                                        if (m_DeviceFwVer < m_Adam6000NewerFwVer)//detect new FW
                                        {
                                            temp.Rng = ReturnFormalRng(m_byRange[i]);
                                            string szFormat = AnalogInput.GetFloatFormat(m_Adam6000Type, m_byRange[i]);
                                            temp.Val_Eg = fValue[i].ToString(szFormat) + " " + AnalogInput.GetUnitName(m_Adam6000Type, m_byRange[i]);
                                        }                                            
                                        else
                                        {
                                            temp.Rng = ReturnFormalRng(m_usRange[i]);
                                            string szFormat = AnalogInput.GetFloatFormat(m_Adam6000Type, m_usRange[i]);
                                            temp.Val_Eg = fValue[i].ToString(szFormat) + " " + AnalogInput.GetUnitName(m_Adam6000Type, m_usRange[i]);
                                        }                                           
                                        
                                    }
                                }

                            }
                            int Idx = BaseListOfIOItems.IndexOf(BfMdfIOCh);
                            BaseListOfIOItems.Remove(BfMdfIOCh);
                            BaseListOfIOItems.Insert(Idx, temp);
                        }
                        else if (BfMdfIOCh.Id == null)
                        {
                            IOModel _BfMdfIOCh = new IOModel()
                            {
                                Id = (int)DevIDDefine.AI + i,
                                Ch = i,
                            };
                            BaseListOfIOItems.Add(_BfMdfIOCh);
                            int _id = _BfMdfIOCh.Id.Value;
                            //GetIOConfigRequest(_id);
                        }
                    }
                }
                catch (Exception e)
                {
                    //OnGetAIHttpRequestError(e);
                }

            }

        }
        private void UpdateAOUIStatus()//Event to update data
        {
            float fValue = 0.0f; int AOid_offset = 60;
            try
            {
                for (int i = 0; i < Device.AoTotal; i++)//get each of channel info
                {
                    IOModel BfMdfIOCh = GetIO(i + AOid_offset);
                    if (BfMdfIOCh.Id != null && BfMdfIOCh.Ch == i)
                    {
                        var temp = BfMdfIOCh;
                        {
                            byte rangeCode = GetRngCode(i);//m_adamConfig.TypeCode;
                            //if (Device.ModuleType == "Adam4021")//ADAM-4021
                            //    adamCom.AnalogOutput(m_iAddr).GetCurrentValue((byte)m_adamConfig.Format, out fValue);
                            //else if (Device.ModuleType == "Adam4024")//ADAM-4024
                            //    adamCom.AnalogOutput(m_iAddr).GetCurrentValue(i, out fValue);

                            temp.Val = Convert.ToInt32(fValue * 1000);//(int)(ai_values[i] * 1000);
                            temp.Val_Eg = "";//fValue.ToString("#0.000") + " "
                            //+ AnalogOutput.GetUnitName(m_Adam4000Type, rangeCode/*m_adamConfig.TypeCode*/);
                            temp.Rng = ReturnFormalRng(rangeCode);//rangeCode;
                        }
                        int Idx = BaseListOfIOItems.IndexOf(BfMdfIOCh);
                        BaseListOfIOItems.Remove(BfMdfIOCh);
                        BaseListOfIOItems.Insert(Idx, temp);
                    }
                    else if (BfMdfIOCh.Id == null)
                    {
                        IOModel _BfMdfIOCh = new IOModel()
                        {
                            Id = AOid_offset + i,
                            Ch = i,
                        };
                        BaseListOfIOItems.Add(_BfMdfIOCh);
                        int _id = _BfMdfIOCh.Id.Value;
                        //GetIOConfigRequest(_id);
                    }
                }
            }
            catch (Exception e)
            {
                //OnGetAIHttpRequestError(e);
            }
        }

        private bool SendCmdToConfig(int ch, int typ)
        {
            int result = 0;
            if (Device.ModuleType == "Adam6015")
            {
                if (typ == (int)ValueRange.Pt385_Neg50to150) result = 32;
                else if (typ == (int)ValueRange.Pt385_0to100) result = 33;
                else if (typ == (int)ValueRange.Pt385_0to200) result = 34;
                else if (typ == (int)ValueRange.Pt385_0to400) result = 35;
                else if (typ == (int)ValueRange.Pt385_Neg200to200) result = 36;
                else if (typ == (int)ValueRange.Pt392_Neg50to150) result = 37;
                else if (typ == (int)ValueRange.Pt392_0to100) result = 38;
                else if (typ == (int)ValueRange.Pt392_0to200) result = 39;
                else if (typ == (int)ValueRange.Pt392_0to400) result = 40;
                else if (typ == (int)ValueRange.Pt392_Neg200to200) result = 41;
                else if (typ == (int)ValueRange.Pt1000_Neg40to160) result = 42;
                else if (typ == (int)ValueRange.BALCO500_Neg30to120) result = 43;
                else if (typ == (int)ValueRange.Ni518_Neg80to100) result = 44;
                else if (typ == (int)ValueRange.Ni518_0to100) result = 45;
                //else result = typ;
            }
            else if (Device.ModuleType == "Adam6017")
            {
                //Only test for ADAM-6017-CE cersion command.
                if (typ == (int)ValueRange.mV_Neg150To150) result = 103;
                else if (typ == (int)ValueRange.mV_Neg500To500) result = 104;
                else if (typ == (int)ValueRange.V_Neg1To1) result = 140;
                else if (typ == (int)ValueRange.V_Neg5To5) result = 142;
                else if (typ == (int)ValueRange.V_Neg10To10) result = 143;
                else if (typ == (int)ValueRange.mA_0To20) result = 182;
                else if (typ == (int)ValueRange.mA_4To20) result = 180;
                else if (typ == (int)ValueRange.mV_0To150) result = 105;// using $aaBRCnn to read if ADAM-6017-CE is applied
                else if (typ == (int)ValueRange.mV_0To500) result = 106;// using $aaBRCnn to read if ADAM-6017-CE is applied
                else if (typ == (int)ValueRange.V_0To1) result = 145;// using $aaBRCnn to read if ADAM-6017-CE is applied
                else if (typ == (int)ValueRange.V_0To5) result = 147;// using $aaBRCnn to read if ADAM-6017-CE is applied
                else if (typ == (int)ValueRange.V_0To10) result = 148;// using $aaBRCnn to read if ADAM-6017-CE is applied
                else if (typ == (int)ValueRange.mA_Neg20To20) result = 181;// using $aaBRCnn to read if ADAM-6017-CE is applied
                //needs to use ASCII code to change.
                string cmd = "$01A" + ch.ToString("00") + result.ToString("0000");
                if(SendCmd(cmd) == "!01\r")
                    return true;
                else
                    return false;

                //if ()
                //    return true;
                //if (typ == (int)ValueRange.mV_Neg150To150) result = 12;
                //else if (typ == (int)ValueRange.mV_Neg500To500) result = 11;
                //else if (typ == (int)ValueRange.V_Neg1To1) result = 10;
                //else if (typ == (int)ValueRange.V_Neg5To5) result = 9;
                //else if (typ == (int)ValueRange.V_Neg10To10) result = 8;
                //else if (typ == (int)ValueRange.mA_0To20) result = 13;
                //else if (typ == (int)ValueRange.mA_4To20) result = 7;
            }
            else if (Device.ModuleType == "Adam6018")
            {
                if (typ == (int)ValueRange.Jtype_0To760C) result = 14;
                else if (typ == (int)ValueRange.Ktype_0To1370C) result = 15;
                else if (typ == (int)ValueRange.Ttype_Neg100To400C) result = 16;
                else if (typ == (int)ValueRange.Etype_0To1000C) result = 17;
                else if (typ == (int)ValueRange.Rtype_500To1750C) result = 18;
                else if (typ == (int)ValueRange.Stype_500To1750C) result = 19;
                else if (typ == (int)ValueRange.Btype_500To1800C) result = 20;
                //else result = typ;
            }
            else return false;

            if (adamModbus.AnalogInput().SetInputRange(ch, (byte)result))
                return true;

            return false;
        }
        private bool SetEnableStatus(int ch, int typ)
        {
            int m_iAiTotal = Device.AiTotal;
            if (m_bChEnabled != null)
            {
                m_bChEnabled[ch] = Convert.ToBoolean(typ);
            }

            if (adamModbus.AnalogInput().SetChannelEnabled(m_bChEnabled))
                return true;

            return false;
        }
        /// <summary>
        /// for ADAM-6000 command set
        /// $01Caabbccdd......
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_typ"></param>
        /// <returns></returns>
        //private bool SetDIO_ModeConfig(int _id, int _typ)
        private bool SetDIO_ModeConfig(IOModel mod)
        {
            int m_iDiTotal = Device.DiTotal;
            string tpyStr = SpiltStr(SendCmd("$01C"), '!', '\r'); 
            ArrayList temp_list = new ArrayList();
            //get DIO mode type
            for (int i = 0; i < tpyStr.Length; i += 2)
            {
                var res = tpyStr.ToCharArray(i, 2);
                string temp = "";
                foreach (var tChar in res)
                {
                    temp += tChar.ToString();
                }
                temp_list.Add(temp);
            }
            if (temp_list.Count < 1) return false;
            var array = temp_list.ToArray();
            if (mod.Id/*_id*/ >= (int)DevIDDefine.DO)
            {
                int ch = mod.Ch;//_id - (int)DevIDDefine.DO + m_iDiTotal;
                string typ_S = "";
                if (mod.Md == 0) typ_S = "00";
                else if (mod.Md == 1) typ_S = "01";
                else if (mod.Md == 2) typ_S = "02";
                else if (mod.Md == 3) typ_S = "03";
                else if (mod.Md == 4) typ_S = "04";
                array[ch] = typ_S;
            }
            else
            {
                int ch = mod.Ch;// _id; 
                string typS = "";
                //if (_typ == 160) typS = "A0";
                //else if(_typ == 161) typS = "A1";
                //else if (_typ == 162) typS = "A2";
                //else if (_typ == 163) typS = "A3";
                //else if (_typ == 164) typS = "A4";
                if (mod.Inv == 1 && mod.Fltr == 1)
                    typS = "E" + mod.Md.ToString();
                else if (mod.Inv == 0 && mod.Fltr == 1)
                    typS = "6" + mod.Md.ToString();
                else typS = "A" + mod.Md.ToString();

                array[ch] = typS;
            }
            string subCmd = "";
            foreach(var _item in array)
            {
                subCmd += _item.ToString();
            }
            string cmd = "$01C" + subCmd;
            if (SendCmd(cmd) == ">01\r")
                return true;

            return false;
        }
        private bool Set_DefaultMod()
        {
            int m_iDiTotal = Device.DiTotal, m_iDoTotal = Device.DoTotal;
            string[] ArrayStr = new string[m_iDiTotal + m_iDoTotal];
            ArrayStr.Initialize();
            for (int i = 0; i < m_iDiTotal; i++)
            {
                if (m_Adam6000Type == Adam6000Type.Adam6052)
                    ArrayStr[i] = "60";
                else
                    ArrayStr[i] = "A0";
            }
            for (int i = m_iDiTotal; i < m_iDiTotal + m_iDoTotal; i++)
            {
                    ArrayStr[i] = "00";
            }


            //string tpyStr = SpiltStr(SendCmd("$01C"), '!', '\r');
            //ArrayList temp_list = new ArrayList();
            ////get DIO mode type
            //for (int i = 0; i < tpyStr.Length; i += 2)
            //{
            //    var res = tpyStr.ToCharArray(i, 2);
            //    string temp = "";
            //    foreach (var tChar in res)
            //    {
            //        temp += tChar.ToString();
            //    }
            //    temp_list.Add(temp);
            //}
            //if (temp_list.Count < 1) return false;
            //var array = temp_list.ToArray();
            //if (mod.Id/*_id*/ >= (int)DevIDDefine.DO)
            //{
            //    int ch = mod.Ch;//_id - (int)DevIDDefine.DO + m_iDiTotal;
            //    string typ_S = "";
            //    if (mod.Md == 0) typ_S = "00";
            //    else if (mod.Md == 1) typ_S = "01";
            //    else if (mod.Md == 2) typ_S = "02";
            //    else if (mod.Md == 3) typ_S = "03";
            //    else if (mod.Md == 4) typ_S = "04";
            //    array[ch] = typ_S;
            //}
            //else
            //{
            //    int ch = mod.Ch;// _id; 
            //    string typS = "";
            //    //if (_typ == 160) typS = "A0";
            //    //else if(_typ == 161) typS = "A1";
            //    //else if (_typ == 162) typS = "A2";
            //    //else if (_typ == 163) typS = "A3";
            //    //else if (_typ == 164) typS = "A4";
            //    if (mod.Inv == 1 && mod.Fltr == 1)
            //        typS = "E" + mod.Md.ToString();
            //    else if (mod.Inv == 0 && mod.Fltr == 1)
            //        typS = "6" + mod.Md.ToString();
            //    else typS = "A" + mod.Md.ToString();

            //    array[ch] = typS;
            //}
            string subCmd = "";
            foreach (var _item in ArrayStr)
            {
                subCmd += _item.ToString();
            }
            string cmd = "$01C" + subCmd;
            if (SendCmd(cmd) == ">01\r")
                return true;

            return false;
        }

        private float OutputByRng(int rng, int val)
        {
            float result = 0.0f;
            if (rng == (int)ValueRange.mA_0To20 || rng == (int)ValueRange.mA_4To20
                || rng == (int)ValueRange.mA_Neg20To20)
            {
                result = Convert.ToSingle(val);
            }
            else result = Convert.ToSingle((double)val / 1000);//Val unit is Int.

            return result;
        }

        private byte GetRngCode(int ch)//need to get correct rng code by different module
        {
            byte byRange = 0;ushort usRange = 0;
            if (m_DeviceFwVer < m_Adam6000NewerFwVer)
            {
                if (adamModbus.AnalogInput().GetInputRange(ch, out byRange))
                    m_byRange[ch] = byRange;
            }
            else
            {
                if (adamModbus.AnalogInput().GetInputRange(ch, out usRange))
                    m_usRange[ch] = usRange;
            }


            //if (Device.ModuleType == "Adam6017")
            //{
            //    string cmd = "$01B" + ch.ToString("00");
            //    //string res = SendCmd(cmd);
            //    string res = SpiltStr(SendCmd(cmd), '!', '\r');
            //    //得到16進位的值轉成10進位
            //    if (res == "07") ubyRange = 7;
            //    else if (res == "08") ubyRange = 8;
            //    else if (res == "09") ubyRange = 9;
            //    else if (res == "0A") ubyRange = 10;
            //    else if (res == "0B") ubyRange = 11;
            //    else if (res == "0C") ubyRange = 12;
            //    else if (res == "0D") ubyRange = 13;
            //    else if (res == "0148") ubyRange = 328;
            //    else if (res == "0147") ubyRange = 327;
            //    else if (res == "0145") ubyRange = 325;
            //    else if (res == "0106") ubyRange = 262;
            //    else if (res == "0105") ubyRange = 261;
            //    else if (res == "0181") ubyRange = 385;

            //    if (m_DeviceFwVer < m_Adam6000NewerFwVer)
            //        m_byRange[ch] = (byte)ubyRange;
            //    else
            //        m_usRange[ch] = ubyRange;
            //}
            //else
            //{
            //    if (adamModbus.AnalogInput().GetInputRange(ch, out byRange))
            //        m_byRange[ch] = byRange;
            //}

            return byRange;
        }
        private bool GetDIO_ModeConfig(out int[] bMd, out int[] bInv, out int[] bFlt)
        {
            bMd = new int[1]; bInv = new int[1]; bFlt = new int[1];
            int m_iDiTotal = Device.DiTotal;
            string tpyStr = SpiltStr(SendCmd("$01C"), '!', '\r');
            ArrayList temp_list = new ArrayList();
            //get DIO mode type
            for (int i = 0; i < tpyStr.Length; i +=2)
            {
                var res = tpyStr.ToCharArray(i, 2);
                string temp = "";
                foreach (var tChar in res)
                {
                    temp += tChar.ToString();
                }
                temp_list.Add(temp);
            }
            if (temp_list.Count < 1) return false;
            var array = temp_list.ToArray();
            bMd = new int[array.Length]; bInv = new int[array.Length]; bFlt = new int[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                string temp = (string)array[i];
                ulong number = UInt64.Parse(temp, System.Globalization.NumberStyles.HexNumber);
                byte rByte = Convert.ToByte(number);
                string binaryString = Convert.ToString(rByte, 2);
                var in_array = binaryString.ToCharArray();
                //補足8bit並反向
                char[] chr_array = new char[8];
                for (int j = 0; j < 8; j++)
                {
                    if (j < in_array.Length)
                        chr_array[j] = in_array[in_array.Length - 1 - j];
                }
                //
                if (i< m_iDiTotal)
                {   //detect binary formate
                    bInv[i] = chr_array[7] == '1' ? 1 : 0;
                    bFlt[i] = chr_array[6] == '1' ? 1 : 0;
                    if (chr_array[2] == '1'&& chr_array[1] == '0' && chr_array[0] == '0') bMd[i] = 4;//0100
                    else if (chr_array[2] == '0' && chr_array[1] == '1' && chr_array[0] == '1') bMd[i] = 3;//0011
                    else if (chr_array[2] == '0' && chr_array[1] == '1' && chr_array[0] == '0') bMd[i] = 2;//0010
                    else bMd[i] = 1;//0001
                }
                else
                {   //00
                    if (temp == "04") bMd[i] = 4;
                    else if (temp == "03") bMd[i] = 3;
                    else if (temp == "02") bMd[i] = 2;
                    else bMd[i] = 1;
                }
                
                
            }

            return false;
        }

        private int ReturnFormalRng(int code)//need to return formal rng code by different module
        {
            ValueRange Rng = new ValueRange();
            if (Device.ModuleType == "Adam6017")
            {
                switch (code)
                {
                    case 7: Rng = ValueRange.mA_4To20; break;
                    case 13: Rng = ValueRange.mA_0To20; break;
                    case 8: Rng = ValueRange.V_Neg10To10; break;
                    case 9: Rng = ValueRange.V_Neg5To5; break;
                    case 10: Rng = ValueRange.V_Neg1To1; break;
                    case 11: Rng = ValueRange.mV_Neg500To500; break;
                    case 12: Rng = ValueRange.mV_Neg150To150; break;
                    
                    case 328: Rng = ValueRange.V_0To10; break;
                    case 327: Rng = ValueRange.V_0To5; break;
                    case 325: Rng = ValueRange.V_0To1; break;
                    case 262: Rng = ValueRange.mV_0To500; break;
                    case 261: Rng = ValueRange.mV_0To150; break;
                    case 385: Rng = ValueRange.mA_Neg20To20; break;
                    case 384: Rng = ValueRange.mA_4To20; break;
                    case 386: Rng = ValueRange.mA_0To20; break;
                    case 323: Rng = ValueRange.V_Neg10To10; break;
                    case 322: Rng = ValueRange.V_Neg5To5; break;
                    case 320: Rng = ValueRange.V_Neg1To1; break;
                    case 260: Rng = ValueRange.mV_Neg500To500; break;
                    case 259: Rng = ValueRange.mV_Neg150To150; break;
                    default:
                        Rng = 0; break;
                }
            }

                //else if (Device.ModuleType == "Adam4011" || Device.ModuleType == "Adam4011D"
                //            || Device.ModuleType == "Adam4018" || Device.ModuleType == "Adam4018P"
                //            || Device.ModuleType == "Adam4018M"
                //            || Device.ModuleType == "Adam4117" || Device.ModuleType == "Adam4118")
                //{
                //    switch (code)
                //    {
                //        case 0: Rng = ValueRange.mV_Neg15To15; break;
                //        case 1: Rng = ValueRange.mV_Neg50To50; break;
                //        case 2: Rng = ValueRange.mV_Neg100To100; break;
                //        case 3: Rng = ValueRange.mV_Neg500To500; break;
                //        case 4: Rng = ValueRange.V_Neg1To1; break;
                //        case 5: Rng = ValueRange.V_Neg2pt5To2pt5; break;
                //        case 6: Rng = ValueRange.mA_Neg20To20; break;
                //        case 7: Rng = ValueRange.mA_4To20; break;
                //        case 8: Rng = ValueRange.V_Neg10To10; break;
                //        case 9: Rng = ValueRange.V_Neg5To5; break;
                //        case 10: Rng = ValueRange.V_Neg1To1; break;
                //        case 11: Rng = ValueRange.mV_Neg500To500; break;
                //        case 12: Rng = ValueRange.mV_Neg150To150; break;
                //        case 13: Rng = ValueRange.mA_Neg20To20; break;
                //        //ADAM-41XX AI
                //        case 21: Rng = ValueRange.V_Neg15To15; break;
                //        case 72: Rng = ValueRange.V_0To10; break;
                //        case 73: Rng = ValueRange.V_0To5; break;
                //        case 74: Rng = ValueRange.V_0To1; break;
                //        case 75: Rng = ValueRange.mV_0To500; break;
                //        case 76: Rng = ValueRange.mV_0To150; break;
                //        case 77: Rng = ValueRange.mA_0To20; break;
                //        case 85: Rng = ValueRange.V_0To15; break;
                //    }
                //}
                //else if (Device.ModuleType == "Adam4019P")
                //{
                //    switch (code)
                //    {
                //        case 2: Rng = ValueRange.mV_Neg100To100; break;
                //        case 3: Rng = ValueRange.mV_Neg500To500; break;
                //        case 4: Rng = ValueRange.V_Neg1To1; break;
                //        case 5: Rng = ValueRange.V_Neg2pt5To2pt5; break;
                //        case 7: Rng = ValueRange.mA_4To20; break;
                //        case 8: Rng = ValueRange.V_Neg10To10; break;
                //        case 9: Rng = ValueRange.V_Neg5To5; break;
                //        case 10: Rng = ValueRange.mA_Neg20To20; break;
                //    }
                //}
                //else if (Device.ModuleType == "Adam4015" || Device.ModuleType == "Adam4015T")
                //{
                //    switch (code)
                //    {
                //        case 32: Rng = ValueRange.Pt385_Neg50to150; break;
                //        case 33: Rng = ValueRange.Pt385_0to100; break;
                //        case 34: Rng = ValueRange.Pt385_0to200; break;
                //        case 35: Rng = ValueRange.Pt385_0to400; break;
                //        case 36: Rng = ValueRange.Pt385_Neg200to200; break;
                //        case 37: Rng = ValueRange.Pt3916_Neg50to150; break;
                //        case 38: Rng = ValueRange.Pt3916_0to100; break;
                //        case 39: Rng = ValueRange.Pt3916_0to200; break;
                //        case 40: Rng = ValueRange.Pt3916_0to400; break;
                //        case 41: Rng = ValueRange.Pt3916_Neg200to200; break;
                //        case 42: Rng = ValueRange.Pt1000_Neg40to160; break;
                //        case 43: Rng = ValueRange.BALCO500_Neg30to120; break;
                //        case 44: Rng = ValueRange.Ni604_Neg80to100; break;
                //        case 45: Rng = ValueRange.Ni604_0to100; break;
                //        case 48: Rng = ValueRange.Thermistor_3K_0To100; break;
                //        case 49: Rng = ValueRange.Thermistor_10K_0To100; break;
                //        case 50: Rng = ValueRange.Ni508_Neg50to200; break;
                //    }
                //}
                //else if (Device.ModuleType == "Adam4021")
                //{
                //    switch (code)
                //    {
                //        case 48: Rng = ValueRange.mA_0To20; break;
                //        case 49: Rng = ValueRange.mA_4To20; break;
                //        case 50: Rng = ValueRange.V_0To10; break;
                //    }
                //}
                //else if (Device.ModuleType == "Adam4024")
                //{
                //    switch (code)
                //    {
                //        case 48: Rng = ValueRange.mA_0To20; break;
                //        case 49: Rng = ValueRange.mA_4To20; break;
                //        case 50: Rng = ValueRange.V_Neg10To10; break;
                //    }
                //}


                return (int)Rng;
        }

        private Adam6000Type GetModuleType(string recev)
        {
            Adam6000Type type = new Adam6000Type();
            string modtype = SpiltStr(recev, '!', '\r');
            if (modtype == "6015") type = Adam6000Type.Adam6015;
            else if (modtype == "6017") type = Adam6000Type.Adam6017;
            else if (modtype == "6018") type = Adam6000Type.Adam6018;
            //AO
            else if (modtype == "6024") type = Adam6000Type.Adam6024;
            //DIO
            else if (modtype == "6050") type = Adam6000Type.Adam6050;
            else if (modtype == "6051") type = Adam6000Type.Adam6051;
            else if (modtype == "6052") type = Adam6000Type.Adam6052;
            else if (modtype == "6060") type = Adam6000Type.Adam6060;
            else if (modtype == "6066") type = Adam6000Type.Adam6066;
            else type = Adam6000Type.Non;
            return type;
        }

        private string SpiltStr(string str, char a, char b) // Spilt string input
        {
            string res = "";
            char[] array = str.ToCharArray();
            for (int i = 0; i < array.Length; i++)
            {
                if (i == 0 && array[i] != a) break;
                else if (i > 2 && array[i] != b)
                {
                    res += array[i].ToString();
                }
            }

            return res;
        }
        private int FloatToRowData(int _ch, float val)
        {
            int res = 0;int _rngCode = 0;
            if (m_DeviceFwVer < m_Adam6000NewerFwVer)
                _rngCode = m_byRange[_ch];
            else
                _rngCode = m_usRange[_ch];

            if (_rngCode == (int)ValueRange.V_0To10)
                res = (int)((val) * 65535 / 10);
            else if (_rngCode == (int)ValueRange.V_0To5)
                res = (int)((val) * 65535 / 5);
            else if (_rngCode == (int)ValueRange.V_0To1)
                res = (int)((val) * 65535 / 1);
            else if (_rngCode == (int)ValueRange.mV_0To500)
                res = (int)((val) * 65535 / 500);
            else if (_rngCode == (int)ValueRange.mV_0To150)
                res = (int)((val) * 65535 / 150);
            else if (_rngCode == (int)ValueRange.V_Neg10To10)
                res = (int)((val + 10) * 65535 / 20);
            else if (_rngCode == (int)ValueRange.V_Neg5To5)
                res = (int)((val + 5) * 65535 / 10);
            else if (_rngCode == (int)ValueRange.V_Neg1To1)
                res = (int)((val + 1) * 65535 / 2);
            else if (_rngCode == (int)ValueRange.mV_Neg500To500)
                res = (int)((val + 500) * 65535 / 1000);
            else if (_rngCode == (int)ValueRange.mV_Neg150To150)
                res = (int)((val + 150) * 65535 / 300);
            else if (_rngCode == (int)ValueRange.mA_0To20)
                res = (int)((val + 0) * 65535 / 20);
            else if (_rngCode == (int)ValueRange.mA_4To20)
                res = (int)((val - 4) * 65535 / 16);
            else if (_rngCode == (int)ValueRange.mA_Neg20To20)
                res = (int)((val + 20) * 65535 / 40);
            //if (mod == (int)ValueRange.mA_4To20
            //                    || mod == (int)ValueRange.mA_0To20
            //                    || mod == (int)ValueRange.mA_Neg20To20)
            //{
            //    res = (uint)(var * 65535 / 20);
            //}
            //else
            //    res = (uint)((var + 10) * 65535 / 20);
            return res;
        }
        

        public enum DevIDDefine
        {
            DI = 0,
            DO = 20,//offset 20
            AI = 40,//offset 40
            AO = 60,//offset 60
        }

    }//class

    public class ADAM6K_IO_Model : DeviceModel
    {
        public ValueRange[] AIRng;
        public ValueRange[] AORng;

        public ADAM6K_IO_Model(Adam6000Type typ)
        {
            ModuleType = typ.ToString();
            if (typ == Adam6000Type.Adam6017)
            {
                AiTotal = 8;
                AIRng = new ValueRange[13]; AIRng.Initialize();
                AIRng[0] = ValueRange.mV_Neg150To150; AIRng[1] = ValueRange.mV_Neg500To500;
                AIRng[2] = ValueRange.V_Neg1To1; AIRng[3] = ValueRange.V_Neg5To5;
                AIRng[4] = ValueRange.V_Neg10To10;
                AIRng[5] = ValueRange.mV_0To150; AIRng[6] = ValueRange.mV_0To500;
                AIRng[7] = ValueRange.V_0To1; AIRng[8] = ValueRange.V_0To5;
                AIRng[9] = ValueRange.V_0To10;
                AIRng[10] = ValueRange.mA_Neg20To20;
                AIRng[11] = ValueRange.mA_0To20; AIRng[12] = ValueRange.mA_4To20;
            }
            else if (typ == Adam6000Type.Adam6015)
            {
                AiTotal = 7;
                AIRng = new ValueRange[14]; AIRng.Initialize();
                AIRng[0] = ValueRange.Pt385_Neg50to150; AIRng[1] = ValueRange.Pt385_0to100;
                AIRng[2] = ValueRange.Pt385_0to200; AIRng[3] = ValueRange.Pt385_0to400;
                AIRng[4] = ValueRange.Pt385_Neg200to200; AIRng[5] = ValueRange.Pt392_Neg50to150;
                AIRng[6] = ValueRange.Pt392_0to100; AIRng[7] = ValueRange.Pt392_0to200;
                AIRng[8] = ValueRange.Pt392_0to400; AIRng[9] = ValueRange.Pt392_Neg200to200;
                AIRng[10] = ValueRange.Pt1000_Neg40to160;
                AIRng[11] = ValueRange.BALCO500_Neg30to120;
                AIRng[12] = ValueRange.Ni518_Neg80to100; AIRng[13] = ValueRange.Ni518_0to100;
            }
            else if (typ == Adam6000Type.Adam6018)
            {
                AiTotal = 8;
                AIRng = new ValueRange[7]; AIRng.Initialize();
                AIRng[0] = ValueRange.Jtype_0To760C; AIRng[1] = ValueRange.Ktype_0To1370C;
                AIRng[2] = ValueRange.Ttype_Neg100To400C; AIRng[3] = ValueRange.Etype_0To1000C;
                AIRng[4] = ValueRange.Rtype_500To1750C; AIRng[5] = ValueRange.Stype_500To1750C;
                AIRng[6] = ValueRange.Btype_500To1800C;
            }
            else if (typ == Adam6000Type.Adam6024)
            {
                AiTotal = 6;
                AIRng = new ValueRange[3]; AIRng.Initialize();
                AIRng[0] = ValueRange.V_Neg10To10;
                AIRng[1] = ValueRange.mA_0To20; AIRng[2] = ValueRange.mA_4To20;

                AoTotal = 2;
                AORng = new ValueRange[3]; AORng.Initialize();
                AORng[0] = ValueRange.mA_0To20; AORng[1] = ValueRange.mA_4To20;
                AORng[2] = ValueRange.V_0To10;
            }
            else if (typ == Adam6000Type.Adam6050)
            {
                DiTotal = 12; DoTotal = 6;
            }
            else if (typ == Adam6000Type.Adam6051)
            {
                DiTotal = 12; DoTotal = 2;
                CntTotal = 2;
            }
            else if (typ == Adam6000Type.Adam6052)
            {
                DiTotal = 8; DoTotal = 8;
            }
            else if (typ == Adam6000Type.Adam6060)
            {
                DiTotal = 6; DoTotal = 6;
            }
            else if (typ == Adam6000Type.Adam6066)
            {
                DiTotal = 6; DoTotal = 6;
            }
        }

    }
}
