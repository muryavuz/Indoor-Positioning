/*  
 * 文件名：         LocAlgV2.cs
 * 文件功能描述：   算法第二版，主要为信号欧几里得距离比对和LCA稳定机制
 *                  快速胜出机制 和 信号同步机制 效果仍有待检验，目前快速胜出关闭，信号同步开启
 * 创建标识：       易飞滔 20111111     
 * 状态：           20120305
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
namespace LocAlgorithm
{
    public class LocAlgV2 : ILocAlgorithm
    {
        static public void Init()
        {
            Parameters.Load();
            AreaManager.Instance.Init();//先AreaManger启动然后启动APManager，因为APManager中要调用AreaManager的函数
            APManager.Instance.Init();
            
            VertexManager.Instance.Init();
            //LogManager.FormMainLog.AddLogEntry("LocAltorithm.LocAlgV2.Init()执行结束！");
        }

        public LocAlgV2(string CarrierId)
        {
            carrierId = CarrierId;
            lastPos = Constants.Unkown.Clone();
            lastClosestVertexIdList = new List<int>();
            historyCompareResult = new HistoryCompareResult();
            historyAPRssiRecord = new HistoryAPRssiRecord();
        }
        string carrierId;
        //暂存的历史比较结果
        private HistoryCompareResult historyCompareResult;
        //暂存的历史APRssi
        private  HistoryAPRssiRecord historyAPRssiRecord;
        //上次定位结果
        private PositionContract lastPos;
        //查找路径用的，上次最近邻节点列表
        private List<int> lastClosestVertexIdList;

        #region ILocAlgorithm 成员
        public SingleTagLocResultContract Loc(List<APRssiPair> apRssiPairList,DateTime intialTime,DateTime refreshTime)
        {
            //刷新历史信息
            historyAPRssiRecord.Refresh(apRssiPairList);
            #region 定位到AP算法,8.31-16:32
            //比较查找收到RSSI最大的AP
            int MaxRssi = int.MinValue;
            string MaxRssiAPMac = null;
            foreach (APRssiPair apRssiPair in apRssiPairList)
            {
                if (apRssiPair.Rssi > MaxRssi)
                {
                    MaxRssi = apRssiPair.Rssi;
                    MaxRssiAPMac = apRssiPair.APMac;
                }
            }
            historyCompareResult.Add(MaxRssiAPMac);

            //创建返回结果变量，并初始化
            SingleTagLocResultContract locResult = new SingleTagLocResultContract();
            locResult.SinglePosLocResultContractList = new List<SinglePosLocResultContract>();
            SinglePosLocResultContract pos = new SinglePosLocResultContract();
            pos.InitialTime = intialTime.ToString();
            pos.RefreshTime = refreshTime.ToString();
            pos.Position = new PositionContract();
            pos.Position.Coordinate = new PointContract();


            //if (MaxRssi >= Parameters.CHANGE_POSITION_RSSI && !lastPos.Equals(APManager.Instance.APDic[MaxRssiAPMac].Position))//说明位置有改变了
            //{
            //    if (historyCompareResult.IsRealiable(MaxRssiAPMac))
            //    {
            //        List<int> currentClosestVertexIdList = PathManager.Instance.GetClosestVertexIdList(APManager.Instance.APDic[MaxRssiAPMac].Position.Coordinate);
            //        List<PositionContract> posList = PathManager.Instance.GetPath(lastClosestVertexIdList, currentClosestVertexIdList);
            //        lastClosestVertexIdList = currentClosestVertexIdList;
            //        if (posList != null)
            //        {
            //            foreach (PositionContract p in posList)
            //            {
            //                SinglePosLocResultContract singlePosLocResult = new SinglePosLocResultContract();
            //                singlePosLocResult.InitialTime = intialTime.ToString();
            //                singlePosLocResult.RefreshTime = intialTime.ToString(); //路径点的刷新时间和起始时间都等于起始时间
            //                singlePosLocResult.Position = p;
            //                locResult.SinglePosLocResultContractList.Add(singlePosLocResult);
            //            }
            //        }
            //        lastPos = APManager.Instance.APDic[MaxRssiAPMac].Position.Clone();
            //        lastPos.Coordinate.Type = CoordinateType.DotDistance;
            //        lastPos.Coordinate.X = 
            //        lastPos.Coordinate.Y = APManager.Instance.DistanceToAP(MaxRssi, MaxRssiAPMac);
            //        pos.Position = lastPos.Clone();
            //        locResult.SinglePosLocResultContractList.Add(pos);
            //        return locResult;
            //    }
            //}
            return null;
            #endregion

            #region 测试算法
            //比较查找收到RSSI最大的AP
            //int MaxRssi = int.MinValue;
            //string MaxRssiAPMac = null;
            //foreach (APRssiPair apRssiPair in apRssiPairList)
            //{
            //    if (apRssiPair.Rssi > MaxRssi)
            //    {
            //        MaxRssi = apRssiPair.Rssi;
            //        MaxRssiAPMac = apRssiPair.APMac;
            //    }
            //}

            ////创建返回结果变量，并初始化
            //SingleTagLocResultContract locResult = new SingleTagLocResultContract();
            //locResult.SinglePosLocResultContractList = new List<SinglePosLocResultContract>();
            //SinglePosLocResultContract pos = new SinglePosLocResultContract();
            //pos.InitialTime = intialTime.ToString();
            //pos.RefreshTime = refreshTime.ToString();
            //pos.Position = new PositionContract();
            //pos.Position.Coordinate = new PointContract();


            //if (MaxRssi >= Parameters.CHANGE_POSITION_RSSI && !lastPos.Coordinate.IsEqual(APManager.Instance.APDic[MaxRssiAPMac].Coordinate))//说明位置有改变了
            //{
            //    bool Realiable = false;
            //    int _2ndMaxRssi = int.MinValue;
            //    string _2ndMaxRssiAPMac = null;
            //    foreach (string aPMac in historyAPRssiRecord.historyAPRssiPairsDic.Keys)
            //    {
            //        if (aPMac != MaxRssiAPMac)
            //        {
            //            int rssi = historyAPRssiRecord.GetLastBigestValidRssiByAPMac(aPMac, DateTime.Now);
            //            if (rssi > _2ndMaxRssi)
            //            {
            //                _2ndMaxRssi = rssi;
            //                _2ndMaxRssiAPMac = aPMac;
            //            }
            //        }
            //    }
            //    if (_2ndMaxRssiAPMac == null)
            //        _2ndMaxRssiAPMac = MaxRssiAPMac;
            //    List<int> maxRssiAPLastValidRssiList = historyAPRssiRecord.GetLastValidRssisByAPMac(MaxRssiAPMac, DateTime.Now);
            //    List<int> _2ndMaxRssiAPLastValidRssiList = historyAPRssiRecord.GetLastValidRssisByAPMac(_2ndMaxRssiAPMac, DateTime.Now);
            //    if (_2ndMaxRssiAPLastValidRssiList.Count == 0)
            //    {
            //        Realiable = true;
            //    }
            //    else
            //    {
            //        double maxRssiAPMeanLastValidRssi;
            //        double _2ndMaxRssiAPMeanLastValidRssi;
            //        if (maxRssiAPLastValidRssiList.Count > 1 && maxRssiAPLastValidRssiList.Max() - maxRssiAPLastValidRssiList.Average() > 10)
            //        {
            //            maxRssiAPMeanLastValidRssi = (maxRssiAPLastValidRssiList.Sum()  - maxRssiAPLastValidRssiList.Min()) / (maxRssiAPLastValidRssiList.Count - 1);
            //        }
            //        else if (maxRssiAPLastValidRssiList.Count > 0)
            //        {
            //            maxRssiAPMeanLastValidRssi = maxRssiAPLastValidRssiList.Average();
            //        }
            //        else
            //        {
            //            maxRssiAPMeanLastValidRssi = MaxRssi;
            //        }
            //        if (_2ndMaxRssiAPLastValidRssiList.Count > 1 && _2ndMaxRssiAPLastValidRssiList.Max() - _2ndMaxRssiAPLastValidRssiList.Average() > 10)
            //        {
            //            _2ndMaxRssiAPMeanLastValidRssi = (_2ndMaxRssiAPLastValidRssiList.Sum() -  - _2ndMaxRssiAPLastValidRssiList.Min()) / (_2ndMaxRssiAPLastValidRssiList.Count - 1);
            //        }
            //        else if (_2ndMaxRssiAPLastValidRssiList.Count > 0)
            //        {
            //            _2ndMaxRssiAPMeanLastValidRssi = _2ndMaxRssiAPLastValidRssiList.Average();
            //        }
            //        else
            //        {
            //            _2ndMaxRssiAPMeanLastValidRssi = _2ndMaxRssi;
            //        }
            //        if (maxRssiAPMeanLastValidRssi - _2ndMaxRssiAPMeanLastValidRssi > 3)
            //            Realiable = true;
            //        else
            //            Realiable = false;
            //    }
            //    if (Realiable)
            //    {
            //        List<int> currentClosestVertexIdList = PathManager.Instance.GetClosestVertexIdList(APManager.Instance.APDic[MaxRssiAPMac].Coordinate);
            //        List<PositionContract> posList = PathManager.Instance.GetPath(lastClosestVertexIdList, currentClosestVertexIdList);
            //        lastClosestVertexIdList = currentClosestVertexIdList;
            //        if (posList != null)
            //        {
            //            foreach (PositionContract p in posList)
            //            {
            //                SinglePosLocResultContract singlePosLocResult = new SinglePosLocResultContract();
            //                singlePosLocResult.InitialTime = intialTime.ToString();
            //                singlePosLocResult.RefreshTime = intialTime.ToString(); //路径点的刷新时间和起始时间都等于起始时间
            //                singlePosLocResult.Position = p;
            //                locResult.SinglePosLocResultContractList.Add(singlePosLocResult);
            //            }
            //        }
            //        lastPos.Coordinate.MapId = APManager.Instance.APDic[MaxRssiAPMac].Coordinate.MapId;
            //        lastPos.Coordinate.X = APManager.Instance.APDic[MaxRssiAPMac].Coordinate.X;
            //        lastPos.Coordinate.Y = APManager.Instance.APDic[MaxRssiAPMac].Coordinate.Y;
            //        pos.Position.Coordinate.MapId = lastPos.Coordinate.MapId;
            //        pos.Position.Coordinate.X = lastPos.Coordinate.X;
            //        pos.Position.Coordinate.Y = lastPos.Coordinate.Y;
            //        locResult.SinglePosLocResultContractList.Add(pos);
            //        return locResult;
            //    }
            //}
            ////位置没有变化，返回空
            //return null;
            #endregion
        }     
        #endregion
    }
    class HistoryCompareResult
    {
        public HistoryCompareResult()
        {
            historyCompareResultAPMacList = new LinkedList<string>();
        }
        private LinkedList<string> historyCompareResultAPMacList;
        public void Add(string apMac)
        {
            while (historyCompareResultAPMacList.Count >= Parameters.HISTORY_COMPARERESULTS_COUNT)
                historyCompareResultAPMacList.RemoveFirst();
            historyCompareResultAPMacList.AddLast(apMac);
        }
        public bool IsRealiable(string currentMaxAPMac) //连续x次比较结果均为改AP说明可靠，可以定位到此AP
        {
            foreach (string apMac in historyCompareResultAPMacList)
            {
                if (!apMac.Equals(currentMaxAPMac))
                    return false;
            }
            return true;
        }
    }
    
}
