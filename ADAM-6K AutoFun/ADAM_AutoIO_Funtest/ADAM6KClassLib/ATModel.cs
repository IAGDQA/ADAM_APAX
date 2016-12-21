namespace Model
{
    public class ADAM_IO_Model : IOListData
    {
        public string ModelType;
        public int DI_num;
        public int DO_num;
        public int AI_num;
        public int AO_num;
        public int Cnt_num;
        public int APAX_AO_HiVal;
        public int APAX_AO_MdVal;
        public int APAX_AO_LoVal;

        public int[] AIRng;
        public int[] AORng;

        /// <summary>
        /// main
        /// </summary>
        public ADAM_IO_Model(string _typ)
        {
            if (_typ == "Adam6015")
            {
                ModelType = _typ;
                AI_num = 7;
                AIRng = new int[14]; AIRng.Initialize();
                AIRng[0] = (int)ValueRange.Pt385_Neg50to150; AIRng[1] = (int)ValueRange.Pt385_0to100;
                AIRng[2] = (int)ValueRange.Pt385_0to200; AIRng[3] = (int)ValueRange.Pt385_0to400;
                AIRng[4] = (int)ValueRange.Pt385_Neg200to200; AIRng[5] = (int)ValueRange.Pt392_Neg50to150;
                AIRng[6] = (int)ValueRange.Pt392_0to100; AIRng[7] = (int)ValueRange.Pt392_0to200;
                AIRng[8] = (int)ValueRange.Pt392_0to400; AIRng[9] = (int)ValueRange.Pt392_Neg200to200;
                AIRng[10] = (int)ValueRange.Pt1000_Neg40to160;
                AIRng[11] = (int)ValueRange.BALCO500_Neg30to120;
                AIRng[12] = (int)ValueRange.Ni518_Neg80to100; AIRng[13] = (int)ValueRange.Ni518_0to100;
                APAX_AO_HiVal = 10000;
                APAX_AO_MdVal = 5000;
                APAX_AO_LoVal = 0;
            }
            else if (_typ == "Adam6017")
            {
                ModelType = _typ;
                AI_num = 8; DO_num = 2;
                AIRng = new int[13];
                AIRng[0] = (int)ValueRange.mV_0To150; AIRng[1] = (int)ValueRange.mV_0To500; AIRng[2] = (int)ValueRange.V_0To1;
                AIRng[3] = (int)ValueRange.V_0To5; AIRng[4] = (int)ValueRange.V_0To10; AIRng[5] = (int)ValueRange.mV_Neg150To150;
                AIRng[6] = (int)ValueRange.mV_Neg500To500; AIRng[7] = (int)ValueRange.V_Neg1To1; AIRng[8] = (int)ValueRange.V_Neg5To5;
                AIRng[9] = (int)ValueRange.V_Neg10To10; AIRng[10] = (int)ValueRange.mA_4To20; AIRng[11] = (int)ValueRange.mA_0To20;
                AIRng[12] = (int)ValueRange.mA_Neg20To20;
                APAX_AO_HiVal = 10000;
                APAX_AO_MdVal = 5000;
                APAX_AO_LoVal = 0;
            }
            else if (_typ == "Adam6018")
            {
                ModelType = _typ;
                AI_num = 8;
                AIRng = new int[7];
                AIRng[0] = (int)ValueRange.Jtype_0To760C; AIRng[1] = (int)ValueRange.Ktype_0To1370C;
                AIRng[2] = (int)ValueRange.Ttype_Neg100To400C; AIRng[3] = (int)ValueRange.Etype_0To1000C;
                AIRng[4] = (int)ValueRange.Rtype_500To1750C; AIRng[5] = (int)ValueRange.Stype_500To1750C;
                AIRng[6] = (int)ValueRange.Btype_500To1800C;
                APAX_AO_HiVal = 10000;
                APAX_AO_MdVal = 5000;
                APAX_AO_LoVal = 0;
            }
            else if (_typ == "Adam6024")
            {
                ModelType = _typ;
                AI_num = 6;
                AIRng = new int[3];
                AIRng[0] = (int)ValueRange.V_Neg10To10;
                AIRng[1] = (int)ValueRange.mA_0To20; AIRng[2] = (int)ValueRange.mA_4To20;
                APAX_AO_HiVal = 10000;
                APAX_AO_MdVal = 5000;
                APAX_AO_LoVal = 0;
                AO_num = 2;
                AORng = new int[3];
                AORng[0] = (int)ValueRange.mA_0To20; AORng[1] = (int)ValueRange.mA_4To20;
                AORng[2] = (int)ValueRange.V_0To10;
            }
            else if (_typ == "Adam6050")
            {
                ModelType = _typ;
                DI_num = 12; DO_num = 6;
            }
            else if (_typ == "Adam6051")
            {
                ModelType = _typ;
                DI_num = 12; DO_num = 2;
                Cnt_num = 2;
            }
            else if (_typ == "Adam6052")
            {
                ModelType = _typ;
                DI_num = 8; DO_num = 8;
            }
            else if (_typ == "Adam6060")
            {
                ModelType = _typ;
                DI_num = 6; DO_num = 6;
            }
            else if (_typ == "Adam6066")
            {
                ModelType = _typ;
                DI_num = 6; DO_num = 6;
            }

        }

    }
}