using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model;
using Service;

namespace ADAM_AutoFun_Demo_Md
{
    class Program
    {
        static void Main(string[] args)
        {
            ADAM6KReqService ADAM6KReqService = new ADAM6KReqService();

            DeviceModel Device = new DeviceModel()
            { IPAddress = "172.18.3.188" };
            if (ADAM6KReqService.OpenCOM(Device.IPAddress))
            {
                Device = ADAM6KReqService.GetDevice();
                List<IOModel> IO_Data = (List<IOModel>)ADAM6KReqService.GetListOfIOItems("");

                //
                IOModel IOitem = new IOModel();//need to get twice.
                foreach (var item in (List<IOModel>)ADAM6KReqService.GetListOfIOItems(""))
                {
                    if (item.Id == 0 && item.Ch == 0)
                    {
                        IOitem = new IOModel()
                        {
                            Id = item.Id,
                            Ch = item.Ch,
                            Tag = item.Tag,
                            Val = item.Val,
                            En = item.En,
                            //DI
                            Md = item.Md,
                            Inv = item.Inv,
                            Fltr = item.Fltr,
                            FtLo = item.FtLo,
                            FtHi = item.FtHi,
                            FqT = item.FqT,
                            FqP = item.FqP,
                            CntIV = item.CntIV,
                            CntKp = item.CntKp,
                            OvLch = item.OvLch,
                        };
                    }
                }
                IOitem.Md = 2; IOitem.Inv = 0; IOitem.Fltr = 1;
                //    IOModel IOitem = new IOModel()
                //{
                //    Id = 0,
                //    Ch = 0,
                //    cEn = 0,
                //};
                ADAM6KReqService.UpdateIOConfig(IOitem);
                Console.WriteLine("Change channel mask is disable.");



            }



            Console.ReadKey();
        }
    }
}
