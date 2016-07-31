/*  
 * 文件名：         Parameters.cs
 * 文件功能描述：   参数
 * 创建标识：       易飞滔 20110721    
 * 状态：           20120301 分模块
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Configuration;
using System.Collections;
using LogManager;
using LocDatabase;
using System.Data;
using InnerClasses.Common;

namespace LocAlgorithm
{
    public static class Parameters
    {
        #region LocAlgorithm运行时参数

        //定位到AP算法中，只有当某次输入的APRSSI序列中最大的RSSI值大于CHANGE_POSITION_RSSI时才会改变位置
        public static int CHANGE_POSITION_RSSI = -45;

        //广度优先搜索路径时最大的搜索步数，当目标顶点和原始顶点间距离超过BFS_MAX_LAYER时，不返回路径，直接跳到目标顶点
        public static int BFS_MAX_LAYER = 6;

        //
        public static int HISTORY_APRSSILIST_COUNT = 10;

        public static int HISTORY_VALID_INTERVAL = 10;

        //标签高度
        public static double AVERAGE_TAG_HEIGHT = 1.5;

        #endregion

        //用于表示没有信号或者信号强度不可信或者信号强度非常弱
        public static int COMPARABLELESS_SIGNAL = -150;

        //RSSI平滑窗口的增长步长
        public static int SMOOTH_RSSI_DVALUE_INTERVAL = 15;

        //位置改变时最大和第二大的RSSI需要满足的差值阈值
        public static int LOC_DVALUE_OF_1ST_AND_2ND = 12;
        public static bool ISDEBUG = false;
        public static string DEBUG_TAG = "";
        public static void Load()
        {
            try
            {
                IDictionary parameters = (IDictionary)ConfigurationManager.GetSection("LocAlgorithm");
                #region LocAlgorithm运行时参数
                MySqlDAO dao = new MySqlDAO();
                DataSet ds = new DataSet();
                ParametersDao parameter = new ParametersDao();
                ds = parameter.GetParameters("1", dao);
                dao.Close();
                if (CommonMethod.DataSetIsExist(ds))
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        if (dr["ParameterName"] != null)
                        {
                            switch (dr["ParameterName"].ToString().Trim())
                            {
                                case "CHANGE_POSITION_RSSI":
                                    CHANGE_POSITION_RSSI = int.Parse(dr["ParameterValue"].ToString());
                                    break;
                                case "TAG_AVERAGE_HEIGHT":
                                    AVERAGE_TAG_HEIGHT = int.Parse(dr["ParameterValue"].ToString());
                                    break;
                            }
                        }
                    }
                }
                BFS_MAX_LAYER = int.Parse(ConfigurationManager.AppSettings["BFS_MAX_LAYER"]);
                ISDEBUG = bool.Parse(ConfigurationManager.AppSettings["isDebug"]);
                DEBUG_TAG = ConfigurationManager.AppSettings["DEBUG_TAG"];
                #endregion
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.WARNING, "FormLocHosting加载参数失败，将使用默认参数,详细信息:" + ex.Message);
            }
        }
    }
}
