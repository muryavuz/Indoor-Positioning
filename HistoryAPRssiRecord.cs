/*  
 * 文件名：         HistoryAPRssiRecord.cs
 * 文件功能描述：   标签的信号缓存和处理，1-缓存一段时间内的信号，2-处理（过滤或者补全）信号
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
    public class HistoryAPRssiRecord
    {
        #region 属性
        public Dictionary<string, LinkedList<TimeRssi>> historyAPRssiPairsDic;
        private Dictionary<string, Smoother> apRssiSmootherDic;
        #endregion 

        #region 构造器
        public HistoryAPRssiRecord()
        {
            historyAPRssiPairsDic = new Dictionary<string, LinkedList<TimeRssi>>();
            apRssiSmootherDic = new Dictionary<string, Smoother>();
        }
        #endregion

        #region 方法
        /// <summary>
        /// 刷新缓存，将新的信号处理后保存至缓存中
        /// </summary>
        /// <param name="apRssiPairList"></param>
        public void Refresh(List<APRssiPair> apRssiPairList)
        {
            try
            {
                //将APRssiPairList存入历史信息，用于定位算法
                foreach (APRssiPair apRssiPair in apRssiPairList)
                {
                    if (!historyAPRssiPairsDic.ContainsKey(apRssiPair.APMac))
                    {
                        historyAPRssiPairsDic.Add(apRssiPair.APMac, new LinkedList<TimeRssi>());
                        apRssiSmootherDic.Add(apRssiPair.APMac, new Smoother());
                    }
                    while (historyAPRssiPairsDic[apRssiPair.APMac].Count >= Parameters.HISTORY_APRSSILIST_COUNT)
                    {
                        historyAPRssiPairsDic[apRssiPair.APMac].RemoveFirst();
                    }
                    if (historyAPRssiPairsDic[apRssiPair.APMac].Count >= 1)
                    {
                        int lastRssi = historyAPRssiPairsDic[apRssiPair.APMac].Last.Value.Rssi;
                        int smoothRssi = 0;
                        if (lastRssi >= apRssiPair.Rssi)
                        {
                            smoothRssi = (int)(lastRssi * 0.6 + apRssiPair.Rssi * 0.4);

                        }
                        else
                        {
                            smoothRssi = (int)(lastRssi * 0.4 + apRssiPair.Rssi * 0.6);
                        }
                        historyAPRssiPairsDic[apRssiPair.APMac].AddLast(new TimeRssi
                        {
                            Rssi = smoothRssi,
                            Time = apRssiPair.Time,
                        });
                    }
                    else
                    {
                        historyAPRssiPairsDic[apRssiPair.APMac].AddLast(new TimeRssi
                            {
                                Rssi = apRssiPair.Rssi,
                                Time = apRssiPair.Time,
                            });
                    }
                    //RSSI差值平滑
                    //int lastValidRssi = GetLastValidRssiByAPMac(apRssiPair.APMac, DateTime.Now);
                    //int timeInterval = 0;
                    //if (historyAPRssiPairsDic[apRssiPair.APMac].Count > 0)
                    //{
                    //    timeInterval = (int)(DateTime.Now - historyAPRssiPairsDic[apRssiPair.APMac].Last.Value.Time).TotalSeconds;
                    //}
                    //else
                    //{
                    //    timeInterval = int.MaxValue;
                    //}
                    //if (Math.Abs(lastValidRssi - apRssiPair.Rssi) <= Parameters.SMOOTH_RSSI_DVALUE_INTERVAL + apRssiSmootherDic[apRssiPair.APMac].SmoothDvalueIntervalDecrease(timeInterval) || lastValidRssi == Parameters.COMPARABLELESS_SIGNAL)
                    //{
                    //    historyAPRssiPairsDic[apRssiPair.APMac].AddLast(new TimeRssi
                    //    {
                    //        Rssi = apRssiPair.Rssi,
                    //        Time = apRssiPair.Time,
                    //    });
                    //    apRssiSmootherDic[apRssiPair.APMac].Clear();
                    //}
                    //else
                    //{
                    //    apRssiSmootherDic[apRssiPair.APMac].NewHip();
                    //}
                }
            }
            catch (Exception ex)
            {
                #region 参数转字符串
                //string parametersStr = "apRssiPairList:";
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
                //        parametersStr += "),";
                //    }
                //    parametersStr += "}";
                //}
                #endregion
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.Refresh()执行异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 获取缓存中该AP，指定time之前一段时间(HISTORY_VALID_INTERVAL)的信号
        /// </summary>
        /// <param name="aPMac"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public List<int> GetAllValidRssisByAPMac(string aPMac, DateTime time)
        {
            List<int> rssiList = new List<int>();
            try
            {
                foreach (TimeRssi timeRssiPair in historyAPRssiPairsDic[aPMac])
                {
                    if ((time - timeRssiPair.Time).TotalSeconds < Parameters.HISTORY_VALID_INTERVAL)
                        rssiList.Add(timeRssiPair.Rssi);
                }
            }
            catch (Exception ex)
            {
                //string parametersStr = "";
                //parametersStr += "aPMac:" + aPMac;
                //parametersStr += ",time:" + time.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.GetAllValidRssiByAPMac()执行异常：" + ex.Message);
            }
            return rssiList;
        }

        public List<APRssiPair> GetCurrentScanRssiPairList(DateTime time)
        {
            List<APRssiPair> apRssiPairList = new List<APRssiPair>();
            try
            {
                foreach (string apMac in this.historyAPRssiPairsDic.Keys)
                {
                    if (historyAPRssiPairsDic[apMac].Last != null && (time - historyAPRssiPairsDic[apMac].Last.Value.Time).TotalSeconds <= Parameters.HISTORY_VALID_INTERVAL)
                    {
                        apRssiPairList.Add(new APRssiPair
                        {
                            APMac = apMac,
                            Rssi = historyAPRssiPairsDic[apMac].Last.Value.Rssi,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.GetCurrentScanRssiPairList()执行异常：" + ex.Message);
            }
            return apRssiPairList;
        }

        /// <summary>
        /// 获取缓存中该AP，指定time之前一段时间(HISTORY_VALID_INTERVAL)中最新的RSSI，如果时间段内没有信号则返回(NOSIGNAL)
        /// </summary>
        /// <param name="aPMac"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public int GetLastValidRssiByAPMac(string aPMac,DateTime time)
        {
            try
            {
                List<int> rssiList = GetAllValidRssisByAPMac(aPMac, time);
                if (rssiList.Count > 0)
                    return rssiList.Last();
                else
                    return Parameters.COMPARABLELESS_SIGNAL;
            }
            catch (Exception ex)
            {
                //string parametersStr = "";
                //parametersStr += "aPMac:" + aPMac;
                //parametersStr += ",time:" + time.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.GetLastValidRssiByAPMac()执行异常：" + ex.Message);
                return Parameters.COMPARABLELESS_SIGNAL;
            }
        }

        /// <summary>
        /// 获取缓存中该AP，指定time之前一段时间(HISTORY_VALID_INTERVAL)中最大的RSSI，如果时间段内没有信号则返回(NOSIGNAL)
        /// </summary>
        /// <param name="aPMac"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public int GetBigestValidRssiByAPMac(string aPMac, DateTime time)
        {
            try
            {
                List<int> rssiList = GetAllValidRssisByAPMac(aPMac, time);
                if (rssiList.Count > 0)
                    return rssiList.Max();
                else
                    return Parameters.COMPARABLELESS_SIGNAL;
            }
            catch (Exception ex)
            {
                //string parametersStr = "";
                //parametersStr += "aPMac:" + aPMac;
                //parametersStr += ",time:" + time.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.GetBigestValidByAPMac()执行异常：" + ex.Message);
                return Parameters.COMPARABLELESS_SIGNAL;
            }
        }

        /// <summary>
        /// 获取缓存中该AP，指定time之前一段时间(HISTORY_VALID_INTERVAL)内所有信号从小到大排序后黄金分割位的RSSI，如果时间段内没有信号则返回(NOSIGNAL)
        /// </summary>
        /// <param name="aPMac"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        internal int GetFibonacciValidRssiByAPMac(string aPMac,DateTime time)
        {
            try
            {
                List<int> rssiList = GetAllValidRssisByAPMac(aPMac, time);
                if (rssiList.Count > 0)
                {
                    rssiList.Sort();
                    int count = rssiList.Count;
                    if (count == 1)
                        return rssiList.First();
                    int reliableIndex = (int)System.Math.Round((double)count * 0.618);
                    return rssiList.ElementAt(reliableIndex - 1);
                }
                else
                    return Parameters.COMPARABLELESS_SIGNAL;
            }
            catch (Exception ex)
            {
                //string parametersStr = "";
                //parametersStr += "aPMac:" + aPMac;
                //parametersStr += ",time:" + time.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.GetFibonacciValidRssiByAPMac()执行异常：" + ex.Message);
                return Parameters.COMPARABLELESS_SIGNAL;
            }
        }

        /// <summary>
        /// 获取缓存中所有AP指定时间之前，信号的最大值和对应AP的Mac
        /// </summary>
        /// <param name="time"></param>
        /// <param name="maxRssi"></param>
        /// <returns></returns>
        public string GetMaxRssiAPMacWithRssi(DateTime time, out int maxRssi)
        {
            string maxRssiAPMac = null;
            maxRssi = Parameters.COMPARABLELESS_SIGNAL;
            try
            {
                foreach (string apMac in this.historyAPRssiPairsDic.Keys)
                {
                    int rssi = GetLastValidRssiByAPMac(apMac, time);
                    if (rssi > maxRssi)
                    {
                        maxRssi = rssi;
                        maxRssiAPMac = apMac;
                    }
                }
                return maxRssiAPMac;
            }
            catch (Exception ex)
            {
                //string parametersStr = "";
                //parametersStr += "time:" + time.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.GetMaxRssiAPMacWithRssi()执行异常：" + ex.Message);
                return maxRssiAPMac;
            }
        }

        /// <summary>
        /// 获取缓存中所有AP在指定时间之前最近信号强度的最大值和第二大值的差值
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public int GetDValueOf1thAnd2ndMaxRssi(DateTime time)
        {
            try
            {
                List<int> rssiList = new List<int>();
                foreach (string apMac in this.historyAPRssiPairsDic.Keys)
                {
                    int rssi = GetLastValidRssiByAPMac(apMac, time);
                    rssiList.Add(rssi);
                }
                if (rssiList.Count > 0)
                {
                    rssiList.Sort();
                    if (rssiList.Count == 1)
                    {
                        return (rssiList.First() - Parameters.COMPARABLELESS_SIGNAL);
                    }
                    else
                    {
                        return (rssiList[rssiList.Count - 1] - rssiList[rssiList.Count - 2]);
                    }
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                //string parametersStr = "";
                //parametersStr += "time:" + time.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPRssiRecord.GedDValueOf1stAnd2ndMaxRssi()执行异常：" + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// 获取该AP在指定时间之前RSSI变化的趋势，待改进，暂时不用
        /// </summary>
        /// <param name="aPMac"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public int GetTrend(string aPMac,DateTime time)
        {
            List<int> rssiList = new List<int>();
            foreach (TimeRssi timeRssiPair in historyAPRssiPairsDic[aPMac])
            {
                if ((time - timeRssiPair.Time).TotalSeconds < 5)
                    rssiList.Add(timeRssiPair.Rssi);
            }
            int count = rssiList.Count;
            if (count == 0)
            {
                return 0;
            }
            int pre = rssiList.ElementAt(0);
            int sum = 0;
            for (int i = 1; i < count; i++)
            {
                sum += rssiList.ElementAt(i) - pre;
                pre = rssiList.ElementAt(i);
            }
            if (sum > count)
            {
                return 1;
            }
            else if (sum + count < 0)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        #endregion
        public override string ToString()
        {
            //将所有内部变量转换成字符串，在异常发生时输出内部变量的当时值
            string str = "";
            str += "historyAPRssiPairsDic:";
            if (historyAPRssiPairsDic == null || historyAPRssiPairsDic.Count <= 0)
            {
                str += "null";
            }
            else
            {
                str += "{";
                foreach (string apmac in historyAPRssiPairsDic.Keys)
                {
                    str += "(";
                    str += apmac + ",";
                    foreach (TimeRssi tr in historyAPRssiPairsDic[apmac])
                    {
                        str += "<";
                        str += tr.Rssi.ToString();
                        str += "@" + tr.Time.ToString();
                        str += ">";
                    }
                    str += ")";
                }
                str += "}";
            }
            str += ",HISTORY_APRSSILIST_COUNT:" + Parameters.HISTORY_APRSSILIST_COUNT.ToString();
            str += ",SMOOTH_RSSI_DVALUE_INTERVAL:" + Parameters.SMOOTH_RSSI_DVALUE_INTERVAL.ToString();
            str += ",HISTORY_VALID_INTERVAL:" + Parameters.HISTORY_VALID_INTERVAL.ToString();
            str += ",COMPARABLELESS_SIGNAL:" + Parameters.COMPARABLELESS_SIGNAL.ToString();
            return str;
        }
    }

    //信号平滑器
    class Smoother
    {
        public int RssiHipCout;
        public int SmoothDvalueIntervalDecreaseUnit;
        public Smoother()
        {
            RssiHipCout = 0;
            SmoothDvalueIntervalDecreaseUnit = 5;
        }
        public int SmoothDvalueIntervalDecrease(int timeInterval)
        {
            return RssiHipCout * SmoothDvalueIntervalDecreaseUnit + timeInterval / 2;
        }
        public void Clear()
        {
            RssiHipCout = 0;
        }
        public void NewHip()
        {
            RssiHipCout++;
        }
    }
}
