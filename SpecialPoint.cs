using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LocAlgorithm
{
    public static class SpecialPoint
    {
        public static LocContract.PointContract OFFLINE_POS = new LocContract.PointContract
        {
            MapId = -1,
            X = -1,
            Y = -1,
            Z = -1
        };
        public static LocContract.PointContract CANTLOC_POS = new LocContract.PointContract
        {
            MapId = 0,
            X = 0,
            Y = 0,
            Z = 0
        };
    }
}
