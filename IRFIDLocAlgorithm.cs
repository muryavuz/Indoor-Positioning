/*  
 * 文件名：         IRFIDLocAlgorithm.cs.cs
 * 文件功能描述：   RFID定位算法接口，确定接口便于修改算法或者使用不同版本的算法
 * 创建标识：       孙泽浩  
 * 状态：           
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LocContract;

namespace LocAlgorithm
{
    public interface IRFIDLocAlgorithm
    {
        string GetLastLocAPMac();

        /// <summary>
        /// RFID定位算法，输入为扫描到的AP，输出为定位结果
        /// </summary>
        /// <param name="scanedAPDic"></param>
        /// <param name="intialTime"></param>
        /// <param name="refreshTime"></param>
        /// <returns></returns>
        SingleTagLocResultContract Loc(Dictionary<string,DateTime> scanedAPDic, DateTime intialTime, DateTime refreshTime);
    }
}