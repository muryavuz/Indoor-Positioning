using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LocContract;
using LocDatabase;
using InnerClasses;
using SamplingContract;
using SamplingManager;
using LogManager;
namespace LocAlgorithm
{
    public class SamplingPointManager
    {
        #region 单件模式实现
        static readonly SamplingPointManager instance = new SamplingPointManager();
        public static SamplingPointManager Instance
        {
            get
            {
                return instance;
            }
        }
        static SamplingPointManager()
        {
        }
        SamplingPointManager()
        {
        }
        #endregion

        #region 属性
        //<EnvironmentId,Environment's SamplingPointSet>
        public Dictionary<int, SamplingPointSet> SamplingPointSetsDic;
        //比较器
        private static APRssiPairComparer apRssiPairComparer;
        #endregion

        #region 方法
        public void Init()
        {
            List<int> environmentIdList = new List<int>();
            MySqlDAO dao = new MySqlDAO();
            try
            {
                environmentIdList = DataBase.GetEnvironmentsIdList(dao);
            }
            finally
            {
                dao.Close();
            }

            this.SamplingPointSetsDic = new Dictionary<int, SamplingPointSet>();
            apRssiPairComparer = new APRssiPairComparer();
            foreach (int environmentId in environmentIdList)
            {
                if (!SamplingPointSetsDic.ContainsKey(environmentId))
                {
                    this.SamplingPointSetsDic.Add(environmentId, new SamplingPointSet(environmentId));
                }
            }
            apRssiPairComparer = new APRssiPairComparer();
            Log.AddLogEntry(LogEntryLevel.INFO, "SamplingPointManager初始化完成");
        }



        public static List<string> GetAdjAPMacList(List<APRssiPair> apRssiPairList, int environmentId)
        {
            List<APRssiPair> maxAPRssiPairList = new List<APRssiPair>();
            List<string> adjAPMacList = new List<string>();
            try
            {
                if (apRssiPairList != null && apRssiPairList.Count > 0)
                {
                    apRssiPairList.Sort(apRssiPairComparer);
                    string maxRssiAPMac = apRssiPairList.First().APMac;
                    maxAPRssiPairList.Add(apRssiPairList.ElementAt(0));
                    if (APManager.Instance.APSetsDic[environmentId].APDic.ContainsKey(maxRssiAPMac))
                    {
                        int maxRssiAPMapId = APManager.Instance.APSetsDic[environmentId].APDic[maxRssiAPMac].pos.MapId;
                        int lastMaxRssi = apRssiPairList.First().Rssi;
                        for (int i = 1; i < Parameters.ADJAP_MAX_COUNT; i++)
                        {
                            if (apRssiPairList.Count <= i)
                            {
                                break;
                            }
                            else
                            {
                                APRssiPair p = apRssiPairList.ElementAt(i);
                                if (!APManager.Instance.APSetsDic[environmentId].APDic.ContainsKey(p.APMac))
                                {
                                    continue;
                                }
                                else
                                {
                                    if (maxRssiAPMapId != APManager.Instance.APSetsDic[environmentId].APDic[p.APMac].pos.MapId)
                                    {
                                        //近邻AP跨地图时，以最大信号强度的AP所在地图为准，其余忽视
                                        continue;
                                    }
                                    else
                                    {
                                        if (System.Math.Abs(lastMaxRssi - p.Rssi) <= Parameters.ADJAP_MAX_ADJINTERVAL)
                                        {
                                            maxAPRssiPairList.Add(p);
                                            lastMaxRssi = p.Rssi;
                                            continue;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                foreach (APRssiPair p in maxAPRssiPairList)
                {
                    adjAPMacList.Add(p.APMac);
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "SamplingPointManager.GetAdjAPMacList()执行错误：" + ex.Message);
            }
            return adjAPMacList;
        }


        public static Dictionary<string,int> GetweightAdjAPMacList(List<APRssiPair> apRssiPairList, int environmentId)
        {
            Dictionary<string, int> weightadjAPMacList = new Dictionary<string, int>();
            List<APRssiPair> maxAPRssiPairList = new List<APRssiPair>();
            List<string> adjAPMacList = new List<string>();
            try
            {
                if (apRssiPairList != null && apRssiPairList.Count > 0)
                {
                    apRssiPairList.Sort(apRssiPairComparer);
                    string maxRssiAPMac = apRssiPairList.First().APMac;
                    maxAPRssiPairList.Add(apRssiPairList.ElementAt(0));
                    if (APManager.Instance.APSetsDic[environmentId].APDic.ContainsKey(maxRssiAPMac))
                    {
                        int maxRssiAPMapId = APManager.Instance.APSetsDic[environmentId].APDic[maxRssiAPMac].pos.MapId;
                        int lastMaxRssi = apRssiPairList.First().Rssi;
                        for (int i = 1; i < Parameters.ADJAP_MAX_COUNT; i++)
                        {
                            if (apRssiPairList.Count <= i)
                            {
                                break;
                            }
                            else
                            {
                                APRssiPair p = apRssiPairList.ElementAt(i);
                                if (!APManager.Instance.APSetsDic[environmentId].APDic.ContainsKey(p.APMac))
                                {
                                    continue;
                                }
                                else
                                {
                                    if (maxRssiAPMapId != APManager.Instance.APSetsDic[environmentId].APDic[p.APMac].pos.MapId)
                                    {
                                        //近邻AP跨地图时，以最大信号强度的AP所在地图为准，其余忽视
                                        continue;
                                    }
                                    else
                                    {
                                        if (System.Math.Abs(lastMaxRssi - p.Rssi) <= Parameters.ADJAP_MAX_ADJINTERVAL)
                                        {
                                            maxAPRssiPairList.Add(p);
                                            lastMaxRssi = p.Rssi;
                                            continue;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                foreach (APRssiPair p in maxAPRssiPairList)
                {
                    adjAPMacList.Add(p.APMac);
                    weightadjAPMacList.Add(p.APMac, (p.Rssi + 80) / 10);
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "SamplingPointManager.GetAdjAPMacList()执行错误：" + ex.Message);
            }
            return weightadjAPMacList;
        }
        #endregion
    }
    public class SamplingPointSet
    {
        //<SamplingPoint's VertexId,SamplingPoint's Detail Info>
        public Dictionary<int, SamplingPointDetailInfo> samplingPointDetaiInfoDic;
        //AP近邻采样点集合,add by szh@2013.10.22
        public Dictionary<string, List<int>> apAdjSamplingPointIdDic;
        public Dictionary<int, List<string>> samplingPointIDAdjapDic;
        public int environmentId;
        public Dictionary<int, Dictionary<string, int>> weightsamplingpointadjapdic;
        public SamplingPointSet(int environmentId)
        {
            this.environmentId = environmentId;
            List<SamplingPoint> samplingPoints = new List<SamplingPoint>();
            SamplingDao samplingDAO = new SamplingDao();
           
            MySqlDAO dao = new MySqlDAO();
            try
            {
                samplingPoints = samplingDAO.GetPointListByEnvironemntID(environmentId, dao);
            }
            finally
            {
                dao.Close();
            }
            this.weightsamplingpointadjapdic=new Dictionary<int, Dictionary<string, int>> ();
            this.samplingPointIDAdjapDic=new Dictionary<int,List<string>>();
            this.samplingPointDetaiInfoDic = new Dictionary<int, SamplingPointDetailInfo>();
            this.apAdjSamplingPointIdDic = new Dictionary<string, List<int>>();
            foreach (SamplingPoint p in samplingPoints)
            {
                if (!samplingPointDetaiInfoDic.ContainsKey(p.VertexId))
                {
                    samplingPointDetaiInfoDic.Add(p.VertexId, new SamplingPointDetailInfo(p));
                }
                else
                {
                    samplingPointDetaiInfoDic[p.VertexId] = new SamplingPointDetailInfo(p);
                }
               

                #region 计算AP附近采样点集合
                foreach (SamplingContract.TagType type in samplingPointDetaiInfoDic[p.VertexId].samplingResultDic.Keys)
                {
                    //获取采样结果的List<APRssiPairList> 表示
                    List<APRssiPair> apRssiPairList = new List<APRssiPair>();
                    foreach (var apMac in samplingPointDetaiInfoDic[p.VertexId].samplingResultDic[type].staticsInfoDic.Keys)
                    {
                        apRssiPairList.Add(new APRssiPair
                        {
                            APMac = apMac,
                            Rssi = samplingPointDetaiInfoDic[p.VertexId].samplingResultDic[type].staticsInfoDic[apMac].MeanRssi,
                        });
                    }
                    //获取采样点近邻AP列表
                    List<string> adjAPMacList = SamplingPointManager.GetAdjAPMacList(apRssiPairList,environmentId);
                    samplingPointIDAdjapDic.Add(p.VertexId, adjAPMacList);
                    Dictionary<string,int> weightadjAPMacList = SamplingPointManager.GetweightAdjAPMacList(apRssiPairList, environmentId);
                    weightsamplingpointadjapdic.Add(p.VertexId, weightadjAPMacList);
                    //放入到AP近邻采样点集合
                    foreach (string apMac in adjAPMacList)
                    {
                        if (!apAdjSamplingPointIdDic.ContainsKey(apMac))
                        {
                            apAdjSamplingPointIdDic.Add(apMac, new List<int>());
                        }
                        if (!apAdjSamplingPointIdDic[apMac].Contains(p.VertexId))
                        {
                            apAdjSamplingPointIdDic[apMac].Add(p.VertexId);
                        }
                    }
                }
                #endregion
            }
        }

        public int CalculateEculidDistance(List<APRssiPair> apRssiPairList, int samplingPointId,SamplingContract.TagType type,bool debugEnable)
        {
            int distance = int.MaxValue;
            try
            {
                if (this.samplingPointDetaiInfoDic.ContainsKey(samplingPointId))
                {
                    distance = samplingPointDetaiInfoDic[samplingPointId].CaluculateEuclidDistance(apRssiPairList, type, debugEnable);
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "SamplingPointManager.CalculateEculidDistance()执行错误：" + ex.Message);
            }
            return distance;
        }
        public int CalculateEculidDistanceABS(List<APRssiPair> apRssiPairList, int samplingPointId,SamplingContract.TagType type,bool debugEnable)
        {
            int distance = int.MaxValue;
            try
            {
                if (this.samplingPointDetaiInfoDic.ContainsKey(samplingPointId))
                {
                    distance = samplingPointDetaiInfoDic[samplingPointId].CaluculateEuclidDistanceABS(apRssiPairList, type, debugEnable);
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "SamplingPointManager.CalculateEculidDistanceABS()执行错误：" + ex.Message);
            }
            return distance;
        }
        public int ComparisonLoc(List<APRssiPair> apRssiPairList,SamplingContract.TagType type, bool isDebug)
        {
            #region 调试输出
            if (isDebug == true)
            {
                string str = string.Format("{0,-12}", "APName");
                str += "   ";
                foreach (string apmac in APManager.Instance.APSetsDic[this.environmentId].APDic.Keys)
                {
                    str += string.Format("{0,-5}",APManager.Instance.APSetsDic[this.environmentId].APDic[apmac].name);
                    str += "  ";
                }
                debuglog.WriteLine(str);
                str = string.Format("{0,-12}", "Rssi");
                str += "   ";
                foreach (string apmac in APManager.Instance.APSetsDic[this.environmentId].APDic.Keys)
                {
                    bool isScaned = false;
                    foreach (APRssiPair p in apRssiPairList)
                    {
                        if (p.APMac == apmac)
                        {
                            isScaned = true;
                            str += string.Format("{0,-5}", p.Rssi.ToString());
                            str += "  ";
                            break;
                        }
                    }
                    if (isScaned == false)
                    {
                        str += string.Format("{0,-5}", "     ");
                        str += "  ";
                    }
                }
                debuglog.WriteLine(str);
                debuglog.WriteLine("         _______________________________________________________________________");
            }
            #endregion
            int minDistance = int.MaxValue;
            int minDistanceVertexId = -1;
            try
            {
                foreach (int vertexId in samplingPointDetaiInfoDic.Keys)
                {
                    if (samplingPointDetaiInfoDic.ContainsKey(vertexId))
                    {

                        int distance = samplingPointDetaiInfoDic[vertexId].CaluculateEuclidDistance(apRssiPairList, type, isDebug);

                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            minDistanceVertexId = vertexId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "SamplingPointManager.ComparisonLoc()执行错误：" + ex.Message);
            }
            return minDistanceVertexId;
        }

    }
    public class SamplingPointDetailInfo
    {
        #region 属性
        public int vertexId;
        public int samplingPointId;
        public string name;
        public bool isSampled;
        public Dictionary<SamplingContract.TagType, SamplingResultContract> samplingResultDic;
        public Dictionary<string, int> aPWeightDic;
        private int eculidDistanceThreshold;
        private int environemtnId;
        #endregion
        public SamplingPointDetailInfo(SamplingPoint p)
        {
            this.vertexId = p.VertexId;
            this.samplingPointId = p.ID;
            this.name = p.Name;
            this.samplingResultDic = new Dictionary<SamplingContract.TagType, SamplingResultContract>();
            this.aPWeightDic = new Dictionary<string, int>();
            this.isSampled = false;
            this.environemtnId = p.EnvironmentId;
            eculidDistanceThreshold = int.MaxValue;
            if (p.IsSampled == 1)
            {
                this.isSampled = true;
                List<SamplingResultContract> samplingResultList = new List<SamplingResultContract>();
                SamplingDao samplingDao = new SamplingDao();
                MySqlDAO dao = new MySqlDAO();
                try
                {
                    samplingResultList = samplingDao.GetSamplingResultContract(p.ID, dao);
                    foreach (SamplingResultContract result in samplingResultList)
                    {
                        if (!samplingResultDic.ContainsKey(result.bindingTagType))
                        {
                            samplingResultDic.Add(result.bindingTagType, result);
                        }
                        else
                        {
                            samplingResultDic[result.bindingTagType] = result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    dao.Close();
                }
            }
            else
            {
                this.isSampled = false;
            }
        }
        //赋予权值，并删除不可比较的AP(RSSI小于一定阈值的AP视为对于该采样点的不可比较AP，直接删除)
        public void AdjustByMode()
        {
            //List<HistoricalStatContract> historicalStatList = historicalStatDic.Values.ToList();
            //Dictionary<string, HistoricalStatContract> newHistoricalStatDic = new Dictionary<string, HistoricalStatContract>();
            //aPWeightDic.Clear();
            //historicalStatList.Sort(SortA);
            //for (int i = 0; i < historicalStatList.Count && historicalStatList[i].Mode > Parameters.COMPARABLE_AP_RSSI_THRESHOLD; i++)
            //{
            //    newHistoricalStatDic.Add(historicalStatList[i].APMac, historicalStatList[i]);
            //}
            //eculidDistanceThreshold = 0;
            //for (int i = 0; i < newHistoricalStatDic.Count; i++)
            //{
            //    aPWeightDic.Add(historicalStatList[i].APMac, newHistoricalStatDic.Count - i);
            //    eculidDistanceThreshold += (newHistoricalStatDic.Count - i) * (int)System.Math.Pow(Parameters.RSSI_JITTER_RANGE, 2);
            //}
            //historicalStatDic = newHistoricalStatDic;

        }
        public static int SortA(SamplingResult r1, SamplingResult r2)
        {
            return r1.Mode.CompareTo(r2.Mode);
        }
        public int CaluculateEuclidDistance(List<APRssiPair> aPRssiPairList,SamplingContract.TagType type,bool debugEnable)
        {
            if (this.isSampled == false || !this.samplingResultDic.ContainsKey(type))
            {
                if (debugEnable == true)
                {
                    debuglog.WriteLine(string.Format("{0,-3}", "    ") + "---------------------------------------------------------------------");
                    string str = "";
                    str+=string.Format("{0,-9}",this.name);
                    str += "该采样点或设备类型未采样！";
                    debuglog.WriteLine(str);
                }
                return int.MaxValue;
            }
            int distance = 0;
            #region 距离计算1
            //foreach (string apMac in samplingResult.staticsInfoDic.Keys)
            //{
            //    distance = distance + (int)System.Math.Pow((samplingResult.staticsInfoDic[apMac].ModeRssi - GetRssiByAPMac(aPRssiPairList, apMac)), 2);
            //}
            //distance = distance > this.eculidDistanceThreshold ? int.MaxValue : distance;
            #endregion
            #region 距离计算2
            //foreach (APRssiPair p in aPRssiPairList)
            //{
            //    if (samplingResult.staticsInfoDic.ContainsKey(p.APMac))
            //    {
            //        distance = distance + (int)System.Math.Pow((samplingResult.staticsInfoDic[p.APMac].ModeRssi - p.Rssi), 2);
            //    }
            //    else
            //    {
            //        distance = distance + (int)System.Math.Pow((Parameters.COMPARABLELESS_SIGNAL - p.Rssi),2);
            //    }
            //}
            #endregion
            #region 距离计算3
            //int minRssi = int.MaxValue;
            //foreach (APRssiPair p in aPRssiPairList)
            //{
            //    if (samplingResult.staticsInfoDic.ContainsKey(p.APMac))
            //    {
            //        if (samplingResult.staticsInfoDic[p.APMac].ModeRssi < minRssi)
            //        {
            //            minRssi = samplingResult.staticsInfoDic[p.APMac].ModeRssi;
            //        }
            //    }
            //}
            //int comparableCount = 0;
            //foreach (string apMac in samplingResult.staticsInfoDic.Keys)
            //{
            //    if (samplingResult.staticsInfoDic[apMac].ModeRssi > minRssi)
            //    {
            //        distance = distance + (int)System.Math.Pow((samplingResult.staticsInfoDic[apMac].ModeRssi - GetRssiByAPMac(aPRssiPairList, apMac)), 2);
            //    }
            //}
            #endregion
            #region 距离计算4
            //foreach (APRssiPair p in aPRssiPairList)
            //{
            //    if (!samplingResult.staticsInfoDic.ContainsKey(p.APMac))
            //    {
            //        return int.MaxValue;
            //    }
            //    else
            //    {
            //        int sampleRssi = samplingResult.staticsInfoDic[p.APMac].ModeRssi;
            //        if (p.Rssi - sampleRssi >= 15 || sampleRssi - p.Rssi >= 20)
            //        {
            //            return int.MaxValue;
            //        }
            //        else
            //        {
            //            distance = distance + (int)System.Math.Pow((sampleRssi - p.Rssi), 2);
            //        }
            //    }
            //}
            #endregion
            #region 距离计算5
            int comparableNum = 0;
            foreach (APRssiPair p in aPRssiPairList)
            {
                if (samplingResultDic[type].staticsInfoDic.ContainsKey(p.APMac))
                {
                    int sampleRsssi = samplingResultDic[type].staticsInfoDic[p.APMac].ModeRssi;
                    if ((p.Rssi - sampleRsssi) > Parameters.ECULID_DISTANCE_MAX_INCREASEMENT || (sampleRsssi - p.Rssi) > Parameters.ECULID_DISTANCE_MAX_DECREASEMENT)         //实时信号强度相比采样信号强度增大超过10或者减小超过15
                    {
                        distance = int.MaxValue;
                        break;
                    }
                    else
                    {
                        distance = distance + System.Math.Abs((sampleRsssi - p.Rssi) * (sampleRsssi - p.Rssi));
                        comparableNum++;
                    }
                }
                else
                {
                    distance = distance + System.Math.Abs((Parameters.COMPARABLELESS_SIGNAL - p.Rssi) * (Parameters.COMPARABLELESS_SIGNAL - p.Rssi));
                }
            }
            //foreach (string apMac in samplingResultDic[type].staticsInfoDic.Keys)
            //{
            //    bool isContains = false;
            //    foreach (APRssiPair p in aPRssiPairList)
            //    {
            //        if (p.APMac == apMac)
            //        {
            //            isContains = true;
            //            break;
            //        }
            //    }
            //    if (isContains == false)
            //    {
            //        distance = distance + System.Math.Abs((Parameters.COMPARABLELESS_SIGNAL - samplingResultDic[type].staticsInfoDic[apMac].ModeRssi) * (Parameters.COMPARABLELESS_SIGNAL - samplingResultDic[type].staticsInfoDic[apMac].ModeRssi));
            //    }
            //}
            if (comparableNum <= samplingResultDic[type].staticsInfoDic.Count / 3)
            {
                distance = int.MaxValue;
            }
            #region 调试输出
            if (debugEnable == true)
            {
                debuglog.WriteLine(string.Format("{0,-3}", "    ") + "---------------------------------------------------------------------");
                string str = "";
                str += string.Format("{0,-9}", this.name);
                Dictionary<string, int> apMacs = new Dictionary<string, int>();
                foreach (APRssiPair p in aPRssiPairList)
                {
                    if (!apMacs.ContainsKey(p.APMac))
                    {
                        apMacs.Add(p.APMac, 1);
                    }
                }
                foreach (string apMac in samplingResultDic[type].staticsInfoDic.Keys)
                {
                    if (!apMacs.ContainsKey(apMac))
                    {
                        apMacs.Add(apMac, 1);
                    }
                }
                foreach (string apMac in apMacs.Keys)
                {
                    str += string.Format("{0,-8}", APManager.Instance.APSetsDic[environemtnId].APDic[apMac].name) + string.Format("{0,-3}", "   ") ;   
                }
                debuglog.WriteLine(str);

                str = "";
                str += string.Format("{0,-9}", "Realtime");
                foreach (string apMac in apMacs.Keys)
                {
                    bool flag = false;
                    foreach (APRssiPair p in aPRssiPairList)
                    {
                        if (p.APMac == apMac)
                        {
                            flag = true;
                            str += string.Format("{0,-8}", p.Rssi) + string.Format("{0,-3}", "   ");
                        }
                    }
                    if (flag == false)
                    {
                        str += string.Format("{0,-8}", "   ") + string.Format("{0,-3}", "   ");
                    }
                }
                debuglog.WriteLine(str);

                str = "";
                str += string.Format("{0,-9}", "Sampling");
                foreach (string apMac in apMacs.Keys)
                {
                    if (this.samplingResultDic[type].staticsInfoDic.ContainsKey(apMac))
                    {
                        str += string.Format("{0,-8}", this.samplingResultDic[type].staticsInfoDic[apMac].ModeRssi) + string.Format("{0,-3}", "   ");
                    }
                    else
                    {
                        str += string.Format("{0,-8}", "   ") + string.Format("{0,-3}", "   ");
                    }
                }
                str += "  distance:" + distance.ToString();
                debuglog.WriteLine(str);
            }
            #endregion
            #endregion
            return distance;
        }
        public int CaluculateEuclidDistanceABS(List<APRssiPair> aPRssiPairList, SamplingContract.TagType type, bool debugEnable)
        {
            if (this.isSampled == false || !this.samplingResultDic.ContainsKey(type))
            {
                if (debugEnable == true)
                {
                    debuglog.WriteLine(string.Format("{0,-3}", "    ") + "---------------------------------------------------------------------");
                    string str = "";
                    str+=string.Format("{0,-9}",this.name);
                    str += "该采样点或设备类型未采样！";
                    debuglog.WriteLine(str);
                }
                return int.MaxValue;
            }
            int distance = 0;
            #region 距离计算1
            //foreach (string apMac in samplingResult.staticsInfoDic.Keys)
            //{
            //    distance = distance + (int)System.Math.Pow((samplingResult.staticsInfoDic[apMac].ModeRssi - GetRssiByAPMac(aPRssiPairList, apMac)), 2);
            //}
            //distance = distance > this.eculidDistanceThreshold ? int.MaxValue : distance;
            #endregion
            #region 距离计算2
            //foreach (APRssiPair p in aPRssiPairList)
            //{
            //    if (samplingResult.staticsInfoDic.ContainsKey(p.APMac))
            //    {
            //        distance = distance + (int)System.Math.Pow((samplingResult.staticsInfoDic[p.APMac].ModeRssi - p.Rssi), 2);
            //    }
            //    else
            //    {
            //        distance = distance + (int)System.Math.Pow((Parameters.COMPARABLELESS_SIGNAL - p.Rssi),2);
            //    }
            //}
            #endregion
            #region 距离计算3
            //int minRssi = int.MaxValue;
            //foreach (APRssiPair p in aPRssiPairList)
            //{
            //    if (samplingResult.staticsInfoDic.ContainsKey(p.APMac))
            //    {
            //        if (samplingResult.staticsInfoDic[p.APMac].ModeRssi < minRssi)
            //        {
            //            minRssi = samplingResult.staticsInfoDic[p.APMac].ModeRssi;
            //        }
            //    }
            //}
            //int comparableCount = 0;
            //foreach (string apMac in samplingResult.staticsInfoDic.Keys)
            //{
            //    if (samplingResult.staticsInfoDic[apMac].ModeRssi > minRssi)
            //    {
            //        distance = distance + (int)System.Math.Pow((samplingResult.staticsInfoDic[apMac].ModeRssi - GetRssiByAPMac(aPRssiPairList, apMac)), 2);
            //    }
            //}
            #endregion
            #region 距离计算4
            //foreach (APRssiPair p in aPRssiPairList)
            //{
            //    if (!samplingResult.staticsInfoDic.ContainsKey(p.APMac))
            //    {
            //        return int.MaxValue;
            //    }
            //    else
            //    {
            //        int sampleRssi = samplingResult.staticsInfoDic[p.APMac].ModeRssi;
            //        if (p.Rssi - sampleRssi >= 15 || sampleRssi - p.Rssi >= 20)
            //        {
            //            return int.MaxValue;
            //        }
            //        else
            //        {
            //            distance = distance + (int)System.Math.Pow((sampleRssi - p.Rssi), 2);
            //        }
            //    }
            //}
            #endregion
            #region 距离计算5
            int comparableNum = 0;
            foreach (APRssiPair p in aPRssiPairList)
            {
                if (samplingResultDic[type].staticsInfoDic.ContainsKey(p.APMac))
                {
                    int sampleRsssi = samplingResultDic[type].staticsInfoDic[p.APMac].ModeRssi;
                    if (System.Math.Abs(sampleRsssi - p.Rssi) > 12)
                    {
                        distance = int.MaxValue;
                        break;
                    }
                    else
                    {
                        distance = distance + System.Math.Abs((sampleRsssi - p.Rssi) * (sampleRsssi - p.Rssi));
                        comparableNum++;
                    }
                }
                else
                {
                    distance = distance + System.Math.Abs((Parameters.COMPARABLELESS_SIGNAL - p.Rssi) * (Parameters.COMPARABLELESS_SIGNAL - p.Rssi));
                }
            }
            //foreach (string apMac in samplingResultDic[type].staticsInfoDic.Keys)
            //{
            //    bool isContains = false;
            //    foreach (APRssiPair p in aPRssiPairList)
            //    {
            //        if (p.APMac == apMac)
            //        {
            //            isContains = true;
            //            break;
            //        }
            //    }
            //    if (isContains == false)
            //    {
            //        distance = distance + System.Math.Abs((Parameters.COMPARABLELESS_SIGNAL - samplingResultDic[type].staticsInfoDic[apMac].ModeRssi) * (Parameters.COMPARABLELESS_SIGNAL - samplingResultDic[type].staticsInfoDic[apMac].ModeRssi));
            //    }
            //}
            if (comparableNum < samplingResultDic[type].staticsInfoDic.Count/3)
            {
                distance = int.MaxValue;
            }
            #region 调试输出
            if (debugEnable == true)
            {
                debuglog.WriteLine(string.Format("{0,-3}", "    ") + "---------------------------------------------------------------------");
                string str = "";
                str += string.Format("{0,-9}", this.name);
                Dictionary<string, int> apMacs = new Dictionary<string, int>();
                foreach (APRssiPair p in aPRssiPairList)
                {
                    if (!apMacs.ContainsKey(p.APMac))
                    {
                        apMacs.Add(p.APMac, 1);
                    }
                }
                foreach (string apMac in samplingResultDic[type].staticsInfoDic.Keys)
                {
                    if (!apMacs.ContainsKey(apMac))
                    {
                        apMacs.Add(apMac, 1);
                    }
                }
                foreach (string apMac in apMacs.Keys)
                {
                    str += string.Format("{0,-8}", APManager.Instance.APSetsDic[environemtnId].APDic[apMac].name) + string.Format("{0,-3}", "   ") ;   
                }
                debuglog.WriteLine(str);

                str = "";
                str += string.Format("{0,-9}", "Realtime");
                foreach (string apMac in apMacs.Keys)
                {
                    bool flag = false;
                    foreach (APRssiPair p in aPRssiPairList)
                    {
                        if (p.APMac == apMac)
                        {
                            flag = true;
                            str += string.Format("{0,-8}", p.Rssi) + string.Format("{0,-3}", "   ");
                        }
                    }
                    if (flag == false)
                    {
                        str += string.Format("{0,-8}", "   ") + string.Format("{0,-3}", "   ");
                    }
                }
                debuglog.WriteLine(str);

                str = "";
                str += string.Format("{0,-9}", "Sampling");
                foreach (string apMac in apMacs.Keys)
                {
                    if (this.samplingResultDic[type].staticsInfoDic.ContainsKey(apMac))
                    {
                        str += string.Format("{0,-8}", this.samplingResultDic[type].staticsInfoDic[apMac].ModeRssi) + string.Format("{0,-3}", "   ");
                    }
                    else
                    {
                        str += string.Format("{0,-8}", "   ") + string.Format("{0,-3}", "   ");
                    }
                }
                str += "  ABSdistance:" + distance.ToString();
                debuglog.WriteLine(str);
            }
            #endregion
            #endregion
            return distance;
        }
        private int GetRssiByAPMac(List<APRssiPair> aPRssiPairList, string aPMac)
        {
            int Rssi = Parameters.COMPARABLELESS_SIGNAL;
            foreach (APRssiPair apRssiPair in aPRssiPairList)
            {
                if (apRssiPair.APMac == aPMac)
                {
                    Rssi = apRssiPair.Rssi;
                }
            }
            return Rssi;
        }
    }
}
