using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SamplingManager
{
    public class SamplingStaticsInfo
    {
        int count;
        int meanRssi;
        int modeRssi;
        int medianRssi;
        int minRssi;
        int maxRssi;
        int Q1Rssi;
        int Q3Rssi;
        double standardDeviation;
        public SamplingStaticsInfo(List<int> rssiList)
        {
            #region 统计总数
            this.count = rssiList.Count;
            #endregion

            #region 计算平均值
            this.meanRssi = (int)rssiList.Average();
            #endregion

            #region 计算众数
            Dictionary<int, int> rssiCountDic = new Dictionary<int, int>();
            foreach (int rssi in rssiList)
            {
                if (!rssiCountDic.ContainsKey(rssi))
                {
                    rssiCountDic.Add(rssi, 1);
                }
                else
                {
                    rssiCountDic[rssi]++;
                }
            }
            int maxCountRssi = -100;
            int maxCount = 0;
            foreach (int rssi in rssiCountDic.Keys)
            {
                if (rssiCountDic[rssi] > maxCount)
                {
                    maxCount = rssiCountDic[rssi];
                    maxCountRssi = rssi;
                }
            }
            this.modeRssi = maxCountRssi;
            #endregion

            #region 计算中位数
            rssiList.Sort();
            int medianIndex = (int)((double)rssiList.Count / 2);
            this.medianRssi = rssiList.ElementAt(medianIndex);
            #endregion

            #region 最小值
            this.minRssi = rssiList.Min();
            #endregion

            #region 最大值
            this.maxRssi = rssiList.Max();
            #endregion

            #region 下四分位数Q1
            int Q1Index = (int)((double)rssiList.Count / 4);
            this.Q1Rssi = rssiList.ElementAt(Q1Index);
            #endregion

            #region 上四分位数Q3
            int Q3Index = (int)((double)rssiList.Count * 3 / 4);
            this.Q3Rssi = rssiList.ElementAt(Q3Index);
            #endregion

            #region 计算均方差
            double deviationSquareSum = 0;
            foreach (int rssi in rssiList)
            {
                deviationSquareSum += System.Math.Pow((rssi - meanRssi), 2);
            }
            this.standardDeviation = System.Math.Sqrt(deviationSquareSum / rssiList.Count);
            #endregion
           
        }
        public SamplingStaticsInfo()
        {
        }
    }
}
