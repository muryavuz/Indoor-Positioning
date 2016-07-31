/*  
 * 文件名：         STALocAlgV1.cs
 * 文件功能描述：   STA定位算法V1
 * 创建标识：       孙泽浩  
 * 状态：           
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LocContract;
using System.Collections;
using LogManager;
using PacketInterpreter;
using System.Threading;
using InnerClasses;
namespace LocAlgorithm
{
    public class STALocAlgV1
    {
        private int environmentId;
        private List<APRssiPair> lastLocAPRssiPairList;
        private List<int> adjSamplingPointsIdList;
        private PointContract lastLocPos;
        private HistoryAPRssiRecord historyAPRssiRecord;
        private TagType type;
        public STALocAlgV1(int e)
        {
            this.environmentId = e;
            this.adjSamplingPointsIdList = new List<int>();
            this.historyAPRssiRecord = new HistoryAPRssiRecord();
            this.type = TagType.STA;
            this.lastLocPos = SingleLocResult.OfflinePos.Clone();
        }

        public SingleLocResult Loc(List<APRssiPair> apRssiPairList, DateTime currentTime, bool debugEnable, out bool PosNotChangeFlag)
        {
            SingleLocResult locResult = new SingleLocResult();
            locResult.time = currentTime;
            List<APRssiPair> avalibleAPRssiPairList = new List<APRssiPair>();
            PosNotChangeFlag = false;
            #region 调试输出
            if (debugEnable == true)
            {
                debuglog.EnterWriteLock();
                debuglog.WriteLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                string str = "";
                str += "STALocAlgV1.Loc(";
                foreach (APRssiPair p in apRssiPairList)
                {
                    str += p.Rssi.ToString() + "(" + p.APMac + "),";
                }
                debuglog.WriteLine(str);
            }
            #endregion
            try
            {
                #region 过滤其他环境的AP
                avalibleAPRssiPairList = new List<APRssiPair>();
                foreach (APRssiPair aprssiPair in apRssiPairList)
                {
                    if (APManager.Instance.IsAPAvalible(environmentId, aprssiPair.APMac))
                    {
                        avalibleAPRssiPairList.Add(aprssiPair);
                    }
                }
                #region 调试输出
                if (debugEnable == true)
                {
                    string str = "";
                    str += "After Environment Filter:";
                    foreach (APRssiPair p in avalibleAPRssiPairList)
                    {
                        str += p.Rssi + "(" + APManager.Instance.APSetsDic[environmentId].APDic[p.APMac].name + "),";
                    }
                    debuglog.WriteLine(str);
                }
                #endregion
                #endregion

                #region 信号强度平滑
                this.historyAPRssiRecord.Refresh(avalibleAPRssiPairList);
                avalibleAPRssiPairList = this.historyAPRssiRecord.GetCurrentScanRssiPairList(currentTime);
                #region 调试输出
                if (debugEnable == true)
                {
                    string str = "";
                    str += "After Smoother:";
                    foreach (APRssiPair p in avalibleAPRssiPairList)
                    {
                        str += p.Rssi + "(" + APManager.Instance.APSetsDic[environmentId].APDic[p.APMac].name + "),";
                    }
                    debuglog.WriteLine(str);
                }
                #endregion
                #endregion

                #region 定位条件检查
                bool IsLocAvalibe = false;
                if (avalibleAPRssiPairList.Count >= 2)
                {
                    foreach (APRssiPair p in avalibleAPRssiPairList)
                    {
                        if (p.Rssi > -75)
                        {
                            IsLocAvalibe = true;
                        }
                    }
                }
                if (IsLocAvalibe == false)
                {
                    //不满足定位条件，直接返回
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("Failed To Meet The Loc Condition!");
                        debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        debuglog.ExitWriteLock();
                    }
                    #endregion
                    return null;
                }
                else
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("Success To Meet The Loc Condition!");
                    }
                    #endregion
                }
                #endregion

                #region 获取信号强度最大值
                int maxRssi = int.MinValue;
                string maxRssiAPMac = null;
                avalibleAPRssiPairList.Sort(new APRssiPairComparer());
                maxRssi = avalibleAPRssiPairList.First().Rssi;
                maxRssiAPMac = avalibleAPRssiPairList.First().APMac;
                int Dvalue;
                if (avalibleAPRssiPairList.Count < 2)
                {
                    Dvalue = int.MaxValue;
                }
                else
                {
                    Dvalue = avalibleAPRssiPairList[0].Rssi - avalibleAPRssiPairList[1].Rssi;
                }
                #region 调试输出
                if (debugEnable == true)
                {
                    debuglog.WriteLine("Max Rssi:" + maxRssi.ToString() + "(" + maxRssiAPMac + ")" + "and Dvalue = " + Dvalue.ToString());
                }
                #endregion
                #endregion

                if (maxRssi > InnerClasses.Parameters.APQUICKWIN_RSSI_THRESHOLD_STA && Dvalue > InnerClasses.Parameters.CHANGE_POSITION_DVALUE)
                {
                    //AP快速胜出
                    locResult.positionList = new List<PointContract>();
                    if (!lastLocPos.Equals(APManager.Instance.APSetsDic[environmentId].APDic[maxRssiAPMac].pos))
                    {
                        locResult.positionList.Add(APManager.Instance.APSetsDic[environmentId].APDic[maxRssiAPMac].pos.Clone());
                        lastLocPos = APManager.Instance.APSetsDic[environmentId].APDic[maxRssiAPMac].pos.Clone();
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("Loc Result:" + APManager.Instance.APSetsDic[environmentId].APDic[maxRssiAPMac].name);
                            debuglog.WriteLine("Loc Reson: AP Quick Wins!!!");
                            debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        return locResult;
                    }
                    else
                    {
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("Loc Result:NULL");
                            debuglog.WriteLine("Loc Reson: Pos Not Changes!!!");
                            debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        PosNotChangeFlag = true;
                        return null;
                    }
                }
                else
                {
                    #region 选取近邻AP和近邻采样点集合
                    List<string> adjAPMacList = SamplingPointManager.GetAdjAPMacList(avalibleAPRssiPairList,this.environmentId);
                    Dictionary<int, int> adjSamplingPointIdDistanceDic = new Dictionary<int, int>();
                    foreach (string apMac in adjAPMacList)
                    {
                        if (!SamplingPointManager.Instance.SamplingPointSetsDic[environmentId].apAdjSamplingPointIdDic.ContainsKey(apMac))
                        {
                            continue;
                        }
                        foreach (int samplingPointId in SamplingPointManager.Instance.SamplingPointSetsDic[environmentId].apAdjSamplingPointIdDic[apMac])
                        {
                            if (!adjSamplingPointIdDistanceDic.ContainsKey(samplingPointId))
                            {
                                adjSamplingPointIdDistanceDic.Add(samplingPointId, int.MaxValue);
                            }
                        }
                    }
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str += "The Adj APs:";
                        foreach (string apMac in adjAPMacList)
                        {
                            str += APManager.Instance.APSetsDic[environmentId].APDic[apMac].name + ",";
                        }
                        debuglog.WriteLine(str);
                        str = "";
                        str += "The Adj Sampling Points:";
                        foreach (int samplingPointId in adjSamplingPointIdDistanceDic.Keys)
                        {
                            str += SamplingPointManager.Instance.SamplingPointSetsDic[environmentId].samplingPointDetaiInfoDic[samplingPointId].name + ",";
                        }
                        debuglog.WriteLine(str);
                    }
                    #endregion
                    #endregion

                    #region 检查是否切换地图,如果是启用绝对对比
                    bool IsChangeMap = false;
                    if (adjAPMacList != null && adjAPMacList.Count > 0 && lastLocPos.MapId != APManager.Instance.APSetsDic[environmentId].APDic[adjAPMacList.First()].pos.MapId)
                    {
                        IsChangeMap = true;
                    }
                    #endregion
                    #region 对比计算与采样点的差异
                    List<CompareResult> compareResultList = new List<CompareResult>();
                    foreach (int samplingPointId in adjSamplingPointIdDistanceDic.Keys)
                    {
                        int distance = int.MaxValue;
                        if (IsChangeMap == false)
                        {
                            distance = SamplingPointManager.Instance.SamplingPointSetsDic[environmentId].CalculateEculidDistance(avalibleAPRssiPairList, samplingPointId, InnerClasses.Common.ConvertHelp.LocTagType2SamplingTagType(type), debugEnable);
                        }
                        else
                        {
                            distance = SamplingPointManager.Instance.SamplingPointSetsDic[environmentId].CalculateEculidDistanceABS(avalibleAPRssiPairList, samplingPointId, InnerClasses.Common.ConvertHelp.LocTagType2SamplingTagType(type), debugEnable);
                        }
                        if (distance < int.MaxValue && distance > 0)
                        {
                            #region distance值很小时，避免权值平均时出现除零
                            if (distance < 10)
                            {
                                distance = 10;
                            }
                            #endregion
                            compareResultList.Add(new CompareResult
                            {
                                samplingPointId = samplingPointId,
                                eculidDistance = distance,
                            });
                        }
                    }
                    #region TEST将上轮定位的信号强度向量作为上个位置的采样结果参与对比计算，其SamplingPointId设为-1
                    if (lastLocAPRssiPairList != null && lastLocAPRssiPairList.Count > 0)
                    {
                        int distance = CalculateEculidDistance(avalibleAPRssiPairList, lastLocAPRssiPairList, debugEnable);
                        if (distance < int.MaxValue && distance > 0 && !this.lastLocPos.Equals(SingleLocResult.UnknownPos) && !this.lastLocPos.Equals(SingleLocResult.OfflinePos))
                        {
                            #region distance值很小时，避免权值平均时出现除零
                            if (distance < 10)
                            {
                                distance = 10;
                            }
                            #endregion
                            compareResultList.Add(new CompareResult
                            {
                                samplingPointId = -1,
                                eculidDistance = distance,
                            });
                        }
                    }
                    #endregion
                    #endregion

                    #region 位置估计V1
                    //PointContract locPos;
                    //compareResultList.Sort();
                    //int locVertexId = compareResultList.Last().samplingPointId;
                    //if (VertexManager.Instance.VertexSetsDic[environmentId].VertexDic.ContainsKey(locVertexId))
                    //{
                    //    locPos = (VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[locVertexId].pos);
                    //}
                    //else
                    //{
                    //    Exception ex = new Exception("位置估计出错，环境" + environmentId.ToString() + "不包含点" + locVertexId.ToString());
                    //    throw ex;
                    //}
                    #endregion

                    #region 位置估计V2
                    PointContract locPos = new PointContract();
                    List<CompareResult> smallerSomeList = GetSmallerSome(compareResultList);
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str += "SmallestSomeSamplingPoints:";
                        foreach (CompareResult r in smallerSomeList)
                        {
                            if (r.samplingPointId == -1)
                            {
                                str += "Last" + ",";
                            }
                            else
                            {
                                str += SamplingPointManager.Instance.SamplingPointSetsDic[environmentId].samplingPointDetaiInfoDic[r.samplingPointId].name + ",";
                            }
                        }
                        debuglog.WriteLine(str);
                    }
                    #endregion
                    if (smallerSomeList.Count == 0)
                    {
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("CAN'T LOC!!!");
                            debuglog.WriteLine("Loc Reson: SMALLESTSOME COUNT EQUELS 0!!!");
                            debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        return null;
                    }
                    int mapId = -1;
                    foreach (CompareResult r in smallerSomeList)
                    {
                        #region test将上轮定位的信号强度向量作为上个位置的采样结果参与对比计算，其samplingpointid设为-1
                        if (r.samplingPointId == -1)
                        {
                            mapId = lastLocPos.MapId;
                            continue;
                        }
                        #endregion
                        if (mapId != -1 && VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[r.samplingPointId].pos.MapId != mapId)
                        {
                            #region 调试输出
                            if (debugEnable == true)
                            {
                                debuglog.WriteLine("CAN'T LOC!!!!");
                                debuglog.WriteLine("Loc Reson: MapID Ununique!!!");
                                debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                                debuglog.ExitWriteLock();
                            }
                            #endregion
                            return null;
                        }
                        else
                        {
                            mapId = VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[r.samplingPointId].pos.MapId;
                        }
                    }
                    locPos.MapId = mapId;
                    locPos.Type = CoordinateType.SPACE_RCTANGULAR;
                    int sum = 0;
                    foreach (CompareResult r in smallerSomeList)
                    {
                        sum += r.eculidDistance;
                    }
                    int xSum = 0;
                    int ySum = 0;
                    int weightSum = 0;
                    foreach (CompareResult r in smallerSomeList)
                    {
                        if (r.samplingPointId != -1)
                        {
                        r.weight = sum / (r.eculidDistance / 10);
                        weightSum += r.weight;
                        xSum += r.weight * VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[r.samplingPointId].pos.X;
                        ySum += r.weight * VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[r.samplingPointId].pos.Y;
                        }
                        else
                        {
                            r.weight = sum / (r.eculidDistance / 10);
                            weightSum += r.weight;
                            xSum += r.weight * lastLocPos.X;
                            ySum += r.weight * lastLocPos.Y;
                        }
                    }
                    locPos.X = xSum / weightSum;
                    locPos.Y = ySum / weightSum;
                    #endregion
                    if (!lastLocPos.Equals(locPos))
                    {
                        #region 位置修正
                        locResult.positionList.Add(locPos);
                        #endregion
                        this.lastLocAPRssiPairList = avalibleAPRssiPairList;
                        this.lastLocPos = locPos.Clone();
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("Loc Result:" + "(" + locPos.X.ToString() + "," + locPos.Y.ToString() + ")@" + locPos.MapId.ToString());
                            debuglog.WriteLine("Loc Reson: Min Eculid Distance!!!");
                            debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        return locResult;
                    }
                    else
                    {
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("Loc Result:NULL");
                            debuglog.WriteLine("Loc Reson: Pos Not Changes!!!");
                            debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        PosNotChangeFlag = true;
                        return null;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "STALocAlgV1.Loc出错！！！" + ex.Message);
                return null;
            }
            finally
            {
                if (debugEnable == true)
                {
                    debuglog.ExitWriteLock();
                }
            }
        }

        private int CalculateEculidDistance(List<APRssiPair> originalAPRssiPairList, List<APRssiPair> dstAPRssiPairList, bool debugEnable)
        {
            int distance = 0;
            if (originalAPRssiPairList == null || dstAPRssiPairList == null)
            {
                if (debugEnable == true)
                {
                    debuglog.WriteLine(string.Format("{0,-3}", "    ") + "---------------------------------------------------------------------");
                    string str = "";
                    str += string.Format("{0,-9}", "Last");
                    str += "该采样点或设备类型未采样！";
                    debuglog.WriteLine(str);
                }
                return int.MaxValue;
            }
            try
            {
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
                foreach (APRssiPair p in originalAPRssiPairList)
                {
                    bool isContains = false;
                    foreach (APRssiPair q in dstAPRssiPairList)
                    {
                        if (p.APMac == q.APMac)
                        {
                            isContains = true;
                            distance = distance + System.Math.Abs((q.Rssi - p.Rssi) * (q.Rssi - p.Rssi));
                            comparableNum++;
                            break;
                        }
                    }
                    if (isContains == false)
                    {
                        distance = distance + System.Math.Abs((Parameters.COMPARABLELESS_SIGNAL - p.Rssi) * (Parameters.COMPARABLELESS_SIGNAL - p.Rssi));
                    }
                }
                //foreach (APRssiPair q in dstAPRssiPairList)
                //{
                //    bool isContains = false;
                //    foreach (APRssiPair p in originalAPRssiPairList)
                //    {
                //        if (q.APMac == p.APMac)
                //        {
                //            isContains = true;
                //            break;
                //        }
                //    }
                //    if (isContains == false)
                //    {
                //        distance = distance + System.Math.Abs((Parameters.COMPARABLELESS_SIGNAL - q.Rssi) * Parameters.COMPARABLELESS_SIGNAL);
                //    }
                //}
                //if (comparableNum < samplingResultDic[type].staticsInfoDic.Count / 2)
                //{
                //    return int.MaxValue;
                //}
                #region 调试输出
                if (debugEnable == true)
                {
                    debuglog.WriteLine(string.Format("{0,-3}", "    ") + "---------------------------------------------------------------------");
                    string str = "";
                    str += string.Format("{0,-9}", "Last");
                    Dictionary<string, int> apMacs = new Dictionary<string, int>();
                    foreach (APRssiPair p in originalAPRssiPairList)
                    {
                        if (!apMacs.ContainsKey(p.APMac))
                        {
                            apMacs.Add(p.APMac, 1);
                        }
                    }
                    foreach (APRssiPair q in dstAPRssiPairList)
                    {
                        if (!apMacs.ContainsKey(q.APMac))
                        {
                            apMacs.Add(q.APMac, 1);
                        }
                    }
                    foreach (string apMac in apMacs.Keys)
                    {
                        str += string.Format("{0,-8}", APManager.Instance.APSetsDic[this.environmentId].APDic[apMac].name) + string.Format("{0,-3}", "   ");
                    }
                    debuglog.WriteLine(str);

                    str = "";
                    str += string.Format("{0,-9}", "Realtime");
                    foreach (string apMac in apMacs.Keys)
                    {
                        bool flag = false;
                        foreach (APRssiPair p in originalAPRssiPairList)
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
                    str += string.Format("{0,-9}", "Last");
                    foreach (string apMac in apMacs.Keys)
                    {
                        bool isContains = false;
                        foreach (APRssiPair q in dstAPRssiPairList)
                        {
                            if (q.APMac == apMac)
                            {
                                isContains = true;
                                str += string.Format("{0,-8}", q.Rssi) + string.Format("{0,-3}", "   ");
                                break;
                            }
                        }
                        if (isContains == false)
                        {
                            str += string.Format("{0,-8}", "   ") + string.Format("{0,-3}", "   ");
                        }
                    }
                    str += "  distance:" + distance.ToString();
                    debuglog.WriteLine(str);
                }
                #endregion
                #endregion
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "STALocAlgV1.CalculateEculidDistance()执行错误：" + ex.Message);
            }
            return distance;
        }
        public List<CompareResult> GetSmallerSome(List<CompareResult> resultList)
        {
            List<CompareResult> smallerSomeList = new List<CompareResult>();
            try
            {
                if (resultList != null && resultList.Count > 1)
                {
                    resultList.Sort();
                    int medianIndex = (int)System.Math.Ceiling(((double)resultList.Count) / 2.0);       //向上取整
                    int minDistance = resultList.First().eculidDistance;
                    int medianDistance = resultList.ElementAt(medianIndex).eculidDistance;
                    smallerSomeList = new List<CompareResult>();
                    foreach (CompareResult r in resultList)
                    {
                        if (r.eculidDistance < ((minDistance + medianDistance) / 2))
                        {
                            smallerSomeList.Add(r);
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (smallerSomeList.Count > 3)
                    {
                        smallerSomeList = GetSmallerSome(smallerSomeList);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "STALocAlgV1.GetSmallerSome()执行错误：" + ex.Message);
            }
            return smallerSomeList;
        }
    }

    public class APRssiPairComparer : IComparer<APRssiPair>
    {
        public int Compare(APRssiPair p1, APRssiPair p2)
        {
            if (p1!=null && p2 != null && p1.Rssi > p2.Rssi)
            {
                return -1;
            }
            else if (p1 != null && p2 != null && p1.Rssi < p2.Rssi)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }

    public class CompareResult:IComparable
    {
        public int samplingPointId;
        public int eculidDistance;
        public int weight;
        public int CompareTo(object obj)
        {
            CompareResult anotherResult = obj as CompareResult;
            if (eculidDistance > anotherResult.eculidDistance)
            {
                //此实例按排序排在obj的前面
                return 1;
            }
            else
            {
                //此实例按排序排在obj的后面
                return -1;
            }
        }
    }
}
