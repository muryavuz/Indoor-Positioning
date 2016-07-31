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
    public class RFIDLocAlgV2
    {
        private HistoryAPScanedRecord historyAPScanedRec;
        private List<string> lastScanedAPMacList;
        private List<int> lastScanedAPVertexIdList;
        private List<int> currentClosestVertexIdList;
        private List<int> lastClosestVertexIdList;
        private int environmentId;
        private PointContract lastLocPos;
        private bool IsUnknown;
        private Dictionary<int, VertexContractEx> vertexDic;
        public RFIDLocAlgV2(int e)
        {
            this.historyAPScanedRec = new HistoryAPScanedRecord();
            this.environmentId = e;
            this.lastLocPos = null;
            this.lastScanedAPVertexIdList = new List<int>();
            this.currentClosestVertexIdList = new List<int>();
            this.lastClosestVertexIdList = new List<int>();
            if (VertexManager.Instance.VertexSetsDic.ContainsKey(environmentId))
            {
                this.vertexDic = VertexManager.Instance.VertexSetsDic[environmentId].VertexDic;
            }
        }

        public SingleLocResult Loc(List<APScaneTimePair> scanedAPList, DateTime time, bool debugEnable)
        {
            SingleLocResult locResult = new SingleLocResult();
            locResult.time = time;
            locResult.positionList = new List<PointContract>();

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

            if (avalibleScanedAPList.Count == 0)
            {
                #region 此刻没有AP扫描到该标签
                if (lastLocPos != null && lastLocPos.Equals(SingleLocResult.UnknownPos))
                {
                    return null;
                }
                else if (lastLocPos == null)
                {
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
                        PointContract unknown = SingleLocResult.UnknownPos.Clone();
                        locResult.positionList.Add(unknown);
                        lastLocPos = SingleLocResult.UnknownPos;
                        return locResult;
                    }
                    else
                    {
                        //位置不变
                        return null;
                    }
                }
                #endregion
            }
            else
            {
                this.historyAPScanedRec.Refresh(avalibleScanedAPList);
                List<string> currentScanedAPMacList = historyAPScanedRec.GetCurrentScanedAPMacList(time);
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
                        currentClosestVertexIdList = new List<int>();
                        currentClosestVertexIdList.Add(currentScanedAPVertexIdList.First());
                    }
                    else
                    {
                        GeometricaContract geometrica = VertexManager.Instance.VertexSetsDic[environmentId].GetGeometricaCenter(currentScanedAPVertexIdList,lastClosestVertexIdList, lastLocPos);
                        locPos = geometrica.pos;
                        currentClosestVertexIdList = geometrica.closestVertexIdList;
                    }
                    if (locPos.Equals(lastLocPos))
                    {
                        return null;
                    }
                    else
                    {
                        lastLocPos = locPos.Clone();
                        #region 计算路径
                        List<PointContract> path = VertexManager.Instance.VertexSetsDic[environmentId].GetPath(lastClosestVertexIdList, currentClosestVertexIdList);
                        lastClosestVertexIdList = currentClosestVertexIdList;
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
