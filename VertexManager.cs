/*  
 * 文件名：         VertexManager.cs
 * 文件功能描述：   管理所有参与定位和路径计算的顶点，1-建图，2-路径计算，3-基于路径的算法优化（计划）
 * 创建标识：       孙泽浩  
 * 状态：           
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LocContract;
using LocDatabase;
using LogManager;
using InnerClasses;
namespace LocAlgorithm
{
    public class VertexManager
    {
        #region //单件模式实现
        static readonly VertexManager instance = new VertexManager();
        public static VertexManager Instance
        {
            get
            {
                return instance;
            }
        }
        static VertexManager()
        {
        }
        VertexManager()
        {
        }
        #endregion

        #region 属性
        public Dictionary<int, VertexSet> VertexSetsDic;
        #endregion

        #region 方法
        public void Init()
        {
            MySqlDAO dao = new MySqlDAO();
            List<int> EnvironmentsIdList = DataBase.GetEnvironmentsIdList(dao);
            dao.Close();
            VertexSetsDic = new Dictionary<int, VertexSet>();
            foreach (int environmentId in EnvironmentsIdList)
            {
                if (!VertexSetsDic.ContainsKey(environmentId))
                {
                    VertexSetsDic.Add(environmentId, new VertexSet(environmentId));
                }
            }
            Log.AddLogEntry(LogEntryLevel.INFO, "VertexManager初始化完成");
        }
        #endregion

    }

    public class VertexSet
    {
        #region 属性
        //定义了这个class可以做的事情，和基本的数据结构，存储了关于某个environment所有vertex的信息和连接信息。
        private int EnvironmentId;
        public Dictionary<int, VertexContractEx> VertexDic;
        public Dictionary<int, BFSInfo> VertexIdBFSInfoDic;
        private List<PathEdge> edgeList;
        public Dictionary<int, PathDictionary> QuickPathTable;     //用于快速计算路径的内存查找表，主键为起点的vertexId
        private Dictionary<int, Dictionary<int, Path>> quickCellPathDic;  //用于快速计算Cell间路径的内存查找表，主键为起点CellId，次级字典的主键是终点CellId
        #endregion

        #region 构造器
        /// <summary>
        /// 初始化，1-获取所有顶点，2-按照所属客户端划分集合，3-根据边关系建图
        /// </summary>
        /// <param name="ClientId"></param>
        public VertexSet(int e)
        {
            this.EnvironmentId = e;
            this.VertexDic = new Dictionary<int, VertexContractEx>();
            this.VertexIdBFSInfoDic = new Dictionary<int, BFSInfo>();
            //获取所有的vertex；
            MySqlDAO dao = new MySqlDAO();
            List<VertexContractEx> vertexList = DataBase.GetAllVertexesEx(dao,EnvironmentId);
          
            this.edgeList = DataBase.GetEdgeListByEnvironmentId(e, dao);
            dao.Close();

            foreach (VertexContractEx vertex in vertexList)
            {
                //填充Vertex的CellId字段
                if (CellManager.Instance.CellSetsDic.ContainsKey(this.EnvironmentId))
                {
                    vertex.cellId = CellManager.Instance.CellSetsDic[this.EnvironmentId].GetCellId(vertex.pos);
                }
                else
                {
                    vertex.cellId = -1;
                }
                //填充Vertex的CellId字段 End

                if (!VertexDic.ContainsKey(vertex.vertexId))
                {
                    VertexDic.Add(vertex.vertexId, vertex);
                }
                else
                {
                    VertexDic[vertex.vertexId] = vertex;
                }
                if (!VertexIdBFSInfoDic.ContainsKey(vertex.vertexId))
                {
                    VertexIdBFSInfoDic.Add(vertex.vertexId, new BFSInfo(vertex.vertexId));
                }
                else
                {
                    VertexIdBFSInfoDic[vertex.vertexId] = new BFSInfo(vertex.vertexId);
                }
            }

            #region 初始化路径查找表
            QuickPathTable = new Dictionary<int, PathDictionary>();
            foreach (int vertexId in VertexDic.Keys)
            {
                if (!QuickPathTable.ContainsKey(vertexId))
                {
                    QuickPathTable.Add(vertexId, new PathDictionary());
                    //在这里把所有的vertex加入到搜索的dictionary中
                }
                foreach (int endVertexId in VertexDic.Keys)
                {
                    if (endVertexId != vertexId && !QuickPathTable[vertexId].vertexIdPathDic.ContainsKey(endVertexId))
                    {
                        QuickPathTable[vertexId].vertexIdPathDic.Add(endVertexId, new List<Path>());
                        //在这里把所有的终止路径加入到dictionary中
                        QuickPathTable[vertexId].vertexIdPathDic[endVertexId] = GetAllPath(vertexId, endVertexId);
                        //在这里把所有的路径加入到dictionary中
                    }
                }
            }
            #endregion

            #region Edge分析
            //Edge分析，CellInfo.EntranceVertex初始化
            if (CellManager.Instance.CellSetsDic.ContainsKey(EnvironmentId))
            {
                Dictionary<int, CellInfo> cellInfoDic = CellManager.Instance.CellSetsDic[EnvironmentId].cellInfoDic;
                foreach (PathEdge edge in edgeList)
                {
                    if (!VertexDic.ContainsKey(edge.VertexId) || !VertexDic.ContainsKey(edge.NeighborId))
                    {
                        Log.AddLogEntry(LogEntryLevel.ERROR, "数据库Edge表存在脏数据！");
                        continue;
                    }
                    //如果一条边的两个端点属于不同的Cell，则这条边必定是跨Cell的，且这两个端点分别为其所属Cell的出入口点
                    if (VertexDic[edge.VertexId].cellId != VertexDic[edge.NeighborId].cellId)
                    {
                        if (VertexDic[edge.VertexId].cellId != -1)
                        {
                            if (cellInfoDic.ContainsKey(VertexDic[edge.VertexId].cellId))
                            {
                                cellInfoDic[VertexDic[edge.VertexId].cellId].entranceVertexIdList.Add(edge.VertexId);
                            }
                        }
                        if (VertexDic[edge.NeighborId].cellId != -1)
                        {
                            if (cellInfoDic.ContainsKey(VertexDic[edge.NeighborId].cellId))
                            {
                                cellInfoDic[VertexDic[edge.NeighborId].cellId].entranceVertexIdList.Add(edge.NeighborId);
                            }
                        }
                    }
                }
                //Path计算，CellInfo.CellPathDic初始化
                foreach (int cellId_sta in cellInfoDic.Keys)
                {
                    foreach (int cellId_end in cellInfoDic.Keys)
                    {
                        if (cellId_sta == cellId_end)
                        {
                            continue;
                        }
                        List<Path> cellPathList = new List<Path>();
                        foreach (int entranceVertex_sta in cellInfoDic[cellId_sta].entranceVertexIdList)
                        {
                            foreach (int entranceVertex_end in cellInfoDic[cellId_end].entranceVertexIdList)
                            {
                                if (QuickPathTable.ContainsKey(entranceVertex_sta))
                                {
                                    if (QuickPathTable[entranceVertex_sta].vertexIdPathDic.ContainsKey(entranceVertex_end))
                                    {
                                        List<Path> tempPathList = QuickPathTable[entranceVertex_sta].vertexIdPathDic[entranceVertex_end];
                                        if (tempPathList != null && tempPathList.Count > 0)
                                        {
                                            foreach (Path p in tempPathList)
                                            {
                                                cellPathList.Add(p);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (cellPathList != null && cellPathList.Count > 0)
                        {
                            cellInfoDic[cellId_sta].cellPathDic.Add(cellId_end, cellPathList);
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        #region 方法

        /// <summary>
        /// 获得从该定点起遍历指定深度(depth)内经过的所有顶点
        /// </summary>
        /// <param name="originVertexId"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public List<int> GetNeighborVertexIdList(int originVertexId, int depth)
        {
            try
            {
                ClearBFSInfo();
                List<int> neighborVertexIdList = new List<int>();
                //启动时遍历所有对比采样点！
                if (!VertexIdBFSInfoDic.ContainsKey(originVertexId))
                {
                    foreach (int id in VertexIdBFSInfoDic.Keys)
                    {
                        neighborVertexIdList.Add(id);
                    }
                    return neighborVertexIdList;
                }

                Queue<int> unVisitedVertexIdQueue = new Queue<int>();
                unVisitedVertexIdQueue.Enqueue(originVertexId);
                VertexIdBFSInfoDic[originVertexId].layer = 0;
                while (true)
                {
                    if (unVisitedVertexIdQueue.Count == 0 || VertexIdBFSInfoDic[unVisitedVertexIdQueue.Peek()].layer > depth)
                    {
                        break;
                    }
                    int vertexId = unVisitedVertexIdQueue.Dequeue();
                    if (VertexIdBFSInfoDic[vertexId].visited == false)
                    {
                        neighborVertexIdList.Add(vertexId);
                        VertexIdBFSInfoDic[vertexId].visited = true;
                        foreach (int neighborVertexId in VertexDic[vertexId].neighborVertexIdList)
                        {
                            if (VertexIdBFSInfoDic[neighborVertexId].visited == false)
                            {
                                VertexIdBFSInfoDic[neighborVertexId].layer = VertexIdBFSInfoDic[vertexId].layer + 1;
                                unVisitedVertexIdQueue.Enqueue(neighborVertexId);
                            }
                        }
                    }

                }
                return neighborVertexIdList;
            }
            catch (Exception ex)
            {
                string parametersStr = "";
                parametersStr += "originVertexId:" + originVertexId.ToString();
                parametersStr += ",depth:" + depth.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "VertexSet.GetNeighborVertexIdList()执行异常：" + ex.Message + "\t异常现场：" + "类变量：" + this.ToString() + "\t参数：" + parametersStr);
                return null;
            }
        }

        /// <summary>
        /// 获得指定坐标最近的顶点id，计算路径用，暂时不用，返回空列表
        /// </summary>
        /// <param name="coordinate"></param>
        /// <returns></returns>
        public List<int> GetClosestVertexIdList(PointContract coordinate)
        {
            List<int> ClosestVertexIdList = new List<int>();
            //foreach (PathEdge edge in edgeList)
            //{
            //    int VertexId1OfEdge = edge.VertexId;
            //    int VertexId2OfEdge = edge.NeighborId;
            //    if (coordinate.IsEqual(vertexIdInfoMap[VertexId1OfEdge].Coordinate))
            //    {
            //        ClosestVertexIdList.Add(VertexId1OfEdge);
            //        return ClosestVertexIdList;
            //    }
            //    if (coordinate.IsEqual(vertexIdInfoMap[VertexId2OfEdge].Coordinate))
            //    {
            //        ClosestVertexIdList.Add(VertexId2OfEdge);
            //        return ClosestVertexIdList;
            //    }
                //if (OnEdge(coordinate, edge))
                //{
                //    ClosestVertexIdList.Add(VertexId1OfEdge);
                //    ClosestVertexIdList.Add(VertexId2OfEdge);
                //    return ClosestVertexIdList;
                //}
            //}
            return ClosestVertexIdList;
        }

        /// <summary>
        /// 判断指定点坐标是否在指定的边上，计算路径用，暂不使用，返回false
        /// </summary>
        /// <param name="coordinate"></param>
        /// <param name="edge"></param>
        /// <returns></returns>
        private bool OnEdge(PointContract coordinate, PathEdge edge)
        {
            //PointContract coordinateA = vertexIdInfoMap[edge.NeighborId].Coordinate;
            //PointContract coordinateB = vertexIdInfoMap[edge.VertexId].Coordinate;
            //if (coordinate.MapId == coordinateA.MapId && coordinate.MapId == coordinateB.MapId)
            //{
            //    double x = coordinate.X; double y = coordinate.Y;
            //    double xA = coordinateA.X; double yA = coordinateA.Y;
            //    double xB = coordinateB.X; double yB = coordinateB.Y;
            //    if ((y - yA) * (xB - x) == (yB - y) * (x - xA)) //确定是否在一条直线上
            //    {
            //        if ((Math.Abs(x - xA) + Math.Abs(x - xB)) == Math.Abs(xA - xB) || (Math.Abs(y - yA) + Math.Abs(y - yB)) == Math.Abs(yA - yB))
            //            return true;
            //    }
            //}
            return false;
        }

        ///// <summary>
        ///// 路径计算，从初始顶点集合到结束顶点集合的路径(不包括初始顶点集合和结束顶点集合，仅包括路径经过的点）
        ///// 如果从初始顶点集合到结束顶点集合没有路径，则返回null
        ///// </summary>
        ///// <param name="LastPosClosestVertexList"></param>
        ///// <param name="CurrentPosClosestVertexList"></param>
        ///// <returns></returns>
        //public List<PointContract> GetPath(List<int> LastPosClosestVertexList, List<int> CurrentPosClosestVertexList)
        //{
        //    try
        //    {
        //        lock (this)
        //        {
        //            if (LastPosClosestVertexList.Count == 0 || CurrentPosClosestVertexList.Count == 0)
        //                return null;
        //            ClearBFSInfo();
        //            Queue<int> unVisitedBFSNeighborQueue = new Queue<int>(); //之前queue是全局变量，我在进入函数的时候没有清理，导致了固定的路线会出现错误！！！
        //            foreach (int vertexId in LastPosClosestVertexList)
        //            {
        //                unVisitedBFSNeighborQueue.Enqueue(vertexId);
        //                VertexIdBFSInfoDic[vertexId].layer = 1;//这些节点属于第一层，当层数到达5时还没找到，则不再查找，返回空
        //            }
        //            while (true)
        //            {
        //                if (unVisitedBFSNeighborQueue.Count == 0)
        //                {
        //                    break;
        //                }
        //                int vertexId = unVisitedBFSNeighborQueue.Dequeue();
        //                VertexIdBFSInfoDic[vertexId].visited = true;
        //                if (VertexIdBFSInfoDic[vertexId].layer >= Parameters.BFS_MAX_LAYER)
        //                {
        //                    break;
        //                }
        //                else if (CurrentPosClosestVertexList.Contains(vertexId))
        //                {
        //                    List<PointContract> posList = new List<PointContract>();
        //                    while (vertexId != 0)
        //                    {
        //                        PointContract pos = VertexDic[vertexId].pos.Clone();
        //                        posList.Insert(0, pos);
        //                        vertexId = VertexIdBFSInfoDic[vertexId].preVertexId;
        //                    }
        //                    if (LastPosClosestVertexList.Count == 1 && posList.Count > 0)
        //                        posList.RemoveAt(0);
        //                    if (CurrentPosClosestVertexList.Count == 1 && posList.Count > 0)
        //                        posList.RemoveAt(posList.Count - 1);
        //                    return posList;
        //                }
        //                else
        //                {
        //                    foreach (int neighborId in VertexDic[vertexId].neighborVertexIdList)
        //                    {
        //                        if (VertexIdBFSInfoDic[neighborId].visited == false)
        //                        {
        //                            VertexIdBFSInfoDic[neighborId].preVertexId = vertexId;
        //                            VertexIdBFSInfoDic[neighborId].layer = VertexIdBFSInfoDic[vertexId].layer + 1;
        //                            unVisitedBFSNeighborQueue.Enqueue(neighborId);
        //                        }
        //                    }
        //                }
        //            }
        //            return null;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        #region 参数转换为字符串
        //        string parametersStr = "";
        //        parametersStr += "LastPosClosestVertexes:";
        //        if(LastPosClosestVertexList==null)
        //        {
        //            parametersStr+="null";
        //        }
        //        else
        //        {
        //            parametersStr += "(";
        //            foreach (int id in LastPosClosestVertexList)
        //            {
        //                parametersStr += id.ToString() + ",";
        //            }
        //            parametersStr += ")";
        //        }
        //        parametersStr += ",CurrentPosClosestVertexes:";
        //        if (CurrentPosClosestVertexList == null)
        //        {
        //            parametersStr += "null";
        //        }
        //        else
        //        {
        //            parametersStr += "(";
        //            foreach (int id in CurrentPosClosestVertexList)
        //            {
        //                parametersStr += id.ToString() + ",";
        //            }
        //            parametersStr += ")";
        //        }
        //        #endregion
        //        Log.AddLogEntry(LogEntryLevel.ERROR, "VertexSet.GetPath()执行异常：" + ex.Message + "\t异常现场：" + "类变量：" + this.ToString() + "\t参数：" + parametersStr);
        //        return null;
        //    }
        //}

        public List<PointContract> GetPath(List<int> lastPosClosestVertexList, List<int> CurrentPosClosestVertexList)
        {
            List<PointContract> posList = new List<PointContract>();
            #region 查表获取所有路径
            List<Path> allPaths = new List<Path>();
            foreach (int staVertexId in lastPosClosestVertexList)
            {
                if (!QuickPathTable.ContainsKey(staVertexId))
                    continue;
                foreach (int endVertexId in CurrentPosClosestVertexList)
                {
                    if (endVertexId == staVertexId)
                    {
                        Path p = new Path();
                        allPaths.Add(p);
                        continue;
                    }
                    if (!QuickPathTable[staVertexId].vertexIdPathDic.ContainsKey(endVertexId))
                        continue;
                    List<Path> temp = QuickPathTable[staVertexId].vertexIdPathDic[endVertexId];
                    foreach (Path p in temp)
                    {
                        allPaths.Add(p);
                    }
                }
            }
            #endregion
            #region 查找不经过AP的路径
            List<Path> pathsNotPassAP = new List<Path>();
            foreach (Path p in allPaths)
            {
                bool passAP = false;
                for (int i = 1; i < p.pathVertexIdList.Count-1; i++)
                {
                    int vertexId = p.pathVertexIdList[i];
                    if (VertexDic[vertexId].type == VertexType.APPOINT)
                    {
                        passAP = true;
                        break;
                    }
                }
                if (passAP == false)
                {
                    pathsNotPassAP.Add(p);
                }
            }
            #endregion
            #region 查找步数最短的路径
            int minPathStepCount = int.MaxValue;
            List<Path> minPaths = new List<Path>();
            if (pathsNotPassAP.Count > 0)
            {
                foreach (Path p in pathsNotPassAP)
                {
                    if (p.pathVertexIdList.Count < minPathStepCount)
                    {
                        minPathStepCount = p.pathVertexIdList.Count;
                        minPaths.Clear();
                        minPaths.Add(p);
                    }
                    else if (p.pathVertexIdList.Count == minPathStepCount)
                    {
                        minPaths.Add(p);
                    }
                }
            }
            else
            {
                foreach (Path p in allPaths)
                {
                    if (p.pathVertexIdList.Count < minPathStepCount)
                    {
                        minPathStepCount = p.pathVertexIdList.Count;
                        minPaths.Clear();
                        minPaths.Add(p);
                    }
                    else if (p.pathVertexIdList.Count == minPathStepCount)
                    {
                        minPaths.Add(p);
                    }
                }
            }
            #endregion
            #region 返回坐标列表
            if (minPaths.Count == 0)
            {
                return posList;
            }
            else
            {
                Path p = minPaths.First();
                int i = lastPosClosestVertexList.Count > 1 ? 0 : 1;
                int c = CurrentPosClosestVertexList.Count > 1 ? p.pathVertexIdList.Count : p.pathVertexIdList.Count - 1;
                for (; i < c; i++)
                {
                    posList.Add(VertexDic[p.pathVertexIdList[i]].pos.Clone());
                }
            }
            #endregion
            return posList;
        }

        public List<PointContract> GetPath(PointContract lastLocPos, int lastCellId, PointContract curLocPos, int curCellId)
        {
            List<PointContract> path = new List<PointContract>();
            if (lastCellId == curCellId)
            {
                //CellID没有变化
                path.Add(curLocPos);
                return path;
            }
            else
            {
                //CellId发生了变化
                Dictionary<int, CellInfo> cellInfoDic = CellManager.Instance.CellSetsDic[this.EnvironmentId].cellInfoDic;
                if (cellInfoDic.ContainsKey(lastCellId))
                {
                    if (cellInfoDic[lastCellId].cellPathDic.ContainsKey(curCellId))
                    {
                        int minLength = int.MaxValue;
                        List<int> minLengthPathVertexIdList = new List<int>();
                        foreach (Path p in cellInfoDic[lastCellId].cellPathDic[curCellId])
                        {
                            int lengthSum = 0;
                            lengthSum += GetGeometricaDistance(lastLocPos, VertexDic[p.pathVertexIdList.First()].pos);
                            lengthSum += p.pathLength;
                            lengthSum += GetGeometricaDistance(VertexDic[p.pathVertexIdList.Last()].pos, curLocPos);
                            if (lengthSum < minLength)
                            {
                                minLength = lengthSum;
                                minLengthPathVertexIdList = p.pathVertexIdList;
                            }
                        }
                        
                        //得到了最小路径的顶点列表，生成返回最小路径的位置列表
                        foreach (int pathVertexId in minLengthPathVertexIdList)
                        {
                            path.Add(VertexDic[pathVertexId].pos.Clone());
                        }
                    }
                }
                path.Add(curLocPos);
                return path;
            }
        }

        public List<int> GetPathVertexIdList(List<int> lastPosClosestVertexList, List<int> currentPosClosestVertexList)
        {
            List<int> vertexIdList = new List<int>();
            #region 查表获取所有路径
            List<Path> allPaths = new List<Path>();
            foreach (int staVertexId in lastPosClosestVertexList)
            {
                if (!QuickPathTable.ContainsKey(staVertexId))
                    continue;
                foreach (int endVertexId in currentPosClosestVertexList)
                {
                    if (endVertexId == staVertexId)
                    {
                        Path p = new Path();
                        allPaths.Add(p);
                        continue;
                    }
                    if (!QuickPathTable[staVertexId].vertexIdPathDic.ContainsKey(endVertexId))
                        continue;
                    List<Path> temp = QuickPathTable[staVertexId].vertexIdPathDic[endVertexId];
                    foreach (Path p in temp)
                    {
                        allPaths.Add(p);
                    }
                }
            }
            #endregion
            #region 查找不经过AP的路径
            List<Path> pathsNotPassAP = new List<Path>();
            foreach (Path p in allPaths)
            {
                bool passAP = false;
                for (int i = 1; i < p.pathVertexIdList.Count - 1; i++)
                {
                    int vertexId = p.pathVertexIdList[i];
                    if (VertexDic[vertexId].type == VertexType.APPOINT)
                    {
                        passAP = true;
                        break;
                    }
                }
                if (passAP == false)
                {
                    pathsNotPassAP.Add(p);
                }
            }
            #endregion
            #region 查找步数最短的路径
            int minPathStepCount = int.MaxValue;
            List<Path> minPaths = new List<Path>();
            if (pathsNotPassAP.Count > 0)
            {
                foreach (Path p in pathsNotPassAP)
                {
                    if (p.pathVertexIdList.Count < minPathStepCount)
                    {
                        minPathStepCount = p.pathVertexIdList.Count;
                        minPaths.Clear();
                        minPaths.Add(p);
                    }
                    else if (p.pathVertexIdList.Count == minPathStepCount)
                    {
                        minPaths.Add(p);
                    }
                }
            }
            else
            {
                foreach (Path p in allPaths)
                {
                    if (p.pathVertexIdList.Count < minPathStepCount)
                    {
                        minPathStepCount = p.pathVertexIdList.Count;
                        minPaths.Clear();
                        minPaths.Add(p);
                    }
                    else if (p.pathVertexIdList.Count == minPathStepCount)
                    {
                        minPaths.Add(p);
                    }
                }
            }
            #endregion
            #region 返回ID列表
            if (minPaths.Count == 0)
            {
                return vertexIdList;
            }
            else
            {
                Path p = minPaths.First();
                for (int i =0; i < p.pathVertexIdList.Count; i++)
                {
                    vertexIdList.Add(p.pathVertexIdList[i]);
                }
            }
            #endregion
            return vertexIdList;
        }
        public List<Path> GetAllPath(int staVertexId, int endVertexId)
        {
            List<Path> candidatePaths = new List<Path>();
            List<Path> resultPaths = new List<Path>();

            Path rootPath = new Path();
            rootPath.pathVertexIdList.Add(staVertexId);
            candidatePaths.Add(rootPath);
            while (candidatePaths.Count != 0)
            {
                List<Path> addList = new List<Path>();
                List<Path> removeList = new List<Path>();
                foreach (Path p in candidatePaths)
                {
                    if (p.pathVertexIdList.Last() == endVertexId)
                    {
                        resultPaths.Add(p);
                    }
                    else
                    {
                        int vertexId = p.pathVertexIdList.Last();
                        foreach (int neighborVertexId in VertexDic[vertexId].neighborVertexIdList)
                        {
                            if (!p.pathVertexIdList.Contains(neighborVertexId))
                            {
                                Path newP = new Path();
                                newP.pathVertexIdList = new List<int>();
                                foreach (int id in p.pathVertexIdList)
                                {
                                    newP.pathVertexIdList.Add(id);
                                }
                                newP.pathVertexIdList.Add(neighborVertexId);
                                addList.Add(newP);
                            }
                        }
                    }
                    removeList.Add(p);
                }
                foreach (Path p in addList)
                {
                    candidatePaths.Add(p);
                }
                foreach (Path p in removeList)
                {
                    candidatePaths.Remove(p);
                }
            }
            //计算路径长度
            foreach (Path p in resultPaths)
            {
                int sum = 0;
                PointContract lastPos = VertexDic[p.pathVertexIdList.First()].pos;
                for (int i = 0; i < p.pathVertexIdList.Count-1; i++)
                {
                    sum+=GetGeometricaDistance(VertexDic[p.pathVertexIdList[i]].pos,VertexDic[p.pathVertexIdList[i+1]].pos);
                }
                p.pathLength = sum;
            }
            return resultPaths;
        }
        /// <summary>
        /// 清理广度优先搜索信息
        /// </summary>
        private void ClearBFSInfo()
        {
            try
            {
                foreach (BFSInfo b in VertexIdBFSInfoDic.Values)
                {
                    b.visited = false;
                    b.preVertexId = 0;
                    b.layer = 0;
                }
            }
            catch (Exception ex)
            {
                Log.AddLogEntry(LogEntryLevel.ERROR, "VertexSet.ClearBFSInfo()执行异常：" + ex.Message + "\t异常现场：" + "类变量：" + this.ToString());
            }
        }

        /// <summary>
        /// 根据vertexId获取vertex坐标
        /// </summary>
        /// <param name="vertexId"></param>
        /// <returns></returns>
        public PointContract GetVertexPosition(int vertexId)
        {
            try
            {
                if (vertexId == -1)
                {
                    PointContract pos = new PointContract();
                    pos.MapId = -1;
                    pos.Type = CoordinateType.RCTANGULAR;
                    pos.X = -1;
                    pos.Y = -1;
                    pos.Z = -1;
                    return pos;
                }
                else
                {
                    PointContract pos = VertexDic[vertexId].pos.Clone();
                    return pos;
                }
            }
            catch (Exception ex)
            {
                string parametersStr = "";
                parametersStr += "vertexId:" + vertexId.ToString();
                Log.AddLogEntry(LogEntryLevel.ERROR, "VertexSet.GetVertexPosition()执行异常：" + ex.Message + "\t异常现场：" + "类变量：" + this.ToString() + "\t参数：" + parametersStr);
                return null;
            }
        }

        public GeometricaContract GetGeometricaCenter(List<int> vertexIdList, List<int> closestVertexIdList, PointContract lastLocPos)
        {
            GeometricaContract returnValue = new GeometricaContract();
            if (vertexIdList.Count == 0)
            {
                returnValue.pos = lastLocPos;
                returnValue.closestVertexIdList = closestVertexIdList;
                return returnValue;
            }
            else if (vertexIdList.Count == 1)
            {
                if (this.VertexDic.ContainsKey(vertexIdList[0]))
                {
                    returnValue.pos = this.VertexDic[vertexIdList[0]].pos.Clone();
                    returnValue.closestVertexIdList.Add(vertexIdList[0]);
                    return returnValue;
                }
                else
                {
                    returnValue.pos = lastLocPos;
                    returnValue.closestVertexIdList = closestVertexIdList;
                    return returnValue;
                }
            }
            else if (vertexIdList.Count == 2)
            {
                if (this.VertexDic.ContainsKey(vertexIdList[0]) && this.VertexDic.ContainsKey(vertexIdList[1]))
                {
                    #region 判断是否在一个地图上
                    PointContract vertexA = VertexDic[vertexIdList[0]].pos;
                    PointContract vertexB = VertexDic[vertexIdList[1]].pos;
                    if (vertexA.MapId != vertexB.MapId)
                    {
                        #region 如果不是则选取与上次定位结果同一个地图的点
                        if (vertexA.MapId == lastLocPos.MapId)
                        {
                            returnValue.pos = vertexA.Clone();
                            returnValue.closestVertexIdList.Add(vertexIdList[0]);
                            return returnValue;
                        }
                        else if (vertexB.MapId == lastLocPos.MapId)
                        {
                            returnValue.pos = vertexB.Clone();
                            returnValue.closestVertexIdList.Add(vertexIdList[1]);
                            return returnValue;
                        }
                        else
                        {
                            returnValue.pos = lastLocPos;
                            returnValue.closestVertexIdList = closestVertexIdList;
                            return returnValue;
                        }
                        #endregion
                    }
                    else
                    {
                        #region 查找所有路径
                        List<Path> allPaths = QuickPathTable[vertexIdList[0]].vertexIdPathDic[vertexIdList[1]];
                        #endregion
                        #region 查找不经过AP的路径
                        List<Path> pathsNotPassAP = new List<Path>();
                        foreach (Path p in allPaths)
                        {
                            bool passAP = false;
                            for (int i = 1; i < p.pathVertexIdList.Count - 1; i++)
                            {
                                int vertexId = p.pathVertexIdList[i];
                                if (VertexDic[vertexId].type == VertexType.APPOINT)
                                {
                                    passAP = true;
                                    break;
                                }
                            }
                            if (passAP == false)
                            {
                                pathsNotPassAP.Add(p);
                            }
                        }
                        #endregion
                        if (allPaths.Count == 0 || pathsNotPassAP.Count == 0)
                        {
                            #region 没有路径或者没有不经过AP的路径
                            PointContract c = new PointContract();
                            c.MapId = vertexA.MapId;
                            c.Type = vertexA.Type;
                            c.X = (vertexA.X + vertexB.X) / 2;
                            c.Y = (vertexA.Y + vertexB.Y) / 2;
                            c.Z = (vertexA.Z + vertexB.Z) / 2;
                            returnValue.pos = c;
                            return returnValue;
                            #endregion
                        }
                        else
                        {
                            #region 有直连路径
                            PointContract ZCXPointA = new PointContract();
                            PointContract ZCXPointB = new PointContract();
                            #region 计算中垂线
                            ZCXPointA.MapId = vertexA.MapId;
                            ZCXPointA.Type = vertexA.Type;
                            ZCXPointA.X = (vertexA.X + vertexB.X) / 2;
                            ZCXPointA.Y = (vertexA.Y + vertexB.Y) / 2;

                            ZCXPointB.MapId = vertexA.MapId;
                            ZCXPointB.Type = vertexA.Type;
                            if (vertexA.X != vertexB.X)
                            {
                                ZCXPointB.X = (int)(System.Math.Pow(vertexA.X, 2) - System.Math.Pow(vertexB.X, 2) + System.Math.Pow(vertexA.Y, 2) - System.Math.Pow(vertexB.Y,2)) / (2 * (vertexA.X - vertexB.X));
                                ZCXPointB.Y = 0;
                            }
                            else
                            {
                                ZCXPointB.X = 0;
                                ZCXPointB.Y = (int)(System.Math.Pow(vertexA.X, 2) - System.Math.Pow(vertexB.X, 2) + System.Math.Pow(vertexA.Y, 2) - System.Math.Pow(vertexB.Y,2)) / (2 * (vertexA.Y - vertexB.Y));
                            }
                            #endregion
                            #region 查找最近映射点
                            double minDistance = double.MaxValue;
                            PointContract c = null;
                            List<int> closestVertexIdList_c = new List<int>() ;
                            foreach (Path p in pathsNotPassAP)
                            {
                                int id_P = -1;
                                int id_Q = -1;
                                foreach (int vertexId in p.pathVertexIdList)
                                {
                                    if (id_P != -1)
                                    {
                                        id_Q = vertexId;
                                        if (CrossProduct(Sub(VertexDic[id_P].pos, ZCXPointB), Sub(ZCXPointA, ZCXPointB)) * CrossProduct(Sub(ZCXPointA, ZCXPointB), Sub(VertexDic[id_Q].pos, ZCXPointB)) >= 0)
                                        {
                                            PointContract pointP = VertexDic[id_P].pos;
                                            PointContract pointQ = VertexDic[id_Q].pos;
                                            int S11 = (ZCXPointA.Y - ZCXPointB.Y);
                                            int S12 = -(ZCXPointA.X - ZCXPointB.X);
                                            int S21 = (pointP.Y - pointQ.Y);
                                            int S22 = -(pointP.X - pointQ.X);
                                            int S_Determinant = (S11 * S22) - (S12 * S21);
                                            int R1 = (ZCXPointA.Y - ZCXPointB.Y) * ZCXPointA.X - (ZCXPointA.X - ZCXPointB.X) * ZCXPointA.Y;
                                            int R2 = (pointP.Y - pointQ.Y) * pointP.X - (pointP.X - pointQ.X) * pointP.Y;
                                            double x = ((double)S22 / S_Determinant) * (double)R1 - ((double)S12 / S_Determinant) * (double)R2;
                                            double y = ((double)S11 / S_Determinant) * (double)R2 - ((double)S21 / S_Determinant) * (double)R1;
                                            double distance = (System.Math.Pow(x - ZCXPointA.X,2) + System.Math.Pow(y - ZCXPointA.Y,2));
                                            if (distance < minDistance)
                                            {
                                                minDistance = distance;
                                                c = new PointContract();
                                                c.MapId = pointP.MapId;
                                                c.X = (int)x;
                                                c.Y = (int)y;
                                                closestVertexIdList_c.Clear();
                                                closestVertexIdList_c.Add(id_P);
                                                closestVertexIdList_c.Add(id_Q);
                                            }
                                        }
                                    }
                                    id_P = vertexId;
                                }
                            }
                            #endregion
                            if (c == null)
                            {
                                c = new PointContract();
                                c.MapId = vertexA.MapId;
                                c.Type = vertexA.Type;
                                c.X = (vertexA.X + vertexB.X) / 2;
                                c.Y = (vertexA.Y + vertexB.Y) / 2;
                                returnValue.pos = c;
                                return returnValue;
                            }
                            else
                            {
                                returnValue.pos = c;
                                returnValue.closestVertexIdList = closestVertexIdList_c;
                                return returnValue;
                            }
                            #endregion
                        }
                    }
                    #endregion
                }
                else
                {
                    returnValue.pos = lastLocPos;
                    returnValue.closestVertexIdList = closestVertexIdList;
                    return returnValue;
                }
            }
            else
            {
                PointContract c = new PointContract();
                int mapId = VertexDic[vertexIdList.First()].pos.MapId;
                int xSum = 0;
                int ySum = 0;
                foreach (int vertexId in vertexIdList)
                {
                    if (VertexDic[vertexId].pos.MapId != mapId)
                    {
                        return null;
                    }
                    xSum += VertexDic[vertexId].pos.X;
                    ySum += VertexDic[vertexId].pos.Y;
                }
                c.MapId = mapId;
                c.X = xSum / vertexIdList.Count;
                c.Y = ySum / vertexIdList.Count;
                returnValue.pos = c;
                return returnValue;
            }
        //    PointContract geometricaCenter = new PointContract();
        //    int mapId = VertexDic[vertexIdList.First()].pos.MapId;
        //    int xSum = 0;
        //    int ySum = 0;
        //    foreach (int vertexId in vertexIdList)
        //    {
        //        if (VertexDic[vertexId].pos.MapId != mapId)
        //        {
        //            return null;
        //        }
        //        xSum += VertexDic[vertexId].pos.X;
        //        ySum += VertexDic[vertexId].pos.Y;
        //    }
        //    geometricaCenter.MapId = mapId;
        //    geometricaCenter.X = xSum / vertexIdList.Count;
        //    geometricaCenter.Y = ySum / vertexIdList.Count;
        //    return geometricaCenter;
            return null;
        }

        public PointContract Sub(PointContract p, PointContract q)
        {
            PointContract s = new PointContract();
            s.X = p.X - q.X;
            s.Y = p.Y - q.Y;
            return s;
        }
        public double CrossProduct(PointContract a, PointContract b)
        {
            double result = 0;
            result += a.X * b.Y - a.Y * b.X;
            return result;
        }
        private int GetGeometricaDistance(PointContract a, PointContract b)
        {
            int distance = int.MaxValue;
            if (a != null && b != null)
            {
                distance = (int)System.Math.Sqrt(System.Math.Pow(a.X - b.X, 2) + System.Math.Pow(a.Y - b.Y, 2));
            }
            return distance;
        }
        public override string ToString()
        {
            //将所有内部变量转换成字符串，在异常发生时输出内部变量的当时值
            string str = "";
            str += "EnvironmentId:" + EnvironmentId.ToString();
            str += ",VertexDic:";
            if (VertexDic == null)
            {
                str += "null";
            }
            else
            {
                str += "{";
                foreach (int vertexId in VertexDic.Keys)
                {
                    str += "(";
                    str += vertexId.ToString() + ",";
                    str += "<";
                    str += VertexDic[vertexId].pos.MapId.ToString();
                    str += "," + VertexDic[vertexId].pos.X.ToString();
                    str += "," + VertexDic[vertexId].pos.Y.ToString();
                    str += ",";
                    if (VertexDic[vertexId].neighborVertexIdList == null)
                    {
                        str += "null";
                    }
                    else
                    {
                        str += "[";
                        foreach (int id in VertexDic[vertexId].neighborVertexIdList)
                        {
                            str += id.ToString() + ",";
                        }
                        str += "]";
                    }
                    str += ">";
                    str += ")";
                }
                str += "}";
            }

            str += ",VertexIdBFSInfoDic:";
            if (VertexIdBFSInfoDic == null)
            {
                str += "null";
            }
            else
            {
                str += "{";
                foreach (int vertexId in VertexIdBFSInfoDic.Keys)
                {
                    str += "(";
                    str += vertexId.ToString() + ",[";
                    str+=VertexIdBFSInfoDic[vertexId].layer.ToString();
                    str+=","+VertexIdBFSInfoDic[vertexId].preVertexId.ToString();
                    str+=","+VertexIdBFSInfoDic[vertexId].visited.ToString()+"]";
                    str += ")";
                }
                str += "}";
            }
            return str;
        }

        #endregion
    }
    //广度优先搜索中每个节点信息
    public class BFSInfo
    {
        public BFSInfo(int id)
        {
            visited = false;
            preVertexId = 0;
            layer = 0;
            vertexId = id;
        }
        public BFSInfo()
        {
            visited = false;
            preVertexId = 0;
            layer = 0;
        }
        public bool visited;
        public int preVertexId;
        public int vertexId;
        public int layer;
    }

    //路径字典类，一个主键为vertexId，值为路径的字典，表示到vertexId的路径几何
    public class PathDictionary
    {
        public Dictionary<int, List<Path>> vertexIdPathDic;
        public PathDictionary()
        {
            vertexIdPathDic = new Dictionary<int, List<Path>>();
        }
    }


    public class Path
    {
        public List<int> pathVertexIdList;
        public int pathLength;
        public Path()
        {
            this.pathVertexIdList = new List<int>();
        }
        public override string ToString()
        {
            string str = "";
            foreach(int vertexId in pathVertexIdList)
            {
                str += vertexId.ToString() + " ";
            }
            return str;
        }
    }
    public class GeometricaContract
    {
        public PointContract pos;
        public List<int> closestVertexIdList;
        public GeometricaContract()
        {
            pos = new PointContract();
            closestVertexIdList = new List<int>();
        }
    }
    public class DFSInfo
    {
        public int vertexId;
        public bool visited;
        public List<int> visitedVertexIdList;
        public DFSInfo()
        {
            this.visited = false;
            this.visitedVertexIdList = new List<int>();
        }
        public DFSInfo(int v)
        {
            this.vertexId = v;
            this.visited = false;
            this.visitedVertexIdList = new List<int>();
        }

        internal bool isAllNeighborVisited(List<int> list)
        {
            foreach (int id in list)
            {
                if (!this.visitedVertexIdList.Contains(id))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
