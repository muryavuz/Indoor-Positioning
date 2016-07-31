/*  
 * 文件名：         APManager.cs
 * 文件功能描述：   管理定位算法使用的AP，1-初始化，2-AP根据所属客户端划分集合
 * 创建标识：       孙泽浩
 * 状态：           20120305
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LocContract;
using LocDatabase;
using InnerClasses;
using LogManager;
using System.IO;
using System.Data;
namespace LocAlgorithm
{
    public sealed class APManager
    {
        #region //单件模式实现
        static readonly APManager instance = new APManager();
        public static APManager Instance
        {
            get
            {
                return instance;
            }
        }
        static APManager()
        {
        }
        APManager()
        {
        }
        #endregion

        #region 属性
        public List<APContractEx> APList;

        //<EnvironmentId, Environment's APSet>
        public Dictionary<int, APSet> APSetsDic;

        #region 定位算法的debug输出字符串，实时显示在控制台
        public string DebugStrOfLocAlg = "";
        #endregion

        #endregion

        #region 方法
        /// <summary>
        /// 初始化定位算法使用的AP，根据Client不同将AP分为不同集合
        /// </summary>
        public void Init()
        {
            MySqlDAO dao = new MySqlDAO();
            try
            {
                List<int> environmentsIdList = DataBase.GetEnvironmentsIdList(dao);
                APSetsDic = new Dictionary<int, APSet>();
                foreach (int environmentId in environmentsIdList)
                {
                    if (APSetsDic != null)
                    {
                        if (!APSetsDic.ContainsKey(environmentId))
                        {
                            APSetsDic.Add(environmentId, new APSet(environmentId));
                        }
                    }
                }
                LogManager.Log.AddLogEntry(LogManager.LogEntryLevel.INFO, "APManager初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Log.AddLogEntry(LogManager.LogEntryLevel.ERROR, "APManager初始化失败："+ex.Message);
            }
            finally
            {
                dao.Close();
            }
        }
        #region 通过RSSI计算距离，已废弃
        //public double DistanceToAP(int rssi,string aPMac)
        //{
        //    if (apDic.ContainsKey(aPMac))
        //    {
        //        if (apDic[aPMac].EquationCoefA != null && apDic[aPMac].EquationCoefA != 0 && apDic[aPMac].EquationCoefB != null && apDic[aPMac].EquationCoefB != 0 && apDic[aPMac].Height!=0)
        //        {
        //            double p2pDistance = System.Math.Pow(10,(rssi - apDic[aPMac].EquationCoefA) / apDic[aPMac].EquationCoefB);
        //            if(p2pDistance<= apDic[aPMac].Height-Parameters.AVERAGE_TAG_HEIGHT)
        //            {
        //                return 0;
        //            }
        //            else 
        //            {
        //            double horizontalDistance = System.Math.Sqrt(System.Math.Pow(p2pDistance, 2) - System.Math.Pow(apDic[aPMac].Height - Parameters.AVERAGE_TAG_HEIGHT, 2));
        //            return horizontalDistance;
        //            }
        //        }
        //    }
        //    return 0;
        //}
        #endregion
        /// <summary>
        /// APMac是否属于给定的环境
        /// </summary>
        /// <param name="environmentId"></param>
        /// <param name="apMac"></param>
        /// <returns></returns>
        public bool IsAPAvalible(int environmentId, string apMac)
        {
            bool flag = false;
            try
            {
                if (this.APSetsDic.ContainsKey(environmentId) && this.APSetsDic[environmentId].APDic.ContainsKey(apMac))
                {
                    flag = true;
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "APManager.IsAPAvalible()执行错误：" + ex.Message);
            }
            return flag;
        }

        /// <summary>
        /// 根据APMac获取AP的名字，需要指定环境Id
        /// </summary>
        /// <param name="environemntId"></param>
        /// <param name="apMac"></param>
        /// <returns></returns>
        public string GetAPName(int environemntId, string apMac)
        {
            string name = "";
            try
            {
                if (IsAPAvalible(environemntId, apMac))
                {
                    name = this.APSetsDic[environemntId].APDic[apMac].name;
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "APManager.GetAPName()执行错误：" + ex.Message);
            }
            return name;
        }

        public string GetDebugStrOfLocAlg()
        {
            string str = DebugStrOfLocAlg;
            DebugStrOfLocAlg = "";
            return str;
        }
        #endregion
    }

    //AP集合，区分不同客户端，属于同一个客户端的AP放在一个集合内
    public class APSet
    {
        #region 属性
        public int EnvironmentId;
        public Dictionary<string, APContractEx> APDic;
        #endregion 

        #region 构造器
        public APSet(int e)
        {
            this.EnvironmentId = e;
            this.APDic = new Dictionary<string, APContractEx>();
            MySqlDAO dao = new MySqlDAO();
            List<APContractEx> apList = DataBase.GetAllAPsEx(dao, EnvironmentId);
            dao.Close();
            foreach (APContractEx ap in apList)
            {
                if (!APDic.ContainsKey(ap.mac))
                {
                    APDic.Add(ap.mac, ap);
                }
                else
                {
                    APDic[ap.mac] = ap;
                }
            }
            dao.Close();

            //fileName = AppDomain.CurrentDomain.SetupInformation.ApplicationBase+"TestAutoLearningThreshold" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
            //streamWriter = new StreamWriter(fileName, true);
        }
        #endregion

        #region 方法
        public void OnLine(string apMac)
        {
            if (APDic.ContainsKey(apMac))
            {
                APDic[apMac].status = DevStatus.ONLINE;
            }
        }
        public void OffLine(string apMac)
        {
            if (APDic.ContainsKey(apMac))
            {
                APDic[apMac].status = DevStatus.OFFLINE;
            }
        }
        public DevStatus GetAPStatus(string apMac)
        {
            if (APDic.ContainsKey(apMac))
            {
                return APDic[apMac].status;
            }
            else
            {
                return DevStatus.OFFLINE;
            }
        }

        #region 自学习阈值测试
        //private string testAPMac = "70:65:82:19:99:02";
        //private string fileName;
        //private StreamWriter streamWriter;
        //public void AutoLearningThresholdTest(List<APRssiPair> apRssiPairList)
        //{
        //    bool ContainsTestAPMacFlag = false;
        //    int TestAPRssi = int.MinValue;
        //    foreach (APRssiPair p in apRssiPairList)
        //    {
        //        if (p.APMac == testAPMac)
        //        {
        //            ContainsTestAPMacFlag = true;
        //            TestAPRssi = p.Rssi;
        //            break;
        //        }
        //    }
        //    if (ContainsTestAPMacFlag && apRssiPairList.Count>=2)
        //    {
        //        apRssiPairList.Sort(new APRssiPairComparer());
        //        if (apRssiPairList.First().APMac != testAPMac)
        //        {
        //            lock (typeof(StreamWriter))
        //            {
        //                try
        //                {
        //                    streamWriter.WriteLine(TestAPRssi.ToString() + "," + apRssiPairList[0].Rssi.ToString());
        //                    streamWriter.Flush();
        //                }
        //                catch (Exception ex)
        //                {
        //                    Log.AddLogEntry(LogEntryLevel.ERROR, "写入DebugLog失败，异常原因：" + ex.Message);
        //                    Exception e = new Exception("写入DebugLog失败，异常原因：" + ex.Message);
        //                    throw e;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            try
        //            {
        //                streamWriter.WriteLine(TestAPRssi.ToString() + "," + apRssiPairList[1].Rssi.ToString());
        //                streamWriter.Flush();
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.AddLogEntry(LogEntryLevel.ERROR, "写入DebugLog失败，异常原因：" + ex.Message);
        //                Exception e = new Exception("写入DebugLog失败，异常原因：" + ex.Message);
        //                throw e;
        //            }
        //        }
        //        foreach (APRssiPair p in apRssiPairList)
        //        {
        //            if (p.APMac != testAPMac)
        //            {
        //                lock (typeof(StreamWriter))
        //                {
        //                    try
        //                    {
        //                        streamWriter.WriteLine(TestAPRssi.ToString()+","+p.Rssi.ToString());
        //                        streamWriter.Flush();
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        Log.AddLogEntry(LogEntryLevel.ERROR, "写入DebugLog失败，异常原因：" + ex.Message);
        //                        Exception e = new Exception("写入DebugLog失败，异常原因：" + ex.Message);
        //                        throw e;
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
        #endregion
        #endregion
    }
}
