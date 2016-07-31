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
    public class CellManager
    {
        #region //单件模式实现
        static readonly CellManager instance = new CellManager();
        public static CellManager Instance
        {
            get
            {
                return instance;
            }
        }
        static CellManager()
        {
        }
        CellManager()
        {
        }
        #endregion

        #region 属性
        public Dictionary<int, CellSet> CellSetsDic;
        #endregion

        #region 方法
        /// <summary>
        /// CellManager的初始化依赖Vertex数据，必须在VertexManager初始化之后进行！！
        /// </summary>
        public void Init()
        {
            MySqlDAO dao = new MySqlDAO();
            List<int> EnvironmentsIdList = DataBase.GetEnvironmentsIdList(dao);
            dao.Close();
            CellSetsDic = new Dictionary<int, CellSet>();
            foreach (int environmentId in EnvironmentsIdList)
            {
                if (!CellSetsDic.ContainsKey(environmentId))
                {
                    CellSetsDic.Add(environmentId, new CellSet(environmentId));
                }
            }
            Log.AddLogEntry(LogEntryLevel.INFO, "CellManager初始化完成");
        }
        #endregion
    }

    public class CellSet
    {
        #region 属性
        public int environmentId;                       //当前CellSet所属环境Id
        public Dictionary<int, CellInfo> cellInfoDic;   //存储当前环境内所有Cell的信息
        #endregion

        #region 构造器
        public CellSet(int e)
        {
            this.environmentId = e;
            this.cellInfoDic = new Dictionary<int, CellInfo>();
            //┏━━━━━初始化CellInfoDic
            MySqlDAO dao = new MySqlDAO();
            List<int> cellIdList = DataBase.GetCellIdList(e, dao);
            dao.Close();
            //CellInfo初始化
            foreach (int cellId in cellIdList)
            {
                if (!cellInfoDic.ContainsKey(cellId))
                {
                    cellInfoDic.Add(cellId, new CellInfo(cellId));
                }
            }
            //┗━━━━━
        }
        #endregion

        #region 方法
        public int GetCellId(PointContract p)
        {
            int cellId = -1;
            if (p != null)
            {
                foreach (CellInfo cell in cellInfoDic.Values)
                {
                    if (cell.IsInCell(p))
                        return cell.cellId;
                }
            }
            return cellId;
        }
        #endregion
    }

    public class CellInfo
    {
        #region 属性
        public int cellId;
        public List<PointContract> cellDotsPointList;
        public Dictionary<int, List<Path>> cellPathDic;
        public List<int> entranceVertexIdList;
        #endregion 

        #region 构造器
        public CellInfo(int c_id)
        {
            this.cellId = c_id;
            MySqlDAO dao = new MySqlDAO();
            this.cellDotsPointList = DataBase.GetCellDotsList(c_id, dao);
            dao.Close();
            this.cellPathDic = new Dictionary<int, List<Path>>();
            this.entranceVertexIdList = new List<int>();
        }
        #endregion 

        #region 方法
        public bool IsInCell(PointContract p)
        {
            //p为待判断的点，vertexList为顶点列表，第一个顶点和最后一个顶点要求重合,下面操作将第一个顶点复制至末尾
            PointContract v = new PointContract();
            v.MapId = cellDotsPointList[0].MapId;
            v.X = cellDotsPointList[0].X;
            v.Y = cellDotsPointList[0].Y;
            cellDotsPointList.Add(v);
            //交点个数
            int CrossCount = 0;
            for (int i = 0; i < cellDotsPointList.Count - 1; i++)
            {
                PointContract p1 = cellDotsPointList[i];
                PointContract p2 = cellDotsPointList[i + 1];
                //若p1p2连线与X轴平行则忽略
                if (p1.Y == p2.Y)
                    continue;
                if (p.Y > Math.Max(p1.Y,p2.Y) || p.Y <= Math.Min(p1.Y,p2.Y))//小于等于是为了避免线段交点处的重复计数
                    continue;
                double x = (p.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y) + p1.X;//计算从P点做平行于X轴的直线与p1p2线段的交点
                if (p.X >= x)
                    CrossCount++;
            }
            cellDotsPointList.RemoveAt(cellDotsPointList.Count - 1);
            if (CrossCount % 2 == 1)
                return true;
            return false;
        }
        #endregion
    }
}
