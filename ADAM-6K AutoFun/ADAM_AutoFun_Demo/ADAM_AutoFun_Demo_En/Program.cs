using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model;
using Service;

namespace ADAM_AutoFun_Demo_En
{
    /// <summary>
    /// this is demo for DIO channel mask change method.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            ADAM6KReqService ADAM6KReqService = new ADAM6KReqService();

            DeviceModel Device = new DeviceModel()
            { IPAddress = "172.18.3.241" };
            if (ADAM6KReqService.OpenCOM(Device.IPAddress))
            {
                Device = ADAM6KReqService.GetDevice();

                IOModel IOitem = new IOModel()
                {
                    Id = 0,
                    Ch = 0,
                    cEn = 0,
                };
                ADAM6KReqService.UpdateIOConfig(IOitem);
                Console.WriteLine("Change channel mask is disable.");



            }



            Console.ReadKey();

        }
    }
}
