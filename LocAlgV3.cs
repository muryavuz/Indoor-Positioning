/*  
 * 文件名：         LocAlgV3.cs
 * 文件功能描述：   算法第三版，主要为定位到AP，路径计算和距离计算
 * 创建标识：       孙泽浩 20120914     
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
    public class LocAlgV3 : ILocAlgorithm
    {
        #region 属性
        private int environmentId;
        private List<int> lastClosestVertexIdList;
        private HistoryAPRssiRecord historyAPRssiRecord;
        private int lastLocAPVertexId;
        private string lastLocAPMac;
        private int lastLocVertexId;
        private LCASSmoother smoother;
        private DateTime lastSendPathTime;
        private TagType type;
        #endregion

        #region 构造器
        public LocAlgV3(int e)
        {
            this.environmentId = e;
            this.lastLocVertexId = -1;
            this.lastLocAPMac = null;
            this.lastClosestVertexIdList = new List<int>();
            this.historyAPRssiRecord = new HistoryAPRssiRecord();
            this.lastLocAPVertexId = -1;
            this.type = TagType.WIFI;
            this.smoother = new LCASSmoother(e);
        }
        #endregion 

        #region 方法
        static public void Init()
        {
            //Parameters.Load();
            CellManager.Instance.Init();
            APManager.Instance.Init();
            VertexManager.Instance.Init();
            SamplingPointManager.Instance.Init();
            Log.AddLogEntry(LogEntryLevel.INFO, "算法初始化完成");
        }

        /// <summary>
        /// 根据APRSSI经过算法处理得当标签的位置
        /// </summary>
        /// <param name="apRssiPairList"></param>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        SingleLocResult ILocAlgorithm.Loc(List<APRssiPair> apRssiPairList, DateTime currentTime,bool debugEnable)
        {
            int step = 1;
            try
            {
                SingleLocResult locResult = new SingleLocResult();
                locResult.time = currentTime;
                #region 点定位部分方法二
                step = 2;
                #region 调试输出
                if (debugEnable == true)
                {
                    debuglog.EnterWriteLock();
                    debuglog.WriteLine("┎------------------------------------");
                    debuglog.WriteLine("Time:" + currentTime.ToString());
                    string str = "";
                    str += "OriginalAPRssiPair:";
                    foreach (APRssiPair p in apRssiPairList)
                    {
                        str += p.APMac;
                        str += "(";
                        str += p.Rssi.ToString();
                        str += ")";
                        str += ",";
                    }
                    debuglog.WriteLine(str);
                }
                #endregion
                step = 3;
                #region 过滤不属于改环境的AP
                List<APRssiPair> avalibleAPRssiPairList = new List<APRssiPair>();
                foreach (APRssiPair aprssiPair in apRssiPairList)
                {
                    if (APManager.Instance.IsAPAvalible(environmentId,aprssiPair.APMac))
                    {
                        avalibleAPRssiPairList.Add(aprssiPair);
                    }
                }

                #endregion
                step = 4;
                #region 调试输出
                if (debugEnable == true)
                {
                    string str = "";
                    str += "APRssiPair of The Environment:";
                    foreach (APRssiPair p in avalibleAPRssiPairList)
                    {
                        str += APManager.Instance.GetAPName(environmentId, p.APMac);
                        str += "(";
                        str += p.Rssi.ToString();
                        str += ")";
                        str += ",";
                    }
                    debuglog.WriteLine(str);
                }
                #endregion
                step = 5;
                historyAPRssiRecord.Refresh(avalibleAPRssiPairList);                    //点定位算法方法二：去所有AP在过去十秒内信号按从小到大排序后的黄金分割位值，即更相信RSSI偏大的信号，具体多少秒内的信号会影响到系统定位的延迟
                step = 6;
                int MaxRssi = int.MinValue;
                string MaxRssiAPMac = historyAPRssiRecord.GetMaxRssiAPMacWithRssi(currentTime, out MaxRssi);
                if (MaxRssiAPMac == null)
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("There is No AP Scaned!");
                        debuglog.WriteLine("┕------------------------------------");
                        debuglog.ExitWriteLock();
                    }
                    #endregion
                    return null;
                }
                step = 7;
                int locVertexId = -1;
                int dValue = historyAPRssiRecord.GetDValueOf1thAnd2ndMaxRssi(currentTime);
                step = 8;
                
                step = 9;
                if ((MaxRssi > Parameters.CHANGE_POSITION_RSSI && dValue > Parameters.CHANGE_POSITION_DVALUE))
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("LocReson:" + MaxRssi.ToString() + "@" + APManager.Instance.APSetsDic[environmentId].APDic[MaxRssiAPMac].name + " & DValue=" + dValue.ToString());
                    }
                    #endregion
                    int smoothLocResult = smoother.Refresh(lastLocVertexId, APManager.Instance.APSetsDic[environmentId].APDic[MaxRssiAPMac].vertexId, debugEnable);
                    if (smoothLocResult == -1)
                    {
                        locVertexId = lastLocVertexId;
                    }
                    else
                    {
                        locVertexId = smoothLocResult;
                        lastLocAPVertexId = locVertexId;
                        lastLocAPMac = MaxRssiAPMac;
                    }
                }
                else
                {
                    if (SamplingPointManager.Instance.SamplingPointSetsDic.ContainsKey(environmentId))
                    {
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("LocReson:" + MaxRssi.ToString() + "@" + APManager.Instance.APSetsDic[environmentId].APDic[MaxRssiAPMac].name + " & DValue=" + dValue.ToString());
                            debuglog.WriteLine("ComparisonSamplingLoc╮(╯▽╰)╭");
                        }
                        #endregion
                        List<APRssiPair> currentScanRssiList = historyAPRssiRecord.GetCurrentScanRssiPairList(currentTime);
                        int comparisonLocResult = SamplingPointManager.Instance.SamplingPointSetsDic[this.environmentId].ComparisonLoc(currentScanRssiList, InnerClasses.Common.ConvertHelp.LocTagType2SamplingTagType(type), debugEnable);
                        if (comparisonLocResult == -1)
                        {
                            locVertexId = lastLocVertexId;
                        }
                        else
                        {
                            int smoothLocResult = smoother.Refresh(lastLocVertexId, comparisonLocResult, debugEnable);
                            if (smoothLocResult == -1)
                            {
                                locVertexId = lastLocVertexId;
                            }
                            else
                            {
                                locVertexId = smoothLocResult;
                            }
                        }
                    }
                    else
                    {
                        locVertexId = lastLocVertexId;
                    }
                    step = 11;
                }
                #endregion
                #region 路径计算部分
                if (locVertexId != -1 && lastLocVertexId != locVertexId)
                {
                    //List<int> currentClosestVertexIdList = PathManager.Instance.GetClosestVertexIdList(LocResultVertexId);
                    List<int> currentClosestVertexIdList = new List<int>();
                    currentClosestVertexIdList.Add(locVertexId);
                    step = 12;
                    List<PointContract> posList = VertexManager.Instance.VertexSetsDic[environmentId].GetPath(lastClosestVertexIdList, currentClosestVertexIdList);
                    lastClosestVertexIdList = currentClosestVertexIdList;
                    step = 13;
                    if (posList != null)
                    {
                        PointContract locPos = VertexManager.Instance.VertexSetsDic[environmentId].GetVertexPosition(locVertexId);
                        posList.Add(locPos);
                        step = 14;
                    }
                    else
                    {
                        posList = new List<PointContract>();
                        PointContract locPos = VertexManager.Instance.VertexSetsDic[environmentId].GetVertexPosition(locVertexId);
                        posList.Add(locPos);
                        step = 14;
                    }
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("LocResult:" + "(" + posList.Last().X.ToString() + "," + posList.Last().Y.ToString() + ")@" + posList.Last().MapId.ToString());
                        debuglog.WriteLine("┕------------------------------------");
                        debuglog.ExitWriteLock();
                    }
                    #endregion
                    step = 15;
                    lastLocVertexId = locVertexId;
                    locResult.positionList = posList;
                    step = 16;
                    lastSendPathTime = DateTime.Now;
                    return locResult;
                }
                else if (lastLocVertexId == locVertexId)
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("LocResult:null");
                        debuglog.WriteLine("┕------------------------------------");
                        debuglog.ExitWriteLock();
                    }
                    #endregion
                    step = 17;
                    if ((DateTime.Now - lastSendPathTime).TotalMinutes > InnerClasses.Parameters.SAMEPOS_SEND_INTERVAL)
                    {
                        List<PointContract> posList = new List<PointContract>();
                        PointContract locPos = VertexManager.Instance.VertexSetsDic[environmentId].GetVertexPosition(locVertexId);
                        posList.Add(locPos);
                        locResult.positionList = posList;
                        lastSendPathTime = DateTime.Now;
                        return locResult;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("LocResult:Unknown");
                        debuglog.WriteLine("┕------------------------------------");
                        debuglog.ExitWriteLock();
                    }
                    #endregion
                    step = 18;
                    locResult.positionList = new List<PointContract>();
                    PointContract unknownPos = SingleLocResult.UnknownPos.Clone();
                    locResult.positionList.Add(unknownPos);
                    step = 19;
                    lastSendPathTime = DateTime.Now;
                    return locResult;
                }
                #endregion

            }
            catch (Exception ex)
            {
                #region 参数转换为字符串
                //string parametersStr = "";
                //parametersStr += "apRssiPairList:";
                //if (apRssiPairList == null)
                //{
                //    parametersStr += "null";
                //}
                //else
                //{
                //    parametersStr += "{";
                //    foreach (APRssiPair p in apRssiPairList)
                //    {
                //        parametersStr += "(";
                //        parametersStr += p.APMac;
                //        parametersStr += ",";
                //        parametersStr += p.Rssi;
                //        parametersStr += ",";
                //        parametersStr += p.Time.ToString();
                //        parametersStr += ",";
                //        parametersStr += "),";
                //    }
                //    parametersStr += "}";
                //}
                //parametersStr += ",";
                //parametersStr += "currentTime:" + currentTime.ToString();
                #endregion
                Log.AddLogEntry(LogEntryLevel.ERROR, "Step is " + step + "LocAlgorithm.LocAlgV3()执行异常：" + ex.Message);
                return null;
            }
        }

        void ILocAlgorithm.Disappear()
        {
            this.lastLocVertexId = -1;
            this.lastLocAPMac = null;
            this.lastClosestVertexIdList = new List<int>();
            this.historyAPRssiRecord = new HistoryAPRssiRecord();
            this.lastLocAPVertexId = -1;
        }
        public override string ToString()
        {
            //将所有内部变量转换成字符串，在异常发生时输出内部变量的当时值
            string str = "";
            str += "environmentId:" + environmentId;
            str += ",lastClosestVertexIdList:";
            if (lastClosestVertexIdList == null)
            {
                str += "null";
            }
            else
            {
                str += "(";
                foreach (int vertexId in lastClosestVertexIdList)
                {
                    str += vertexId.ToString()+",";
                }
                str += ")";
            }
            str += ",historyAPRssiRecord:{" + historyAPRssiRecord.ToString()+"}";
            str += ",lastLocVertexId:" + lastLocVertexId.ToString();
            return str;
        }
        #endregion
    }
}
