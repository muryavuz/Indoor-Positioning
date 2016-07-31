/*  
 * 文件名：         ILocAlgorithm.cs
 * 文件功能描述：   定位算法接口，确定接口便于修改算法或者使用不同版本的算法
 * 创建标识：       孙泽浩  
 * 状态：           
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InnerClasses;
using LocContract;

namespace LocAlgorithm
{
    public interface ILocAlgorithm
    {
        /// <summary>
        /// 定位算法，输入为实时信号，输出为定位结果
        /// 算法会填充SingleLocResult中的位置序列，时间和客户ID
        /// 如果位置相比上次没有移动，则返回null
        /// 如果定位算法无法计算出定位结果，也返回null
        /// </summary>
        /// <param name="apRssiPairList"></param>
        /// <param name="intialTime"></param>
        /// <param name="refreshTime"></param>
        /// <returns></returns>
        SingleLocResult  Loc(List<APRssiPair> apRssiPairList,DateTime time,bool debugEnable);
        void Disappear();
    }
}
