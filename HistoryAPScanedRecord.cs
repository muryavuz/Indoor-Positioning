using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LogManager;
namespace LocAlgorithm
{
    public class HistoryAPScanedRecord
    {
        #region 属性
        public Dictionary<string, LinkedList<DateTime>> historyAPScanDic;
        #endregion 

        #region 构造器
        public HistoryAPScanedRecord()
        {
            this.historyAPScanDic = new Dictionary<string, LinkedList<DateTime>>();
        }
        #endregion

        #region 方法
        public void Refresh(List<APScaneTimePair> newAPScanePairList)
        {
            try
            {
                foreach (APScaneTimePair p in newAPScanePairList)
                {
                    if (!this.historyAPScanDic.ContainsKey(p.apMac))
                    {
                        this.historyAPScanDic.Add(p.apMac, new LinkedList<DateTime>());
                    }
                    if (historyAPScanDic[p.apMac].Count > 10)
                    {
                        this.historyAPScanDic[p.apMac].RemoveFirst();
                    }
                    this.historyAPScanDic[p.apMac].AddLast(p.scaneTime);
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPScanedRecord.Refresh()执行错误：" + ex.Message);
            }
        }

        public List<string> GetCurrentScanedAPMacList(DateTime currentTime)
        {
            List<string> apMacList = new List<string>();
            try
            {
                foreach (string apMac in historyAPScanDic.Keys)
                {
                    if (historyAPScanDic[apMac].Count > 0 && (currentTime - historyAPScanDic[apMac].Last.Value).TotalSeconds <= InnerClasses.Parameters.RFID_AVALIBE_THRESHOLD)
                    {
                        apMacList.Add(apMac);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPScanedRecord.GetCurrentScanedAPMacList()执行错误：" + ex.Message);
            }
            return apMacList;
        }

        public DateTime GetLastScanedTime()
        {
            DateTime lastScanedTime = DateTime.MinValue;
            try
            {
                foreach (string apMac in historyAPScanDic.Keys)
                {
                    if (historyAPScanDic[apMac].Count > 0 && (historyAPScanDic[apMac].Last.Value > lastScanedTime))
                    {
                        lastScanedTime = historyAPScanDic[apMac].Last.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "HistoryAPScanedRecord.GetLastScanedTime()执行错误：" + ex.Message);
            }
            return lastScanedTime;
        }
        #endregion
    }
}
