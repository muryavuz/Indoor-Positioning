/*  
 * 文件名：         RFIDLocAlgV1.cs
 * 文件功能描述：   RFID定位算法V1
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
    public class RFIDLocAlgV1
    {
        private HistoryAPScanedRecord historyAPScanedRec;
        private List<string> lastScanedAPMacList;
        private List<int> lastScanedAPVertexIdList;
        private int environmentId;
        private PointContract lastLocPos;
        private bool IsUnknown;
        private Dictionary<int,VertexContractEx> vertexDic;
        public RFIDLocAlgV1(int e)
        {
            this.historyAPScanedRec = new HistoryAPScanedRecord();
            this.environmentId = e;
            this.lastLocPos = null;
            this.lastScanedAPVertexIdList = new List<int>();
            if(VertexManager.Instance.VertexSetsDic.ContainsKey(environmentId))
            {
                this.vertexDic = VertexManager.Instance.VertexSetsDic[environmentId].VertexDic;
            }
        }

        public SingleLocResult Loc(List<APScaneTimePair> scanedAPList, DateTime time, bool debugEnable)
        {
            SingleLocResult locResult = new SingleLocResult();
            locResult.time = time;
            locResult.positionList = new List<PointContract>();
            #region 调试输出
            if (debugEnable == true)
            {
                debuglog.EnterWriteLock();
                debuglog.WriteLine("-------------------------------------");
                debuglog.WriteLine("Time:" + time.ToString());
                string str = "";
                str += "ScanAPs:";
                foreach (APScaneTimePair p in scanedAPList)
                {
                    str += p.apMac;
                    str += ",";
                }
                debuglog.WriteLine(str);
            }
            #endregion

            #region 过滤不属于该环境的AP
            List<APScaneTimePair> avalibleScanedAPList = new List<APScaneTimePair>();
            foreach (APScaneTimePair p in scanedAPList)
            {
                if (APManager.Instance.APSetsDic[environmentId].APDic.ContainsKey(p.apMac))
                {
                    avalibleScanedAPList.Add(p);
                }
            }
            #endregion
            
            #region 调试输出
            if (debugEnable == true)
            {
                string str = "";
                str += "ScanAPs of The Environment:";
                foreach (APScaneTimePair p in avalibleScanedAPList)
                {
                    str += p.apMac;
                    str += "(";
                    str += APManager.Instance.APSetsDic[this.environmentId].APDic[p.apMac].name;
                    str += ")";
                    str += ",";
                }
                debuglog.WriteLine(str);
            }
            #endregion

            if (avalibleScanedAPList.Count == 0)
            {
                #region 此刻没有AP扫描到该标签
                if (lastLocPos != null && lastLocPos.Equals(SingleLocResult.UnknownPos))
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("LocResult:null");
                        debuglog.WriteLine("-------------------------------------");
                        debuglog.ExitWriteLock();
                    }
                    #endregion
                    return null;
                }
                else if (lastLocPos == null)
                {
                    #region 调试输出
                    if (debugEnable == true)
                    {
                        debuglog.WriteLine("LocResult:Unknown");
                        debuglog.WriteLine("-------------------------------------");
                        debuglog.ExitWriteLock();
                    }
                    #endregion
                    PointContract unknown = SingleLocResult.UnknownPos.Clone();
                    locResult.positionList.Add(unknown);
                    lastLocPos = SingleLocResult.UnknownPos;
                    return locResult;
                }
                else
                {
                    if ((time - historyAPScanedRec.GetLastScanedTime()).TotalSeconds > 5)
                    {
                        //标签消失
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("LocResult:Unknown(Disappear)");
                            debuglog.WriteLine("-------------------------------------");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        PointContract unknown = SingleLocResult.UnknownPos.Clone();
                        locResult.positionList.Add(unknown);
                        lastLocPos = SingleLocResult.UnknownPos;
                        return locResult;
                    }
                    else
                    {
                        //位置不变
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("LocResult:null");
                            debuglog.WriteLine("-------------------------------------");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        return null;
                    }
                }
                #endregion
            }
            else
            {
                this.historyAPScanedRec.Refresh(avalibleScanedAPList);
                List<string> currentScanedAPMacList = historyAPScanedRec.GetCurrentScanedAPMacList(time);
                #region 调试输出
                if (debugEnable == true)
                {
                    string str = "";
                    str += "Recently ScanAPs:";
                    foreach (string apMac in currentScanedAPMacList)
                    {
                        str += apMac;
                        str += "(";
                        str += APManager.Instance.APSetsDic[environmentId].APDic[apMac].name;
                        str += "),";
                    }
                    debuglog.WriteLine(str);
                }
                #endregion 
                List<int> currentScanedAPVertexIdList = new List<int>();
                foreach (string apMac in currentScanedAPMacList)
                {
                    currentScanedAPVertexIdList.Add(APManager.Instance.APSetsDic[environmentId].APDic[apMac].vertexId);
                }
                if (currentScanedAPVertexIdList.Count >= 1)
                {
                    PointContract locPos = new PointContract();
                    if (currentScanedAPVertexIdList.Count == 1)
                    {
                        locPos = VertexManager.Instance.VertexSetsDic[environmentId].GetVertexPosition(currentScanedAPVertexIdList.First());
                    }
                    else
                    {
                        GeometricaContract geometrica = VertexManager.Instance.VertexSetsDic[environmentId].GetGeometricaCenter(currentScanedAPVertexIdList, new List<int>(), lastLocPos);
                        locPos = geometrica.pos;
                    }
                    if (locPos.Equals(lastLocPos))
                    {
                        #region 调试输出
                        if (debugEnable == true)
                        {
                            debuglog.WriteLine("LocResult:null");
                            debuglog.WriteLine("-------------------------------------");
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
                            debuglog.WriteLine("LocResult:" + "(" + locPos.X.ToString() + "," + locPos.Y.ToString() + ")@" + locPos.MapId.ToString());
                            debuglog.WriteLine("-------------------------------------");
                            debuglog.ExitWriteLock();
                        }
                        #endregion
                        lastLocPos = locPos.Clone();
                        #region 计算路径
                        List<PointContract> path = VertexManager.Instance.VertexSetsDic[environmentId].GetPath(lastScanedAPVertexIdList, currentScanedAPVertexIdList);
                        lastScanedAPVertexIdList = currentScanedAPVertexIdList;
                        if (path != null)
                        {
                            locResult.positionList = path;
                        }
                        #endregion
                        locResult.positionList.Add(locPos);
                        return locResult;
                    }
                }
            }
            return locResult;
        }
    }
}
