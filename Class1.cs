/*  
 * 文件名：         STALocAlgV2.cs
 * 文件功能描述：   STA定位算法V2，利用Cell进行平滑和寻路
 * 创建标识：       孙泽浩  
 * 状态：           2013/12/10
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InnerClasses;
using LocContract;
using SamplingContract;
using LogManager;
using PacketInterpreter;
using System.Collections;
using System.Threading;

namespace LocAlgorithm
{
    public class STALocAlgV3
    {
        #region 属性
        //┌-------基本信息--------
        private int environmentId;
        private LocContract.TagType type;
        private bool enableFlag;
        private int failedMeetLocConditionTimes;
        private bool isDisappearAvalible;
        //└-----------------------

        //┌-------历史信息--------
        private HistoryAPRssiRecord historyAPRssiRecord;
        private PointContract lastLocPos;
        private DateTime lastLoctime;
        private PointContract lastlastLocPos;
        private DateTime lastlastLoctime;
        private int lastCellId;
        private List<APRssiPair> lastAvalibleAPRssiPairList;
        //└-----------------------

        //┌-------数据字典引用--------
        private APSet apSet;
        private SamplingPointSet samplingPointSet;
        private VertexSet vertexSet;
        //└---------------------------

        //┌-------比较器--------
        private APRssiPairComparer apRssiPairComparer;
        //└---------------------
        #endregion

        #region 构造器
        public STALocAlgV3(int e)
        {
            //┌-------基本信息初始化--------
            this.environmentId = e;
            this.type = LocContract.TagType.STA;
            this.enableFlag = true;
            this.failedMeetLocConditionTimes = 0;
            this.isDisappearAvalible = true;
            //└-----------------------------
            //┌-------历史信息--------
            this.historyAPRssiRecord = new HistoryAPRssiRecord();
            this.apRssiPairComparer = new APRssiPairComparer();
            this.lastLocPos = SingleLocResult.OfflinePos.Clone();
            this.lastLoctime = DateTime.Now;
            this.lastlastLoctime = DateTime.Now;
            this.lastlastLocPos = SingleLocResult.OfflinePos.Clone();
            this.lastCellId = -1;
            this.lastAvalibleAPRssiPairList = new List<APRssiPair>();
            //└-----------------------
            //┌-------数据字典引用初始化--------
            #region 数据字典引用初始化
            if (APManager.Instance.APSetsDic.ContainsKey(this.environmentId))
            {
                this.apSet = APManager.Instance.APSetsDic[this.environmentId];
            }
            else
            {
                enableFlag = false;
                Log.AddLogEntry(LogEntryLevel.ERROR, "STALocAlgV2:不存在环境Id" + this.environmentId.ToString());
            }
            if (SamplingPointManager.Instance.SamplingPointSetsDic.ContainsKey(this.environmentId))
            {
                this.samplingPointSet = SamplingPointManager.Instance.SamplingPointSetsDic[this.environmentId];
            }
            else
            {
                enableFlag = false;
                Log.AddLogEntry(LogEntryLevel.ERROR, "STALocAlgV2:不存在环境Id" + this.environmentId.ToString());
            }
            if (VertexManager.Instance.VertexSetsDic.ContainsKey(this.environmentId))
            {
                this.vertexSet = VertexManager.Instance.VertexSetsDic[this.environmentId];
            }
            else
            {
                enableFlag = false;
                Log.AddLogEntry(LogEntryLevel.ERROR, "STALocAlgV2:不存在环境Id" + this.environmentId.ToString());
            }
            #endregion
            //└---------------------------------
            //┌-------比较器初始化--------
            apRssiPairComparer = new APRssiPairComparer();
            //└---------------------------
        }
        #endregion

        #region 方法
        /// <summary>
        /// 算法执行主体，位置相比上次不变或者无法定位（消失、无效等，通过IsDisappered区分）返回null
        /// </summary>
        /// <param name="apRssiPairList"></param>
        /// <param name="currentTime"></param>
        /// <param name="debugEnable"></param>
        /// <param name="IsDisappered"></param>
        /// <returns></returns>
        public SingleLocResult Loc(List<APRssiPair> apRssiPairList, DateTime currentTime, bool debugEnable)
        {
            if (enableFlag == false)
            {
                return null;
            }

            //局部变量，存放返回值
            SingleLocResult locResult = new SingleLocResult();
            locResult.time = currentTime;
            locResult.positionList = new List<PointContract>();
            //局部变量，存放返回值 END

            //局部变量，存放中间结果
            PointContract locPos;
            int locCellId = -1;
            //局部变量，存放中间接过 END

            #region 调试输出
            if (debugEnable == true)
            {
                debuglog.EnterWriteLock();
                debuglog.WriteLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                APManager.Instance.DebugStrOfLocAlg += "┏━━━━━━━━━━━━━━━━━━━━━━━" + "\r\n";
                string str = "";
                str += DateTime.Now.ToLongTimeString() + "\r\n";
                str += "STALocAlgV3.Loc(";
                foreach (APRssiPair p in apRssiPairList)
                {
                    str += p.Rssi.ToString() + "(" + p.APMac + "),";
                }
                debuglog.WriteLine(str);
                APManager.Instance.DebugStrOfLocAlg += str + "\r\n";
            }
            #endregion
            try
            {
                List<APRssiPair> avalibleAPRssiPairList = new List<APRssiPair>();
                //┏━━━━━过滤其他环境的AP━━━━━
                #region 过滤其他环境的AP
                foreach (APRssiPair apRssiPair in apRssiPairList)
                {
                    if (APManager.Instance.IsAPAvalible(environmentId, apRssiPair.APMac)&&apRssiPair.Rssi>-90)//在这里可以对信号强度进行过滤 &&apRssiPair.Rssi<=-70
                    {
                        avalibleAPRssiPairList.Add(apRssiPair);
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
                    //APManager.Instance.DebugStrOfLocAlg += str + "\r";
                }
                #endregion
                #endregion
                //┗━━━━━━━━━━━━━━━━━━
                //┏━━━━━信号强度平滑━━━━━
                #region 信号强度平滑
                this.historyAPRssiRecord.Refresh(avalibleAPRssiPairList);
                avalibleAPRssiPairList = this.historyAPRssiRecord.GetCurrentScanRssiPairList(currentTime);
//这里是我自己加的用以过滤太小的信号
                foreach (APRssiPair apRssiPair in avalibleAPRssiPairList)
                {
                    if (apRssiPair.Rssi < -90)
                    {
                        avalibleAPRssiPairList.Remove(apRssiPair);
                    }
                
                }
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
                    APManager.Instance.DebugStrOfLocAlg += str + "\r\n";
                    //APManager.Instance.APSetsDic[environmentId].AutoLearningThresholdTest(avalibleAPRssiPairList);
                }
                #endregion
                #endregion
                //┗━━━━━━━━━━━━━━━
                //┏━━━━━定位条件检查━━━━━
                #region 定位条件检查
                //定位条件：实时信号强度个数大于等于2，且至少有一个信号强度大于-75
//这里已经定了定位条件是信号强度个数大于等于2了，后面还要判断，主要是用来使算法该起来更加灵活，但是对我的算法貌似用处不大
                bool IsLocAvalibe = false;
                if (avalibleAPRssiPairList.Count >= Parameters.LOC_CONDITION_COUNT)
                {
                    foreach (APRssiPair p in avalibleAPRssiPairList)
                    {
                        if (p.Rssi > Parameters.LOC_CONDITION_RSSI)
                        {
                            IsLocAvalibe = true;
                        }
                    }
                }
                if (IsLocAvalibe == false)
                {
                    //不满足定位条件，直接返回
                    failedMeetLocConditionTimes++;
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("Failed To Meet The Loc Condition ! Times NO" + failedMeetLocConditionTimes.ToString());
                        APManager.Instance.DebugStrOfLocAlg += "Failed To Meet The Loc Condition ! Times NO" + failedMeetLocConditionTimes.ToString() + "\r\n";
                        debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        APManager.Instance.DebugStrOfLocAlg += "┗━━━━━━━━━━━━━━━━━━━━━━━" + "\r\n";
                        debuglog.ExitWriteLock();
                    }
                    #endregion

                    #region 返回结果填充
                    //如果最近的上次位置不是离线位置，则以上次位置的MapId作为本次消失位置的MapId
                    //如果最近的上次位置是离线位置，则以本次实时信号中信号强度最大的AP所在Map为消失位置的MapId
                    //lastLocPos = ?
                    //连续消失一段时间是否该认为是离线呢？
                    if ((failedMeetLocConditionTimes >= 3 || lastLocPos.Equals(SingleLocResult.OfflinePos) || (lastLocPos.X == 0 && lastLocPos.Y == 0)))
                    {
                        //只有连续3次不满足定位条件才会认为其消失
                        //增加条件：如果上次结果为离线,即有可能是新扫描到的终端，那么如果有1个数据也要对其定位，这样便不会遗漏那些被突然间扫描到但是后续没有在被扫描到的终端 add by szh@20140523
                        //isDisappearAvalible = false;
                        failedMeetLocConditionTimes = 0;

                        //if (lastLocPos.Equals(SingleLocResult.OfflinePos) || (lastLocPos.X == 0 && lastLocPos.Y == 0))
                        //{
                        if (avalibleAPRssiPairList.Count > 0)
                        {
                            avalibleAPRssiPairList.Sort(apRssiPairComparer);
                            int rssi = avalibleAPRssiPairList[0].Rssi;
                            string aPMac = avalibleAPRssiPairList[0].APMac;
                            int mapId = apSet.APDic[aPMac].pos.MapId;
                            locResult.positionList.Add(new PointContract
                            {
                                MapId = mapId,
                                X = 0,
                                Y = 0,
                                Z = 0,
                                Type = CoordinateType.SPACE_RCTANGULAR
                            });
                        }
                        else
                        {
                            locResult.positionList.Add(new PointContract
                            {
                                MapId = 0,
                                X = 0,
                                Y = 0,
                                Z = 0,
                                Type = CoordinateType.SPACE_RCTANGULAR
                            });
                        }
                        //}
                        //else
                        //{
                        //    locResult.positionList.Add(new PointContract
                        //    {
                        //        MapId = lastLocPos.MapId,
                        //        X = 0,
                        //        Y = 0,
                        //        Z = 0,
                        //        Type = CoordinateType.RCTANGULAR
                        //    });
                        //}
                        //返回定位结果
                        if (lastLocPos.Equals(locResult.positionList.Last()))
                        {
                            return null;
                        }
                        else
                        {
                            lastlastLocPos = lastlastLocPos.Clone();
                            lastlastLoctime=lastLoctime;
                            lastLocPos = locResult.positionList.Last().Clone();
                            lastLoctime=locResult.time;
                            return locResult;
                        }
                    }
                    else
                    {
                        return null;
                    }
                    #endregion
                }
                else
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("Success To Meet The Loc Condition!");
                        APManager.Instance.DebugStrOfLocAlg += "Success To Meet The Loc Condition!" + "\r\n";
                    }
                    #endregion
                }
                #endregion
                //┗━━━━━━━━━━━━━━━
                //┏━━━━━获取信号强度最大值━━━━━
                #region 获取信号强度最大值
                int maxRssi = int.MinValue;
                string maxRssiAPMac = null;
                int divValue = 0;
                avalibleAPRssiPairList.Sort(apRssiPairComparer);
                maxRssi = avalibleAPRssiPairList[0].Rssi;
                maxRssiAPMac = avalibleAPRssiPairList[0].APMac;
                if (avalibleAPRssiPairList.Count <= 1)
                {
                    divValue = int.MaxValue;
                    //Log.AddLogEntry(LogEntryLevel.ERROR,"执行定位条件检查后avalibleAPRssiPairList.Cout<=1!");
                }
                else
                {
                    divValue = avalibleAPRssiPairList[0].Rssi - avalibleAPRssiPairList[1].Rssi;
                }
                #region 调试输出
                if (debugEnable == true)
                {
                    debuglog.WriteLine("Max Rssi:" + maxRssi.ToString() + "(" + maxRssiAPMac + ")" + "and Dvalue = " + divValue.ToString());
                    debuglog.WriteLine( " yuzhi" + apSet.APDic[maxRssiAPMac].RssiThreshold_STA);
                   // APManager.Instance.DebugStrOfLocAlg += "Max Rssi:" + maxRssi.ToString() + "(" + maxRssiAPMac + ")" + "and Dvalue = " + divValue.ToString() +"and yuzhi"+apSet.APDic[maxRssiAPMac].RssiThreshold_STA +"\r\n";
                }
                #endregion
                #endregion
                //┗━━━━━━━━━━━━━━━━━━━
                if (avalibleAPRssiPairList.Count == 1)
                {
                    //当只有一个AP时，允许定位到AP，但定位阈值略微增大，提高可信度；如果不满足则认为无法定位  add by szh @ 20140515
                    if (maxRssi > (apSet.APDic[maxRssiAPMac].RssiThreshold_STA + 3   ))
                    {
                        //AP快速胜出
                        #region AP快速胜出    
                        locPos = apSet.APDic[maxRssiAPMac].pos.Clone();
                        int locAPVertexId = apSet.APDic[maxRssiAPMac].vertexId;
                        locCellId = vertexSet.VertexDic[locAPVertexId].cellId;
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("Loc Result:" + apSet.APDic[maxRssiAPMac].name);
                            APManager.Instance.DebugStrOfLocAlg += "Loc Result:" + apSet.APDic[maxRssiAPMac].name + "\r\n";
                            debuglog.WriteLine("Loc Reson: AP Quick Wins!!!MaxRssi(" + maxRssi + ")>Threshold(" + apSet.APDic[maxRssiAPMac].RssiThreshold_STA + ")");
                            APManager.Instance.DebugStrOfLocAlg += "Loc Reson: AP Quick Wins!!!MaxRssi(" + maxRssi + ")>Threshold(" + apSet.APDic[maxRssiAPMac].RssiThreshold_STA + ")" + "\r\n";
                        }
                        #endregion
                        #endregion
                        //清空连续跳跃次数计数器
                        lastComparisonLocCellTimes = 0;
                        //清空无法定位计数器
                        failedMeetLocConditionTimes = 0;
                    }
                    else
                    {
                        failedMeetLocConditionTimes++;
                        #region 调试输出
                        if (debugEnable)
                        {
                            debuglog.WriteLine("Loc Result:null");
                            APManager.Instance.DebugStrOfLocAlg += "Loc Result:null" + "\r\n";
                            debuglog.WriteLine("Loc Reson: Disable To Calculate Loc Result!!!Times NO" + failedMeetLocConditionTimes.ToString());
                            APManager.Instance.DebugStrOfLocAlg += "Loc Reson: Disable To Calculate Loc Result!!!Times NO" + failedMeetLocConditionTimes.ToString() + "\r\n";
                            debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            APManager.Instance.DebugStrOfLocAlg += "┗━━━━━━━━━━━━━━━━━━━━━━━" + "\r\n";
                            debuglog.ExitWriteLock();
                        }
                        #endregion

                        #region 返回结果填充
                        //如果最近的上次位置不是离线位置，则以上次位置的MapId作为本次消失位置的MapId
                        //如果最近的上次位置是离线位置，则以本次实时信号中信号强度最大的AP所在Map为消失位置的MapId
                        //lastLocPos = ?
                        //连续消失一段时间是否该认为是离线呢？
                        if ((failedMeetLocConditionTimes >= 3 || lastLocPos.Equals(SingleLocResult.OfflinePos) || (lastLocPos.X == 0 && lastLocPos.Y == 0)))
                        {
                            //只有连续3次不满足定位条件才会认为其消失
                            //增加条件：如果上次结果为离线,即有可能是新扫描到的终端，那么如果有1个数据也要对其定位，这样便不会遗漏那些被突然间扫描到但是后续没有在被扫描到的终端 add by szh@20140523
                            //isDisappearAvalible = false;
                            failedMeetLocConditionTimes = 0;

                            //if (lastLocPos.Equals(SingleLocResult.OfflinePos) || (lastLocPos.X == 0 && lastLocPos.Y == 0))
                            //{
                            if (avalibleAPRssiPairList.Count > 0)
                            {
                                avalibleAPRssiPairList.Sort(apRssiPairComparer);
                                int rssi = avalibleAPRssiPairList[0].Rssi;
                                string aPMac = avalibleAPRssiPairList[0].APMac;
                                int mapId = apSet.APDic[aPMac].pos.MapId;
                                locResult.positionList.Add(new PointContract
                                {
                                    MapId = mapId,
                                    X = 0,
                                    Y = 0,
                                    Z = 0,
                                    Type = CoordinateType.SPACE_RCTANGULAR
                                });
                            }
                            else
                            {
                                locResult.positionList.Add(new PointContract
                                {
                                    MapId = 0,
                                    X = 0,
                                    Y = 0,
                                    Z = 0,
                                    Type = CoordinateType.SPACE_RCTANGULAR
                                });
                            }
                            //}
                            //else
                            //{
                            //    locResult.positionList.Add(new PointContract
                            //    {
                            //        MapId = lastLocPos.MapId,
                            //        X = 0,
                            //        Y = 0,
                            //        Z = 0,
                            //        Type = CoordinateType.RCTANGULAR
                            //    });
                            //}
                            //返回定位结果
                            if (lastLocPos.Equals(locResult.positionList.Last()))
                            {
                                return null;
                            }
                            else
                            {
                                lastlastLoctime=lastLoctime;
                                lastlastLocPos = lastLocPos.Clone();
                                lastLoctime=locResult.time;
                                lastLocPos = locResult.positionList.Last().Clone();
                                return locResult;
                            }
                        }
                        else
                        {
                            return null;
                        }
                        #endregion
                    }
                }
                else if (maxRssi > apSet.APDic[maxRssiAPMac].RssiThreshold_STA && divValue>=10)
                {
                    //AP快速胜出
                    #region AP快速胜出
                    locPos = apSet.APDic[maxRssiAPMac].pos.Clone();
                    int locAPVertexId = apSet.APDic[maxRssiAPMac].vertexId;
                    locCellId = vertexSet.VertexDic[locAPVertexId].cellId;
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("Loc Result:" + apSet.APDic[maxRssiAPMac].name);
                        APManager.Instance.DebugStrOfLocAlg += "Loc Result:" + apSet.APDic[maxRssiAPMac].name + "\r\n";
                        debuglog.WriteLine("Loc Reson: AP Quick Wins!!!MaxRssi(" + maxRssi + ")>Threshold(" + apSet.APDic[maxRssiAPMac].RssiThreshold_STA + ")");
                        APManager.Instance.DebugStrOfLocAlg += "Loc Reson: AP Quick Wins!!!MaxRssi(" + maxRssi + ")>Threshold(" + apSet.APDic[maxRssiAPMac].RssiThreshold_STA + ")" + "\r\n";
                    }
                    #endregion
                    #endregion
                    //清空连续跳跃次数计数器
                    lastComparisonLocCellTimes = 0;
                    //清空无法定位计数器
                    failedMeetLocConditionTimes = 0;
                    isDisappearAvalible = true;
                }
                else
                {
                    //没有AP快速胜出，执行采样对比算法
                    #region 采样对比
                    locPos = ComparisonLoc(avalibleAPRssiPairList, debugEnable, out locCellId);
                    if (locPos != null)
                    {
                        #region 调试输出
                        if (debugEnable)
                        {
                            debuglog.WriteLine("Loc Result:" + "(" + locPos.X.ToString() + "," + locPos.Y.ToString() + ")" + " in Cell " + locCellId.ToString() + " @Map" + locPos.MapId.ToString());
                            APManager.Instance.DebugStrOfLocAlg += "Loc Result:" + "(" + locPos.X.ToString() + "," + locPos.Y.ToString() + ")" + " in Cell " + locCellId.ToString() + " @Map" + locPos.MapId.ToString() + "\r\n";
                            debuglog.WriteLine("Loc Reson: Comparison Loc!!!");
                            APManager.Instance.DebugStrOfLocAlg += "Loc Reson: Comparison Loc!!!" + "\r\n";
                        }
                        #endregion
                        //清空无法定位计数器
                        failedMeetLocConditionTimes = 0;
                        isDisappearAvalible = true;
                    }
                    else
                    {
                        failedMeetLocConditionTimes++;

                        #region 调试输出
                        if (debugEnable)
                        {
                            debuglog.WriteLine("Loc Result:null");
                            APManager.Instance.DebugStrOfLocAlg += "Loc Result:null" + "\r\n";
                            debuglog.WriteLine("Loc Reson: Disable To Calculate Loc Result!!!Times NO" + failedMeetLocConditionTimes.ToString());
                            APManager.Instance.DebugStrOfLocAlg += "Loc Reson: Disable To Calculate Loc Result!!!Times NO" + failedMeetLocConditionTimes.ToString() + "\r\n";
                            debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            APManager.Instance.DebugStrOfLocAlg += "┗━━━━━━━━━━━━━━━━━━━━━━━" + "\r\n";
                            debuglog.ExitWriteLock();
                        }
                        #endregion

                        #region 返回结果填充
                        //如果最近的上次位置不是离线位置，则以上次位置的MapId作为本次消失位置的MapId
                        //如果最近的上次位置是离线位置，则以本次实时信号中信号强度最大的AP所在Map为消失位置的MapId
                        //lastLocPos = ?
                        //连续消失一段时间是否该认为是离线呢？
                        if ((failedMeetLocConditionTimes >= 3 || lastLocPos.Equals(SingleLocResult.OfflinePos) || (lastLocPos.X == 0 && lastLocPos.Y == 0)))
                        {
                            //只有连续3次不满足定位条件才会认为其消失
                            //增加条件：如果上次结果为离线,即有可能是新扫描到的终端，那么如果有1个数据也要对其定位，这样便不会遗漏那些被突然间扫描到但是后续没有在被扫描到的终端 add by szh@20140523
                            //isDisappearAvalible = false;
                            failedMeetLocConditionTimes = 0;

                            //if (lastLocPos.Equals(SingleLocResult.OfflinePos) || (lastLocPos.X == 0 && lastLocPos.Y == 0))
                            //{
                            if (avalibleAPRssiPairList.Count > 0)
                            {
                                avalibleAPRssiPairList.Sort(apRssiPairComparer);
                                int rssi = avalibleAPRssiPairList[0].Rssi;
                                string aPMac = avalibleAPRssiPairList[0].APMac;
                                int mapId = apSet.APDic[aPMac].pos.MapId;
                                locResult.positionList.Add(new PointContract
                                {
                                    MapId = mapId,
                                    X = 0,
                                    Y = 0,
                                    Z = 0,
                                    Type = CoordinateType.SPACE_RCTANGULAR
                                });
                            }
                            else
                            {
                                locResult.positionList.Add(new PointContract
                                {
                                    MapId = 0,
                                    X = 0,
                                    Y = 0,
                                    Z = 0,
                                    Type = CoordinateType.SPACE_RCTANGULAR
                                });
                            }
                            //}
                            //else
                            //{
                            //    locResult.positionList.Add(new PointContract
                            //    {
                            //        MapId = lastLocPos.MapId,
                            //        X = 0,
                            //        Y = 0,
                            //        Z = 0,
                            //        Type = CoordinateType.RCTANGULAR
                            //    });
                            //}
                            //返回定位结果
                            if (lastLocPos.Equals(locResult.positionList.Last()))
                            {
                                return null;
                            }
                            else if (lastlastLocPos.Equals(locResult.positionList.Last()) && (locResult.time-lastlastLoctime).TotalSeconds<=6)
                            {
                                return null;
                            }
                            else
                            {
                                lastlastLoctime=lastLoctime;
                                lastlastLocPos = lastLocPos.Clone();
                                lastLoctime=locResult.time;
                                lastLocPos = locResult.positionList.Last().Clone();
                                return locResult;
                            }
                        }
                        else
                        {
                            return null;
                        }
                        #endregion
                    }
                    #endregion
                }
                //┏━━━━━路径计算━━━━━
                #region 路径计算
                #region 调试输出
                if (debugEnable == true)
                {
                    if (lastLocPos.MapId != locPos.MapId)
                    {
                        APManager.Instance.DebugStrOfLocAlg += "Map Change:" + lastLocPos.MapId + ">>>>>>>>>>>>>>>>" + locPos.MapId + "!!!!!\r\n";
                        APManager.Instance.DebugStrOfLocAlg += "Map Change:" + lastLocPos.MapId + ">>>>>>>>>>>>>>>>" + locPos.MapId + "!!!!!（因为重要，所以再来一次）\r\n";
                        APManager.Instance.DebugStrOfLocAlg += "Map Change:" + lastLocPos.MapId + ">>>>>>>>>>>>>>>>" + locPos.MapId + "!!!!!（因为重要，所以再来一次）\r\n";
                    }
                    debuglog.WriteLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    APManager.Instance.DebugStrOfLocAlg += "┗━━━━━━━━━━━━━━━━━━━━━━━" + "\r\n";
                    debuglog.ExitWriteLock();
                }
                #endregion
                if (locPos == null || lastLocPos.Equals(locPos))
                {
                    return null;
                }
                else
                {
                    List<PointContract> path = VertexManager.Instance.VertexSetsDic[environmentId].GetPath(lastLocPos, lastCellId, locPos, locCellId);
                    locResult.positionList = path;
                    lastAvalibleAPRssiPairList = avalibleAPRssiPairList;
                    lastCellId = locCellId;
                    lastlastLocPos = lastLocPos.Clone();
                    lastLocPos = locPos;
                    return locResult;
                }

                #endregion
                //┗━━━━━━━━━━━━━━
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "STALocAlgV2.Loc执行错误：" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 采样点对比定位，如果定位失败返回null，cellId返回-1，如果成功返回位置，cellId返回定位结果所在cell
        /// </summary>
        /// <param name="apRssiPairList"></param>
        /// <param name="debugEnable"></param>
        /// <param name="cellId"></param>
        /// <returns></returns>
        private PointContract ComparisonLoc(List<APRssiPair> apRssiPairList, bool debugEnable, out int cellId)
        {
            PointContract locPos;
            cellId = -1;
            //┏━━━━━选取近邻AP和近邻采样点集合━━━━━
            #region 选取近邻AP，得到近邻采样点集合
            List<string> adjAPMacList = SamplingPointManager.GetAdjAPMacList(apRssiPairList, this.environmentId);
            int largestscore = 0;
            List<int> orgcommonsamplingPointIdlist = new List<int>();
            Dictionary<int, int> adjSamplingPointIdDistanceDic = new Dictionary<int, int>();
           
           
                try
                {
                    foreach (int p in samplingPointSet.samplingPointIDAdjapDic.Keys)
                    {
                        int score = ScoreForSamplingPoint(samplingPointSet.samplingPointIDAdjapDic[p], adjAPMacList);
                        adjSamplingPointIdDistanceDic.Add(p, score);
                        if (score >= largestscore)
                        { largestscore = score; }
                    }
                    foreach (KeyValuePair<int, int> p in adjSamplingPointIdDistanceDic)
                    {
                        if (p.Value == largestscore)
                            orgcommonsamplingPointIdlist.Add(p.Key);
                    }
                }
                catch (Exception ex)
                {
                    Log.AddLogEntry(LogEntryLevel.ERROR, ex.Message + ex.StackTrace.ToString());
                }
                List<int> commonsamplingPointIdlist = new List<int>();
                commonsamplingPointIdlist = comparecommonsamplingpoint(orgcommonsamplingPointIdlist, adjAPMacList, apRssiPairList, debugEnable);

            //#region 选取最合理的采样点，这里是选取commonsamplingpoint

            //string ApMac = adjAPMacList[0];
            //if (samplingPointSet.apAdjSamplingPointIdDic.ContainsKey(ApMac))
            //{
            //    foreach (int samplingPointId in samplingPointSet.apAdjSamplingPointIdDic[ApMac])
            //    {
            //        //这里是需要注意的地方，这里只把采样点重新加到list里面，但是我需要对采样点进行排序，确定那个是最邻近的采样点
            //        commonsamplingPointIdlist.Add(samplingPointId);
            //    }
            //}



            //foreach (string apMac in adjAPMacList)
            //{

            //    if (samplingPointSet.apAdjSamplingPointIdDic.ContainsKey(apMac))
            //    {
            //        List<int> commonsamplingPointIdDistancelist = new List<int>();
            //        foreach (int samplingPointIdfirst in commonsamplingPointIdlist)
            //        {

            //            foreach (int samplingPointId in samplingPointSet.apAdjSamplingPointIdDic[apMac])
            //            {
            //                if (samplingPointId == samplingPointIdfirst) { commonsamplingPointIdDistancelist.Add(samplingPointId); }

            //                //这里是需要注意的地方，这里只把采样点重新加到list里面，但是我需要对采样点进行排序，确定那个是最邻近的采样点
            //            }
            //        }
            //        commonsamplingPointIdlist = commonsamplingPointIdDistancelist;

            //    }
            //}
            //#endregion
            #region 调试输出
            try
            {
                if (debugEnable == true)
                {
                    string str = "";
                    str += "The Adj APs:";
                    foreach (string apMac in adjAPMacList)
                    {
                        str += apSet.APDic[apMac].name + ",";
                    }
                    debuglog.WriteLine(str);
                    str = "";
                    str += "The Adj Sampling Points:";
                    foreach (int samplingPointId in commonsamplingPointIdlist)
                    {
                        str += samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].name + ",";
                    }
                    debuglog.WriteLine(str);
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, ex.Message + ex.StackTrace.ToString());
            }
            #endregion
            #endregion
            //┗━━━━━━━━━━━━━━━━━━━━━━━

            #region 判断是否跨Cell
            
            int locCellId = -1;
            if (commonsamplingPointIdlist.Count >= 1)
            {
                int minEculidDistanceSamplingPointId = commonsamplingPointIdlist[0];
                int minEculidDistanceSamplingPointVertexId = samplingPointSet.samplingPointDetaiInfoDic[minEculidDistanceSamplingPointId].vertexId;
                int locMapId = vertexSet.VertexDic[minEculidDistanceSamplingPointVertexId].pos.MapId;
                locCellId = vertexSet.VertexDic[minEculidDistanceSamplingPointVertexId].cellId;
                if ((locCellId != lastCellId) || (locMapId != lastLocPos.MapId))
                {
                    //┏━━━━━跨Cell或者跨地图━━━━━
                    //产生了跨区域跳跃,如果满足可信条件则将欧式距离最小的采样点位置作为定位结果返回；否则返回null表示位置不变
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str = "跨Cell from " + lastCellId.ToString() + "@Map" + lastLocPos.MapId.ToString() + " to " + locCellId.ToString() + "@Map" + locMapId.ToString();
                        debuglog.WriteLine(str);
                    }
                    #endregion
                    if (IsComparisonRelible(apRssiPairList, minEculidDistanceSamplingPointId, locCellId, debugEnable))
                    {
                        cellId = locCellId;
                        //修改连续跳跃次数为0
                        lastComparisonLocCellTimes = 0;
                        return vertexSet.VertexDic[minEculidDistanceSamplingPointVertexId].pos.Clone();
                    }
                    else
                    {
                        cellId = lastCellId;
                        return null;
                    }
                    //┗━━━━━━━━━━━━━━━━━━
                }
                else
                {
                    //┏━━━━━没有跨Cell━━━━━
                    //修改连续跳跃次数为0
                    lastComparisonLocCellTimes = 0;
                    //没有产生跨区域跳跃，则选取加权平均位置作为定位结果返回；
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str = "未跨Cell";
                        debuglog.WriteLine(str);
                    }
                    #endregion
            
                    locPos = new PointContract();
                    
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str += "SmallestSomeSamplingPoints:";
                        foreach (int  r in commonsamplingPointIdlist)
                        {
                            if (r == -1)
                            {
                                str += "Last" + ",";
                            }
                            else
                            {
                                str += SamplingPointManager.Instance.SamplingPointSetsDic[environmentId].samplingPointDetaiInfoDic[r].name + ",";
                            }
                        }
                        debuglog.WriteLine(str);
                    }
                    #endregion
                    //List<CompareResult> smallerSomeList = new List<CompareResult>();
                    if (commonsamplingPointIdlist.Count == 0)
                    {
                        return null;
                    }
                    else if (commonsamplingPointIdlist.Count == 1)
                    {   
                        locPos.MapId = locMapId;
                        locPos.Type = CoordinateType.SPACE_RCTANGULAR;
                        int samplingPointId = commonsamplingPointIdlist[0];
                        locPos.X=vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.X;
                        locPos.Y = vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.Y;
                      return locPos;
                    }
                    else if (commonsamplingPointIdlist.Count >= 2)
                    {
                        //在这里加入了近邻ap的rssi数据，作为定位到采样点的信息进行考虑，如果并没有存在两个以上的采样点，那么这种是唯一可以
                        //进入考虑的信息了，所以在这里作为一个分类依据重新进行考虑。
                       // int commonsamplingPointId = Comparecommonsamplingpoint(commonsamplingPointIdlist, adjAPMacList, apRssiPairList, debugEnable);

                            if (!this.lastLocPos.Equals(SingleLocResult.OfflinePos) && !this.lastLocPos.Equals(SingleLocResult.UnknownPos))
                            {
                                int commonsamplingPointId = GetTheBestFitbyDistance(commonsamplingPointIdlist);
                                if (commonsamplingPointId!=-8)
                                {
                                    locPos.MapId = locMapId;
                                    locPos.Type = CoordinateType.SPACE_RCTANGULAR;
                                    int samplingPointId = commonsamplingPointId;
                                    locPos.X = vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.X;
                                    locPos.Y = vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.Y;
                                    return locPos;
                                }
                                else
                                {
                                    return null;

                                }
                            }
                            else
                            {
                               // locPos.MapId = locMapId;
                               // locPos.Type = CoordinateType.SPACE_RCTANGULAR;
                               // int samplingPointId = commonsamplingPointIdlist[0];
                               //locPos.X = vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.X;
                               //locPos.Y = vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.Y;
                               // return locPos;
                                return null;
                            }
                     
                       

                    }
                    else
                    {
                        return null;
                    }
                 
                  
                    
                  
                    //┗━━━━━━━━━━━━━━━
                }
            }

            #endregion

            return null;
        }


        private int lastComparisonLocCellId = -1;
        private int lastComparisonLocCellTimes = 0;
        /// <summary>
        /// 对比结果可信判断，先用简化的条件，根据测试结果再进行扩充
        /// </summary>
        /// <param name="实时信号强度特征向量"></param>
        /// <param name="欧式距离最小的采样点Id"></param>
        /// <param name="欧式距离最小的采样点所处CellId"></param>
        /// <returns></returns>
        /// 
        private bool IsComparisonRelible(List<APRssiPair> apRssiPairList, int locSamplingPointId, int locCellId, bool debugEnable)
        {
            //条件1：采样结果中信号强队最大的3个AP都有实时信号与其对应，且每个的差绝对值小于10
            //条件2：采样结果中信号强度最大的3个AP都有实时信号与其对应，且连续三次均跨到相同cell

            //判断信号强度最大的3个AP是否都有实时信号与其对应,flag存放判断结果
            bool flag = true;
            Dictionary<string, SamplingStaticsInfo> samplingStaticsDic = samplingPointSet.samplingPointDetaiInfoDic[locSamplingPointId].samplingResultDic[SamplingContract.TagType.STA].staticsInfoDic;
            List<APRssiPair> staticsRssiPairList = new List<APRssiPair>();
            foreach (string apMac in samplingStaticsDic.Keys)
            {
                staticsRssiPairList.Add(new APRssiPair
                {
                    APMac = apMac,
                    Rssi = samplingStaticsDic[apMac].MeanRssi
                });
            }
            if (staticsRssiPairList.Count >= Math.Min(Parameters.CROSSCELL_BIGGEST_RSSI_COUNT, staticsRssiPairList.Count / 2))
            {
                List<string> apMacList = GetMaxRssiAPMacList(staticsRssiPairList, Math.Min(Parameters.CROSSCELL_BIGGEST_RSSI_COUNT, staticsRssiPairList.Count / 2));
                foreach (string apMac in apMacList)
                {
                    bool isContains = false;
                    foreach (APRssiPair p in apRssiPairList)
                    {
                        if (p.APMac == apMac)
                        {
                            isContains = true;
                            break;
                        }
                    }
                    if (!isContains)
                    {
                        flag = false;
                        break;
                    }
                }
                if (flag)
                {
                    //┏━━━━━最大的3个AP都有实时信号与其对应━━━━━
                    #region 采样结果中信号强度最大的3个AP都有实时信号与其对应
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str = "最大的" + Math.Min(Parameters.CROSSCELL_BIGGEST_RSSI_COUNT, staticsRssiPairList.Count / 2) + "个AP都有实时信号与其对应:满足√";
                        debuglog.WriteLine(str);
                    }
                    #endregion
                    //┏━━━━━判断条件1━━━━━━
                    #region 判断条件1
                    bool Condition1Meeted = true;
                    foreach (string apMac in apMacList)
                    {
                        foreach (APRssiPair p in apRssiPairList)
                        {
                            if (apMac == p.APMac)
                            {
                                if (System.Math.Abs(p.Rssi - samplingStaticsDic[apMac].MeanRssi) > Parameters.CROSSCELL_MAX_DVALUE)
                                {
                                    Condition1Meeted = false;
                                    break;
                                }
                            }
                        }
                    }
                    if (Condition1Meeted)
                    {
                        //满足条件1
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            string str = "";
                            str = "每个的差绝对值小于" + Parameters.CROSSCELL_MAX_DVALUE + ":满足√";
                            debuglog.WriteLine(str);
                        }
                        #endregion
                        return true;
                    }
                    #endregion
                    //┗━━━━━━━━━━━━━━━━
                    //┏━━━━━判断条件2━━━━━━
                    #region 判断条件2
                    bool Condition2Meeted = true;
                    if (lastComparisonLocCellId == locCellId)
                    {
                        lastComparisonLocCellTimes++;
                        if (lastComparisonLocCellTimes < Parameters.CROSSCELL_CREDIBLE_CROSS_COUNT)
                        {
                            Condition2Meeted = false;
                        }
                    }
                    else
                    {
                        lastComparisonLocCellId = locCellId;
                        lastComparisonLocCellTimes = 1;
                        Condition2Meeted = false;
                    }
                    if (Condition2Meeted)
                    {
                        //满足条件2
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            string str = "";
                            str = "连续" + Parameters.CROSSCELL_CREDIBLE_CROSS_COUNT + "次均跨到相同cell:满足√";
                            debuglog.WriteLine(str);
                        }
                        #endregion
                        return true;
                    }
                    #endregion
                    //┗━━━━━━━━━━━━━━━━
                    //┏━━━━━不满足任何条件━━━━
                    #region 不满足条件
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str = "不满足任何条件×";
                        debuglog.WriteLine(str);
                    }
                    #endregion
                    return false;
                    #endregion
                    //┗━━━━━━━━━━━━━━━━
                    #endregion
                    //┗━━━━━━━━━━━━━━━━━━━━━━━━━
                }
                else
                {
                    //最大的3个AP并不都有实时信号与其对应
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        string str = "";
                        str = "最大的" + Math.Min(Parameters.CROSSCELL_BIGGEST_RSSI_COUNT, staticsRssiPairList.Count / 2) + "个AP并不都有实时信号与其对应×";
                        debuglog.WriteLine(str);
                    }
                    #endregion
                    return false;
                }

            }
            else
            {
                //实时信号强度少于3个
                #region 调试输出
                if (debugEnable == true)
                {
                    string str = "";
                    str = "实时信号强度少于" + Parameters.CROSSCELL_BIGGEST_RSSI_COUNT + "个×";
                    debuglog.WriteLine(str);
                }
                #endregion
                return false;
            }
        }

        /// <summary>
        /// 获取最多maxCount个信号强度最大的APMac的List
        /// </summary>
        /// <param name="apRssiPairList"></param>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        private List<string> GetMaxRssiAPMacList(List<APRssiPair> apRssiPairList, int maxCount)
        {
            List<string> apMacList = new List<string>();
            if (apRssiPairList != null)
            {
                apRssiPairList.Sort(new APRssiPairComparer());
                for (int i = 0; i < maxCount && i < apRssiPairList.Count; i++)
                {
                    if (APManager.Instance.APSetsDic.ContainsKey(this.environmentId) && APManager.Instance.APSetsDic[this.environmentId].GetAPStatus(apRssiPairList[i].APMac) == DevStatus.ONLINE)
                    {
                        apMacList.Add(apRssiPairList[i].APMac);
                    }
                }
            }
            return apMacList;
        }

        /// <summary>
        /// 获取采样对比结果中欧式距离且采样点位置处于相同Cell的最小的若干个采样对比结果列表
        /// </summary>
        /// <param name="resultList"></param>
        /// <param name="cellId"></param>
        /// <returns></returns>
        public List<CompareResult> GetSmallerSome(List<CompareResult> resultList, int cellId)
        {
            if (resultList != null && resultList.Count > 1)
            {
                resultList.Sort();
                int medianIndex = (int)System.Math.Ceiling(((double)resultList.Count) / 2.0);       //向上取整
                int minDistance = resultList.First().eculidDistance;
                int medianDistance = resultList.ElementAt(medianIndex).eculidDistance;
                List<CompareResult> smallerSomeList = new List<CompareResult>();
                foreach (CompareResult r in resultList)
                {
                    if (r.samplingPointId != -1 && vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[r.samplingPointId].vertexId].cellId != cellId)
                    {
                        continue;
                    }
                    if (r.eculidDistance < ((minDistance + medianDistance) / 2))
                    {
                        smallerSomeList.Add(r);
                    }
                    else
                    {
                        break;
                    }
                }
                if (smallerSomeList.Count > Parameters.ECULIDDISTANCE_SMALLER_SOME_MAX_COUNT)
                {
                    smallerSomeList = GetSmallerSome(smallerSomeList, cellId);
                }
                return smallerSomeList;
            }
            else
            {
                return new List<CompareResult>();
            }
        }

        /// <summary>
        /// 计算欧式距离，可以合并整理到SamplingPointManager里
        /// </summary>
        /// <param name="originalAPRssiPairList"></param>
        /// <param name="dstAPRssiPairList"></param>
        /// <param name="debugEnable"></param>
        /// <returns></returns>
        private int CalculateEculidDistance(List<APRssiPair> originalAPRssiPairList, List<APRssiPair> dstAPRssiPairList, bool debugEnable)
        {
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
            return distance;
        }

        private int CalculateDistance(int samplingPointId)
        {

           
            
            int x = vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.X;
            int y = vertexSet.VertexDic[samplingPointSet.samplingPointDetaiInfoDic[samplingPointId].vertexId].pos.Y;
            int x1 = this.lastLocPos.X;
            int y1 = this.lastLocPos.Y;

            return (int)Math.Sqrt((x - x1)*(x - x1)+(y-y1)*(y-y1));
        
        }

        private List<string> GetAdjAPMacById(int samplingPointId, List<string> adjAPMacList)
        { 
            List<string> adjAPmaclist=new List<string>();

            foreach (string apMac in adjAPMacList)
            {
                if (samplingPointSet.apAdjSamplingPointIdDic.ContainsKey(apMac))
                {
                        foreach (int samplingPoint in samplingPointSet.apAdjSamplingPointIdDic[apMac])
                        {
                            if (samplingPointId == samplingPoint)

                                adjAPmaclist.Add(apMac);
                         }

                           //这里是需要注意的地方，这里只把采样点重新加到list里面，但是我需要对采样点进行排序，确定那个是最邻近的采样点
                 }
            }
            return adjAPmaclist;
        
        }
        private int ScoreForSamplingPoint(List<string> SamplingPointadjAPlist, List<string> adjAPlist)
        {
            
            int score=0;
            if (SamplingPointadjAPlist.Count != 0 && adjAPlist.Count != 0)
            {
                if (this.equal(SamplingPointadjAPlist,adjAPlist))
                {
                    score = int.MaxValue;
                    return score;
                }
                else 
                {
                foreach (string samplingpointadjap in SamplingPointadjAPlist)
                {
                    if (adjAPlist.Contains(samplingpointadjap)) 
                    {
                        score++;
                    }
                }
                
                }

            }
            
            return score;
        }

        private Dictionary<string,int> getadjaprssibyap(List<string> adjapmaclist,List<APRssiPair> aprssipairlist)
        {

            Dictionary<string,int> adjAPRssiPairDic=new Dictionary<string,int>();
            foreach (string apmac in adjapmaclist)
            {
                foreach (APRssiPair aprssipair in aprssipairlist )
                  if (aprssipair.APMac==apmac)
                 adjAPRssiPairDic.Add(apmac,aprssipair.Rssi);
            }
                
              return  adjAPRssiPairDic;  
        }

        private int Comparecommonsamplingpoint(List<int> commomsamplingpoint, List<string> adjAPMacList, List<APRssiPair> APRssiPairList, bool debugEnable)
        {
            Dictionary<int, int> SamplingPointScoreDic = new Dictionary<int, int>();
            int lowerscore = int.MaxValue;
            Dictionary<string, int> adjAPRssiPairDic = getadjaprssibyap(adjAPMacList, APRssiPairList);
            foreach (int p in commomsamplingpoint)
            {
                int score = 0;
                Dictionary<string, int> WeightadjAPDic = samplingPointSet.weightsamplingpointadjapdic[p];
                foreach (KeyValuePair<string, int> adjp in adjAPRssiPairDic)
                {
                    foreach (KeyValuePair<string, int> adjp2 in adjAPRssiPairDic)
                    {
                        score = score + Math.Abs((adjp.Value - adjp2.Value) / 5 - (WeightadjAPDic[adjp.Key] - WeightadjAPDic[adjp2.Key]));
                    }
                }
                
                #region 调试输出
                try
                {
                    if (debugEnable == true)
                    {
                        string str = "";

                        str += "The Adj Sampling Points:";

                        str += samplingPointSet.samplingPointDetaiInfoDic[p].name + ",";
                        str += score;
                        debuglog.WriteLine(str);
                    }
                }
                catch (Exception ex)
                {
                    Log.AddLogEntry(LogEntryLevel.ERROR, ex.Message + ex.StackTrace.ToString());
                }
                #endregion
                if (score <= lowerscore)
                {
                    SamplingPointScoreDic.Add(p, score);
                    lowerscore = score;

                }
            }

            if (lowerscore != int.MaxValue)
            {
                return SamplingPointScoreDic.Keys.Last();
            }
            return -8;
        }
        private List<int> comparecommonsamplingpoint(List<int> commomsamplingpoint, List<string> adjAPMacList, List<APRssiPair> APRssiPairList, bool debugEnable)
        {
            Dictionary<int, int> SamplingPointScoreDic = new Dictionary<int, int>();
            int lowerscore = int.MaxValue;
            Dictionary<string, int> adjAPRssiPairDic = getadjaprssibyap(adjAPMacList, APRssiPairList);
            foreach (int p in commomsamplingpoint)
            {
                int score = 0;
                Dictionary<string, int> WeightadjAPDic = samplingPointSet.weightsamplingpointadjapdic[p];

                foreach (KeyValuePair<string, int> adjp in adjAPRssiPairDic)
                {
                    if (!WeightadjAPDic.Keys.Contains(adjp.Key))
                    {
                        WeightadjAPDic.Add(adjp.Key, 1);
                    }
                }
                foreach (KeyValuePair<string, int> adjp in adjAPRssiPairDic)
                {
                    foreach (KeyValuePair<string, int> adjp2 in adjAPRssiPairDic)
                    {
                        score = score + Math.Abs((adjp.Value - adjp2.Value) / 5 - (WeightadjAPDic[adjp.Key] - WeightadjAPDic[adjp2.Key]));
                    }
                }
                SamplingPointScoreDic.Add(p, score);
                #region 调试输出
                try
                {
                    if (debugEnable == true)
                    {
                        string str = "";
                        
                        str += "The Adj Sampling Points:";
                       
                          str += samplingPointSet.samplingPointDetaiInfoDic[p].name + ",";
                          str += score;
                        debuglog.WriteLine(str);
                    }
                }
                catch (Exception ex)
                {
                    Log.AddLogEntry(LogEntryLevel.ERROR, ex.Message + ex.StackTrace.ToString());
                }
                #endregion
                if (score <= lowerscore)
                {
                    lowerscore = score;
                   
                }
            }
            List<int> compareresult=new List<int>();
            foreach (KeyValuePair<int, int> SamplingPointScore in SamplingPointScoreDic)
            { 
                if (SamplingPointScore.Value==lowerscore)
                    compareresult.Add(SamplingPointScore.Key);
            }
            if (lowerscore != int.MaxValue)
            {
                return compareresult;
            }
            //这里如果是return null 会报错，因为这时候compareresult不存在
            return compareresult;
        }
        private bool equal(List<string> SamplingPointadjAPlist, List<string> adjAPlist)
        {
            bool equal = true;
            foreach (string samplingpointadjap in SamplingPointadjAPlist) 
            {
                if (!adjAPlist.Contains(samplingpointadjap))
                    equal = false;
            }
            foreach (string adjAP in adjAPlist)
            {
                if (!SamplingPointadjAPlist.Contains(adjAP))
                    equal = false;
            }
            return equal;
        }
         private int GetTheBestFitbyDistance(List<int> commonsamplingpointlist)
         {   int nearst=int.MaxValue;
             Dictionary<int,int> SamplingPointDistanceDic=new Dictionary<int,int>();
             if (!this.lastLocPos.Equals(SingleLocResult.OfflinePos) && !this.lastLocPos.Equals(SingleLocResult.UnknownPos))
             {
                 foreach (int commonsamplingpoint in commonsamplingpointlist)
                 {
                     int distance = CalculateDistance(commonsamplingpoint);
                     SamplingPointDistanceDic.Add(commonsamplingpoint, distance);
                     if (distance <= nearst)
                     {
                         nearst = distance;
                     }
                 }
                 foreach (KeyValuePair<int, int> SamplingPointDistance in SamplingPointDistanceDic)
                 {
                     if (SamplingPointDistance.Value == nearst)
                         return SamplingPointDistance.Key;
                 }
             }
             else
             {
                 return -8;
             }
             return -8;
         }
        #endregion

    }
}
