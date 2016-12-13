using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Text;
using Model;
//
using System.Net.Sockets;
using Advantech.Adam;
using Advantech.Common;

namespace Service
{
    /// <summary>
    /// 20151016    Create new function
    /// </summary>
    public class ModbusTCPService
    {
        DeviceModel Device;
        private AdamSocket adamTCP;
        private AdamCom adamCom;
        //Parameter
        private int m_iPort = 502, modbusTimeout = 3000;
        private int m_iStart, m_iLength, m_oStart, m_oLength;
        private bool m_connFlg, m_bRegister, m_bStart;

        /// <summary>
        /// main
        /// </summary>
        public ModbusTCPService()
        {

        }

        public bool ModbusConnection(DeviceModel obj)
        {
            if (obj.IPAddress == "" || obj.IPAddress == null) return false;

            Device = new DeviceModel()//20150626 建立一個DeviceModel給所有Service
            {
                IPAddress = obj.IPAddress,
                Port = 1,//obj.ModbusAddr,
                //ModbusTimeOut = 3000,//obj.ModbusTimeOut,
                //MbCoils = obj.MbCoils,
                //MbRegs = obj.MbRegs,
            };

            //
            adamTCP = new AdamSocket();
            adamTCP.SetTimeout(1000, Device.Port, modbusTimeout); // set timeout for TCP
            if (adamTCP.Connect(Device.IPAddress, ProtocolType.Tcp, m_iPort))
            {
                m_connFlg = true;
                return true;
            }
            m_connFlg = false;
            return false;
        }

        public void DisConnection()
        {
            if (adamTCP != null) adamTCP.Disconnect();	// disconnect slave
        }
        bool[] bData_in;
        public bool[] ReadCoils(int _idx, int _len)
        {
            bData_in = new bool[_len]; bool[] t_Data_in;
            if (adamTCP.Modbus(Device.Port).ReadCoilStatus(_idx, _len, out t_Data_in) && m_connFlg)
            {
                bData_in = t_Data_in;
            }
            return bData_in;
        }
        int[] rData_int;
        public int[] ReadHoldingRegs(int _idx, int _len)
        {
            rData_int = new int[_len]; int[] t_rData_in;
            if (adamTCP.Modbus(Device.Port).ReadHoldingRegs(_idx, _len, out t_rData_in) && m_connFlg)
            {
                rData_int = t_rData_in;
            }
            return rData_int;
        }

        public bool ForceSigCoil(int _idx, int _data)
        {
            if (!m_connFlg) return false;
            return adamTCP.Modbus(Device.Port).ForceSingleCoil(_idx, _data);
        }

        public bool ForceMultiCoils(int _idx, bool[] _data)
        {
            if (!m_connFlg) return false;
            return adamTCP.Modbus(Device.Port).ForceMultiCoils(_idx, _data);
        }

        public bool ForceSigReg(int _idx, int _data)
        {
            if (!m_connFlg) return false;
            return adamTCP.Modbus(Device.Port).PresetSingleReg(_idx, _data);
        }

        public bool ForceMultiRegs(int _idx, int[] _data)
        {
            if (!m_connFlg) return false;
            return adamTCP.Modbus(Device.Port).PresetMultiRegs(_idx, _data);
        }




    }//class
}

