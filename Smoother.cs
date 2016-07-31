using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InnerClasses;
using LogManager;
namespace LocAlgorithm
{
    public class LCASSmoother
    {
        public Dictionary<int, LCASSmoothInfo> LCASSmoothInfoDic;
        //public int lastLocVertexId;
        public int environmentId;
        public LCASSmoother(int e)
        {
            environmentId = e;
            LCASSmoothInfoDic = new Dictionary<int, LCASSmoothInfo>();
            //lastLocVertexId = -1;
        }
        public int Refresh(int lastLocVertexId, int currentCalcuVertexId, bool debugEnable)
        {
            if (lastLocVertexId == -1)
            {
                if (!LCASSmoothInfoDic.ContainsKey(currentCalcuVertexId))
                {
                    LCASSmoothInfoDic.Add(currentCalcuVertexId, new LCASSmoothInfo(currentCalcuVertexId));
                }
                LCASSmoothInfoDic[currentCalcuVertexId].PassBy();
                if (debugEnable == true)
                {
                    string str = "";
                    foreach (int vertexId in LCASSmoothInfoDic.Keys)
                    {
                        str += VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[vertexId].name;
                        str += "(";
                        str += LCASSmoothInfoDic[vertexId].ToString();
                        str += ")";
                        str += "   ";
                    }
                    debuglog.WriteLine(str);
                }
                return currentCalcuVertexId;
            }
            else
            {
                foreach (LCASSmoothInfo i in LCASSmoothInfoDic.Values)
                {
                    i.Move();
                }
                List<int> list1 = new List<int>();
                list1.Add(lastLocVertexId);
                List<int> list2 = new List<int>();
                list2.Add(currentCalcuVertexId);
                List<int> passByVertexIdList = VertexManager.Instance.VertexSetsDic[environmentId].GetPathVertexIdList(list1, list2);
                if (passByVertexIdList.Count != 0)
                {
                    foreach (int vertexId in passByVertexIdList)
                    {
                        if (!LCASSmoothInfoDic.ContainsKey(vertexId))
                        {
                            LCASSmoothInfoDic.Add(vertexId, new LCASSmoothInfo(vertexId));
                        }
                        LCASSmoothInfoDic[vertexId].PassBy();
                    }
                }
                else
                {
                    if (!LCASSmoothInfoDic.ContainsKey(currentCalcuVertexId))
                    {
                        LCASSmoothInfoDic.Add(currentCalcuVertexId, new LCASSmoothInfo(currentCalcuVertexId));
                    }
                    LCASSmoothInfoDic[currentCalcuVertexId].PassBy();
                }
            }
            int pathMaxLen = int.MinValue;
            int locVertexId = -1;
            foreach (LCASSmoothInfo i in LCASSmoothInfoDic.Values)
            {
                if (i.IsAvalible() == true)
                {
                    List<int> list1 = new List<int>();
                    List<int> list2 = new List<int>();
                    list1.Add(lastLocVertexId);
                    list2.Add(i.vertexId);
                    int len = VertexManager.Instance.VertexSetsDic[environmentId].GetPathVertexIdList(list1, list2).Count;
                    if (len > pathMaxLen)
                    {
                        pathMaxLen = len;
                        locVertexId = i.vertexId;
                    }
                    
                }
            }
            if (locVertexId != -1)
            {
                if (debugEnable == true)
                {
                    string str = "";
                    foreach (int vertexId in LCASSmoothInfoDic.Keys)
                    {
                        str += VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[vertexId].name;
                        str += "(";
                        str += LCASSmoothInfoDic[vertexId].ToString();
                        str += ")";
                        str += "   ";
                    }
                    debuglog.WriteLine(str);
                }
                if (lastLocVertexId != locVertexId)
                {
                    lastLocVertexId = locVertexId;
                    ClearSmoothWindow();
                }
                return locVertexId;
            }
            else
            {
                if (debugEnable == true)
                {
                    string str = "";
                    foreach (int vertexId in LCASSmoothInfoDic.Keys)
                    {
                        str += VertexManager.Instance.VertexSetsDic[environmentId].VertexDic[vertexId].name;
                        str += "(";
                        str += LCASSmoothInfoDic[vertexId].ToString();
                        str += ")";
                        str += "   ";
                    }
                    debuglog.WriteLine(str);
                }
                return lastLocVertexId;
            }
        }

        private void ClearSmoothWindow()
        {
            foreach (LCASSmoothInfo s in LCASSmoothInfoDic.Values)
            {
                for (int i = 0; i < Parameters.LCAS_SMOOTH_WINDOW_LEN; i++)
                {
                    s.smoothWindow[i] = 0;
                }
            }
        }
    }
    public class LCASSmoothInfo
    {
        public int vertexId;
        public byte[] smoothWindow;
        private int ptr;
        public LCASSmoothInfo(int id)
        {
            this.vertexId = id;
            this.smoothWindow = new byte[Parameters.LCAS_SMOOTH_WINDOW_LEN];
            this.ptr = 0;
        }
        public void Move()
        {
            for (int i = 1; i < Parameters.LCAS_SMOOTH_WINDOW_LEN; i++)
            {
                this.smoothWindow[i - 1] = this.smoothWindow[i];
            }
            this.smoothWindow[Parameters.LCAS_SMOOTH_WINDOW_LEN-1] = 0;
        }
        public void PassBy()
        {
            this.smoothWindow[Parameters.LCAS_SMOOTH_WINDOW_LEN - 1] = 1;
        }
        public bool IsAvalible()
        {
            foreach (byte b in this.smoothWindow)
            {
                if (b == 0)
                {
                    return false;
                }
            }
            return true;
        }
        public override string ToString()
        {
            string str = "";
            foreach (byte b in this.smoothWindow)
            {
                str += b.ToString();
                str += " ";
            }
            return str;
        }
    }
}
