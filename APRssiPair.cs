/*  
 * 文件名：         APRssiPair.cs
 * 文件功能描述：   
 * 创建标识：       徐晗曦 20110817     
 * 状态：           20120301    代码转移 易飞滔
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LocAlgorithm
{
    public class APRssiPair
    {

        #region 属性
        public string APMac;
        public int Rssi;
        public DateTime Time;
        #endregion 

        #region 构造器
        public APRssiPair(string createMac, int createRssi, DateTime createTime)
        {
            this.APMac = createMac;
            this.Rssi = createRssi;
            this.Time = createTime;
        }

        public APRssiPair()
        {
        }
        #endregion 

        #region 方法
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(APMac);

            sb.Append("\t");

            sb.Append(Rssi.ToString());

            return sb.ToString();
        }


        public APRssiPair Clone()
        {
            APRssiPair newCopy = new APRssiPair();
            newCopy.APMac = this.APMac;
            newCopy.Rssi = this.Rssi;
            newCopy.Time = this.Time;
            return newCopy;
        }
        #endregion
    }
}
