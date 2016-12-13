using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Model;
using Service;

namespace ADAM_AutoFun_Demo
{
    /// <summary>
    /// this is demo for AI range code change method.
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
                string typ = ADAM6KReqService.GetDevRng(Device.ModuleType);
                Console.WriteLine("Get range code is [%s].", typ);


                IOModel IOitem = new IOModel()
                {
                    Id = 40,
                    Ch = 0,
                    cRng = 251,
                };
                ADAM6KReqService.UpdateIOConfig(IOitem);
                Console.WriteLine("Change range code is [%s].", IOitem.cRng);

                

            }



            Console.ReadKey();

        }
    }
}
