/*  
 * 文件名：         LocAlgDemo.cs
 * 文件功能描述：   Demo定位算法，测试用没有实际环境也可以提供定位结果，可以检测数据流是否流通
 * 创建标识：       孙泽浩 20120914     
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
    public class LocAlgDemo
    {
        #region 属性
        private List<int> lastClosestVertexIdList;
        private int lastLocVertexId;
        private int counter;
        private Random rd;
        //标识属于哪个客户端，定位只会使用该客户端的资源(AP,路径,顶点,地图)
        public int environmentId;
        #endregion

        #region 构造器
        /// <summary>
        /// 构造函数，需要传入ClientId，用以区分该算法实例在计算时可以使用的资源(AP,路径,顶点,地图)
        /// </summary>
        /// <param name="clientId"></param>
        public LocAlgDemo(int e)
        {
            this.rd = new Random(DateTime.Now.GetHashCode());
            this.lastLocVertexId = -1;
            this.lastClosestVertexIdList = new List<int>();
            this.counter = 0;
            this.environmentId = e;
        }
        #endregion

        static public void Init()
        {
            //Parameters.Load();
            APManager.Instance.Init();
            VertexManager.Instance.Init();
        }

        public SingleLocResult Loc(DateTime currentTime)
        {
            SingleLocResult locResult = new SingleLocResult();
            locResult.time = currentTime;

            #region 随机点定位
            int locVertexId = -1;
            if (lastLocVertexId == -1 || counter == 0)
            {
                int n = APManager.Instance.APSetsDic[environmentId].APDic.Count;
                locVertexId = APManager.Instance.APSetsDic[environmentId].APDic.ElementAt(rd.Next(0, n - 1)).Value.vertexId;
                counter = rd.Next(4, 8);
            }
            else
            {
                locVertexId = lastLocVertexId;
            }
            counter--;
           
            #endregion
            #region 路径计算部分
            if (locVertexId != -1)
            {
                //List<int> currentClosestVertexIdList = PathManager.Instance.GetClosestVertexIdList(LocResultVertexId);
                List<int> currentClosestVertexIdList = new List<int>();
                currentClosestVertexIdList.Add(locVertexId);
                List<PointContract> posList = VertexManager.Instance.VertexSetsDic[environmentId].GetPath(lastClosestVertexIdList, currentClosestVertexIdList);
                lastClosestVertexIdList = currentClosestVertexIdList;
                if (posList != null)
                {
                    PointContract locPos = VertexManager.Instance.VertexSetsDic[environmentId].GetVertexPosition(locVertexId);
                    posList.Add(locPos);
                }
                else
                {
                    posList = new List<PointContract>();
                    PointContract locPos = VertexManager.Instance.VertexSetsDic[environmentId].GetVertexPosition(locVertexId);
                    posList.Add(locPos);
                }
                lastLocVertexId = locVertexId;
                locResult.positionList = posList;
                return locResult;
            }
            else
            {
                locResult.positionList = new List<PointContract>();
                PointContract unknownPos = SingleLocResult.UnknownPos.Clone();
                locResult.positionList.Add(unknownPos);
                return locResult;
            }
            #endregion
        }
    }
}
